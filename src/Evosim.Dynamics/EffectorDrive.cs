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

            int dof = body.Dof > 0 ? body.Dof : 1;
            _history = new float[dof * SmoothWindow];
            _runningSum = new float[dof];
            _torquePerUnit = new float[dof];

            for (int i = 1; i < body.Links; i++)
            {
                int n = body.DofCount[i];
                if (n == 0 || body.DofStart[i] < 0) continue;

                // Straight from the genome, with no floor: a link too weak to move its own limb
                // is a verdict for selection rather than something to paper over here.
                float perUnit = (float)body.Power[i];
                for (int d = 0; d < n; d++) _torquePerUnit[body.DofStart[i] + d] = perUnit;
            }
        }

        /// <summary>
        /// Feeds one raw signal per degree of freedom and sums the resulting torques into the
        /// body's external forces. Values outside [-1, 1] are clamped, not rejected.
        /// </summary>
        public void Drive(float[] raw)
        {
            int dof = _body.Dof;
            if (dof == 0) return;

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

                    torque += frame.Rotate(Creature.AxisOf(d)) * magnitude;
                }

                // A muscle pushes against something. The reaction on the parent is what makes
                // this an internal joint torque rather than free thrust.
                Mat3 rotation = Mat3.Read(_body.RotationMatrix, 9 * b);
                Vec3 world = rotation * torque;

                Vec3.Add(_body.Fext, 6 * b, world);
                Vec3.Add(_body.Fext, 6 * _body.Parent[b], -world);
            }
        }
    }
}
