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
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;

namespace Gpu.Spike3
{
    /// <summary>
    /// The third spike's second kernel: the senses and the brain, checked against the library on
    /// ILGPU's CPU device with the CPU's own arithmetic, then run in single on the card.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The bodies do not move.</b> This is the senses and the brain alone, as acceptance item 3
    /// asks: the pose is the recorded one and stays so, and the thousand steps exercise the
    /// neurons' recurrence, their memories, the oscillators on the body's clock and every sense
    /// channel's arithmetic on a fixed input.
    /// </para>
    /// <para>
    /// <b>Four inputs are synthetic, and each is said where it is made.</b> A snapshot row
    /// carries no reserve, no contact or damage hand-over and no relative velocity, and the fields
    /// directory holds the snow field's column sums and not its cells. So each body's reserve,
    /// relative water velocity, joint rates and hand-overs are drawn from a hash of its id, and the
    /// snow field is the recorded column sums spread evenly down each column's live cells. The
    /// brain clock is the body's age from lineage.jsonl, which is what the clock is.
    /// </para>
    /// </remarks>
    internal static class BrainSpike
    {
        private static readonly StringBuilder Log = new StringBuilder();

        public static int Run(string[] args)
        {
            string run = ContactSpike.DefaultRun;
            int at = 30000;
            int checkSteps = 1000;
            int timeSteps = 1000;
            int cpuSteps = 200;
            int repeats = 3;
            int threads = 16;
            int group = 32;
            var sizes = new List<int> { 0, 10000, 30000 };
            bool doCheck = true, doCard = true, doTime = true;
            string tag = "brain";

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--run": run = args[++i]; break;
                    case "--at": at = ContactSpike.Int(args[++i]); break;
                    case "--check-steps": checkSteps = ContactSpike.Int(args[++i]); break;
                    case "--time-steps": timeSteps = ContactSpike.Int(args[++i]); break;
                    case "--cpu-steps": cpuSteps = ContactSpike.Int(args[++i]); break;
                    case "--repeats": repeats = ContactSpike.Int(args[++i]); break;
                    case "--threads": threads = ContactSpike.Int(args[++i]); break;
                    case "--group": group = ContactSpike.Int(args[++i]); break;
                    case "--sizes": sizes = args[++i].Split(',').Select(ContactSpike.Int).ToList(); break;
                    case "--no-check": doCheck = false; break;
                    case "--no-card": doCard = false; break;
                    case "--no-time": doTime = false; break;
                    case "--tag": tag = args[++i]; break;
                    default: Console.Error.WriteLine("unknown option " + args[i]); return 2;
                }
            }

            foreach (string name in new[] { "Unity", "Evosim.Farm" })
            {
                if (Process.GetProcessesByName(name).Length > 0)
                {
                    Console.Error.WriteLine($"refusing: {name} is running.");
                    return 3;
                }
            }

            Directory.CreateDirectory(ContactSpike.ScratchDir);

            Say("=== the senses and the brain on the card");
            Say("  command     " + Environment.CommandLine);
            Say("  host        " + Environment.MachineName + ", " + Environment.ProcessorCount +
                " logical processors, .NET " + Environment.Version);
            Say("  run         " + run + " at " + at + " s");

            Crowd3 crowd = Crowd3.Load(run, at);
            Say($"  crowd       genomes {crowd.GenomesRead}, rebuilt {crowd.Plans.Count}, no genome {crowd.Missing}, undeveloped {crowd.Undeveloped}");

            GridField field = MakeField(crowd, out string fieldNote);
            Say("  snow field  " + fieldNote);

            Dictionary<long, double> births = ReadBirths(run);
            Say($"  births      {births.Count} birth rows read from lineage.jsonl for the brain clocks");

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

            List<Creature> real = crowd.Build(crowd.Solver, crowd.Plans);
            var inputs = Prepare(real, crowd, field, births, at);
            Describe(real);
            Say("");

            List<Creature> reference = null;
            BrainHost realHost = new BrainHost(real, crowd.Solver, field, inputs.reserve, inputs.contact, inputs.damage);

            if (doCheck)
            {
                reference = Check(real, realHost, cpuAcc, checkSteps, threads, crowd.Solver);
            }

