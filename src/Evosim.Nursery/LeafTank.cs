using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Evosim.Core;
using Evosim.Dynamics.Placement;
using Evosim.Farm;

namespace Evosim.Nursery
{
    /// <summary>
    /// A leaf pool (logbook/specs/nursery-leaf-tank-plan.md, step 1; written by <c>scripts/reads/leaf-pool.py</c>): gzipped text, one living
    /// leaf a line, its id, a tab, its height y (m, negative down), a tab, its body fraction as the snapshot held it, a tab, and its genome's
    /// JSON. Every row is read and checked when the pool is read, and a fault is refused naming its line.
    /// </summary>
    public sealed class LeafPool
    {
        public sealed class Leaf
        {
            /// <summary>The leaf's id in the run it was read from. A label: nothing in the tank reads it.</summary>
            public long Id;
            public float HeightY, BodyFraction;
            public Genome Genome;
        }

        public string Path { get; private set; }

        public IReadOnlyList<Leaf> Leaves { get; private set; }

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Reads and checks every row; <see cref="InvalidDataException"/> names the file and the line of the first fault.</summary>
        public static LeafPool Read(string path)
        {
            var leaves = new List<Leaf>();
            var lineOf = new Dictionary<long, int>();
            int n = 0;
            foreach (string line in Lines(path))
            {
                n++;
                string at = $"{path} line {n}";
                if (line.Length == 0) throw new InvalidDataException(at + " is empty");
                string[] f = line.Split('\t', 4);
                if (f.Length != 4) throw new InvalidDataException(at + " has " + f.Length + " fields; a leaf is an id, a height, a body fraction and a genome, tab-separated");
                if (!long.TryParse(f[0], NumberStyles.AllowLeadingSign, Inv, out long id))
                    throw new InvalidDataException(at + ": the id \"" + f[0] + "\" is not a whole number");
                if (lineOf.TryGetValue(id, out int first)) throw new InvalidDataException(at + ": the id " + id + " is on line " + first + " too");
                lineOf[id] = n;
                if (!float.TryParse(f[1], NumberStyles.Float, Inv, out float y) || !float.IsFinite(y) || y > 0f)
                    throw new InvalidDataException(at + ": the height \"" + f[1] + "\" is not a finite number at or below the surface (0)");
                if (!float.TryParse(f[2], NumberStyles.Float, Inv, out float fraction) || !(fraction > 0f && fraction <= 1f))
                    throw new InvalidDataException(at + ": the body fraction \"" + f[2] + "\" is not a number in (0, 1]");
                Genome genome;
                try { genome = GenomeJson.Read(f[3]); }
                catch (Exception e) when (e is InvalidDataException || e is FormatException || e is InvalidOperationException || e is ArgumentException)
                {
                    throw new InvalidDataException(at + ": the genome does not load: " + e.Message, e);
                }
                leaves.Add(new Leaf { Id = id, HeightY = y, BodyFraction = fraction, Genome = genome });
            }
            if (leaves.Count == 0) throw new InvalidDataException(path + " holds no leaves");
            return new LeafPool { Path = path, Leaves = leaves };
        }

        /// <summary>The pool's rows, counted without reading a genome: what a recipe checks its count against.</summary>
        public static int CountRows(string path)
        {
            int n = 0;
            foreach (string line in Lines(path)) if (line.Length > 0) n++;
            return n;
        }

        private static IEnumerable<string> Lines(string path)
        {
            using (var file = File.OpenRead(path))
            using (var gz = new GZipStream(file, CompressionMode.Decompress))
            using (var reader = new StreamReader(gz, new UTF8Encoding(false)))
            {
                string line;
                while ((line = reader.ReadLine()) != null) yield return line;
            }
        }
    }

