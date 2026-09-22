using System.Collections.Generic;
using System.IO;
using Evosim.Core;
using Evosim.Dynamics;

namespace Evosim.Farm
{
    /// <summary>
    /// The harness's own state — its per-body bookkeeping, its instruments and the two objects it
    /// owns that hold something between steps (<c>logbook/specs/checkpoint-spec.md</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The bodies are rebuilt through the ordinary birth path, not deserialized.</b>
    /// <see cref="Reconcile"/> walks <c>World.Living</c> and builds a solver body for every
    /// creature that has none, in the world's own order, adding each in ascending id order and
    /// wiring its senses; that is the one path a body has ever been made by, and a second one
    /// here would be a second definition of what a creature is. What the checkpoint then writes
    /// over each body is where it had got to — <c>Creature.State.cs</c>.
    /// </para>
    /// <para>
    /// <b>A checkpoint reconciles before it writes, and that is what makes the restore simple.</b>
    /// Between the economy's step and the top of the next physics step the harness holds bodies
    /// for creatures that have just died and holds none for creatures that have just been
    /// conceived — a half-state that a restore would have to reproduce exactly, including the
    /// dead bodies' undrained limiter counts. Running the reconcile first puts the harness in the
    /// state the next step would have put it in anyway: the same bodies built from the same
    /// reserved placements, the same departed bodies drained in the same order, and the same
    /// <c>_movedOutsideTheSolver</c> debt standing. The work is moved earlier in the loop and
    /// nothing about it is repeated, so a run that takes checkpoints is the same realisation as
    /// one that does not.
    /// </para>
    /// </remarks>
    public sealed partial class Simulation
    {
        /// <summary>
        /// True while a restore is rebuilding bodies, so <see cref="Build"/> does not complain
        /// about a placement nobody reserved.
        /// </summary>
        /// <remarks>
        /// Every body being rebuilt already exists somewhere in the world; its reservation was
        /// consumed when it was first born, possibly thousands of seconds ago. The origin
        /// <see cref="Build"/> puts it at is overwritten a moment later by the body's own saved
        /// pose, so the warning would be a line of noise per living creature saying nothing.
        /// </remarks>
        private bool _restoring;

        /// <summary>
        /// Brings the harness to the state the top of the next step would bring it to, so that a
        /// checkpoint records a whole state rather than a half-stepped one.
        /// </summary>
        public void SettleBeforeCheckpoint() => Reconcile();

        public void WriteState(BinaryWriter w)
        {
            StateIo.Tag(w, "HARN");

            w.Write(Steps);
            w.Write(_reconciledAt);
            w.Write(_movedOutsideTheSolver);
            w.Write(_sinceGrowthStep);

            w.Write(MeanSpeed);
            w.Write(MaxSpeed);
            w.Write(WorkThisStep);
            w.Write(AboveSurface);
            w.Write(DriveImpulsesLimited);
            w.Write(DragImpulsesLimited);
            w.Write(Resizes);
            w.Write(MaxResizeJumpMetres);
            w.Write(MaxResizeStepMetres);
            w.Write(MaxJointMassRatio);
            w.Write(BodiesOverMassRatio10);
            w.Write(DissipatedJoules);

            w.Write(_jointedSpeedSum);
            w.Write(_jointedSpeedSamples);
            w.Write(_rigidSpeedSum);
            w.Write(_rigidSpeedSamples);

            Dynamics.WriteState(w);

            w.Write(Volume != null);
            Volume?.WriteState(w);

            // In the order the harness steps them, which is World.Living's — the order every
            // accumulator above was summed in.
            StateIo.Tag(w, "BDYS");
            w.Write(_order.Count);

            for (int i = 0; i < _order.Count; i++)
            {
                Body body = _order[i];

                w.Write(body.Creature.Id);
                w.Write(body.Radius);
                StateIo.WriteFloat3(w, body.LastRootPosition);
                StateIo.WriteFloat3(w, body.PreviousCentre);
                StateIo.WriteFloat3(w, body.PreviousRoot);
                w.Write(body.Settled);
                w.Write(body.ResizedLastStep);
                w.Write(body.CountedOverMassRatio);

                body.Solver.WriteState(w);
            }

            StateIo.Tag(w, "HEND");
        }

