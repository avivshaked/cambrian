using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Evosim.Nursery;
using Evosim.NurseryRoom;
using Xunit;

namespace Evosim.NurseryRoom.Tests
{
    /// <summary>
    /// Whether a recipe file is committed (RecipeGit): each state, run for real against a temporary repository made in a fresh folder of the build output,
    /// the timeout and the failures, and the state on the recipe resource.
    /// </summary>
    public class RecipeGitTests : IDisposable
    {
        private readonly string _dir = Where.Fresh("recipe-git");
        private const string Rel = "rounds/nursery/a.json";

        public void Dispose() => Remove(_dir);

        /// <summary>A folder holding a git repository: its object files are read-only on Windows, so the attributes go first.</summary>
        internal static void Remove(string dir)
        {
            if (!Directory.Exists(dir)) return;
            foreach (string f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
            Directory.Delete(dir, true);
        }

        private static readonly Dictionary<string, string> Dated = new Dictionary<string, string>
        {
            ["GIT_AUTHOR_DATE"] = "2026-10-04T12:00:00 +0000",
            ["GIT_COMMITTER_DATE"] = "2026-10-04T12:00:00 +0000",
        };

        internal static string Git(string dir, params string[] args)
        {
            GitResult r = RecipeGit.RunGit(dir, args, 30000, Dated);
            Assert.True(r.Failure == null && r.ExitCode == 0, "git " + string.Join(" ", args) + ": " + r.Failure + r.Error);
            return r.Output;
        }

        internal static void Commit(string dir, string subject) =>
            Git(dir, "-c", "user.name=test", "-c", "user.email=nursery-test", "-c", "commit.gpgsign=false", "commit", "-q", "-m", subject);

        private string File1 => Path.Combine(_dir, "rounds", "nursery", "a.json");

        private void Write(string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(File1));
            File.WriteAllText(File1, text);
        }

        private RecipeGitState Read() => RecipeGit.Read(_dir, Rel);

        private void InitAndCommit(string subject = "pre-register round 56")
        {
            Git(_dir, "init", "-q");
            Write("{\"a\":1}\n");
            Git(_dir, "add", "-A");
            Commit(_dir, subject);
        }

        [Fact]
        public void ACommittedUnchangedFileIsCommittedAndNamesItsLastCommit()
        {
            InitAndCommit("pre-register round 56");
            RecipeGitState g = Read();
            Assert.Equal("committed", g.State);
            Assert.Equal("committed", g.Label);
            Assert.NotNull(g.LastCommit);
            Assert.Equal(Git(_dir, "rev-parse", "--short", "HEAD").Trim(), g.LastCommit.Hash);
            Assert.Equal("2026-10-04", g.LastCommit.Date);
            Assert.Equal("pre-register round 56", g.LastCommit.Subject);
            Assert.Contains(g.LastCommit.Hash, g.Detail);
            Assert.Contains("pre-register round 56", g.Detail);
            Assert.Null(g.GitMessage);
        }

        [Fact]
        public void TheLastCommitIsTheOneThatTouchedTheFileNotTheHead()
        {
            InitAndCommit("touches the recipe");
            string touched = Git(_dir, "rev-parse", "--short", "HEAD").Trim();
            File.WriteAllText(Path.Combine(_dir, "other.txt"), "x");
            Git(_dir, "add", "-A");
            Commit(_dir, "touches something else");
            RecipeGitState g = Read();
            Assert.Equal("committed", g.State);
            Assert.Equal(touched, g.LastCommit.Hash);
            Assert.Equal("touches the recipe", g.LastCommit.Subject);
        }

        [Fact]
        public void AChangeInTheWorkingTreeIsChangedAndStillNamesTheLastCommit()
        {
            InitAndCommit();
            Write("{\"a\":2}\n");
            RecipeGitState g = Read();
            Assert.Equal("changed", g.State);
            Assert.Equal("changed since its last commit", g.Label);
            Assert.Contains("working tree", g.Detail);
            Assert.Equal("pre-register round 56", g.LastCommit.Subject);
        }

