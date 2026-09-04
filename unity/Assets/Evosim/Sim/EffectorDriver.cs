using UnityEngine;
using Evosim.Core;

namespace Evosim.Sim
{
    /// <summary>
    /// Effector conditioning from DESIGN.md §4.4, after [K12 §2.2, p.5]: clamp the raw signal
    /// to [-1, 1]; scale by the driving link's capacity; average over the previous 10 values;
    /// apply the result as torque.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Departure from [K12], recorded.</b> Krčah scaled by the mass of the smaller connected
    /// part, which <i>"limits the maximum size of a force to some reasonable value"</i> and stops
    /// evolution discovering arbitrarily powerful tiny motors. Here the limit is economic
    /// instead: strength is <see cref="PhenotypePart.Power"/>, evolved per link and billed as
    /// standing upkeep in proportion to capacity and degrees of freedom (§5A.1). A tiny part
    /// may be arbitrarily strong; it simply cannot afford to be. Bounds on Power keep the
    /// solver stable, which is the part of Krčah's motive that still applies.
    /// </para>
    /// <para>
    /// The 10-sample average is retained unchanged: it <i>"eliminates sudden large forces and
    /// also improves stability of the simulation"</i>, and suppresses the high-frequency
    /// buzzing that a GA otherwise converges on and viewers dislike — at a fraction of the
    /// complexity of PD control.
    /// </para>
    /// <para>
    /// <b>Torque, not a position target.</b> Spike 01 drove position targets with a derived
    /// force limit, which was adequate for measuring throughput but is not what §4.4
    /// specifies. This applies torque about the joint's free axes with drive stiffness at
    /// zero.
    /// </para>
    /// </remarks>
    public sealed class EffectorDriver
    {
        private const int SmoothWindow = 10;

        private readonly CreatureInstance _creature;
        private readonly float[,] _history;
        private readonly float[] _runningSum;
        private readonly float[] _torquePerUnit;

        private int _cursor;
        private int _filled;

        /// <summary>
        /// Multiplier on every link's evolved <see cref="PhenotypePart.Power"/>. A diagnostic
        /// knob, not physics — leave at 1.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Torque used to be derived here, as a fixed newton-metres-per-kilogram applied to
        /// every joint in every creature. It is now a property of the link that carries the
        /// joint (§5A.1): <see cref="PhenotypePart.Power"/> is evolved, and paid for in standing
        /// upkeep proportional to capacity and degrees of freedom, whether or not the joint
        /// moves. Capacity that costs nothing while idle is capacity evolution takes all of.
        /// </para>
        /// <para>
        /// The mass scaling that scheme inherited from [K12 §2.2, p.5] was for
        /// <i>numerical stability</i> — it <i>"limits the maximum size of a force to some
        /// reasonable value"</i> — and that job now belongs to the bounds on Power in
        /// <c>RandomGenomeOptions</c> and to mutation limits, not to a coefficient here.
        /// </para>
        /// <para>
        /// This multiplier remains only so a harness can sweep drive strength while holding a
        /// genome fixed, which is how the joint-limit energy sink was isolated (logbook/0008).
        /// Anything reported as a physical quantity should be measured with it at 1.
        /// </para>
        /// </remarks>
        public float PowerScale
        {
            get => _torqueScale;
            set
            {
                _torqueScale = value;
                RecomputeTorquePerUnit();
            }
        }

        private float _torqueScale = 1f;

        public int Dof => _creature.TotalDof;

        /// <summary>
        /// The most joint angular velocity one step's drive torque may add, in rad/s.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Thirty rad/s is about five revolutions a second — faster than anything [K12] reports a
        /// creature doing, and far faster than anything that has ever swum here. So the cap does
        /// not remove a gait; it removes a torque a link could not physically apply. The dump from
        /// <c>r20q-s1</c> is the measurement: a 143-gram link, inertia of order 1e-5 kg·m2, was
        /// being driven at about 1.5 N·m — some 1e5 rad/s2, thousands of rad/s added per 0.02 s
        /// step — and its articulation reached 4e13 rad/s and then NaN inside two seconds of the
        /// creature's life. Evolved <see cref="PhenotypePart.Power"/> has no upper bound relative
        /// to the inertia it acts on, and nothing else in the loop notices.
        /// </para>
        /// <para>
        /// <b>Gated to steps coarser than 0.01, exactly like the drag limiter</b>
        /// (<see cref="FluidEnvironment"/>). Every number this project has published was measured
        /// at 0.01, and the whole value of that step is that it replays bit for bit under its own
        /// config hash; a limiter that engaged there would silently make a different world of
        /// every historical run. At 0.02 it engages, and every bind is counted rather than
        /// assumed — <see cref="ImpulsesLimited"/>, printed per run, the same discipline
        /// <c>DragImpulsesLimited</c> is held to.
        /// </para>
        /// <para>
        /// A cap on the torque, never on <c>ArticulationBody.maxJointVelocity</c>: the solver
        /// would clamp silently and there would be no number to read afterwards, which is the one
        /// thing a stabiliser in this project may not do.
        /// </para>
        /// </remarks>
        public const float MaxJointAngularVelocity = 30f;

