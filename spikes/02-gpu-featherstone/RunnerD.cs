// ---------------------------------------------------------------------------------------------
// GENERATED from runner.template by gen.py. Do not edit; edit the template and regenerate.
//
// Device buffers and the launch, for one precision. The double and the single runner are one
// source for the same reason the kernels are.
// ---------------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using ILGPU;
using ILGPU.Runtime;

namespace Gpu.Dbl
{
    using Real = System.Double;

    internal sealed class Runner : Gpu.Spike.IRunner
    {
        private readonly Accelerator _acc;
        private readonly int _n;

        private readonly MemoryBuffer1D<int, Stride1D.Dense> _links, _dof, _parent, _dofCount,
            _dofStart, _panelStart;

        private readonly MemoryBuffer1D<Real, Stride1D.Dense> _mass, _lift, _inertia, _childAnchor,
            _parentAnchor, _jointFrame, _restFrame, _driveTorque, _limitLo, _limitHi, _limitStiff,
            _limitDamp, _excess, _panelCentre, _panelNormal, _panelArea;

        private readonly MemoryBuffer1D<Real, Stride1D.Dense> _basePos, _baseRot, _q, _qd,
            _ballRot, _rootSpin, _rootVel, _outPos;

        private readonly Action<Index1D, GTopo, GBody, GPanels, GState, GCfg> _kernel;

        private readonly Real[] _basePos0, _baseRot0, _q0, _qd0, _ballRot0, _rootSpin0, _rootVel0;
        private readonly Real[] _driveHost;
        private readonly Real[] _posScratch;

        private GCfg _cfg;

        public double CompileMs { get; }

        public string Where => _acc.Name + " / " + _acc.AcceleratorType;

        public Runner(Accelerator accelerator, Gpu.Spike.Flat flat, Gpu.Spike.Scalars s, int groupSize)
        {
            _acc = accelerator;
            _n = flat.N;

            _links = Int(flat.Links); _dof = Int(flat.Dof);
            _parent = Int(flat.Parent); _dofCount = Int(flat.DofCount);
            _dofStart = Int(flat.DofStart); _panelStart = Int(flat.PanelStart);

            _mass = Re(flat.Mass); _lift = Re(flat.Lift); _inertia = Re(flat.Inertia);
            _childAnchor = Re(flat.ChildAnchor); _parentAnchor = Re(flat.ParentAnchor);
            _jointFrame = Re(flat.JointFrame); _restFrame = Re(flat.RestFrame);
            _driveTorque = Re(flat.DriveTorque);
            _limitLo = Re(flat.LimitLo); _limitHi = Re(flat.LimitHi);
            _limitStiff = Re(flat.LimitStiff); _limitDamp = Re(flat.LimitDamp);
            _excess = Re(flat.Excess);
            _panelCentre = Re(flat.PanelCentre); _panelNormal = Re(flat.PanelNormal);
            _panelArea = Re(flat.PanelArea);

            _basePos0 = Cv(flat.BasePos); _baseRot0 = Cv(flat.BaseRot);
            _q0 = Cv(flat.Q); _qd0 = Cv(flat.Qd); _ballRot0 = Cv(flat.BallRot);
            _rootSpin0 = Cv(flat.RootSpin); _rootVel0 = Cv(flat.RootVel);

            _basePos = _acc.Allocate1D(_basePos0);
            _baseRot = _acc.Allocate1D(_baseRot0);
            _q = _acc.Allocate1D(_q0);
            _qd = _acc.Allocate1D(_qd0);
            _ballRot = _acc.Allocate1D(_ballRot0);
            _rootSpin = _acc.Allocate1D(_rootSpin0);
            _rootVel = _acc.Allocate1D(_rootVel0);
            _outPos = _acc.Allocate1D<Real>(flat.OutPos.Length);

            _driveHost = Cv(flat.DriveTorque);
            _posScratch = new Real[flat.OutPos.Length];

            _cfg = new GCfg
            {
                Dt = (Real)s.Dt,
                DragK = (Real)s.DragK,
                Gravity = (Real)s.Gravity,
                TissueDensity = (Real)s.TissueDensity,
                RestoringDensity = (Real)s.RestoringDensity,
                WorldDepth = (Real)s.WorldDepth,
                Damping = (Real)s.Damping,
                N = _n,
                Steps = 1,
                UseRestore = s.UseRestore ? 1 : 0,
                FloorRestores = s.FloorRestores ? 1 : 0,
            };

            var watch = Stopwatch.StartNew();
            _kernel = groupSize > 0
                ? _acc.LoadImplicitlyGroupedStreamKernel<Index1D, GTopo, GBody, GPanels, GState, GCfg>(
                    Featherstone.Kernel, groupSize)
                : _acc.LoadAutoGroupedStreamKernel<Index1D, GTopo, GBody, GPanels, GState, GCfg>(
                    Featherstone.Kernel);
            _acc.Synchronize();
            CompileMs = watch.Elapsed.TotalMilliseconds;
        }

        private MemoryBuffer1D<int, Stride1D.Dense> Int(int[] a) => _acc.Allocate1D(a);