    /// <summary>A pool file's lines (a body, a tab, a genome's JSON) asked a question of each genome.</summary>
    public static class PoolLines
    {
        /// <summary>Each line whose genome <paramref name="why"/> answers with a reason, by its 1-based number in the file; empty lines are skipped.</summary>
        public static List<(int line, string why)> Refused(IReadOnlyList<string> lines, Func<Genome, string> why)
        {
            var refused = new List<(int line, string why)>();
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Length == 0) continue;
                string w = why(GenomeJson.Read(lines[i].Substring(lines[i].IndexOf('\t') + 1)));
                if (w != null) refused.Add((i + 1, w));
            }
            return refused;
        }
    }

    /// <summary>D182's patchy leaf tank: where the patches lie, and how <c>--check</c> reads a tank's layout.</summary>
    public static class LeafPatches
    {
        /// <summary>Where one tank's leaves were laid, for <c>--check</c>: the patches' centres (none without patches), and each leaf's place, radius and patch.</summary>
        public sealed class Layout
        {
            public readonly List<(double x, double z)> Centres = new List<(double x, double z)>();
            /// <summary>The ring's turn this tank drew, radians: the angle of patch 0 (D236's plan stands on it). 0 without patches.</summary>
            public double Turn;
            public readonly List<(float x, float z, float radius, int patch)> Leaves = new List<(float x, float z, float radius, int patch)>();
        }

        /// <summary>
        /// D182: the centre of patch <paramref name="p"/> of <paramref name="count"/>, evenly spaced on a ring of <paramref name="ring"/> about
        /// the axis of a tank of radius <paramref name="tank"/> (at <c>(tank, tank)</c>), the ring turned by <paramref name="turn"/> radians.
        /// </summary>
        public static (double x, double z) Centre(double tank, double ring, int count, double turn, int p)
        {
            double a = turn + 2d * Math.PI * p / count;
            return (tank + ring * Math.Cos(a), tank + ring * Math.Sin(a));
        }

        /// <summary>
        /// One seed's patch layout as <c>--check</c> prints it, with the nearest gap between leaves of different patches,
        /// the nearest leaf edge to the glass, and the share of the tank's surface (on a 0.5 m grid) farther than <paramref name="reach"/>
        /// from every leaf's centre. All horizontal: the smell is read in three dimensions, so the share is an upper bound on the water a
        /// swimmer at a leaf's height smells nothing in.
        /// </summary>
        public static (string line, double gap, double glass, double free) Describe(string name, Layout layout, double tank, double reach)
        {
            var perPatch = new int[layout.Centres.Count];
            foreach (var l in layout.Leaves) perPatch[l.patch]++;

            double gap = double.PositiveInfinity, glass = double.PositiveInfinity;
            for (int i = 0; i < layout.Leaves.Count; i++)
            {
                var a = layout.Leaves[i];
                glass = Math.Min(glass, tank - Math.Sqrt((a.x - tank) * (a.x - tank) + (a.z - tank) * (a.z - tank)) - a.radius);
                for (int j = i + 1; j < layout.Leaves.Count; j++)
                {
                    var b = layout.Leaves[j];
                    if (a.patch == b.patch) continue;
                    gap = Math.Min(gap, Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z)) - a.radius - b.radius);
                }
            }

            int inside = 0, free = 0;
            const double cell = 0.5;
            for (double x = cell / 2; x < 2 * tank; x += cell)
                for (double z = cell / 2; z < 2 * tank; z += cell)
                {
                    if ((x - tank) * (x - tank) + (z - tank) * (z - tank) > tank * tank) continue;
                    inside++;
                    bool smelt = false;
                    foreach (var l in layout.Leaves)
                        if ((x - l.x) * (x - l.x) + (z - l.z) * (z - l.z) <= reach * reach) { smelt = true; break; }
                    if (!smelt) free++;
                }
            double share = inside == 0 ? 0d : (double)free / inside;

            string centres = string.Join(" ", layout.Centres.Select(c => FormattableString.Invariant($"({c.x:0.0}, {c.z:0.0})")));
            string line = FormattableString.Invariant(
                $"  {name}: centres {centres}; leaves {string.Join("/", perPatch)}; nearest gap {gap:0.0} m; edge to glass {glass:0.0} m; smell-free {share:0.00}");
            return (line, gap, glass, share);
        }
    }

    internal static partial class Program
    {
        /// <summary>The recipe's leaf pool, read by <see cref="ApplyRecipe"/>; null when the recipe has no leaves, and then nothing below runs.</summary>
        private static LeafPool _leafPool;

        /// <summary>The salt of the leaves' own stream: which rows, and where. The episode's other draws are not on it, so they move for nothing here.</summary>
        private const ulong LeafSalt = 0x6C656166UL;

        /// <summary>Draws for one leaf's place before the tank is given up as too full to hold it.</summary>
        private const int LeafAttempts = 64;

        /// <summary>
        /// The leaf tank's water and leaves (logbook/specs/nursery-leaf-tank-plan.md, step 2), laid before the bred bodies: the spent matter at
        /// the recipe's density, then the recipe's count of distinct pool rows, each at an x and z drawn uniformly over the water inside the
        /// glass and at its recorded height kept inside the water, each paid the growth it owes so that it starts grown. Every draw is from the
        /// episode seed's own leaf stream. Returns the leaves, which are not members: they have no slot, no record and no score.
        /// <para>
        /// The leaves are placed largest first, by the radius of the sphere that bounds each (the coordinator's choice of 2026-10-13). In
        /// the order they were drawn, a large leaf drawn late found no room in five of the first leaf arms' 272 tanks and stopped both
        /// runs; largest first places all 272, with the same leaves at the same heights. Only where each stands in x and z moves.
        /// </para>
        /// </summary>
        /// <summary>The recipe's leaf patches (D182), or null when its leaves lie over the whole tank.</summary>
        private static Recipe.PatchSetting LeafPatchesSet => _o.LeafPatchCount == 0 ? null
            : new Recipe.PatchSetting { Count = _o.LeafPatchCount, RadiusMetres = _o.LeafPatchRadius, RingMetres = _o.LeafPatchRing };

        private static List<Organism> LayLeaves(World world, Simulation sim, ulong seed, RunConfig config, LeafPatches.Layout layout = null)
        {
            if (_o.LeafSpent > 0d) world.LaySpentMatter((float)_o.LeafSpent);

            var rng = new Rng(Rng.SeedFor(seed, LeafSalt));
            IReadOnlyList<LeafPool.Leaf> pool = _leafPool.Leaves;
            int count = _o.LeafCount;

            // Which rows: the first count places of a partial shuffle, so every row is drawn at most once.
            var order = new int[pool.Count];
            for (int k = 0; k < order.Length; k++) order[k] = k;
            for (int k = 0; k < count; k++)
            {
                int j = k + rng.Range(order.Length - k);
                (order[k], order[j]) = (order[j], order[k]);
            }

            // Largest first: the drawn rows by their bounding radius, descending, and by the order they were drawn among equals.
            var drawn = new (LeafPool.Leaf leaf, Phenotype adult, float radius, int k)[count];
            for (int k = 0; k < count; k++)
            {
                LeafPool.Leaf leaf = pool[order[k]];
                Phenotype adult = Developer.Develop(leaf.Genome, config.Development, null, config.Shapes, cellTypes: config.CellTypes);
                drawn[k] = (leaf, adult, SharedVolume.BoundingRadius(adult), k);
            }
            Array.Sort(drawn, (a, b) => a.radius != b.radius ? b.radius.CompareTo(a.radius) : a.k.CompareTo(b.k));

            float tank = world.TankRadiusMetres, depth = config.WorldDepthMetres;

            // D182's patches: the ring's turn, drawn after the rows so that the rows a seed draws are the ones it drew without patches.
            Recipe.PatchSetting patches = LeafPatchesSet;
            double turn = 0d;
            if (patches != null)
            {
                if (!(tank > 0f)) throw new InvalidDataException("the recipe's leaf patches lie on a ring about a tank's axis, and this world is not a tank");
                if (patches.RingMetres + patches.RadiusMetres > tank)
                    throw new InvalidDataException(FormattableString.Invariant(
                        $"the leaf patches reach {patches.RingMetres + patches.RadiusMetres} m from the axis (ring {patches.RingMetres} m and radius {patches.RadiusMetres} m), beyond the glass at {tank} m"));
                turn = 2d * Math.PI * rng.NextFloat();
                if (layout != null)
                {
                    layout.Turn = turn;
                    for (int p = 0; p < patches.Count; p++) layout.Centres.Add(LeafPatches.Centre(tank, patches.RingMetres, patches.Count, turn, p));
                }
            }

            var leaves = new List<Organism>(count);
            int dealt = 0;
            foreach ((LeafPool.Leaf leaf, Phenotype adult, float radius, int _) in drawn)
            {
                // Dealt to the patches in turn, largest first, so each patch holds a like mix of sizes.
                int patch = patches == null ? -1 : dealt % patches.Count;
                dealt++;
                if (patches != null && radius > patches.RadiusMetres)
                    throw new InvalidDataException(FormattableString.Invariant(
                        $"leaf {leaf.Id} of {_leafPool.Path} has a radius of {radius} m, more than the patches' {patches.RadiusMetres} m"));

                Organism o = null;
                string last = null;
                float x = 0f, z = 0f;
                for (int attempt = 0; attempt < LeafAttempts && o == null; attempt++)
                {
                    // Uniform over the water the whole body fits in: the disc inside the glass, less the body's radius;
                    // with patches, the patch's disc less the body's radius.
                    if (patches != null)
                    {
                        (double cx, double cz) = LeafPatches.Centre(tank, patches.RingMetres, patches.Count, turn, patch);
                        double r = (patches.RadiusMetres - radius) * Math.Sqrt(rng.NextFloat()), a = 2d * Math.PI * rng.NextFloat();
                        x = (float)(cx + r * Math.Cos(a));
                        z = (float)(cz + r * Math.Sin(a));
                    }
                    else if (tank > 0f)
                    {
                        double r = Math.Max(0f, tank - radius) * Math.Sqrt(rng.NextFloat()), a = 2d * Math.PI * rng.NextFloat();
                        x = tank + (float)(r * Math.Cos(a));
                        z = tank + (float)(r * Math.Sin(a));
                    }
                    else
                    {
                        x = rng.NextFloat() * world.Matter.LengthMetres;
                        z = rng.NextFloat() * world.Matter.WidthMetres;
                    }

                    // The recorded height, kept inside the water: under the surface by the body's radius, and clear of the bed under it.
                    float lowest = sim.Floor != null ? sim.Floor.MinimumPlacementY(x, z, radius) : -depth + radius;
                    if (lowest > -radius)
                        throw new InvalidDataException(FormattableString.Invariant(
                            $"leaf {leaf.Id} of {_leafPool.Path} has a radius of {radius} m, and the water at ({x}, {z}) is too shallow to hold it"));
                    float y = Math.Min(-radius, Math.Max(lowest, leaf.HeightY));

                    try { o = world.InoculateAt(leaf.Genome, new Float3(x, y, z)); }
                    catch (ArgumentException e) when (e.ParamName == "point") { last = e.Message; }
                    catch (ArgumentException e)
                    {
                        throw new InvalidDataException($"leaf {leaf.Id} of {_leafPool.Path} is refused by the world: {e.Message}", e);
                    }
                }
                if (o == null)
                    throw new InvalidDataException($"the tank found no place for leaf {leaf.Id} of {_leafPool.Path} in {LeafAttempts} draws (the last: {last}); it holds too many leaves");

                // Grown, as the bred bodies' purse rule "grown" pays them: the tissue it owes, given to its reserve, which its growth then spends.
                // Under the growth climb (D241) that reserve would buy a climb, not an adult, so the leaf is made adult outright and owes nothing below.
                if (config.GrowthAdultSeconds > 0f) world.MakeAdult(o);
                double owed = Math.Max(0d, Metabolism.TissueJoules(o.AdultPhenotype, config) - o.TissueJoules);
                if (owed > 0d) world.GiveReserve(o, owed);
                leaves.Add(o);
                layout?.Leaves.Add((x, z, radius, patch));
            }
            return leaves;
        }

        /// <summary>
        /// Null when a bred body may swim beside leaves; else why not. A tank with leaves holds no food but its leaves, so a bred body that
        /// absorbs snow or fixes light would be fed by something the tank does not mean to offer.
        /// </summary>
        internal static string FeederBesideLeaves(bool absorptive, bool photosynthetic)
        {
            if (!absorptive && !photosynthetic) return null;
            string what = absorptive && photosynthetic ? "absorptive and photosynthetic" : absorptive ? "absorptive" : "photosynthetic";
            return "carries " + what + " tissue, and a tank with leaves feeds its bodies on the leaves alone";
        }

        /// <summary>The same question of a genome, by the body it develops into under the run's config.</summary>
        private static string FeederBesideLeaves(Genome genome)
        {
            bool absorptive = false, photosynthetic = false;
            foreach (PhenotypePart part in BodyOf(genome).Parts)
            {
                if (part.CellTypeId == CellTypeIds.Absorptive) absorptive = true;
                if (part.CellTypeId == CellTypeIds.Photosynthetic) photosynthetic = true;
            }
            return FeederBesideLeaves(absorptive, photosynthetic);
        }

        /// <summary>
        /// <c>--check</c>'s pass of <see cref="RefuseFeedersBesideLeaves"/>, which a run makes at its start and the check did not reach: every line
        /// of the pool asked, and each refused line printed by its number, since a pool's first column (the body) names no genome when every line
        /// is body 0. Returns the number refused. Added on 2026-10-15, when round 59's D2 pools carried absorptive and photosynthetic nodes and
        /// the check had passed them.
        /// </summary>
        private static int CheckFeeders()
        {
            string[] lines = File.ReadAllLines(_o.Pool);
            List<(int line, string why)> refused = PoolLines.Refused(lines, FeederBesideLeaves);
            foreach ((int line, string why) in refused) Console.WriteLine($"  pool line {line}: {why}");
            Console.WriteLine($"feeders: {lines.Count(l => l.Length > 0)} genomes in {_o.Pool}, {refused.Count} refused beside the leaves");
            return refused.Count;
        }

        /// <summary>Refuses, before anything swims, a run with leaves whose bodies include one that feeds otherwise (<see cref="FeederBesideLeaves(Genome)"/>).</summary>
        private static void RefuseFeedersBesideLeaves(IEnumerable<(int body, Genome genome)> bodies)
        {
            if (_leafPool == null) return;
            foreach ((int body, Genome genome) in bodies)
            {
                string why = FeederBesideLeaves(genome);
                if (why != null) throw new InvalidDataException($"recipe: body {body} {why}; the recipe's leaves refuse it");
            }
        }

        /// <summary>
        /// <c>--check</c>'s placement of a leaf tank, for every episode seed the run will swim: each generation's draws and the exam's
        /// episodes. A draw's leaves and places follow its seed alone, so every tank the run lays is laid here, as
        /// <see cref="TankEpisode"/> lays it: the world, the leaves, then a tank of bred bodies at the episode's own drawn heights. Nothing
        /// steps. Prints one line a seed that fails, and the counts; returns the number of seeds whose leaves did not all fit or whose
        /// tank lost a bred body. Run on 2026-10-13 after both leaf arms failed at generation 0, draw 3, when the first --check had
        /// read only the hashes.
        /// </summary>
        private static int CheckLeafPlacement()
        {
            RunConfig config = EpisodeConfig(_configText);
            Genome[] bodies = File.ReadAllLines(_o.Pool).Where(l => l.Length > 0)
                .Select(l => GenomeJson.Read(l.Substring(l.IndexOf('\t') + 1))).ToArray();
            var seeds = new List<(string name, ulong seed)>();
            // Generations 0 to Generations, inclusive: the search swims its last generation before the exam (EvolveTank).
            // A bench has no search, and swims only the exam's seeds.
            int generations = _o.TankBench ? 0 : _o.Generations + 1;
            for (int g = 0; g < generations; g++)
            {
                ulong[] s = SeedsFor(g, _o.Draws, GenerationSalt);
                for (int d = 0; d < s.Length; d++) seeds.Add(($"generation {g} draw {d}", s[d]));
            }
            ulong[] exam = SeedsFor(0, _o.FinalEpisodes, _o.ExamSalt);
            for (int e = 0; e < exam.Length; e++) seeds.Add(($"exam {e}", exam[e]));

            int stepsPerMetabolic = (int)Math.Round(Simulation.MetabolicStepSeconds / _o.Dt);
            int leafFailures = 0, bodyFailures = 0;
            var lines = new string[seeds.Count];
            // D182: with patches, every seed's layout is printed, whether or not it failed.
            var layouts = LeafPatchesSet == null ? null : new (string line, double gap, double glass, double free)?[seeds.Count];
            // D236: with switching patches, each seed's plan and the check that every fresh complement can be laid.
            Recipe.SwitchingSetting switching = LeafSwitchingSet;
            var plans = switching == null ? null : new (string line, LeafSwitchPlan plan)?[seeds.Count];
            int complementsRefused = 0, complementsChecked = 0;
            Parallel.For(0, seeds.Count, new ParallelOptions { MaxDegreeOfParallelism = _o.Threads }, k =>
            {
                (string name, ulong seed) = seeds[k];
                var world = new World(config, seed);
                world.BirthsHeld = true;
                var sim = new Simulation(world, seed, _o.Dt, stepsPerMetabolic, 1, null);
                try
                {
                    var rng = new Rng(Rng.SeedFor(seed, 0x74616E6BUL));
                    if (_o.Snow > 0d) rng.NextFloat();
                    LeafPatches.Layout layout = layouts == null ? null : new LeafPatches.Layout();
                    try { LayLeaves(world, sim, seed, config, layout); }
                    catch (InvalidDataException e)
                    {
                        System.Threading.Interlocked.Increment(ref leafFailures);
                        lines[k] = $"  {name}: the leaves do not fit: {e.Message}";
                        return;
                    }
                    if (layout != null) layouts[k] = LeafPatches.Describe(name, layout, world.TankRadiusMetres, config.LeafScentRangeMetres);
                    if (switching != null && layout != null)
                    {
                        var switcher = new LeafSwitcher(_recipe.Leaves.Patches, _leafPool, config, seed, world.TankRadiusMetres, _o.Seconds,
                            layout.Turn, LeafSwitcher.DealtOf(layout.Leaves), sim.Floor);
                        LeafSwitchPlan plan = switcher.Schedule.Plan;
                        plans[k] = (LeafSwitcher.Describe(name, plan), plan);
                        foreach (LeafSwitch s in plan.Switches)
                        {
                            // A world of its own for each: the tank empty but for the complement, the placer and the floor as an episode has them.
                            var fresh = new World(config, seed);
                            fresh.BirthsHeld = true;
                            var freshSim = new Simulation(fresh, seed, _o.Dt, stepsPerMetabolic, 1, null);
                            try
                            {
                                System.Threading.Interlocked.Increment(ref complementsChecked);
                                int refused = switcher.LayComplementForCheck(fresh, s);
                                if (refused > 0)
                                {
                                    System.Threading.Interlocked.Increment(ref complementsRefused);
                                    lines[k] = $"  {name}: switch {s.Index} (at {s.Time:0} s): {refused} leaves of its fresh complement found no place";
                                }
                            }
                            catch (InvalidDataException e)
                            {
                                System.Threading.Interlocked.Increment(ref complementsRefused);
                                lines[k] = $"  {name}: switch {s.Index} (at {s.Time:0} s): its fresh complement does not fit: {e.Message}";
                            }
                            finally
                            {
                                freshSim.Dispose();
                            }
                        }
                    }

                    float bed = -config.WorldDepthMetres;
                    int placed = 0;
                    for (int i = 0; i < _o.TankSize; i++)
                    {
                        int before = world.Living.Count;
                        float height = bed + (float)(_o.BodyAboveBed * (0.5d + rng.NextFloat()));
                        try { world.Inoculate(bodies[i % bodies.Length], 1, height); }
                        catch (ArgumentException) { continue; }
                        if (world.Living.Count > before) placed++;
                    }
                    if (placed < _o.TankSize)
                    {
                        System.Threading.Interlocked.Increment(ref bodyFailures);
                        lines[k] = $"  {name}: the leaves fit, and {placed} of {_o.TankSize} bred bodies found a place";
                    }
                }
                finally
                {
                    sim.Dispose();
                }
            });

            if (layouts != null)
            {
                Recipe.PatchSetting p = LeafPatchesSet;
                Console.WriteLine(FormattableString.Invariant(
                    $"leaf patches (D182): {p.Count} of {p.RadiusMetres} m radius on a ring of {p.RingMetres} m; the smell reaches {config.LeafScentRangeMetres} m. ") +
                    "Per seed: the centres (x, z), the leaves in each patch, the nearest gap between leaves of different patches (edge to edge, " +
                    "horizontally), the nearest leaf's edge to the glass, and the share of the tank's surface farther than the reach from every leaf's centre.");
                foreach (var l in layouts) if (l != null) Console.WriteLine(l.Value.line);
                var laid = layouts.Where(l => l != null).Select(l => l.Value).ToArray();
                if (laid.Length > 0)
                    Console.WriteLine(FormattableString.Invariant(
                        $"leaf patches over {laid.Length} laid seeds: nearest gap {laid.Min(l => l.gap):0.0} to {laid.Max(l => l.gap):0.0} m, edge to glass {laid.Min(l => l.glass):0.0} to {laid.Max(l => l.glass):0.0} m, smell-free share {laid.Min(l => l.free):0.00} to {laid.Max(l => l.free):0.00}"));
            }
            if (plans != null)
            {
                Recipe.SwitchingSetting sw = switching;
                Console.WriteLine(
                    FormattableString.Invariant($"switching patches (D236): the fed disc regrows a leaf {sw.RegrowAfterSeconds} s after it leaves the living, and every {sw.PeriodSeconds} s the fed disc and the other wither over {sw.WitherOverSeconds} s ") +
                    FormattableString.Invariant($"and the other disc is fed by a fresh complement at a new angle at least {sw.MinimumTurnDegrees} degrees from the one that withered, over {_o.Seconds} s episodes. ") +
                    "Per seed: the first fed disc, and for each switch its time, the new opening's angle and centre, the angle turned and the distance between the two centres.");
                foreach (var pl in plans) if (pl != null) Console.WriteLine(pl.Value.line);
                var every = plans.Where(pl => pl != null).SelectMany(pl => pl.Value.plan.Switches).ToArray();
                Console.WriteLine(every.Length == 0
                    ? "switching patches: no switch falls inside an episode"
                    : FormattableString.Invariant($"switching patches over {plans.Count(pl => pl != null)} laid seeds: {every.Length} switches, turned {every.Min(s => s.TurnDegrees):0.0} to {every.Max(s => s.TurnDegrees):0.0} degrees, ") +
                      FormattableString.Invariant($"centres {every.Min(s => s.CentreDistance):0.0} to {every.Max(s => s.CentreDistance):0.0} m apart"));
                Console.WriteLine($"switching patches: {complementsChecked} fresh complements laid in worlds built for the check, {complementsRefused} refused");
                leafFailures += complementsRefused;
            }
            foreach (string line in lines) if (line != null) Console.WriteLine(line);
            Console.WriteLine($"leaf placement: {seeds.Count} seeds ({generations} generations of {_o.Draws} draws, {exam.Length} exam episodes), " +
                              $"{_o.LeafCount} leaves and {_o.TankSize} bred bodies each; leaves refused in {leafFailures}, a bred body lost in {bodyFailures}");
            return leafFailures + bodyFailures;
        }

    }
}
