using System;
using System.Collections.Generic;
using Evosim.Core;
using Evosim.Dynamics;

namespace Gpu.Spike
{
    /// <summary>
    /// A struct-of-arrays copy of everything one creature's step reads, for N creatures,
    /// strided by creature so that neighbouring threads read neighbouring words.
    /// </summary>
    /// <remarks>
    /// Every array is <c>slot * N + creature</c>. The contents are read off real
    /// <see cref="Creature"/> objects, which are also what the CPU reference steps — so the
    /// kernel and the reference start from one set of numbers and not from two derivations of it.
    /// </remarks>
    internal sealed class Flat
    {
        public readonly int N, MaxLinks, MaxDof, MaxPanels;

        public int[] Links, Dof, Parent, DofCount, DofStart, PanelStart;

        public double[] Mass, Lift, Inertia, ChildAnchor, ParentAnchor, JointFrame, RestFrame;
        public double[] DriveTorque, LimitLo, LimitHi, LimitStiff, LimitDamp, Excess;
        public double[] PanelCentre, PanelNormal, PanelArea;

        // State: the whole of it. Everything else the step touches is derived by the kinematics.
        public double[] BasePos, BaseRot, Q, Qd, BallRot, RootSpin, RootVel, OutPos;

        public Flat(IReadOnlyList<Creature> bodies, double[][] driveTorque,
                    SolverConfig config, int maxLinks)
        {
            N = bodies.Count;
            MaxLinks = maxLinks;
            MaxDof = 3 * maxLinks;

            int panels = 1;
            for (int c = 0; c < N; c++)
            {
                int total = bodies[c].PanelStart[bodies[c].Links];
                if (total > panels) panels = total;
            }
            MaxPanels = panels;

            Links = new int[N];
            Dof = new int[N];
            Parent = new int[MaxLinks * N];
            DofCount = new int[MaxLinks * N];
            DofStart = new int[MaxLinks * N];
            PanelStart = new int[(MaxLinks + 1) * N];

            Mass = new double[MaxLinks * N];
            Lift = new double[MaxLinks * N];
            Inertia = new double[3 * MaxLinks * N];
            ChildAnchor = new double[3 * MaxLinks * N];
            ParentAnchor = new double[3 * MaxLinks * N];
            JointFrame = new double[4 * MaxLinks * N];
            RestFrame = new double[4 * MaxLinks * N];
            DriveTorque = new double[3 * MaxLinks * N];

            LimitLo = new double[MaxDof * N];
            LimitHi = new double[MaxDof * N];
            LimitStiff = new double[MaxDof * N];
            LimitDamp = new double[MaxDof * N];
            Excess = new double[N];

            PanelCentre = new double[3 * MaxPanels * N];
            PanelNormal = new double[3 * MaxPanels * N];
            PanelArea = new double[MaxPanels * N];

            BasePos = new double[3 * N];
            BaseRot = new double[4 * N];
            Q = new double[MaxDof * N];
            Qd = new double[MaxDof * N];
            BallRot = new double[4 * MaxLinks * N];
            RootSpin = new double[3 * N];
            RootVel = new double[3 * N];
            OutPos = new double[3 * MaxLinks * N];

            double excessBase = config.TissueExcessDensity;

            for (int c = 0; c < N; c++)
            {
                Creature body = bodies[c];
                Links[c] = body.Links;
                Dof[c] = body.Dof;

                // D064, per creature, exactly as Fluid.Apply takes it.
                double excess = excessBase;
                if (config.NeutralBodyVolume > 0)
                {
                    excess *= BuoyancyModel.ExcessDensityFactor(
                        (float)body.TotalVolume, (float)config.NeutralBodyVolume);
                }
                Excess[c] = excess;

                for (int i = 0; i < body.Links; i++)
                {
                    Parent[i * N + c] = body.Parent[i];
                    DofCount[i * N + c] = body.DofCount[i];
                    DofStart[i * N + c] = body.DofStart[i];
                    PanelStart[i * N + c] = body.PanelStart[i];

                    Mass[i * N + c] = body.Mass[i];
                    Lift[i * N + c] = body.Lift[i];

                    for (int k = 0; k < 3; k++)
                    {
                        Inertia[(3 * i + k) * N + c] = body.InertiaLocal[3 * i + k];
                        ChildAnchor[(3 * i + k) * N + c] = body.ChildAnchor[3 * i + k];
                        ParentAnchor[(3 * i + k) * N + c] = body.ParentAnchor[3 * i + k];
                        DriveTorque[(3 * i + k) * N + c] = driveTorque[c][3 * i + k];
                    }

                    for (int k = 0; k < 4; k++)
                    {
                        JointFrame[(4 * i + k) * N + c] = body.JointFrame[4 * i + k];
                        RestFrame[(4 * i + k) * N + c] = body.RestFrame[4 * i + k];
                        BallRot[(4 * i + k) * N + c] = body.BallRotation[4 * i + k];
                    }
                }
                PanelStart[body.Links * N + c] = body.PanelStart[body.Links];

                for (int d = 0; d < body.Dof; d++)
                {
                    LimitLo[d * N + c] = body.LimitLo[d];
                    LimitHi[d * N + c] = body.LimitHi[d];
                    LimitStiff[d * N + c] = body.LimitStiffness[d];
                    LimitDamp[d * N + c] = body.LimitDamping[d];
                    Q[d * N + c] = body.Q[d];
                    Qd[d * N + c] = body.Qd[d];
                }

                int total = body.PanelStart[body.Links];
                for (int p = 0; p < total; p++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        PanelCentre[(3 * p + k) * N + c] = body.PanelCentre[3 * p + k];
                        PanelNormal[(3 * p + k) * N + c] = body.PanelNormal[3 * p + k];
                    }
                    PanelArea[p * N + c] = body.PanelArea[p];
                }

                BasePos[c] = body.BasePosition.X;
                BasePos[N + c] = body.BasePosition.Y;
                BasePos[2 * N + c] = body.BasePosition.Z;

                BaseRot[c] = body.BaseRotation.X;
                BaseRot[N + c] = body.BaseRotation.Y;
                BaseRot[2 * N + c] = body.BaseRotation.Z;
                BaseRot[3 * N + c] = body.BaseRotation.W;

                for (int k = 0; k < 3; k++)
                {
                    RootSpin[k * N + c] = body.Spin[k];
                    RootVel[k * N + c] = body.Velocity[k];
                }
            }
        }

        public static float[] Narrow(double[] a)
        {
            var v = new float[a.Length];
            for (int i = 0; i < a.Length; i++) v[i] = (float)a[i];
            return v;
        }
    }
}
