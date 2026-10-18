using System;
using Evosim.Farm.Gpu.Sgl;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using Xunit;
using Real = System.Single;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// The grid's mean, one group a chunk (WholeStep.MeanGroups, card-next-r57.md part 5), against the
    /// chunk-a-thread kernel it replaces (MeanChunks), on ILGPU's CPU accelerator in single precision, the
    /// card's: every partial sum, every count and the cell to the bit. A crowd of round 57's size in chunks of
    /// 256 with a short last chunk, bodies refused for their class and inactive ones, one to 16 links, spheres
    /// with a NaN or an infinity in any coordinate, per-part and body spheres, the redo pass that runs and the
    /// one that returns, at groups of 4 and of 256 threads.
    /// </summary>
    public class GpuMeanTests
    {
        private const int Count = 7000, Slots = 7200;

        [Theory]
        [InlineData(4, 1)]
        [InlineData(8, 32)]
        public void TheGroupedMeanIsTheChunkThreadsMeanToTheBit(int warp, int warps)
        {
            using Context context = Context.Create(b => b.CPU(new CPUDevice(warp, warps, 1)).EnableAlgorithms());
            using Accelerator acc = context.GetCPUDevice(0).CreateCPUAccelerator(context, CPUAcceleratorMode.Parallel);
            var chunked = acc.LoadAutoGroupedStreamKernel<Index1D, SSph, SGlob, SGrid, SCfg>(WholeStep.MeanChunks);
            var grouped = acc.LoadStreamKernel<SSph, SGlob, SGrid, SCfg>(WholeStep.MeanGroups);
            var combine = acc.LoadAutoGroupedStreamKernel<Index1D, SGrid, SCfg>(WholeStep.MeanCombine);
            int group = Math.Min(WholeStep.MeanGroupBodies, acc.MaxNumThreadsPerGroup);
            Assert.True(group == (warp * warps < 256 ? warp * warps : 256), $"group {group}");

            var rng = new Random(57);
            int ls = WholeStep.LinkStride;

            // The crowd: a shuffled slot for each rank, some refused (-1), some inactive.
            var rankToSlot = new int[Count];
            var order = new int[Slots];
            for (int i = 0; i < Slots; i++) order[i] = i;
            for (int i = Slots - 1; i > 0; i--) { int j = rng.Next(i + 1); (order[i], order[j]) = (order[j], order[i]); }
            for (int r = 0; r < Count; r++) rankToSlot[r] = rng.Next(50) == 0 ? -1 : order[r];
            var active = new int[Slots];
            var links = new int[Slots];
            for (int g = 0; g < Slots; g++) { active[g] = rng.Next(20) == 0 ? 0 : 1; links[g] = 1 + rng.Next(ls); }

            using var vRank = acc.Allocate1D(rankToSlot);
            using var vActive = acc.Allocate1D(active);
            using var vLinks = acc.Allocate1D(links);
            var body = new MemoryBuffer1D<Real, Stride1D.Dense>[4];
            var link = new MemoryBuffer1D<Real, Stride1D.Dense>[4];
            for (int k = 0; k < 4; k++)
            {
                body[k] = acc.Allocate1D(Spheres(rng, Slots, k == 3));
                link[k] = acc.Allocate1D(Spheres(rng, Slots * ls, k == 3));
            }

            try
            {
                var s = new SSph
                {
                    Cx = body[0].View, Cy = body[1].View, Cz = body[2].View, R = body[3].View,
                    LCx = link[0].View, LCy = link[1].View, LCz = link[2].View, LR = link[3].View,
                    Active = vActive.View,
                };
                var gl = new SGlob { RankToSlot = vRank.View, Links = vLinks.View };
                int chunks = (Count + WholeStep.MeanGroupBodies - 1) / WholeStep.MeanGroupBodies;

                foreach (int perPart in new[] { 1, 0 })
                {
                    foreach ((int redo, int flag) in new[] { (0, 0), (1, 1), (1, 0) })
                    {
                        var cfg = new SCfg
                        {
                            Count = Count, Chunk = WholeStep.MeanGroupBodies, Chunks = chunks, PerPart = perPart, Redo = redo,
                        };
                        string at = $"group {group}, per part {perPart}, redo {redo} flag {flag}";

                        (Real[] p1, int[] n1, Real c1) = Run(acc, chunks, flag, gr => { chunked(chunks, s, gl, gr, cfg); combine(1, gr, cfg); });
                        (Real[] p2, int[] n2, Real c2) = Run(acc, chunks, flag, gr => { grouped(new KernelConfig(chunks, group), s, gl, gr, cfg); combine(1, gr, cfg); });

                        for (int c = 0; c < chunks; c++)
                        {
                            Assert.True(BitConverter.SingleToInt32Bits(p1[c]) == BitConverter.SingleToInt32Bits(p2[c]),
                                $"{at}: chunk {c} sums {p2[c]:R}, the chunk thread {p1[c]:R}");
                            Assert.True(n1[c] == n2[c], $"{at}: chunk {c} counts {n2[c]}, the chunk thread {n1[c]}");
                        }
                        Assert.True(BitConverter.SingleToInt32Bits(c1) == BitConverter.SingleToInt32Bits(c2), $"{at}: cell {c2:R} against {c1:R}");

                        // The redo that returns leaves the sentinels; every other case wrote a real mean.
                        if (redo == 1 && flag == 0) Assert.Equal(-7f, p2[0]);
                        else Assert.True(n2[0] > 0 && p2[0] > 0f, $"{at}: chunk 0 holds {n2[0]} spheres");
                    }
                }
            }
            finally
            {
                for (int k = 0; k < 4; k++) { body[k].Dispose(); link[k].Dispose(); }
            }
        }

        /// <summary>One coordinate column, or the radii; a NaN or an infinity in about one value in 30.</summary>
        private static Real[] Spheres(Random rng, int n, bool radius)
        {
            var v = new Real[n];
            for (int i = 0; i < n; i++)
            {
                int bad = rng.Next(60);
                v[i] = bad == 0 ? Real.NaN : bad == 1 ? Real.PositiveInfinity
                    : radius ? (Real)(0.05 + 0.5 * rng.NextDouble()) : (Real)(200.0 * rng.NextDouble() - 100.0);
            }
            return v;
        }

        /// <summary>The partial sums, counts and cell one mean leaves, from sentinels of -7.</summary>
        private static (Real[], int[], Real) Run(Accelerator acc, int chunks, int flag, Action<SGrid> launch)
        {
            var sentinel = new Real[chunks];
            var sentinelN = new int[chunks];
            Array.Fill(sentinel, -7f);
            Array.Fill(sentinelN, -7);
            using var partial = acc.Allocate1D(sentinel);
            using var partialN = acc.Allocate1D(sentinelN);
            using var cell = acc.Allocate1D(new Real[] { -7f });
            using var stray = acc.Allocate1D(new[] { 0, flag, 0 });
            var gr = new SGrid { Partial = partial.View, PartialN = partialN.View, Cell = cell.View, Stray = stray.View };
            launch(gr);
            acc.Synchronize();
            return (partial.GetAsArray1D(), partialN.GetAsArray1D(), cell.GetAsArray1D()[0]);
        }
    }
}
