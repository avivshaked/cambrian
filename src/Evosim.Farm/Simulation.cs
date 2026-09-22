using System;
using System.Collections.Generic;
using Evosim.Core;
using Evosim.Dynamics;
using Evosim.Dynamics.Placement;

namespace Evosim.Farm
{
    /// <summary>
    /// The harness — <c>Evosim.Sim.Ecosystem</c> out of Unity: Core's <see cref="World"/> and a
    /// <see cref="DynamicsWorld"/> stepped together, one economy step every
    /// <see cref="StepsPerMetabolicStep"/> physics steps.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The order of the step is <c>Ecosystem.Step</c>'s, with one substitution and two
    /// consequences.</b> The substitution is that the control loop, the drag pass and the solve
    /// are one call — <see cref="DynamicsWorld.Step"/> — where the farm had three, because this
    /// engine steps a body's senses, brain, drive, fluid, contacts and integration on the body's
    /// own thread rather than in three passes over the population. The first consequence is that
    /// the throw trace and the state digest are written inside that call, which puts them
    /// <i>before</i> D077's seam wrap where the farm wrote them after it. The second is that
    /// there is no <c>read</c> phase: a link's pose is a number in the creature's own array and
    /// there is nothing to read it out of. Both are stated in the report rather than papered
    /// over; neither changes what is simulated, and neither can make the trajectory depend on the
    /// thread count.
    /// </para>
    /// <para>
    /// <b>The narrowing rule, in one sentence and one place.</b> This engine is double throughout
    /// and Core is float throughout, so a double is narrowed with a plain <c>(float)</c> cast at
    /// exactly the call that hands it to Core and nowhere else —
    /// <see cref="World.Observe(Organism, Float3, float)"/>'s centre and work,
    /// <see cref="IBodyPlacement"/>'s positions and radii, and the position rows. Nothing is
    /// rounded, nothing is clamped, and no double is narrowed and then widened again: a
    /// quantity is a double until Core owns it, and a float from then on. The casts are marked
    /// <c>// narrow</c> at each site.
    /// </para>
    /// </remarks>
    public sealed partial class Simulation : IDisposable
    {
        /// <summary>The economy's step — <c>Ecosystem.MetabolicStepSeconds</c>, not a tunable.</summary>
        public const float MetabolicStepSeconds = EnvBinding.MetabolicStepSeconds;

        /// <summary>The spatial hash's column, m — <c>Ecosystem.ColumnMetres</c>.</summary>
        public const float ColumnMetres = 1f;

        /// <summary>Half a physics step, s — <c>Ecosystem.GrowthStepEpsilon</c>.</summary>
        private const float GrowthStepEpsilon = 0.005f;

        /// <summary>The ratio at which a body is counted as top-heavy — <c>MassRatioThreshold</c>.</summary>
        private const double MassRatioThreshold = 10d;

        /// <summary>One living creature, as the harness books it — <c>Ecosystem.Body</c>.</summary>
        private sealed class Body
        {
            public Creature Solver;
            public Organism Creature;

            /// <summary>The root, as the last divergence check left it. Core's frame, float.</summary>
            public Float3 LastRootPosition;

            /// <summary>Centre of mass at the previous metabolic step — the speed's baseline.</summary>
            public Float3 PreviousCentre;

            /// <summary>The root at the previous metabolic step — the motility instrument's.</summary>
            public Float3 PreviousRoot;

            /// <summary>False for one metabolic step after a birth: nothing to difference yet.</summary>
            public bool Settled;

            public bool ResizedLastStep;

            /// <summary>What the placer reserved for it — <c>SharedVolume.BoundingRadius</c>.</summary>
            public float Radius;

            public bool CountedOverMassRatio;
        }

        private readonly Dictionary<long, Body> _bodies = new Dictionary<long, Body>();
        private readonly List<Body> _order = new List<Body>();
        private readonly HashSet<long> _departed = new HashSet<long>();
        private readonly List<Body> _condemned = new List<Body>();

