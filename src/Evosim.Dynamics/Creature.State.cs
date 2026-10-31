using System;
using System.IO;
using Evosim.Core;

namespace Evosim.Dynamics
{
    /// <summary>
    /// One body, written down and put back — the solver's half of a checkpoint
    /// (<c>logbook/specs/checkpoint-spec.md</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Everything the next step will read, and nothing it will write first.</b> A creature is
    /// rebuilt by its constructor from its phenotype and the solver's config, which fills every
    /// quantity that is a function of the body's size and plan — masses, inertias, anchors, joint
    /// frames, limits, drag panels. What the constructor cannot know is where the body has got
    /// to, and that is what is here.
    /// </para>
    /// <para>
    /// <b>The derived pose arrays are written rather than re-derived, deliberately.</b>
    /// <see cref="Kinematics.Poses"/> and <see cref="Kinematics.Velocities"/> would reproduce
    /// <c>Position</c>, <c>Rotation</c>, <c>RotationMatrix</c>, the non-root <c>Spin</c> and
    /// <c>Velocity</c>, and the motion subspace, from the root pose and the joint coordinates —
    /// they are the same two functions the step itself last ran. Writing the answers down costs
    /// forty-six doubles a link and removes the question of whether the restore's derivation is
    /// the step's derivation, which is exactly the class of fault a checkpoint exists to make
    /// impossible.
    /// </para>
    /// <para>
    /// <b>What is left out is what one step fills before it reads it.</b> <c>Fext</c> and
    /// <c>Tau</c> are cleared at the top of the step; the drag hand-off buffers are written for
    /// every link by <see cref="Fluid.Apply"/> and the passive ones for every degree of freedom
    /// by the joint-torque pass, and all three pending flags are false between steps because
    /// <see cref="Settle"/> clears them; the step's own overlap list is emptied by
    /// <c>BeginContactStep</c>; and the senses are sampled before the brain reads them. The
    /// <i>held</i> overlap list is not in that set — it is this step's list kept for the next
    /// one's "were these two touching before" — so it is written.
    /// </para>
    /// </remarks>
    public sealed partial class Creature
    {
        /// <summary>Writes everything about this body that the next step will read.</summary>
        public void WriteState(BinaryWriter w) => WriteState(w, cardBlock: true);

