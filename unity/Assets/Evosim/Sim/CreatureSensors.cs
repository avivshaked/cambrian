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

        private readonly CreatureInstance _creature;
        private readonly float _worldDepthMetres;

        private readonly float[] _depth;
        private readonly float[] _up;
        private readonly float[] _angle;
        private readonly float[] _rate;

        public CreatureSensors(CreatureInstance creature, float worldDepthMetres)
        {
            _creature = creature;
            _worldDepthMetres = Mathf.Max(0.001f, worldDepthMetres);

            int parts = creature.Bodies.Length;
            _depth = new float[parts];
            _up = new float[parts];

            int dof = Mathf.Max(1, creature.TotalDof);
            _angle = new float[dof];
            _rate = new float[dof];
        }

        /// <summary>
        /// Reads the body. Call once per physics step, before <see cref="Brain.Step"/>.
        /// </summary>
        public void Sample()
        {
            ArticulationBody[] bodies = _creature.Bodies;
            IReadOnlyList<PhenotypePart> parts = _creature.Phenotype.Parts;

            for (int b = 0; b < bodies.Length; b++)
            {
                Transform t = bodies[b].transform;

                // Positive downward and clamped, so it reads as a fraction of the water column
                // rather than as a world coordinate. A creature above the surface or below the
                // floor is at the end of the scale, not off it.
                _depth[b] = Mathf.Clamp01(-t.position.y / _worldDepthMetres);
                _up[b] = Vector3.Dot(t.up, Vector3.up);

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

                // Contact and Damage arrive with predation (Milestone 3); Chemical needs the
                // nutrient field sampled at a world position, Energy needs the organism's reserve
                // carried across the seam, and Photo is Milestone 6. Reading zero is the defined
                // answer for an unimplemented channel (§4.4) — which is exactly why mutation may
                // not introduce one: a dead input and an early input are indistinguishable from
                // inside the brain.
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
