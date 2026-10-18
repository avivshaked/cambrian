using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Evosim.Core;
using Evosim.Nursery;

namespace Evosim.NurseryRoom
{
    /// <summary>One brain of an exam, by its index in the exam's order. The index is the only identity: two brains may carry one label.</summary>
    public sealed class ExamBrainView
    {
        public int Index { get; set; }
        /// <summary>The label the plan gives it: the run's own record, or the recipe's.</summary>
        public string Label { get; set; }
        public string Kind { get; set; }
        /// <summary>The control this brain applies, as the recipe states it (kind, and its channels, power, bias, gain or mode where it has them); null when no recipe was found to say.</summary>
        public JsonElement? Control { get; set; }
        /// <summary>The label the run's own exam rows (<c>tank-exam.jsonl</c>) carry for this index, or null when the run holds no row of it.</summary>
        public string RunLabel { get; set; }
        /// <summary>True when <see cref="RunLabel"/> is not <see cref="Label"/>.</summary>
        public bool LabelDiffers { get; set; }
        /// <summary>The registry entry's known difference that covers a differing label, quoted as the entry writes it; null when the label matches or no known difference covers it.</summary>
        public string KnownDifference { get; set; }
    }

    /// <summary>One genome of the exam's pool, by its index in pool order.</summary>
    public sealed class ExamGenomeView
    {
        public int Index { get; set; }
        /// <summary>The id the run records, or, for a run made before the plan was recorded, <c>&lt;name&gt;.&lt;index&gt;</c> (its line in the genomes file, as the genome screen names it).</summary>
        public string Id { get; set; }
        public int Body { get; set; }
    }

    /// <summary>One draw of the exam: its episode seed and its tanks, each a list of pool indices in slot order.</summary>
    public sealed class ExamDrawView
    {
        public int Draw { get; set; }
        /// <summary>A decimal string: it is 64 bits, and a reader that takes JSON numbers as doubles would round it.</summary>
        public string Seed { get; set; }
        /// <summary>Null when the tank size is not known, so that no tank could be dealt.</summary>
        public List<List<int>> Tanks { get; set; }
        /// <summary><c>recorded</c> (the run's draws file) or <c>reconstructed</c> (<c>TankLog.PlanTanks</c>).</summary>
        public string TanksFrom { get; set; }
    }

    /// <summary>Where one input of the plan came from.</summary>
    public sealed class ExamInputView
    {
        public string Input { get; set; }
        public string Source { get; set; }
        public string From { get; set; }
    }

    /// <summary>One thing the plan was checked against, and whether it held.</summary>
    public sealed class ExamCheckView
    {
        public string Name { get; set; }
        public bool Ok { get; set; }
        public string Detail { get; set; }
    }

    /// <summary>
    /// What an exam of a run swims, as the room knows it: the brains in index order, the genomes in pool order, the episode seeds, and each draw's tanks
    /// as pool indices. Served read-only as <c>nursery://exam-plan/{run}</c>. (Not named <c>ExamPlan</c>: the nursery's class of that name, which writes
    /// the record this reads, is in scope wherever the room says <c>Evosim.Nursery</c>.)
    /// </summary>
    public sealed class ExamPlanView
    {
        public string Run { get; set; }
        public string Folder { get; set; }
        /// <summary><c>exam</c> (the held-out swim after a search) or <c>bench</c> (a pool swum as it is).</summary>
        public string Name { get; set; }
        /// <summary><c>record</c> (run.json's exam plan) or <c>reconstruction</c> (the registry entry's recipe and the pool file).</summary>
        public string Source { get; set; }
        /// <summary>The registry entry the plan was tied to, or null.</summary>
        public int? Entry { get; set; }
        public string RunSeed { get; set; }
        public string Salt { get; set; }
        public int Episodes { get; set; }
        public int? TankSize { get; set; }
        public List<ExamBrainView> Brains { get; set; } = new List<ExamBrainView>();
        public List<ExamGenomeView> Genomes { get; set; } = new List<ExamGenomeView>();
        public List<string> Seeds { get; set; } = new List<string>();
        public List<ExamDrawView> Draws { get; set; } = new List<ExamDrawView>();
        /// <summary>The registry entry's known differences, quoted as it writes them, when the plan was tied to an entry that has any.</summary>
        public List<string> KnownDifferences { get; set; } = new List<string>();
        public List<ExamInputView> Inputs { get; set; } = new List<ExamInputView>();
        public List<ExamCheckView> Checks { get; set; } = new List<ExamCheckView>();
        public List<string> Notes { get; set; } = new List<string>();
        /// <summary>The recipe the run was made from, as the plan's source names it (run.json's recipe path, or the registry entry's), repository-relative where it lies inside; null when the source names none.</summary>
        public string RecipePath { get; set; }
        /// <summary>The hash of that recipe resolved, as the run (or the entry) recorded it. A re-step's request carries it, and the nursery refuses a recipe that hashes otherwise.</summary>
        public string RecipeHash { get; set; }
        /// <summary>The pool file in the run's folder, one genome a line: <c>tank-&lt;name&gt;-genomes.tsv</c>. A pool index is a line of it.</summary>
        public string PoolFile { get; set; }
        /// <summary>The recipe's full path, found as the room finds a file (this tree, the main checkout); never served. Null when it was not found.</summary>
        internal string RecipeFull;
    }

