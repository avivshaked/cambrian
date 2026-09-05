using System.Collections.Generic;
using UnityEngine;
using Evosim.Core;

namespace Evosim.Sim
{
    /// <summary>
    /// What one creature can perceive — DESIGN.md §4.4. Implements the channels listed in
    /// <see cref="SensorChannels.Implemented"/>; everything else reads zero.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nothing implemented this interface until now, and the consequence was not a missing
    /// feature.</b> Founder genomes have always drawn sensor inputs, so roughly half of every
    /// neuron input in every creature ever run here evaluated to a constant zero. The brains were
    /// smaller than they looked. Worse, the world's whole ecology is a gradient in depth, and an
    /// open-loop swimmer cannot tell up from down — so locomotion earned nothing on average while
    /// costing real work, and the economy correctly deleted it (logbook/0018).
    /// </para>
    /// <para>
    /// <b>Sampled once per step, not read on demand.</b> Two neurons reading the same channel in
    /// the same step must see the same number: §4.3's synchronous update makes that true of neuron
    /// outputs, and it would be odd for perception to be the one place where evaluation order
    /// showed through. It is also much cheaper — a <c>Transform</c> read is not free, and §5A.9
    /// already measured a per-part per-step loop as the bottleneck.
    /// </para>
    /// <para>
    /// <b>Every channel here is a scalar at a part, and none reports a bearing.</b> A creature made
    /// of several parts reads the same channel in several places at once, and the difference is a
    /// direction — which is why §4.4 rejects a "direction to nearest food" channel rather than
    /// deferring it. Morphology is part of the sensory apparatus: a long body resolves a gradient
    /// better than a compact one, and a one-part creature resolves nothing at all.
    /// </para>
    /// <para>
    /// <b>What is computed is what some neuron reads</b> — §4.4's requirement mask, taken from
    /// <see cref="Brain.SensorMask"/> at birth. Until it existed every channel was computed for
    /// every part on every physics step whether or not anything referenced it, which is the
    /// promise §4.4 makes about cost and never kept. Skipping the computation of a value nobody
    /// reads cannot change a run: the mask is derived from the same inputs <see cref="Brain"/>
    /// evaluates, and an unread array slot is only ever written, never read.
    /// </para>
    /// </remarks>
    public sealed class CreatureSensors : ISensorField
    {
        /// <summary>
        /// Angular rate that reads as full scale, rad/s.
        /// </summary>
        /// <remarks>
        /// ⚠ Unmeasured (§5A.10). Normalisation has to divide by something, and the alternatives
        /// are worse: dividing by the joint's own range makes the reading depend on a limit that
        /// mutation moves, and dividing by rate×dt makes what a creature can perceive depend on
        /// the timestep, which is exactly the class of coupling §11.2 exists to keep out. A
        /// constant is honest about being a constant. Saturation is not a failure — a joint moving
        /// faster than this reads as "fast", which is all a tanh was going to say anyway.
        /// </remarks>
        public const float FullScaleRadPerSecond = 10f;

        /// <summary>
        /// The scales the three later channels are normalised against, when no run supplies its
        /// own. One instance rather than a <c>new RunConfig()</c> per creature.
        /// </summary>
        private static readonly RunConfig DefaultScales = new RunConfig();

        private readonly CreatureInstance _creature;
        private readonly float _worldDepthMetres;

        /// <summary>The field a nose smells — <c>World.Nutrients</c>, or null in a harness.</summary>
        private readonly NutrientField _nutrients;

        /// <summary>The creature's own account, for <see cref="SensorChannel.Energy"/>.</summary>
        private readonly IReserveSource _reserve;

        private readonly int _mask;

        private readonly float _chemicalHalfScale;
        private readonly float _energyFullScaleSeconds;
        private readonly float _flowFullScale;

        private readonly bool _readsDepth;
        private readonly bool _readsUp;
        private readonly bool _readsJoint;
        private readonly bool _readsChemical;
        private readonly bool _readsEnergy;
        private readonly bool _readsFlow;
        private readonly bool _readsAnythingPerPart;

        private readonly float[] _depth;
        private readonly float[] _up;
        private readonly float[] _angle;
        private readonly float[] _rate;
        private readonly float[] _chemical;
        private readonly float[] _flow;

        private float _energy;

        /// <summary>
        /// The pre-perception constructor: the four body channels only, every one of them computed.
        /// </summary>
        /// <remarks>
        /// Kept for the harnesses that have no world to sense — <c>SwimSurvey</c> drives creatures
        /// in a bare tank. A creature built this way answers <see cref="SensorChannel.Chemical"/>
        /// and <see cref="SensorChannel.Energy"/> with zero, and <see cref="SensorChannel.Flow"/>
        /// with zero, because there is nothing there to report.
        /// </remarks>
        public CreatureSensors(CreatureInstance creature, float worldDepthMetres)
            : this(creature, worldDepthMetres, null, null, Brain.AllSensorChannels, null)
        {
        }

