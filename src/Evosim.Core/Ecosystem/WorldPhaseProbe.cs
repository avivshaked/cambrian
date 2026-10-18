using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Evosim.Core
{
    /// <summary>
    /// A timing build's split of <see cref="World.Step"/>: the wall clock between each of its
    /// passes, summed, and printed to standard error every <see cref="PrintEvery"/> steps as
    /// milliseconds a step. A diagnostic, never a scored run's.
    /// </summary>
    /// <remarks>
    /// Every method is <see cref="ConditionalAttribute"/> on <c>WORLD_PHASE_PROBE</c>, so a build
    /// without the symbol (every scored run's, and the Editor's) compiles none of the calls and
    /// steps exactly as it did. Build with <c>-p:WorldPhaseProbe=1</c> to an output of its own.
    /// The marks read a clock and add; they draw nothing and write nothing the world reads.
    /// </remarks>
    public static class WorldPhaseProbe
    {
        public const int Patches = 0, Light = 1, Reserve = 2, Metabolise = 3, Mouth = 4, Gestate = 5,
            Grow = 6, Disperse = 7, AdvectBodies = 8, Influx = 9, Corpses = 10, Settle = 11,
            Remineralise = 12, MixSnow = 13, MixMatter = 14, AdvectSnow = 15, AdvectMatter = 16,
            Bury = 17, Cull = 18, Reproduce = 19, Floor = 20, Trickle = 21, Ceiling = 22,
            LightContribute = 23, LightSolve = 24, Ledgers = 25,
            CorpseDrift = 26, CorpseDeposits = 27, ScentFill = 28,
            MouthForget = 29, MouthDamage = 30, MouthIntake = 31, MouthHeal = 32,
            MetaboliseFreeze = 33, MetaboliseShares = 34, MetaboliseBooks = 35, MetaboliseAge = 36,
            MetaboliseBurials = 37, MetabolisePrePass = 38;

        /// <summary>The counts a step, for pricing the body loops in parallel (card-next-r57.md Part 8).</summary>
        public const int Juveniles = 0, Grown = 1, Rebilled = 2, Mouths = 3, CorpsesAtIntake = 4;

        private static readonly string[] CountNames =
        {
            "juveniles", "grown", "re-billed", "mouths", "corpses at intake",
        };

        private static readonly long[] Counts = new long[CountNames.Length];

        private static readonly string[] Names =
        {
            "patches", "light", "reserve", "metabolise", "mouth", "gestate", "grow", "disperse",
            "advect bodies", "influx", "corpses", "settle", "remineralise", "mix snow", "mix matter",
            "advect snow", "advect matter", "bury", "cull", "reproduce", "floor", "trickle", "ceiling",
            "(metabolise: light contribute)", "(light solve)", "(ledgers)",
            // The corpses phase in three (C0, card-next-r57.md); "corpses" keeps what is left of it.
            "(corpses: drift)", "(corpse deposits)", "(scent fill)",
            // ApplyMouth in four; "mouth" keeps what is left of it, the dead collected.
            "(mouth: forget)", "(mouth: damage)", "(mouth: intake)", "(mouth: heal)",
            // Metabolise's freezes and its reverse pass in four, marked inside the walk, so each
            // part carries its share of the marks' own cost; "metabolise" keeps the walk's rest.
            "(metabolise: freeze)", "(metabolise: shares and re-bills)", "(metabolise: takes and books)",
            "(metabolise: trim and age)", "(metabolise: burials)", "(metabolise: pre-pass)",
        };

        /// <summary>Steps summed before a line is printed: 40 half-second steps, 20 s.</summary>
        public const int PrintEvery = 40;

        private static readonly long[] Ticks = new long[Names.Length];
        private static long _last;
        private static int _steps;
        private static long _living;

        [Conditional("WORLD_PHASE_PROBE")]
        public static void Begin() => _last = Stopwatch.GetTimestamp();

        [Conditional("WORLD_PHASE_PROBE")]
        public static void Mark(int phase)
        {
            long now = Stopwatch.GetTimestamp();
            Ticks[phase] += now - _last;
            _last = now;
        }

        /// <summary>Adds <paramref name="n"/> to a count a step.</summary>
        [Conditional("WORLD_PHASE_PROBE")]
        public static void Count(int counter, long n) => System.Threading.Interlocked.Add(ref Counts[counter], n);

        [Conditional("WORLD_PHASE_PROBE")]
        public static void End(int living)
        {
            _steps++;
            _living += living;
            if (_steps < PrintEvery) return;

            var inv = CultureInfo.InvariantCulture;
            double toMs = 1000.0 / Stopwatch.Frequency / _steps;
            double total = 0;
            var line = new StringBuilder();
            for (int p = 0; p < Names.Length; p++)
            {
                double ms = Ticks[p] * toMs;
                total += ms;
                if (ms < 0.05) continue;
                line.Append(string.Format(inv, ", {0} {1:0.0}", Names[p], ms));
            }

            Console.Error.WriteLine(string.Format(inv,
                "world-probe, ms a step over {0} steps at {1:0} bodies: {2:0.0}{3}",
                _steps, (double)_living / _steps, total, line));

            // The counts on a line of their own, so a reader of the times never sums them as times.
            var counts = new StringBuilder();
            for (int k = 0; k < CountNames.Length; k++)
            {
                counts.Append(string.Format(inv, "{0}{1} {2:0.0}", k == 0 ? "" : ", ", CountNames[k],
                    (double)Counts[k] / _steps));
            }

            Console.Error.WriteLine(string.Format(inv,
                "world-probe counts, a step over {0} steps: {1}", _steps, counts));

            Array.Clear(Ticks, 0, Ticks.Length);
            Array.Clear(Counts, 0, Counts.Length);
            _steps = 0;
            _living = 0;
        }
    }
}
