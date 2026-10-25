using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Evosim.Core;

namespace Evosim.Nursery
{
    /// <summary>
    /// A request to step one episode of a finished run again (<c>--restep &lt;request.json&gt;</c>), with the numbers that name it. Every field is
    /// required but two (<c>config</c> and <c>sampleRate</c>, which may be left out or null), and an unknown one is refused.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Paths are relative to the repository root</b>, and refused if rooted or if they leave it, as a recipe's are: a request can be written by
    /// an agent through the room, so it reads nothing outside the repository. A pool that lives in another checkout is copied into the tree first.
    /// </para>
    /// <para>
    /// <b>Seeds and salts are strings</b> (hex with <c>0x</c>, or decimal), read exactly: a JSON number rounds above 2^53, and a re-step on a
    /// rounded seed is another episode. The episode's seed is the run's own formula, <c>Rng.SeedFor(seed ^ salt, generation * 1000 + episode)</c>.
    /// </para>
    /// <para>
    /// <b>The members</b> are indices into the pool file's lines (its non-empty lines, in order, whatever the recipe's <c>keep</c> says), in the
    /// slot order the tank was dealt in: heights are drawn per slot, so the order is part of the episode. <b>The brain</b> is an index into the
    /// recipe's exam controls, never a label.
    /// </para>
    /// <para>
    /// <b>The run's numbers are checked against the recipe</b>, which the hash ties to the run (<see cref="Restep.Load"/>): the seed is the recipe's, the
    /// salt is the exam's, the generation is 0, the episode is one of the exam's, and the members are exactly the tank the nursery's own deal
    /// (<see cref="TankLog.PlanTanks"/>) gives that episode at <c>tank</c>, in slot order. A request that names a tank the deal does not give is
    /// refused naming the field, and not stepped as another episode.
    /// </para>
    /// </remarks>
    public sealed class RestepRequest
    {
        public const double DefaultSampleRate = 10d;

        public string Recipe, RecipeHash, Config, Pool;

        /// <summary>The run's seed and the exam's (or the search's) salt, as the request wrote them and as numbers.</summary>
        public string SeedText, SaltText;
        public ulong Seed, Salt;

        /// <summary>The episode is the exam's draw; <c>Tank</c> is the index of the tank in that draw's deal that <see cref="Members"/> are.</summary>
        public int Generation, Episode, Brain, Tank;
        public int[] Members = Array.Empty<int>();
        public double SampleRate = DefaultSampleRate;

        private static readonly string[] Required = { "recipe", "recipeHash", "pool", "seed", "salt", "generation", "episode", "brain", "tank", "members" };
        private static readonly string[] Optional = { "config", "sampleRate" };

        public static RestepRequest Read(string path)
        {
            string text;
            try { text = File.ReadAllText(path); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                throw new InvalidDataException($"the request {path} cannot be read ({e.Message})", e);
            }
            return Parse(text);
        }

        public static RestepRequest Parse(string json)
        {
            JsonNode n;
            try { n = Json.Parse(json); }
            catch (Exception e) when (!(e is InvalidDataException)) { throw new InvalidDataException("the request is not JSON: " + e.Message, e); }
            if (n.Kind != JsonNode.NodeKind.Object) throw Bad("is a JSON object");

            var have = new HashSet<string>(n.Keys(), StringComparer.Ordinal);
            string[] missing = Required.Where(k => !have.Contains(k)).ToArray();
            string[] unknown = have.Where(k => !Required.Contains(k) && !Optional.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToArray();
            if (missing.Length > 0) throw Bad("is missing " + string.Join(", ", missing));
            if (unknown.Length > 0) throw Bad("has unknown " + string.Join(", ", unknown));

            var q = new RestepRequest
            {
                Recipe = RelPath(n, "recipe"),
                RecipeHash = Text(n, "recipeHash"),
                Pool = RelPath(n, "pool"),
                Config = n.Has("config") && n["config"].Kind != JsonNode.NodeKind.Null ? RelPath(n, "config") : null,
                SeedText = Text(n, "seed"),
                SaltText = Text(n, "salt"),
                Generation = Whole(n["generation"], "generation"),
                Episode = Whole(n["episode"], "episode"),
                Brain = Whole(n["brain"], "brain"),
                Tank = Whole(n["tank"], "tank"),
            };
            q.Seed = U64(q.SeedText, "seed");
            q.Salt = U64(q.SaltText, "salt");
            if (q.Generation < 0) throw Bad("generation must be 0 or more");
            if (q.Episode < 0 || q.Episode >= 1000) throw Bad("episode must be 0 to 999 (a seed is the generation times 1,000 plus the episode)");
            if (q.Brain < 0) throw Bad("brain must be 0 or more");
            if (q.Tank < 0) throw Bad("tank must be 0 or more");
            if (q.RecipeHash.Length == 0) throw Bad("recipeHash is the resolved recipe's hash");

            JsonNode m = n["members"];
            if (m.Kind != JsonNode.NodeKind.Array || m.Count == 0) throw Bad("members is a non-empty array of pool indices");
            q.Members = m.Items().Select(x => Whole(x, "members")).ToArray();
            if (q.Members.Any(x => x < 0)) throw Bad("members are pool indices of 0 or more");
            int twice = q.Members.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).DefaultIfEmpty(-1).First();
            if (twice >= 0) throw Bad($"members lists pool index {twice} twice: a tank holds each genome of the pool once");

            if (n.Has("sampleRate") && n["sampleRate"].Kind != JsonNode.NodeKind.Null)
            {
                if (n["sampleRate"].Kind != JsonNode.NodeKind.Number) throw Bad("sampleRate is a number of samples a second");
                q.SampleRate = n["sampleRate"].AsDouble();
                if (!(q.SampleRate > 0d) || double.IsInfinity(q.SampleRate)) throw Bad("sampleRate is above 0");
            }
            return q;
        }

