using System.Diagnostics;
using System.Text;
using UnityEditor;
using UnityEngine;
using Evosim.Core;
using Debug = UnityEngine.Debug;

namespace Evosim.Sim.EditorTools
{
    /// <summary>
    /// The first run in which swimming costs something — DESIGN.md §10 M4.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What this exists to find out.</b> Every §5A.2b number was measured with
    /// <c>workJoules: 0</c> and a height fixed at birth, so the population it described could not
    /// move and was not billed for trying. Joining the halves makes both real, and the question is
    /// whether the economy survives contact with the physics.
    /// </para>
    /// <para>
    /// <b>The specific risk it is watching for.</b> The Milestone 1 smoke test measured 55–91% of a
    /// creature's expenditure being destroyed by its own joint limits rather than delivered to the
    /// water, because an open-loop sine has no way to decelerate before a stop (that needs the
    /// brain graph, Milestone 6). Now that work is billed, the cheapest escape from that cost is to
    /// stop moving — and <c>Power</c> is an evolved trait, so selection can take it. A world that
    /// converges on motionless photosynthetic mats is a defensible ecosystem and a terrible thing
    /// to watch, which is the priority order this project committed to (§0d).
    /// </para>
    /// <para>
    /// So the two columns that matter are <b>mean speed</b> and <b>work as a share of
    /// expenditure</b>. If both fall towards zero while the population holds, that is the collapse,
    /// and it is visible on the first run rather than after a week of them.
    /// </para>
    /// </remarks>
    public static class EmbodiedRun
    {
        private const int MetabolicSteps = 400;    // 400 x 0.5 s = 200 simulated seconds
        private const int ReportEvery = 40;

        [MenuItem("Evosim/Run — embodied ecosystem")]
        public static void RunFromMenu() => Run();

        public static void Run()
        {
            SimulationMode previousMode = Physics.simulationMode;
            Vector3 previousGravity = Physics.gravity;

            Physics.simulationMode = SimulationMode.Script;
            FluidEnvironment.ConfigureScene(selfCollision: true);

            var config = new RunConfig();
            var eco = new Ecosystem(config, seed: 1);

            var report = new StringBuilder();
            report.AppendLine("=== Embodied ecosystem run ===");
            report.AppendLine(
                $"Unity {Application.unityVersion}   dt={Ecosystem.FixedDt}   " +
                $"metabolic step {Ecosystem.StepsPerMetabolicStep * Ecosystem.FixedDt} s   " +
                $"configHash {config.Hash()}");
            report.AppendLine();
            report.AppendLine(
                "| t (s) | alive | births | deaths | mean speed m/s | work J/step | " +
                "work share of spend | with joints | mean DOF | mean depth m | audit |");
            report.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");

            var clock = Stopwatch.StartNew();
            int metabolicSteps = 0;

            while (metabolicSteps < MetabolicSteps)
            {
                if (eco.Step()) metabolicSteps++;
                else continue;

                if (metabolicSteps % ReportEvery != 0) continue;

                World world = eco.World;

                double spend = 0d, workSpend = 0d, depth = 0d;
                int jointed = 0, dof = 0;

                for (int i = 0; i < world.Living.Count; i++)
                {
                    Organism creature = world.Living[i];
                    spend += creature.Lifetime.Expenditure;
                    workSpend += creature.Lifetime.Work;
                    depth += creature.HeightY;

                    // Turns "work fell to zero" from a hypothesis into a measurement. Zero work
                    // has two very different causes: creatures that still have joints and have
                    // evolved not to use them, or a population with no joints left at all. The
                    // first is behaviour and the second is anatomy, and they need different fixes.
                    int creatureDof = ActuatedDof(creature.Phenotype);
                    if (creatureDof > 0) jointed++;
                    dof += creatureDof;
                }

                int alive = world.Living.Count;
                double workShare = spend > 0d ? workSpend / spend : 0d;

                report.AppendLine(
                    $"| {world.ElapsedSeconds:0.#} | {alive} | {world.Births} | {world.Deaths} | " +
                    $"{eco.MeanSpeed:0.####} | {eco.WorkThisStep:0.##} | {workShare:P1} | " +
                    $"{(alive > 0 ? 100d * jointed / alive : 0d):0.#}% | " +
                    $"{(alive > 0 ? (double)dof / alive : 0d):0.##} | " +
                    $"{(alive > 0 ? depth / alive : 0d):0.##} | " +
                    $"{Residual(world):0.0000}% |");
            }

            clock.Stop();

            report.AppendLine();
            report.AppendLine(
                $"{eco.Steps} physics steps, {eco.World.ElapsedSeconds:0.#} simulated seconds in " +
                $"{clock.Elapsed.TotalSeconds:0.#} s wall clock " +
                $"({eco.World.ElapsedSeconds / clock.Elapsed.TotalSeconds:0.##}x real time).");

            report.AppendLine();
            report.AppendLine(
                "**Mean speed and work share are the columns that matter.** Both falling towards " +
                "zero while the population holds is §5A.7's photosynthetic mat arriving through " +
                "the actuator rather than through the light budget: the cheapest way to stop " +
                "paying for bang-bang actuation is to stop actuating.");

            report.AppendLine();
            report.AppendLine(
                "The audit column is §5A.2's hard equality — `EnergyIn - EnergyOut - Standing`, " +
                "as a share of what has entered. It must stay at zero with the work term live, " +
                "because mechanical work is an expenditure like any other. If it drifts, the " +
                "physics is delivering energy the economy did not account for, which is §11.2.");

            Debug.Log(report.ToString());

            eco.DestroyAll();
            Physics.simulationMode = previousMode;
            Physics.gravity = previousGravity;
        }

        /// <summary>Actuated degrees of freedom in a developed body.</summary>
        private static int ActuatedDof(Phenotype phenotype)
        {
            int dof = 0;
            foreach (PhenotypePart part in phenotype.Parts) dof += part.JointType.DofCount();
            return dof;
        }

        /// <summary>Audit residual as a share of energy in — §5A.2.</summary>
        private static double Residual(World world) =>
            world.EnergyIn > 0d ? 100d * world.AuditResidual / world.EnergyIn : 0d;
    }
}
