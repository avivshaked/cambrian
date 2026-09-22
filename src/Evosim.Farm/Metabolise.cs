using System.Collections.Generic;
using Evosim.Core;
using Evosim.Dynamics;

namespace Evosim.Farm
{
    /// <summary>
    /// The economy's step — <c>Ecosystem.Metabolise</c> and <c>Ecosystem.ApplyGrowth</c>, out of
    /// Unity.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Serial, and in <c>World.Living</c> order.</b> Everything here reads or writes Core's
    /// world, which is not thread-safe and is not meant to be; the parallel phase is the physics
    /// step and ends before this begins. The order is the world's own rather than the solver's,
    /// which is the farm's order and is the order every accumulator below is summed in.
    /// </para>
    /// <para>
    /// <b>The divergence check runs first</b>, before anything reads what the solver has been
    /// producing. A body whose state has stopped being finite, or whose root has left the sea, is
    /// removed here as a counted death; if it were left, <c>World.Observe</c> would see the
    /// height and take the run down — which is how <c>r20q-s1</c> was censored at t=15,345 of
    /// 20,000 s. The refusal inside <c>Observe</c> stays exactly as it is: it is the guard for
    /// anything that gets past this, and this runs first.
    /// </para>
    /// </remarks>
    public sealed partial class Simulation
    {
        private void Metabolise()
        {
            long checkStarted = Now();

            CheckFinite();

            // `finite`'s second bracket of the step, and the reason the phase is the one with two.
            long metaboliseStarted = NotePhase(PhaseFinite, checkStarted);
            long nestedAtEntry = _worldTicks + _phaseTicks[PhaseGrowth];

            float seconds = StepsPerMetabolicStep * PhysicsDt;

            double speedSum = 0d;
            double fastest = 0d;
            double work = 0d;
            int counted = 0;
            int above = 0;

            // D077. A fresh census of what is where, before the world steps and therefore before
            // anything reads a patch or conceives into a gap. Emptied and refilled rather than
            // maintained incrementally: every body has moved since the last one.
            Volume?.Begin();

            IReadOnlyList<Organism> living = World.Living;

            for (int i = 0; i < living.Count; i++)
            {
                Organism creature = living[i];
                if (!_bodies.TryGetValue(creature.Id, out Body body)) continue;

                Creature solver = body.Solver;

                // The root, as the divergence check left it a few lines ago — free, and the
                // position D077's rules are all denominated in.
                Float3 root = body.LastRootPosition;
                if (root.Y > 0f) above++;

                Volume?.Note(creature.Id, root, body.Radius);

                Float3 centre = CentreOfMass(solver);

                // Unsigned, and drained per interval. The solver reports the magnitude of the
                // work at each joint precisely because a joint being driven *by* the water is
                // doing negative work at the actuator, and crediting that would pay a creature to
                // be pushed around — §11.2's free-energy failure through the ledger.
                double interval = solver.DrainMechanicalWork();

                // The two limiters' tallies, drained on the pass that already has the body in
                // hand so the same bind cannot be counted twice.
                DriveImpulsesLimited += solver.DrainDriveImpulsesLimited();
                DragImpulsesLimited += solver.DrainDragImpulsesLimited();

                // The water's own take, where FluidEnvironment.Settle's accumulator stood. A
                // diagnostic in the farm and a diagnostic here — see Simulation.DissipatedJoules.
                DissipatedJoules += solver.DrainDissipated();

                // D083: the whole centre, so a grid or a vertex field feeds the body where it is.
                // narrow: interval is the solver's double and Observe takes a float.
                World.Observe(creature, centre, (float)interval);

                if (body.Settled)
                {
                    // D077. On a ring, across the shorter arc: a body translated at a seam has
                    // moved a patch-ring's width in one metabolic step, and differencing its raw
                    // coordinates would report that as a swim.
                    double speed = Distance(centre, body.PreviousCentre) / seconds;
                    speedSum += speed;
                    counted++;
                    if (speed > fastest) fastest = speed;

                    double rootSpeed = Distance(root, body.PreviousRoot) / seconds;

                    if (solver.Jointed)
                    {
                        _jointedSpeedSum += rootSpeed;
                        _jointedSpeedSamples++;
                    }
                    else
                    {
                        _rigidSpeedSum += rootSpeed;
                        _rigidSpeedSamples++;
                    }
                }

                // The jump check's second reading, taken on the one step that follows a resize.
                if (body.ResizedLastStep)
                {
                    body.ResizedLastStep = false;

                    if (body.Settled)
                    {
                        double moved = Distance(root, body.PreviousRoot);
                        if (moved > MaxResizeStepMetres) MaxResizeStepMetres = moved;
                    }
                }

                body.Settled = true;
                body.PreviousCentre = centre;
                body.PreviousRoot = root;
                work += interval;
            }

            MeanSpeed = counted > 0 ? speedSum / counted : 0d;
            MaxSpeed = fastest;
            WorkThisStep = work;
            AboveSurface = above;

            long worldStarted = Now();
            World.Step(seconds);
            _worldTicks += Now() - worldStarted;

            // D066. After the world has stepped, because that is where a creature's patch changes
            // — D061's dispersal and D066's advection both move it, and the physics steps that
            // follow have to sample the water the creature is actually in.
            if (Config.HorizontalPatches > 1f)
            {
                IReadOnlyList<Organism> after = World.Living;

                for (int i = 0; i < after.Count; i++)
                {
                    if (_bodies.TryGetValue(after[i].Id, out Body body))
                    {
                        body.Solver.Patch = after[i].Patch;
                    }
                }
            }

            // fable-propose-growth.md rule 8. After the world has stepped, because that is where
            // a body's size changes: Core moved the joules and scaled the phenotype a few lines
            // ago, and until this runs the creature has drag and lit area from its new size and
            // mass and anchors from its old one. Counted in simulated seconds, never against a
            // wall clock.
            _sinceGrowthStep += seconds;

            float growthStep = Config.GrowthStepSeconds;
            if (_sinceGrowthStep + GrowthStepEpsilon >= growthStep)
            {
                // What actually elapsed, not what was asked for: D106's drop clock counts
                // continuous seconds under a line, and a clock fed the nominal cadence would
                // drift from the world's own second the moment the two differed.
                float sinceLastGrowthStep = _sinceGrowthStep;
                _sinceGrowthStep = 0f;

                long growthStarted = Now();
                ApplyGrowth(sinceLastGrowthStep);
                _phaseTicks[PhaseGrowth] += Now() - growthStarted;
            }

            _phaseTicks[PhaseMetabolise] += Now() - metaboliseStarted -
                                            (_worldTicks + _phaseTicks[PhaseGrowth] - nestedAtEntry);
        }

