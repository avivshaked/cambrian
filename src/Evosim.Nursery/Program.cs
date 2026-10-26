using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Evosim.Core;
using Evosim.Farm;

namespace Evosim.Nursery
{
    /// <summary>
    /// The nursery (propose-nursery.md): brains evolved by direct selection, fast, on fixed bodies,
    /// scored in the ecology's own currency, and the run of one experiment written as a recipe file
    /// (<c>--recipe</c>, Recipe.cs; <c>--rerun</c> runs a registry entry's recipe again).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The tank</b> (Tank.cs) is a small world built from the recipe's config, its overrides
    /// applied and the founder floor closed, into which several bodies are inoculated at once, each
    /// given a founder's purse, with snow laid on the bed and carrion on it. It is stepped by the
    /// farm's own <see cref="Simulation"/> for the recipe's seconds, births held, and each body's
    /// episode is read by the recipe's reward (Reward.cs).
    /// </para>
    /// <para>
    /// <b>The search</b> evolves the population generation by generation, every genome swum in
    /// several tanks a generation on common seeds, the top few kept and the rest bred by tournament,
    /// by mutation and, under the recipe's crossover, by crossing two parents' brains (Crossover.cs).
    /// Only the brain evolves under <c>evolve: brain</c>: a child is a <see cref="Mutator.Mutate"/>
    /// of its parent with only the child's neurons grafted onto a copy of the parent's body, or the
    /// one brain mutated on it (D146), and with the envelope on the joint drive genes as well
    /// (Envelope.cs).
    /// </para>
    /// <para>
    /// <b>The exam</b> swims the search's best genomes under each of the recipe's controls (its own
    /// brain, frozen, full power, knockouts, brainless, hand-wired kinesis), on held-out seeds, and
    /// pairs each control against the genome's own. A recipe with no search runs the exam alone, on
    /// the bodies as drawn or as a pool file holds them. <c>--measure-dref</c> measures how far one
    /// ordinary mutation moves a brain, on a pool file's genomes, with no episode swum.
    /// </para>
    /// <para>
    /// A run writes a record beside its results (RunRecord.cs) and a line in the registry
    /// (Registry.cs), and nothing else is read from the command line: the experiment is the recipe.
    /// </para>
    /// </remarks>
    internal static partial class Program
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>
        /// The settings the tank, the search and the exam read. A recipe fills them
        /// (<see cref="ApplyRecipe"/>); the command line sets only the run record's and the
        /// mode's.
        /// </summary>
        private sealed class Options
        {
            public string Config, Out;
            public double Seconds = 900, CorpseJoules = 40, BodyAboveBed = 1.5;
            public int Population = 100, Generations = 50, Corpses = 5, Threads = 4, FinalEpisodes = 8;
            public ulong Seed = 1;
            public float Dt = 0.01f;

            // The tank (Tank.cs): several bodies a world, net energy, the founder's purse, snow on
            // the bed, generational with elites; and its bench of control brains.
            public bool Tank, TankBench;
            public int TankSize = 8, Draws = 3, Elites = 8, Tournament = 3, SnowLayers = 1, Founders;
            public bool PerBody;
            public string KeepBodies, Pool;
            // D178: a search seeded from a pool keeps the pool's own brains (Recipe.Brains "kept") in place of a fresh one each.
            public bool KeepBrains;
            public ulong ExamSalt = 0x62656E6368UL;
            // D172: the search's stop on a stalled curve (StopMeasure null swims to Generations; Recipe.StopSetting says the rule),
            // and the exam's pool (null: the last generation's best).
            public string StopMeasure, ExamPool;
            public int StopWindow, StopOver, StopFloor;
            public double StopGain = double.NaN;
            public float SnowIslandWavelength, SnowIslandCover = 0.25f;
            public double PurseSeconds = double.NaN, Snow;
            public bool SnowFixed;
            public string Evolve = "kind", Trunk, PurseRule = "grown";

            // Envelope.cs: under evolve brain, keep the mutant's joint drive genes too.
            public bool EvolveEnvelope;

            // LeafTank.cs: the recipe's leaves, a tank's count of them and the water's spent units; no pool, no leaves.
            public string LeafPool;
            public int LeafCount;
            public double LeafSpent;
            // D182: no patches (count 0), the leaves over the whole tank; else the patches they are laid in. Three plain fields, as the run record writes options.
            public int LeafPatchCount;
            public double LeafPatchRadius, LeafPatchRing;

            // propose-nursery-crossover.md: two parents a child, one route of the donor's passed
            // into the base, and mutation's chance falling with the brain distance it moved.
            public double Crossover, BaseShare = double.NaN, MutationByDistance = double.NaN;

            // --measure-dref: the yardstick d_ref, the brain distance one ordinary mutation moves a
            // brain, measured on an exam's genomes with no episode swum.
            public string MeasureDref;
            public int DrefSamples = 200;

            // nursery-general-build.md, section 2: the experiment as a recipe file.
            public string Recipe;

            // --check: read the recipe and the world config, print every resolved setting and the
            // episode config's hash, and stop: no folder, no file, no episode.
            public bool Check;

            // --rerun <entry>: the registry's entry (its line number, or a unique prefix of its
            // recipe's hash) run again into --out on this build, and compared (Registry.cs).
            public string Rerun;
        }

        /// <summary>A world an episode is swum in: its config, its length and its carrion.</summary>
        private sealed class Arena
        {
            public string Name, Text;
            public RunConfig Config;
            public double Seconds, CorpseJoules;
            public int Corpses;
        }

        private sealed class Body
        {
            public int Index;
            public long SourceId;
            public Genome Ancestor;
            public int Parts, Joints;
        }

        private static Options _o;
        private static RunRecord _record;

        /// <summary>The experiment: the recipe the run was given, or the entry's recipe under --rerun.</summary>
        private static Recipe _recipe;

        /// <summary>The salt of the search's episode seeds (<see cref="SeedsFor"/>); the exam's is the recipe's.</summary>
        private const ulong GenerationSalt = EpisodeSeeds.GenerationSalt;
        private static RunConfig _config;
        private static string _configText;
        private static Arena _search;

        /// <summary>
        /// A world's own copy of the config, the recipe's overrides applied and the floor closed. The
        /// current is an object on the config (<c>RunConfig.Current</c>), pinned and unpinned by its
        /// world every step, so two worlds stepping on two threads must never share one: every
        /// episode reads its own.
        /// </summary>
        private static RunConfig EpisodeConfig(string text) => _recipe.EpisodeConfig(text);

