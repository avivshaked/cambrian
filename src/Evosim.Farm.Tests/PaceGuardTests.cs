using System;
using System.Collections.Generic;
using System.IO;
using Evosim.Dynamics;
using Evosim.Farm;
using Xunit;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// The pace guard, the cap watch and the contact window it quotes (proposals/propose-farm-pace-guard.md):
    /// when the guard trips and when it does not, the probe rows before its stop, and the reason's parts. The
    /// guard reads the wall clock, so these drive it with made-up rows; that a run it stops resumes to the same
    /// bits is the gate's to show, on the card.
    /// </summary>
    public class PaceGuardTests
    {
        // A row every 10 simulated seconds at 8 s of wall, the pace round 61 ran at.
        private const double RowSeconds = 10, RowWallMs = 8000;

        private static PaceGuard Guard(bool on = true, int rows = 2, int probeRows = 1, double warmup = 10,
            double tripAt = -1) => new PaceGuard(on, 0.5, rows, probeRows, 10, 60, warmup, tripAt);

        /// <summary>Feeds <paramref name="count"/> rows at <paramref name="wallPerRow"/> ms each and returns the acts.</summary>
        private static List<PaceGuard.Act> Feed(PaceGuard g, ref double wall, ref double t, int count,
            double wallPerRow, bool canProbe = true)
        {
            var acts = new List<PaceGuard.Act>();
            for (int i = 0; i < count; i++)
            {
                wall += wallPerRow;
                t += RowSeconds;
                acts.Add(g.Row(wall, t, canProbe));
            }

            return acts;
        }

        [Fact]
        public void ASteadyPaceNeverTrips()
        {
            PaceGuard g = Guard();
            double wall = 0, t = 45000;
            List<PaceGuard.Act> acts = Feed(g, ref wall, ref t, 2000, RowWallMs);
            Assert.All(acts, a => Assert.Equal(PaceGuard.Act.None, a));
        }

        [Fact]
        public void TheRecentWindowPastTwiceTheReferenceTurnsTheProbeOnThenStopsAfterItsRow()
        {
            // After k rows at three times the wall, the last ten minutes hold those k and 75 - 3k of the steady
            // rows before them: 600 s of wall over 750 - 20k simulated seconds, past 1.6 (twice 0.8) at k = 19.
            PaceGuard g = Guard();
            double wall = 0, t = 45000;
            Feed(g, ref wall, ref t, 600, RowWallMs);
            double slowFrom = t;

            List<PaceGuard.Act> acts = Feed(g, ref wall, ref t, 18, 3 * RowWallMs);
            Assert.All(acts, a => Assert.Equal(PaceGuard.Act.None, a));

            Assert.Equal(PaceGuard.Act.Probe, Feed(g, ref wall, ref t, 1, 3 * RowWallMs)[0]);
            Assert.Equal(slowFrom - 18 * RowSeconds, g.SlowFrom);
            Assert.Equal(37, g.StreakRows);
            Assert.Equal(0.8, g.TrailingPace, 6);
            Assert.Equal(600.0 / 370, g.RecentPace, 6);
            Assert.False(g.Forced);

            acts = Feed(g, ref wall, ref t, 1, 3 * RowWallMs);
            Assert.Equal(new[] { PaceGuard.Act.Stop }, acts);
            Assert.Contains("1.62 s of wall a simulated second over the last 10 minutes' 37 rows", g.Pace());
            Assert.Contains("against 0.80 over the 60 minutes before them", g.Pace());
        }

        [Fact]
        public void AShortSpikeIsAveragedAndDoesNotTrip()
        {
            // Four minutes at three times the wall, then the steady pace: the window's worst is 600 s of wall
            // over 550 simulated seconds, under twice the reference.
            PaceGuard g = Guard();
            double wall = 0, t = 0;
            Feed(g, ref wall, ref t, 600, RowWallMs);
            for (int k = 0; k < 10; k++)
            {
                List<PaceGuard.Act> acts = Feed(g, ref wall, ref t, 10, 3 * RowWallMs);
                acts.AddRange(Feed(g, ref wall, ref t, 200, RowWallMs));
                Assert.All(acts, a => Assert.Equal(PaceGuard.Act.None, a));
            }
        }

        [Fact]
        public void ANoisySlowdownTripsThoughSomeRowsAreUnderTheLine()
        {
            // The gate's g5 (2026-10-17): a slowdown of about 2.4 times whose rows now and then fell just under
            // twice the reference. A rule of slow rows in a row never tripped on it; the window does.
            PaceGuard g = Guard();
            double wall = 0, t = 0;
            Feed(g, ref wall, ref t, 600, RowWallMs);
            var acts = new List<PaceGuard.Act>();
            for (int k = 0; k < 20; k++)
            {
                acts.AddRange(Feed(g, ref wall, ref t, 2, 2.6 * RowWallMs));
                acts.AddRange(Feed(g, ref wall, ref t, 1, 1.9 * RowWallMs));
            }

            Assert.Contains(PaceGuard.Act.Probe, acts);
            Assert.True(g.RecentPace > 2 * g.TrailingPace);
        }

        [Fact]
        public void UnderHalfTheTrailingPaceIsNotEnough()
        {
            // 1.9 times the wall is a pace above half the trailing one: never a slow row.
            PaceGuard g = Guard();
            double wall = 0, t = 0;
            Feed(g, ref wall, ref t, 600, RowWallMs);
            List<PaceGuard.Act> acts = Feed(g, ref wall, ref t, 200, 1.9 * RowWallMs);
            Assert.All(acts, a => Assert.Equal(PaceGuard.Act.None, a));
        }

        [Fact]
        public void NothingTripsInTheWarmUp()
        {
            // Five rows and twenty slow ones are 520 s of wall, inside the ten minutes' warm-up. A slow start is
            // the run's opening, not a slowdown.
            PaceGuard g = Guard();
            double wall = 0, t = 0;
            Feed(g, ref wall, ref t, 5, RowWallMs);
            List<PaceGuard.Act> acts = Feed(g, ref wall, ref t, 20, 3 * RowWallMs);
            Assert.All(acts, a => Assert.Equal(PaceGuard.Act.None, a));
        }

        [Fact]
        public void WithoutACardProbeTheGuardStopsAtTheTrip()
        {
            PaceGuard g = Guard();
            double wall = 0, t = 0;
            Feed(g, ref wall, ref t, 600, RowWallMs, canProbe: false);
            List<PaceGuard.Act> acts = Feed(g, ref wall, ref t, 19, 3 * RowWallMs, canProbe: false);
            Assert.Equal(PaceGuard.Act.Stop, acts[18]);
            Assert.All(acts.GetRange(0, 18), a => Assert.Equal(PaceGuard.Act.None, a));
        }

        [Fact]
        public void OffNeverTrips()
        {
            PaceGuard g = Guard(on: false);
            double wall = 0, t = 0;
            Feed(g, ref wall, ref t, 600, RowWallMs);
            List<PaceGuard.Act> acts = Feed(g, ref wall, ref t, 200, 10 * RowWallMs);
            Assert.All(acts, a => Assert.Equal(PaceGuard.Act.None, a));
            Assert.Contains("off", g.Describe());
        }

        [Fact]
        public void ATestsTripFiresAtItsSecondWhateverThePace()
        {
            PaceGuard g = Guard(tripAt: 45100);
            double wall = 0, t = 45000;
            List<PaceGuard.Act> acts = Feed(g, ref wall, ref t, 11, RowWallMs);
            Assert.All(acts.GetRange(0, 9), a => Assert.Equal(PaceGuard.Act.None, a));
            Assert.Equal(PaceGuard.Act.Probe, acts[9]);
            Assert.True(g.Forced);
            Assert.Equal(PaceGuard.Act.Stop, acts[10]);
            Assert.Contains("a test's trip", g.Pace());
        }

        [Fact]
        public void TheSettingsAreRefusedOutsideTheirRange()
        {
            Assert.Throws<ArgumentException>(() => new PaceGuard(true, 1.0, 2, 1, 10, 60, 10, -1));
            Assert.Throws<ArgumentException>(() => new PaceGuard(true, 0, 2, 1, 10, 60, 10, -1));
            Assert.Throws<ArgumentException>(() => new PaceGuard(true, 0.5, 0, 1, 10, 60, 10, -1));
            Assert.Throws<ArgumentException>(() => new PaceGuard(true, 0.5, 2, -1, 10, 60, 10, -1));
            Assert.Throws<ArgumentException>(() => new PaceGuard(true, 0.5, 2, 1, 0, 60, 10, -1));
            Assert.Throws<ArgumentException>(() => new PaceGuard(true, 0.5, 2, 1, 10, 0, 10, -1));
        }

        [Fact]
        public void TheSettingsBindWithTheirDefaultsAndAZeroTurnsTheGuardOff()
        {
            EnvSettings unset = EnvBinding.Read(EnvBinding.Of(new Dictionary<string, string>()));
            Assert.True(unset.PaceGuard);
            Assert.Equal(0.5f, unset.PaceRatio);
            Assert.Equal(2, unset.PaceRows);
            Assert.Equal(1, unset.PaceProbeRows);
            Assert.Equal(10f, unset.PaceWindowMinutes);
            Assert.Equal(60f, unset.PaceHourMinutes);
            Assert.Equal(10f, unset.PaceWarmupMinutes);
            Assert.True(unset.TestPaceTripAt < 0);
            Assert.True(unset.OverflowCheckpoint);

            EnvSettings off = EnvBinding.Read(EnvBinding.Of(new Dictionary<string, string>
            {
                ["EVOSIM_PACE_GUARD"] = "0",
                ["EVOSIM_OVERFLOW_CHECKPOINT"] = "0",
                ["EVOSIM_PACE_ROWS"] = "7",
            }));
            Assert.False(off.PaceGuard);
            Assert.False(off.OverflowCheckpoint);
            Assert.Equal(7, off.PaceRows);
            Assert.Empty(EnvBinding.UnknownNames(new[] { "EVOSIM_PACE_GUARD", "EVOSIM_TEST_PACE_TRIP_AT" }));
        }

        [Fact]
        public void TheCapWatchSaysTheFirstOverflowOnceAndCountsSinceTheCheckpoint()
        {
            var log = new StringWriter();
            var caps = new CapWatch(new long[4], 1);

            Assert.False(caps.Row(45000, new long[4], 1, log));
            Assert.Equal("since the run began, nothing past the card's caps and no divergence", caps.Since(double.NegativeInfinity));

            Assert.True(caps.Row(45410, new long[] { 3, 12, 0, 0 }, 1, log));
            Assert.False(caps.Row(45420, new long[] { 5, 12, 0, 0 }, 2, log));
            Assert.False(caps.Row(45430, new long[] { 5, 12, 0, 0 }, 2, log));

            string text = log.ToString();
            Assert.Single(text.Split('\n'), l => l.StartsWith("overflow:", StringComparison.Ordinal));
            Assert.Single(text.Split('\n'), l => l.StartsWith("diverged:", StringComparison.Ordinal));
            Assert.Contains("t=45410 s", text);

            Assert.Equal(
                "since the checkpoint at t=45000 s, 17 past the card's caps in the rows from t=45410 s to t=45420 s " +
                "and 1 divergences in the rows from t=45420 s to t=45420 s",
                caps.Since(45000));

            caps.Checkpointed();
            Assert.False(caps.Row(45800, new long[] { 9, 12, 0, 0 }, 2, log));
            Assert.Equal(
                "since the checkpoint at t=45430 s, 4 past the card's caps in the rows from t=45800 s to t=45800 s " +
                "and no divergence",
                caps.Since(45430));
        }

        [Fact]
        public void TheCapWatchOnTheCpuSeesNoCaps()
        {
            var log = new StringWriter();
            var caps = new CapWatch(null, 0);
            Assert.False(caps.Row(10, null, 0, log));
            Assert.Equal("", log.ToString());
        }

        [Fact]
        public void TheContactWindowKeepsWhatTheRowClosed()
        {
            var watch = new ContactWatch();
            Assert.Null(watch.LastWindow);

            watch.NoteCandidates(40);
            watch.NoteCandidates(512);
            watch.NoteOverlaps(64, 5, 80271, 7);
            watch.NoteOverlaps(64, 6, 12345, 3);
            watch.NoteOverlaps(10, 1, 54321, 2);
            watch.QueriesAtCap = 3;
            watch.OverlapsPastCap = 12;
            watch.NoteCells(81000, 7, 82419, 8);
            watch.NoteCells(81000, 9, 99999, 2);
            watch.NoteCells(80000, 1, 11111, 1);
            watch.Reset();

            ContactWatch closed = watch.LastWindow;
            Assert.Equal(512, closed.CandidatesMost);
            Assert.Equal(64, closed.OverlapsMost);
            Assert.Equal(80271, closed.OverlapsMostBody);
            Assert.Equal(7, closed.OverlapsMostLinks);
            Assert.Equal(3, closed.QueriesAtCap);
            Assert.Equal(12, closed.OverlapsPastCap);
            Assert.Equal(81000, closed.CellsMost);
            Assert.Equal(82419, closed.CellsMostBody);
            Assert.Equal(8, closed.CellsMostLinks);

            Assert.Equal(0, watch.CandidatesMost);
            Assert.Equal(0, watch.CellsMost);
            Assert.Equal(-1, watch.CellsMostBody);
            Assert.Equal(-1, watch.OverlapsMostBody);
        }
    }
}
