using System;

namespace Evosim.Dynamics
{
    /// <summary>
    /// The last three physics steps of every link's motion, kept on the body — the farm's throw
    /// trace, ported.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The ring keeps the last three <i>finite</i> frames.</b> A frame carrying a number that
    /// is not one is refused and the ring is left exactly as it was, which is the repair
    /// logbook/0097 asked for: 42 of round 37b's 43 traces held three frames of NaN and could
    /// not say when the body left. <see cref="FirstNonFiniteStep"/> keeps the step the refusals
    /// started at and <see cref="FirstNonFiniteLink"/> the link that started them.
    /// </para>
    /// <para>
    /// <b>Written inside the parallel region, by the body's own thread and by nobody else.</b>
    /// Everything the frame holds is this creature's own state and the ring is this creature's
    /// own array, so the trace is as thread-count-blind as the step around it. The farm's ring
    /// is single-threaded for the same reason stated the other way round — there it is the main
    /// thread, here it is the owner.
    /// </para>
    /// <para>
    /// <b>Jointed bodies only</b>, as the farm's is from 2026-09-20: an unjointed body has no
    /// joint to be thrown by and the majority of every population is unjointed, so the ring is
    /// not allocated and the post-mortem's <c>traceOmitted</c> says why.
    /// </para>
    /// </remarks>
    public sealed partial class Creature
    {
        /// <summary>Frames the ring holds at most — the farm's three.</summary>
        public const int TraceFrames = 3;

        /// <summary>
        /// Numbers kept per link per frame — the farm's twenty-one, of which fifteen are the
        /// same quantity and six are this solver's nearest thing to the farm's.
        /// </summary>
        /// <remarks>
        /// Position, velocity, angular velocity and the three reduced-space joint velocities are
        /// the farm's own. The farm's next nine are the drag force, D090's acceleration force and
        /// the water's acceleration, each of which <c>FluidEnvironment</c> keeps per part; this
        /// solver sums every external load straight into <c>Fext</c> and never separates the drag
        /// from the rest, so what is kept in their place is the total external force and torque
        /// on the link and the water's <i>velocity</i> there. Six numbers of the twenty-one
        /// therefore answer a different question, and the dump says which.
        /// </remarks>
        public const int TraceValuesPerLink = 21;

        private double[] _trace;
        private long[] _traceStep;
        private double[] _traceTime;
        private bool[] _traceResized;
        private int _traceHeld;
        private int _traceCursor;
        private double[] _traceScratch;

        /// <summary>Whether this body keeps a ring at all.</summary>
        public bool HasTrace => _trace != null;

        /// <summary>Frames held, 0 to <see cref="TraceFrames"/>.</summary>
        public int TraceHeld => _traceHeld;

        /// <summary>The slot the next frame would be written into.</summary>
        public int TraceCursor => _traceCursor;

        /// <summary>The step this body's numbers first stopped being finite, or −1.</summary>
        public long FirstNonFiniteStep { get; private set; } = -1;

        /// <summary>The link that first stopped being finite, or −1.</summary>
        public int FirstNonFiniteLink { get; private set; } = -1;

        /// <summary>The step this body was last resized at, or −1 — the farm's <c>ResizedAtStep</c>.</summary>
        public long ResizedAtStep { get; private set; } = -1;

        /// <summary>Set by a resize and cleared by the frame that records it.</summary>
        private bool _resizedSinceLastFrame;

        /// <summary>
        /// Turns the ring on for this body. Idempotent, and a no-op on a body with no movable
        /// joint.
        /// </summary>
        public void EnableTrace()
        {
            if (_trace != null || Dof == 0) return;

            _trace = new double[TraceFrames * Links * TraceValuesPerLink];
            _traceStep = new long[TraceFrames];
            _traceTime = new double[TraceFrames];
            _traceResized = new bool[TraceFrames];
            _traceScratch = new double[Links * TraceValuesPerLink];
        }

        /// <summary>Records that this body was resized, for the next frame's flag.</summary>
        public void MarkResized(long atStep)
        {
            ResizedAtStep = atStep;
            _resizedSinceLastFrame = true;
        }

        /// <summary>The step a held frame stands at.</summary>
        public long TraceStepAt(int slot) => _traceStep[slot];

        /// <summary>The simulated second a held frame stands at.</summary>
        public double TraceTimeAt(int slot) => _traceTime[slot];

