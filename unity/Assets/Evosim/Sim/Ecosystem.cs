using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Evosim.Core;

namespace Evosim.Sim
{
    /// <summary>
    /// The join: an <see cref="Evosim.Core.World"/> whose creatures have bodies — DESIGN.md §10 M4.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two correct halves that had never met.</b> Physics could swim a creature and measure the
    /// work it did; the economy could feed, bill and kill one. Nothing carried a number between
    /// them: <c>World</c> was constructed only in tests, every call site passed <c>workJoules: 0</c>,
    /// and an organism's height was inherited at birth and never written again. So swimming was
    /// free and motion was impossible, and under endogenous selection (§5A.0) that leaves nothing
    /// for a swimmer to be selected *for*.
    /// </para>
    /// <para>
    /// <b>The seam points one way.</b> §6.1 forbids <c>UnityEngine</c> in <c>Evosim.Core</c>, so
    /// the world cannot ask where anything is. This class reads the articulations and pushes two
    /// numbers in through <see cref="Evosim.Core.World.Observe"/>, then reads back who was born and
    /// who died and makes the scene match. The world remains runnable with none of this attached.
    /// </para>
    /// <para>
    /// <b>Two clocks.</b> Physics integrates at <see cref="FixedDt"/> because a solver needs it;
    /// metabolism does not, and evaluating an integral more finely than the thing it integrates
    /// buys nothing. The economy therefore steps once per <see cref="StepsPerMetabolicStep"/>
    /// physics steps, over the accumulated work of all of them.
    /// </para>
    /// <para>
    /// <b>Depth is the whole ecology, for now.</b> Light falls off downward and detritus sinks, so
    /// a creature's two incomes pull in opposite directions along one axis and swimming to a depth
    /// is a strategy with a price. Horizontal position is real in physics and ecologically inert:
    /// creatures are tiled far apart (§6.3) and cannot meet, which is what makes predation a
    /// Milestone 7 problem rather than one this has to solve now.
    /// </para>
    /// </remarks>
    public sealed class Ecosystem
    {
        /// <summary>The metabolic step, seconds. Fixed: the economy runs at 2 Hz whatever the physics does.</summary>
        public const float MetabolicStepSeconds = 0.5f;

        /// <summary>
        /// Physics timestep. 0.01 s unless <see cref="ConfigurePhysicsStep"/> was called (env
        /// <c>EVOSIM_DT</c>); carried in the report header and the run-identity record.
        /// </summary>
        public static float FixedDt { get; private set; } = 0.01f;

        /// <summary>
        /// Physics steps per metabolic step. 50 at the default step, so the economy runs at 2 Hz
        /// against physics' 100; always <see cref="MetabolicStepSeconds"/> / <see cref="FixedDt"/>.
        /// </summary>
        /// <remarks>
        /// Energy is an integral, so a coarser metabolic clock changes only its quantisation and
        /// not its value — unlike a coarser <i>physics</i> clock, which changes what is physically
        /// possible and hands free energy to anything that finds it (§11.2). The two are not the
        /// same kind of approximation and only one of them is safe to take — which is why the
        /// physics step is configurable only for a validation against a seed already run at 0.01
        /// (logbook/0052), and the metabolic step is not configurable at all.
        /// </remarks>
        public static int StepsPerMetabolicStep { get; private set; } = 50;

        /// <summary>
        /// Sets the physics timestep for every <see cref="Ecosystem"/> built afterwards. The
        /// metabolic step stays at <see cref="MetabolicStepSeconds"/>, so <paramref name="dt"/>
        /// must divide it exactly (0.01, 0.02, 0.025, 0.05, 0.1, 0.125, 0.25, 0.5).
        /// </summary>
        public static void ConfigurePhysicsStep(float dt)
        {
            if (!(dt > 0f) || dt > MetabolicStepSeconds)
            {
                throw new ArgumentOutOfRangeException(nameof(dt), dt, "Must be in (0, 0.5].");
            }

            float steps = MetabolicStepSeconds / dt;
            int rounded = (int)Math.Round(steps);
            if (rounded < 1 || Math.Abs(steps - rounded) > 1e-4f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(dt), dt,
                    "Must divide the 0.5 s metabolic step exactly: 0.01, 0.02, 0.025, 0.05, 0.1, 0.125, 0.25 or 0.5.");
            }

            FixedDt = dt;
            StepsPerMetabolicStep = rounded;
        }

        /// <summary>
        /// The most job worker threads this process can be given — the job system's own ceiling,
        /// which is a property of the machine and cannot be raised.
        /// </summary>
        public static int JobWorkerMaximum => Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerMaximumCount;

        /// <summary>
        /// Sets how many job worker threads the physics solver may spread a step over, and
        /// returns the count the job system reports afterwards — D078, logbook/0069.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Why this is a knob at all.</b> The shared-space world does not replay at the
        /// default fifteen workers: two runs of one seed, config and build agree for about
        /// 148,000 steps and then part in one body's velocity by an ulp, where two touching
        /// bodies were solved in a different order. At zero workers — all physics on the main
        /// thread — the same pair is identical over 300,000 steps. PhysX documents results that
        /// do not depend on the thread count; this build, with articulations in contact, does
        /// not have that property.
        /// </para>
        /// <para>
        /// <b>It is a runner setting, not a tunable.</b> It changes no rule the world obeys, so
        /// it must not reach <c>config.json</c> or its hash — every stored config would become
        /// unreadable under §9's refuse-rather-than-default rule for a knob that decides nothing
        /// about the ecology. What it does decide is whether a recording can be watched again,
        /// which is why the value read back is written into the run manifest and the header.
        /// </para>
        /// <para>
        /// <b>Clamped rather than refused.</b> The job system throws outside
        /// [0, <see cref="JobWorkerMaximum"/>], and a run is too expensive to lose to a number
        /// somebody typed one too large. The caller records what came back, not what it asked
        /// for, so a clamp is visible in the manifest rather than silent.
        /// </para>
        /// <para>
        /// <b>Call it before the world is built.</b> The setting is process-wide and takes effect
        /// on the next job scheduled; setting it mid-run would leave a run that was two different
        /// solvers and one header.
        /// </para>
        /// </remarks>
        public static int ConfigurePhysicsJobWorkers(int requested)
        {
            int max = Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerMaximumCount;
            int clamped = requested < 0 ? 0 : requested > max ? max : requested;

            Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount = clamped;

            // Read back rather than returned from the local: the job system is free to disagree,
            // and the number worth recording is the one it will actually run with.
            return Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount;
        }

        /// <summary>Metres between tiled creatures — §6.3.</summary>
        public const float TileSpacing = 100f;

        public World World { get; }
        public FluidEnvironment Fluid { get; }

        /// <summary>Physics steps taken. Simulated seconds is this times <see cref="FixedDt"/>.</summary>
        public long Steps { get; private set; }

        // ---- instrumentation (see the remarks on Report)

        /// <summary>Mean speed of every living creature's centre of mass, m/s, this metabolic step.</summary>
        public double MeanSpeed { get; private set; }

        /// <summary>
        /// Speed of the fastest living creature, m/s, this metabolic step.
        /// </summary>
        /// <remarks>
        /// <b>Reported because the mean was actively misleading.</b> An embodied run showed a mean
        /// of 0.0002 m/s, which reads as "nothing swims"; the same population of random genomes
        /// contains creatures doing 0.48 m/s, because a mean over a population that is mostly
        /// motionless plants is dominated by the zeros (logbook/0016). Selection acts on the tail,
        /// so the tail is what has to be watched.
        /// </remarks>
        public double MaxSpeed { get; private set; }

        /// <summary>Joules the population's joints did this metabolic step.</summary>
        public double WorkThisStep { get; private set; }

        /// <summary>
        /// Root speed of the living, m/s, summed over every metabolic step since the last
        /// <see cref="DrainMotility"/> and split by whether the body has a joint.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The prize side of the movement question, which the ledger has never been able to
        /// read.</b> The cost of swimming is closed — <c>EffectorDriver</c> bills the work — and
        /// what has never been measured is whether the animals that pay it end up anywhere
        /// better. A mean over the whole population cannot answer that, for logbook/0029's
        /// reason: jointed bodies are a minority and the majority sets the mean. So the split is
        /// the instrument, and the reading is <c>food jnt</c> against <c>food rig</c> with
        /// <c>spd jnt</c> non-trivial.
        /// </para>
        /// <para>
        /// Accumulated over the report window rather than sampled at the row, because a speed
        /// taken at one instant of a stroke cycle is a phase, not a speed.
        /// </para>
        /// </remarks>
        private double _jointedSpeedSum;
        private double _rigidSpeedSum;
        private long _jointedSpeedSamples;
        private long _rigidSpeedSamples;

        /// <summary>
        /// Root speed summed over the window and the number of creature-steps it was summed over,
        /// jointed and jointless separately; the window is emptied.
        /// </summary>
        /// <remarks>
        /// Sums and counts rather than two means, so the caller can tell an empty guild from a
        /// stationary one — a mean of 0 is a real speed and a world with no swimmer in it has no
        /// swimming speed at all. The report prints an em-dash for the first and a number for the
        /// second.
        /// </remarks>
        public void DrainMotility(
            out double jointedSum, out long jointedSamples,
            out double rigidSum, out long rigidSamples)
        {
            jointedSum = _jointedSpeedSum;
            jointedSamples = _jointedSpeedSamples;
            rigidSum = _rigidSpeedSum;
            rigidSamples = _rigidSpeedSamples;

            _jointedSpeedSum = 0d;
            _rigidSpeedSum = 0d;
            _jointedSpeedSamples = 0L;
            _rigidSpeedSamples = 0L;
        }

