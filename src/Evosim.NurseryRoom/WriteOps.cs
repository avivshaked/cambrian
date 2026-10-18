using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Evosim.Nursery;

namespace Evosim.NurseryRoom
{
    /// <summary>Where a recipe may lie for the nursery to enter its run in the registry: a plain file directly under rounds/nursery/.</summary>
    public static class RecipePlace
    {
        private static readonly Regex RegisteredFile = new Regex(@"\Arounds/nursery/[A-Za-z0-9][A-Za-z0-9._-]*\.json\z", RegexOptions.IgnoreCase);
        private static readonly Regex DeviceName = new Regex(@"\A(con|prn|aux|nul|com[1-9]|lpt[1-9])\z", RegexOptions.IgnoreCase);

        /// <summary>Windows treats these names as devices with any extension (con.json is the console), so none is a file or a folder name.</summary>
        public static bool IsDeviceName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            int dot = name.IndexOf('.');
            return DeviceName.IsMatch(dot < 0 ? name : name.Substring(0, dot));
        }

        /// <summary>True for a plain recipe file directly under rounds/nursery/ (not a device name, not the room's own settings): the nursery enters such a run in the registry.</summary>
        public static bool IsRegistered(string rel) =>
            rel != null && RegisteredFile.IsMatch(rel) && !IsDeviceName(Path.GetFileName(rel))
            && !string.Equals(Path.GetFileName(rel), "room.json", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Why a recipe may not be launched, or null; <paramref name="trial"/> is true for a recipe outside rounds/nursery/, which runs unregistered.
        /// Inside rounds/nursery/ only a plain file directly there is launched.
        /// </summary>
        public static string LaunchProblem(string rel, out bool trial)
        {
            trial = false;
            if (IsRegistered(rel)) return null;
            if (string.Equals(rel, "rounds/nursery/room.json", StringComparison.OrdinalIgnoreCase)) return $"{rel} is the room's own settings, not a recipe";
            if (rel.StartsWith("rounds/nursery/", StringComparison.OrdinalIgnoreCase))
                return $"{rel} is not launched: the room launches only plain files directly under rounds/nursery/ (a .json file named with A-Z a-z 0-9 . _ - only, not a device name), not one in a subfolder.";
            trial = true;
            return null;
        }
    }

    /// <summary>
    /// The room's writes (phase 2): a recipe file written under a hash guard, the diff of two recipes, a run launched,
    /// rerun or stopped, and the comparison of two runs' files. Each is an operation in the one list; nothing here
    /// knows which experiment is running or what its recipe sets.
    /// </summary>
    public sealed partial class OperationList
    {
        // Anchored with \A and \z: a $ would let a trailing line break through, and a name goes into a file path and a command line.
        private static readonly Regex RunName = new Regex(@"\A[a-z0-9][a-z0-9-]{0,63}\z");
        private static readonly Regex RecipeFile = new Regex(@"\Arounds/nursery/[A-Za-z0-9][A-Za-z0-9._-]*\.json\z", RegexOptions.IgnoreCase);
        private static readonly Regex RawRecipePath = new Regex(@"\A[A-Za-z0-9._/\\-]+\z");

        private const int MaxRecipeChars = 262144;

        /// <summary>
        /// Why a recipe path as the caller sent it is refused before it is resolved, or null: a character outside A-Z a-z 0-9 . _ - and the
        /// separators, or a trailing dot (Windows drops it, so "x.json." would name x.json while no check of the text saw that).
        /// </summary>
        internal static string RawPathProblem(string raw)
        {
            if (raw == null || !RawRecipePath.IsMatch(raw))
                return $"the path \"{ActivityLog.Clean(raw, 80)}\" holds a character outside A-Z a-z 0-9 . _ - and the separators";
            if (raw.EndsWith(".", StringComparison.Ordinal))
                return $"the path \"{ActivityLog.Clean(raw, 80)}\" ends in a dot, which Windows drops from a file name; give the name as the file has it";
            return null;
        }

        private static bool IsDeviceName(string name) => RecipePlace.IsDeviceName(name);

        /// <summary>True for a plain recipe file directly under the trials folder (room.json's trialsDir): the one other folder a recipe is written into.</summary>
        private bool IsTrialRecipeFile(string rel) =>
            Regex.IsMatch(rel ?? "", @"\A" + Regex.Escape(_room.Settings.TrialsDir) + @"/[A-Za-z0-9][A-Za-z0-9._-]*\.json\z", RegexOptions.IgnoreCase);

        /// <summary>
        /// A recipe file's name as a path the room writes, or the reason it does not: only A-Z a-z 0-9 . _ - and the separators in what the caller
        /// sent (a space, a colon or a line break is refused where it stands, not normalised away), directly under rounds/nursery/ or directly under
        /// the trials folder (room.json's trialsDir), a .json file, not a device name. Every rule is the same in both folders.
        /// </summary>
        private string RecipeNameProblem(string raw, string rel)
        {
            string rawProblem = RawPathProblem(raw);
            if (rawProblem != null) return rawProblem + "; a recipe's file name is plain";
            if (!RecipeFile.IsMatch(rel) && !IsTrialRecipeFile(rel))
                return $"{rel} is not a recipe the room takes: a recipe is a .json file directly under rounds/nursery/ or directly under the trials folder {_room.Settings.TrialsDir}/, named with A-Z a-z 0-9 . _ - only, starting with a letter or digit";
            if (IsDeviceName(Path.GetFileName(rel)))
                return $"{rel} is named for a Windows device (con, prn, aux, nul, com1-9, lpt1-9, with any extension), which a file cannot be";
            if (string.Equals(Path.GetFileName(rel), "room.json", StringComparison.OrdinalIgnoreCase))
                return $"{rel} is the room's own settings, not a recipe";
            return null;
        }

        // ------------------------------------------------------------------ recipe.write, recipe.diff

