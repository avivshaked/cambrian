using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Evosim.Nursery;

namespace Evosim.NurseryRoom
{
    /// <summary>Asks whether a process the room started still lives, and ends one. The room's tests give it a fake, so that no process is ever started or ended.</summary>
    public interface IProcessControl
    {
        /// <summary>True while the process lives. <paramref name="launchedUtc"/> is when the room started it: a process that began before that is another one that was given the same id.</summary>
        bool IsAlive(int pid, DateTime launchedUtc);

        /// <summary>Ends the process and every process it started. Throws when it could not be ended.</summary>
        void Stop(int pid);
    }

    /// <summary>The real thing: the system's own process list.</summary>
    public sealed class SystemProcessControl : IProcessControl
    {
        public bool IsAlive(int pid, DateTime launchedUtc)
        {
            try
            {
                using (Process p = Process.GetProcessById(pid))
                {
                    if (p.HasExited) return false;
                    try { if (p.StartTime.ToUniversalTime() < launchedUtc.AddSeconds(-5)) return false; }
                    catch (Exception e) when (e is InvalidOperationException || e is Win32Exception || e is NotSupportedException) { /* the start is not readable: the process is taken to be the one the room started */ }
                    return true;
                }
            }
            catch (ArgumentException) { return false; }            // no process has this id
            catch (InvalidOperationException) { return false; }    // it ended while it was being looked at
        }

        public void Stop(int pid)
        {
            try
            {
                using (Process p = Process.GetProcessById(pid))
                {
                    if (p.HasExited) return;
                    p.Kill(entireProcessTree: true);
                    p.WaitForExit(10000);
                }
            }
            catch (ArgumentException) { /* already gone */ }
            catch (InvalidOperationException) { /* already gone */ }
        }
    }

    /// <summary>Copies a file by content. The room checks the copy's SHA-256 against the source's afterwards, so a copier that damages one is caught.</summary>
    public interface IEpisodeFileCopier
    {
        void Copy(string source, string target);
    }

    public sealed class ByContentCopier : IEpisodeFileCopier
    {
        public void Copy(string source, string target) => File.Copy(source, target, overwrite: false);
    }

    /// <summary>What the timing-read file says now.</summary>
    public sealed class TimingReadState
    {
        /// <summary>
        /// <c>none</c> (no file), <c>declared</c> (a timing read is in force), <c>stale</c> (its <c>until</c> has passed: it counts as absent),
        /// <c>unparsable</c> (it does not parse: it counts as absent) or <c>unreadable</c> (it is there and could not be read: no step starts).
        /// </summary>
        public string State = "none";
        public string Who, Why, Problem, Note;
        public DateTimeOffset? Until;

        /// <summary>True when the state refuses a step: a declared timing read, or a file that exists and cannot be read.</summary>
        public bool Blocks => State == "declared" || State == "unreadable";

        public object View() => new
        {
            state = State, who = Who, why = Why, until = Until?.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
            problem = Problem, note = Note, file = TimingRead.RelativePath, blocks = Blocks,
        };
    }

    /// <summary>
    /// The coordinating session's timing-read declaration (its ruling, 2026-10-02 evening): <c>scratch/machine/timing-read.json</c> in the main
    /// checkout, <c>{"who","why","until"}</c> with <c>until</c> an ISO time. <b>The room only reads it.</b> It never writes, creates, replaces or deletes
    /// the file: the session that times writes it before and deletes it after, and a stale one is simply treated as absent. It is opened for reading
    /// only, sharing every other kind of access, so that a writer is never held off.
    /// </summary>
    public static class TimingRead
    {
        public const string RelativePath = "scratch/machine/timing-read.json";

        public static string PathIn(RoomContext room) => Path.Combine(room.MainCheckout, "scratch", "machine", "timing-read.json");

        public static TimingReadState Read(RoomContext room, DateTimeOffset? now = null)
        {
            string path = PathIn(room);
            var s = new TimingReadState();
            string text;
            try
            {
                if (!File.Exists(path)) return s;
                using (var f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var r = new StreamReader(f, new UTF8Encoding(false)))
                    text = r.ReadToEnd();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                s.State = "unreadable";
                s.Problem = ActivityLog.Clean(room.Scrub(e.Message), 200);
                s.Note = "the timing-read file is there and could not be read, so whether a timing read is declared is not known: no episode is stepped until it can be";
                return s;
            }
            JsonElement j;
            try { j = Js.Parse(text); }
            catch (JsonException e)
            {
                s.State = "unparsable";
                s.Problem = "not JSON (" + ActivityLog.Clean(e.Message, 120) + ")";
                s.Note = "the timing-read file does not parse, so it counts as absent";
                return s;
            }
            string until = j.ValueKind == JsonValueKind.Object ? Js.Str(j, "until") : null;
            if (until == null || !DateTimeOffset.TryParse(until, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTimeOffset when))
            {
                s.State = "unparsable";
                s.Problem = j.ValueKind != JsonValueKind.Object ? "not a JSON object" : until == null ? "has no \"until\" time" : "\"until\" is not a time";
                s.Note = "the timing-read file does not parse, so it counts as absent";
                return s;
            }
            s.Who = ActivityLog.Clean(Js.Str(j, "who"), 200);
            s.Why = ActivityLog.Clean(Js.Str(j, "why"), 300);
            s.Until = when;
            if (when <= (now ?? DateTimeOffset.UtcNow))
            {
                s.State = "stale";
                s.Note = "the timing-read file's until time has passed, so it counts as absent";
                return s;
            }
            s.State = "declared";
            return s;
        }
    }

    /// <summary>
    /// The room's side of an episode re-step: the operation <c>episode.step</c>, the cap's count of the steps that live, the stop when a timing read is
    /// declared, the cache folder, and the resource <c>nursery://episode/{run}/{b}/{d}/{t}</c>. The state is the cache folder's files, so that a room that
    /// is started again finds a step its predecessor began. Nothing here swims: the nursery does that, as a process of its own.
    /// </summary>
    public static class EpisodeSteps
    {
        public const string StoppedForTimingRead = "stopped for a timing read";
        public const string StepFile = "step.json", StoppedFile = "stopped.json", RequestFile = "request.json", ResultFile = "result.json", EpisodeFile = "episode.json", FramesFile = "frames.bin";

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
        private static readonly Regex CacheName = new Regex(@"\Ab\d+-d\d+-t\d+\z");
        private static readonly Regex Leaf = new Regex(@"\A[A-Za-z0-9][A-Za-z0-9._-]{0,63}\z");

        // ------------------------------------------------------------------ where

        public static string EpisodesRoot(RoomContext room) => Path.Combine(room.Root, room.Settings.RunsDir.Replace('/', Path.DirectorySeparatorChar), "episodes");

        public static string NameOf(int b, int d, int t) => $"b{b.ToString(Inv)}-d{d.ToString(Inv)}-t{t.ToString(Inv)}";

