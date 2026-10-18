using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Evosim.NurseryRoom
{
    /// <summary>What a git command did: its exit code and both streams, or why it did not finish (<see cref="Failure"/>).</summary>
    public sealed class GitResult
    {
        public int ExitCode { get; set; }
        public string Output { get; set; } = "";
        public string Error { get; set; } = "";

        /// <summary>Set when git did not run to an end (it could not start, or did not finish in time): nothing else here is then to be read.</summary>
        public string Failure { get; set; }
    }

    /// <summary>Runs git with these arguments in this folder, ending it after the time given.</summary>
    public delegate GitResult GitRunner(string workingDirectory, IReadOnlyList<string> arguments, int timeoutMs);

    /// <summary>The commit that last touched a recipe file.</summary>
    public sealed class RecipeLastCommit
    {
        public string Hash { get; set; }
        public string Date { get; set; }
        public string Subject { get; set; }
    }

    /// <summary>
    /// Whether a recipe file is committed, as the recipe screen and the recipe resource say it. A pre-registered run launches only from a committed
    /// recipe, so the state is shown before anything is launched from the file; a trial from an uncommitted recipe is still allowed.
    /// </summary>
    public sealed class RecipeGitState
    {
        /// <summary><c>committed</c>, <c>changed</c>, <c>untracked</c>, <c>outside</c> or <c>unreadable</c>.</summary>
        public string State { get; set; }

        /// <summary>The state in a few words, for a plate.</summary>
        public string Label { get; set; }

        /// <summary>The state in a sentence: what git said, and for a committed or changed file the commit it was last in.</summary>
        public string Detail { get; set; }

        /// <summary>The last commit that touched the file: set for <c>committed</c> and <c>changed</c>, null otherwise.</summary>
        public RecipeLastCommit LastCommit { get; set; }

        /// <summary>Git's own message, for <c>outside</c> and <c>unreadable</c>: what it wrote, or why it did not run; null otherwise.</summary>
        public string GitMessage { get; set; }
    }

    /// <summary>
    /// Asks git, read-only, whether a recipe file is committed: <c>status --porcelain</c> for its changes and <c>log -1</c> for the commit it was last in,
    /// both run in the room's repository root (as the nursery reads the build, with no optional locks) and each with a timeout. A git that cannot be read
    /// is never taken for clean: the state is then <c>outside</c> (the folder is no repository) or <c>unreadable</c> (anything else), with git's own message.
    /// </summary>
    public static class RecipeGit
    {
        public const int DefaultTimeoutMs = 10000;
        private const char Sep = '\u001f';

        public static RecipeGitState Read(string root, string relative, GitRunner git = null, int timeoutMs = DefaultTimeoutMs)
        {
            git = git ?? ((dir, args, ms) => RunGit(dir, args, ms));
            // a literal path (no glob), relative to the root the commands run in
            string spec = ":(literal)" + relative.Replace('\\', '/');

            GitResult status = git(root, new[] { "--no-optional-locks", "status", "--porcelain", "--", spec }, timeoutMs);
            RecipeGitState bad = Cannot(status, "git status");
            if (bad != null) return bad;
            string[] lines = status.Output.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).Where(l => l.Length >= 2).ToArray();

            if (lines.Any(l => l.StartsWith("??", StringComparison.Ordinal)))
                return Made("untracked", "not committed", "git does not track this file: it has never been committed.");

            GitResult log = git(root, new[] { "--no-optional-locks", "log", "-1", "--format=%h" + "%x1f" + "%cs" + "%x1f" + "%s", "--", spec }, timeoutMs);
            RecipeLastCommit last = null;
            if (log.Failure == null && log.ExitCode != 0 && log.Error.Contains("does not have any commits yet", StringComparison.Ordinal))
            {
                // a repository with no commit: nothing has been committed
            }
            else
            {
                bad = Cannot(log, "git log");
                if (bad != null) return bad;
                string[] parts = log.Output.TrimEnd('\r', '\n').Split(Sep);
                if (parts.Length >= 3 && parts[0].Length > 0) last = new RecipeLastCommit { Hash = parts[0], Date = parts[1], Subject = string.Join(Sep.ToString(), parts.Skip(2)) };
                else if (log.Output.Trim().Length > 0) return Unreadable("git log gave an answer that could not be read: " + log.Output.Trim());
            }

            if (last == null)
            {
                string staged = lines.Any(l => l[0] == 'A') ? "it is added to the index and not yet committed." : "git lists no commit for it and no change: the file is not tracked, or the repository ignores it.";
                return Made("untracked", "not committed", "git does not hold this file in any commit: " + staged);
            }

            string was = $"{last.Hash}, {last.Date}, {last.Subject}";
            if (lines.Length > 0)
            {
                bool index = lines.Any(l => l[0] != ' ' && l[0] != '?'), tree = lines.Any(l => l[1] != ' '), conflict = lines.Any(l => l[0] == 'U' || l[1] == 'U');
                string where = conflict ? "has an unresolved merge conflict" : index && tree ? "has changes staged in the index and more in the working tree" : index ? "has changes staged in the index" : "has changes in the working tree";
                return new RecipeGitState { State = "changed", Label = "changed since its last commit", Detail = $"The file {where} since its last commit ({was}).", LastCommit = last };
            }
            return new RecipeGitState { State = "committed", Label = "committed", Detail = $"Committed and unchanged since its last commit ({was}).", LastCommit = last };
        }

        private static RecipeGitState Made(string state, string label, string detail) => new RecipeGitState { State = state, Label = label, Detail = detail };

        private static RecipeGitState Unreadable(string message) =>
            new RecipeGitState { State = "unreadable", Label = "git cannot be read", Detail = "Git could not be read for this file, so whether it is committed is not known: " + message, GitMessage = message };

        /// <summary>The state for a command that did not give an answer (null when it did): outside any repository, or not readable.</summary>
        private static RecipeGitState Cannot(GitResult r, string what)
        {
            if (r == null) return Unreadable(what + " gave no result");
            if (r.Failure != null) return Unreadable(what + " " + r.Failure);
            if (r.ExitCode == 0) return null;
            string message = (r.Error ?? "").Trim();
            if (message.Length == 0) message = "exit " + r.ExitCode;
            if (message.Contains("not a git repository", StringComparison.OrdinalIgnoreCase))
                return new RecipeGitState { State = "outside", Label = "outside the repository", Detail = "The room's folder is not inside a git repository, so whether this file is committed is not known: " + message, GitMessage = message };
            return Unreadable(what + " exit " + r.ExitCode + ": " + message);
        }

        /// <summary>
        /// Runs git and gives its answer, or why it gave none: it could not start, or did not finish within <paramref name="timeoutMs"/> (it is then ended, with the
        /// processes it started). Both streams are read at once, so that a git that writes a lot cannot stall on a full pipe. <paramref name="program"/>
        /// and <paramref name="environment"/> are for the tests.
        /// </summary>
        public static GitResult RunGit(string workingDirectory, IReadOnlyList<string> arguments, int timeoutMs, IDictionary<string, string> environment = null, string program = "git")
        {
            try
            {
                var info = new ProcessStartInfo(program)
                {
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = new UTF8Encoding(false),
                    StandardErrorEncoding = new UTF8Encoding(false),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                foreach (string a in arguments) info.ArgumentList.Add(a);
                info.Environment["GIT_TERMINAL_PROMPT"] = "0";
                if (environment != null) foreach (KeyValuePair<string, string> kv in environment) info.Environment[kv.Key] = kv.Value;
                using (var process = Process.Start(info))
                {
                    Task<string> output = process.StandardOutput.ReadToEndAsync(), error = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(timeoutMs))
                    {
                        try { process.Kill(true); } catch (Exception) { /* it ended meanwhile */ }
                        return new GitResult { Failure = $"timed out after {timeoutMs / 1000.0:0.#} s" };
                    }
                    process.WaitForExit();
                    // a child git left running can keep the pipes open: the answer is then not complete
                    if (!Task.WaitAll(new Task[] { output, error }, 5000)) return new GitResult { Failure = "did not finish writing its answer" };
                    return new GitResult { ExitCode = process.ExitCode, Output = output.Result, Error = error.Result };
                }
            }
            catch (Exception e)
            {
                return new GitResult { Failure = "could not run: " + e.GetType().Name + ": " + e.Message };
            }
        }
    }
}
