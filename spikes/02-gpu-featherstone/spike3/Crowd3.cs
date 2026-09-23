using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Evosim.Core;
using Evosim.Dynamics;

namespace Gpu.Spike3
{
    /// <summary>
    /// A recorded crowd rebuilt where it stood, for the contact and brain kernels of the third
    /// spike: every living body's genome from the snapshot at one second, developed with the
    /// module counts and lost parts the row carries, and put down at the root pose and joint
    /// angles <c>poses.jsonl</c> recorded at that second.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The rebuild is a copy of the Dynamics bench's record mode</b>
    /// (<c>src/Evosim.Dynamics.Bench/Program.cs</c>, <c>Record</c>, lines 587-755 at commit
    /// d153224): the same development call, the same lost-path pruning, the same
    /// <c>PlaceAt</c>, joint angles and <c>Kinematics.Refresh</c>, and the same
    /// <c>AddInIdOrder</c>. Copied rather than called because it is a private method of an
    /// executable.
    /// </para>
    /// <para>
    /// <b>poses.jsonl, not positions.jsonl.</b> The brief named positions.jsonl; poses.jsonl is the
    /// same second's root positions plus the root attitude and the joint angles, which a
    /// positions row does not carry. The root positions agree between the two files.
    /// </para>
    /// </remarks>
    internal sealed class Crowd3
    {
        public RunConfig Config;
        public SolverConfig Solver;
        public BedShape Bed;
        public string Run;
        public int At;
        public int GenomesRead, GenomesRefused, WithPlan, Missing, Undeveloped, DofMismatch;

        public readonly List<Plan> Plans = new List<Plan>();

        internal sealed class Plan
        {
            public long Id;
            public Phenotype Adult;
            public Vec3 P;
            public QuatD R;
            public double[] Q;
        }

        public double TankRadius => Solver.TankRadiusMetres;

        public static Crowd3 Load(string run, int at)
        {
            var crowd = new Crowd3 { Run = run, At = at };

            crowd.Config = RunConfigJson.Read(
                File.ReadAllText(Path.Combine(run, "config.json")), out string mismatch);
            if (mismatch != null) throw new InvalidDataException("config.json: " + mismatch);

            // The world's own bed, built as World's constructor builds it (World.cs:1001-1005),
            // from the run's seed, so the contact's twelve cosines are the run's.
            RunConfig c = crowd.Config;
            ulong seed = ReadSeed(run);
            double radius = TankGeometry.RadiusFor(c.WorldAreaSquareMetres);
            crowd.Bed = c.WorldShape == WorldShape.Tank && (c.BedReliefMetres > 0f || c.BedTiltMetres > 0f)
                ? new BedShape((float)radius, c.WorldDepthMetres, c.BedReliefMetres,
                    c.BedTiltMetres, c.BedScaleMetres, Rng.SeedFor(seed, World.BedShapeIndex))
                : null;

            // The farm's solver (Simulation.cs:194) less the world: the water is still, as in the
            // bench's record mode. Contact on, instrument off.
            crowd.Solver = SolverConfig.FromWorld(c, 0.01, crowd.Bed, null);
            crowd.Solver.CreatureContact = true;
            crowd.Solver.ContactInstrument = false;

            string snapshotPath = Path.Combine(
                run, "snapshots", at.ToString("000000000", CultureInfo.InvariantCulture) + ".jsonl");

            var genomes = new Dictionary<long, Genome>();
            var recordedCounts = new Dictionary<long, int[]>();
            var recordedLost = new Dictionary<long, List<int[]>>();
            foreach (string line in File.ReadLines(snapshotPath))
            {
                if (line.Length == 0) continue;
                try
                {
                    long id = GenomeJson.ReadId(line);
                    genomes[id] = GenomeJson.Read(line);
                    int[] counts = GenomeJson.ReadModuleCounts(line);
                    List<int[]> lost = GenomeJson.ReadLostPartPaths(line);
                    if (counts != null) recordedCounts[id] = counts;
                    if (lost != null) recordedLost[id] = lost;
                    if (counts != null || lost != null) crowd.WithPlan++;
                    crowd.GenomesRead++;
                }
                catch (Exception) { crowd.GenomesRefused++; }
            }

            string marker = "\"t\":" + at.ToString(CultureInfo.InvariantCulture) + ",";
            JsonNode frame = null;
            foreach (string line in File.ReadLines(Path.Combine(run, "poses.jsonl")))
            {
                int head = line.Length < 24 ? line.Length : 24;
                if (line.IndexOf(marker, 0, head, StringComparison.Ordinal) < 0) continue;
                frame = Json.Parse(line);
                break;
            }
            if (frame == null) throw new InvalidDataException("no poses.jsonl row at t=" + at);

            JsonNode bodies = frame["bodies"];
            for (int i = 0; i < bodies.Count; i++)
            {
                JsonNode body = bodies[i];
                long id = (long)body["id"].AsDouble();
                if (!genomes.TryGetValue(id, out Genome genome)) { crowd.Missing++; continue; }

                recordedCounts.TryGetValue(id, out int[] counts);

                Phenotype adult;
                try
                {
                    var paths = new List<int[]>();
                    adult = Developer.Develop(genome, c.Development, null, c.Shapes, counts, paths);

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
                                for (int s = 0; s < path.Length && same; s++) same = paths[k][s] == path[s];
                                if (!same) continue;
                                drop[k] = true;
                                any = true;
                                break;
                            }
                        }
                        if (any) adult = adult.WithoutSubtrees(drop, out _);
                    }
                }
                catch (Exception) { crowd.Undeveloped++; continue; }

