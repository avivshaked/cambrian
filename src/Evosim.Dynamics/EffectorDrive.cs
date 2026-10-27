using Evosim.Core;

namespace Evosim.Dynamics
{
    /// <summary>
    /// Effector conditioning from DESIGN.md §4.4, ported from <c>Evosim.Sim.EffectorDriver</c>:
    /// clamp the raw signal to [-1, 1], scale by the link's evolved
    /// <see cref="PhenotypePart.Power"/>, average over the previous ten values, cap the per-step
    /// spin it could add, and apply the result as a torque on the child with its reaction on the
    /// parent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The torque is an external torque on two links, not a generalised joint force.</b> That
    /// is what <c>AddTorque</c> does in the farm, and it is what the port does here: the pair
    /// goes into each link's external force and the constraint absorbs whatever component the
    /// joint does not leave free. Projecting the torque onto the joint's own axes instead would
    /// have been a second model of the same thing, and the two would differ on any joint whose
    /// drive axes and free axes are not the same set — which is every two-degree joint, since
    /// <c>PhenotypeBuilder</c> drives X and Y and locks Z.
    /// </para>
    /// <para>
    /// <b>The signal path stays in <c>float</c>.</b> The brain is Core's and produces floats, and
    /// the moving average is the farm's arithmetic to the bit. Only the torque that comes out of
    /// it is widened, which is the seam between the controller and the solver.
    /// </para>
    /// </remarks>
    public sealed class EffectorDrive
    {
        private const int SmoothWindow = 10;

        private readonly Creature _body;
        private readonly float[] _history;      // dof x SmoothWindow, row-major
        private readonly float[] _runningSum;
        private readonly float[] _torquePerUnit;

        private readonly bool _limitDrive;
        private readonly double _spinBudgetPerStep;

        private int _cursor;
        private int _filled;

        // The bend-and-power drive (propose-joint-drive.md). Per degree of freedom: the power and
        // bend the joint is at (0 to 1 each), the command it is moving to, the rates it moves at,
        // and the seconds left before it takes another command.
        private readonly double[] _power, _bend, _powerTarget, _bendTarget, _powerRate, _bendRate, _holdLeft;
        private readonly double _poseOmega;
        private readonly double _step;

        /// <summary>True when the world runs the bend-and-power drive.</summary>
        public readonly bool BendAndPower;

        /// <summary>
        /// Each degree of freedom's power as the envelope has it now, 0 to 1: what the fluid's push
        /// reads and what the record logs. All zero under the torque drive.
        /// </summary>
        public readonly double[] Stroke;

        /// <summary>Each degree of freedom's bend as the envelope has it now, 0 to 1 across its range.</summary>
        public double Bend(int dof) => BendAndPower && dof >= 0 && dof < _bend.Length ? _bend[dof] : 0.5;

        /// <summary>
        /// Seconds of the hold still to run on one degree of freedom, 0 or less while the joint will
        /// take the next command; 0 under the torque drive. What <c>SensorChannel.Hold</c> reads.
        /// </summary>
        public double HoldLeft(int dof) => BendAndPower && dof >= 0 && dof < _holdLeft.Length ? _holdLeft[dof] : 0.0;

        /// <summary>
        /// The power command one degree of freedom last took, 0 to 1, which <see cref="Stroke"/> ramps
        /// toward; 0 under the torque drive. What <c>SensorChannel.Ramp</c> reads, less the stroke.
        /// </summary>
        public double PowerTarget(int dof) => BendAndPower && dof >= 0 && dof < _powerTarget.Length ? _powerTarget[dof] : 0.0;

        /// <summary>
        /// The world-space torque the last <see cref="Drive"/> put on each link, 3 per link —
        /// <c>EffectorDriver.AppliedTorque</c>. Kept rather than cleared, so a probe taken after
        /// the step can still say what the creature asked of its own joints on it.
        /// </summary>
        public readonly double[] AppliedTorque;

        /// <summary>A diagnostic multiplier on every link's power. Leave at 1.</summary>
        public float PowerScale { get; set; } = 1f;