            if (doCard && gpu != null)
            {
                Card(gpu, realHost, reference, checkSteps, group);
            }

            if (doTime && gpu != null)
            {
                foreach (int size in sizes)
                {
                    Time(crowd, field, births, at, gpu, size, timeSteps, cpuSteps, repeats, threads, group);
                }
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string outPath = Path.Combine(ContactSpike.ScratchDir, tag + "-" + stamp + ".txt");
            File.WriteAllText(outPath, Log.ToString());
            Say("  raw output written to " + outPath);
            return 0;
        }

        // ------------------------------------------------------------------ the world's pieces

        /// <summary>
        /// The snow field as World builds it (World.cs:1080-1084), filled from the recorded column
        /// sums, each spread evenly down its column's live cells.
        /// </summary>
        internal static GridField MakeField(Crowd3 crowd, out string note)
        {
            RunConfig c = crowd.Config;
            int patchCount = Math.Max(1, (int)c.HorizontalPatches);
            int patchesAcross = Math.Max(1, (int)c.PatchesAcross);
            float radius = TankGeometry.RadiusFor(c.WorldAreaSquareMetres);

            var field = new GridField(
                c.WorldAreaSquareMetres, c.NutrientSinkMetresPerSecond, c.WorldDepthMetres,
                c.FloorRefugeMetres, c.RefugeEdibleFraction, patchCount, c.FieldCellMetres,
                patchesAcross, c.WorldShape, radius, crowd.Bed);

            string path = Path.Combine(crowd.Run, "fields",
                crowd.At.ToString("000000000", CultureInfo.InvariantCulture) + ".snow-columns.f32");
            byte[] raw = File.ReadAllBytes(path);
            int columns = raw.Length / 4;
            if (columns != field.CellsX * field.CellsZ)
            {
                throw new InvalidDataException($"{path}: {columns} columns against a field of {field.CellsX} x {field.CellsZ}");
            }

            double fileTotal = 0;
            int filled = 0;
            for (int ix = 0; ix < field.CellsX; ix++)
            for (int iz = 0; iz < field.CellsZ; iz++)
            {
                float sum = BitConverter.ToSingle(raw, 4 * (ix * field.CellsZ + iz));
                fileTotal += sum;
                int lowest = field.LowestLiveLayer(ix, iz);
                if (lowest < 0 || !(sum > 0f)) continue;

                int live = 0;
                for (int iy = 0; iy <= lowest; iy++) if (field.IsLive(ix, iy, iz)) live++;
                float each = sum / live;
                for (int iy = 0; iy <= lowest; iy++)
                {
                    if (!field.IsLive(ix, iy, iz)) continue;
                    float cell = field.CellMetres;
                    var at = new Float3((ix + 0.5f) * cell, -(iy + 0.5f) * cell, (iz + 0.5f) * cell);
                    field.Deposit(new FieldPoint(at, 0), each);
                }
                filled++;
            }

            note = $"{field.CellsX} x {field.CellsY} x {field.CellsZ} cells of {field.CellMetres} m, " +
                   $"{field.LiveCellCount} live; recorded column sums total {fileTotal:F1} J over {filled} columns, " +
                   $"the field holds {field.TotalJoules:F1} J (spread evenly down each column: SYNTHETIC vertical profile)";
            return field;
        }

        internal static Dictionary<long, double> ReadBirths(string run)
        {
            var births = new Dictionary<long, double>();
            foreach (string line in File.ReadLines(Path.Combine(run, "lineage.jsonl")))
            {
                if (!line.StartsWith("{\"e\":\"b\"", StringComparison.Ordinal)) continue;
                JsonNode row = Json.Parse(line);
                births[(long)row["id"].AsDouble()] = row["t"].AsDouble();
            }
            return births;
        }

