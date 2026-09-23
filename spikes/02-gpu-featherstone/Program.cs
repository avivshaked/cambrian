using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Evosim.Core;
using Evosim.Dynamics;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.CPU;
using ILGPU.Algorithms;

namespace Gpu.Spike
{
    internal interface IRunner : IDisposable
    {
        double CompileMs { get; }
        string Where { get; }
        void ResetState();
        void Launch(int steps);
        void Synchronize();
        void UploadDrive();
        void DownloadPositions();
        double[] Positions();
        double[] ReadQ();
        double[] ReadQd();
        ulong Digest();
    }

    /// <summary>The handful of world numbers the reduced step reads, in one blittable place.</summary>
    internal struct Scalars
    {
        public double Dt, DragK, Gravity, TissueDensity, RestoringDensity, WorldDepth, Damping;
        public bool UseRestore, FloorRestores;
    }

    internal static class Program
    {
        private const string DefaultRun =
            @"D:\Projects\experiments\evolution-simulator\runs\r43-s1\2026-09-22-053156-08613877";

        private static RunConfig _config;
        private static SolverConfig _solver;
        private static List<Phenotype> _developed;
        private static readonly StringBuilder Log = new StringBuilder();

        private static int Main(string[] args)
        {
            // The third spike's two kernels (spike3/): contacts, then the brain and the senses.
            if (args.Length > 0 && args[0] == "contact") return Gpu.Spike3.ContactSpike.Run(args[1..]);
            if (args.Length > 0 && args[0] == "brain") return Gpu.Spike3.BrainSpike.Run(args[1..]);

            string run = DefaultRun;
            string snapshot = null;
            int genomeLimit = 2000;
            int steps = 1000;
            int repeats = 3;
            int groupSize = 0;
            int threads = 16;
            int[] sizes = { 1000, 10000 };
            int checkSize = 256;
            double dt = 0.01;
            bool describeOnly = false;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--run": run = args[++i]; break;
                    case "--snapshot": snapshot = args[++i]; break;
                    case "--genomes": genomeLimit = Int(args[++i]); break;
                    case "--steps": steps = Int(args[++i]); break;
                    case "--repeats": repeats = Int(args[++i]); break;
                    case "--group": groupSize = Int(args[++i]); break;
                    case "--threads": threads = Int(args[++i]); break;
                    case "--check-size": checkSize = Int(args[++i]); break;
                    case "--describe": describeOnly = true; break;
                    case "--sizes":
                        string[] parts = args[++i].Split(',');
                        sizes = new int[parts.Length];
                        for (int k = 0; k < parts.Length; k++) sizes[k] = Int(parts[k]);
                        break;
                    default: Console.Error.WriteLine("unknown option " + args[i]); return 2;
                }
            }

            // The machine's owner is present and the card is theirs: never on top of a farm or
            // an open Editor.
            foreach (string name in new[] { "Unity", "Evosim.Farm" })
            {
                if (Process.GetProcessesByName(name).Length > 0)
                {
                    Console.Error.WriteLine($"refusing: {name} is running.");
                    return 3;
                }
            }

            Say("=== Featherstone on the GPU: a measurement spike");
            Say("  host        " + Environment.MachineName + ", " +
                Environment.ProcessorCount + " logical processors, .NET " +
                Environment.Version);
            Say("  commit      " + Git());
            Say("  run         " + run);

            if (snapshot == null)
            {
                var files = Directory.GetFiles(Path.Combine(run, "snapshots"), "*.jsonl");
                Array.Sort(files, StringComparer.Ordinal);
                snapshot = files[files.Length - 1];
            }
            else
            {
                snapshot = Path.Combine(run, "snapshots", snapshot);
            }

            Say("  snapshot    " + snapshot);

            _config = RunConfigJson.Read(
                File.ReadAllText(Path.Combine(run, "config.json")), out string mismatch);
            Say("  config      hash " + _config.Hash() +
                (mismatch != null ? "  (recorded " + mismatch + ")" : "  (matches the record)"));