        [Fact]
        public void AChangeOnlyInTheIndexIsChangedToo()
        {
            InitAndCommit();
            Write("{\"a\":3}\n");
            Git(_dir, "add", "--", Rel);
            RecipeGitState g = Read();
            Assert.Equal("changed", g.State);
            Assert.Contains("staged in the index", g.Detail);
            Assert.DoesNotContain("working tree", g.Detail);
        }

        [Fact]
        public void AChangeToAnotherFileDoesNotChangeThisOnesState()
        {
            InitAndCommit();
            File.WriteAllText(Path.Combine(_dir, "unrelated.txt"), "x");
            Git(_dir, "add", "-A");
            File.WriteAllText(Path.Combine(_dir, "unrelated.txt"), "y");
            Assert.Equal("committed", Read().State);
        }

        [Fact]
        public void AFileGitHasNeverSeenIsNotCommitted()
        {
            Git(_dir, "init", "-q");
            File.WriteAllText(Path.Combine(_dir, "first.txt"), "x");
            Git(_dir, "add", "-A");
            Commit(_dir, "first");
            Write("{\"a\":1}\n");
            RecipeGitState g = Read();
            Assert.Equal("untracked", g.State);
            Assert.Equal("not committed", g.Label);
            Assert.Null(g.LastCommit);
            Assert.Contains("never been committed", g.Detail);
        }

        [Fact]
        public void AFileAddedToTheIndexButNotCommittedIsNotCommitted()
        {
            Git(_dir, "init", "-q");
            File.WriteAllText(Path.Combine(_dir, "first.txt"), "x");
            Git(_dir, "add", "-A");
            Commit(_dir, "first");
            Write("{\"a\":1}\n");
            Git(_dir, "add", "--", Rel);
            RecipeGitState g = Read();
            Assert.Equal("untracked", g.State);
            Assert.Contains("added to the index", g.Detail);
        }

        [Fact]
        public void ARepositoryWithNoCommitAtAllHoldsNothingCommitted()
        {
            Git(_dir, "init", "-q");
            Write("{\"a\":1}\n");
            Assert.Equal("untracked", Read().State);
            Git(_dir, "add", "--", Rel);
            RecipeGitState g = Read();
            Assert.Equal("untracked", g.State);
            Assert.Contains("added to the index", g.Detail);
        }

        [Fact]
        public void AFileTheRepositoryIgnoresIsNotCommittedEitherAndIsNeverReadAsClean()
        {
            Git(_dir, "init", "-q");
            File.WriteAllText(Path.Combine(_dir, ".gitignore"), "rounds/\n");
            Git(_dir, "add", "-A");
            Commit(_dir, "ignore");
            Write("{\"a\":1}\n");
            Assert.Equal("", Git(_dir, "status", "--porcelain", "--", Rel).Trim());   // git says nothing about it: an empty status is not "clean"
            RecipeGitState g = Read();
            Assert.Equal("untracked", g.State);
            Assert.Contains("ignores it", g.Detail);
        }

        [Fact]
        public void APathWithGlobCharactersIsReadAsItIsWritten()
        {
            InitAndCommit();
            File.WriteAllText(Path.Combine(_dir, "rounds", "nursery", "b1.json"), "{}");
            Git(_dir, "add", "-A");
            Commit(_dir, "b1");
            // read as a glob, b[1].json would be b1.json, which is committed; read as written, it is a file git has never seen
            File.WriteAllText(Path.Combine(_dir, "rounds", "nursery", "b[1].json"), "{}");
            Assert.Equal("untracked", RecipeGit.Read(_dir, "rounds/nursery/b[1].json").State);
            Assert.Equal("committed", Read().State);
        }

