using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Evosim.Core;
using Evosim.Sim;
using Debug = UnityEngine.Debug;

namespace Evosim.Theatre.EditorTools
{
    /// <summary>
    /// The PhysX half of the solver spike's parity test: round 42's evolved bodies, one at a
    /// time, alone in still water, under their own brains, with the root's place and every joint
    /// angle written every 0.1 s for 60 s.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It writes CSVs the other engine's bench can be laid beside, and nothing else.</b> The
    /// run directory it is pointed at is opened read-only — one <c>config.json</c> and one
    /// snapshot — and everything it produces goes to the output directory it is given. There is
    /// no economy, no field, no ledger, no glass and no neighbour: the only question is what the
    /// integrator does with a body.
    /// </para>
    /// <para>
    /// <b>Why this is not <see cref="SoloCreature"/>.</b> Mode A is the same wiring and would
    /// have done most of the job, but three of the parity conditions are inside its
    /// <c>Step</c>: the water has to be still rather than the run's current, and
    /// <see cref="SensorChannel.Chemical"/> and <see cref="SensorChannel.Energy"/> have to read
    /// the spike's constant rather than a field and an account. The second needs an
    /// <see cref="ISensorField"/> the brain is handed, and Mode A hands it its own. So the
    /// forty lines of <c>Ecosystem.Build</c>'s wiring are duplicated here as they are duplicated
    /// there — and the step below is <c>Ecosystem.Step</c>'s order, term for term, because a
    /// parity run that reordered perception and drag would be measuring the reordering.
    /// </para>
    /// <para>
    /// <b>Edit mode, one physics step at a time.</b> <c>Physics.simulationMode</c> is
    /// <c>Script</c> and <c>Physics.Simulate</c> is called by the loop, which is what
    /// <see cref="TheatreSoloCheck"/> does headlessly and what the farm does in Play mode. One
    /// body cannot share a solver island with anything, so the job worker count cannot change
    /// what happens here; it is set to zero anyway, the way D078 sets it everywhere else, so
    /// that a setting is never right by accident.
    /// </para>
    /// <code>
    /// $env:EVOSIM_PARITY_SNAPSHOT = 'D:\...\runs\r42-s4\&lt;run&gt;\snapshots\000020000.jsonl'
    /// $env:EVOSIM_PARITY_CONFIG   = 'D:\...\runs\r42-s4\&lt;run&gt;\config.json'
    /// $env:EVOSIM_PARITY_OUT      = 'D:\...\scratch\solver-spike\traj-physx'
    /// -batchmode -nographics -executeMethod Evosim.Theatre.EditorTools.ParitySwim.Run
    /// </code>
    /// </remarks>
    public static class ParitySwim
    {
        /// <summary>
        /// Genomes read from the snapshot before the reader stops — the bench's
        /// <c>--genomes</c> default, which is what fixes the numbering in the file names.
        /// </summary>
        /// <remarks>
        /// It binds nothing at round 42 (the snapshot holds 747 rows), and it is stated here
        /// rather than left implicit because a file called <c>genome04</c> means "the fifth body
        /// this rule admitted" and the rule has to be the same rule on both sides.
        /// </remarks>
        private const int GenomeLimit = 1000;

        /// <summary>Seconds between samples — the spike's, and the CSV's row spacing.</summary>
        private const double SampleSeconds = 0.1;

        [MenuItem("Evosim/Theatre — parity swim against the solver spike")]
        public static void FromMenu() => Swim();