        private void AddRecipeWrites()
        {
            string trials = _room.Settings.TrialsDir;
            Add(new Operation
            {
                Family = "recipe", Action = "write", Mutates = true,
                Summary = "Write a recipe file directly under rounds/nursery/ or directly under the trials folder (" + trials + "/, a gitignored folder: a recipe launched from there runs as an unregistered trial). The text is checked by the nursery first and nothing is written if it refuses. To replace a file, expectHash is the SHA-256 of the file as it is now (recipe.check and the recipe resource return it), so that two writers cannot overwrite each other blindly; a new file takes none.",
                Schema = @"{""type"":""object"",""properties"":{""path"":{""type"":""string"",""description"":""The recipe file, directly under rounds/nursery/ or under " + trials + @"/ and ending .json (rounds/nursery/trial.json).""},""text"":{""type"":""string"",""description"":""The recipe's JSON, whole.""},""expectHash"":{""type"":""string"",""description"":""SHA-256 of the file's bytes now, when it exists. Required to replace a file, refused for a new one.""}},""required"":[""path"",""text""],""additionalProperties"":false}",
                PathArgs = new[] { "path" }, HashArgs = new[] { "text" },
                Handler = a => RecipeWrite(Js.Str(a, "path"), Js.Str(a, "text"), Js.Str(a, "expectHash")),
                Record = r => Pick(r, "path", "hash", "created", "fileSha256"),
            });

            Add(new Operation
            {
                Family = "recipe", Action = "diff", Mutates = false,
                Summary = "The resolved difference of two recipes, field by field: path and other, or path and its own base when other is left out. Both are resolved first, each with its base merged under it. Has no side effect.",
                Schema = @"{""type"":""object"",""properties"":{""path"":{""type"":""string"",""description"":""The recipe file, relative to the repository root.""},""other"":{""type"":""string"",""description"":""The recipe to compare it with. Left out: the recipe's own base.""}},""required"":[""path""],""additionalProperties"":false}",
                PathArgs = new[] { "path", "other" },
                Handler = a => RecipeDiff(Js.Str(a, "path"), Js.Str(a, "other")),
            });
        }

        /// <summary>The full path and the repository-relative path of a recipe file the room may write: directly under rounds/nursery/, a .json file with a plain name, not the room's own settings.</summary>
        private string RecipeTarget(string path, out string rel)
        {
            string full = _room.ResolveInRepo(path, out string problem);
            if (full == null) throw new ArgumentException(problem);
            rel = Path.GetRelativePath(_room.Root, full).Replace('\\', '/');
            string bad = RecipeNameProblem(path, rel);
            if (bad != null) throw new ArgumentException(bad);
            return full;
        }

        /// <summary>
        /// Why a recipe file may not be replaced, or null: the registry's record depends on it when an entry names it as its recipe, or names a recipe
        /// under rounds/nursery/ that has it in its base chain. An entry's recipe hash is of the resolved recipe, so editing a base changes what the
        /// entry's run would be rerun as. The registry unreadable is a refusal too, since the check cannot be made.
        /// </summary>
        private string ReplaceRefusal(string rel)
        {
            string active = ActiveRunRefusal(rel);
            if (active != null) return active;
            List<RegistryEntry> all;
            try { all = RegistryEntry.ReadAll(_room.RegistryFile); }
            catch (InvalidDataException bad) { return "the registry cannot be read (" + _room.Scrub(bad.Message) + "), so whether the record depends on this recipe cannot be checked"; }
            for (int i = 0; i < all.Count; i++)
            {
                string recipe = NormalRecipePath(all[i].RecipePath);
                if (recipe == null) continue;
                if (string.Equals(recipe, rel, StringComparison.OrdinalIgnoreCase)) return $"registry entry {i + 1} ({all[i].Recipe}) names {rel} as its recipe";
                foreach (string baseRel in BaseChain(recipe))
                    if (string.Equals(baseRel, rel, StringComparison.OrdinalIgnoreCase))
                        return $"registry entry {i + 1} ({all[i].Recipe}) names {recipe}, which has {rel} in its base chain";
            }
            return null;
        }