        /// <summary>
        /// The ten-sample average currently standing on one degree of freedom, and the torque it
        /// asks for. Diagnostic only — the parity comparison against PhysX needs to know what the
        /// brain is actually emitting before it can read a joint angle as anything.
        /// </summary>
        public float Smoothed(int dof) =>
            _filled > 0 && dof >= 0 && dof < _runningSum.Length
                ? _runningSum[dof] / _filled
                : 0f;

        /// <summary>The steady torque one degree of freedom is asking for, N·m.</summary>
        public double Magnitude(int dof) =>
            dof >= 0 && dof < _torquePerUnit.Length
                ? Smoothed(dof) * _torquePerUnit[dof] * PowerScale
                : 0;

        public EffectorDrive(Creature body, SolverConfig config)
        {
            _body = body;

            _limitDrive = config.LimitersEngage;
            _spinBudgetPerStep = config.StepSeconds > 0
                ? config.MaxJointAngularVelocity / config.StepSeconds
                : 0;

            AppliedTorque = new double[3 * body.Links];

            int dof = body.Dof > 0 ? body.Dof : 1;

            BendAndPower = config.PowerPushRadiansPerSecond > 0;
            _poseOmega = 2.0 * System.Math.PI * config.PoseHertz;
            _step = config.StepSeconds;
            Stroke = new double[dof];
            _power = new double[dof];
            _bend = new double[dof];
            _powerTarget = new double[dof];
            _bendTarget = new double[dof];
            _powerRate = new double[dof];
            _bendRate = new double[dof];
            _holdLeft = new double[dof];
            for (int i = 0; i < dof; i++) { _bend[i] = 0.5; _bendTarget[i] = 0.5; }

            _history = new float[dof * SmoothWindow];
            _runningSum = new float[dof];
            _torquePerUnit = new float[dof];

            Refresh();
        }

        /// <summary>
        /// Re-reads the torque one unit of signal buys at each degree of freedom, from the body's
        /// <see cref="Creature.Power"/> as it now stands.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Called by the constructor and by <see cref="Creature.Resize"/>, and it touches
        /// nothing else.</b> The smoothing history, the running sums, the cursor and the fill
        /// count are all preserved across a growth resize: a creature that grew mid-stroke must
        /// carry on the stroke, not restart the ten-sample average with its muscles cold.
        /// </para>
        /// <para>
        /// <b>The per-step spin budget is not re-read here and does not need to be.</b> The cap a
        /// drive is held to is <c>SmallestInertia * budget</c>, read from the body on every step,
        /// so a resize that changes the inertia changes the cap on the next step with nothing to
        /// refresh. Only the budget itself is a property of the config, and a config does not
        /// change while a body is alive.
        /// </para>
        /// </remarks>
        public void Refresh()
        {
            System.Array.Clear(_torquePerUnit, 0, _torquePerUnit.Length);

            for (int i = 1; i < _body.Links; i++)
            {
                int n = _body.DofCount[i];
                if (n == 0 || _body.DofStart[i] < 0) continue;

                // Straight from the genome, with no floor: a link too weak to move its own limb
                // is a verdict for selection rather than something to paper over here.
                float perUnit = (float)_body.Power[i];
                for (int d = 0; d < n; d++) _torquePerUnit[_body.DofStart[i] + d] = perUnit;
            }
        }

