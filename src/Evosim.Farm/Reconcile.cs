using System;
using System.Collections.Generic;
using Evosim.Core;
using Evosim.Dynamics;
using Evosim.Dynamics.Placement;

namespace Evosim.Farm
{
    /// <summary>
    /// Births and deaths — <c>Ecosystem.Reconcile</c> and <c>Ecosystem.Build</c>, out of Unity.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Run before stepping rather than after the economy</b>, so that a creature born on one
    /// metabolic step is simulated for the whole of the next one rather than for all of it but
    /// the first stroke.
    /// </para>
    /// <para>
    /// <b>Added in ascending id order and never appended.</b> The solver steps its list in list
    /// order and takes every cross-body sum in list order, so the trajectory depends on the order
    /// the list is in; ascending id is the one order that is a function of the population rather
    /// than of its history, which is what makes a run reproducible across births and deaths.
    /// <c>AddInIdOrder</c> and not <c>Add</c>, and the contact instrument checks it at every
    /// step.
    /// </para>
    /// </remarks>
    public sealed partial class Simulation
    {
        /// <summary>Gives every new organism a body and takes it away from every dead one.</summary>
        private void Reconcile()
        {
            // Every way a creature enters or leaves the economy, or the next inoculant would have
            // no body until the next birth or death happened to bump the count.
            // D159's immigrants are one of those ways, and Immigrated is 0 outside a migrating
            // group, so the count everywhere else is the one it always was.
            //
            // So is D115's trickle, which counts its founders in TrickleSpawns and in neither of the
            // floor's counters. Until 2026-10-10 it was left out here, and a trickle founder admitted
            // in a step with no birth, death or floor spawn waited for its body, fed by the world at
            // its patch's centre and out of physics, until one of those happened: up to 2 s in round
            // 57's world just after its floor closed, and 12.5 s in a sparse one (the bodiless
            // instrument, Metabolise.cs; logbook/specs/build-readings.md). Every trickle world from
            // round 46's build through round 57 stepped with the wait.
            //
            // D220's two are 0 outside a contact-eating world, where remains are neither founded nor closed, so the
            // count there is the one it always was. A founding or a close is a body to build or to remove; a whole
            // body that dies into remains is a death and is counted above.
            long revision = World.Births + World.Deaths + World.FloorSpawns + World.TrickleSpawns +
                            World.Inoculated + World.Immigrated + World.RemainsFounded + World.RemainsClosed;
            if (revision == _reconciledAt) return;

            _reconciledAt = revision;

            // A body has been built or destroyed, so the next divergence check is owed one. Set
            // before the work rather than after it, so a throw inside Build cannot leave the debt
            // unrecorded.
            _movedOutsideTheSolver = true;

            System.Collections.Generic.IReadOnlyList<Organism> living = World.Living;
            System.Collections.Generic.IReadOnlyList<Organism> remains = World.Remains;

            _departed.Clear();
            foreach (System.Collections.Generic.KeyValuePair<long, Body> entry in _bodies)
            {
                _departed.Add(entry.Key);
            }

            for (int i = 0; i < living.Count; i++)
            {
                Organism creature = living[i];
                _departed.Remove(creature.Id);

                if (_bodies.ContainsKey(creature.Id)) continue;

                Build(creature);
            }

            // D220: a body leaves the solver only when its id is in neither list. One that has passed from the
            // living to the remains keeps its solver body, which carries on from its pose and its velocity and is
            // told it is remains; one the solver has never held (a restore's, or a founding with no body to pose it
            // from) is built as a newborn is.
            for (int i = 0; i < remains.Count; i++)
            {
                Organism dead = remains[i];
                _departed.Remove(dead.Id);

                if (_bodies.TryGetValue(dead.Id, out Body held))
                {
                    held.Solver.BecomeRemains();
                    continue;
                }

                Build(dead);
            }

            foreach (long id in _departed)
            {
                Body body = _bodies[id];

                // Last chance: the body is about to leave the world, and the binds it made since
                // the last metabolic step would go with it.
                DriveImpulsesLimited += body.Solver.DrainDriveImpulsesLimited();
                DragImpulsesLimited += body.Solver.DrainDragImpulsesLimited();

                // And its dissipation, for the same reason and into the same diagnostic.
                DissipatedJoules += body.Solver.DrainDissipated();

                // D127: the pose it died in, for its corpse's picture. A body the solver lost is
                // not finite and gives none. Remains leave no corpse, so they are not asked.
                QuatD q = body.Solver.BaseRotation;
                if (!body.Creature.IsRemains &&
                    double.IsFinite(q.X) && double.IsFinite(q.Y) && double.IsFinite(q.Z) && double.IsFinite(q.W))
                {
                    _corpsePoses[id] = new Quat((float)q.X, (float)q.Y, (float)q.Z, (float)q.W);
                }

                Dynamics.Remove(id);
                _bodies.Remove(id);
            }

            if (_corpsePoses.Count > 0) StampCorpsePoses();

            // Rebuilt whether or not anything departed, because Build appends to this and a birth
            // alone leaves it correct but a death leaves it holding a body that has gone. The
            // early return above is what keeps this off the hot path.
            _order.Clear();

            if (remains.Count == 0)
            {
                for (int i = 0; i < living.Count; i++)
                {
                    if (_bodies.TryGetValue(living[i].Id, out Body body)) _order.Add(body);
                }

                return;
            }

            // D220: the solver's order is ascending id over the living and the remains together, so it is still a
            // function of the population. Both lists are in ascending id order; this is their merge.
            int l = 0, r = 0;
            while (l < living.Count || r < remains.Count)
            {
                bool takeLiving = r >= remains.Count || (l < living.Count && living[l].Id < remains[r].Id);
                Organism next = takeLiving ? living[l++] : remains[r++];

                if (_bodies.TryGetValue(next.Id, out Body ordered)) _order.Add(ordered);
            }
        }