        public static void Run()
        {
            bool ok;

            try
            {
                ok = Swim();
            }
            catch (Exception e)
            {
                Debug.LogError("[ParitySwim] " + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
                ok = false;
            }

            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool Swim()
        {
            string snapshotPath = Text("EVOSIM_PARITY_SNAPSHOT");
            string configPath = Text("EVOSIM_PARITY_CONFIG");

            if (string.IsNullOrEmpty(snapshotPath) || !File.Exists(snapshotPath))
            {
                Debug.LogError("[ParitySwim] EVOSIM_PARITY_SNAPSHOT names no file: '" + snapshotPath + "'");
                return false;
            }

            if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
            {
                Debug.LogError("[ParitySwim] EVOSIM_PARITY_CONFIG names no file: '" + configPath + "'");
                return false;
            }

            string outDirectory = Text("EVOSIM_PARITY_OUT");
            if (string.IsNullOrEmpty(outDirectory)) outDirectory = DefaultOutDirectory();

            int wanted = Whole("EVOSIM_PARITY_COUNT", 5);
            double seconds = Number("EVOSIM_PARITY_SECONDS", 60f);

            // 0.01 rather than the config's step, because the bench hardcodes 0.01 and a parity
            // pair has to be stepped at one number. The run's own is printed beside it.
            float dt = Number("EVOSIM_PARITY_DT", 0.01f);
            float depth = Number("EVOSIM_PARITY_DEPTH", 10f);
            float constant = Number("EVOSIM_PARITY_SENSE_CONSTANT", 0.5f);
            bool selfCollision = Flag("EVOSIM_PARITY_SELF_COLLISION", true);
            string tag = Text("EVOSIM_PARITY_TAG");
            if (string.IsNullOrEmpty(tag)) tag = TagFrom(snapshotPath);

            RunConfig config = RunConfigJson.Read(File.ReadAllText(configPath), out string mismatch);

            Directory.CreateDirectory(outDirectory);

            Debug.Log(
                "[ParitySwim] snapshot " + snapshotPath + "\n" +
                "  config    " + configPath + "  hash " + config.Hash() +
                (mismatch != null ? "  (recorded: " + mismatch + ")" : "  (matches the record)") + "\n" +
                "  out       " + outDirectory + "\n" +
                "  tag       " + tag + "\n" +
                "  " + wanted + " genomes, " + Say(seconds) + " s each, dt " + Say(dt) +
                " (the run's own step is " + Say(config.PhysicsStepSeconds) + "), sampled every " +
                Say(SampleSeconds) + " s\n" +
                "  start     root at (0, " + Say(-depth) + ", 0), identity rotation, at rest\n" +
                "  water     still (no current field), density " + Say(config.Fluid.Density) +
                ", drag " + Say(config.Fluid.DragCoefficient) +
                ", addedMass " + Say(config.Fluid.AddedMassCoefficient) +
                ", fluidAccel " + Say(config.Fluid.FluidAccelerationCoefficient) +
                ", panels " + config.Fluid.PanelsPerAxis +
                ", tissueExcessDensity " + Say(config.Fluid.TissueExcessDensity) +
                ", neutralBodyVolume " + Say(config.Fluid.NeutralBodyVolume) +
                ", surfaceRestoring " + Say(config.Fluid.SurfaceRestoringFraction) +
                ", waterHold " + Say(config.Fluid.WaterHoldSeconds) + " s\n" +
                "  world     depth " + Say(config.WorldDepthMetres) + " m, shape " + config.WorldShape +
                ", sharedSpace " + config.SharedSpace + " (the tank's glass and the bed are NOT built here)\n" +
                "  sense     Chemical and Energy read a constant " + Say(constant) +
                ", flow full scale " + Say(config.FlowFullScaleMetresPerSecond) + " m/s\n" +
                "  drive     limitAtEveryStep " + config.DriveLimitAtEveryStep +
                ", self-collision " + selfCollision);

            string[] rows = JsonlWriter.ReadRows(snapshotPath);

            // The bench's reader, clause for clause: blank rows skipped, a refusal counted and
            // dropped, the first refusal named. A genome the build refuses must be dropped on
            // both sides or every file after it is a picture of a different animal.
            var genomes = new List<Genome>();
            var genomeRow = new List<int>();
            int refused = 0;
            string firstRefusal = null;

            for (int i = 0; i < rows.Length && genomes.Count < GenomeLimit; i++)
            {
                if (rows[i].Length == 0) continue;

                try
                {
                    genomes.Add(GenomeJson.Read(rows[i]));
                    genomeRow.Add(i);
                }
                catch (Exception ex)
                {
                    refused++;
                    if (firstRefusal == null)
                    {
                        firstRefusal = ex.GetType().Name + ": " + FirstLine(ex.Message);
                    }
                }
            }

            if (genomes.Count == 0)
            {
                Debug.LogError("[ParitySwim] no genome in " + snapshotPath + " this build will read.");
                return false;
            }

            var developed = new List<Phenotype>();
            var developedFrom = new List<int>();
            int undeveloped = 0;

            for (int i = 0; i < genomes.Count; i++)
            {
                try
                {
                    developed.Add(Developer.Develop(genomes[i], config.Development, null, config.Shapes));
                    developedFrom.Add(i);
                }
                catch (Exception)
                {
                    undeveloped++;
                }
            }

            Debug.Log(
                "[ParitySwim] rows " + rows.Length + ", genomes read " + genomes.Count +
                ", refused " + refused + (firstRefusal != null ? " (" + firstRefusal + ")" : "") +
                ", developed " + developed.Count + ", refused " + undeveloped +
                ". The index in a file name is the index into the developed list, which is the " +
                "bench's numbering.");

            // Everything process-wide, set once and put back at the end. ConfigureScene zeroes
            // gravity (§5.2) and takes the depenetration velocity down to the number
            // FluidEnvironment measured; both are read back and printed below.
            SimulationMode previousMode = Physics.simulationMode;
            Vector3 previousGravity = Physics.gravity;
            int previousWorkers = Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount;
            float previousDepenetration = Physics.defaultMaxDepenetrationVelocity;

            bool ok = true;
            int written = 0;

            try
            {
                Ecosystem.ConfigurePhysicsStep(dt);
                int workers = Ecosystem.ConfigurePhysicsJobWorkers(0);
                FluidEnvironment.ConfigureScene(selfCollision);
                Physics.simulationMode = SimulationMode.Script;

                Debug.Log(
                    "[ParitySwim] PhysX, scene-wide: simulationMode=" + Physics.simulationMode +
                    " gravity=" + Physics.gravity.ToString("0.###") +
                    " defaultSolverIterations=" + Physics.defaultSolverIterations +
                    " defaultSolverVelocityIterations=" + Physics.defaultSolverVelocityIterations +
                    " defaultMaxDepenetrationVelocity=" + Say(Physics.defaultMaxDepenetrationVelocity) +
                    " defaultContactOffset=" + Say(Physics.defaultContactOffset) +
                    " bounceThreshold=" + Say(Physics.bounceThreshold) +
                    " sleepThreshold=" + Say(Physics.sleepThreshold) +
                    " jobWorkers=" + workers +
                    " Time.fixedDeltaTime=" + Say(Time.fixedDeltaTime) +
                    " (unused: the loop calls Physics.Simulate(" + Say(dt) + ") itself)");

                for (int i = 0; i < developed.Count && written < wanted; i++)
                {
                    Phenotype adult = developed[i];
                    if (adult.PartCount < 2) continue;

                    bool jointed = false;
                    for (int p = 1; p < adult.PartCount; p++)
                    {
                        if (adult.Parts[p].JointType != JointType.Fixed) { jointed = true; break; }
                    }

                    if (!jointed) continue;

                    long id = GenomeJson.ReadId(rows[genomeRow[developedFrom[i]]]);

                    ok &= One(
                        adult, config, i, id, outDirectory, tag, depth, dt, seconds, constant,
                        first: written == 0);

                    written++;
                }
            }
            finally
            {
                Physics.simulationMode = previousMode;
                Physics.gravity = previousGravity;
                Physics.defaultMaxDepenetrationVelocity = previousDepenetration;
                Ecosystem.ConfigurePhysicsJobWorkers(previousWorkers);
            }

            if (written == 0)
            {
                Debug.LogError("[ParitySwim] no multi-link jointed genome in this snapshot.");
                return false;
            }

            Debug.Log("[ParitySwim] wrote " + written + " trajectories into " + outDirectory);
            return ok;
        }

        /// <summary>One body, alone in still water, for the whole run.</summary>
        private static bool One(
            Phenotype adult, RunConfig config, int index, long id, string outDirectory,
            string tag, float depth, float dt, double seconds, float constant, bool first)
        {
            var start = new Vector3(0f, -Mathf.Abs(depth), 0f);

            CreatureInstance instance =
                PhenotypeBuilder.Build(adult, start, null, config.Shapes);

            instance.Root.name = "ParityCreature" + index.ToString("00", CultureInfo.InvariantCulture);

            try
            {
                // Still water: the current is null rather than the run's, which is the one line
                // that makes this the spike's world. Everything else about the fluid is the
                // run's own — same drag, same added mass, same panels, same buoyancy.
                var fluid = new FluidEnvironment(config.Fluid, config.Shapes, null)
                {
                    PatchCount = Mathf.Max(1, (int)config.HorizontalPatches),
                    WorldDepthMetres = config.WorldDepthMetres,
                    FloorIsSolid = config.SharedSpace,
                };

                fluid.ApplyAddedMass(instance);

                Brain brain = Brain.For(adult);

                if (brain.TotalDof != instance.TotalDof)
                {
                    Debug.LogError(
                        "[ParitySwim] genome " + index + ": the brain makes " + brain.TotalDof +
                        " drive values and the articulation has " + instance.TotalDof + " DOF.");
                    return false;
                }

                // No field and no account: both read zero inside, and the wrapper below answers
                // the spike's constant for them. Everything else is the farm's own perception.
                var inner = new CreatureSensors(
                    instance, config.WorldDepthMetres, null, null, brain.SensorMask, config);

                var senses = new ParitySenses(inner, constant);
                var driver = new EffectorDriver(instance, Ecosystem.FixedDt, config.DriveLimitAtEveryStep);

                var drive = new float[Mathf.Max(1, brain.TotalDof)];
                int dof = instance.TotalDof;
                var angles = new float[Mathf.Max(1, dof)];

                if (first) ReportArticulation(instance, adult);

                var text = new StringBuilder();
                text.Append("t,x,y,z,qw,qx,qy,qz");
                for (int d = 0; d < dof; d++) text.Append(",q").Append(d);
                text.AppendLine();

                int steps = (int)Math.Round(seconds / dt);
                int every = Math.Max(1, (int)Math.Round(SampleSeconds / dt));

                ArticulationBody root = instance.Bodies[0];
                Vector3 previous = root.transform.position;
                double path = 0d;
                bool lost = false;
                double lostAt = -1d;

                for (int step = 0; step <= steps; step++)
                {
                    double t = step * (double)dt;

                    if (step % every == 0)
                    {
                        Vector3 place = root.transform.position;
                        Quaternion turn = root.transform.rotation;

                        if (!Finite(place) || !Finite(turn))
                        {
                            lost = true;
                            lostAt = t;
                            break;
                        }

                        if (step > 0) path += Vector3.Distance(place, previous);
                        previous = place;

                        Sample(text, t, place, turn, ReadAngles(instance, adult, angles, dof), dof);
                    }

                    if (step == steps) break;

                    // Ecosystem.Step's order, term for term. Sensors before the brain so every
                    // neuron perceives one instant; the fluid's clock off the physics step so a
                    // current would be a flow rather than a staircase (there is none here, and
                    // the line stays so that the ordering is the farm's).
                    inner.Sample();
                    brain.Step(dt, drive, senses);
                    driver.Drive(drive);

                    fluid.ElapsedSeconds = t;
                    fluid.Apply(instance, dt);

                    Physics.Simulate(dt);

                    fluid.Settle(instance);
                    driver.Settle();
                }

                string name = Path.Combine(
                    outDirectory,
                    "physx-" + tag + "-genome" + index.ToString("00", CultureInfo.InvariantCulture) +
                    "-parts" + adult.PartCount + "-dof" + dof + ".csv");

                File.WriteAllText(name, text.ToString());

                Vector3 end = lost ? previous : root.transform.position;
                float net = Vector3.Distance(end, start);
                float centre = lost ? float.NaN
                    : Vector3.Distance(FluidEnvironment.CentreOfMass(instance), start);

                double lived = lost ? lostAt : seconds;

                Debug.Log(
                    "[ParitySwim] " + Path.GetFileName(name) +
                    "   creature " + id +
                    ", parts " + adult.PartCount +
                    ", dof " + dof +
                    ", net root displacement " + Say(net) + " m" +
                    ", mean speed " + Say(lived > 0 ? net / lived : 0d) + " m/s (net over " +
                    Say(lived) + " s)" +
                    ", path " + Say(path) + " m" +
                    ", mean speed " + Say(lived > 0 ? path / lived : 0d) + " m/s (path)" +
                    ", centre-of-mass from start " + Say(centre) + " m (the bench's own column)" +
                    ", " + (lost
                        ? "LOST at " + Say(lostAt) + " s — the CSV stops there"
                        : "finite"));

                return true;
            }
            finally
            {
                instance.Destroy();
            }
        }

        /// <summary>
        /// Everything PhysX was handed about this articulation, read back off the built bodies
        /// rather than off the code that set it.
        /// </summary>
        private static void ReportArticulation(CreatureInstance instance, Phenotype adult)
        {
            var report = new StringBuilder();
            report.Append("[ParitySwim] PhysX, read back off the first articulation (")
                  .Append(instance.Bodies.Length).Append(" links, ")
                  .Append(instance.TotalDof).Append(" DOF):");

            for (int b = 0; b < instance.Bodies.Length; b++)
            {
                ArticulationBody body = instance.Bodies[b];

                report.Append("\n  link ").Append(b)
                      .Append("  ").Append(adult.Parts[b].JointType)
                      .Append("  mass ").Append(Say(body.mass))
                      .Append(" kg  solverIterations ").Append(body.solverIterations)
                      .Append("/").Append(body.solverVelocityIterations)
                      .Append("  linearDamping ").Append(Say(body.linearDamping))
                      .Append("  angularDamping ").Append(Say(body.angularDamping))
                      .Append("  jointFriction ").Append(Say(body.jointFriction))
                      .Append("  useGravity ").Append(body.useGravity)
                      .Append("  maxDepenetrationVelocity ").Append(Say(body.maxDepenetrationVelocity))
                      .Append("  maxAngularVelocity ").Append(Say(body.maxAngularVelocity))
                      .Append("  maxJointVelocity ").Append(Say(body.maxJointVelocity))
                      .Append("  sleepThreshold ").Append(Say(body.sleepThreshold));

                if (b == 0)
                {
                    report.Append("  (root, immovable ").Append(body.immovable).Append(")");
                    continue;
                }

                report.Append("\n           jointType ").Append(body.jointType)
                      .Append("  locks twist/swingY/swingZ ").Append(body.twistLock)
                      .Append("/").Append(body.swingYLock).Append("/").Append(body.swingZLock)
                      .Append("  matchAnchors ").Append(body.matchAnchors)
                      .Append("\n           xDrive ").Append(Drive(body.xDrive))
                      .Append("\n           yDrive ").Append(Drive(body.yDrive))
                      .Append("\n           zDrive ").Append(Drive(body.zDrive));
            }

            Debug.Log(report.ToString());
        }

        private static string Drive(ArticulationDrive drive) =>
            "stiffness " + Say(drive.stiffness) +
            ", damping " + Say(drive.damping) +
            ", forceLimit " + Say(drive.forceLimit) +
            ", limits " + Say(drive.lowerLimit) + ".." + Say(drive.upperLimit) + " deg" +
            ", target " + Say(drive.target) +
            ", targetVelocity " + Say(drive.targetVelocity) +
            ", driveType " + drive.driveType;

        /// <summary>
        /// The joint angles in the phenotype's own DOF order — the order
        /// <c>CreatureInstance.DofOffset</c> assigns, which is the order the brain's drive
        /// values are in and the order the spike's <c>Q</c> is in.
        /// </summary>
        private static float[] ReadAngles(
            CreatureInstance instance, Phenotype adult, float[] into, int dof)
        {
            for (int d = 0; d < dof; d++) into[d] = 0f;

            for (int b = 0; b < instance.Bodies.Length; b++)
            {
                int offset = instance.DofOffset[b];
                if (offset < 0) continue;

                int n = adult.Parts[b].JointType.DofCount();
                if (n == 0) continue;

                ArticulationReducedSpace q = instance.Bodies[b].jointPosition;

                for (int d = 0; d < n && d < q.dofCount; d++)
                {
                    if (offset + d < dof) into[offset + d] = q[d];
                }
            }

            return into;
        }

        /// <summary>One CSV row, in the bench's columns and the bench's number formats.</summary>
        private static void Sample(
            StringBuilder text, double t, Vector3 place, Quaternion turn, float[] angles, int dof)
        {
            text.Append(t.ToString("0.###", CultureInfo.InvariantCulture));
            text.Append(',').Append(place.x.ToString("0.######", CultureInfo.InvariantCulture));
            text.Append(',').Append(place.y.ToString("0.######", CultureInfo.InvariantCulture));
            text.Append(',').Append(place.z.ToString("0.######", CultureInfo.InvariantCulture));
            text.Append(',').Append(turn.w.ToString("0.######", CultureInfo.InvariantCulture));
            text.Append(',').Append(turn.x.ToString("0.######", CultureInfo.InvariantCulture));
            text.Append(',').Append(turn.y.ToString("0.######", CultureInfo.InvariantCulture));
            text.Append(',').Append(turn.z.ToString("0.######", CultureInfo.InvariantCulture));

            for (int d = 0; d < dof; d++)
            {
                text.Append(',').Append(angles[d].ToString("0.######", CultureInfo.InvariantCulture));
            }

            text.AppendLine();
        }

        /// <summary>
        /// The farm's perception with the two channels the spike does not model answered by the
        /// spike's constant.
        /// </summary>
        /// <remarks>
        /// A constant and not zero, for the spike's own reason: zero is what an
        /// <i>unimplemented</i> channel reads in the farm, so a brain that happened to read one
        /// would be driven by a different number on each side of the comparison. 0.5 is
        /// comparable in shape while being obviously not a measurement.
        /// </remarks>
        private sealed class ParitySenses : ISensorField
        {
            private readonly CreatureSensors _inner;
            private readonly float _constant;

            public ParitySenses(CreatureSensors inner, float constant)
            {
                _inner = inner;
                _constant = constant;
            }

            public float Read(int partIndex, SensorChannel channel, int index)
            {
                if (channel == SensorChannel.Chemical || channel == SensorChannel.Energy)
                {
                    return _constant;
                }

                return _inner.Read(partIndex, channel, index);
            }
        }

        // ---------------------------------------------------------------- small change

        /// <summary>
        /// <c>r42-s4-20000</c> from <c>runs/r42-s4/&lt;run&gt;/snapshots/000020000.jsonl</c> —
        /// the arm's directory name and the snapshot's second, which is what the bench's file
        /// names carry.
        /// </summary>
        private static string TagFrom(string snapshotPath)
        {
            string second = Path.GetFileNameWithoutExtension(snapshotPath) ?? "snapshot";
            second = second.TrimStart('0');
            if (second.Length == 0) second = "0";

            string arm = "run";

            try
            {
                DirectoryInfo snapshots = Directory.GetParent(snapshotPath);
                DirectoryInfo armDirectory = snapshots?.Parent?.Parent;
                if (armDirectory != null) arm = armDirectory.Name;
            }
            catch (Exception)
            {
                // A path that is not a run directory still gets a name; it is a label, not a key.
            }

            return arm + "-" + second;
        }

        private static string DefaultOutDirectory()
        {
            // <repo>/scratch/solver-spike/traj-physx, from <repo>/unity-wN/Assets.
            DirectoryInfo project = Directory.GetParent(Application.dataPath);
            string repo = project?.Parent?.FullName ?? Application.dataPath;
            return Path.Combine(repo, "scratch", "solver-spike", "traj-physx");
        }

        private static bool Finite(Vector3 v)
        {
            float sum = v.x + v.y + v.z;
            return !float.IsNaN(sum) && !float.IsInfinity(sum);
        }

        private static bool Finite(Quaternion q)
        {
            float sum = q.x + q.y + q.z + q.w;
            return !float.IsNaN(sum) && !float.IsInfinity(sum);
        }

        private static string FirstLine(string s)
        {
            int n = s.IndexOf('\n');
            return n < 0 ? s : s.Substring(0, n).TrimEnd('\r');
        }

        private static string Say(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);

        private static string Text(string variable) => Environment.GetEnvironmentVariable(variable);

        private static float Number(string variable, float fallback)
        {
            string text = Environment.GetEnvironmentVariable(variable);
            return !string.IsNullOrEmpty(text) &&
                   float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)
                ? v
                : fallback;
        }

        private static int Whole(string variable, int fallback)
        {
            string text = Environment.GetEnvironmentVariable(variable);
            return !string.IsNullOrEmpty(text) &&
                   int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)
                ? v
                : fallback;
        }

        private static bool Flag(string variable, bool fallback)
        {
            string text = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrEmpty(text)) return fallback;

            text = text.Trim();
            return !(text == "0" ||
                     string.Equals(text, "false", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(text, "off", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(text, "no", StringComparison.OrdinalIgnoreCase));
        }
    }
}