        private static int Main(string[] args)
        {
            // --restep (Restep.cs) steps one episode of a finished run again and writes its frames; it takes no recipe or run of its own.
            if (args.Length > 0 && args[0] == "--restep") return Restep.Cli(args);
            // --graft-nose (NoseGraftCli.cs) writes D235's graft film's config and pool; it swims nothing.
            if (args.Length > 0 && args[0] == "--graft-nose") return NoseGraftCli.Cli(args);

            try
            {
                _o = Parse(args);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                return 2;
            }

            if (_o.Rerun != null)
            {
                string refusal;
                try { refusal = FindRerun(); }
                catch (Exception e) when (e is InvalidDataException || e is IOException || e is UnauthorizedAccessException)
                {
                    refusal = "--rerun: " + e.Message;
                }
                if (refusal != null)
                {
                    Console.Error.WriteLine(refusal);
                    return 2;
                }
            }
            try
            {
                _recipe = Recipe.Load(_o.Recipe);
                if (_rerun != null && _recipe.Hash() != _rerun.RecipeHash)
                    throw new InvalidDataException($"the entry's recipe {_rerun.RecipePath} now hashes to {_recipe.Hash()}, not {_rerun.RecipeHash}: it was edited after its run, which rounds/README.md forbids");
            }
            catch (Exception e) when (e is InvalidDataException || e is IOException || e is ArgumentException || e is FormatException
                || e is InvalidOperationException || e is OverflowException || e is UnauthorizedAccessException)
            {
                Console.Error.WriteLine(e.Message);
                return 2;
            }
            ApplyRecipe();
            _reward = RewardRegistry.Create(_recipe.RewardClass, _recipe.RewardParameters);
            if (_o.MeasureDref != null && _o.Evolve != "brain")
            {
                Console.Error.WriteLine("--measure-dref measures the brain's mutation: the recipe's search.evolve must be \"brain\"" +
                    (_recipe.Search == null ? ", and this recipe has no search" : $", and this recipe's is \"{_o.Evolve}\""));
                return 2;
            }
            if (_o.Check) return CheckOnly();

            // A recipe's run digests every file in its folder as a result, so the folder must hold
            // only what this run writes.
            // --measure-dref is neither digested nor registered, so its folder may hold a launcher's log.
            if (_o.MeasureDref == null && Directory.Exists(_o.Out) && Directory.EnumerateFileSystemEntries(_o.Out).Any())
            {
                Console.Error.WriteLine($"--out {_o.Out} is not empty: a recipe's run is given a new folder, since every file in it is digested as a result");
                return 2;
            }
            Directory.CreateDirectory(_o.Out);
            _configText = File.ReadAllText(_o.Config);
            _config = RunConfigJson.Read(_configText, out string mismatch);
            _record = new RunRecord
            {
                Args = (string[])args.Clone(), ConfigPath = Path.GetFullPath(_o.Config),
                ConfigFileSha256 = RunRecord.FileSha256(_o.Config), ConfigHash = _config.Hash(),
            };
            if (mismatch != null) Console.WriteLine($"note: the config's recorded hash differs ({mismatch})");

            // The config's own purse, as the log line below says it (the episode config's may differ
            // by an override): the tank's purse is the grown body's own, set in TankEpisode.

            // Checked on the config the episodes swim in, a recipe's overrides applied.
            _config = EpisodeConfig(_configText);
            if (_config.FoundingTricklePerSecond > 0f || _config.FoundingTricklePoolCount > 0)
            {
                Console.Error.WriteLine("the episode config runs a founding trickle; make it with EVOSIM_TRICKLE 0 and no pool");
                return 2;
            }
            if (_o.EvolveEnvelope && !(_config.Mutation.DriveGeneChance > 0f))
            {
                Console.Error.WriteLine("the recipe's search.evolveEnvelope needs the config's driveGeneChance above 0: at 0 the mutation never steps a drive gene");
                return 2;
            }
            _search = new Arena
            {
                Name = "search", Text = _configText, Config = EpisodeConfig(_configText), Seconds = _o.Seconds,
                Corpses = _o.Corpses, CorpseJoules = _o.CorpseJoules,
            };
            if (_search.Config.FoundingTricklePerSecond > 0f || _search.Config.FoundingTricklePoolCount > 0)
            {
                Console.Error.WriteLine($"the {_search.Name} config runs a founding trickle; make it with EVOSIM_TRICKLE 0 and no pool");
                return 2;
            }
            Say(FormattableString.Invariant($"arena {_search.Name}: config hash {_search.Config.Hash()}, tank {_search.Config.WorldAreaSquareMetres} m2 x {_search.Config.WorldDepthMetres} m, {_search.Seconds:0} s, {_search.Corpses} corpses of {_search.CorpseJoules:0.#} J"));
            Say($"nursery: config {_o.Config} (hash {_config.Hash()} after the overrides, founder energy {_config.FounderEnergyJoules} J), tank {_config.WorldAreaSquareMetres} m2 x {_config.WorldDepthMetres} m, " +
                $"push {_config.Fluid.LimbPushCoefficient}, scent {_config.SenseScent}, drive sense {_config.SenseDrive}, work cost {_config.WorkCostMultiplier}, brain {_config.Brain.BrainModel}");

            // The run record (nursery-general-build.md, section 1), before the first episode, and
            // again when the run ends however it ends but killed.
            try { WriteRecord(); }
            catch (InvalidDataException e)
            {
                // The one failure of the build's half that stops a run: the list of appended files is missing or unreadable.
                Console.Error.WriteLine(e.Message);
                return 2;
            }
            int code;
            try
            {
                code = Run();
            }
            catch (Exception e)
            {
                _record.Fail(e);
                CloseRecord();
                Register();
                throw;
            }
            if (_stopped)
            {
                // A run that found the stop file ended cleanly, on a generation or an exam brain: no ending code
                // of its own, and no registry entry, since an experiment is a run to its end.
                _record.Stop();
                CloseRecord();
                return 0;
            }
            _record.End(code);
            CloseRecord();
            Register();
            return code;
        }

        /// <summary>Set when the run found the stop file (<see cref="StopNow"/>).</summary>
        private static bool _stopped;

        /// <summary>
        /// Whether the run's folder holds the stop file (<see cref="RunRecord.StopFileName"/>). The first time it does, one
        /// line says where the run stopped. The check reads a directory entry and touches no seed, draw or result.
        /// </summary>
        private static bool StopNow(string where)
        {
            if (!RunRecord.StopRequested(_o.Out)) return false;
            if (!_stopped)
            {
                _stopped = true;
                Say($"stopped: {RunRecord.StopFileName} found {where}; nothing further is swum, run.json says stopped, and the run is not entered in the registry");
            }
            return true;
        }

