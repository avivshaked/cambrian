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
            public int Tile;
        }

        public Ecosystem(RunConfig config, ulong seed = 1, Transform parent = null)
        {
            World = new World(config, seed);
            Fluid = new FluidEnvironment(config.Fluid, config.Shapes, config.Current);

            // D066. The same K the world's fields were built with — read once here rather than per
            // body per step, because HorizontalPatches cannot change during a run.
            Fluid.PatchCount = Mathf.Max(1, (int)config.HorizontalPatches);

            _parent = parent;
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
            Fluid.Settle(_instances);

            for (int i = 0; i < _order.Count; i++) _order[i].Driver.Settle();

            Steps++;

            if (Steps % StepsPerMetabolicStep != 0) return false;

            Metabolise();
            return true;
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
        /// Checks that every living body is still finite, and kills the ones that are not.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>One native read and one branch per creature, once per metabolic step.</b> The
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
            for (int i = 0; i < _order.Count; i++)
            {
                Body body = _order[i];
                ArticulationBody[] bodies = body.Instance.Bodies;
                if (bodies == null || bodies.Length == 0) continue;

                Vector3 root = bodies[0].transform.position;
                float sum = root.x + root.y + root.z;

                if (!float.IsNaN(sum) && !float.IsInfinity(sum))
                {
                    body.LastRootPosition = root;
                    continue;
                }

                HandleDivergence(i);
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
            // after logbook/0056. A body whose state has stopped being finite is removed here as
            // a death; if it were left, World.Observe would see the non-finite height and take
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

            IReadOnlyList<Organism> living = World.Living;

            for (int i = 0; i < living.Count; i++)
            {
                Organism creature = living[i];
                if (!_bodies.TryGetValue(creature.Id, out Body body)) continue;

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

                World.Observe(creature, centre.y, interval);

                if (body.Settled)
                {
                    double speed = Vector3.Distance(centre, body.PreviousCentre) / seconds;
                    speedSum += speed;
                    counted++;
                    if (speed > fastest) fastest = speed;

                    // The motility instrument. CheckFinite read this position a few lines ago,
                    // so the whole cost is a subtraction and a branch per creature per metabolic
                    // step — one fiftieth of the physics rate.
                    double rootSpeed =
                        Vector3.Distance(body.LastRootPosition, body.PreviousRoot) / seconds;

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

                body.Settled = true;
                body.PreviousCentre = centre;
                body.PreviousRoot = body.LastRootPosition;
                work += interval;
            }

            MeanSpeed = counted > 0 ? speedSum / counted : 0d;
            MaxSpeed = fastest;
            WorkThisStep = work;

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
            long revision = World.Births + World.Deaths + World.FloorSpawns;
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

                _freeTiles.Push(body.Tile);
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
            int tile = _freeTiles.Count > 0 ? _freeTiles.Pop() : _nextTile++;
            int side = 64;
            var origin = new Vector3(
                (tile % side) * TileSpacing, creature.HeightY, (tile / side) * TileSpacing);

            CreatureInstance instance = PhenotypeBuilder.Build(
                creature.Phenotype, origin, _parent, World.Config.Shapes);

            // D066. So the first physics step after a birth samples the right roll leg rather than
            // patch 0's — Metabolise refreshes it from then on.
            instance.Patch = creature.Patch;

            Fluid.ApplyAddedMass(instance);

            Brain brain = Brain.For(creature.Phenotype, creature.Genome.GlobalBrain);

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
            };

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
            foreach (KeyValuePair<long, Body> entry in _bodies) entry.Value.Instance.Destroy();

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