        /// <summary>
        /// Wires each body's senses to the world and gives it the inputs no record carries.
        /// </summary>
        private static (float[] reserve, bool[][] contact, float[][] damage) Prepare(
            List<Creature> bodies, Crowd3 crowd, GridField field, Dictionary<long, double> births, int at)
        {
            int n = bodies.Count;
            var reserve = new float[n];
            var contact = new bool[n][];
            var damage = new float[n][];

            for (int i = 0; i < n; i++)
            {
                Creature c = bodies[i];
                long id = c.Id;
                long original = id % 100000;

                for (int l = 0; l < c.Links; l++)
                    for (int k = 0; k < 3; k++)
                        c.RelativeVelocity[3 * l + k] = Crowd3.Hash01(id, 100 + 3 * l + k) - 0.5;   // SYNTHETIC
                for (int d = 0; d < c.Dof; d++)
                    c.Qd[d] = 4.0 * (Crowd3.Hash01(id, 1000 + d) - 0.5);                           // SYNTHETIC

                reserve[i] = (float)(9000.0 * Crowd3.Hash01(id, 7));                              // SYNTHETIC
                contact[i] = new bool[c.Links];
                damage[i] = new float[c.Links];
                for (int l = 0; l < c.Links; l++)
                {
                    contact[i][l] = Crowd3.Hash01(id, 2000 + l) < 0.1;                            // SYNTHETIC
                    damage[i][l] = Crowd3.Hash01(id, 3000 + l) < 0.05
                        ? (float)(1.2 * Crowd3.Hash01(id, 4000 + l)) : 0f;                         // SYNTHETIC
                }

                c.Senses.Nutrients = field;
                c.Senses.Reserve = new FixedReserve(reserve[i]);
                c.Senses.Contact = contact[i];
                c.Senses.Damage = damage[i];

                // The brain's clock is the body's age: it is stepped from birth.
                double born = births.TryGetValue(original, out double t) ? t : 0;
                BrainHost.SetClock(c.Brain, at - born);
            }

            return (reserve, contact, damage);
        }

        private static void Describe(List<Creature> bodies)
        {
            var neurons = bodies.Select(c => c.Brain.NeuronCount).ToList();
            var ops = new SortedDictionary<string, int>();
            var arity = new int[5];
            var channels = new int[10];
            int jointed = 0, readers = 0;
            foreach (Creature c in bodies)
            {
                if (c.Dof > 0) jointed++;
                int mask = c.Brain.SensorMask;
                if (mask != 0) readers++;
                for (int ch = 0; ch < 10; ch++) if ((mask & (1 << ch)) != 0) channels[ch]++;
                foreach (PhenotypePart p in c.Phenotype.Parts)
                {
                    foreach (NeuronDef d in p.Neurons ?? Array.Empty<NeuronDef>())
                    {
                        string op = d.Op.ToString();
                        ops.TryGetValue(op, out int k); ops[op] = k + 1;
                        arity[Math.Min(4, (d.Inputs ?? Array.Empty<NeuronInput>()).Length)]++;
                    }
                }
            }
            Say($"  brains      neurons a body: {ContactSpike.Dist(neurons)}; total {neurons.Sum()}; jointed bodies {jointed} of {bodies.Count}");
            Say($"              inputs a neuron: 0 -> {arity[0]}, 1 -> {arity[1]}, 2 -> {arity[2]}, 3 -> {arity[3]}, more -> {arity[4]}");
            Say("              ops: " + string.Join(", ", ops.Select(kv => kv.Key + " " + kv.Value)));
            Say($"              bodies whose brain reads any sense: {readers}; by channel: " +
                string.Join(", ", Enumerable.Range(0, 10).Where(ch => ch != 4).Select(ch => ((SensorChannel)ch) + " " + channels[ch])));
        }

        // ------------------------------------------------------------------ 1. transcription

        private sealed class Tally
        {
            public long Values, Mismatch;
            public long NeuronVals, NeuronMis, MemVals, MemMis, ClockVals, ClockMis, DriveVals, DriveMis, SenseVals, SenseMis;
            public readonly List<string> First = new List<string>();
            public void Note(string s) { lock (First) { if (First.Count < 12) First.Add(s); } }
        }

