using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Evosim.Core;
using Evosim.Dynamics;

namespace Evosim.Farm
{
    /// <summary>
    /// The farm's pace guard (proposals/propose-farm-pace-guard.md, items 2 and 3): a world whose pace stays
    /// well under its own trailing pace is stopped at a checkpoint, with a reason that says what grew.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Round 61's r61-p6103 slowed tenfold from 45,300 s on 2026-10-17, because one ribbon's contact sphere
    /// covered 81,000 grid cells. It was found by an agent reading a stats file an hour later. The owner
    /// ruled the same day that a world running too slow exits with a detailed reason.
    /// </para>
    /// <para>
    /// <b>The pace</b> is wall seconds a simulated second, read at every stats row from the run's clock less
    /// the time spent waiting at D159's exchanges, so a world held by a slow peer does not read as slow
    /// itself. Two windows of wall time are read at every row: the recent pace over the rows of the last
    /// <see cref="WindowMinutes"/>, and the reference pace over the <see cref="HourMinutes"/> of wall before
    /// them. After a warm-up, the guard trips when the recent pace is more than the reference divided by
    /// <see cref="Ratio"/> and each window holds <see cref="Rows"/> rows or more. On the card it then turns the
    /// probe on for <see cref="ProbeRows"/> rows, so the reason can quote the card's split, and stops at the
    /// next row. The farm writes a checkpoint at that row and ends the run "slow".
    /// </para>
    /// <para>
    /// The windows are wall time, not rows, because a row is ten simulated seconds however long it takes: a
    /// rule of twenty slow rows in a row waited 38 wall minutes on the gate's g5 (2026-10-17), longest where the
    /// world was slowest. And a streak of slow rows is broken by one row just under the line, which then joins
    /// the reference: on g5 a 2.4-fold slowdown ran 17 wall minutes so and ratcheted the reference up without
    /// ever tripping. A window is a sum, so no row resets it, and a slow row reaches the reference only after
    /// leaving the recent window. A short spike is averaged with the rows around it and does not trip.
    /// </para>
    /// <para>
    /// <b>It moves no bit.</b> It reads the wall clock and the world, and the probe it switches on joins
    /// and times the card without changing what any kernel computes. When it fires depends on the wall
    /// clock, so two runs of one world can stop at different seconds; each stops at a checkpoint taken
    /// after a row, and a resume from it writes the rows the unbroken run wrote.
    /// </para>
    /// </remarks>
    internal sealed class PaceGuard
    {
        public enum Act
        {
            None,
            Probe,
            Stop,
        }

        public bool On { get; }

        /// <summary>The guard trips when the recent pace is under this share of the reference (0.5: twice the wall).</summary>
        public double Ratio { get; }

        /// <summary>The fewest rows each window holds before the guard reads it.</summary>
        public int Rows { get; }

        /// <summary>The rows the card probe runs before the stop; 0 stops at once.</summary>
        public int ProbeRows { get; }

        /// <summary>The recent window, in minutes of wall.</summary>
        public double WindowMinutes { get; }

        /// <summary>The reference window before the recent one, in minutes of wall.</summary>
        public double HourMinutes { get; }

        public double WarmupMinutes { get; }

        /// <summary>A test's trip: the first row at or past this second trips the guard, whatever the pace. Below 0, none.</summary>
        public double TripAt { get; }

        // Each window's rows, oldest first, as { wall at the row's end, its wall, its simulated seconds, the
        // second it began }, and their sums.
        private readonly Queue<double[]> _recent = new Queue<double[]>();
        private readonly Queue<double[]> _hour = new Queue<double[]>();
        private double _recentWallMs, _recentSeconds, _hourWallMs, _hourSeconds;
        private double _firstWallMs = double.NaN, _lastWallMs = double.NaN, _lastT = double.NaN;
        private int _probing = -1;

        /// <summary>The reference pace at the trip, wall seconds a simulated second.</summary>
        public double TrailingPace { get; private set; }

        /// <summary>The simulated second the recent window began at, at the trip.</summary>
        public double SlowFrom { get; private set; } = double.NaN;

        /// <summary>The recent window's pace at the trip, wall seconds a simulated second.</summary>
        public double RecentPace { get; private set; }

        /// <summary>The rows in the recent window at the trip.</summary>
        public int StreakRows { get; private set; }

        /// <summary>Whether the trip was the test's (<see cref="TripAt"/>) rather than the pace's.</summary>
        public bool Forced { get; private set; }

