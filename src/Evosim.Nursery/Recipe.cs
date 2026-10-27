using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Evosim.Core;

namespace Evosim.Nursery
{
    /// <summary>
    /// One experiment as a settings file (logbook/specs/nursery-general-build.md, sections 2 and 3):
    /// the world config and its overrides, the bodies, the episode's setup, the search, the reward
    /// and the exam. Every field is required, as the world's config reader requires them, and an
    /// unknown one is refused besides. The one exception is the section <c>leaves</c>
    /// (<see cref="LeafSetting"/>), which a recipe without leaves leaves out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A recipe may name a base</b> (<c>"base"</c>, a path) and override parts of it: objects
    /// merge key by key, anything else replaces. The two maps, <c>world.overrides</c> and
    /// <c>reward.parameters</c>, replace whole, so that a child can drop what its base set. What is hashed, saved and registered is the
    /// resolved recipe (<see cref="Canonical"/>), never the pair.
    /// </para>
    /// <para>
    /// <b>Paths are relative to the repository root</b>, the first folder above the recipe holding
    /// <c>src/Evosim.Core</c>, so a recipe and its base read the same files from any worktree. A
    /// rooted path is refused: it would name one machine's files, and put that machine in the hash.
    /// </para>
    /// <para>
    /// <b>Seeds and salts are strings</b> (<c>"0x1"</c> or <c>"1"</c>): Core's reader takes a number
    /// through a double, which rounds an integer above 2^53.
    /// </para>
    /// <para>
    /// Threads are not in a recipe. They set the pace and not the result, so they come on the command
    /// line and stay out of the hash.
    /// </para>
    /// </remarks>
    public sealed class Recipe
    {
        public const int FormatVersion = 1;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public string Name, Description;
        public ulong Seed;

        // ---- the world
        /// <summary>The world config as the recipe names it, and resolved against the repository root.</summary>
        public string ConfigPath, ConfigFullPath;

        /// <summary>The config's own hash as read (<see cref="RunConfig.Hash"/>), before the overrides; checked on load.</summary>
        public string ConfigHash;

        /// <summary>Tunable path to value, sorted ordinal by path.</summary>
        public List<KeyValuePair<string, JsonNode>> Overrides = new List<KeyValuePair<string, JsonNode>>();

        // ---- the bodies
        /// <summary><c>founders</c> (drawn from the config's own genome options) or <c>pool</c> (a genomes file an exam wrote).</summary>
        public string BodySource;
        public int Founders;
        /// <summary>Founders only: the root's cell type, or null for any.</summary>
        public string Trunk;
        public string PoolPath, PoolFullPath;
        /// <summary>The bodies kept, by body index (the draw's index, or a pool's body column), ascending; null keeps every one.</summary>
        public int[] Keep;
        /// <summary>
        /// D178: <c>kept</c> starts a search from a pool's genomes with their own brains; null (the field absent) gives each member a
        /// fresh one brain on the pool's body, as every recipe before D178 (EvolveTank's FreshBrain). A recipe without it is written
        /// without it, so every recipe made before it hashes as it did.
        /// </summary>
        public string Brains;

        // ---- the episode's setup
        public double Seconds;
        public float Dt;
        public int TankSize;
        public bool FloorClosed, BirthsHeld;
        public SnowSetting Snow;
        public int CarrionCount;
        public double CarrionJoules;
        public double AboveBed;
        public string PurseRule;
        /// <summary>Seconds of standing watts; NaN takes the config's own (<c>FounderEndowmentSeconds</c>).</summary>
        public double PurseSeconds = double.NaN;

        // ---- the leaves (logbook/specs/nursery-leaf-tank-plan.md, step 2); null, the section left out, lays none
        public LeafSetting Leaves;

        // ---- the search; null runs the exam on the bodies alone (the bench)
        public SearchSetting Search;

        // ---- the reward
        public string RewardClass;
        public JsonNode RewardParameters;

        // ---- the exam
        public int ExamEpisodes;
        public ulong ExamSalt;
        /// <summary>
        /// Which genomes the exam that follows a search swims. Null (the field absent): each body's best of the last generation, or the
        /// elites without perBody, as every recipe before D172. <c>tops</c> (D172): every generation's top beside the last generation's
        /// elites, each genome once.
        /// </summary>
        public string ExamPool;
        public List<ExamControl> Controls = new List<ExamControl>();

        public sealed class SnowSetting
        {
            public double Density;
            /// <summary>True: the density as given. False: drawn per episode from half to double.</summary>
            public bool Fixed;
            /// <summary>Layers of snow on the bed; 0 when laid in islands.</summary>
            public int Layers;
            /// <summary>Islands' wavelength in metres and the share of the bed they cover; 0 when laid in layers.</summary>
            public float IslandWavelength, IslandCover;
        }

        /// <summary>
        /// Living leaves laid in every tank before the bred bodies: <see cref="Count"/> rows of the leaf pool (LeafPool.cs) drawn per
        /// episode, and the water's spent matter at <see cref="SpentUnitsPerCubicMetre"/> laid before them. The section is optional, and a
        /// recipe without it is written without it, so every recipe made before it hashes as it did.
        /// </summary>
        public sealed class LeafSetting
        {
            /// <summary>The leaf pool (a <c>.tsv.gz</c>) as the recipe names it, and resolved against the repository root.</summary>
            public string PoolPath, PoolFullPath;
            public int Count;
            public double SpentUnitsPerCubicMetre;
            /// <summary>Null: the leaves are placed over the whole tank. Else in the patches it names (D182).</summary>
            public PatchSetting Patches;
        }

        /// <summary>
        /// D182's patchy tank: <see cref="Count"/> discs of <see cref="RadiusMetres"/>, their centres evenly spaced on a ring of
        /// <see cref="RingMetres"/> about the tank's axis, the ring turned by an angle each episode draws. Every leaf lies wholly inside
        /// its disc, and the leaves are dealt to the discs in turn, largest first. Optional, and written only when set, so that every
        /// recipe made before it hashes as it did.
        /// </summary>
        public sealed class PatchSetting
        {
            public int Count;
            public double RadiusMetres, RingMetres;
            /// <summary>D236's switching patches; null (the field absent) is the patches of D182, fixed for the episode.</summary>
            public SwitchingSetting Switching;
        }

        /// <summary>
        /// D236's switching patches, for a tank of two: the fed disc regrows what dies <see cref="RegrowAfterSeconds"/> later, the other holds
        /// still, and every <see cref="PeriodSeconds"/> the fed disc withers over <see cref="WitherOverSeconds"/> and the other is fed by a fresh
        /// complement at a new angle on the ring, at least <see cref="MinimumTurnDegrees"/> from the one that withered. Optional, written only
        /// when set, so that every recipe made before it hashes as it did (LeafSwitching.cs holds the rules).
        /// </summary>
        public sealed class SwitchingSetting
        {
            public double PeriodSeconds, RegrowAfterSeconds, WitherOverSeconds, MinimumTurnDegrees;
        }