    /// <summary>
    /// Rebuilds the plan of a run's exam. The run's own record is preferred (run.json's exam plan, with the draws file when the run wrote one); a run made
    /// before those existed is rebuilt from its registry entry's recipe (its hash checked against the entry) and its pool file, with the nursery's own
    /// <see cref="TankLog.PlanTanks"/> and Core's <see cref="Rng.SeedFor"/>. Nothing here swims, writes or guesses: what cannot be rebuilt is refused, with why.
    /// </summary>
    public static class ExamPlans
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly Regex PlainNameRe = new Regex(@"\A[A-Za-z0-9_-][A-Za-z0-9._-]{0,31}\z", RegexOptions.CultureInvariant);
        private static readonly Regex LogLineRe = new Regex(@"^(?<name>\w+): (?<genomes>\d+) genomes under (?<brains>\d+) brains, tanks of (?<tank>\d+), (?<draws>\d+) draws on held-out seeds \(salt (?<salt>\d+)\)", RegexOptions.CultureInvariant);

        /// <summary>
        /// The exam's episode seeds: seed <c>e</c> is <c>Rng.SeedFor(runSeed ^ salt, generation * 1000 + e)</c>. <b>This is a copy of one line of
        /// <c>Program.SeedsFor</c></b> (src/Evosim.Nursery/Program.cs), which is private; the room's test pins it to values worked out by hand from the formula, so
        /// a change on either side shows. The exam's generation is 0.
        /// </summary>
        public static ulong[] SeedsFor(ulong runSeed, ulong salt, int generation, int count)
        {
            var seeds = new ulong[count];
            for (int e = 0; e < count; e++) seeds[e] = Rng.SeedFor(runSeed ^ salt, (ulong)(generation * 1000 + e));
            return seeds;
        }

        // ------------------------------------------------------------------ the entry point

        public static ExamPlanView Build(RoomContext room, ResourceResolver.RunRef run, IReadOnlyList<ResourceResolver.RunRef> catalog)
        {
            string folder = run.Folder;
            if (folder == null || !Directory.Exists(folder)) throw new ResourceNotFound($"the run folder of {run.Id} does not exist on this machine, so its exam plan cannot be rebuilt");

            ResourceResolver.RunRef entryRef = run.Entry != null ? run : catalog.FirstOrDefault(r => r.Entry != null && r.Folder != null && SameFolder(r.Folder, folder));

            JsonElement? runJson = null;
            string runJsonProblem = null;
            FileLines rj = GenomeSet.ReadFile(Path.Combine(folder, "run.json"));
            if (rj.Problem != null) throw new ResourceRefused($"the exam plan of run {run.Id} cannot be rebuilt: run.json could not be read ({rj.Problem}), and the plan is not guessed meanwhile");
            if (!rj.Absent)
            {
                try { runJson = Js.Parse(string.Join("\n", rj.Lines)); }
                catch (JsonException e) { runJsonProblem = "run.json is not JSON (" + GenomeSet.Flat(e.Message) + ")"; }
            }

            JsonElement? plan = Js.At(runJson, "exam");
            if (plan.HasValue) return FromRecord(room, run, folder, runJson, plan.Value, entryRef);

            string why = rj.Absent ? "the run holds no run.json" : runJsonProblem ?? "run.json holds no exam plan (the run has not reached its exam, or its build did not write the plan)";
            if (entryRef == null)
                throw new ResourceRefused($"the exam plan of run {run.Id} cannot be rebuilt: {why}, and no registry entry names this run's folder, so there is no recipe to rebuild it from");
            return FromReconstruction(room, run, folder, entryRef, why);
        }

