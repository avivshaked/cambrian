using System;

namespace Evosim.Dynamics
{
    /// <summary>
    /// The private state a step backend reads and writes back, reached without widening any of
    /// it: internal members that name the fields the CPU step touches, for
    /// <see cref="IStepBackend"/> and nothing else.
    /// </summary>
    /// <remarks>
    /// <b>Accessors and not a second copy.</b> Every member here reads or writes the very field
    /// the CPU solver uses, so a body the GPU engine has stepped is a body in the state the CPU
    /// would have left, and its checkpoint is the same file (the spec's section 5). Nothing here
    /// runs on the CPU path.
    /// </remarks>
    public sealed partial class Creature
    {
        // ---- the pending body sphere

        internal Vec3 MirrorPendingCentre { get => _pendingCentre; set => _pendingCentre = value; }

        internal double MirrorPendingRadius { get => _pendingRadius; set => _pendingRadius = value; }

        internal Vec3 MirrorPendingVelocity { get => _pendingVelocity; set => _pendingVelocity = value; }

        internal bool MirrorPendingActive { get => _pendingActive; set => _pendingActive = value; }

        // ---- the pending link spheres, D114; null in a per-body world

        internal double[] MirrorPendingLinkCentre => _pendingLinkCentre;

        internal double[] MirrorPendingLinkRadius => _pendingLinkRadius;

        internal double[] MirrorPendingLinkVelocity => _pendingLinkVelocity;

        /// <summary>The pending link turns, 9 a link (D228's true shape); null unless the world asked for it.</summary>
        internal double[] MirrorPendingLinkRotation => _pendingLinkRotation;

        // ---- the overlap list of the step just taken, and the held list the next will read

        /// <summary>
        /// Replaces the step's overlap list: <paramref name="count"/> ids ascending, and under
        /// per-part contact the link pair of each (this body's part, then the other's).
        /// </summary>
        internal void MirrorSetOverlaps(int count, long[] ids, int[] parts)
        {
            if (_overlapIds.Length < count)
            {
                _overlapIds = new long[Math.Max(count, _overlapIds.Length == 0 ? 8 : 2 * _overlapIds.Length)];
            }

            Array.Copy(ids, _overlapIds, count);
            _overlapCount = count;

            if (parts == null) return;

            if (_overlapParts.Length < 2 * _overlapIds.Length)
            {
                Array.Resize(ref _overlapParts, 2 * _overlapIds.Length);
            }

            Array.Copy(parts, _overlapParts, 2 * count);
        }

        internal int MirrorHeldCount => _heldCount;

        internal long MirrorHeldId(int at) => _heldIds[at];

        /// <summary>Replaces the held list: the ids the next step's census calls held.</summary>
        internal void MirrorSetHeld(int count, long[] ids)
        {
            if (_heldIds.Length < count) _heldIds = new long[Math.Max(count, 8)];
            Array.Copy(ids, _heldIds, count);
            _heldCount = count;
        }

        // ---- package B's scratch that a step fills and Settle reads is the step's own; only
        // the drain marks are the host's, and a backend never touches them.

        // ---- the throw trace

        internal double[] MirrorTrace => _trace;

        internal long[] MirrorTraceSteps => _traceStep;

        internal double[] MirrorTraceTimes => _traceTime;

        internal bool[] MirrorTraceResized => _traceResized;

        internal int MirrorTraceHeld { get => _traceHeld; set => _traceHeld = value; }

        internal int MirrorTraceCursor { get => _traceCursor; set => _traceCursor = value; }

        internal long MirrorFirstNonFiniteStep { get => FirstNonFiniteStep; set => FirstNonFiniteStep = value; }

        internal int MirrorFirstNonFiniteLink { get => FirstNonFiniteLink; set => FirstNonFiniteLink = value; }

        /// <summary>Whether a resize is waiting for the next recorded frame to carry its flag.</summary>
        internal bool MirrorResizedSinceLastFrame
        {
            get => _resizedSinceLastFrame;
            set => _resizedSinceLastFrame = value;
        }

        /// <summary>The link a test poisoned in the trace, or -1.</summary>
        internal int MirrorPoisonedLink => _poisonedLink;

        // ---- what the card holds in two parts, as it holds them (round 60, World.StateVersion 18)

        /// <summary>
        /// The committed link-sphere centres as the card holds them: each coordinate's float
        /// (<c>Hi</c>) and the rest the float leaves out (<c>Lo</c>), whose sum is
        /// <see cref="LinkContactCentre"/>. Null until a card backend hands the body back, and never
        /// set on the CPU engine.
        /// </summary>
        /// <remarks>
        /// <b>The pair and not its sum.</b> The card's two-sum chain lets the rest pass half a float
        /// step, so the float of the sum is not always the card's float, and a body packed from the
        /// sum alone carries a different pair into the next step. A resume did exactly that until
        /// round 60's build: every body was packed from a sum the restore had rebuilt from the poses,
        /// with the rest at zero, and a settled crowd parted from the run at its first second (round
        /// 59's B3, <c>logbook/specs/r59-resume-survey.md</c>).
        /// </remarks>
        internal double[] CardLinkCentreHi;

        /// <summary>The rest of each committed link-sphere centre; see <see cref="CardLinkCentreHi"/>.</summary>
        internal double[] CardLinkCentreLo;

        /// <summary>
        /// The root's position as the card holds it, its float and the carry its compensated sum
        /// keeps: <see cref="BasePosition"/> is <c>Hi</c> less <c>Lo</c>. Null until a card backend
        /// hands the body back.
        /// </summary>
        internal double[] CardBaseHi;

        /// <summary>The carry of the root's position; see <see cref="CardBaseHi"/>.</summary>
        internal double[] CardBaseLo;

        /// <summary>
        /// Whether the card's pair still sums to the committed link centres: false once anything on
        /// the host has moved them (a resize, a birth, a restore without the pair), and then the
        /// card splits the host's centres afresh.
        /// </summary>
        internal bool CardLinkPairHolds()
        {
            if (CardLinkCentreHi == null || LinkContactCentre == null || CardLinkCentreHi.Length != LinkContactCentre.Length)
            {
                return false;
            }

            for (int k = 0; k < LinkContactCentre.Length; k++)
            {
                if (CardLinkCentreHi[k] + CardLinkCentreLo[k] != LinkContactCentre[k]) return false;
            }

            return true;
        }

        /// <summary>Whether the card's pair still gives the root's position; see <see cref="CardLinkPairHolds"/>.</summary>
        internal bool CardBasePairHolds() =>
            CardBaseHi != null &&
            CardBaseHi[0] - CardBaseLo[0] == BasePosition.X &&
            CardBaseHi[1] - CardBaseLo[1] == BasePosition.Y &&
            CardBaseHi[2] - CardBaseLo[2] == BasePosition.Z;
    }
}
