using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Evosim.Core;
using Evosim.Dynamics;
using Evosim.Farm;

namespace Evosim.Dynamics.Bench
{
    /// <summary>
    /// The spike's measurements: round 42's evolved bodies, developed by Core, stepped by
    /// <see cref="DynamicsWorld"/> under their own brains in still water.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The run directory is read and never written.</b> Only <c>config.json</c> and one
    /// snapshot are opened, both read-only; everything this produces goes to stdout or to the
    /// trajectory directory it is given.
    /// </para>
    /// <para>
    /// <b>The world is the run's, and the water is still.</b> The fluid, development and world
    /// tunables come from the recorded config, so the bodies are the size, the mass and the drag
    /// the round gave them; the current is not ported, so every body sits in water at rest and
    /// D090's acceleration term is identically zero however the config sets its coefficient.
    /// </para>
    /// </remarks>
    internal static class Program
    {
        private const string DefaultRun =
            @"D:\Projects\experiments\evolution-simulator\runs\r45fixb-s4\2026-09-22-213554-f943f5f1";

        private const string DefaultSnapshot = "000020000.jsonl";

        private static int Main(string[] args)
        {
            string run = DefaultRun;
            string snapshot = DefaultSnapshot;
            int bodies = 1000;
            int genomeLimit = 1000;
            string mode = "all";
            string trajectoryDirectory =
                @"D:\Projects\experiments\evolution-simulator\scratch\solver-spike\traj";
            int stabilitySteps = 0;      // 0 = the spec's full lengths
            int paceSteps = 2000;
            int warmup = 200;
            int[] threadCounts = { 1, 2, 4, 8, 16, 24 };
            int recordAt = -1;           // --at: the recorded second the record mode rebuilds
            double cellOverride = 0;     // --cell: a contact-grid cell in place of the rule
            int recordSteps = 300;
            string configOverride = null;   // --config: a config.json this build reads, for a run whose own it refuses
            bool modulesAtMax = false;      // --modules max: develop every indeterminate node at its ceiling
            long dumpId = -1;               // --dump <id>: print one rebuilt body part by part
            bool shaped = false;            // --shaped: ShapedAddedMass on in memory (the pace read's fin law)
            string family = null;           // --family nearest: jointed nodes become the nearest family member
            int repeats = 1;                // --repeats <n>: the timed steps, n times over
            bool repeatsGiven = false;
            bool farmVariantOnly = false;   // --variant farm: time only the farm's own variant (contact on, instrument on)

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--run": run = args[++i]; break;
                    case "--snapshot": snapshot = args[++i]; break;
                    case "--at":
                        recordAt = int.Parse(args[++i], CultureInfo.InvariantCulture);
                        snapshot = recordAt.ToString("000000000", CultureInfo.InvariantCulture) + ".jsonl";
                        break;
                    case "--config": configOverride = args[++i]; break;
                    case "--modules": modulesAtMax = args[++i] == "max"; break;
                    case "--dump": dumpId = long.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--shaped": shaped = true; break;
                    case "--family": family = args[++i]; break;
                    case "--repeats":
                        repeats = int.Parse(args[++i], CultureInfo.InvariantCulture);
                        repeatsGiven = true;
                        break;
                    case "--variant":
                        string variant = args[++i];
                        if (variant != "farm")
                        {
                            Console.Error.WriteLine("--variant takes 'farm' (the farm's own variant: contact on, instrument on), not '" + variant + "'");
                            return 2;
                        }
                        farmVariantOnly = true;
                        break;
                    case "--cell": cellOverride = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--record-steps": recordSteps = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--bodies": bodies = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--genomes": genomeLimit = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--mode": mode = args[++i]; break;
                    case "--traj": trajectoryDirectory = args[++i]; break;
                    case "--stability-steps": stabilitySteps = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--pace-steps": paceSteps = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--warmup": warmup = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--no-contact": _noContact = true; break;
                    case "--limit-omega":
                        _limitOmega = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--limit-zeta":
                        _limitZeta = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--stability-threads": StabilityThreads = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--no-wall": _noWall = true; break;
                    case "--threads":
                        string[] parts = args[++i].Split(',');
                        threadCounts = new int[parts.Length];
                        for (int k = 0; k < parts.Length; k++)
                        {
                            threadCounts[k] = int.Parse(parts[k], CultureInfo.InvariantCulture);
                        }
                        break;
                    default:
                        Console.Error.WriteLine("unknown option " + args[i]);
                        return 2;
                }
            }

            if (family != null && family != "nearest")
            {
                Console.Error.WriteLine("--family takes 'nearest' (box, sphere and capsule each become the nearest member of the superellipsoid family), not '" + family + "'");
                return 2;
            }

            if (repeats < 1)
            {
                Console.Error.WriteLine("--repeats must be at least 1");
                return 2;
            }

            if ((shaped || family != null || repeatsGiven || farmVariantOnly) && mode != "record")
            {
                Console.Error.WriteLine("--shaped, --family, --repeats and --variant belong to --mode record, the pace read's mode");
                return 2;
            }

            // A run recorded before a tunable has a config this build refuses (CLAUDE.md's rule);
            // the record mode can still rebuild its crowd from a config of the same world written
            // by this build, which is what --config names. The hash line below says which was read.
            string configPath = configOverride ?? Path.Combine(run, "config.json");
            string snapshotPath = Path.Combine(run, "snapshots", snapshot);

            // Which record the run holds is asked in the record mode only; every other mode, and a run
            // of the JSONL record, reads and prints as it always did.
            int recordFormat = mode == "record" ? RecordFiles.FormatOf(run) : RunRecordFormat.Jsonl;
            if (recordFormat == RunRecordFormat.Compact && recordAt >= 0)
            {
                snapshotPath = Path.Combine(run, "snapshots", RecordFiles.SnapshotName(recordAt, recordFormat));
            }

            RunConfig config = RunConfigJson.Read(File.ReadAllText(configPath), out string mismatch);

            Console.WriteLine("=== Evosim.Dynamics bench");
            Console.WriteLine("  run       " + run);
            Console.WriteLine("  snapshot  " + snapshotPath);
            Console.WriteLine("  config    hash " + config.Hash() +
                              (mismatch != null ? "  (recorded: " + mismatch + ")" : "  (matches the record)"));
            Console.WriteLine("  machine   " + Environment.ProcessorCount + " logical processors, " +
                              (Environment.Is64BitProcess ? "x64" : "x86") +
                              ", server GC " + System.Runtime.GCSettings.IsServerGC);
            Console.WriteLine();

            // The pace read's two config edits, made in memory after the header above has said what the
            // record's own config hashes to. Neither touches the file.
            if (shaped)
            {
                if (config.Fluid == null || !(config.Fluid.PowerPushRadiansPerSecond > 0f))
                {
                    Console.Error.WriteLine(
                        "--shaped refused: the recorded config does not drive the bend-and-power stroke " +
                        "(Fluid.PowerPushRadiansPerSecond is " +
                        (config.Fluid == null ? "absent" : config.Fluid.PowerPushRadiansPerSecond.ToString(CultureInfo.InvariantCulture)) +
                        ", and has to be above 0). The fin law's family limb is built for the power push only.");
                    return 2;
                }

                config.Fluid.ShapedAddedMass = true;
                Console.WriteLine(
                    "  shaped    ShapedAddedMass on in memory; the recorded config drives the bend-and-power stroke " +
                    "(PowerPushRadiansPerSecond " + config.Fluid.PowerPushRadiansPerSecond.ToString(CultureInfo.InvariantCulture) +
                    ", LimbPushCoefficient " + config.Fluid.LimbPushCoefficient.ToString(CultureInfo.InvariantCulture) + ")");
            }

            if (family != null)
            {
                if (Array.IndexOf(config.ShapeIds, ShapeIds.Superellipsoid) < 0)
                {
                    var withFamily = new string[config.ShapeIds.Length + 1];
                    Array.Copy(config.ShapeIds, withFamily, config.ShapeIds.Length);
                    withFamily[withFamily.Length - 1] = ShapeIds.Superellipsoid;
                    config.ShapeIds = withFamily;
                }

                Console.WriteLine("  family    nearest; shape table in memory: " + string.Join(", ", config.ShapeIds));
            }

            var recordOptions = new RecordOptions
            {
                Format = recordFormat,
                FamilyNearest = family != null,
                Repeats = repeats,
                FarmVariantOnly = farmVariantOnly,
                Extra = recordFormat == RunRecordFormat.Compact || shaped || family != null || repeatsGiven || farmVariantOnly,
            };

