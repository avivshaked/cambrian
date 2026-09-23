using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Evosim.Core;
using Evosim.Dynamics;
using Gpu.Spike3;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.CPU;
using ILGPU.Algorithms;

namespace Gpu.Spike4
{
    /// <summary>
    /// The fourth spike: the whole body phase of <see cref="DynamicsWorld.Step"/> as one ILGPU
    /// kernel a step, with the third spike's grid kernels in front of it and the commit as a swap
    /// of two sphere buffers behind it.
    /// </summary>
    internal static class StepSpike
    {
        internal const string ScratchDir = @"D:\Projects\experiments\evolution-simulator\scratch\gpu-spike4";

        private static readonly StringBuilder Log = new StringBuilder();

        // The farm's physics step: Simulation takes it as a float (Evosim.Farm/Program.cs:183-187,
        // Simulation.cs:194), so the solver's StepSeconds is (double)0.01f.
        private const double FarmDt = (double)0.01f;
        private const double StartSeconds = 30000.0;
        private const long StartSteps = 3_000_000;

        public static int Run(string[] args)
        {
            string run = ContactSpike.DefaultRun;
            int at = 30000;
            int checkSteps = 1000;
            int cardSteps = 1000;
            int timeSteps = 500;
            int cpuSteps = 100;
            int cpuSteps1 = 20;
            int repeats = 3;
            int threads = 16;
            int group = 32;
            int group2 = 64;
            int overCap = 64;
            int block = 50;
            double dtArg = FarmDt;
            bool limitAlways = false;
            int poison = 0;
            var sizes = new List<int> { 0, 10000, 30000 };
            bool doCheck = true, doCard = true, doTime = true;
            string tag = "wholestep";

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--run": run = args[++i]; break;
                    case "--at": at = ContactSpike.Int(args[++i]); break;
                    case "--check-steps": checkSteps = ContactSpike.Int(args[++i]); break;
                    case "--card-steps": cardSteps = ContactSpike.Int(args[++i]); break;
                    case "--time-steps": timeSteps = ContactSpike.Int(args[++i]); break;
                    case "--cpu-steps": cpuSteps = ContactSpike.Int(args[++i]); break;
                    case "--cpu-steps-1": cpuSteps1 = ContactSpike.Int(args[++i]); break;
                    case "--repeats": repeats = ContactSpike.Int(args[++i]); break;
                    case "--threads": threads = ContactSpike.Int(args[++i]); break;
                    case "--group": group = ContactSpike.Int(args[++i]); break;
                    case "--group2": group2 = ContactSpike.Int(args[++i]); break;
                    case "--over-cap": overCap = ContactSpike.Int(args[++i]); break;
                    case "--dt": dtArg = (double)float.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--drive-limit-always": limitAlways = true; break;
                    case "--poison": poison = ContactSpike.Int(args[++i]); break;
                    case "--sizes": sizes = args[++i].Split(',').Select(ContactSpike.Int).ToList(); break;
                    case "--no-check": doCheck = false; break;
                    case "--no-card": doCard = false; break;
                    case "--no-time": doTime = false; break;
                    case "--tag": tag = args[++i]; break;
                    default: Console.Error.WriteLine("unknown option " + args[i]); return 2;
                }
            }

            Directory.CreateDirectory(ScratchDir);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

            Say("=== the whole body phase in one kernel");
            Say("  command     " + Environment.CommandLine);
            Say("  host        " + Environment.MachineName + ", " + Environment.ProcessorCount +
                " logical processors, .NET " + Environment.Version);
            Say("  run         " + run + " at " + at + " s");

            Crowd3 crowd = Crowd3.Load(run, at);
            Say($"  crowd       genomes {crowd.GenomesRead}, rebuilt {crowd.Plans.Count}, no genome {crowd.Missing}, undeveloped {crowd.Undeveloped}");

            // The real world: World's constructor builds the current (World.cs:1252) and the bed
            // (World.cs:1001-1005) from the run's config and seed, and the farm's solver is
            // SolverConfig.FromWorld with that world in hand (Simulation.cs:194-201).
            ulong seed = (ulong)Json.Parse(File.ReadAllText(Path.Combine(run, "run.json")))["seed"].AsDouble();
            var core = new World(crowd.Config, seed);
            CurrentField current = crowd.Config.Current;
            Say($"  world       seed {seed}; current {(current == null ? "none" : current.Mode + " in a " + current.Shape)}, " +
                $"speed {crowd.Config.Current?.Speed}; bed {(core.Bed != null && core.Bed.HasRelief ? "relief" : "flat")}; " +
                $"tank radius {core.TankRadiusMetres} m; physics dt {FarmDt:R} s (the farm's float 0.01)");

            GridField field = BrainSpike.MakeField(crowd, out string fieldNote);
            Say("  snow field  " + fieldNote);

            Dictionary<long, double> births = BrainSpike.ReadBirths(run);
            Say($"  births      {births.Count} birth rows read from lineage.jsonl for the brain clocks");
            Say($"  start       world clock {StartSeconds} s, step count {StartSteps} (set through DynamicsWorld.ReadState)");
            Say($"  ceilings    {StepHost.MaxLinks} links, {StepHost.MaxDof} degrees of freedom, {256} contact candidates, " +
                $"{overCap} overlaps a body; neuron stride = the crowd's largest brain");

            using var context = Context.Create(b => b.Cuda().CPU().EnableAlgorithms().Profiling());
            Device cudaDev = null, cpuDev = null;
            foreach (Device d in context)
            {
                if (d.AcceleratorType == AcceleratorType.Cuda) cudaDev = d;
                if (d.AcceleratorType == AcceleratorType.CPU) cpuDev = d;
            }
            using Accelerator cpuAcc = cpuDev.CreateAccelerator(context);
            using Accelerator gpu = cudaDev?.CreateAccelerator(context);
            if (gpu is CudaAccelerator ca)
            {
                Say("  card        " + gpu.Name + ", SMs " + ca.Device.NumMultiprocessors + ", " +
                    ca.Device.Architecture + ", driver " + ca.Device.DriverVersion);
            }
            Say("  local memory, declared a thread: " + LocalBytes(8) + " bytes in double, " + LocalBytes(4) +
                " bytes in single (every local array at the ceiling; registers and spills not counted)");
            Say("");

            var env = new Env { Crowd = crowd, Core = core, Field = field, Births = births, At = at, OverCap = overCap,
                Dt = dtArg, LimitAlways = limitAlways, Poison = poison };
            Say($"  options     dt {dtArg:R} s, drive limiter at every step {limitAlways}, bodies poisoned {poison}");

            Reference reference = null;
            if (doCheck)
            {
                reference = Check(env, cpuAcc, checkSteps, threads, block, Path.Combine(ScratchDir, "digest-" + stamp));
            }

            if (doCard && gpu != null)
            {
                if (reference == null || reference.At1000 == null || reference.Steps < cardSteps)
                {
                    reference = CpuReference(env, cardSteps, threads);
                }
                Card(env, gpu, reference, cardSteps, group, group2, block);
            }

