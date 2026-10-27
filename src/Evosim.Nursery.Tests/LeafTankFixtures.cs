using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Evosim.Core;
using Evosim.Nursery;

namespace Evosim.Nursery.Tests
{
    /// <summary>
    /// A small tank built in the test from a <see cref="RunConfig"/> of this build, written beside the test program (under artifacts/, inside the
    /// repository), with a recipe and a pool of synthetic bred bodies, swum in the nursery's own <c>TankEpisode</c> through <see cref="Restep"/>.
    /// The committed world configs predate this build's tunables, so nothing here reads them.
    /// </summary>
    internal static class LeafTankFixtures
    {
        /// <summary>The committed leaf pool (step 1 of logbook/specs/nursery-leaf-tank-plan.md), repository-relative.</summary>
        public const string LeafPool = "rounds/nursery/pools/leaves-3f6c390cd561cb09.tsv.gz";

        /// <summary>
        /// A tank of 400 m² and 10 m, its fields on grids, the one brain, and the price of a node's buoyancy offset that the leaves carry.
        /// <paramref name="leaves"/> lowers the newborn mass floor to 0.01 kg: a leaf is born at its own genome's birth fraction, which the
        /// round that bred it held above that round's floor, and this is that floor's order. Without it, the tank is the one
        /// <c>ARecipeWithoutLeavesSwimsTodaysTank</c> recorded.
        /// </summary>
        public static RunConfig Config(bool leaves = true)
        {
            var c = new RunConfig
            {
                MinimumPopulation = 0,
                WorldDepthMetres = 10f,
                RemineralisationPerSecond = 0f,
                SharedSpace = true,
                WorldShape = WorldShape.Tank,
                FieldModel = MatterField.Grid,
                WorldAreaSquareMetres = 400f,
                HorizontalPatches = 1,
                FieldCellMetres = 1f,
                FieldMatterCellMetres = 2f,
                NutrientMixingDiffusivity = 0.02f,
                HorizontalMixingDiffusivity = 0.02f,
                MatterMixingDiffusivity = 0.02f,
                MatterBudgetUnits = 100f,
                BuoyancyOffsetWattsPerCubicMetre = 1f,
                NeuralCostPerNeuronWatts = 0f,
                NeuralCostPerConnectionWatts = 0f,            };
            c.Brain.BrainModel = BrainModel.Central;
            if (leaves) c.MinNewbornPartKilograms = 0.01f;
            return c;
        }

        /// <summary>The config written as the world config a recipe names; its repository-relative path and its hash.</summary>
        public static (string path, string hash) WriteConfig(string dir, RunConfig config = null)
        {
            config = config ?? Config();
            string path = Path.Combine(dir, "world.json");
            File.WriteAllText(path, RunConfigJson.Write(config));
            return (RestepFixtures.Relative(path), RunConfigJson.Read(File.ReadAllText(path)).Hash());
        }

        /// <summary>A pool of <paramref name="count"/> synthetic bred bodies (a structural root and two hinged links, the one brain): body i, a tab, the genome.</summary>
        public static string WriteBredPool(string dir, int count, Func<int, Genome> make = null)
        {
            string path = Path.Combine(dir, "bred.tsv");
            File.WriteAllLines(path, Enumerable.Range(0, count).Select(i =>
                i.ToString(CultureInfo.InvariantCulture) + "\t" + GenomeJson.Write((make ?? (k => RestepFixtures.Founder((ulong)k + 1)))(i))));
            return RestepFixtures.Relative(path);
        }