        private long _reconciledAt = -1;
        private bool _movedOutsideTheSolver;
        private float _sinceGrowthStep;

        private bool[] _columnHeld;
        private bool[] _columnHeldAbsorptive;

        public World World { get; }
        public DynamicsWorld Dynamics { get; }
        public SolverConfig Solver { get; }

        /// <summary>D077's box, or null in a tiled world.</summary>
        public SharedVolume Volume { get; }

        /// <summary>The bed the placer reads, or null where there is no floor.</summary>
        public PlacementFloor Floor { get; }

        public DivergenceDumps Dumps { get; }

        public RunConfig Config => World.Config;
        public float PhysicsDt { get; }
        public int StepsPerMetabolicStep { get; }

        /// <summary>Physics steps taken.</summary>
        public long Steps { get; private set; }

        // ---- the per-sample readings Row and the footer take

        public double MeanSpeed { get; private set; }
        public double MaxSpeed { get; private set; }
        public double WorkThisStep { get; private set; }
        public int AboveSurface { get; private set; }
        public long DriveImpulsesLimited { get; private set; }
        public long DragImpulsesLimited { get; private set; }
        public long Resizes { get; private set; }
        public double MaxResizeJumpMetres { get; private set; }
        public double MaxResizeStepMetres { get; private set; }
        public double MaxJointMassRatio { get; private set; }
        public long BodiesOverMassRatio10 { get; private set; }

        /// <summary>
        /// What the water has taken off the population since the run began, joules.
        /// </summary>
        /// <remarks>
        /// <b>A diagnostic and nothing else, which is exactly what it was in the farm.</b>
        /// <c>Ecosystem.DissipatedJoules</c> forwards <c>FluidEnvironment.DissipatedJoules</c>
        /// and the only thing in the record that ever reads it is <c>Milestone1Smoke</c>'s energy
        /// balance: <c>World.AuditResidual</c> is <c>EnergyIn − EnergyOut − StandingJoules</c>
        /// and the water's share of a body's kinetic energy is in none of the three. So the drain
        /// is kept where the farm kept it — beside the work hand-off, per metabolic step, in
        /// creature order — and it is summed here rather than fed to the books, because feeding
        /// it to the books would be a change to the economy and not a port of one.
        /// </remarks>
        public double DissipatedJoules { get; private set; }

        public long Wraps => Volume?.Wraps ?? 0L;
        public long Crowded => World.CrowdedStillbirths;

        // The motility instrument's window — Ecosystem's four accumulators, drained at a sample.
        private double _jointedSpeedSum;
        private long _jointedSpeedSamples;
        private double _rigidSpeedSum;
        private long _rigidSpeedSamples;

        /// <summary>
        /// Builds the harness over a world that has already been constructed.
        /// </summary>
        /// <param name="world">
        /// Core's world. Constructed by the caller and not here, because the report header and the
        /// manifest describe the world the run will actually have — the patch width, the tank's
        /// radius and the four facts the bed measured of itself exist nowhere else — and a config
        /// the world refuses has to stop the launch before a run directory is made for it.
        /// </param>
        /// <param name="seed">The run's seed, which the placer draws its own stream off.</param>
        /// <param name="physicsDt">The physics step, already validated against the metabolic one.</param>
        /// <param name="stepsPerMetabolicStep">0.5 s over the physics step.</param>
        /// <param name="threads">How many threads step the bodies. Pace only, never a realisation.</param>
        /// <param name="runDirectory">The run's directory, for the divergence dumps and the digest.</param>
        public Simulation(
            World world, ulong seed, float physicsDt, int stepsPerMetabolicStep, int threads,
            string runDirectory)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            PhysicsDt = physicsDt;
            StepsPerMetabolicStep = stepsPerMetabolicStep;

            RunConfig config = world.Config;

            // The solver's own view of the world. Through FromWorld with the world in hand, which
            // is the reconciled factory: without the world the current is null and the round runs
            // in still water while every other token in the header says it does not. See
            // SolverConfig.FromWorld's remarks.
            Solver = SolverConfig.FromWorld(config, physicsDt, world.Bed, world);

