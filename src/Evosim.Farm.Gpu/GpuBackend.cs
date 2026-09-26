using System;
using System.Globalization;
using System.Runtime.CompilerServices;
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

        long RefusedForClass { get; }

        /// <summary>Neurons at or past 1e36 in magnitude at the last readback, and the most at once.</summary>
        long NeuronsAtCeiling { get; }

        long NeuronsAtCeilingMost { get; }

        int GroupSize { get; }

        double CompileMs { get; }
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

        private readonly Context _context;
        private readonly Accelerator _accelerator;
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
        public GpuBackend(SolverConfig config, GpuOptions options)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            Options = options ?? new GpuOptions();
            Options.Validate();

            // The world's refusals first, before a context is made: they name settings, and a
            // refused run should not have to find a card to say so.
            var gpuWorld = new GpuWorld(config);
            _world = gpuWorld;

            if (Options.OnCpu)
            {
                var device = new CPUDevice(Options.CpuThreads, 1, 1);
                _context = Context.Create(b => b.CPU(device).EnableAlgorithms());
                _accelerator = _context.GetCPUDevice(0).CreateCPUAccelerator(_context, CPUAcceleratorMode.Parallel);
                DeviceName = "cpu-accelerator";
                Driver = "";
            }
            else
            {
                _context = Context.Create(b => b.Cuda().EnableAlgorithms());
                if (_context.GetCudaDevices().Count == 0)
                {
                    _context.Dispose();
                    throw new NotSupportedException(
                        "EVOSIM_GPU_DEVICE is cuda and ILGPU finds no CUDA device on this machine.");
                }

                var cuda = (CudaAccelerator)_context.GetCudaDevice(0).CreateAccelerator(_context);
                _accelerator = cuda;
                DeviceName = cuda.Name + " (" + cuda.NumMultiprocessors.ToString(CultureInfo.InvariantCulture) + " SMs)";
                Driver = cuda.DriverVersion.ToString();
            }

            try
            {
                _runner = Options.Single
                    ? (IGpuRunner)new Sgl.Runner(_accelerator, gpuWorld, Options)
                    : new Dbl.Runner(_accelerator, gpuWorld, Options);
            }
            catch
            {
                _accelerator.Dispose();
                _context.Dispose();
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

            _runner.Block(world, steps);
        }

        private bool _checked;

        /// <summary>
        /// The world the kernels were built for is the one being stepped: the same current, bed
        /// and reefs, the same step, the same contact model. Everything else the two configs
        /// hold came from one <c>RunConfig</c> through one factory.
        /// </summary>
        private void Check(SolverConfig stepped)
        {
            SolverConfig built = _world.Config;
            if (ReferenceEquals(stepped, built)) return;

            if (!ReferenceEquals(stepped.Current, built.Current) || !ReferenceEquals(stepped.Bed, built.Bed) ||
                !ReferenceEquals(stepped.Reefs, built.Reefs) || stepped.StepSeconds != built.StepSeconds ||
                stepped.ContactPerPart != built.ContactPerPart || stepped.CreatureContact != built.CreatureContact ||
                stepped.TankRadiusMetres != built.TankRadiusMetres)
            {
                throw new InvalidOperationException(
                    "The gpu engine was built for one world and handed another to step: its current, " +
                    "bed, reefs, step or contact model differ from the ones its kernels read.");
            }
        }

        /// <summary>A no-op: every block leaves the whole mirror current (the first build's choice).</summary>
        public void SyncMirror(DynamicsWorld world)
        {
        }

        public long[] Overflow => _runner.Overflow;

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

        public int GroupSize => _runner.GroupSize;

        public double CompileMs => _runner.CompileMs;

        /// <summary>The header's words after <c>engine</c>: <c>gpu single cpu-accelerator g4 classes 2/4/8/16</c>.</summary>
        public string HeaderToken() =>
            "gpu " + (Options.Single ? "single" : "double") + " " + ShortDevice() +
            " g" + GroupSize.ToString(CultureInfo.InvariantCulture) +
            " classes " + Options.ClassesToken() + (Options.Concurrent ? " concurrent" : "") +
            (Options.Transport ? " transport" : "");

        /// <summary>
        /// The snow's transport on this engine's card (<see cref="GpuTransport"/>), for the farm to
        /// hand the snow's grid when <see cref="GpuOptions.Transport"/> is on.
        /// </summary>
        public GpuTransport CreateTransport() => new GpuTransport(_accelerator);

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
            _accelerator?.Dispose();
            _context?.Dispose();
        }
    }
}
