using System;
using System.Globalization;

namespace Evosim.Farm.Gpu
{
    /// <summary>
    /// How the <c>gpu</c> engine runs: which device, which precision, which group size and which
    /// reduction order (logbook/specs/gpu-port-spec.md). None of it is a world rule, so none of it
    /// reaches a run's config or its hash; the manifest's engine block records it.
    /// </summary>
    public sealed class GpuOptions
    {
        /// <summary><c>cpu</c>, ILGPU's CPU accelerator (the transcription's test), or <c>cuda</c>.</summary>
        public string Device = "cuda";

        /// <summary><c>double</c> (the CPU's arithmetic) or <c>single</c> (the card's speed).</summary>
        public string Precision = "single";

        /// <summary>Threads a group on the card; 0 lets ILGPU choose. The CPU accelerator ignores it.</summary>
        public int GroupSize = 32;

        /// <summary>
        /// The contact grid's mean radius: <c>serial</c> sums in list order on one thread, which is
        /// the CPU's rounding exactly; <c>chunked</c> sums fixed chunks of the list and then the
        /// chunks, which is the same at any group size and not the CPU's last bit. Empty picks
        /// serial in double and chunked in single.
        /// </summary>
        public string Mean = "";

        /// <summary>Threads the CPU accelerator simulates a group with: the owner's load rule, four.</summary>
        public int CpuThreads = 4;

        /// <summary>
        /// A diagnostic: every body is uploaded again at every block, as if born. Changes no
        /// number when the mirror is right, which is what makes it a test of the mirror.
        /// </summary>
        public bool Resync;

        /// <summary>The size classes' link ceilings, ascending; the last must be 16.</summary>
        public int[] ClassLinks = { 2, 4, 8, 16 };

        /// <summary>The size classes' neuron ceilings, ascending, one per link ceiling.</summary>
        public int[] ClassNeurons = { 8, 32, 128, 256 };

        /// <summary>Overlap entries a body keeps a step; one past it is counted, never written.</summary>
        public int OverlapCap = 64;

        public bool OnCpu => string.Equals(Device, "cpu", StringComparison.OrdinalIgnoreCase);

        public bool Single => string.Equals(Precision, "single", StringComparison.OrdinalIgnoreCase);

        public bool SerialMean =>
            string.IsNullOrEmpty(Mean)
                ? !Single
                : string.Equals(Mean, "serial", StringComparison.OrdinalIgnoreCase);

        /// <summary>Refuses a value that is not one of the words above, naming the setting.</summary>
        public void Validate()
        {
            if (!OnCpu && !string.Equals(Device, "cuda", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "EVOSIM_GPU_DEVICE is '" + Device + "'; the gpu engine knows 'cpu' (ILGPU's CPU " +
                    "accelerator) and 'cuda'.");
            }

            if (!Single && !string.Equals(Precision, "double", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "EVOSIM_GPU_PRECISION is '" + Precision + "'; the gpu engine knows 'double' and 'single'.");
            }

            if (!string.IsNullOrEmpty(Mean) &&
                !string.Equals(Mean, "serial", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(Mean, "chunked", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "EVOSIM_GPU_MEAN is '" + Mean + "'; the gpu engine knows 'serial' and 'chunked'.");
            }

            if (GroupSize < 0 || GroupSize > 1024)
            {
                throw new ArgumentException(
                    "EVOSIM_GPU_GROUP is " + GroupSize.ToString(CultureInfo.InvariantCulture) +
                    "; a group is 0 (ILGPU's choice) to 1024 threads.");
            }

            if (CpuThreads < 1 || CpuThreads > 64)
            {
                throw new ArgumentException("The CPU accelerator's thread count must be 1 to 64.");
            }

            if (ClassLinks == null || ClassNeurons == null || ClassLinks.Length == 0 ||
                ClassLinks.Length != ClassNeurons.Length || ClassLinks.Length != KernelGenerator.ClassCount)
            {
                throw new ArgumentException(
                    "The gpu engine's size classes are " + KernelGenerator.ClassCount.ToString(CultureInfo.InvariantCulture) +
                    " pairs of link and neuron ceilings; the kernel is generated for that many.");
            }

            for (int c = 0; c < ClassLinks.Length; c++)
            {
                if (ClassLinks[c] != KernelGenerator.ClassLinks[c])
                {
                    throw new ArgumentException(
                        "A size class's link ceiling is compiled into its kernel: class " +
                        c.ToString(CultureInfo.InvariantCulture) + " holds " +
                        KernelGenerator.ClassLinks[c].ToString(CultureInfo.InvariantCulture) + " links.");
                }

                if (ClassNeurons[c] < 1 || (c > 0 && ClassNeurons[c] < ClassNeurons[c - 1]))
                {
                    throw new ArgumentException("The size classes' neuron ceilings must be positive and ascending.");
                }
            }

            if (OverlapCap < 1) throw new ArgumentException("The overlap capacity must be at least 1.");
        }

        /// <summary>The header's words: <c>gpu single 4090 g32 classes 2/4/8/16</c> without the device.</summary>
        public string ClassesToken() => string.Join("/", Array.ConvertAll(ClassLinks, x => x.ToString(CultureInfo.InvariantCulture)));
    }
}