        /// <summary>
        /// Writes the body in this build's layout, or with <paramref name="cardBlock"/> false in the
        /// layout of <c>World.StateVersion</c> 17, which has no place for the card's pairs.
        /// </summary>
        public void WriteState(BinaryWriter w, bool cardBlock)
        {
            // The step's own sphere, committed now rather than at the next grid build, so the
            // committed one written below is what the next step reads (EnsureContactCommitted).
            EnsureContactCommitted();

            StateIo.Tag(w, "BODY");
            w.Write(Id);
            w.Write(Links);
            w.Write(Dof);

            // ---- the root's pose, which every other link hangs off

            w.Write(BasePosition.X);
            w.Write(BasePosition.Y);
            w.Write(BasePosition.Z);
            w.Write(BaseRotation.X);
            w.Write(BaseRotation.Y);
            w.Write(BaseRotation.Z);
            w.Write(BaseRotation.W);

            // ---- the articulation's own coordinates

            StateIo.WriteDoubles(w, Q);
            StateIo.WriteDoubles(w, Qd);
            StateIo.WriteDoubles(w, BallRotation);

            // ---- the derived pose and motion, as the last step left them

            StateIo.WriteDoubles(w, Position);
            StateIo.WriteDoubles(w, Rotation);
            StateIo.WriteDoubles(w, RotationMatrix);
            StateIo.WriteDoubles(w, Spin);
            StateIo.WriteDoubles(w, Velocity);
            StateIo.WriteDoubles(w, Sang);
            StateIo.WriteDoubles(w, Slin);
            StateIo.WriteDoubles(w, Cbias);

            // ---- the water. RelativeVelocity is read by the next step's sensors before the
            // drag pass rewrites it, which is the farm's one-step-stale flow channel.

            StateIo.WriteDoubles(w, Water);
            StateIo.WriteDoubles(w, WaterAcceleration);
            StateIo.WriteDoubles(w, RelativeVelocity);
            w.Write(Patch);
            StateIo.WriteFloat3(w, HeldWater);
            StateIo.WriteFloat3(w, HeldWaterAcceleration);
            w.Write(WaterSampledAt);

            // ---- the contact sphere other bodies read, and its pending twin

            WriteVec(w, ContactCentre);
            w.Write(ContactRadius);
            WriteVec(w, ContactVelocity);
            w.Write(ContactActive);
            WriteVec(w, _pendingCentre);
            w.Write(_pendingRadius);
            WriteVec(w, _pendingVelocity);
            w.Write(_pendingActive);

            // ---- the overlaps the next step will call held

            w.Write(_heldCount);
            for (int i = 0; i < _heldCount; i++) w.Write(_heldIds[i]);

            // ---- package B's four accumulators and the four drain marks

            w.Write(MechanicalWorkJoules);
            w.Write(SignedWorkJoules);
            w.Write(DissipatedJoules);
            w.Write(PassiveJointWorkJoules);
            w.Write(_workDrainedAt);
            w.Write(_dissipationDrainedAt);
            w.Write(_driveLimitDrainedAt);
            w.Write(_dragLimitDrainedAt);

            w.Write(DriveImpulsesLimited);
            w.Write(DragImpulsesLimited);

            // ---- identity, growth and the throw trace

            w.Write(Alive);
            w.Write(AppliedBodyFraction);
            w.Write(Resizes);
            w.Write(ResizedAtStep);
            w.Write(_resizedSinceLastFrame);
            w.Write(FirstNonFiniteStep);
            w.Write(FirstNonFiniteLink);
            w.Write(_poisonedLink);

            w.Write(_trace != null);
            if (_trace != null)
            {
                w.Write(_traceHeld);
                w.Write(_traceCursor);
                StateIo.WriteDoubles(w, _trace);
                StateIo.WriteLongs(w, _traceStep, _traceStep.Length);
                StateIo.WriteDoubles(w, _traceTime);

                w.Write(_traceResized.Length);
                for (int i = 0; i < _traceResized.Length; i++) w.Write(_traceResized[i]);
            }

            // ---- the touch of the rock and the grip (D162, World.StateVersion 17). Each block is
            // there exactly when the constructor allocates it, which is a function of the plan and
            // the solver's config, so the reader's body says which to expect; the world refuses an
            // older layout in a world that allocates either.

            if (RockTouched != null)
            {
                StateIo.Tag(w, "TUCH");
                WriteBools(w, RockTouched);
                StateIo.WriteDoubles(w, RockPoint);
                StateIo.WriteDoubles(w, RockNormal);
            }

            if (Anchors != null)
            {
                StateIo.Tag(w, "GRIP");
                WriteBools(w, GripHeld);
                StateIo.WriteDoubles(w, GripAnchor);
                StateIo.WriteDoubles(w, GripAttitude);
                StateIo.WriteDoubles(w, GripPoint);
                StateIo.WriteDoubles(w, GripStrength);
                w.Write(ReserveLowSeconds);
                w.Write(GripOwedJoules);
                w.Write(GripsFormed);
                w.Write(GripsBroken);
                w.Write(GripHeldSeconds);
            }

            // D268: the second grip sense's record and the let-go count, there exactly when the cell lets the brain
            // hold. No checkpoint before the build can have it on, so the layout version does not move.
            if (GripPull != null)
            {
                StateIo.Tag(w, "GRPL");
                StateIo.WriteDoubles(w, GripPull);
                w.Write(GripsLetGo);
            }

            // ---- what the card holds in two parts (round 60, World.StateVersion 18). The link
            // centres' float and rest, with the radii and velocities the card left beside them, and
            // the root's float and carry, each written only while it still gives what the host holds
            // (CardLinkPairHolds). A body the CPU engine stepped writes neither, and its restore
            // rebuilds the spheres from the poses exactly, as it always did.

            if (cardBlock)
            {
                StateIo.Tag(w, "CARD");

                bool links = CardLinkPairHolds();
                w.Write(links);
                if (links)
                {
                    StateIo.WriteDoubles(w, CardLinkCentreHi);
                    StateIo.WriteDoubles(w, CardLinkCentreLo);
                    StateIo.WriteDoubles(w, LinkContactRadius);
                    StateIo.WriteDoubles(w, LinkContactVelocity);
                }

                bool root = CardBasePairHolds();
                w.Write(root);
                if (root)
                {
                    StateIo.WriteDoubles(w, CardBaseHi);
                    StateIo.WriteDoubles(w, CardBaseLo);
                }
            }

            // ---- the controller

            Brain.WriteState(w);
            Drive.WriteState(w);

            StateIo.Tag(w, "BEND");
        }

        /// <summary>Puts the body back. Call on a creature built from the same phenotype.</summary>
        public void ReadState(BinaryReader r) => ReadState(r, cardBlock: true);

