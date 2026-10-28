using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using Evosim.Core;
using Evosim.Dynamics;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;

[assembly: InternalsVisibleTo("Evosim.Farm.Tests")]

namespace Evosim.Farm.Gpu
{
    /// <summary>What a precision's runner offers the backend.</summary>
    internal interface IGpuRunner : IDisposable
    {
        void Block(DynamicsWorld world, int steps);

        /// <summary>Cumulative: candidates past the cap, overlaps past it, held ids past it, cell entries past it.</summary>
        long[] Overflow { get; }

        /// <summary>The probe (EVOSIM_GPU_PROBE), switched between blocks; it times and joins and moves no result.</summary>
        bool Probe { get; set; }

        /// <summary>The probe's last print, a line each; empty until it has printed.</summary>
        System.Collections.Generic.IReadOnlyList<string> ProbeLines { get; }

        long RefusedForClass { get; }

        /// <summary>Neurons at or past 1e36 in magnitude at the last readback, and the most at once.</summary>
        long NeuronsAtCeiling { get; }

        long NeuronsAtCeilingMost { get; }

        /// <summary>Cumulative: the bed's angles past the double guard's 1e9 rad, in either precision.</summary>
        long BedAnglesPastGuard { get; }

        int GroupSize { get; }

        double CompileMs { get; }

        /// <summary>The one brain's pools, for the tests: the slack over twice the live links before
        /// they are laid again (4,096), how often they have been, and the link pool's room.</summary>
        int PoolSlack { get; set; }

        long PoolRelays { get; }

        int LinkPoolCapacity { get; }

        /// <summary>D235, for the tests: the cells of the held-sniff columns across the classes (one a class in a world without the nose).</summary>
        long HeldCells { get; }

        /// <summary>D235, for the tests: the bytes of the held-sniff columns the blocks have uploaded so far (none in a world without the nose).</summary>
        long HeldBytesUp { get; }

        /// <summary>D235, for the tests: the rows of the nose-flag columns across the classes (one a class in a world without the nose, else the class's links).</summary>
        long NoseFlagRows { get; }

        /// <summary>The scent's fill on the card from the world's corpses (GpuScent), under <see cref="GpuOptions.Scent"/>.</summary>
        void FillScentOnCard(World world);
    }

    /// <summary>
    /// Where the engine's probe prints: standard error, or a world's own file when two worlds share
    /// the process (the farm's --pair). Per thread, as the farm's own writers are.
    /// </summary>
    public static class GpuConsole
    {
        [ThreadStatic] private static System.IO.TextWriter _error;

        public static System.IO.TextWriter Error => _error ?? Console.Error;

        public static void Use(System.IO.TextWriter error) => _error = error;
    }

    /// <summary>
    /// A context and its accelerator, which one engine opens for itself or several share
    /// (card-next-r57.md, part 4: two worlds in one farm process). An engine on a shared device
    /// keeps its work on a stream of its own and waits on that stream only, so one world's waits
    /// never wait on the other's kernels. The kernels compile once per context and are cached.
    /// </summary>
    public sealed class GpuDevice : IDisposable
    {
        internal Context Context { get; }

        internal Accelerator Accelerator { get; }

        /// <summary>The card's name and SM count, or <c>cpu-accelerator</c>.</summary>
        public string Name { get; }

        /// <summary>The CUDA driver's version, or empty on the CPU accelerator.</summary>
        public string Driver { get; }

        /// <summary>Held while an engine builds its runner on this device: one world's buffers and kernels at a time.</summary>
        internal object Gate { get; } = new object();

        public bool OnCpu { get; }

        private GpuDevice(Context context, Accelerator accelerator, string name, string driver, bool onCpu)
        {
            Context = context;
            Accelerator = accelerator;
            Name = name;
            Driver = driver;
            OnCpu = onCpu;
        }

        /// <summary>The device <paramref name="options"/> names: the CPU accelerator at its thread count, or the first card.</summary>
        public static GpuDevice Open(GpuOptions options)
        {
            if (options.OnCpu)
            {
                var device = new CPUDevice(options.CpuThreads, 1, 1);
                var context = Context.Create(b => b.CPU(device).EnableAlgorithms());
                var accelerator = context.GetCPUDevice(0).CreateCPUAccelerator(context, CPUAcceleratorMode.Parallel);
                return new GpuDevice(context, accelerator, "cpu-accelerator", "", true);
            }
            else
            {
                var context = Context.Create(b => b.Cuda().EnableAlgorithms());
                if (context.GetCudaDevices().Count == 0)
                {
                    context.Dispose();
                    throw new NotSupportedException(
                        "EVOSIM_GPU_DEVICE is cuda and ILGPU finds no CUDA device on this machine.");
                }

                var cuda = (CudaAccelerator)context.GetCudaDevice(0).CreateAccelerator(context);
                string name = cuda.Name + " (" + cuda.NumMultiprocessors.ToString(CultureInfo.InvariantCulture) + " SMs)";
                return new GpuDevice(context, cuda, name, cuda.DriverVersion.ToString(), false);
            }
        }

