using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// A settled crowd resumed on the gpu engine is its unbroken run, on ILGPU's CPU accelerator in
    /// single precision with the card flags a round runs with (round 60's build).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Round 59's B3 resumed a card world 45,000 s in and parted from its run at the first second,
    /// while the founded check's resume at 15 s matched. The checkpoint dropped what the card holds
    /// in two parts, each link centre's float and its rest, and the restore rebuilt the centres from
    /// the poses with the rest at zero (<c>logbook/specs/r59-resume-survey.md</c>). At 15 s few
    /// bodies rest on the floor, so a zero rest changed nothing yet. <c>CheckpointFidelity</c> runs
    /// the CPU engine, where the centre is the position, and could not see it.
    /// </para>
    /// <para>
    /// So the crowd here is settled first: founded from the floor on the CPU engine and run for
    /// <see cref="SettleSeconds"/>, then taken onto the accelerator, which runs 20 s with a
    /// checkpoint at 10 s. A second run resumes that checkpoint, and every digest row and stats
    /// row after it must be the unbroken run's. Slow: about ten minutes at four threads.
    /// </para>
    /// </remarks>
    [Collection(FarmProcessCollection.Name)]
    public class GpuSettledResumeTests
    {
        private const int SettleSeconds = 2000;

        private readonly ITestOutputHelper _output;

        public GpuSettledResumeTests(ITestOutputHelper output) => _output = output;

        private static List<string> Tank(string runs, string arm, int seconds, int checkpointEvery, bool card)
        {
            var settings = new List<string>(
                FilmWindowTests.SmallRun(runs, arm, seconds, checkpointEvery).Where(l => !l.StartsWith("EVOSIM_WALL_MINUTES=")))
            {
                "EVOSIM_WALL_MINUTES=60",
                "EVOSIM_SEED=1",
                "EVOSIM_SHAPE=tank",
                "EVOSIM_CURRENT_MODE=Transport",
                "EVOSIM_CURRENT=0.05",
                "EVOSIM_WATER_HOLD=0",
                "EVOSIM_SENSE_SCENT=1",
                "EVOSIM_CORPSE_DECAY=0.000385",
                "EVOSIM_CONTACT_PER_PART=1",
            };

            if (card)
            {
                settings.AddRange(new[]
                {
                    "EVOSIM_DIGEST_EVERY=1",
                    "EVOSIM_ENGINE=gpu",
                    "EVOSIM_GPU_DEVICE=cpu",
                    "EVOSIM_GPU_PRECISION=single",
                    "EVOSIM_GPU_GROUP=32",
                    "EVOSIM_GPU_SCENT=1",
                    "EVOSIM_GPU_TRANSPORT=1",
                    "EVOSIM_GPU_CONCURRENT=1",
                });
            }

            return settings;
        }

        private static string RunIn(string runs, string arm) => Directory.GetDirectories(Path.Combine(runs, arm)).Single();

        private static List<(long Step, string Row)> Digests(string run) =>
            File.ReadAllLines(Path.Combine(run, "digest.jsonl"))
                .Where(l => l.Length > 0)
                .Select(l => ((long)Json.Parse(l)["step"].AsDouble(), l))
                .ToList();

        /// <summary>
        /// A stats row as written, less what a resume counts from its own start: the wall timings and
        /// two step counters, all plain numbers.
        /// </summary>
        private static string Comparable(string line) =>
            Regex.Replace(line, "\"(wall[A-Za-z]*|harnessBodySteps|fluidLinkSteps)\":[^,}]*,?", "");

        private static List<string> StatsAfter(string run, double t) =>
            File.ReadAllLines(Path.Combine(run, "stats.jsonl"))
                .Where(l => l.Length > 0 && Json.Parse(l)["t"].AsDouble() > t + 1e-6)
                .Select(Comparable)
                .ToList();

        [Fact]
        [Trait("Category", "Slow")]
        public void ASettledCrowdResumedOnTheAcceleratorIsItsUnbrokenRun()
        {
            string root = Path.Combine(AppContext.BaseDirectory, "gpu-settled", Guid.NewGuid().ToString("N"));
            try
            {
                string runs = Path.Combine(root, "runs");
                Directory.CreateDirectory(runs);

                // The crowd, settled on the CPU engine and checkpointed at its end.
                Assert.Equal(0, Program.Main(Tank(runs, "settle", SettleSeconds, SettleSeconds, card: false).ToArray()));
                string settled = RunIn(runs, "settle");

                // The unbroken run on the accelerator: 20 s from the settled crowd, a checkpoint at 10 s.
                // A cousin of the settling run, since the engine changes; the comparison is between the
                // two accelerator runs, the second resumed from the first's own checkpoint.
                var whole = Tank(runs, "whole", SettleSeconds + 20, 10, card: true);
                whole.Add("EVOSIM_RESUME=" + settled);
                whole.Add("EVOSIM_RESUME_AT=" + SettleSeconds);
                whole.Add("EVOSIM_ALLOW_SOURCE_MISMATCH=1");
                Assert.Equal(0, Program.Main(whole.ToArray()));
                string unbroken = RunIn(runs, "whole");

                // Its second half again, from its own checkpoint at 10 s.
                var half = Tank(runs, "half", SettleSeconds + 20, 10, card: true);
                half.Add("EVOSIM_RESUME=" + unbroken);
                half.Add("EVOSIM_RESUME_AT=" + (SettleSeconds + 10));
                Assert.Equal(0, Program.Main(half.ToArray()));
                string resumed = RunIn(runs, "half");

                List<(long Step, string Row)> fromResume = Digests(resumed);
                Assert.NotEmpty(fromResume);
                long first = fromResume[0].Step;
                List<(long Step, string Row)> fromWhole = Digests(unbroken).Where(d => d.Step >= first).ToList();

                int alike = 0;
                while (alike < Math.Min(fromWhole.Count, fromResume.Count) && fromWhole[alike].Row == fromResume[alike].Row) alike++;
                _output.WriteLine($"digest rows from step {first}: {fromResume.Count} resumed, {fromWhole.Count} unbroken, {alike} alike");

                Assert.True(fromResume.Count >= 400, "too few digest rows to compare: " + fromResume.Count);
                Assert.True(alike == fromResume.Count && fromWhole.Count == fromResume.Count,
                    "the resume parts from the run at step " + (alike < fromResume.Count ? fromResume[alike].Step.ToString() : "-") +
                    ": " + (alike < fromResume.Count ? fromResume[alike].Row : "") + " against " +
                    (alike < fromWhole.Count ? fromWhole[alike].Row : "(no row)"));

                Assert.Equal(StatsAfter(unbroken, SettleSeconds + 10), StatsAfter(resumed, SettleSeconds + 10));
            }
            finally
            {
                try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch (IOException) { }
            }
        }
    }
}
