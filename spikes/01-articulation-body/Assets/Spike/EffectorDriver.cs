using UnityEngine;

namespace Spike
{
    /// <summary>
    /// Effector conditioning from DESIGN.md §4.4, after [K12 §2.2, p.5]:
    ///   1. clamp raw signal to [-1, 1]
    ///   2. scale by the mass of the SMALLER of the two connected bodies
    ///      ("limits the maximum size of a force to some reasonable value")
    ///   3. average over the previous 10 values
    ///      ("eliminates sudden large forces and also improves stability")
    ///
    /// The conditioned value drives a position target within the joint limit,
    /// with forceLimit derived from the mass scaling. M4 measures whether this
    /// holds together under maximum amplitude.
    /// </summary>
    public class EffectorDriver
    {
        const int SmoothWindow = 10;

        readonly BuiltCreature _c;
        readonly float[,] _history;   // [dofIndex, SmoothWindow] ring buffer
        readonly float[] _runningSum;
        readonly float[] _limitDeg;
        readonly float[] _forceLimit;
        int _cursor;
        int _filled;

        public float TorqueScale = 300f;   // N·m per kg of smaller connected body

        public EffectorDriver(BuiltCreature c, CreatureSpec spec)
        {
            _c = c;
            int dof = Mathf.Max(1, c.totalDof);
            _history = new float[dof, SmoothWindow];
            _runningSum = new float[dof];
            _limitDeg = new float[dof];
            _forceLimit = new float[dof];

            for (int i = 1; i < c.bodies.Length; i++)
            {
                int n = JointTypeInfo.DofCount(c.jointTypes[i]);
                if (n == 0) continue;
                float fl = Mathf.Max(1f, c.smallerMassAtJoint[i] * TorqueScale);
                for (int d = 0; d < n; d++)
                {
                    int idx = c.dofOffset[i] + d;
                    _limitDeg[idx] = spec.parts[i].jointLimitDeg;
                    _forceLimit[idx] = fl;
                }
                ApplyForceLimit(c.bodies[i], fl);
            }
        }

        static void ApplyForceLimit(ArticulationBody ab, float fl)
        {
            var x = ab.xDrive; x.forceLimit = fl; ab.xDrive = x;
            var y = ab.yDrive; y.forceLimit = fl; ab.yDrive = y;
            var z = ab.zDrive; z.forceLimit = fl; ab.zDrive = z;
        }

        /// <summary>
        /// Feed one raw signal per DOF. Values outside [-1,1] are clamped, not rejected.
        /// </summary>
        public void Drive(float[] raw)
        {
            int dof = _c.totalDof;
            if (dof == 0) return;

            for (int i = 0; i < dof; i++)
            {
                float v = Mathf.Clamp(raw[i], -1f, 1f);
                _runningSum[i] -= _history[i, _cursor];
                _history[i, _cursor] = v;
                _runningSum[i] += v;
            }

            _cursor = (_cursor + 1) % SmoothWindow;
            if (_filled < SmoothWindow) _filled++;
            float inv = 1f / _filled;

            for (int b = 1; b < _c.bodies.Length; b++)
            {
                int n = JointTypeInfo.DofCount(_c.jointTypes[b]);
                if (n == 0) continue;
                var ab = _c.bodies[b];
                int off = _c.dofOffset[b];

                for (int d = 0; d < n; d++)
                {
                    int idx = off + d;
                    float smoothed = _runningSum[idx] * inv;
                    float targetDeg = smoothed * _limitDeg[idx];

                    switch (d)
                    {
                        case 0: { var dr = ab.xDrive; dr.target = targetDeg; ab.xDrive = dr; break; }
                        case 1: { var dr = ab.yDrive; dr.target = targetDeg; ab.yDrive = dr; break; }
                        case 2: { var dr = ab.zDrive; dr.target = targetDeg; ab.zDrive = dr; break; }
                    }
                }
            }
        }

        /// <summary>Full-amplitude sinusoidal drive — the worst case for M4.</summary>
        public void DriveSine(float t, float freqHz, float[] scratch)
        {
            for (int i = 0; i < _c.totalDof; i++)
                scratch[i] = Mathf.Sin(2f * Mathf.PI * freqHz * t + i * 0.7f);
            Drive(scratch);
        }
    }
}