        /// <summary>
        /// D127: hands each corpse founded since the last reconcile the pose its body died in. A
        /// corpse is appended at its death, so the walk runs from the newest and stops when every
        /// departed body with a pose has found its corpse; a body that left no corpse (a world without
        /// corpses, or one that died with nothing to give) is simply not found. A record: nothing
        /// in the world reads the pose.
        /// </summary>
        private void StampCorpsePoses()
        {
            System.Collections.Generic.IReadOnlyList<Corpse> corpses = World.Corpses;

            for (int i = corpses.Count - 1; i >= 0 && _corpsePoses.Count > 0; i--)
            {
                Corpse corpse = corpses[i];
                if (corpse.HasPose) continue;
                if (!_corpsePoses.TryGetValue(corpse.CreatureId, out Quat pose)) continue;

                corpse.SetPose(pose);
                _corpsePoses.Remove(corpse.CreatureId);
            }

            _corpsePoses.Clear();
        }

        /// <summary>Builds one newborn's body at the spot the placer reserved for it.</summary>
        /// <param name="creature">The organism: a newborn, or remains the solver has no body for.</param>
        /// <param name="poseSource">
        /// D220: for remains cut off another body, the solver body they were cut from, as it stands. The new body is
        /// then posed from its links instead of placed. Null for every other body.
        /// </param>
        /// <param name="poseLinks">For each link of the new body, the link of <paramref name="poseSource"/> it is.</param>
        private void Build(Organism creature, Creature poseSource = null, int[] poseLinks = null)
        {
            // The trickle question's instrument (Metabolise.cs): the wait ends here.
            _bodilessSince.Remove(creature.Id);

            Vec3 origin;
            float radius = 0f;

            if (poseSource != null)
            {
                // Posed from the body it was cut from: no place was reserved for it and none is wanted.
                origin = new Vec3(creature.X, creature.HeightY, creature.Z);
                if (Volume != null) radius = SharedVolume.BoundingRadius(creature.Phenotype);
            }
            else if (Volume != null)
            {
                radius = SharedVolume.BoundingRadius(creature.Phenotype);

                if (Volume.TryTakePlacement(creature.Id, out Float3 at))
                {
                    origin = new Vec3(at.X, at.Y, at.Z);
                }
                else
                {
                    // A body nobody reserved a spot for. Within one shared-space run this cannot
                    // happen — every admission goes through the placer — so rather than invent a
                    // position, put it where the world thinks it is and say so out loud.
                    //
                    // A restore is the one case where it is expected and silent: every body it
                    // rebuilds was placed when it was born, possibly thousands of seconds ago,
                    // and the pose written over this one a moment later is the one the checkpoint
                    // recorded. See Simulation.State.cs.
                    origin = new Vec3(creature.X, creature.HeightY, creature.Z);

                    if (!_restoring && !creature.IsRemains)
                    {
                        FarmConsole.Error.WriteLine(
                            FormattableString.Invariant(
                                $"warning: creature {creature.Id} was admitted into a shared volume ") +
                            "with no reserved placement — built at the height the world admitted it " +
                            "at, which is an overlapping spawn and therefore a force in the physics.");
                    }
                }
            }
            else
            {
                // The tiled world's lattice. No round has run this engine tiled and none will —
                // D077's box is what every recorded round since has used — but a config with
                // SharedSpace off is still a config, and a body at the origin on top of every
                // other body is not an answer.
                origin = new Vec3(0d, creature.HeightY, 0d);
            }

            // The id is the organism's, so the solver's list order is the world's own and the
            // contact instrument's held pairs are keyed on an animal rather than on a slot.
            // Refused rather than truncated past int: an id that wrapped would name two animals
            // and the held-pair test would silently be about the wrong one. The longest run on
            // file carries nine thousand births, so this is a guard and not a limit.
            if (creature.Id > int.MaxValue)
            {
                throw new InvalidOperationException(
                    FormattableString.Invariant(
                        $"Creature id {creature.Id} is past int.MaxValue, which the solver's own ") +
                    "id is. An id names one animal for as long as it lives and the contact " +
                    "instrument is keyed on it, so a truncated id is two animals wearing one name.");
            }

            var solver = new Creature((int)creature.Id, creature.Phenotype, Solver, Config.Shapes);

            if (poseSource != null) solver.PoseFromLinksOf(poseSource, poseLinks);
            else solver.PlaceAsDeveloped(origin, creature.Phenotype);

            // D066, and before the first step: so the water this body is asked for is the water
            // of the patch it was born into rather than patch 0's.
            solver.Patch = creature.Patch;

            // fable-propose-growth.md rule 8. The body above was built from creature.Phenotype,
            // which Core has already scaled to this fraction, so the body and the ledger start in
            // agreement and the first resize is the first growth step that changes something.
            // Left at 1, a newborn would be resized on every growth step for the whole of its
            // life and the resize count would be the population.
            solver.AppliedBodyFraction = creature.BodyFraction;

            // D106 item 2, rule 7, and the same argument one field along: a newborn is built at
            // whatever plan revision its organism already carries, so the growth step's
            // comparison is false until the module rule moves it. That is 0 for most births, and
            // 1 or more for a newborn the module rule reached in the growth step of the metabolic
            // step it was conceived on, before it had a body. Such a body is built on the plan it
            // has now, so the map from the plan before is nothing to carry. It is taken and
            // dropped, because Core composes a pending map with the next change's and a stale
            // one would be composed into it.
            solver.AppliedPlanRevision = creature.PlanRevision;
            creature.TakePartMapFromPreviousPlan();

            // §4.4's two world channels, wired here because this is the one place a body and its
            // organism are both in hand. Reads of World state from inside the parallel region,
            // and both of the reads are of state nothing writes during a physics step — the field
            // is written by World.Step and the deposit passes inside it, the reserve by the same
            // step, and both run serially between physics steps. CreatureSenses' remarks carry
            // the argument; the verification is that GridField.EdibleDensityAt is a pure read of
            // the stock array through a cell index, touching no cache and no frozen flag.
            solver.Senses.Nutrients = World.Nutrients;
            solver.Senses.Scent = World.Scent;
            solver.Senses.LeafScent = World.LeafScent;
            solver.Senses.Reserve = creature;

            // The throw trace's ring — jointed bodies only, which is the farm's rule since
            // 2026-09-20: writing it costs a fifth of the harness and every throw it was built
            // for was jointed.
            if (solver.Jointed) solver.EnableTrace();

            var body = new Body
            {
                Solver = solver,
                Creature = creature,
                Radius = radius,

                // A newborn has never been read by the divergence check.
                Touched = true,

                // narrow: the root, in Core's frame.
                LastRootPosition = new Float3(
                    (float)solver.Position[0], (float)solver.Position[1], (float)solver.Position[2]),
            };

            body.PreviousCentre = CentreOfMass(solver);
            body.PreviousRoot = body.LastRootPosition;

            // D220: remains are not counted in the mass-ratio instrument, which is about the living bodies that
            // were built. A body built for remains is told what it is before it is ever stepped.
            if (creature.IsRemains) solver.BecomeRemains();
            else NoteMassRatio(body);

            // The invariant that cannot fail quietly: Brain indexes drive by walking every part
            // in order and the solver indexes it through the joint's own DOF offset, and the two
            // agree only because development forces the root's joint to Fixed.
            if (solver.Brain.TotalDof != solver.Dof)
            {
                throw new InvalidOperationException(
                    FormattableString.Invariant(
                        $"Creature {creature.Id}: the brain produces {solver.Brain.TotalDof} ") +
                    FormattableString.Invariant(
                        $"drive values and the body has {solver.Dof} degrees of freedom. ") +
                    "The two DOF orderings have diverged and every joint would be driven by the " +
                    "wrong neuron.");
            }

            _bodies.Add(creature.Id, body);
            Dynamics.AddInIdOrder(solver);
            _order.Add(body);
        }

