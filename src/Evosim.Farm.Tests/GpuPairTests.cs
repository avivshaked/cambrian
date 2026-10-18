using System;
using System.Collections.Generic;
using System.Threading;
using Evosim.Core;
using Evosim.Dynamics;
using Evosim.Farm.Gpu;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// Two worlds in one process on one device (card-next-r57.md, part 4): two different worlds, each
    /// with its engine on one shared <see cref="GpuDevice"/> and stepped from a thread of its own at the
    /// same time, are each the same world stepped alone on a device of its own, to the bit after every
    /// block, a death and a birth included. On ILGPU's CPU accelerator, in both precisions, at a test's
    /// scale.
    /// </summary>
    public class GpuPairTests
    {
        private const int Blocks = 4, Steps = 25;

        private readonly ITestOutputHelper _out;

        public GpuPairTests(ITestOutputHelper output) => _out = output;

        [Theory]
        [InlineData("double")]
        [InlineData("single")]
        public void EachWorldOfAPairIsTheWorldAlone(string precision)
        {
            var options = new GpuOptions { Device = "cpu", Precision = precision, CpuThreads = 4 };

            // Alone: each world on a device of its own, one after the other.
            List<byte[]> aloneA = Run(Make(true, true, 11UL), options, null);
            List<byte[]> aloneB = Run(Make(false, false, 23UL), options, null);
            Assert.False(aloneA[Blocks].AsSpan().SequenceEqual(aloneB[Blocks]), "the two worlds must differ, or a crossed pair could pass");

            // Together: both engines on one device, each world stepped on its own thread at once.
            using var device = GpuDevice.Open(options);
            List<byte[]> pairA = null, pairB = null;
            Exception failA = null, failB = null;
            var start = new Barrier(2);
            var a = new Thread(() =>
            {
                try { pairA = Run(Make(true, true, 11UL), options, device, start); }
                catch (Exception e) { failA = e; try { start.RemoveParticipant(); } catch (InvalidOperationException) { } }
            });
            var b = new Thread(() =>
            {
                try { pairB = Run(Make(false, false, 23UL), options, device, start); }
                catch (Exception e) { failB = e; try { start.RemoveParticipant(); } catch (InvalidOperationException) { } }
            });
            a.Start(); b.Start();
            a.Join(); b.Join();

            Assert.True(failA == null, "world A failed in the pair: " + failA);
            Assert.True(failB == null, "world B failed in the pair: " + failB);

            for (int k = 0; k <= Blocks; k++)
            {
                string when = k == 0 ? "before the first block" : "after block " + (k - 1);
                Assert.True(aloneA[k].AsSpan().SequenceEqual(pairA[k]), "world A parts from itself alone " + when);
                Assert.True(aloneB[k].AsSpan().SequenceEqual(pairB[k]), "world B parts from itself alone " + when);
            }
        }

        private static (DynamicsWorld world, World core) Make(bool perPart, bool reefs, ulong seed)
        {
            (World core, SolverConfig solver) = GpuTank.Build(perPart, reefs);
            var world = new DynamicsWorld(solver) { Threads = 2 };
            Populate(world, core, 6, seed, 1);
            return (world, core);
        }

        private static void Populate(DynamicsWorld world, World core, int count, ulong seed, int firstId)
        {
            var rng = new Rng(seed);
            Vec3 axis = GpuTank.Axis(core);

            for (int k = 0; k < count; k++)
            {
                // GpuCpuAcceleratorTests' cluster: bodies that touch, one at the glass and one on the bed.
                var at = new Vec3(axis.X + 0.7 * (k % 3) - 0.7, -9.0 - 0.6 * (k / 3), axis.Z + 0.5 * (k % 2));
                if (k == 4) at = new Vec3(axis.X + core.TankRadiusMetres - 0.4, -6.0, axis.Z);
                if (k == 5) at = new Vec3(axis.X - 2.0, -core.Config.WorldDepthMetres + 0.6, axis.Z - 2.0);
                world.AddInIdOrder(GpuTank.Body(firstId + k, world.Config, core, rng, at));
            }
        }

        /// <summary>The world's snapshot before the first block and after each, with a death and a birth after the second.</summary>
        private List<byte[]> Run((DynamicsWorld world, World core) w, GpuOptions options, GpuDevice device, Barrier start = null)
        {
            using var backend = new GpuBackend(w.world.Config, options, device);
            w.world.UseBackend(backend);
            _out.WriteLine((device == null ? "alone" : "paired") + ": kernels in " + backend.CompileMs.ToString("0") + " ms");

            var shots = new List<byte[]> { GpuTank.Snapshot(w.world) };
            start?.SignalAndWait();

            for (int block = 0; block < Blocks; block++)
            {
                w.world.StepBlock(Steps);
                shots.Add(GpuTank.Snapshot(w.world));

                if (block == 1)
                {
                    w.world.Remove(w.world.Creatures[0].Id);
                    Populate(w.world, w.core, 1, 99UL, 50);
                }
            }

            Assert.Equal(0, backend.RefusedForClass);
            Assert.Equal(0L, backend.OverflowTotal);
            return shots;
        }
    }
}