        [Fact]
        public void AFolderThatIsNoRepositoryIsOutsideWithGitsOwnMessageAndNeverCommitted()
        {
            Write("{\"a\":1}\n");
            // the build folder lies inside this repository, so git is told to look no higher than this folder's parent
            string ceiling = Path.GetDirectoryName(_dir);
            GitRunner noAbove = (dir, args, ms) => RecipeGit.RunGit(dir, args, ms, new Dictionary<string, string> { ["GIT_CEILING_DIRECTORIES"] = ceiling });
            RecipeGitState g = RecipeGit.Read(_dir, Rel, noAbove);
            Assert.Equal("outside", g.State);
            Assert.Equal("outside the repository", g.Label);
            Assert.Contains("not a git repository", g.GitMessage);
            Assert.Contains(g.GitMessage, g.Detail);
            Assert.Null(g.LastCommit);
        }

        // ---- the failure paths: none of them is ever read as committed

        private static GitResult Ok(string output = "") => new GitResult { ExitCode = 0, Output = output };
        private static GitResult Exit(int code, string error) => new GitResult { ExitCode = code, Error = error };

        public static IEnumerable<object[]> Failing()
        {
            // status fails: timed out, could not start, another exit, nothing back
            yield return new object[] { (Func<IReadOnlyList<string>, GitResult>)(a => new GitResult { Failure = "timed out after 10 s" }), "timed out" };
            yield return new object[] { (Func<IReadOnlyList<string>, GitResult>)(a => new GitResult { Failure = "could not run: Win32Exception: no git" }), "could not run" };
            yield return new object[] { (Func<IReadOnlyList<string>, GitResult>)(a => Exit(128, "fatal: detected dubious ownership in repository")), "dubious ownership" };
            yield return new object[] { (Func<IReadOnlyList<string>, GitResult>)(a => Exit(1, "")), "exit 1" };
            yield return new object[] { (Func<IReadOnlyList<string>, GitResult>)(a => null), "no result" };
            // status is clean but log fails
            yield return new object[] { (Func<IReadOnlyList<string>, GitResult>)(a => a.Contains("log") ? new GitResult { Failure = "timed out after 10 s" } : Ok()), "git log timed out" };
            yield return new object[] { (Func<IReadOnlyList<string>, GitResult>)(a => a.Contains("log") ? Exit(128, "fatal: bad object HEAD") : Ok()), "bad object HEAD" };
            // log answers with something that is no commit line
            yield return new object[] { (Func<IReadOnlyList<string>, GitResult>)(a => a.Contains("log") ? Ok("garbage") : Ok()), "could not be read" };
        }

        [Theory]
        [MemberData(nameof(Failing))]
        public void AGitThatCannotBeReadIsUnreadableWithItsMessageAndNeverCommitted(Func<IReadOnlyList<string>, GitResult> fake, string message)
        {
            RecipeGitState g = RecipeGit.Read(_dir, Rel, (dir, args, ms) => fake(args));
            Assert.Equal("unreadable", g.State);
            Assert.NotEqual("committed", g.State);
            Assert.Equal("git cannot be read", g.Label);
            Assert.Contains(message, g.GitMessage);
            Assert.Contains(message, g.Detail);
            Assert.Null(g.LastCommit);
        }

        [Fact]
        public void BothCommandsAreReadOnlyAndTakeNoOptionalLocksAndTheTimeoutIsPassedOn()
        {
            var seen = new List<(string[] args, int ms)>();
            RecipeGit.Read(_dir, Rel, (dir, args, ms) =>
            {
                seen.Add((args.ToArray(), ms));
                Assert.Equal(_dir, dir);
                return args.Contains("log") ? Ok("abc1234\u001f2026-10-04\u001fsubject\n") : Ok();
            }, 1234);
            Assert.Equal(2, seen.Count);
            Assert.Equal(new[] { "--no-optional-locks", "status", "--porcelain", "--", ":(literal)" + Rel }, seen[0].args);
            Assert.Equal("--no-optional-locks", seen[1].args[0]);
            Assert.Equal("log", seen[1].args[1]);
            Assert.Contains("-1", seen[1].args);
            Assert.All(seen, s => Assert.Equal(1234, s.ms));
        }

