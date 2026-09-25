using System;
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
    /// wiring its two world senses; that is the one path a body has ever been made by, and a
    /// second one here would be a second definition of what a creature is. What the checkpoint
    /// then writes over each body is where it had got to — <c>Creature.State.cs</c> — and, from
    /// <c>Checkpoint.Version</c> 6, which of its other two senses the metabolic step had wired
    /// (<see cref="WriteSenses"/>). Until then a restored body read contact and damage as 0 until
    /// its first metabolic step, and round 48's resume parted from the run at the first sample.
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

        /// <summary>
        /// Living bodies whose plan has changed since their solver was built (D106 item 2,
        /// rule 7). Zero whenever a metabolic step has finished, from round 49.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>A checkpoint cannot be restored while this is above zero.</b> The world writes the
        /// creature on its new plan and the harness writes the solver on its old one; a restore
        /// builds the body from the new plan, with a link count the saved solver state does not
        /// have, and <see cref="ReadState"/> refuses it. Carrying the old plan through the file
        /// instead would need the developer's pre-bite plan, which nothing keeps.
        /// </para>
        /// <para>
        /// <b>Round 49 closed the window this counted.</b> Until then a part bitten off inside
        /// the world's step waited for the next growth step to be rebuilt, and the run loop
        /// deferred any checkpoint that fell in between. Now <see cref="RebuildChangedPlans"/>
        /// rebuilds it on the same metabolic step and the growth step rebuilds the module rule's
        /// changes, so nothing is pending when the loop asks. The loop still asks, and still
        /// defers, as the guard for a plan-changing path that forgets its rebuild.
        /// </para>
        /// </remarks>
        public int PlanChangesPending()
        {
            int pending = 0;
            IReadOnlyList<Organism> living = World.Living;

            for (int i = 0; i < living.Count; i++)
            {
                if (!_bodies.TryGetValue(living[i].Id, out Body body)) continue;
                if (living[i].PlanRevision != body.Solver.AppliedPlanRevision) pending++;
            }

            return pending;
        }

        public void WriteState(BinaryWriter w) => WriteState(w, World.StateVersion);

        /// <summary>
        /// Writes the harness in the layout that goes with a world layout: this build's, or the
        /// lossy one for the test that holds the reader to what round 48's build wrote.
        /// </summary>
        /// <param name="w">The payload writer, after the world's own state.</param>
        /// <param name="layout">
        /// <c>World.StateVersion</c> or <c>World.LossyStateVersion</c> — the world's version and
        /// not the file's, because <see cref="ReadState"/> tells the two layouts apart by the
        /// version the world has just read.
        /// </param>
        internal void WriteState(BinaryWriter w, int layout)
        {
            bool current = layout >= World.StateVersion;

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

            // Cumulative, and a stats column: restarted at zero it put a step in every window a
            // reader differences. Not written until Checkpoint.Version 6.
            if (current) w.Write(ModuleRebuilds);

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

                if (current)
                {
                    w.Write(body.Solver.AppliedPlanRevision);
                    WriteAsBuilt(w, body);
                    WriteSenses(w, body);
                }

                body.Solver.WriteState(w);
            }

            StateIo.Tag(w, "HEND");
        }

        /// <summary>
        /// Which body the solver is stepping: the organism's own phenotype, or the size it was
        /// last resized to when the organism has grown since.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Core grows a body every metabolic step and the harness resizes it every growth
        /// step</b> (fable-propose-growth.md rule 8), so between two growth steps the solver steps
        /// a smaller body than the organism's. A restore builds each body from the organism's
        /// phenotype, which is the right body at a growth step and a larger one anywhere else; a
        /// checkpoint taken between growth steps restored every growing body at a size the run
        /// had not given it yet, with its masses, anchors, limits and drag panels to match, and
        /// the restore parted from the run at its first physics step (found by
        /// <c>CheckpointFidelity</c>'s twin step, 2026-09-25). A cadence that is a multiple of
        /// the growth step, which every round's is, never met it; a stop or a wall between two
        /// growth steps did.
        /// </para>
        /// <para>
        /// The body is the organism's adult or one scaled copy of it, the contract the world's own
        /// writer holds its organisms to, so a flag and the copy's scale say which.
        /// </para>
        /// </remarks>
        private static void WriteAsBuilt(BinaryWriter w, Body body)
        {
            Phenotype built = body.Solver.Phenotype;
            Organism creature = body.Creature;

            bool asTheOrganism = ReferenceEquals(built, creature.Phenotype);
            w.Write(asTheOrganism);
            if (asTheOrganism) return;

            bool adult = ReferenceEquals(built, creature.AdultPhenotype);
            w.Write(adult);
            w.Write(built.ScaledBy);

            // A body still on its old plan is written as it stands and refused by the reader on
            // its plan revision, which is the clearer refusal. The harness rebuilds a changed
            // plan on the metabolic step of the change, so the loop never meets one.
            bool onItsPlan = body.Solver.AppliedPlanRevision == creature.PlanRevision;

            if (onItsPlan && !adult && built.ScaledBy == 1f)
            {
                throw new InvalidOperationException(
                    "Body " + creature.Id + " is stepping a phenotype that is neither its " +
                    "organism's current one, its adult, nor a scaled copy of the adult. The harness " +
                    "builds and resizes a body only from the organism's phenotype, so this is a " +
                    "path the checkpoint cannot restore.");
            }
        }

        /// <summary>
        /// Puts the rebuilt body back at the size the saved one was stepping, when that is not
        /// the organism's. See <see cref="WriteAsBuilt"/>.
        /// </summary>
        /// <remarks>
        /// A resize and not a rebuild: <c>Creature.Resize</c> and the constructor are one
        /// derivation of everything a size decides, and the solver state read next overwrites the
        /// pose, the motion, the contact sphere and the counts the resize touches.
        /// </remarks>
        private void ReadAsBuilt(BinaryReader r, Body body)
        {
            if (r.ReadBoolean()) return;

            bool adult = r.ReadBoolean();
            float scale = r.ReadSingle();

            Organism creature = body.Creature;
            Phenotype built = adult
                ? creature.AdultPhenotype
                : creature.AdultPhenotype.Scaled(scale, Config.Shapes);

            body.Solver.Resize(built);
        }

        /// <summary>
        /// Whether the body's contact and damage senses were wired, as two flags, after checking
        /// that each wired one is its own organism's array.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Flags and not the arrays.</b> The arrays are the organism's, carried by the world's
        /// own state (<c>Organism.PartContact</c> and <c>PartDamage</c>), and a sense is either
        /// null or that very array: <c>HandBackWhatWasFelt</c> and the rebuild of a body on a new
        /// plan (<c>RebuildOnTheNewPlan</c>) are the only things that set one, and both set it to
        /// the organism's. So what the harness has to carry is which of the two the body holds,
        /// and <see cref="ReadState"/> puts the restored organism's array back wherever the flag
        /// is set.
        /// </para>
        /// <para>
        /// <b>Not a blanket re-wire</b>, because null is a real state here that the organism's
        /// array does not predict: a newborn the checkpoint's own reconcile has just built holds
        /// null senses until the next metabolic step hands it the arrays, while its organism may
        /// already hold one. A restore that wired it would sense a step early. A body rebuilt on
        /// a new plan was a second such case until round 49, when the rebuild began handing it
        /// the organism's carried arrays itself.
        /// </para>
        /// <para>
        /// <b>A wired sense that is not its organism's array is refused.</b> Nothing in the loop
        /// can produce one; if something does, the restore could not reproduce it, and a
        /// checkpoint that cannot be restored should fail where it is written.
        /// </para>
        /// </remarks>
        private static void WriteSenses(BinaryWriter w, Body body)
        {
            CreatureSenses senses = body.Solver.Senses;
            Organism creature = body.Creature;

            if (senses.Contact != null && !ReferenceEquals(senses.Contact, creature.PartContact))
            {
                throw new InvalidOperationException(
                    "Body " + creature.Id + " senses a contact array that is not its organism's. " +
                    "The harness hands a body its organism's array and nothing else, so this is a " +
                    "wiring the checkpoint cannot restore.");
            }

            if (senses.Damage != null && !ReferenceEquals(senses.Damage, creature.PartDamage))
            {
                throw new InvalidOperationException(
                    "Body " + creature.Id + " senses a damage array that is not its organism's. " +
                    "The harness hands a body its organism's array and nothing else, so this is a " +
                    "wiring the checkpoint cannot restore.");
            }

            w.Write(senses.Contact != null);
            w.Write(senses.Damage != null);
        }

        /// <summary>
        /// Puts the harness back. Call on a simulation built over a world that has already read
        /// its own state.
        /// </summary>
        /// <remarks>
        /// <b>The layout is the world's.</b> A file of <c>Checkpoint.Version</c> 6 holds a world of
        /// <c>World.StateVersion</c> 12 (11 before round 49's instruments, which the world refuses)
        /// and a harness with the rebuild count, each body's plan
        /// revision and its two sense flags; a file of version 4, round 48's, holds a version-10
        /// world and a harness without them. The world has just read its version, so this reads
        /// the half that goes with it: from a version-4 file every body comes back with its
        /// contact and damage senses unwired and the rebuild count at zero, which is what the
        /// build that wrote it restored, and the header's <c>Differences</c> has already named
        /// the file as read lossily.
        /// </remarks>
        public void ReadState(BinaryReader r)
        {
            bool current = World.StateVersionRead >= World.StateVersion;

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

            long rebuilds = current ? r.ReadInt64() : 0L;

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

            ModuleRebuilds = rebuilds;

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

                if (current)
                {
                    // Asked before the solver's own state is read, so that a checkpoint taken
                    // between a plan change and the rebuild is refused for what it is rather than
                    // as "two animals wearing one name". Since round 49 the harness rebuilds on
                    // the metabolic step of the change and the run loop does not write one
                    // (PlanChangesPending); this is the guard behind both.
                    int applied = r.ReadInt32();

                    if (applied != body.Creature.PlanRevision)
                    {
                        throw new InvalidDataException(
                            "Body " + id + " was saved on plan revision " + applied + " and its " +
                            "creature is on revision " + body.Creature.PlanRevision + ": the " +
                            "checkpoint was taken after the body's plan changed and before the " +
                            "harness rebuilt it, and the restore can build only the new plan. " +
                            "Resume from an earlier checkpoint.");
                    }

                    ReadAsBuilt(r, body);
                    ReadSenses(r, body);
                }

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

        /// <summary>
        /// Wires the body's contact and damage senses back to its restored organism's arrays
        /// where the saved body held them. See <see cref="WriteSenses"/>.
        /// </summary>
        private static void ReadSenses(BinaryReader r, Body body)
        {
            bool contact = r.ReadBoolean();
            bool damage = r.ReadBoolean();

            Organism creature = body.Creature;

            if ((contact && creature.PartContact == null) || (damage && creature.PartDamage == null))
            {
                throw new InvalidDataException(
                    "Body " + creature.Id + " was saved sensing its organism's " +
                    (contact && creature.PartContact == null ? "contact" : "damage") +
                    " array and the restored organism holds none. The world's state and the " +
                    "harness's disagree about the same moment.");
            }

            CreatureSenses senses = body.Solver.Senses;
            senses.Contact = contact ? creature.PartContact : null;
            senses.Damage = damage ? creature.PartDamage : null;
        }
    }
}