            // The contact instrument, redefined on sphere overlaps (the proposal's change 4). On
            // for every run rather than only in a shared world: it costs a branch a body a step,
            // and a column that reads 0 because nobody switched an instrument on is the fault
            // CLAUDE.md's species column exists to warn about.
            Solver.ContactInstrument = true;
            Solver.ContactEvents = true;

            Dynamics = new DynamicsWorld(Solver) { Threads = threads < 1 ? 1 : threads };

            if (config.SharedSpace)
            {
                // The patch width from the fields themselves — sqrt(area / K) — and the shape and
                // radius the world derived, so the water a body feeds from and the glass it is
                // stopped by are one circle. Ecosystem's constructor, argument for argument.
                Volume = new SharedVolume(
                    Math.Max(1, (int)config.HorizontalPatches),
                    world.Nutrients.PatchWidthMetres,
                    config.WorldDepthMetres, seed, config.OffspringDispersalMetres,
                    Math.Max(1, (int)config.PatchesAcross),
                    config.WorldShape, world.TankRadiusMetres);

                World.Placement = Volume;

                // The bed before anything is placed: the placer asks it how much clearance a body
                // needs and the very first floor spawn has to get the answer. The shape is the
                // world's own (D092), never rebuilt here.
                Floor = new PlacementFloor(config.WorldDepthMetres, world.Bed);
                Volume.Floor = Floor;
            }

            if (!string.IsNullOrEmpty(runDirectory))
            {
                Dumps = new DivergenceDumps(
                    System.IO.Path.Combine(runDirectory, "diverged"));
            }
        }

        /// <summary>Turns the state digest on — <c>EVOSIM_DIGEST_EVERY</c>.</summary>
        public void EnableDigest(string runDirectory, long everySteps, IEnumerable<long> dumpSteps)
        {
            if (everySteps <= 0) return;
            Dynamics.EnableDigest(runDirectory, everySteps, dumpSteps);
        }

        /// <summary>
        /// Advances physics one step, and the economy once every
        /// <see cref="StepsPerMetabolicStep"/>. Returns true on the steps the economy ran.
        /// </summary>
        public bool Step()
        {
            long stepStarted = Now();
            long bucketedAtEntry = _physicsTicks + _worldTicks;
            long phaseStarted = stepStarted;

            Reconcile();

            phaseStarted = NotePhase(PhaseReconcile, phaseStarted);

            // The second bracket on the divergence check, and the reason it is here rather than
            // only at the top of Metabolise: World.Step decides births and deaths, ApplyGrowth
            // resizes a living body in place and Reconcile builds a newborn's, and the very next
            // thing to read a position is the sensor pass inside the solver's own step. So the
            // check runs again on exactly the steps where one of those three has happened; on
            // every other physics step it is one bool test. Reconcile follows it because the
            // check kills, and it returns at once when nothing died.
            if (_movedOutsideTheSolver)
            {
                phaseStarted = Now();

                _movedOutsideTheSolver = false;
                CheckFinite();
                Reconcile();

                _phaseTicks[PhaseFinite] += Now() - phaseStarted;
            }

            // The profile's denominator, taken where the work it measures is about to happen.
            _bodyStepSum += _order.Count;
            _linkStepSum += Dynamics.TotalLinks();

            long physicsStarted = Now();
            Dynamics.Step();
            _physicsTicks += Now() - physicsStarted;

            phaseStarted = Now();

            // D077, immediately after the solver, so nothing ever reads a position outside the
            // box. Nothing to do in a tank, where the boundary is a collider.
            if (Volume != null && Volume.Shape != WorldShape.Tank) WrapAtTheSeams();

            phaseStarted = NotePhase(PhaseSettle, phaseStarted);

            Steps++;

            if (Steps % StepsPerMetabolicStep != 0)
            {
                NoteHarnessWall(stepStarted, bucketedAtEntry);
                return false;
            }

            Metabolise();

            NoteHarnessWall(stepStarted, bucketedAtEntry);
            return true;
        }