        /// <summary>The cache folder of one episode: <c>&lt;runsDir&gt;/episodes/&lt;run's folder name&gt;/b&lt;b&gt;-d&lt;d&gt;-t&lt;t&gt;</c>. Keyed by the run's folder name, so a registry number and the name of its folder share one cache.</summary>
        public static string CacheFolder(RoomContext room, string runLeaf, int b, int d, int t) => Path.Combine(EpisodesRoot(room), runLeaf, NameOf(b, d, t));

        private static string LeafOf(ResourceResolver.RunRef run)
        {
            if (run.Folder == null) throw new ArgumentException($"the run \"{run.Id}\" has no folder on this machine");
            string leaf = Path.GetFileName(Path.GetFullPath(run.Folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (!Leaf.IsMatch(leaf) || RecipePlace.IsDeviceName(leaf))
                throw new ArgumentException($"the run's folder name \"{ActivityLog.Clean(leaf, 80)}\" is not one the episode cache is keyed by (A-Z a-z 0-9 . _ -, up to 64, starting with a letter or digit, not a device name); nothing was started");
            return leaf;
        }

        // ------------------------------------------------------------------ the steps that live

        private sealed class StepRecord
        {
            public string Folder;
            public int Pid, Threads;
            public DateTime StartedUtc;
        }

        private static bool ReadJson(string path, out JsonElement json)
        {
            json = default;
            FileLines f = GenomeSet.ReadFile(path);
            if (f.Absent || f.Problem != null || f.Lines.Count == 0) return false;
            try { json = Js.Parse(string.Join("\n", f.Lines)); return true; }
            catch (JsonException) { return false; }
        }

        private static IEnumerable<string> CacheFolders(RoomContext room)
        {
            string root = EpisodesRoot(room);
            if (!Directory.Exists(root)) yield break;
            string[] runDirs;
            try { runDirs = Directory.GetDirectories(root); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { yield break; }
            foreach (string runDir in runDirs)
            {
                string[] folders;
                try { folders = Directory.GetDirectories(runDir); }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { continue; }
                foreach (string f in folders)
                    if (CacheName.IsMatch(Path.GetFileName(f))) yield return f;
            }
        }

        /// <summary>Re-steps whose process the room started and that have neither ended with a result nor been stopped: their process is asked after.</summary>
        private static List<StepRecord> Live(RoomContext room)
        {
            var l = new List<StepRecord>();
            foreach (string folder in CacheFolders(room))
            {
                if (File.Exists(Path.Combine(folder, ResultFile)) || File.Exists(Path.Combine(folder, StoppedFile))) continue;
                if (!ReadJson(Path.Combine(folder, StepFile), out JsonElement m)) continue;
                double? pid = Js.Num(m, "pid"), threads = Js.Num(m, "threads");
                if (!pid.HasValue) continue;
                DateTime started = DateTime.TryParse(Js.Str(m, "startedUtc"), Inv, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime t) ? t : DateTime.MinValue;
                if (!room.Processes.IsAlive((int)pid.Value, started)) continue;
                l.Add(new StepRecord { Folder = folder, Pid = (int)pid.Value, Threads = Math.Max(1, (int)(threads ?? 1)), StartedUtc = started });
            }
            return l;
        }

        /// <summary>
        /// The threads the live re-steps count against the room's free cores: one each, for exactly as long as the process lives (not for <see cref="Cap.RecentLaunch"/>:
        /// a step ends when it ends, and its thread is free at once).
        /// </summary>
        public static int LiveThreads(RoomContext room)
        {
            lock (room.StepGate) return Live(room).Sum(r => r.Threads);
        }

        /// <summary>
        /// The timing-read file as it is now, and, when it declares a timing read, every re-step that lives stopped and marked "stopped for a timing read"
        /// (the room started it, so the room may stop it). Called before each step and at every poll: when a resource is read and when the machine panel asks.
        /// </summary>
        public static TimingReadState Poll(RoomContext room)
        {
            lock (room.StepGate)
            {
                TimingReadState t = TimingRead.Read(room);
                if (t.State != "declared") return t;
                foreach (StepRecord r in Live(room))
                {
                    string note = null;
                    try { room.Processes.Stop(r.Pid); }
                    catch (Exception e) when (!(e is OutOfMemoryException)) { note = "ending the process failed: " + ActivityLog.Clean(room.Scrub(e.Message), 200); }
                    string body = Js.Serialize(new
                    {
                        reason = StoppedForTimingRead, who = t.Who, why = t.Why, until = t.Until?.ToUniversalTime().ToString("o", Inv),
                        atUtc = DateTime.UtcNow.ToString("o", Inv), pid = r.Pid, stopNote = note,
                    });
                    File.WriteAllText(Path.Combine(r.Folder, StoppedFile), body + "\n", Utf8);
                }
                return t;
            }
        }

        /// <summary>What <c>/api/room</c> adds: the timing-read file's state (with its note, for the machine panel) and the re-steps that live.</summary>
        public static (object timingRead, object resteps) Status(RoomContext room)
        {
            TimingReadState t = Poll(room);
            int live;
            lock (room.StepGate) live = Live(room).Count;
            return (t.View(), new { running = live, threads = live });
        }

        // ------------------------------------------------------------------ the step

        private static string Sha(byte[] b) => ActivityLog.Hex(SHA256.HashData(b));

        private static string RelToRoot(RoomContext room, string full)
        {
            string rel = Path.GetRelativePath(room.Root, full);
            return Path.IsPathRooted(rel) || rel.StartsWith("..", StringComparison.Ordinal) ? null : rel.Replace('\\', '/');
        }

        /// <summary>The nursery's program, found as a run launch finds it.</summary>
        private static string NurseryDll(RoomContext room)
        {
            string dll = Path.IsPathRooted(room.Settings.NurseryDll)
                ? room.Settings.NurseryDll
                : Path.GetFullPath(Path.Combine(room.Root, room.Settings.NurseryDll.Replace('/', Path.DirectorySeparatorChar)));
            if (!File.Exists(dll))
                throw new ArgumentException($"the nursery is not built where room.json says ({room.Settings.NurseryDll}); build it first with `dotnet build src/Evosim.Nursery -c Release` (run from the repository root), then step again. Nothing was started.");
            return dll;
        }

        /// <summary>Moves a folder (and the log beside it) to <c>&lt;name&gt;.prev-N</c>, N the lowest not taken; the old files are kept, never deleted. Returns the new name.</summary>
        private static string MoveAside(string folder)
        {
            string log = folder + ".step.log";
            for (int n = 1; ; n++)
            {
                string aside = folder + ".prev-" + n.ToString(Inv);
                if (Directory.Exists(aside) || File.Exists(aside) || File.Exists(aside + ".step.log")) continue;
                if (Directory.Exists(folder)) Directory.Move(folder, aside);
                if (File.Exists(log)) File.Move(log, aside + ".step.log");
                return Path.GetFileName(aside);
            }
        }

        /// <summary>The operation <c>episode.step</c>.</summary>
        public static object Step(RoomContext room, ResourceResolver resources, JsonElement a)
        {
            string runId = (Js.Str(a, "run") ?? "").Trim();
            int b = (int)(Js.Num(a, "brain") ?? -1), d = (int)(Js.Num(a, "draw") ?? -1), t = (int)(Js.Num(a, "tank") ?? -1);
            bool fresh = Js.Bool(a, "fresh") == true;
            string reason = Js.Str(a, "reason");
            if (runId.Length == 0) throw new ArgumentException("run is a registry number or the folder name of a run the room knows");
            if (b < 0 || d < 0 || t < 0) throw new ArgumentException($"brain, draw and tank are 0 or more (the exam brain's index, the draw's, and the tank's in that draw), not {b}, {d} and {t}");

            // 1. A timing read in force refuses the step, before anything else is looked at.
            TimingReadState timing = Poll(room);
            if (timing.State == "declared")
                throw new ArgumentException($"no episode is stepped while a timing read is declared: {timing.Who ?? "(no one named)"} is timing, because {timing.Why ?? "(no reason given)"}, until {timing.Until?.ToUniversalTime().ToString("o", Inv)}. {TimingRead.RelativePath} in the main checkout holds the declaration; the session that wrote it deletes it when it is done, and the room never does. Nothing was started.");
            if (timing.State == "unreadable")
                throw new ArgumentException($"{timing.Note} ({timing.Problem}). Nothing was started.");

            // 2. The plan and the request it gives.
            ResourceResolver.RunRef run;
            ExamPlanView plan;
            try
            {
                run = resources.FindRun(runId);
                plan = (ExamPlanView)resources.ExamPlanRead(runId);
            }
            catch (ResourceNotFound e) { throw new ArgumentException(e.Message); }
            catch (ResourceRefused e) { throw new ArgumentException(room.Scrub(e.Message) + " Nothing was started."); }
            string leaf = LeafOf(run);

            if (b >= plan.Brains.Count) throw new ArgumentException($"brain {b} is not one of the exam's: it has {plan.Brains.Count} brains (0 to {plan.Brains.Count - 1}), matched by index and never by label");
            if (d >= plan.Episodes || d >= plan.Draws.Count) throw new ArgumentException($"draw {d} is not one of the exam's: it has {plan.Episodes} draws (0 to {plan.Episodes - 1})");
            ExamDrawView draw = plan.Draws[d];
            if (draw.Tanks == null) throw new ArgumentException("the plan does not know the exam's tank size, so no tank is dealt for this draw and there is nothing to re-step");
            if (t >= draw.Tanks.Count) throw new ArgumentException($"tank {t} is not one of draw {d}'s: it has {draw.Tanks.Count} tank(s) (0 to {draw.Tanks.Count - 1})");
            if (string.IsNullOrEmpty(plan.RecipeHash) || plan.RecipeFull == null)
                throw new ArgumentException("the plan names no recipe or no recipe hash (run.json's recipe, or the registry entry's), so a re-step could not be checked against the recipe the run was made from");
            if (!ulong.TryParse(plan.RunSeed ?? "", NumberStyles.None, Inv, out ulong runSeed) || !ulong.TryParse(plan.Salt ?? "", NumberStyles.None, Inv, out ulong salt))
                throw new ArgumentException("the plan's run seed or salt is not a whole number, so the episode's seed cannot be derived");
            string derived = ExamPlans.SeedsFor(runSeed, salt, 0, d + 1)[d].ToString(Inv);
            if (derived != plan.Seeds[d])
                throw new ArgumentException($"the seed the run's seed {plan.RunSeed} and salt {plan.Salt} give for draw {d} is {derived}, and the plan's is {plan.Seeds[d]}: a re-step on this request would swim another episode than the exam's. Nothing was started.");

            string poolSource = Path.Combine(run.Folder, plan.PoolFile ?? "");
            if (string.IsNullOrEmpty(plan.PoolFile) || !File.Exists(poolSource))
                throw new ArgumentException($"the run's pool file {plan.PoolFile} is not in its folder, so there are no genomes to step. Nothing was started.");
            string recipeSource = plan.RecipeFull;
            if (!File.Exists(recipeSource)) throw new ArgumentException($"the plan's recipe {plan.RecipePath} is not on this machine, so it cannot be stepped from. Nothing was started.");
            foreach (string source in new[] { poolSource, recipeSource })
                if (!UnderTree(room, source))
                    throw new ArgumentException($"{room.RelativeOrOutside(source)} lies outside this tree and the main checkout, which the room reads nothing from. Nothing was started.");

            byte[] poolBytes = ReadWhole(poolSource), recipeBytes = ReadWhole(recipeSource);
            string poolSha = Sha(poolBytes), recipeSha = Sha(recipeBytes);
            string folder = CacheFolder(room, leaf, b, d, t);
            string folderRel = RelToRoot(room, folder) ?? throw new ArgumentException("the episode cache folder lies outside the repository");
            string poolRel = RelToRoot(room, poolSource), recipeRel = RelToRoot(room, recipeSource);
            bool copyPool = poolRel == null, copyRecipe = recipeRel == null;
            if (copyPool) poolRel = folderRel + "/pool.tsv";
            if (copyRecipe) recipeRel = folderRel + "/recipe.json";
            string requestText = Js.Serialize(new
            {
                recipe = recipeRel, recipeHash = plan.RecipeHash, pool = poolRel, seed = plan.RunSeed, salt = plan.Salt,
                generation = 0, episode = d, brain = b, tank = t, members = draw.Tanks[t].ToArray(),
            });
            string uri = $"nursery://episode/{runId}/{b}/{d}/{t}";

            lock (room.StepGate)
            {
                // 3. The cache: a finished episode of this very request is served, unless a fresh one is asked for.
                string resultFile = Path.Combine(folder, ResultFile);
                bool there = Directory.Exists(folder);
                if (there && Live(room).Any(r => string.Equals(Path.GetFullPath(r.Folder), Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException($"this episode is being stepped now (read {uri} for its progress); one step of an episode at a time. Nothing was started.");
                if (there && File.Exists(resultFile) && !fresh)
                {
                    string cachedProblem = CacheMismatch(folder, requestText, poolSha, recipeSha);
                    if (cachedProblem != null)
                        throw new ArgumentException($"{folderRel} holds a finished episode that was stepped from another request or other inputs ({cachedProblem}); it is not served as this one. Ask for fresh: true to step it again (the old files are kept beside it). Nothing was started.");
                    Dictionary<string, object> view = Read(room, resources, runId, b, d, t);
                    return new
                    {
                        run = run.Id, brain = b, draw = d, tank = t, uri, cached = true, fresh = false, state = "done", folder = room.MainRelative(folder),
                        verdict = view.TryGetValue("verdict", out object verdictSeen) ? verdictSeen as string : null, pid = (int?)null, threads = 0, affinity = (string)null, droppedEnvironment = new List<string>(),
                        copied = new List<object>(), movedAside = (string)null,
                        note = "served from the cache: a finished episode of this request is already there, and nothing was stepped. fresh: true steps it again.",
                    };
                }

                // 4. The machine: the bound and the thread, then the room's slot in the queue, the heat, and the free cores.
                string noSuchCore = CoreMask.Problem(room.Settings.Affinity, room.SystemMask);
                if (noSuchCore != null) throw new ArgumentException(noSuchCore);
                string tooMany = CoreMask.ThreadsProblem(1, room.Settings.Affinity);
                if (tooMany != null) throw new ArgumentException(tooMany);
                string dll = NurseryDll(room);
                DateTime now = DateTime.UtcNow;
                Slot slot = room.ReadSlot();
                HeatReading heat = room.Heat.Read();
                int granted = room.GrantedThreads(now);
                string over = Cap.Refusal(1, slot, heat, granted);
                if (over != null) throw new ArgumentException(over + ". Nothing was started.");

                // 4b. The world: a run whose world this build cannot load is refused before anything is made or moved. Read gives the recipe's hash and
                // WorldProblem the world's refusal; the child would only fail on Load after the folder had been moved and the copies written.
                try
                {
                    Recipe asRead = Recipe.Read(recipeSource);
                    string readHash = asRead.Hash();
                    if (!string.Equals(readHash, plan.RecipeHash, StringComparison.OrdinalIgnoreCase))
                        throw new ArgumentException($"the recipe {recipeRel} hashes to {readHash}, not the {plan.RecipeHash} the run recorded: it is not the recipe the run was made from (it was edited, or its base is not where the run had it). Nothing was started.");
                    string worldProblem = asRead.WorldProblem();
                    if (worldProblem != null)
                        throw new ArgumentException($"this run's world is from an older build: re-stepping it needs a build of commit {BuildCommitOf(run)}, built in a worktree under scratch/. This build refuses the world: {room.Scrub(GenomeSet.Flat(worldProblem))}. Nothing was started.");
                }
                catch (InvalidDataException e) { throw new ArgumentException($"the nursery refuses the recipe {recipeRel}: " + room.Scrub(e.Message) + ". Nothing was started."); }

                // 5. The folder: an old one is moved aside, never written over; the copies and the request are written into a new one.
                string linked = room.LinkProblem(folder) ?? room.LinkProblem(folder + ".step.log");
                if (linked != null) throw new ArgumentException($"{folderRel} is not made: {linked}. Nothing was started.");
                string movedAside = null;
                if (there || File.Exists(folder + ".step.log")) movedAside = MoveAside(folder);
                room.EnsureDirectory(folder);
                var copied = new List<object>();
                string recipeUsed = recipeSource;
                if (copyPool) copied.Add(CopyChecked(room, "pool", poolSource, Path.Combine(folder, "pool.tsv"), poolBytes, poolRel));
                if (copyRecipe)
                {
                    copied.Add(CopyChecked(room, "recipe", recipeSource, Path.Combine(folder, "recipe.json"), recipeBytes, recipeRel));
                    recipeUsed = Path.Combine(folder, "recipe.json");
                }
                // The recipe the nursery will load, resolved as it resolves it, must be the one the run was made from.
                try
                {
                    string got = Recipe.Load(recipeUsed).Hash();
                    if (!string.Equals(got, plan.RecipeHash, StringComparison.OrdinalIgnoreCase))
                        throw new ArgumentException($"the recipe {recipeRel} hashes to {got}, not the {plan.RecipeHash} the run recorded: it is not the recipe the run was made from (it was edited, or its base is not where the run had it). Nothing was started.");
                }
                catch (InvalidDataException e) { throw new ArgumentException($"the nursery refuses the recipe {recipeRel}: " + room.Scrub(e.Message) + ". Nothing was started."); }
                File.WriteAllText(Path.Combine(folder, RequestFile), requestText + "\n", Utf8);

                // 6. Start it: pinned, with the environment cleaned, and the room does not wait.
                string log = folder + ".step.log";
                var args = new List<string> { dll, "--restep", folderRel + "/" + RequestFile, "--out", folderRel, "--threads", "1" };
                var spec = new LaunchSpec
                {
                    FileName = room.Settings.Dotnet, Arguments = args, WorkingDirectory = room.Root, Folder = folder, LogFile = log,
                    Affinity = slot.Free, CleanForRestep = true,
                };
                string badLine = CommandLine.Problem(spec);
                if (badLine != null) throw new ArgumentException(badLine);
                LaunchOutcome outcome;
                DateTime launchedUtc = DateTime.UtcNow;
                try { outcome = room.Launcher.Start(spec); }
                catch (Exception e) when (!(e is OutOfMemoryException))
                {
                    throw new InvalidOperationException("the episode could not be started: " + room.Scrub(e.Message), e);
                }
                string note = outcome.Note;
                if (outcome.Affinity != spec.Affinity)
                    note = (note == null ? "" : note + "; ") + $"the launcher applied {(outcome.Affinity == 0 ? "no affinity mask" : "the affinity mask " + CoreMask.Format(outcome.Affinity))}, not the {CoreMask.Format(spec.Affinity)} of the room's free cores";
                File.WriteAllText(Path.Combine(folder, StepFile), Js.Serialize(new
                {
                    run = run.Id, runFolder = leaf, brain = b, draw = d, tank = t, pid = outcome.Pid, startedUtc = launchedUtc.ToString("o", Inv), threads = 1,
                    affinity = outcome.Affinity == 0 ? null : CoreMask.Format(outcome.Affinity), reason = ActivityLog.Clean(reason, ActivityLog.MaxReason),
                    poolSha256 = poolSha, recipeSha256 = recipeSha, detached = outcome.Detached, launcherNote = note,
                    droppedEnvironment = (outcome.DroppedEnvironment ?? new string[0]).ToList(),
                }) + "\n", Utf8);
                return new
                {
                    run = run.Id, brain = b, draw = d, tank = t, uri, cached = false, fresh, state = "running", folder = room.MainRelative(folder),
                    verdict = (string)null, pid = (int?)outcome.Pid, threads = 1, detached = outcome.Detached, launcherNote = note,
                    affinity = outcome.Affinity == 0 ? null : CoreMask.Format(outcome.Affinity),
                    droppedEnvironment = (outcome.DroppedEnvironment ?? new string[0]).ToList(),
                    copied, movedAside,
                    command = "dotnet " + string.Join(" ", new[] { room.Display(dll) }.Concat(args.Skip(1))),
                    request = Js.Parse(requestText),
                    machine = Machine(slot, granted),
                    heat = Heat(heat),
                };
            }
        }

        private static byte[] ReadWhole(string path)
        {
            using (var s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var m = new MemoryStream())
            {
                s.CopyTo(m);
                return m.ToArray();
            }
        }

        private static bool UnderTree(RoomContext room, string full)
        {
            string f = Path.GetFullPath(full);
            foreach (string baseDir in new[] { room.Root, room.MainCheckout })
            {
                string b = Path.GetFullPath(baseDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (f.StartsWith(b, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>A file outside the tree, copied by content into the episode's folder and checked: the copy's SHA-256 must be the source's.</summary>
        private static object CopyChecked(RoomContext room, string what, string source, string target, byte[] sourceBytes, string targetRel)
        {
            string want = Sha(sourceBytes);
            room.Copier.Copy(source, target);
            string got = Sha(ReadWhole(target));
            if (got != want)
                throw new ArgumentException($"the copy of the {what} in the episode's folder ({targetRel}) hashes to {got}, and the source {room.RelativeOrOutside(source)} to {want}: the copy is not the file, so nothing was started");
            return new { what, from = room.RelativeOrOutside(source), to = targetRel, sha256 = got };
        }

        /// <summary>Why a finished episode in the cache is not of this request, or null. A folder without a record of its inputs is taken as it is, and the resource says so.</summary>
        private static string CacheMismatch(string folder, string requestText, string poolSha, string recipeSha)
        {
            string requestFile = Path.Combine(folder, RequestFile);
            if (File.Exists(requestFile))
            {
                FileLines f = GenomeSet.ReadFile(requestFile);
                string have = f.Lines == null || f.Lines.Count == 0 ? null : f.Lines[0];
                if (have != requestText) return "its request.json is not the request this draw gives now";
            }
            if (ReadJson(Path.Combine(folder, StepFile), out JsonElement m))
            {
                string p = Js.Str(m, "poolSha256"), r = Js.Str(m, "recipeSha256");
                if (p != null && p != poolSha) return "the pool file is not the one it was stepped from";
                if (r != null && r != recipeSha) return "the recipe file is not the one it was stepped from";
            }
            return null;
        }

        // ------------------------------------------------------------------ the resource

        /// <summary>A parsed <c>nursery://episode/{run}/{b}/{d}/{t}</c>: the run is everything before the last three parts.</summary>
        public static bool TryParseAddress(string tail, out string run, out int b, out int d, out int t)
        {
            run = null; b = d = t = 0;
            string[] parts = tail.Split('/');
            if (parts.Length < 4) return false;
            int n = parts.Length;
            if (!int.TryParse(parts[n - 3], NumberStyles.None, Inv, out b) || !int.TryParse(parts[n - 2], NumberStyles.None, Inv, out d) || !int.TryParse(parts[n - 1], NumberStyles.None, Inv, out t)) return false;
            run = string.Join("/", parts.Take(n - 3));
            return run.Length > 0;
        }

        /// <summary>
        /// Why a brain, draw and tank are not one the run's exam has, or null: the tank screen's own words (<c>the address asked for brain 99, but this run has 18 brains (0 to 17)</c>),
        /// the first of the three that is out of range.
        /// </summary>
        public static string AddressProblem(ExamPlanView plan, int b, int d, int t)
        {
            string Over(string what, int asked, int count) =>
                $"the address asked for {what} {asked}, but this run has {count} {what}{(count == 1 ? "" : "s")}" + (count > 0 ? $" (0 to {count - 1})" : "");
            if (b >= plan.Brains.Count) return Over("brain", b, plan.Brains.Count);
            if (d >= plan.Draws.Count) return Over("draw", d, plan.Draws.Count);
            List<List<int>> tanks = plan.Draws[d].Tanks;
            if (tanks == null) return $"draw {d} has no tank dealt in the plan, so there is no episode to read";
            if (t >= tanks.Count) return Over("tank", t, tanks.Count);
            return null;
        }

        private static List<string> Tail(string path, int lines)
        {
            FileLines f = GenomeSet.ReadFile(path);
            if (f.Lines == null) return new List<string>();
            return f.Lines.Skip(Math.Max(0, f.Lines.Count - lines)).Select(l => ActivityLog.Clean(l, 300)).ToList();
        }

        /// <summary>The episode's state as a resource gives it. Polls first, so that reading it while a timing read is declared stops a step that lives.</summary>
        public static Dictionary<string, object> Read(RoomContext room, ResourceResolver resources, string runId, int b, int d, int t)
        {
            TimingReadState timing = Poll(room);
            ResourceResolver.RunRef run = resources.FindRun(runId);
            // A run whose exam plan is refused has no episode to read: the read is refused with the plan's own reason, as the other reads are, and it says nothing of
            // the cache's folders (an episode's folder is named only where the plan lets there be one).
            ExamPlanView plan;
            try { plan = (ExamPlanView)resources.ExamPlanRead(runId); }
            catch (ResourceNotFound e) { throw new ResourceNotFound($"no episode of run {run.Id} can be read: {e.Message}"); }
            catch (ResourceRefused e) { throw new ResourceRefused($"no episode of run {run.Id} can be read: {e.Message}"); }
            // A brain, draw or tank the run does not have is refused, in the words the tank screen's notice uses for the same address.
            string outOfRange = AddressProblem(plan, b, d, t);
            if (outOfRange != null) throw new ResourceRefused(outOfRange);
            string leaf;
            try { leaf = LeafOf(run); }
            catch (ArgumentException e) { throw new ResourceRefused(e.Message); }
            string folder = CacheFolder(room, leaf, b, d, t);
            var v = new Dictionary<string, object>
            {
                ["run"] = run.Id, ["brain"] = b, ["draw"] = d, ["tank"] = t, ["uri"] = $"nursery://episode/{runId}/{b}/{d}/{t}",
                ["folder"] = room.MainRelative(folder), ["timingRead"] = timing.View(),
            };
            string resultFile = Path.Combine(folder, ResultFile);
            bool hasStep = ReadJson(Path.Combine(folder, StepFile), out JsonElement step);
            if (hasStep)
            {
                v["startedUtc"] = Js.Str(step, "startedUtc");
                v["pid"] = Js.Num(step, "pid");
                v["reason"] = Js.Str(step, "reason");
                v["affinity"] = Js.Str(step, "affinity");
            }

            if (File.Exists(resultFile))
            {
                v["state"] = "done";
                FillDone(room, resources, v, run, runId, b, d, t, folder);
                return v;
            }
            if (!Directory.Exists(folder) || !hasStep)
            {
                v["state"] = "none";
                v["note"] = Directory.Exists(folder)
                    ? "the cache folder holds no step record and no result: nothing is stepping here"
                    : "no episode has been stepped for this brain, draw and tank; episode.step starts one";
                return v;
            }
            if (File.Exists(Path.Combine(folder, StoppedFile)))
            {
                v["state"] = "stopped";
                ReadJson(Path.Combine(folder, StoppedFile), out JsonElement s);
                v["reason"] = Js.Str(s, "reason") ?? "stopped";
                v["stoppedBy"] = new { who = Js.Str(s, "who"), why = Js.Str(s, "why"), until = Js.Str(s, "until"), atUtc = Js.Str(s, "atUtc"), note = Js.Str(s, "stopNote") };
                v["stepReason"] = Js.Str(step, "reason");
                v["progress"] = Progress(folder);
                return v;
            }
            DateTime started = DateTime.TryParse(Js.Str(step, "startedUtc"), Inv, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime st) ? st : DateTime.MinValue;
            int pid = (int)(Js.Num(step, "pid") ?? 0);
            if (room.Processes.IsAlive(pid, started))
            {
                v["state"] = "running";
                v["progress"] = Progress(folder);
                return v;
            }
            v["state"] = "failed";
            v["note"] = "the process ended without writing result.json (the nursery writes it last, so a missing one means the episode did not finish); its log's tail is below";
            v["logTail"] = Tail(folder + ".step.log", 20);
            v["progress"] = Progress(folder);
            return v;
        }

        /// <summary>How far the stream has got: the size of frames.bin, and, when episode.json gives the frame size, the frames. (episode.json is written when the episode ends, so a step still running gives the bytes only.)</summary>
        private static object Progress(string folder)
        {
            string frames = Path.Combine(folder, FramesFile);
            long bytes = 0;
            try { if (File.Exists(frames)) bytes = new FileInfo(frames).Length; }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
            double? floats = ReadJson(Path.Combine(folder, EpisodeFile), out JsonElement ep) ? Js.Num(ep, "frameFloats") : null;
            long? n = floats.HasValue && floats.Value > 0 ? (long?)(bytes / (4L * (long)floats.Value)) : null;
            return new { framesBytes = bytes, frameFloats = floats, frames = n };
        }

        // ------------------------------------------------------------------ done: the comparison and the provenance

        private sealed class Expected
        {
            public bool Known;
            public string Net, Bits, Note;
        }

        private static string BitsOf(double v) => BitConverter.DoubleToInt64Bits(v).ToString("x16", Inv);

        private static void FillDone(RoomContext room, ResourceResolver resources, Dictionary<string, object> v, ResourceResolver.RunRef run, string runId, int b, int d, int t, string folder)
        {
            if (!ReadJson(Path.Combine(folder, ResultFile), out JsonElement result) || Js.Bool(result, "complete") != true)
            {
                v["state"] = "failed";
                v["note"] = "result.json is there and cannot be read as a finished result";
                v["verdict"] = null;
                return;
            }
            v["verdict"] = "UNCOMPARED";
            v["episodeSeed"] = Js.Str(result, "episodeSeed");
            v["control"] = Js.Str(result, "control");

            ExamPlanView plan = null;
            string planProblem = null;
            try { plan = (ExamPlanView)resources.ExamPlanRead(runId); }
            catch (ResourceNotFound e) { planProblem = e.Message; }
            catch (ResourceRefused e) { planProblem = e.Message; }

            JsonElement episode = default;
            bool hasEpisode = ReadJson(Path.Combine(folder, EpisodeFile), out episode);
            v["provenance"] = Provenance(room, run, plan, hasEpisode ? episode : (JsonElement?)null, planProblem);

            var slots = new List<object>();
            var uncompared = new List<string>();
            var differs = new List<int>();
            int equal = 0;
            List<JsonElement> members = Js.Items(Js.At(result, "members")).ToList();
            string why = planProblem;
            if (plan == null) why = "the exam plan cannot be read: " + planProblem;
            else if (d >= plan.Draws.Count || plan.Draws[d].Tanks == null || t >= plan.Draws[d].Tanks.Count) why = "the plan holds no such tank";
            else if (b >= plan.Brains.Count) why = "the plan holds no such brain";

            if (why != null)
            {
                foreach (JsonElement m in members) slots.Add(new { slot = (int)(Js.Num(m, "slot") ?? -1), pool = (int)(Js.Num(m, "pool") ?? -1), actual = new { net = Js.Str(m, "net"), bits = Js.Str(m, "bits") }, equal = (bool?)null });
                v["comparison"] = new { verdict = "UNCOMPARED", reason = why, slots, differs, equal };
                v["verdict"] = "UNCOMPARED";
                return;
            }

            List<int> tank = plan.Draws[d].Tanks[t];
            int G = plan.Genomes.Count;
            FileLines rows = GenomeSet.ReadFile(Path.Combine(run.Folder, "tank-" + plan.Name + ".jsonl"));
            string rowsProblem = rows.Absent ? $"tank-{plan.Name}.jsonl is not in the run" : rows.Problem != null ? $"tank-{plan.Name}.jsonl could not be read ({rows.Problem})" : null;

            string requestNote = null;
            bool? requestMatches = null;
            FileLines req = GenomeSet.ReadFile(Path.Combine(folder, RequestFile));
            if (req.Lines != null && req.Lines.Count > 0)
            {
                try
                {
                    JsonElement r = Js.Parse(req.Lines[0]);
                    int[] stepped = Js.Items(Js.At(r, "members")).Select(x => (int)x.GetDouble()).ToArray();
                    requestMatches = stepped.SequenceEqual(tank) && (int)(Js.Num(r, "brain") ?? -1) == b && (int)(Js.Num(r, "episode") ?? -1) == d && Js.Str(r, "seed") == plan.RunSeed && Js.Str(r, "salt") == plan.Salt;
                    if (requestMatches == false) requestNote = "the request this episode was stepped from is not what the plan gives for this brain, draw and tank";
                }
                catch (Exception e) when (e is JsonException || e is InvalidOperationException || e is FormatException) { requestNote = "request.json cannot be read"; requestMatches = false; }
            }

            if (members.Count != tank.Count) uncompared.Add($"the result holds {members.Count} members, and the plan's tank {tank.Count}");
            for (int i = 0; i < members.Count; i++)
            {
                JsonElement m = members[i];
                int slot = (int)(Js.Num(m, "slot") ?? i), pool = (int)(Js.Num(m, "pool") ?? -1);
                string net = Js.Str(m, "net"), bits = Js.Str(m, "bits");
                bool landed = Js.Bool(m, "landed") == true;
                string slotNote = null;

                // The re-stepped value: its net's own 64 bits; the file's bits, when it gives them, must be those.
                string actualBits = null;
                bool actualNull = net == null;
                if (!actualNull)
                {
                    if (!double.TryParse(net, NumberStyles.Float, Inv, out double parsed)) { slotNote = "the net is not a number"; actualNull = true; }
                    else actualBits = BitsOf(parsed);
                }
                if (net != null && actualBits != null && bits != null && !string.Equals(bits, actualBits, StringComparison.OrdinalIgnoreCase))
                    slotNote = $"result.json's bits {bits} are not the bits of its net {net} ({actualBits})";
                if (net == null && bits != null) slotNote = "result.json gives bits for a net it gives as null";

                Expected ex = ExpectedOf(rows, rowsProblem, plan, b, d, pool, G);
                bool? same = null;
                if (slotNote != null) same = false;
                else if (ex.Known)
                    same = (ex.Net == null && actualBits == null && net == null) || (ex.Net != null && actualBits != null && ex.Bits == actualBits);
                if (same == true) equal++;
                else if (same == false) differs.Add(slot);
                else uncompared.Add($"slot {slot}: {ex.Note}");
                slots.Add(new
                {
                    slot, pool,
                    genome = pool >= 0 && pool < plan.Genomes.Count ? plan.Genomes[pool].Id : null,
                    body = pool >= 0 && pool < plan.Genomes.Count ? (int?)plan.Genomes[pool].Body : null,
                    expected = new { known = ex.Known, net = ex.Net, bits = ex.Bits, note = ex.Note },
                    actual = new { landed, net, bits = actualBits ?? bits, survived = Js.Bool(m, "survived") },
                    equal = same, note = slotNote,
                });
            }

            string verdict;
            string reason = null;
            if (differs.Count > 0)
            {
                verdict = "DIFFERS";
                reason = "slot " + string.Join(", ", differs) + " differ" + (differs.Count == 1 ? "s" : "") + " from the run's exam, bit for bit";
            }
            else if (uncompared.Count > 0) { verdict = "UNCOMPARED"; reason = string.Join("; ", uncompared); }
            else if (requestMatches == false) { verdict = "UNCOMPARED"; reason = requestNote; }
            else if (members.Count == 0) { verdict = "UNCOMPARED"; reason = "the result holds no member"; }
            else verdict = "EXACT";
            v["verdict"] = verdict;
            v["comparison"] = new
            {
                verdict, reason, equal, of = members.Count, differs, slots,
                requestMatchesPlan = requestMatches, requestNote,
                rule = "a slot is equal when its net has the same 64 bits as the run's per[d] (the nursery's round-trip text of the same double), or both are null (a body that did not land); there is no tolerance. EXACT only when every slot is equal.",
                exam = new { file = $"tank-{plan.Name}.jsonl", brain = b, brainLabel = plan.Brains[b].Label, draw = d, tank = t, seed = plan.Draws[d].Seed },
            };
        }

        /// <summary>The run's own number for one member under one brain on one draw: row <c>b * G + genome</c> of the exam's rows, its <c>per[d]</c>.</summary>
        private static Expected ExpectedOf(FileLines rows, string rowsProblem, ExamPlanView plan, int b, int d, int pool, int G)
        {
            if (rowsProblem != null) return new Expected { Note = rowsProblem };
            if (pool < 0 || pool >= G) return new Expected { Note = "the member is not one of the plan's genomes" };
            int k = b * G + pool;
            if (k >= rows.Lines.Count) return new Expected { Note = $"the run's exam rows hold {rows.Lines.Count} of {plan.Brains.Count * G} rows so far, and this member's (row {k + 1}) is not there" };
            JsonElement row;
            try { row = Js.Parse(rows.Lines[k]); }
            catch (JsonException) { return new Expected { Note = $"row {k + 1} of the run's exam rows is not JSON" }; }
            double? genome = Js.Num(row, "genome"), body = Js.Num(row, "body");
            if (!genome.HasValue || (int)genome.Value != pool || !body.HasValue || (int)body.Value != plan.Genomes[pool].Body)
                return new Expected { Note = $"row {k + 1} of the run's exam rows is not genome {pool} of brain {b} (it names genome {genome}, body {body})" };
            JsonElement? per = Js.At(row, "per");
            if (!per.HasValue || per.Value.ValueKind != JsonValueKind.Array || d >= per.Value.GetArrayLength())
                return new Expected { Note = $"row {k + 1} holds no per[{d}]" };
            JsonElement x = per.Value[d];
            if (x.ValueKind == JsonValueKind.Null) return new Expected { Known = true };
            if (x.ValueKind != JsonValueKind.Number || !double.TryParse(x.GetRawText(), NumberStyles.Float, Inv, out double value))
                return new Expected { Note = $"row {k + 1}'s per[{d}] is not a number or null" };
            return new Expected { Known = true, Net = x.GetRawText(), Bits = BitsOf(value) };
        }

        /// <summary>The commit a run was made on: run.json's build, else its registry entry's; "(not recorded)" when neither says.</summary>
        private static string BuildCommitOf(ResourceResolver.RunRef run)
        {
            if (run.Folder != null && ReadJson(Path.Combine(run.Folder, "run.json"), out JsonElement runJson))
            {
                string c = Js.Str(runJson, "build", "gitCommit");
                if (!string.IsNullOrEmpty(c)) return c;
            }
            return string.IsNullOrEmpty(run.Entry?.GitCommit) ? "(not recorded)" : run.Entry.GitCommit;
        }

        private static readonly string[] BuildKeys = { "gitCommit", "coreHash", "dynamicsHash", "farmHash", "nurseryHash" };

        /// <summary>The episode's build next to the run's, and where the plan came from.</summary>
        private static object Provenance(RoomContext room, ResourceResolver.RunRef run, ExamPlanView plan, JsonElement? episode, string planProblem)
        {
            var episodeBuild = new Dictionary<string, object>();
            var runBuild = new Dictionary<string, object>();
            string runBuildFrom = null;
            JsonElement? eb = Js.At(episode, "build");
            if (eb.HasValue)
            {
                foreach (string k in BuildKeys) episodeBuild[k] = Js.Str(eb, k);
                episodeBuild["gitDirty"] = Js.Bool(eb, "gitDirty");
                episodeBuild["runtime"] = Js.Str(eb, "runtime");
                episodeBuild["tieredCompilation"] = Js.Str(eb, "tieredCompilation");
                episodeBuild["note"] = Js.Str(eb, "note");
            }
            if (run.Folder != null && ReadJson(Path.Combine(run.Folder, "run.json"), out JsonElement runJson) && Js.At(runJson, "build").HasValue)
            {
                foreach (string k in BuildKeys) runBuild[k] = Js.Str(runJson, "build", k);
                runBuild["gitDirty"] = Js.Bool(runJson, "build", "gitDirty");
                runBuildFrom = "run.json";
            }
            else if (run.Entry != null)
            {
                runBuild["gitCommit"] = run.Entry.GitCommit; runBuild["coreHash"] = run.Entry.CoreHash; runBuild["dynamicsHash"] = run.Entry.DynamicsHash;
                runBuild["farmHash"] = run.Entry.FarmHash; runBuild["nurseryHash"] = run.Entry.NurseryHash;
                runBuildFrom = "registry entry " + run.Id;
            }
            // Which trees differ: only where both sides give one. A different tree is a different build, and a mismatch under it says nothing of the run.
            var differ = new List<string>();
            var unknown = new List<string>();
            foreach (string k in BuildKeys)
            {
                string e = episodeBuild.TryGetValue(k, out object ev) ? ev as string : null, r = runBuild.TryGetValue(k, out object rv) ? rv as string : null;
                if (string.IsNullOrEmpty(e) || string.IsNullOrEmpty(r)) unknown.Add(k);
                else if (!string.Equals(e, r, StringComparison.OrdinalIgnoreCase)) differ.Add(k);
            }
            bool? same = differ.Count > 0 ? false : unknown.Count > 0 ? (bool?)null : true;
            return new
            {
                planSource = plan == null ? null : plan.Source == "record" ? "recorded" : "rebuilt",
                planSourceNote = plan == null ? "the exam plan cannot be read: " + planProblem
                    : plan.Source == "record" ? "the plan is the run's own record (run.json's exam plan)" : "the plan is rebuilt from the registry entry's recipe, its pool file and the nursery's own deal; the run recorded none",
                entry = plan?.Entry,
                episodeBuild, runBuild, runBuildFrom,
                buildsDiffer = differ, buildsNotComparable = unknown,
                sameBuild = same,
            };
        }

        /// <summary>The files of a finished episode the tank screen plays, by the name a query gives them.</summary>
        public static readonly string[] ServedFiles = { "episode", "events", "frames" };

        /// <summary>
        /// One file of a <b>finished</b> episode's cache folder, for <c>/api/episode-file</c>: only the three the nursery writes for a client to play
        /// (<c>episode.json</c>, <c>events.jsonl</c>, <c>frames.bin</c>), and only once <c>result.json</c> is there (a stream still being written is not served).
        /// A read like <c>/api/genome-file</c>: nothing is written, and a link on the way refuses.
        /// </summary>
        public static (string path, string contentType) FileOf(RoomContext room, ResourceResolver resources, string runId, int b, int d, int t, string which)
        {
            string file, type;
            switch (which)
            {
                case "episode": file = EpisodeFile; type = "application/json; charset=utf-8"; break;
                case "events": file = "events.jsonl"; type = "application/x-ndjson; charset=utf-8"; break;
                case "frames": file = FramesFile; type = "application/octet-stream"; break;
                default: throw new ResourceRefused("file is one of " + string.Join(", ", ServedFiles));
            }
            ResourceResolver.RunRef run = resources.FindRun(runId);
            string leaf;
            try { leaf = LeafOf(run); }
            catch (ArgumentException e) { throw new ResourceRefused(e.Message); }
            string folder = CacheFolder(room, leaf, b, d, t);
            if (!File.Exists(Path.Combine(folder, ResultFile)))
                throw new ResourceNotFound($"no finished episode for run {runId}, brain {b}, draw {d}, tank {t}: result.json is not there, and the room serves a stream only once it is");
            string path = Path.Combine(folder, file);
            string linked = room.LinkProblem(folder) ?? room.LinkProblem(path);
            if (linked != null) throw new ResourceRefused(room.Scrub(linked) + ". Nothing was served.");
            if (!File.Exists(path)) throw new ResourceNotFound($"the finished episode holds no {file}");
            return (path, type);
        }

        /// <summary>
        /// What would refuse a step now, for the screen's STEP button to say before it is pressed (the operation itself checks again): a declared timing read
        /// (or a timing-read file that cannot be read), then the cap: the room's slot in the queue, the heat, and the free cores against the threads already
        /// granted. Null when neither refuses.
        /// </summary>
        public static object StepRefusal(RoomContext room, Slot slot, HeatReading heat, int grantedThreads)
        {
            TimingReadState t = TimingRead.Read(room);
            if (t.State == "declared")
                return new { kind = "timing", reason = $"a timing read is declared: {t.Who ?? "(no one named)"} is timing, because {t.Why ?? "(no reason given)"}, until {t.Until?.ToUniversalTime().ToString("o", Inv)}; no episode is stepped" };
            if (t.State == "unreadable")
                return new { kind = "timing", reason = t.Note };
            string over = Cap.Refusal(1, slot, heat, grantedThreads);
            return over == null ? null : new { kind = "cap", reason = over };
        }

        /// <summary>What a start's result says of the machine: the room's slot, the free cores the start was checked against, and the threads already granted.</summary>
        public static object Machine(Slot slot, int granted) => new
        {
            slot = CoreMask.List(slot.Room), free = CoreMask.List(slot.Free), freeCount = CoreMask.Count(slot.Free), granted,
            claims = slot.Claims.Select(c => new { line = c.Job.Text, cores = CoreMask.List(c.Inside) }).ToList(),
        };

        /// <summary>What a start's result says of the heat: the verdict and its line, and, for WARN, that it did not refuse.</summary>
        public static object Heat(HeatReading heat) => new
        {
            verdict = heat.Verdict, line = heat.Line,
            note = heat.Verdict == HeatReading.Warn ? "the heat check reads WARN; WARN does not refuse, so this was started, and the heat is worth watching" : null,
        };

        /// <summary>The episodes the cache holds, as resources a client can list.</summary>
        public static IEnumerable<(string uri, string name)> Cached(RoomContext room)
        {
            foreach (string folder in CacheFolders(room).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                string run = Path.GetFileName(Path.GetDirectoryName(folder));
                Match m = Regex.Match(Path.GetFileName(folder), @"\Ab(\d+)-d(\d+)-t(\d+)\z");
                if (!m.Success) continue;
                yield return ($"nursery://episode/{run}/{m.Groups[1].Value}/{m.Groups[2].Value}/{m.Groups[3].Value}", $"episode {run} b{m.Groups[1].Value} d{m.Groups[2].Value} t{m.Groups[3].Value}");
            }
        }
    }

    public sealed partial class OperationList
    {
        private void AddEpisodeOperations()
        {
            Add(new Operation
            {
                Family = "episode", Action = "step", Mutates = true,
                Summary = "Step one tank of one draw of a run's exam again, with the nursery's own code, as a process of its own pinned to the free cores of the room's slot in the machine's queue (--restep, one thread), and write its frames and its result into the episode's cache folder (<runsDir>/episodes/<run folder>/b<brain>-d<draw>-t<tank>). The request is built from nursery://exam-plan/{run}; read nursery://episode/{run}/{brain}/{draw}/{tank} for its progress and, when it is done, the comparison of each member's net with the run's own per[draw], bit for bit (EXACT only when every member is equal). A finished episode of the same request is served from the cache and not stepped again, unless fresh is true (the old folder is kept beside it, never written over). It counts 1 thread against the room's free cores for as long as its process lives, and is refused when the machine's queue gives the room no slot, the heat check reads HOT or cannot read, or no core is free. Refused while a timing read is declared (scratch/machine/timing-read.json in the main checkout, which the room only reads), and a step that lives when one is declared is stopped.",
                Schema = @"{""type"":""object"",""properties"":{""run"":{""type"":""string"",""description"":""The run: a registry number, or the folder name of a run the room knows.""},""brain"":{""type"":""integer"",""description"":""The exam brain's index in the plan (never its label).""},""draw"":{""type"":""integer"",""description"":""The exam draw: 0 to the plan's episodes minus 1. The draw fixes the episode's seed.""},""tank"":{""type"":""integer"",""description"":""The tank's index within that draw's tanks (0 for an exam that holds all its genomes in one).""},""fresh"":{""type"":""boolean"",""description"":""Step it again even though a finished episode of this request is in the cache.""}},""required"":[""run"",""brain"",""draw"",""tank""],""additionalProperties"":false}",
                Handler = a => EpisodeSteps.Step(_room, _resources, a),
                Record = r => Pick(r, "run", "brain", "draw", "tank", "uri", "cached", "fresh", "state", "folder", "pid", "threads", "affinity", "heat", "droppedEnvironment", "copied", "movedAside", "verdict"),
            });
        }
    }
}
