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
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.CPU;

namespace Gpu.Spike3
{
    /// <summary>
    /// The third spike's first kernel: the contact grid built on the card and the contact pass,
    /// checked against the library on ILGPU's CPU device in double, then run in single on the
    /// card for identity, deviation and pace.
    /// </summary>
    internal static class ContactSpike
    {
        internal const string DefaultRun =
            @"D:\Projects\experiments\evolution-simulator\runs\r45-s2\2026-09-22-232754-e51997d5";

        internal const string ScratchDir = @"D:\Projects\experiments\evolution-simulator\scratch\gpu-spike3";

        private static readonly StringBuilder Log = new StringBuilder();

        public static int Run(string[] args)
        {
            string run = DefaultRun;
            int at = 30000;
            int checkSteps = 1000;
            int timeSteps = 200;
            int cpuSteps = 100;
            int repeats = 3;
            int threads = 16;
            int candCap = 256, overCap = 64;
            int group = 32;
            var sizes = new List<int> { 0, 10000, 30000 };
            bool doCheck = true, doCard = true, doTime = true;
            string tag = "contact";

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--run": run = args[++i]; break;
                    case "--at": at = Int(args[++i]); break;
                    case "--check-steps": checkSteps = Int(args[++i]); break;
                    case "--time-steps": timeSteps = Int(args[++i]); break;
                    case "--cpu-steps": cpuSteps = Int(args[++i]); break;
                    case "--repeats": repeats = Int(args[++i]); break;
                    case "--threads": threads = Int(args[++i]); break;
                    case "--cand": candCap = Int(args[++i]); break;
                    case "--over": overCap = Int(args[++i]); break;
                    case "--group": group = Int(args[++i]); break;
                    case "--sizes": sizes = args[++i].Split(',').Select(Int).ToList(); break;
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

            Directory.CreateDirectory(ScratchDir);

            Say("=== the contact kernel: the grid on the card, the query and the push");
            Say("  command     " + Environment.CommandLine);
            Say("  host        " + Environment.MachineName + ", " + Environment.ProcessorCount +
                " logical processors, .NET " + Environment.Version + ", server GC " +
                System.Runtime.GCSettings.IsServerGC);
            Say("  run         " + run + " at " + at + " s");

            var load = Stopwatch.StartNew();
            Crowd3 crowd = Crowd3.Load(run, at);
            Say($"  crowd       genomes {crowd.GenomesRead} (refused {crowd.GenomesRefused}, " +
                $"{crowd.WithPlan} with a recorded plan); rebuilt {crowd.Plans.Count}, no genome " +
                $"{crowd.Missing}, undeveloped {crowd.Undeveloped} ({load.Elapsed.TotalSeconds:F1} s)");
            Say($"  solver      dt {crowd.Solver.StepSeconds}, tank radius {crowd.TankRadius:F4} m, " +
                $"bed {(crowd.Solver.Bed != null ? "relief" : "flat")}, omega {crowd.Solver.ContactOmega}, " +
                $"zeta {crowd.Solver.ContactDampingRatio}, max separation {crowd.Solver.MaxSeparationSpeed}, " +
                $"max dv {crowd.Solver.MaxContactSpeedChange}");
            Say($"  capacities  candidates {candCap}, overlaps {overCap} per body");

            using var context = Context.Create(b => b.Cuda().CPU().EnableAlgorithms().Profiling());
            Device cudaDev = null, cpuDev = null;
            foreach (Device d in context)
            {
                if (d.AcceleratorType == AcceleratorType.Cuda) cudaDev = d;
                if (d.AcceleratorType == AcceleratorType.CPU) cpuDev = d;
            }

            using Accelerator cpuAcc = cpuDev.CreateAccelerator(context);
            using Accelerator gpu = cudaDev?.CreateAccelerator(context);
            Say("  cpu device  " + cpuAcc.Name + ", max threads/group " + cpuAcc.MaxNumThreadsPerGroup);
            if (gpu != null)
            {
                Say("  card        " + gpu.Name + ", SMs " +
                    (gpu is CudaAccelerator ca ? ca.Device.NumMultiprocessors + ", " + ca.Device.Architecture +
                     ", driver " + ca.Device.DriverVersion : "?") +
                    ", max threads/group " + gpu.MaxNumThreadsPerGroup);
            }
            Say("");

            List<Creature> checkedCrowd = null;

            if (doCheck)
            {
                checkedCrowd = Check(crowd, cpuAcc, checkSteps, threads, candCap, overCap);
            }

            if (doCard && gpu != null)
            {
                List<Creature> initial = crowd.Build(crowd.Solver, crowd.Plans);
                Card(crowd, gpu, initial, "the crowd as recorded (at rest)", candCap, overCap, group);
                if (checkedCrowd != null)
                {
                    Card(crowd, gpu, checkedCrowd, $"the crowd after {checkSteps - 1} steps of the library", candCap, overCap, group);
                }
            }

            if (doTime && gpu != null)
            {
                foreach (int size in sizes)
                {
                    Time(crowd, gpu, size, timeSteps, cpuSteps, repeats, threads, candCap, overCap, group);
                }
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string outPath = Path.Combine(ScratchDir, tag + "-" + stamp + ".txt");
            File.WriteAllText(outPath, Log.ToString());
            Say("  raw output written to " + outPath);
            return 0;
        }

        // ------------------------------------------------------------------ 1. transcription

        private sealed class Tally
        {
            public long Values, Mismatch;
            public long CandVals, CandMis, OverVals, OverMis, PushVals, PushMis, SumVals, SumMis;
            public long ForceVals, ForceMis, FextVals, FextMis, CopyVals, CopyMis, BedVals, BedMis;
            public long CandOverflow, OverOverflow, BodySteps;
            public readonly List<string> First = new List<string>();

            public void Note(string what)
            {
                lock (First) { if (First.Count < 12) First.Add(what); }
            }
        }

        private static List<Creature> Check(
            Crowd3 crowd, Accelerator cpuAcc, int steps, int threads, int candCap, int overCap)
        {
            Say($"--- 1. transcription: double on ILGPU's CPU device against the library, {steps} steps");
            Say("    the library steps the crowd (DynamicsWorld, contact on, still water); after every step");
            Say("    the kernel is handed the committed spheres and its lists and forces are compared");
            Say("    with the library's own ContactGrid.Neighbours, Contacts.Apply (Fext, overlap ids,");
            Say("    bed/glass flag) and a line-for-line copy of Contacts.Apply for the per-pair pushes.");

            List<Creature> bodies = crowd.Build(crowd.Solver, crowd.Plans);
            var world = new DynamicsWorld(crowd.Solver) { Threads = threads };
            foreach (Creature c in bodies) world.AddInIdOrder(c);

            SolverConfig probe = crowd.SolverFor(crowd.TankRadius);
            probe.ContactInstrument = true;

            var host = new ContactHost(world.Creatures, crowd.Solver, candCap, overCap, 64 * bodies.Count);
            int reduce = Math.Min(1024, cpuAcc.MaxNumThreadsPerGroup);
            using var runner = new Dbl.ContactRunner(cpuAcc, host, 0, reduce);
            using var local = new Dbl.ContactRunner(cpuAcc, host, 0, reduce);
            local.SetLocal(true);
            Say($"    N {host.N}, max links {host.MaxLinks}, buckets {runner.Buckets}; kernel compile " +
                $"{runner.CompileMs:F0} ms; dof mismatches in the rebuild {crowd.DofMismatch}");
            Say("    two queries, both checked every step: the candidate scratch strided in global memory,");
            Say("    and the same algorithm with the scratch in the thread's local memory");

            var tallies = new[] { new Tally(), new Tally() };
            var runners = new IContactRunner[] { runner, local };
            var grid = new ContactGrid();
            var watch = Stopwatch.StartNew();
            double kernelSeconds = 0;
            int lost = 0;
            double cellMin = double.MaxValue, cellMax = 0;

            for (int step = 0; step < steps; step++)
            {
                if (step > 0) world.Step();

                grid.Build(world.Creatures);
                host.Fill(world.Creatures);

                for (int v = 0; v < 2; v++)
                {
                    Tally tally = tallies[v];
                    IContactRunner rr = runners[v];

                    var k = Stopwatch.StartNew();
                    rr.UploadSpheres();
                    rr.ClearFlags();
                    rr.Step(serialMean: true);
                    rr.Synchronize();
                    ContactResult res = rr.Read();
                    kernelSeconds += k.Elapsed.TotalSeconds;

                    if (!Same(res.Cell, grid.CellSize))
                    {
                        tally.Note($"step {step}: cell {res.Cell:R} against {grid.CellSize:R}");
                        Interlocked.Increment(ref tally.Mismatch);
                    }
                    Interlocked.Increment(ref tally.Values);
                    if (res.Entries != grid.Entries)
                    {
                        tally.Note($"step {step}: entries {res.Entries} against {grid.Entries}");
                        Interlocked.Increment(ref tally.Mismatch);
                    }
                    Interlocked.Increment(ref tally.Values);
                    if (res.EntryOverflow != 0) tally.Note($"step {step}: entry overflow {res.EntryOverflow}");

                    if (res.Cell < cellMin) cellMin = res.Cell;
                    if (res.Cell > cellMax) cellMax = res.Cell;

                    Compare(world.Creatures, grid, probe, crowd.Solver, res, tally, step);
                }

                if (step % 100 == 0 || step == steps - 1)
                {
                    Say($"    step {step,5}: values {tallies[0].Values,14:N0} / {tallies[1].Values,14:N0}  " +
                        $"mismatches {tallies[0].Mismatch} / {tallies[1].Mismatch}  (wall {watch.Elapsed.TotalSeconds:F0} s)");
                }
            }

            foreach (Creature c in world.Creatures) if (!c.Alive) lost++;

            Say("");
            Say($"    grid cell over the run         {cellMin:F6} .. {cellMax:F6} m; bodies lost by the library: {lost}");
            for (int v = 0; v < 2; v++)
            {
                Say(v == 0 ? "    -- query with the scratch strided in global memory" : "    -- query with the scratch in local memory");
                Report(tallies[v]);
            }
            Say($"    cpu-device kernel wall {kernelSeconds:F1} s over {steps} steps and two queries; total {watch.Elapsed.TotalSeconds:F0} s");
            Say("");

            return world.Creatures.ToList();
        }

        private static void Report(Tally tally)
        {
            Say($"    body-steps compared            {tally.BodySteps:N0}");
            Say($"    candidate lists (Neighbours)   {tally.CandVals,14:N0} values, {tally.CandMis} mismatches");
            Say($"    overlap ids (instrument)       {tally.OverVals,14:N0} values, {tally.OverMis} mismatches");
            Say($"    per-pair push vectors (copy)   {tally.PushVals,14:N0} values, {tally.PushMis} mismatches");
            Say($"    pair sums (copy)               {tally.SumVals,14:N0} values, {tally.SumMis} mismatches");
            Say($"    capped body force (copy)       {tally.ForceVals,14:N0} values, {tally.ForceMis} mismatches");
            Say($"    per-link Fext (library)        {tally.FextVals,14:N0} values, {tally.FextMis} mismatches");
            Say($"    bed/glass flag (library)       {tally.BedVals,14:N0} values, {tally.BedMis} mismatches");
            Say($"    the copy against the library   {tally.CopyVals,14:N0} values, {tally.CopyMis} mismatches");
            Say($"    TOTAL                          {tally.Values,14:N0} values, {tally.Mismatch} mismatches");
            Say($"    overflow: candidate lists {tally.CandOverflow} body-steps, overlap lists {tally.OverOverflow} body-steps (excluded from the comparison)");
            foreach (string s in tally.First) Say("      " + s);
        }

        private static void Compare(
            IReadOnlyList<Creature> bodies, ContactGrid grid, SolverConfig probe, SolverConfig solver,
            ContactResult res, Tally t, int step)
        {
            int n = bodies.Count;
            var ids = new Dictionary<long, int>(n);
            for (int i = 0; i < n; i++) ids[bodies[i].Id] = i;

            Parallel.For(0, n, new ParallelOptions { MaxDegreeOfParallelism = 16 },
                () => (new int[64], new int[64], new int[64], new ContactRef.Result()),
                (i, _, local) =>
                {
                    var (s1, s2, s3, r) = local;
                    Creature body = bodies[i];
                    if (!body.Alive) return local;

                    long vals = 0, mis = 0;
                    Interlocked.Increment(ref t.BodySteps);

                    if (res.Found[i] > res.CandCap) { Interlocked.Increment(ref t.CandOverflow); return local; }
                    if (res.NOver[i] > res.OverCap) { Interlocked.Increment(ref t.OverOverflow); return local; }

                    // Candidates against the library's own query.
                    int count = grid.Neighbours(i, ref s1);
                    long cv = 1, cm = 0;
                    if (count != res.Unique[i]) { cm++; t.Note($"step {step} body {i}: {res.Unique[i]} candidates against {count}"); }
                    else
                    {
                        for (int k = 0; k < count; k++)
                        {
                            cv++;
                            if (s1[k] != res.Cand[k * n + i]) { cm++; t.Note($"step {step} body {i}: candidate {k}"); break; }
                        }
                    }

                    // The line-for-line copy: pairs, pushes, the sums.
                    ContactRef.Apply(body, i, grid, solver, ref s2, r);

                    long ov = 1, om = 0, pv = 0, pm = 0;
                    if (r.Pairs.Count != res.NOver[i]) { om++; t.Note($"step {step} body {i}: {res.NOver[i]} overlaps against {r.Pairs.Count}"); }
                    else
                    {
                        for (int k = 0; k < r.Pairs.Count; k++)
                        {
                            pv += 3;
                            Vec3 p = r.Pushes[k];
                            if (!Same(p.X, res.Push[(3 * k) * n + i]) || !Same(p.Y, res.Push[(3 * k + 1) * n + i]) ||
                                !Same(p.Z, res.Push[(3 * k + 2) * n + i]))
                            {
                                pm++;
                                t.Note($"step {step} body {i}: push {k} ({p.X:R},{p.Y:R},{p.Z:R}) against ({res.Push[(3 * k) * n + i]:R},{res.Push[(3 * k + 1) * n + i]:R},{res.Push[(3 * k + 2) * n + i]:R})");
                            }
                        }
                    }

                    long sv = 3, sm = 0;
                    if (!Same(r.PairSum.X, res.PairSum[i]) || !Same(r.PairSum.Y, res.PairSum[n + i]) || !Same(r.PairSum.Z, res.PairSum[2 * n + i]))
                    { sm++; t.Note($"step {step} body {i}: pair sum"); }

                    long fv = 3, fm = 0;
                    if (!Same(r.Force.X, res.Force[i]) || !Same(r.Force.Y, res.Force[n + i]) || !Same(r.Force.Z, res.Force[2 * n + i]))
                    { fm++; t.Note($"step {step} body {i}: force ({r.Force.X:R},{r.Force.Y:R},{r.Force.Z:R}) against ({res.Force[i]:R},{res.Force[n + i]:R},{res.Force[2 * n + i]:R})"); }

                    // The library itself: Fext, overlap ids, the bed/glass flag.
                    Array.Clear(body.Fext, 0, body.Fext.Length);
                    Contacts.Apply(body, i, grid, probe, ref s3);

                    long xv = 0, xm = 0, yv = 0, ym = 0;
                    for (int l = 0; l < body.Links; l++)
                    {
                        for (int c = 0; c < 3; c++)
                        {
                            double lib = body.Fext[6 * l + 3 + c];
                            xv++;
                            if (!Same(lib, res.Fext[(3 * l + c) * n + i]))
                            {
                                xm++;
                                t.Note($"step {step} body {i}: Fext link {l} axis {c} {lib:R} against {res.Fext[(3 * l + c) * n + i]:R}");
                            }
                            yv++;
                            if (!Same(lib, r.Fext[3 * l + c])) { ym++; t.Note($"step {step} body {i}: the COPY differs from the library at link {l}"); }
                        }
                    }

                    if (body.OverlapCount != res.NOver[i]) { om++; t.Note($"step {step} body {i}: {res.NOver[i]} overlaps against the instrument's {body.OverlapCount}"); }
                    else
                    {
                        for (int k = 0; k < body.OverlapCount; k++)
                        {
                            ov++;
                            if (body.OverlapId(k) != bodies[res.Over[k * n + i]].Id) { om++; t.Note($"step {step} body {i}: overlap id {k}"); break; }
                        }
                    }

                    long bv = 1, bm = 0;
                    if (body.TouchedBedOrGlass != (res.BedGlass[i] != 0)) { bm++; t.Note($"step {step} body {i}: bed/glass flag"); }

                    Interlocked.Add(ref t.CandVals, cv); Interlocked.Add(ref t.CandMis, cm);
                    Interlocked.Add(ref t.OverVals, ov); Interlocked.Add(ref t.OverMis, om);
                    Interlocked.Add(ref t.PushVals, pv); Interlocked.Add(ref t.PushMis, pm);
                    Interlocked.Add(ref t.SumVals, sv); Interlocked.Add(ref t.SumMis, sm);
                    Interlocked.Add(ref t.ForceVals, fv); Interlocked.Add(ref t.ForceMis, fm);
                    Interlocked.Add(ref t.FextVals, xv); Interlocked.Add(ref t.FextMis, xm);
                    Interlocked.Add(ref t.CopyVals, yv); Interlocked.Add(ref t.CopyMis, ym);
                    Interlocked.Add(ref t.BedVals, bv); Interlocked.Add(ref t.BedMis, bm);

                    // The copy's own agreement with the library is a check on the reference, not
                    // on the kernel, so it is reported apart and kept out of the kernel's total.
                    vals = cv + ov + pv + sv + fv + xv + bv;
                    mis = cm + om + pm + sm + fm + xm + bm;
                    Interlocked.Add(ref t.Values, vals);
                    Interlocked.Add(ref t.Mismatch, mis);
                    return local;
                },
                _ => { });
        }

        // ------------------------------------------------------------------ 2. the card

        private static void Card(
            Crowd3 crowd, Accelerator gpu, List<Creature> bodies, string what, int candCap, int overCap, int group)
        {
            Say($"--- 2. single precision on the card: {what}, N {bodies.Count}");

            var host = new ContactHost(bodies, crowd.Solver, candCap, overCap, 64 * bodies.Count);
            int reduce = Math.Min(1024, gpu.MaxNumThreadsPerGroup);

            // Identity: twice at group 32, twice at the automatic group, both means, both queries.
            ulong[] digests = new ulong[16];
            ContactResult single = null;
            int slot = 0;
            foreach (int g in new[] { group, 0 })
            {
                using var r = new Sgl.ContactRunner(gpu, host, g, reduce);
                foreach (bool localQuery in new[] { false, true })
                {
                    r.SetLocal(localQuery);
                    foreach (bool serial in new[] { false, true })
                    {
                        for (int rep = 0; rep < 2; rep++)
                        {
                            r.UploadSpheres();
                            r.ClearFlags();
                            r.Step(serial);
                            r.Synchronize();
                            digests[slot++] = r.Digest();
                            if (g == group && !localQuery && !serial && rep == 0) single = r.Read();
                        }
                    }
                }
            }

            // slot = 8*groupIndex + 4*local + 2*serial + rep
            Say($"    identity, group {group}, global scratch: tree mean {Pair(digests[0], digests[1])}; serial mean {Pair(digests[2], digests[3])}");
            Say($"    identity, group {group}, local scratch:  tree mean {Pair(digests[4], digests[5])}; serial mean {Pair(digests[6], digests[7])}");
            Say($"    identity, auto group, global scratch: tree mean {Pair(digests[8], digests[9])}; serial mean {Pair(digests[10], digests[11])}");
            Say($"    identity, auto group, local scratch:  tree mean {Pair(digests[12], digests[13])}; serial mean {Pair(digests[14], digests[15])}");
            bool groupsAgree = digests[0] == digests[8] && digests[2] == digests[10] && digests[4] == digests[12] && digests[6] == digests[14];
            bool scratchAgree = digests[0] == digests[4] && digests[2] == digests[6] && digests[8] == digests[12] && digests[10] == digests[14];
            Say($"    group {group} against auto: {(groupsAgree ? "bit-identical" : "DIFFERS")}; global against local scratch: " +
                $"{(scratchAgree ? "bit-identical" : "DIFFERS")}; tree mean against serial mean: " +
                $"{(digests[0] == digests[2] ? "bit-identical" : "differs (expected: the cell's last bit)")}");

            // Deviation from the double reference: the library and the copy on the same spheres.
            var grid = new ContactGrid();
            grid.Build(bodies);
            int n = bodies.Count;
            int[] scratch = new int[64];
            var r0 = new ContactRef.Result();

            int setDiff = 0, candDiff = 0, zeroMismatch = 0, compared = 0, pairsCompared = 0, overflow = 0;
            var forceRel = new List<double>();
            var pushRel = new List<double>();
            var fextRel = new List<double>();
            var forceAbs = new List<double>();
            var dvErr = new List<double>();
            var capFrac = new List<double>();
            double worstAbs = 0;

            for (int i = 0; i < n; i++)
            {
                Creature body = bodies[i];
                if (!body.Alive) continue;
                if (single.Found[i] > candCap || single.NOver[i] > overCap) { overflow++; continue; }

                int count = grid.Neighbours(i, ref scratch);
                bool candSame = count == single.Unique[i];
                for (int k = 0; candSame && k < count; k++) candSame = scratch[k] == single.Cand[k * n + i];
                if (!candSame) candDiff++;

                ContactRef.Apply(body, i, grid, crowd.Solver, ref scratch, r0);
                bool setSame = r0.Pairs.Count == single.NOver[i];
                for (int k = 0; setSame && k < r0.Pairs.Count; k++) setSame = r0.Pairs[k] == single.Over[k * n + i];
                if (!setSame) { setDiff++; continue; }

                for (int k = 0; k < r0.Pairs.Count; k++)
                {
                    Vec3 p = r0.Pushes[k];
                    Vec3 q = new Vec3(single.Push[(3 * k) * n + i], single.Push[(3 * k + 1) * n + i], single.Push[(3 * k + 2) * n + i]);
                    double m = p.Magnitude;
                    if (m > 0) { pushRel.Add((p - q).Magnitude / m); pairsCompared++; }
                }

                Vec3 f = r0.Force;
                Vec3 g = new Vec3(single.Force[i], single.Force[n + i], single.Force[2 * n + i]);
                double fm = f.Magnitude;
                if (fm > 0) { forceRel.Add((f - g).Magnitude / fm); compared++; }
                else if (g.Magnitude != 0) zeroMismatch++;
                double abs = (f - g).Magnitude;
                if (abs > worstAbs) worstAbs = abs;
                if (fm > 0 || g.Magnitude > 0)
                {
                    forceAbs.Add(abs);
                    // What the error does to the body in one step: |df| dt / m, m/s.
                    dvErr.Add(abs * crowd.Solver.StepSeconds / body.TotalMass);
                    capFrac.Add(abs / (body.TotalMass * crowd.Solver.MaxContactSpeedChange / crowd.Solver.StepSeconds));
                }

                for (int l = 0; l < body.Links; l++)
                {
                    Vec3 a = new Vec3(r0.Fext[3 * l], r0.Fext[3 * l + 1], r0.Fext[3 * l + 2]);
                    Vec3 b = new Vec3(single.Fext[(3 * l) * n + i], single.Fext[(3 * l + 1) * n + i], single.Fext[(3 * l + 2) * n + i]);
                    double am = a.Magnitude;
                    if (am > 0) fextRel.Add((a - b).Magnitude / am);
                }
            }

            Say($"    cell: single {single.Cell:R} m against double {grid.CellSize:R} m");
            Say($"    candidate lists differing from the library's: {candDiff} of {n} bodies");
            Say($"    overlap sets differing (a pair in one precision and not the other): {setDiff} bodies; " +
                $"overflowed {overflow}");
            Say($"    per-pair push, relative error |single-double|/|double|, {pairsCompared} pairs: " + Stats(pushRel));
            Say($"    body force after the cap, relative error, {compared} bodies with a force: " + Stats(forceRel));
            Say($"    per-link Fext, relative error, {fextRel.Count} links: " + Stats(fextRel));
            Say($"    bodies where double reads zero force and single does not: {zeroMismatch}; worst absolute force error {worstAbs:E3} N");
            Say($"    body force, absolute error, N: " + Stats(forceAbs));
            Say($"    body force error as a speed change in one step, |df| dt / m, m/s: " + Stats(dvErr));
            Say($"    body force error over the body's own cap m * {crowd.Solver.MaxContactSpeedChange} m/s / dt: " + Stats(capFrac));

            // What the error is made of: the positions' own resolution in float at this distance
            // from the origin, against the penetrations the pairs carry.
            var pens = new List<double>();
            double farthest = 0;
            for (int i = 0; i < n; i++)
            {
                Creature a = bodies[i];
                if (!a.Alive) continue;
                farthest = Math.Max(farthest, Math.Max(Math.Abs(a.ContactCentre.X), Math.Max(Math.Abs(a.ContactCentre.Y), Math.Abs(a.ContactCentre.Z))));
                int cnt = grid.Neighbours(i, ref scratch);
                for (int k = 0; k < cnt; k++)
                {
                    Creature b = bodies[scratch[k]];
                    if (!b.ContactActive || scratch[k] < i) continue;
                    double pen = a.ContactRadius + b.ContactRadius - (a.ContactCentre - b.ContactCentre).Magnitude;
                    if (pen > 0) pens.Add(pen);
                }
            }
            Say($"    penetrations of the overlapping pairs, m: " + Stats(pens) +
                $"; a float's spacing at the farthest coordinate ({farthest:F1} m) is {MathF.BitIncrement((float)farthest) - (float)farthest:E2} m");
            Say("");
        }

        // ------------------------------------------------------------------ 3. pace

        private static void Time(
            Crowd3 crowd, Accelerator gpu, int size, int steps, int cpuSteps, int repeats, int threads,
            int candCap, int overCap, int group)
        {
            List<Creature> bodies;
            SolverConfig solver;
            string label;

            if (size <= 0 || size == crowd.Plans.Count)
            {
                solver = crowd.Solver;
                bodies = crowd.Build(solver, crowd.Plans);
                label = "the recorded crowd";
            }
            else
            {
                var (plans, offsets, ids, radius, side, inSquare) = crowd.Tile(size);
                solver = crowd.SolverFor(radius);
                bodies = crowd.Build(solver, plans, offsets, ids);
                label = $"tiled: {inSquare} bodies of the inscribed {side:F2} m square, disc radius {radius:F2} m " +
                        $"(recorded {crowd.TankRadius:F2} m)";
            }

            int n = bodies.Count;
            Say($"=== pace at N = {n}: {label}");

            // ---- occupancy on the library's own grid
            var grid = new ContactGrid();
            grid.Build(bodies);
            int[] scratch = new int[64];
            var unique = new List<int>(n);
            for (int i = 0; i < n; i++) unique.Add(grid.Neighbours(i, ref scratch));
            Say($"    library grid: cell {grid.CellSize:F4} m (2 x mean radius {grid.MeanRadius:F4}), largest radius " +
                $"{grid.LargestRadius:F3} m, {grid.Entries} cell entries ({(double)grid.Entries / n:F2} a body)");

            // ---- the card, single, the chosen group
            var host = new ContactHost(bodies, solver, candCap, overCap, Math.Max(64 * n, 2 * grid.Entries));
            int reduce = Math.Min(1024, gpu.MaxNumThreadsPerGroup);

            using var r = new Sgl.ContactRunner(gpu, host, group, reduce);
            r.UploadSpheres();
            r.ClearFlags();
            r.SetRecord(false);
            r.Step(false);
            r.Synchronize();
            ContactResult res = r.Read();

            var found = new List<int>(n); var uq = new List<int>(n); var ov = new List<int>(n);
            int candOver = 0, overOver = 0;
            for (int i = 0; i < n; i++)
            {
                found.Add(res.Found[i]); uq.Add(res.Unique[i]); ov.Add(res.NOver[i]);
                if (res.Found[i] > candCap) candOver++;
                if (res.NOver[i] > overCap) overOver++;
            }
            Say($"    occupancy (single on the card, this crowd at rest):");
            Say($"      candidates before dedup (what the scratch holds): {Dist(found)}");
            Say($"      candidates after dedup:                          {Dist(uq)}   library after dedup: {Dist(unique)}");
            Say($"      overlaps:                                        {Dist(ov)}");
            Say($"      overflow at candidates {candCap}: {candOver} bodies; at overlaps {overCap}: {overOver} bodies; " +
                $"zero overflow needs candidates >= {found.Max()} and overlaps >= {ov.Max()}; entry overflow {res.EntryOverflow}");
            Say($"      cell on the card {res.Cell:F6} m, entries {res.Entries}, buckets {r.Buckets}");

            // Cells each body's sphere covers: the length of its query's outer loop, which is
            // what one thread walks alone and so what bounds the kernel.
            var cells = new List<int>(n);
            for (int i = 0; i < n; i++)
            {
                Creature c = bodies[i];
                double rr = c.ContactRadius, cs = grid.CellSize;
                int nx = Fl((c.ContactCentre.X + rr) / cs) - Fl((c.ContactCentre.X - rr) / cs) + 1;
                int ny = Fl((c.ContactCentre.Y + rr) / cs) - Fl((c.ContactCentre.Y - rr) / cs) + 1;
                int nz = Fl((c.ContactCentre.Z + rr) / cs) - Fl((c.ContactCentre.Z - rr) / cs) + 1;
                cells.Add(nx * ny * nz);
            }
            var radii = bodies.Select(c => (int)Math.Round(c.ContactRadius * 100)).ToList();
            Say($"      cells covered per body: {Dist(cells)}; bounding radius, cm: {Dist(radii)}");

            long bodySteps = (long)n * steps;

            // Warm, and the spin that keeps the card busy while launches queue.
            for (int s = 0; s < 20; s++) r.Step(false);
            r.Synchronize();
            if (SpinRate == 0) CalibrateSpin(r.Spin, r.Synchronize);
            double enqueueStep = Median(repeats, () => Enqueue(r, steps, 6, () => r.Step(false)));

            double wallTree = Median(repeats, () => Wall(r, steps, () => r.Step(false)));
            double wallSerial = Median(repeats, () => Wall(r, steps, () => r.Step(true)));
            double wallQuery = Median(repeats, () => Wall(r, steps, r.Query));
            double wallEmpty = Median(repeats, () => Wall(r, steps, r.EmptyLaunch));
            double wallUpload = Median(repeats, () => Wall(r, steps, r.UploadSpheresTimed));

            // Kernel time on the card: every step is the whole step, with a marker pair around
            // each piece, so every piece runs on the state the one before it left.
            string[] names = { "memset counts", "mean, tree", "mean, serial", "ranges + histogram", "scan", "scatter", "query + push" };
            var tree = Median3(repeats, () => GpuPieces(gpu, r, steps, false));
            var serial = Median3(repeats, () => GpuPieces(gpu, r, steps, true));
            var gpuTimes = new[] { tree[0], tree[1], serial[1], tree[2], tree[3], tree[4], tree[5] };

            // The same with the candidate scratch in local memory.
            r.SetLocal(true);
            double wallTreeLocal = Median(repeats, () => Wall(r, steps, () => r.Step(false)));
            double queryLocal = Median3(repeats, () => GpuPieces(gpu, r, steps, false))[5];
            r.SetLocal(false);

            // The query at the automatic group, for the group-size question.
            double queryAuto, queryAutoLocal;
            using (var ra = new Sgl.ContactRunner(gpu, host, 0, reduce))
            {
                ra.UploadSpheres(); ra.SetRecord(false); ra.Step(false); ra.Synchronize();
                queryAuto = Median3(repeats, () => GpuPieces(gpu, ra, steps, false))[5];
                ra.SetLocal(true);
                queryAutoLocal = Median3(repeats, () => GpuPieces(gpu, ra, steps, false))[5];
            }

            // The card's answer after all of that is still the answer: nothing was corrupted.
            r.UploadSpheres(); r.ClearFlags(); r.Step(false); r.Synchronize();
            ContactResult after = r.Read();
            int drift = 0;
            for (int i = 0; i < n; i++) if (after.NOver[i] != res.NOver[i] || after.Found[i] != res.Found[i]) drift++;
            if (drift != 0 || after.EntryOverflow != 0) Say($"    WARNING: {drift} bodies changed after the timing loops; entry overflow {after.EntryOverflow}");

            Say($"    card, single, group {group}, {steps} steps, median of {repeats}; microseconds per body-step (and per step):");
            Say($"      whole step, tree mean (memset + 4 launches), wall  {Us(wallTree, bodySteps)}  ({wallTree * 1e6 / steps,9:F1} us/step)");
            Say($"      whole step, serial mean, wall                      {Us(wallSerial, bodySteps)}  ({wallSerial * 1e6 / steps,9:F1} us/step)");
            double kernelSum = gpuTimes[0] + gpuTimes[1] + gpuTimes[3] + gpuTimes[4] + gpuTimes[5] + gpuTimes[6];
            Say($"      kernel time on the card, tree mean (sum of pieces) {Us(kernelSum, bodySteps)}  ({kernelSum * 1e6 / steps,9:F1} us/step)");
            for (int p = 0; p < names.Length; p++)
            {
                Say($"        {names[p],-22}                           {Us(gpuTimes[p], bodySteps)}  ({gpuTimes[p] * 1e6 / steps,9:F1} us/step)");
            }
            Say($"        query + push at the auto group                   {Us(queryAuto, bodySteps)}  ({queryAuto * 1e6 / steps,9:F1} us/step)");
            Say($"      with the candidate scratch in local memory:");
            Say($"        whole step, tree mean, wall                      {Us(wallTreeLocal, bodySteps)}  ({wallTreeLocal * 1e6 / steps,9:F1} us/step)");
            Say($"        query + push, group {group,-4}                        {Us(queryLocal, bodySteps)}  ({queryLocal * 1e6 / steps,9:F1} us/step)");
            Say($"        query + push, auto group                         {Us(queryAutoLocal, bodySteps)}  ({queryAutoLocal * 1e6 / steps,9:F1} us/step)");
            Say($"      query launched alone, wall                         {Us(wallQuery, bodySteps)}  ({wallQuery * 1e6 / steps,9:F1} us/step)");
            Say($"      one empty launch, wall                             {Us(wallEmpty, bodySteps)}  ({wallEmpty * 1e6 / steps,9:F1} us/launch)");
            Say($"      host launch cost of a whole step (memset + 5 launches, enqueued behind a busy card)");
            Say($"                                                         {Us(enqueueStep, bodySteps)}  ({enqueueStep * 1e6 / steps,9:F1} us/step)");
            Say($"      sphere upload if the host fed it (8 reals + flag)  {Us(wallUpload, bodySteps)}  ({wallUpload * 1e6 / steps,9:F1} us/step)");

            // ---- the CPU's own phase: ContactGrid.Build, then Contacts.Apply for every body
            SolverConfig probe = crowd.SolverFor(solver.TankRadiusMetres);
            probe.ContactInstrument = true;
            SolverConfig quiet = crowd.SolverFor(solver.TankRadiusMetres);

            foreach (int t in new[] { 1, threads })
            {
                foreach ((string name, SolverConfig cfg) in new[] { ("instrument on", probe), ("instrument off", quiet) })
                {
                    var (build, apply) = CpuPhase(bodies, cfg, t, cpuSteps, repeats);
                    long cs = (long)n * cpuSteps;
                    Say($"    CPU, {t,2} thread(s), {name,-14}: grid build {Us(build, cs)}, apply {Us(apply, cs)}, " +
                        $"phase {Us(build + apply, cs)} us/body-step  ({(build + apply) * 1e6 / cpuSteps:F1} us/step, {cpuSteps} steps)");
                }
            }
            Say("");
        }

        private static (double build, double apply) CpuPhase(
            List<Creature> bodies, SolverConfig cfg, int threads, int steps, int repeats)
        {
            var grid = new ContactGrid();
            int n = bodies.Count;
            var scratch = new int[n][];
            for (int i = 0; i < n; i++) scratch[i] = new int[32];
            var options = new ParallelOptions { MaxDegreeOfParallelism = threads };

            double[] builds = new double[repeats], applies = new double[repeats];
            for (int rep = 0; rep < repeats; rep++)
            {
                long b = 0, a = 0;
                for (int s = 0; s < steps; s++)
                {
                    long t0 = Stopwatch.GetTimestamp();
                    grid.Build(bodies);
                    long t1 = Stopwatch.GetTimestamp();
                    Parallel.For(0, n, options, i =>
                    {
                        Creature body = bodies[i];
                        if (!body.Alive) return;
                        Contacts.Apply(body, i, grid, cfg, ref scratch[i]);
                    });
                    long t2 = Stopwatch.GetTimestamp();
                    b += t1 - t0; a += t2 - t1;
                }
                builds[rep] = (double)b / Stopwatch.Frequency;
                applies[rep] = (double)a / Stopwatch.Frequency;
            }
            Array.Sort(builds); Array.Sort(applies);
            return (builds[repeats / 2], applies[repeats / 2]);
        }

        private static double Wall(IContactRunner r, int steps, Action act)
        {
            r.Synchronize();
            var watch = Stopwatch.StartNew();
            for (int s = 0; s < steps; s++) act();
            r.Synchronize();
            return watch.Elapsed.TotalSeconds;
        }

        /// <summary>
        /// Seconds on the card for each of memset, mean, ranges, scan, scatter and query, summed
        /// over <paramref name="steps"/> whole steps, from a marker pair around each.
        /// </summary>
        private static double[] GpuPieces(Accelerator gpu, IContactRunner r, int steps, bool serialMean)
        {
            var acts = new Action[]
            {
                r.Memset, () => r.Mean(serialMean), r.RangesOnly, r.ScanOnly, r.ScatterOnly, r.Query,
            };
            gpu.Synchronize();
            // Everything below queues behind a busy card, so it runs back to back on the device.
            r.Spin(SpinIterations(steps * 13));
            var marks = new ProfilingMarker[steps, acts.Length + 1];
            for (int s = 0; s < steps; s++)
            {
                marks[s, 0] = gpu.DefaultStream.AddProfilingMarker();
                for (int p = 0; p < acts.Length; p++)
                {
                    acts[p]();
                    marks[s, p + 1] = gpu.DefaultStream.AddProfilingMarker();
                }
            }
            gpu.Synchronize();
            var total = new double[acts.Length];
            for (int s = 0; s < steps; s++)
            {
                for (int p = 0; p < acts.Length; p++)
                {
                    total[p] += Math.Abs(marks[s, p + 1].MeasureFrom(marks[s, p]).TotalSeconds);
                }
                for (int p = 0; p <= acts.Length; p++) marks[s, p].Dispose();
            }
            return total;
        }

        /// <summary>Spin iterations per second on this card, measured once.</summary>
        internal static double SpinRate;

        /// <summary>A spin long enough to cover the host's enqueueing of this many launches at 60 us each, plus 20 ms.</summary>
        internal static int SpinIterations(int launches) =>
            (int)Math.Min(int.MaxValue - 1, (launches * 60e-6 + 0.02) * SpinRate);

        internal static void CalibrateSpin(Action<int> spin, Action sync)
        {
            spin(1000); sync();
            var w = Stopwatch.StartNew();
            spin(50_000_000); sync();
            SpinRate = 50_000_000 / w.Elapsed.TotalSeconds;
        }

        /// <summary>
        /// What the host spends launching <paramref name="steps"/> of <paramref name="act"/>,
        /// queued behind a busy card so that nothing waits on the device.
        /// </summary>
        /// <remarks>
        /// At most 25 steps are queued: a longer queue fills the driver's launch queue, and the
        /// host then blocks on the busy card, which is what 200 steps (1,200 launches) did in the
        /// first pace run. The result is scaled back to <paramref name="steps"/>.
        /// </remarks>
        private static double Enqueue(IContactRunner r, int steps, int launchesPerStep, Action act)
        {
            int queued = Math.Min(steps, 25);
            r.Synchronize();
            r.Spin(SpinIterations(queued * launchesPerStep));
            var w = Stopwatch.StartNew();
            for (int s = 0; s < queued; s++) act();
            double t = w.Elapsed.TotalSeconds;
            r.Synchronize();
            return t * steps / queued;
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

        // ------------------------------------------------------------------ small things

        private static int Fl(double v)
        {
            int i = (int)v;
            return v < i ? i - 1 : i;
        }

        internal static bool Same(double a, double b) =>
            BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);

        internal static string Pair(ulong a, ulong b) =>
            (a == b ? "bit-identical" : "DIFFERS") + $" ({a:x16} / {b:x16})";

        internal static string Stats(List<double> v)
        {
            if (v.Count == 0) return "none";
            v.Sort();
            double mean = v.Average();
            return $"max {v[v.Count - 1]:E2}, p99 {v[(int)(0.99 * (v.Count - 1))]:E2}, median {v[v.Count / 2]:E2}, mean {mean:E2}";
        }

        internal static string Dist(List<int> v)
        {
            var s = v.OrderBy(x => x).ToList();
            return $"mean {s.Average():F2}, p50 {s[s.Count / 2]}, p99 {s[(int)(0.99 * (s.Count - 1))]}, max {s[s.Count - 1]}";
        }

        internal static double Median(int repeats, Func<double> what)
        {
            var values = new double[repeats];
            for (int i = 0; i < repeats; i++) values[i] = what();
            Array.Sort(values);
            return values[repeats / 2];
        }

        internal static string Us(double seconds, long bodySteps) =>
            (seconds * 1e6 / bodySteps).ToString("F5", CultureInfo.InvariantCulture).PadLeft(10);

        internal static int Int(string s) => int.Parse(s, CultureInfo.InvariantCulture);

        internal static void Say(string line)
        {
            lock (Log) Log.AppendLine(line);
            Console.WriteLine(line);
        }
    }
}