        /// <summary>Whether a held frame is the first after a resize.</summary>
        public bool TraceResizedAt(int slot) => _traceResized[slot];

        /// <summary>One value out of a held frame.</summary>
        public double TraceValue(int slot, int link, int which) =>
            _trace[(slot * Links + link) * TraceValuesPerLink + which];

        /// <summary>
        /// Takes one frame of this body, or refuses it and records the onset.
        /// </summary>
        /// <param name="step">The step just taken.</param>
        /// <param name="stepTime">The simulated second it ended at.</param>
        internal void RecordTraceFrame(long step, double stepTime)
        {
            double[] trace = _trace;
            if (trace == null) return;

            double[] frame = _traceScratch;
            int bad = -1;

            for (int b = 0; b < Links; b++)
            {
                int o = b * TraceValuesPerLink;

                frame[o + 0] = Position[3 * b];
                frame[o + 1] = Position[3 * b + 1];
                frame[o + 2] = Position[3 * b + 2];
                frame[o + 3] = Velocity[3 * b];
                frame[o + 4] = Velocity[3 * b + 1];
                frame[o + 5] = Velocity[3 * b + 2];
                frame[o + 6] = b == _poisonedLink ? double.NaN : Spin[3 * b];
                frame[o + 7] = Spin[3 * b + 1];
                frame[o + 8] = Spin[3 * b + 2];

                int n = DofCount[b];
                int at = DofStart[b];

                frame[o + 9] = n > 0 ? Qd[at] : 0;
                frame[o + 10] = n > 1 ? Qd[at + 1] : 0;
                frame[o + 11] = n > 2 ? Qd[at + 2] : 0;

                frame[o + 12] = Fext[6 * b + 3];
                frame[o + 13] = Fext[6 * b + 4];
                frame[o + 14] = Fext[6 * b + 5];
                frame[o + 15] = Fext[6 * b];
                frame[o + 16] = Fext[6 * b + 1];
                frame[o + 17] = Fext[6 * b + 2];

                frame[o + 18] = Water[3 * b];
                frame[o + 19] = Water[3 * b + 1];
                frame[o + 20] = Water[3 * b + 2];

                // One at a time rather than through a sum of them: two large finite numbers of
                // the same sign add to an infinity, and a summed test would read that as a
                // divergence the solver never had. The farm's own note, and its reason.
                for (int k = 0; k < TraceValuesPerLink; k++)
                {
                    double v = frame[o + k];
                    if (double.IsNaN(v) || double.IsInfinity(v)) { bad = b; break; }
                }

                if (bad >= 0) break;
            }

            if (bad >= 0)
            {
                if (FirstNonFiniteStep < 0)
                {
                    FirstNonFiniteStep = step;
                    FirstNonFiniteLink = bad;
                }

                return;
            }

            int slot = _traceCursor;
            int floats = Links * TraceValuesPerLink;
            Array.Copy(frame, 0, trace, slot * floats, floats);

            _traceStep[slot] = step;
            _traceTime[slot] = stepTime;

            // Cleared as it is recorded, so exactly one frame per resize carries the flag.
            _traceResized[slot] = _resizedSinceLastFrame;
            _resizedSinceLastFrame = false;

            _traceCursor = slot + 1 == TraceFrames ? 0 : slot + 1;
            if (_traceHeld < TraceFrames) _traceHeld++;
        }

        /// <summary>
        /// Test-only: from the next frame on, one link's angular velocity goes into the frame as
        /// a value that is not a number, so the ring's refusal can be exercised on a body the
        /// solver was never going to lose. −1 turns it off.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Into the frame rather than into the state, which is the farm's own choice</b>
        /// (<c>RecordTrace</c>'s <c>if (b == poisoned) w.x = float.NaN;</c>). Poisoning the state
        /// instead would be a test of a different thing: the solve carries a NaN through the
        /// whole articulation within one step, so by the next frame every link is non-finite and
        /// <see cref="FirstNonFiniteLink"/> records the first index walked rather than the link
        /// that went. Written at the one place the solver's own number would be, so what refuses
        /// the frame is the real test and not a second path around it.
        /// </para>
        /// <para>
        /// One comparison against a field per traced body per step, and −1 for the whole of every
        /// run.
        /// </para>
        /// </remarks>
        public void InjectNonFiniteForTest(int link) => _poisonedLink = link;

        private int _poisonedLink = -1;
    }
}