            // The compact record holds no snapshots/NNNNNNNNN.jsonl for the read below to open, and the record
            // mode reads its own snapshot, so it goes straight there.
            if (mode == "record" && recordFormat == RunRecordFormat.Compact)
            {
                if (recordAt < 0)
                {
                    Console.Error.WriteLine("--mode record needs --at <recorded second>");
                    return 2;
                }

                Record(config, run, recordAt, threadCounts, warmup, recordSteps, cellOverride, modulesAtMax, dumpId, recordOptions);
                return 0;
            }

            List<Genome> genomes = ReadGenomes(snapshotPath, genomeLimit, out int refused, out string firstRefusal);
            Console.WriteLine($"  genomes read {genomes.Count}, refused {refused}" +
                              (firstRefusal != null ? " (" + firstRefusal + ")" : ""));

            if (genomes.Count == 0)
            {
                Console.Error.WriteLine("nothing to step");
                return 1;
            }

            var developed = new List<Phenotype>();
            int undeveloped = 0;
            foreach (Genome genome in genomes)
            {
                try { developed.Add(Developer.Develop(genome, config.Development, null, config.Shapes, cellTypes: config.CellTypes)); }
                catch (Exception) { undeveloped++; }
            }

            Console.WriteLine($"  developed {developed.Count}, refused {undeveloped}");
            Console.WriteLine(Describe(developed));
            Console.WriteLine();

            if (mode == "record")
            {
                if (recordAt < 0)
                {
                    Console.Error.WriteLine("--mode record needs --at <recorded second>");
                    return 2;
                }

                Record(config, run, recordAt, threadCounts, warmup, recordSteps, cellOverride, modulesAtMax, dumpId, recordOptions);
                return 0;
            }

            if (mode == "all" || mode == "stability")
            {
                Stability(config, developed, bodies, 0.01, stabilitySteps > 0 ? stabilitySteps : 60000);
                Stability(config, developed, bodies, 0.02, stabilitySteps > 0 ? stabilitySteps / 2 : 30000);
            }

            if (mode == "all" || mode == "pace")
            {
                Pace(config, developed, bodies, threadCounts, warmup, paceSteps);
            }

            if (mode == "all" || mode == "identity")
            {
                Identity(config, developed, bodies, 10000);
            }

            if (mode == "trace")
            {
                // --bodies names the genome to trace, since only one is stepped.
                Trace(config, developed, bodies, stabilitySteps > 0 ? stabilitySteps : 100, 0.01);
            }

            if (mode == "parity")
            {
                // --bodies names the genome, as in trace; --stability-steps its length.
                Parity(config, developed, bodies, stabilitySteps > 0 ? stabilitySteps : 6000, 0.01);
            }

            if (mode == "limits")
            {
                // One line per driven degree of freedom, for the parity table's join: which stop
                // each engine's joint actually reached is the whole of the drive-axis question.
                for (int i = 0; i < developed.Count; i++)
                {
                    Phenotype adult = developed[i];
                    if (adult.PartCount < 2) continue;

                    bool jointed = false;
                    for (int p = 1; p < adult.PartCount; p++)
                    {
                        if (adult.Parts[p].JointType != JointType.Fixed) { jointed = true; break; }
                    }
                    if (!jointed) continue;

                    SolverConfig one = Configure(config, 0.01);
                    var probeBody = new Creature(0, adult, one, config.Shapes);

                    for (int p = 1; p < probeBody.Links; p++)
                    {
                        int at = probeBody.DofStart[p];
                        for (int d = 0; d < probeBody.DofCount[p]; d++)
                        {
                            Console.WriteLine(string.Format(
                                CultureInfo.InvariantCulture,
                                "LIMIT genome={0} link={1} joint={2} mirrored={3} dof={4} local={5} lo={6:0.####} hi={7:0.####} power={8:0.####}",
                                i, p, adult.Parts[p].JointType, adult.Parts[p].Mirrored,
                                at + d, d, probeBody.LimitLo[at + d], probeBody.LimitHi[at + d],
                                probeBody.Power[p]));
                        }
                    }
                }
            }

            if (mode == "diagnose")
            {
                Diagnose(config, developed, stabilitySteps > 0 ? stabilitySteps : 6000, 0.01);
                Diagnose(config, developed, stabilitySteps > 0 ? stabilitySteps : 6000, 0.02);
            }

            if (mode == "all" || mode == "traj")
            {
                Trajectories(config, developed, genomes, trajectoryDirectory, int.Parse(Environment.GetEnvironmentVariable("EVOSIM_TRAJ_COUNT") ?? "5"), 60.0);
            }

