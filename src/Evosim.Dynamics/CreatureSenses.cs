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
    /// <b><see cref="SensorChannel.Chemical"/> and <see cref="SensorChannel.Energy"/> read the
    /// world once they are given one</b> (work package G). <see cref="Nutrients"/> and
    /// <see cref="Reserve"/> are the farm's <c>World.Nutrients</c> and the creature's own
    /// <c>Organism</c> as an <see cref="IReserveSource"/>, wired after the body is built because
    /// the body is built before the organism has a place; with both set, the two channels are
    /// <c>Evosim.Sim.CreatureSensors</c>'s term for term — the edible density at each part's own
    /// position through <c>x/(x+k)</c>, and the whole body's seconds of reserve through a tanh.
    /// Left unset — a bench, a solver test, a world that does not exist —
    /// <see cref="SolverConfig.ConstantChemicalAndEnergy"/> stands in, and it is not zero on
    /// purpose: zero is what an <i>unimplemented</i> channel reads in the farm, and a brain that
    /// happened to read one would be driven by a different number here than in a parity run.
    /// </para>
    /// <para>
    /// <b>Both reads happen inside the parallel region, and both are of state nothing writes
    /// during a physics step.</b> The field is written only by <c>World.Step</c> and by the
    /// deposit and transport passes inside it, all of which run serially between physics steps;
    /// <c>GridField.EdibleDensityAt</c> is a pure read of <c>_stock</c> through a cell index and
    /// touches no cache and no <c>_frozen</c> flag. <c>Organism.SecondsOfReserve</c> is a
    /// division over two floats the metabolic step writes, likewise between steps. So neither is
    /// a shared write and neither can make the trajectory depend on the thread count.
    /// </para>
    /// <para>
    /// <b>A part that is not finite reads as an absence.</b> The farm's own note: a NaN position
    /// reaching <c>GridField</c> is refused — correctly — and that refusal ended <c>r35old-s3</c>
    /// with the whole process. Such a body is already gone and the harness kills it at the next
    /// metabolic step; what the skip buys is that the world is not taken down with it.
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
        private readonly bool _readsChemical;
        private readonly bool _readsEnergy;

        private readonly float[] _depth;
        private readonly float[] _up;
        private readonly float[] _angle;
        private readonly float[] _rate;
        private readonly float[] _flow;
        private readonly float[] _chemical;
        private float _energy;

        private readonly float _worldDepthMetres;
        private readonly float _chemicalHalfScale;
        private readonly float _energyFullScaleSeconds;

        /// <summary>
        /// The field <see cref="SensorChannel.Chemical"/> smells — <c>World.Nutrients</c>. Null
        /// falls back to <see cref="SolverConfig.ConstantChemicalAndEnergy"/>.
        /// </summary>
        /// <remarks>
        /// Settable rather than a constructor argument because a <see cref="Creature"/> is built
        /// before its organism has been placed, and because the bench and the solver tests build
        /// bodies in worlds that have no field at all.
        /// </remarks>
        public IMatterField Nutrients { get; set; }

        /// <summary>
        /// The creature's own account, for <see cref="SensorChannel.Energy"/> — the farm's
        /// <c>Organism</c>, handed over as an <see cref="IReserveSource"/> so that the one thing
        /// perception needs from the ledger is the only thing it can reach. Null falls back to the
        /// constant.
        /// </summary>
        public IReserveSource Reserve { get; set; }

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
            _readsChemical = Brain.MaskReads(_mask, SensorChannel.Chemical);
            _readsEnergy = Brain.MaskReads(_mask, SensorChannel.Energy);

            _worldDepthMetres = (float)System.Math.Max(0.001, config.WorldDepthMetres);
            _chemicalHalfScale =
                (float)System.Math.Max(1e-6, config.ChemicalHalfScaleJoulesPerCubicMetre);
            _energyFullScaleSeconds =
                (float)System.Math.Max(1e-6, config.EnergyFullScaleSeconds);

            _depth = new float[body.Links];
            _up = new float[body.Links];
            _flow = new float[body.Links * 3];
            _chemical = new float[body.Links];

            int dof = body.Dof > 0 ? body.Dof : 1;
            _angle = new float[dof];
            _rate = new float[dof];
        }

        /// <summary>Reads the body. Call once per step, before <c>Brain.Step</c>.</summary>
        public void Sample()
        {
            // Once per creature rather than per part, as the farm takes it: SecondsOfReserve is a
            // division over two fields the metabolic step writes, so it changes fifty times more
            // slowly than it is read here.
            if (_readsEnergy && Reserve != null) _energy = Squash(Reserve.SecondsOfReserve);

            bool smells = _readsChemical && Nutrients != null;

            if (!_readsDepth && !_readsUp && !_readsJoint && !_readsFlow && !smells) return;

            float flowScale = (float)System.Math.Max(1e-6, _config.FlowFullScaleMetresPerSecond);
            float rateScale = (float)System.Math.Max(1e-6, _config.JointRateFullScale);

            int patch = _body.Patch;
            if (smells && (patch < 0 || patch >= Nutrients.PatchCount)) patch = 0;

            for (int b = 0; b < _body.Links; b++)
            {
                double px = _body.Position[3 * b];
                double py = _body.Position[3 * b + 1];
                double pz = _body.Position[3 * b + 2];

                // The farm's second line, transcribed. A part that is not finite reads as an
                // absence rather than as a number — see the class remarks.
                double finite = px + py + pz;
                if (double.IsNaN(finite) || double.IsInfinity(finite))
                {
                    _depth[b] = 0f;
                    _up[b] = 0f;
                    _chemical[b] = 0f;

                    if (_readsFlow)
                    {
                        int gone = b * 3;
                        _flow[gone] = 0f;
                        _flow[gone + 1] = 0f;
                        _flow[gone + 2] = 0f;
                    }

                    continue;
                }

                if (_readsDepth)
                {
                    double d = -py / _worldDepthMetres;
                    _depth[b] = (float)(d < 0 ? 0 : d > 1 ? 1 : d);
                }

                if (_readsUp)
                {
                    // dot(this part's own up axis, world up) — the (1,1) entry of the rotation.
                    _up[b] = (float)_body.RotationMatrix[9 * b + 4];
                }

                if (smells)
                {
                    // The *edible* density and not the field's own, at the part's own position
                    // and in the creature's own patch: what a mouth may draw is what a nose
                    // should report, D055's refuge discount included. x / (x + k), so it is 0 in
                    // empty water, half at the half-scale, and never quite 1.
                    float density = Nutrients.EdibleDensityAt(
                        new FieldPoint(new Float3((float)px, (float)py, (float)pz), patch));

                    _chemical[b] = density > 0f ? density / (density + _chemicalHalfScale) : 0f;
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

                // The world, once the body has been given one; the standing-in constant
                // otherwise, which is not the zero an unimplemented channel reads in the farm.
                // See the class remarks.
                case SensorChannel.Chemical:
                    return Nutrients != null
                        ? _chemical[partIndex]
                        : _config.ConstantChemicalAndEnergy;

                // Whole-creature, and NeuronInput.Index is ignored here for the reason §5A.6
                // kills the creature rather than the part: there is one account, and every part
                // of the body is spending out of it.
                case SensorChannel.Energy:
                    return Reserve != null ? _energy : _config.ConstantChemicalAndEnergy;

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

        /// <summary>
        /// Seconds of reserve, squashed onto (−1, 1] — the farm's <c>Squash</c>, transcribed.
        /// </summary>
        /// <remarks>
        /// <b>Infinity is answered before the tanh, not after it.</b> A creature at zero burn has
        /// infinite reserve, which is a real state and the safest one there is; letting it reach
        /// <see cref="Brain"/>'s NaN/Inf guard would report the safest creature in the world as
        /// indistinguishable from a dead input.
        /// </remarks>
        private float Squash(float seconds)
        {
            if (float.IsNaN(seconds)) return 0f;
            if (float.IsPositiveInfinity(seconds)) return 1f;
            if (float.IsNegativeInfinity(seconds)) return -1f;

            return (float)System.Math.Tanh(seconds / _energyFullScaleSeconds);
        }
    }
}