        public void Dispose()
        {
            Accelerator?.Dispose();
            Context?.Dispose();
        }
    }

    /// <summary>
    /// The farm's <c>gpu</c> engine (logbook/specs/gpu-port-spec.md): the whole physics step of
    /// <see cref="DynamicsWorld"/> run on a card, or on ILGPU's CPU accelerator, a block at a
    /// time, with the <see cref="Creature"/> objects on the host as the mirror the farm reads.
    /// </summary>
    public sealed class GpuBackend : IStepBackend
    {
        /// <summary>The longest block the instants buffer is first sized for; it grows past it.</summary>
        public const int BlockCeiling = 1000;

        private readonly GpuDevice _device;
        private readonly bool _ownsDevice;
        private readonly Accelerator _accelerator;
        private readonly AcceleratorStream _stream;
        private readonly IGpuRunner _runner;
        private readonly GpuWorld _world;

        public GpuOptions Options { get; }

        /// <summary>The card's name and SM count, or <c>cpu-accelerator</c>.</summary>
        public string DeviceName { get; }

        /// <summary>The CUDA driver's version, or empty on the CPU accelerator.</summary>
        public string Driver { get; }

        public string Name => "gpu";

        public string Precision => Options.Single ? "single" : "double";

        public int PreferredBlockSteps => BlockCeiling;

        /// <summary>
        /// Opens the device and compiles the kernels for the world <paramref name="config"/>
        /// describes. The farm builds it from <c>SolverConfig.FromWorld</c> before the run
        /// directory exists, so a refused world makes no directory; the blocks are then taken on
        /// the <see cref="DynamicsWorld"/> the simulation built from the same world, which
        /// <see cref="StepBlock"/> checks by the objects they share.
        /// </summary>
        /// <param name="shared">
        /// A device other engines step on too (<see cref="GpuDevice"/>), which this engine neither
        /// opens nor closes; null opens one of its own, as an engine always did.
        /// </param>
        public GpuBackend(SolverConfig config, GpuOptions options, GpuDevice shared = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            Options = options ?? new GpuOptions();
            Options.Validate();

            // The world's refusals first, before a context is made: they name settings, and a
            // refused run should not have to find a card to say so.
            var gpuWorld = new GpuWorld(config);
            _world = gpuWorld;

            if (shared != null && shared.OnCpu != Options.OnCpu)
            {
                throw new InvalidOperationException(
                    "The shared device is " + shared.Name + " and this engine's options name " +
                    (Options.OnCpu ? "the CPU accelerator" : "a card") + ".");
            }

            _ownsDevice = shared == null;
            _device = shared ?? GpuDevice.Open(Options);
            _accelerator = _device.Accelerator;
            DeviceName = _device.Name;
            Driver = _device.Driver;

            try
            {
                // An engine alone on its device works on the default stream, every call as it always
                // was; one of several works on a stream of its own (GpuDevice).
                lock (_device.Gate)
                {
                    if (!_ownsDevice) _accelerator.Bind();
                    _stream = _ownsDevice ? _accelerator.DefaultStream : _accelerator.CreateStream();
                    _runner = Options.Single
                        ? (IGpuRunner)new Sgl.Runner(_accelerator, _stream, gpuWorld, Options)
                        : new Dbl.Runner(_accelerator, _stream, gpuWorld, Options);
                }
            }
            catch
            {
                if (!_ownsDevice) _stream?.Dispose();
                if (_ownsDevice) _device.Dispose();
                throw;
            }
        }

        public void StepBlock(DynamicsWorld world, int steps)
        {
            if (!_checked)
            {
                Check(world.Config);
                _checked = true;
            }

            // A shared device is stepped from each world's own thread, and a CUDA context is
            // current per thread.
            if (!_ownsDevice) _accelerator.Bind();
            _runner.Block(world, steps);
        }

        private bool _checked;

        /// <summary>
        /// The world the kernels were built for is the one being stepped: the same current, bed
        /// and reefs, the same step, the same contact model. Everything else the two configs
        /// hold came from one <c>RunConfig</c> through one factory.
        /// </summary>
        private void Check(SolverConfig stepped) => Check(_world.Config, stepped);