        public PaceGuard(bool on, double ratio, int rows, int probeRows, double windowMinutes, double hourMinutes,
            double warmupMinutes, double tripAt)
        {
            if (!(ratio > 0 && ratio < 1))
                throw new ArgumentException("EVOSIM_PACE_RATIO must be above 0 and under 1, and is " + ratio);
            if (rows < 1) throw new ArgumentException("EVOSIM_PACE_ROWS must be 1 or more, and is " + rows);
            if (probeRows < 0) throw new ArgumentException("EVOSIM_PACE_PROBE_ROWS must be 0 or more, and is " + probeRows);
            if (!(windowMinutes > 0))
                throw new ArgumentException("EVOSIM_PACE_WINDOW_MINUTES must be above 0, and is " + windowMinutes);
            if (!(hourMinutes > 0))
                throw new ArgumentException("EVOSIM_PACE_HOUR_MINUTES must be above 0, and is " + hourMinutes);
            if (!(warmupMinutes >= 0))
                throw new ArgumentException("EVOSIM_PACE_WARMUP_MINUTES must be 0 or more, and is " + warmupMinutes);

            On = on;
            Ratio = ratio;
            Rows = rows;
            ProbeRows = probeRows;
            WindowMinutes = windowMinutes;
            HourMinutes = hourMinutes;
            WarmupMinutes = warmupMinutes;
            TripAt = tripAt;
        }

        public static PaceGuard Of(EnvSettings s) => new PaceGuard(
            s.PaceGuard, s.PaceRatio, s.PaceRows, s.PaceProbeRows, s.PaceWindowMinutes, s.PaceHourMinutes,
            s.PaceWarmupMinutes, s.TestPaceTripAt);

        /// <summary>The guard's settings in a line, for the run's opening.</summary>
        public string Describe()
        {
            if (!On) return "pace guard: off (EVOSIM_PACE_GUARD=0)";

            return string.Format(CultureInfo.InvariantCulture,
                "pace guard: on; stops a world at a checkpoint when its pace over the last {0:0.#} minutes of wall " +
                "is under {1:0.##} of its pace over the {2:0.#} minutes before them, each window holding {3} rows " +
                "or more, after {4:0.#} minutes' warm-up, with the card probe on for {5} rows first{6}",
                WindowMinutes, Ratio, HourMinutes, Rows, WarmupMinutes, ProbeRows,
                TripAt >= 0 ? string.Format(CultureInfo.InvariantCulture, "; a test's trip at t={0:0.#} s", TripAt) : "");
        }

        /// <summary>
        /// A stats row: the wall that counts (the run's clock less the exchanges' waits), in ms, and the world's
        /// second. <paramref name="canProbe"/> says whether a card probe can be switched on.
        /// </summary>
        public Act Row(double wallMs, double t, bool canProbe)
        {
            if (!On) return Act.None;

            if (_probing >= 0)
            {
                _probing++;
                return _probing >= ProbeRows ? Act.Stop : Act.None;
            }

            if (double.IsNaN(_lastWallMs))
            {
                // The first row is the baseline; its own wall holds the run's opening.
                _firstWallMs = _lastWallMs = wallMs;
                _lastT = t;
                return TripAt >= 0 && t >= TripAt ? Trip(t, canProbe, forced: true) : Act.None;
            }

            double dWall = wallMs - _lastWallMs;
            double dT = t - _lastT;
            _lastWallMs = wallMs;
            _lastT = t;
            if (dT <= 0) return Act.None;

            // The row joins the recent window; rows past it move to the reference, and rows past that leave.
            _recent.Enqueue(new[] { wallMs, dWall, dT, t - dT });
            _recentWallMs += dWall;
            _recentSeconds += dT;
            double recentFrom = wallMs - WindowMinutes * 60000.0;
            while (_recent.Count > 0 && _recent.Peek()[0] <= recentFrom)
            {
                double[] old = _recent.Dequeue();
                _recentWallMs -= old[1];
                _recentSeconds -= old[2];
                _hour.Enqueue(old);
                _hourWallMs += old[1];
                _hourSeconds += old[2];
            }

            while (_hour.Count > 0 && _hour.Peek()[0] <= recentFrom - HourMinutes * 60000.0)
            {
                double[] old = _hour.Dequeue();
                _hourWallMs -= old[1];
                _hourSeconds -= old[2];
            }

            bool forced = TripAt >= 0 && t >= TripAt;
            double recent = _recentSeconds > 0 ? _recentWallMs / 1000.0 / _recentSeconds : 0;
            double reference = _hourSeconds > 0 ? _hourWallMs / 1000.0 / _hourSeconds : 0;
            bool warm = wallMs - _firstWallMs >= WarmupMinutes * 60000.0;
            bool slow = warm && _recent.Count >= Rows && _hour.Count >= Rows && reference > 0 &&
                        recent * Ratio > reference;

            if (slow || forced)
            {
                RecentPace = recent;
                TrailingPace = reference;
                SlowFrom = _recent.Peek()[3];
                StreakRows = _recent.Count;
                return Trip(t, canProbe, forced);
            }

            return Act.None;
        }