        private static bool SameFolder(string a, string b) =>
            string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                          Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);

        // ------------------------------------------------------------------ source 1: the run's own record

        private static ExamPlanView FromRecord(RoomContext room, ResourceResolver.RunRef run, string folder, JsonElement? runJson, JsonElement plan, ResourceResolver.RunRef entryRef)
        {
            ResourceRefused Bad(string what) => new ResourceRefused($"the exam plan in run {run.Id}'s run.json is not usable: {what}. It is not repaired or guessed.");

            string name = Js.Str(plan, "name") ?? "exam";
            if (!PlainNameRe.IsMatch(name)) throw Bad("its name is not a plain name");
            var v = new ExamPlanView { Run = run.Id, Folder = room.RelativeOrOutside(folder), Name = name, Source = "record", Entry = entryRef?.Number };
            v.PoolFile = "tank-" + name + "-genomes.tsv";
            string recordedRecipe = Js.Str(runJson, "recipe", "path");
            v.RecipeHash = Js.Str(runJson, "recipe", "hash");
            if (recordedRecipe != null)
            {
                v.RecipePath = room.RelativeOrOutside(recordedRecipe);
                // run.json holds the path as the nursery was given it, made absolute; one that lies in no tree of this machine is not followed.
                v.RecipeFull = Path.IsPathRooted(recordedRecipe) ? Path.GetFullPath(recordedRecipe) : room.ResolveFile(recordedRecipe, out _);
            }

            var controls = Js.Items(Js.At(runJson, "recipe", "resolved", "exam", "controls")).ToList();
            int b = 0;
            foreach (JsonElement brain in Js.Items(Js.At(plan, "brains")))
            {
                string label = Js.Str(brain, "label"), kind = Js.Str(brain, "kind");
                if (label == null || kind == null) throw Bad($"brain {b} has no label or no kind");
                JsonElement? control = b < controls.Count && Js.Str(controls[b], "kind") == kind ? controls[b] : (JsonElement?)null;
                v.Brains.Add(new ExamBrainView { Index = b, Label = label, Kind = kind, Control = control });
                b++;
            }
            if (v.Brains.Count == 0) throw Bad("it lists no brain");
            if (controls.Count == 0) v.Notes.Add("run.json holds no resolved recipe, so the controls the brains apply are not given");
            else if (v.Brains.Any(x => x.Control == null)) v.Notes.Add("a brain's control could not be matched to the recipe's control of that index and kind, so it is not given");

            int g = 0;
            foreach (JsonElement genome in Js.Items(Js.At(plan, "genomes")))
            {
                string id = Js.Str(genome, "id");
                double? body = Js.Num(genome, "body");
                if (id == null || !body.HasValue || body.Value != Math.Floor(body.Value)) throw Bad($"genome {g} has no id or no whole body");
                v.Genomes.Add(new ExamGenomeView { Index = g, Id = id, Body = (int)body.Value });
                g++;
            }
            if (v.Genomes.Count == 0) throw Bad("it lists no genome");

            double? episodes = Js.Num(plan, "episodes");
            if (!episodes.HasValue || episodes.Value != Math.Floor(episodes.Value) || episodes.Value < 1) throw Bad("its episodes is not a whole number of 1 or more");
            v.Episodes = (int)episodes.Value;
            string salt = Js.Str(plan, "salt");
            if (salt == null || !ulong.TryParse(salt, NumberStyles.None, Inv, out _)) throw Bad("its salt is not a decimal string");
            v.Salt = salt;
            var seeds = new List<ulong>();
            foreach (JsonElement s in Js.Items(Js.At(plan, "seeds")))
            {
                if (s.ValueKind != JsonValueKind.String || !ulong.TryParse(s.GetString(), NumberStyles.None, Inv, out ulong parsed)) throw Bad("a seed is not a decimal string");
                seeds.Add(parsed);
                v.Seeds.Add(s.GetString());
            }
            if (seeds.Count != v.Episodes) throw Bad($"it lists {seeds.Count} seeds for {v.Episodes} episodes");

            // The run's seed as run.json writes it, a JSON number: its text is kept, never a double.
            JsonElement? seedEl = Js.At(runJson, "seed");
            v.RunSeed = seedEl.HasValue && seedEl.Value.ValueKind == JsonValueKind.Number ? seedEl.Value.GetRawText() : null;

            v.TankSize = (int?)Js.Num(runJson, "recipe", "resolved", "setup", "tankSize");

            v.Inputs.Add(new ExamInputView { Input = "brains", Source = "run.json", From = "exam.brains (label and kind)" + (controls.Count > 0 ? ", controls from recipe.resolved.exam.controls by index" : "") });
            v.Inputs.Add(new ExamInputView { Input = "genomes", Source = "run.json", From = "exam.genomes (id and body), in pool order" });
            v.Inputs.Add(new ExamInputView { Input = "seeds", Source = "run.json", From = "exam.seeds, one a draw (salt " + v.Salt + ")" });

            // The tanks: the run's own draws file when it wrote one, else dealt by the nursery's own PlanTanks.
            string drawsFile = TankLog.BenchOrExamDrawsFile(name);
            RecordedDraws recorded = ReadDraws(Path.Combine(folder, drawsFile), v, seeds, out string drawsNote);
            if (drawsNote != null) v.Notes.Add(drawsNote);
            if (v.TankSize == null && recorded != null && recorded.MaxTank > 0) v.TankSize = recorded.MaxTank;
            DealTanks(v, seeds.ToArray(), recorded, drawsFile);

            AttachRunRows(v, folder, entryRef);
            return v;
        }

        // ------------------------------------------------------------------ source 2: a reconstruction for a run made before the record

        private static ExamPlanView FromReconstruction(RoomContext room, ResourceResolver.RunRef run, string folder, ResourceResolver.RunRef entryRef, string whyNoRecord)
        {
            RegistryEntry entry = entryRef.Entry;
            string label = $"registry entry {entryRef.Number}";
            ResourceRefused Refuse(string what) => new ResourceRefused($"the exam plan of run {run.Id} cannot be rebuilt: {whyNoRecord}, and rebuilding it from {label} fails: {what}");

            if (string.IsNullOrEmpty(entry.RecipePath)) throw Refuse("the entry names no recipe file");
            string recipeFull = room.ResolveFile(entry.RecipePath, out string problem);
            if (recipeFull == null) throw Refuse(problem);
            if (!File.Exists(recipeFull)) throw Refuse($"the recipe {entry.RecipePath} is not in this tree or the main checkout");
            Recipe recipe;
            try { recipe = Recipe.Read(recipeFull); }   // Read: the plan needs the recipe's fields and hash, not a world this build can load
            catch (Exception e) when (e is InvalidDataException || e is IOException || e is UnauthorizedAccessException)
            {
                throw Refuse($"the recipe {entry.RecipePath} does not load ({room.Scrub(GenomeSet.Flat(e.Message))})");
            }
            string hash = recipe.Hash();
            if (!string.Equals(hash, entry.RecipeHash, StringComparison.OrdinalIgnoreCase))
                throw new ResourceRefused($"the exam plan of run {run.Id} is not rebuilt: the recipe {entry.RecipePath} hashes to {hash}, but {label} records {entry.RecipeHash}. " +
                    "The recipe is not the one the run was made from (it was edited after the run, or the entry names another file), so its seed, salt and brains would be guesses.");

            string name = recipe.Search != null ? "exam" : "bench";
            string poolFile = "tank-" + name + "-genomes.tsv";
            FileLines pool = GenomeSet.ReadFile(Path.Combine(folder, poolFile));
            if (pool.Absent) throw Refuse($"{poolFile} is not in the run's folder, so the pool is unknown");
            if (pool.Problem != null) throw Refuse($"{poolFile} could not be read ({pool.Problem})");

            var v = new ExamPlanView
            {
                Run = run.Id, Folder = room.RelativeOrOutside(folder), Name = name, Source = "reconstruction", Entry = entryRef.Number,
                RunSeed = recipe.Seed.ToString(Inv), Salt = recipe.ExamSalt.ToString(Inv), Episodes = recipe.ExamEpisodes, TankSize = recipe.TankSize,
                RecipePath = entry.RecipePath, RecipeHash = hash, PoolFile = poolFile, RecipeFull = recipeFull,
            };

            for (int i = 0; i < recipe.Controls.Count; i++)
                v.Brains.Add(new ExamBrainView { Index = i, Label = recipe.Controls[i].Label, Kind = recipe.Controls[i].Kind, Control = ControlOf(recipe.Controls[i]) });

            for (int i = 0; i < pool.Lines.Count; i++)
            {
                int tab = pool.Lines[i].IndexOf('\t');
                if (tab <= 0 || !int.TryParse(pool.Lines[i].Substring(0, tab), NumberStyles.None, Inv, out int body))
                    throw Refuse($"line {i + 1} of {poolFile} does not start with a body index and a tab");
                v.Genomes.Add(new ExamGenomeView { Index = i, Id = name + "." + i.ToString(Inv), Body = body });
            }
            if (v.Genomes.Count == 0) throw Refuse($"{poolFile} holds no genome");

            ulong[] seeds = SeedsFor(recipe.Seed, recipe.ExamSalt, 0, recipe.ExamEpisodes);
            v.Seeds.AddRange(seeds.Select(s => s.ToString(Inv)));

            string recipeShown = entry.RecipePath;
            v.Inputs.Add(new ExamInputView { Input = "brains", Source = "recipe", From = $"exam.controls of {recipeShown} ({label}; its hash {hash} equals the entry's), matched by index, never by label" });
            v.Inputs.Add(new ExamInputView { Input = "genomes", Source = "pool file", From = $"{poolFile}, one genome a line in pool order; ids are the line's place, since the run recorded none" });
            v.Inputs.Add(new ExamInputView { Input = "seeds", Source = "recipe", From = $"Rng.SeedFor(seed ^ salt, 0 * 1000 + e) with seed {v.RunSeed} and salt {v.Salt}, both from {recipeShown}" });

            DealTanks(v, seeds, null, null);
            v.Checks.Add(new ExamCheckView { Name = "recipe", Ok = true, Detail = $"{recipeShown} hashes to {hash}, which {label} records" });
            if (entry.Reference != null) v.KnownDifferences.AddRange(entry.Reference.KnownDifferences);

            CheckLog(v, folder);
            AttachRunRows(v, folder, entryRef);
            return v;
        }

        /// <summary>A control as the recipe states it: its kind and whichever of channels, power, bias, gain and mode that kind has.</summary>
        private static JsonElement ControlOf(Recipe.ExamControl c)
        {
            var d = new Dictionary<string, object> { ["kind"] = c.Kind };
            switch (c.Kind)
            {
                case "knockout": d["channels"] = c.Channels.Select(x => x.ToString()).ToArray(); break;
                case "constant": d["power"] = c.Power; break;
                case "kinesis": d["mode"] = c.Mode; d["bias"] = c.Bias; d["gain"] = c.Gain; break;
            }
            return Js.Parse(Js.Serialize(d));
        }

        // ------------------------------------------------------------------ the tanks

        private sealed class RecordedDraws
        {
            /// <summary>Draw to its tanks as pool indices, from the first brain that recorded the draw.</summary>
            public readonly Dictionary<int, List<List<int>>> ByDraw = new Dictionary<int, List<List<int>>>();
            public int MaxTank;
            public int Rows;
            public bool Usable = true;
        }

        /// <summary>
        /// The draws file's rows, as pool indices. Null when the run wrote none. A file the plan cannot be read against (an id the plan lacks, one id twice,
        /// a seed that is not the plan's, brains that disagree on a draw) is not used, and a check says so.
        /// </summary>
        private static RecordedDraws ReadDraws(string path, ExamPlanView v, List<ulong> seeds, out string note)
        {
            note = null;
            FileLines f = GenomeSet.ReadFile(path);
            string file = Path.GetFileName(path);
            if (f.Absent) { note = $"the run wrote no {file}, so the tanks are dealt by TankLog.PlanTanks and not read from the run"; return null; }
            if (f.Problem != null) { note = $"{file} could not be read ({f.Problem}), so the tanks are dealt by TankLog.PlanTanks"; return null; }

            var byId = new Dictionary<string, int>(StringComparer.Ordinal);
            bool dup = false;
            foreach (ExamGenomeView g in v.Genomes) if (!byId.TryAdd(g.Id, g.Index)) dup = true;

            var r = new RecordedDraws();
            var problems = new List<string>();
            int lineNo = 0;
            foreach (string line in f.Lines)
            {
                lineNo++;
                JsonElement row;
                try { row = Js.Parse(line); }
                catch (JsonException) { problems.Add($"line {lineNo} is not JSON"); continue; }
                double? draw = Js.Num(row, "draw"), brain = Js.Num(row, "b");
                string seed = Js.Str(row, "seed");
                if (!draw.HasValue || !brain.HasValue || seed == null) { problems.Add($"line {lineNo} has no draw, b or seed"); continue; }
                int d = (int)draw.Value;
                if (d < 0 || d >= seeds.Count) { problems.Add($"line {lineNo}: draw {d} is outside the plan's {seeds.Count}"); continue; }
                if (seed != seeds[d].ToString(Inv)) { problems.Add($"line {lineNo}: draw {d}'s seed {seed} is not the plan's {seeds[d].ToString(Inv)}"); continue; }
                var tanks = new List<List<int>>();
                bool good = true;
                foreach (JsonElement tank in Js.Items(Js.At(row, "tanks")))
                {
                    var members = new List<int>();
                    foreach (JsonElement m in tank.ValueKind == JsonValueKind.Array ? tank.EnumerateArray().ToList() : new List<JsonElement>())
                    {
                        if (m.ValueKind != JsonValueKind.String || dup || !byId.TryGetValue(m.GetString(), out int idx)) { good = false; break; }
                        members.Add(idx);
                    }
                    if (!good) break;
                    tanks.Add(members);
                }
                if (!good) { problems.Add($"line {lineNo}: a tank names a genome the plan does not hold, or the plan's ids are not unique"); continue; }
                r.Rows++;
                if (!r.ByDraw.TryGetValue(d, out List<List<int>> have)) { r.ByDraw[d] = tanks; r.MaxTank = Math.Max(r.MaxTank, tanks.Select(t => t.Count).DefaultIfEmpty(0).Max()); }
                else if (!SameTanks(have, tanks)) problems.Add($"line {lineNo}: brain {(int)brain.Value}'s tanks for draw {d} differ from another brain's");
            }

            if (problems.Count > 0)
            {
                r.Usable = false;
                v.Checks.Add(new ExamCheckView { Name = "draws file", Ok = false, Detail = $"{file} is not used: {problems[0]}" + (problems.Count > 1 ? $" (and {problems.Count - 1} more)" : "") });
            }
            else
            {
                v.Checks.Add(new ExamCheckView { Name = "draws file", Ok = true, Detail = $"{file}: {r.Rows} rows over {r.ByDraw.Count} of {seeds.Count} draws, each in the plan's seeds and genome ids, every brain's tanks for a draw alike" });
            }
            return r;
        }

        private static bool SameTanks(List<List<int>> a, List<List<int>> b) =>
            a.Count == b.Count && a.Zip(b, (x, y) => x.SequenceEqual(y)).All(ok => ok);

        /// <summary>Gives every draw its seed and tanks: the recorded tanks where the draws file holds the draw, the nursery's own deal otherwise.</summary>
        private static void DealTanks(ExamPlanView v, ulong[] seeds, RecordedDraws recorded, string drawsFile)
        {
            int[][][] dealt = v.TankSize.HasValue && v.TankSize.Value >= 1 ? TankLog.PlanTanks(v.Genomes.Count, v.TankSize.Value, seeds) : null;
            if (dealt == null) v.Notes.Add("the tank size is not known (the run holds no resolved recipe and no draws file), so no tank is dealt");
            else if (recorded == null || !recorded.Usable)
                v.Inputs.Add(new ExamInputView { Input = "tanks", Source = "reconstruction", From = $"TankLog.PlanTanks({v.Genomes.Count}, {v.TankSize}, seeds)" + (drawsFile != null ? $", since {drawsFile} could not be used" : "") });

            int agree = 0, differ = 0, recordedDraws = 0;
            for (int d = 0; d < seeds.Length; d++)
            {
                List<List<int>> rec = recorded != null && recorded.Usable && recorded.ByDraw.TryGetValue(d, out List<List<int>> t) ? t : null;
                List<List<int>> dealtHere = dealt?[d].Select(x => x.ToList()).ToList();
                if (rec != null && dealtHere != null) { if (SameTanks(rec, dealtHere)) agree++; else differ++; }
                if (rec != null) recordedDraws++;
                v.Draws.Add(new ExamDrawView { Draw = d, Seed = seeds[d].ToString(Inv), Tanks = rec ?? dealtHere, TanksFrom = rec != null ? "recorded" : dealtHere != null ? "reconstructed" : null });
            }
            if (recordedDraws > 0)
            {
                v.Inputs.Add(new ExamInputView { Input = "tanks", Source = "run", From = $"{drawsFile}: {recordedDraws} of {seeds.Length} draws, as pool indices by genome id" });
                if (recordedDraws < seeds.Length)
                    v.Notes.Add($"the draws file holds {recordedDraws} of {seeds.Length} draws so far; the others are dealt by TankLog.PlanTanks");
                if (dealt != null)
                    v.Checks.Add(new ExamCheckView
                    {
                        Name = "tanks against TankLog.PlanTanks",
                        Ok = differ == 0,
                        Detail = differ == 0 ? $"the reconstruction equals the recorded tanks for all {agree} recorded draws" : $"the reconstruction differs from the recorded tanks on {differ} of {agree + differ} recorded draws; the recorded tanks are shown",
                    });
            }
        }

        // ------------------------------------------------------------------ what the run's own rows and log say

        /// <summary>
        /// <c>tank-&lt;name&gt;.jsonl</c> holds each brain's rows in a block of one row a genome, so a row's brain is its place, never its label. The label the
        /// rows carry is compared with the plan's; a difference is reported against the entry's known differences.
        /// </summary>
        private static void AttachRunRows(ExamPlanView v, string folder, ResourceResolver.RunRef entryRef)
        {
            string file = "tank-" + v.Name + ".jsonl";
            FileLines f = GenomeSet.ReadFile(Path.Combine(folder, file));
            if (f.Absent) { v.Notes.Add($"{file} is not in the run, so the labels its rows carry are not compared with the plan's"); return; }
            if (f.Problem != null) { v.Notes.Add($"{file} could not be read ({f.Problem}), so its labels are not compared with the plan's"); return; }

            int G = v.Genomes.Count, B = v.Brains.Count;
            var labels = new List<string>();
            var problems = new List<string>();
            for (int k = 0; k < f.Lines.Count; k++)
            {
                JsonElement row;
                try { row = Js.Parse(f.Lines[k]); }
                catch (JsonException) { problems.Add($"row {k + 1} is not JSON"); break; }
                int brain = k / G, genome = k % G;
                if (brain >= B) { problems.Add($"it holds {f.Lines.Count} rows, more than {B} brains of {G} genomes"); break; }
                double? gi = Js.Num(row, "genome"), body = Js.Num(row, "body");
                string label = Js.Str(row, "brain");
                if (label == null || !gi.HasValue || (int)gi.Value != genome) { problems.Add($"row {k + 1} is not genome {genome} of brain {brain}"); break; }
                if (!body.HasValue || (int)body.Value != v.Genomes[genome].Body) { problems.Add($"row {k + 1}'s body is not the plan's body of genome {genome}"); break; }
                if (genome == 0) labels.Add(label);
                else if (label != labels[brain]) { problems.Add($"row {k + 1}'s label differs from the first row of its brain"); break; }
                int per = Js.Items(Js.At(row, "per")).Count();
                if (per != v.Episodes) { problems.Add($"row {k + 1} holds {per} episodes, not the plan's {v.Episodes}"); break; }
            }
            if (problems.Count > 0)
            {
                v.Checks.Add(new ExamCheckView { Name = "exam rows", Ok = false, Detail = $"{file} does not fit the plan, so its labels are not compared: {problems[0]}" });
                return;
            }
            bool complete = f.Lines.Count == B * G;
            v.Checks.Add(new ExamCheckView
            {
                Name = "exam rows", Ok = true,
                Detail = complete ? $"{file}: {f.Lines.Count} rows, {B} brains x {G} genomes, in the plan's order, each of the plan's episodes" : $"{file}: {f.Lines.Count} of {B * G} rows so far, in the plan's order, each of the plan's episodes",
            });

            string[] known = entryRef?.Entry?.Reference?.KnownDifferences ?? new string[0];
            if (known.Length > 0 && v.KnownDifferences.Count == 0) v.KnownDifferences.AddRange(known);
            var unexplained = new List<int>();
            for (int b = 0; b < labels.Count; b++)
            {
                ExamBrainView brain = v.Brains[b];
                brain.RunLabel = labels[b];
                brain.LabelDiffers = labels[b] != brain.Label;
                if (!brain.LabelDiffers) continue;
                brain.KnownDifference = known.FirstOrDefault(k => k.IndexOf(brain.Label, StringComparison.OrdinalIgnoreCase) >= 0 && k.IndexOf("label", StringComparison.OrdinalIgnoreCase) >= 0);
                if (brain.KnownDifference == null) unexplained.Add(b);
            }
            int differing = v.Brains.Count(x => x.LabelDiffers);
            v.Checks.Add(new ExamCheckView
            {
                Name = "brain labels",
                Ok = unexplained.Count == 0,
                Detail = differing == 0 ? "every brain's label is the one the run's rows carry"
                       : unexplained.Count == 0 ? $"{differing} label(s) differ from the run's rows, at index {string.Join(", ", v.Brains.Where(x => x.LabelDiffers).Select(x => x.Index))}, each covered by the registry entry's known differences; brains are matched by index"
                       : $"the label at index {string.Join(", ", unexplained)} differs from the run's rows and no known difference of the registry entry covers it; brains are matched by index",
            });
        }

        /// <summary>
        /// The nursery logged the exam's shape when it started (<c>exam: 6 genomes under 18 brains, tanks of 8, 32 draws on held-out seeds (salt N)</c>): a second
        /// witness, apart from the recipe, to the pool, the brains, the tank size, the draws and the salt the seeds come from.
        /// </summary>
        private static void CheckLog(ExamPlanView v, string folder)
        {
            FileLines f = GenomeSet.ReadFile(Path.Combine(folder, "nursery.log"));
            if (f.Absent || f.Problem != null) { v.Notes.Add("nursery.log is not readable, so the plan's shape is not checked against what the run logged"); return; }
            Match m = null;
            foreach (string line in f.Lines)
            {
                Match x = LogLineRe.Match(line);
                if (x.Success && x.Groups["name"].Value == v.Name) m = x;
            }
            if (m == null) { v.Notes.Add($"nursery.log holds no line of the {v.Name}'s shape, so the plan is not checked against it"); return; }
            var differ = new List<string>();
            void Cmp(string what, string logged, string planned) { if (logged != planned) differ.Add($"{what}: the log says {logged}, the plan {planned}"); }
            Cmp("genomes", m.Groups["genomes"].Value, v.Genomes.Count.ToString(Inv));
            Cmp("brains", m.Groups["brains"].Value, v.Brains.Count.ToString(Inv));
            Cmp("tank size", m.Groups["tank"].Value, v.TankSize?.ToString(Inv));
            Cmp("draws", m.Groups["draws"].Value, v.Episodes.ToString(Inv));
            Cmp("salt", m.Groups["salt"].Value, v.Salt);
            v.Checks.Add(new ExamCheckView
            {
                Name = "nursery.log", Ok = differ.Count == 0,
                Detail = differ.Count == 0 ? $"the run logged {m.Groups["genomes"].Value} genomes under {m.Groups["brains"].Value} brains, tanks of {m.Groups["tank"].Value}, {m.Groups["draws"].Value} draws, salt {m.Groups["salt"].Value}: the plan's" : string.Join("; ", differ),
            });
        }
    }
}