        [Fact]
        public void ARealGitThatHangsIsEndedAtTheTimeout()
        {
            // an alias that sleeps stands in for a git that does not answer
            var clock = System.Diagnostics.Stopwatch.StartNew();
            GitResult r = RecipeGit.RunGit(_dir, new[] { "-c", "alias.hang=!sleep 30", "hang" }, 800);
            Assert.NotNull(r.Failure);
            Assert.Contains("timed out", r.Failure);
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(20), "the hung git was waited for: " + clock.Elapsed);
            // and through Read it is unreadable, never clean
            RecipeGitState g = RecipeGit.Read(_dir, Rel, (dir, args, ms) => RecipeGit.RunGit(dir, new[] { "-c", "alias.hang=!sleep 30", "hang" }, 500));
            Assert.Equal("unreadable", g.State);
            Assert.Contains("timed out", g.GitMessage);
        }

        [Fact]
        public void AGitThatCannotBeStartedOrAFolderThatIsGoneIsAFailureNotAnAnswer()
        {
            GitResult none = RecipeGit.RunGit(_dir, new[] { "status" }, 5000, null, "no-such-git-program-" + Guid.NewGuid().ToString("N"));
            Assert.Contains("could not run", none.Failure);
            GitResult gone = RecipeGit.RunGit(Path.Combine(_dir, "no-such-folder"), new[] { "status" }, 5000);
            Assert.Contains("could not run", gone.Failure);
            RecipeGitState g = RecipeGit.Read(Path.Combine(_dir, "no-such-folder"), Rel);
            Assert.Equal("unreadable", g.State);
        }