        /// <summary>
        /// Puts every body that has left the box back in at the opposite face — D077's third rule.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The whole articulation by its root, and the velocities untouched, which is what a
        /// periodic boundary means. Here that is three lines rather than a teleport call: the
        /// root's place is <see cref="Creature.BasePosition"/>, every other link hangs off it
        /// through <see cref="Kinematics.Poses"/>, and a link's velocity is built from the root's
        /// and the joint rates against offsets a translation does not change — so the poses are
        /// re-derived and the motion is left exactly as the solver produced it.
        /// </para>
        /// <para>
        /// The contact sphere is refreshed and committed with it. It was committed at the foot of
        /// the solver's own step, before this ran, and a body other creatures read at the place
        /// it has just left is the one thing a periodic boundary must not produce.
        /// </para>
        /// </remarks>
        private void WrapAtTheSeams()
        {
            for (int i = 0; i < _order.Count; i++)
            {
                Creature body = _order[i].Solver;
                if (!body.Alive || body.Links == 0) continue;

                Vec3 root = Vec3.Read(body.Position, 0);

                // narrow: the placer is Core's, and speaks Core's Float3.
                var at = new Float3((float)root.X, (float)root.Y, (float)root.Z);

                if (!Volume.TryWrap(at, out Float3 wrapped)) continue;

                body.BasePosition += new Vec3(
                    (double)wrapped.X - at.X, (double)wrapped.Y - at.Y, (double)wrapped.Z - at.Z);

                Kinematics.Poses(body);
                body.RefreshContactSphere();
                body.CommitContactSphere();
            }
        }

        /// <summary>
        /// Kills every body the world can no longer hold — <c>Ecosystem.CheckFinite</c>, through
        /// <see cref="Divergence"/>'s four guards.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Condemned first and killed second, in two passes. <c>World.KillDiverged</c> takes the
        /// creature out of <c>World.Living</c> and the removal takes it out of the solver's list,
        /// and walking a list while it is being emptied is how a body gets skipped. The farm
        /// walked its own <c>_order</c> and killed as it went for the same reason; this is the
        /// same discipline written down.
        /// </para>
        /// <para>
        /// The bounding radius handed to the guards is the placer's — the developed body's,
        /// refreshed at a resize — and not the live contact sphere, because that is the number
        /// <c>CheckFinite</c> compares the bed against.
        /// </para>
        /// </remarks>
        private void CheckFinite()
        {
            _condemned.Clear();
            var reasons = new List<string>();

            for (int i = 0; i < _order.Count; i++)
            {
                Body body = _order[i];
                Creature solver = body.Solver;
                if (solver.Links == 0) continue;

                if (!Divergence.Diverged(solver, Solver, body.Radius, out string reason))
                {
                    // narrow: the root, in Core's frame, for the placer and positions.jsonl.
                    body.LastRootPosition = new Float3(
                        (float)solver.Position[0],
                        (float)solver.Position[1],
                        (float)solver.Position[2]);

                    continue;
                }

                _condemned.Add(body);
                reasons.Add(reason);
            }

            for (int i = 0; i < _condemned.Count; i++)
            {
                HandleDivergence(_condemned[i], reasons[i]);
            }
        }