        /// <summary>
        /// Feeds one raw signal per degree of freedom and sums the resulting torques into the
        /// body's external forces. Values outside [-1, 1] are clamped, not rejected.
        /// </summary>
        public void Drive(float[] raw, float[] bend = null)
        {
            int dof = _body.Dof;
            if (dof == 0) return;

            if (BendAndPower)
            {
                DriveBendAndPower(raw, bend);
                return;
            }

            for (int i = 0; i < dof; i++)
            {
                float v = i < raw.Length ? raw[i] : 0f;
                if (v < -1f) v = -1f;
                else if (v > 1f) v = 1f;

                _runningSum[i] -= _history[i * SmoothWindow + _cursor];
                _history[i * SmoothWindow + _cursor] = v;
                _runningSum[i] += v;
            }

            _cursor = (_cursor + 1) % SmoothWindow;
            if (_filled < SmoothWindow) _filled++;
            float inv = 1f / _filled;

            for (int b = 1; b < _body.Links; b++)
            {
                int n = _body.DofCount[b];
                if (n == 0) continue;

                int offset = _body.DofStart[b];
                QuatD frame = QuatD.Read(_body.JointFrame, 4 * b);

                // The most this link may be asked for on this step, against its smallest
                // principal inertia — the same conservative choice the farm's limiter makes.
                double allowed = _limitDrive
                    ? _body.SmallestInertia[b] * _spinBudgetPerStep
                    : 0;

                Vec3 torque = Vec3.Zero;

                for (int d = 0; d < n; d++)
                {
                    int index = offset + d;
                    float smoothed = _runningSum[index] * inv;
                    double magnitude = smoothed * _torquePerUnit[index] * PowerScale;

                    if (_limitDrive)
                    {
                        if (magnitude > allowed) { magnitude = allowed; _body.DriveImpulsesLimited++; }
                        else if (magnitude < -allowed) { magnitude = -allowed; _body.DriveImpulsesLimited++; }
                    }

                    // D234: the torque of a reflected degree of freedom is the signal's with its sign reversed,
                    // after the limiter, which is the same either way. Exactly 1 elsewhere.
                    torque += frame.Rotate(Creature.AxisOf(d)) * (_body.DofSign(b, d) * magnitude);
                }

                // A muscle pushes against something. The reaction on the parent is what makes
                // this an internal joint torque rather than free thrust.
                Mat3 rotation = Mat3.Read(_body.RotationMatrix, 9 * b);
                Vec3 world = rotation * torque;

                Vec3.Write(AppliedTorque, 3 * b, world);

                // Package B: the work ledger's first half — this torque and the relative spin it
                // starts the step with. EffectorDriver.Drive does the same two stores for the
                // same reason, and says why the integral cannot be closed here.
                _body.NoteDriveTorque(b, world);

                Vec3.Add(_body.Fext, 6 * b, world);
                Vec3.Add(_body.Fext, 6 * _body.Parent[b], -world);
            }
        }

        // ------------------------------------------------------------------ the bend-and-power drive

