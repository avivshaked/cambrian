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
            Transport = s.GpuTransport,
            Scent = s.GpuScent,
        };

        /// <summary>
        /// The engine for the world <paramref name="solver"/> describes: refusals first, then the device,
        /// its own or <paramref name="shared"/> with another world in the process (Program.Pair).
        /// </summary>
        public static GpuBackend Open(
            EnvSettings s, SolverConfig solver, Evosim.Core.PartShapeRegistry shapes, GpuDevice shared = null)
        {
            // D233's family carries its form to the host's contact as a hull of core points (ConvexCore.OfFamily).
            // The card packs every core as a box's half-extents, so a family link would touch there as the box of its
            // half-extents and part from the host at its first touch. A world develops only the shapes its list holds,
            // so a list with no shape that takes a form holds no family part. This stands until the card's port.
            if (shapes == null) throw new System.ArgumentNullException(nameof(shapes));
            for (int i = 0; i < shapes.Count; i++)
            {
                if (shapes[i].TakesForm)
                {
                    throw new System.InvalidOperationException(
                        "the gpu engine has no family shape ('" + shapes[i].Id + "' takes a form, D233); " +
                        "run this world on the cpu engine");
                }
            }

            // The card carries the limb push, the bend-and-power drive and its senses, and the scent
            // (card-audit-r57.md). The bed's friction is in the CPU solver only, so a world with it
            // would part from the CPU's at its first slide.
            if (solver.BedFriction > 0)
            {
                throw new System.InvalidOperationException(
                    "the gpu engine has no bed's friction (EVOSIM_BED_FRICTION); run this world on the cpu engine");
            }

            // D157 makes single the card's engine. In double, ILGPU 1.5.3 computes sin, cos and exp on
            // the card in loops whose length grows with the argument, so a far body can hold one
            // thread for minutes (card-audit-r57.md, the card in double hangs on a far body). The CPU
            // accelerator keeps double for the transcription proofs, where these are .NET's own.
            GpuOptions options = Options(s);
            if (!options.OnCpu && !options.Single)
            {
                throw new System.InvalidOperationException(
                    "the gpu engine runs on the card in single only (EVOSIM_GPU_PRECISION=single): ILGPU's " +
                    "double sine on the card loops with its argument, so a far body hangs it");
            }

            // D146's one brain is on the card (card-audit-r57.md, the central brain). D146's open
            // measurement of it on the card against the cpu is the speed work's finish-line pace read:
            // the card in single against the cpu at 16 threads on round 57's world at its largest
            // crowd, round trip included (card-single-plan.md, what finished looks like).

            return new GpuBackend(solver, options, shared);
        }

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

        /// <summary>The card's overflow counts this run (four), or null on the CPU solver, which has no caps.</summary>
        public static long[] Overflow(IStepBackend backend) => backend is GpuBackend gpu ? gpu.Overflow : null;

        /// <summary>Whether the engine has a card probe for the pace guard to switch on.</summary>
        public static bool CanProbe(IStepBackend backend) => backend is GpuBackend;

        /// <summary>The card probe on, from the next block (PaceGuard).</summary>
        public static void Probe(IStepBackend backend)
        {
            if (backend is GpuBackend gpu) gpu.Probe = true;
        }

        /// <summary>The card probe's last print, a line each, or none.</summary>
        public static System.Collections.Generic.IReadOnlyList<string> ProbeLines(IStepBackend backend) =>
            backend is GpuBackend gpu ? gpu.ProbeLines : System.Array.Empty<string>();

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