        /// <summary>
        /// Rebuilds every living body whose plan moved inside the world's step: a part bitten off
        /// by the mouth (D106 item 1). Called between <c>World.Step</c> and the next physics step.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Round 49, and why the rebuild used to wait.</b> The rebuild was written for the
        /// module gene (round 44), whose rule runs at the growth step, so the growth step was
        /// where a changed plan was rebuilt, and a module's rebuild never waited. The mouth
        /// (round 45) changes a plan inside <c>World.Step</c>, on any metabolic step, and nothing
        /// moved the rebuild with it. So a bitten body kept its old solver for up to
        /// <see cref="RunConfig.GrowthStepSeconds"/>. Nothing needed the wait: the solver
        /// rebuilds a body between any two physics steps, as it builds a newborn, and the
        /// module rule's rebuild at the growth step is the same call.
        /// </para>
        /// <para>
        /// <b>What the wait cost.</b> The contact list is built from the solver's links and Core
        /// reads each index as a part of the organism's plan, so a bite in the window landed on
        /// whichever part now held the index and was priced against that part's pool. The two
        /// senses were the new plan's arrays read by the old plan's links. And a checkpoint
        /// taken in the window could not be restored, so the run loop deferred it to the growth
        /// step.
        /// </para>
        /// <para>
        /// <b>A world with no bite never enters the loop's branch</b>: nothing inside
        /// <c>World.Step</c> changes a plan but a kill, so this is one walk of the living and one
        /// comparison a body, and the trajectory is the one the tree before it stepped.
        /// </para>
        /// </remarks>
        internal void RebuildChangedPlans()
        {
            System.Collections.Generic.IReadOnlyList<Organism> living = World.Living;

            for (int i = 0; i < living.Count; i++)
            {
                Organism creature = living[i];

                // A newborn has no body yet and is built on its plan by the next Reconcile; a body
                // that died in the step is not among the living.
                if (!_bodies.TryGetValue(creature.Id, out Body body)) continue;
                if (creature.PlanRevision == body.Solver.AppliedPlanRevision) continue;

                RebuildOnTheNewPlan(body, creature);
            }

            // D220: remains that shed a part are rebuilt on the plan they have now by the same path, keeping what
            // their surviving links were doing. The list is empty with the dial off.
            IReadOnlyList<Organism> remains = World.Remains;

            for (int i = 0; i < remains.Count; i++)
            {
                Organism dead = remains[i];

                if (!_bodies.TryGetValue(dead.Id, out Body body)) continue;
                if (dead.PlanRevision == body.Solver.AppliedPlanRevision) continue;

                RebuildOnTheNewPlan(body, dead);
            }
        }