        /// <param name="nutrients">
        /// The field <see cref="SensorChannel.Chemical"/> smells — <c>World.Nutrients</c>, the same
        /// one feeding prices against. Null reads zero.
        /// </param>
        /// <param name="reserve">
        /// The creature's own account, for <see cref="SensorChannel.Energy"/>. Null reads zero.
        /// </param>
        /// <param name="sensorMask">
        /// <see cref="Brain.SensorMask"/> — which channels any neuron in this creature references.
        /// <see cref="Brain.AllSensorChannels"/> computes everything, which is what a test wants
        /// and what a body whose brain reads everything gets anyway.
        /// </param>
        /// <param name="config">Where the three squash scales come from; null uses the defaults.</param>
        public CreatureSensors(
            CreatureInstance creature,
            float worldDepthMetres,
            NutrientField nutrients,
            IReserveSource reserve,
            int sensorMask,
            RunConfig config)
        {
            _creature = creature;
            _worldDepthMetres = Mathf.Max(0.001f, worldDepthMetres);
            _nutrients = nutrients;
            _reserve = reserve;
            _mask = sensorMask;

            RunConfig scales = config ?? DefaultScales;

            // Floored rather than trusted: a half-scale of zero would divide by the density
            // itself and read 1 in any water at all, which is a sensor that says nothing.
            _chemicalHalfScale = Mathf.Max(1e-6f, scales.ChemicalHalfScaleJoulesPerCubicMetre);
            _energyFullScaleSeconds = Mathf.Max(1e-6f, scales.EnergyFullScaleSeconds);
            _flowFullScale = Mathf.Max(1e-6f, scales.FlowFullScaleMetresPerSecond);

            _readsDepth = Brain.MaskReads(_mask, SensorChannel.Depth);
            _readsUp = Brain.MaskReads(_mask, SensorChannel.OrientationUp);
            _readsJoint = Brain.MaskReads(_mask, SensorChannel.JointAngle) ||
                          Brain.MaskReads(_mask, SensorChannel.JointAngularVelocity);
            _readsChemical = Brain.MaskReads(_mask, SensorChannel.Chemical) && _nutrients != null;
            _readsEnergy = Brain.MaskReads(_mask, SensorChannel.Energy) && _reserve != null;
            _readsFlow = Brain.MaskReads(_mask, SensorChannel.Flow);

            _readsAnythingPerPart =
                _readsDepth || _readsUp || _readsJoint || _readsChemical || _readsFlow;

            int parts = creature.Bodies.Length;
            _depth = new float[parts];
            _up = new float[parts];
            _chemical = new float[parts];
            _flow = new float[parts * 3];

            int dof = Mathf.Max(1, creature.TotalDof);
            _angle = new float[dof];
            _rate = new float[dof];

            // The drag pass fills this in place; asking for it is what makes it exist. See
            // CreatureInstance.RelativeVelocity for why the array lives on the creature.
            if (_readsFlow && creature.RelativeVelocity == null)
            {
                creature.RelativeVelocity = new Float3[parts];
            }
        }