        /// <summary>The request as compact JSON, seeds as the strings they were written as; what <c>episode.json</c> echoes.</summary>
        public string ToJson()
        {
            var w = new Json.Writer(indent: false);
            w.BeginObject();
            w.Field("recipe", Recipe);
            w.Field("recipeHash", RecipeHash);
            w.Field("config", Config);
            w.Field("pool", Pool);
            w.Field("seed", SeedText);
            w.Field("salt", SaltText);
            w.Field("generation", Generation);
            w.Field("episode", Episode);
            w.Field("brain", Brain);
            w.Field("tank", Tank);
            w.Raw("members", "[" + string.Join(",", Members.Select(x => x.ToString(CultureInfo.InvariantCulture))) + "]");
            w.Field("sampleRate", SampleRate);
            w.EndObject();
            return w.ToString();
        }

        private static InvalidDataException Bad(string what) => new InvalidDataException("the request " + what);

        private static string Text(JsonNode n, string key)
        {
            JsonNode v = n[key];
            if (v.Kind != JsonNode.NodeKind.String) throw Bad(key + " is a string");
            return v.AsString();
        }

        /// <summary>A repository-relative path as text: not rooted, no drive, not empty. That it stays inside the repository is checked where the root is known.</summary>
        private static string RelPath(JsonNode n, string key)
        {
            string p = Text(n, key);
            if (p.Length == 0 || System.IO.Path.IsPathRooted(p) || p.Contains(':'))
                throw Bad($"{key} is {(p.Length == 0 ? "empty" : p)}; paths are relative to the repository root, so that every worktree reads its own");
            return p;
        }

        private static int Whole(JsonNode v, string key)
        {
            if (v.Kind != JsonNode.NodeKind.Number) throw Bad(key + " is a whole number");
            double d = v.AsDouble();
            if (d != Math.Floor(d) || d < int.MinValue || d > int.MaxValue) throw Bad(key + " is a whole number");
            return (int)d;
        }