        /// <summary>The registry entry --rerun names, read before anything else; null under --recipe.</summary>
        private static RegistryEntry _rerun;
        private static int _rerunNumber;

        /// <summary>The one registry, the main checkout's (<see cref="RunRecord.MainCheckoutOf"/>).</summary>
        private static string _registry;

        /// <summary>
        /// Finds the entry --rerun names, and refuses one that cannot be rerun here: an entry that is
        /// not in the registry, or a build whose tree hashes are not the entry's. Null when the rerun
        /// may go ahead, with the recipe set to the entry's.
        /// </summary>
        private static string FindRerun()
        {
            string root = Recipe.RepoRootAbove(Directory.GetCurrentDirectory()) ?? Recipe.RepoRootAbove(AppContext.BaseDirectory);
            if (root == null) return "--rerun: no repository root above the working directory or the program";
            string registry = RegistryEntry.FileUnder(RunRecord.MainCheckoutOf(root));
            List<RegistryEntry> all = RegistryEntry.ReadAll(registry);
            int number;
            // An entry's number is short; a recipe hash's prefix is 8 hex digits or more.
            if (_o.Rerun.Length < 8)
            {
                if (!int.TryParse(_o.Rerun, NumberStyles.None, Inv, out number) || number < 1 || number > all.Count)
                    return $"--rerun: {registry} has {all.Count} entries, and no entry {_o.Rerun} (name an entry by its number, or by 8 or more digits of its recipe's hash)";
            }
            else
            {
                int[] matches = Enumerable.Range(0, all.Count).Where(i => all[i].Reference == null && all[i].RecipeHash != null
                    && all[i].RecipeHash.StartsWith(_o.Rerun, StringComparison.OrdinalIgnoreCase) && all[i].RerunOf == 0 && all[i].Rerunnable).ToArray();
                if (matches.Length != 1) return $"--rerun: {matches.Length} rerunnable first runs have a recipe hash starting {_o.Rerun}; name the entry by its number";
                number = matches[0] + 1;
            }
            RegistryEntry e = all[number - 1];
            if (e.Reference != null)
                return $"--rerun: entry {number} is a reference entry, a run made before the framework on {e.Reference.Build}. " +
                    $"Its tie to {e.RecipePath} is a check, not a rerun: {e.Reference.Verdict}. " +
                    $"Its digests cannot match a rerun's because: {string.Join("; ", e.Reference.KnownDifferences ?? new string[0])}. Run the recipe itself instead.";
            if (!e.Rerunnable)
                return $"--rerun: entry {number} is not rerunnable: {e.NotRerunnableBecause}";
            if (e.Out == null || e.RecipePath == null)
                return $"--rerun: entry {number} names no output folder or no recipe";
            if (string.Equals(Path.GetFullPath(_o.Out), Path.GetFullPath(e.Out), StringComparison.OrdinalIgnoreCase))
                return $"--rerun: --out is the entry's own folder, {e.Out}; a rerun writes to a new one";
            var here = new RunRecord();
            here.ReadBuild(root);
            IReadOnlyList<string> differ = e.BuildDifferences(here);
            if (differ.Count > 0)
                return $"--rerun: entry {number} ran on commit {e.GitCommit}, and this build's {string.Join(", ", differ)} tree hashes differ from it. A rerun is bit for bit on the entry's build only: build that commit in a worktree under scratch/ and rerun there.";
            _rerun = e;
            _rerunNumber = number;
            _registry = registry;
            _o.Recipe = Path.Combine(root, e.RecipePath.Replace('/', Path.DirectorySeparatorChar));
            return null;
        }