        /// <summary>Joules drag took out of the population, over the run.</summary>
        public double DissipatedJoules => Fluid.DissipatedJoules;

        /// <summary>
        /// Times a drive torque was capped at <see cref="EffectorDriver.MaxJointAngularVelocity"/>,
        /// over the whole population and the whole run. 0 at dt 0.01, where the cap is gated off.
        /// </summary>
        /// <remarks>
        /// Totalled here rather than read from the drivers, because a driver dies with its
        /// creature: each one is drained while it is still reachable — every metabolic step for
        /// the living, and once more in <see cref="Reconcile"/> for a body about to be destroyed
        /// — so a bind is counted exactly once and none is lost with the body that made it.
        /// </remarks>
        public long DriveImpulsesLimited { get; private set; }

        /// <summary>
        /// Articulations resized so far because their creature grew, fable-propose-growth.md
        /// rule 8.
        /// </summary>
        /// <remarks>
        /// A count of bodies-times-resizes, so it is read against the population like
        /// <c>mat blk</c> and <c>crowded</c> (CLAUDE.md). 0 for the life of a run in which nothing
        /// grows, which is every run before this build.
        /// </remarks>
        public long Resizes { get; private set; }

        /// <summary>
        /// The largest distance a root moved across a resize itself, m, over the run so far. This
        /// is the jump check.
        /// </summary>
        /// <remarks>
        /// <b>Measured with no <c>Physics.Simulate</c> in between, so anything above zero is the
        /// engine moving a body because its anchors moved.</b> It is the question rule 8 has to
        /// answer before a round runs on this: a resize that teleports a body is a position
        /// change nobody paid for, and next to a joint drive it is the same accident that
        /// diverged r20q-s1 (logbook/0059). Reported rather than asserted, because "PhysX keeps
        /// the joint state across an anchor change" is documentation and this is a measurement.
        /// </remarks>
        public double MaxResizeJumpMetres { get; private set; }

        /// <summary>
        /// The largest distance a resized root travelled over the metabolic step that followed
        /// its resize, m.
        /// </summary>
        /// <remarks>
        /// The second half of the jump check, and the half that catches a solver kick rather than
        /// a teleport: a constraint frame that moves can leave the solver with an error to correct
        /// and it corrects it as velocity. Read against the population's own <c>spd jnt</c> over
        /// the same window. A swimmer covers about 1.5 mm in a 0.5 s step at the speeds on
        /// record, so a centimetre is far outside what growing should be able to do.
        /// </remarks>
        public double MaxResizeStepMetres { get; private set; }

        private readonly Dictionary<long, Body> _bodies = new Dictionary<long, Body>();

        /// <summary>The bodies to step, and whose creature each one is. Parallel, same order.</summary>
        /// <remarks>
        /// <see cref="FluidEnvironment.Apply(IReadOnlyList{CreatureInstance}, float)"/> wants a
        /// flat list and knows nothing about organisms, so the identity is carried alongside
        /// rather than hung off the phenotype — a phenotype is a developed body and is shared by
        /// every creature that develops the same genome.
        /// </remarks>
        private readonly List<CreatureInstance> _instances = new List<CreatureInstance>();
        private readonly List<long> _instanceIds = new List<long>();

        /// <summary>The living articulations, read-only — for a harness that needs positions.</summary>
        /// <remarks>
        /// The list itself, not a copy: it is rebuilt on every birth and death, so a caller must
        /// read it within one step rather than hold it. Exposed for the shared-space smoke, which
        /// has to ask where two hundred bodies are on every physics step; the run itself never
        /// uses it.
        /// </remarks>
        public IReadOnlyList<CreatureInstance> Instances => _instances;

        /// <summary>Ids whose creature has died, scratch for <see cref="Reconcile"/>.</summary>
        /// <remarks>
        /// A set rather than a list, because it is built from every body and then has every
        /// <i>living</i> creature removed from it. On a list that removal is a linear scan, so the
        /// method was quadratic in population — about 610,000 element shifts at 781 creatures,
        /// a hundred times per simulated second (logbook/0023).
        /// </remarks>
        private readonly HashSet<long> _departed = new HashSet<long>();

        /// <summary>
        /// The bodies to step, in the same order as <see cref="_instances"/>.
        /// </summary>
        /// <remarks>
        /// Held directly so the two hot loops in <see cref="Step"/> do not hash an id per creature
        /// per physics step. Rebuilt only when the population changes, which is what
        /// <see cref="_reconciledAt"/> decides.
        /// </remarks>
        private readonly List<Body> _order = new List<Body>();

        /// <summary>
        /// Value of the world's birth-and-death counter when the scene last matched it.
        /// </summary>
        /// <remarks>
        /// <b>Creatures are born and die inside <see cref="World.Step"/>, which runs once every
        /// <see cref="StepsPerMetabolicStep"/> physics steps.</b> Reconciling on every physics step
        /// therefore did the whole scan forty-nine times out of fifty to discover that nothing had
        /// changed. Births, deaths and floor spawns are the only things that add or remove a
        /// creature, so their sum is a complete revision number for the population.
        /// </remarks>
        private long _reconciledAt = -1;
        private readonly Transform _parent;

        /// <summary>
        /// Lattice slots freed by death, reused before any new one is issued.
        /// </summary>
        /// <remarks>
        /// <b>Without this the world walks away from the origin and takes its own precision with
        /// it.</b> Slots were issued from a counter that only ever went up, so after 100,000 births
        /// creatures were being built 156 km out — where a <c>float</c> resolves about 1 cm, which
        /// is larger than the 3.75 mm a creature covers in a metabolic step. Speeds would quantise
        /// toward zero and the solver would degrade, and both would look like biology rather than
        /// arithmetic. Harmless at the few hundred births measured so far; fatal to exactly the
        /// long run this exists for.
        /// </remarks>
        private readonly Stack<int> _freeTiles = new Stack<int>();
        private int _nextTile;

        /// <summary>One creature's physical presence, and the bookkeeping the join needs.</summary>
        private sealed class Body
        {
            public CreatureInstance Instance;

            /// <summary>
            /// The organism this body belongs to.
            /// </summary>
            /// <remarks>
            /// Held rather than looked up because the divergence check runs between physics steps
            /// and has only the body: <see cref="World.Living"/> is a list, so finding the
            /// organism by id there is a scan of the whole population, and the id-to-body
            /// dictionary points the wrong way. The reference is stable for the creature's whole
            /// life — <see cref="World"/> never replaces an organism object — and it dies with
            /// the body at <see cref="Reconcile"/>.
            /// </remarks>
            public Organism Creature;

            /// <summary>
            /// Where the root part stood the last time <see cref="CheckFinite"/> found it finite.
            /// </summary>
            /// <remarks>
            /// Free: the check reads that position anyway, and a diverged body's own transform
            /// reads NaN by the time the post-mortem asks it. One <c>Vector3</c> copy per creature
            /// per step buys the dump the one place the body actually was.
            /// </remarks>
            public Vector3 LastRootPosition;

            public EffectorDriver Driver;

            /// <summary>
            /// The creature's own nervous system — DESIGN.md §4.3.
            /// </summary>
            /// <remarks>
            /// Replaces the shared test sine that drove every creature identically regardless of
            /// genome. That constant controller is why billing mechanical work exterminated every
            /// joint in the world in sixty seconds (logbook/0015): with no genome able to change
            /// how it moved, work was a tax on having a body part rather than a price for using
            /// one. Held per creature because the brain carries state — oscillator phase, and the
            /// previous step's outputs that every non-local input reads.
            /// </remarks>
            public Brain Brain;

            /// <summary>
            /// What that nervous system can perceive — DESIGN.md §4.4.
            /// </summary>
            /// <remarks>
            /// Held per creature because it caches a sample of that creature's own body. Until it
            /// existed every sensor input in every genome read zero, which made the brain an open
            /// loop: it could produce a stroke but could not aim one, so swimming cost work and
            /// returned nothing on average and the ledger deleted it (logbook/0018).
            /// </remarks>
            public CreatureSensors Sensors;

            public float[] Drive;

            /// <summary>
            /// <see cref="EffectorDriver.MechanicalWorkJoules"/> at the last metabolic step.
            /// </summary>
            /// <remarks>
            /// The driver reports a running total since construction, and the economy needs the
            /// interval. Stored per creature rather than reset on the driver, because the total is
            /// also what the lifetime figures are drawn from and resetting it would quietly make
            /// every one of those wrong instead.
            /// </remarks>
            public double WorkAtLastStep;

            public Vector3 PreviousCentre;

            /// <summary>
            /// Where the root part stood at the previous metabolic step — the motility
            /// instrument's baseline.
            /// </summary>
            /// <remarks>
            /// The root rather than the centre of mass, and separate from
            /// <see cref="PreviousCentre"/> rather than folded into it: the centre moves when a
            /// creature folds up without going anywhere, and "did the animal travel" is the
            /// question the movement round asks. Free — <see cref="CheckFinite"/> has already
            /// read the position this is differenced against.
            /// </remarks>
            public Vector3 PreviousRoot;