        /// <summary>
        /// Times a drive torque was capped at <see cref="MaxJointAngularVelocity"/>. Always 0 at
        /// dt 0.01, where the limiter is gated off.
        /// </summary>
        public long ImpulsesLimited { get; private set; }

        /// <summary>Reads the count and zeroes it, so a caller can total it across the population.</summary>
        /// <remarks>
        /// Drained rather than summed from outside, because a driver dies with its creature: a
        /// total taken over the living alone would lose every bind the dead ever made, and this
        /// count exists to say how often the stabiliser engaged over a whole run.
        /// </remarks>
        public long DrainImpulsesLimited()
        {
            long n = ImpulsesLimited;
            ImpulsesLimited = 0;
            return n;
        }

        /// <summary>
        /// Whether <see cref="MaxJointAngularVelocity"/> applies at this driver's timestep.
        /// </summary>
        /// <remarks>
        /// Decided once, in the constructor, from the same threshold and for the same reason the
        /// drag limiter uses: a comparison per DOF per step would be the identical answer a
        /// million times over, and the inertia read below is not free.
        /// </remarks>
        private readonly bool _limitDrive;

        /// <summary>rad/s of allowance per second — <see cref="MaxJointAngularVelocity"/> / dt.</summary>
        private readonly float _spinBudgetPerStep;

        /// <summary>
        /// Cumulative mechanical work done by every joint, in joules — DESIGN.md §5A.2.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Joint power is τ·ω where ω is the <b>relative</b> angular velocity of the two
        /// connected bodies, which is the only frame in which the number means anything: a
        /// creature tumbling freely has large ω per body and does no work at its joints.
        /// </para>
        /// <para>
        /// The absolute value is deliberate. Negative work — a joint being driven backwards by
        /// the fluid while resisting — costs a real muscle energy, and the alternative would
        /// let a creature bank credit by being pushed around, which is a free-energy source of
        /// exactly the kind §11.2 exists to prevent.
        /// </para>
        /// <para>
        /// Reported only, at Milestone 2. It becomes an expenditure at Milestone 3, and the
        /// coefficient converting a joule of this into a joule of sunlight is one of the
        /// unmeasured parameters listed in §5A.10.
        /// </para>
        /// </remarks>
        public double MechanicalWorkJoules { get; private set; }

        /// <summary>
        /// The same integral without the absolute value — net energy the joints put into the
        /// creature, in joules.
        /// </summary>
        /// <remarks>
        /// Not a metabolic quantity: it is the term in the energy balance. With no external
        /// force but drag, this must equal the change in kinetic energy plus everything drag
        /// dissipated. That equality is the only reason to believe
        /// <see cref="MechanicalWorkJoules"/> at all — kilowatt figures in water are plausible
        /// enough to pass inspection whether or not they are right.
        /// </remarks>
        public double SignedWorkJoules { get; private set; }

        /// <summary>Mean mechanical power since construction, in watts.</summary>
        public double MeanPowerWatts => _elapsed > 0d ? MechanicalWorkJoules / _elapsed : 0d;

        private readonly float _stepSeconds;
        private double _elapsed;

        private readonly Vector3[] _pendingTorque;
        private readonly Vector3[] _pendingRelOmega;
        private bool _pending;

        /// <param name="stepSeconds">
        /// The fixed timestep this driver will be called at. Required rather than defaulted to
        /// <see cref="Time.fixedDeltaTime"/>: the harnesses here call
        /// <c>Physics.Simulate</c> with their own dt, which is not the project setting, and a
        /// silently wrong dt would corrupt every energy figure while leaving the torques right.
        /// </param>
        public EffectorDriver(CreatureInstance creature, float stepSeconds)
        {
            _creature = creature;
            _stepSeconds = stepSeconds;

            // The same threshold, to the same bit, as FluidEnvironment's drag limiter: the two
            // stabilisers must switch on together or "dt 0.01 replays the historical record" is
            // true of one of them and not the other.
            _limitDrive = stepSeconds > 0.0100001f;
            _spinBudgetPerStep = stepSeconds > 0f ? MaxJointAngularVelocity / stepSeconds : 0f;

            int dof = Mathf.Max(1, creature.TotalDof);
            _history = new float[dof, SmoothWindow];
            _runningSum = new float[dof];
            _torquePerUnit = new float[dof];

            _pendingTorque = new Vector3[creature.Bodies.Length];
            _pendingRelOmega = new Vector3[creature.Bodies.Length];

            RecomputeTorquePerUnit();
        }

