using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Evosim.Core;
using Evosim.Dynamics;

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
            @"D:\Projects\experiments\evolution-simulator\runs\r42-s4\2026-09-21-051718-ff557bce";

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

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--run": run = args[++i]; break;
                    case "--snapshot": snapshot = args[++i]; break;
                    case "--bodies": bodies = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--genomes": genomeLimit = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--mode": mode = args[++i]; break;
                    case "--traj": trajectoryDirectory = args[++i]; break;
                    case "--stability-steps": stabilitySteps = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--pace-steps": paceSteps = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--warmup": warmup = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--no-contact": _noContact = true; break;
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

            string configPath = Path.Combine(run, "config.json");
            string snapshotPath = Path.Combine(run, "snapshots", snapshot);

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
                try { developed.Add(Developer.Develop(genome, config.Development, null, config.Shapes)); }
                catch (Exception) { undeveloped++; }
            }

            Console.WriteLine($"  developed {developed.Count}, refused {undeveloped}");
            Console.WriteLine(Describe(developed));
            Console.WriteLine();

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

            if (mode == "diagnose")
            {
                Diagnose(config, developed, stabilitySteps > 0 ? stabilitySteps : 6000, 0.01);
                Diagnose(config, developed, stabilitySteps > 0 ? stabilitySteps : 6000, 0.02);
            }

            if (mode == "all" || mode == "traj")
            {
                Trajectories(config, developed, genomes, trajectoryDirectory, 5, 60.0);
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

        private static SolverConfig Configure(RunConfig config, double dt)
        {
            SolverConfig solver = SolverConfig.From(config, dt);

            solver.TankRadiusMetres = config.WorldShape == WorldShape.Tank && !_noWall
                ? System.Math.Sqrt(config.WorldAreaSquareMetres / System.Math.PI)
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

                body.PlaceAt(
                    new Vec3(
                        radius * System.Math.Cos(theta),
                        -ScatterDepth * rng.NextFloat(),
                        radius * System.Math.Sin(theta)),
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
                body.PlaceAt(new Vec3(0, -10, 0), QuatD.Identity);

                var world = new DynamicsWorld(solver);
                world.Add(body);

                var text = new StringBuilder();
                text.Append("t,x,y,z,qw,qx,qy,qz");
                for (int d = 0; d < body.Dof; d++) text.Append(",q").Append(d);
                text.AppendLine();

                int steps = (int)System.Math.Round(seconds / solver.StepSeconds);
                int every = (int)System.Math.Round(0.1 / solver.StepSeconds);

                for (int step = 0; step <= steps; step++)
                {
                    if (step % every == 0) Sample(text, world.ElapsedSeconds, body);
                    if (step < steps) world.Step();
                }

                string name = Path.Combine(
                    directory,
                    FormattableString.Invariant(
                        $"r42-s4-20000-genome{i:00}-parts{adult.PartCount}-dof{body.Dof}.csv"));

                File.WriteAllText(name, text.ToString());
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