            /// <summary>
            /// False until this creature has completed one whole metabolic step.
            /// </summary>
            /// <remarks>
            /// <b>A newborn's first speed sample is not a speed.</b> <see cref="PreviousCentre"/>
            /// is taken the instant the articulation is built, before the solver has run once, and
            /// the interval that follows contains whatever the build transient does — added mass
            /// being applied, a spawn pose depenetrating at up to
            /// <c>Physics.defaultMaxDepenetrationVelocity</c>. Including it made "fastest creature
            /// seen at any point" report 0.075 m/s in runs whose fastest living creature at every
            /// sampled row was doing 0.003, and report the <i>same</i> figure for two different
            /// seeds — which is the signature this project has twice agreed means the number is
            /// not measuring what it says (logbook/0007, logbook/0008).
            /// </remarks>
            public bool Settled;

            /// <summary>Lattice slot this creature occupies, returned to the pool when it dies.</summary>
            /// <remarks>−1 in a shared volume, where there is no lattice — D077.</remarks>
            public int Tile;

            /// <summary>
            /// Radius of the sphere that holds the whole body — <see cref="SharedVolume.BoundingRadius"/>.
            /// </summary>
            /// <remarks>
            /// <b>This used to be computed once because a body could not change size.</b> It can
            /// now (fable-propose-growth.md rule 8, 2026-09-08), so it is recomputed on every
            /// resize. Left at its birth value it would tell the placer that a grown adult was
            /// still newborn-sized, and D077's crowding test would let bodies be conceived inside
            /// each other. An overlap is a force, and one logbook/0007 measured a creature
            /// learning to farm. 0 in a tiled world, where nothing asks.
            /// </remarks>
            public float Radius;

            /// <summary>
            /// The <c>Organism.BodyFraction</c> the articulation was last built or resized at.
            /// </summary>
            /// <remarks>
            /// <b>Held here rather than read off the phenotype, so that the physics knows what it
            /// applied and not what the ledger wants.</b> Core moves joules into tissue on every
            /// metabolic step and the harness follows at <c>RunConfig.GrowthStepSeconds</c>, so
            /// the two are almost always apart; comparing them is what decides whether this body
            /// needs the expensive pass. A creature that reaches fraction 1 is resized once, on
            /// the growth step after it gets there, and never again, because from then on the two
            /// numbers agree.
            /// </remarks>
            public float AppliedBodyFraction;

            /// <summary>
            /// True for the one metabolic step that follows a resize. The jump check's flag.
            /// </summary>
            /// <remarks>
            /// A resize moves a joint's constraint frame while the solver holds the body's state,
            /// and the failure mode is a body that teleports or is kicked when it does. Every
            /// divergence on record was a body accelerated in one step (logbook/0059, 0077), so
            /// the displacement over the step after a resize is measured rather than assumed
            /// harmless. Costs one distance per resized body per step and nothing at all once a
            /// population is grown.
            /// </remarks>
            public bool ResizedLastStep;
        }

        public Ecosystem(RunConfig config, ulong seed = 1, Transform parent = null)
        {
            World = new World(config, seed);
            Fluid = new FluidEnvironment(config.Fluid, config.Shapes, config.Current);

            // D066. The same K the world's fields were built with — read once here rather than per
            // body per step, because HorizontalPatches cannot change during a run.
            Fluid.PatchCount = Mathf.Max(1, (int)config.HorizontalPatches);

            // D077. The world's floor, so the restoring boundary knows where the bottom is. Set
            // whatever SharedSpace says: SurfaceRestoringFraction is its own knob and the bottom
            // is a vertical rule, readable without the box.
            Fluid.WorldDepthMetres = config.WorldDepthMetres;

            if (config.SharedSpace)
            {
                // The patch width from the fields themselves — sqrt(area / K) — rather than
                // recomputed here. One derivation: a world with two answers for how wide a patch
                // is would price the ecology against one and place bodies against the other.
                Volume = new SharedVolume(
                    Fluid.PatchCount, World.Nutrients.PatchWidthMetres,
                    config.WorldDepthMetres, seed, config.OffspringDispersalMetres);

                World.Placement = Volume;

                // The sea bed, before anything is placed: SharedVolume asks it how much clearance
                // a body needs, and the very first floor spawn of the run has to get the answer.
                Floor = SeaFloor.Build(Volume, parent);
                Volume.Floor = Floor;

                if (Floor != null)
                {
                    _floorEntityId = Floor.ColliderEntityId;
                    _hasFloor = true;
                }

                // And with rock under the box, the restoring mirror below −D is retired: two
                // things holding the same boundary is a trampoline. FluidEnvironment.FloorIsSolid
                // says why at length.
                Fluid.FloorIsSolid = Floor != null;

                Physics.ContactEvent += OnContactEvent;
                _countingContacts = true;
            }

            _parent = parent;
        }

        private bool _countingContacts;

        /// <summary>
        /// The engine's own contact report, summed into <see cref="ContactPairs"/>.
        /// </summary>
        /// <remarks>
        /// Raised by PhysX rather than dispatched as a MonoBehaviour message, which is what makes
        /// it readable in <c>-batchmode</c> against a scene that is not playing — the spike's
        /// second counter, promoted to the only one because it is the one that cannot silently
        /// read zero (logbook/0064). Interlocked because the event can arrive on a worker thread.
        /// </remarks>
        /// <remarks>
        /// <b>A body resting on the bed is not two animals meeting</b>, and the pairs are split so
        /// that a benthic crowd shows up as itself rather than swamping the number the crowding
        /// question is asked of. Split per <i>pair</i> rather than per header: a header names the
        /// two bodies, and a static collider has no <c>Rigidbody</c> or <c>ArticulationBody</c> to
        /// be named by, so <c>bodyInstanceID</c> cannot be trusted to identify the floor. The pair
        /// carries the colliders' own instance ids, which the floor definitely has one of. The
        /// extra work is one comparison per contact pair per step — the solver has already done
        /// far more to produce it.
        /// </remarks>
        private void OnContactEvent(
            PhysicsScene scene, Unity.Collections.NativeArray<ContactPairHeader>.ReadOnly headers)
        {
            EntityId floorId = _floorEntityId;
            bool hasFloor = _hasFloor;
            long pairs = 0;
            long floorPairs = 0;

            for (int i = 0; i < headers.Length; i++)
            {
                ContactPairHeader header = headers[i];
                int count = (int)header.pairCount;

                // No floor in this world: every pair is a creature pair and there is nothing to
                // ask of any of them. This is also the tiled path, where the event is not even
                // subscribed.
                if (!hasFloor) { pairs += count; continue; }

                for (int j = 0; j < count; j++)
                {
                    ContactPair pair = header.GetContactPair(j);

                    if (pair.colliderEntityId.Equals(floorId) ||
                        pair.otherColliderEntityId.Equals(floorId))
                    {
                        floorPairs++;
                    }
                    else
                    {
                        pairs++;
                    }
                }
            }

            if (pairs != 0) System.Threading.Interlocked.Add(ref _contactPairs, pairs);
            if (floorPairs != 0) System.Threading.Interlocked.Add(ref _floorContactPairs, floorPairs);
        }

        private long _contactPairs;
        private long _floorContactPairs;

        /// <summary>The sea bed's collider id, cached for the contact callback.</summary>
        /// <remarks>
        /// Paired with <see cref="_hasFloor"/> rather than compared against <c>EntityId.None</c>:
        /// "there is no bed" is a fact about this world and not a value a collider might happen to
        /// have, and the callback runs on a worker thread where a wrong answer is invisible.
        /// </remarks>
        private readonly EntityId _floorEntityId;

        private readonly bool _hasFloor;

        /// <summary>
        /// The box, when there is one — D077. Null in a tiled world, which is every run before
        /// D077 and every run with <c>EVOSIM_SHARED_SPACE</c> unset.
        /// </summary>
        public SharedVolume Volume { get; }

        /// <summary>
        /// The sea bed under the box — <c>scratch/floor-spec.md</c>. Null in a tiled world.
        /// </summary>
        public SeaFloor Floor { get; }

        /// <summary>Living creatures whose root is above the waterline, at the last sample.</summary>
        /// <remarks>
        /// The instrument the surface question needed before the fix could be judged
        /// (logbook/0061): the vent's plume lifts bodies and above y = 0 nothing acted on them, so
        /// "the population sits at +1 m" was an inference from the code rather than a count. One
        /// comparison per creature per metabolic step, against a position <see cref="CheckFinite"/>
        /// has already read — it reads and does not act, so it is bit-identical by construction and
        /// is counted in the tiled world too.
        /// </remarks>
        public int AboveSurface { get; private set; }

        /// <summary>Bodies translated at a seam, running total. 0 unless shared — D077.</summary>
        public long Wraps => Volume != null ? Volume.Wraps : 0L;

        /// <summary>Births refused for want of room, running total. 0 unless shared — D077.</summary>
        public long Crowded => World.CrowdedStillbirths;

        /// <summary>Contact pairs the engine has reported, running total. 0 unless shared.</summary>
        /// <remarks>
        /// From <c>Physics.ContactEvent</c> rather than from <c>OnCollisionStay</c>: the engine
        /// raises it itself, where the MonoBehaviour message depends on the editor dispatching
        /// physics messages to a scene that is not playing. The spike learned the harder half of
        /// this — the event needs <c>Collider.providesContacts</c>, which defaults off, and
        /// without it PhysX resolves the contact and tells nobody (logbook/0064).
        /// </remarks>
        public long ContactPairs => System.Threading.Interlocked.Read(ref _contactPairs);

