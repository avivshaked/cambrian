using System.Text;
using UnityEditor;
using UnityEngine;
using Evosim.Core;

namespace Evosim.Sim.EditorTools
{
    /// <summary>
    /// Headless check that a genome becomes a moving articulation: develop, build, actuate,
    /// step, tear down. Run in batchmode via
    /// <c>-executeMethod Evosim.Sim.EditorTools.Milestone1Smoke.Run</c>.
    /// </summary>
    /// <remarks>
    /// The unit tests in Evosim.Core.Tests prove development is right. They cannot prove the
    /// articulation mapping is, because that needs PhysX. This is the smallest thing that
    /// exercises the whole chain without opening the Editor.
    ///
    /// <b>It asserts the creatures are awake.</b> Spike 01's first run reported numbers two
    /// orders of magnitude too good because zero gravity plus no actuation let PhysX sleep
    /// the entire scene, and every timing measured an idle solver. Any physics check here
    /// reports mean speed for that reason — see logbook/0002.
    /// </remarks>
    public static class Milestone1Smoke
    {
        private const int Creatures = 12;
        private const int WarmupSteps = 50;
        private const int MeasureSteps = 400;
        private const float FixedDt = 0.01f;
        private const float TestSineHz = 0.8f;

        /// <summary>Minimum mean speed, m/s, below which the creatures are assumed asleep.</summary>
        private const float AwakeThreshold = 0.001f;

        /// <summary>Metres of disagreement tolerated between the phenotype and what was built.</summary>
        private const float GeometryTolerance = 1e-3f;

        [MenuItem("Evosim/Milestone 1 — Smoke Test")]
        public static void RunFromMenu() => Execute();