        [Fact]
        public void AnExitCodeAndGitsMessageAreKeptWhenGitRefuses()
        {
            GitResult r = RecipeGit.RunGit(_dir, new[] { "no-such-subcommand-zz" }, 10000);
            Assert.Null(r.Failure);
            Assert.NotEqual(0, r.ExitCode);
            Assert.False(string.IsNullOrWhiteSpace(r.Error));
        }
    }

    /// <summary>The state and the labels on the recipe resource: what the screen reads, and what the MCP gets with it.</summary>
    public class RecipeResourceB2Tests : IDisposable
    {
        private readonly Fx _fx = new Fx();

        public void Dispose() { RecipeGitTests.Remove(_fx.Dir); }

        private const string Brain2 = "rounds/nursery/brain2-w11.json";

        private JsonElement View(string rel, ResourceResolver r = null) =>
            Js.Parse(Js.Serialize((r ?? new ResourceResolver(_fx.Room)).RecipeView(rel)));

        [Fact]
        public void TheResourceCarriesTheStateOfARepositoryItsRootIsIn()
        {
            string root = _fx.Root;
            RecipeGitTests.Git(root, "init", "-q");
            RecipeGitTests.Git(root, "add", "-A");
            RecipeGitTests.Commit(root, "the round's recipe");

            JsonElement v = View(Brain2);
            JsonElement g = v.GetProperty("gitState");
            Assert.Equal("committed", g.GetProperty("state").GetString());
            Assert.Equal("the round's recipe", g.GetProperty("lastCommit").GetProperty("subject").GetString());
            Assert.Equal("2026-10-04", g.GetProperty("lastCommit").GetProperty("date").GetString());
            Assert.True(v.GetProperty("ok").GetBoolean());
            Assert.Empty(RoomResponseShapeTests.EmptyObjects(g));

            File.AppendAllText(_fx.Full(Brain2), " ");
            JsonElement changed = View(Brain2).GetProperty("gitState");
            Assert.Equal("changed", changed.GetProperty("state").GetString());
            Assert.Equal("the round's recipe", changed.GetProperty("lastCommit").GetProperty("subject").GetString());

            // a recipe the repository has never seen, written the way recipe.write does
            string fresh = "rounds/nursery/zz-new.json";
            File.WriteAllText(_fx.Full(fresh), File.ReadAllText(_fx.Full(Brain2)).Replace("\"name\": \"brain2-w11\"", "\"name\": \"zz-new\""));
            JsonElement none = View(fresh).GetProperty("gitState");
            Assert.Equal("untracked", none.GetProperty("state").GetString());
            Assert.Equal(JsonValueKind.Null, none.GetProperty("lastCommit").ValueKind);
        }

        [Fact]
        public void TheMcpReadsTheSameStateAsTheScreen()
        {
            var resolver = new ResourceResolver(_fx.Room);
            JsonElement v = Js.Parse(Js.Serialize(resolver.Read("nursery://recipe/" + Brain2)));
            Assert.True(v.TryGetProperty("gitState", out JsonElement g));
            Assert.Contains(g.GetProperty("state").GetString(), new[] { "committed", "changed", "untracked", "outside", "unreadable" });
            Assert.True(v.TryGetProperty("labels", out JsonElement labels));
            Assert.Equal(RecipeLabels.Table.Count, labels.EnumerateObject().Count());
        }

        [Fact]
        public void ARecipeTheNurseryRefusesStillSaysWhetherItIsCommitted()
        {
            File.WriteAllText(_fx.Full("rounds/nursery/zz-bad.json"), "{ not json");
            JsonElement v = View("rounds/nursery/zz-bad.json");
            Assert.False(v.GetProperty("ok").GetBoolean());
            Assert.True(v.TryGetProperty("gitState", out JsonElement g));
            Assert.False(string.IsNullOrEmpty(g.GetProperty("state").GetString()));
            Assert.True(v.TryGetProperty("labels", out _));
        }

        [Fact]
        public void AGitThatCannotBeReadIsSaidOnTheResourceAndNotCalledCommitted()
        {
            var resolver = new ResourceResolver(_fx.Room) { GitForRecipes = (dir, args, ms) => new GitResult { Failure = "timed out after 10 s" } };
            JsonElement g = View(Brain2, resolver).GetProperty("gitState");
            Assert.Equal("unreadable", g.GetProperty("state").GetString());
            Assert.Contains("timed out", g.GetProperty("gitMessage").GetString());
            resolver = new ResourceResolver(_fx.Room) { GitForRecipes = (dir, args, ms) => new GitResult { ExitCode = 128, Error = "fatal: not a git repository (or any of the parent directories): .git" } };
            g = View(Brain2, resolver).GetProperty("gitState");
            Assert.Equal("outside", g.GetProperty("state").GetString());
        }

        [Fact]
        public void TheRecipeScreenShowsThePlateBesideTheNameAndInTheLaunchDialog()
        {
            string js = File.ReadAllText(Path.Combine(Where.WwwRoot, "js", "recipe.js"));
            Assert.Contains("gitPlate(d.gitState)", js);                  // beside the recipe's name
            Assert.Contains("fillLaunchGit(d.gitState, !!d.trial)", js);  // and again in the LAUNCH dialog, which knows whether the launch is a trial
            // what this launch is, said for it: a registered run from an uncommitted recipe goes ahead but is not a pre-registration; a trial never is one
            Assert.Contains("so it does not count as a pre-registered run", js);
            Assert.Contains("A trial is never a pre-registration", js);
            Assert.Contains("COMMITTED? NOT READ", js);                   // no reading is said, never an empty space
            Assert.Contains("d.labels", js);
        }
    }

    /// <summary>The plain-word labels: one table, and a test that fails when Recipe.cs accepts a field the table does not label.</summary>
    public class RecipeLabelTests
    {
        private static string RecipeSource => File.ReadAllText(Path.Combine(Where.Root, "src", "Evosim.Nursery", "Recipe.cs"));

        /// <summary>
        /// Every path Recipe.cs accepts, taken from Recipe.cs itself: the key lists it passes to <c>Exactly</c> (which refuses a key outside the list), each with
        /// the path of the object it checks. The exam's controls are checked under a variable (<c>at</c>), which stands for <c>exam.controls[i]</c>.
        /// </summary>
        internal static SortedSet<string> AcceptedPaths(string source, out SortedSet<string> variables)
        {
            var paths = new SortedSet<string>(StringComparer.Ordinal);
            variables = new SortedSet<string>(StringComparer.Ordinal);
            foreach (Match m in Regex.Matches(source, @"Exactly\(\s*\w+\s*,\s*(?<at>""[^""]*""|\w+)\s*,(?<keys>[^)]*)\)"))
            {
                string at = m.Groups["at"].Value;
                if (at.StartsWith("\"", StringComparison.Ordinal)) at = at.Trim('"');
                else { variables.Add(at); at = "exam.controls[]"; }
                foreach (Match k in Regex.Matches(m.Groups["keys"].Value, "\"([^\"]+)\""))
                    paths.Add(at == "recipe" ? k.Groups[1].Value : at + "." + k.Groups[1].Value);
            }
            // the one key Exactly never sees: Resolve reads a recipe's base and takes it out before the fields are checked
            if (source.Contains("node.Has(\"base\")")) paths.Add("base");
            return paths;
        }

        [Fact]
        public void TheKeyListsAreReadFromRecipeCsAndAreWhatTheTestExpects()
        {
            SortedSet<string> paths = AcceptedPaths(RecipeSource, out SortedSet<string> variables);
            Assert.Equal(new[] { "at" }, variables.ToArray());   // only the exam's controls are checked under a variable
            foreach (string known in new[] { "format", "base", "world.overrides", "bodies.keep", "setup.snow.islands.cover", "setup.purse.seconds", "search.crossover.mutationByDistance", "reward.parameters", "exam.controls", "exam.controls[].kind", "exam.controls[].gain", "exam.controls[].channels" })
                Assert.Contains(known, paths);
            Assert.True(paths.Count >= 60, "only " + paths.Count + " accepted paths were found in Recipe.cs");
        }

        [Fact]
        public void EveryFieldRecipeCsAcceptsHasALabel()
        {
            string[] unlabelled = AcceptedPaths(RecipeSource, out _).Where(p => RecipeLabels.Of(p) == null).ToArray();
            Assert.True(unlabelled.Length == 0, "Recipe.cs accepts these fields and RecipeLabels.cs does not label them: " + string.Join(", ", unlabelled));
        }

        [Fact]
        public void NoLabelIsLeftForAFieldRecipeCsDoesNotAccept()
        {
            SortedSet<string> accepted = AcceptedPaths(RecipeSource, out _);
            // an array's item (exam.controls[]) is labelled as the array's own path is accepted: Recipe.cs checks each item's keys under it
            string[] stale = RecipeLabels.Table.Keys.Where(k => !k.EndsWith(".*", StringComparison.Ordinal) && !accepted.Contains(k)
                && !(k.EndsWith("[]", StringComparison.Ordinal) && accepted.Contains(k.Substring(0, k.Length - 2)))).ToArray();
            Assert.True(stale.Length == 0, "RecipeLabels.cs labels these, which Recipe.cs does not accept: " + string.Join(", ", stale));
            // a map's entries are data: its wildcard stands under a path Recipe.cs accepts
            foreach (string k in RecipeLabels.Table.Keys.Where(k => k.EndsWith(".*", StringComparison.Ordinal)))
                Assert.Contains(k.Substring(0, k.Length - 2), accepted);
        }

        [Fact]
        public void TheCheckFailsWhenRecipeCsAcceptsAFieldTheTableLacks()
        {
            string source = RecipeSource;
            string withNew = Regex.Replace(source, @"(Exactly\(s, ""search"", ""population"",)", "$1 \"zzNewField\",");
            Assert.NotEqual(source, withNew);
            string[] unlabelled = AcceptedPaths(withNew, out _).Where(p => RecipeLabels.Of(p) == null).ToArray();
            Assert.Equal(new[] { "search.zzNewField" }, unlabelled);
        }

        [Fact]
        public void EveryLeafOfEveryRecipeUnderRoundsNurseryIsLabelledAsTheScreenWouldShowIt()
        {
            int loaded = 0;
            var unlabelled = new SortedSet<string>();
            foreach (string file in Directory.GetFiles(Path.Combine(Where.Root, "rounds", "nursery"), "*.json"))
            {
                if (Path.GetFileName(file) == "room.json") continue;
                string canonical;
                try { canonical = Recipe.Load(file).Canonical(); }
                catch (Exception e) when (e is InvalidDataException || e is IOException) { continue; }   // a recipe whose files are not in this tree
                catch (FormatException e) when (e.Message.Contains("Missing required field")) { continue; }   // a recipe whose world config this build refuses: the old brain2 six name configs/7319d6b528cc77f1.json, which lacks the round-56 tunables, and their -w2 namesakes configs/6eb86f181a6506ee.json, which lacks round 57's, and their -w3 namesakes configs/55aab9ae45f61732.json, which lacks round 59's (their -w4 namesakes are read instead)
                loaded++;
                void Walk(JsonElement e, string at)
                {
                    if (e.ValueKind == JsonValueKind.Object && e.EnumerateObject().Any())
                        foreach (JsonProperty p in e.EnumerateObject()) Walk(p.Value, at.Length == 0 ? p.Name : at + "." + p.Name);
                    else if (e.ValueKind == JsonValueKind.Array && e.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.Object))
                    {
                        if (RecipeLabels.Of(at + "[0]") == null) unlabelled.Add(at + "[]");
                        foreach (JsonElement x in e.EnumerateArray()) Walk(x, at + "[]");
                    }
                    else if (RecipeLabels.Of(at) == null) unlabelled.Add(at);
                }
                // the heading fields are listed under 'recipe.' on the screen
                JsonElement root = Js.Parse(canonical);
                foreach (JsonProperty p in root.EnumerateObject())
                    Walk(p.Value, new[] { "format", "name", "description", "seed" }.Contains(p.Name) ? "recipe." + p.Name : p.Name);
            }
            Assert.True(loaded >= 2, "only " + loaded + " recipes under rounds/nursery loaded");
            Assert.True(unlabelled.Count == 0, "fields of the recipes under rounds/nursery with no label: " + string.Join(", ", unlabelled));
        }

        [Fact]
        public void ALabelIsFoundByPathWithItemsSharingOneAndMapEntriesNamedByTheirOwnKey()
        {
            Assert.Equal("recipe name", RecipeLabels.Of("recipe.name"));
            Assert.Equal("genomes per generation", RecipeLabels.Of("search.population"));
            Assert.Equal("exam brain", RecipeLabels.Of("exam.controls[0]"));
            Assert.Equal(RecipeLabels.Of("exam.controls[0]"), RecipeLabels.Of("exam.controls[11]"));
            Assert.Equal("world setting changed: WorkCostMultiplier", RecipeLabels.Of("world.overrides.WorkCostMultiplier"));
            Assert.Equal("world setting changed: Development.MaxPartVolume", RecipeLabels.Of("world.overrides.Development.MaxPartVolume"));
            Assert.Equal("world settings changed", RecipeLabels.Of("world.overrides"));
            Assert.Equal("reward parameter: lambda", RecipeLabels.Of("reward.parameters.lambda"));
            Assert.Null(RecipeLabels.Of("setup.nonsense"));
        }

        [Fact]
        public void EveryLabelIsOneShortPlainLine()
        {
            foreach (KeyValuePair<string, string> kv in RecipeLabels.Table)
            {
                Assert.False(string.IsNullOrWhiteSpace(kv.Value), kv.Key);
                Assert.True(kv.Value.Length <= 60, kv.Key + ": " + kv.Value);
                Assert.DoesNotContain('\n', kv.Value);
            }
        }
    }
}