        private static List<Creature> Check(
            List<Creature> bodies, BrainHost host, Accelerator cpuAcc, int steps, int threads, SolverConfig solver)
        {
            Say($"--- 1. transcription: the kernel's double build on ILGPU's CPU device against the library, {steps} steps");
            Say("    each step: the kernel steps every body once; the library runs Senses.Sample and");
            Say("    Brain.Step on every body; then every neuron's two buffers and memory, the clock, the");
            Say("    drive targets and every sense value (through CreatureSenses.Read) are compared to the bit.");

            using var runner = new Dbl.BrainRunner(cpuAcc, host, 0);
            Say($"    N {host.N}, max links {host.MaxLinks}, max dof {host.MaxDof}, max neurons {host.MaxNeurons}; " +
                $"kernel compile {runner.CompileMs:F0} ms");

            var tally = new Tally();
            float dt = (float)solver.StepSeconds;
            var options = new ParallelOptions { MaxDegreeOfParallelism = threads };
            var watch = Stopwatch.StartNew();
            int n = host.N;

            for (int step = 0; step < steps; step++)
            {
                runner.Launch(1);
                runner.Synchronize();
                BrainResult r = runner.Read();

                Parallel.For(0, n, options, i =>
                {
                    Creature c = bodies[i];
                    c.Senses.Sample();
                    c.Brain.Step(dt, c.DriveSignal, c.Senses);
                });

                Parallel.For(0, n, options, i => CompareBody(bodies[i], i, host, r, tally, step));

                if (step % 100 == 0 || step == steps - 1)
                {
                    Say($"    step {step,5}: values {tally.Values,14:N0}  mismatches {tally.Mismatch}  (wall {watch.Elapsed.TotalSeconds:F0} s)");
                }
            }

            Say("");
            Say($"    neuron outputs, both buffers   {tally.NeuronVals,14:N0} values, {tally.NeuronMis} mismatches");
            Say($"    neuron memories                {tally.MemVals,14:N0} values, {tally.MemMis} mismatches");
            Say($"    brain clocks (double)          {tally.ClockVals,14:N0} values, {tally.ClockMis} mismatches");
            Say($"    drive targets                  {tally.DriveVals,14:N0} values, {tally.DriveMis} mismatches");
            Say($"    sense values (Read)            {tally.SenseVals,14:N0} values, {tally.SenseMis} mismatches");
            Say($"    TOTAL                          {tally.Values,14:N0} values, {tally.Mismatch} mismatches");
            foreach (string s in tally.First) Say("      " + s);
            Say($"    wall {watch.Elapsed.TotalSeconds:F0} s");
            Say("");
            return bodies;
        }

        private static readonly SensorChannel[] Checked =
        {
            SensorChannel.Depth, SensorChannel.OrientationUp, SensorChannel.Chemical, SensorChannel.Energy,
        };