            return 0;
        }

        // ---------------------------------------------------------------- the world

        /// <summary>
        /// The solver's view of the run's world, with the water at rest and the tank's own
        /// glass. The wall radius comes from <c>worldAreaSquareMetres</c> the way
        /// <c>TankGeometry</c> takes it.
        /// </summary>
        /// <summary>Threads the stability arm runs on. The trajectory is the same at any count.</summary>
        private static int StabilityThreads = 8;

        private static bool _noContact;
        private static bool _noWall;

        /// <summary>
        /// The joint-limit spring, swept from the command line so the overshoot it allows and the
        /// energy it takes out of a body resting on its stop can be read against each other.
        /// Negative means "leave the solver's own default".
        /// </summary>
        private static double _limitOmega = -1;

        private static double _limitZeta = -1;

        private static SolverConfig Configure(RunConfig config, double dt)
        {
            SolverConfig solver = SolverConfig.From(config, dt);

            if (_limitOmega >= 0) solver.JointLimitOmegaTimesStep = _limitOmega * dt;
            if (_limitZeta >= 0) solver.JointLimitDampingRatio = _limitZeta;

            // Core's own function rather than a square root taken here, so the glass stands
            // exactly where the field's mask, the placer's disc and the gyre's normalised radius
            // put theirs — and the axis with it, at (R, R), which is Core's frame.
            solver.TankRadiusMetres = config.WorldShape == WorldShape.Tank && !_noWall
                ? TankGeometry.RadiusFor(config.WorldAreaSquareMetres)
                : 0;

            solver.CreatureContact = !_noContact;

            return solver;
        }

        private const double ScatterRadius = 26.0;
        private const double ScatterDepth = 15.0;

        private static DynamicsWorld Populate(
            RunConfig config, List<Phenotype> developed, int bodies, double dt, int threads)
        {
            SolverConfig solver = Configure(config, dt);
            var world = new DynamicsWorld(solver) { Threads = threads };

            // One stream, one seed: the same placement every time this bench is run, which is
            // what lets the identity section compare digests across separate populations.
            var rng = new Rng(4242424242);

            for (int i = 0; i < bodies; i++)
            {
                Phenotype adult = developed[i % developed.Count];
                var body = new Creature(i, adult, solver, config.Shapes);

                double radius = ScatterRadius * System.Math.Sqrt(rng.NextFloat());
                double theta = 2.0 * System.Math.PI * rng.NextFloat();

                // About the tank's own axis, which Core puts at (R, R) and not at the origin —
                // the water is [0, 2R) on both horizontal axes. Scattering about the origin, as
                // this did, put every body some 26 m outside a 26 m glass, and the wall spring
                // answered on the first step.
                double axis = solver.TankAxisX;

                body.PlaceAt(
                    new Vec3(
                        axis + radius * System.Math.Cos(theta),
                        -ScatterDepth * rng.NextFloat(),
                        axis + radius * System.Math.Sin(theta)),
                    QuatD.FromAxisAngle(
                        new Vec3(rng.NextFloat() - 0.5, rng.NextFloat() - 0.5, rng.NextFloat() - 0.5)
                            .Normalized,
                        rng.NextFloat() * 2.0 * System.Math.PI));

                world.Add(body);
            }

            return world;
        }

        // ---------------------------------------------------------------- (a) stability

        private static void Stability(
            RunConfig config, List<Phenotype> developed, int bodies, double dt, int steps)
        {
            DynamicsWorld world = Populate(config, developed, bodies, dt, StabilityThreads);

            var watch = Stopwatch.StartNew();
            double worstSpeed = 0;
            int firstLoss = -1;

            for (int step = 0; step < steps; step++)
            {
                world.Step();

                if (step % 200 != 0 && step != steps - 1) continue;

                double speed = world.MaxLinkSpeed();
                if (speed > worstSpeed) worstSpeed = speed;
                if (firstLoss < 0 && world.NonFiniteBodies() > 0) firstLoss = step;
            }

            watch.Stop();

            Console.WriteLine($"--- stability, dt {dt}, {steps} steps ({steps * dt:0.#} simulated seconds), " +
                              $"{bodies} bodies");
            Console.WriteLine($"    non-finite bodies   {world.NonFiniteBodies()} of {bodies}" +
                              (firstLoss >= 0 ? $"   (first seen at step {firstLoss})" : ""));
            Console.WriteLine($"    max link speed      {worstSpeed:0.###} m/s");
            Console.WriteLine($"    drag impulses capped {world.DragImpulsesLimited()}, " +
                              $"drive impulses capped {world.DriveImpulsesLimited()}");
            Console.WriteLine($"    wall {watch.Elapsed.TotalSeconds:0.##} s " +
                              $"({steps * dt / watch.Elapsed.TotalSeconds:0.##}x real time on {StabilityThreads} thread(s))");
            Console.WriteLine();
        }

        /// <summary>One genome, alone, printed step by step until it stops being a number.</summary>
        private static void Trace(
            RunConfig config, List<Phenotype> developed, int which, int steps, double dt)
        {
            SolverConfig solver = Configure(config, dt);
            solver.CreatureContact = false;
            solver.TankRadiusMetres = 0;

            Phenotype adult = developed[which];
            var body = new Creature(which, adult, solver, config.Shapes);
            body.PlaceAt(new Vec3(0, -10, 0), QuatD.Identity);

            var world = new DynamicsWorld(solver);
            world.Add(body);

            Console.WriteLine($"--- trace of genome {which}: {adult.PartCount} parts, {body.Dof} dof, dt {dt}");
            for (int p = 1; p < body.Links; p++)
            {
                Console.WriteLine($"    link {p}: {adult.Parts[p].JointType}, mass {body.Mass[p]:0.###} kg, " +
                                  $"inertia {body.InertiaLocal[3 * p]:0.####}/{body.InertiaLocal[3 * p + 1]:0.####}/" +
                                  $"{body.InertiaLocal[3 * p + 2]:0.####}, power {body.Power[p]:0.##}");
            }

            for (int step = 0; step < steps; step++)
            {
                world.Step();

                double worstRate = 0, worstD = 0;
                for (int d = 0; d < body.Dof; d++)
                {
                    double v = System.Math.Abs(body.Qd[d]);
                    if (v > worstRate) worstRate = v;
                }
                for (int p = 1; p < body.Links; p++)
                {
                    for (int k = 0; k < 9; k++)
                    {
                        double v = System.Math.Abs(body.Dinv[9 * p + k]);
                        if (v > worstD) worstD = v;
                    }
                }

                bool interesting = worstRate > 20 || !body.Alive || step % 25 == 0;
                if (interesting)
                {
                    var q = new StringBuilder();
                    for (int d = 0; d < body.Dof; d++)
                    {
                        q.Append(body.Q[d].ToString("0.00", CultureInfo.InvariantCulture)).Append(' ');
                    }

                    Console.WriteLine(
                        $"    step {step,5}  max|qd| {worstRate,10:0.###}  max|Dinv| {worstD,12:0.###e+00}  " +
                        $"max link {body.MaxLinkSpeed(),9:0.###} m/s  q = {q}" +
                        (body.Alive ? "" : "   LOST"));
                }

                if (!body.Alive) break;
            }

            Console.WriteLine();
        }

        /// <summary>
        /// One genome's whole drive-and-limit account, in the terms the PhysX parity comparison
        /// needs: what the brain settles on emitting, what the genome's limits are, what the
        /// penalty spring is worth, and where those three put the joint.
        /// </summary>
        /// <remarks>
        /// The last column is the prediction. At rest the drive damping and the limit damper both
        /// vanish, so a degree of freedom sitting outside its stop is in equilibrium at
        /// <c>stop + m / k</c> with <c>m</c> the drive torque and <c>k</c> the penalty stiffness.
        /// A measured angle that matches it is an overshoot; one that does not is a bug.
        /// </remarks>
        private static void Parity(
            RunConfig config, List<Phenotype> developed, int which, int steps, double dt)
        {
            SolverConfig solver = Configure(config, dt);
            solver.CreatureContact = false;
            solver.TankRadiusMetres = 0;

            Phenotype adult = developed[which];
            var body = new Creature(which, adult, solver, config.Shapes);
            body.PlaceAt(new Vec3(0, -10, 0), QuatD.Identity);

            var world = new DynamicsWorld(solver);
            world.Add(body);

            for (int step = 0; step < steps; step++) world.Step();

            Console.WriteLine(
                $"--- parity account for genome {which}: {adult.PartCount} parts, {body.Dof} dof, " +
                $"dt {dt}, after {steps} steps ({steps * dt:0.#} s)");
            Console.WriteLine(
                $"    limit spring omega {solver.JointLimitOmegaTimesStep / dt:0.#} rad/s, " +
                $"zeta {solver.JointLimitDampingRatio}, drive damping {solver.JointDriveDamping}");

            for (int p = 1; p < body.Links; p++)
            {
                Console.WriteLine(
                    $"    link {p}: {adult.Parts[p].JointType}, parent {body.Parent[p]}, " +
                    $"limits {LimitText(body, p)}, " +
                    $"mirrored {adult.Parts[p].Mirrored}, " +
                    $"mass {body.Mass[p]:0.#####} kg, power {body.Power[p]:0.####} N.m, " +
                    $"inertia {body.InertiaLocal[3 * p]:0.###e+00}/" +
                    $"{body.InertiaLocal[3 * p + 1]:0.###e+00}/{body.InertiaLocal[3 * p + 2]:0.###e+00}");
            }

            Console.WriteLine(
                "    dof   link  lo rad   hi rad    k N.m/rad       m N.m      q rad     qd rad/s" +
                "   stop+m/k   over rad");

            for (int p = 1; p < body.Links; p++)
            {
                int n = body.DofCount[p];
                int at = body.DofStart[p];

                for (int d = 0; d < n; d++)
                {
                    int j = at + d;
                    double q = body.Q[j];
                    double k = body.LimitStiffness[j];
                    double m = body.Drive.Magnitude(j);
                    double stop = q > body.LimitHi[j] ? body.LimitHi[j]
                        : q < body.LimitLo[j] ? body.LimitLo[j]
                        : double.NaN;
                    double predicted = stop + m / k;
                    double over = double.IsNaN(stop) ? 0 : q - stop;

                    Console.WriteLine(string.Format(
                        CultureInfo.InvariantCulture,
                        "    {0,3}   {1,4}  {2,7:0.###}  {3,7:0.###}  {4,11:0.####e+00}  {5,10:0.###e+00}  " +
                        "{6,9:0.#####}  {7,11:0.#####}  {8,9:0.#####}  {9,9:0.#####}",
                        j, p, body.LimitLo[j], body.LimitHi[j], k, m, q, body.Qd[j],
                        double.IsNaN(stop) ? 0 : predicted, over));
                }
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Every genome alone, out of anything's reach, with no contact and no glass: whatever
        /// the solver loses here it loses on the body's own dynamics rather than on a push.
        /// </summary>
        private static void Diagnose(RunConfig config, List<Phenotype> developed, int steps, double dt)
        {
            SolverConfig solver = Configure(config, dt);
            solver.CreatureContact = false;
            solver.TankRadiusMetres = 0;

            var world = new DynamicsWorld(solver) { Threads = 16 };

            for (int i = 0; i < developed.Count; i++)
            {
                var body = new Creature(i, developed[i], solver, config.Shapes);
                body.PlaceAt(new Vec3(1000.0 * i, -10, 0), QuatD.Identity);
                world.Add(body);
            }

            var lostAt = new int[developed.Count];
            for (int i = 0; i < lostAt.Length; i++) lostAt[i] = -1;

            for (int step = 0; step < steps; step++)
            {
                world.Step();
                for (int i = 0; i < world.Creatures.Count; i++)
                {
                    if (lostAt[i] < 0 && !world.Creatures[i].Alive) lostAt[i] = step;
                }
            }

            int lost = 0;
            var lines = new List<string>();

            for (int i = 0; i < lostAt.Length; i++)
            {
                if (lostAt[i] < 0) continue;
                lost++;
                if (lines.Count >= 12) continue;

                Creature body = (Creature)world.Creatures[i];
                Phenotype adult = developed[i];

                double heaviest = 0, lightest = double.MaxValue, power = 0, thinnest = double.MaxValue;
                for (int p = 0; p < body.Links; p++)
                {
                    if (body.Mass[p] > heaviest) heaviest = body.Mass[p];
                    if (body.Mass[p] < lightest) lightest = body.Mass[p];
                    if (body.Power[p] > power) power = body.Power[p];
                    if (body.SmallestInertia[p] < thinnest) thinnest = body.SmallestInertia[p];
                }

                double worstRatio = 0;
                for (int p = 1; p < body.Links; p++)
                {
                    double a = body.Mass[p], b = body.Mass[body.Parent[p]];
                    double ratio = a > b ? a / b : b / a;
                    if (ratio > worstRatio) worstRatio = ratio;
                }

                lines.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "      genome {0,3}  step {1,5}  parts {2,2}  dof {3,2}  mass {4:0.###}-{5:0.###} kg  " +
                    "worst joint ratio {6:0.##}  peak power {7:0.#} N m  smallest inertia {8:0.###e+00}",
                    i, lostAt[i], adult.PartCount, body.Dof, lightest, heaviest, worstRatio, power, thinnest));
            }

            Console.WriteLine($"--- alone, no contact, no glass: dt {dt}, {steps} steps " +
                              $"({steps * dt:0.#} s), {developed.Count} genomes");
            Console.WriteLine($"    lost {lost} of {developed.Count}");
            foreach (string line in lines) Console.WriteLine(line);
            Console.WriteLine();
        }

        // ---------------------------------------------------------------- (e) a recorded crowd

        /// <summary>
        /// The crowd a run recorded at one second, rebuilt where it stood: every living body's
        /// genome from the snapshot at that second, its root pose and joint angles from
        /// <c>poses.jsonl</c>, developed by Core and placed by the solver. Then what the contact
        /// grid makes of it and what a step costs, with the contact on, off, and on a forced cell.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>An approximation of the recorded crowd, and the entry says where.</b> A snapshot
        /// holds genomes and not module counts, so an indeterminate body is developed at its
        /// genome minimum, which is smaller than the chain the run was stepping; and no row holds
        /// how far a body had grown, so every body is at adult size. The first understates a
        /// module chain's reach, the second overstates a newborn's. Where the recorded joint
        /// count disagrees with the developed body's the angles are left at rest and the
        /// mismatch is counted.
        /// </para>
        /// <para>
        /// Built for round 44 seed 1's slowdown (logbook/0113): the solver's cost per body-step
        /// rose 7.6-fold with the overlap census and not with the link count, and no checkpoint
        /// existed to profile from.
        /// </para>
        /// </remarks>
        private static void Record(
            RunConfig config, string run, int at, int[] threadCounts, int warmup, int steps,
            double cellOverride, bool modulesAtMax, long dumpId, RecordOptions options)
        {
            bool compact = options.Format == RunRecordFormat.Compact;
            string snapshotPath = Path.Combine(
                run, "snapshots", at.ToString("000000000", CultureInfo.InvariantCulture) + ".jsonl");

            var genomes = new Dictionary<long, Genome>();
            var recordedCounts = new Dictionary<long, int[]>();
            var recordedLost = new Dictionary<long, List<int[]>>();
            var recordedFractions = new Dictionary<long, float>();
            int refused = 0, withPlan = 0;
            int snapshotRows = 0, withoutGenome = 0;
            string snapshotSource = "snapshots/" + Path.GetFileName(snapshotPath) + " (record format 1)";
            var readNotes = new List<string>();

            // The JSONL record's rows are the file's own lines. The compact record's are the slim rows joined to
            // their genomes, which RecordFiles.ReadSnapshot hands back as the old full rows, with each body's
            // growth fraction beside its row.
            IEnumerable<string> snapshotLines;
            float[] rowFractions = null;
            if (compact)
            {
                SnapshotRead snap = RecordFiles.ReadSnapshot(run, at, options.Format);
                if (snap == null)
                {
                    Console.Error.WriteLine("no snapshot at t=" + at + " in " + run + " (record format 2 names it snapshots/" +
                                            RecordFiles.SnapshotName(at, options.Format) + ")");
                    return;
                }

                snapshotLines = snap.Rows;
                rowFractions = snap.BodyFractions;
                withoutGenome = snap.WithoutAGenome;
                snapshotSource = snap.Source;
                readNotes.AddRange(snap.Notes);
            }
            else
            {
                snapshotLines = File.ReadLines(snapshotPath);
            }

            int rowIndex = -1;
            foreach (string line in snapshotLines)
            {
                rowIndex++;
                if (line.Length == 0) continue;
                snapshotRows++;
                try
                {
                    long id = GenomeJson.ReadId(line);
                    genomes[id] = GenomeJson.Read(line);
                    if (rowFractions != null) recordedFractions[id] = rowFractions[rowIndex];

                    // The body's own plan, on a row the farm wrote with it (2026-09-22 night).
                    int[] counts = GenomeJson.ReadModuleCounts(line);
                    List<int[]> lost = GenomeJson.ReadLostPartPaths(line);
                    if (counts != null) recordedCounts[id] = counts;
                    if (lost != null) recordedLost[id] = lost;
                    if (counts != null || lost != null) withPlan++;
                }
                catch (Exception) { refused++; }
            }

            // Every living body's root pose and joint angles at the second, from whichever file the record keeps
            // them in: poses.jsonl for the JSONL record, the state stream poses.bin for the compact one.
            var poseRows = new List<PoseRow>();
            string poseSource = "poses.jsonl";

            if (compact)
            {
                string stream = PoseStream.PathIn(run);
                if (stream == null)
                {
                    Console.Error.WriteLine("no " + PoseStream.FileName + " in " + run + ", which the compact record keeps its poses in");
                    return;
                }

                using (PoseStreamReader reader = PoseStreamReader.Open(stream))
                {
                    int found = reader.IndexOf(at);
                    if (found < 0 && reader.Header.CadenceSeconds > 0f)
                    {
                        // Frames sit on physics steps near their nominal second, so a second the cadence does not
                        // land on exactly is read from the nearest frame within half a cadence.
                        found = reader.IndexOf(at, 0.5 * reader.Header.CadenceSeconds);
                    }

                    if (found < 0)
                    {
                        Console.Error.WriteLine(
                            "no " + PoseStream.FileName + " frame at t=" + at + " (the stream holds " + reader.Frames.Length +
                            " frame(s), cadence " + reader.Header.CadenceSeconds.ToString(CultureInfo.InvariantCulture) + " s)");
                        return;
                    }

                    PoseFrame poseFrame = reader.Read(found);
                    poseSource = PoseStream.FileName + " frame at t=" + poseFrame.Seconds.ToString("0.###", CultureInfo.InvariantCulture) +
                                 " (stream version " + reader.Header.Version + ", cadence " +
                                 reader.Header.CadenceSeconds.ToString(CultureInfo.InvariantCulture) + " s, index " +
                                 (reader.IndexRead ? "read" : "rebuilt by a scan") + ")";

                    foreach (PoseBody poseBody in poseFrame.Bodies)
                    {
                        var angles = new double[poseBody.Joints.Length];
                        for (int d = 0; d < angles.Length; d++) angles[d] = poseBody.Joints[d];

                        poseRows.Add(new PoseRow
                        {
                            Id = poseBody.Id,
                            P = new Vec3(poseBody.X, poseBody.Y, poseBody.Z),
                            R = new QuatD(poseBody.Qx, poseBody.Qy, poseBody.Qz, poseBody.Qw),
                            Q = angles,
                            Fraction = poseBody.BodyFraction,
                        });
                    }
                }
            }
            else
            {
                string marker = "\"t\":" + at.ToString(CultureInfo.InvariantCulture) + ",";
                JsonNode frame = null;
                foreach (string line in File.ReadLines(Path.Combine(run, "poses.jsonl")))
                {
                    int head = line.Length < 24 ? line.Length : 24;
                    if (line.IndexOf(marker, 0, head, StringComparison.Ordinal) < 0) continue;
                    frame = Json.Parse(line);
                    break;
                }

                if (frame == null)
                {
                    Console.Error.WriteLine("no poses.jsonl row at t=" + at);
                    return;
                }

                JsonNode bodies = frame["bodies"];
                for (int i = 0; i < bodies.Count; i++)
                {
                    JsonNode body = bodies[i];
                    JsonNode p = body["p"], r = body["r"], q = body["q"];
                    var angles = new double[q.Count];
                    for (int d = 0; d < angles.Length; d++) angles[d] = q[d].AsDouble();

                    poseRows.Add(new PoseRow
                    {
                        Id = (long)body["id"].AsDouble(),
                        P = new Vec3(p[0].AsDouble(), p[1].AsDouble(), p[2].AsDouble()),
                        R = new QuatD(r[0].AsDouble(), r[1].AsDouble(), r[2].AsDouble(), r[3].AsDouble()),
                        Q = angles,
                        Fraction = float.NaN,   // poses.jsonl never carried a body fraction
                    });
                }
            }

            Console.WriteLine($"--- record: {run} at {at} s" + (modulesAtMax ? " (indeterminate nodes at their ceiling)" : withPlan > 0 ? " (module counts as recorded)" : " (module counts at the genome minimum)"));
            Console.WriteLine($"    genomes {genomes.Count} (refused {refused}, {withPlan} with a recorded plan), recorded bodies {poseRows.Count}");

            if (options.Extra)
            {
                Console.WriteLine($"    read from {snapshotSource}; poses from {poseSource}");
                foreach (string note in readNotes) Console.WriteLine("    note: " + note);
                Console.WriteLine(
                    $"    fin law (ShapedAddedMass) {(config.Fluid.ShapedAddedMass ? "on" : "off")}, " +
                    $"family {(options.FamilyNearest ? "nearest" : "off")}, repeats {options.Repeats}");
            }

            // Developed once and shared by every world built below, as the farm shares an adult.
            var plans = new List<(long id, Phenotype adult, Vec3 p, QuatD r, double[] q)>();
            int missing = 0, undeveloped = 0, dofMismatch = 0;

            // The pace read's counts and clocks. The build clock (develop here, construct in Build below) is kept
            // apart from the step's; the counts are what two arms of one read must share.
            int familyRefused = 0, fractionsNotRecorded = 0, builtJuvenile = 0, builtAdult = 0;
            string firstFamilyRefusal = null;
            var familyTally = new int[4];   // nodes made from a box, a sphere, a capsule; bodies whose root node is also a child
            int familyBodies = 0;
            var developWatch = new Stopwatch();
            var constructWatch = new Stopwatch();

            foreach (PoseRow row in poseRows)
            {
                long id = row.Id;

                if (!genomes.TryGetValue(id, out Genome genome)) { missing++; continue; }

                // The snapshot holds no module counts. At the genome minimum the body is what a
                // determinate lineage grows; at the ceiling it is the largest chain the rule could
                // have built, an upper bound on the reach the run's contact grid saw.
                int[] counts = null;
                recordedCounts.TryGetValue(id, out counts);
                if (modulesAtMax)
                {
                    counts = new int[genome.Nodes.Count];
                    for (int n = 0; n < counts.Length; n++)
                    {
                        MorphNode node = genome.Nodes[n];
                        counts[n] = node.Growth == ModuleGrowth.Indeterminate
                            ? System.Math.Max(node.MaxModules, node.RecursiveLimit)
                            : node.RecursiveLimit;
                    }
                }

                // --family nearest: the same body with each jointed node made the nearest member of the family,
                // on a copy; the genome read from the snapshot is never touched.
                Genome toDevelop = genome;
                if (options.FamilyNearest)
                {
                    toDevelop = NearestFamily(genome, familyTally, out string refusal);
                    if (toDevelop == null)
                    {
                        familyRefused++;
                        if (firstFamilyRefusal == null) firstFamilyRefusal = "body " + id + ": " + refusal;
                        continue;
                    }

                    familyBodies++;
                }

                // The growth fraction the record keeps for this body: the snapshot row's, else the state stream's.
                // Where neither has one the body is built at adult size and counted.
                float fraction = recordedFractions.TryGetValue(id, out float rowFraction) ? rowFraction : float.NaN;
                if (float.IsNaN(fraction)) fraction = row.Fraction;
                bool fractionRecorded = !float.IsNaN(fraction) && fraction > 0f;

                Phenotype adult;
                developWatch.Start();
                try
                {
                    var paths = new List<int[]>();
                    adult = Developer.Develop(toDevelop, config.Development, null, config.Shapes, counts, paths, config.CellTypes);

                    if (recordedLost.TryGetValue(id, out List<int[]> lost) && lost.Count > 0)
                    {
                        var drop = new bool[adult.PartCount];
                        bool any = false;
                        for (int k = 0; k < adult.PartCount; k++)
                        {
                            foreach (int[] path in lost)
                            {
                                if (paths[k].Length != path.Length) continue;
                                bool same = true;
                                for (int step = 0; step < path.Length && same; step++) same = paths[k][step] == path[step];
                                if (!same) continue;
                                drop[k] = true;
                                any = true;
                                break;
                            }
                        }
                        if (any) adult = adult.WithoutSubtrees(drop, out _);
                    }

                    // The body at the size it had, as the world makes a juvenile: the cube root of the fraction,
                    // because the fraction is a volume and the parts scale by a length.
                    if (options.Extra && fractionRecorded && fraction < 1f)
                    {
                        adult = adult.ScaledFor(config, (float)System.Math.Pow(fraction, 1d / 3d));
                    }
                }
                catch (Exception) { developWatch.Stop(); undeveloped++; continue; }
                developWatch.Stop();

                if (!fractionRecorded) fractionsNotRecorded++;
                else if (options.Extra && fraction < 1f) builtJuvenile++;
                else builtAdult++;

                plans.Add((id, adult, row.P, row.R, row.Q));
            }

            Console.WriteLine($"    rebuilt {plans.Count}: no genome {missing}, undeveloped {undeveloped}");

            if (options.Extra)
            {
                // What two arms of one read must share. A body the family refuses is one the other arm built, so a
                // nonzero count here is a comparison of different crowds.
                int refusedBodies = withoutGenome + missing + familyRefused + undeveloped;
                Console.WriteLine(
                    $"    arm counts: bodies read {poseRows.Count} (snapshot rows {snapshotRows}), built {plans.Count}, " +
                    $"refused {refusedBodies} (no genome in the join {withoutGenome}, no genome for a pose {missing}, " +
                    $"family refused {familyRefused}, undeveloped {undeveloped}), genome rows unreadable {refused}");
                Console.WriteLine(
                    $"    growth: built at a recorded fraction below 1 {builtJuvenile}, recorded as 1 or more {builtAdult}, " +
                    $"fraction not recorded and built at adult size {fractionsNotRecorded}");

                if (options.FamilyNearest)
                {
                    Console.WriteLine(
                        $"    family nearest: {familyBodies} bodies rewritten, jointed nodes made from a box {familyTally[0]}, " +
                        $"a sphere {familyTally[1]}, a capsule {familyTally[2]}; bodies whose root node is also a child " +
                        $"(its jointed copies keep the root's shape) {familyTally[3]}");
                    if (firstFamilyRefusal != null) Console.WriteLine("    family refusal, first of " + familyRefused + ": " + firstFamilyRefusal);
                }
            }

            if (dumpId >= 0)
            {
                foreach ((long id, Phenotype adult, Vec3 p, QuatD r, double[] q) in plans)
                {
                    if (id != dumpId) continue;
                    Genome g = genomes[id];
                    Console.WriteLine($"    body {id}: adultScale {g.AdultScale:0.###}, {g.Nodes.Count} nodes, {adult.PartCount} parts, root at ({p.X:0.##}, {p.Y:0.##}, {p.Z:0.##})");
                    for (int n = 0; n < g.Nodes.Count; n++)
                    {
                        MorphNode node = g.Nodes[n];
                        Console.WriteLine($"      node {n}: {node.CellTypeId} {node.ShapeId} growth {node.Growth} limit {node.RecursiveLimit} maxModules {node.MaxModules} dims ({node.Dimensions.X:0.###}, {node.Dimensions.Y:0.###}, {node.Dimensions.Z:0.###}) edges {node.Edges.Count}");
                    }
                    for (int i = 0; i < adult.PartCount; i++)
                    {
                        PhenotypePart part = adult.Parts[i];
                        Quat rot = part.Rotation;
                        Console.WriteLine($"      part {i,2}: node {part.SourceNode} parent {part.ParentIndex} depth {part.Depth} {part.JointType} at ({part.Position.X:0.###}, {part.Position.Y:0.###}, {part.Position.Z:0.###}) half-extents ({part.HalfExtents.X:0.###}, {part.HalfExtents.Y:0.###}, {part.HalfExtents.Z:0.###}) rot ({rot.X:0.####}, {rot.Y:0.####}, {rot.Z:0.####}, {rot.W:0.####}){(part.Mirrored ? " mirrored" : "")}");
                    }
                }
            }

            DynamicsWorld Build(int threads, bool contact, bool instrument, double cell)
            {
                SolverConfig solver = Configure(config, 0.01);
                solver.CreatureContact = contact;
                solver.ContactInstrument = instrument;
                solver.ContactEvents = instrument;

                var world = new DynamicsWorld(solver) { Threads = threads, ContactCellOverrideMetres = cell };
                dofMismatch = 0;
                constructWatch.Reset();

                foreach ((long id, Phenotype adult, Vec3 p, QuatD r, double[] q) in plans)
                {
                    constructWatch.Start();
                    var creature = new Creature((int)id, adult, solver, config.Shapes);
                    creature.PlaceAt(p, r);

                    if (q.Length == creature.Dof)
                    {
                        Array.Copy(q, creature.Q, q.Length);
                        Kinematics.Refresh(creature);
                        creature.RefreshContactSphere();
                        creature.CommitContactSphere();
                    }
                    else dofMismatch++;
                    constructWatch.Stop();

                    world.AddInIdOrder(creature);
                }

                return world;
            }

            // The crowd as the grid sees it, on the rule's cell and on the forced one.
            DynamicsWorld probe = Build(1, true, true, cellOverride);
            Console.WriteLine($"    joint angles applied to {plans.Count - dofMismatch}, at rest for {dofMismatch} (dof mismatch)");

            // The build, apart from the step: Developer.Develop and the scaling in the loop above, plus the body's
            // construction in the probe world just made, summed over the crowd.
            double buildMicrosPerBody = plans.Count == 0
                ? 0
                : (developWatch.Elapsed.TotalMilliseconds + constructWatch.Elapsed.TotalMilliseconds) * 1000.0 / plans.Count;
            if (options.Extra)
            {
                Console.WriteLine(
                    $"    build: {buildMicrosPerBody:0.#} us/body over {plans.Count} bodies " +
                    $"(develop {(plans.Count == 0 ? 0 : developWatch.Elapsed.TotalMilliseconds * 1000.0 / plans.Count):0.#}, " +
                    $"construct {(plans.Count == 0 ? 0 : constructWatch.Elapsed.TotalMilliseconds * 1000.0 / plans.Count):0.#}; " +
                    "one stopwatch each, summed, the first call of each included)");
                Console.WriteLine(
                    $"    ARMCOUNTS read={poseRows.Count} refused={withoutGenome + missing + familyRefused + undeveloped} " +
                    $"atRest={dofMismatch} fractionsNotRecorded={fractionsNotRecorded}");
            }

            var radii = new List<double>();
            int links = 0, dof = 0, largestLinks = 0, jointedBodies = 0;
            long largestId = -1;
            double largest = 0;
            foreach (Creature c in probe.Creatures)
            {
                radii.Add(c.ContactRadius);
                links += c.Links;
                dof += c.Dof;
                if (c.Dof > 0) jointedBodies++;
                if (c.ContactRadius > largest) { largest = c.ContactRadius; largestId = c.Id; largestLinks = c.Links; }
            }
            radii.Sort();
            double At(double f) => radii.Count == 0 ? 0 : radii[(int)System.Math.Min(radii.Count - 1, f * radii.Count)];

            Console.WriteLine($"    links per body {(double)links / plans.Count:0.##}, dof per body {(double)dof / plans.Count:0.###}");
            if (options.Extra) Console.WriteLine($"    jointed bodies {jointedBodies} of {plans.Count}, {dof} degrees of freedom between them");
            Console.WriteLine($"    bounding radius: p50 {At(0.5):0.###} m, p90 {At(0.9):0.###}, p99 {At(0.99):0.###}, " +
                              $"max {largest:0.###} (id {largestId}, {largestLinks} links)");

            var grid = new ContactGrid();
            grid.Build(probe.Creatures, cellOverride);
            int[] scratch = new int[64];
            long neighbours = 0, mostNeighbours = 0;
            for (int i = 0; i < probe.Creatures.Count; i++)
            {
                int n = grid.Neighbours(i, ref scratch);
                neighbours += n;
                if (n > mostNeighbours) mostNeighbours = n;
            }

            Console.WriteLine($"    grid cell {grid.CellSize:0.###} m ({(cellOverride > 0 ? "forced" : "2 x mean radius")}, {grid.Entries} entries): " +
                              $"neighbours per body mean {(double)neighbours / probe.Creatures.Count:0.#}, max {mostNeighbours}");

            probe.Step();
            Console.WriteLine($"    first step: overlapping pairs {probe.OverlapPairsThisStep}, " +
                              $"bodies touching {probe.OverlapBodiesThisStep}, at bed or glass {probe.BedOrGlassBodiesThisStep}");
            Console.WriteLine();

            Console.WriteLine($"    {warmup} warm-up then {steps} timed steps, dt 0.01");
            Console.WriteLine("    variant                    threads   us/body-step   steps/s   x real time   pairs/step" +
                              (options.Extra ? "   ms/step   us/step per jointed body" : ""));

            void Time(string name, bool contact, bool instrument, double cell)
            {
                foreach (int threads in threadCounts)
                {
                    DynamicsWorld world = Build(threads, contact, instrument, cell);
                    int bodyCount = world.Creatures.Count;

                    for (int step = 0; step < warmup; step++) world.Step();

                    // One pass is the read as it always was. --repeats times the same steps again on the same world,
                    // each pass with its own collection before it and its own phase shares.
                    var passMicros = new List<double>();

                    for (int pass = 0; pass < options.Repeats; pass++)
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();

                        // The read as it always was takes its phase shares since the world was made, warm-up included
                        // (a zero baseline); a pace read takes them for the pass alone.
                        long[] phasesBefore = options.Extra
                            ? (long[])world.PhaseTicks.Clone()
                            : new long[world.PhaseTicks.Length];

                        long pairsBefore = world.OverlapPairs;
                        var watch = Stopwatch.StartNew();
                        for (int step = 0; step < steps; step++) world.Step();
                        watch.Stop();

                        double micros = watch.Elapsed.TotalMilliseconds * 1000.0;
                        double stepsPerSecond = steps / watch.Elapsed.TotalSeconds;
                        passMicros.Add(micros);

                        long phaseTotal = 0;
                        for (int p = 0; p < world.PhaseTicks.Length; p++) phaseTotal += world.PhaseTicks[p] - phasesBefore[p];
                        var phases = new System.Text.StringBuilder();
                        for (int p = 0; p < world.PhaseTicks.Length; p++)
                        {
                            phases.Append(p == 0 ? "   " : " ").Append(DynamicsWorld.PhaseNames[p]).Append(' ')
                                .Append((phaseTotal > 0 ? 100.0 * (world.PhaseTicks[p] - phasesBefore[p]) / phaseTotal : 0).ToString("0", CultureInfo.InvariantCulture))
                                .Append('%');
                        }

                        string label = options.Extra && options.Repeats > 1 ? name + " #" + (pass + 1) : name;

                        Console.WriteLine(
                            $"    {label,-26} {threads,7}   {micros / ((double)steps * bodyCount),12:0.###}   " +
                            $"{stepsPerSecond,7:0}   {stepsPerSecond * 0.01,11:0.##}   " +
                            $"{(world.OverlapPairs - pairsBefore) / (double)steps,10:0.#}" +
                            (options.Extra ? StepColumns(micros, steps, jointedBodies) : "") + phases);
                    }

                    if (options.Extra)
                    {
                        // The median of the passes, by the step's time; the line a script reads beside the table.
                        double median = Median(passMicros);
                        double medianStepsPerSecond = steps / (median / 1e6);

                        if (options.Repeats > 1)
                        {
                            string medianLabel = name + " median";
                            string noPairs = "";

                            Console.WriteLine(
                                $"    {medianLabel,-26} {threads,7}   {median / ((double)steps * bodyCount),12:0.###}   " +
                                $"{medianStepsPerSecond,7:0}   {medianStepsPerSecond * 0.01,11:0.##}   " +
                                $"{noPairs,10}" + StepColumns(median, steps, jointedBodies));
                        }

                        var eachPass = new List<string>();
                        foreach (double each in passMicros)
                        {
                            eachPass.Add((each / steps / 1000.0).ToString("0.###", CultureInfo.InvariantCulture));
                        }

                        string passList = string.Join(",", eachPass);
                        double medianMillis = median / steps / 1000.0;
                        double medianPerJointed = jointedBodies > 0 ? median / steps / jointedBodies : 0;

                        Console.WriteLine(
                            $"    PACEREAD variant={name}|threads={threads}|repeats={options.Repeats}|bodies={bodyCount}|jointed={jointedBodies}" +
                            $"|median_ms_per_step={medianMillis:0.###}|us_per_step_per_jointed={medianPerJointed:0.##}" +
                            $"|build_us_per_body={buildMicrosPerBody:0.#}|pass_ms_per_step={passList}");
                    }
                }
            }

            Time("contact on, instrument on", true, true, cellOverride);

            // The farm's own variant alone: contact on and the instrument on, as Simulation sets them. The other
            // six variants are the contact investigation's, and cost six times the pace read's machine time.
            if (options.FarmVariantOnly)
            {
                Console.WriteLine();
                return;
            }

            Time("contact on, instrument off", true, false, cellOverride);
            Time("contact off", false, false, cellOverride);

            if (cellOverride <= 0)
            {
                foreach (double cell in new[] { 2.0, 4.0, 8.0, 12.0 })
                {
                    Time($"cell forced {cell:0} m", true, true, cell);
                }
            }

            Console.WriteLine();
        }

        // ---------------------------------------------------------------- the pace read's additions

        /// <summary>What the record mode is asked for beyond its old self; every field's default is the old read.</summary>
        private sealed class RecordOptions
        {
            /// <summary><see cref="RunRecordFormat"/> of the run: the JSONL record unless the record mode found a compact one.</summary>
            public int Format = RunRecordFormat.Jsonl;

            /// <summary><c>--family nearest</c>: every jointed node but the root is made the nearest member of the superellipsoid family.</summary>
            public bool FamilyNearest;

            /// <summary><c>--repeats</c>: how many times the timed steps are run and reported.</summary>
            public int Repeats = 1;

            /// <summary><c>--variant farm</c>: time the farm's own variant and no other.</summary>
            public bool FarmVariantOnly;

            /// <summary>
            /// Whether any pace-read output is wanted: the build clock, the arm counts, the per-jointed-body column.
            /// False for a JSONL run with none of the new flags, so that read prints what it always printed.
            /// </summary>
            public bool Extra;
        }

        /// <summary>One living body's recorded pose, from either file the record keeps poses in.</summary>
        private struct PoseRow
        {
            public long Id;
            public Vec3 P;
            public QuatD R;
            public double[] Q;

            /// <summary>The state stream's body fraction, or NaN (the JSONL poses never carried one).</summary>
            public float Fraction;
        }

        /// <summary>
        /// The step's time in milliseconds and per jointed body, as the two columns the pace read adds beside the
        /// step's own.
        /// </summary>
        private static string StepColumns(double microsOfAllSteps, int steps, int jointedBodies)
        {
            double perStep = microsOfAllSteps / steps;
            double perJointed = jointedBodies > 0 ? perStep / jointedBodies : 0;
            return string.Format(
                CultureInfo.InvariantCulture, "   {0,7:0.###} ms   {1,10:0.##} us/jointed", perStep / 1000.0, perJointed);
        }

        private static double Median(List<double> values)
        {
            var sorted = new List<double>(values);
            sorted.Sort();
            int n = sorted.Count;
            if (n == 0) return 0;
            return n % 2 == 1 ? sorted[n / 2] : 0.5 * (sorted[n / 2 - 1] + sorted[n / 2]);
        }

        /// <summary>
        /// A copy of a genome with every jointed node but the root made the nearest member of the superellipsoid
        /// family, or null, with the reason, when a node's shape has no nearest member.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The nearest member is the family's own limit: a box is the profile and section at their smallest and a
        /// taper of 1, a sphere is all three genes at 1, and a capsule is a profile of one half. The genes are in
        /// <see cref="PartForm"/>'s order, profile, section, taper. The half-extents stay as the genome has them, so
        /// a node's parts keep their size and only their shape changes.
        /// </para>
        /// <para>
        /// <b>At the node, before development</b>, so the root node is left alone whole: it has no joint and no push.
        /// A root node that is also some edge's child keeps its shape in those jointed copies too, which is counted in
        /// <paramref name="tally"/>[3] and said in the report. The caller's genome is never written; the copy is.
        /// </para>
        /// </remarks>
        /// <param name="tally">Nodes made from a box, a sphere and a capsule, then bodies whose root node is also a child.</param>
        private static Genome NearestFamily(Genome source, int[] tally, out string refusal)
        {
            refusal = null;

            // All of the checks come before the copy, so a refused body counts nothing in the tally.
            for (int n = 0; n < source.Nodes.Count; n++)
            {
                MorphNode node = source.Nodes[n];
                if (n == source.RootIndex || node.JointType == JointType.Fixed) continue;

                if (node.ShapeId != ShapeIds.Box && node.ShapeId != ShapeIds.Sphere && node.ShapeId != ShapeIds.Capsule)
                {
                    refusal = "node " + n + " carries a " + node.JointType + " joint and its shape is '" + node.ShapeId +
                              "', which is not a box, a sphere or a capsule and has no nearest family member";
                    return null;
                }
            }

            Genome copy = source.Clone();

            for (int n = 0; n < copy.Nodes.Count; n++)
            {
                MorphNode node = copy.Nodes[n];
                if (n == copy.RootIndex || node.JointType == JointType.Fixed) continue;

                PartForm form;
                if (node.ShapeId == ShapeIds.Box)
                {
                    form = new PartForm(0.1f, 0.1f, 1f);
                    tally[0]++;
                }
                else if (node.ShapeId == ShapeIds.Sphere)
                {
                    form = new PartForm(1f, 1f, 1f);
                    tally[1]++;
                }
                else
                {
                    form = new PartForm(0.5f, 1f, 1f);
                    tally[2]++;
                }

                node.ShapeId = ShapeIds.Superellipsoid;
                node.Form = form;
            }

            bool rootIsAlsoAChild = false;
            for (int n = 0; n < source.Nodes.Count && !rootIsAlsoAChild; n++)
            {
                foreach (MorphEdge edge in source.Nodes[n].Edges)
                {
                    if (edge.Child == source.RootIndex) { rootIsAlsoAChild = true; break; }
                }
            }
            if (rootIsAlsoAChild) tally[3]++;

            return copy;
        }

        // ---------------------------------------------------------------- (b) pace

        private static void Pace(
            RunConfig config, List<Phenotype> developed, int bodies, int[] threadCounts,
            int warmup, int steps)
        {
            Console.WriteLine($"--- pace, dt 0.01, {bodies} bodies, {warmup} warm-up then {steps} timed steps");
            Console.WriteLine("    threads   us/body-step   us/link-step   steps/s   x real time   alive");

            foreach (int threads in threadCounts)
            {
                DynamicsWorld world = Populate(config, developed, bodies, 0.01, threads);
                int links = world.TotalLinks();

                for (int step = 0; step < warmup; step++) world.Step();

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                var watch = Stopwatch.StartNew();
                for (int step = 0; step < steps; step++) world.Step();
                watch.Stop();

                double micros = watch.Elapsed.TotalMilliseconds * 1000.0;
                double perBodyStep = micros / ((double)steps * bodies);
                double perLinkStep = micros / ((double)steps * links);
                double stepsPerSecond = steps / watch.Elapsed.TotalSeconds;

                Console.WriteLine(
                    $"    {threads,7}   {perBodyStep,12:0.###}   {perLinkStep,12:0.###}   " +
                    $"{stepsPerSecond,7:0}   {stepsPerSecond * 0.01,11:0.##}   " +
                    $"{bodies - world.NonFiniteBodies(),5}");
            }

            Console.WriteLine();
        }

        // ---------------------------------------------------------------- (c) identity

        private static void Identity(RunConfig config, List<Phenotype> developed, int bodies, int steps)
        {
            Console.WriteLine($"--- identity, dt 0.01, {bodies} bodies, {steps} steps");

            ulong first = 0;
            bool agree = true;

            foreach (int threads in new[] { 1, 4, 16 })
            {
                DynamicsWorld world = Populate(config, developed, bodies, 0.01, threads);
                for (int step = 0; step < steps; step++) world.Step();

                ulong digest = world.Digest();
                if (threads == 1) first = digest;
                else if (digest != first) agree = false;

                Console.WriteLine($"    {threads,2} thread(s)   {digest:x16}   " +
                                  $"non-finite {world.NonFiniteBodies()}");
            }

            Console.WriteLine("    " + (agree ? "IDENTICAL" : "*** THE THREAD COUNT MOVED THE TRAJECTORY ***"));
            Console.WriteLine();
        }

        // ---------------------------------------------------------------- the parity probe

        /// <summary>
        /// The steps a probe line is written on — the same six on both sides of the parity
        /// comparison, so the two logs can be diffed line for line.
        /// </summary>
        private static bool IsProbeStep(int step) =>
            step == 0 || step == 1 || step == 10 || step == 100 || step == 1000 || step == 5999;

        /// <summary>
        /// The channels a probe reports, in this order on both sides. Chemical and Energy are
        /// included although neither engine models them here — both answer the parity constant,
        /// and a line that left them out could not show that they had.
        /// </summary>
        private static readonly SensorChannel[] ProbeChannels =
        {
            SensorChannel.Depth,
            SensorChannel.OrientationUp,
            SensorChannel.JointAngle,
            SensorChannel.JointAngularVelocity,
            SensorChannel.Chemical,
            SensorChannel.Energy,
            SensorChannel.Flow,
        };

        /// <summary>
        /// One line of everything that crosses between perception, the brain, the drive and the
        /// solver, in the format <c>ParitySwim</c> writes it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Emitted after the step, reporting that step's inputs and the state it produced.</b>
        /// So <c>step=0</c> carries the sensors read from the initial state, the drive the brain
        /// emitted from them, and the pose one step later — which is the comparison that matters,
        /// because at step 0 the two engines are handed the same body in the same place and any
        /// difference in <c>sensors=</c> or <c>drive=</c> is a port error and nothing else.
        /// </para>
        /// <para>
        /// Only channels this creature's <c>Brain.SensorMask</c> actually reads are printed. A
        /// channel no neuron references is never computed by either engine, so printing it would
        /// be comparing two arrays that both happen to hold whatever they were left holding.
        /// </para>
        /// </remarks>
        private static void Probe(
            StringBuilder into, int genome, int step, Phenotype adult, Creature body)
        {
            var sensors = new StringBuilder();
            int mask = body.Brain.SensorMask;

            for (int p = 0; p < body.Links; p++)
            {
                foreach (SensorChannel channel in ProbeChannels)
                {
                    if (!Brain.MaskReads(mask, channel)) continue;

                    int count = channel == SensorChannel.Flow ? 3
                        : channel == SensorChannel.JointAngle ||
                          channel == SensorChannel.JointAngularVelocity
                            ? adult.Parts[p].JointType.DofCount()
                            : 1;

                    for (int k = 0; k < count; k++)
                    {
                        if (sensors.Length > 0) sensors.Append(',');
                        sensors.Append(p).Append(':').Append(channel);
                        if (count > 1) sensors.Append('[').Append(k).Append(']');
                        sensors.Append('=').Append(
                            body.Senses.Read(p, channel, k)
                                .ToString("0.######", CultureInfo.InvariantCulture));
                    }
                }
            }

            into.Append("[ParitySwim] probe genome=").Append(genome)
                .Append(" step=").Append(step)
                .Append(" sensors=[").Append(sensors).Append(']')
                .Append(" drive=[").Append(Numbers(body.DriveSignal, body.Dof)).Append(']')
                .Append(" torque=[").Append(Numbers(body.Drive.AppliedTorque, 3 * body.Links)).Append(']')
                .Append(" q=[").Append(Numbers(body.Q, body.Dof)).Append(']')
                .Append(" qd=[").Append(Numbers(body.Qd, body.Dof)).Append(']')
                .AppendLine();
        }

        /// <summary>One link's per-dof limits, for the parity table's "which stop did it reach".</summary>
        private static string LimitText(Creature body, int link)
        {
            var text = new StringBuilder();
            int at = body.DofStart[link];
            for (int d = 0; d < body.DofCount[link]; d++)
            {
                if (d > 0) text.Append('/');
                text.Append(body.LimitHi[at + d].ToString("0.###", CultureInfo.InvariantCulture));
            }
            return text.ToString();
        }

        private static string Numbers(double[] values, int count)
        {
            var text = new StringBuilder();
            for (int i = 0; i < count && i < values.Length; i++)
            {
                if (i > 0) text.Append(',');
                text.Append(values[i].ToString("0.######", CultureInfo.InvariantCulture));
            }
            return text.ToString();
        }

        private static string Numbers(float[] values, int count)
        {
            var text = new StringBuilder();
            for (int i = 0; i < count && i < values.Length; i++)
            {
                if (i > 0) text.Append(',');
                text.Append(values[i].ToString("0.######", CultureInfo.InvariantCulture));
            }
            return text.ToString();
        }

        // ---------------------------------------------------------------- (d) trajectories

        /// <summary>
        /// One genome at a time, alone in still water, under its own brain: the root's place and
        /// every joint angle every 0.1 s, for a later comparison against PhysX.
        /// </summary>
        private static void Trajectories(
            RunConfig config, List<Phenotype> developed, List<Genome> genomes,
            string directory, int wanted, double seconds)
        {
            Directory.CreateDirectory(directory);
            Console.WriteLine($"--- trajectories, dt 0.01, alone in still water, {seconds:0} s each");

            int written = 0;

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

                SolverConfig solver = Configure(config, 0.01);
                solver.CreatureContact = false;
                solver.TankRadiusMetres = 0;   // alone, so the glass is not part of the question

                var body = new Creature(0, adult, solver, config.Shapes);

                // As PhenotypeBuilder puts it down, not upright at the origin: see
                // Creature.PlaceAsDeveloped for why the difference reaches the brain.
                body.PlaceAsDeveloped(new Vec3(0, -10, 0), adult);

                var world = new DynamicsWorld(solver);
                world.Add(body);

                var probe = new StringBuilder();

                var text = new StringBuilder();
                text.Append("t,x,y,z,qw,qx,qy,qz");
                for (int d = 0; d < body.Dof; d++) text.Append(",q").Append(d);
                text.AppendLine();

                int steps = (int)System.Math.Round(seconds / solver.StepSeconds);
                int every = (int)System.Math.Round(0.1 / solver.StepSeconds);

                for (int step = 0; step <= steps; step++)
                {
                    if (step % every == 0) Sample(text, world.ElapsedSeconds, body);
                    if (step < steps)
                    {
                        world.Step();
                        if (IsProbeStep(step)) Probe(probe, i, step, adult, body);
                    }
                }

                string name = Path.Combine(
                    directory,
                    FormattableString.Invariant(
                        $"r42-s4-20000-genome{i:00}-parts{adult.PartCount}-dof{body.Dof}.csv"));

                File.WriteAllText(name, text.ToString());

                if (probe.Length > 0)
                {
                    string probeDirectory = Path.Combine(
                        Path.GetDirectoryName(directory) ?? directory, "probe");
                    Directory.CreateDirectory(probeDirectory);
                    File.WriteAllText(
                        Path.Combine(
                            probeDirectory,
                            FormattableString.Invariant($"bench-genome{i:00}.txt")),
                        probe.ToString());
                    Console.Write(probe.ToString());
                }

                written++;

                Console.WriteLine(
                    $"    {Path.GetFileName(name)}   parts {adult.PartCount}, dof {body.Dof}, " +
                    $"net displacement {(body.CentreOfMass() - new Vec3(0, -10, 0)).Magnitude:0.####} m, " +
                    (body.Alive ? "finite" : "LOST"));
            }

            if (written == 0) Console.WriteLine("    no multi-link jointed genome in this snapshot");
            Console.WriteLine();
        }

        private static void Sample(StringBuilder text, double t, Creature body)
        {
            text.Append(t.ToString("0.###", CultureInfo.InvariantCulture));
            text.Append(',').Append(body.Position[0].ToString("0.######", CultureInfo.InvariantCulture));
            text.Append(',').Append(body.Position[1].ToString("0.######", CultureInfo.InvariantCulture));
            text.Append(',').Append(body.Position[2].ToString("0.######", CultureInfo.InvariantCulture));
            text.Append(',').Append(body.Rotation[3].ToString("0.######", CultureInfo.InvariantCulture));
            text.Append(',').Append(body.Rotation[0].ToString("0.######", CultureInfo.InvariantCulture));
            text.Append(',').Append(body.Rotation[1].ToString("0.######", CultureInfo.InvariantCulture));
            text.Append(',').Append(body.Rotation[2].ToString("0.######", CultureInfo.InvariantCulture));

            for (int d = 0; d < body.Dof; d++)
            {
                text.Append(',').Append(body.Q[d].ToString("0.######", CultureInfo.InvariantCulture));
            }

            text.AppendLine();
        }

        // ---------------------------------------------------------------- reading

        private static List<Genome> ReadGenomes(
            string path, int limit, out int refused, out string firstRefusal)
        {
            var genomes = new List<Genome>();
            refused = 0;
            firstRefusal = null;

            foreach (string line in File.ReadLines(path))
            {
                if (line.Length == 0) continue;
                if (genomes.Count >= limit) break;

                try { genomes.Add(GenomeJson.Read(line)); }
                catch (Exception ex)
                {
                    refused++;
                    if (firstRefusal == null)
                    {
                        firstRefusal = ex.GetType().Name + ": " + FirstLine(ex.Message);
                    }
                }
            }

            return genomes;
        }

        private static string Describe(List<Phenotype> developed)
        {
            int links = 0, jointed = 0, dof = 0, most = 0;
            var histogram = new SortedDictionary<int, int>();

            foreach (Phenotype body in developed)
            {
                links += body.PartCount;
                dof += body.TotalDof;
                if (body.PartCount > most) most = body.PartCount;
                if (body.TotalDof > 0) jointed++;

                histogram.TryGetValue(body.PartCount, out int n);
                histogram[body.PartCount] = n + 1;
            }

            var parts = new List<string>();
            foreach (KeyValuePair<int, int> entry in histogram)
            {
                parts.Add(entry.Key + "->" + entry.Value);
            }

            double meanLinks = (double)links / developed.Count;
            double meanDof = (double)dof / developed.Count;

            return "  mean links per body " + meanLinks.ToString("0.###", CultureInfo.InvariantCulture) +
                   ", largest " + most +
                   ", mean dof " + meanDof.ToString("0.###", CultureInfo.InvariantCulture) +
                   ", jointed " + jointed + " of " + developed.Count +
                   Environment.NewLine +
                   "  parts histogram: " + string.Join(", ", parts);
        }

        private static string FirstLine(string s)
        {
            int n = s.IndexOf('\n');
            return n < 0 ? s : s.Substring(0, n).TrimEnd('\r');
        }
    }
}
