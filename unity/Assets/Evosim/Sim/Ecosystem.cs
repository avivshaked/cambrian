using System.Collections.Generic;
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
        /// <summary>Physics timestep. In the config hash (§7).</summary>
        public const float FixedDt = 0.01f;

        /// <summary>
        /// Physics steps per metabolic step. 50, so the economy runs at 2 Hz against physics' 100.
        /// </summary>
        /// <remarks>
        /// Energy is an integral, so a coarser metabolic clock changes only its quantisation and
        /// not its value — unlike a coarser <i>physics</i> clock, which changes what is physically
        /// possible and hands free energy to anything that finds it (§11.2). The two are not the
        /// same kind of approximation and only one of them is safe to take.
        /// </remarks>
        public const int StepsPerMetabolicStep = 50;

        /// <summary>Metres between tiled creatures — §6.3.</summary>
        public const float TileSpacing = 100f;

        public World World { get; }
        public FluidEnvironment Fluid { get; }

        /// <summary>Physics steps taken. Simulated seconds is this times <see cref="FixedDt"/>.</summary>
        public long Steps { get; private set; }

        // ---- instrumentation (see the remarks on Report)

        /// <summary>Mean speed of every living creature's centre of mass, m/s, this metabolic step.</summary>
        public double MeanSpeed { get; private set; }

        /// <summary>Joules the population's joints did this metabolic step.</summary>
        public double WorkThisStep { get; private set; }

        /// <summary>Joules drag took out of the population, over the run.</summary>
        public double DissipatedJoules => Fluid.DissipatedJoules;

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

        private readonly List<long> _departed = new List<long>();
        private readonly Transform _parent;
        private int _nextTile;

        /// <summary>One creature's physical presence, and the bookkeeping the join needs.</summary>
        private sealed class Body
        {
            public CreatureInstance Instance;
            public EffectorDriver Driver;
            public float[] Scratch;

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
        }

        public Ecosystem(RunConfig config, ulong seed = 1, Transform parent = null)
        {
            World = new World(config, seed);
            Fluid = new FluidEnvironment(config.Fluid, config.Shapes);
            _parent = parent;
        }

        /// <summary>
        /// Advances physics one step, and the economy once every
        /// <see cref="StepsPerMetabolicStep"/>. Returns true on the steps the economy ran.
        /// </summary>
        public bool Step()
        {
            Reconcile();

            for (int i = 0; i < _instanceIds.Count; i++)
            {
                Body body = _bodies[_instanceIds[i]];
                body.Driver.DriveTestSine(Steps * FixedDt, TestSineHz, body.Scratch);
            }

            Fluid.Apply(_instances, FixedDt);
            Physics.Simulate(FixedDt);
            Fluid.Settle(_instances);

            for (int i = 0; i < _instanceIds.Count; i++) _bodies[_instanceIds[i]].Driver.Settle();

            Steps++;

            if (Steps % StepsPerMetabolicStep != 0) return false;

            Metabolise();
            return true;
        }

        /// <summary>Drive frequency until the brain graph exists (Milestone 6).</summary>
        public float TestSineHz { get; set; } = 1.2f;

        private void Metabolise()
        {
            float seconds = StepsPerMetabolicStep * FixedDt;

            double speedSum = 0d;
            double work = 0d;

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

                World.Observe(creature, centre.y, interval);

                speedSum += Vector3.Distance(centre, body.PreviousCentre) / seconds;
                body.PreviousCentre = centre;
                work += interval;
            }

            MeanSpeed = living.Count > 0 ? speedSum / living.Count : 0d;
            WorkThisStep = work;

            World.Step(seconds);
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

            if (_departed.Count == 0) return;

            for (int i = 0; i < _departed.Count; i++)
            {
                Body body = _bodies[_departed[i]];
                body.Instance.Destroy();
                _bodies.Remove(_departed[i]);
            }

            _instances.Clear();
            _instanceIds.Clear();
            for (int i = 0; i < living.Count; i++)
            {
                if (!_bodies.TryGetValue(living[i].Id, out Body body)) continue;

                _instances.Add(body.Instance);
                _instanceIds.Add(living[i].Id);
            }
        }

        private void Build(Organism creature)
        {
            // Tiled on a lattice rather than placed at the parent, because §6.3 keeps creatures
            // apart and two overlapping articulations would depenetrate — which is a force, and
            // one logbook/0007 measured a creature learning to farm.
            int tile = _nextTile++;
            int side = 64;
            var origin = new Vector3(
                (tile % side) * TileSpacing, creature.HeightY, (tile / side) * TileSpacing);

            CreatureInstance instance = PhenotypeBuilder.Build(
                creature.Phenotype, origin, _parent, World.Config.Shapes);

            Fluid.ApplyAddedMass(instance);

            var body = new Body
            {
                Instance = instance,
                Driver = new EffectorDriver(instance, FixedDt),
                Scratch = new float[Mathf.Max(1, instance.TotalDof)],
                PreviousCentre = FluidEnvironment.CentreOfMass(instance),
            };

            _bodies.Add(creature.Id, body);
            _instances.Add(instance);
            _instanceIds.Add(creature.Id);
        }

        public void DestroyAll()
        {
            foreach (KeyValuePair<long, Body> entry in _bodies) entry.Value.Instance.Destroy();

            _bodies.Clear();
            _instances.Clear();
            _instanceIds.Clear();
        }
    }
}
