using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Evosim.Core;
using Evosim.Dynamics;

namespace Evosim.SwimProbe
{
    /// <summary>
    /// Do a run's jointed bodies swim? Three parts: (A) the joint angles the run recorded in
    /// poses.jsonl over a window; (B) every jointed genome of the last snapshot alone in still
    /// water under its own brain, as the bench's parity trajectories; (C) chosen bodies under
    /// hand-made strokes (passive, a sine in phase, a sine lagging 90 degrees a joint down the
    /// chain, an asymmetric fast-out slow-back stroke).
    /// </summary>
    internal static class Program
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static RunConfig _config;
        private static SolverConfig _solver;
        private static double _dt = 0.01;
        private static double _uniformWater;   // m/s along x, brain pass only: what the flow sensor reads in a current
        private static MethodInfo _jointTorques =
            typeof(DynamicsWorld).GetMethod("JointTorques", BindingFlags.NonPublic | BindingFlags.Instance);

        private static int Main(string[] args)
        {
            string run = null, label = "run", outDir = null;
            int at = 30000, window = 2000, top = 20, random = 5, par = 4;
            double seconds = 120;
            bool clean = false;
            string idsFile = null;
            double constSense = double.NaN;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--run": run = args[++i]; break;
                    case "--label": label = args[++i]; break;
                    case "--out": outDir = args[++i]; break;
                    case "--at": at = int.Parse(args[++i], Inv); break;
                    case "--window": window = int.Parse(args[++i], Inv); break;
                    case "--top": top = int.Parse(args[++i], Inv); break;
                    case "--random": random = int.Parse(args[++i], Inv); break;
                    case "--threads": par = int.Parse(args[++i], Inv); break;
                    case "--seconds": seconds = double.Parse(args[++i], Inv); break;
                    case "--clean": clean = true; break;
                    case "--ids": idsFile = args[++i]; break;
                    case "--const": constSense = double.Parse(args[++i], Inv); break;
                    case "--water": _uniformWater = double.Parse(args[++i], Inv); break;
                    default: Console.Error.WriteLine("unknown " + args[i]); return 2;
                }
            }

            Directory.CreateDirectory(outDir);
            _config = RunConfigJson.Read(File.ReadAllText(Path.Combine(run, "config.json")), out string mismatch);
            Console.WriteLine($"=== swim probe {label}: {run}, config {_config.Hash()} {(mismatch == null ? "(matches)" : "(recorded " + mismatch + ")")}");

            _solver = SolverConfig.From(_config, _dt);
            _solver.TankRadiusMetres = 0;       // alone: no glass
            _solver.CreatureContact = false;    // alone: nobody to touch
            if (!double.IsNaN(constSense)) _solver.ConstantChemicalAndEnergy = (float)constSense;
            Console.WriteLine($"  sensor constant {_solver.ConstantChemicalAndEnergy}, uniform water {_uniformWater} m/s along x");
            if (clean)
            {
                // The control: neutrally buoyant and no D111 torque, so nothing but the stroke moves the body.
                _solver.TissueExcessDensity = 0;
                _solver.BuoyancyOffsetTorque = false;
            }
            Console.WriteLine(FormattableString.Invariant(
                $"  solver: density {_solver.Density}, Cd {_solver.DragCoefficient}, panels/axis {_solver.PanelsPerAxis}, added mass {_solver.AddedMassCoefficient}, fluidAccel {_solver.FluidAccelerationCoefficient}, excess {_solver.TissueExcessDensity}, neutralVol {_solver.NeutralBodyVolume}, offsetTorque {_solver.BuoyancyOffsetTorque}, perPart {_solver.ContactPerPart}, current {(_solver.Current == null ? "none" : "SET")}, bed {(_solver.Bed == null ? "none" : "SET")}, floorSolid {_solver.FloorIsSolid}, depth {_solver.WorldDepthMetres}, limiters {_solver.LimitersEngage}"));

            // ---- the snapshot
            string snapPath = Path.Combine(run, "snapshots", at.ToString("000000000", Inv) + ".jsonl");
            var adults = new Dictionary<long, Phenotype>();
            int refused = 0;
            foreach (string line in File.ReadLines(snapPath))
            {
                if (line.Length == 0) continue;
                try
                {
                    long id = GenomeJson.ReadId(line);
                    Genome genome = GenomeJson.Read(line);
                    int[] counts = GenomeJson.ReadModuleCounts(line);
                    List<int[]> lost = GenomeJson.ReadLostPartPaths(line);
                    var paths = new List<int[]>();
                    Phenotype adult = Developer.Develop(genome, _config.Development, null, _config.Shapes, counts, paths);
                    if (lost != null && lost.Count > 0)
                    {
                        var drop = new bool[adult.PartCount];
                        bool any = false;
                        for (int k = 0; k < adult.PartCount; k++)
                            foreach (int[] p in lost)
                                if (paths[k].Length == p.Length && paths[k].SequenceEqual(p)) { drop[k] = true; any = true; break; }
                        if (any) adult = adult.WithoutSubtrees(drop, out _);
                    }
                    adults[id] = adult;
                }
                catch (Exception) { refused++; }
            }
            Console.WriteLine($"  snapshot {at}: {adults.Count} developed, {refused} refused");

            // ---- (A) the poses window
            var poses = ReadPoses(Path.Combine(run, "poses.jsonl"), at - window, at, out int frames, out var heightAtEnd);
            Console.WriteLine($"  poses: {frames} frames in [{at - window}, {at}], {poses.Count} bodies with any joint coordinate");

            var records = new List<BodyRecord>();
            foreach (var kv in adults)
            {
                Phenotype adult = kv.Value;
                var probe = new Creature(0, adult, _solver, _config.Shapes);
                if (probe.Dof == 0) continue;
                var rec = new BodyRecord { Id = kv.Key, Adult = adult, Dof = probe.Dof, Links = probe.Links, Parts = adult.PartCount };
                rec.Mass = probe.TotalMass;
                rec.JointLinks = new List<int>();
                for (int b = 1; b < probe.Links; b++) if (probe.DofCount[b] > 0) rec.JointLinks.Add(b);
                rec.DofStart = (int[])probe.DofStart.Clone();
                rec.DofCount = (int[])probe.DofCount.Clone();
                rec.Parent = (int[])probe.Parent.Clone();
                rec.LimitLo = (double[])probe.LimitLo.Clone();
                rec.LimitHi = (double[])probe.LimitHi.Clone();
                rec.JointTypes = string.Join("+", rec.JointLinks.Select(b => adult.Parts[b].JointType.ToString()));
                {
                    // Body length: the largest distance between any two parts' far corners, as centre distance plus both longest half-extents.
                    double len = 0;
                    for (int i = 0; i < adult.PartCount; i++)
                        for (int j = i; j < adult.PartCount; j++)
                        {
                            var pi = adult.Parts[i]; var pj = adult.Parts[j];
                            double dx = pi.Position.X - pj.Position.X, dy = pi.Position.Y - pj.Position.Y, dz = pi.Position.Z - pj.Position.Z;
                            double hi = Math.Max(Math.Abs(pi.HalfExtents.X), Math.Max(Math.Abs(pi.HalfExtents.Y), Math.Abs(pi.HalfExtents.Z)));
                            double hj = Math.Max(Math.Abs(pj.HalfExtents.X), Math.Max(Math.Abs(pj.HalfExtents.Y), Math.Abs(pj.HalfExtents.Z)));
                            len = Math.Max(len, Math.Sqrt(dx * dx + dy * dy + dz * dz) + hi + hj);
                        }
                    rec.Length = len;
                }
                rec.StartHeight = heightAtEnd.TryGetValue(kv.Key, out double h) ? h : -20.0;
                if (poses.TryGetValue(kv.Key, out var samples)) AnalysePoses(rec, samples);
                records.Add(rec);
            }
            Console.WriteLine($"  jointed bodies in the snapshot: {records.Count}");

            // ---- (B) every jointed body alone under its own brain
            int steps = (int)Math.Round(seconds / _dt);
            var opts = new ParallelOptions { MaxDegreeOfParallelism = par };
            Parallel.ForEach(records, opts, rec =>
            {
                rec.Brain = Swim(rec, _uniformWater != 0 ? Variant.BrainLoop : Variant.BrainWorld, steps, null);
            });

            // Validation: the probe's own step loop against DynamicsWorld.Step, brain on, on the first 30.
            int same = 0, checkedN = 0;
            foreach (var rec in records.Take(30))
            {
                var mine = Swim(rec, Variant.BrainLoop, steps, null);
                checkedN++;
                if (mine.FinalBits == rec.Brain.FinalBits) same++;
            }
            Console.WriteLine($"  step-loop check: {same} of {checkedN} bodies bit-identical to DynamicsWorld.Step after {seconds} s");

            // Activity: in-situ amplitude (poses) x brain frequency (alone) x active joints.
            foreach (var rec in records)
            {
                double f = rec.Brain.DominantHz;
                rec.Activity = rec.PoseMeanAmp * f * Math.Max(1, rec.PoseActiveJoints);
            }

            var ranked = records.Where(r => r.PoseSamples >= 20).OrderByDescending(r => r.Activity).ToList();
            var chosen = ranked.Take(top).ToList();
            var rng = new Random(47);
            var rest = ranked.Skip(top).OrderBy(_ => rng.Next()).Take(random).ToList();
            foreach (var r in chosen) r.Group = "top";
            foreach (var r in rest) r.Group = "random";
            var selected = chosen.Concat(rest).ToList();
            if (idsFile != null)
            {
                // The same bodies as another pass chose: "group,id" per line.
                foreach (var r in records) r.Group = null;
                var byId = records.ToDictionary(r => r.Id);
                selected = new List<BodyRecord>();
                foreach (string line in File.ReadLines(idsFile))
                {
                    string[] f = line.Split(',');
                    if (f.Length < 2 || !long.TryParse(f[1], out long id) || !byId.TryGetValue(id, out var r)) continue;
                    r.Group = f[0];
                    selected.Add(r);
                }
            }

            // ---- (C) strokes
            Parallel.ForEach(selected, opts, rec =>
            {
                rec.Passive = Swim(rec, Variant.Passive, steps, null);
                StrokePlan plan = Plan(rec);
                rec.Plan = plan;
                rec.SineIn = Swim(rec, Variant.Servo, steps, plan.WithLag(0));
                rec.SineLag = Swim(rec, Variant.Servo, steps, plan.WithLag(Math.PI / 2));
                rec.Asym = Swim(rec, Variant.Servo, steps, plan.Asymmetric());
                rec.OpenIn = Swim(rec, Variant.Open, steps, plan.WithLag(0));
                rec.OpenLag = Swim(rec, Variant.Open, steps, plan.WithLag(Math.PI / 2));
            });

            WriteOutputs(outDir, label, records, selected);
            return 0;
        }

        // ================================================================ (A)

        private static Dictionary<long, List<double[]>> ReadPoses(
            string path, int from, int to, out int frames, out Dictionary<long, double> heightAtEnd)
        {
            var result = new Dictionary<long, List<double[]>>();
            heightAtEnd = new Dictionary<long, double>();
            frames = 0;
            foreach (string line in File.ReadLines(path))
            {
                int comma = line.IndexOf(',');
                if (comma < 6) continue;
                double t = double.Parse(line.Substring(5, comma - 5), Inv);
                if (t < from || t > to) continue;
                frames++;
                JsonNode frame = Json.Parse(line);
                JsonNode bodies = frame["bodies"];
                for (int i = 0; i < bodies.Count; i++)
                {
                    JsonNode b = bodies[i];
                    long id = (long)b["id"].AsDouble();
                    if (Math.Abs(t - to) < 1e-6) heightAtEnd[id] = b["p"][1].AsDouble();
                    JsonNode q = b["q"];
                    if (q.Count == 0) continue;
                    var v = new double[q.Count];
                    for (int d = 0; d < v.Length; d++) v[d] = q[d].AsDouble();
                    if (!result.TryGetValue(id, out var list)) result[id] = list = new List<double[]>();
                    list.Add(v);
                }
            }
            return result;
        }

        private const double ActiveAmp = 0.05; // rad: a joint whose angle swings less than ~3 degrees is not stroking

        private static void AnalysePoses(BodyRecord rec, List<double[]> samples)
        {
            var good = samples.Where(s => s.Length == rec.Dof).ToList();
            rec.PoseSamples = good.Count;
            if (good.Count < 2) return;
            int n = good.Count;
            rec.PoseSd = new double[rec.Dof];
            var mean = new double[rec.Dof];
            for (int d = 0; d < rec.Dof; d++)
            {
                double m = good.Average(s => s[d]);
                mean[d] = m;
                rec.PoseSd[d] = Math.Sqrt(good.Sum(s => (s[d] - m) * (s[d] - m)) / n);
            }

            // Per joint: the primary dof (largest sd) and its amplitude, sqrt2 * sd (a sinusoid read at random phases).
            rec.PosePrimary = new Dictionary<int, int>();
            rec.PoseAmp = new Dictionary<int, double>();
            foreach (int b in rec.JointLinks)
            {
                int best = rec.DofStart[b];
                for (int d = rec.DofStart[b]; d < rec.DofStart[b] + rec.DofCount[b]; d++)
                    if (rec.PoseSd[d] > rec.PoseSd[best]) best = d;
                rec.PosePrimary[b] = best;
                rec.PoseAmp[b] = Math.Sqrt(2) * rec.PoseSd[best];
            }
            var active = rec.JointLinks.Where(b => rec.PoseAmp[b] >= ActiveAmp).ToList();
            rec.PoseActiveJoints = active.Count;
            rec.PoseMeanAmp = active.Count > 0 ? active.Average(b => rec.PoseAmp[b]) : rec.PoseAmp.Values.DefaultIfEmpty(0).Max();

            // Pairs of active joints: the correlation of their primary angles across samples, which for
            // two sinusoids of one frequency read at phases incoherent with it is cos(phase lag).
            rec.Pairs = new List<(int a, int b, bool chain, double corr)>();
            for (int i = 0; i < active.Count; i++)
                for (int j = i + 1; j < active.Count; j++)
                {
                    int a = active[i], c = active[j];
                    int da = rec.PosePrimary[a], dc = rec.PosePrimary[c];
                    double cov = good.Sum(s => (s[da] - mean[da]) * (s[dc] - mean[dc])) / n;
                    double corr = cov / (rec.PoseSd[da] * rec.PoseSd[dc] + 1e-300);
                    bool chain = rec.Parent[c] == a || rec.Parent[a] == c;
                    rec.Pairs.Add((a, c, chain, corr));
                }
        }

        // ================================================================ (B, C)

        private enum Variant { BrainWorld, BrainLoop, Passive, Servo, Open }

        private sealed class StrokePlan
        {
            public double Hz;
            public Dictionary<int, double> Amp = new Dictionary<int, double>();   // by joint link
            public Dictionary<int, int> Primary = new Dictionary<int, int>();      // dof driven per joint
            public Dictionary<int, int> Rank = new Dictionary<int, int>();         // order down the chain
            public double Lag;
            public bool Skew;

            public StrokePlan WithLag(double lag) => new StrokePlan { Hz = Hz, Amp = Amp, Primary = Primary, Rank = Rank, Lag = lag };
            public StrokePlan Asymmetric() => new StrokePlan { Hz = Hz, Amp = Amp, Primary = Primary, Rank = Rank, Lag = 0, Skew = true };

            // Target angle and rate of joint b at time t.
            public (double q, double qd) Target(int b, double t)
            {
                double a = Amp[b];
                double w = 2 * Math.PI * Hz;
                double phase = w * t - Rank[b] * Lag;
                if (!Skew) return (a * Math.Sin(phase), a * w * Math.Cos(phase));
                // Fast out (a quarter of the period, -a to +a), slow back (three quarters), each a half cosine.
                double T = 1.0 / Hz;
                double u = ((phase / w) % T + T) % T / T;
                if (u < 0.25)
                {
                    double s = u / 0.25;
                    return (-a * Math.Cos(Math.PI * s), a * Math.PI * Math.Sin(Math.PI * s) / (0.25 * T));
                }
                else
                {
                    double s = (u - 0.25) / 0.75;
                    return (a * Math.Cos(Math.PI * s), -a * Math.PI * Math.Sin(Math.PI * s) / (0.75 * T));
                }
            }

            // Open loop: a unit-amplitude torque signal of the same phase.
            public double Signal(int b, double t)
            {
                double w = 2 * Math.PI * Hz;
                return Math.Sin(w * t - Rank[b] * Lag);
            }
        }

        private static StrokePlan Plan(BodyRecord rec)
        {
            var plan = new StrokePlan();
            double hz = rec.Brain.DominantAmp >= 0.05 ? rec.Brain.DominantHz : 1.0;
            plan.Hz = Math.Max(0.3, Math.Min(2.5, hz));
            // Rank: depth of the joint in the tree (joints below joints lag further).
            int k = 0;
            foreach (int b in rec.JointLinks)
            {
                int depth = 0;
                for (int p = rec.Parent[b]; p > 0; p = rec.Parent[p]) if (rec.DofCount[p] > 0) depth++;
                plan.Rank[b] = depth + 0 * k++;
                int primary = rec.PosePrimary != null ? rec.PosePrimary[b] : rec.DofStart[b];
                plan.Primary[b] = primary;
                double observed = rec.PoseAmp != null ? rec.PoseAmp[b] : 0;
                double room = 0.9 * Math.Min(Math.Abs(rec.LimitLo[primary]), Math.Abs(rec.LimitHi[primary]));
                plan.Amp[b] = Math.Min(room, Math.Max(0.15, observed));
            }
            // A star (every joint on the root) has no chain: then rank by link order so a lag still exists.
            if (plan.Rank.Values.All(r => r == 0))
            {
                int r = 0;
                foreach (int b in rec.JointLinks) plan.Rank[b] = r++;
            }
            return plan;
        }

        private const double Kp = 4.0;   // signal per radian
        private const double Kd = 0.25;  // signal per rad/s

        private static SwimResult Swim(BodyRecord rec, Variant variant, int steps, StrokePlan plan)
        {
            var body = new Creature(0, rec.Adult, _solver, _config.Shapes);
            double y0 = Math.Max(-40.0, Math.Min(-2.0, rec.StartHeight));
            body.PlaceAsDeveloped(new Vec3(0, y0, 0), rec.Adult);

            DynamicsWorld world = new DynamicsWorld(_solver);
            world.Add(body);
            var list = new List<Creature> { body };
            var grid = new ContactGrid();
            int[] scratch = new int[32];
            var jtArgs = new object[] { body };

            int dof = body.Dof;
            int every = (int)Math.Round(0.5 / _dt);
            int keepFrom = steps / 2;
            var com = new List<Vec3>();
            var root = new List<Vec3>();
            int m = steps - keepFrom;
            var q = new double[dof][];
            var qd = new double[dof][];
            var sig = new double[dof][];
            for (int d = 0; d < dof; d++) { q[d] = new double[m]; qd[d] = new double[m]; sig[d] = new double[m]; }

            double t = 0;
            bool lost = false;
            for (int step = 0; step <= steps; step++)
            {
                if (step % every == 0)
                {
                    com.Add(body.CentreOfMass());
                    root.Add(new Vec3(body.Position[0], body.Position[1], body.Position[2]));
                }
                if (step == steps) break;

                if (variant == Variant.BrainWorld)
                {
                    world.Step();
                }
                else
                {
                    if (_solver.ContactPerPart) grid.BuildLinks(list); else grid.Build(list);
                    if (_uniformWater != 0 && variant == Variant.BrainLoop)
                        for (int i = 0; i < body.Links; i++) body.Water[3 * i] = _uniformWater;
                    body.Senses.Sample();
                    if (variant == Variant.BrainLoop) body.Brain.Step((float)_dt, body.DriveSignal, body.Senses);
                    else
                    {
                        Array.Clear(body.DriveSignal, 0, body.DriveSignal.Length);
                        if (variant != Variant.Passive)
                        {
                            foreach (int b in rec.JointLinks)
                            {
                                for (int d = body.DofStart[b]; d < body.DofStart[b] + body.DofCount[b]; d++)
                                {
                                    double s;
                                    if (variant == Variant.Open)
                                        s = d == plan.Primary[b] ? plan.Signal(b, t) : 0.0;
                                    else
                                    {
                                        var (tq, tqd) = d == plan.Primary[b] ? plan.Target(b, t) : (0.0, 0.0);
                                        s = Kp * (tq - body.Q[d]) + Kd * (tqd - body.Qd[d]);
                                    }
                                    body.DriveSignal[d] = (float)Math.Max(-1, Math.Min(1, s));
                                }
                            }
                        }
                    }
                    Array.Clear(body.Fext, 0, body.Fext.Length);
                    Array.Clear(body.Tau, 0, body.Tau.Length);
                    body.Drive.Drive(body.DriveSignal);
                    Fluid.Apply(body, _solver);
                    Contacts.Apply(body, 0, grid, _solver, ref scratch);
                    _jointTorques.Invoke(world, jtArgs);
                    Aba.Solve(body);
                    Aba.Integrate(body, _dt);
                    Kinematics.Poses(body);
                    Kinematics.Velocities(body);
                    body.Settle(_dt);
                    if (!body.IsFinite()) { body.Alive = false; body.MarkLost(); }
                    else body.RefreshContactSphere();
                    body.CommitContactSphere();
                }
                t += _dt;

                if (!body.Alive) { lost = true; break; }
                if (step >= keepFrom && step - keepFrom < m)
                {
                    int k = step - keepFrom;
                    for (int d = 0; d < dof; d++) { q[d][k] = body.Q[d]; qd[d][k] = body.Qd[d]; sig[d][k] = body.DriveSignal[d]; }
                }
            }

            var res = new SwimResult { Lost = lost };
            if (lost) return res;
            res.FinalBits = BitConverter.DoubleToInt64Bits(body.Position[0]) ^ BitConverter.DoubleToInt64Bits(body.Position[1]) * 31 ^ BitConverter.DoubleToInt64Bits(body.Spin[0]) * 7;

            int half = com.Count / 2;   // the sample at t = seconds/2
            Vec3 a = com[half], z = com[com.Count - 1];
            double window = (com.Count - 1 - half) * 0.5;
            res.Horizontal = Math.Sqrt((z.X - a.X) * (z.X - a.X) + (z.Z - a.Z) * (z.Z - a.Z));
            res.Vertical = z.Y - a.Y;
            double path = 0;
            for (int i = half + 1; i < com.Count; i++)
            {
                Vec3 u = com[i], v = com[i - 1];
                path += Math.Sqrt((u.X - v.X) * (u.X - v.X) + (u.Z - v.Z) * (u.Z - v.Z));
            }
            res.PathSpeed = path / window;
            res.NetSpeed = res.Horizontal / window;
            res.Net3 = (z - a).Magnitude;
            double rootPath = 0;
            for (int i = half + 1; i < root.Count; i++) rootPath += (root[i] - root[i - 1]).Magnitude;
            res.RootPathSpeed = rootPath / window;

            // Per joint (primary dof by alone amplitude): amplitude, dominant frequency, stroke asymmetry.
            res.JointAmp = new Dictionary<int, double>();
            res.JointHz = new Dictionary<int, double>();
            res.JointAsym = new Dictionary<int, double>();
            res.JointSigRms = new Dictionary<int, double>();
            res.JointPrimary = new Dictionary<int, int>();
            foreach (int b in rec.JointLinks)
            {
                int best = body.DofStart[b];
                double bestAmp = -1;
                for (int d = body.DofStart[b]; d < body.DofStart[b] + body.DofCount[b]; d++)
                {
                    double amp = (q[d].Max() - q[d].Min()) / 2;
                    if (amp > bestAmp) { bestAmp = amp; best = d; }
                }
                res.JointPrimary[b] = best;
                res.JointAmp[b] = bestAmp;
                res.JointHz[b] = DominantHz(q[best], _dt);
                double num = 0, den = 0;
                for (int k = 0; k < m; k++) { num += qd[best][k] * Math.Abs(qd[best][k]); den += qd[best][k] * qd[best][k]; }
                res.JointAsym[b] = den > 0 ? num / den : 0;
                double ss = 0; for (int k = 0; k < m; k++) ss += sig[best][k] * sig[best][k];
                res.JointSigRms[b] = Math.Sqrt(ss / m);
            }
            int lead = res.JointAmp.OrderByDescending(kv => kv.Value).First().Key;
            res.DominantHz = res.JointHz[lead];
            res.DominantAmp = res.JointAmp[lead];
            res.LeadAsym = res.JointAsym[lead];
            res.ActiveJoints = res.JointAmp.Count(kv => kv.Value >= ActiveAmp);
            res.PerStroke = res.DominantHz > 0 ? res.Horizontal / (window * res.DominantHz) : double.NaN;

            // Phase lag between active joint pairs from the alone traces (cross-spectrum at the lead frequency).
            res.PairLagDeg = new List<(int, int, bool, double)>();
            var act = res.JointAmp.Where(kv => kv.Value >= ActiveAmp).Select(kv => kv.Key).ToList();
            for (int i = 0; i < act.Count; i++)
                for (int j = i + 1; j < act.Count; j++)
                {
                    double pa = PhaseAt(q[res.JointPrimary[act[i]]], _dt, res.DominantHz);
                    double pc = PhaseAt(q[res.JointPrimary[act[j]]], _dt, res.DominantHz);
                    double lag = Math.Abs(Math.IEEERemainder(pa - pc, 2 * Math.PI)) * 180 / Math.PI;
                    bool chain = rec.Parent[act[j]] == act[i] || rec.Parent[act[i]] == act[j];
                    res.PairLagDeg.Add((act[i], act[j], chain, lag));
                }
            return res;
        }

        private static double DominantHz(double[] x, double dt)
        {
            double mean = x.Average();
            double best = 0, bestHz = 0;
            for (double f = 0.05; f <= 5.0001; f += 0.01)
            {
                double w = 2 * Math.PI * f * dt, re = 0, im = 0;
                for (int k = 0; k < x.Length; k += 2) { re += (x[k] - mean) * Math.Cos(w * k); im += (x[k] - mean) * Math.Sin(w * k); }
                double p = re * re + im * im;
                if (p > best) { best = p; bestHz = f; }
            }
            return bestHz;
        }

        private static double PhaseAt(double[] x, double dt, double f)
        {
            double mean = x.Average(), w = 2 * Math.PI * f * dt, re = 0, im = 0;
            for (int k = 0; k < x.Length; k++) { re += (x[k] - mean) * Math.Cos(w * k); im += (x[k] - mean) * Math.Sin(w * k); }
            return Math.Atan2(im, re);
        }

        // ================================================================ output

        private static string F(double v, string fmt = "0.####") => double.IsNaN(v) ? "nan" : v.ToString(fmt, Inv);

        private static void WriteOutputs(string outDir, string label, List<BodyRecord> records, List<BodyRecord> selected)
        {
            var a = new StringBuilder();
            a.AppendLine("id,parts,dof,joints,jointTypes,poseSamples,poseActiveJoints,poseMeanAmp,poseAmps,pairs(a-b:chain:corr),aloneHz,aloneAmp,aloneActive,aloneAsym,aloneSigRms,aloneHoriz60,aloneVert60,aloneNetSpeed,alonePathSpeed,alonePerStroke,alonePairLags,activity,lost,lengthM,mass");
            foreach (var r in records)
            {
                var br = r.Brain;
                a.Append(r.Id).Append(',').Append(r.Parts).Append(',').Append(r.Dof).Append(',').Append(r.JointLinks.Count).Append(',').Append(r.JointTypes).Append(',');
                a.Append(r.PoseSamples).Append(',').Append(r.PoseActiveJoints).Append(',').Append(F(r.PoseMeanAmp)).Append(',');
                a.Append(r.PoseAmp == null ? "" : string.Join(" ", r.JointLinks.Select(b => F(r.PoseAmp[b], "0.###")))).Append(',');
                a.Append(r.Pairs == null ? "" : string.Join(" ", r.Pairs.Select(p => $"{p.a}-{p.b}:{(p.chain ? "c" : "s")}:{F(p.corr, "0.##")}"))).Append(',');
                if (br.Lost) a.Append(",,,,,,,,,,");
                else
                {
                    a.Append(F(br.DominantHz, "0.##")).Append(',').Append(F(br.DominantAmp, "0.###")).Append(',').Append(br.ActiveJoints).Append(',');
                    a.Append(F(br.LeadAsym, "0.###")).Append(',').Append(F(br.JointSigRms.Values.Max(), "0.###")).Append(',');
                    a.Append(F(br.Horizontal)).Append(',').Append(F(br.Vertical)).Append(',').Append(F(br.NetSpeed, "0.#####")).Append(',').Append(F(br.PathSpeed, "0.#####")).Append(',').Append(F(br.PerStroke, "0.#####")).Append(',');
                    a.Append(string.Join(" ", br.PairLagDeg.Select(p => $"{p.Item1}-{p.Item2}:{(p.Item3 ? "c" : "s")}:{F(p.Item4, "0")}"))).Append(',');
                }
                a.Append(F(r.Activity)).Append(',').Append(br.Lost ? "LOST" : "").Append(',').Append(F(r.Length, "0.###")).Append(',').Append(F(r.Mass, "0.###")).AppendLine();
            }
            File.WriteAllText(Path.Combine(outDir, label + "-bodies.csv"), a.ToString());

            var c = new StringBuilder();
            c.AppendLine("group,id,parts,joints,jointTypes,planHz,planAmps,variant,horiz60,vert60,netSpeed,pathSpeed,perStroke,leadAmp,leadHz,leadAsym,sigRms,lost,lengthM");
            foreach (var r in selected)
            {
                foreach (var (name, res) in new[] { ("brain", r.Brain), ("passive", r.Passive), ("servoSineIn", r.SineIn), ("servoSineLag90", r.SineLag), ("servoAsym", r.Asym), ("openSineIn", r.OpenIn), ("openSineLag90", r.OpenLag) })
                {
                    c.Append(r.Group).Append(',').Append(r.Id).Append(',').Append(r.Parts).Append(',').Append(r.JointLinks.Count).Append(',').Append(r.JointTypes).Append(',');
                    c.Append(F(r.Plan.Hz, "0.##")).Append(',').Append(string.Join(" ", r.JointLinks.Select(b => F(r.Plan.Amp[b], "0.##")))).Append(',').Append(name).Append(',');
                    if (res.Lost) { c.AppendLine(",,,,,,,,,LOST"); continue; }
                    c.Append(F(res.Horizontal)).Append(',').Append(F(res.Vertical)).Append(',').Append(F(res.NetSpeed, "0.#####")).Append(',').Append(F(res.PathSpeed, "0.#####")).Append(',').Append(F(res.PerStroke, "0.#####")).Append(',');
                    c.Append(F(res.DominantAmp, "0.###")).Append(',').Append(F(res.DominantHz, "0.##")).Append(',').Append(F(res.LeadAsym, "0.###")).Append(',').Append(F(res.JointSigRms.Values.Max(), "0.###")).Append(",,").Append(F(r.Length, "0.###")).AppendLine();
                }
            }
            File.WriteAllText(Path.Combine(outDir, label + "-strokes.csv"), c.ToString());
            Console.WriteLine($"  wrote {label}-bodies.csv ({records.Count}) and {label}-strokes.csv ({selected.Count} bodies x 7)");
        }

        private sealed class BodyRecord
        {
            public long Id;
            public Phenotype Adult;
            public int Dof, Links, Parts;
            public List<int> JointLinks;
            public int[] DofStart, DofCount, Parent;
            public double[] LimitLo, LimitHi;
            public string JointTypes;
            public double StartHeight;
            public double Length, Mass;
            public int PoseSamples;
            public double[] PoseSd;
            public Dictionary<int, int> PosePrimary;
            public Dictionary<int, double> PoseAmp;
            public int PoseActiveJoints;
            public double PoseMeanAmp;
            public List<(int a, int b, bool chain, double corr)> Pairs;
            public double Activity;
            public string Group;
            public StrokePlan Plan;
            public SwimResult Brain, Passive, SineIn, SineLag, Asym, OpenIn, OpenLag;
        }

        private sealed class SwimResult
        {
            public bool Lost;
            public long FinalBits;
            public double Horizontal, Vertical, NetSpeed, PathSpeed, RootPathSpeed, Net3, PerStroke;
            public double DominantHz, DominantAmp, LeadAsym;
            public int ActiveJoints;
            public Dictionary<int, double> JointAmp, JointHz, JointAsym, JointSigRms;
            public Dictionary<int, int> JointPrimary;
            public List<(int, int, bool, double)> PairLagDeg;
        }
    }
}
