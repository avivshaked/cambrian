namespace Evosim.Dynamics
{
    /// <summary>
    /// Package B: the two ledgers the farm bills from — the mechanical work a creature's own
    /// joints did, and the energy the water took off it. Ported from
    /// <c>Evosim.Sim.EffectorDriver.Settle</c> and <c>Evosim.Sim.FluidEnvironment.Settle</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Per body, and nothing shared.</b> In the farm both totals are single accumulators on
    /// objects one thread owns, and <c>FluidEnvironment.Settle</c> says in as many words that
    /// summing them beside the parallel phase would make a run's energy audit depend on thread
    /// scheduling. Here every creature carries its own four doubles and the world adds them up
    /// afterwards, in <see cref="Creature.Id"/> order — the same rule the limiter counts already
    /// follow, and the reason a thread count cannot reach these numbers.
    /// </para>
    /// <para>
    /// <b>Midpoint, both of them.</b> <c>EffectorDriver</c>'s own note: evaluating τ·ω at the
    /// pre-step velocity alone put the energy balance out by about 22% in the direction of too
    /// much drag, and averaging the velocities across the step is the cheapest correction that is
    /// a correction. So each hook stores what the step is about to be given and the motion it
    /// starts from, and <see cref="Settle"/> — called after the integrator, which is where
    /// <c>Physics.Simulate</c> sat — closes the integral against the motion it ended with.
    /// </para>
    /// <para>
    /// <b>A third accumulator the farm does not have.</b> The drive damper and the joint-limit
    /// springs are generalised torques, not external forces, and PhysX applies both inside its own
    /// solver where nothing can see their work. Here they are written down
    /// (<see cref="PassiveJointWorkJoules"/>) because the acceptance test needs them: without it
    /// the energy balance of a driven body closes only to the damper's own dissipation, which at
    /// <c>PhenotypeBuilder.MakeDrive</c>'s 1 N·m·s/rad is not small. It is a diagnostic and no
    /// part of the metabolism — <c>World.Observe</c> is billed from
    /// <see cref="MechanicalWorkJoules"/> alone, as it always was.
    /// </para>
    /// </remarks>
    public sealed partial class Creature
    {
        /// <summary>
        /// Unsigned mechanical work at the joints, joules, for the life of the body — what
        /// <c>Ecosystem</c> differences per metabolic step and hands to <c>World.Observe</c>.
        /// </summary>
        /// <remarks>
        /// Unsigned deliberately, and the farm's comment is the reason: a joint being driven
        /// <i>by</i> the water is doing negative work at the actuator, and crediting that would
        /// pay a creature to be pushed around — §11.2's free-energy failure arriving through the
        /// ledger rather than through the solver.
        /// </remarks>
        public double MechanicalWorkJoules;

        /// <summary>
        /// The same integral without the absolute value — the term in the energy balance.
        /// </summary>
        public double SignedWorkJoules;

        /// <summary>
        /// Energy drag has removed from this body, joules. Positive means energy left it, which
        /// is the only direction drag is allowed to move it.
        /// </summary>
        public double DissipatedJoules;

        /// <summary>
        /// Work done by the passive generalised joint torques — the drive damper and the limit
        /// springs. Negative while they bleed motion. Diagnostic; see the class remarks.
        /// </summary>
        public double PassiveJointWorkJoules;

        /// <summary>
        /// The drive torque this step put on each link, world axes, 3 per link — the port's
        /// <c>_pendingTorque</c>. Cleared by <see cref="Settle"/> as the farm's is.
        /// </summary>
        public double[] DriveTorque;

        /// <summary>
        /// The drag force on each link before the limiter had its say, 3 per link.
        /// </summary>
        /// <remarks>
        /// Before, because <c>FluidEnvironment</c> integrates <c>_force</c> and keeps the capped
        /// vector in a slot of its own: the ledger is the drag law's own work and the limiter is
        /// a stability device. Above dt 0.01, where the limiter can bind, the ledger therefore
        /// over-counts by whatever it capped — in the farm too, and said here rather than fixed.
        /// </remarks>
        public double[] DragForce;

        /// <summary>The drag torque on each link before the limiter, 3 per link.</summary>
        public double[] DragTorque;

        private double[] _preRelativeSpin;
        private double[] _preVelocity;
        private double[] _preSpin;
        private double[] _passiveTorque;
        private double[] _preJointRate;

        private bool _drivePending;
        private bool _dragPending;
        private bool _passivePending;

        private double _workDrainedAt;
        private double _dissipationDrainedAt;

        private void InitLedger()
        {
            DriveTorque = new double[3 * Links];
            DragForce = new double[3 * Links];
            DragTorque = new double[3 * Links];

            _preRelativeSpin = new double[3 * Links];
            _preVelocity = new double[3 * Links];
            _preSpin = new double[3 * Links];

            int dof = Dof > 0 ? Dof : 1;
            _passiveTorque = new double[dof];
            _preJointRate = new double[dof];
        }

        /// <summary>
        /// The drive's half of the hand-off: one link's world torque, and the relative spin
        /// through its joint as the step begins. Called from <see cref="EffectorDrive.Drive"/>.
        /// </summary>
        internal void NoteDriveTorque(int link, Vec3 torque)
        {
            Vec3.Write(DriveTorque, 3 * link, torque);
            Vec3.Write(
                _preRelativeSpin, 3 * link,
                Vec3.Read(Spin, 3 * link) - Vec3.Read(Spin, 3 * Parent[link]));

            _drivePending = true;
        }

        /// <summary>
        /// The water's half: one link's drag before the limiter, and the motion it acts on.
        /// Called from <see cref="Fluid.Apply"/>.
        /// </summary>
        internal void NoteDrag(int link, Vec3 force, Vec3 torque)
        {
            Vec3.Write(DragForce, 3 * link, force);
            Vec3.Write(DragTorque, 3 * link, torque);
            Vec3.Write(_preVelocity, 3 * link, Vec3.Read(Velocity, 3 * link));
            Vec3.Write(_preSpin, 3 * link, Vec3.Read(Spin, 3 * link));

            _dragPending = true;
        }

        /// <summary>
        /// The damper's and the limit spring's half: one degree of freedom's passive torque and
        /// the rate it acts on. Called from <c>DynamicsWorld.JointTorques</c>.
        /// </summary>
        internal void NotePassiveTorque(int dof, double torque, double rate)
        {
            _passiveTorque[dof] = torque;
            _preJointRate[dof] = rate;
            _passivePending = true;
        }

        /// <summary>
        /// Closes all three integrals against the motion the step ended with. Call once per step,
        /// after the integrator and after the kinematics pass that refreshed the velocities —
        /// which is exactly where the farm calls its two <c>Settle</c>s, immediately past
        /// <c>Physics.Simulate</c>.
        /// </summary>
        public void Settle(double dt)
        {
            if (dt <= 0) return;

            if (_drivePending)
            {
                _drivePending = false;

                for (int b = 1; b < Links; b++)
                {
                    Vec3 torque = Vec3.Read(DriveTorque, 3 * b);
                    if (torque.X == 0 && torque.Y == 0 && torque.Z == 0) continue;

                    Vec3 after = Vec3.Read(Spin, 3 * b) - Vec3.Read(Spin, 3 * Parent[b]);
                    double power = Vec3.Dot(
                        torque, (Vec3.Read(_preRelativeSpin, 3 * b) + after) * 0.5);

                    MechanicalWorkJoules += System.Math.Abs(power) * dt;
                    SignedWorkJoules += power * dt;

                    Vec3.Write(DriveTorque, 3 * b, Vec3.Zero);
                }
            }

            if (_dragPending)
            {
                _dragPending = false;

                for (int i = 0; i < Links; i++)
                {
                    Vec3 v = (Vec3.Read(_preVelocity, 3 * i) + Vec3.Read(Velocity, 3 * i)) * 0.5;
                    Vec3 w = (Vec3.Read(_preSpin, 3 * i) + Vec3.Read(Spin, 3 * i)) * 0.5;

                    double power = Vec3.Dot(Vec3.Read(DragForce, 3 * i), v) +
                                   Vec3.Dot(Vec3.Read(DragTorque, 3 * i), w);

                    DissipatedJoules -= power * dt;   // power is negative: drag opposes
                }
            }

            if (!_passivePending) return;
            _passivePending = false;

            for (int j = 0; j < Dof; j++)
            {
                // The limit spring's implicit half, and it is not bookkeeping. Adding
                // (c + k·dt)·dt to the joint's D is the rotor-inertia trick: the accelerations
                // that come out are the ones the unmodified body would have under an extra
                // generalised torque of -L·q̈, so that torque is applied and does work. Left out,
                // the balance of a body resting on its stops is out by an order of magnitude —
                // 14 times the drive's own work on a narrow-limited chain. Tau holds the
                // acceleration Aba.Outward parked there, which is exactly the q̈ in question.
                double torque = _passiveTorque[j] - LimitImplicit[j] * Tau[j];
                if (torque == 0) continue;

                PassiveJointWorkJoules +=
                    torque * (_preJointRate[j] + Qd[j]) * 0.5 * dt;

                _passiveTorque[j] = 0;
            }
        }

        /// <summary>
        /// The unsigned work done since the last drain, joules — <c>Ecosystem</c>'s
        /// <c>total - body.WorkAtLastStep</c>, kept on the body so the caller needs no side table.
        /// </summary>
        /// <remarks>
        /// Drained rather than read, so the same joule cannot be billed twice, and floored at
        /// zero for the reason the farm floors it: the total only rises, and a negative reading
        /// would be a bug charged to the creature.
        /// </remarks>
        public double DrainMechanicalWork()
        {
            double total = MechanicalWorkJoules;
            double interval = total - _workDrainedAt;
            _workDrainedAt = total;
            return interval > 0 ? interval : 0;
        }

        /// <summary>The energy drag took since the last drain, joules. Signed, unlike the work.</summary>
        public double DrainDissipated()
        {
            double total = DissipatedJoules;
            double interval = total - _dissipationDrainedAt;
            _dissipationDrainedAt = total;
            return interval;
        }

        /// <summary>
        /// The times this body's drive and drag were capped since the last drain — the farm's
        /// <c>DrainImpulsesLimited</c>, for the same reason it drains rather than reads.
        /// </summary>
        public long DrainDriveImpulsesLimited()
        {
            long total = DriveImpulsesLimited;
            long interval = total - _driveLimitDrainedAt;
            _driveLimitDrainedAt = total;
            return interval;
        }

        /// <summary>The drag limiter's binds since the last drain.</summary>
        public long DrainDragImpulsesLimited()
        {
            long total = DragImpulsesLimited;
            long interval = total - _dragLimitDrainedAt;
            _dragLimitDrainedAt = total;
            return interval;
        }

        private long _driveLimitDrainedAt;
        private long _dragLimitDrainedAt;
    }
}
