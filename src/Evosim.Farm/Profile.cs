using System;
using System.Diagnostics;

namespace Evosim.Farm
{
    /// <summary>
    /// The timing split and the harness profile — work package K, the port of
    /// <c>Ecosystem</c>'s <c>_phaseTicks</c>, <c>NotePhase</c> and <c>NoteHarnessWall</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Three buckets and a remainder, as the footer has always printed them.</b>
    /// <c>physics</c> is the whole of <see cref="Dynamics.DynamicsWorld.Step"/> — the control
    /// loop, the drag pass, the contacts and the solve, which this engine does in one call where
    /// the farm did them in three; <c>world</c> is <see cref="Core.World.Step"/>; <c>harness</c>
    /// is everything else in the per-step loop, which is the reconcile and the metabolic glue
    /// around the other two; <c>writers</c> is what a sample costs, on the loop's own stopwatch;
    /// and <c>other</c> is the subtraction against the run's clock.
    /// </para>
    /// <para>
    /// <b>The phase list is the farm's, less two and no more.</b> <c>control</c>, <c>fluid</c>,
    /// <c>contacts</c> and <c>trace</c> are inside the solver's own call and are therefore inside
    /// <c>physics</c>, so naming them here would be four zeros; <c>read</c> does not exist,
    /// because a link's pose is a number in the creature's own array and there is nothing to read
    /// it out of. What is left — <c>reconcile</c>, <c>finite</c>, <c>settle</c>,
    /// <c>metabolise</c>, <c>growth</c>, <c>other</c> — keeps the farm's spelling and the farm's
    /// meaning, so <c>RunEnding.FieldFor</c> writes them under the same statistics field names.
    /// </para>
    /// <para>
    /// <b>Each phase is one timestamp pair per step and nothing per creature</b>, and nothing
    /// branches on a clock — so a run under the profile is the same realisation as a run without
    /// it.
    /// </para>
    /// </remarks>
    public sealed partial class Simulation
    {
        /// <summary>The phases, <c>other</c> last, in the order the footer prints them.</summary>
        public static readonly string[] HarnessPhases =
        {
            "reconcile", "finite", "settle", "metabolise", "growth", "other",
        };

        /// <summary>The statistics field each phase is written under — <c>wallHarnessSettleMs</c>.</summary>
        /// <remarks>
        /// Built from <see cref="HarnessPhases"/> through <see cref="RunEnding.FieldFor"/>, which
        /// is <c>Ecosystem.BuildHarnessPhaseFields</c>'s own rule: one list, so the two cannot
        /// disagree.
        /// </remarks>
        public static readonly string[] HarnessPhaseFields = BuildHarnessPhaseFields();

        private static string[] BuildHarnessPhaseFields()
        {
            var fields = new string[HarnessPhases.Length];
            for (int i = 0; i < fields.Length; i++) fields[i] = RunEnding.FieldFor(HarnessPhases[i]);
            return fields;
        }

        internal const int PhaseReconcile = 0;
        internal const int PhaseFinite = 1;
        internal const int PhaseSettle = 2;
        internal const int PhaseMetabolise = 3;
        internal const int PhaseGrowth = 4;

        /// <summary>Stopwatch ticks in each named phase; `other` is the subtraction.</summary>
        private readonly long[] _phaseTicks = new long[HarnessPhases.Length - 1];

        private long _physicsTicks;
        private long _worldTicks;
        private long _harnessTicks;
        private long _bodyStepSum;
        private long _linkStepSum;

        /// <summary>The run's own clock, which every share is taken against.</summary>
        internal Stopwatch RunClock;

        /// <summary>What the writers cost — started and stopped around a sample.</summary>
        internal readonly Stopwatch WritersClock = new Stopwatch();

        public long WallPhysicsMs => Milliseconds(_physicsTicks);
        public long WallWorldMs => Milliseconds(_worldTicks);
        public long WallHarnessMs => Milliseconds(_harnessTicks);
        public long WallWritersMs => WritersClock.ElapsedMilliseconds;
        public long WallTotalMs => RunClock?.ElapsedMilliseconds ?? 0L;

        /// <summary>Living bodies summed over every physics step — the profile's denominator.</summary>
        public long HarnessBodySteps => _bodyStepSum;

        /// <summary>Links stepped, summed over every physics step — the fluid's denominator.</summary>
        public long FluidLinkSteps => _linkStepSum;

        public double HarnessMicrosecondsPerBodyStep =>
            _bodyStepSum > 0L ? _harnessTicks * (1e6 / Stopwatch.Frequency) / _bodyStepSum : 0d;

        /// <summary>
        /// The solver's microseconds per link per physics step.
        /// </summary>
        /// <remarks>
        /// The farm's <c>fluid per link-step</c> line measured its drag pass alone. This engine
        /// has no separate drag pass to time — the whole of a body's step, drag included, runs on
        /// the body's own thread — so the line is the solver's total over the same denominator,
        /// and the four-way <c>fluid split</c> beside it reads zero. Said in the report rather
        /// than left to be read as a drag pass that costs nothing.
        /// </remarks>
        public double FluidMicrosecondsPerLinkStep =>
            _linkStepSum > 0L ? _physicsTicks * (1e6 / Stopwatch.Frequency) / _linkStepSum : 0d;

        /// <summary>Wall milliseconds in each of <see cref="HarnessPhases"/>, `other` last.</summary>
        public long[] HarnessPhaseMs()
        {
            var ms = new long[HarnessPhases.Length];
            long named = 0L;

            for (int i = 0; i < _phaseTicks.Length; i++)
            {
                ms[i] = Milliseconds(_phaseTicks[i]);
                named += ms[i];
            }

            ms[ms.Length - 1] = Math.Max(0L, WallHarnessMs - named);
            return ms;
        }

        internal static long Now() => Stopwatch.GetTimestamp();

        private static long Milliseconds(long ticks) => ticks * 1000L / Stopwatch.Frequency;

        /// <summary>
        /// Books <paramref name="phase"/> and starts the next one from the same reading of the
        /// clock, so two adjacent phases cost one timestamp between them rather than two.
        /// </summary>
        private long NotePhase(int phase, long startedAt)
        {
            long now = Now();
            _phaseTicks[phase] += now - startedAt;
            return now;
        }

        /// <summary>
        /// Books the step's harness time: the whole step less what the solver and the world took
        /// out of it.
        /// </summary>
        private void NoteHarnessWall(long stepStarted, long bucketedAtEntry)
        {
            long spent = Now() - stepStarted;
            long bucketed = _physicsTicks + _worldTicks - bucketedAtEntry;
            long harness = spent - bucketed;

            if (harness > 0L) _harnessTicks += harness;
        }
    }
}