        /// <summary><see cref="Check(SolverConfig)"/> for two configs, so that a test can hold it without a device.</summary>
        internal static void Check(SolverConfig built, SolverConfig stepped)
        {
            if (ReferenceEquals(stepped, built)) return;

            if (!ReferenceEquals(stepped.Current, built.Current) || !ReferenceEquals(stepped.Bed, built.Bed) ||
                !ReferenceEquals(stepped.Reefs, built.Reefs) || !ReferenceEquals(stepped.Coral, built.Coral) || stepped.StepSeconds != built.StepSeconds ||
                stepped.PaceFactor != built.PaceFactor ||
                stepped.ContactPerPart != built.ContactPerPart || stepped.CreatureContact != built.CreatureContact ||
                stepped.TankRadiusMetres != built.TankRadiusMetres ||
                stepped.ShapedAddedMass != built.ShapedAddedMass || stepped.StrokePushesAssembly != built.StrokePushesAssembly ||
                stepped.ContactShape != built.ContactShape ||
                stepped.MirroredDrive != built.MirroredDrive ||
                (stepped.Nose != null) != (built.Nose != null) ||
                stepped.SeamLengthMetres != built.SeamLengthMetres || stepped.SeamWidthMetres != built.SeamWidthMetres)
            {
                throw new InvalidOperationException(
                    "The gpu engine was built for one world and handed another to step: its current, " +
                    "bed, reefs, step, pace factor, contact model, contact shape, added-mass law, stroke assembly, mirrored-joint drive or nose differ from the ones its kernels read.");
            }
        }

        /// <summary>A no-op: every block leaves the whole mirror current (the first build's choice).</summary>
        public void SyncMirror(DynamicsWorld world)
        {
        }

        public long[] Overflow => _runner.Overflow;

        /// <summary>
        /// The card probe, on from the start under EVOSIM_GPU_PROBE=1 or switched on by the farm's pace guard
        /// while the world runs. It joins the card between kernels and times them, and moves no result.
        /// </summary>
        public bool Probe
        {
            get => _runner.Probe;
            set => _runner.Probe = value;
        }

        /// <summary>The probe's last print, a line each, every twenty blocks while it is on.</summary>
        public System.Collections.Generic.IReadOnlyList<string> ProbeLines => _runner.ProbeLines;

        public long OverflowTotal
        {
            get
            {
                long total = 0;
                foreach (long n in _runner.Overflow) total += n;
                return total;
            }
        }

        public long RefusedForClass => _runner.RefusedForClass;

        public long NeuronsAtCeiling => _runner.NeuronsAtCeiling;

        public long NeuronsAtCeilingMost => _runner.NeuronsAtCeilingMost;

        /// <summary>
        /// How many of the bed's angles have passed 1e9 rad since the engine opened, counted on the
        /// device in the water's bed sample. In double the card reduces such an angle before its sine
        /// (KernelGenerator, AngleGuard), so the body that took it parts from the CPU's arithmetic;
        /// the tests read this to drop a comparison only where the guard acted.
        /// </summary>
        public long BedAnglesPastGuard => _runner.BedAnglesPastGuard;

        public int GroupSize => _runner.GroupSize;

        public double CompileMs => _runner.CompileMs;

        /// <summary>The one brain's pools' slack (a test hook; negative lays them again every block).</summary>
        internal int PoolSlack { get => _runner.PoolSlack; set => _runner.PoolSlack = value; }

        internal long PoolRelays => _runner.PoolRelays;

        internal int LinkPoolCapacity => _runner.LinkPoolCapacity;

        internal long HeldCells => _runner.HeldCells;

        internal long HeldBytesUp => _runner.HeldBytesUp;

        internal long NoseFlagRows => _runner.NoseFlagRows;

        /// <summary>The header's words after <c>engine</c>: <c>gpu single cpu-accelerator g4 classes 2/4/8/16</c>.</summary>
        public string HeaderToken() =>
            "gpu " + (Options.Single ? "single" : "double") + " " + ShortDevice() +
            " g" + GroupSize.ToString(CultureInfo.InvariantCulture) +
            " classes " + Options.ClassesToken() + (Options.Concurrent ? " concurrent" : "") +
            (Options.Transport ? " transport" : "") + (Options.Scent ? " scent" : "");

        /// <summary>
        /// The scent's fill on this engine's card (C3, <see cref="GpuScent"/>): the world hands its
        /// corpses over where it would have filled, and the card fills the grid its kernels read.
        /// The farm calls it under <see cref="GpuOptions.Scent"/>.
        /// </summary>
        public void FillScentOnCard(World world) => _runner.FillScentOnCard(world);

        /// <summary>
        /// The snow's transport on this engine's card (<see cref="GpuTransport"/>), for the farm to
        /// hand the snow's grid when <see cref="GpuOptions.Transport"/> is on.
        /// </summary>
        public GpuTransport CreateTransport() => new GpuTransport(_accelerator, _stream);

        private string ShortDevice()
        {
            if (Options.OnCpu) return "cpu-accelerator";

            string name = DeviceName;
            int paren = name.IndexOf(" (", StringComparison.Ordinal);
            if (paren > 0) name = name.Substring(0, paren);

            string[] words = name.Split(' ');
            return words.Length > 0 ? words[words.Length - 1] : name;
        }

        public void Dispose()
        {
            _runner?.Dispose();
            if (_ownsDevice) _device?.Dispose();
            else _stream?.Dispose();
        }
    }
}