        /// <summary>
        /// Contact pairs against the sea bed, running total. 0 unless there is one — D077's floor.
        /// </summary>
        /// <remarks>
        /// Kept out of <see cref="ContactPairs"/> rather than added to it: <see cref="ContactPairs"/>
        /// answers "how crowded is the water", which the placement budget and the crowded
        /// stillbirth count are read against, and a population settled on the bottom would inflate
        /// it by a body-count's worth of pairs that are nothing to do with each other. This one
        /// answers a different question — how much of the population is on the floor — and is the
        /// only reading of it the report has, since depth-by-guild is not measurable
        /// (CLAUDE.md's lineage-dissection gotcha).
        /// </remarks>
        public long FloorContactPairs => System.Threading.Interlocked.Read(ref _floorContactPairs);

        // -------------------------------------------------------------- where the bodies are, flat

        /// <summary>The side of one horizontal column the spread instrument counts, metres.</summary>
        /// <remarks>
        /// A metre, because a creature is metre-scale (§4.1's dimension range) and the ribbon this
        /// instrument was built for was about a metre wide. It also makes the reading easy to say
        /// out loud: the campaign's box is 20 x 5 m, so the count is out of 100.
        /// </remarks>
        public const float ColumnMetres = 1f;

        /// <summary>
        /// How much of the box's footprint the living actually stand on, at one sample.
        /// </summary>
        /// <remarks>
        /// <b><see cref="TotalColumns"/> is 0 when the instrument is off</b>, which is every tiled
        /// world: bodies there sit on a lattice a hundred metres apart and a footprint column
        /// means nothing. The report prints an em-dash on that, for the reason <c>contacts</c>
        /// does. 0 occupied columns out of 100 and no box are different facts.
        /// </remarks>
        public struct HorizontalSpread
        {
            /// <summary>Columns holding at least one living root.</summary>
            public int OccupiedColumns;

            /// <summary>Columns in the box's footprint. 0 means the instrument is off.</summary>
            public int TotalColumns;

            /// <summary>Columns holding at least one living root with absorptive tissue.</summary>
            public int OccupiedColumnsAbsorptive;

            /// <summary>Circular standard deviation of x, metres, on the ring's circumference.</summary>
            public double XSpreadMetres;

            /// <summary>Circular standard deviation of z, metres, across the box's width.</summary>
            public double ZSpreadMetres;

            /// <summary>Living bodies the reading is taken over.</summary>
            public int Bodies;
        }

        /// <summary>Reused between samples, since the footprint's size cannot change in a run.</summary>
        private bool[] _columnHeld;
        private bool[] _columnHeldAbsorptive;

        /// <summary>
        /// Where the living are horizontally: how many columns of the footprint they stand on, and
        /// how spread out they are on each axis.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The report had no x and no z at all, and that is how a world could run for thirty
        /// thousand seconds as two ribbons without anyone seeing it.</b> On 2026-09-10 the theatre
        /// showed round 33 seed 3 as two vertical columns about a metre wide. Nothing in the world
        /// moves a sitter sideways: a newborn is placed against its parent (D077 rule 5), the
        /// rolling current puts a body back where it found it (D059), and no other rule touches
        /// the horizontal. Since the grid (D086) a mouth drains the one cell it stands in, so a
        /// ribbon drains the same handful of cells forever. The report carried a mean depth and
        /// per-patch bins, and a patch is 10 m wide: four bins cannot tell a spread population
        /// from a pair of threads inside one of them.
        /// </para>
        /// <para>
        /// <b>Circular, not linear, on both axes.</b> The box is periodic (<see cref="SharedVolume"/>),
        /// so x = 0.1 and x = 19.9 are neighbours and a linear standard deviation would call that
        /// pair the most spread population possible. The circular deviation is the standard fix:
        /// average the unit vectors, and read the spread off the length of the mean. It is
        /// reported in metres by scaling radians back onto the axis, so it is comparable with
        /// <c>depth sd</c> beside it, and it is capped at one whole extent, which no real
        /// population reaches.
        /// </para>
        /// <para>
        /// <b>At the sample cadence, off the physics path.</b> One dictionary lookup per living
        /// creature against a root position <see cref="CheckFinite"/> already read this metabolic
        /// step, so the instrument makes no Transform read of its own, the same discipline
        /// <see cref="AboveSurface"/> keeps. A creature conceived during this step has no body
        /// yet and is skipped; so is one whose root is not finite, which is a diverged body about
        /// to be killed rather than a position.
        /// </para>
        /// </remarks>
        /// <param name="absorptive">
        /// Ids of the living that carry absorptive tissue, or null for none. Passed in rather than
        /// walked here, so that <c>cols abs</c> and <c>absorpt</c> in the same row cannot come
        /// from two different definitions of a stomach.
        /// </param>
        public HorizontalSpread MeasureHorizontalSpread(HashSet<long> absorptive)
        {
            var reading = new HorizontalSpread();
            if (Volume == null) return reading;

            float length = Volume.LengthMetres;
            float width = Volume.PatchWidthMetres;

            // Ceiling, not rounding, so a box whose side is not a whole number of metres still has
            // a column for every point in it; the last one on each axis is then a part column, and
            // the clamp below is what keeps a body exactly on the far face inside the array.
            int nx = Mathf.Max(1, Mathf.CeilToInt(length / ColumnMetres));
            int nz = Mathf.Max(1, Mathf.CeilToInt(width / ColumnMetres));
            int total = nx * nz;

            if (_columnHeld == null || _columnHeld.Length != total)
            {
                _columnHeld = new bool[total];
                _columnHeldAbsorptive = new bool[total];
            }
            else
            {
                Array.Clear(_columnHeld, 0, total);
                Array.Clear(_columnHeldAbsorptive, 0, total);
            }

            double sinX = 0d, cosX = 0d, sinZ = 0d, cosZ = 0d;

            IReadOnlyList<Organism> living = World.Living;

            for (int i = 0; i < living.Count; i++)
            {
                Organism creature = living[i];
                if (!_bodies.TryGetValue(creature.Id, out Body body)) continue;

                Vector3 root = body.LastRootPosition;
                float horizontal = root.x + root.z;
                if (float.IsNaN(horizontal) || float.IsInfinity(horizontal)) continue;

                reading.Bodies++;

                int ix = Mathf.Clamp(Mathf.FloorToInt(root.x / ColumnMetres), 0, nx - 1);
                int iz = Mathf.Clamp(Mathf.FloorToInt(root.z / ColumnMetres), 0, nz - 1);
                int column = iz * nx + ix;

                if (!_columnHeld[column])
                {
                    _columnHeld[column] = true;
                    reading.OccupiedColumns++;
                }

                if (absorptive != null && absorptive.Contains(creature.Id) && !_columnHeldAbsorptive[column])
                {
                    _columnHeldAbsorptive[column] = true;
                    reading.OccupiedColumnsAbsorptive++;
                }

                double angleX = 2d * Math.PI * root.x / length;
                double angleZ = 2d * Math.PI * root.z / width;

                sinX += Math.Sin(angleX);
                cosX += Math.Cos(angleX);
                sinZ += Math.Sin(angleZ);
                cosZ += Math.Cos(angleZ);
            }

            reading.TotalColumns = total;
            reading.XSpreadMetres = CircularSpread(sinX, cosX, reading.Bodies, length);
            reading.ZSpreadMetres = CircularSpread(sinZ, cosZ, reading.Bodies, width);

            return reading;
        }

        /// <summary>
        /// Mardia's circular standard deviation, in metres on an axis of <paramref name="extent"/>.
        /// </summary>
        /// <remarks>
        /// sqrt(-2 ln R), where R is the length of the mean unit vector, converted from radians by
        /// extent / 2pi. It is unbounded as R goes to zero, which is a population spread perfectly
        /// evenly round the ring, so it is capped at one whole extent: a scattered world reads
        /// about 0.46 of it and nothing real gets past that. 0 for fewer than two bodies, where
        /// there is no spread to speak of rather than a spread of nothing.
        /// </remarks>
        private static double CircularSpread(double sinSum, double cosSum, int count, double extent)
        {
            if (count < 2 || extent <= 0d) return 0d;

            double r = Math.Sqrt(sinSum * sinSum + cosSum * cosSum) / count;
            if (r <= 0d) return extent;

            double radians = Math.Sqrt(Math.Max(0d, -2d * Math.Log(Math.Min(1d, r))));

            return Math.Min(extent, radians * extent / (2d * Math.PI));
        }