        /// <summary>
        /// The run's line in the registry, for every run of a recipe under rounds/nursery/ however it
        /// ended but stopped by a STOP file; a dirty tree's run is marked not rerunnable. A rerun also says whether it came out
        /// identical. A --measure-dref run swims no episode and is no run of the experiment, so it
        /// is not entered: its line would make a recipe's hash prefix name two entries.
        /// </summary>
        private static void Register()
        {
            if (_o.MeasureDref != null) return;
            // Whatever goes wrong here leaves the run's ending as it was: a finished run is not
            // turned into a crash by its bookkeeping.
            try { RegisterOrThrow(); }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                string line = "not entered in the registry: " + e.GetType().Name + ": " + e.Message;
                try { Say(line); }
                catch (Exception) { Console.Error.WriteLine(line); }   // the log's own disk may be what failed
            }
        }

        private static void RegisterOrThrow()
        {
            string root = _record.RepoRoot;
            if (root == null)
            {
                Say("not entered in the registry: the run record found no repository root");
                return;
            }
            string relative = Path.GetRelativePath(root, _record.RecipePath).Replace(Path.DirectorySeparatorChar, '/');
            if (!relative.StartsWith("rounds/nursery/", StringComparison.OrdinalIgnoreCase))
            {
                Say("not entered in the registry: its recipe is not under rounds/nursery/, so the run is a trial");
                return;
            }
            var entry = new RegistryEntry
            {
                Recipe = _recipe.Name, RecipeHash = _record.RecipeHash, RecipePath = relative, ConfigHash = _recipe.ConfigHash,
                GitCommit = _record.GitCommit, CoreHash = _record.CoreHash, DynamicsHash = _record.DynamicsHash,
                FarmHash = _record.FarmHash, NurseryHash = _record.NurseryHash,
                Out = Path.GetFullPath(_o.Out), Status = _record.Status, StartedAtUtc = _record.StartedAtUtc, EndedAtUtc = _record.EndedAtUtc,
                Digests = RegistryEntry.DigestsOf(_o.Out),
                Rerunnable = _record.GitDirty == false,
                NotRerunnableBecause = _record.GitDirty == false ? null
                    : _record.GitDirty == true ? "the tree it ran from was dirty, so its commit does not hold what ran"
                    : "git could not say whether the tree was clean",
            };
            if (_rerun != null)
            {
                string first = _rerun.FirstDifference(_o.Out, entry.Digests);
                entry.RerunOf = _rerunNumber;
                entry.Identical = first == null;
                Say(first == null ? $"rerun of entry {_rerunNumber}: identical" : $"rerun of entry {_rerunNumber}: differs. First: {first}");
            }
            string registry = _registry ?? RegistryEntry.FileUnder(RunRecord.MainCheckoutOf(root));
            int n;
            try { n = RegistryEntry.Append(registry, entry); }
            catch (IOException)
            {
                // Held open elsewhere past the retries: the line is kept beside the run (.txt is not a
                // result), to be appended by hand, so that no run of an experiment goes unentered.
                string pending = Path.Combine(_o.Out, "registry-pending.txt");
                File.WriteAllText(pending, entry.ToJson() + "\n");
                Say($"not entered in the registry: {registry} stayed locked. Its line is in {pending}; append it to the registry when the file is free");
                return;
            }
            Say($"entered in the registry as entry {n} ({registry})" + (entry.Rerunnable ? "" : "; not rerunnable: " + entry.NotRerunnableBecause));
        }

        /// <summary>
        /// run.json again, now holding the exam's plan (<see cref="RunRecord.Exam"/>), before the exam's first episode. It warns rather than
        /// stop the exam: run.json is not a result, and a screen reads the exam's progress from tank-exam.jsonl's rows.
        /// </summary>
        private static void WriteExamPlan()
        {
            string failure = _record.TryWrite(_o.Out);
            if (failure != null) Say("run record: the exam's plan could not be written to run.json (" + failure + "); the exam goes on");
        }

        /// <summary>The closing write, which warns rather than turn a run's ending into a crash.</summary>
        private static void CloseRecord()
        {
            string failure = _record.TryWrite(_o.Out);
            if (failure != null) Say("run record: the closing write failed (" + failure + "); run.json still says running");
        }

        /// <summary>
        /// The recipe's settings into the options the tank reads (the options are the one place the
        /// tank, the search and the exam read their settings from).
        /// </summary>
        private static void ApplyRecipe()
        {
            Recipe r = _recipe;
            _o.Config = r.ConfigFullPath;
            _o.Seed = r.Seed;
            _o.Founders = r.BodySource == "founders" ? r.Founders : 0;
            _o.Trunk = r.Trunk;
            _o.Pool = r.PoolFullPath;
            _o.KeepBodies = r.Keep == null ? null : string.Join(",", r.Keep.Select(k => k.ToString(Inv)));
            _o.KeepBrains = r.Brains == "kept";

            _o.Seconds = r.Seconds;
            _o.Dt = r.Dt;
            _o.TankSize = r.TankSize;
            _o.Snow = r.Snow?.Density ?? 0d;
            _o.SnowFixed = r.Snow?.Fixed ?? false;
            _o.SnowLayers = r.Snow != null && r.Snow.Layers > 0 ? r.Snow.Layers : 1;
            _o.SnowIslandWavelength = r.Snow?.IslandWavelength ?? 0f;
            if (r.Snow != null && r.Snow.Layers == 0) _o.SnowIslandCover = r.Snow.IslandCover;
            _o.Corpses = r.CarrionCount;
            _o.CorpseJoules = r.CarrionJoules;
            _o.BodyAboveBed = r.AboveBed;
            _o.PurseRule = r.PurseRule;
            _o.PurseSeconds = r.PurseSeconds;

            _o.LeafPool = r.Leaves?.PoolFullPath;
            _o.LeafCount = r.Leaves?.Count ?? 0;
            _o.LeafSpent = r.Leaves?.SpentUnitsPerCubicMetre ?? 0d;
            _o.LeafPatchCount = r.Leaves?.Patches?.Count ?? 0;
            _o.LeafPatchRadius = r.Leaves?.Patches?.RadiusMetres ?? 0d;
            _o.LeafPatchRing = r.Leaves?.Patches?.RingMetres ?? 0d;
            _leafPool = r.Leaves == null ? null : LeafPool.Read(r.Leaves.PoolFullPath);

            _o.FinalEpisodes = r.ExamEpisodes;
            _o.ExamSalt = r.ExamSalt;
            _o.ExamPool = r.ExamPool;
            if (r.Search?.Stop is Recipe.StopSetting st)
            {
                _o.StopMeasure = st.Measure;
                _o.StopWindow = st.Window;
                _o.StopGain = st.Gain;
                _o.StopOver = st.Over;
                _o.StopFloor = st.Floor;
            }

            Recipe.SearchSetting s = r.Search;
            if (s == null)
            {
                // No search: the exam on the bodies as they are, which is the old bench.
                _o.TankBench = true;
                _o.Population = int.MaxValue;
                return;
            }
            _o.Tank = true;
            _o.Population = s.Population;
            _o.Draws = s.Draws;
            _o.Generations = s.Generations;
            _o.Elites = s.Elites;
            _o.Tournament = s.Tournament;
            _o.PerBody = s.PerBody;
            _o.Evolve = s.Evolve;
            _o.EvolveEnvelope = s.EvolveEnvelope;
            _o.Crossover = s.Crossover?.Rate ?? 0d;
            _o.BaseShare = s.Crossover?.BaseShare ?? 0.7;
            _o.MutationByDistance = s.Crossover?.MutationByDistance ?? double.NaN;
        }

        /// <summary>
        /// --check: every setting as the run would resolve it, the exam's brains, and the hash of the
        /// config an episode is built from, printed one to a line. Swims nothing and writes nothing.
        /// </summary>
        private static int CheckOnly()
        {
            _configText = File.ReadAllText(_o.Config);
            RunConfig raw = RunConfigJson.Read(_configText, out _);
            RunConfig episode = EpisodeConfig(_configText);
            Console.WriteLine("config hash " + raw.Hash());
            Console.WriteLine("episode config hash " + episode.Hash());
            Console.WriteLine("recipe hash " + _recipe.Hash());
            foreach (KeyValuePair<string, string> kv in RunRecord.FieldsOf(_o)) Console.WriteLine("option " + kv.Key + " = " + kv.Value);
            // D240: the step, the pace factor beside it and the steps a metabolic step holds, so that a batch says which pace it ran at.
            // The recipe's read has refused a dt that cannot make a metabolic step at this factor.
            EnvBinding.ResolvePhysicsStep(_o.Dt, episode.PaceFactor, out int physicsSteps);
            Console.WriteLine(FormattableString.Invariant($"physics dt {_o.Dt:R} s, pace factor {episode.PaceFactor:R}, {physicsSteps} physics steps a metabolic step"));
            foreach (Recipe.ExamControl c in _recipe.Controls) Console.WriteLine("exam brain " + c.Label);
            // A leaf tank is laid for every seed the run will swim (LeafTank.cs), since a tank too full for its leaves stops the run.
            // And the feeder refusal a run with leaves makes at its start, asked of every line of the pool (LeafTank.cs).
            // The body a genome grows is the episode config's, as the run's own refusal develops it with (the run sets _config so before it).
            int bad = 0;
            _config = episode;
            if (_leafPool != null && _o.Pool != null) bad += CheckFeeders();
            if (_leafPool != null) bad += CheckLeafPlacement();
            return bad > 0 ? 1 : 0;
        }

        /// <summary>The run record's opening half: every option as resolved, the episode config, the seeds, the build.</summary>
        private static void WriteRecord()
        {
            // The list of appended files is read first: a tree that has none refuses the run (InvalidDataException) before any file of the
            // run, recipe.json included, is written. With no repository root there is no list to read, and the build's half skips its dirty check.
            string listRoot = Environment.GetEnvironmentVariable("EVOSIM_REPO_ROOT");
            if (string.IsNullOrEmpty(listRoot)) listRoot = RunRecord.FindRepoRoot(Directory.GetCurrentDirectory()) ?? RunRecord.FindRepoRoot(AppContext.BaseDirectory);
            if (!string.IsNullOrEmpty(listRoot)) RunRecord.AppendedPaths(listRoot);

            // The resolved recipe beside the run, and its identity in the record.
            string text = _recipe.Canonical();
            File.WriteAllText(Path.Combine(_o.Out, "recipe.json"), text, new System.Text.UTF8Encoding(false));
            _record.RecipePath = Path.GetFullPath(_o.Recipe);
            _record.RecipeName = _recipe.Name;
            _record.RecipeHash = _recipe.Hash();
            _record.RecipeJson = text;
            // The resolved recipe is the record of the settings. The options would show their
            // defaults for what the recipe sets elsewhere, and a reader would take them for the run's.
            _record.Options = null;
            _record.EpisodeConfigHash = _config.Hash();
            _record.EpisodeConfigJson = RunConfigJson.Write(_config);
            _record.Seed = _o.Seed;
            _record.Salts = new List<KeyValuePair<string, ulong>>
            {
                new KeyValuePair<string, ulong>("generations", GenerationSalt),
                new KeyValuePair<string, ulong>("exam", _o.ExamSalt),
            };
            _record.Threads = _o.Threads;
            // D240: the step, the pace factor and the steps a metabolic step holds, beside the episode config that carries the factor too.
            _record.PhysicsDt = _o.Dt;
            _record.PaceFactor = _config.PaceFactor;
            EnvBinding.ResolvePhysicsStep(_o.Dt, _config.PaceFactor, out int physicsSteps);
            _record.StepsPerMetabolicStep = physicsSteps;
            _record.ReadBuild(Environment.GetEnvironmentVariable("EVOSIM_REPO_ROOT"));
            _record.Write(_o.Out);
            if (_record.Note != null) Say("run record: " + _record.Note);
        }

        /// <summary>Everything after the record: the mode the recipe chose.</summary>
        private static int Run()
        {
            if (_o.MeasureDref != null) return MeasureDref();
            if (_leafPool != null)
                Say(FormattableString.Invariant($"  leaves: {_o.LeafCount} a tank, drawn from the {_leafPool.Leaves.Count} of {_o.LeafPool}; the water starts with {_o.LeafSpent} spent units/m3"));
            if (LeafPatchesSet != null)
                Say(FormattableString.Invariant($"  in {_o.LeafPatchCount} patches of {_o.LeafPatchRadius} m radius on a ring of {_o.LeafPatchRing} m, the ring turned each episode (D182)"));
            if (LeafSwitchingSet is Recipe.SwitchingSetting sw)
                Say(FormattableString.Invariant($"  the patches switch (D236): every {sw.PeriodSeconds} s, a fed disc regrows {sw.RegrowAfterSeconds} s after a leaf leaves the living, withers over {sw.WitherOverSeconds} s, and the other disc is fed anew at least {sw.MinimumTurnDegrees} degrees round the ring"));

            // A pool body source with no search: the genomes an exam wrote (tank-*-genomes.tsv, the
            // body's index, a tab, the genome), benched as they are, in place of drawing founders.
            // A pool with a search: the search starts from those genomes' bodies, and the recipe's keep
            // picks among them. A genome with one brain (D146) gets a fresh brain (EvolveTank's
            // FreshBrain), so its exam's brain is not carried on, unless the recipe keeps the pool's
            // brains (D178); a per-node brain is kept as it is.
            if (_o.Pool != null && _o.Tank && !_o.TankBench)
            {
                var seeded = File.ReadAllLines(_o.Pool).Where(l => l.Length > 0).Select(l =>
                {
                    int tab = l.IndexOf('\t');
                    return new Body { Index = int.Parse(l.Substring(0, tab), Inv), Ancestor = GrowthStart.Give(GenomeJson.Read(l.Substring(tab + 1)), _config) };
                }).ToList();
                if (_o.KeepBodies != null)
                {
                    var keep = new HashSet<int>(_o.KeepBodies.Split(',').Select(x => int.Parse(x, Inv)));
                    seeded = seeded.Where(b => keep.Contains(b.Index)).ToList();
                }
                Say($"  search seeded from {_o.Pool}: bodies {string.Join(", ", seeded.Select(b => b.Index))}{(_o.KeepBrains ? ", each genome's own brain kept (D178)" : "")}");
                RefuseFeedersBesideLeaves(seeded.Select(b => (b.Index, b.Ancestor)));
                return EvolveTank(seeded);
            }
            if (_o.Pool != null && _o.TankBench)
            {
                var pool = File.ReadAllLines(_o.Pool).Where(l => l.Length > 0).Select(l =>
                {
                    int tab = l.IndexOf('\t');
                    return (GrowthStart.Give(GenomeJson.Read(l.Substring(tab + 1)), _config), int.Parse(l.Substring(0, tab), Inv));
                }).ToList();
                if (_o.KeepBodies != null)
                {
                    var keep = new HashSet<int>(_o.KeepBodies.Split(',').Select(x => int.Parse(x, Inv)));
                    pool = pool.Where(p => keep.Contains(p.Item2)).ToList();
                }
                Say($"  pool {_o.Pool}: {pool.Count} genomes, bodies {string.Join(", ", pool.Select(p => p.Item2))}");
                RefuseFeedersBesideLeaves(pool.Select(p => (p.Item2, p.Item1)));
                return TankBench(pool, "bench");
            }

            List<Body> bodies = DrawFounders();
            // The recipe's keep: only the listed bodies, by their index in the draw, which they keep.
            if (_o.KeepBodies != null)
            {
                var keep = new HashSet<int>(_o.KeepBodies.Split(',').Select(x => int.Parse(x, Inv)));
                bodies = bodies.Where(b => keep.Contains(b.Index)).ToList();
                Say("  kept bodies " + string.Join(", ", bodies.Select(b => b.Index)));
            }
            if (bodies.Count == 0)
            {
                Console.Error.WriteLine("no founder in the draw has a joint");
                return 1;
            }
            RefuseFeedersBesideLeaves(bodies.Select(b => (b.Index, b.Ancestor)));

            return _o.TankBench ? TankBench(bodies) : EvolveTank(bodies);
        }

        // ------------------------------------------------------------------ the bodies

        /// <summary>
        /// The recipe's founders: bodies drawn as the world draws its founders
        /// (<c>GenomeFactory.Founder</c> with the config's genome options, sensor pool and cell
        /// types), one seed each, keeping those with a joint (and the recipe's trunk), until there
        /// are as many as the recipe asks.
        /// </summary>
        /// <remarks>
        /// With the drive sense on (<c>RunConfig.SenseDrive</c>) the founders are drawn from the pool
        /// without its three channels, which is the pool with it off element for element, so a sense
        /// arm starts from exactly the control's founders and its generation 0 swims as the control's
        /// did; the sense enters the population by mutation only.
        /// </remarks>
        private static List<Body> DrawFounders()
        {
            var picked = new List<Body>();
            SensorChannel[] founderPool = _config.SensorPool()
                .Where(c => c != SensorChannel.Stroke && c != SensorChannel.Hold && c != SensorChannel.Ramp).ToArray();
            if (_config.SenseDrive) Say($"founders drawn without the drive sense: {founderPool.Length} of {_config.SensorPool().Length} channels");
            // D146: the pool above draws the old model's neurons, which the one brain drops; its
            // bodies are the control's, and its brain is wired from every channel of the pool.
            if (Central) Say($"  the one brain: bodies as drawn, every brain wired from all {_config.SensorPool().Length} channels");
            for (ulong k = 0; picked.Count < _o.Founders && k < (ulong)(_o.Founders * 200); k++)
            {
                var rng = new Rng(Rng.SeedFor(_o.Seed, 0x464F554EUL + k));
                Genome genome = GenomeFactory.Founder(rng, _config.Genome, founderPool, _config.CellTypes);
                Phenotype adult;
                try { adult = Developer.Develop(genome, _config.Development, null, _config.Shapes, cellTypes: _config.CellTypes); }
                catch (Exception) { continue; }
                int joints = 0;
                foreach (PhenotypePart part in adult.Parts) if (part.JointType != JointType.Fixed) joints++;
                if (joints == 0) continue;
                if (_o.Trunk != null && adult.Parts[0].CellTypeId != _o.Trunk) continue;
                // D146: under the one brain the founder keeps its body and drops its neurons, and
                // its ancestor brain is wired fresh to that body (every member of the search draws
                // its own, EvolveTank).
                if (Central) genome = FreshBrain(genome, Rng.SeedFor(Rng.SeedFor(_o.Seed, 0x42524E00UL), k));
                picked.Add(new Body { Index = picked.Count, SourceId = (long)k, Ancestor = GrowthStart.Give(genome, _config), Parts = adult.PartCount, Joints = joints });
            }
            foreach (Body b in picked)
                Say(FormattableString.Invariant($"  body {b.Index}: founder draw {b.SourceId}, {b.Parts} parts, {b.Joints} joints"));
            return picked;
        }

        // ------------------------------------------------------------------ the brain

        /// <summary>
        /// A copy of the parent's body carrying the child's neurons, and under the recipe's evolveEnvelope
        /// the child's joint drive genes as well (Envelope.cs).
        /// </summary>
        private static Genome Graft(Genome parent, Genome child)
        {
            Genome g = parent.Clone();
            for (int i = 0; i < g.Nodes.Count; i++)
            {
                NeuronDef[] from = child.Nodes[i].Neurons;
                var copy = new NeuronDef[from.Length];
                for (int k = 0; k < from.Length; k++) copy[k] = from[k].Clone();
                g.Nodes[i].Neurons = copy;
            }
            if (_o.EvolveEnvelope) Envelope.Take(g, child);
            return g;
        }

        /// <summary>D146: the run's creatures carry the one brain (the config's <c>brainModel</c>).</summary>
        private static bool Central => _config.Brain.BrainModel == BrainModel.Central;

        /// <summary>The body a genome develops into under the run's limits and shapes.</summary>
        private static Phenotype BodyOf(Genome g) =>
            Developer.Develop(g, _config.Development, null, _config.Shapes, cellTypes: _config.CellTypes);

        /// <summary>
        /// D146: <paramref name="ancestor"/>'s body with a one brain wired fresh to it from
        /// <paramref name="seed"/>: neurons dropped, edges numbered, every output's bias and links
        /// drawn (<see cref="CentralWiring.Effective"/> with <c>founder</c> set), from every port of
        /// the run's sensor pool.
        /// </summary>
        private static Genome FreshBrain(Genome ancestor, ulong seed)
        {
            Genome g = ancestor.Clone();
            CentralWiring.Convert(g);
            g.Brain = CentralWiring.Effective(g.Brain, BodyOf(g), new Rng(seed), _config.SensorPool(), _config.Brain, founder: true);
            return g;
        }

        /// <summary>
        /// D146's child: the parent's body as it is, and its one brain mutated on that body
        /// (<see cref="CentralMutation"/>). Under evolveEnvelope the joint drive genes of a
        /// <see cref="Mutator"/> mutant of the same node count are taken as well (Envelope.cs).
        /// </summary>
        private static Genome ChildCentral(Genome parent, Rng rng)
        {
            Genome g = parent.Clone();
            if (_o.EvolveEnvelope)
            {
                bool took = false;
                for (int attempt = 0; attempt < 20 && !took; attempt++)
                {
                    Genome mutant = Mutator.Mutate(
                        parent, new Rng(rng.NextUInt() | ((ulong)rng.NextUInt() << 32)), _config.Mutation,
                        _config.CellTypes, _config.Genome, _config.SensorPool(),
                        buoyancyOffsetPriced: _config.BuoyancyOffsetWattsPerCubicMetre > 0f,
                        shapes: _config.Shapes,
                        growthReserveMin: _config.GrowthReserveMinSeconds,
                        growthReserveMax: _config.GrowthReserveMaxSeconds);
                    if (mutant.Nodes.Count != parent.Nodes.Count) continue;
                    Envelope.Take(g, mutant);
                    took = true;
                }
                if (!took) return null;
            }
            var brainRng = new Rng(rng.NextUInt() | ((ulong)rng.NextUInt() << 32));
            CentralMutation.Mutate(g.Brain, BodyOf(g), _config.SensorPool(), brainRng, _config.Brain);
            return g;
        }

        private static Genome Child(Genome parent, Rng rng)
        {
            if (parent.Brain != null) return ChildCentral(parent, rng);
            SensorChannel[] pool = _config.SensorPool();
            for (int attempt = 0; attempt < 20; attempt++)
            {
                Genome mutant = Mutator.Mutate(
                    parent, new Rng(rng.NextUInt() | ((ulong)rng.NextUInt() << 32)), _config.Mutation,
                    _config.CellTypes, _config.Genome, pool,
                    buoyancyOffsetPriced: _config.BuoyancyOffsetWattsPerCubicMetre > 0f,
                    shapes: _config.Shapes,
                    growthReserveMin: _config.GrowthReserveMinSeconds,
                    growthReserveMax: _config.GrowthReserveMaxSeconds);
                if (mutant.Nodes.Count != parent.Nodes.Count) continue;
                return Graft(parent, mutant);
            }
            return null;
        }

        /// <summary>
        /// --measure-dref (propose-nursery-crossover.md, "The mutation"): every genome of an exam's
        /// pool (tank-*-genomes.tsv) mutated --dref-samples times (200 unless given) by today's path (<see cref="Child"/>,
        /// which keeps the body), and the nursery's brain distance between each child and its
        /// parent. Prints the median (the yardstick d_ref), the quartiles and the share of children
        /// at d = 0, and writes every distance to dref-distances.tsv. No episode is swum. It runs
        /// under a recipe (<c>--recipe r.json --measure-dref genomes.tsv --out dir</c>), which gives
        /// the config and the seed, and its search must evolve the brain.
        /// </summary>
        private static int MeasureDref()
        {
            var pool = File.ReadAllLines(_o.MeasureDref).Where(l => l.Length > 0).Select(l =>
            {
                int tab = l.IndexOf('\t');
                return (body: int.Parse(l.Substring(0, tab), Inv), genome: GrowthStart.Give(GenomeJson.Read(l.Substring(tab + 1)), _config));
            }).ToList();
            var rng = new Rng(Rng.SeedFor(_o.Seed, 0x6472656655UL));
            var all = new List<double>();
            int failed = 0;
            Say($"measure d_ref: {pool.Count} genomes from {_o.MeasureDref}, {_o.DrefSamples} mutations each, seed {_o.Seed}");
            using (var w = new StreamWriter(Path.Combine(_o.Out, "dref-distances.tsv")))
            {
                w.WriteLine("genome\tbody\tsample\td");
                for (int i = 0; i < pool.Count; i++)
                {
                    var mine = new List<double>();
                    for (int s = 0; s < _o.DrefSamples; s++)
                    {
                        Genome child = Child(pool[i].genome, rng);
                        if (child == null) { failed++; continue; }
                        double d = Crossover.BrainDistance(child, pool[i].genome);
                        mine.Add(d);
                        w.WriteLine(FormattableString.Invariant($"{i}\t{pool[i].body}\t{s}\t{d:R}"));
                    }
                    all.AddRange(mine);
                    Say(FormattableString.Invariant(
                        $"  genome {i} (body {pool[i].body}, {pool[i].genome.Nodes.Sum(nd => nd.Neurons.Length)} neurons): median {Quantile(mine, 0.5):0.000}, quartiles {Quantile(mine, 0.25):0.000} to {Quantile(mine, 0.75):0.000}, d = 0 in {(mine.Count == 0 ? double.NaN : mine.Count(x => x == 0d) / (double)mine.Count):P0}"));
                }
            }
            Say(FormattableString.Invariant(
                $"d_ref: median {Quantile(all, 0.5):0.0000}, quartiles {Quantile(all, 0.25):0.0000} to {Quantile(all, 0.75):0.0000}, mean {(all.Count == 0 ? double.NaN : all.Average()):0.0000}, max {(all.Count == 0 ? double.NaN : all.Max()):0.0000}, d = 0 in {(all.Count == 0 ? double.NaN : all.Count(x => x == 0d) / (double)all.Count):P1} of {all.Count} children ({failed} mutations failed and were not counted)"));
            return 0;
        }

        /// <summary>The q-quantile by linear interpolation between order statistics (R's type 7); NaN when empty.</summary>
        private static double Quantile(List<double> values, double q)
        {
            if (values.Count == 0) return double.NaN;
            double[] v = values.OrderBy(x => x).ToArray();
            double h = (v.Length - 1) * q;
            int lo = (int)Math.Floor(h);
            int hi = Math.Min(lo + 1, v.Length - 1);
            return v[lo] + (h - lo) * (v[hi] - v[lo]);
        }

        /// <summary>The genome with every sensor input on the listed channels reading the constant 0.</summary>
        private static Genome KnockoutOf(Genome genome, SensorChannel[] channels, out int cut) => KnockoutOf(genome, channels, BodyOf, out cut);

        /// <summary>The knockout with the body a genome develops into given: <see cref="KnockoutReach"/> reads it with limits and shapes of its own, where the run's are not set.</summary>
        internal static Genome KnockoutOf(Genome genome, SensorChannel[] channels, Func<Genome, Phenotype> bodyOf, out int cut)
        {
            if (genome.Brain != null) return KnockoutCentral(genome, channels, bodyOf, out cut);
            Genome g = genome.Clone();
            cut = 0;
            foreach (MorphNode node in g.Nodes)
            {
                foreach (NeuronDef neuron in node.Neurons)
                {
                    NeuronInput[] inputs = neuron.Inputs;
                    for (int k = 0; k < inputs.Length; k++)
                    {
                        if (inputs[k].Kind != NeuronInputKind.Sensor || Array.IndexOf(channels, inputs[k].Channel) < 0) continue;
                        inputs[k] = NeuronInput.FromConstant(0f, inputs[k].Weight);
                        cut++;
                    }
                }
            }
            return g;
        }

        /// <summary>
        /// D146's knockout: the links from every port of the listed channels removed (null: every
        /// port), which is a link of weight 0 and so a sense read as 0. The links between units stay.
        /// </summary>
        internal static Genome KnockoutCentral(Genome genome, SensorChannel[] channels, out int cut) => KnockoutCentral(genome, channels, BodyOf, out cut);

        internal static Genome KnockoutCentral(Genome genome, SensorChannel[] channels, Func<Genome, Phenotype> bodyOf, out int cut)
        {
            Genome g = genome.Clone();
            Dictionary<ulong, BrainPort> ports = CentralWiring.AllPorts(bodyOf(g));
            cut = g.Brain.Links.RemoveAll(l =>
                ports.TryGetValue(l.From, out BrainPort p) && (channels == null || Array.IndexOf(channels, p.Channel) >= 0));
            return g;
        }

        // ------------------------------------------------------------------ the seeds

        private static ulong[] SeedsFor(int generation, int count, ulong salt) => SeedsFor(_o.Seed, generation, count, salt);

        /// <summary>The same function of a run's seed given, which <c>--restep</c> reads before it sets the nursery's settings.</summary>
        private static ulong[] SeedsFor(ulong runSeed, int generation, int count, ulong salt) =>
            EpisodeSeeds.For(runSeed, generation, count, salt);

        /// <summary>
        /// Every degree of freedom a constant power and bend, through neurons with no inputs and
        /// a bias: neuron d the power and neuron n + d the bend (propose-joint-drive.md).
        /// </summary>
        private static Genome GraftConstant(Genome body, float power, float bend)
        {
            Genome g = body.Clone();
            g.Brain = null;   // D146: a hand-wired brain replaces the one brain whole
            foreach (MorphNode node in g.Nodes)
            {
                int dofs = node.JointType.DofCount();
                if (dofs == 0) continue;
                var neurons = new NeuronDef[2 * dofs];
                for (int d = 0; d < 2 * dofs; d++)
                    neurons[d] = new NeuronDef { Op = NeuronOp.Sum, Frequency = 1f, Amplitude = 1f, Bias = d < dofs ? power : bend, Inputs = Array.Empty<NeuronInput>() };
                node.Neurons = neurons;
            }
            return g;
        }

        // ------------------------------------------------------------------ plumbing

        private static string Num(double v) => double.IsNaN(v) ? "null" : v.ToString("R", Inv);

        private static readonly object _sayLock = new object();

        // Locked: the slow-tank report says from the episode threads.
        private static void Say(string line)
        {
            lock (_sayLock)
            {
                Console.WriteLine(line);
                File.AppendAllText(Path.Combine(_o.Out, "nursery.log"), line + Environment.NewLine);
            }
        }

        private const string Usage =
            "usage: Evosim.Nursery --recipe <recipe.json> --out <dir> [--threads n]\n" +
            "       Evosim.Nursery --recipe <recipe.json> --check\n" +
            "       Evosim.Nursery --rerun <entry> --out <dir> [--threads n]\n" +
            "       Evosim.Nursery --recipe <recipe.json> --measure-dref <genomes.tsv> [--dref-samples n] --out <dir>\n" +
            "       Evosim.Nursery --restep <request.json> --out <dir> [--check] [--threads 1]\n" +
            "       Evosim.Nursery --graft-nose <pool.tsv> --config <config.json> --out <dir>";

        private static Options Parse(string[] args)
        {
            var o = new Options();
            var seen = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                string v() => args[++i];
                seen.Add(args[i]);
                switch (args[i])
                {
                    case "--recipe": o.Recipe = v(); break;
                    case "--check": o.Check = true; break;
                    case "--rerun": o.Rerun = v(); break;
                    case "--out": o.Out = v(); break;
                    case "--threads": o.Threads = int.Parse(v(), Inv); break;
                    case "--measure-dref": o.MeasureDref = v(); break;
                    case "--dref-samples": o.DrefSamples = int.Parse(v(), Inv); break;
                    default: throw new ArgumentException("unknown " + args[i] + "\n" + Usage);
                }
            }

            if (o.Rerun == null && o.Recipe == null) throw new ArgumentException(Usage);
            string[] twice = seen.Where(f => f.StartsWith("--", StringComparison.Ordinal)).GroupBy(f => f).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
            if (twice.Length > 0) throw new ArgumentException("given twice: " + string.Join(" ", twice));
            if (o.Rerun != null)
            {
                string[] other = seen.Where(f => f != "--rerun" && f != "--out" && f != "--threads").ToArray();
                if (other.Length > 0) throw new ArgumentException("with --rerun the experiment is the entry's recipe: only --out and --threads may be given, not " + string.Join(" ", other));
                if (o.Out == null) throw new ArgumentException("usage: Evosim.Nursery --rerun <entry> --out <new dir> [--threads n]");
                if (o.Threads < 1) throw new ArgumentException("--threads must be 1 or more");
                return o;
            }
            string[] extra = seen.Where(f => f != "--recipe" && f != "--out" && f != "--threads" && f != "--check" && f != "--measure-dref" && f != "--dref-samples").ToArray();
            if (extra.Length > 0) throw new ArgumentException("with --recipe the experiment is the recipe: only --out, --threads, --check, --measure-dref and --dref-samples may be given, not " + string.Join(" ", extra));
            if (o.Out == null && !o.Check) throw new ArgumentException("usage: Evosim.Nursery --recipe <recipe.json> --out <dir> [--threads n]");
            if (o.Threads < 1) throw new ArgumentException("--threads must be 1 or more");
            if (seen.Contains("--dref-samples") && o.MeasureDref == null) throw new ArgumentException("--dref-samples acts only with --measure-dref");
            if (o.MeasureDref != null && o.DrefSamples < 1) throw new ArgumentException("--dref-samples must be 1 or more");
            return o;
        }
    }

    /// <summary>
    /// D241's growth-reserve gene, given to a bred-pool genome as the search and the exam read it (logbook/specs/growth-runway-design.md).
    /// Called once, where a pool line becomes a genome and before any episode: episodes run in parallel on shared genome objects, and a member's
    /// genome text is its identity in the run's logs, so nothing inside an episode writes the gene. The tank's own bodies, the leaves and a
    /// re-step need nothing, because <c>World.Admit</c> gives a gene-less genome the same value on a clone.
    /// </summary>
    public static class GrowthStart
    {
        /// <summary>
        /// With the climb on (<see cref="RunConfig.GrowthAdultSeconds"/> above 0) and no gene on <paramref name="genome"/>, sets it to
        /// <see cref="RunConfig.GrowthReserveStartSeconds"/>; otherwise leaves the genome alone, so a gene it already carries is kept, and with the
        /// climb off (T = 0) nothing is written. <paramref name="config"/> is the episode's own (the recipe's overrides applied). Returns
        /// <paramref name="genome"/> itself, changed in place, so that it can stand where a genome is read.
        /// </summary>
        public static Genome Give(Genome genome, RunConfig config)
        {
            if (genome == null) throw new ArgumentNullException(nameof(genome));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (config.GrowthAdultSeconds > 0f && !genome.GrowthReserveSeconds.HasValue)
                genome.GrowthReserveSeconds = config.GrowthReserveStartSeconds;
            return genome;
        }
    }
}