        /// <summary>
        /// propose-joint-drive.md. Each degree of freedom's two outputs pass the joint's threshold
        /// and envelope; the bend is held by a critically damped muscle capped by the part's Power;
        /// the power is left in <see cref="Stroke"/> for the fluid's push.
        /// </summary>
        private void DriveBendAndPower(float[] raw, float[] bendRaw)
        {
            for (int b = 1; b < _body.Links; b++)
            {
                int n = _body.DofCount[b];
                if (n == 0) continue;

                int offset = _body.DofStart[b];
                DriveGenes genes = _body.JointGenes[b];
                QuatD frame = QuatD.Read(_body.JointFrame, 4 * b);

                double allowed = _limitDrive ? _body.SmallestInertia[b] * _spinBudgetPerStep : double.MaxValue;
                double cap = System.Math.Min(allowed, _body.Power[b] * PowerScale);
                double inertia = _body.SmallestInertia[b];

                Vec3 torque = Vec3.Zero;

                for (int d = 0; d < n; d++)
                {
                    int i = offset + d;

                    // The power is the positive half of its neuron (the owner's design, D135: "0 or
                    // threshold"): a silent or negative neuron gives no power, and the power is off
                    // at or below the joint's threshold and rescaled above it, so its range of
                    // effort lies between the threshold and 1. Read from [-1, 1] onto [0, 1] as the
                    // first build did, a silent neuron was half power, above almost every threshold,
                    // and round 54's screen lost two thirds of its jointed bodies to the stroke they
                    // could not stop (2026-09-30). The bend keeps that reading: a silent neuron
                    // holds the joint centred, its rest pose.
                    float v = i < raw.Length ? raw[i] : 0f;
                    double threshold = genes.Threshold;
                    double level = System.Math.Max(0f, System.Math.Min(1f, v));
                    double power = level <= threshold ? 0.0 : System.Math.Min(1.0, (level - threshold) / (1.0 - threshold));
                    float w = bendRaw != null && i < bendRaw.Length ? bendRaw[i] : 0f;
                    double bend = (System.Math.Max(-1f, System.Math.Min(1f, w)) + 1.0) * 0.5;

                    // The envelope: a command is taken only when the last has been held its time,
                    // and only when it differs from the last by more than a hair, so a brain's noise
                    // does not restart the hold. From rest the ramp is the reaction; after that it
                    // is the release.
                    if (_holdLeft[i] > 0)
                    {
                        _holdLeft[i] -= _step;
                    }
                    else if (System.Math.Abs(power - _powerTarget[i]) > 0.01 || System.Math.Abs(bend - _bendTarget[i]) > 0.01)
                    {
                        bool fromRest = _power[i] == 0 && _powerTarget[i] == 0;
                        double ramp = fromRest ? genes.Reaction : genes.Release;
                        if (ramp < _step) ramp = _step;
                        _powerTarget[i] = power;
                        _bendTarget[i] = bend;
                        _powerRate[i] = System.Math.Abs(power - _power[i]) / ramp;
                        _bendRate[i] = System.Math.Abs(bend - _bend[i]) / ramp;
                        _holdLeft[i] = genes.Hold;
                    }

                    _power[i] = Toward(_power[i], _powerTarget[i], _powerRate[i] * _step);
                    _bend[i] = Toward(_bend[i], _bendTarget[i], _bendRate[i] * _step);
                    Stroke[i] = _power[i];

                    // The muscle: toward the bend's angle across the joint's own limits.
                    if (_poseOmega <= 0 || inertia <= 0) continue;

                    // D234: the muscle works in the signed reading, where a reflected degree of freedom has the
                    // genome's own stops and the bend means what it means for the original: the target across
                    // the signed stops, the signed angle and rate, and the torque reversed back to the stored
                    // angle. The sign is exactly 1 on every other degree of freedom.
                    double sign = _body.DofSign(b, d);
                    double lo = _body.SignedLimitLo(i, sign), hi = _body.SignedLimitHi(i, sign);
                    double target = lo + _bend[i] * (hi - lo);
                    double magnitude = inertia * _poseOmega * (_poseOmega * (target - sign * _body.Q[i]) - 2.0 * (sign * _body.Qd[i]));
                    if (magnitude > cap) { magnitude = cap; _body.DriveImpulsesLimited++; }
                    else if (magnitude < -cap) { magnitude = -cap; _body.DriveImpulsesLimited++; }

                    torque += frame.Rotate(Creature.AxisOf(d)) * (sign * magnitude);
                }

                Mat3 rotation = Mat3.Read(_body.RotationMatrix, 9 * b);
                Vec3 world = rotation * torque;
                Vec3.Write(AppliedTorque, 3 * b, world);
                _body.NoteDriveTorque(b, world);
                Vec3.Add(_body.Fext, 6 * b, world);
                Vec3.Add(_body.Fext, 6 * _body.Parent[b], -world);
            }
        }

        /// <summary>
        /// Stills the drive for good (D220, remains): the smoothing window, the power and bend envelopes, the stroke
        /// and the last applied torque all go to zero, so a body that no longer runs a brain pushes nothing and a
        /// checkpoint of it holds the still drive. The bend rests at its centre, where a fresh drive starts it.
        /// </summary>
        internal void Silence()
        {
            System.Array.Clear(_history, 0, _history.Length);
            System.Array.Clear(_runningSum, 0, _runningSum.Length);
            System.Array.Clear(AppliedTorque, 0, AppliedTorque.Length);
            System.Array.Clear(Stroke, 0, Stroke.Length);
            System.Array.Clear(_power, 0, _power.Length);
            System.Array.Clear(_powerTarget, 0, _powerTarget.Length);
            System.Array.Clear(_powerRate, 0, _powerRate.Length);
            System.Array.Clear(_bendRate, 0, _bendRate.Length);
            System.Array.Clear(_holdLeft, 0, _holdLeft.Length);
            for (int i = 0; i < _bend.Length; i++) { _bend[i] = 0.5; _bendTarget[i] = 0.5; }
        }

        private static double Toward(double from, double to, double step)
        {
            if (from < to) return from + step >= to ? to : from + step;
            if (from > to) return from - step <= to ? to : from - step;
            return from;
        }