            if (doTime && gpu != null)
            {
                foreach (int size in sizes)
                {
                    Time(env, gpu, size, timeSteps, cpuSteps, cpuSteps1, repeats, threads, group, block);
                }
            }

            string outPath = Path.Combine(ScratchDir, tag + "-" + stamp + ".txt");
            File.WriteAllText(outPath, Log.ToString());
            Say("  raw output written to " + outPath);
            return 0;
        }

        private sealed class Env
        {
            public Crowd3 Crowd;
            public World Core;
            public GridField Field;
            public Dictionary<long, double> Births;
            public int At, OverCap, Poison;
            public double Dt;
            public bool LimitAlways;
        }

        private sealed class Scene
        {
            public List<Creature> Bodies;
            public DynamicsWorld World;
            public SolverConfig Solver;
            public StepHost Host;
            public long[] Ids;
            public string Label;
        }

        /// <summary>Declared local memory a thread, from the kernel's own list of local arrays.</summary>
        private static int LocalBytes(int real)
        {
            int L = StepHost.MaxLinks, D = StepHost.MaxDof;
            // Reals: per link 174 (mass, lift, volume, smallI, reach; inertia and two anchors; two
            // frames; ball and link rotations; the rotation matrix; position, spin, velocity, relative
            // velocity, water, water acceleration; the three articulated-inertia blocks; pa; the two
            // subspaces; cbias; un, uf, dinv; ubar; acc; fext; the drive and drag ledger's six vectors),
            // per dof 10 (four limits; q, qd, tau, the implicit term, the passive torque and rate),
            // and 51 for the six-by-six solve's scratch.
            int reals = 174 * L + 10 * D + 51;
            int floats = 6 * L + 2 * D;                 // the senses
            int ints = 4 * L + 1 + 256;                  // parent, dof count and start, panel start; the candidates
            return reals * real + floats * 4 + ints * 4;
        }

        private static Scene MakeScene(Env env, int size, int threads, string digestDir)
        {
            Crowd3 crowd = env.Crowd;
            SolverConfig solver = SolverConfig.FromWorld(crowd.Config, env.Dt, env.Core.Bed, env.Core);
            solver.DriveLimitAtEveryStep = env.LimitAlways;
            solver.ContactInstrument = true;
            solver.ContactEvents = true;

            List<Creature> bodies;
            string label;
            if (size <= 0 || size == crowd.Plans.Count)
            {
                bodies = crowd.Build(solver, crowd.Plans);
                label = "the recorded crowd";
            }
            else
            {
                var (plans, offsets, tileIds, radius, side, inSquare) = crowd.Tile(size);
                solver.TankRadiusMetres = radius;
                bodies = crowd.Build(solver, plans, offsets, tileIds);
                label = $"tiled as the third spike tiles (Crowd3.Tile): the {inSquare} bodies whose roots lie in the " +
                        $"{side:F2} m square inscribed in the recorded tank, repeated edge to edge, the {size} nearest " +
                        $"the new axis kept, the glass moved to r = {radius:F2} m (the streams and the bed stay the " +
                        $"recorded tank's, centred on the old axis)";
            }

            Prepare(bodies, env);

            var world = new DynamicsWorld(solver) { Threads = threads };
            foreach (Creature c in bodies) world.AddInIdOrder(c);
            SetClock(world, StartSeconds, StartSteps);
            foreach (Creature c in world.Creatures) c.EnableTrace();

            // The lost path, exercised on purpose: a NaN in the root's spin of a few jointed bodies,
            // spread through the list, which the step carries into every link and the finiteness
            // check catches at the end of the first step (DynamicsWorld.cs:179-184).
            if (env.Poison > 0)
            {
                var jointedBodies = world.Creatures.Where(c => c.Dof > 0).ToList();
                for (int k = 0; k < env.Poison && k < jointedBodies.Count; k++)
                {
                    jointedBodies[k * jointedBodies.Count / env.Poison].Spin[1] = double.NaN;   // SYNTHETIC
                }
            }
            if (digestDir != null) world.EnableDigest(digestDir, 1, null);

            var reserve = new float[bodies.Count];
            var contact = new bool[bodies.Count][];
            var damage = new float[bodies.Count][];
            IReadOnlyList<Creature> list = world.Creatures;
            for (int i = 0; i < list.Count; i++)
            {
                Creature c = list[i];
                reserve[i] = c.Senses.Reserve.SecondsOfReserve;
                contact[i] = c.Senses.Contact;
                damage[i] = c.Senses.Damage;
            }

            var host = new StepHost(list, solver, env.Field, reserve, contact, damage, env.OverCap,
                                    Math.Max(64 * list.Count, 1 << 16));
            var ids = new long[list.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = list[i].Id;

            return new Scene { Bodies = bodies, World = world, Solver = solver, Host = host, Ids = ids, Label = label };
        }

        /// <summary>
        /// The inputs no record carries, as the third spike draws them: a reserve, a contact flag and a
        /// damage a part, hashed from the id. Unlike the third spike's brain check, velocities and joint
        /// rates are NOT overwritten: the bodies start at rest in their recorded poses and move under
        /// the real current, their own drive and buoyancy.
        /// </summary>
        private static void Prepare(List<Creature> bodies, Env env)
        {
            foreach (Creature c in bodies)
            {
                long id = c.Id;
                long original = id % 100000;

                float reserve = (float)(9000.0 * Crowd3.Hash01(id, 7));                              // SYNTHETIC
                var contact = new bool[c.Links];
                var damage = new float[c.Links];
                for (int l = 0; l < c.Links; l++)
                {
                    contact[l] = Crowd3.Hash01(id, 2000 + l) < 0.1;                                  // SYNTHETIC
                    damage[l] = Crowd3.Hash01(id, 3000 + l) < 0.05
                        ? (float)(1.2 * Crowd3.Hash01(id, 4000 + l)) : 0f;                           // SYNTHETIC
                }

                c.Senses.Nutrients = env.Field;
                c.Senses.Reserve = new FixedReserve(reserve);
                c.Senses.Contact = contact;
                c.Senses.Damage = damage;

                double born = env.Births.TryGetValue(original, out double t) ? t : 0;
                BrainHost.SetClock(c.Brain, env.At - born);
            }
        }

        /// <summary>The world's clock and step count, set through its own ReadState.</summary>
        private static void SetClock(DynamicsWorld world, double seconds, long steps)
        {
            using var ms = new MemoryStream();
            using (var w = new BinaryWriter(ms, Encoding.UTF8, true)) world.WriteState(w);
            byte[] bytes = ms.ToArray();
            BitConverter.GetBytes(seconds).CopyTo(bytes, 4);
            BitConverter.GetBytes(steps).CopyTo(bytes, 12);
            using var r = new BinaryReader(new MemoryStream(bytes));
            world.ReadState(r);
            if (world.ElapsedSeconds != seconds || world.Steps != steps) throw new InvalidOperationException("clock not set");
        }

        /// <summary>
        /// The block of instants the next <paramref name="count"/> steps read, from the world's clock
        /// as <see cref="DynamicsWorld.Step"/> advances it (ElapsedSeconds += dt, a running sum).
        /// </summary>
        private static double FillBlock(StepHost host, double clock, double dt, int count, double[] block)
        {
            for (int k = 0; k < count; k++)
            {
                host.Instant(clock, block, k * StepHost.Inst);
                clock += dt;
            }
            return clock;
        }

        // ------------------------------------------------------------------ 1. transcription

        private sealed class Tally
        {
            public readonly Dictionary<string, long[]> By = new Dictionary<string, long[]>();
            public readonly List<string> First = new List<string>();
            public long Values, Mismatch, NanOnly, NanOnlyAlive;

            public void Add(string what, long values, long bad)
            {
                lock (By)
                {
                    if (!By.TryGetValue(what, out long[] v)) By[what] = v = new long[2];
                    v[0] += values; v[1] += bad;
                    Values += values; Mismatch += bad;
                }
            }

            public void Note(string what)
            {
                lock (First) { if (First.Count < 20) First.Add(what); }
            }
        }

        private sealed class Reference
        {
            public int Steps;
            public Snapshot At100, At1000;
        }

        /// <summary>What the card's single build is held against: the CPU's poses, angles and targets.</summary>
        private sealed class Snapshot
        {
            public int N;
            public int[] Links, Dof, SigLen, Alive;
            public double[] Pos, Q;
            public float[] Signal;

            public static Snapshot Of(IReadOnlyList<Creature> bodies)
            {
                int n = bodies.Count;
                var s = new Snapshot
                {
                    N = n, Links = new int[n], Dof = new int[n], SigLen = new int[n], Alive = new int[n],
                    Pos = new double[3 * StepHost.MaxLinks * n], Q = new double[StepHost.MaxDof * n],
                    Signal = new float[StepHost.MaxDof * n],
                };
                for (int i = 0; i < n; i++)
                {
                    Creature c = bodies[i];
                    s.Links[i] = c.Links; s.Dof[i] = c.Dof; s.SigLen[i] = c.DriveSignal.Length; s.Alive[i] = c.Alive ? 1 : 0;
                    for (int l = 0; l < c.Links; l++)
                        for (int k = 0; k < 3; k++) s.Pos[(3 * l + k) * n + i] = c.Position[3 * l + k];
                    for (int d = 0; d < c.Dof; d++) s.Q[d * n + i] = c.Q[d];
                    for (int d = 0; d < c.DriveSignal.Length; d++) s.Signal[d * n + i] = c.DriveSignal[d];
                }
                return s;
            }
        }

        private static Reference Check(Env env, Accelerator cpuAcc, int steps, int threads, int block, string digestDir)
        {
            Say($"--- 1. transcription: the double build on ILGPU's CPU device against DynamicsWorld.Step, {steps} steps");
            Say("    both start from one crowd; each step the kernel takes the whole step (grid, body phase, swap)");
            Say("    and the library takes DynamicsWorld.Step at " + threads + " threads with the contact instrument,");
            Say("    its events and the digest on (EVOSIM_DIGEST_EVERY = 1); then every value below is compared to the bit.");

            Scene scene = MakeScene(env, 0, threads, digestDir);
            StepHost host = scene.Host;
            DynamicsWorld world = scene.World;
            IReadOnlyList<Creature> bodies = world.Creatures;
            int N = host.N;

            var (iv, im) = host.VerifyInstant(StartSeconds);
            Say($"    the streams' instant as the host transcribes it, against the field's own pinned slots: {iv} values, {im} mismatches");

            using var runner = new Dbl.StepRunner(cpuAcc, host, 0, Math.Min(1024, cpuAcc.MaxNumThreadsPerGroup), block);
            Say($"    N {N}, max neurons {host.MaxNeurons}, max panels {host.MaxPanels}, buckets {runner.Buckets}; " +
                $"kernel compile {runner.CompileMs:F0} ms; dof mismatches in the rebuild {env.Crowd.DofMismatch}");
            Describe(bodies);

            var tally = new Tally();
            var reference = new Reference();
            var digests = new List<(long step, ulong hash, int bodies)>();
            var blockInst = new double[StepHost.Inst * block];
            double dt = scene.Solver.StepSeconds;
            double clock = world.ElapsedSeconds;
            var heldPrev = new long[N][];
            for (int i = 0; i < N; i++) heldPrev[i] = Array.Empty<long>();
            var prevC = new double[7][];
            var prevA = new int[N];
            long candOverflowSteps = 0, overOverflow = 0, entryOverflow = 0;
            int maxFound = 0, maxOver = 0;
            long lostTotal = 0;
            var watch = Stopwatch.StartNew();
            double moved = 0;

            var startPos = new double[N * 3];
            for (int i = 0; i < N; i++) for (int k = 0; k < 3; k++) startPos[3 * i + k] = bodies[i].Position[k];

            for (int step = 0; step < steps; step++)
            {
                if (step % block == 0)
                {
                    int count = Math.Min(block, steps - step);
                    FillBlock(host, clock, dt, count, blockInst);
                    runner.UploadInstants(blockInst, count);
                }

                if (BitConverter.DoubleToInt64Bits(clock) != BitConverter.DoubleToInt64Bits(world.ElapsedSeconds))
                {
                    throw new InvalidOperationException($"step {step}: the host's clock {clock:R} against the world's {world.ElapsedSeconds:R}");
                }

                // The committed spheres the step starts from, to hold the kernel's other buffer against.
                prevC[0] = bodies.Select(c => c.ContactCentre.X).ToArray();
                prevC[1] = bodies.Select(c => c.ContactCentre.Y).ToArray();
                prevC[2] = bodies.Select(c => c.ContactCentre.Z).ToArray();
                prevC[3] = bodies.Select(c => c.ContactRadius).ToArray();
                prevC[4] = bodies.Select(c => c.ContactVelocity.X).ToArray();
                prevC[5] = bodies.Select(c => c.ContactVelocity.Y).ToArray();
                prevC[6] = bodies.Select(c => c.ContactVelocity.Z).ToArray();
                for (int i = 0; i < N; i++) prevA[i] = bodies[i].ContactActive ? 1 : 0;

                long traceStep = world.Steps + 1;
                runner.ClearFlags();
                runner.Step(true, step % block, traceStep, traceStep * dt);
                runner.Synchronize();

                world.Step();
                clock += dt;

                StepResult r = runner.Read();

                Compare(bodies, host, r, prevC, prevA, tally, step);
                Census(world, bodies, host, scene.Ids, r, heldPrev, tally, step);

                ulong h = DigestOf(bodies, host, r, out int counted);
                digests.Add((world.Steps, h, counted));
                ulong hk = DigestOf(bodies, host, r, out _, canonicalNaN: true);
                ulong hl = DigestOfLibraryCanonical(bodies);
                tally.Add("digest with every NaN written as one pattern (host, both sides)", 1, hk == hl ? 0 : 1);

                for (int i = 0; i < N; i++)
                {
                    if (r.Found[i] > 256) candOverflowSteps++;
                    if (r.NOver[i] > host.OverCap) overOverflow++;
                    if (r.Found[i] > maxFound) maxFound = r.Found[i];
                    if (r.NOver[i] > maxOver) maxOver = r.NOver[i];
                }
                entryOverflow += r.EntryOverflow;

                if (step == 99) reference.At100 = Snapshot.Of(bodies);
                if (step == 999) reference.At1000 = Snapshot.Of(bodies);
                reference.Steps = step + 1;

                if (step % 50 == 0 || step == steps - 1)
                {
                    Say($"    step {step,5}: values {tally.Values,16:N0}  mismatches {tally.Mismatch}  (wall {watch.Elapsed.TotalSeconds:F0} s)");
                }
            }

            int alive = 0;
            var moves = new List<double>();
            for (int i = 0; i < N; i++)
            {
                if (!bodies[i].Alive) { lostTotal++; continue; }
                alive++;
                double dx = bodies[i].Position[0] - startPos[3 * i], dy = bodies[i].Position[1] - startPos[3 * i + 1],
                       dz = bodies[i].Position[2] - startPos[3 * i + 2];
                double d = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                moves.Add(d);
                moved = Math.Max(moved, d);
            }

            // The world's own digest rows, EVOSIM_DIGEST_EVERY = 1, against the same hash of the kernel's state.
            world.CloseDigest();
            var rows = new Dictionary<long, string>();
            foreach (string line in File.ReadLines(Path.Combine(digestDir, "digest.jsonl")))
            {
                JsonNode row = Json.Parse(line);
                rows[(long)row["step"].AsDouble()] = row["hash"].AsString();
            }
            int dv = 0, dm = 0;
            foreach (var (s, hash, _) in digests)
            {
                dv++;
                if (!rows.TryGetValue(s, out string hex) || hex != hash.ToString("x16", CultureInfo.InvariantCulture))
                {
                    dm++;
                    tally.Note($"digest at step {s}: kernel {hash:x16} against {(hex ?? "no row")}");
                }
            }
            tally.Add("digest rows (world's digest.jsonl, every step)", dv, dm);

            Say("");
            foreach (var kv in tally.By)
            {
                Say($"    {kv.Key,-60} {kv.Value[0],16:N0} values, {kv.Value[1]} mismatches");
            }
            Say($"    {"TOTAL",-60} {tally.Values,16:N0} values, {tally.Mismatch} mismatches");
            Say($"    values NaN on both sides with different bits (not counted above): {tally.NanOnly:N0}, " +
                $"of which in bodies still alive: {tally.NanOnlyAlive:N0}");
            Say($"    limiter counts at the end, the library's: drive {bodies.Sum(c => c.DriveImpulsesLimited):N0}, " +
                $"drag {bodies.Sum(c => c.DragImpulsesLimited):N0}; lost bodies {bodies.Count(c => !c.Alive)}");
            foreach (string s in tally.First) Say("      " + s);
            Say($"    digest: computed by the world (DynamicsWorld.EnableDigest every step, {rows.Count} rows in {digestDir}) " +
                $"and by the host over the kernel's state; last {digests[digests.Count - 1].hash:x16} over {digests[digests.Count - 1].bodies} bodies");
            Say($"    bodies alive at the end {alive} of {N}, lost {lostTotal}; root displacement over the run: " +
                $"max {moved:F4} m, median {(moves.Count > 0 ? moves.OrderBy(x => x).ElementAt(moves.Count / 2) : 0):F4} m");
            Say($"    occupancy: candidate lists over 256 in {candOverflowSteps} body-steps (largest {maxFound}); " +
                $"overlap lists over {host.OverCap} in {overOverflow} body-steps (largest {maxOver}); grid entries dropped {entryOverflow}");
            Say($"    wall {watch.Elapsed.TotalSeconds:F0} s");
            Say("");
            return reference;
        }

        private static void Describe(IReadOnlyList<Creature> bodies)
        {
            int jointed = bodies.Count(c => c.Dof > 0);
            int maxLinks = bodies.Max(c => c.Links), maxDof = bodies.Max(c => c.Dof);
            double links = bodies.Average(c => c.Links);
            Say($"    crowd: {bodies.Count} bodies, {jointed} jointed, links mean {links:F2} max {maxLinks}, dof max {maxDof}");
        }

        private static bool Same(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);
        private static bool SameF(float a, float b) => BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b);

        private static void Compare(IReadOnlyList<Creature> bodies, StepHost h, StepResult r, double[][] prevC,
                                    int[] prevA, Tally t, int step)
        {
            int N = h.N;
            var names = new[]
            {
                "positions", "rotations (quaternion)", "rotation matrices", "velocities", "spins", "ball rotations",
                "motion subspaces and cbias", "relative velocity, water, water acceleration", "Fext",
                "base pose", "joint angles q and rates qd", "tau (the parked q-dot-dot) and the implicit term",
                "drive signal (brain targets)", "drive window: history, sums, cursor, filled",
                "limiter counts (drive, drag)", "settle sums (work, signed, dissipated, passive)",
                "neuron buffers, memories, clocks", "trace ring, steps, times, flags, cursor, held",
                "lost flags", "committed spheres (after the swap)", "pending spheres against the step's start",
                "overlap lists and bed/glass flags",
            };
            var vals = new long[names.Length];
            long nanOnly = 0, nanOnlyAlive = 0;
            var bad = new long[names.Length];

            Parallel.For(0, N, i =>
            {
                Creature c = bodies[i];
                var v = new long[names.Length];
                var m = new long[names.Length];

                void D(int cat, double lib, double ker, string what)
                {
                    v[cat]++;
                    if (!Same(lib, ker))
                    {
                        // Both NaN with different bits: counted apart, never hidden (see NanOnly).
                        if (double.IsNaN(lib) && double.IsNaN(ker))
                        {
                            Interlocked.Increment(ref nanOnly);
                            if (c.Alive) Interlocked.Increment(ref nanOnlyAlive);
                            return;
                        }
                        m[cat]++;
                        t.Note($"step {step} body {i} (id {c.Id}): {what} {lib:R} ({BitConverter.DoubleToInt64Bits(lib):x16}) against {ker:R} ({BitConverter.DoubleToInt64Bits(ker):x16})");
                    }
                }

                void F(int cat, float lib, float ker, string what)
                {
                    v[cat]++;
                    if (!SameF(lib, ker)) { m[cat]++; t.Note($"step {step} body {i} (id {c.Id}): {what} {lib:R} against {ker:R}"); }
                }

                void I(int cat, long lib, long ker, string what)
                {
                    v[cat]++;
                    if (lib != ker) { m[cat]++; t.Note($"step {step} body {i} (id {c.Id}): {what} {lib} against {ker}"); }
                }

                for (int l = 0; l < c.Links; l++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        int a3 = (3 * l + k) * N + i;
                        D(0, c.Position[3 * l + k], r.Pos[a3], $"position {l}.{k}");
                        D(3, c.Velocity[3 * l + k], r.Vel[a3], $"velocity {l}.{k}");
                        D(4, c.Spin[3 * l + k], r.Spin[a3], $"spin {l}.{k}");
                        D(7, c.RelativeVelocity[3 * l + k], r.RelVel[a3], $"relative velocity {l}.{k}");
                        D(7, c.Water[3 * l + k], r.Water[a3], $"water {l}.{k}");
                        D(7, c.WaterAcceleration[3 * l + k], r.WaterAcc[a3], $"water acceleration {l}.{k}");
                    }
                    for (int k = 0; k < 4; k++)
                    {
                        int a4 = (4 * l + k) * N + i;
                        D(1, c.Rotation[4 * l + k], r.Rot[a4], $"rotation {l}.{k}");
                        D(5, c.BallRotation[4 * l + k], r.BallRot[a4], $"ball rotation {l}.{k}");
                    }
                    for (int k = 0; k < 9; k++)
                    {
                        int a9 = (9 * l + k) * N + i;
                        D(2, c.RotationMatrix[9 * l + k], r.RotM[a9], $"rotation matrix {l}.{k}");
                        D(6, c.Sang[9 * l + k], r.Sang[a9], $"sang {l}.{k}");
                        D(6, c.Slin[9 * l + k], r.Slin[a9], $"slin {l}.{k}");
                    }
                    for (int k = 0; k < 6; k++)
                    {
                        int a6 = (6 * l + k) * N + i;
                        D(6, c.Cbias[6 * l + k], r.Cbias[a6], $"cbias {l}.{k}");
                        D(8, c.Fext[6 * l + k], r.Fext[a6], $"Fext {l}.{k}");
                    }
                }

                D(9, c.BasePosition.X, r.BasePos[i], "base x"); D(9, c.BasePosition.Y, r.BasePos[N + i], "base y");
                D(9, c.BasePosition.Z, r.BasePos[2 * N + i], "base z");
                D(9, c.BaseRotation.X, r.BaseRot[i], "base qx"); D(9, c.BaseRotation.Y, r.BaseRot[N + i], "base qy");
                D(9, c.BaseRotation.Z, r.BaseRot[2 * N + i], "base qz"); D(9, c.BaseRotation.W, r.BaseRot[3 * N + i], "base qw");

                for (int d = 0; d < c.Dof; d++)
                {
                    int at = d * N + i;
                    D(10, c.Q[d], r.Q[at], $"q {d}");
                    D(10, c.Qd[d], r.Qd[at], $"qd {d}");
                    D(11, c.Tau[d], r.Tau[at], $"tau {d}");
                    D(11, c.LimitImplicit[d], r.LimitImplicit[at], $"implicit {d}");
                }

                for (int d = 0; d < c.DriveSignal.Length; d++) F(12, c.DriveSignal[d], r.Signal[d * N + i], $"drive signal {d}");

                var history = (float[])StepHost.Field(c.Drive, "_history");
                var runSum = (float[])StepHost.Field(c.Drive, "_runningSum");
                for (int d = 0; d < c.Dof; d++)
                {
                    F(13, runSum[d], r.RunSum[d * N + i], $"running sum {d}");
                    for (int w = 0; w < StepHost.Window; w++)
                        F(13, history[d * StepHost.Window + w], r.History[(d * StepHost.Window + w) * N + i], $"history {d}.{w}");
                }
                I(13, (int)StepHost.Field(c.Drive, "_cursor"), r.Cursor[i], "drive cursor");
                I(13, (int)StepHost.Field(c.Drive, "_filled"), r.Filled[i], "drive filled");

                I(14, c.DriveImpulsesLimited, r.DriveLimited[i], "drive limited");
                I(14, c.DragImpulsesLimited, r.DragLimited[i], "drag limited");

                D(15, c.MechanicalWorkJoules, r.Work[i], "mechanical work");
                D(15, c.SignedWorkJoules, r.Signed[i], "signed work");
                D(15, c.DissipatedJoules, r.Dissipated[i], "dissipated");
                D(15, c.PassiveJointWorkJoules, r.Passive[i], "passive work");

                var (clock, prev, cur, mem) = BrainHost.BrainState(c.Brain);
                D(16, clock, r.Clock[i], "brain clock");
                int pb = r.Parity[i] * h.MaxNeurons, cb = (1 - r.Parity[i]) * h.MaxNeurons;
                for (int k = 0; k < prev.Length; k++)
                {
                    F(16, prev[k], r.S[(pb + k) * N + i], $"neuron {k} (previous)");
                    F(16, cur[k], r.S[(cb + k) * N + i], $"neuron {k} (current)");
                    F(16, mem[k], r.Mem[k * N + i], $"memory {k}");
                }

                I(17, c.FirstNonFiniteStep, r.TraceFirstBadStep[i], "trace first bad step");
                I(17, c.FirstNonFiniteLink, r.TraceFirstBadLink[i], "trace first bad link");
                I(17, c.HasTrace ? 1 : 0, h.TraceEnabled[i], "trace enabled");
                if (c.HasTrace)
                {
                    I(17, c.TraceHeld, r.TraceHeld[i], "trace held");
                    I(17, c.TraceCursor, r.TraceCursor[i], "trace cursor");
                    for (int f = 0; f < StepHost.TraceFrames; f++)
                    {
                        I(17, c.TraceStepAt(f), r.TraceStep[f * N + i], $"trace step {f}");
                        D(17, c.TraceTimeAt(f), r.TraceTime[f * N + i], $"trace time {f}");
                        I(17, c.TraceResizedAt(f) ? 1 : 0, r.TraceResized[f * N + i], $"trace resized {f}");
                        for (int l = 0; l < c.Links; l++)
                            for (int k = 0; k < StepHost.TraceValues; k++)
                                D(17, c.TraceValue(f, l, k),
                                  r.Ring[((f * StepHost.MaxLinks + l) * StepHost.TraceValues + k) * N + i],
                                  $"trace {f} link {l} value {k}");
                    }
                }

                I(18, c.Alive ? 1 : 0, r.Alive[i], "alive");

                var (pc, pr, pv, pa) = StepHost.Pending(c);
                D(19, c.ContactCentre.X, r.CCx[i], "committed x"); D(19, c.ContactCentre.Y, r.CCy[i], "committed y");
                D(19, c.ContactCentre.Z, r.CCz[i], "committed z"); D(19, c.ContactRadius, r.CR[i], "committed r");
                D(19, c.ContactVelocity.X, r.CVx[i], "committed vx"); D(19, c.ContactVelocity.Y, r.CVy[i], "committed vy");
                D(19, c.ContactVelocity.Z, r.CVz[i], "committed vz");
                I(19, c.ContactActive ? 1 : 0, r.CActive[i], "committed active");
                D(19, pc.X, r.CCx[i], "pending x"); D(19, pc.Y, r.CCy[i], "pending y"); D(19, pc.Z, r.CCz[i], "pending z");
                D(19, pr, r.CR[i], "pending r"); D(19, pv.X, r.CVx[i], "pending vx"); D(19, pv.Y, r.CVy[i], "pending vy");
                D(19, pv.Z, r.CVz[i], "pending vz"); I(19, pa ? 1 : 0, r.CActive[i], "pending active");

                D(20, prevC[0][i], r.PCx[i], "old committed x"); D(20, prevC[1][i], r.PCy[i], "old committed y");
                D(20, prevC[2][i], r.PCz[i], "old committed z"); D(20, prevC[3][i], r.PR[i], "old committed r");
                D(20, prevC[4][i], r.PVx[i], "old committed vx"); D(20, prevC[5][i], r.PVy[i], "old committed vy");
                D(20, prevC[6][i], r.PVz[i], "old committed vz"); I(20, prevA[i], r.PActive[i], "old committed active");

                I(21, c.OverlapCount, r.NOver[i], "overlap count");
                int shown = Math.Min(c.OverlapCount, h.OverCap);
                for (int k = 0; k < shown; k++)
                {
                    int other = r.Over[k * N + i];
                    long id = other >= 0 && other < N ? bodies[other].Id : -1;
                    I(21, c.OverlapId(k), id, $"overlap {k}");
                }
                I(21, c.TouchedBedOrGlass ? 1 : 0, r.BedGlass[i], "bed or glass");

                for (int k = 0; k < names.Length; k++)
                {
                    Interlocked.Add(ref vals[k], v[k]);
                    Interlocked.Add(ref bad[k], m[k]);
                }
            });

            for (int k = 0; k < names.Length; k++) t.Add(names[k], vals[k], bad[k]);
            t.NanOnly += nanOnly;
            t.NanOnlyAlive += nanOnlyAlive;
        }

        /// <summary>DynamicsWorld.CloseContactStep's census, from the kernel's lists (DynamicsWorldContacts.cs:280-345).</summary>
        private static void Census(DynamicsWorld world, IReadOnlyList<Creature> bodies, StepHost h, long[] ids,
                                   StepResult r, long[][] heldPrev, Tally t, int step)
        {
            int N = h.N;
            long pairs = 0, jointed = 0, held = 0, touchingBodies = 0, bedGlass = 0;
            var now = new long[N][];
            for (int i = 0; i < N; i++)
            {
                int n = Math.Min(r.NOver[i], h.OverCap);
                now[i] = new long[n];
                for (int k = 0; k < n; k++) now[i][k] = ids[r.Over[k * N + i]];
            }
            for (int i = 0; i < N; i++)
            {
                if (r.Alive[i] == 0) continue;
                if (r.BedGlass[i] != 0) bedGlass++;
                bool touching = false;
                for (int k = 0; k < now[i].Length; k++)
                {
                    long id = now[i][k];
                    int other = Array.BinarySearch(ids, id);
                    if (other < 0 || r.Alive[other] == 0) continue;
                    touching = true;
                    if (id <= ids[i]) continue;
                    pairs++;
                    if (h.Dof[i] > 0 || h.Dof[other] > 0) jointed++;
                    if (Array.IndexOf(heldPrev[i], id) >= 0) held++;
                }
                if (touching) touchingBodies++;
            }
            for (int i = 0; i < N; i++) heldPrev[i] = now[i];

            void C(string what, long lib, long ker)
            {
                t.Add("census counts (CloseContactStep)", 1, lib == ker ? 0 : 1);
                if (lib != ker) t.Note($"step {step}: census {what} {lib} against {ker}");
            }
            C("pairs", world.OverlapPairsThisStep, pairs);
            C("jointed pairs", world.OverlapPairsJointedThisStep, jointed);
            C("held pairs", world.OverlapPairsHeldThisStep, held);
            C("bodies", world.OverlapBodiesThisStep, touchingBodies);
            C("bed or glass", world.BedOrGlassBodiesThisStep, bedGlass);
        }

        /// <summary>The digest of DynamicsWorldDigest.WriteDigestRow, over the kernel's state.</summary>
        private static ulong DigestOf(IReadOnlyList<Creature> bodies, StepHost h, StepResult r, out int counted,
                                      bool canonicalNaN = false)
        {
            int N = h.N;
            ulong hash = 14695981039346656037UL;
            counted = 0;
            var part = new double[13];
            var bytes = new byte[13 * 8];
            for (int i = 0; i < N; i++)
            {
                int links = h.Links[i];
                if (links == 0) continue;
                counted++;
                hash = Fnv(hash, BitConverter.GetBytes((long)bodies[i].Id), 8);
                for (int l = 0; l < links; l++)
                {
                    for (int k = 0; k < 3; k++) part[k] = r.Pos[(3 * l + k) * N + i];
                    for (int k = 0; k < 4; k++) part[3 + k] = r.Rot[(4 * l + k) * N + i];
                    for (int k = 0; k < 3; k++) part[7 + k] = r.Vel[(3 * l + k) * N + i];
                    for (int k = 0; k < 3; k++) part[10 + k] = r.Spin[(3 * l + k) * N + i];
                    if (canonicalNaN) Canon(part);
                    Buffer.BlockCopy(part, 0, bytes, 0, bytes.Length);
                    hash = Fnv(hash, bytes, bytes.Length);
                }
            }
            return hash;
        }

        /// <summary>The same digest over the library's own creatures, every NaN written as one pattern.</summary>
        private static ulong DigestOfLibraryCanonical(IReadOnlyList<Creature> bodies)
        {
            ulong hash = 14695981039346656037UL;
            var part = new double[13];
            var bytes = new byte[13 * 8];
            foreach (Creature c in bodies)
            {
                if (c.Links == 0) continue;
                hash = Fnv(hash, BitConverter.GetBytes((long)c.Id), 8);
                for (int l = 0; l < c.Links; l++)
                {
                    for (int k = 0; k < 3; k++) part[k] = c.Position[3 * l + k];
                    for (int k = 0; k < 4; k++) part[3 + k] = c.Rotation[4 * l + k];
                    for (int k = 0; k < 3; k++) part[7 + k] = c.Velocity[3 * l + k];
                    for (int k = 0; k < 3; k++) part[10 + k] = c.Spin[3 * l + k];
                    Canon(part);
                    Buffer.BlockCopy(part, 0, bytes, 0, bytes.Length);
                    hash = Fnv(hash, bytes, bytes.Length);
                }
            }
            return hash;
        }

        private static void Canon(double[] part)
        {
            for (int k = 0; k < part.Length; k++) if (double.IsNaN(part[k])) part[k] = BitConverter.Int64BitsToDouble(0x7FF8000000000000L);
        }

        private static ulong Fnv(ulong hash, byte[] data, int count)
        {
            for (int k = 0; k < count; k++) { hash ^= data[k]; hash *= 1099511628211UL; }
            return hash;
        }

        /// <summary>The library alone, stepped from the same crowd, for the card to be held against.</summary>
        private static Reference CpuReference(Env env, int steps, int threads)
        {
            Say($"--- the CPU reference alone: DynamicsWorld.Step, {steps} steps at {threads} threads");
            Scene scene = MakeScene(env, 0, threads, null);
            var reference = new Reference();
            var w = Stopwatch.StartNew();
            for (int s = 0; s < steps; s++)
            {
                scene.World.Step();
                if (s == 99) reference.At100 = Snapshot.Of(scene.World.Creatures);
                if (s == 999) reference.At1000 = Snapshot.Of(scene.World.Creatures);
                reference.Steps = s + 1;
            }
            Say($"    wall {w.Elapsed.TotalSeconds:F1} s");
            Say("");
            return reference;
        }

        // ------------------------------------------------------------------ 2. the card

        private static (ulong at100, ulong atEnd, StepResult r100, StepResult rEnd, long overCand, long overOver, int maxFound, int maxOver, long entryDrop)
            RunCard(IStepRunner runner, StepHost host, int steps, int block, bool serialMean)
        {
            runner.ResetState();
            var blockInst = new double[StepHost.Inst * block];
            double clock = StartSeconds, dt = host.Dt;
            long traceStep = StartSteps;
            ulong d100 = 0;
            StepResult r100 = null;
            long overCand = 0, overOver = 0, entryDrop = 0;
            int maxFound = 0, maxOver = 0;
            for (int step = 0; step < steps; step++)
            {
                if (step % block == 0)
                {
                    int count = Math.Min(block, steps - step);
                    FillBlock(host, clock, dt, count, blockInst);
                    runner.UploadInstants(blockInst, count);
                }
                traceStep++;
                runner.Step(serialMean, step % block, traceStep, traceStep * dt);
                clock += dt;

                if ((step + 1) % block == 0 || step == steps - 1)
                {
                    StepResult rr = runner.Read();
                    for (int i = 0; i < host.N; i++)
                    {
                        if (rr.Found[i] > 256) overCand++;
                        if (rr.NOver[i] > host.OverCap) overOver++;
                        maxFound = Math.Max(maxFound, rr.Found[i]);
                        maxOver = Math.Max(maxOver, rr.NOver[i]);
                    }
                    entryDrop += rr.EntryOverflow;
                    runner.ClearFlags();
                }
                if (step == 99) { runner.Synchronize(); d100 = runner.Digest(); r100 = runner.Read(); }
            }
            runner.Synchronize();
            return (d100, runner.Digest(), r100, runner.Read(), overCand, overOver, maxFound, maxOver, entryDrop);
        }

        private static void Card(Env env, Accelerator gpu, Reference reference, int steps, int group, int group2, int block)
        {
            Say($"--- 2. single precision on the card, {steps} steps, the grid's mean by the tree reduction");
            Scene scene = MakeScene(env, 0, 1, null);
            StepHost host = scene.Host;

            using var a = new Sgl.StepRunner(gpu, host, group, 1024, block);
            Say($"    group {group}: compile {a.CompileMs:F0} ms");
            var r1 = RunCard(a, host, steps, block, false);
            var r2 = RunCard(a, host, steps, block, false);
            using var b = new Sgl.StepRunner(gpu, host, group2, 1024, block);
            Say($"    group {group2}: compile {b.CompileMs:F0} ms");
            var r3 = RunCard(b, host, steps, block, false);

            Say($"    identity across repeats, group {group}: at 100 steps {ContactSpike.Pair(r1.at100, r2.at100)}; at {steps} {ContactSpike.Pair(r1.atEnd, r2.atEnd)}");
            Say($"    group {group} against group {group2}: at 100 steps {ContactSpike.Pair(r1.at100, r3.at100)}; at {steps} {ContactSpike.Pair(r1.atEnd, r3.atEnd)}");
            Say($"    occupancy over the run (checked every {block} steps): candidates over 256 {r1.overCand} body-reads (largest {r1.maxFound}); " +
                $"overlaps over {host.OverCap} {r1.overOver} (largest {r1.maxOver}); grid entries dropped {r1.entryDrop}");

            Deviation(host, reference.At100, r1.r100, 100);
            if (reference.At1000 != null && steps >= 1000) Deviation(host, reference.At1000, r1.rEnd, 1000);
            Say("");
        }

        private static void Deviation(StepHost h, Snapshot cpu, StepResult sgl, int steps)
        {
            if (cpu == null || sgl == null) return;
            int N = h.N;
            var pos = new List<double>();
            var q = new List<double>();
            var drive = new List<double>();
            int lostCpu = 0, lostCard = 0, both = 0;
            for (int i = 0; i < N; i++)
            {
                bool ca = cpu.Alive[i] != 0, ga = sgl.Alive[i] != 0;
                if (!ca) lostCpu++;
                if (!ga) lostCard++;
                if (!ca || !ga) continue;
                both++;
                for (int l = 0; l < cpu.Links[i]; l++)
                    for (int k = 0; k < 3; k++)
                    {
                        int at = (3 * l + k) * N + i;
                        pos.Add(Math.Abs(cpu.Pos[at] - sgl.Pos[at]));
                    }
                for (int d = 0; d < cpu.Dof[i]; d++) q.Add(Math.Abs(cpu.Q[d * N + i] - sgl.Q[d * N + i]));
                for (int d = 0; d < cpu.SigLen[i]; d++) drive.Add(Math.Abs((double)cpu.Signal[d * N + i] - sgl.Signal[d * N + i]));
            }
            Say($"    deviation of single on the card from the CPU's double after {steps} steps ({both} bodies alive in both; lost: cpu {lostCpu}, card {lostCard}):");
            Say($"      link positions, m      {ContactSpike.Stats(pos)} over {pos.Count} values");
            Say($"      joint angles, rad      {ContactSpike.Stats(q)} over {q.Count} values");
            Say($"      drive targets, [-1,1]  {ContactSpike.Stats(drive)} over {drive.Count} values");
        }

        // ------------------------------------------------------------------ 3. pace

        private static void Time(Env env, Accelerator gpu, int size, int steps, int cpuSteps, int cpuSteps1,
                                 int repeats, int threads, int group, int block)
        {
            Scene scene = MakeScene(env, size, threads, null);
            StepHost host = scene.Host;
            int n = host.N;
            Say($"=== pace at N = {n}: {scene.Label}");
            Describe(scene.World.Creatures);

            long traceBytes = 0;
            int jointed = 0;
            for (int i = 0; i < n; i++)
            {
                if (host.TraceEnabled[i] == 0) continue;
                jointed++;
                traceBytes += host.Links[i] * StepHost.TraceValues * 4L + 8 + 8 + 4 + 4 + 4;
            }
            Say($"    trace ring writes a step (single): {jointed} jointed bodies, {traceBytes:N0} bytes " +
                $"({traceBytes / (double)n:F0} bytes a body; {traceBytes * 2 - (8 + 8 + 4 + 4 + 4) * (long)jointed:N0} in double)");

            foreach (bool single in new[] { true, false })
            {
                using IStepRunner r = single
                    ? new Sgl.StepRunner(gpu, host, group, 1024, block)
                    : (IStepRunner)new Dbl.StepRunner(gpu, host, group, 1024, block);
                if (ContactSpike.SpinRate == 0) ContactSpike.CalibrateSpin(r.Spin, r.Synchronize);

                var blockInst = new double[StepHost.Inst * block];
                FillBlock(host, StartSeconds, host.Dt, block, blockInst);
                long bodySteps = (long)n * steps;
                int blocks = Math.Max(1, steps / block);

                // Warm.
                r.ResetState(); r.UploadInstants(blockInst, block);
                for (int s = 0; s < block; s++) r.Step(false, s, StartSteps + s + 1, (StartSteps + s + 1) * host.Dt);
                r.Synchronize();

                // The whole thing: per block, the instants up, 50 steps, the metabolic readback.
                long readBytes = 0;
                double wall = ContactSpike.Median(repeats, () =>
                {
                    r.ResetState();
                    var w = Stopwatch.StartNew();
                    long ts = StartSteps;
                    for (int bl = 0; bl < blocks; bl++)
                    {
                        r.UploadInstantsTimed(blockInst);
                        for (int s = 0; s < block; s++) { ts++; r.Step(false, s, ts, ts * host.Dt); }
                        readBytes = r.ReadMetabolic();
                    }
                    r.Synchronize();
                    return w.Elapsed.TotalSeconds;
                });

                // Kernel time, by markers: the grid's five launches and the memset, then the body kernel.
                double[] kernel = Median3(repeats, () =>
                {
                    r.ResetState();
                    r.Synchronize();
                    r.Spin(ContactSpike.SpinIterations(8 * Math.Min(steps, 200)));
                    int count = Math.Min(steps, 200);
                    var marks = new ProfilingMarker[count, 3];
                    long ts = StartSteps;
                    for (int s = 0; s < count; s++)
                    {
                        ts++;
                        marks[s, 0] = gpu.DefaultStream.AddProfilingMarker();
                        r.GridOnly(false);
                        marks[s, 1] = gpu.DefaultStream.AddProfilingMarker();
                        r.BodyOnly(s % block, ts, ts * host.Dt);
                        marks[s, 2] = gpu.DefaultStream.AddProfilingMarker();
                        r.Swap();
                    }
                    gpu.Synchronize();
                    double g = 0, bk = 0;
                    for (int s = 0; s < count; s++)
                    {
                        g += Math.Abs(marks[s, 1].MeasureFrom(marks[s, 0]).TotalSeconds);
                        bk += Math.Abs(marks[s, 2].MeasureFrom(marks[s, 1]).TotalSeconds);
                        marks[s, 0].Dispose(); marks[s, 1].Dispose(); marks[s, 2].Dispose();
                    }
                    return new[] { g * steps / count, bk * steps / count };
                });

                // The host's launch cost: steps queued behind a busy card.
                double enqueue = ContactSpike.Median(repeats, () =>
                {
                    r.ResetState();
                    int queued = Math.Min(steps, 25);
                    r.Synchronize(); r.Spin(ContactSpike.SpinIterations(6 * queued));
                    var w = Stopwatch.StartNew();
                    long ts = StartSteps;
                    for (int s = 0; s < queued; s++) { ts++; r.Step(false, s, ts, ts * host.Dt); }
                    double t = w.Elapsed.TotalSeconds;
                    r.Synchronize();
                    return t * steps / queued;
                });

                double readback = ContactSpike.Median(repeats, () =>
                {
                    r.Synchronize();
                    var w = Stopwatch.StartNew();
                    for (int k = 0; k < 10; k++) r.ReadMetabolic();
                    return w.Elapsed.TotalSeconds / 10;
                });

                double upload = ContactSpike.Median(repeats, () =>
                {
                    r.Synchronize();
                    var w = Stopwatch.StartNew();
                    for (int k = 0; k < 10; k++) { r.UploadInstantsTimed(blockInst); r.Synchronize(); }
                    return w.Elapsed.TotalSeconds / 10;
                });

                StepResult end = r.Read();
                int alive = end.Alive.Count(x => x != 0);
                int maxFound = end.Found.Max(), maxOver = end.NOver.Max();
                int overCand = end.Found.Count(x => x > 256), overOver = end.NOver.Count(x => x > host.OverCap);

                Say($"    card, {r.Precision}, group {group}, {steps} steps in blocks of {block}, median of {repeats}; us per body-step (and per step):");
                Say($"      all in: grid + body kernel + swap, instants up and the readback every {block} steps " +
                    $"{ContactSpike.Us(wall, bodySteps)}  ({wall * 1e6 / steps,9:F1} us/step)");
                Say($"      kernel time, grid (memset, mean, ranges, scan, scatter)   {ContactSpike.Us(kernel[0], bodySteps)}  ({kernel[0] * 1e6 / steps,9:F1} us/step)");
                Say($"      kernel time, body kernel                                  {ContactSpike.Us(kernel[1], bodySteps)}  ({kernel[1] * 1e6 / steps,9:F1} us/step)");
                Say($"      host launch cost of one step's six launches               {ContactSpike.Us(enqueue, bodySteps)}  ({enqueue * 1e6 / steps,9:F1} us/step)");
                Say($"      instants up, once per {block} steps ({r.InstantBytes:N0} bytes)            {upload * 1e6,9:F1} us a block ({upload * 1e6 / block:F2} us/step)");
                Say($"      metabolic readback, once per {block} steps ({readBytes / 1048576.0:F2} MB) {readback * 1e6,9:F1} us a block ({readback * 1e6 / block:F2} us/step)");
                Say($"      after the timing runs: alive {alive} of {n}; candidates largest {maxFound} ({overCand} over 256), overlaps largest {maxOver} ({overOver} over {host.OverCap})");
            }

            // ---- DynamicsWorld.Step at 1 and N threads on the same crowd.
            foreach (int t in new[] { 1, threads })
            {
                int count = t == 1 ? cpuSteps1 : cpuSteps;
                Scene cpu = MakeScene(env, size, t, null);
                for (int s = 0; s < 5; s++) cpu.World.Step();
                var w = Stopwatch.StartNew();
                for (int s = 0; s < count; s++) cpu.World.Step();
                double sec = w.Elapsed.TotalSeconds;
                Say($"    CPU, DynamicsWorld.Step, {t,2} thread(s): {ContactSpike.Us(sec, (long)n * count)} us/body-step  ({sec * 1e6 / count:F1} us/step, {count} steps after 5 warm)");
            }
            Say("");
        }

        private static double[] Median3(int repeats, Func<double[]> what)
        {
            var runs = new double[repeats][];
            for (int i = 0; i < repeats; i++) runs[i] = what();
            var median = new double[runs[0].Length];
            for (int p = 0; p < median.Length; p++)
            {
                var col = new double[repeats];
                for (int i = 0; i < repeats; i++) col[i] = runs[i][p];
                Array.Sort(col);
                median[p] = col[repeats / 2];
            }
            return median;
        }

        private static void Say(string line)
        {
            lock (Log) Log.AppendLine(line);
            Console.WriteLine(line);
        }
    }
}