        /// <summary>
        /// Why a recipe file may not be replaced while a run uses it, or null: a run this room launched that has not ended (its run.json says
        /// running with a process that lives or is not known gone, or it began in the last <see cref="Cap.RecentLaunch"/> and has no run.json yet),
        /// or any run.json under the folders the room knows (room.json's runs, and the runs folder of this tree and of the main checkout) with
        /// status running, that names the file as its recipe or has it in its base chain. A run.json that is there and cannot be read is a refusal
        /// too, since whether that run uses the file cannot be told.
        /// </summary>
        private string ActiveRunRefusal(string rel)
        {
            var folders = new List<(string folder, string name, string launchedRecipe, DateTime? startedUtc)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Consider(string folder, string name, string recipe, DateTime? started)
            {
                if (string.IsNullOrEmpty(folder)) return;
                string full = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (seen.Add(full)) folders.Add((full, name, recipe, started));
            }
            foreach (RoomContext.LaunchedRun r in _room.LaunchedRuns())
                Consider(_room.ResolveFromMain(r.Folder), r.Name, r.Recipe, r.StartedUtc);
            foreach (string run in _room.Settings.Runs)
                Consider(_room.ResolveFolder(run), run.Substring(run.LastIndexOf('/') + 1), null, null);
            foreach (string baseDir in new[] { _room.Root, _room.MainCheckout }.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string runsDir = Path.Combine(baseDir, _room.Settings.RunsDir.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(runsDir)) continue;
                foreach (string d in Directory.GetDirectories(runsDir)) Consider(d, Path.GetFileName(d), null, null);
            }

            DateTime now = DateTime.UtcNow;
            foreach (var f in folders)
            {
                string recordFile = Path.Combine(f.folder, "run.json");
                JsonElement? record = null;
                if (File.Exists(recordFile))
                {
                    try
                    {
                        using (var s = new FileStream(recordFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var r = new StreamReader(s, new UTF8Encoding(false)))
                            record = Js.Parse(r.ReadToEnd());
                    }
                    catch (Exception e) when (e is JsonException || e is IOException || e is UnauthorizedAccessException)
                    {
                        return $"the run \"{f.name}\" has a run.json that cannot be read ({_room.Scrub(e.Message)}), so whether it uses {rel} cannot be checked";
                    }
                }
                bool active;
                string recipe = f.launchedRecipe;
                if (record.HasValue)
                {
                    active = Js.Str(record, "status") == "running" && RunFolder.ProcessAlive(record) != false;
                    recipe = Js.Str(record, "recipe", "path") ?? recipe;
                }
                else active = f.startedUtc.HasValue && now - f.startedUtc.Value < Cap.RecentLaunch;
                if (!active) continue;
                string normal = PlaceRecipe(recipe, out RecipeSite site);
                if (site == RecipeSite.OtherTree) continue;
                // Fail closed: a run that has not ended and whose recipe cannot be placed might be using this very file.
                if (normal == null)
                    return $"the run \"{f.name}\" has not ended and {(string.IsNullOrWhiteSpace(recipe) ? "no recipe is recorded for it" : "its recipe (" + _room.RelativeOrOutside(recipe) + ") cannot be placed inside this tree")}, so whether it uses {rel} cannot be checked";
                if (string.Equals(normal, rel, StringComparison.OrdinalIgnoreCase)) return $"the run \"{f.name}\" has not ended and uses {rel} as its recipe";
                foreach (string baseRel in BaseChain(normal))
                    if (string.Equals(baseRel, rel, StringComparison.OrdinalIgnoreCase))
                        return $"the run \"{f.name}\" has not ended and uses {normal}, which has {rel} in its base chain";
            }
            return null;
        }

        /// <summary>Where a recipe path a record names lies, as far as this tree can tell.</summary>
        private enum RecipeSite
        {
            /// <summary>Inside this tree: the path was made relative to the root.</summary>
            Here,
            /// <summary>Under the main checkout but not under this root: a file of another tree, which is not this tree's file whatever its name.</summary>
            OtherTree,
            /// <summary>Neither (empty, outside every tree, not a path): it cannot be told which file it is.</summary>
            Unknown,
        }

        /// <summary>
        /// A recipe path made plain (relative to the root, forward slashes, no ./), or null when it is not one this tree can place. The nursery writes
        /// the path in a run.json absolute (<c>Path.GetFullPath</c> of what it was given), so a rooted path that lies under the root is taken and made
        /// relative, the comparison on full, normalised paths without regard to case; a relative one is resolved as the room resolves every path.
        /// </summary>
        private string PlaceRecipe(string path, out RecipeSite site)
        {
            site = RecipeSite.Unknown;
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (Path.IsPathRooted(path))
            {
                string rooted;
                try { rooted = Path.GetFullPath(path); }
                catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is PathTooLongException) { return null; }
                if (UnderFolder(rooted, _room.Root)) { site = RecipeSite.Here; return Path.GetRelativePath(_room.Root, rooted).Replace('\\', '/'); }
                if (UnderFolder(rooted, _room.MainCheckout)) site = RecipeSite.OtherTree;
                return null;
            }
            string full = _room.ResolveInRepo(path, out _);
            if (full == null) return null;
            site = RecipeSite.Here;
            return Path.GetRelativePath(_room.Root, full).Replace('\\', '/');
        }

