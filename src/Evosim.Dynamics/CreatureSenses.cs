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
        private readonly bool _readsScent;
        private readonly bool _readsEnergy;
        private readonly bool _readsDrive;
        private readonly bool _readsRock;
        private readonly float[] _rock;
        private readonly bool _readsLeaf;
        private readonly bool _readsLeafDirection;
        private readonly float[] _leaf;
        private readonly float[] _leafDirection;
        private readonly float _leafHalfScale;

        private readonly float[] _depth;
        private readonly float[] _up;
        private readonly float[] _angle;
        private readonly float[] _rate;
        private readonly float[] _stroke;
        private readonly float[] _hold;
        private readonly float[] _ramp;
        private readonly float[] _flow;
        private readonly float[] _chemical;
        private readonly float[] _scent;
        private readonly float _scentHalfScale;
        private float _energy;

        private readonly float _worldDepthMetres;
        private readonly float _chemicalHalfScale;
        private readonly float _energyFullScaleSeconds;
        private readonly EnergySenseSquash _energySquash;
        private readonly float _energyHalfScaleSeconds;

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
        /// The field <see cref="SensorChannel.Scent"/> reads — <c>World.Scent</c>. Null reads 0,
        /// the smell of no corpse, which is a real state and not a stand-in.
        /// </summary>
        public ScentField Scent { get; set; }

        /// <summary>
        /// The field <see cref="SensorChannel.LeafScent"/> and
        /// <see cref="SensorChannel.LeafScentDirection"/> read — <c>World.LeafScent</c> (D167).
        /// Null reads 0, the smell of no living leaf.
        /// </summary>
        public ScentField LeafScent { get; set; }

        /// <summary>
        /// The creature's own account, for <see cref="SensorChannel.Energy"/> — the farm's
        /// <c>Organism</c>, handed over as an <see cref="IReserveSource"/> so that the one thing
        /// perception needs from the ledger is the only thing it can reach. Null falls back to the
        /// constant.
        /// </summary>
        public IReserveSource Reserve { get; set; }

        /// <summary>
        /// Whether each part was touching another body at the last metabolic step — what
        /// <see cref="SensorChannel.Contact"/> reports, D106 item 5. Null reads 0 everywhere.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Handed over rather than sampled, and that is the only way round it could work.</b>
        /// Who is touching whom is settled by the overlap census on the main thread between steps
        /// and applied by Core's mouth pass at the metabolic step; this array is
        /// <c>Organism.PartContact</c>, set on the body by the farm on the step that computed it.
        /// A creature sampling its own contacts inside the parallel region would be asking about
        /// other bodies while they are being stepped, which is the one thing the solver's
        /// thread-count identity rests on not happening.
        /// </para>
        /// <para>
        /// <b>One metabolic step stale at the brain</b>, which is the staleness
        /// <see cref="SensorChannel.Flow"/> already has and is the honest latency of a nerve. See
        /// the class remarks.
        /// </para>
        /// </remarks>
        public bool[] Contact { get; set; }

        /// <summary>
        /// The share of its own health pool each part lost at the last metabolic step — what
        /// <see cref="SensorChannel.Damage"/> reports. Null reads 0 everywhere. See
        /// <see cref="Contact"/>.
        /// </summary>
        public float[] Damage { get; set; }

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
            _readsScent = Brain.MaskReads(_mask, SensorChannel.Scent);
            _readsEnergy = Brain.MaskReads(_mask, SensorChannel.Energy);
            _readsDrive = Brain.MaskReads(_mask, SensorChannel.Stroke) ||
                          Brain.MaskReads(_mask, SensorChannel.Hold) ||
                          Brain.MaskReads(_mask, SensorChannel.Ramp);
            _readsRock = Brain.MaskReads(_mask, SensorChannel.BedTouch);
            _rock = new float[body.Links];
            _readsLeaf = Brain.MaskReads(_mask, SensorChannel.LeafScent);
            _readsLeafDirection = Brain.MaskReads(_mask, SensorChannel.LeafScentDirection);
            _leaf = new float[body.Links];
            _leafDirection = new float[3 * body.Links];
            _leafHalfScale = (float)System.Math.Max(1e-6, config.LeafScentHalfScaleSquareMetres);

            _worldDepthMetres = (float)System.Math.Max(0.001, config.WorldDepthMetres);
            _chemicalHalfScale =
                (float)System.Math.Max(1e-6, config.ChemicalHalfScaleJoulesPerCubicMetre);
            _energyFullScaleSeconds =
                (float)System.Math.Max(1e-6, config.EnergyFullScaleSeconds);
            _energySquash = config.EnergySenseSquash;
            _energyHalfScaleSeconds =
                (float)System.Math.Max(1e-6, config.EnergyHalfScaleSeconds);

            _depth = new float[body.Links];
            _up = new float[body.Links];
            _flow = new float[body.Links * 3];
            _chemical = new float[body.Links];
            _scent = new float[body.Links];
            _scentHalfScale = (float)System.Math.Max(1e-6, config.ScentHalfScaleJoules);

            int dof = body.Dof > 0 ? body.Dof : 1;
            _angle = new float[dof];
            _rate = new float[dof];
            _stroke = new float[dof];
            _hold = new float[dof];
            _ramp = new float[dof];
        }

        /// <summary>Reads the body. Call once per step, before <c>Brain.Step</c>.</summary>
        public void Sample()
        {
            // Once per creature rather than per part, as the farm takes it: SecondsOfReserve is a
            // division over two fields the metabolic step writes, so it changes fifty times more
            // slowly than it is read here.
            if (_readsEnergy && Reserve != null) _energy = Squash(Reserve.SecondsOfReserve);

            // The touch of the rock as the last step's contact pass left it, so one step stale. A
            // world without the record reads 0, as an unimplemented channel does.
            if (_readsRock)
            {
                bool[] touched = _body.RockTouched;
                for (int b = 0; b < _body.Links; b++)
                {
                    _rock[b] = touched != null && touched[b] ? 1f : 0f;
                }
            }

            bool smells = _readsChemical && Nutrients != null;
            bool scents = _readsScent && Scent != null;
            bool leaves = (_readsLeaf || _readsLeafDirection) && LeafScent != null;

            if (!_readsDepth && !_readsUp && !_readsJoint && !_readsFlow && !smells && !scents && !_readsDrive &&
                !leaves) return;

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
                    _scent[b] = 0f;
                    _leaf[b] = 0f;
                    _leafDirection[3 * b] = 0f;
                    _leafDirection[3 * b + 1] = 0f;
                    _leafDirection[3 * b + 2] = 0f;

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

                if (scents)
                {
                    float s = Scent.At((float)px, (float)py, (float)pz);
                    _scent[b] = s > 0f ? s / (s + _scentHalfScale) : 0f;
                }

                if (leaves) SampleLeaf(b, (float)px, (float)py, (float)pz);

                if (_readsFlow)
                {
                    Mat3 rotation = Mat3.Read(_body.RotationMatrix, 9 * b);
                    // D234: a reflected part reads the flow in the frame its geometry stands in, the component
                    // along the reflection's normal negated (Creature.FrameNormalOf). Unchanged elsewhere.
                    Vec3 local = _body.ReflectFrameVector(b, rotation.TransposedTimes(
                        Vec3.Read(_body.RelativeVelocity, 3 * b)));

                    int at = b * 3;
                    _flow[at] = Clamp((float)(local.X / flowScale));
                    _flow[at + 1] = Clamp((float)(local.Y / flowScale));
                    _flow[at + 2] = Clamp((float)(local.Z / flowScale));
                }

                if (!_readsJoint && !_readsDrive) continue;

                int n = _body.DofCount[b];
                int offset = _body.DofStart[b];
                if (n == 0 || offset < 0) continue;

                for (int d = 0; d < n; d++)
                {
                    if (_readsJoint)
                    {
                        // Against the joint's own upper limit, so a hinge free to swing a little and
                        // one free to swing a lot both report "at the stop" as 1.
                        // D234: a reflected degree of freedom reads the signed angle and rate against the signed
                        // upper stop, so a twin moving as the mirror image of its original reports what the
                        // original does. The sign is exactly 1 on every other degree of freedom.
                        double sign = _body.DofSign(b, d);
                        double limit = System.Math.Abs(_body.SignedLimitHi(offset + d, sign));

                        _angle[offset + d] = limit > 1e-6
                            ? Clamp((float)(sign * _body.Q[offset + d] / limit))
                            : 0f;

                        _rate[offset + d] = Clamp((float)(sign * _body.Qd[offset + d] / rateScale));
                    }

                    if (_readsDrive) SampleDrive(b, offset + d);
                }
            }
        }

        /// <summary>
        /// D167's leaf smell at one part: the squashed field, and in the direction form the field's
        /// gradient in the part's own frame, unit length, times that value.
        /// </summary>
        private void SampleLeaf(int b, float px, float py, float pz)
        {
            float s = LeafScent.At(px, py, pz);
            float strength = s > 0f ? s / (s + _leafHalfScale) : 0f;
            _leaf[b] = strength;

            if (!_readsLeafDirection) return;

            Float3 g = LeafScent.GradientAt(px, py, pz);
            Mat3 rotation = Mat3.Read(_body.RotationMatrix, 9 * b);
            Vec3 local = _body.ReflectFrameVector(b, rotation.TransposedTimes(new Vec3(g.X, g.Y, g.Z)));   // D234, as the flow
            double length = local.Magnitude;
            int at = 3 * b;
            if (length > 0 && strength > 0f)
            {
                double scale = strength / length;
                _leafDirection[at] = (float)(local.X * scale);
                _leafDirection[at + 1] = (float)(local.Y * scale);
                _leafDirection[at + 2] = (float)(local.Z * scale);
            }
            else
            {
                _leafDirection[at] = 0f;
                _leafDirection[at + 1] = 0f;
                _leafDirection[at + 2] = 0f;
            }
        }

        /// <summary>
        /// The drive sense on one degree of freedom (<see cref="SensorChannel.Stroke"/>,
        /// <see cref="SensorChannel.Hold"/>, <see cref="SensorChannel.Ramp"/>): the envelope as the
        /// last step's drive left it, so one step stale, as Flow is. The drive is this body's own and
        /// is written only by its own step, so the read is no shared state.
        /// </summary>
        private void SampleDrive(int link, int at)
        {
            if (at >= _stroke.Length) return;
            EffectorDrive drive = _body.Drive;
            double stroke = at < drive.Stroke.Length ? drive.Stroke[at] : 0.0;
            double hold = _body.JointGenes[link].Hold;

            _stroke[at] = Clamp((float)stroke);
            _hold[at] = hold > 0 ? (float)System.Math.Max(0.0, System.Math.Min(1.0, drive.HoldLeft(at) / hold)) : 0f;
            _ramp[at] = Clamp((float)(drive.PowerTarget(at) - stroke));
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

                // The drive sense (RunConfig.SenseDrive): the joint's envelope, per degree of freedom.
                case SensorChannel.Stroke: return Dof(_stroke, partIndex, index);
                case SensorChannel.Hold: return Dof(_hold, partIndex, index);
                case SensorChannel.Ramp: return Dof(_ramp, partIndex, index);

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

                case SensorChannel.Scent:
                    return Scent != null ? _scent[partIndex] : 0f;

                case SensorChannel.Flow:
                    return index >= 0 && index < 3 ? _flow[partIndex * 3 + index] : 0f;

                // D106 item 5's two. Zero where the world has handed over nothing — a body that
                // touched nothing and lost nothing — which is the same zero an unimplemented
                // channel reads, and is the right answer rather than a stand-in: not being
                // touched is a real state and the commonest one there is. NeuronInput.Index is
                // ignored on both, as it is on Energy and for a weaker version of its reason:
                // there is one flag and one share per part and no axis to choose between.
                case SensorChannel.Contact:
                    return Contact != null && partIndex < Contact.Length && Contact[partIndex]
                        ? 1f
                        : 0f;

                // D162: this part against the bed or a reef on the last physics step.
                case SensorChannel.BedTouch:
                    return partIndex < _rock.Length ? _rock[partIndex] : 0f;

                // D167: the living leaves' smell, and its direction in the part's own frame.
                case SensorChannel.LeafScent:
                    return partIndex < _leaf.Length ? _leaf[partIndex] : 0f;

                case SensorChannel.LeafScentDirection:
                    return index >= 0 && index < 3 && partIndex < _leaf.Length
                        ? _leafDirection[partIndex * 3 + index]
                        : 0f;

                // Already a share of the part's own pool, so it needs no scale of its own: 0 is
                // untouched, 1 is a part taken from full to nothing in one step, and it is clamped
                // rather than left to run past 1 on an overkill blow, because a brain reading a
                // channel outside its stated range would be reading a different channel.
                case SensorChannel.Damage:
                    return Damage != null && partIndex < Damage.Length
                        ? Clamp(Damage[partIndex])
                        : 0f;

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
        private float Squash(float seconds) =>
            SquashSeconds(seconds, _energySquash, _energyFullScaleSeconds, _energyHalfScaleSeconds);

        /// <summary>
        /// The energy sense's squash, written once (D245): the host's reading and the card's
        /// <c>Squash</c> in <c>gpu-kernel.template</c>, which transcribes it operation for operation.
        /// </summary>
        /// <param name="seconds">Seconds of reserve.</param>
        /// <param name="squash">Which squash: <c>tanh(S / fullScale)</c>, or <c>S / (|S| + halfScale)</c> in float.</param>
        /// <param name="fullScale"><c>RunConfig.EnergyFullScaleSeconds</c>, floored as the sense floors it.</param>
        /// <param name="halfScale"><c>RunConfig.EnergyHalfScaleSeconds</c>, floored as the sense floors it.</param>
        /// <remarks>
        /// NaN, +infinity and -infinity are answered first, whichever squash is asked for.
        /// </remarks>
        public static float SquashSeconds(float seconds, EnergySenseSquash squash, float fullScale, float halfScale)
        {
            if (float.IsNaN(seconds)) return 0f;
            if (float.IsPositiveInfinity(seconds)) return 1f;
            if (float.IsNegativeInfinity(seconds)) return -1f;

            // D245: S / (|S| + k), in float. Equal to S / (S + k) from 0 up and odd below it.
            if (squash == EnergySenseSquash.HalfScale) return seconds / (System.Math.Abs(seconds) + halfScale);

            return (float)System.Math.Tanh(seconds / fullScale);
        }
    }
}