        private void RecomputeTorquePerUnit()
        {
            if (_torquePerUnit == null) return;   // set during construction, before the array exists

            for (int b = 1; b < _creature.Bodies.Length; b++)
            {
                int n = _creature.Phenotype.Parts[b].JointType.DofCount();
                if (n == 0 || _creature.DofOffset[b] < 0) continue;

                // Straight from the genome. No floor: a link too weak to move its own limb is
                // a creature that wasted tissue on a joint it cannot use, and that is a verdict
                // for selection to reach rather than something to paper over here.
                float perUnit = _creature.Phenotype.Parts[b].Power * _torqueScale;
                for (int d = 0; d < n; d++)
                {
                    _torquePerUnit[_creature.DofOffset[b] + d] = perUnit;
                }
            }
        }

        /// <summary>
        /// Feeds one raw signal per DOF. Values outside [-1, 1] are clamped, not rejected —
        /// a controller saturating is normal, not an error.
        /// </summary>
        public void Drive(float[] raw)
        {
            int dof = _creature.TotalDof;
            if (dof == 0) return;

            for (int i = 0; i < dof; i++)
            {
                float v = Mathf.Clamp(i < raw.Length ? raw[i] : 0f, -1f, 1f);
                _runningSum[i] -= _history[i, _cursor];
                _history[i, _cursor] = v;
                _runningSum[i] += v;
            }

            _cursor = (_cursor + 1) % SmoothWindow;
            if (_filled < SmoothWindow) _filled++;
            float inv = 1f / _filled;

            for (int b = 1; b < _creature.Bodies.Length; b++)
            {
                PhenotypePart part = _creature.Phenotype.Parts[b];
                int n = part.JointType.DofCount();
                if (n == 0) continue;

                ArticulationBody body = _creature.Bodies[b];
                Quaternion frame = body.anchorRotation;
                int offset = _creature.DofOffset[b];

                // The most this link may be asked for on this step: the torque that would add
                // exactly MaxJointAngularVelocity to its spin, and no more. Against the smallest
                // principal inertia rather than the inertia about each drive axis — conservative,
                // and the same choice the drag limiter makes for the same reason: the tensor is
                // expressed in the body's own inertia frame, and resolving each free axis into it
                // is arithmetic that buys a tighter bound on a term that exists to be loose.
                // Read once per driven link per step, and not at all at dt 0.01.
                float allowed = 0f;
                if (_limitDrive)
                {
                    Vector3 inertia = body.inertiaTensor;
                    allowed = Mathf.Min(inertia.x, Mathf.Min(inertia.y, inertia.z)) *
                              _spinBudgetPerStep;
                }

                Vector3 torque = Vector3.zero;
                for (int d = 0; d < n; d++)
                {
                    int idx = offset + d;
                    float smoothed = _runningSum[idx] * inv;

                    // The un-limited path is the original expression, character for character,
                    // and not a tidier shared one: writing the product through a
                    // float local and then multiplying changed the last bits of the torque
                    // under Mono's codegen, and a 300 s default arm that had been reproducing
                    // runs/r20v-age1.md exactly started differing at t=200 — 0.0633 m/s against
                    // 0.063, work 27.43 W against 27.45. Nothing was wrong with either number;
                    // the run was simply a different chaotic realisation, which is precisely
                    // what dt 0.01 is not allowed to become (logbook/0052). So the branch is
                    // duplication on purpose, and the duplicate is the one that must not move.
                    if (!_limitDrive)
                    {
                        torque += DriveAxis(frame, d) * (smoothed * _torquePerUnit[idx]);
                        continue;
                    }

                    // Per degree of freedom, on the signed magnitude before it is resolved onto
                    // its axis — so a capped DOF keeps its direction and its sign, and the other
                    // DOFs of the same joint pass untouched.
                    float magnitude = smoothed * _torquePerUnit[idx];

                    if (magnitude > allowed)
                    {
                        magnitude = allowed;
                        ImpulsesLimited++;
                    }
                    else if (magnitude < -allowed)
                    {
                        magnitude = -allowed;
                        ImpulsesLimited++;
                    }

                    torque += DriveAxis(frame, d) * magnitude;
                }

                // A muscle pushes against something. Applying torque to the child alone
                // injects angular momentum from nowhere: the creature spins up without bound
                // and never has to push against the water to do it. The reaction on the
                // parent is what makes this an internal joint torque rather than free thrust,
                // and it is not optional — an evolutionary search would find the free version
                // within a few generations and build its entire gait on it (DESIGN.md §11.2).
                Vector3 worldTorque = body.transform.TransformDirection(torque);
                ArticulationBody parent = _creature.Bodies[part.ParentIndex];

                body.AddTorque(worldTorque);
                parent.AddTorque(-worldTorque);

                // Power at the joint: torque against the RELATIVE rotation of the two bodies.
                // Recorded, not integrated yet: the work over this step depends on the relative
                // velocity through it, and only half of that is known before the solver runs.
                // See Settle.
                _pendingTorque[b] = worldTorque;
                _pendingRelOmega[b] = body.angularVelocity - parent.angularVelocity;
                _pending = true;
            }

            _elapsed += _stepSeconds;
        }

