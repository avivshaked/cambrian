using UnityEngine;
using Evosim.Core;

namespace Evosim.Sim
{
    /// <summary>
    /// Effector conditioning from DESIGN.md §4.4, after [K12 §2.2, p.5]:
    /// clamp the raw signal to [-1, 1]; scale by the mass of the <b>smaller</b> of the two
    /// connected parts; average over the previous 10 values; apply the result as torque.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The mass scaling <i>"limits the maximum size of a force to some reasonable value"</i>,
    /// which stops evolution discovering arbitrarily powerful tiny motors. The 10-sample
    /// average <i>"eliminates sudden large forces and also improves stability of the
    /// simulation"</i>, and suppresses the high-frequency buzzing that a GA otherwise
    /// converges on and viewers dislike — at a fraction of the complexity of PD control.
    /// </para>
    /// <para>
    /// <b>Torque, not a position target.</b> Spike 01 drove position targets with a derived
    /// force limit, which was adequate for measuring throughput but is not what §4.4
    /// specifies. This applies torque about the joint's free axes with drive stiffness at
    /// zero.
    /// </para>
    /// <para>
    /// <b><see cref="TorqueScale"/> is not calibrated.</b> 300 N·m per kg is carried over
    /// from Spike 01, where it was a force limit under position control and only ever had to
    /// not explode. The right value depends on fluid drag, which does not exist yet.
    /// Milestone 2 measures it.
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
        /// Newton-metres per kilogram of the smaller connected part.
        /// </summary>
        /// <remarks>
        /// Reasoned rather than tuned, and still provisional. For a cube of mass m and
        /// half-extent h the moment of inertia about its centre is (2/3)mh². A part around
        /// 100 kg with h = 0.25 m gives roughly 4 kg·m², and swinging a joint through about a
        /// radian in a quarter second needs on the order of 30 rad/s², so ~130 N·m — a little
        /// over 1 N·m per kilogram.
        ///
        /// This was 300, carried over from Spike 01 where the same number meant something
        /// completely different: a force LIMIT under position control, a ceiling that is
        /// rarely reached. As directly applied torque it is roughly two orders of magnitude
        /// too large, which is why the first sandbox creatures were flung rather than swum.
        ///
        /// The value that matters is the one measured against real fluid drag at Milestone 2.
        /// Until then this is a viewing figure.
        /// </remarks>
        public float TorqueScale { get; set; } = 2f;

        public int Dof => _creature.TotalDof;

        public EffectorDriver(CreatureInstance creature)
        {
            _creature = creature;

            int dof = Mathf.Max(1, creature.TotalDof);
            _history = new float[dof, SmoothWindow];
            _runningSum = new float[dof];
            _torquePerUnit = new float[dof];

            for (int b = 1; b < creature.Bodies.Length; b++)
            {
                int n = creature.Phenotype.Parts[b].JointType.DofCount();
                if (n == 0) continue;

                float smallerMass = Mathf.Min(
                    creature.Bodies[b].mass,
                    creature.Bodies[creature.Phenotype.Parts[b].ParentIndex].mass);

                float perUnit = Mathf.Max(1f, smallerMass * TorqueScale);
                for (int d = 0; d < n; d++)
                {
                    _torquePerUnit[creature.DofOffset[b] + d] = perUnit;
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

                Vector3 torque = Vector3.zero;
                for (int d = 0; d < n; d++)
                {
                    int idx = offset + d;
                    float smoothed = _runningSum[idx] * inv;
                    torque += DriveAxis(frame, d) * (smoothed * _torquePerUnit[idx]);
                }

                // A muscle pushes against something. Applying torque to the child alone
                // injects angular momentum from nowhere: the creature spins up without bound
                // and never has to push against the water to do it. The reaction on the
                // parent is what makes this an internal joint torque rather than free thrust,
                // and it is not optional — an evolutionary search would find the free version
                // within a few generations and build its entire gait on it (DESIGN.md §11.2).
                Vector3 worldTorque = body.transform.TransformDirection(torque);
                body.AddTorque(worldTorque);
                _creature.Bodies[part.ParentIndex].AddTorque(-worldTorque);
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