        /// <summary>The solver bodies and links a founding of remains is posed from, while a drain is being worked.</summary>
        private sealed class FoundingSource
        {
            /// <summary>The solver body the remains were (in the end) cut from, as it stood at the cut.</summary>
            public Creature Solver;

            /// <summary>For each part of the remains' plan as founded, the link of <see cref="Solver"/> it is, or -1.</summary>
            public int[] Links;
        }

        private readonly Dictionary<long, FoundingSource> _foundingSources = new Dictionary<long, FoundingSource>();

        /// <summary>
        /// D220: builds a solver body for each remains Core founded since the last call, posed from the links of the
        /// body it was cut from.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Before anything rebuilds the source.</b> The pose is read off the source's solver body as it stands, so
        /// this runs straight after <c>World.Step</c> (and after the module rule at the growth step), ahead of
        /// <see cref="RebuildChangedPlans"/> and <see cref="RebuildOnTheNewPlan"/>.
        /// </para>
        /// <para>
        /// <b>Which link is which.</b> A founding names each of the new body's parts by its index in the plan the
        /// cut started from. That plan is the source's solver plan only when nothing changed the source earlier in
        /// the step; <see cref="RemainsFounding.SourcePrior"/> carries the source's pending map at the cut, which
        /// takes the index back to the solver plan. A source that is itself remains founded in this drain has no
        /// solver body of its own yet, so its link table (the plan it was founded on, mapped to the first source's
        /// links) stands in for one, and the chain is followed to the body that has. A body is then built on the
        /// plan its organism holds now, which can be a later shed's, through the organism's own pending map.
        /// </para>
        /// <para>
        /// A founding with no solver body to pose it from, or no root link, is left for <see cref="Reconcile"/> to
        /// build as it builds any body it finds without one.
        /// </para>
        /// </remarks>
        internal void PoseFoundings()
        {
            IReadOnlyList<RemainsFounding> foundings = World.DrainRemainsFoundings();
            if (foundings.Count == 0) return;

            _foundingSources.Clear();

            for (int i = 0; i < foundings.Count; i++)
            {
                RemainsFounding founding = foundings[i];

                Creature solver;
                int[] through = null;

                if (_foundingSources.TryGetValue(founding.FromBodyId, out FoundingSource chain))
                {
                    solver = chain.Solver;
                    through = chain.Links;
                }
                else if (_bodies.TryGetValue(founding.FromBodyId, out Body from))
                {
                    solver = from.Solver;
                }
                else
                {
                    continue;
                }

                int[] prior = founding.SourcePrior;
                var links = new int[founding.Map.Length];

                for (int k = 0; k < links.Length; k++)
                {
                    int at = founding.Map[k];
                    if (prior != null) at = at >= 0 && at < prior.Length ? prior[at] : -1;
                    if (through != null) at = at >= 0 && at < through.Length ? through[at] : -1;

                    links[k] = at;
                }

                _foundingSources[founding.RemainsId] = new FoundingSource { Solver = solver, Links = links };
            }

            for (int i = 0; i < foundings.Count; i++)
            {
                long id = foundings[i].RemainsId;

                if (!_foundingSources.TryGetValue(id, out FoundingSource source)) continue;
                if (_bodies.ContainsKey(id)) continue;

                Organism remains = FindRemains(id);
                if (remains == null) continue;

                // The organism's plan now, which a shed after the founding in the same step has moved: its pending
                // map runs from the plan it was founded on to the one it holds, and is taken here as Build would.
                int[] pending = remains.TakePartMapFromPreviousPlan();
                var posed = new int[remains.Phenotype.PartCount];

                for (int j = 0; j < posed.Length; j++)
                {
                    int founded = pending != null ? (j < pending.Length ? pending[j] : -1) : j;
                    posed[j] = founded >= 0 && founded < source.Links.Length ? source.Links[founded] : -1;
                }

                if (posed.Length == 0 || posed[0] < 0 || posed[0] >= source.Solver.Links) continue;

                Build(remains, source.Solver, posed);
            }

            _foundingSources.Clear();
        }