        // ------------------------------------------------------------------ the whole state
        //
        // The ten-sample average is a stroke in progress. A creature restored with an empty
        // history would spend the next ten steps ramping its muscles from nothing, which is
        // exactly the reset a growth resize takes care not to do (Refresh's remarks) and exactly
        // the difference a resumed run must not have. The torque per unit of signal is re-read
        // from the body's Power by the constructor, so it is derived and not written.

        /// <summary>Writes the smoothing window and where it stands.</summary>
        internal void WriteState(System.IO.BinaryWriter w)
        {
            Evosim.Core.StateIo.Tag(w, "DRIV");
            Evosim.Core.StateIo.WriteFloats(w, _history);
            Evosim.Core.StateIo.WriteFloats(w, _runningSum);
            Evosim.Core.StateIo.WriteFloats(w, AppliedTorqueAsFloats());
            w.Write(_cursor);
            w.Write(_filled);
            w.Write(PowerScale);

            // The bend-and-power drive's envelope, only in a world that runs it, so a checkpoint
            // of a torque-drive world keeps the layout every earlier build wrote.
            if (!BendAndPower) return;
            foreach (double[] a in new[] { _power, _bend, _powerTarget, _bendTarget, _powerRate, _bendRate, _holdLeft })
            {
                w.Write(a.Length);
                foreach (double x in a) w.Write(x);
            }
        }

        /// <summary>Puts the smoothing window back.</summary>
        internal void ReadState(System.IO.BinaryReader r)
        {
            Evosim.Core.StateIo.Tag(r, "DRIV");
            Evosim.Core.StateIo.ReadFloats(r, _history, "drive history");
            Evosim.Core.StateIo.ReadFloats(r, _runningSum, "drive running sums");

            var applied = Evosim.Core.StateIo.ReadFloats(r);
            for (int i = 0; i < AppliedTorque.Length && i < applied.Length; i++)
            {
                AppliedTorque[i] = applied[i];
            }

            _cursor = r.ReadInt32();
            _filled = r.ReadInt32();
            PowerScale = r.ReadSingle();

            if (!BendAndPower) return;
            foreach (double[] a in new[] { _power, _bend, _powerTarget, _bendTarget, _powerRate, _bendRate, _holdLeft })
            {
                int length = r.ReadInt32();
                if (length != a.Length) throw new System.IO.InvalidDataException($"drive envelope of {length} where the body has {a.Length}");
                for (int i = 0; i < length; i++) a[i] = r.ReadDouble();
            }
            System.Array.Copy(_power, Stroke, _power.Length);
        }

        /// <summary>
        /// The last applied torque, narrowed. It is a probe's readout and nothing steps on it, so
        /// it is carried at single precision rather than costing three doubles a link.
        /// </summary>
        // ------------------------------------------------------------------ the mirror
        //
        // The fields a step backend reads and writes back (DynamicsWorldBackend.cs): the very
        // arrays Drive uses, so a body the GPU engine stepped carries the window it left.

        internal float[] MirrorHistory => _history;

        internal float[] MirrorRunningSum => _runningSum;

        internal float[] MirrorTorquePerUnit => _torquePerUnit;

        internal int MirrorCursor { get => _cursor; set => _cursor = value; }

        internal int MirrorFilled { get => _filled; set => _filled = value; }

        // The bend-and-power drive's envelope, a degree of freedom each, the very arrays
        // DriveBendAndPower steps; Stroke is the power, set again from it after a block.
        internal double[] MirrorPower => _power;
        internal double[] MirrorBend => _bend;
        internal double[] MirrorPowerTarget => _powerTarget;
        internal double[] MirrorBendTarget => _bendTarget;
        internal double[] MirrorPowerRate => _powerRate;
        internal double[] MirrorBendRate => _bendRate;
        internal double[] MirrorHoldLeft => _holdLeft;
        internal void MirrorStrokeFromPower() => System.Array.Copy(_power, Stroke, _power.Length);

        private float[] AppliedTorqueAsFloats()
        {
            var values = new float[AppliedTorque.Length];
            for (int i = 0; i < values.Length; i++) values[i] = (float)AppliedTorque[i];
            return values;
        }
    }
}