        private static void CompareBody(Creature c, int i, BrainHost h, BrainResult r, Tally t, int step)
        {
            int N = h.N;
            long nv = 0, nm = 0, mv = 0, mm = 0, cv = 1, cm = 0, dv = 0, dm = 0, sv = 0, sm = 0;

            var (clock, prev, cur, mem) = BrainHost.BrainState(c.Brain);
            if (BitConverter.DoubleToInt64Bits(clock) != BitConverter.DoubleToInt64Bits(r.Clock[i]))
            { cm++; t.Note($"step {step} body {i}: clock {clock:R} against {r.Clock[i]:R}"); }

            int p = r.Parity[i];
            int pb = p * h.MaxNeurons, cb = (1 - p) * h.MaxNeurons;
            for (int k = 0; k < prev.Length; k++)
            {
                nv += 2; mv++;
                if (!Same(prev[k], r.S[(pb + k) * N + i]))
                { nm++; t.Note($"step {step} body {i} (id {c.Id}): neuron {k} op {OpOf(h, i, k)} {prev[k]:R} against {r.S[(pb + k) * N + i]:R}"); }
                if (!Same(cur[k], r.S[(cb + k) * N + i])) nm++;
                if (!Same(mem[k], r.Mem[k * N + i])) { mm++; t.Note($"step {step} body {i}: memory {k}"); }
            }

            for (int d = 0; d < c.Brain.TotalDof; d++)
            {
                dv++;
                if (!Same(c.DriveSignal[d], r.Drive[d * N + i]))
                { dm++; t.Note($"step {step} body {i}: drive {d} {c.DriveSignal[d]:R} against {r.Drive[d * N + i]:R}"); }
            }

            for (int l = 0; l < c.Links; l++)
            {
                foreach (SensorChannel ch in Checked)
                {
                    float lib = c.Senses.Read(l, ch, 0);
                    float ker = ch == SensorChannel.Depth ? r.Depth[l * N + i]
                        : ch == SensorChannel.OrientationUp ? r.Up[l * N + i]
                        : ch == SensorChannel.Chemical ? r.Chem[l * N + i]
                        : r.Energy[i];
                    sv++;
                    if (!Same(lib, ker)) { sm++; t.Note($"step {step} body {i} link {l}: {ch} {lib:R} against {ker:R}"); }
                }
                for (int k = 0; k < 3; k++)
                {
                    sv++;
                    float lib = c.Senses.Read(l, SensorChannel.Flow, k);
                    if (!Same(lib, r.Flow[(3 * l + k) * N + i])) { sm++; t.Note($"step {step} body {i} link {l}: flow {k}"); }
                }
                for (int k = 0; k < c.DofCount[l]; k++)
                {
                    int at = (c.DofStart[l] + k) * N + i;
                    sv += 2;
                    if (!Same(c.Senses.Read(l, SensorChannel.JointAngle, k), r.Angle[at])) { sm++; t.Note($"step {step} body {i} link {l}: angle {k}"); }
                    if (!Same(c.Senses.Read(l, SensorChannel.JointAngularVelocity, k), r.Rate[at])) { sm++; t.Note($"step {step} body {i} link {l}: rate {k}"); }
                }
            }

            Interlocked.Add(ref t.NeuronVals, nv); Interlocked.Add(ref t.NeuronMis, nm);
            Interlocked.Add(ref t.MemVals, mv); Interlocked.Add(ref t.MemMis, mm);
            Interlocked.Add(ref t.ClockVals, cv); Interlocked.Add(ref t.ClockMis, cm);
            Interlocked.Add(ref t.DriveVals, dv); Interlocked.Add(ref t.DriveMis, dm);
            Interlocked.Add(ref t.SenseVals, sv); Interlocked.Add(ref t.SenseMis, sm);
            Interlocked.Add(ref t.Values, nv + mv + cv + dv + sv);
            Interlocked.Add(ref t.Mismatch, nm + mm + cm + dm + sm);
        }

        private static string OpOf(BrainHost h, int i, int k) => ((NeuronOp)h.Op[k * h.N + i]).ToString();

        // ------------------------------------------------------------------ 2. the card

        private static void Card(Accelerator gpu, BrainHost host, List<Creature> reference, int steps, int group)
        {
            Say($"--- 2. single precision on the card, N {host.N}, {steps} steps");

            ulong a, b, c, d, e;
            BrainResult single;
            using (var r = new Sgl.BrainRunner(gpu, host, group))
            {
                r.ResetState(); r.Launch(steps); r.Synchronize(); a = r.Digest(); single = r.Read();
                r.ResetState(); r.Launch(steps); r.Synchronize(); b = r.Digest();
                r.ResetState(); for (int s = 0; s < steps; s++) r.Launch(1); r.Synchronize(); e = r.Digest();
            }
            using (var r = new Sgl.BrainRunner(gpu, host, 0))
            {
                r.ResetState(); r.Launch(steps); r.Synchronize(); c = r.Digest();
                r.ResetState(); r.Launch(steps); r.Synchronize(); d = r.Digest();
            }

            Say($"    identity, group {group}: {ContactSpike.Pair(a, b)}");
            Say($"    identity, auto group: {ContactSpike.Pair(c, d)}");
            Say($"    group {group} against auto: {(a == c ? "bit-identical" : "DIFFERS")}; one launch of {steps} steps against {steps} launches of one: {(a == e ? "bit-identical" : "DIFFERS")}");

            if (reference == null)
            {
                Say("    (no CPU reference: the transcription check was skipped)");
                Say("");
                return;
            }

            // Deviation from the CPU's arithmetic after the same number of steps from the same state.
            int N = host.N;
            var driveErr = new List<double>();
            var neuronErr = new List<double>();
            var neuronRel = new List<double>();
            int driveOver1e2 = 0, driveSign = 0, bodiesDiffer = 0;
            for (int i = 0; i < N; i++)
            {
                Creature cr = reference[i];
                bool differs = false;
                for (int k = 0; k < cr.Brain.TotalDof; k++)
                {
                    double x = cr.DriveSignal[k], y = single.Drive[k * N + i];
                    double err = Math.Abs(x - y);
                    driveErr.Add(err);
                    if (err > 1e-2) driveOver1e2++;
                    if (Math.Sign(x) != Math.Sign(y) && Math.Abs(x) > 1e-3) driveSign++;
                    if (err != 0) differs = true;
                }
                var (_, prev, _, _) = BrainHost.BrainState(cr.Brain);
                int pb = single.Parity[i] * host.MaxNeurons;
                for (int k = 0; k < prev.Length; k++)
                {
                    double x = prev[k], y = single.S[(pb + k) * N + i];
                    double err = Math.Abs(x - y);
                    neuronErr.Add(err);
                    if (Math.Abs(x) > 1e-6) neuronRel.Add(err / Math.Abs(x));
                    if (err != 0) differs = true;
                }
                if (differs) bodiesDiffer++;
            }

            Say($"    deviation from the CPU after {steps} steps (drive targets are in [-1, 1]):");
            Say($"      drive targets, |single - cpu|: {ContactSpike.Stats(driveErr)} over {driveErr.Count} values");
            Say($"      drive targets off by more than 0.01: {driveOver1e2}; with the sign flipped (|cpu| > 1e-3): {driveSign}");
            Say($"      neuron outputs, |single - cpu|: {ContactSpike.Stats(neuronErr)} over {neuronErr.Count} values");
            Say($"      neuron outputs, relative (|cpu| > 1e-6): {ContactSpike.Stats(neuronRel)}");
            Say($"      bodies with any difference: {bodiesDiffer} of {N}");
            Say("");
        }