        private static bool UnderFolder(string full, string folder)
        {
            string b = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return full.StartsWith(b, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>A repository-relative recipe path made plain, or null when this tree cannot place it (see <see cref="PlaceRecipe"/>).</summary>
        private string NormalRecipePath(string path) => PlaceRecipe(path, out _);

        /// <summary>The recipes a recipe names as its base, then theirs, and so on (each repository-relative); stops at a file that is missing or does not read, and at a loop.</summary>
        private List<string> BaseChain(string rel)
        {
            var chain = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { rel };
            string at = rel;
            while (chain.Count < 32)
            {
                string full = _room.ResolveInRepo(at, out _);
                if (full == null || !File.Exists(full)) break;
                string next;
                try { next = Js.Str(Js.Parse(File.ReadAllText(full)), "base"); }
                catch (Exception e) when (e is JsonException || e is IOException || e is UnauthorizedAccessException) { break; }
                next = NormalRecipePath(next);
                if (next == null || !seen.Add(next)) break;
                chain.Add(next);
                at = next;
            }
            return chain;
        }

        private object RecipeWrite(string path, string text, string expectHash)
        {
            string full = RecipeTarget(path, out string rel);
            string linked = _room.LinkProblem(full);
            if (linked != null) throw new ArgumentException($"{rel} is not written: {linked}. Nothing was written.");
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("text is the recipe's JSON and may not be empty");
            if (text.Length > MaxRecipeChars) throw new ArgumentException($"the text is {text.Length} characters; a recipe is at most {MaxRecipeChars}");
            bool exists = File.Exists(full);
            string current = exists ? RunRecord.FileSha256(full) : null;
            if (exists)
            {
                string depends = ReplaceRefusal(rel);
                if (depends != null)
                    throw new ArgumentException($"{rel} cannot be replaced: {depends}. A file the registry's record or a running run depends on is never overwritten; write a new file under another name instead (with \"base\": \"{rel}\" if it should start from this one). Nothing was written.");
                if (string.IsNullOrWhiteSpace(expectHash))
                    throw new ArgumentException($"{rel} exists, so expectHash is required: the SHA-256 of the file as it is now ({current}). Nothing was written.");
                if (!string.Equals(expectHash.Trim(), current, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException($"{rel} is not the file expectHash names: its SHA-256 is {current}, not {expectHash.Trim()}. Someone changed it since it was read; read it again. Nothing was written.");
            }
            else if (!string.IsNullOrWhiteSpace(expectHash))
                throw new ArgumentException($"{rel} does not exist, so there is no hash to expect; leave expectHash out to create it. Nothing was written.");

            byte[] bytes = new UTF8Encoding(false).GetBytes(text);
            // The nursery reads files, not text: the text is checked as a file (a name no listing shows) beside the target, or, when the target's
            // folder does not exist yet (the trials folder before its first recipe), in the nearest folder above it that does, which is inside the
            // repository too, so that a recipe the nursery refuses leaves no folder behind. The folder is made only after the nursery accepts the text.
            // The temporary name is unique to this write: two writers of one file (two rooms) never share, truncate or delete each other's.
            string targetDir = Path.GetDirectoryName(full);
            string checkDir = targetDir;
            while (!Directory.Exists(checkDir)) checkDir = Path.GetDirectoryName(checkDir);
            string temp = Path.Combine(checkDir, Path.GetFileName(full) + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".writing");
            File.WriteAllBytes(temp, bytes);
            try
            {
                try { Recipe.Load(temp); }
                catch (Exception e) when (!(e is OutOfMemoryException))
                {
                    // the nursery names the check copy; the message names the file the text was for
                    throw new ArgumentException("the nursery refuses this recipe, so nothing was written: " + _room.Scrub(e.Message.Replace(Path.GetFullPath(temp), full, StringComparison.OrdinalIgnoreCase)));
                }
                _room.EnsureDirectory(targetDir);
                // Hashed again just before the move: the file may have changed since the first read, in the time the nursery took to check the text.
                byte[] old = exists ? File.ReadAllBytes(full) : null;
                string now = old == null ? (File.Exists(full) ? "(a file now)" : null) : ActivityLog.Hex(SHA256.HashData(old));
                if (now != current)
                    throw new ArgumentException($"{rel} changed while its text was being checked ({(current == null ? "it did not exist" : "its SHA-256 was " + current)}, and now {(now == null ? "it is gone" : now == "(a file now)" ? "it exists" : "it is " + now)}). Read it again. Nothing was written.");
                try { File.Move(temp, full, overwrite: exists); }
                catch (IOException) when (!exists && File.Exists(full))
                {
                    throw new ArgumentException($"{rel} was created by someone else while its text was being checked. Read it again. Nothing was written.");
                }
                Recipe r;
                try { r = Recipe.Load(full); }
                catch (Exception e) when (!(e is OutOfMemoryException))
                {
                    // Only its place can make a recipe that loaded as a copy refuse here: it names itself as a base, directly or round a chain.
                    if (old != null) File.WriteAllBytes(full, old); else File.Delete(full);
                    throw new ArgumentException($"the nursery refuses this recipe at {rel}, so the file is as it was: " + _room.Scrub(e.Message));
                }
                return new { path = rel, ok = true, created = !exists, name = r.Name, hash = r.Hash(), fileSha256 = RunRecord.FileSha256(full), previousSha256 = current };
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        private (Recipe recipe, JsonElement resolved, string rel) LoadResolved(string path)
        {
            string full = _room.ResolveInRepo(path, out string problem);
            if (full == null) throw new ArgumentException(problem);
            string rel = Path.GetRelativePath(_room.Root, full).Replace('\\', '/');
            if (!File.Exists(full)) throw new ArgumentException($"{rel} does not exist");
            try
            {
                Recipe r = Recipe.Read(full);   // Read: a diff compares fields, and a world this build cannot load is no reason to refuse it
                return (r, Js.Parse(r.Canonical()), rel);
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                throw new ArgumentException($"the nursery refuses {rel}, so it cannot be compared: " + _room.Scrub(e.Message));
            }
        }

        private object RecipeDiff(string path, string other)
        {
            var a = LoadResolved(path);
            bool isBase = false;
            if (other == null)
            {
                string baseRel;
                try { baseRel = Js.Str(Js.Parse(File.ReadAllText(_room.ResolveInRepo(path, out _))), "base"); }
                catch (Exception e) when (e is JsonException || e is IOException || e is UnauthorizedAccessException) { baseRel = null; }
                if (baseRel == null) throw new ArgumentException($"{a.rel} has no base; give other, the recipe to compare it with");
                other = baseRel;
                isBase = true;
            }
            var b = LoadResolved(other);
            List<RecipeDifference> differs = RecipeDifference.Between(a.resolved, b.resolved);
            return new
            {
                ok = true,
                a = new { path = a.rel, name = a.recipe.Name, hash = a.recipe.Hash() },
                b = new { path = b.rel, name = b.recipe.Name, hash = b.recipe.Hash(), isBase },
                identical = differs.Count == 0,
                differs = differs.Select(d => new { path = d.Path, a = d.A, b = d.B, side = d.Side }).ToList(),
            };
        }

        // ------------------------------------------------------------------ run.launch, run.rerun, run.stop

        private const string ThreadsSchema = @"""threads"":{""type"":""integer"",""description"":""Worker threads the run takes (the nursery's --threads). Counted against the machine's cap with the load read now.""}";

        private void AddRunOperations()
        {
            Add(new Operation
            {
                Family = "run", Action = "launch", Mutates = true,
                Summary = "Start the nursery on a recipe as a process of its own, in a new folder of that name under the room's runs folder. Refused if the recipe is refused, the folder exists, the threads pass the processors the affinity mask allows, or the threads plus the machine's load would pass the cap. The run is pinned, before its first instruction, to the logical processors room.json's affinity names, and the result says which the launcher applied. A recipe directly under rounds/nursery/ is registered by the nursery when its run ends; a recipe anywhere else in the repository runs as an unregistered trial, which the result, the activity row and the registry mark (trial: true). The run shows in the registry at once.",
                Schema = @"{""type"":""object"",""properties"":{""recipe"":{""type"":""string"",""description"":""The recipe file, relative to the repository root: directly under rounds/nursery/, or anywhere else in the repository for an unregistered trial.""},""name"":{""type"":""string"",""description"":""The run's folder name: a-z, 0-9 and '-', up to 64, not all digits (a number names a registry entry).""}," + ThreadsSchema + @"},""required"":[""recipe"",""name"",""threads""],""additionalProperties"":false}",
                PathArgs = new[] { "recipe" },
                Handler = a => Launch(a, rerun: false),
                Record = r => Pick(r, "id", "folder", "recipe", "trial", "tree", "threads", "affinity", "pid", "detached", "launcherNote", "droppedEnvironment"),
            });

            Add(new Operation
            {
                Family = "run", Action = "rerun", Mutates = true,
                Summary = "Run a registry entry's recipe again, on this build, into a new folder, and compare it with the entry (the nursery's --rerun). The entry is its number or 8 or more hex digits of its recipe's hash. Refused if the entry cannot be rerun on this build, or as for launch.",
                Schema = @"{""type"":""object"",""properties"":{""entry"":{""type"":""string"",""description"":""A registry entry's number, or 8 or more hex digits of its recipe's hash.""},""name"":{""type"":""string"",""description"":""The new run's folder name, as for launch.""}," + ThreadsSchema + @"},""required"":[""entry"",""name"",""threads""],""additionalProperties"":false}",
                Handler = a => Launch(a, rerun: true),
                Record = r => Pick(r, "id", "folder", "recipe", "trial", "tree", "threads", "affinity", "pid", "detached", "launcherNote", "droppedEnvironment", "entry"),
            });

            Add(new Operation
            {
                Family = "run", Action = "stop", Mutates = true,
                Summary = "Ask a running nursery to end cleanly: writes a STOP file into its folder, which it reads between generations and between exam brains. Never kills a process. A run built before the stop file is told so.",
                Schema = @"{""type"":""object"",""properties"":{""id"":{""type"":""string"",""description"":""The run: a registry number, or the folder name of a run the room knows.""}},""required"":[""id""],""additionalProperties"":false}",
                Handler = a => Stop(Js.Str(a, "id")),
                Record = r => Pick(r, "id", "stopFile", "readsStopFile", "stoppedBeforeItBegan"),
            });
        }

        private static string FolderNameProblem(string name)
        {
            if (!RunName.IsMatch(name ?? "")) return $"the name \"{ActivityLog.Clean(name, 80)}\" is not a folder name: a-z, 0-9 and '-', starting with a letter or digit, 1 to 64 characters";
            if (name.All(char.IsDigit)) return $"the name \"{name}\" is all digits, which names a registry entry; add a letter";
            if (IsDeviceName(name)) return $"the name \"{name}\" is a Windows device name (con, prn, aux, nul, com1-9, lpt1-9), which a folder cannot be called";
            return null;
        }

        private object Launch(JsonElement a, bool rerun)
        {
            string name = Js.Str(a, "name");
            int threads = (int)(Js.Num(a, "threads") ?? 0);
            string problem = FolderNameProblem(name);
            if (problem != null) throw new ArgumentException(problem);
            if (threads < 1) throw new ArgumentException($"threads is 1 or more, not {threads}");
            // The mask and the threads are checked first, before anything reads git (a rerun's checks do): a refusal that costs nothing comes first.
            // The mask names only processors this machine has (room.json's affinity is never 0: RoomSettings), or nothing is started. The machine is
            // the system's mask the room read once, the very value the launcher checks against. And a run asks for no more threads than the processors it is pinned to.
            string noSuchCore = CoreMask.Problem(_room.Settings.Affinity, _room.SystemMask);
            if (noSuchCore != null) throw new ArgumentException(noSuchCore);
            string tooMany = CoreMask.ThreadsProblem(threads, _room.Settings.Affinity);
            if (tooMany != null) throw new ArgumentException(tooMany);

            var args = new List<string>();
            string entryArg = null, recipeUsed;
            bool trial = false;
            if (!rerun)
            {
                string recipePath = Js.Str(a, "recipe");
                string full = _room.ResolveInRepo(recipePath, out string why);
                if (full == null) throw new ArgumentException(why);
                string rel = Path.GetRelativePath(_room.Root, full).Replace('\\', '/');
                string rawProblem = RawPathProblem(recipePath);
                if (rawProblem != null) throw new ArgumentException(rawProblem + "; no run was started");
                // A recipe directly under rounds/nursery/ is registered by the nursery; one anywhere else in the repository (the path guard above keeps
                // it inside) runs unregistered, as a trial (the owner's ruling, 2026-10-02). A subfolder of rounds/nursery/ is still refused.
                string refused = RecipePlace.LaunchProblem(rel, out trial);
                if (refused != null) throw new ArgumentException(refused + " No run was started.");
                string linkedRecipe = _room.LinkProblem(full);
                if (linkedRecipe != null) throw new ArgumentException($"{rel} is not launched: {linkedRecipe}. No run was started.");
                if (!File.Exists(full)) throw new ArgumentException($"{rel} does not exist");
                try { Recipe.Load(full); }
                catch (Exception e) when (!(e is OutOfMemoryException))
                {
                    throw new ArgumentException($"the nursery refuses {rel}, so no run was started: " + _room.Scrub(e.Message));
                }
                args.Add("--recipe"); args.Add(rel);
                recipeUsed = rel;
            }
            else
            {
                entryArg = Js.Str(a, "entry").Trim();
                recipeUsed = CheckRerunnable(entryArg);
                args.Add("--rerun"); args.Add(entryArg);
            }

            string dll = Path.IsPathRooted(_room.Settings.NurseryDll)
                ? _room.Settings.NurseryDll
                : Path.GetFullPath(Path.Combine(_room.Root, _room.Settings.NurseryDll.Replace('/', Path.DirectorySeparatorChar)));
            if (!File.Exists(dll))
                throw new ArgumentException($"the nursery is not built where room.json says ({_room.Settings.NurseryDll}); build it first with `dotnet build src/Evosim.Nursery -c Release` (run from the repository root), then launch again. No run was started.");

            string folder = _room.LaunchFolder(name);
            string folderRel = Path.GetRelativePath(_room.Root, folder).Replace('\\', '/');
            string folderMain = _room.MainRelative(folder);
            string log = folder + ".launch.log";
            // The folder and its launch log are made under the runs folder: no link may lie on the way (the one check, here and where the folder is made).
            string linkedFolder = _room.LinkProblem(folder) ?? _room.LinkProblem(log);
            if (linkedFolder != null) throw new ArgumentException($"{folderRel} is not made: {linkedFolder}. No run was started.");
            if (Directory.Exists(folder) || File.Exists(folder))
                throw new ArgumentException($"{folderRel} exists; a run is given a new folder, since every file in it is digested as a result. Choose another name.");
            if (File.Exists(log)) throw new ArgumentException($"{folderRel}.launch.log exists from an earlier run of this name; choose another name");

            args.Add("--out"); args.Add(folderRel);
            args.Add("--threads"); args.Add(threads.ToString(CultureInfo.InvariantCulture));
            var spec = new LaunchSpec
            {
                FileName = _room.Settings.Dotnet,
                Arguments = new[] { dll }.Concat(args).ToList(),
                WorkingDirectory = _room.Root,
                Folder = folder,
                LogFile = log,
                Affinity = _room.Settings.Affinity,
            };
            // Every part of the command line is checked, the dotnet and dll paths from room.json included, and refused rather than cleaned.
            string badLine = CommandLine.Problem(spec);
            if (badLine != null) throw new ArgumentException(badLine);

            DateTime now = DateTime.UtcNow;
            LoadReading load = _room.Load.Read();
            int granted = _room.GrantedThreads(now);
            string over = Cap.Refusal(threads, _room.Settings.CapLogical, _room.Logical, load, granted);
            if (over != null) throw new ArgumentException(over);

            // The name is claimed by creating its launch log, beside the folder and not in it (the nursery refuses a folder that holds anything), with
            // CreateNew: of two rooms asking for one name at once, one creates the file and the other is refused. Then the folder is made empty.
            _room.EnsureDirectory(Path.GetDirectoryName(folder));
            try { using (new FileStream(log, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { } }
            catch (IOException) when (File.Exists(log))
            {
                throw new ArgumentException($"{folderRel}.launch.log exists: this name was just taken (by an earlier run, or by another room at the same moment); choose another name");
            }
            LaunchOutcome outcome;
            try
            {
                if (Directory.Exists(folder) || File.Exists(folder))
                    throw new ArgumentException($"{folderRel} exists; a run is given a new folder, since every file in it is digested as a result. Choose another name.");
                _room.EnsureDirectory(folder);
                try { outcome = _room.Launcher.Start(spec); }
                catch (Exception e) when (!(e is OutOfMemoryException))
                {
                    try { Directory.Delete(folder); } catch (IOException) { }
                    throw new InvalidOperationException("the run could not be started: " + _room.Scrub(e.Message), e);
                }
            }
            catch
            {
                // Nothing started: the claim is given back, so that the name is free again.
                try { File.Delete(log); } catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
                throw;
            }
            _room.NoteLaunched(name, folderMain, threads, now, recipeUsed, trial);
            // What the launcher applied is what is recorded, not what was asked: a launcher that pinned otherwise says so.
            string note = outcome.Note;
            if (outcome.Affinity != spec.Affinity)
                note = (note == null ? "" : note + "; ") + $"the launcher applied {(outcome.Affinity == 0 ? "no affinity mask" : "the affinity mask " + CoreMask.Format(outcome.Affinity))}, not the {CoreMask.Format(spec.Affinity)} room.json names";
            // The run now takes the threads it was granted: a reading kept from before it started would let the next launch pass the cap.
            _room.Load.Invalidate();
            return new
            {
                id = name, kind = rerun ? "rerun" : "launch", entry = entryArg, recipe = recipeUsed, trial, folder = folderMain, tree = _room.TreeRelative, threads, pid = outcome.Pid,
                detached = outcome.Detached, launcherNote = note,
                affinity = outcome.Affinity == 0 ? null : CoreMask.Format(outcome.Affinity),
                droppedEnvironment = (outcome.DroppedEnvironment ?? new string[0]).ToList(),
                launchLog = folderMain + ".launch.log",
                command = "dotnet " + string.Join(" ", new[] { _room.Display(dll) }.Concat(args)),
                load = new { percent = load.Percent, read = Cap.InUse(load.Percent ?? 0, _room.Logical), granted, inUse = Cap.InUse(load.Percent ?? 0, _room.Logical, granted), logical = _room.Logical, cap = _room.Settings.CapLogical },
            };
        }

        /// <summary>The nursery's own checks of a rerun, made here too so that a refused rerun leaves no empty folder: the entry exists, can be rerun, its recipe is unchanged, and this build is the entry's.</summary>
        private string CheckRerunnable(string entryArg)
        {
            List<RegistryEntry> all;
            try { all = RegistryEntry.ReadAll(_room.RegistryFile); }
            catch (InvalidDataException bad) { throw new ArgumentException("the registry cannot be read: " + _room.Scrub(bad.Message)); }
            int number;
            if (entryArg.Length < 8)
            {
                if (!int.TryParse(entryArg, NumberStyles.None, CultureInfo.InvariantCulture, out number) || number < 1 || number > all.Count)
                    throw new ArgumentException($"the registry has {all.Count} entries, and no entry \"{entryArg}\" (name an entry by its number, or by 8 or more digits of its recipe's hash)");
            }
            else
            {
                if (!Regex.IsMatch(entryArg, @"\A[0-9a-fA-F]{8,64}\z")) throw new ArgumentException($"\"{entryArg}\" is neither an entry's number nor 8 or more hex digits of a recipe's hash");
                int[] matches = Enumerable.Range(0, all.Count).Where(i => all[i].Reference == null && all[i].RecipeHash != null
                    && all[i].RecipeHash.StartsWith(entryArg, StringComparison.OrdinalIgnoreCase) && all[i].RerunOf == 0 && all[i].Rerunnable).ToArray();
                if (matches.Length != 1) throw new ArgumentException($"{matches.Length} rerunnable first runs have a recipe hash starting {entryArg}; name the entry by its number");
                number = matches[0] + 1;
            }
            RegistryEntry e = all[number - 1];
            if (e.Reference != null) throw new ArgumentException($"entry {number} is a reference entry, a run made before the framework: its tie to its recipe is a check, not a rerun ({e.Reference.Verdict})");
            if (!e.Rerunnable) throw new ArgumentException($"entry {number} is not rerunnable: {e.NotRerunnableBecause}");
            if (e.Out == null || e.RecipePath == null) throw new ArgumentException($"entry {number} names no output folder or no recipe");
            string recipeFull = _room.ResolveInRepo(e.RecipePath, out string why);
            if (recipeFull == null || !File.Exists(recipeFull)) throw new ArgumentException($"entry {number}'s recipe {e.RecipePath} is not in this tree");
            string linkedRecipe = _room.LinkProblem(recipeFull);
            if (linkedRecipe != null) throw new ArgumentException($"entry {number}'s recipe: {linkedRecipe}. No run was started.");
            // The same checks as a launch makes of its recipe: a plain path, and a registered recipe (a plain file directly under rounds/nursery/).
            // A rerun is of a registered entry only: a recipe elsewhere is a trial, which is launched and not rerun.
            string rawProblem = RawPathProblem(e.RecipePath);
            if (rawProblem != null) throw new ArgumentException($"entry {number}'s recipe: {rawProblem}; no run was started");
            string recipeRel = Path.GetRelativePath(_room.Root, recipeFull).Replace('\\', '/');
            string unregistered = RecipePlace.LaunchProblem(recipeRel, out bool entryIsTrial);
            if (unregistered == null && entryIsTrial)
                unregistered = $"{recipeRel} is not a registered recipe: a recipe outside rounds/nursery/ runs as an unregistered trial, which is launched and not rerun (only a registered entry is rerun).";
            if (unregistered != null) throw new ArgumentException($"entry {number}'s recipe: " + unregistered + " No run was started.");
            Recipe entryRecipe;
            try
            {
                // Read, not Load: the hash is the same either way, and an entry whose world this build cannot load is refused below by the build check, which names its commit.
                entryRecipe = Recipe.Read(recipeFull);
                if (entryRecipe.Hash() != e.RecipeHash)
                    throw new ArgumentException($"entry {number}'s recipe {e.RecipePath} now hashes differently from the entry's: it was edited after its run");
            }
            catch (InvalidDataException x) { throw new ArgumentException($"entry {number}'s recipe is refused now: " + _room.Scrub(x.Message)); }
            RunRecord here;
            try { here = _room.ReadBuild(_room.Root); }
            catch (InvalidDataException x) { throw new ArgumentException("this build cannot be read: " + _room.Scrub(x.Message)); }
            IReadOnlyList<string> differ = e.BuildDifferences(here);
            if (differ.Count > 0)
                throw new ArgumentException($"entry {number} ran on commit {e.GitCommit}, and this build's {string.Join(", ", differ)} tree hashes differ from it. A rerun is bit for bit on the entry's build only: build that commit in a worktree under scratch/ and rerun there.");
            // On the entry's own build its world loads; if it does not, the nursery would refuse the rerun, so the room does first.
            string worldProblem = entryRecipe.WorldProblem();
            if (worldProblem != null) throw new ArgumentException($"entry {number}'s recipe is refused now: " + _room.Scrub(worldProblem));
            return recipeRel;
        }

        private object Stop(string id)
        {
            ResourceResolver.RunRef run;
            try { run = _resources.FindRun(id); }
            catch (ResourceNotFound e) { throw new ArgumentException(e.Message); }
            if (run.Folder == null || !Directory.Exists(run.Folder)) throw new ArgumentException($"the run \"{id}\" has no folder on this machine, so there is nothing to stop");
            string folderRel = _room.MainRelative(run.Folder);

            JsonElement? record = null;
            string recordFile = Path.Combine(run.Folder, "run.json");
            bool haveRecordFile = File.Exists(recordFile);
            if (haveRecordFile)
            {
                try
                {
                    using (var s = new FileStream(recordFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var r = new StreamReader(s, new UTF8Encoding(false)))
                        record = Js.Parse(r.ReadToEnd());
                }
                catch (Exception e) when (e is JsonException || e is IOException || e is UnauthorizedAccessException)
                {
                    // A run.json that is there and does not parse is a run whose state is unknown: a STOP file is not written on a guess.
                    throw new ArgumentException($"the run \"{id}\" has a run.json that cannot be read ({_room.Scrub(e.Message)}), so its state is unknown and a STOP file is not written. Nothing was written.");
                }
            }
            // A folder with no run.json is a run only when this room started it (and has not yet seen it begin): the nursery writes run.json
            // first, so a folder without one that the room did not launch is something else, and a STOP file in it would be a stray file.
            bool launchedHere = _room.IsLaunchedFolder(run.Folder);
            if (!haveRecordFile && !launchedHere)
                throw new ArgumentException($"the run \"{id}\" has no run.json, and this room did not launch it, so it is not known to be a run that a STOP file would reach. Nothing was written.");
            string status = Js.Str(record, "status");
            if (status == "completed" || status == "exited" || status == "failed" || status == "stopped")
                throw new ArgumentException($"the run \"{id}\" has ended ({status}); a STOP file in its folder would only become one of its result files. Nothing was written.");
            if (status == "running")
            {
                RunFolder f = _room.Runs.Get(run.Folder);
                f.Refresh(1.0);
                string phase = Js.Str(Js.Parse(Js.Serialize(f.Snapshot(DateTime.UtcNow, full: false)["state"])), "phase");
                if (phase == "killed")
                    throw new ArgumentException($"the run \"{id}\" is recorded as running but its process is gone; a STOP file has nothing to stop. Nothing was written.");
            }

            string stop = Path.Combine(run.Folder, RunRecord.StopFileName);
            bool already = File.Exists(stop);
            if (!already)
                File.WriteAllText(stop, "stop requested through the nursery room at " + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + "\n", new UTF8Encoding(false));
            string commit = Js.Str(record, "build", "gitCommit");
            bool reads = Js.Str(record, "stopFile") == RunRecord.StopFileName;
            bool before = !haveRecordFile;
            string note = before
                ? "The STOP file was written before the run wrote its run.json: it is reported as stopped before it began. The nursery refuses an output folder that holds anything, so a run still starting ends at once (exit 2, its console output in the .launch.log beside the folder) having written nothing but STOP, and is not entered in the registry. A run already past that check reads the file between generations and between the exam's brains."
                : reads
                    ? "The run's build reads the STOP file between generations and between the exam's brains; it ends with status stopped and is not entered in the registry unless it is already swimming its last exam brain. A generation or a brain already being swum finishes first."
                    : $"The STOP file was written, but this run's build (commit {(commit == null ? "unknown" : commit.Substring(0, Math.Min(7, commit.Length)))}) predates the stop file: its run.json does not name one. This build may not read it.";
            return new
            {
                id = run.Id, folder = folderRel, stopFile = folderRel + "/" + RunRecord.StopFileName, written = !already, alreadyThere = already,
                readsStopFile = reads, stoppedBeforeItBegan = before, build = commit, note,
            };
        }

        // ------------------------------------------------------------------ compare

        private void AddCompare()
        {
            Add(new Operation
            {
                Family = "compare", Action = null, Mutates = false,
                Summary = "Two runs' folders, file by file: each file's SHA-256 in each, and for a text file that differs the first differing line (its number and both lines, cut to 200 characters). The verdict is identical only if every file is, run.json and the logs included; resultsIdentical says the same of the files the registry digests as results. Has no side effect.",
                Schema = @"{""type"":""object"",""properties"":{""a"":{""type"":""string"",""description"":""One run: a registry number, or the folder name of a run the room knows.""},""b"":{""type"":""string"",""description"":""The other run.""}},""required"":[""a"",""b""],""additionalProperties"":false}",
                Handler = a => Compare(Js.Str(a, "a"), Js.Str(a, "b")),
            });
        }

        private static readonly string[] TextExtensions = { ".json", ".jsonl", ".tsv", ".csv", ".log", ".txt", ".err", ".md" };

        private string FolderOf(string id)
        {
            ResourceResolver.RunRef run;
            try { run = _resources.FindRun(id); }
            catch (ResourceNotFound e) { throw new ArgumentException(e.Message); }
            if (run.Folder == null || !Directory.Exists(run.Folder)) throw new ArgumentException($"the run \"{id}\" has no folder on this machine");
            return run.Folder;
        }

        private object Compare(string idA, string idB)
        {
            string fa = FolderOf(idA), fb = FolderOf(idB);
            string[] names = Directory.GetFiles(fa).Select(Path.GetFileName).Union(Directory.GetFiles(fb).Select(Path.GetFileName)).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            // A subfolder is listed and not read: its files are not hashed, so neither the verdict nor resultsIdentical speaks for them.
            string[] subfolders = Directory.GetDirectories(fa).Select(Path.GetFileName).Union(Directory.GetDirectories(fb).Select(Path.GetFileName)).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            var files = new List<object>();
            bool all = true, results = true;
            foreach (string n in names)
            {
                string pa = Path.Combine(fa, n), pb = Path.Combine(fb, n);
                string ha = Sha(pa), hb = Sha(pb);
                bool same = ha != null && ha == hb;
                string first = null; int? line = null; string la = null, lb = null;
                if (!same && ha != null && hb != null && TextExtensions.Contains(Path.GetExtension(n).ToLowerInvariant()))
                {
                    FirstDifferentLine(pa, pb, out line, out la, out lb);
                    first = line.HasValue ? null : "the bytes differ but no line does (line ends or a final newline)";
                }
                files.Add(new { name = n, sha256A = ha, sha256B = hb, identical = same, firstDifferentLine = line, lineA = la, lineB = lb, note = first });
                all &= same;
                if (RegistryEntry.IsResult(n)) results &= same;
            }
            return new
            {
                a = new { id = idA, folder = _room.MainRelative(fa) },
                b = new { id = idB, folder = _room.MainRelative(fb) },
                verdict = all ? "identical" : "differs",
                resultsIdentical = results,
                files,
                subfolders = subfolders.Select(n => new
                {
                    name = n, inA = Directory.Exists(Path.Combine(fa, n)), inB = Directory.Exists(Path.Combine(fb, n)), note = "not compared",
                }).ToList(),
                verdictNote = subfolders.Length == 0 ? null : $"{subfolders.Length} subfolder(s) were listed and not compared, so the verdict covers the files directly in the two folders only",
            };
        }

        /// <summary>SHA-256 of a file's bytes, read while a run may still be writing it; null when the file is not there.</summary>
        private static string Sha(string path)
        {
            if (!File.Exists(path)) return null;
            using (var s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                return ActivityLog.Hex(SHA256.HashData(s));
        }

        private static string Cut(string s) => s.Length <= 200 ? s : s.Substring(0, 200) + "...";

        /// <summary>The first line two files differ on (numbered from 1; a line missing from the shorter file is "&lt;end of file&gt;"), each cut to 200 characters.</summary>
        public static void FirstDifferentLine(string a, string b, out int? line, out string lineA, out string lineB)
        {
            line = null; lineA = null; lineB = null;
            using (var sa = new FileStream(a, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sb = new FileStream(b, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var ra = new StreamReader(sa, new UTF8Encoding(false)))
            using (var rb = new StreamReader(sb, new UTF8Encoding(false)))
            {
                for (int n = 1; ; n++)
                {
                    string x = ra.ReadLine(), y = rb.ReadLine();
                    if (x == null && y == null) return;
                    if (x != y)
                    {
                        line = n; lineA = x == null ? "<end of file>" : Cut(x); lineB = y == null ? "<end of file>" : Cut(y);
                        return;
                    }
                }
            }
        }

        // ------------------------------------------------------------------ small helpers

        /// <summary>The named members of a result, as the row records them.</summary>
        private static object Pick(object result, params string[] keys)
        {
            JsonElement e = Js.Parse(Js.Serialize(result));
            var d = new Dictionary<string, object>();
            foreach (string k in keys) if (e.TryGetProperty(k, out JsonElement v)) d[k] = v.Clone();
            return d;
        }
    }

    /// <summary>One field two resolved recipes differ on.</summary>
    public sealed class RecipeDifference
    {
        public string Path;
        /// <summary>The value in the first recipe and in the second, null where it has none (see <see cref="Side"/>).</summary>
        public JsonElement? A, B;
        /// <summary><c>a</c> when only the first has the field, <c>b</c> when only the second does, null when both have it and differ.</summary>
        public string Side;

        /// <summary>Every leaf of the two, by dotted path (an array is one leaf), that is not the same in both; ordinal by path.</summary>
        public static List<RecipeDifference> Between(JsonElement a, JsonElement b)
        {
            Dictionary<string, string> fa = ResourceResolver.Flatten(a), fb = ResourceResolver.Flatten(b);
            var l = new List<RecipeDifference>();
            foreach (string k in fa.Keys.Union(fb.Keys).OrderBy(k => k, StringComparer.Ordinal))
            {
                fa.TryGetValue(k, out string x); fb.TryGetValue(k, out string y);
                if (x == y) continue;
                l.Add(new RecipeDifference
                {
                    Path = k,
                    A = x == null ? (JsonElement?)null : Js.Parse(x),
                    B = y == null ? (JsonElement?)null : Js.Parse(y),
                    Side = x == null ? "b" : y == null ? "a" : null,
                });
            }
            return l;
        }
    }
}