        /// <summary>
        /// A bench recipe (no search; the exam's own brain alone, two episodes) on the config and the pool; <paramref name="leaves"/> is the
        /// section's JSON or null to leave the section out. <paramref name="carrion"/> corpses of 40 J each lie on the bed (none by default).
        /// </summary>
        public static string RecipeJson(string config, string hash, string pool, int tankSize, string leaves = null, double seconds = 2, int carrion = 0, double? purseSeconds = null) =>
            "{\n" +
            "  \"format\": 1, \"name\": \"leaf-test\", \"description\": \"\", \"seed\": \"0x1\",\n" +
            "  \"world\": {\"config\": \"" + config + "\", \"configHash\": \"" + hash + "\", \"overrides\": {}},\n" +
            "  \"bodies\": {\"source\": \"pool\", \"founders\": 0, \"trunk\": null, \"pool\": \"" + pool + "\", \"keep\": null},\n" +
            "  \"setup\": {\"seconds\": " + seconds.ToString("R", CultureInfo.InvariantCulture) + ", \"dt\": 0.01, \"tankSize\": " + tankSize +
            ", \"floorClosed\": true, \"birthsHeld\": true, \"snow\": null, \"carrion\": {\"count\": " + carrion + ", \"joules\": 40}, \"start\": {\"aboveBed\": 0.5}," +
            " \"purse\": {\"rule\": \"grown\", \"seconds\": " + (purseSeconds.HasValue ? purseSeconds.Value.ToString("R", CultureInfo.InvariantCulture) : "null") + "}},\n" +
            (leaves == null ? "" : "  \"leaves\": " + leaves + ",\n") +
            "  \"search\": null,\n" +
            "  \"reward\": {\"class\": \"Net\", \"parameters\": {}},\n" +
            "  \"exam\": {\"episodes\": 4, \"salt\": \"0x62656e6368\", \"controls\": [{\"kind\": \"own\"}]}\n" +
            "}\n";

        public static string Write(string dir, string name, string text)
        {
            string path = Path.Combine(dir, name);
            File.WriteAllText(path, text);
            return path;
        }

        /// <summary>What a watcher saw of one episode: each member's root pose at the first physics step, and the world at the start.</summary>
        public sealed class Seen : ITankWatcher
        {
            public LinkPose?[] FirstRoots;
            public int Members;
            public bool Took;

            public void Begin(EpisodeProbe probe)
            {
                Members = probe.MemberCount;
                FirstRoots = new LinkPose?[Members];
                Took = OnBegin(probe);
            }

            public void Physics(EpisodeProbe probe)
            {
                for (int m = 0; m < Members; m++)
                    if (FirstRoots[m] == null && probe.TryLinkPose(m, 0, out LinkPose pose)) FirstRoots[m] = pose;
            }

            public void Metabolic(EpisodeProbe probe) => Physics(probe);

            public void End(EpisodeProbe probe) { }

            /// <summary>Read at the start, before the first step; true when it read.</summary>
            public Func<EpisodeProbe, bool> OnBegin = _ => false;
        }

        /// <summary>
        /// Episode <paramref name="episode"/> of the recipe's exam, the own brain, every member of the pool in one tank, swum in-process through
        /// <see cref="Restep"/> with <paramref name="watcher"/>. The caller holds the "restep" collection, since the nursery's settings are static.
        /// </summary>
        public static RestepOutcome Swim(string dir, string recipePath, int poolCount, int tankSize, int episode, ITankWatcher watcher)
        {
            string recipeRel = RestepFixtures.Relative(recipePath);
            string hash = Recipe.Load(recipePath).Hash();
            Recipe r = Recipe.Load(recipePath);
            int[] members = RestepFixtures.Deal(poolCount, tankSize, episode, 0);
            string json = RestepFixtures.RequestJson(recipeRel, hash, RestepFixtures.Relative(r.PoolFullPath), members, episode: episode);
            string outDir = Path.Combine(dir, "out-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outDir);
            RestepPlan plan = Restep.Load(RestepRequest.Parse(json), RestepFixtures.Root, outDir);
            return Restep.Swim(plan, watcher);
        }

        public static string Line(RestepMemberOutcome m) => FormattableString.Invariant(
            $"{m.Landed} {m.Survived} {m.Start:R} {m.End:R} {m.Net:R} {m.Path:R} {m.Work:R} {m.Upkeep:R}");

        public static string Line(LinkPose? p) => p == null ? "none" : FormattableString.Invariant(
            $"{p.Value.X:R} {p.Value.Y:R} {p.Value.Z:R}");
    }
}