        /// <summary>
        /// Puts the harness back. Call on a simulation built over a world that has already read
        /// its own state.
        /// </summary>
        public void ReadState(BinaryReader r)
        {
            StateIo.Tag(r, "HARN");

            long steps = r.ReadInt64();
            long reconciledAt = r.ReadInt64();
            bool moved = r.ReadBoolean();
            float sinceGrowth = r.ReadSingle();

            double meanSpeed = r.ReadDouble();
            double maxSpeed = r.ReadDouble();
            double work = r.ReadDouble();
            int above = r.ReadInt32();
            long driveLimited = r.ReadInt64();
            long dragLimited = r.ReadInt64();
            long resizes = r.ReadInt64();
            double resizeJump = r.ReadDouble();
            double resizeStep = r.ReadDouble();
            double massRatio = r.ReadDouble();
            long overRatio = r.ReadInt64();
            double dissipated = r.ReadDouble();

            double jointedSum = r.ReadDouble();
            long jointedSamples = r.ReadInt64();
            double rigidSum = r.ReadDouble();
            long rigidSamples = r.ReadInt64();

            Dynamics.ReadState(r);

            bool hasVolume = r.ReadBoolean();
            if (hasVolume != (Volume != null))
            {
                throw new InvalidDataException(
                    "The checkpoint was taken in a " + (hasVolume ? "shared" : "tiled") +
                    " world and this one is " + (Volume != null ? "shared" : "tiled") + ".");
            }

            Volume?.ReadState(r);

            // Every living creature gets a body, through the one path that builds one. The
            // reconciled-at mark is forced back so this cannot early-return on a world whose
            // birth and death counts happen to match the mark the checkpoint carries.
            _reconciledAt = -1;
            _restoring = true;
            try
            {
                Reconcile();
            }
            finally
            {
                _restoring = false;
            }

            // After the rebuild, because Build's own mass-ratio reading counts every body it
            // makes and would otherwise tally the whole population as newly over the threshold.
            Steps = steps;
            _reconciledAt = reconciledAt;
            _movedOutsideTheSolver = moved;
            _sinceGrowthStep = sinceGrowth;

            MeanSpeed = meanSpeed;
            MaxSpeed = maxSpeed;
            WorkThisStep = work;
            AboveSurface = above;
            DriveImpulsesLimited = driveLimited;
            DragImpulsesLimited = dragLimited;
            Resizes = resizes;
            MaxResizeJumpMetres = resizeJump;
            MaxResizeStepMetres = resizeStep;
            MaxJointMassRatio = massRatio;
            BodiesOverMassRatio10 = overRatio;
            DissipatedJoules = dissipated;

            _jointedSpeedSum = jointedSum;
            _jointedSpeedSamples = jointedSamples;
            _rigidSpeedSum = rigidSum;
            _rigidSpeedSamples = rigidSamples;

            StateIo.Tag(r, "BDYS");

            int count = r.ReadInt32();
            if (count != _order.Count)
            {
                throw new InvalidDataException(
                    "The checkpoint holds " + count + " bodies and rebuilding the world's " +
                    "living gave " + _order.Count + ". A checkpoint is written with the harness " +
                    "reconciled, so the two are the same set by construction; a difference means " +
                    "the population and the checkpoint are not the same moment.");
            }

            var seen = new HashSet<long>();

            for (int i = 0; i < count; i++)
            {
                long id = r.ReadInt64();

                if (!_bodies.TryGetValue(id, out Body body))
                {
                    throw new InvalidDataException(
                        "The checkpoint holds a body for creature " + id + " and the world it " +
                        "was restored into has no such creature alive.");
                }

                seen.Add(id);

                body.Radius = r.ReadSingle();
                body.LastRootPosition = StateIo.ReadFloat3(r);
                body.PreviousCentre = StateIo.ReadFloat3(r);
                body.PreviousRoot = StateIo.ReadFloat3(r);
                body.Settled = r.ReadBoolean();
                body.ResizedLastStep = r.ReadBoolean();
                body.CountedOverMassRatio = r.ReadBoolean();

                body.Solver.ReadState(r);
            }

            if (seen.Count != _bodies.Count)
            {
                throw new InvalidDataException(
                    "The checkpoint restored " + seen.Count + " bodies into a harness holding " +
                    _bodies.Count + ". A body with no state is a creature at the origin at rest.");
            }

            StateIo.Tag(r, "HEND");
        }
    }
}