                JsonNode p = body["p"], r = body["r"], q = body["q"];
                var angles = new double[q.Count];
                for (int d = 0; d < angles.Length; d++) angles[d] = q[d].AsDouble();

                crowd.Plans.Add(new Plan
                {
                    Id = id,
                    Adult = adult,
                    P = new Vec3(p[0].AsDouble(), p[1].AsDouble(), p[2].AsDouble()),
                    R = new QuatD(r[0].AsDouble(), r[1].AsDouble(), r[2].AsDouble(), r[3].AsDouble()),
                    Q = angles,
                });
            }

            crowd.Plans.Sort((a, b) => a.Id.CompareTo(b.Id));
            return crowd;
        }

        private static ulong ReadSeed(string run)
        {
            JsonNode manifest = Json.Parse(File.ReadAllText(Path.Combine(run, "run.json")));
            return (ulong)manifest["seed"].AsDouble();
        }

        /// <summary>One creature per plan, placed as the bench places it, in id order.</summary>
        public List<Creature> Build(SolverConfig solver, IList<Plan> plans, IList<Vec3> offsets = null,
                                    IList<int> ids = null)
        {
            var list = new List<Creature>(plans.Count);
            DofMismatch = 0;
            for (int k = 0; k < plans.Count; k++)
            {
                Plan plan = plans[k];
                int id = ids != null ? ids[k] : (int)plan.Id;
                var creature = new Creature(id, plan.Adult, solver, Config.Shapes);
                Vec3 at = offsets != null ? plan.P + offsets[k] : plan.P;
                creature.PlaceAt(at, plan.R);

                if (plan.Q.Length == creature.Dof)
                {
                    Array.Copy(plan.Q, creature.Q, plan.Q.Length);
                    Kinematics.Refresh(creature);
                    creature.RefreshContactSphere();
                    creature.CommitContactSphere();
                }
                else DofMismatch++;

                list.Add(creature);
            }
            return list;
        }

        /// <summary>
        /// A larger crowd at the recorded one's number density: the bodies whose roots lie in the
        /// square inscribed in the tank's disc, tiled edge to edge over the plane, and the
        /// <paramref name="n"/> nearest the new axis kept. Returns the plans, the offsets and
        /// fresh ascending ids, and the radius of the disc that holds them.
        /// </summary>
        /// <remarks>
        /// The inscribed square rather than the disc, because a disc does not tile: copies of the
        /// disc would leave gaps and the density between copies would fall. The square carries
        /// the crowd's own clustering and its own vertical distribution; only its seams are new,
        /// where two copies meet bodies that were never neighbours in the run.
        /// </remarks>
        public (List<Plan> plans, List<Vec3> offsets, List<int> ids, double radius, double side, int inSquare)
            Tile(int n)
        {
            double R = TankRadius;
            double side = R * Math.Sqrt(2.0);
            double lo = R - side / 2, hi = R + side / 2;

            var square = new List<Plan>();
            foreach (Plan p in Plans)
            {
                if (p.P.X >= lo && p.P.X < hi && p.P.Z >= lo && p.P.Z < hi) square.Add(p);
            }

            // Copies centred on the origin first; the new axis is placed after the cut.
            double density = square.Count / (side * side);
            double wanted = Math.Sqrt(n / (Math.PI * density));
            int reach = (int)Math.Ceiling(wanted / side) + 1;

            var candidates = new List<(double r2, Plan plan, Vec3 offset, int copy)>();
            int copy = 0;
            for (int ix = -reach; ix <= reach; ix++)
            for (int iz = -reach; iz <= reach; iz++, copy++)
            {
                // Square centred on (R, R) moved to be centred on (ix*side, iz*side).
                var offset = new Vec3(ix * side - R, 0, iz * side - R);
                foreach (Plan p in square)
                {
                    double x = p.P.X + offset.X, z = p.P.Z + offset.Z;
                    candidates.Add((x * x + z * z, p, offset, copy));
                }
            }

            candidates.Sort((a, b) =>
            {
                int c = a.r2.CompareTo(b.r2);
                if (c != 0) return c;
                c = a.copy.CompareTo(b.copy);
                return c != 0 ? c : a.plan.Id.CompareTo(b.plan.Id);
            });

            if (candidates.Count < n) throw new InvalidOperationException("tiling too small");
            double radius = Math.Sqrt(candidates[n - 1].r2) + 1.0;

            // Ids ascending in (copy, id) order so the list is in id order as AddInIdOrder wants.
            var chosen = candidates.GetRange(0, n);
            chosen.Sort((a, b) => a.copy != b.copy ? a.copy.CompareTo(b.copy) : a.plan.Id.CompareTo(b.plan.Id));

            var plans = new List<Plan>(n);
            var offsets = new List<Vec3>(n);
            var ids = new List<int>(n);
            foreach (var c in chosen)
            {
                plans.Add(c.plan);
                // The new tank's axis at (radius, radius), Core's frame.
                offsets.Add(new Vec3(c.offset.X + radius, 0, c.offset.Z + radius));
                ids.Add(c.copy * 100000 + (int)c.plan.Id);
            }

            return (plans, offsets, ids, radius, side, square.Count);
        }

        /// <summary>A copy of the solver with the glass moved to another tank's radius.</summary>
        public SolverConfig SolverFor(double tankRadius)
        {
            SolverConfig s = SolverConfig.FromWorld(Config, 0.01, Bed, null);
            s.CreatureContact = true;
            s.ContactInstrument = false;
            s.TankRadiusMetres = tankRadius;
            return s;
        }

        /// <summary>A deterministic number in [0, 1) from two integers: the spike's synthetic inputs.</summary>
        public static double Hash01(long a, long b)
        {
            ulong h = 1469598103934665603UL;
            h ^= (ulong)a; h *= 1099511628211UL; h ^= h >> 29;
            h ^= (ulong)b; h *= 1099511628211UL; h ^= h >> 32;
            h *= 0x9E3779B97F4A7C15UL; h ^= h >> 31;
            return (h >> 11) * (1.0 / 9007199254740992.0);
        }
    }
}