        private Act Trip(double t, bool canProbe, bool forced)
        {
            Forced = forced;
            StreakRows = Math.Max(1, StreakRows);
            if (double.IsNaN(SlowFrom)) SlowFrom = t;

            if (canProbe && ProbeRows > 0)
            {
                _probing = 0;
                return Act.Probe;
            }

            _probing = int.MaxValue / 2;
            return Act.Stop;
        }

        /// <summary>The pace in a clause, for the probe's line and the reason.</summary>
        public string Pace()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0}{1:0.00} s of wall a simulated second over the last {2:0.#} minutes' {3} rows from t={4:0.#} s, " +
                "against {5:0.00} over the {6:0.#} minutes before them ({7:0.0} times slower)",
                Forced ? "a test's trip (EVOSIM_TEST_PACE_TRIP_AT); " : "",
                RecentPace, WindowMinutes, StreakRows, SlowFrom, TrailingPace, HourMinutes,
                TrailingPace > 0 ? RecentPace / TrailingPace : 0);
        }

        /// <summary>
        /// The reason the run ends with (item 3): the pace, the last row's contact work and the largest part,
        /// what passed a cap or diverged since the last checkpoint, and the card probe's split.
        /// </summary>
        public string Reason(World world, ContactWatch window, string sinceCheckpoint, IReadOnlyList<string> probe)
        {
            var text = new StringBuilder();
            text.Append(Pace());

            if (window != null)
            {
                text.Append(string.Format(CultureInfo.InvariantCulture,
                    "; the last row's contact work: most cells one row covered {0} (body {1}, {2} links), most " +
                    "candidates a query kept {3} with {4} queries at the card's cap, most overlaps a body listed " +
                    "{5} (body {6}, {7} links) with {8} past the card's cap",
                    window.CellsMost, window.CellsMostBody, window.CellsMostLinks, window.CandidatesMost,
                    window.QueriesAtCap, window.OverlapsMost, window.OverlapsMostBody, window.OverlapsMostLinks,
                    window.OverlapsPastCap));
            }

            (double metres, long body, long parts) = LargestPart(world);
            text.Append(string.Format(CultureInfo.InvariantCulture,
                "; the largest part a half-diagonal of {0:0.00} m, on body {1} of {2} parts", metres, body, parts));

            if (!string.IsNullOrEmpty(sinceCheckpoint)) text.Append("; ").Append(sinceCheckpoint);

            // The probe's time split and its grid line, which name the class and the grid part that grew; the
            // rest of its print is in the error log above.
            int quoted = 0;
            if (probe != null)
            {
                foreach (string line in probe)
                {
                    if (!line.StartsWith("gpu-probe t=", StringComparison.Ordinal) &&
                        !line.StartsWith("gpu-probe concurrent", StringComparison.Ordinal) &&
                        !line.StartsWith("gpu-probe grid parts", StringComparison.Ordinal) &&
                        !line.StartsWith("gpu-probe phases", StringComparison.Ordinal)) continue;
                    text.Append(quoted == 0 ? "; the card probe's last print: " : " | ").Append(line);
                    quoted++;
                }
            }

            if (quoted == 0) text.Append("; no card probe print");
            return text.ToString();
        }

        /// <summary>
        /// The largest part among the living by its half-diagonal, the radius its contact sphere takes, with its
        /// body's id and part count; a tie goes to the earliest body in the list. -1 and 0 for an empty world.
        /// </summary>
        public static (double Metres, long Body, long Parts) LargestPart(World world)
        {
            double largest = 0;
            long body = -1;
            long parts = 0;
            for (int i = 0; i < world.Living.Count; i++)
            {
                Organism creature = world.Living[i];
                foreach (PhenotypePart part in creature.Phenotype.Parts)
                {
                    Float3 h = part.HalfExtents;
                    double reach = Math.Sqrt((double)h.X * h.X + (double)h.Y * h.Y + (double)h.Z * h.Z);
                    if (reach <= largest) continue;
                    largest = reach;
                    body = creature.Id;
                    parts = creature.Phenotype.PartCount;
                }
            }

            return (largest, body, parts);
        }
    }

    /// <summary>
    /// The card's caps and the world's divergences as the run goes (proposals/propose-farm-pace-guard.md, item
    /// 4): the first time either cap is passed, or a body diverges, in this run is said in a line, and a first
    /// overflow asks for a checkpoint at that row. Neither stops the run. What passed or diverged since the last
    /// checkpoint goes into the pace guard's reason.
    /// </summary>
    /// <remarks>
    /// Read at every stats row from counts the run already keeps, so a checkpoint it asks for is taken after a
    /// row, as every checkpoint is, and nothing the world reads moves.
    /// </remarks>
    internal sealed class CapWatch
    {
        private long _overflowSeen;
        private long _divergedSeen;
        private bool _overflowSaid, _divergedSaid;
        private double _overflowFrom = double.NaN, _overflowTo;
        private long _overflowSince;
        private double _divergedFrom = double.NaN, _divergedTo;
        private long _divergedSince;

        /// <summary>The counts the run starts from: the card's are this run's own, the divergences inherited.</summary>
        public CapWatch(long[] overflow, long diverged)
        {
            _overflowSeen = Sum(overflow);
            _divergedSeen = diverged;
        }

        /// <summary>
        /// A row. True when this is the run's first overflow, for which the farm takes a checkpoint at this row.
        /// </summary>
        public bool Row(double t, long[] overflow, long diverged, TextWriter log)
        {
            bool first = false;
            long total = Sum(overflow);

            if (total > _overflowSeen)
            {
                long grew = total - _overflowSeen;
                _overflowSeen = total;
                if (double.IsNaN(_overflowFrom)) _overflowFrom = t;
                _overflowTo = t;
                _overflowSince += grew;

                if (!_overflowSaid)
                {
                    _overflowSaid = true;
                    first = true;
                    log.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "overflow: the card passed a cap for the first time in this run by the row at t={0:0.#} s: " +
                        "candidates past the cap {1}, overlaps past it {2}, held ids past it {3}, cell entries past " +
                        "it {4}; a checkpoint is taken at this row and the run goes on",
                        t, overflow[0], overflow[1], overflow[2], overflow[3]));
                }
            }

            if (diverged > _divergedSeen)
            {
                long grew = diverged - _divergedSeen;
                _divergedSeen = diverged;
                if (double.IsNaN(_divergedFrom)) _divergedFrom = t;
                _divergedTo = t;
                _divergedSince += grew;

                if (!_divergedSaid)
                {
                    _divergedSaid = true;
                    log.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "diverged: a body diverged for the first time in this run by the row at t={0:0.#} s, {1} in " +
                        "all; its dump is written as before and the run goes on", t, diverged));
                }
            }

            return first;
        }

        /// <summary>A checkpoint was taken: what follows is since it.</summary>
        public void Checkpointed()
        {
            _overflowFrom = double.NaN;
            _overflowSince = 0;
            _divergedFrom = double.NaN;
            _divergedSince = 0;
        }

        /// <summary>What passed a cap or diverged since the last checkpoint, with its times, for the reason.</summary>
        public string Since(double checkpointAt)
        {
            string from = double.IsNegativeInfinity(checkpointAt)
                ? "since the run began"
                : string.Format(CultureInfo.InvariantCulture, "since the checkpoint at t={0:0.#} s", checkpointAt);

            string overflow = double.IsNaN(_overflowFrom)
                ? "nothing past the card's caps"
                : string.Format(CultureInfo.InvariantCulture,
                    "{0} past the card's caps in the rows from t={1:0.#} s to t={2:0.#} s",
                    _overflowSince, _overflowFrom, _overflowTo);

            string diverged = double.IsNaN(_divergedFrom)
                ? "no divergence"
                : string.Format(CultureInfo.InvariantCulture,
                    "{0} divergences in the rows from t={1:0.#} s to t={2:0.#} s",
                    _divergedSince, _divergedFrom, _divergedTo);

            return from + ", " + overflow + " and " + diverged;
        }

        private static long Sum(long[] counts)
        {
            if (counts == null) return 0;
            long total = 0;
            foreach (long n in counts) total += n;
            return total;
        }
    }
}