            var genomes = new List<Genome>();
            int refusedGenomes = 0;
            foreach (string line in File.ReadLines(snapshot))
            {
                if (line.Length == 0 || genomes.Count >= genomeLimit) continue;
                try { genomes.Add(GenomeJson.Read(line)); } catch { refusedGenomes++; }
            }

            _developed = new List<Phenotype>();
            int undeveloped = 0;
            foreach (Genome g in genomes)
            {
                try { _developed.Add(Developer.Develop(g, _config.Development, null, _config.Shapes)); }
                catch { undeveloped++; }
            }

            Say($"  genomes     read {genomes.Count}, refused {refusedGenomes}, " +
                $"developed {_developed.Count}, undeveloped {undeveloped}");

            // ---- the solver's world, with the spike's reductions written down --------------
            _solver = SolverConfig.From(_config, dt);
            _solver.CreatureContact = false;      // no creature-creature contact in the kernel
            _solver.TankRadiusMetres = 0;         // no glass
            _solver.Current = null;               // still water
            _solver.FluidAccelerationCoefficient = 0;  // identically zero in still water anyway

            Say($"  solver      dt {_solver.StepSeconds}, limiters engage " +
                $"{_solver.LimitersEngage}, drag limiter {_solver.DragLimiterEngages}, " +
                $"restoring {_solver.SurfaceRestoringFraction}, excess " +
                $"{_solver.TissueExcessDensity}, neutral {_solver.NeutralBodyVolume}");

            int maxLinks = Gpu.Dbl.Featherstone.MaxLinks;

            var histogram = new SortedDictionary<int, int>();
            int over = 0, jointed = 0;
            foreach (Phenotype p in _developed)
            {
                histogram.TryGetValue(p.PartCount, out int at);
                histogram[p.PartCount] = at + 1;
                if (p.PartCount > maxLinks) over++;
                if (p.TotalDof > 0) jointed++;
            }

            var shape = new StringBuilder();
            foreach (var entry in histogram) shape.Append(entry.Key).Append("->").Append(entry.Value).Append(' ');
            Say("  parts       " + shape.ToString().Trim());
            Say($"  jointed     {jointed} of {_developed.Count}; over MaxLinks={maxLinks}: {over}");

            if (over > 0)
            {
                var kept = new List<Phenotype>();
                foreach (Phenotype p in _developed) if (p.PartCount <= maxLinks) kept.Add(p);
                _developed = kept;
                Say($"  kept        {_developed.Count} phenotypes of MaxLinks or fewer parts");
            }

            if (describeOnly) return 0;
            if (_developed.Count == 0) { Console.Error.WriteLine("nothing to step"); return 1; }

            Scalars scalars = MakeScalars(_solver);

            using var context = Context.Create(b => b.Cuda().CPU().EnableAlgorithms());
            Device cuda = null;
            foreach (Device d in context) if (d.AcceleratorType == AcceleratorType.Cuda) cuda = d;
            if (cuda == null) { Console.Error.WriteLine("no CUDA device"); return 4; }

            using Accelerator gpu = cuda.CreateAccelerator(context);
            Say("  device      " + gpu.Name + ", SMs " +
                (gpu is CudaAccelerator ca ? ca.Device.NumMultiprocessors + ", " + ca.Device.Architecture : "?") +
                ", max threads/group " + gpu.MaxNumThreadsPerGroup);
            Say("");

            // ---- 0. the transcription check: the same kernel on ILGPU's CPU device -----------
            TranscriptionCheck(context, scalars, checkSize, steps, dt);

            // ---- 1..3. deviation, identity and pace at each size ----------------------------
            foreach (int n in sizes)
            {
                Measure(gpu, scalars, n, steps, repeats, groupSize, threads, dt);
            }

