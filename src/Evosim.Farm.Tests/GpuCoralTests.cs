using System;
using System.Collections.Generic;
using Evosim.Core;
using Evosim.Dynamics;
using Evosim.Farm.Gpu;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// D194's coral on the gpu engine (<c>logbook/specs/coral-build.md</c>, step 3): the rows GpuWorld packs
    /// against the rock's own readers, and the kernel in double on ILGPU's CPU accelerator against the solver,
    /// to the bit, with bodies resting on the coral, pressed into it, in its fade and away from it, in D204's
    /// square and in a tank.
    /// </summary>
    public class GpuCoralTests
    {
        private readonly ITestOutputHelper _out;

        public GpuCoralTests(ITestOutputHelper output) => _out = output;

        private static Dictionary<string, string> Square()
        {
            var extra = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> kv in GpuSquare.Settings) extra[kv.Key] = kv.Value;
            foreach (KeyValuePair<string, string> kv in GpuSquareCoralTests.Coral) extra[kv.Key] = kv.Value;
            return extra;
        }

        private static Dictionary<string, string> Tank() => new Dictionary<string, string>
        {
            { "EVOSIM_AREA", "1600" },
            { "EVOSIM_MATTER_ISLANDS", "25" }, { "EVOSIM_MATTER_ISLAND_COVER", "0.15" },
            { "EVOSIM_MATTER_ISLAND_DEPTH", "6" }, { "EVOSIM_MATTER_ISLAND_MARGIN", "4" },
            { "EVOSIM_REEF_SHAPE", "coral" },
        };

        private static (World core, SolverConfig solver) Build(bool perPart, Dictionary<string, string> extra) =>
            GpuTank.Build(perPart, reefs: false, extra: extra);

        [Fact]
        public void TheRowsAreTheRocksOwn()
        {
            (World core, SolverConfig s) = Build(false, Square());
            CoralRock rock = core.Coral;
            Assert.NotNull(rock);

            var w = new GpuWorld(s);
            Assert.Equal(rock.Count, w.CoralCount);
            Assert.Equal(rock.FadeMetres, w.CoralFade);
            Assert.Equal(rock.FadeFloor, w.CoralFloor);
            Assert.Equal(core.BoxLengthMetres, w.CoralPeriodX, 9);

            CoralGeometry g = rock.Coral;
            for (int c = 0; c < g.Count; c++)
            {
                int o = w.CoralColonyAt + c * 16;
                g.Bounds(c, out double x0, out double y0, out double z0, out double x1, out double y1, out double z1);
                Assert.Equal(new[] { x0, y0, z0, x1, y1, z1 }, new[] { w.WR[o], w.WR[o + 1], w.WR[o + 2], w.WR[o + 3], w.WR[o + 4], w.WR[o + 5] });
                Assert.Equal(g.ColonyAt(c).FirstPiece, (int)w.WR[o + 14]);
                Assert.Equal(g.ColonyAt(c).PieceCount, (int)w.WR[o + 15]);
            }

            for (int p = 0; p < g.PieceCount; p++)
            {
                int o = w.CoralPieceAt + p * 9;
                CoralGeometry.Piece piece = g.PieceAt(p);
                Assert.Equal(piece.Kind == CoralGeometry.PieceKind.Plate ? 1.0 : 0.0, w.WR[o]);
                Assert.Equal(new[] { piece.AX, piece.AY, piece.AZ, piece.BX, piece.BY, piece.BZ, piece.Radius, piece.HalfThickness },
                             new[] { w.WR[o + 1], w.WR[o + 2], w.WR[o + 3], w.WR[o + 4], w.WR[o + 5], w.WR[o + 6], w.WR[o + 7], w.WR[o + 8] });
            }

            Assert.Equal(w.CoralCellsX * w.CoralCellsZ + 1, w.CoralListAt - w.CoralGridAt);
            Assert.Equal(w.CoralCellsX * w.CoralCellsZ + 1, w.CoralFadeListAt - w.CoralFadeGridAt);
            _out.WriteLine($"{rock.Count} colonies, {g.PieceCount} pieces, a {w.CoralCellsX} by {w.CoralCellsZ} grid");
        }

        /// <summary>
        /// Bodies about the coral: on a colony's plate, pressed against its trunk, beside it in the fade, and in open water
        /// (between a seam and the coral's margin in the square, clear of the seam: the card folds a root into the
        /// box within its block, and the solver leaves that to the farm's Simulation), one of each a colony in turn.
        /// </summary>
        private static void Populate(DynamicsWorld world, World core, int count, ulong seed, int firstId)
        {
            var rng = new Rng(seed);
            CoralRock rock = core.Coral;
            double floor = -core.Config.WorldDepthMetres;

            for (int k = 0; k < count; k++)
            {
                int c = (k / 4) % rock.Count;
                rock.Coral.Bounds(c, out double x0, out double y0, out double z0, out double x1, out double y1, out double z1);
                double cx = 0.5 * (x0 + x1), cz = 0.5 * (z0 + z1);

                // The colony's first plate, else its last piece, and its trunk.
                CoralGeometry.Colony colony = rock.Coral.ColonyAt(c);
                CoralGeometry.Piece top = rock.Coral.PieceAt(colony.FirstPiece + colony.PieceCount - 1);
                for (int q = 0; q < colony.PieceCount; q++)
                {
                    CoralGeometry.Piece piece = rock.Coral.PieceAt(colony.FirstPiece + q);
                    if (piece.Kind == CoralGeometry.PieceKind.Plate) { top = piece; break; }
                }

                CoralGeometry.Piece trunk = rock.Coral.PieceAt(colony.FirstPiece);
                double topY = top.Kind == CoralGeometry.PieceKind.Plate ? top.AY + top.HalfThickness : Math.Max(top.AY, top.BY) + top.Radius;

                Vec3 at = (k % 4) switch
                {
                    0 => new Vec3(top.AX + 0.1, topY + 0.3, top.AZ - 0.1),
                    1 => new Vec3(0.5 * (trunk.AX + trunk.BX) + trunk.Radius + 0.2, 0.5 * (trunk.AY + trunk.BY), 0.5 * (trunk.AZ + trunk.BZ)),
                    2 => new Vec3(x1 + 2.0, 0.5 * (y0 + y1), cz),
                    _ => core.Config.WorldShape == WorldShape.Tank
                        ? new Vec3(core.TankRadiusMetres, floor + 3.0, core.TankRadiusMetres * 0.6)
                        : new Vec3(4.0, -9.0, core.BoxWidthMetres / 2),
                };

                if (at.Y > -1.5) at = new Vec3(at.X, -1.5, at.Z);
                world.AddInIdOrder(GpuTank.Body(firstId + k, world.Config, core, rng, at));
            }
        }

        private static void AssertSame(DynamicsWorld cpu, DynamicsWorld gpu, string when)
        {
            byte[] a = GpuTank.Snapshot(cpu), b = GpuTank.Snapshot(gpu);
            if (a.Length == b.Length && a.AsSpan().SequenceEqual(b)) return;

            Assert.Fail("The engines part " + when + ": " + GpuTank.FirstDifference(cpu, gpu));
        }

        [Theory]
        [InlineData("square", true)]
        [InlineData("square", false)]
        [InlineData("tank", true)]
        [InlineData("tank", false)]
        public void DoubleOnTheCpuAcceleratorIsTheSolverAroundTheCoral(string where, bool perPart)
        {
            Dictionary<string, string> extra = where == "square" ? Square() : Tank();
            (World coreA, SolverConfig a) = Build(perPart, extra);
            (World coreB, SolverConfig b) = Build(perPart, extra);
            Assert.NotNull(coreA.Coral);

            var cpu = new DynamicsWorld(a) { Threads = 2 };
            var gpu = new DynamicsWorld(b) { Threads = 2 };
            Populate(cpu, coreA, 8, 11UL, 1);
            Populate(gpu, coreB, 8, 11UL, 1);

            using var backend = new GpuBackend(gpu.Config, GpuTank.CpuDouble());
            gpu.UseBackend(backend);
            _out.WriteLine($"{where}: {coreA.Coral.Count} colonies of {coreA.Coral.Coral.PieceCount} pieces; kernels compiled in " +
                           backend.CompileMs.ToString("0") + " ms on " + backend.DeviceName);

            AssertSame(cpu, gpu, "before the first block");

            // Bodies start against the rock: roots within a metre of it, before any step pushes them off.
            int near = 0;
            foreach (Creature body in cpu.Creatures)
            {
                if (coreA.Coral.SignedDistance(body.Position[0], body.Position[1], body.Position[2], 1d, out _, out _) < 1d) near++;
            }

            for (int block = 0; block < 4; block++)
            {
                cpu.StepBlock(25);
                gpu.StepBlock(25);
                AssertSame(cpu, gpu, "after block " + block);

                if (block == 1)
                {
                    cpu.Remove(cpu.Creatures[0].Id);
                    gpu.Remove(gpu.Creatures[0].Id);
                    Populate(cpu, coreA, 2, 99UL, 50);
                    Populate(gpu, coreB, 2, 99UL, 50);
                }
            }

            Assert.Equal(0, backend.RefusedForClass);
            Assert.Equal(0L, backend.OverflowTotal);
            // The coral's fade was read: roots within it at the end.
            int faded = 0;
            foreach (Creature body in cpu.Creatures)
            {
                if (coreA.Coral.FadeAt(body.Position[0], body.Position[1], body.Position[2]) < 1d) faded++;
            }

            _out.WriteLine("pairs " + gpu.OverlapPairs + ", bed, glass or rock " + gpu.BedOrGlassBodies +
                           ", roots in the coral's fade at the end " + faded + ", within 1 m of its rock at the start " + near);
            Assert.True(faded > 0 && near > 0);
        }
    }
}
