using Evosim.Core;

namespace Evosim.Dynamics
{
    /// <summary>
    /// What one creature perceives, ported from <c>Evosim.Sim.CreatureSensors</c>: the channels
    /// the spike implements are <see cref="SensorChannel.JointAngle"/>,
    /// <see cref="SensorChannel.JointAngularVelocity"/>,
    /// <see cref="SensorChannel.OrientationUp"/>, <see cref="SensorChannel.Depth"/> and
    /// <see cref="SensorChannel.Flow"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><see cref="SensorChannel.Chemical"/> and <see cref="SensorChannel.Energy"/> read a
    /// constant.</b> The spike has no field and no ledger (the spec's "what it is not"), and a
    /// constant is not zero on purpose: zero is what an <i>unimplemented</i> channel reads in the
    /// farm, and a brain that happened to read one would then be driven by a different number
    /// here than in a parity run. A constant 0.5 makes the two comparable in shape while being
    /// obviously not a measurement.
    /// </para>
    /// <para>
    /// <b>Sampled once per step, before the brain.</b> Two neurons reading the same channel in
    /// the same step must see the same number, which is the farm's rule and the reason the
    /// channels are arrays rather than accessors.
    /// </para>
    /// <para>
    /// <b>Flow is one step stale here too.</b> It is filled by the previous step's drag pass,
    /// which is the farm's ordering exactly: <c>Sample</c> runs before <c>Fluid.Apply</c>.
    /// </para>
    /// </remarks>
    public sealed class CreatureSenses : ISensorField
    {
        private readonly Creature _body;
        private readonly SolverConfig _config;
        private readonly int _mask;

        private readonly bool _readsDepth;
        private readonly bool _readsUp;
        private readonly bool _readsJoint;
        private readonly bool _readsFlow;

        private readonly float[] _depth;
        private readonly float[] _up;
        private readonly float[] _angle;
        private readonly float[] _rate;
        private readonly float[] _flow;

        private readonly float _worldDepthMetres;

        public CreatureSenses(Creature body, SolverConfig config)
        {
            _body = body;
            _config = config;
            _mask = body.Brain.SensorMask;

            _readsDepth = Brain.MaskReads(_mask, SensorChannel.Depth);
            _readsUp = Brain.MaskReads(_mask, SensorChannel.OrientationUp);
            _readsJoint = Brain.MaskReads(_mask, SensorChannel.JointAngle) ||
                          Brain.MaskReads(_mask, SensorChannel.JointAngularVelocity);
            _readsFlow = Brain.MaskReads(_mask, SensorChannel.Flow);

            _worldDepthMetres = (float)System.Math.Max(0.001, config.WorldDepthMetres);

            _depth = new float[body.Links];
            _up = new float[body.Links];
            _flow = new float[body.Links * 3];

            int dof = body.Dof > 0 ? body.Dof : 1;
            _angle = new float[dof];
            _rate = new float[dof];
        }

        /// <summary>Reads the body. Call once per step, before <c>Brain.Step</c>.</summary>
        public void Sample()
        {
            if (!_readsDepth && !_readsUp && !_readsJoint && !_readsFlow) return;

            float flowScale = (float)System.Math.Max(1e-6, _config.FlowFullScaleMetresPerSecond);
            float rateScale = (float)System.Math.Max(1e-6, _config.JointRateFullScale);

            for (int b = 0; b < _body.Links; b++)
            {
                if (_readsDepth)
                {
                    double d = -_body.Position[3 * b + 1] / _worldDepthMetres;
                    _depth[b] = (float)(d < 0 ? 0 : d > 1 ? 1 : d);
                }

                if (_readsUp)
                {
                    // dot(this part's own up axis, world up) — the (1,1) entry of the rotation.
                    _up[b] = (float)_body.RotationMatrix[9 * b + 4];
                }

                if (_readsFlow)
                {
                    Mat3 rotation = Mat3.Read(_body.RotationMatrix, 9 * b);
                    Vec3 local = rotation.TransposedTimes(
                        Vec3.Read(_body.RelativeVelocity, 3 * b));

                    int at = b * 3;
                    _flow[at] = Clamp((float)(local.X / flowScale));
                    _flow[at + 1] = Clamp((float)(local.Y / flowScale));
                    _flow[at + 2] = Clamp((float)(local.Z / flowScale));
                }

                if (!_readsJoint) continue;

                int n = _body.DofCount[b];
                int offset = _body.DofStart[b];
                if (n == 0 || offset < 0) continue;

                for (int d = 0; d < n; d++)
                {
                    // Against the joint's own upper limit, so a hinge free to swing a little and
                    // one free to swing a lot both report "at the stop" as 1.
                    double limit = System.Math.Abs(_body.LimitHi[offset + d]);

                    _angle[offset + d] = limit > 1e-6
                        ? Clamp((float)(_body.Q[offset + d] / limit))
                        : 0f;

                    _rate[offset + d] = Clamp((float)(_body.Qd[offset + d] / rateScale));
                }
            }
        }

        public float Read(int partIndex, SensorChannel channel, int index)
        {
            if (partIndex < 0 || partIndex >= _depth.Length) return 0f;

            switch (channel)
            {
                case SensorChannel.Depth: return _depth[partIndex];
                case SensorChannel.OrientationUp: return _up[partIndex];
                case SensorChannel.JointAngle: return Dof(_angle, partIndex, index);
                case SensorChannel.JointAngularVelocity: return Dof(_rate, partIndex, index);

                // Not modelled by the spike; a constant rather than the zero an unimplemented
                // channel reads in the farm. See the class remarks.
                case SensorChannel.Chemical:
                case SensorChannel.Energy:
                    return _config.ConstantChemicalAndEnergy;

                case SensorChannel.Flow:
                    return index >= 0 && index < 3 ? _flow[partIndex * 3 + index] : 0f;

                default: return 0f;
            }
        }

        private float Dof(float[] values, int partIndex, int index)
        {
            int n = _body.DofCount[partIndex];
            int offset = _body.DofStart[partIndex];

            if (offset < 0 || index < 0 || index >= n) return 0f;

            int at = offset + index;
            return at < values.Length ? values[at] : 0f;
        }

        private static float Clamp(float v) => v < -1f ? -1f : v > 1f ? 1f : v;
    }
}
