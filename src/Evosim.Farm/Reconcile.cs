using System;
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
            long revision = World.Births + World.Deaths + World.FloorSpawns + World.Inoculated;
            if (revision == _reconciledAt) return;

            _reconciledAt = revision;

            // A body has been built or destroyed, so the next divergence check is owed one. Set
            // before the work rather than after it, so a throw inside Build cannot leave the debt
            // unrecorded.
            _movedOutsideTheSolver = true;

            System.Collections.Generic.IReadOnlyList<Organism> living = World.Living;

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

            foreach (long id in _departed)
            {
                Body body = _bodies[id];

                // Last chance: the body is about to leave the world, and the binds it made since
                // the last metabolic step would go with it.
                DriveImpulsesLimited += body.Solver.DrainDriveImpulsesLimited();
                DragImpulsesLimited += body.Solver.DrainDragImpulsesLimited();

                // And its dissipation, for the same reason and into the same diagnostic.
                DissipatedJoules += body.Solver.DrainDissipated();

                Dynamics.Remove(id);
                _bodies.Remove(id);
            }

            // Rebuilt whether or not anything departed, because Build appends to this and a birth
            // alone leaves it correct but a death leaves it holding a body that has gone. The
            // early return above is what keeps this off the hot path.
            _order.Clear();

            for (int i = 0; i < living.Count; i++)
            {
                if (_bodies.TryGetValue(living[i].Id, out Body body)) _order.Add(body);
            }
        }

        /// <summary>Builds one newborn's body at the spot the placer reserved for it.</summary>
        private void Build(Organism creature)
        {
            Vec3 origin;
            float radius = 0f;

            if (Volume != null)
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

                    if (!_restoring)
                    {
                        Console.Error.WriteLine(
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

            solver.PlaceAsDeveloped(origin, creature.Phenotype);

            // D066, and before the first step: so the water this body is asked for is the water
            // of the patch it was born into rather than patch 0's.
            solver.Patch = creature.Patch;

            // fable-propose-growth.md rule 8. The body above was built from creature.Phenotype,
            // which Core has already scaled to this fraction, so the body and the ledger start in
            // agreement and the first resize is the first growth step that changes something.
            // Left at 1, a newborn would be resized on every growth step for the whole of its
            // life and the resize count would be the population.
            solver.AppliedBodyFraction = creature.BodyFraction;

            // §4.4's two world channels, wired here because this is the one place a body and its
            // organism are both in hand. Reads of World state from inside the parallel region,
            // and both of the reads are of state nothing writes during a physics step — the field
            // is written by World.Step and the deposit passes inside it, the reserve by the same
            // step, and both run serially between physics steps. CreatureSenses' remarks carry
            // the argument; the verification is that GridField.EdibleDensityAt is a pure read of
            // the stock array through a cell index, touching no cache and no frozen flag.
            solver.Senses.Nutrients = World.Nutrients;
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

                // narrow: the root, in Core's frame.
                LastRootPosition = new Float3(
                    (float)solver.Position[0], (float)solver.Position[1], (float)solver.Position[2]),
            };

            body.PreviousCentre = CentreOfMass(solver);
            body.PreviousRoot = body.LastRootPosition;

            NoteMassRatio(body);

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
