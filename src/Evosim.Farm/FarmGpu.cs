#if EVOSIM_GPU
using System.Globalization;
using System.IO;
using Evosim.Dynamics;
using Evosim.Farm.Gpu;

namespace Evosim.Farm
{
    /// <summary>
    /// The farm's side of the gpu engine (logbook/specs/gpu-port-spec.md): its options from the
    /// launcher, its facts for run.json, and the counts the loop reads back from it.
    /// </summary>
    /// <remarks>
    /// <b>Compiled only where the engine is.</b> Evosim.Farm is also a Unity local package, and
    /// Unity has no ILGPU; everything that names the engine sits under <c>EVOSIM_GPU</c>, which
    /// only src/Evosim.Farm's own csproj defines.
    /// </remarks>
    internal static class FarmGpu
    {
        public static GpuOptions Options(EnvSettings s) => new GpuOptions
        {
            Device = s.GpuDevice,
            Precision = s.GpuPrecision,
            GroupSize = s.GpuGroup,
            Mean = s.GpuMean,
            Resync = s.GpuResync,
            Concurrent = s.GpuConcurrent,
        };

        /// <summary>The engine for the world <paramref name="solver"/> describes: refusals first, then the device.</summary>
        public static GpuBackend Open(EnvSettings s, SolverConfig solver) => new GpuBackend(solver, Options(s));

        public static GpuFacts Facts(GpuBackend gpu, string repoRoot)
        {
            GpuOptions o = gpu.Options;

            string kernelHash = string.IsNullOrEmpty(repoRoot)
                ? null
                : Manifest.HashSourceTree(Path.Combine(repoRoot, "src", "Evosim.Farm.Gpu"));

            var facts = new GpuFacts
            {
                Precision = o.Single ? "single" : "double",
                Device = gpu.DeviceName,
                Driver = gpu.Driver,
                GroupSize = gpu.GroupSize,
                Mean = o.SerialMean ? "serial" : "chunked",
                ClassLinks = string.Join("/", System.Array.ConvertAll(o.ClassLinks, x => x.ToString(CultureInfo.InvariantCulture))),
                ClassNeurons = string.Join("/", System.Array.ConvertAll(o.ClassNeurons, x => x.ToString(CultureInfo.InvariantCulture))),
                KernelHash = kernelHash ?? "unknown",
                CompileMs = gpu.CompileMs,
            };

            Note(facts, gpu);
            return facts;
        }

        /// <summary>The engine's counts as they stand, into the manifest's gpu block.</summary>
        public static void Note(RunManifest manifest, IStepBackend backend)
        {
            if (manifest.Gpu != null && backend is GpuBackend gpu) Note(manifest.Gpu, gpu);
        }

        private static void Note(GpuFacts facts, GpuBackend gpu)
        {
            long[] overflow = gpu.Overflow;
            facts.OverflowCandidates = overflow[0];
            facts.OverflowOverlaps = overflow[1];
            facts.OverflowHeld = overflow[2];
            facts.OverflowEntries = overflow[3];
            facts.RefusedForClass = gpu.RefusedForClass;
            facts.NeuronsAtCeiling = gpu.NeuronsAtCeiling;
            facts.NeuronsAtCeilingMost = gpu.NeuronsAtCeilingMost;
        }
    }
}
#endif