        private MemoryBuffer1D<Real, Stride1D.Dense> Re(double[] a) => _acc.Allocate1D(Cv(a));

        private static Real[] Cv(double[] a)
        {
            var v = new Real[a.Length];
            for (int i = 0; i < a.Length; i++) v[i] = (Real)a[i];
            return v;
        }

        private static double[] Wd(Real[] a)
        {
            var v = new double[a.Length];
            for (int i = 0; i < a.Length; i++) v[i] = a[i];
            return v;
        }

        private GTopo Topo => new GTopo
        {
            Links = _links.View, Dof = _dof.View, Parent = _parent.View,
            DofCount = _dofCount.View, DofStart = _dofStart.View, PanelStart = _panelStart.View,
        };

        private GBody Body => new GBody
        {
            Mass = _mass.View, Inertia = _inertia.View, ChildAnchor = _childAnchor.View,
            ParentAnchor = _parentAnchor.View, JointFrame = _jointFrame.View,
            RestFrame = _restFrame.View, LimitLo = _limitLo.View, LimitHi = _limitHi.View,
            LimitStiff = _limitStiff.View, LimitDamp = _limitDamp.View,
            DriveTorque = _driveTorque.View, Lift = _lift.View, Excess = _excess.View,
        };

        private GPanels Panels => new GPanels
        {
            Centre = _panelCentre.View, Normal = _panelNormal.View, Area = _panelArea.View,
        };

        private GState State => new GState
        {
            BasePos = _basePos.View, BaseRot = _baseRot.View, Q = _q.View, Qd = _qd.View,
            BallRot = _ballRot.View, RootSpin = _rootSpin.View, RootVel = _rootVel.View,
            OutPos = _outPos.View,
        };

        /// <summary>Puts the state back as it was built, so a second run starts where the first did.</summary>
        public void ResetState()
        {
            _basePos.View.CopyFromCPU(_basePos0);
            _baseRot.View.CopyFromCPU(_baseRot0);
            _q.View.CopyFromCPU(_q0);
            _qd.View.CopyFromCPU(_qd0);
            _ballRot.View.CopyFromCPU(_ballRot0);
            _rootSpin.View.CopyFromCPU(_rootSpin0);
            _rootVel.View.CopyFromCPU(_rootVel0);
            _acc.Synchronize();
        }

        public void Launch(int steps)
        {
            GCfg cfg = _cfg;
            cfg.Steps = steps;
            _kernel(_n, Topo, Body, Panels, State, cfg);
        }

        public void Synchronize() => _acc.Synchronize();

        /// <summary>The drive targets going up — one metabolic step's worth of traffic.</summary>
        public void UploadDrive() => _driveTorque.View.CopyFromCPU(_driveHost);

        /// <summary>Every link's place coming down — what the world step needs each metabolic step.</summary>
        public void DownloadPositions() => _outPos.View.CopyToCPU(_posScratch);

        public double[] Positions() { DownloadPositions(); return Wd(_posScratch); }

        public double[] ReadQ() => Wd(_q.GetAsArray1D());

        public double[] ReadQd() => Wd(_qd.GetAsArray1D());

        /// <summary>FNV-1a over the raw bits of the whole state — the identity check.</summary>
        public ulong Digest()
        {
            ulong hash = 14695981039346656037UL;
            Bits(ref hash, _basePos.GetAsArray1D());
            Bits(ref hash, _baseRot.GetAsArray1D());
            Bits(ref hash, _q.GetAsArray1D());
            Bits(ref hash, _qd.GetAsArray1D());
            Bits(ref hash, _ballRot.GetAsArray1D());
            Bits(ref hash, _rootSpin.GetAsArray1D());
            Bits(ref hash, _rootVel.GetAsArray1D());
            Bits(ref hash, _outPos.GetAsArray1D());
            return hash;
        }

        private static void Bits(ref ulong hash, Real[] a)
        {
            byte[] raw = new byte[a.Length * 8];
            Buffer.BlockCopy(a, 0, raw, 0, raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                hash ^= raw[i];
                hash *= 1099511628211UL;
            }
        }

        public void Dispose()
        {
            _links.Dispose(); _dof.Dispose(); _parent.Dispose(); _dofCount.Dispose();
            _dofStart.Dispose(); _panelStart.Dispose();
            _mass.Dispose(); _lift.Dispose(); _inertia.Dispose(); _childAnchor.Dispose();
            _parentAnchor.Dispose(); _jointFrame.Dispose(); _restFrame.Dispose();
            _driveTorque.Dispose(); _limitLo.Dispose(); _limitHi.Dispose();
            _limitStiff.Dispose(); _limitDamp.Dispose(); _excess.Dispose();
            _panelCentre.Dispose(); _panelNormal.Dispose(); _panelArea.Dispose();
            _basePos.Dispose(); _baseRot.Dispose(); _q.Dispose(); _qd.Dispose();
            _ballRot.Dispose(); _rootSpin.Dispose(); _rootVel.Dispose(); _outPos.Dispose();
        }
    }
}
