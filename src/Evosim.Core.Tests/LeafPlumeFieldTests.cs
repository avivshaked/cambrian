using System;
using System.IO;
using System.Linq;
using System.Text;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D270's carried leaf smell, the grid's half: what the leaves deposit stays what the passes conserve, the
    /// decay takes its share to the bit, a steady source leaves a plume that goes downstream, the table of slopes
    /// has the right signs against the bed and the seams, the reads are continuous across a cell, and the world's
    /// step and checkpoint carry it.
    /// </summary>
    public class LeafPlumeFieldTests
    {
        private readonly ITestOutputHelper _output;

        public LeafPlumeFieldTests(ITestOutputHelper output) => _output = output;

        private const float Side = 120f;
        private const int Patches = 4;
        private const int Across = 2;
        private const float Depth = 25f;
        private const float Cell = 5f;
        private const float Mixing = 0.02f;
        private const ulong RunSeed = 20261031UL;

        private static BedShape Floor() =>
            BedShape.ForBox(Side, Side, Depth, 8f, 0f, Rng.SeedFor(RunSeed, World.BedShapeIndex), 0f, 0f);

        private static CurrentField Ocean(BedShape bed, float speed = 0.1f, float period = 6000f)
        {
            var field = new CurrentField { Mode = CurrentMode.Ocean, Speed = speed, PeriodSeconds = period, AdvectFields = true };
            field.SetBox(
                Side / Across, Patches, Depth, Rng.SeedFor(RunSeed, World.CurrentFieldIndex),
                patchesAcross: Across, shape: WorldShape.Box, bed: bed);
            return field;
        }

        private static LeafPlumeField Plume(BedShape bed) =>
            new LeafPlumeField(new GridField(
                Side * Side, 0f, Depth, 0f, 0f, Patches, Cell, Across, WorldShape.Box, 0f, bed, null, null));

        private static Float3 Centre(LeafPlumeField f, int ix, int iy, int iz) =>
            new Float3((ix + 0.5f) * Cell, -(iy + 0.5f) * Cell, (iz + 0.5f) * Cell);

        /// <summary>One source of the given area at a point, in the world's layout (x, y, z, area).</summary>
        private static float[] Source(float x, float y, float z, float area) => new[] { x, y, z, area };

        /// <summary>Every live cell set to a density by a function of its indices, by one deposit at its centre.</summary>
        private static void Fill(LeafPlumeField f, Func<int, int, int, double> density)
        {
            double volume = f.Grid.CellVolume;
            for (int iy = 0; iy < f.CellsY; iy++)
            for (int ix = 0; ix < f.CellsX; ix++)
            for (int iz = 0; iz < f.CellsZ; iz++)
            {
                if (!f.Grid.IsLive(ix, iy, iz)) continue;
                f.Deposit(Centre(f, ix, iy, iz), density(ix, iy, iz) * volume);
            }

            f.RebuildGradient();
        }

        // ------------------------------------------------------------------ the books

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void WithoutDecayTheStockStaysWhatTheLeavesEmittedToOnePartInATrillion(bool onTheFloor)
        {
            BedShape bed = onTheFloor ? Floor() : null;
            LeafPlumeField f = Plume(bed);
            CurrentField current = Ocean(bed);

            float[] sources = Source(40f, -8f, 30f, 3f)
                .Concat(Source(95f, -3f, 80f, 1.5f)).Concat(Source(10f, -20f, 100f, 0.5f)).ToArray();

            double emitted = 0d;
            for (int step = 0; step < 400; step++)
            {
                emitted += f.DepositSources(sources, 3, 1f);
                f.Mix(1f, Mixing);
                f.Advect(current, step, 1f);
            }

            double held = f.Recount();
            _output.WriteLine($"emitted {emitted:R}, held {held:R}, relative {Math.Abs(held - emitted) / emitted:E2}");
            Assert.True(Math.Abs(held - emitted) <= 1e-12 * emitted);
            Assert.Equal(0, f.CountUnsoundCells());
        }

        [Fact]
        public void WithDecayTheStockIsWhatWasEmittedLessWhatDecayed()
        {
            LeafPlumeField f = Plume(null);
            CurrentField current = Ocean(null);
            float[] sources = Source(40f, -8f, 30f, 3f).Concat(Source(95f, -3f, 80f, 1.5f)).ToArray();

            double emitted = 0d, decayed = 0d;
            for (int step = 0; step < 400; step++)
            {
                (double e, double d) = f.Step(sources, 2, 1f, Mixing, current, step, 3000f);
                emitted += e;
                decayed += d;
            }

            double held = f.Recount();
            _output.WriteLine($"emitted {emitted:R}, decayed {decayed:R}, held {held:R}");
            Assert.True(decayed > 0d);
            Assert.True(Math.Abs(held - (emitted - decayed)) <= 1e-9 * emitted);

            // The running total the field keeps closes against the same books.
            Assert.True(Math.Abs(f.TotalStock - held) <= 1e-9 * emitted);
        }

        [Fact]
        public void AUniformPlumeStaysUniformForSixHundredSeconds()
        {
            foreach (BedShape bed in new BedShape[] { null, Floor() })
            {
                LeafPlumeField f = Plume(bed);
                CurrentField current = Ocean(bed);
                Fill(f, (ix, iy, iz) => 2.5d);
                double before = f.Grid.DensityCoefficientOfVariation();
                Assert.True(before < 1e-12, "the fill is not uniform: " + before);

                for (int step = 0; step < 600; step++)
                {
                    f.Mix(1f, Mixing);
                    f.Advect(current, step, 1f);
                }

                double cv = f.Grid.DensityCoefficientOfVariation();
                _output.WriteLine((bed == null ? "flat" : "bed") + $": CV {cv:E2}");
                Assert.True(cv < 1e-9, "the plume left uniform: CV " + cv);
            }
        }

        [Fact]
        public void OneDecayStepMultipliesEachCellByTheFactorToTheBitAndThreeThousandSecondsLeavesEToTheMinusOne()
        {
            LeafPlumeField f = Plume(null);
            var rng = new Rng(77);
            Fill(f, (ix, iy, iz) => 0.1d + rng.NextFloat());

            int cells = f.CellCount;
            var before = new double[cells];
            for (int i = 0; i < cells; i++) before[i] = f.StockOfCell(i);

            double total0 = f.Recount();
            double factor = Math.Exp(-7d / 3000d);
            double removed = f.Decay(7d, 3000d);

            for (int i = 0; i < cells; i++)
                Assert.Equal(BitConverter.DoubleToInt64Bits(before[i] * factor), BitConverter.DoubleToInt64Bits(f.StockOfCell(i)));

            Assert.True(Math.Abs(total0 - removed - f.Recount()) <= 1e-12 * total0);
            Assert.True(Math.Abs(f.TotalStock - f.Recount()) <= 1e-12 * total0);

            // The rest of 3,000 s, in 10 s steps: the lifetime is the time to fall to 1/e.
            double t = 7d;
            while (t + 10d <= 3000d)
            {
                f.Decay(10d, 3000d);
                t += 10d;
            }

            f.Decay(3000d - t, 3000d);
            double ratio = f.Recount() / total0;
            _output.WriteLine($"after 3000 s: {ratio:R} against {Math.Exp(-1d):R}");
            Assert.True(Math.Abs(ratio - Math.Exp(-1d)) <= 1e-6);
        }

        [Fact]
        public void NoDeadCellHoldsStockAndADepositUnderTheBedLandsAboveIt()
        {
            BedShape bed = Floor();
            LeafPlumeField f = Plume(bed);
            GridField g = f.Grid;

            // A point at the bottom of the box under the shallowest column of the floor.
            float bestX = 0f, bestZ = 0f;
            double bestFloor = double.NegativeInfinity;
            for (float x = 1f; x < Side; x += 3f)
            for (float z = 1f; z < Side; z += 3f)
            {
                double floorY = bed.FloorY(x, z);
                if (floorY > bestFloor) { bestFloor = floorY; bestX = x; bestZ = z; }
            }

            var under = new Float3(bestX, -Depth + 0.1f, bestZ);
            Assert.True(under.Y < bestFloor, "the point is not under the floor");
            int cell = f.CellIndexAt(under);
            (int cx, int cy, int cz) = Decode(f, cell);
            Assert.True(g.IsLive(cx, cy, cz), "the deposit's cell is rock");

            f.Deposit(under, 4d);
            Assert.Equal(4d, f.StockOfCell(cell));

            // Mix and carry it round for a while: nothing seeps into the rock.
            CurrentField current = Ocean(bed);
            for (int step = 0; step < 200; step++)
            {
                f.Deposit(under, 0.5d);
                f.Mix(1f, Mixing);
                f.Advect(current, step, 1f);
            }

            int dead = 0;
            for (int iy = 0; iy < f.CellsY; iy++)
            for (int ix = 0; ix < f.CellsX; ix++)
            for (int iz = 0; iz < f.CellsZ; iz++)
            {
                if (g.IsLive(ix, iy, iz)) continue;
                dead++;
                Assert.Equal(0d, f.StockOfCell(f.CellIndex(ix, iy, iz)));
            }

            _output.WriteLine($"{dead} dead cells, all empty");
            Assert.True(dead > 0, "the floor masked nothing");
        }

        private static (int X, int Y, int Z) Decode(LeafPlumeField f, int cell)
        {
            int layer = f.CellsX * f.CellsZ;
            int iy = cell / layer;
            int rest = cell - iy * layer;
            return (rest / f.CellsZ, iy, rest % f.CellsZ);
        }

        // ------------------------------------------------------------------ the transport

        [Fact]
        public void ASteadyPointSourceReadsAtLeastFiveTimesTheDensityTwentyMetresDownstreamThatItReadsUpstream()
        {
            // A current that hardly turns in 1,500 s, so that "its local current" is one direction.
            // Slow enough that 1,500 s does not carry the plume round the 120 m box: the swiftest water of this
            // current is a few times its nominal speed.
            CurrentField current = Ocean(null, speed: 0.015f, period: 1e7f);
            LeafPlumeField f = Plume(null);

            // The swiftest water in the box, away from the swirls' still centres.
            var at = new Float3(60f, -12.5f, 60f);
            double fastest = 0d;
            for (float x = 5f; x < Side; x += 10f)
            for (float z = 5f; z < Side; z += 10f)
            {
                Float3 u = current.VelocityAt(x, -12.5f, z, 1500d);
                double sp = Math.Sqrt((double)u.X * u.X + (double)u.Z * u.Z);
                if (sp > fastest) { fastest = sp; at = new Float3(x, -12.5f, z); }
            }

            float[] source = Source(at.X, at.Y, at.Z, 1f);

            for (int step = 0; step < 1500; step++)
            {
                f.DepositSources(source, 1, 1f);
                f.Mix(1f, Mixing);
                f.Advect(current, step, 1f);
            }

            Float3 v = current.VelocityAt(at.X, at.Y, at.Z, 1500d);
            double speed = Math.Sqrt((double)v.X * v.X + (double)v.Z * v.Z);
            Assert.True(speed > 0.02, "the current at the source is " + speed);
            double dx = v.X / speed, dz = v.Z / speed;

            double down = f.DensityAt(new Float3((float)(at.X + 20 * dx), at.Y, (float)(at.Z + 20 * dz)));
            double up = f.DensityAt(new Float3((float)(at.X - 20 * dx), at.Y, (float)(at.Z - 20 * dz)));
            _output.WriteLine($"local current {speed:0.000} m/s; downstream {down:E3}, upstream {up:E3}, ratio {down / up:0.0}");
            Assert.True(down > 0d);
            Assert.True(down >= 5d * up, $"downstream {down:E3} against upstream {up:E3}");
        }

        [Fact]
        public void ASourceAtTheFarEdgeReachesTheCellsAtTheNearEdgeRoundTheSeam()
        {
            LeafPlumeField f = Plume(null);
            int last = f.CellsX - 1;

            f.DepositSources(Source(Side - 1f, -12.5f, 60f, 100f), 1, 1f);
            Assert.Equal(0d, f.StockOfCell(f.CellIndex(0, 2, 12)));

            for (int step = 0; step < 30; step++) f.Mix(1f, Mixing);

            double across = f.StockOfCell(f.CellIndex(0, 2, 12));
            _output.WriteLine($"cell x 0 to 5 m holds {across:E3} of 100 after 30 s");
            Assert.True(across > 0d);
            Assert.True(f.StockOfCell(f.CellIndex(last, 2, 12)) > across);

            // The same through the current: a plume carried east past the far edge arrives at the near one.
            var ocean = Ocean(null, speed: 0.1f, period: 1e7f);
            LeafPlumeField g = Plume(null);
            g.DepositSources(Source(Side - 1f, -12.5f, 60f, 100f), 1, 1f);
            for (int step = 0; step < 600; step++) g.Advect(ocean, step, 1f);
            double near = 0d;
            for (int iy = 0; iy < g.CellsY; iy++)
            for (int iz = 0; iz < g.CellsZ; iz++) near += g.StockOfCell(g.CellIndex(0, iy, iz));
            _output.WriteLine($"the near edge's column holds {near:E3} after being carried 600 s");
            Assert.True(near >= 0d);
            Assert.True(Math.Abs(g.Recount() - 100d) <= 1e-9);
        }

        // ------------------------------------------------------------------ the table of slopes

        [Fact]
        public void ARampGivesItsSlopeInsideAndTheSameSlopeOneSidedAgainstTheBedWithTheYSignPinned()
        {
            BedShape bed = Floor();
            LeafPlumeField f = Plume(bed);

            // Density 1 + 0.1 * iy: it grows with depth, 0.02 per metre, so the slope on the world's y (up) is
            // minus that.
            Fill(f, (ix, iy, iz) => 1d + 0.1d * iy);
            double want = -0.1d / Cell;

            int inside = 0, oneSided = 0;
            for (int ix = 0; ix < f.CellsX; ix++)
            for (int iz = 0; iz < f.CellsZ; iz++)
            for (int iy = 0; iy < f.CellsY; iy++)
            {
                if (!f.Grid.IsLive(ix, iy, iz)) continue;
                f.GradientOfCell(f.CellIndex(ix, iy, iz), out double gx, out double gy, out double gz);

                bool upLive = iy > 0 && f.Grid.IsLive(ix, iy - 1, iz);
                bool downLive = iy < f.CellsY - 1 && f.Grid.IsLive(ix, iy + 1, iz);
                if (upLive || downLive)
                {
                    Assert.True(Math.Abs(gy - want) <= 1e-12, $"({ix}, {iy}, {iz}): gy {gy}, want {want}");
                    if (upLive && downLive) inside++; else oneSided++;
                }
                else
                {
                    Assert.Equal(0d, gy);
                }

                // Nothing varies sideways in this ramp, except where a neighbour column is rock.
                if (f.Grid.IsLive(ix, iy, (iz + 1) % f.CellsZ) && f.Grid.IsLive(ix, iy, (iz + f.CellsZ - 1) % f.CellsZ))
                    Assert.Equal(0d, gz);
            }

            _output.WriteLine($"{inside} cells with both vertical neighbours live, {oneSided} one-sided");
            Assert.True(inside > 0);
            Assert.True(oneSided > 0, "the bed gave no one-sided cell");

            // Dead cells hold no slope.
            for (int ix = 0; ix < f.CellsX; ix++)
            for (int iz = 0; iz < f.CellsZ; iz++)
            for (int iy = 0; iy < f.CellsY; iy++)
            {
                if (f.Grid.IsLive(ix, iy, iz)) continue;
                f.GradientOfCell(f.CellIndex(ix, iy, iz), out double gx, out double gy, out double gz);
                Assert.Equal(0d, gx);
                Assert.Equal(0d, gy);
                Assert.Equal(0d, gz);
            }
        }

        [Fact]
        public void AnXRampHasItsSlopeInsideAndTheTableWrapsAtTheSeams()
        {
            LeafPlumeField f = Plume(null);
            int nx = f.CellsX;
            Fill(f, (ix, iy, iz) => 1d + 0.1d * ix);

            f.GradientOfCell(f.CellIndex(5, 2, 3), out double gx, out double gy, out double gz);
            Assert.True(Math.Abs(gx - 0.1d / Cell) <= 1e-12);
            Assert.Equal(0d, gz);

            // At the first column the minus neighbour is the last one, round the seam: (d(1) - d(nx-1)) / 2c.
            f.GradientOfCell(f.CellIndex(0, 2, 3), out double first, out _, out _);
            double want = ((1d + 0.1d * 1) - (1d + 0.1d * (nx - 1))) / (2d * Cell);
            Assert.True(Math.Abs(first - want) <= 1e-12, $"{first} against {want}");

            f.GradientOfCell(f.CellIndex(nx - 1, 2, 3), out double last, out _, out _);
            double wantLast = ((1d + 0.1d * 0) - (1d + 0.1d * (nx - 2))) / (2d * Cell);
            Assert.True(Math.Abs(last - wantLast) <= 1e-12);

            // And a z ramp keeps the axes apart.
            LeafPlumeField h = Plume(null);
            Fill(h, (ix, iy, iz) => 1d + 0.05d * iz);
            h.GradientOfCell(h.CellIndex(5, 2, 3), out double hx, out double hy, out double hz);
            Assert.Equal(0d, hx);
            Assert.Equal(0d, hy);
            Assert.True(Math.Abs(hz - 0.05d / Cell) <= 1e-12);
        }

        // ------------------------------------------------------------------ the reads

        [Fact]
        public void TheInterpolatedReadIsTheCellsOwnAtItsCentreAndHasNoStepAtACellBoundary()
        {
            LeafPlumeField f = Plume(null);
            var rng = new Rng(5);
            Fill(f, (ix, iy, iz) => 1d + 4d * rng.NextFloat());

            // At a centre, the cell's own density and slope.
            for (int k = 0; k < 20; k++)
            {
                int ix = rng.Range(f.CellsX), iy = rng.Range(f.CellsY), iz = rng.Range(f.CellsZ);
                int cell = f.CellIndex(ix, iy, iz);
                f.Sample(Centre(f, ix, iy, iz), out double d, out double gx, out double gy, out double gz);
                f.GradientOfCell(cell, out double cx, out double cy, out double cz);
                Assert.True(Math.Abs(d - f.DensityOfCell(cell)) <= 1e-12);
                Assert.True(Math.Abs(gx - cx) <= 1e-12 && Math.Abs(gy - cy) <= 1e-12 && Math.Abs(gz - cz) <= 1e-12);
            }

            // Across every kind of boundary the read moves by a hair's breadth and not by a step.
            double largestJump = 0d, largestStep = 0d;
            for (int k = 0; k < 200; k++)
            {
                int ix = 1 + rng.Range(f.CellsX - 2), iy = 1 + rng.Range(f.CellsY - 2), iz = 1 + rng.Range(f.CellsZ - 2);
                float x = ix * Cell, y = -iy * Cell, z = iz * Cell;
                float e = 1e-3f;
                foreach (var (a, b) in new[]
                {
                    (new Float3(x - e, y - 2.5f, z + 2.5f), new Float3(x + e, y - 2.5f, z + 2.5f)),
                    (new Float3(x + 2.5f, y - 2.5f, z - e), new Float3(x + 2.5f, y - 2.5f, z + e)),
                    (new Float3(x + 2.5f, y + e, z + 2.5f), new Float3(x + 2.5f, y - e, z + 2.5f)),
                })
                {
                    f.Sample(a, out double da, out double gax, out _, out _);
                    f.Sample(b, out double db, out double gbx, out _, out _);
                    largestJump = Math.Max(largestJump, Math.Abs(da - db));
                    largestStep = Math.Max(largestStep, Math.Abs(f.DensityOfCell(f.CellIndexAt(a)) - f.DensityOfCell(f.CellIndexAt(b))));
                }
            }

            _output.WriteLine($"largest read jump across a boundary {largestJump:E2}; the cell values differ by up to {largestStep:E2}");
            Assert.True(largestStep > 0.5d, "the cells do not differ, the test shows nothing");
            Assert.True(largestJump < 1e-2d, "the read steps at a boundary: " + largestJump);
        }

        [Fact]
        public void APointThatIsNotFiniteReadsZeros()
        {
            LeafPlumeField f = Plume(null);
            Fill(f, (ix, iy, iz) => 3d);
            f.Sample(new Float3(float.NaN, -1f, 3f), out double d, out double gx, out double gy, out double gz);
            Assert.Equal(0d, d);
            Assert.Equal(0d, gx + gy + gz);
        }

        // ------------------------------------------------------------------ the world

        private static RunConfig WorldConfig(LeafPlumeForm form, bool leafScent = false)
        {
            return new RunConfig
            {
                Light = new LightModel(100f, 12f),
                SharedSpace = true,
                WorldShape = WorldShape.Box,
                FieldModel = MatterField.Grid,
                WorldAreaSquareMetres = 6400f,
                HorizontalPatches = 4,
                WorldDepthMetres = 20f,
                FieldCellMetres = 1f,
                FieldMatterCellMetres = 2f,
                MatterBudgetUnits = 1000f,
                NutrientMixingDiffusivity = 0.02f,
                HorizontalMixingDiffusivity = 0.02f,
                MatterMixingDiffusivity = 0.02f,
                MinimumPopulation = 0,
                MaximumPopulation = 100_000,
                Current = new CurrentField { Mode = CurrentMode.Ocean, Speed = 0.1f, PeriodSeconds = 6000f, AdvectFields = true },
                LeafPlume = form,
                LeafPlumeCellMetres = 5f,
                LeafScent = leafScent ? LeafScentForm.Strength : LeafScentForm.None,
            };
        }

        private static World LeafWorld(RunConfig config, int steps)
        {
            var world = new World(config, seed: 5);
            world.InoculateAt(Fixtures.SingleLeaf(cellTypeId: CellTypeIds.Photosynthetic), new Float3(20f, -3f, 25f));
            world.InoculateAt(Fixtures.SingleLeaf(cellTypeId: CellTypeIds.Photosynthetic), new Float3(55f, -6f, 40f));
            for (int i = 0; i < steps; i++) world.Step(0.5f);
            return world;
        }

        private static byte[] StateOf(World world)
        {
            using (var buffer = new MemoryStream())
            {
                using (var w = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
                {
                    world.WriteState(w);
                    w.Flush();
                }

                return buffer.ToArray();
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void AWorldWithLeavesFeedsThePlumeWithOrWithoutTheLeafSmellAndItsBooksClose(bool leafScent)
        {
            World world = LeafWorld(WorldConfig(LeafPlumeForm.Strength, leafScent), steps: 40);
            Assert.NotNull(world.LeafPlume);

            _output.WriteLine($"emitted {world.LeafPlumeEmittedTotal:E4}, decayed {world.LeafPlumeDecayedTotal:E4}");
            Assert.True(world.LeafPlumeEmittedTotal > 0d);
            Assert.True(world.LeafPlumeDecayedTotal > 0d);

            double held = world.LeafPlume.Recount();
            Assert.True(Math.Abs(held - (world.LeafPlumeEmittedTotal - world.LeafPlumeDecayedTotal)) <= 1e-9 * world.LeafPlumeEmittedTotal);
            Assert.Equal(0, world.LeafPlume.CountUnsoundCells());
        }

        [Fact]
        public void WithThePlumeOffNoGridIsBuiltAndTheStateIsNotLonger()
        {
            var off = new World(WorldConfig(LeafPlumeForm.None), seed: 5);
            Assert.Null(off.LeafPlume);

            var on = new World(WorldConfig(LeafPlumeForm.Strength), seed: 5);
            Assert.NotNull(on.LeafPlume);
            Assert.True(StateOf(on).Length > StateOf(off).Length);
            Assert.Equal(0d, off.LeafPlumeEmittedTotal);
        }

        [Fact]
        public void ARestoredPlumeWorldHasTheStockAndTheTableAndStepsOnBitForBit()
        {
            RunConfig config = WorldConfig(LeafPlumeForm.Gradient);
            World world = LeafWorld(config, steps: 30);

            byte[] state = StateOf(world);
            var restored = new World(config, 5);
            using (var buffer = new MemoryStream(state, writable: false))
            using (var r = new BinaryReader(buffer, Encoding.UTF8))
            {
                restored.ReadState(r);
                Assert.Equal(state.Length, buffer.Position);
            }

            Assert.Equal(world.LeafPlumeEmittedTotal, restored.LeafPlumeEmittedTotal);
            Assert.Equal(world.LeafPlumeDecayedTotal, restored.LeafPlumeDecayedTotal);
            Assert.Equal(state, StateOf(restored));
            for (int i = 0; i < world.LeafPlume.CellCount; i++)
            {
                world.LeafPlume.GradientOfCell(i, out double ax, out double ay, out double az);
                restored.LeafPlume.GradientOfCell(i, out double bx, out double by, out double bz);
                Assert.Equal((ax, ay, az), (bx, by, bz));
            }

            for (int i = 0; i < 30; i++)
            {
                world.Step(0.5f);
                restored.Step(0.5f);
            }

            Assert.Equal(StateOf(world), StateOf(restored));
        }
    }
}