        private static ulong U64(string s, string key)
        {
            ulong value = 0;
            bool ok = s.StartsWith("0x", StringComparison.Ordinal)
                ? ulong.TryParse(s.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value)
                : ulong.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out value);
            if (!ok) throw Bad(key + " is a string holding a whole number, hex with 0x or decimal");
            return value;
        }
    }

    /// <summary>One slot of the re-stepped tank: where it came from in the pool, and the body it carries.</summary>
    public sealed class RestepMember
    {
        public int Slot, PoolIndex, Body;
    }

    /// <summary>What a request resolved to: everything the episode needs, loaded and checked, and nothing stepped.</summary>
    public sealed class RestepPlan
    {
        public RestepRequest Request;
        public string RepoRoot, RecipePath, ConfigPath, OutFolder;
        public string RecipeHash, ConfigHash, EpisodeConfigHash;
        public ulong EpisodeSeed;
        public int ControlIndex;
        public string ControlLabel, ControlKind;
        public int PoolCount;
        public double Seconds;
        public float Dt;
        public int StepsPerSample;
        public double SampleRate;
        public List<RestepMember> Members = new List<RestepMember>();

        internal Genome[] Genomes;

        /// <summary>The plan as lines of text, which is what <c>--check</c> prints.</summary>
        public string Describe()
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("restep plan: nothing is stepped\n");
            sb.Append("recipe ").Append(Request.Recipe).Append(" hash ").Append(RecipeHash).Append('\n');
            sb.Append("config hash ").Append(ConfigHash).Append('\n');
            sb.Append("episode config hash ").Append(EpisodeConfigHash).Append('\n');
            sb.Append("pool ").Append(Request.Pool).Append(": ").Append(PoolCount.ToString(inv)).Append(" genomes\n");
            sb.Append("control ").Append(ControlIndex.ToString(inv)).Append(": ").Append(ControlLabel).Append(" (").Append(ControlKind).Append(")\n");
            sb.Append("generation ").Append(Request.Generation.ToString(inv)).Append(", episode ").Append(Request.Episode.ToString(inv))
              .Append(", run seed ").Append(Request.Seed.ToString(inv)).Append(", salt ").Append(Request.Salt.ToString(inv)).Append('\n');
            sb.Append("episode seed ").Append(EpisodeSeed.ToString(inv)).Append('\n');
            sb.Append("seconds ").Append(Seconds.ToString("R", inv)).Append(", dt ").Append(Dt.ToString("R", inv))
              .Append(", sample rate ").Append(SampleRate.ToString("R", inv)).Append(" per second (every ").Append(StepsPerSample.ToString(inv)).Append(" steps)\n");
            foreach (RestepMember m in Members)
                sb.Append("member ").Append(m.Slot.ToString(inv)).Append(": pool ").Append(m.PoolIndex.ToString(inv)).Append(", body ").Append(m.Body.ToString(inv)).Append('\n');
            return sb.ToString();
        }
    }

    /// <summary>One body's reading from the re-stepped episode: the fields of the nursery's own <c>Resident</c>, as numbers.</summary>
    public sealed class RestepMemberOutcome
    {
        public bool Landed, Survived;
        public double Net, Start, End, Food, Eaten, Kills, Killed, Meals, Light, Work, Path, Turn, Fresh, Upkeep, Handling, Exuded, DiedAt;
    }

    /// <summary>What a swum request gave: one reading per slot, in slot order.</summary>
    public sealed class RestepOutcome
    {
        public RestepMemberOutcome[] Members = Array.Empty<RestepMemberOutcome>();
    }

    /// <summary>
    /// <c>--restep</c>: one episode of a finished run stepped again, in the nursery's own <c>TankEpisode</c>, with a watcher that records frames. The
    /// whole of the work is in <see cref="Program"/>'s partial below, since the episode's settings are that class's private state.
    /// </summary>
    public static class Restep
    {
        public const string Usage = "usage: Evosim.Nursery --restep <request.json> --out <dir> [--threads 1]\n" +
                                    "       Evosim.Nursery --restep <request.json> --check\n" +
                                    "       Evosim.Nursery --restep <request.json> --out <dir> --no-watch   (test-only: steps with no watcher, writes result.json alone)\n" +
                                    "       Evosim.Nursery --restep <request.json> --out <dir> --patches    (D182: no frames; result.json and patches.jsonl)";

        /// <summary>The program's entry for <c>--restep</c>: standard output and error, the repository found above the working directory.</summary>
        public static int Cli(string[] args) => Program.RestepMain(args, null, Console.Out, Console.Error);

        /// <summary>The same with the repository root and the two streams given, which is what a test calls.</summary>
        public static int Run(string[] args, string repoRoot, TextWriter stdout, TextWriter stderr) => Program.RestepMain(args, repoRoot, stdout, stderr);

        /// <summary>
        /// Loads and checks a request and sets the nursery's settings to it, ready to be swum; <paramref name="outFolder"/> is required (a slow tank's
        /// line goes to its <c>nursery.log</c>). Swims nothing and writes nothing; refuses with <see cref="InvalidDataException"/>. <b>A refused load
        /// leaves nothing half-set</b>: the plan loaded before is cleared first (so that a swim of it refuses), and the nursery's settings are as they
        /// were before the call.
        /// </summary>
        public static RestepPlan Load(RestepRequest request, string repoRoot, string outFolder) => Program.RestepLoad(request, repoRoot, outFolder, planOnly: false);

        /// <summary>
        /// The same checks and the plan they give, for <c>--check</c>: no out folder, and nothing left set. The plan cannot be swum (its settings are
        /// not left in place), and it clears the plan loaded before.
        /// </summary>
        public static RestepPlan Plan(RestepRequest request, string repoRoot) => Program.RestepLoad(request, repoRoot, null, planOnly: true);

        /// <summary>Swims the loaded plan's episode once, with the watcher or without it (null), and gives the readings. Single-threaded.</summary>
        public static RestepOutcome Swim(RestepPlan plan, ITankWatcher watcher) => Program.RestepSwim(plan, watcher);
    }

    /// <summary>A <c>result.json</c> that was there when this run came to publish its own, which is left as it is.</summary>
    public sealed class ResultAlreadyThereException : IOException
    {
        public ResultAlreadyThereException(string message) : base(message) { }
    }

    /// <summary>The files a re-step publishes.</summary>
    public static class RestepFiles
    {
        public const string ResultName = "result.json";

        /// <summary>
        /// Publishes <c>result.json</c> last, so that its presence means a complete episode: the text is written to a temporary file of this process's
        /// own name, flushed to the disk (not left in the system's cache), and moved into place without overwriting. A <c>result.json</c> that is
        /// there by then (another <c>--restep</c> into the same folder finished first) is never replaced, and the exception says so; this run's
        /// result is then left in the temporary file, named in the message.
        /// </summary>
        public static void PublishResult(string outFolder, string text)
        {
            string dest = Path.Combine(outFolder, ResultName);
            string tempName = ResultName + "." + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + ".tmp";
            string temp = Path.Combine(outFolder, tempName);
            byte[] bytes = new UTF8Encoding(false).GetBytes(text);
            using (var f = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                f.Write(bytes, 0, bytes.Length);
                f.Flush(true);
            }

            string said = $"{ResultName} appeared in {outFolder} while this episode was being stepped (another --restep into the same folder finished first); it is left as it is, and this run's own result is in {tempName}";
            if (File.Exists(dest)) throw new ResultAlreadyThereException(said);
            try { File.Move(temp, dest, overwrite: false); }
            catch (IOException) when (File.Exists(dest)) { throw new ResultAlreadyThereException(said); }
        }
    }

    internal static partial class Program
    {
        private static readonly object _restepLock = new object();
        private static RestepPlan _restepActive;

        /// <summary>A path the request names, resolved against the repository root and refused if it leaves it.</summary>
        private static string RestepResolve(string path, string root, string what)
        {
            string full = Path.GetFullPath(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)));
            string inside = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(inside, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"the request's {what} is {path}, which leaves the repository; paths stay under the repository root");
            if (!File.Exists(full)) throw new InvalidDataException($"the request's {what} {path} does not exist under the repository root {root}");
            RestepRefuseReparse(full, inside, what, path);
            return full;
        }

        /// <summary>
        /// Refuses a path that passes through a reparse point (a symbolic link or a junction) below the repository root: such a path can name a file
        /// outside the repository while looking as if it lay inside it, and the room copies a file in rather than linking it. The root itself is not
        /// asked (a worktree may be reached through one); every folder and the file below it are.
        /// </summary>
        private static void RestepRefuseReparse(string full, string insideRoot, string what, string path)
        {
            string[] parts = full.Substring(insideRoot.Length).Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            string at = insideRoot.TrimEnd(Path.DirectorySeparatorChar);
            foreach (string part in parts)
            {
                at = Path.Combine(at, part);
                FileAttributes attributes;
                try { attributes = File.GetAttributes(at); }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    throw new InvalidDataException($"the request's {what} {path} cannot be checked for links at {part} ({e.Message})", e);
                }
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException($"the request's {what} {path} passes through {part}, which is a reparse point (a link or a junction); paths stay inside the repository, so give the file itself");
            }
        }

        /// <summary>
        /// Loads and checks a request. <b>Every check that needs no setting of the nursery's is made first, on locals</b>; the shared settings (the static
        /// options, recipe, config, arena and reward the episode reads) are set only after those have passed, and are put back as they were if what
        /// is left (the control applied to each genome, which reads the config) refuses, or when only a plan is asked for. The plan loaded before is
        /// cleared before anything is looked at, so a refused load leaves no plan to swim.
        /// </summary>
        internal static RestepPlan RestepLoad(RestepRequest q, string repoRoot, string outFolder, bool planOnly)
        {
            lock (_restepLock)
            {
                _restepActive = null;
                if (!planOnly && string.IsNullOrWhiteSpace(outFolder))
                    throw new InvalidDataException("a re-step is loaded with an out folder (the episode's slow-tank line and its files go there); only a --check plan is made without one");

                string root = repoRoot ?? Recipe.RepoRootAbove(Directory.GetCurrentDirectory()) ?? Recipe.RepoRootAbove(AppContext.BaseDirectory)
                    ?? throw new InvalidDataException("no repository root (a folder holding src/Evosim.Core) above the working directory or the program");

                // The recipe, by its hash.
                string recipeFull = RestepResolve(q.Recipe, root, "recipe");
                Recipe recipe = Recipe.Load(recipeFull);
                string recipeHash = recipe.Hash();
                if (!string.Equals(recipeHash, q.RecipeHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"the recipe {q.Recipe} hashes to {recipeHash}, not the {q.RecipeHash} the request gives: it is not the recipe the run was made from (it was edited, or the request names another file)");

                // The pool, by line: a body index, a tab, a genome.
                string poolFull = RestepResolve(q.Pool, root, "pool");
                string[] lines = File.ReadAllLines(poolFull).Where(l => l.Length > 0).ToArray();
                foreach (int m in q.Members)
                    if (m >= lines.Length) throw new InvalidDataException($"the request's members name pool index {m}, and {q.Pool} holds {lines.Length} genomes (0 to {lines.Length - 1})");
                if (q.Members.Length > recipe.TankSize)
                    throw new InvalidDataException($"the request's tank holds {q.Members.Length} members, and the recipe's tanks hold {recipe.TankSize}");
                if (q.Brain >= recipe.Controls.Count)
                    throw new InvalidDataException($"the request's brain is {q.Brain}, and the recipe's exam has {recipe.Controls.Count} controls (0 to {recipe.Controls.Count - 1})");

                // The run's numbers, against the recipe the hash ties to the run: an exam episode of that recipe, and the tank its own deal gives.
                if (q.Seed != recipe.Seed)
                    throw new InvalidDataException($"the request's seed is {q.SeedText}, and the recipe's seed is {recipe.Seed}: it is not the run the recipe made");
                if (q.Salt != recipe.ExamSalt)
                    throw new InvalidDataException($"the request's salt is {q.SaltText}, and the recipe's exam salt is {recipe.ExamSalt}: this tool re-steps an exam's episodes");
                if (q.Generation != 0)
                    throw new InvalidDataException($"the request's generation is {q.Generation}, and an exam's is 0");
                if (q.Episode >= recipe.ExamEpisodes)
                    throw new InvalidDataException($"the request's episode is {q.Episode}, and the recipe's exam has {recipe.ExamEpisodes} episodes (0 to {recipe.ExamEpisodes - 1})");
                ulong dealSeed = SeedsFor(recipe.Seed, 0, q.Episode + 1, recipe.ExamSalt)[q.Episode];
                int[][] deal = TankLog.PlanTanks(lines.Length, recipe.TankSize, new[] { dealSeed })[0];
                if (q.Tank >= deal.Length)
                    throw new InvalidDataException($"the request's tank is {q.Tank}, and the exam's deal of episode {q.Episode} over the pool's {lines.Length} genomes has {deal.Length} tank(s) (0 to {deal.Length - 1})");
                if (!deal[q.Tank].SequenceEqual(q.Members))
                    throw new InvalidDataException($"the request's members [{string.Join(",", q.Members)}] are not tank {q.Tank} of episode {q.Episode}'s deal, which is [{string.Join(",", deal[q.Tank])}] (the pool's {lines.Length} genomes in tanks of {recipe.TankSize}, in slot order)");

                // The config the run ran on: the recipe's own, or the request's, which must hash to the recipe's.
                string configFull = recipe.ConfigFullPath;
                if (q.Config != null)
                {
                    configFull = RestepResolve(q.Config, root, "config");
                    string got = RunConfigJson.Read(File.ReadAllText(configFull)).Hash();
                    if (!string.Equals(got, recipe.ConfigHash, StringComparison.Ordinal))
                        throw new InvalidDataException($"the request's config {q.Config} hashes to {got}, and the recipe's world is {recipe.ConfigHash}");
                }

                string configText = File.ReadAllText(configFull);
                RunConfig episodeConfig = recipe.EpisodeConfig(configText);
                if (episodeConfig.FoundingTricklePerSecond > 0f || episodeConfig.FoundingTricklePoolCount > 0)
                    throw new InvalidDataException("the episode config runs a founding trickle; make it with EVOSIM_TRICKLE 0 and no pool");

                double stepsPerSample = 1d / (q.SampleRate * recipe.Dt);
                if (Math.Abs(stepsPerSample - Math.Round(stepsPerSample)) > 1e-3 || Math.Round(stepsPerSample) < 1d)
                    throw new InvalidDataException($"a sample rate of {q.SampleRate.ToString("R", CultureInfo.InvariantCulture)} a second is not a whole number of the recipe's {recipe.Dt.ToString("R", CultureInfo.InvariantCulture)} s steps");

                // The pool's lines the members name: a body index and a genome, read before anything is set.
                var genomes = new Genome[q.Members.Length];
                var bodies = new int[q.Members.Length];
                for (int slot = 0; slot < q.Members.Length; slot++)
                {
                    string line = lines[q.Members[slot]];
                    int tab = line.IndexOf('\t');
                    if (tab <= 0 || !int.TryParse(line.Substring(0, tab), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out bodies[slot]))
                        throw new InvalidDataException($"line {q.Members[slot] + 1} of {q.Pool} does not start with a body index and a tab");
                    try { genomes[slot] = GenomeJson.Read(line.Substring(tab + 1)); }
                    catch (Exception e) when (e is InvalidDataException || e is FormatException || e is InvalidOperationException || e is ArgumentException)
                    {
                        throw new InvalidDataException($"the genome on line {q.Members[slot] + 1} of {q.Pool} does not load: {e.Message}", e);
                    }
                }

                // Every check that can be made without the nursery's settings has passed. Now they are set, as Main sets them for a run, so that TankEpisode
                // reads what it read then (the control's brain is grafted with the config's development rules, so it needs them); and put back as they
                // were if anything below refuses, and when a plan alone is asked for.
                Options savedOptions = _o;
                Recipe savedRecipe = _recipe;
                RunConfig savedConfig = _config;
                string savedConfigText = _configText;
                Arena savedSearch = _search;
                IReward savedReward = _reward;
                LeafPool savedLeaves = _leafPool;
                void PutBack()
                {
                    _o = savedOptions; _recipe = savedRecipe; _config = savedConfig; _configText = savedConfigText; _search = savedSearch; _reward = savedReward; _leafPool = savedLeaves;
                }

                RestepPlan plan;
                try
                {
                    _o = new Options { Recipe = recipeFull };
                    _recipe = recipe;
                    ApplyRecipe();
                    _o.Config = configFull;
                    _o.Threads = 1;
                    _o.Out = outFolder;
                    _o.Seed = q.Seed;
                    _configText = configText;
                    _config = EpisodeConfig(_configText);
                    _search = new Arena
                    {
                        Name = "search", Text = _configText, Config = EpisodeConfig(_configText), Seconds = _o.Seconds,
                        Corpses = _o.Corpses, CorpseJoules = _o.CorpseJoules,
                    };
                    _reward = RewardRegistry.Create(_recipe.RewardClass, _recipe.RewardParameters);

                    // The control, applied to each member's genome, and the episode's seed by the run's own function.
                    Recipe.ExamControl control = recipe.Controls[q.Brain];
                    Func<Genome, Genome> make = ExamBrain(control);
                    plan = new RestepPlan
                    {
                        Request = q, RepoRoot = root, RecipePath = recipeFull, ConfigPath = configFull, OutFolder = outFolder,
                        RecipeHash = recipeHash, ConfigHash = recipe.ConfigHash, EpisodeConfigHash = _search.Config.Hash(),
                        EpisodeSeed = SeedsFor(q.Generation, q.Episode + 1, q.Salt)[q.Episode],
                        ControlIndex = q.Brain, ControlLabel = control.Label, ControlKind = control.Kind,
                        PoolCount = lines.Length, Seconds = _search.Seconds, Dt = _o.Dt,
                        StepsPerSample = (int)Math.Round(stepsPerSample), SampleRate = q.SampleRate,
                        Genomes = new Genome[q.Members.Length],
                    };
                    for (int slot = 0; slot < q.Members.Length; slot++)
                    {
                        plan.Genomes[slot] = make(genomes[slot]);
                        plan.Members.Add(new RestepMember { Slot = slot, PoolIndex = q.Members[slot], Body = bodies[slot] });
                    }
                }
                catch
                {
                    PutBack();
                    throw;
                }

                if (planOnly)
                {
                    PutBack();
                    return plan;
                }
                _restepActive = plan;
                return plan;
            }
        }

        internal static RestepOutcome RestepSwim(RestepPlan plan, ITankWatcher watcher)
        {
            lock (_restepLock)
            {
                if (!ReferenceEquals(plan, _restepActive))
                    throw new InvalidOperationException("this plan is not the one loaded last: the nursery's settings are static, so a plan is swum right after it is loaded");
                Resident[] r = TankEpisode(plan.Genomes, plan.EpisodeSeed, _search, watcher);
                return new RestepOutcome
                {
                    Members = r.Select(x => new RestepMemberOutcome
                    {
                        Landed = x.Landed, Survived = x.Survived, Net = x.Net, Start = x.Start, End = x.End, Food = x.Food, Eaten = x.Eaten, Kills = x.Kills, Killed = x.Killed, Meals = x.Meals, Light = x.Light,
                        Work = x.Work, Path = x.Path, Turn = x.Turn, Fresh = x.Fresh, Upkeep = x.Upkeep, Handling = x.Handling, Exuded = x.Exuded, DiedAt = x.DiedAt,
                    }).ToArray(),
                };
            }
        }

        /// <summary>
        /// <c>--restep &lt;request.json&gt; --out &lt;dir&gt; [--check] [--threads 1] [--no-watch]</c>: 0 done, 2 refused, 1 failed. <c>--no-watch</c> is the
        /// test-only mode of the swim tests: the episode is stepped with no watcher, and only <c>result.json</c> is written.
        /// </summary>
        internal static int RestepMain(string[] args, string repoRoot, TextWriter stdout, TextWriter stderr)
        {
            string requestPath = null, outFolder = null;
            bool check = false, noWatch = false, patches = false;
            int threads = 1;
            try
            {
                var seen = new List<string>();
                for (int i = 0; i < args.Length; i++)
                {
                    string a = args[i];
                    string Value() => i + 1 < args.Length ? args[++i] : throw new ArgumentException(a + " needs a value\n" + Restep.Usage);
                    if (a.StartsWith("--", StringComparison.Ordinal)) seen.Add(a);
                    switch (a)
                    {
                        case "--restep": requestPath = Value(); break;
                        case "--out": outFolder = Value(); break;
                        case "--check": check = true; break;
                        case "--no-watch": noWatch = true; break;
                        case "--patches": patches = true; break;
                        case "--threads": threads = int.Parse(Value(), CultureInfo.InvariantCulture); break;
                        default: throw new ArgumentException("unknown " + a + "\n" + Restep.Usage);
                    }
                }
                string[] twice = seen.GroupBy(f => f).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
                if (twice.Length > 0) throw new ArgumentException("given twice: " + string.Join(" ", twice));
                if (requestPath == null) throw new ArgumentException(Restep.Usage);
                if (threads != 1) throw new ArgumentException("--restep steps one episode on one thread: --threads must be 1 (the room's launcher pins it), not " + threads);
                if (outFolder == null && !check) throw new ArgumentException("--restep needs --out <dir> (or --check, which writes nothing)\n" + Restep.Usage);
                if (noWatch && check) throw new ArgumentException("--no-watch steps an episode and --check steps none: give one\n" + Restep.Usage);
                if (patches && (check || noWatch)) throw new ArgumentException("--patches steps an episode with its own watcher: give it without --check or --no-watch\n" + Restep.Usage);
            }
            catch (Exception e) when (e is ArgumentException || e is FormatException || e is OverflowException)
            {
                stderr.WriteLine(e.Message);
                return 2;
            }

            RestepPlan plan;
            try
            {
                RestepRequest request = RestepRequest.Read(requestPath);
                if (!check)
                {
                    string result = Path.Combine(outFolder, "result.json");
                    if (File.Exists(result))
                        throw new InvalidDataException($"--out {outFolder} already holds a result.json: a finished episode is not written over, give a new folder");
                }
                plan = check ? Restep.Plan(request, repoRoot) : Restep.Load(request, repoRoot, outFolder);
            }
            catch (Exception e) when (e is InvalidDataException || e is IOException || e is UnauthorizedAccessException || e is ArgumentException
                || e is FormatException || e is InvalidOperationException || e is OverflowException)
            {
                stderr.WriteLine("--restep: " + e.Message);
                return 2;
            }

            stdout.Write(plan.Describe());
            if (check) return 0;

            try
            {
                Directory.CreateDirectory(outFolder);
                return noWatch ? RestepSwimUnwatched(plan, outFolder, stdout)
                    : patches ? RestepSwimPatches(plan, outFolder, stdout)
                    : RestepSwimAndWrite(plan, outFolder, stdout);
            }
            catch (ResultAlreadyThereException e)
            {
                stderr.WriteLine("--restep: " + e.Message);
                return 1;
            }
            catch (Exception e)
            {
                stderr.WriteLine("--restep failed, and no result.json is written: " + e.GetType().Name + ": " + e.Message);
                return 1;
            }
        }

        private static int RestepSwimAndWrite(RestepPlan plan, string outFolder, TextWriter stdout)
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            string framesPath = Path.Combine(outFolder, "frames.bin"), eventsPath = Path.Combine(outFolder, "events.jsonl");
            RunRecord build = new RunRecord();
            try { build.ReadBuild(plan.RepoRoot); }
            catch (InvalidDataException e) { build.Note = e.Message; }

            RestepOutcome outcome;
            FrameRecorder recorder = new FrameRecorder(framesPath, eventsPath, plan.SampleRate, plan.StepsPerSample);
            try { outcome = Restep.Swim(plan, recorder); }
            finally { recorder.Dispose(); }

            // episode.json: the request, the build, and what the frames are.
            var w = new Json.Writer(indent: true);
            w.BeginObject();
            w.Field("format", 1);
            w.Field("program", "Evosim.Nursery --restep");
            w.Raw("request", plan.Request.ToJson());
            w.BeginObject("build");
            w.Field("gitCommit", build.GitCommit);
            if (build.GitDirty.HasValue) w.Field("gitDirty", build.GitDirty.Value); else w.Raw("gitDirty", "null");
            w.Field("coreHash", build.CoreHash);
            w.Field("dynamicsHash", build.DynamicsHash);
            w.Field("farmHash", build.FarmHash);
            w.Field("nurseryHash", build.NurseryHash);
            w.Field("runtime", System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
            w.Field("tieredCompilation", AppContext.GetData("System.Runtime.TieredCompilation")?.ToString());
            w.Field("note", build.Note);
            w.EndObject();
            w.Field("recipeHash", plan.RecipeHash);
            w.Field("configHash", plan.ConfigHash);
            w.Field("episodeConfigHash", plan.EpisodeConfigHash);
            w.Field("episodeSeed", plan.EpisodeSeed.ToString(inv));
            w.BeginObject("control");
            w.Field("index", plan.ControlIndex);
            w.Field("label", plan.ControlLabel);
            w.Field("kind", plan.ControlKind);
            w.EndObject();
            w.BeginArray("members");
            int[] links = recorder.LinksOfMember;
            foreach (RestepMember m in plan.Members)
            {
                w.BeginObject();
                w.Field("slot", m.Slot);
                w.Field("pool", m.PoolIndex);
                w.Field("body", m.Body);
                w.Field("landed", outcome.Members[m.Slot].Landed);
                w.Field("links", m.Slot < links.Length ? links[m.Slot] : 0);
                w.EndObject();
            }
            w.EndArray();
            w.Field("seconds", plan.Seconds);
            w.Field("dt", plan.Dt);
            w.Field("sampleRate", plan.SampleRate);
            w.Field("stepsPerSample", plan.StepsPerSample);
            w.Field("frames", recorder.FrameCount);
            w.Field("frameFloats", recorder.Channels.Count);
            w.Field("encoding", "float32 little-endian, frame-major: frame k is frameFloats values, channel order as listed; the first channel is the physics clock t");
            w.Field("islandRule", recorder.IslandRule);
            IslandScan scan = recorder.Islands;
            if (scan != null)
            {
                w.BeginObject("islands");
                w.Field("read", scan.Read);
                w.Field("rule", scan.Rule);
                w.Field("grid", IslandScan.Grid);
                w.Field("scanHeightAboveBed", IslandScan.HeightAboveBed);
                w.Field("enterShare", IslandScan.EnterShare);
                w.Field("exitShare", IslandScan.ExitShare);
                w.Field("patches", scan.Patches);
                w.Field("points", scan.Points);
                w.Field("pointsInIsland", scan.PointsInIsland);
                if (scan.Read)
                {
                    w.Field("richest", scan.Richest);
                    w.Field("enterAt", scan.EnterAt);
                    w.Field("exitAt", scan.ExitAt);
                }
                else
                {
                    w.Raw("richest", "null");
                    w.Raw("enterAt", "null");
                    w.Raw("exitAt", "null");
                }
                w.EndObject();
            }
            w.Field("finalSolverDigest", recorder.FinalDigest.ToString("x16", inv));
            var lines = new List<string>();
            foreach (FrameChannel c in recorder.Channels)
            {
                var one = new Json.Writer(indent: false);
                one.BeginObject();
                one.Field("name", c.Name);
                one.Field("unit", c.Unit);
                one.EndObject();
                lines.Add("    " + one);
            }
            w.Raw("channels", "[\n" + string.Join(",\n", lines) + "\n  ]");
            w.EndObject();
            File.WriteAllText(Path.Combine(outFolder, "episode.json"), w.ToString().Replace("\r\n", "\n") + "\n", new UTF8Encoding(false));

            // result.json last, flushed and moved into place, so that its presence means a complete episode.
            RestepFiles.PublishResult(outFolder, ResultText(plan, outcome));

            stdout.WriteLine($"wrote {outFolder}: {recorder.FrameCount} frames of {recorder.Channels.Count} channels, {plan.Members.Count} nets");
            return 0;
        }

        /// <summary>The test-only <c>--no-watch</c>: the same episode stepped with no watcher, and its <c>result.json</c> alone written.</summary>
        /// <summary>
        /// <c>--patches</c> (D182): the episode stepped with <see cref="PatchWatcher"/> alone, and no frames. <c>patches.jsonl</c> holds one row a
        /// landed member and is written before <c>result.json</c>, so a folder with a result holds its patches. Refused in a tank with no leaf smell.
        /// </summary>
        private static int RestepSwimPatches(RestepPlan plan, string outFolder, TextWriter stdout)
        {
            var watcher = new PatchWatcher();
            RestepOutcome outcome = Restep.Swim(plan, watcher);
            if (!watcher.HasScent) throw new InvalidDataException("--patches reads the leaf smell, and this tank has none");
            var rows = plan.Members.Where(m => watcher.Tracks[m.Slot].StartedInScent.HasValue)
                .Select(m => PatchWatcher.Row(plan.Request, m, watcher.Tracks[m.Slot], watcher.PatchCount, watcher.Travel?[m.Slot], watcher.OpenedAt));
            File.WriteAllText(Path.Combine(outFolder, "patches.jsonl"), string.Join("\n", rows) + "\n");
            RestepFiles.PublishResult(outFolder, ResultText(plan, outcome));
            stdout.WriteLine($"wrote {outFolder}: {watcher.PatchCount} patches of {watcher.LeafCount} leaves, {plan.Members.Count} nets");
            return 0;
        }

        private static int RestepSwimUnwatched(RestepPlan plan, string outFolder, TextWriter stdout)
        {
            RestepOutcome outcome = Restep.Swim(plan, null);
            RestepFiles.PublishResult(outFolder, ResultText(plan, outcome));
            stdout.WriteLine($"wrote {outFolder}: no watcher, {plan.Members.Count} nets");
            return 0;
        }

        /// <summary>The text of <c>result.json</c>: the episode's seed, the control, and each slot's net as its round-trip text and its 64 bits.</summary>
        private static string ResultText(RestepPlan plan, RestepOutcome outcome)
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            var r = new Json.Writer(indent: true);
            r.BeginObject();
            r.Field("complete", true);
            r.Field("episodeSeed", plan.EpisodeSeed.ToString(inv));
            r.Field("control", plan.ControlLabel);
            r.BeginArray("members");
            foreach (RestepMember m in plan.Members)
            {
                RestepMemberOutcome x = outcome.Members[m.Slot];
                r.BeginObject();
                r.Field("slot", m.Slot);
                r.Field("pool", m.PoolIndex);
                r.Field("landed", x.Landed);
                if (x.Landed)
                {
                    r.Field("net", x.Net.ToString("R", inv));
                    r.Field("bits", BitConverter.DoubleToInt64Bits(x.Net).ToString("x16", inv));
                }
                else
                {
                    r.Raw("net", "null");
                    r.Raw("bits", "null");
                }
                r.Field("survived", x.Survived);
                r.EndObject();
            }
            r.EndArray();
            r.EndObject();
            return r.ToString().Replace("\r\n", "\n") + "\n";
        }
    }
}