        /// <summary>
        /// Where one living creature's root stood this metabolic step, or false if it has no
        /// position to report.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The whole of what <c>positions.jsonl</c> reads.</b> Nothing in a run said where a
        /// body was until 2026-09-10, when the theatre showed round 33 seed 3 as two ribbons a
        /// metre wide (logbook/0083) and thirty-three rounds turned out to have been read on a
        /// mean depth and four patch bins. The file that fixes that is written by the harness, and
        /// this is the only thing it needs from the physics side.
        /// </para>
        /// <para>
        /// <b>No Transform read of its own</b>, the same discipline
        /// <see cref="MeasureHorizontalSpread"/> keeps: <see cref="CheckFinite"/> took this
        /// position a few lines earlier in the step. A creature conceived during this step has no
        /// body yet and returns false, and so does one whose root is not finite, which is a
        /// diverged body about to be killed rather than a place.
        /// </para>
        /// <para>
        /// All three axes are checked here where the spread instrument checks only x and z: that
        /// one wants a footprint column and this one writes a depth, and a NaN y written as a
        /// number would be a creature plotted at a height it never had.
        /// </para>
        /// </remarks>
        public bool TryRootPosition(long organismId, out Vector3 root)
        {
            if (!_bodies.TryGetValue(organismId, out Body body))
            {
                root = Vector3.zero;
                return false;
            }

            root = body.LastRootPosition;

            float finite = root.x + root.y + root.z;
            if (float.IsNaN(finite) || float.IsInfinity(finite))
            {
                root = Vector3.zero;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Advances physics one step, and the economy once every
        /// <see cref="StepsPerMetabolicStep"/>. Returns true on the steps the economy ran.
        /// </summary>
        public bool Step()
        {
            Reconcile();

            for (int i = 0; i < _order.Count; i++)
            {
                Body body = _order[i];

                // Sampled before the brain reads it, so every neuron in the creature perceives
                // the same instant — the sensory counterpart of §4.3's synchronous update.
                body.Sensors.Sample();
                body.Brain.Step(FixedDt, body.Drive, body.Sensors);
                body.Driver.Drive(body.Drive);
            }

            // The water's own clock, advanced from the physics step rather than the metabolic one:
            // a current that only updated twice a second would be a staircase to swim against, and
            // the creature would feel the discretisation rather than the flow.
            Fluid.ElapsedSeconds = Steps * (double)FixedDt;

            Fluid.Apply(_instances, FixedDt);
            Physics.Simulate(FixedDt);

            // D077. Immediately after the solver, so nothing ever reads a position outside the
            // box: the ring is periodic and a body that has crossed a face is on the other side
            // of the world, not outside it. Before Settle, which reads velocities and not
            // positions, so the order between the two is a matter of the rule's wording rather
            // than of arithmetic.
            if (Volume != null) WrapAtTheSeams();

            Fluid.Settle(_instances);

            for (int i = 0; i < _order.Count; i++) _order[i].Driver.Settle();

            Steps++;

            // The state digest — scratch/digest-spec.md. One null test per physics step when it
            // is off, which is every run that does not set EVOSIM_DIGEST_EVERY: the instrument
            // reads the solver and writes a file, and touches nothing the world will read back.
            // Placed after Settle rather than immediately after Physics.Simulate because neither
            // Settle writes to a body — both only read velocities to integrate work — so this is
            // the same state the solver left, with the step counter already advanced to name it.
            if (_digest != null) Digest();

            if (Steps % StepsPerMetabolicStep != 0) return false;

            Metabolise();
            return true;
        }

        // ---- the state digest (scratch/digest-spec.md)

        /// <summary>Floats recorded per part: position 3, rotation 4, linear 3, angular 3.</summary>
        /// <remarks>
        /// The spec's prose says "the same ten floats" while enumerating thirteen; the enumeration
        /// is what is implemented, because dropping any of the four quantities would make the
        /// digest blind to a divergence that shows up first in the one dropped.
        /// </remarks>
        private const int DigestFloatsPerPart = 13;

        private JsonlWriter _digest;
        private JsonlWriter _digestBodies;
        private string _digestDirectory;
        private long _digestEvery;
        private HashSet<long> _digestDumpSteps;

        // Reused, because the digest runs inside the step loop and a per-value BitConverter
        // allocation is 13 arrays per part per digest. Buffer.BlockCopy gives the raw bits
        // without unsafe code, which neither Sim asmdef allows.
        private readonly long[] _digestId = new long[1];
        private readonly byte[] _digestIdBytes = new byte[8];
        private readonly float[] _digestPart = new float[DigestFloatsPerPart];
        private readonly byte[] _digestPartBytes = new byte[DigestFloatsPerPart * 4];

        /// <summary>
        /// Turns the per-step digest on, writing into an existing run directory.
        /// </summary>
        /// <param name="runDirectory">The run's own directory; <c>digest.jsonl</c> goes in it.</param>
        /// <param name="everySteps">Physics steps between digests. 0 or less leaves it off.</param>
        /// <param name="dumpSteps">Steps at which to also write a row per living body, or null.</param>
        public void EnableDigest(string runDirectory, long everySteps, IEnumerable<long> dumpSteps)
        {
            if (string.IsNullOrEmpty(runDirectory))
            {
                throw new ArgumentException("A run directory is required.", nameof(runDirectory));
            }

            if (everySteps <= 0) return;

            _digestDirectory = runDirectory;
            _digestEvery = everySteps;

            _digestDumpSteps = null;
            if (dumpSteps != null)
            {
                var set = new HashSet<long>(dumpSteps);
                if (set.Count > 0) _digestDumpSteps = set;
            }

            // Flushed each row: this file exists to be diffed against another run's, and a run
            // stopped or wedged part-way through is exactly the case it is read in.
            _digest = new JsonlWriter(Path.Combine(runDirectory, "digest.jsonl"), flushEachRow: true);
        }

        /// <summary>One digest row, and the per-body dump on the steps that asked for one.</summary>
        private void Digest()
        {
            bool due = Steps == 1 || Steps % _digestEvery == 0;
            bool dump = _digestDumpSteps != null && _digestDumpSteps.Contains(Steps);
            if (!due && !dump) return;

            // FNV-1a 64, over the raw bits of every living body's state in World.Living order —
            // which is the order Reconcile built _order in.
            ulong hash = 14695981039346656037UL;
            long first = -1;
            int counted = 0;

            for (int i = 0; i < _order.Count; i++)
            {
                Body body = _order[i];
                ArticulationBody[] bodies = body.Instance.Bodies;
                if (bodies == null || bodies.Length == 0) continue;

                long id = body.Creature.Id;
                if (first < 0) first = id;
                counted++;

                _digestId[0] = id;
                Buffer.BlockCopy(_digestId, 0, _digestIdBytes, 0, 8);
                hash = Fnv1a(hash, _digestIdBytes, 8);

                for (int b = 0; b < bodies.Length; b++)
                {
                    ReadPartState(bodies[b], _digestPart);
                    Buffer.BlockCopy(_digestPart, 0, _digestPartBytes, 0, DigestFloatsPerPart * 4);
                    hash = Fnv1a(hash, _digestPartBytes, DigestFloatsPerPart * 4);
                }
            }

            string hex = hash.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);
            long step = Steps;
            double t = step * (double)FixedDt;
            int bodyCount = counted;
            long firstId = first;

            _digest.WriteRow(w => w
                .Field("step", step)
                .Field("t", t)
                .Field("bodies", bodyCount)
                .Field("hash", hex)
                .Field("first", firstId));

            if (dump) DumpBodies();
        }

        /// <summary>One row per living creature: every number the digest hashed, in the clear.</summary>
        private void DumpBodies()
        {
            if (_digestBodies == null)
            {
                _digestBodies = new JsonlWriter(
                    Path.Combine(_digestDirectory, "digest-bodies.jsonl"), flushEachRow: true);
            }

            for (int i = 0; i < _order.Count; i++)
            {
                Body body = _order[i];
                ArticulationBody[] bodies = body.Instance.Bodies;
                if (bodies == null || bodies.Length == 0) continue;

                var links = new StringBuilder();
                links.Append('[');
                for (int b = 1; b < bodies.Length; b++)
                {
                    if (b > 1) links.Append(',');
                    links.Append(PartStateJson(bodies[b]));
                }
                links.Append(']');

                var w = new Json.Writer(indent: false);
                w.BeginObject();
                w.Field("step", Steps);
                w.Field("id", body.Creature.Id);
                w.Raw("root", PartStateJson(bodies[0]));
                w.Raw("links", links.ToString());
                w.Field("sleeping", bodies[0].IsSleeping());

                // PhysX reports contacts as pairs to a scene-wide callback, not per body, so
                // there is no cheap per-body count to read — the spec allows 0 and this is it.
                w.Field("contacts", 0);
                w.EndObject();

                _digestBodies.Write(w.ToString());
            }
        }

        /// <summary>
        /// One part's thirteen numbers, in the order the hash takes them.
        /// </summary>
        /// <remarks>
        /// Velocities come off the <see cref="ArticulationBody"/>; the pose has to come off the
        /// Transform, which is the only place Unity exposes an articulation link's world pose —
        /// <c>Physics.Simulate</c> writes it back on every step whatever
        /// <c>Physics.autoSyncTransforms</c> is set to, and reading it syncs nothing. It is the
        /// same read <see cref="CheckFinite"/> and the divergence dump already make.
        /// </remarks>
        private static void ReadPartState(ArticulationBody part, float[] into)
        {
            Transform t = part.transform;
            Vector3 p = t.position;
            Quaternion r = t.rotation;
            Vector3 v = part.linearVelocity;
            Vector3 w = part.angularVelocity;

            into[0] = p.x; into[1] = p.y; into[2] = p.z;
            into[3] = r.x; into[4] = r.y; into[5] = r.z; into[6] = r.w;
            into[7] = v.x; into[8] = v.y; into[9] = v.z;
            into[10] = w.x; into[11] = w.y; into[12] = w.z;
        }

        /// <summary>The same thirteen numbers as a JSON array, round-trip formatted.</summary>
        /// <remarks>
        /// "R" so two dumps can be diffed to the bit. A non-finite component goes in as a quoted
        /// string for the divergence dump's reason — JSON cannot represent one, and a file that
        /// throws while recording a divergence records nothing.
        /// </remarks>
        private string PartStateJson(ArticulationBody part)
        {
            ReadPartState(part, _digestPart);

            var sb = new StringBuilder(192);
            sb.Append('[');

            for (int i = 0; i < DigestFloatsPerPart; i++)
            {
                if (i > 0) sb.Append(',');
                float v = _digestPart[i];

                if (float.IsNaN(v) || float.IsInfinity(v))
                {
                    sb.Append('"')
                      .Append(v.ToString(System.Globalization.CultureInfo.InvariantCulture))
                      .Append('"');
                }
                else
                {
                    sb.Append(v.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                }
            }

            sb.Append(']');
            return sb.ToString();
        }

        private static ulong Fnv1a(ulong hash, byte[] bytes, int count)
        {
            for (int i = 0; i < count; i++)
            {
                hash ^= bytes[i];
                hash *= 1099511628211UL;
            }

            return hash;
        }

        /// <summary>
        /// Where a diverged body's last finite state is written, one file per creature, or null
        /// to record nothing. Set by the harness to <c>runs/&lt;arm&gt;/&lt;run&gt;/diverged</c>.
        /// </summary>
        /// <remarks>
        /// Null in every test harness and in the sandbox scene, where there is no run directory to
        /// write into. A divergence there is still a death and still counted; it is only the
        /// post-mortem that has nowhere to go.
        /// </remarks>
        public string DivergenceDumpDirectory { get; set; }

        /// <summary>
        /// Dumps written this run. Capped, because a world that has started diverging in bulk
        /// would otherwise write a genome-sized file per creature per step, and the fiftieth file
        /// says nothing the first fifty did not.
        /// </summary>
        private int _dumpsWritten;

        private const int MaxDumps = 50;

        /// <summary>
        /// Checks that every living body is still finite and still in the world, and kills the
        /// ones that are not.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Finite is not enough.</b> <c>r31-s3</c> lost four bodies in one step at 11,533.5 s;
        /// three were caught here at NaN and dumped, and a fourth whose root was finite and
        /// astronomical went through to <c>World.Observe</c>, passed its non-finite guard, and
        /// overflowed the light field's layer index — an <c>ArgumentOutOfRangeException</c> from
        /// <c>LightField.Contribute</c> and a censored arm (logbook/0077). The height is now read
        /// against <see cref="World.HeightIsInTheWorld"/>, the one bound Core's guard refuses at,
        /// so a body the solver has thrown out of the sea dies here as the counted death a NaN
        /// body does, and the guard behind it stays a guard.
        /// </para>
        /// <para>
        /// <b>One native read and two branches per creature, once per metabolic step.</b> The
        /// root's position is the whole test: a divergence reaches it before it reaches anything
        /// else — <c>3075</c>'s dump has the root at NaN while its velocities were still
        /// (enormously) finite — and reading two velocities per <i>part</i> per <i>physics</i>
        /// step instead cost 27% of the wall clock of a five-thousand-creature world, against 16%
        /// for the root alone and under 5% at this cadence. NaN and infinity both propagate
        /// through addition, so summing the three components and testing the sum once is the same
        /// test as testing all three.
        /// </para>
        /// <para>
        /// Read from the solver rather than from the fluid's cached copies: those were gathered
        /// <i>before</i> the last <c>Physics.Simulate</c>, and the steps just taken are the ones
        /// that could have blown up. Everything the post-mortem wants beyond this — velocities,
        /// spins, torques — is read only after this test has already failed.
        /// </para>
        /// <para>
        /// The last finite root position is kept as it goes past, on the body rather than in an
        /// array indexed by position: <see cref="Reconcile"/> rebuilds the order on every birth
        /// and death, so slot <i>i</i> is a different creature from one step to the next.
        /// </para>
        /// </remarks>
        private void CheckFinite()
        {
            float depth = World.Config.WorldDepthMetres;

            for (int i = 0; i < _order.Count; i++)
            {
                Body body = _order[i];
                ArticulationBody[] bodies = body.Instance.Bodies;
                if (bodies == null || bodies.Length == 0) continue;

                Vector3 root = bodies[0].transform.position;
                float horizontal = root.x + root.z;

                if (World.HeightIsInTheWorld(root.y, depth) &&
                    !float.IsNaN(horizontal) && !float.IsInfinity(horizontal))
                {
                    body.LastRootPosition = root;
                    continue;
                }

                HandleDivergence(i);
            }
        }

        /// <summary>
        /// Puts every body that has left the box back in at the opposite face — D077's third rule.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The whole articulation, by its root.</b> <c>ArticulationBody.TeleportRoot</c> moves
        /// the root and every part hanging off it in one call and leaves the solver's velocities
        /// alone, which is what a periodic boundary means: a creature swimming out of the x = 0
        /// face keeps swimming, in the same direction and at the same speed, having arrived at
        /// x = K·W. Setting <c>transform.position</c> instead would move one part out of its own
        /// articulation.
        /// </para>
        /// <para>
        /// One Transform read per creature per physics step, on top of the per-part reads
        /// <see cref="FluidEnvironment.Apply"/> already makes for drag — the cheapest place to
        /// notice, since a body that has left the box must not be integrated outside it even
        /// once, and the metabolic cadence is fifty steps too slow for that.
        /// </para>
        /// <para>
        /// ⚠ The rotation handed back is the one just read, unchanged. Reading it costs the same
        /// Transform access as the position, and <c>TeleportRoot</c> has no position-only
        /// overload.
        /// </para>
        /// </remarks>
        private void WrapAtTheSeams()
        {
            for (int i = 0; i < _order.Count; i++)
            {
                ArticulationBody[] bodies = _order[i].Instance.Bodies;
                if (bodies == null || bodies.Length == 0) continue;

                ArticulationBody root = bodies[0];
                Transform t = root.transform;

                if (Volume.TryWrap(t.position, out Vector3 wrapped)) root.TeleportRoot(wrapped, t.rotation);
            }
        }

        /// <summary>Records one diverged creature and kills it. Dump first, then the death.</summary>
        private void HandleDivergence(int index)
        {
            Body body = _order[index];
            Organism creature = body.Creature;

            // The dump before the kill, because World.KillDiverged empties the organism — and
            // because a file written after a throw is a file that does not exist.
            Dump(index, body, creature);

            Debug.LogWarning(
                $"Creature {creature.Id} diverged at t={World.ElapsedSeconds:0.#} s " +
                $"(physics step {Steps + 1}, {body.Instance.Bodies.Length} parts, " +
                $"{body.Instance.TotalDof} dof) — killed as a death, see the diverged/ dump.");

            World.KillDiverged(creature);
        }

        /// <summary>
        /// Writes one diverged creature's post-mortem: what it was, and the last state it held
        /// before the solver lost it.
        /// </summary>
        /// <remarks>
        /// <b>Best-effort, and never allowed to take the run down.</b> The run continues after a
        /// divergence — that is the whole point — and losing the record of one body is a smaller
        /// loss than losing the rest of the arm to an IO error while writing it.
        /// </remarks>
        private void Dump(int index, Body body, Organism creature)
        {
            if (string.IsNullOrEmpty(DivergenceDumpDirectory) || _dumpsWritten >= MaxDumps) return;

            try
            {
                CreatureInstance instance = body.Instance;
                ArticulationBody[] bodies = instance.Bodies;
                Phenotype phenotype = instance.Phenotype;

                var w = new Json.Writer(indent: true);
                w.BeginObject();

                w.Field("creatureId", creature.Id);
                w.Field("t", World.ElapsedSeconds);
                w.Field("physicsStep", Steps + 1);
                w.Field("physicsDtSeconds", FixedDt);
                w.Field("lastObservedHeightY", creature.HeightY);
                w.Field("generationDepth", creature.GenerationDepth);
                w.Field("ageSeconds", creature.Age);
                w.Field("parts", bodies.Length);
                w.Field("totalDof", instance.TotalDof);
                w.Field("jointed", instance.TotalDof > 0);

                // Where the body last was. One position for the creature rather than one per
                // part, because that is what the check already reads and a per-part copy cost
                // more than it was worth — and at the magnitudes a divergence reaches, a float
                // cannot tell the parts apart anyway: 3075's three parts shared one coordinate
                // to the last bit, 1.06e10 m out, where float resolution is about a kilometre.
                Vector(w, "lastRootPosition", body.LastRootPosition);

                w.BeginArray("partStates");

                for (int b = 0; b < bodies.Length; b++)
                {
                    ArticulationBody part = bodies[b];
                    PhenotypePart shape = b < phenotype.Parts.Count ? phenotype.Parts[b] : null;

                    w.BeginObject();
                    w.Field("index", b);
                    w.Field("name", part.name);
                    w.Field("parentIndex", shape != null ? shape.ParentIndex : -1);
                    w.Field("jointType", shape != null ? shape.JointType.ToString() : "unknown");
                    w.Field("cellTypeId", shape != null ? shape.CellTypeId : null);
                    Number(w, "power", shape != null ? shape.Power : 0f);
                    Number(w, "volumeM3", shape != null ? shape.Volume : 0f);
                    Number(w, "massKg", part.mass);

                    // The velocities the fluid gathered before the last physics step. Since the
                    // check runs at the metabolic cadence these can themselves be NaN — the body
                    // may have been gone for up to fifty steps by the time anyone looked — and
                    // that is recorded rather than hidden: Number writes a non-finite value as
                    // its own name. The last state known to be finite is lastRootPosition above,
                    // which the check itself keeps. The three fields at the end of this object
                    // are what the solver has now, read here and nowhere else in a run.
                    if (Fluid.TryLastVelocity(index, b, out Vector3 lastV, out Vector3 lastW))
                    {
                        Vector(w, "lastVelocity", lastV);
                        Vector(w, "lastAngularVelocity", lastW);
                        Number(w, "lastSpeed", lastV.magnitude);
                        Number(w, "lastSpinRate", lastW.magnitude);
                    }

                    // What the driver asked of this joint on the step that ended here. Zero for
                    // the root and for anything unjointed, which is an answer rather than a gap.
                    Vector(w, "driveTorque", body.Driver.AppliedTorque(b));

                    Vector(w, "position", part.transform.position);
                    Vector(w, "velocity", part.linearVelocity);
                    Vector(w, "angularVelocity", part.angularVelocity);
                    w.EndObject();
                }

                w.EndArray();

                // The genome last, because it is the long part and a reader opening this file
                // wants the numbers above it first. Written by GenomeJson, compact, as one line:
                // there is exactly one genome serialiser and this is not a second one.
                w.Raw("genome", GenomeJson.Write(creature.Genome));

                w.EndObject();

                Directory.CreateDirectory(DivergenceDumpDirectory);
                File.WriteAllText(
                    Path.Combine(DivergenceDumpDirectory, creature.Id + ".json"),
                    w.ToString(),
                    new UTF8Encoding(false));

                _dumpsWritten++;
            }
            catch (Exception e)
            {
                Debug.LogWarning("diverged dump not written: " + e.Message);
            }
        }

        /// <summary>One vector, as an object of three numbers.</summary>
        private static void Vector(Json.Writer w, string name, Vector3 v)
        {
            w.BeginObject(name);
            Number(w, "x", v.x);
            Number(w, "y", v.y);
            Number(w, "z", v.z);
            w.EndObject();
        }

        /// <summary>
        /// One number, non-finite included.
        /// </summary>
        /// <remarks>
        /// <see cref="Json.Writer"/> refuses NaN and infinity, correctly: JSON cannot represent
        /// either, and everywhere else in this project a non-finite number is a fault nobody
        /// noticed. This file is the exception — it is written <i>because</i> something stopped
        /// being finite, and a post-mortem that throws while recording the death records nothing.
        /// So the value goes in as its own name, in quotes, and the reader sees which slot it was
        /// rather than a missing key.
        /// </remarks>
        private static void Number(Json.Writer w, string name, float v)
        {
            if (float.IsNaN(v) || float.IsInfinity(v))
            {
                w.Field(name, v.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            else
            {
                w.Field(name, v);
            }
        }

        private void Metabolise()
        {
            // Before anything reads what the solver has been producing — the divergence spec,
            // after logbook/0056. A body whose state has stopped being finite, or whose root has
            // left the sea, is removed here as a death; if it were left, World.Observe would see the height and take
            // the run down, which is how r20q-s1 was censored at t=15,345 of 20,000 s. That
            // refusal in Observe stays exactly as it is: it is the guard for anything that gets
            // past this, and this runs first, so a diverged body never reaches it.
            //
            // At the metabolic cadence rather than every physics step, and the difference is
            // 16% of the wall clock of a five-thousand-creature world: reading one Transform per
            // creature costs about 120 ns, which is nothing 50 times a second and is a third of
            // an hour per run at 100 Hz. Nothing is lost by waiting. The check protects Observe,
            // Observe runs here, and a diverged body is therefore killed at exactly the instant
            // it used to end the run — r20q-s1 itself spent nine steps with NaN forces before
            // that instant arrived. What those steps cost is a burst of PhysX "force is not
            // valid" warnings for the one dying body, which is a fair description of what is
            // happening to it.
            CheckFinite();

            float seconds = StepsPerMetabolicStep * FixedDt;

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

                // The root, as CheckFinite left it a few lines ago — free, and the position D077's
                // rules are all denominated in.
                Vector3 root = body.LastRootPosition;
                if (root.y > 0f) above++;

                Volume?.Note(creature.Id, root, body.Radius);

                Vector3 centre = FluidEnvironment.CentreOfMass(body.Instance);

                // Unsigned, and drained per interval. EffectorDriver reports the magnitude of the
                // work at each joint precisely because a joint being driven *by* the water is doing
                // negative work at the actuator, and crediting that would pay a creature to be
                // pushed around — §11.2's free-energy failure, arriving through the ledger rather
                // than through the solver.
                double total = body.Driver.MechanicalWorkJoules;
                float interval = (float)System.Math.Max(0d, total - body.WorkAtLastStep);
                body.WorkAtLastStep = total;

                // The joint-torque cap's tally, taken on the pass that already has the driver in
                // hand. Drained rather than read, so the same bind cannot be counted twice.
                DriveImpulsesLimited += body.Driver.DrainImpulsesLimited();

                // D083: the whole centre, so a vertex field feeds the body where it is. The
                // cell field reads only the height, as it always did.
                World.Observe(creature, centre.ToFloat3(), interval);

                if (body.Settled)
                {
                    // D077. On a ring, across the shorter arc: a body translated at a seam has
                    // moved a patch-ring's width in one metabolic step, and differencing its raw
                    // coordinates reports that as a swim. Written as a branch rather than folded
                    // into one expression so the tiled world's arithmetic is the character-for-
                    // character expression every run on file was measured with.
                    double speed = (Volume != null
                        ? Volume.ShortestDistance(centre, body.PreviousCentre)
                        : Vector3.Distance(centre, body.PreviousCentre)) / seconds;
                    speedSum += speed;
                    counted++;
                    if (speed > fastest) fastest = speed;

                    // The motility instrument. CheckFinite read this position a few lines ago,
                    // so the whole cost is a subtraction and a branch per creature per metabolic
                    // step — one fiftieth of the physics rate.
                    double rootSpeed = (Volume != null
                        ? Volume.ShortestDistance(body.LastRootPosition, body.PreviousRoot)
                        : Vector3.Distance(body.LastRootPosition, body.PreviousRoot)) / seconds;

                    if (body.Instance.TotalDof > 0)
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
                // Free of any new Transform read: CheckFinite took LastRootPosition a few lines
                // ago and PreviousRoot is where this body stood when it was resized.
                if (body.ResizedLastStep)
                {
                    body.ResizedLastStep = false;

                    if (body.Settled)
                    {
                        double moved = Volume != null
                            ? Volume.ShortestDistance(body.LastRootPosition, body.PreviousRoot)
                            : Vector3.Distance(body.LastRootPosition, body.PreviousRoot);

                        if (moved > MaxResizeStepMetres) MaxResizeStepMetres = moved;
                    }
                }

                body.Settled = true;
                body.PreviousCentre = centre;
                body.PreviousRoot = body.LastRootPosition;
                work += interval;
            }

            MeanSpeed = counted > 0 ? speedSum / counted : 0d;
            MaxSpeed = fastest;
            WorkThisStep = work;
            AboveSurface = above;

            World.Step(seconds);

            // D066. After the world has stepped, because that is where a creature's patch changes
            // — D061's dispersal and D066's advection both move it, and the physics steps that
            // follow have to sample the water the creature is actually in. One dictionary lookup
            // per creature per metabolic step, which is one fiftieth of the physics rate, and only
            // in a world that has patches at all.
            if (Fluid.PatchCount > 1)
            {
                IReadOnlyList<Organism> after = World.Living;

                for (int i = 0; i < after.Count; i++)
                {
                    if (_bodies.TryGetValue(after[i].Id, out Body body)) body.Instance.Patch = after[i].Patch;
                }
            }

            // fable-propose-growth.md rule 8. After the world has stepped, because that is where
            // a body's size changes: Core moved the joules and scaled the phenotype a few lines
            // ago, and until this runs the creature has drag and lit area from its new size and
            // colliders and mass from its old one. Counted in simulated seconds, never against a
            // wall clock: a growth cadence that depended on how loaded the machine was would make
            // a run unreproducible from its own config.
            _sinceGrowthStep += seconds;

            float growthStep = World.Config.GrowthStepSeconds;
            if (_sinceGrowthStep + GrowthStepEpsilon >= growthStep)
            {
                _sinceGrowthStep = 0f;
                ApplyGrowth();
            }
        }

        /// <summary>
        /// Half a physics step, seconds. The slack the growth cadence is compared with.
        /// </summary>
        /// <remarks>
        /// The metabolic step is 0.5 s and the cadence is a float read from the config, so an
        /// accumulator compared exactly would sometimes take twenty-one steps to clear a
        /// ten-second cadence and sometimes twenty. That would be a growth rate that drifted
        /// against its own setting, which is the shape of fault this project has agreed means an
        /// instrument is not measuring what it says.
        /// </remarks>
        private const float GrowthStepEpsilon = 0.005f;

        private float _sinceGrowthStep;

        /// <summary>
        /// Puts every body that has grown since the last growth step at the size Core says it is,
        /// fable-propose-growth.md rule 8.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Walks the living rather than the body table.</b> Deaths have happened inside
        /// <c>World.Step</c> and <see cref="Reconcile"/> has not run yet, so the table still holds
        /// bodies whose creature is dead and about to be destroyed; resizing those would be work
        /// done on a corpse. Newborns are the other way round: they have no articulation until
        /// Reconcile builds one, and it is built from <c>creature.Phenotype</c>, which is already
        /// the scaled body, so a newborn needs no resize at all.
        /// </para>
        /// <para>
        /// <b>A creature at fraction 1 is resized exactly once.</b> The applied fraction catches
        /// up with the ledger's on the step it reaches its adult size, and the two agree from
        /// then on, so a grown population costs one float comparison per creature per growth step
        /// and nothing else.
        /// </para>
        /// </remarks>
        private void ApplyGrowth()
        {
            IReadOnlyList<Organism> living = World.Living;

            for (int i = 0; i < living.Count; i++)
            {
                Organism creature = living[i];
                if (!_bodies.TryGetValue(creature.Id, out Body body)) continue;
                if (creature.BodyFraction == body.AppliedBodyFraction) continue;

                ArticulationBody root = body.Instance.Bodies[0];
                Vector3 before = root.transform.position;

                PhenotypeBuilder.Resize(
                    body.Instance, creature.Phenotype, World.Config.Fluid, World.Config.Shapes);

                // Nothing has simulated between these two reads, so any distance here is the
                // engine having moved the body because its anchors moved. See MaxResizeJumpMetres.
                double jump = Vector3.Distance(before, root.transform.position);
                if (jump > MaxResizeJumpMetres) MaxResizeJumpMetres = jump;

                body.AppliedBodyFraction = creature.BodyFraction;
                body.ResizedLastStep = true;

                // The placer's picture of how much room this body needs, refreshed with the body.
                if (Volume != null) body.Radius = SharedVolume.BoundingRadius(creature.Phenotype);

                Resizes++;
            }
        }

        /// <summary>
        /// Gives every new organism a body and takes it away from every dead one.
        /// </summary>
        /// <remarks>
        /// Run before stepping rather than after the economy, so that a creature born on one
        /// metabolic step is being simulated for the whole of the next one rather than for all of
        /// it but the first stroke.
        /// </remarks>
        private void Reconcile()
        {
            // Every way a creature enters or leaves the economy, or the next inoculant would
            // have no body until the next birth or death happened to bump the count (the Astra
            // review, 2026-09-07). Zero in a run that never inoculates, so the sum is unchanged.
            long revision = World.Births + World.Deaths + World.FloorSpawns + World.Inoculated;
            if (revision == _reconciledAt) return;

            _reconciledAt = revision;

            IReadOnlyList<Organism> living = World.Living;

            _departed.Clear();
            foreach (KeyValuePair<long, Body> entry in _bodies) _departed.Add(entry.Key);

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

                // Last chance: the driver is about to be destroyed with the body, and the binds
                // it made since the last metabolic step would go with it.
                DriveImpulsesLimited += body.Driver.DrainImpulsesLimited();

                if (body.Tile >= 0) _freeTiles.Push(body.Tile);
                body.Instance.Destroy();
                _bodies.Remove(id);
            }

            // Rebuilt whether or not anything departed, because Build appends to these and a birth
            // alone leaves them correct but a death leaves them holding a destroyed instance. The
            // early return above is what keeps this off the hot path.
            _instances.Clear();
            _instanceIds.Clear();
            _order.Clear();

            for (int i = 0; i < living.Count; i++)
            {
                if (!_bodies.TryGetValue(living[i].Id, out Body body)) continue;

                _instances.Add(body.Instance);
                _instanceIds.Add(living[i].Id);
                _order.Add(body);
            }
        }

        private void Build(Organism creature)
        {
            // Tiled on a lattice rather than placed at the parent, because §6.3 keeps creatures
            // apart and two overlapping articulations would depenetrate — which is a force, and
            // one logbook/0007 measured a creature learning to farm.
            //
            // D077 replaces the lattice with the box: the spot was chosen and reserved when the
            // birth was decided, one metabolic step ago and with no Physics.Simulate since, so
            // nothing has moved into it. The lattice stays for the tiled world, which is every
            // run on file.
            int tile = -1;
            Vector3 origin;
            float radius = 0f;

            if (Volume != null)
            {
                radius = SharedVolume.BoundingRadius(creature.Phenotype);

                if (!Volume.TryTakePlacement(creature.Id, out origin))
                {
                    // A body nobody reserved a spot for. Within one shared-space run this cannot
                    // happen — every admission goes through the placer — so rather than invent a
                    // position, take a lattice slot far from the box and say so: an overlapping
                    // spawn is a force in the physics, and a silent one is worse than a loud
                    // creature standing a kilometre away.
                    tile = _freeTiles.Count > 0 ? _freeTiles.Pop() : _nextTile++;
                    origin = new Vector3(
                        (tile % 64) * TileSpacing, creature.HeightY, (tile / 64) * TileSpacing);

                    Debug.LogWarning(
                        $"Creature {creature.Id} was admitted into a shared volume with no " +
                        "reserved placement — built on the lattice instead.");
                }
            }
            else
            {
                tile = _freeTiles.Count > 0 ? _freeTiles.Pop() : _nextTile++;
                int side = 64;
                origin = new Vector3(
                    (tile % side) * TileSpacing, creature.HeightY, (tile / side) * TileSpacing);
            }

            CreatureInstance instance = PhenotypeBuilder.Build(
                creature.Phenotype, origin, _parent, World.Config.Shapes);

            // D066. So the first physics step after a birth samples the right roll leg rather than
            // patch 0's — Metabolise refreshes it from then on.
            instance.Patch = creature.Patch;

            Fluid.ApplyAddedMass(instance);

            Brain brain = Brain.For(creature.Phenotype);

            var body = new Body
            {
                Instance = instance,
                Creature = creature,
                Driver = new EffectorDriver(instance, FixedDt),
                Brain = brain,
                // The world it can perceive, and only the channels its own brain reads —
                // CreatureSensors' remarks on §4.4's requirement mask. The organism is handed
                // over as an IReserveSource rather than as itself, so that the one thing
                // perception needs from the account is the only thing it can reach.
                Sensors = new CreatureSensors(
                    instance, World.Config.WorldDepthMetres, World.Nutrients, creature,
                    brain.SensorMask, World.Config),
                Drive = new float[Mathf.Max(1, brain.TotalDof)],
                PreviousCentre = FluidEnvironment.CentreOfMass(instance),
                Tile = tile,
                Radius = radius,

                // fable-propose-growth.md rule 8. The articulation above was built from
                // creature.Phenotype, which Core has already scaled to this fraction, so the body
                // and the ledger start in agreement and the first resize is the first growth step
                // that actually changes something. Building from AdultPhenotype instead would
                // hand every newborn an adult's colliders and an adult's mass, which is the exact
                // mismatch rule 8 exists to close.
                AppliedBodyFraction = creature.BodyFraction,
            };

            // D077. Contact reporting is opt-in per collider and defaults off — without it PhysX
            // resolves a contact and tells nobody, which is how the spike's first contact-check
            // cell read zero pairs while its physics time rose six-fold (logbook/0064). Switched
            // on only where the crowd is real, so a tiled run pays nothing for an instrument that
            // would read zero anyway.
            if (Volume != null)
            {
                for (int b = 0; b < instance.Bodies.Length; b++)
                {
                    Collider collider = instance.Bodies[b].GetComponent<Collider>();
                    if (collider != null) collider.providesContacts = true;
                }
            }

            // The one silent failure in this wiring: Brain indexes drive by walking every part in
            // order, EffectorDriver indexes it through CreatureInstance.DofOffset, which skips the
            // root. They agree only because Developer forces the root's joint to Fixed. If they
            // ever stopped agreeing, every creature would drive the wrong joints and nothing would
            // throw (logbook/0007, logbook/0008). BrainTests holds the invariant; this catches a
            // build that got past it.
            if (brain.TotalDof != instance.TotalDof)
            {
                throw new System.InvalidOperationException(
                    $"Creature {creature.Id}: the brain produces {brain.TotalDof} drive values and " +
                    $"the articulation has {instance.TotalDof} degrees of freedom. The two DOF " +
                    "orderings have diverged and every joint would be driven by the wrong neuron.");
            }

            _bodies.Add(creature.Id, body);
            _instances.Add(instance);
            _instanceIds.Add(creature.Id);
            _order.Add(body);
        }

        public void DestroyAll()
        {
            if (_countingContacts)
            {
                Physics.ContactEvent -= OnContactEvent;
                _countingContacts = false;
            }

            foreach (KeyValuePair<long, Body> entry in _bodies) entry.Value.Instance.Destroy();

            // The bed goes with them. It is a GameObject in a scene that outlives this object —
            // the editor harnesses build several worlds in one process — and a leaked floor would
            // sit in the next world's water, colliding with it.
            Floor?.Destroy();

            // The digest's files close with the world that wrote them. Left null afterwards, so a
            // harness that builds a second world in the same process has to ask for it again
            // rather than inherit the first world's file handle.
            _digest?.Dispose();
            _digest = null;
            _digestBodies?.Dispose();
            _digestBodies = null;

            _bodies.Clear();
            _instances.Clear();
            _instanceIds.Clear();
            _order.Clear();
            _freeTiles.Clear();
            _nextTile = 0;
            _reconciledAt = -1;
        }
    }
}