        // ------------------------------------------------------------------ 3. pace

        private static void Time(
            Crowd3 crowd, GridField field, Dictionary<long, double> births, int at, Accelerator gpu,
            int size, int steps, int cpuSteps, int repeats, int threads, int group)
        {
            List<Creature> bodies;
            string label;
            if (size <= 0 || size == crowd.Plans.Count)
            {
                bodies = crowd.Build(crowd.Solver, crowd.Plans);
                label = "the recorded crowd";
            }
            else
            {
                // Copies at the recorded places: the senses read the real field and the real poses.
                var plans = new List<Crowd3.Plan>(size);
                var ids = new List<int>(size);
                for (int k = 0; k < size; k++)
                {
                    Crowd3.Plan p = crowd.Plans[k % crowd.Plans.Count];
                    plans.Add(p);
                    ids.Add((k / crowd.Plans.Count) * 100000 + (int)p.Id);
                }
                bodies = crowd.Build(crowd.Solver, plans, null, ids);
                label = $"the recorded crowd repeated at its recorded places ({size / (double)crowd.Plans.Count:F2} copies)";
            }

            var inputs = Prepare(bodies, crowd, field, births, at);
            var host = new BrainHost(bodies, crowd.Solver, field, inputs.reserve, inputs.contact, inputs.damage);
            int n = host.N;
            Say($"=== pace at N = {n}: {label}; max neurons {host.MaxNeurons}, max links {host.MaxLinks}");

            long bodySteps = (long)n * steps;

            using var r = new Sgl.BrainRunner(gpu, host, group);
            if (ContactSpike.SpinRate == 0) ContactSpike.CalibrateSpin(r.Spin, r.Synchronize);

            r.ResetState(); r.Launch(10); r.Synchronize();

            double oneLaunch = ContactSpike.Median(repeats, () => KernelTime(gpu, r, () => r.Launch(steps), 1));
            double perStepGpu = ContactSpike.Median(repeats, () => KernelTime(gpu, r, () => r.Launch(1), steps));
            double perStepWall = ContactSpike.Median(repeats, () => { r.ResetState(); var w = Stopwatch.StartNew(); for (int s = 0; s < steps; s++) r.Launch(1); r.Synchronize(); return w.Elapsed.TotalSeconds; });
            double enqueue = ContactSpike.Median(repeats, () =>
            {
                int queued = Math.Min(steps, 25);
                r.Synchronize(); r.Spin(ContactSpike.SpinIterations(queued));
                var w = Stopwatch.StartNew();
                for (int s = 0; s < queued; s++) r.Launch(1);
                double t = w.Elapsed.TotalSeconds;
                r.Synchronize();
                return t * steps / queued;
            });
            int metabolic = Math.Max(1, steps / 50);
            double upload = ContactSpike.Median(repeats, () => { r.Synchronize(); var w = Stopwatch.StartNew(); for (int s = 0; s < metabolic; s++) r.UploadMetabolic(); r.Synchronize(); return w.Elapsed.TotalSeconds; });

            double autoLaunch;
            using (var ra = new Sgl.BrainRunner(gpu, host, 0))
            {
                ra.ResetState(); ra.Launch(10); ra.Synchronize();
                autoLaunch = ContactSpike.Median(repeats, () => KernelTime(gpu, ra, () => ra.Launch(steps), 1));
            }

            Say($"    card, single, group {group}, {steps} steps, median of {repeats}; microseconds per body-step (and per step):");
            Say($"      kernel only, one launch of {steps} steps       {ContactSpike.Us(oneLaunch, bodySteps)}  ({oneLaunch * 1e6 / steps,9:F1} us/step)");
            Say($"      kernel only, one launch of {steps} steps, auto group {ContactSpike.Us(autoLaunch, bodySteps)}  ({autoLaunch * 1e6 / steps,9:F1} us/step)");
            Say($"      kernel only, {steps} launches of one step    {ContactSpike.Us(perStepGpu, bodySteps)}  ({perStepGpu * 1e6 / steps,9:F1} us/step)");
            Say($"      wall, {steps} launches of one step           {ContactSpike.Us(perStepWall, bodySteps)}  ({perStepWall * 1e6 / steps,9:F1} us/step)");
            Say($"      host launch cost of one step's launch          {ContactSpike.Us(enqueue, bodySteps)}  ({enqueue * 1e6 / steps,9:F1} us/step)");
            Say($"      field stock + reserves up once per 50 steps    {ContactSpike.Us(upload, bodySteps)}  " +
                $"({upload * 1e6 / metabolic,9:F1} us per upload of {r.MetabolicBytes / 1048576.0:F2} MB)");

            // ---- the CPU's own phase: Senses.Sample then Brain.Step for every body
            float dt = (float)crowd.Solver.StepSeconds;
            foreach (int t in new[] { 1, threads })
            {
                var options = new ParallelOptions { MaxDegreeOfParallelism = t };
                double cpu = ContactSpike.Median(repeats, () =>
                {
                    var w = Stopwatch.StartNew();
                    for (int s = 0; s < cpuSteps; s++)
                    {
                        Parallel.For(0, n, options, i =>
                        {
                            Creature c = bodies[i];
                            c.Senses.Sample();
                            c.Brain.Step(dt, c.DriveSignal, c.Senses);
                        });
                    }
                    return w.Elapsed.TotalSeconds;
                });
                Say($"    CPU, {t,2} thread(s): senses + brain {ContactSpike.Us(cpu, (long)n * cpuSteps)} us/body-step  ({cpu * 1e6 / cpuSteps:F1} us/step, {cpuSteps} steps)");
            }
            Say("");
        }

        /// <summary>Seconds on the card for <paramref name="count"/> calls, each bracketed by markers, queued behind a spin.</summary>
        private static double KernelTime(Accelerator gpu, IBrainRunner r, Action act, int count)
        {
            r.ResetState();
            r.Synchronize();
            r.Spin(ContactSpike.SpinIterations(3 * count));
            var marks = new ProfilingMarker[count, 2];
            for (int s = 0; s < count; s++)
            {
                marks[s, 0] = gpu.DefaultStream.AddProfilingMarker();
                act();
                marks[s, 1] = gpu.DefaultStream.AddProfilingMarker();
            }
            gpu.Synchronize();
            double total = 0;
            for (int s = 0; s < count; s++)
            {
                total += Math.Abs(marks[s, 1].MeasureFrom(marks[s, 0]).TotalSeconds);
                marks[s, 0].Dispose(); marks[s, 1].Dispose();
            }
            return total;
        }

        private static bool Same(float a, float b) =>
            BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b);

        private static void Say(string line)
        {
            lock (Log) Log.AppendLine(line);
            Console.WriteLine(line);
        }
    }
}