            string outPath = Path.Combine(
                @"D:\Projects\experiments\evolution-simulator\scratch\gpu-spike\featherstone",
                "raw-output.txt");
            File.WriteAllText(outPath, Log.ToString());
            Say("  raw output written to " + outPath);

            return 0;
        }

        // ------------------------------------------------------------------ population

        private sealed class Population
        {
            public List<Creature> Bodies = new List<Creature>();
            public List<Vec3> Origins = new List<Vec3>();
            public List<Phenotype> Plans = new List<Phenotype>();
            public double[][] DriveTorque;
        }

        /// <summary>
        /// N bodies drawn from the snapshot's developed phenotypes, scattered through the water
        /// by one seeded stream so that the same index is the same body at every N.
        /// </summary>
        private static Population Build(int n, double dt)
        {
            var population = new Population { DriveTorque = new double[n][] };
            var rng = new Rng(4242424242);

            for (int i = 0; i < n; i++)
            {
                Phenotype plan = _developed[i % _developed.Count];
                var body = new Creature(i, plan, _solver, _config.Shapes);

                // Scattered in a box well clear of the waterline and the floor, so that D077's
                // restoring clamp is exercised by the bodies that swim into it and not by every
                // body on the first step.
                double x = 40.0 * (rng.NextFloat() - 0.5);
                double z = 40.0 * (rng.NextFloat() - 0.5);
                double y = -5.0 - 30.0 * rng.NextFloat();

                var origin = new Vec3(x, y, z);
                body.PlaceAsDeveloped(origin, plan);

                population.Bodies.Add(body);
                population.Origins.Add(origin);
                population.Plans.Add(plan);
                population.DriveTorque[i] = FreezeDrive(body, dt);
            }

            return population;
        }

        private static void Reset(Population p)
        {
            for (int i = 0; i < p.Bodies.Count; i++)
            {
                p.Bodies[i].PlaceAsDeveloped(p.Origins[i], p.Plans[i]);
            }
        }

        /// <summary>
        /// The drive, frozen: one pass of the creature's own senses and brain at the pose it is
        /// placed in, clamped as <c>EffectorDrive.Drive</c> clamps it, turned into the link-frame
        /// torque vector that the tail of that method applies. Held for the whole launch, which
        /// is the spike's brief.
        /// </summary>
        private static double[] FreezeDrive(Creature body, double dt)
        {
            var torque = new double[3 * body.Links];

            try
            {
                body.Senses.Sample();
                body.Brain.Step((float)dt, body.DriveSignal, body.Senses);
            }
            catch
            {
                // A body whose senses want a world they have not been given: leave the signal at
                // whatever the brain last wrote, which for a fresh body is zero.
            }

            for (int b = 1; b < body.Links; b++)
            {
                int n = body.DofCount[b];
                if (n == 0) continue;

                int at = body.DofStart[b];
                QuatD frame = QuatD.Read(body.JointFrame, 4 * b);
                Vec3 sum = Vec3.Zero;

                for (int d = 0; d < n; d++)
                {
                    float raw = at + d < body.DriveSignal.Length ? body.DriveSignal[at + d] : 0f;
                    if (raw < -1f) raw = -1f; else if (raw > 1f) raw = 1f;

                    double magnitude = raw * (float)body.Power[b];
                    sum += frame.Rotate(Axis(d)) * magnitude;
                }

                Vec3.Write(torque, 3 * b, sum);
            }

            return torque;
        }

        /// <summary>
        /// <c>Creature.AxisOf</c>, which is internal to the library: X for the first degree of
        /// freedom, Y for the second, Z for the third.
        /// </summary>
        private static Vec3 Axis(int d) =>
            d == 0 ? Vec3.UnitX : d == 1 ? Vec3.UnitY : Vec3.UnitZ;

        private static Scalars MakeScalars(SolverConfig s) => new Scalars
        {
            Dt = s.StepSeconds,
            DragK = 0.5 * s.Density * s.DragCoefficient,
            Gravity = s.GravityMetresPerSecondSquared,
            TissueDensity = s.TissueDensity,
            RestoringDensity = s.SurfaceRestoringFraction * s.TissueDensity,
            WorldDepth = s.WorldDepthMetres,
            Damping = s.JointDriveDamping,
            UseRestore = s.SurfaceRestoringFraction > 0,
            FloorRestores = !s.FloorIsSolid,
        };

        // ------------------------------------------------------------------ the reference

        /// <summary>
        /// The library, on <see cref="Creature"/> objects, <paramref name="steps"/> steps.
        /// Returns the wall seconds.
        /// </summary>
        private static double RunReference(Population p, int steps, double dt, int threads)
        {
            var options = new ParallelOptions { MaxDegreeOfParallelism = threads };
            var watch = Stopwatch.StartNew();

            Parallel.For(0, p.Bodies.Count, options, i =>
            {
                Creature body = p.Bodies[i];
                double[] drive = p.DriveTorque[i];
                for (int step = 0; step < steps; step++)
                {
                    RefStep.Step(body, _solver, drive, dt);
                }
            });

            watch.Stop();
            return watch.Elapsed.TotalSeconds;
        }

        // ------------------------------------------------------------------ transcription

        private static void TranscriptionCheck(
            Context context, Scalars scalars, int n, int steps, double dt)
        {
            Say($"--- transcription: the same kernel on ILGPU's CPU device, N={n}, {steps} steps");

            Population p = Build(n, dt);
            var flat = new Flat(p.Bodies, p.DriveTorque, _solver, Gpu.Dbl.Featherstone.MaxLinks);

            Device cpu = null;
            foreach (Device d in context) if (d.AcceleratorType == AcceleratorType.CPU) cpu = d;

            using Accelerator acc = cpu.CreateAccelerator(context);
            using var runner = new Gpu.Dbl.Runner(acc, flat, scalars, 0);

            var watch = Stopwatch.StartNew();
            runner.Launch(steps);
            runner.Synchronize();
            watch.Stop();

            RunReference(p, steps, dt, 16);

            Deviation(p, flat, runner, "  library vs kernel-on-CPU (double)");
            Say($"    cpu-device wall {watch.Elapsed.TotalSeconds:F2} s, " +
                $"kernel compile {runner.CompileMs:F0} ms");
            Say("");
        }

        // ------------------------------------------------------------------ the measurement

        private static void Measure(
            Accelerator gpu, Scalars scalars, int n, int steps, int repeats, int groupSize,
            int threads, double dt)
        {
            Say($"=== N = {n}, {steps} steps at dt {dt}");

            Population p = Build(n, dt);
            var flat = new Flat(p.Bodies, p.DriveTorque, _solver, Gpu.Dbl.Featherstone.MaxLinks);
            Say($"  links max {MaxLinksOf(p)}, panels max {flat.MaxPanels}, " +
                $"device state {Bytes(flat)} ");

            using var dbl = new Gpu.Dbl.Runner(gpu, flat, scalars, groupSize);
            using var sgl = new Gpu.Sgl.Runner(gpu, flat, scalars, groupSize);

            Say($"  kernel compile: double {dbl.CompileMs:F0} ms, single {sgl.CompileMs:F0} ms");

            // ---- deviation ---------------------------------------------------------------
            dbl.ResetState(); dbl.Launch(steps); dbl.Synchronize();
            sgl.ResetState(); sgl.Launch(steps); sgl.Synchronize();

            double refSeconds = RunReference(p, steps, dt, threads);
            int lost = 0;
            foreach (Creature body in p.Bodies) if (!body.IsFinite()) lost++;
            Say($"  reference   {refSeconds:F2} s at {threads} threads; non-finite bodies {lost}");

            Deviation(p, flat, dbl, "  library vs GPU double");
            Deviation(p, flat, sgl, "  library vs GPU single");

            // ---- identity on the card -----------------------------------------------------
            dbl.ResetState(); dbl.Launch(steps); dbl.Synchronize();
            ulong a = dbl.Digest();
            dbl.ResetState(); dbl.Launch(steps); dbl.Synchronize();
            ulong b = dbl.Digest();

            sgl.ResetState(); sgl.Launch(steps); sgl.Synchronize();
            ulong c = sgl.Digest();
            sgl.ResetState(); sgl.Launch(steps); sgl.Synchronize();
            ulong d = sgl.Digest();

            Say($"  identity    double {(a == b ? "bit-identical" : "DIFFERS")} ({a:x16} / {b:x16}); " +
                $"single {(c == d ? "bit-identical" : "DIFFERS")} ({c:x16} / {d:x16})");

            // ---- pace ----------------------------------------------------------------------
            long bodySteps = (long)n * steps;

            double kernelD = Median(repeats, () => TimeKernel(dbl, steps));
            double kernelF = Median(repeats, () => TimeKernel(sgl, steps));
            double wholeD = Median(repeats, () => TimeWhole(dbl, steps));
            double wholeF = Median(repeats, () => TimeWhole(sgl, steps));
            double perStepD = Median(repeats, () => TimeCadence(dbl, steps, 1));
            double perStepF = Median(repeats, () => TimeCadence(sgl, steps, 1));
            double metabolicD = Median(repeats, () => TimeCadence(dbl, steps, 50));
            double metabolicF = Median(repeats, () => TimeCadence(sgl, steps, 50));

            double cpu16 = Median(repeats, () => { Reset(p); return RunReference(p, steps, dt, threads); });
            double cpu1 = n <= 2000
                ? Median(repeats, () => { Reset(p); return RunReference(p, steps, dt, 1); })
                : double.NaN;

            Say("");
            Say("  pace, microseconds per body-step (median of " + repeats + ")");
            Say("    kernel only, one launch of " + steps + "  double " + Us(kernelD, bodySteps) +
                "   single " + Us(kernelF, bodySteps));
            Say("    + positions down once per " + steps + "     double " + Us(wholeD, bodySteps) +
                "   single " + Us(wholeF, bodySteps));
            Say("    + drive up / positions down per 50  double " + Us(metabolicD, bodySteps) +
                "   single " + Us(metabolicF, bodySteps));
            Say("    + drive up / positions down per 1   double " + Us(perStepD, bodySteps) +
                "   single " + Us(perStepF, bodySteps));
            Say("    CPU reference, " + threads + " threads          " + Us(cpu16, bodySteps));
            if (!double.IsNaN(cpu1)) Say("    CPU reference, 1 thread            " + Us(cpu1, bodySteps));
            Say("");
        }

        private static double TimeKernel(IRunner r, int steps)
        {
            r.ResetState();
            var watch = Stopwatch.StartNew();
            r.Launch(steps);
            r.Synchronize();
            return watch.Elapsed.TotalSeconds;
        }

        private static double TimeWhole(IRunner r, int steps)
        {
            r.ResetState();
            var watch = Stopwatch.StartNew();
            r.UploadDrive();
            r.Launch(steps);
            r.Synchronize();
            r.DownloadPositions();
            return watch.Elapsed.TotalSeconds;
        }

        private static double TimeCadence(IRunner r, int steps, int chunk)
        {
            r.ResetState();
            var watch = Stopwatch.StartNew();
            for (int at = 0; at < steps; at += chunk)
            {
                r.UploadDrive();
                r.Launch(chunk);
                r.Synchronize();
                r.DownloadPositions();
            }
            return watch.Elapsed.TotalSeconds;
        }

        private static double Median(int repeats, Func<double> what)
        {
            var values = new double[repeats];
            for (int i = 0; i < repeats; i++) values[i] = what();
            Array.Sort(values);
            return values[repeats / 2];
        }

        // ------------------------------------------------------------------ deviation

        private static void Deviation(Population p, Flat flat, IRunner runner, string label)
        {
            double[] q = runner.ReadQ();
            double[] qd = runner.ReadQd();
            double[] pos = runner.Positions();

            int n = flat.N;
            double qMax = 0, qSum = 0; long qCount = 0;
            double vMax = 0, vSum = 0; long vCount = 0;
            double pMax = 0, pSum = 0; long pCount = 0;
            double pScale = 0;
            int worst = -1; double worstAt = 0;
            int nonFinite = 0;

            for (int c = 0; c < n; c++)
            {
                Creature body = p.Bodies[c];
                double bodyWorst = 0;

                for (int d = 0; d < body.Dof; d++)
                {
                    double e = Math.Abs(body.Q[d] - q[d * n + c]);
                    if (!double.IsFinite(e)) { nonFinite++; continue; }
                    if (e > qMax) qMax = e;
                    qSum += e * e; qCount++;

                    double f = Math.Abs(body.Qd[d] - qd[d * n + c]);
                    if (double.IsFinite(f)) { if (f > vMax) vMax = f; vSum += f * f; vCount++; }
                }

                for (int i = 0; i < body.Links; i++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        double e = Math.Abs(body.Position[3 * i + k] - pos[(3 * i + k) * n + c]);
                        if (!double.IsFinite(e)) { nonFinite++; continue; }
                        if (e > pMax) pMax = e;
                        if (e > bodyWorst) bodyWorst = e;
                        pSum += e * e; pCount++;
                        pScale += body.Position[3 * i + k] * body.Position[3 * i + k];
                    }
                }

                if (bodyWorst > worstAt) { worstAt = bodyWorst; worst = c; }
            }

            Say(label);
            Say($"    q   max {qMax:E3} rad     rms {Rms(qSum, qCount):E3} rad   over {qCount} dof");
            Say($"    qd  max {vMax:E3} rad/s   rms {Rms(vSum, vCount):E3} rad/s");
            Say($"    pos max {pMax:E3} m       rms {Rms(pSum, pCount):E3} m     " +
                $"(|pos| rms {Rms(pScale, pCount):E3} m, {pCount} components)");
            Say($"    worst body {worst} at {worstAt:E3} m; non-finite comparisons {nonFinite}");
        }

        private static double Rms(double sum, long count) => count > 0 ? Math.Sqrt(sum / count) : 0;

        // ------------------------------------------------------------------ small things

        private static int MaxLinksOf(Population p)
        {
            int most = 0;
            foreach (Creature body in p.Bodies) if (body.Links > most) most = body.Links;
            return most;
        }

        private static string Bytes(Flat flat)
        {
            long words = (long)flat.MaxLinks * flat.N * (1 + 1 + 3 + 3 + 3 + 4 + 4 + 3 + 4 + 3) +
                         (long)flat.MaxDof * flat.N * 6 +
                         (long)flat.MaxPanels * flat.N * 7 +
                         14L * flat.N;
            return $"{words * 8 / (1024 * 1024)} MB double / {words * 4 / (1024 * 1024)} MB single";
        }

        private static string Us(double seconds, long bodySteps) =>
            (seconds * 1e6 / bodySteps).ToString("F4", CultureInfo.InvariantCulture).PadLeft(9);

        private static int Int(string s) => int.Parse(s, CultureInfo.InvariantCulture);

        private static void Say(string line) { Log.AppendLine(line); Console.WriteLine(line); }

        private static string Git()
        {
            try
            {
                var psi = new ProcessStartInfo("git", "rev-parse HEAD")
                {
                    WorkingDirectory = @"D:\Projects\experiments\evolution-simulator",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                };
                using Process process = Process.Start(psi);
                string text = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                return text;
            }
            catch { return "?"; }
        }
    }
}