        public sealed class SearchSetting
        {
            public int Population, Draws, Generations, Elites, Tournament;
            public bool PerBody, EvolveEnvelope;
            /// <summary><c>brain</c> (the body kept, the brain varied) or <c>kind</c> (the whole genome, the set of cell types kept).</summary>
            public string Evolve;
            /// <summary>Null: no crossover.</summary>
            public CrossoverSetting Crossover;
            /// <summary>D172's stop on a stalled curve; null (the field absent) swims every generation to <see cref="Generations"/>.</summary>
            public StopSetting Stop;
        }

        /// <summary>
        /// D172: the search stops after generation g when g is at least <see cref="Floor"/> and the measure, averaged over the
        /// last <see cref="Window"/> generations, has gained no more than <see cref="Gain"/> of itself in <see cref="Over"/>
        /// generations. It always stops after <see cref="SearchSetting.Generations"/>, which is the cap.
        /// </summary>
        public sealed class StopSetting
        {
            /// <summary>
            /// What the rule reads, each generation: <c>eaten</c>, the population's mean energy eaten; or <c>reward</c> (D178), the
            /// population's mean of the recipe's reward taken over each of the generation's episodes alone, for a search that selects
            /// on something other than eating.
            /// </summary>
            public string Measure;
            public int Window, Over, Floor;
            public double Gain;
        }

        public sealed class CrossoverSetting
        {
            public double Rate, BaseShare;
            /// <summary>d_ref; NaN mutates every child.</summary>
            public double MutationByDistance = double.NaN;
        }

        /// <summary>One brain the exam swims each genome under.</summary>
        public sealed class ExamControl
        {
            /// <summary><c>own</c>, <c>frozen</c>, <c>full</c>, <c>brainless</c>, <c>knockout</c>, <c>constant</c> or <c>kinesis</c>.</summary>
            public string Kind;
            public SensorChannel[] Channels;
            public float Power, Bias, Gain;
            public string Mode;

            /// <summary>The label the exam's rows and log carry.</summary>
            public string Label =>
                Kind == "knockout" ? "knockout " + string.Join(",", Channels)
              : Kind == "constant" ? "constant " + Power.ToString("R", Inv)
              : Kind == "kinesis" ? FormattableString.Invariant($"kinesis {Mode} {Bias:R} {Gain:R}")
              : Kind;
        }

        // ------------------------------------------------------------------ reading

        /// <summary>Reads a recipe file, resolves its base, and checks every field and its world config.</summary>
        public static Recipe Load(string path)
        {
            Recipe r = Read(path);
            r.CheckWorld();
            return r;
        }