        /// <summary>
        /// Puts the body back from this build's layout, or with <paramref name="cardBlock"/> false
        /// from <c>World.StateVersion</c> 17's, which holds no card pairs: its link spheres are
        /// rebuilt from the poses, exact on the CPU engine and the card's centres less their rest.
        /// </summary>
        public void ReadState(BinaryReader r, bool cardBlock)
        {
            StateIo.Tag(r, "BODY");

            int id = r.ReadInt32();
            int links = r.ReadInt32();
            int dof = r.ReadInt32();

            if (id != Id || links != Links || dof != Dof)
            {
                throw new InvalidDataException(
                    "The checkpoint holds body " + id + " with " + links + " links and " + dof +
                    " degrees of freedom, and this body is " + Id + " with " + Links + " and " +
                    Dof + ". A body's plan is fixed from birth to death, so this is two animals " +
                    "wearing one name.");
            }

            BasePosition = new Vec3(r.ReadDouble(), r.ReadDouble(), r.ReadDouble());
            BaseRotation = new QuatD(
                r.ReadDouble(), r.ReadDouble(), r.ReadDouble(), r.ReadDouble());

            StateIo.ReadDoubles(r, Q, "joint coordinates");
            StateIo.ReadDoubles(r, Qd, "joint rates");
            StateIo.ReadDoubles(r, BallRotation, "ball rotations");

            StateIo.ReadDoubles(r, Position, "link positions");
            StateIo.ReadDoubles(r, Rotation, "link rotations");
            StateIo.ReadDoubles(r, RotationMatrix, "link rotation matrices");
            StateIo.ReadDoubles(r, Spin, "link spins");
            StateIo.ReadDoubles(r, Velocity, "link velocities");
            StateIo.ReadDoubles(r, Sang, "motion subspace (angular)");
            StateIo.ReadDoubles(r, Slin, "motion subspace (linear)");
            StateIo.ReadDoubles(r, Cbias, "bias acceleration");

            StateIo.ReadDoubles(r, Water, "water velocity");
            StateIo.ReadDoubles(r, WaterAcceleration, "water acceleration");
            StateIo.ReadDoubles(r, RelativeVelocity, "relative velocity");
            Patch = r.ReadInt32();
            HeldWater = StateIo.ReadFloat3(r);
            HeldWaterAcceleration = StateIo.ReadFloat3(r);
            WaterSampledAt = r.ReadDouble();

            ContactCentre = ReadVec(r);
            ContactRadius = r.ReadDouble();
            ContactVelocity = ReadVec(r);
            ContactActive = r.ReadBoolean();
            _pendingCentre = ReadVec(r);
            _pendingRadius = r.ReadDouble();
            _pendingVelocity = ReadVec(r);
            _pendingActive = r.ReadBoolean();

            // The file's committed sphere is the one the next step reads: the writer committed
            // the step's own before it wrote (WriteState's first line), so nothing is due.
            _commitDue = false;

            // D114's spheres, re-derived from what was just read rather than stored.
            RestoreLinkSpheres();

            int held = r.ReadInt32();
            if (_heldIds.Length < held) _heldIds = new long[held];
            for (int i = 0; i < held; i++) _heldIds[i] = r.ReadInt64();
            _heldCount = held;

            MechanicalWorkJoules = r.ReadDouble();
            SignedWorkJoules = r.ReadDouble();
            DissipatedJoules = r.ReadDouble();
            PassiveJointWorkJoules = r.ReadDouble();
            _workDrainedAt = r.ReadDouble();
            _dissipationDrainedAt = r.ReadDouble();
            _driveLimitDrainedAt = r.ReadInt64();
            _dragLimitDrainedAt = r.ReadInt64();

            DriveImpulsesLimited = r.ReadInt64();
            DragImpulsesLimited = r.ReadInt64();

            Alive = r.ReadBoolean();
            AppliedBodyFraction = r.ReadSingle();
            Resizes = r.ReadInt64();
            ResizedAtStep = r.ReadInt64();
            _resizedSinceLastFrame = r.ReadBoolean();
            FirstNonFiniteStep = r.ReadInt64();
            FirstNonFiniteLink = r.ReadInt32();
            _poisonedLink = r.ReadInt32();

            if (r.ReadBoolean())
            {
                // The ring is allocated by EnableTrace, which the harness calls for a jointed
                // body before this runs. A checkpoint that holds one for a body that has none is
                // a jointedness that has changed under a fixed plan, which cannot happen.
                EnableTrace();

                _traceHeld = r.ReadInt32();
                _traceCursor = r.ReadInt32();
                StateIo.ReadDoubles(r, _trace, "throw trace");
                long[] steps = StateIo.ReadLongs(r);
                for (int i = 0; i < _traceStep.Length; i++) _traceStep[i] = steps[i];
                StateIo.ReadDoubles(r, _traceTime, "throw trace times");

                int resized = r.ReadInt32();
                for (int i = 0; i < resized; i++) _traceResized[i] = r.ReadBoolean();
            }

            if (RockTouched != null)
            {
                StateIo.Tag(r, "TUCH");
                ReadBools(r, RockTouched, "touches of the rock");
                StateIo.ReadDoubles(r, RockPoint, "rock contact points");
                StateIo.ReadDoubles(r, RockNormal, "rock normals");
            }

            if (Anchors != null)
            {
                StateIo.Tag(r, "GRIP");
                ReadBools(r, GripHeld, "grips held");
                StateIo.ReadDoubles(r, GripAnchor, "grip anchors");
                StateIo.ReadDoubles(r, GripAttitude, "grip attitudes");
                StateIo.ReadDoubles(r, GripPoint, "grip points");
                StateIo.ReadDoubles(r, GripStrength, "grip strengths");
                ReserveLowSeconds = r.ReadDouble();
                GripOwedJoules = r.ReadDouble();
                GripsFormed = r.ReadInt64();
                GripsBroken = r.ReadInt64();
                GripHeldSeconds = r.ReadDouble();
            }

            if (GripPull != null)
            {
                StateIo.Tag(r, "GRPL");
                StateIo.ReadDoubles(r, GripPull, "grip pulls");
                GripsLetGo = r.ReadInt64();
            }

            CardLinkCentreHi = CardLinkCentreLo = CardBaseHi = CardBaseLo = null;
            if (cardBlock)
            {
                StateIo.Tag(r, "CARD");

                if (r.ReadBoolean())
                {
                    if (!LinkContactActive)
                    {
                        throw new InvalidDataException(
                            "The checkpoint holds the card's link spheres for body " + Id + ", which has " +
                            "none: its world steps without per-part contact.");
                    }

                    CardLinkCentreHi = new double[3 * Links];
                    CardLinkCentreLo = new double[3 * Links];
                    StateIo.ReadDoubles(r, CardLinkCentreHi, "card link centres");
                    StateIo.ReadDoubles(r, CardLinkCentreLo, "card link centre rests");
                    StateIo.ReadDoubles(r, LinkContactRadius, "card link radii");
                    StateIo.ReadDoubles(r, LinkContactVelocity, "card link velocities");

                    // The spheres as the card handed them back, committed and pending alike, in
                    // place of the ones rebuilt from the poses above.
                    for (int k = 0; k < 3 * Links; k++)
                    {
                        LinkContactCentre[k] = CardLinkCentreHi[k] + CardLinkCentreLo[k];
                    }

                    Array.Copy(LinkContactCentre, _pendingLinkCentre, 3 * Links);
                    Array.Copy(LinkContactRadius, _pendingLinkRadius, Links);
                    Array.Copy(LinkContactVelocity, _pendingLinkVelocity, 3 * Links);
                }

                if (r.ReadBoolean())
                {
                    CardBaseHi = new double[3];
                    CardBaseLo = new double[3];
                    StateIo.ReadDoubles(r, CardBaseHi, "card root position");
                    StateIo.ReadDoubles(r, CardBaseLo, "card root carry");

                    if (!CardBasePairHolds())
                    {
                        throw new InvalidDataException(
                            "The checkpoint's card root position for body " + Id + " does not give the " +
                            "position it holds, so one of the two was written from another moment.");
                    }
                }
            }

            Brain.ReadState(r);
            Drive.ReadState(r);

            StateIo.Tag(r, "BEND");
        }

        private static void WriteVec(BinaryWriter w, Vec3 v)
        {
            w.Write(v.X);
            w.Write(v.Y);
            w.Write(v.Z);
        }

        private static Vec3 ReadVec(BinaryReader r) =>
            new Vec3(r.ReadDouble(), r.ReadDouble(), r.ReadDouble());

        private static void WriteBools(BinaryWriter w, bool[] values)
        {
            w.Write(values.Length);
            for (int i = 0; i < values.Length; i++) w.Write(values[i]);
        }

        private static void ReadBools(BinaryReader r, bool[] into, string what)
        {
            int n = r.ReadInt32();
            if (n != into.Length)
            {
                throw new InvalidDataException(
                    "The checkpoint holds " + n + " " + what + " and this body has " + into.Length + ".");
            }

            for (int i = 0; i < n; i++) into[i] = r.ReadBoolean();
        }
    }
}