        public static void Run()
        {
            bool ok = Execute();
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool Execute()
        {
            SimulationMode previousMode = Physics.simulationMode;
            Vector3 previousGravity = Physics.gravity;

            Physics.simulationMode = SimulationMode.Script;
            Physics.gravity = Vector3.zero;
            Physics.IgnoreLayerCollision(
                PhenotypeBuilder.CreatureLayer, PhenotypeBuilder.CreatureLayer, true);

            var report = new StringBuilder();
            report.AppendLine("=== Milestone 1 smoke test ===");
            report.AppendLine($"Unity {Application.unityVersion}   dt={FixedDt}   {MeasureSteps} steps");
            report.AppendLine();
            report.AppendLine("| seed | parts | depth | DOF | volume m3 | mean speed m/s | finite |");
            report.AppendLine("|---|---|---|---|---|---|---|");

            bool allOk = true;
            int spawned = 0;

            for (ulong seed = 1; seed <= Creatures; seed++)
            {
                var limits = DevelopmentLimits.Default;
                var rng = new Rng(seed);

                Genome genome = GenomeFactory.RandomViable(rng, RandomGenomeOptions.Default, limits, minParts: 3);
                Phenotype phenotype = Developer.Develop(genome, limits);

                CreatureInstance creature = PhenotypeBuilder.Build(phenotype, Vector3.zero);

                // Verify the built articulation matches what development asked for, BEFORE
                // stepping physics moves everything. Parts are parented to each other, so the
                // builder has to undo Unity's compounding of parent scale into children — the
                // most error-prone code in the chain, and the unit tests cannot reach it
                // because it is Unity-side.
                float worstPos = 0f, worstSize = 0f;
                for (int b = 0; b < creature.Bodies.Length; b++)
                {
                    PhenotypePart expected = phenotype.Parts[b];
                    Transform actual = creature.Bodies[b].transform;

                    worstPos = Mathf.Max(worstPos,
                        Vector3.Distance(actual.position, expected.Position.ToVector3()));

                    // Size lives on the collider, not the transform — see PhenotypeBuilder.
                    // Checking lossyScale here would pass trivially now that every part
                    // transform is unit scale, so check the thing physics actually uses.
                    Vector3 expectedSize = (expected.HalfExtents * 2f).ToVector3();
                    Vector3 actualSize = creature.Bodies[b].GetComponent<BoxCollider>().size;
                    worstSize = Mathf.Max(worstSize, Mathf.Max(
                        Mathf.Abs(actualSize.x - expectedSize.x),
                        Mathf.Max(
                            Mathf.Abs(actualSize.y - expectedSize.y),
                            Mathf.Abs(actualSize.z - expectedSize.z))));
                }

                if (worstPos > GeometryTolerance || worstSize > GeometryTolerance)
                {
                    Debug.LogError(
                        $"[Evosim] seed {seed}: built geometry does not match the phenotype — " +
                        $"worst position error {worstPos:0.#####} m, worst size error {worstSize:0.#####} m.");
                    allOk = false;
                }

                var driver = new EffectorDriver(creature);
                var scratch = new float[Mathf.Max(1, creature.TotalDof)];

                float t = 0f;
                for (int s = 0; s < WarmupSteps; s++)
                {
                    driver.DriveTestSine(t, TestSineHz, scratch);
                    Physics.Simulate(FixedDt);
                    t += FixedDt;
                }

                double speedSum = 0;
                int samples = 0;
                bool finite = true;

                for (int s = 0; s < MeasureSteps; s++)
                {
                    driver.DriveTestSine(t, TestSineHz, scratch);
                    Physics.Simulate(FixedDt);
                    t += FixedDt;

                    for (int b = 0; b < creature.Bodies.Length; b++)
                    {
                        Vector3 v = creature.Bodies[b].linearVelocity;
                        if (float.IsNaN(v.x) || float.IsInfinity(v.x) ||
                            float.IsNaN(v.y) || float.IsInfinity(v.y) ||
                            float.IsNaN(v.z) || float.IsInfinity(v.z))
                        {
                            finite = false;
                            continue;
                        }
                        speedSum += v.magnitude;
                        samples++;
                    }
                }

                float meanSpeed = samples > 0 ? (float)(speedSum / samples) : 0f;
                bool awake = meanSpeed > AwakeThreshold || creature.TotalDof == 0;

                report.AppendLine(
                    $"| {seed} | {phenotype.PartCount} | {phenotype.MaxDepthReached} | " +
                    $"{phenotype.TotalDof} | {phenotype.TotalVolume:0.###} | {meanSpeed:0.####} | " +
                    $"{(finite ? "yes" : "**NO**")} |");

                if (!finite)
                {
                    Debug.LogError($"[Evosim] seed {seed}: non-finite velocity — the articulation blew up.");
                    allOk = false;
                }

                if (!awake)
                {
                    Debug.LogError(
                        $"[Evosim] seed {seed}: mean speed {meanSpeed:0.######} m/s with " +
                        $"{creature.TotalDof} actuated DOF — bodies are asleep or the drive is not reaching them.");
                    allOk = false;
                }

                if (phenotype.PartCount < 1)
                {
                    Debug.LogError($"[Evosim] seed {seed}: developed to nothing.");
                    allOk = false;
                }

                spawned++;
                creature.Destroy();
            }

            report.AppendLine();
            report.AppendLine(allOk
                ? $"**PASS** — {spawned} creatures built, geometry verified, actuated and torn down."
                : "**FAIL** — see errors above.");
            report.AppendLine();
            report.AppendLine(
                "Mean speed is an awake-check, NOT a measure of swimming. There is no fluid " +
                "drag yet, so nothing resists the drive and the figures are as large as the " +
                "torque happens to be; the effector scale is uncalibrated for the same reason " +
                "(§4.4, Milestone 2). Read this column as 'the drive reaches the joints', " +
                "and nothing more.");

            Debug.Log(report.ToString());

            Physics.simulationMode = previousMode;
            Physics.gravity = previousGravity;

            return allOk;
        }
    }
}
