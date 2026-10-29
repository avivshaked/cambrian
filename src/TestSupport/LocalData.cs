using System;
using System.Collections.Concurrent;
using System.IO;
using Xunit;

namespace Evosim.TestSupport
{
    /// <summary>
    /// A fact that reads local data it did not make: a run, a checkpoint, a report or a pool that sits in a gitignored folder
    /// (<c>runs/</c>, <c>rounds/nursery/pools/</c>) and is too large to commit. Each path is declared here, relative to the
    /// repository root with forward slashes, so a search for the path finds every test that needs it and
    /// <c>scripts/test-pins.py</c> lists them as the keep list for a disk cleanup. Where a declared path is absent the fact is reported
    /// as skipped with the path named, in the run's output: it never fails for the absence and never passes silently.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Looked for in this tree and in every folder above it</b>, as the tests that walked up from the test binary already did, because a
    /// worktree under <c>scratch/</c> holds none of the main checkout's <c>runs/</c>. A test reads the same data through
    /// <see cref="LocalData.Find"/> so that what the attribute found is what the test opens. <see cref="LocalDataTreeFactAttribute"/> looks in this
    /// tree alone, for a test whose code (or a child process it starts) reads the path from the repository root it was built in.
    /// </para>
    /// <para>
    /// A path may hold <c>*</c> in a segment (<c>runs/*/*/run.json</c>), which is satisfied by any match. xunit 2.9.2 has no runtime
    /// skip, so the skip is decided when the tests are discovered: a path that appears afterwards is picked up by the next discovery.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    internal class LocalDataFactAttribute : FactAttribute
    {
        public LocalDataFactAttribute(params string[] paths)
        {
            string why = LocalData.Missing(paths, thisTreeOnly: false);
            if (why != null) Skip = why;
        }
    }

    /// <summary>As <see cref="LocalDataFactAttribute"/>, for a theory.</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    internal class LocalDataTheoryAttribute : TheoryAttribute
    {
        public LocalDataTheoryAttribute(params string[] paths)
        {
            string why = LocalData.Missing(paths, thisTreeOnly: false);
            if (why != null) Skip = why;
        }
    }

    /// <summary>As <see cref="LocalDataFactAttribute"/>, but the paths are looked for under this tree's repository root alone.</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    internal class LocalDataTreeFactAttribute : FactAttribute
    {
        public LocalDataTreeFactAttribute(params string[] paths)
        {
            string why = LocalData.Missing(paths, thisTreeOnly: true);
            if (why != null) Skip = why;
        }
    }

    /// <summary>As <see cref="LocalDataTreeFactAttribute"/>, for a theory.</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    internal class LocalDataTreeTheoryAttribute : TheoryAttribute
    {
        public LocalDataTreeTheoryAttribute(params string[] paths)
        {
            string why = LocalData.Missing(paths, thisTreeOnly: true);
            if (why != null) Skip = why;
        }
    }

    /// <summary>Where the local data a test reads is, found the way the attributes found it.</summary>
    internal static class LocalData
    {
        private static readonly ConcurrentDictionary<string, string> Roots = new ConcurrentDictionary<string, string>();

        /// <summary>
        /// This tree's repository root: the nearest folder above the test binary that holds <c>DESIGN.md</c> (the binary is built under
        /// <c>artifacts/</c>, inside the repository). Null where there is none.
        /// </summary>
        public static string RepoRoot
        {
            get
            {
                for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
                    if (File.Exists(Path.Combine(dir.FullName, "DESIGN.md"))) return dir.FullName;
                return null;
            }
        }

        /// <summary>
        /// The folder, this tree's root or one above it (nearest first), under which <paramref name="relative"/> exists; null if none does.
        /// <paramref name="thisTreeOnly"/> looks under the root alone.
        /// </summary>
        public static string FindRoot(string relative, bool thisTreeOnly = false)
        {
            string found = Roots.GetOrAdd(relative + "|" + thisTreeOnly, _ => Search(relative, thisTreeOnly) ?? "");
            return found.Length == 0 ? null : found;
        }

        /// <summary>The full path of <paramref name="relative"/> where it exists (see <see cref="FindRoot"/>), else null. No wildcard here.</summary>
        public static string Find(string relative, bool thisTreeOnly = false)
        {
            string root = FindRoot(relative, thisTreeOnly);
            return root == null ? null : Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>The skip message naming the first of <paramref name="paths"/> that is absent, or null when all are there.</summary>
        public static string Missing(string[] paths, bool thisTreeOnly)
        {
            if (paths == null || paths.Length == 0) return "skipped: the attribute names no path, so there is nothing to look for";
            foreach (string path in paths)
            {
                if (FindRoot(path, thisTreeOnly) != null) continue;
                string where = thisTreeOnly ? "in this tree (" + (RepoRoot ?? AppContext.BaseDirectory) + ")" : "in this tree or in any folder above it";
                return "skipped: " + path + " is not on this machine, " + where + ". It is gitignored data (a run, a checkpoint, a report or a pool) " +
                       "that this test reads and did not make; scripts/test-pins.py lists every such path.";
            }
            return null;
        }

        private static string Search(string relative, bool thisTreeOnly)
        {
            string[] parts = relative.Split('/');
            if (thisTreeOnly)
            {
                string root = RepoRoot;
                return root != null && Matches(root, parts, 0) ? root : null;
            }
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
                if (Matches(dir.FullName, parts, 0)) return dir.FullName;
            return null;
        }

        private static bool Matches(string baseDir, string[] parts, int at)
        {
            if (at == parts.Length) return true;
            string part = parts[at];
            bool last = at == parts.Length - 1;
            try
            {
                if (part.IndexOf('*') < 0)
                {
                    string next = Path.Combine(baseDir, part);
                    if (last) return File.Exists(next) || Directory.Exists(next);
                    return Directory.Exists(next) && Matches(next, parts, at + 1);
                }

                if (!Directory.Exists(baseDir)) return false;
                foreach (string entry in Directory.EnumerateFileSystemEntries(baseDir, part))
                {
                    if (last) return true;
                    if (Directory.Exists(entry) && Matches(entry, parts, at + 1)) return true;
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return false;
        }
    }
}