        /// <summary>
        /// The world-space torque the last <see cref="Drive"/> put on one body, before
        /// <see cref="Settle"/> clears it — for the divergence dump (the divergence spec, after
        /// logbook/0056).
        /// </summary>
        /// <remarks>
        /// Read once in the life of a run, between <c>Physics.Simulate</c> and
        /// <see cref="Settle"/>, when a body has stopped being finite: what the creature was
        /// asking of its own joints on the step it exploded is the first thing that has to be
        /// ruled in or out. Zero for the root, for an unjointed part, and for a driver whose
        /// smoothed signal came out at zero — which is a real answer, not a missing one.
        /// </remarks>
        public Vector3 AppliedTorque(int bodyIndex) =>
            bodyIndex >= 0 && bodyIndex < _pendingTorque.Length
                ? _pendingTorque[bodyIndex]
                : Vector3.zero;

        /// <summary>
        /// Integrates the work done by the last <see cref="Drive"/>. Call immediately after
        /// <c>Physics.Simulate</c>; harmless to omit if the energy figures are not wanted.
        /// </summary>
        /// <remarks>
        /// Midpoint rather than left-rectangle. Evaluating τ·ω at the pre-step velocity alone
        /// over-counts systematically — measured against a controlled configuration with the
        /// joint limits widened, it put the energy balance out by about 22% in the direction of
        /// too much drag. Averaging the velocities across the step is the cheapest correction
        /// that is a correction rather than a wider tolerance.
        /// </remarks>
        public void Settle()
        {
            if (!_pending) return;
            _pending = false;

            for (int b = 1; b < _creature.Bodies.Length; b++)
            {
                Vector3 torque = _pendingTorque[b];
                if (torque == Vector3.zero) continue;

                PhenotypePart part = _creature.Phenotype.Parts[b];
                Vector3 after =
                    _creature.Bodies[b].angularVelocity -
                    _creature.Bodies[part.ParentIndex].angularVelocity;

                float power = Vector3.Dot(torque, (_pendingRelOmega[b] + after) * 0.5f);

                MechanicalWorkJoules += Mathf.Abs(power) * _stepSeconds;
                SignedWorkJoules += power * _stepSeconds;

                _pendingTorque[b] = Vector3.zero;
            }
        }

        /// <summary>
        /// The free axis for one DOF, in the part's local space. A revolute joint turns about
        /// the anchor frame's X; a spherical joint uses X for twist and Y/Z for the swings.
        /// </summary>
        private static Vector3 DriveAxis(Quaternion frame, int dof)
        {
            switch (dof)
            {
                case 0: return frame * Vector3.right;
                case 1: return frame * Vector3.up;
                default: return frame * Vector3.forward;
            }
        }

        /// <summary>
        /// A placeholder signal: one sine per DOF, phase-offset so the creature moves rather
        /// than clenching. Stands in for the brain graph until Milestone 3 — the conditioning
        /// above is the real §4.4 machinery, only the signal feeding it is a stub.
        /// </summary>
        public void DriveTestSine(float time, float hz, float[] scratch)
        {
            for (int i = 0; i < _creature.TotalDof; i++)
            {
                scratch[i] = Mathf.Sin(2f * Mathf.PI * hz * time + i * 0.7f);
            }
            Drive(scratch);
        }
    }
}
