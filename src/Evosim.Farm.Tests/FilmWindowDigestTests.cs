using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Evosim.Core;
using Evosim.Farm;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// A window of a world with contact eating off is the window the farm wrote before the remains reached it:
    /// the same frames, genomes, plans, events and verdict counts, bit for bit, and no remains file.
    /// </summary>
    /// <remarks>
    /// <b>How the digest was obtained.</b> This file was built and run, unchanged, on the worktree as it stood before
    /// the remains rows were added to <c>FilmWindow</c> (main at <c>d17d2878</c>), and the digest printed there is
    /// <see cref="PinnedDigest"/>. The same file then ran on the tree with the remains rows and printed the same
    /// digest. It covers what a theatre reads of a window: the pose stream's bytes, every genome row, every plans
    /// row, every event row, the identity rows before the verdict, and the verdict's counts and word (not its wall
    /// time, thread count or paths, which measure the machine).
    /// </remarks>
    [Collection(FarmProcessCollection.Name)]
    public class FilmWindowDigestTests
    {
        private readonly ITestOutputHelper _out;

        public FilmWindowDigestTests(ITestOutputHelper output) => _out = output;

        /// <summary>The digest taken on the tree before the remains rows (see the remarks).</summary>
        private const string PinnedDigest = "CHo4QtXZ6xmIVVak47v3guHGjrNmomNA5fcWcC8kCy0=";

        private static void Mix(HashAlgorithm sha, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text + "\n");
            sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }

        private static IEnumerable<string> PlainLines(string path)
        {
            if (!File.Exists(path)) yield break;

            foreach (string line in File.ReadAllText(path).Split('\n'))
            {
                if (line.Length > 0) yield return line.TrimEnd('\r');
            }
        }

        /// <summary>The digest of a window's directory: see the class remarks for what it covers.</summary>
        internal static string DigestOf(string windowDirectory, out string summary)
        {
            var sha = SHA256.Create();
            var counts = new StringBuilder();

            byte[] poses = File.ReadAllBytes(Path.Combine(windowDirectory, FilmWindow.PosesFileName));
            sha.TransformBlock(poses, 0, poses.Length, null, 0);
            counts.Append("poses ").Append(poses.Length);

            int genomes = 0;
            foreach (string line in GzipMemberReader.ReadLines(Path.Combine(windowDirectory, FilmWindow.GenomesFileName), out _))
            {
                Mix(sha, line);
                genomes++;
            }

            counts.Append(", genomes ").Append(genomes);

            int plans = 0;
            foreach (string line in PlainLines(Path.Combine(windowDirectory, FilmWindow.PlansFileName)))
            {
                Mix(sha, line);
                plans++;
            }

            counts.Append(", plans ").Append(plans);

            int events = 0;
            foreach (string line in PlainLines(Path.Combine(windowDirectory, FilmWindow.EventsFileName)))
            {
                Mix(sha, line);
                events++;
            }

            counts.Append(", events ").Append(events);

            var identity = new List<string>(PlainLines(Path.Combine(windowDirectory, FilmWindow.IdentityFileName)));
            for (int i = 0; i < identity.Count - 1; i++) Mix(sha, identity[i]);
            counts.Append(", identity ").Append(identity.Count);

            FilmWindowVerdict v = FilmWindowReader.ReadVerdictIn(windowDirectory);
            Mix(sha, string.Join("|", v.Verdict, v.Reason, v.Frames, v.Bodies, v.Births, v.Deaths, v.Bites,
                v.BirthsWithoutAGenome, v.Rows, v.RowsAgreed, v.Corpses, v.CorpseSteps));

            sha.TransformFinalBlock(new byte[0], 0, 0);
            summary = counts.ToString();
            return Convert.ToBase64String(sha.Hash);
        }

        [Fact]
        public void AWindowOfAWorldWithContactEatingOffIsTheWindowTheFarmWroteBeforeTheRemainsRows()
        {
            string root = Path.Combine(AppContext.BaseDirectory, "film-digest-tests", Guid.NewGuid().ToString("N"));

            try
            {
                string runsRoot = Path.Combine(root, "runs");
                Assert.Equal(0, Program.Main(FilmWindowTests.SmallRun(runsRoot, "plain", seconds: 30, checkpointEvery: 10)));
                string run = Directory.GetDirectories(Path.Combine(runsRoot, "plain"))[0];

                string first = Path.Combine(root, "first");
                FilmWindow.Result result = FilmWindow.Film(
                    new FilmWindow.Options { Run = run, FromSeconds = 12d, ToSeconds = 18d, Out = first, FramesPerSecond = 10d, Threads = 2 },
                    setting => null,
                    TextWriter.Null);

                Assert.Equal(FilmWindow.Faithful, result.Verdict);

                string digest = DigestOf(first, out string summary);
                _out.WriteLine("window digest: " + digest + " (" + summary + ")");

                string second = Path.Combine(root, "second");
                FilmWindow.Film(
                    new FilmWindow.Options { Run = run, FromSeconds = 12d, ToSeconds = 18d, Out = second, FramesPerSecond = 10d, Threads = 2 },
                    setting => null,
                    TextWriter.Null);

                Assert.Equal(digest, DigestOf(second, out _));

                Assert.False(File.Exists(Path.Combine(first, "remains.jsonl.gz")));
                Assert.Equal(PinnedDigest, digest);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(root)) Directory.Delete(root, true);
                }
                catch (IOException)
                {
                }
            }
        }
    }
}