        /// <summary>
        /// Puts every body that has grown since the last growth step at the size Core says it is.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Walks the living rather than the body table.</b> Deaths have happened inside
        /// <c>World.Step</c> and <c>Reconcile</c> has not run yet, so the table still holds bodies
        /// whose creature is dead and about to be removed; resizing those would be work done on a
        /// corpse. Newborns are the other way round: they have no body until Reconcile builds
        /// one, and it is built from the already-scaled phenotype.
        /// </para>
        /// <para>
        /// <b>The jump is zero by construction here and was merely small in the farm.</b> PhysX
        /// re-derives every link from anchors that have moved and the root can be dragged with
        /// them; this holds <c>BasePosition</c> and walks the new anchors outward from it, so
        /// <c>resizeJumpMetres</c> is exact and reads 0. It is still measured, because an
        /// instrument that is asserted rather than read is an instrument nobody would notice
        /// breaking.
        /// </para>
        /// </remarks>
        private void ApplyGrowth(float sinceLastGrowthStep)
        {
            // D106 item 2's rule, run before the resizes and in the same step, because a body
            // that has just gained or lost a module has a new plan as well as a new size: the
            // loop below would otherwise resize it to a phenotype of a different part count,
            // which Creature.Resize refuses by design. It is one comparison per growth step at
            // the module tunables' defaults, which is every world in the record.
            World.ApplyModuleRule(sinceLastGrowthStep);

            IReadOnlyList<Organism> living = World.Living;

            for (int i = 0; i < living.Count; i++)
            {
                Organism creature = living[i];
                if (!_bodies.TryGetValue(creature.Id, out Body body)) continue;

                // Rule 7. A changed plan is rebuilt rather than resized, and the revision rather
                // than the part count is what says so: a re-development can move a body's shape
                // without moving its count, and a resize of that body would write one part's size
                // onto another.
                if (creature.PlanRevision != body.Solver.AppliedPlanRevision)
                {
                    RebuildOnTheNewPlan(body, creature);
                    continue;
                }

                if (creature.BodyFraction == body.Solver.AppliedBodyFraction) continue;

                Creature solver = body.Solver;
                Vec3 before = Vec3.Read(solver.Position, 0);

                solver.Resize(creature.Phenotype, creature.BodyFraction);

                double jump = (Vec3.Read(solver.Position, 0) - before).Magnitude;
                if (jump > MaxResizeJumpMetres) MaxResizeJumpMetres = jump;

                body.ResizedLastStep = true;

                // The masses have all been rewritten, so the ratio is re-read; and the next frame
                // the ring records is the first one taken with the new anchors in place, which is
                // the frame a post-mortem asks about.
                NoteMassRatio(body);
                solver.MarkResized(Steps);

                // The placer's picture of how much room this body needs, refreshed with the body.
                if (Volume != null)
                {
                    body.Radius = Evosim.Dynamics.Placement.SharedVolume.BoundingRadius(
                        creature.Phenotype);
                }

                Resizes++;
                _movedOutsideTheSolver = true;
            }
        }

        /// <summary>
        /// How far apart two places are: across the shorter arc in a periodic box, plainly in a
        /// tank or a tiled world.
        /// </summary>
        /// <remarks>
        /// The farm's branch, kept as a branch: the tiled world's arithmetic is the
        /// character-for-character expression every run on file was measured with.
        /// </remarks>
        private double Distance(Float3 a, Float3 b)
        {
            if (Volume != null) return Volume.ShortestDistance(a, b);

            double dx = (double)a.X - b.X;
            double dy = (double)a.Y - b.Y;
            double dz = (double)a.Z - b.Z;

            return System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}
