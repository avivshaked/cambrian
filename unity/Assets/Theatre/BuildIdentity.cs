using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Evosim.Theatre
{
    /// <summary>
    /// What this build is, in the same terms <c>run.json</c> records — DESIGN.md §7.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The theatre shows a faithful replay or says it cannot.</b> Bit-identical replay is
    /// measured same-machine, same-build (CLAUDE.md's "PhysX replays bit for bit"); a run
    /// recorded by different source is not a replay, it is a cousin. <c>run.json</c> carries
    /// <c>source.coreHash</c> and <c>source.simHash</c> precisely so the difference can be
    /// <i>detected</i>, and this is the reader's half of that.
    /// </para>
    /// <para>
    /// <b>The digest is duplicated from <c>EvolutionRun.HashSourceTree</c> deliberately, not
    /// shared.</b> That method is private to an editor-only assembly the theatre must not
    /// reference (§6.1 keeps the farm and the theatre separate programs), and the algorithm is
    /// already a three-way contract — the C#, <c>scripts/run-arm.ps1</c>, and now this. What
    /// keeps the copies honest is that they are checked against the same file: a mismatch here
    /// against a manifest this build wrote is a bug in this copy, and shows up the first time
    /// anyone replays their own recording.
    /// </para>
    /// <para>
    /// <b>The theatre is deliberately outside the tree <c>simHash</c> covers.</b> The digest is
    /// taken over <c>Assets/Evosim</c>; the theatre lives at <c>Assets/Theatre</c>, so editing a
    /// HUD label cannot refuse a recording and cannot make a refreshed worker report a new build
    /// to the farm's identity record. It sat inside for one commit and did both. What that buys
    /// is that a difference reported here is a difference in simulation source and nothing else —
    /// which is what makes refusing on it worth doing. <see cref="Difference"/> still names which
    /// of the two hashes moved, because a <c>coreHash</c> difference and a <c>simHash</c> one are
    /// different halves of the build.
    /// </para>
    /// </remarks>
    public static class BuildIdentity
    {
        /// <summary>The worker or project this assembly is running out of.</summary>
        public static string ProjectPath => Path.GetDirectoryName(Application.dataPath);

        /// <summary>
        /// The repository root, by the same rule <c>EvolutionRun.BuildManifest</c> uses:
        /// <c>EVOSIM_REPO_ROOT</c>, else the project's parent directory.
        /// </summary>
        public static string RepositoryRoot()
        {
            string set = Environment.GetEnvironmentVariable("EVOSIM_REPO_ROOT");
            if (!string.IsNullOrEmpty(set)) return set;

            return Path.GetFullPath(Path.Combine(ProjectPath, ".."));
        }

        /// <summary>SHA-256 over <c>src/Evosim.Core</c>, or "unknown".</summary>
        public static string CoreHash() =>
            HashSourceTree(Path.Combine(RepositoryRoot(), "src", "Evosim.Core")) ?? "unknown";

        /// <summary>
        /// SHA-256 over this project's <c>Assets/Evosim</c>, or "unknown" — the simulation source
        /// only, which is what <c>EvolutionRun</c> and <c>run-arm.ps1</c> hash and therefore what
        /// a recorded <c>simHash</c> can be compared against. The theatre's own tree is not in it,
        /// by design (see the class remarks).
        /// </summary>
        public static string SimHash() =>
            HashSourceTree(Path.Combine(Application.dataPath, "Evosim")) ?? "unknown";

        /// <summary>
        /// How this build differs from the source a run recorded, or null when it does not.
        /// </summary>
        /// <param name="recordedCoreHash">run.json's <c>source.coreHash</c>.</param>
        /// <param name="recordedSimHash">run.json's <c>source.simHash</c>.</param>
        public static string Difference(string recordedCoreHash, string recordedSimHash)
        {
            string core = CoreHash();
            string sim = SimHash();

            bool coreDiffers = !Equal(core, recordedCoreHash);
            bool simDiffers = !Equal(sim, recordedSimHash);

            if (!coreDiffers && !simDiffers) return null;

            var sb = new StringBuilder();

            if (coreDiffers)
            {
                sb.Append("coreHash ").Append(Short(recordedCoreHash))
                  .Append(" recorded, ").Append(Short(core)).Append(" here");
            }

            if (simDiffers)
            {
                if (sb.Length > 0) sb.Append("; ");
                sb.Append("simHash ").Append(Short(recordedSimHash))
                  .Append(" recorded, ").Append(Short(sim)).Append(" here");
            }

            return sb.ToString();
        }

        /// <summary>Missing on either side is a difference, not a match.</summary>
        private static bool Equal(string a, string b) =>
            !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) &&
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        public static string Short(string hash) =>
            string.IsNullOrEmpty(hash) ? "(none)" :
            hash.Length <= 12 ? hash : hash.Substring(0, 12) + "…";

        /// <summary>
        /// SHA-256 over every <c>.cs</c> under <paramref name="root"/>, or null if there are
        /// none. Byte-compatible with <c>EvolutionRun.HashSourceTree</c> and with
        /// <c>run-arm.ps1</c>'s <c>Get-SourceTreeHash</c>.
        /// </summary>
        /// <remarks>
        /// For every file, in ordinal order of its path relative to the root with <c>/</c>
        /// separators: <c>relativePath \n sha256OfBytes \n</c>; SHA-256 over that string, in
        /// lowercase hex. Filtered by extension rather than by search pattern, because
        /// <c>Directory.GetFiles(root, "*.cs")</c> also matches longer extensions through 8.3
        /// short names on Windows.
        /// </remarks>
        public static string HashSourceTree(string root)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return null;

            string full = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            var files = new List<string>();

            foreach (string path in Directory.GetFiles(full, "*", SearchOption.AllDirectories))
            {
                if (string.Equals(Path.GetExtension(path), ".cs", StringComparison.OrdinalIgnoreCase))
                {
                    files.Add(path);
                }
            }

            if (files.Count == 0) return null;

            var relative = new List<string>(files.Count);
            var byRelative = new Dictionary<string, string>(files.Count, StringComparer.Ordinal);

            foreach (string path in files)
            {
                string rel = Path.GetFullPath(path)
                    .Substring(full.Length + 1)
                    .Replace(Path.DirectorySeparatorChar, '/')
                    .Replace('\\', '/');

                relative.Add(rel);
                byRelative[rel] = path;
            }

            relative.Sort(StringComparer.Ordinal);

            var manifest = new StringBuilder();
            using (SHA256 sha256 = SHA256.Create())
            {
                foreach (string rel in relative)
                {
                    byte[] digest = sha256.ComputeHash(File.ReadAllBytes(byRelative[rel]));
                    manifest.Append(rel).Append('\n')
                        .Append(BitConverter.ToString(digest).Replace("-", "").ToLowerInvariant())
                        .Append('\n');
                }

                byte[] total = sha256.ComputeHash(
                    new UTF8Encoding(false).GetBytes(manifest.ToString()));

                return BitConverter.ToString(total).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
