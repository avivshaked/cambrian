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
            bool byExposure = Config.LightByExposure;

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

                // D110. The pose the world prices this body's light on, read on the pass that
                // already holds it. Skipped entirely with the tunable off, so the array is never
                // allocated and the world takes the orientation average it always has.
                if (byExposure)
                {
                    // wallExposureMs' bracket, inside `metabolise` and not a phase of its own, so
                    // the harness split's sum is unchanged.
                    long exposureStarted = Now();
                    Exposure(solver, creature);
                    _exposureTicks += Now() - exposureStarted;
                }

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

            // D106 item 5. The step's overlapping pairs, each resolved to the nearest pair of
            // parts, handed to Core immediately before the step that reads them — the list names
            // parts by their index in a body's phenotype, and Core's own mouth pass can change a
            // body's plan, so a list that outlived one step would name the wrong parts.
            HandOverContacts();

            long worldStarted = Now();
            World.Step(seconds);
            _worldTicks += Now() - worldStarted;

            // Round 49. A body the mouth took a part off inside World.Step is rebuilt on its new
            // plan here, before any physics step reads it, as a newborn is built before its first.
            // Until round 49 it waited for the growth step below, and for up to ten seconds the
            // contact list, the two senses and a checkpoint all read one plan's indices against
            // the other's. Timed with the growth step's rebuilds, which are the same work.
            long rebuildStarted = Now();
            RebuildChangedPlans();
            _phaseTicks[PhaseGrowth] += Now() - rebuildStarted;

            // And the other direction, once the world has settled what was hurt: what each body
            // felt goes onto its own senses, for the physics steps that follow to read. After
            // World.Step because the mouth's damage pass is inside it, and after the rebuild just
            // above, so that a rebuilt body is handed its organism's arrays like any other.
            HandBackWhatWasFelt();

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
        /// Writes each part's exposure factor and the world's up in the body's frame onto the
        /// creature, from the links' rotations as the physics left them — D110,
        /// <c>logbook/specs/light-exposure-spec.md</c> §4.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Row 1 of a link's matrix is the whole of what is read.</b> The matrix takes the
        /// link's own axes to the world's, so its second row is the world's up expressed on each
        /// of the link's three axes — the three dot products the factor needs — and a link's
        /// frame is its part's (<c>Creature</c>'s remarks), so link <i>i</i> is part <i>i</i>.
        /// </para>
        /// <para>
        /// <b>Only while the solver holds the creature's plan.</b> Link <i>i</i> is part <i>i</i>
        /// only on the plan the solver was built on. Since round 49 the harness rebuilds a changed
        /// plan on the metabolic step that changed it, so the two always agree here; the test
        /// stays as the guard, and on a disagreement the array is dropped and the body earns and
        /// shades on the orientation average, both sides together.
        /// </para>
        /// </remarks>
        private static void Exposure(Creature solver, Organism creature)
        {
            Phenotype phenotype = creature.Phenotype;
            int links = solver.Links;

            if (links != phenotype.PartCount ||
                creature.PlanRevision != solver.AppliedPlanRevision)
            {
                creature.PartExposure = null;
                return;
            }

            float[] exposure = creature.PartExposure;
            if (exposure == null || exposure.Length != links)
            {
                exposure = new float[links];
                creature.PartExposure = exposure;
            }

            double[] m = solver.RotationMatrix;
            for (int i = 0; i < links; i++)
            {
                int at = 9 * i + 3;
                exposure[i] = phenotype.Parts[i].ExposureFactor(m[at], m[at + 1], m[at + 2]);
            }

            // The root link's axes are the root part's, which the developer may have turned in
            // the body's own frame; the hull is in that frame, so up is carried the last step.
            var upOnRoot = new Float3((float)m[3], (float)m[4], (float)m[5]);
            creature.UpInBody = phenotype.Parts[0].Rotation.Rotate(upOnRoot);
        }

        /// <summary>
        /// Turns the solver's overlapping pairs into the contact list Core's mouth reads — D106
        /// item 5.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>One list, reused</b>, because this runs twice a simulated second for the life of a
        /// run and the list is empty again by the time anything else could look at it: Core holds
        /// it for exactly one <c>World.Step</c> and drops it.
        /// </para>
        /// <para>
        /// <b>The solver's ids are the organisms' ids</b> (<c>Reconcile.Build</c> makes them so),
        /// and a body's links are its phenotype's parts in the same order, so the translation is
        /// the identity in both directions and there is no map to keep in step.
        /// </para>
        /// <para>
        /// <b>That holds because every body is on its organism's plan when this runs</b>
        /// (<see cref="RebuildChangedPlans"/>). Until round 49 a body bitten inside the last
        /// world step kept its old solver until the growth step. The list then named the old
        /// plan's links, Core read them as the new plan's parts, and a bite could land on
        /// whichever part now held the index.
        /// </para>
        /// </remarks>
        private void HandOverContacts()
        {
            _contacts.Clear();

            System.Collections.Generic.IReadOnlyList<OverlapPair> overlaps = Dynamics.Overlaps;

            for (int i = 0; i < overlaps.Count; i++)
            {
                OverlapPair pair = overlaps[i];
                if (!Dynamics.NearestParts(pair, out int partA, out int partB)) continue;

                _contacts.Add(new CreatureContact(pair.A, partA, pair.B, partB));
            }

            World.SetContacts(_contacts);
        }

        /// <summary>
        /// Puts each body's contact and damage record onto its own senses — D106 item 5's two
        /// channels.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Skipped entirely unless the run has opened one of the two channels.</b> The arrays
        /// are Core's and are handed over by reference, so this is a pointer per body per
        /// metabolic step and nothing else — but a run that nothing can draw them in is a run in
        /// which no neuron will ever ask, and the whole of what the walk would buy is a field
        /// assignment nobody reads.
        /// </para>
        /// <para>
        /// <b>What the physics steps of the next interval read is this step's record</b> (D123,
        /// round 49): Core's mouth pass zeroes both arrays in place at its top and writes the
        /// step's contacts and losses into them, so the reference handed here stays the one the
        /// next pass writes. It is handed again every step all the same, because Core replaces an
        /// array on a body's first touch or wound, and replaces both on a plan change with arrays
        /// on the new plan's indices (which the rebuild above has already handed a rebuilt body).
        /// </para>
        /// </remarks>
        private void HandBackWhatWasFelt()
        {
            if (!Config.SenseContact && !Config.SenseDamage) return;

            IReadOnlyList<Organism> living = World.Living;

            for (int i = 0; i < living.Count; i++)
            {
                Organism creature = living[i];
                if (!_bodies.TryGetValue(creature.Id, out Body body)) continue;

                body.Solver.Senses.Contact = creature.PartContact;
                body.Solver.Senses.Damage = creature.PartDamage;
            }
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
                // onto another. A bite's change was rebuilt straight after World.Step, so what
                // reaches this branch is the module rule's, a few lines up.
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