        /// <summary>
        /// Reads a recipe file, resolves its base and checks every field, but not its world: the
        /// config is not opened, so a recipe whose world this build cannot load still reads, with the
        /// same <see cref="Hash"/> and <see cref="Canonical"/> it has under <see cref="Load"/>. For
        /// showing what a recipe holds; anything that would run it uses <see cref="Load"/>.
        /// </summary>
        public static Recipe Read(string path)
        {
            string full = Path.GetFullPath(path);
            string root = RepoRootAbove(Path.GetDirectoryName(full))
                ?? throw new InvalidDataException($"no repository root (a folder holding src/Evosim.Core) above {full}");
            JsonNode resolved = Resolve(full, root, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            return ReadNode(resolved, root);
        }

        /// <summary>
        /// Null when this build loads the recipe's world (<see cref="Load"/> would pass), else the
        /// refusal's message: a config it cannot read, a hash that differs, or an override it refuses.
        /// </summary>
        public string WorldProblem()
        {
            try { CheckWorld(); return null; }
            catch (Exception e) { return e.Message; }
        }

        /// <summary>The recipe's JSON with its base chain merged in, the <c>base</c> key removed.</summary>
        private static JsonNode Resolve(string full, string root, HashSet<string> seen)
        {
            if (!seen.Add(full)) throw new InvalidDataException($"the recipe {full} is its own base");
            JsonNode node;
            try { node = Json.Parse(File.ReadAllText(full)); }
            catch (Exception e) when (!(e is InvalidDataException)) { throw new InvalidDataException($"{full}: {e.Message}", e); }
            if (node.Kind != JsonNode.NodeKind.Object) throw new InvalidDataException($"{full}: a recipe is a JSON object");
            if (!node.Has("base")) return node;

            string basePath = Str(node, "base", "recipe");
            if (string.IsNullOrEmpty(basePath)) throw Bad("recipe.base", "names a recipe file, or is left out");
            JsonNode baseNode = Resolve(Resolved(basePath, root, "recipe.base"), root, seen);
            var mine = Members(node);
            mine.Remove("base");
            return Merge(baseNode, JsonNode.FromObject(mine), "");
        }

        /// <summary>Maps whose keys are data, not fields: a child's replaces its base's whole.</summary>
        private static readonly string[] WholeMaps = { "world.overrides", "reward.parameters" };

        private static JsonNode Merge(JsonNode under, JsonNode over, string at)
        {
            if (under.Kind != JsonNode.NodeKind.Object || over.Kind != JsonNode.NodeKind.Object || WholeMaps.Contains(at)) return over;
            var merged = Members(under);
            foreach (string key in over.Keys())
                merged[key] = merged.TryGetValue(key, out JsonNode u) ? Merge(u, over[key], at.Length == 0 ? key : at + "." + key) : over[key];
            return JsonNode.FromObject(merged);
        }

        private static Dictionary<string, JsonNode> Members(JsonNode node)
        {
            var d = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
            foreach (string k in node.Keys()) d[k] = node[k];
            return d;
        }

        /// <summary>Builds and checks a recipe from its resolved JSON; <paramref name="root"/> resolves its paths.</summary>
        public static Recipe FromNode(JsonNode n, string root)
        {
            Recipe r = ReadNode(n, root);
            r.CheckWorld();
            return r;
        }

        /// <summary>As <see cref="FromNode"/>, without the world: every field is checked, the config is not opened.</summary>
        public static Recipe ReadNode(JsonNode n, string root)
        {
            var r = new Recipe();
            // The one optional section: leaves, absent (or null) for none.
            bool leaves = n.Kind == JsonNode.NodeKind.Object && n.Has("leaves");
            if (leaves) Exactly(n, "recipe", "format", "name", "description", "seed", "world", "bodies", "setup", "leaves", "search", "reward", "exam");
            else Exactly(n, "recipe", "format", "name", "description", "seed", "world", "bodies", "setup", "search", "reward", "exam");
            int format = Int(n, "format", "recipe");
            if (format != FormatVersion) throw Bad("recipe.format", $"is {format}; this build reads {FormatVersion}");
            r.Name = Str(n, "name", "recipe");
            if (string.IsNullOrEmpty(r.Name) || r.Name.Any(c => !(char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.')))
                throw Bad("recipe.name", "takes letters, digits, '-', '_' and '.' only, since it names files");
            r.Description = Str(n, "description", "recipe") ?? throw Bad("recipe.description", "is required (it may be empty)");
            r.Seed = Ulong(n, "seed", "recipe");

            JsonNode w = n["world"];
            Exactly(w, "world", "config", "configHash", "overrides");
            r.ConfigPath = Str(w, "config", "world") ?? throw Bad("world.config", "is required");
            r.ConfigFullPath = Resolved(r.ConfigPath, root, "world.config");
            r.ConfigHash = Str(w, "configHash", "world") ?? throw Bad("world.configHash", "is required");
            JsonNode ov = w["overrides"];
            if (ov.Kind != JsonNode.NodeKind.Object) throw Bad("world.overrides", "is an object of tunable paths");
            r.Overrides = ov.Keys().OrderBy(k => k, StringComparer.Ordinal).Select(k => new KeyValuePair<string, JsonNode>(k, ov[k])).ToList();

            JsonNode b = n["bodies"];
            if (b.Kind == JsonNode.NodeKind.Object && b.Has("brains")) Exactly(b, "bodies", "source", "founders", "trunk", "pool", "keep", "brains");
            else Exactly(b, "bodies", "source", "founders", "trunk", "pool", "keep");
            r.BodySource = Str(b, "source", "bodies");
            if (r.BodySource == "founders")
            {
                r.Founders = Int(b, "founders", "bodies");
                if (r.Founders < 1) throw Bad("bodies.founders", "must be 1 or more");
                r.Trunk = Str(b, "trunk", "bodies");
                if (b["pool"].Kind != JsonNode.NodeKind.Null) throw Bad("bodies.pool", "is null when the bodies are founders");
            }
            else if (r.BodySource == "pool")
            {
                r.PoolPath = Str(b, "pool", "bodies") ?? throw Bad("bodies.pool", "names the genomes file");
                r.PoolFullPath = Resolved(r.PoolPath, root, "bodies.pool");
                if (!File.Exists(r.PoolFullPath)) throw Bad("bodies.pool", $"names {r.PoolFullPath}, which does not exist");
                if (Int(b, "founders", "bodies") != 0) throw Bad("bodies.founders", "is 0 when the bodies come from a pool");
                if (b["trunk"].Kind != JsonNode.NodeKind.Null) throw Bad("bodies.trunk", "is null when the bodies come from a pool");
            }
            else throw Bad("bodies.source", "is founders or pool");
            if (b["keep"].Kind != JsonNode.NodeKind.Null)
            {
                r.Keep = Array(b, "keep", "bodies").Select(x => WholeInt(x, "bodies.keep")).ToArray();
                if (r.Keep.Length == 0 || r.Keep.Any(k => k < 0) || r.Keep.Zip(r.Keep.Skip(1), (a, c) => a < c).Any(up => !up))
                    throw Bad("bodies.keep", "lists body indices of 0 or more, each once and ascending, or is null for every body");
                if (r.BodySource == "founders" && r.Keep.Any(k => k >= r.Founders))
                    throw Bad("bodies.keep", $"names body {r.Keep.First(k => k >= r.Founders)}, and the draw has bodies 0 to {r.Founders - 1}");
                if (r.BodySource == "pool")
                {
                    var bodies = new HashSet<int>(File.ReadLines(r.PoolFullPath).Where(l => l.Length > 0)
                        .Select(l => int.TryParse(l.Substring(0, Math.Max(0, l.IndexOf('\t'))), NumberStyles.AllowLeadingSign, Inv, out int x) ? x : int.MinValue));
                    if (r.Keep.Any(k => !bodies.Contains(k)))
                        throw Bad("bodies.keep", $"names body {r.Keep.First(k => !bodies.Contains(k))}, which the pool's body column does not hold");
                }
            }

            if (b.Has("brains"))
            {
                r.Brains = Str(b, "brains", "bodies");
                if (r.Brains != "kept") throw Bad("bodies.brains", "is kept (the pool's own brains), or left out for a fresh brain each");
                if (r.BodySource != "pool") throw Bad("bodies.brains", "keeps a pool's brains, and these bodies are founders");
            }

            ReadSetup(n["setup"], r);
            if (leaves) ReadLeaves(n["leaves"], r, root);
            ReadSearch(n["search"], r);
            if (r.Brains != null && r.Search == null) throw Bad("bodies.brains", "acts on a search's start, and the recipe has no search (a bench swims a pool's genomes as they are)");

            JsonNode rw = n["reward"];
            Exactly(rw, "reward", "class", "parameters");
            r.RewardClass = Str(rw, "class", "reward");
            r.RewardParameters = rw["parameters"];
            // An unknown class or parameter is refused now, not at the first score.
            try { RewardRegistry.Create(r.RewardClass, r.RewardParameters); }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException || e is FormatException) { throw Bad("reward", e.Message); }

            ReadExam(n["exam"], r);
            // The exam's episodes are held out: none may be one the search swims, at any generation up to its cap. The seeds differ
            // only by salt, so a recipe whose exam salt repeated the search's, or happened to meet one of its seeds, would examine
            // genomes on episodes they were selected on.
            if (r.Search != null)
            {
                var met = EpisodeSeeds.ExamOverlap(r.Seed, r.Search.Generations, r.Search.Draws, r.ExamSalt, r.ExamEpisodes);
                if (met.HasValue)
                    throw Bad("exam.salt", $"draws exam episode {met.Value.examDraw} on the seed of the search's generation {met.Value.generation}, draw {met.Value.draw}; the exam's episodes must be held out from every one the search swims");
            }
            return r;
        }

        private static void ReadSetup(JsonNode s, Recipe r)
        {
            Exactly(s, "setup", "seconds", "dt", "tankSize", "floorClosed", "birthsHeld", "snow", "carrion", "start", "purse");
            r.Seconds = Num(s, "seconds", "setup");
            if (!(r.Seconds > 0)) throw Bad("setup.seconds", "must be above 0");
            r.Dt = (float)Num(s, "dt", "setup");
            if (!(r.Dt > 0f)) throw Bad("setup.dt", "must be above 0");
            r.TankSize = Int(s, "tankSize", "setup");
            if (r.TankSize < 1) throw Bad("setup.tankSize", "must be 1 or more");

            // The two departures every current recipe makes. Code exists only for what a recipe uses,
            // so the other value of each is refused rather than half supported.
            r.FloorClosed = Bool(s, "floorClosed", "setup");
            if (!r.FloorClosed) throw Bad("setup.floorClosed", "must be true: the nursery's tanks never refill from the population floor");
            r.BirthsHeld = Bool(s, "birthsHeld", "setup");
            if (!r.BirthsHeld) throw Bad("setup.birthsHeld", "must be true: every current recipe holds births");

            JsonNode snow = s["snow"];
            if (snow.Kind != JsonNode.NodeKind.Null)
            {
                Exactly(snow, "setup.snow", "density", "draw", "layers", "islands");
                var sn = new SnowSetting { Density = Num(snow, "density", "setup.snow") };
                if (!(sn.Density > 0)) throw Bad("setup.snow.density", "must be above 0 (no snow is \"snow\": null)");
                string draw = Str(snow, "draw", "setup.snow");
                if (draw == "fixed") sn.Fixed = true;
                else if (draw != "halfToDouble") throw Bad("setup.snow.draw", "is fixed or halfToDouble");
                bool layered = snow["layers"].Kind != JsonNode.NodeKind.Null, islands = snow["islands"].Kind != JsonNode.NodeKind.Null;
                if (layered == islands) throw Bad("setup.snow", "lays its snow in layers or in islands: one of the two is null");
                if (layered)
                {
                    sn.Layers = Int(snow, "layers", "setup.snow");
                    if (sn.Layers < 1) throw Bad("setup.snow.layers", "must be 1 or more");
                }
                else
                {
                    JsonNode isl = snow["islands"];
                    Exactly(isl, "setup.snow.islands", "wavelength", "cover");
                    sn.IslandWavelength = (float)Num(isl, "wavelength", "setup.snow.islands");
                    sn.IslandCover = (float)Num(isl, "cover", "setup.snow.islands");
                    if (!(sn.IslandWavelength > 0f)) throw Bad("setup.snow.islands.wavelength", "must be above 0");
                    if (!(sn.IslandCover > 0f && sn.IslandCover <= 1f)) throw Bad("setup.snow.islands.cover", "is a share in (0, 1]");
                }
                r.Snow = sn;
            }

            JsonNode c = s["carrion"];
            Exactly(c, "setup.carrion", "count", "joules");
            r.CarrionCount = Int(c, "count", "setup.carrion");
            r.CarrionJoules = Num(c, "joules", "setup.carrion");
            if (r.CarrionCount < 0) throw Bad("setup.carrion.count", "must be 0 or more");
            if (r.CarrionCount > 0 && !(r.CarrionJoules > 0)) throw Bad("setup.carrion.joules", "must be above 0");

            JsonNode st = s["start"];
            Exactly(st, "setup.start", "aboveBed");
            r.AboveBed = Num(st, "aboveBed", "setup.start");
            if (!(r.AboveBed >= 0)) throw Bad("setup.start.aboveBed", "must be 0 or more");

            JsonNode p = s["purse"];
            Exactly(p, "setup.purse", "rule", "seconds");
            r.PurseRule = Str(p, "rule", "setup.purse");
            if (r.PurseRule != "grown") throw Bad("setup.purse.rule", "is grown (\"world\", the floor's founder rule, was retired: no recipe used it)");
            if (p["seconds"].Kind != JsonNode.NodeKind.Null)
            {
                r.PurseSeconds = Num(p, "seconds", "setup.purse");
                if (!(r.PurseSeconds >= 0)) throw Bad("setup.purse.seconds", "must be 0 or more, or null for the config's own");
            }
        }

        private static void ReadLeaves(JsonNode l, Recipe r, string root)
        {
            if (l.Kind == JsonNode.NodeKind.Null) return;
            bool patches = l.Kind == JsonNode.NodeKind.Object && l.Has("patches");
            if (patches) Exactly(l, "leaves", "pool", "count", "spentUnitsPerCubicMetre", "patches");
            else Exactly(l, "leaves", "pool", "count", "spentUnitsPerCubicMetre");
            var le = new LeafSetting { PoolPath = Str(l, "pool", "leaves") ?? throw Bad("leaves.pool", "names the leaf pool file (a .tsv.gz)") };
            if (!le.PoolPath.EndsWith(".tsv.gz", StringComparison.Ordinal)) throw Bad("leaves.pool", $"is {le.PoolPath}; a leaf pool is gzipped text (.tsv.gz)");
            le.PoolFullPath = Resolved(le.PoolPath, root, "leaves.pool");
            if (!File.Exists(le.PoolFullPath)) throw Bad("leaves.pool", $"names {le.PoolFullPath}, which does not exist");
            le.Count = Int(l, "count", "leaves");
            if (le.Count < 1) throw Bad("leaves.count", "must be 1 or more (no leaves is the section left out)");
            le.SpentUnitsPerCubicMetre = Num(l, "spentUnitsPerCubicMetre", "leaves");
            if (!(le.SpentUnitsPerCubicMetre >= 0)) throw Bad("leaves.spentUnitsPerCubicMetre", "must be 0 or more");
            int rows = LeafPool.CountRows(le.PoolFullPath);
            if (le.Count > rows) throw Bad("leaves.count", $"is {le.Count}, and the pool {le.PoolPath} holds {rows} leaves (a tank draws distinct rows)");
            if (patches && l["patches"].Kind != JsonNode.NodeKind.Null)
            {
                JsonNode p = l["patches"];
                // D236's switching is optional, as the patches are: absent is the fixed discs every recipe before it swam.
                bool switching = p.Kind == JsonNode.NodeKind.Object && p.Has("switching");
                if (switching) Exactly(p, "leaves.patches", "count", "radius", "ring", "switching");
                else Exactly(p, "leaves.patches", "count", "radius", "ring");
                var pa = new PatchSetting
                {
                    Count = Int(p, "count", "leaves.patches"),
                    RadiusMetres = Num(p, "radius", "leaves.patches"),
                    RingMetres = Num(p, "ring", "leaves.patches"),
                };
                if (pa.Count < 1) throw Bad("leaves.patches.count", "must be 1 or more (no patches is the field left out)");
                if (pa.Count > le.Count) throw Bad("leaves.patches.count", $"is {pa.Count}, more than the {le.Count} leaves; a patch holds one leaf at least");
                if (!(pa.RadiusMetres > 0)) throw Bad("leaves.patches.radius", "must be more than 0 m");
                if (!(pa.RingMetres >= 0)) throw Bad("leaves.patches.ring", "must be 0 m or more");
                if (pa.Count > 1 && !(pa.RingMetres > 0)) throw Bad("leaves.patches.ring", "must be more than 0 m when there is more than one patch, or the patches lie on one another");
                if (switching && p["switching"].Kind != JsonNode.NodeKind.Null) pa.Switching = ReadSwitching(p["switching"], pa);
                le.Patches = pa;
            }
            r.Leaves = le;
        }

        private static SwitchingSetting ReadSwitching(JsonNode s, PatchSetting patches)
        {
            const string at = "leaves.patches.switching";
            Exactly(s, at, "period", "regrowAfter", "witherOver", "minimumTurn");
            var sw = new SwitchingSetting
            {
                PeriodSeconds = Num(s, "period", at),
                RegrowAfterSeconds = Num(s, "regrowAfter", at),
                WitherOverSeconds = Num(s, "witherOver", at),
                MinimumTurnDegrees = Num(s, "minimumTurn", at),
            };
            if (patches.Count != 2) throw Bad(at, $"is for two patches, and leaves.patches.count is {patches.Count}");
            if (!(sw.PeriodSeconds > 0)) throw Bad(at + ".period", "must be more than 0 s");
            if (!(sw.RegrowAfterSeconds >= 0)) throw Bad(at + ".regrowAfter", "must be 0 s or more");
            if (!(sw.WitherOverSeconds > 0)) throw Bad(at + ".witherOver", "must be more than 0 s");
            if (sw.WitherOverSeconds > sw.PeriodSeconds) throw Bad(at + ".witherOver", $"is {sw.WitherOverSeconds} s, more than the period of {sw.PeriodSeconds} s");
            if (!(sw.MinimumTurnDegrees >= 0 && sw.MinimumTurnDegrees <= 180)) throw Bad(at + ".minimumTurn", "must be from 0 to 180 degrees");
            return sw;
        }

        private static void ReadSearch(JsonNode s, Recipe r)
        {
            if (s.Kind == JsonNode.NodeKind.Null) return;
            // D172's stop is optional, as the leaves are: absent is the fixed count every recipe before it swam, so those
            // recipes read, hash and rerun as they did.
            bool stop = s.Has("stop");
            if (stop) Exactly(s, "search", "population", "draws", "generations", "elites", "tournament", "perBody", "evolve", "evolveEnvelope", "crossover", "stop");
            else Exactly(s, "search", "population", "draws", "generations", "elites", "tournament", "perBody", "evolve", "evolveEnvelope", "crossover");
            var se = new SearchSetting
            {
                Population = Int(s, "population", "search"),
                Draws = Int(s, "draws", "search"),
                Generations = Int(s, "generations", "search"),
                Elites = Int(s, "elites", "search"),
                Tournament = Int(s, "tournament", "search"),
                PerBody = Bool(s, "perBody", "search"),
                Evolve = Str(s, "evolve", "search"),
                EvolveEnvelope = Bool(s, "evolveEnvelope", "search"),
            };
            if (se.Population < 1) throw Bad("search.population", "must be 1 or more");
            // Fault 6 of the build spec: a draw's seed is the generation times 1,000 plus the draw, so
            // a thousand draws would swim generation g's last draws on generation g + 1's seeds.
            if (se.Draws < 1 || se.Draws >= 1000) throw Bad("search.draws", "must be 1 to 999 (seeds are the generation times 1,000 plus the draw)");
            if (se.Generations < 0) throw Bad("search.generations", "must be 0 or more");
            if (se.Elites < 0 || se.Elites >= se.Population) throw Bad("search.elites", "must be 0 or more and fewer than the population");
            if (se.Tournament < 1) throw Bad("search.tournament", "must be 1 or more");
            if (se.Evolve != "brain" && se.Evolve != "kind") throw Bad("search.evolve", "is brain or kind");
            if (se.EvolveEnvelope && se.Evolve != "brain") throw Bad("search.evolveEnvelope", "acts only with evolve brain (kind mutates the whole genome, envelope included)");

            JsonNode x = s["crossover"];
            if (x.Kind != JsonNode.NodeKind.Null)
            {
                Exactly(x, "search.crossover", "rate", "baseShare", "mutationByDistance");
                var cx = new CrossoverSetting { Rate = Num(x, "rate", "search.crossover"), BaseShare = Num(x, "baseShare", "search.crossover") };
                if (!(cx.Rate > 0 && cx.Rate <= 1)) throw Bad("search.crossover.rate", "is a share in (0, 1] (no crossover is \"crossover\": null)");
                if (!(cx.BaseShare >= 0 && cx.BaseShare <= 1)) throw Bad("search.crossover.baseShare", "is a share in [0, 1]");
                if (x["mutationByDistance"].Kind != JsonNode.NodeKind.Null)
                {
                    cx.MutationByDistance = Num(x, "mutationByDistance", "search.crossover");
                    if (!(cx.MutationByDistance > 0)) throw Bad("search.crossover.mutationByDistance", "takes d_ref above 0, or null to mutate every child");
                }
                if (!(se.Evolve == "brain" && se.PerBody))
                    throw Bad("search.crossover", "needs evolve brain and perBody: both parents must carry one body");
                se.Crossover = cx;
            }
            if (stop)
            {
                JsonNode t = s["stop"];
                Exactly(t, "search.stop", "measure", "window", "gain", "over", "floor");
                var st = new StopSetting
                {
                    Measure = Str(t, "measure", "search.stop"),
                    Window = Int(t, "window", "search.stop"),
                    Gain = Num(t, "gain", "search.stop"),
                    Over = Int(t, "over", "search.stop"),
                    Floor = Int(t, "floor", "search.stop"),
                };
                if (st.Measure != "eaten" && st.Measure != "reward")
                    throw Bad("search.stop.measure", "is eaten (the population's mean energy eaten) or reward (its mean reward, each episode scored alone)");
                if (st.Window < 1) throw Bad("search.stop.window", "must be 1 or more");
                if (st.Over < 1) throw Bad("search.stop.over", "must be 1 or more");
                if (!(st.Gain >= 0)) throw Bad("search.stop.gain", "is a share of 0 or more");
                if (st.Floor < 0 || st.Floor > se.Generations) throw Bad("search.stop.floor", "is 0 to search.generations, the cap");
                se.Stop = st;
            }
            r.Search = se;
        }

        private static void ReadExam(JsonNode e, Recipe r)
        {
            // D172's pool is optional, as the stop is: absent is the exam every recipe before it swam.
            if (e.Kind == JsonNode.NodeKind.Object && e.Has("pool"))
            {
                Exactly(e, "exam", "episodes", "salt", "controls", "pool");
                r.ExamPool = Str(e, "pool", "exam");
                if (r.ExamPool != "tops") throw Bad("exam.pool", "is tops (every generation's top and the last elites), or absent for the last generation's best");
                if (r.Search == null) throw Bad("exam.pool", "acts only after a search; a bench swims its pool");
            }
            else Exactly(e, "exam", "episodes", "salt", "controls");
            r.ExamEpisodes = Int(e, "episodes", "exam");
            if (r.ExamEpisodes < 1 || r.ExamEpisodes >= 1000) throw Bad("exam.episodes", "must be 1 to 999");
            r.ExamSalt = Ulong(e, "salt", "exam");
            int i = 0;
            foreach (JsonNode c in Array(e, "controls", "exam"))
            {
                string at = $"exam.controls[{i++}]";
                if (c.Kind != JsonNode.NodeKind.Object || !c.Has("kind")) throw Bad(at, "is an object with a kind");
                var ctl = new ExamControl { Kind = c["kind"].AsString() };
                switch (ctl.Kind)
                {
                    case "own":
                    case "frozen":
                    case "full":
                    case "brainless":
                        Exactly(c, at, "kind");
                        break;
                    case "knockout":
                        Exactly(c, at, "kind", "channels");
                        ctl.Channels = Array(c, "channels", at).Select(ch => Channel(ch, at + ".channels")).ToArray();
                        if (ctl.Channels.Length == 0 || ctl.Channels.Distinct().Count() != ctl.Channels.Length)
                            throw Bad(at + ".channels", "lists one or more distinct sensor channels");
                        break;
                    case "constant":
                        Exactly(c, at, "kind", "power");
                        ctl.Power = (float)Num(c, "power", at);
                        if (!(ctl.Power >= -1f && ctl.Power <= 1f)) throw Bad(at + ".power", "is in [-1, 1]");
                        break;
                    case "kinesis":
                        Exactly(c, at, "kind", "mode", "bias", "gain");
                        ctl.Mode = Str(c, "mode", at);
                        if (ctl.Mode != "level" && ctl.Mode != "rate") throw Bad(at + ".mode", "is level or rate");
                        ctl.Bias = (float)Num(c, "bias", at);
                        ctl.Gain = (float)Num(c, "gain", at);
                        break;
                    default:
                        throw Bad(at + ".kind", "is own, frozen, full, brainless, knockout, constant or kinesis");
                }
                r.Controls.Add(ctl);
            }
            // Every other brain is paired against the genome's own, genome by genome, so own comes first.
            if (r.Controls.Count == 0 || r.Controls[0].Kind != "own") throw Bad("exam.controls", "starts with {\"kind\": \"own\"}");
            if (r.Controls.Skip(1).Any(c => c.Kind == "own")) throw Bad("exam.controls", "has one own");
            // A knockout is its set of channels, in whatever order they are listed.
            var labels = r.Controls.Select(c => c.Kind == "knockout" ? "knockout " + string.Join(",", c.Channels.OrderBy(x => x)) : c.Label).ToList();
            if (labels.Distinct().Count() != labels.Count) throw Bad("exam.controls", "lists each control once");
        }

        /// <summary>Reads the world config, checks its hash, and checks every override against it.</summary>
        private void CheckWorld()
        {
            if (!File.Exists(ConfigFullPath)) throw Bad("world.config", $"names {ConfigFullPath}, which does not exist");
            RunConfig raw = RunConfigJson.Read(File.ReadAllText(ConfigFullPath));
            string hash = raw.Hash();
            if (!string.Equals(hash, ConfigHash, StringComparison.Ordinal))
                throw Bad("world.configHash", $"is {ConfigHash}, and the config at {ConfigFullPath} hashes to {hash}");
            if (FloorClosed && Overrides.Any(kv => kv.Key == nameof(RunConfig.MinimumPopulation)))
                throw Bad("world.overrides", "sets MinimumPopulation, which setup.floorClosed already sets to 0");
            EpisodeConfig(File.ReadAllText(ConfigFullPath));   // every override path and value, refused now rather than at the first episode
        }

        /// <summary>
        /// The config one episode is built from: the world config read afresh from its text, every
        /// override set through Core's tunable walk, and the floor closed. Each episode needs its own,
        /// because a world pins the config's current (src/Evosim.Core/CLAUDE.md).
        /// </summary>
        public RunConfig EpisodeConfig(string text)
        {
            RunConfig c = RunConfigJson.Read(text);
            if (Overrides.Count > 0)
            {
                Dictionary<string, TunableEntry> byPath = ConfigSchema.Of(c).ToDictionary(t => t.Path, StringComparer.Ordinal);
                foreach (KeyValuePair<string, JsonNode> kv in Overrides)
                {
                    if (!byPath.TryGetValue(kv.Key, out TunableEntry entry))
                        throw Bad("world.overrides." + kv.Key, "is not a tunable path (ConfigSchema; e.g. WorkCostMultiplier, Development.MaxPartVolume)");
                    object value = OverrideValue(entry.ValueType, kv.Value, "world.overrides." + kv.Key);
                    try { entry.Set(value); }
                    catch (Exception e) when (e is System.Reflection.TargetInvocationException || e is ArgumentException || e is InvalidOperationException)
                    {
                        throw Bad("world.overrides." + kv.Key, "was refused by the config: " + (e.InnerException ?? e).Message);
                    }
                }
            }
            if (FloorClosed) c.MinimumPopulation = 0;

            // D239, D240. The tank carries the world's pace factor k, whether its config or an override set it: a metabolic step holds
            // 0.5·k physics seconds, so setup.dt must divide that. A dt that cannot is refused here, at the recipe's read (--check
            // among the readers), and not at the first episode.
            CheckStepAtPace(c.PaceFactor);

            return c;
        }

        /// <summary>
        /// Refuses a <c>setup.dt</c> that cannot make a metabolic step at the pace factor <paramref name="paceFactor"/>, by the setting and its
        /// value, with the steps the farm's own check (<c>EnvBinding.ResolvePhysicsStep</c>) says would be accepted. At a factor of 1 it is the
        /// check every dt of 0.01 has always passed.
        /// </summary>
        private void CheckStepAtPace(float paceFactor)
        {
            try { Evosim.Farm.EnvBinding.ResolvePhysicsStep(Dt, paceFactor, out _); }
            catch (ArgumentOutOfRangeException e)
            {
                string said = e.Message;
                int cut = said.IndexOf(" (Parameter", StringComparison.Ordinal);
                if (cut >= 0) said = said.Substring(0, cut);
                throw Bad(
                    "setup.dt",
                    $"is {Dt.ToString("R", Inv)} at the world's PaceFactor of {paceFactor.ToString("R", Inv)} (D239), which cannot make a metabolic step: {said}");
            }
        }

        private static object OverrideValue(Type t, JsonNode v, string at)
        {
            try
            {
                if (t == typeof(float)) return Finite(v.AsFloat(), at);
                if (t == typeof(double)) return Finite(v.AsDouble(), at);
                if (t == typeof(int)) return WholeInt(v, at);
                if (t == typeof(bool)) return v.AsBool();
                if (t == typeof(string)) return v.AsString();
                if (t.IsEnum) return EnumNamed(t, v.AsString(), at);
                if (t == typeof(string[])) return v.Items().Select(x => x.AsString()).ToArray();
                Type element = ConfigSchema.EnumElementOf(t);
                if (element != null)
                {
                    JsonNode[] items = v.Items().ToArray();
                    Array a = System.Array.CreateInstance(element, items.Length);
                    for (int i = 0; i < items.Length; i++) a.SetValue(EnumNamed(element, items[i].AsString(), at), i);
                    return a;
                }
            }
            catch (Exception e) when (e is FormatException || e is OverflowException || e is InvalidOperationException) { throw Bad(at, e.Message); }
            throw Bad(at, "is a tunable of type " + t.Name + ", which a recipe cannot set");
        }

        private static float Finite(float f, string at) => float.IsFinite(f) ? f : throw Bad(at, "is a finite number");

        private static double Finite(double d, string at) => double.IsFinite(d) ? d : throw Bad(at, "is a finite number");

        private static object EnumNamed(Type t, string name, string at)
        {
            if (name == null || !Enum.GetNames(t).Contains(name)) throw Bad(at, $"\"{name}\" is not one of {string.Join(", ", Enum.GetNames(t))}");
            return Enum.Parse(t, name);
        }

        // ------------------------------------------------------------------ writing

        /// <summary>
        /// The resolved recipe as indented JSON in a fixed order, LF line ends: what is saved beside the
        /// run, hashed and registered. Reading it back gives the same recipe and the same text.
        /// </summary>
        public string Canonical()
        {
            var w = new Json.Writer(indent: true);
            w.BeginObject();
            w.Field("format", FormatVersion);
            w.Field("name", Name);
            w.Field("description", Description);
            w.Field("seed", Hex(Seed));

            w.BeginObject("world");
            w.Field("config", ConfigPath);
            w.Field("configHash", ConfigHash);
            w.BeginObject("overrides");
            foreach (KeyValuePair<string, JsonNode> kv in Overrides) w.Raw(kv.Key, Write(kv.Value));
            w.EndObject();
            w.EndObject();

            w.BeginObject("bodies");
            w.Field("source", BodySource);
            w.Field("founders", Founders);
            w.Field("trunk", Trunk);
            w.Field("pool", PoolPath);
            if (Keep == null) w.Raw("keep", "null");
            else w.Raw("keep", "[" + string.Join(", ", Keep.Select(k => k.ToString(Inv))) + "]");
            if (Brains != null) w.Field("brains", Brains);
            w.EndObject();

            w.BeginObject("setup");
            w.Field("seconds", Seconds);
            w.Field("dt", Dt);
            w.Field("tankSize", TankSize);
            w.Field("floorClosed", FloorClosed);
            w.Field("birthsHeld", BirthsHeld);
            if (Snow == null) w.Raw("snow", "null");
            else
            {
                w.BeginObject("snow");
                w.Field("density", Snow.Density);
                w.Field("draw", Snow.Fixed ? "fixed" : "halfToDouble");
                if (Snow.Layers > 0) w.Field("layers", Snow.Layers); else w.Raw("layers", "null");
                if (Snow.Layers > 0) w.Raw("islands", "null");
                else
                {
                    w.BeginObject("islands");
                    w.Field("wavelength", Snow.IslandWavelength);
                    w.Field("cover", Snow.IslandCover);
                    w.EndObject();
                }
                w.EndObject();
            }
            w.BeginObject("carrion");
            w.Field("count", CarrionCount);
            w.Field("joules", CarrionJoules);
            w.EndObject();
            w.BeginObject("start");
            w.Field("aboveBed", AboveBed);
            w.EndObject();
            w.BeginObject("purse");
            w.Field("rule", PurseRule);
            if (double.IsNaN(PurseSeconds)) w.Raw("seconds", "null"); else w.Field("seconds", PurseSeconds);
            w.EndObject();
            w.EndObject();

            // Written only when set, so that a recipe without leaves keeps the text and the hash it had before the section existed.
            if (Leaves != null)
            {
                w.BeginObject("leaves");
                w.Field("pool", Leaves.PoolPath);
                w.Field("count", Leaves.Count);
                w.Field("spentUnitsPerCubicMetre", Leaves.SpentUnitsPerCubicMetre);
                // Written only when set, for the section's own reason.
                if (Leaves.Patches != null)
                {
                    w.BeginObject("patches");
                    w.Field("count", Leaves.Patches.Count);
                    w.Field("radius", Leaves.Patches.RadiusMetres);
                    w.Field("ring", Leaves.Patches.RingMetres);
                    // Written only when set, for the patches' own reason.
                    if (Leaves.Patches.Switching != null)
                    {
                        w.BeginObject("switching");
                        w.Field("period", Leaves.Patches.Switching.PeriodSeconds);
                        w.Field("regrowAfter", Leaves.Patches.Switching.RegrowAfterSeconds);
                        w.Field("witherOver", Leaves.Patches.Switching.WitherOverSeconds);
                        w.Field("minimumTurn", Leaves.Patches.Switching.MinimumTurnDegrees);
                        w.EndObject();
                    }
                    w.EndObject();
                }
                w.EndObject();
            }

            if (Search == null) w.Raw("search", "null");
            else
            {
                w.BeginObject("search");
                w.Field("population", Search.Population);
                w.Field("draws", Search.Draws);
                w.Field("generations", Search.Generations);
                w.Field("elites", Search.Elites);
                w.Field("tournament", Search.Tournament);
                w.Field("perBody", Search.PerBody);
                w.Field("evolve", Search.Evolve);
                w.Field("evolveEnvelope", Search.EvolveEnvelope);
                if (Search.Crossover == null) w.Raw("crossover", "null");
                else
                {
                    w.BeginObject("crossover");
                    w.Field("rate", Search.Crossover.Rate);
                    w.Field("baseShare", Search.Crossover.BaseShare);
                    if (double.IsNaN(Search.Crossover.MutationByDistance)) w.Raw("mutationByDistance", "null");
                    else w.Field("mutationByDistance", Search.Crossover.MutationByDistance);
                    w.EndObject();
                }
                if (Search.Stop != null)
                {
                    w.BeginObject("stop");
                    w.Field("measure", Search.Stop.Measure);
                    w.Field("window", Search.Stop.Window);
                    w.Field("gain", Search.Stop.Gain);
                    w.Field("over", Search.Stop.Over);
                    w.Field("floor", Search.Stop.Floor);
                    w.EndObject();
                }
                w.EndObject();
            }

            w.BeginObject("reward");
            w.Field("class", RewardClass);
            w.Raw("parameters", Write(RewardParameters));
            w.EndObject();

            w.BeginObject("exam");
            w.Field("episodes", ExamEpisodes);
            w.Field("salt", Hex(ExamSalt));
            if (ExamPool != null) w.Field("pool", ExamPool);
            // One control a line: Core's writer has no raw array item, and a line each reads well.
            var lines = new List<string>();
            foreach (ExamControl c in Controls)
            {
                var one = new Json.Writer(indent: false);
                one.BeginObject();
                one.Field("kind", c.Kind);
                if (c.Kind == "knockout")
                {
                    one.BeginArray("channels");
                    foreach (SensorChannel ch in c.Channels) one.Value(ch.ToString());
                    one.EndArray();
                }
                if (c.Kind == "constant") one.Field("power", c.Power);
                if (c.Kind == "kinesis")
                {
                    one.Field("mode", c.Mode);
                    one.Field("bias", c.Bias);
                    one.Field("gain", c.Gain);
                }
                one.EndObject();
                lines.Add("      " + one.ToString());
            }
            w.Raw("controls", "[\n" + string.Join(",\n", lines) + "\n    ]");
            w.EndObject();

            w.EndObject();
            return w.ToString().Replace("\r\n", "\n") + "\n";
        }

        /// <summary>SHA-256 of <see cref="Canonical"/>, lowercase hex: the recipe's identity.</summary>
        public string Hash()
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(new UTF8Encoding(false).GetBytes(Canonical()))).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>A JSON value written compactly, keys in ordinal order so that the text does not hang on a parser's order.</summary>
        private static string Write(JsonNode v)
        {
            switch (v.Kind)
            {
                case JsonNode.NodeKind.Null: return "null";
                case JsonNode.NodeKind.Bool: return v.AsBool() ? "true" : "false";
                case JsonNode.NodeKind.Number: return v.AsDouble().ToString("R", Inv);
                case JsonNode.NodeKind.String: return RunRecord.JsonValue(v.AsString());
                case JsonNode.NodeKind.Array: return "[" + string.Join(", ", v.Items().Select(Write)) + "]";
                default:
                    return "{" + string.Join(", ", v.Keys().OrderBy(k => k, StringComparer.Ordinal)
                        .Select(k => RunRecord.JsonValue(k) + ": " + Write(v[k]))) + "}";
            }
        }

        private static string Hex(ulong v) => "0x" + v.ToString("x", Inv);

        // ------------------------------------------------------------------ small readers

        private static InvalidDataException Bad(string at, string what) => new InvalidDataException($"recipe: {at} {what}");

        /// <summary>Refuses a node that is not an object with exactly these keys: none missing, none unknown.</summary>
        private static void Exactly(JsonNode n, string at, params string[] keys)
        {
            if (n.Kind != JsonNode.NodeKind.Object) throw Bad(at, "is an object");
            var have = new HashSet<string>(n.Keys(), StringComparer.Ordinal);
            string[] missing = keys.Where(k => !have.Contains(k)).ToArray();
            string[] unknown = have.Where(k => !keys.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToArray();
            if (missing.Length > 0) throw Bad(at, "is missing " + string.Join(", ", missing) + " (every field is required; null where a setting is off)");
            if (unknown.Length > 0) throw Bad(at, "has unknown " + string.Join(", ", unknown));
        }

        private static double Num(JsonNode n, string key, string at)
        {
            JsonNode v = n[key];
            if (v.Kind != JsonNode.NodeKind.Number) throw Bad(at + "." + key, "is a number");
            return Finite(v.AsDouble(), at + "." + key);
        }

        private static int Int(JsonNode n, string key, string at) => WholeInt(n[key], at + "." + key);

        private static int WholeInt(JsonNode v, string at)
        {
            if (v.Kind != JsonNode.NodeKind.Number) throw Bad(at, "is a whole number");
            double d = v.AsDouble();
            if (d != Math.Floor(d) || d < int.MinValue || d > int.MaxValue) throw Bad(at, "is a whole number");
            return (int)d;
        }

        private static bool Bool(JsonNode n, string key, string at)
        {
            JsonNode v = n[key];
            if (v.Kind != JsonNode.NodeKind.Bool) throw Bad(at + "." + key, "is true or false");
            return v.AsBool();
        }

        private static string Str(JsonNode n, string key, string at)
        {
            JsonNode v = n[key];
            if (v.Kind != JsonNode.NodeKind.String && v.Kind != JsonNode.NodeKind.Null) throw Bad(at + "." + key, "is a string");
            return v.AsString();
        }

        private static IEnumerable<JsonNode> Array(JsonNode n, string key, string at)
        {
            JsonNode v = n[key];
            if (v.Kind != JsonNode.NodeKind.Array) throw Bad(at + "." + key, "is an array");
            return v.Items();
        }

        /// <summary>A seed or salt: a string, hex with 0x or decimal, read exactly.</summary>
        private static ulong Ulong(JsonNode n, string key, string at)
        {
            JsonNode v = n[key];
            string s = v.Kind == JsonNode.NodeKind.String ? v.AsString() : null;
            ulong value = 0;
            bool ok = s != null && (s.StartsWith("0x", StringComparison.Ordinal)
                ? ulong.TryParse(s.Substring(2), NumberStyles.AllowHexSpecifier, Inv, out value)
                : ulong.TryParse(s, NumberStyles.None, Inv, out value));
            if (!ok) throw Bad(at + "." + key, "is a string holding a whole number, hex with 0x or decimal (a JSON number would round above 2^53)");
            return value;
        }

        private static SensorChannel Channel(JsonNode v, string at)
        {
            string name = v.Kind == JsonNode.NodeKind.String ? v.AsString() : null;
            if (name == null || !Enum.GetNames(typeof(SensorChannel)).Contains(name))
                throw Bad(at, $"\"{name}\" is not a sensor channel ({string.Join(", ", Enum.GetNames(typeof(SensorChannel)))})");
            return (SensorChannel)Enum.Parse(typeof(SensorChannel), name);
        }

        /// <summary>A path the recipe names, resolved against the repository root and refused if it
        /// leaves it: a recipe can be written by an agent through the room, so it reads nothing
        /// outside the repository.</summary>
        private static string Resolved(string path, string root, string at)
        {
            if (Path.IsPathRooted(path) || path.Contains(':'))
                throw Bad(at, $"is {path}; paths are relative to the repository root, so that every worktree reads its own");
            string full = Path.GetFullPath(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)));
            string inside = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(inside, StringComparison.OrdinalIgnoreCase))
                throw Bad(at, $"is {path}, which leaves the repository; paths stay under the repository root");
            return full;
        }

        /// <summary>The first folder at or above <paramref name="from"/> holding src/Evosim.Core, or null.</summary>
        public static string RepoRootAbove(string from)
        {
            for (var dir = new DirectoryInfo(Path.GetFullPath(from)); dir != null; dir = dir.Parent)
                if (Directory.Exists(Path.Combine(dir.FullName, "src", "Evosim.Core"))) return dir.FullName;
            return null;
        }
    }
}