        /// <summary>Dumps a diverged body's post-mortem and kills it as a counted death.</summary>
        private void HandleDivergence(Body body, string reason)
        {
            Organism creature = body.Creature;

            // The dump before the kill, because World.KillDiverged empties the organism.
            Dumps?.Write(new DivergedBody
            {
                Body = body.Solver,
                Config = Solver,
                PhysicsStep = Steps + 1,
                ElapsedSeconds = World.ElapsedSeconds,
                Reason = reason,
                Genome = creature.Genome,
                AgeSeconds = creature.Age,
                GenerationDepth = creature.GenerationDepth,
                LastObservedHeightY = creature.HeightY,
                LastRootPosition = new Vec3(
                    body.LastRootPosition.X, body.LastRootPosition.Y, body.LastRootPosition.Z),
            });

            Console.Error.WriteLine(
                FormattableString.Invariant(
                    $"creature {creature.Id} diverged at t={World.ElapsedSeconds:0.#} s ") +
                FormattableString.Invariant(
                    $"(physics step {Steps + 1}, {body.Solver.Links} parts, {body.Solver.Dof} dof") +
                (reason != null ? ", " + reason : string.Empty) +
                ") — killed as a death, see the diverged/ dump.");

            World.KillDiverged(creature);
        }

        /// <summary>The heavier of a link's mass and its parent's over the lighter, or 0.</summary>
        /// <remarks>
        /// <c>PhenotypeBuilder.MassRatio</c>, term for term. Taken here rather than asked of
        /// <c>Evosim.Dynamics</c>, which has the same arithmetic private inside its dump writer:
        /// the masses are public on the body and two transcriptions of one division is a smaller
        /// risk than an accessor added to an assembly for one caller.
        /// </remarks>
        private static double MaxMassRatio(Creature body)
        {
            double worst = 0;

            for (int b = 1; b < body.Links; b++)
            {
                int parent = body.Parent[b];
                if (parent < 0 || parent >= body.Links) continue;

                double a = body.Mass[parent], c = body.Mass[b];
                double heavier = a > c ? a : c;
                double lighter = a > c ? c : a;

                if (!(lighter > 0) || double.IsNaN(heavier) || double.IsInfinity(heavier)) continue;

                double ratio = heavier / lighter;
                if (double.IsNaN(ratio) || double.IsInfinity(ratio)) continue;
                if (ratio > worst) worst = ratio;
            }

            return worst;
        }

        private void NoteMassRatio(Body body)
        {
            double ratio = MaxMassRatio(body.Solver);
            if (!(ratio > 0)) return;

            if (ratio > MaxJointMassRatio) MaxJointMassRatio = ratio;

            if (ratio > MassRatioThreshold && !body.CountedOverMassRatio)
            {
                body.CountedOverMassRatio = true;
                BodiesOverMassRatio10++;
            }
        }

        /// <summary>The window's motility, and the window emptied — <c>Ecosystem.DrainMotility</c>.</summary>
        public void DrainMotility(
            out double jointedSum, out long jointedSamples,
            out double rigidSum, out long rigidSamples)
        {
            jointedSum = _jointedSpeedSum;
            jointedSamples = _jointedSpeedSamples;
            rigidSum = _rigidSpeedSum;
            rigidSamples = _rigidSpeedSamples;

            _jointedSpeedSum = 0d;
            _jointedSpeedSamples = 0L;
            _rigidSpeedSum = 0d;
            _rigidSpeedSamples = 0L;
        }

        /// <summary>Where one living creature's root stood this metabolic step.</summary>
        /// <remarks>
        /// A creature conceived during this step has no body yet and returns false, and so does
        /// one whose root is not finite — a diverged body about to be killed rather than a place.
        /// </remarks>
        public bool TryRootPosition(long organismId, out Float3 root)
        {
            if (!_bodies.TryGetValue(organismId, out Body body))
            {
                root = default;
                return false;
            }

            root = body.LastRootPosition;

            float finite = root.X + root.Y + root.Z;
            if (float.IsNaN(finite) || float.IsInfinity(finite))
            {
                root = default;
                return false;
            }

            return true;
        }

        /// <summary>
        /// The solver's body for a living creature — what <c>poses.jsonl</c> reads its root pose
        /// and joint coordinates off.
        /// </summary>
        public bool TryPose(long organismId, out Creature body)
        {
            if (_bodies.TryGetValue(organismId, out Body held))
            {
                body = held.Solver;
                return true;
            }

            body = null;
            return false;
        }

        public void Dispose()
        {
            Dynamics.CloseDigest();
        }
    }
}