        /// <summary>The remains with this id, or null where none stands. The list is in ascending id order.</summary>
        private Organism FindRemains(long id)
        {
            IReadOnlyList<Organism> remains = World.Remains;
            int low = 0, high = remains.Count - 1;

            while (low <= high)
            {
                int middle = (low + high) >> 1;
                long at = remains[middle].Id;

                if (at == id) return remains[middle];
                if (at < id) low = middle + 1;
                else high = middle - 1;
            }

            return null;
        }

        /// <summary>
        /// Builds a living body again on the plan Core has just given it, keeping where it is and
        /// what it was doing — D106 item 2, rule 7.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>A new <see cref="Creature"/> and not a resize.</b> Every array on a body is indexed
        /// by link and sized at construction, and a module is a link that has just appeared or
        /// just gone; <c>Creature.Resize</c> says so itself, in as many words, by refusing a
        /// phenotype of a different part count. So this builds one as <see cref="Build"/> builds a
        /// newborn's and then hands it the old body's state through
        /// <c>Creature.AdoptStateFrom</c>, which is where the argument about what is carried
        /// lives.
        /// </para>
        /// <para>
        /// <b>The old body's tallies are drained before it goes</b>, exactly as a departing body's
        /// are in <see cref="Reconcile"/>: the limiter binds and the water's take belong to the
        /// run rather than to the object holding them. The mechanical work needs no draining —
        /// this runs inside the same metabolic step that drained it, after
        /// <c>World.Observe</c> and before the next physics step, so there is none standing.
        /// That holds at both callers, <see cref="RebuildChangedPlans"/> after the world's step
        /// and the growth step after the module rule.
        /// </para>
        /// </remarks>
        private void RebuildOnTheNewPlan(Body body, Organism creature)
        {
            Creature previous = body.Solver;

            DriveImpulsesLimited += previous.DrainDriveImpulsesLimited();
            DragImpulsesLimited += previous.DrainDragImpulsesLimited();
            DissipatedJoules += previous.DrainDissipated();

            Dynamics.Remove(creature.Id);

            var solver = new Creature((int)creature.Id, creature.Phenotype, Solver, Config.Shapes);

            // The developer's own match between the two plans. Core writes it whenever it moves
            // a plan, composed across every change since the harness last took it, so it runs
            // from the plan this solver was built on even when a pass took two parts off one
            // body. Taken and not just read: Core composes a pending map with the next change's,
            // so one left behind would be composed into the next. A body that arrives without
            // one is rebuilt with every joint at rest and says so, rather than being handed a
            // map that names the wrong parts.
            int[] map = creature.TakePartMapFromPreviousPlan();

            if (map == null || map.Length < solver.Links)
            {
                FarmConsole.Error.WriteLine(
                    FormattableString.Invariant(
                        $"warning: creature {creature.Id} changed plan with no part map, so its ") +
                    "joints and its brain start empty — the body is still where it was.");

                map = new int[solver.Links];
                for (int i = 0; i < map.Length; i++) map[i] = -1;
            }

            solver.AdoptStateFrom(previous, map);

            solver.Patch = creature.Patch;
            solver.AppliedBodyFraction = creature.BodyFraction;
            solver.AppliedPlanRevision = creature.PlanRevision;

            solver.Senses.Nutrients = World.Nutrients;
            solver.Senses.Scent = World.Scent;
            solver.Senses.LeafScent = World.LeafScent;
            solver.Senses.Reserve = creature;

            // D123 through a plan change (round 49). Core has carried the step's contact and
            // damage records onto the new plan's indices, so the rebuilt body reads, on every
            // physics step until the next metabolic step, what its surviving parts felt on the
            // step that changed its plan: the bite that took a part, or the step before a module
            // came or went. Handed here and not left to the next hand-back, because the growth
            // step's rebuild comes after this step's hand-back and the body would otherwise sense
            // nothing for the rest of the interval. Under the hand-back's own condition, so a
            // world with both senses shut wires nothing, as before.
            if (Config.SenseContact || Config.SenseDamage)
            {
                solver.Senses.Contact = creature.PartContact;
                solver.Senses.Damage = creature.PartDamage;
            }

            if (solver.Jointed) solver.EnableTrace();

            // D220: remains stay remains through a rebuild on a new plan.
            if (creature.IsRemains) solver.BecomeRemains();

            if (solver.Brain.TotalDof != solver.Dof)
            {
                throw new InvalidOperationException(
                    FormattableString.Invariant(
                        $"Creature {creature.Id}: the brain produces {solver.Brain.TotalDof} ") +
                    FormattableString.Invariant(
                        $"drive values and the rebuilt body has {solver.Dof} degrees of ") +
                    "freedom. The two DOF orderings have diverged across a module change and " +
                    "every joint would be driven by the wrong neuron.");
            }

            body.Solver = solver;

            // narrow: the root, in Core's frame. Taken here rather than left at the old body's,
            // because the next thing to read it is the placer and the position row.
            body.LastRootPosition = new Float3(
                (float)solver.Position[0], (float)solver.Position[1], (float)solver.Position[2]);

            // The same three the resize path refreshes: the masses have all been rewritten, the
            // jump check wants the next frame, and the placer's picture of how much room this
            // body needs has changed with its plan.
            NoteMassRatio(body);
            body.ResizedLastStep = true;

            if (Volume != null)
            {
                body.Radius = SharedVolume.BoundingRadius(creature.Phenotype);
            }

            Dynamics.AddInIdOrder(solver);

            // `_order` holds the Body and not the solver, so it needs nothing; what has changed
            // outside the solver is a body's whole state, which is the debt the next divergence
            // check is owed.
            _movedOutsideTheSolver = true;
            body.Touched = true;

            ModuleRebuilds++;
        }

        /// <summary>Centre of mass of a body, narrowed into Core's frame.</summary>
        /// <remarks>
        /// narrow: the centre is what <see cref="World.Observe(Organism, Float3, float)"/> takes,
        /// and it is also what the speed instrument differences — so it is narrowed once, here,
        /// and the difference is taken in Core's own precision rather than in two.
        /// </remarks>
        private static Float3 CentreOfMass(Creature body)
        {
            Vec3 centre = body.CentreOfMass();
            return new Float3((float)centre.X, (float)centre.Y, (float)centre.Z);
        }
    }
}