        /// <summary>
        /// Reads the body. Call once per physics step, before <see cref="Brain.Step"/>.
        /// </summary>
        public void Sample()
        {
            // Once per creature rather than per part: SecondsOfReserve is a division over two
            // fields the metabolic step writes, so it changes fifty times more slowly than it is
            // read here. That is a cached quotient, not work invented — and reading it every
            // physics step is what makes hunger a continuous input rather than a staircase.
            if (_readsEnergy) _energy = Squash(_reserve.SecondsOfReserve);

            if (!_readsAnythingPerPart) return;

            ArticulationBody[] bodies = _creature.Bodies;
            IReadOnlyList<PhenotypePart> parts = _creature.Phenotype.Parts;
            Float3[] water = _creature.RelativeVelocity;

            for (int b = 0; b < bodies.Length; b++)
            {
                Transform t = bodies[b].transform;

                // Positive downward and clamped, so it reads as a fraction of the water column
                // rather than as a world coordinate. A creature above the surface or below the
                // floor is at the end of the scale, not off it.
                if (_readsDepth) _depth[b] = Mathf.Clamp01(-t.position.y / _worldDepthMetres);
                if (_readsUp) _up[b] = Vector3.Dot(t.up, Vector3.up);

                // Smell, at the part's own height and in the creature's own patch. The *edible*
                // density and not the field's own: what a mouth may draw is what a nose should
                // report, D055's refuge discount included, and a sensor that promised food inside
                // a refuge would be teaching every lineage to swim at a wall.
                //
                // Per part, because §4.4's whole argument is that the gradient is read by a body
                // rather than by a point — two parts at two depths give a difference, and the
                // difference is a direction. A root-only reading would throw that away.
                if (_readsChemical)
                {
                    int patch = _creature.Patch;
                    if (patch < 0 || patch >= _nutrients.PatchCount) patch = 0;

                    float density = _nutrients.EdibleDensityAt(t.position.y, patch);

                    // x / (x + k): 0 in empty water, ½ at the half-scale, and it never quite
                    // arrives at 1. A linear clamp would saturate in rich water, which is blind
                    // exactly where the food is — RunConfig.ChemicalHalfScaleJoulesPerCubicMetre.
                    _chemical[b] = density > 0f
                        ? density / (density + _chemicalHalfScale)
                        : 0f;
                }

                // The lateral line. Rotated into the part's own axes, because that is the frame
                // the design names and the only one in which "the water is pushing me sideways"
                // means the same thing to a body at any orientation.
                //
                // ⚠ One physics step stale, by design. Sample() runs before Fluid.Apply in
                // Ecosystem.Step, so this is the relative velocity the drag pass computed last
                // step, and a creature's first step of life reads zero. Everything else in this
                // method reads start-of-step state too, so Flow is consistent with the rest of
                // perception rather than an exception to it — and the alternative, reordering the
                // step so the fluid ran first, would change the drag pass and end the replay of
                // every run in the record. Recomputing the current here instead was rejected on
                // cost: a second CurrentField.VelocityAt per part per step for a number the
                // solver already has.
                if (_readsFlow)
                {
                    Vector3 local = water != null && b < water.Length
                        ? t.InverseTransformDirection(water[b].ToVector3())
                        : Vector3.zero;

                    int at = b * 3;
                    _flow[at] = Mathf.Clamp(local.x / _flowFullScale, -1f, 1f);
                    _flow[at + 1] = Mathf.Clamp(local.y / _flowFullScale, -1f, 1f);
                    _flow[at + 2] = Mathf.Clamp(local.z / _flowFullScale, -1f, 1f);
                }

                if (!_readsJoint) continue;

                int n = parts[b].JointType.DofCount();
                int offset = _creature.DofOffset[b];
                if (n == 0 || offset < 0) continue;

                ArticulationReducedSpace position = bodies[b].jointPosition;
                ArticulationReducedSpace velocity = bodies[b].jointVelocity;

                for (int d = 0; d < n; d++)
                {
                    if (d >= position.dofCount) break;

                    // Against the joint's own limit, so a hinge free to swing a little and one
                    // free to swing a lot both report "at the stop" as 1. The genome moves those
                    // limits, and a proprioceptor that rescaled itself with the anatomy is the
                    // one an animal has.
                    float limit = d < parts[b].JointLimits.Length
                        ? Mathf.Abs(parts[b].JointLimits[d].Y)
                        : 1f;

                    _angle[offset + d] = limit > 1e-6f
                        ? Mathf.Clamp(position[d] / limit, -1f, 1f)
                        : 0f;

                    _rate[offset + d] = Mathf.Clamp(
                        velocity[d] / FullScaleRadPerSecond, -1f, 1f);
                }
            }
        }

        /// <summary>
        /// Seconds of reserve, squashed onto (-1, 1] — <see cref="SensorChannel.Energy"/>.
        /// </summary>
        /// <remarks>
        /// <b>Infinity is answered before the tanh, not after it.</b> A creature at zero burn has
        /// infinite reserve, which is a real state and the safest one there is; letting it reach
        /// <see cref="Brain"/>'s NaN/Inf guard would report the safest creature in the world as
        /// indistinguishable from a dead input. It reads 1, explicitly.
        /// </remarks>
        private float Squash(float seconds)
        {
            if (float.IsNaN(seconds)) return 0f;
            if (float.IsPositiveInfinity(seconds)) return 1f;
            if (float.IsNegativeInfinity(seconds)) return -1f;

            return (float)System.Math.Tanh(seconds / _energyFullScaleSeconds);
        }

        /// <inheritdoc/>
        public float Read(int partIndex, SensorChannel channel, int index)
        {
            if (partIndex < 0 || partIndex >= _depth.Length) return 0f;

            switch (channel)
            {
                case SensorChannel.Depth:
                    return _depth[partIndex];

                case SensorChannel.OrientationUp:
                    return _up[partIndex];

                case SensorChannel.JointAngle:
                    return Dof(_angle, partIndex, index);

                case SensorChannel.JointAngularVelocity:
                    return Dof(_rate, partIndex, index);

                case SensorChannel.Chemical:
                    return _chemical[partIndex];

                // Whole-creature, and NeuronInput.Index is documented as ignored here for the
                // same reason §5A.6 kills the creature rather than the part: there is one
                // account, and every part of the body is spending out of it.
                case SensorChannel.Energy:
                    return _energy;

                case SensorChannel.Flow:
                    return index >= 0 && index < 3 ? _flow[partIndex * 3 + index] : 0f;

                // Contact and Damage arrive with predation (Milestone 3) and Photo is Milestone 6.
                // Reading zero is the defined answer for an unimplemented channel (§4.4) — which
                // is exactly why the run's sensor pool may not draw one: a dead input and an early
                // input are indistinguishable from inside the brain.
                default:
                    return 0f;
            }
        }

        private float Dof(float[] values, int partIndex, int index)
        {
            int n = _creature.Phenotype.Parts[partIndex].JointType.DofCount();
            int offset = _creature.DofOffset[partIndex];

            if (offset < 0 || index < 0 || index >= n) return 0f;

            int at = offset + index;
            return at < values.Length ? values[at] : 0f;
        }
    }
}
