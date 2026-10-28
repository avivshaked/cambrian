using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D251's far smell: a fed island adds a flat top and a falling skirt, by its leaves' banded lit area, to
    /// the leaf smell. The term, the membership of a leaf in an island, the grid's identity with the switch
    /// off, the flat top's lack of any gradient, the wrap, the threads, the checkpoint and the refusals.
    /// </summary>
    /// <remarks>
    /// Read on the bedded tank of <see cref="IslandBloomTests"/> where a world is wanted, and on a bare
    /// <see cref="ScentField"/> where the term alone is.
    /// </remarks>
    public class IslandFarSmellTests
    {
        private readonly ITestOutputHelper _output;

        public IslandFarSmellTests(ITestOutputHelper output) => _output = output;

        private const float Area = 400f;
        private const float Depth = 20f;
        private const int Rings = 4;

        /// <summary>
        /// A bedded tank with islands, fed on them, with a leaf smell, and the far smell as given. The band and
        /// reach default to small values so that a flat top and a skirt both fit inside the small tank.
        /// </summary>
        private static RunConfig Fed(
            bool still, IslandFarSmell far = IslandFarSmell.Leaves, float gain = 0.005f, float band = 3f,
            float reach = 4f)
        {
            var config = new RunConfig
            {
                Light = new LightModel(100f, 12f),
                SharedSpace = true,
                WorldShape = WorldShape.Tank,
                FieldModel = MatterField.Grid,
                WorldAreaSquareMetres = Area,
                HorizontalPatches = Rings,
                WorldDepthMetres = Depth,
                FieldCellMetres = 1f,
                FieldMatterCellMetres = 2f,
                BedReliefMetres = 1.5f,
                BedTiltMetres = 6f,
                MatterBudgetUnits = 1000f,
                MatterIslandWavelengthMetres = 8f,
                MatterIslandCover = 0.25f,
                MatterIslandDepthMetres = 6f,
                MatterSinkMetresPerSecond = 0.05f,
                MatterInfluxPerSecond = 1.2f,
                MatterInfluxAt = MatterInflux.Islands,
                MatterBurialPerSecond = 0.01f,
                NutrientMixingDiffusivity = 0.02f,
                HorizontalMixingDiffusivity = 0.02f,
                MatterMixingDiffusivity = 0.02f,
                Current = new CurrentField
                {
                    Mode = CurrentMode.Transport, Speed = 0.1f, PeriodSeconds = 6000f, AdvectFields = true,
                },
                LeafScent = LeafScentForm.Strength,
                IslandFarSmell = far,
                IslandFarSmellGain = gain,
                IslandFarSmellBandMetres = band,
                IslandFarSmellReachMetres = reach,
            };

            if (still)
            {
                config.MinimumPopulation = 0;
                config.MaximumPopulation = 100_000;
                config.MatterBurialPerSecond = 0f;
                config.MatterSinkMetresPerSecond = 0f;
                config.MatterMixingDiffusivity = 0f;
                config.NutrientSinkMetresPerSecond = 0f;
                config.NutrientMixingDiffusivity = 0f;
                config.HorizontalMixingDiffusivity = 0f;
                config.RemineralisationPerSecond = 0f;
                config.Current = null;
            }

            return config;
        }

        /// <summary>A leaf over each of the first <paramref name="perIsland"/> columns of every island, in scan order.</summary>
        private static void PlaceLeaves(World world, int perIsland)
        {
            var grid = (GridField)world.Matter;
            GridField.IslandPlan plan = world.IslandInflux;
            var seen = new int[plan.IslandCount];
            for (int ix = 0; ix < grid.CellsX; ix++)
            for (int iz = 0; iz < grid.CellsZ; iz++)
            {
                int island = plan.IslandOfColumn(ix, iz);
                if (island < 0 || seen[island] >= perIsland) continue;
                seen[island]++;
                world.InoculateAt(
                    Fixtures.SingleLeaf(cellTypeId: CellTypeIds.Photosynthetic),
                    new Float3((ix + 0.5f) * grid.CellMetres, -2f, (iz + 0.5f) * grid.CellMetres));
            }
        }

        private static World LeafWorld(RunConfig config, int perIsland = 3, int steps = 4)
        {
            var world = new World(config, seed: 5);
            PlaceLeaves(world, perIsland);
            for (int i = 0; i < steps; i++) world.Step(0.5f);
            Assert.True(world.LeafSourceCount > 0, "no leaf was gathered");
            return world;
        }

        /// <summary>The leaf smell as the leaves alone make it: a fresh field of the world's dimensions filled from its sources.</summary>
        private static ScentField LeavesAlone(World world)
        {
            ScentField s = world.LeafScent;
            var field = new ScentField(
                s.CellsX * s.CellSizeMetres, s.CellsZ * s.CellSizeMetres, s.CellsY * s.CellSizeMetres,
                s.RangeMetres, s.CellSizeMetres, s.Wraps);
            field.FillSources(world.CopyLeafSources(), world.LeafSourceCount);
            Assert.Equal(s.CellsX, field.CellsX);
            Assert.Equal(s.CellsY, field.CellsY);
            Assert.Equal(s.CellsZ, field.CellsZ);
            return field;
        }

        private static void AssertSameBits(float[] want, float[] got, string what)
        {
            Assert.Equal(want.Length, got.Length);
            for (int i = 0; i < want.Length; i++)
            {
                if (BitConverter.SingleToInt32Bits(want[i]) != BitConverter.SingleToInt32Bits(got[i]))
                    Assert.Fail($"{what}: cell {i} reads {got[i]:R}, want {want[i]:R}");
            }
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

        // ---------------------------------------------------------------------------------
        // The switch
        // ---------------------------------------------------------------------------------

        [Fact]
        public void WithTheSwitchOffTheLeafGridIsTheLeavesAloneAndAGainOfZeroAddsNothing()
        {
            var off = new World(Fed(still: false, IslandFarSmell.None), seed: 5);
            var zero = new World(Fed(still: false, IslandFarSmell.Leaves, gain: 0f), seed: 5);
            PlaceLeaves(off, 3);
            PlaceLeaves(zero, 3);
            for (int i = 0; i < 40; i++)
            {
                off.Step(0.5f);
                zero.Step(0.5f);
            }

            Assert.True(off.LeafSourceCount > 0);
            Assert.False(off.HasIslandFarSmell);
            Assert.Empty(off.IslandFarSmellHeights());
            Assert.True(zero.HasIslandFarSmell);

            // Off, the grid is the sources' cones and nothing else, which is what the world filled before.
            AssertSameBits(LeavesAlone(off).CopyCells(), off.LeafScent.CopyCells(), "switch off");

            // Switched on at a gain of 0, every height is 0 and every cell is the same bits; so is the world.
            Assert.All(zero.IslandFarSmellHeights(), h => Assert.Equal(0d, h));
            AssertSameBits(off.LeafScent.CopyCells(), zero.LeafScent.CopyCells(), "gain 0");
            Assert.Equal(StateOf(off), StateOf(zero));
        }

        // ---------------------------------------------------------------------------------
        // The term
        // ---------------------------------------------------------------------------------

        private static int At(ScentField f, int i, int j, int k) => (i * f.CellsY + j) * f.CellsZ + k;

        [Fact]
        public void TheTermIsTheHeightInsideTheFlatTopFallsOnTheSkirtAndIsGonePastTheReach()
        {
            // 60 by 60 m on 2 m cells; the island's point is the centre of cell (15, 15), (31, 31) m.
            var field = new ScentField(60f, 60f, 6f, 5f, 2f);
            const double Height = 3d, Flat = 7d, Reach = 10d;
            field.AddLevelPlateaus(new[] { 31d }, new[] { 31d }, new[] { Flat }, new[] { Height }, 1, Reach);
            float[] cells = field.CopyCells();

            // Along x from the point, the cell n over is 2n m away; along a diagonal the distance is the root.
            foreach ((int dx, int dz) in new[] { (0, 0), (3, 0), (0, -3), (4, 0), (0, 5), (7, 0), (8, 0), (3, 4), (-6, 6), (9, 0), (-9, -1) })
            {
                double d = 2d * Math.Sqrt(dx * dx + dz * dz);
                double over = d - Flat;
                double want = d >= Flat + Reach ? 0d : over > 0d ? Height * (1d - over / Reach) : Height;
                for (int j = 0; j < field.CellsY; j++)
                {
                    Assert.Equal((float)want, cells[At(field, 15 + dx, j, 15 + dz)]);
                }

                _output.WriteLine(FormattableString.Invariant($"cell ({dx}, {dz}): d {d:0.###} m, term {want:0.####}"));
            }

            // Spot values the formula gives: inside, on the slope, and past the reach.
            Assert.Equal(3f, cells[At(field, 15 + 3, 2, 15)]);                        // d 6 m, inside 7 m
            Assert.Equal((float)(3d * (1d - 1d / 10d)), cells[At(field, 15 + 4, 2, 15)]);  // d 8 m, 1 m out
            Assert.Equal((float)(3d * (1d - 7d / 10d)), cells[At(field, 15 + 7, 2, 15)]);  // d 14 m, 7 m out
            Assert.Equal(0f, cells[At(field, 15 + 9, 2, 15)]);                        // d 18 m, past 17 m

            // It adds to what is there and clears nothing.
            field.AddLevelPlateaus(new[] { 31d }, new[] { 31d }, new[] { Flat }, new[] { Height }, 1, Reach);
            Assert.Equal(6f, field.CopyCells()[At(field, 15, 0, 15)]);

            // An island of no height adds nothing.
            var quiet = new ScentField(60f, 60f, 6f, 5f, 2f);
            quiet.AddLevelPlateaus(new[] { 31d }, new[] { 31d }, new[] { Flat }, new[] { 0d }, 1, Reach);
            Assert.All(quiet.CopyCells(), v => Assert.Equal(0f, v));
        }

        [Fact]
        public void TwoIslandsSumAndAnIslandOutsideTheCountIsLeftOut()
        {
            var field = new ScentField(60f, 60f, 4f, 5f, 2f);
            var x = new[] { 11d, 13d, 50d };
            var z = new[] { 31d, 31d, 31d };
            var flat = new[] { 3d, 3d, 3d };
            var height = new[] { 1d, 2d, 4d };
            field.AddLevelPlateaus(x, z, flat, height, 2, 5d);
            float[] cells = field.CopyCells();
            Assert.Equal(3f, cells[At(field, 5, 0, 15)]);   // (11, 31): inside both
            Assert.Equal(0f, cells[At(field, 25, 0, 15)]);  // (51, 31): the third island was not counted
        }

        // ---------------------------------------------------------------------------------
        // No gradient on the flat top
        // ---------------------------------------------------------------------------------

        [Fact]
        public void InsideAnIslandsFlatTopTheLeafSmellsDirectionIsTheSameAndItsStrengthHigherByTheConstant()
        {
            RunConfig config = Fed(still: true, IslandFarSmell.Leaves, gain: 0.02f, band: 3f, reach: 4f);
            World world = LeafWorld(config, perIsland: 6);
            ScentField on = world.LeafScent;
            ScentField off = LeavesAlone(world);
            GridField.IslandPlan plan = world.IslandInflux;
            double[] heights = world.IslandFarSmellHeights();
            double cell = on.CellSizeMetres;
            Assert.Equal(plan.IslandCount, heights.Length);
            _output.WriteLine("heights H_k: " + string.Join(", ", heights.Select(h => h.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture))));

            int tested = 0;
            double worstDirection = 0d, worstConstant = 0d;
            string sample = null;
            for (int i0 = 0; i0 < on.CellsX - 1; i0++)
            for (int k0 = 0; k0 < on.CellsZ - 1; k0++)
            {
                // The eight cells round this point are the two columns' worth i0, i0 + 1 by k0, k0 + 1 at two depths.
                double constant = 0d;
                bool clean = true;
                for (int n = 0; n < plan.IslandCount && clean; n++)
                {
                    double flat = plan.IslandRadius(n) + config.IslandFarSmellBandMetres;
                    int inside = 0, beyond = 0;
                    for (int di = 0; di < 2; di++)
                    for (int dk = 0; dk < 2; dk++)
                    {
                        double ddx = (i0 + di + 0.5) * cell - plan.IslandX(n), ddz = (k0 + dk + 0.5) * cell - plan.IslandZ(n);
                        double d = Math.Sqrt(ddx * ddx + ddz * ddz);
                        if (d <= flat - 1e-6) inside++;
                        else if (d >= flat + config.IslandFarSmellReachMetres + 1e-6) beyond++;
                    }

                    if (inside == 4) constant += heights[n];
                    else if (beyond != 4) clean = false;
                }

                if (!clean || !(constant > 0d)) continue;

                float px = (float)((i0 + 1) * cell), py = (float)(-1 * cell), pz = (float)((k0 + 1) * cell);
                Float3 gOn = on.GradientAt(px, py, pz), gOff = off.GradientAt(px, py, pz);
                double mOn = Math.Sqrt(gOn.X * gOn.X + gOn.Y * gOn.Y + gOn.Z * gOn.Z);
                double mOff = Math.Sqrt(gOff.X * gOff.X + gOff.Y * gOff.Y + gOff.Z * gOff.Z);

                double dStrength = on.At(px, py, pz) - off.At(px, py, pz);
                worstConstant = Math.Max(worstConstant, Math.Abs(dStrength - constant));
                if (!(mOff > 0.005)) continue;

                tested++;
                worstDirection = Math.Max(worstDirection, Math.Max(
                    Math.Abs(gOn.X / mOn - gOff.X / mOff), Math.Max(Math.Abs(gOn.Y / mOn - gOff.Y / mOff), Math.Abs(gOn.Z / mOn - gOff.Z / mOff))));
                Assert.True(Math.Abs(gOn.X / mOn - gOff.X / mOff) < 1e-5, $"x direction differs at ({px}, {py}, {pz})");
                Assert.True(Math.Abs(gOn.Y / mOn - gOff.Y / mOff) < 1e-5, $"y direction differs at ({px}, {py}, {pz})");
                Assert.True(Math.Abs(gOn.Z / mOn - gOff.Z / mOff) < 1e-5, $"z direction differs at ({px}, {py}, {pz})");
                if (sample == null)
                {
                    sample = FormattableString.Invariant(
                        $"at ({px}, {py}, {pz}): strength with the switch on {on.At(px, py, pz):0.#####}, off {off.At(px, py, pz):0.#####}, ") +
                        FormattableString.Invariant(
                        $"constant {constant:0.#####}; gradient direction on ({gOn.X / mOn:0.#######}, {gOn.Y / mOn:0.#######}, {gOn.Z / mOn:0.#######}), ") +
                        FormattableString.Invariant(
                        $"off ({gOff.X / mOff:0.#######}, {gOff.Y / mOff:0.#######}, {gOff.Z / mOff:0.#######}); |gradient| on {mOn:0.#####}, off {mOff:0.#####}");
                }
            }

            _output.WriteLine(sample ?? "no point tested");
            _output.WriteLine(FormattableString.Invariant(
                $"{tested} points on flat tops with a gradient above 0.005 per metre; worst direction difference {worstDirection:0.#########}, worst strength difference from the constant {worstConstant:0.#########}"));
            Assert.True(tested >= 3, "too few points on a flat top with a gradient to test: " + tested);
            Assert.True(worstConstant < 1e-5, "the strength did not rise by the constant: " + worstConstant);
        }

        // ---------------------------------------------------------------------------------
        // The height follows the area
        // ---------------------------------------------------------------------------------

        [Fact]
        public void TheHeightIsTheGainTimesTheBandedAreaAndDoublesWithIt()
        {
            RunConfig config = Fed(still: true, IslandFarSmell.Leaves, gain: 0.005f, band: 3f, reach: 4f);
            World world = LeafWorld(config);
            GridField.IslandPlan plan = world.IslandInflux;
            float[] sources = world.CopyLeafSources();
            int count = world.LeafSourceCount;

            var cx = new double[plan.IslandCount];
            var cz = new double[plan.IslandCount];
            var r = new double[plan.IslandCount];
            for (int k = 0; k < cx.Length; k++) { cx[k] = plan.IslandX(k); cz[k] = plan.IslandZ(k); r[k] = plan.IslandRadius(k); }

            var area = new double[cx.Length];
            World.BandedLeafArea(sources, count, cx, cz, r, cx.Length, 3d, 0d, 0d, false, area);
            double[] heights = world.IslandFarSmellHeights();
            for (int k = 0; k < cx.Length; k++) Assert.Equal((double)0.005f * area[k], heights[k]);
            Assert.True(heights.Any(h => h > 0d));

            // Doubling every source's area doubles every island's banded area, and so its height.
            float[] doubled = (float[])sources.Clone();
            for (int i = 0; i < count; i++) doubled[4 * i + 3] *= 2f;
            var area2 = new double[cx.Length];
            World.BandedLeafArea(doubled, count, cx, cz, r, cx.Length, 3d, 0d, 0d, false, area2);
            for (int k = 0; k < cx.Length; k++) Assert.Equal(2d * area[k], area2[k]);

            // And the same through the field: twice the heights add exactly twice the term.
            var once = new ScentField(60f, 60f, 6f, 5f, 2f);
            var twice = new ScentField(60f, 60f, 6f, 5f, 2f);
            var h1 = new[] { 0.7d, 1.3d };
            var h2 = new[] { 1.4d, 2.6d };
            once.AddLevelPlateaus(new[] { 20d, 40d }, new[] { 25d, 35d }, new[] { 4d, 5d }, h1, 2, 9d);
            twice.AddLevelPlateaus(new[] { 20d, 40d }, new[] { 25d, 35d }, new[] { 4d, 5d }, h2, 2, 9d);
            float[] a = once.CopyCells(), b = twice.CopyCells();
            for (int i = 0; i < a.Length; i++) Assert.Equal(2f * a[i], b[i]);
        }

        // ---------------------------------------------------------------------------------
        // Whose leaf it is
        // ---------------------------------------------------------------------------------

        [Fact]
        public void ALeafCountsTowardsTheIslandWithTheNearestEdgeWithinTheBandAndOtherwiseNone()
        {
            // Island 0 at (0, 0) with radius 5; island 1 at (30, 0) with radius 3.
            var cx = new[] { 0d, 30d };
            var cz = new[] { 0d, 0d };
            var r = new[] { 5d, 3d };

            // x, z, area. Edge distances (to island 0, to island 1) in the comments.
            float[] sources =
            {
                14f, -1f, 0f, 1f,    // 9, 13: island 0, inside the band of 10
                20f, -1f, 0f, 2f,    // 15, 7: island 1
                17f, -1f, 0f, 4f,    // 12, 10: island 1, on the band's edge
                16f, -1f, 0f, 8f,    // 11, 11: a tie at 11, beyond a band of 10
                3f, -1f, 0f, 16f,    // -2, 25: island 0, inside its radius
                50f, -1f, 40f, 32f,  // far from both
            };

            var area = new double[2];
            World.BandedLeafArea(sources, 6, cx, cz, r, 2, 10d, 0d, 0d, false, area);
            Assert.Equal(new[] { 1d + 16d, 2d + 4d }, area);

            // The same tie inside a wider band goes to the lower label.
            World.BandedLeafArea(sources, 6, cx, cz, r, 2, 12d, 0d, 0d, false, area);
            Assert.Equal(new[] { 1d + 8d + 16d, 2d + 4d }, area);

            // A band of 1000 takes the far leaf too, towards the nearer edge.
            World.BandedLeafArea(sources, 6, cx, cz, r, 2, 1000d, 0d, 0d, false, area);
            Assert.Equal(63d, area.Sum());

            // The count is a prefix.
            World.BandedLeafArea(sources, 2, cx, cz, r, 2, 10d, 0d, 0d, false, area);
            Assert.Equal(new[] { 1d, 2d }, area);

            // Round a ring of 60 m, island 1's leaf at x = -29 is 1 m past the seam from island 1 at 30.
            World.BandedLeafArea(new float[] { -29f, -1f, 0f, 5f }, 1, cx, cz, r, 2, 10d, 60d, 60d, true, area);
            Assert.Equal(new[] { 0d, 5d }, area);
            World.BandedLeafArea(new float[] { -29f, -1f, 0f, 5f }, 1, cx, cz, r, 2, 10d, 0d, 0d, false, area);
            Assert.Equal(new[] { 0d, 0d }, area);
        }

        // ---------------------------------------------------------------------------------
        // The wrap
        // ---------------------------------------------------------------------------------

        [Fact]
        public void RoundASeamAnIslandAddsTheTermItWouldAddAwayFromOne()
        {
            var plain = new ScentField(60f, 60f, 4f, 5f, 2f);
            var ring = new ScentField(60f, 60f, 4f, 5f, 2f, wraps: true);

            // Plain: the island at (31, 31). Ring: the island at (1, 59), cell (0, 29): the same cell, 30 cells
            // away in x and 14 in z from the plain one's, with the seams beside it.
            const double Height = 2d, Flat = 5d, Reach = 8d;
            plain.AddLevelPlateaus(new[] { 31d }, new[] { 31d }, new[] { Flat }, new[] { Height }, 1, Reach);
            ring.AddLevelPlateaus(new[] { 1d }, new[] { 59d }, new[] { Flat }, new[] { Height }, 1, Reach);

            int cells = 0;
            for (int di = -7; di <= 7; di++)
            for (int dk = -7; dk <= 7; dk++)
            {
                int ip = 15 + di, kp = 15 + dk;
                int ir = ((0 + di) % 30 + 30) % 30, kr = ((29 + dk) % 30 + 30) % 30;
                float want = plain.CopyCells()[At(plain, ip, 1, kp)];
                float got = ring.CopyCells()[At(ring, ir, 1, kr)];
                Assert.Equal(want, got);
                if (want > 0f) cells++;
            }

            Assert.True(cells > 50, "the island's term covered too few cells to be a test: " + cells);

            // Not round a seam, the same island at the edge adds to the near side only.
            var edge = new ScentField(60f, 60f, 4f, 5f, 2f);
            edge.AddLevelPlateaus(new[] { 1d }, new[] { 59d }, new[] { Flat }, new[] { Height }, 1, Reach);
            Assert.Equal(0f, edge.CopyCells()[At(edge, 29, 1, 0)]);
            Assert.True(ring.CopyCells()[At(ring, 29, 1, 0)] > 0f);
        }

        // ---------------------------------------------------------------------------------
        // The threads
        // ---------------------------------------------------------------------------------

        [Fact]
        public void TheGridFilledAtOneThreadAndAtFourIsTheSameBits()
        {
            var rng = new Random(251);
            const int n = 600;
            var sources = new float[4 * n];
            for (int i = 0; i < n; i++)
            {
                sources[4 * i] = (float)(61 * rng.NextDouble());
                sources[4 * i + 1] = (float)(-33 * rng.NextDouble());
                sources[4 * i + 2] = (float)(47 * rng.NextDouble());
                sources[4 * i + 3] = (float)(0.5 * rng.NextDouble());
            }

            var x = new[] { 8.3, 30.1, 52.7, 19.9 };
            var z = new[] { 10.2, 25.5, 40.4, 44.6 };
            var flat = new[] { 6.1, 3.3, 9.4, 4.8 };
            var height = new[] { 0.31, 1.7, 0.0, 0.9 };

            int was = Parallelism.Threads;
            try
            {
                float[] one = null;
                foreach (int threads in new[] { 1, 4 })
                {
                    Parallelism.Threads = threads;
                    var field = new ScentField(61f, 47f, 33f, 8f, 1.5f);
                    field.FillSources(sources, n);
                    field.AddLevelPlateaus(x, z, flat, height, 4, 12d);
                    float[] cells = field.CopyCells();
                    if (one == null) { one = cells; continue; }
                    AssertSameBits(one, cells, threads + " threads");
                }
            }
            finally
            {
                Parallelism.Threads = was;
            }
        }

        // ---------------------------------------------------------------------------------
        // The checkpoint
        // ---------------------------------------------------------------------------------

        [Fact]
        public void ARestoredWorldOnTheFarSmellHasTheLiveGridAndStepsOnBitForBit()
        {
            RunConfig config = Fed(still: false, IslandFarSmell.Leaves, gain: 0.02f);
            var world = new World(config, seed: 9);
            PlaceLeaves(world, 3);
            for (int i = 0; i < 60; i++) world.Step(0.5f);
            Assert.True(world.LeafSourceCount > 0);
            Assert.True(world.IslandFarSmellHeights().Any(h => h > 0d), "no island had a height at the checkpoint");

            byte[] state = StateOf(world);
            var restored = new World(config, 9);
            using (var buffer = new MemoryStream(state, writable: false))
            using (var r = new BinaryReader(buffer, Encoding.UTF8))
            {
                restored.ReadState(r);
                Assert.Equal(state.Length, buffer.Position);
            }

            Assert.Equal(world.CopyLeafSources(), restored.CopyLeafSources());
            AssertSameBits(world.LeafScent.CopyCells(), restored.LeafScent.CopyCells(), "restore");
            Assert.Equal(world.IslandFarSmellHeights(), restored.IslandFarSmellHeights());

            // The far smell is on in this grid: it is not the leaves alone.
            Assert.NotEqual(LeavesAlone(world).CopyCells(), world.LeafScent.CopyCells());

            for (int i = 0; i < 60; i++)
            {
                world.Step(0.5f);
                restored.Step(0.5f);
            }

            AssertSameBits(world.LeafScent.CopyCells(), restored.LeafScent.CopyCells(), "after stepping on");
            Assert.Equal(StateOf(world), StateOf(restored));
        }

        // ---------------------------------------------------------------------------------
        // The refusals
        // ---------------------------------------------------------------------------------

        [Fact]
        public void AFarSmellWithoutALeafSmellOrWithoutTheIslandsInfluxIsRefused()
        {
            RunConfig noLeaves = Fed(still: true);
            noLeaves.LeafScent = LeafScentForm.None;
            ArgumentException a = Assert.Throws<ArgumentException>(() => new World(noLeaves, seed: 5));
            _output.WriteLine(a.Message);
            Assert.Contains("IslandFarSmell is Leaves", a.Message);
            Assert.Contains("leaf smell", a.Message);

            RunConfig surface = Fed(still: true);
            surface.MatterInfluxAt = MatterInflux.Surface;
            ArgumentException b = Assert.Throws<ArgumentException>(() => new World(surface, seed: 5));
            _output.WriteLine(b.Message);
            Assert.Contains("IslandFarSmell is Leaves", b.Message);

            RunConfig dry = Fed(still: true);
            dry.MatterInfluxPerSecond = 0f;
            ArgumentException c = Assert.Throws<ArgumentException>(() => new World(dry, seed: 5));
            _output.WriteLine(c.Message);
            Assert.Contains("IslandFarSmell is Leaves", c.Message);

            // Off, the same configs are the worlds they always were.
            noLeaves.IslandFarSmell = IslandFarSmell.None;
            Assert.Null(new World(noLeaves, seed: 5).LeafScent);
            surface.IslandFarSmell = IslandFarSmell.None;
            Assert.Null(new World(surface, seed: 5).IslandInflux);
        }

        [Theory]
        [InlineData("gain", -0.001f)]
        [InlineData("gain", float.NaN)]
        [InlineData("gain", float.PositiveInfinity)]
        [InlineData("band", -1f)]
        [InlineData("band", float.NaN)]
        [InlineData("band", float.PositiveInfinity)]
        [InlineData("reach", 0f)]
        [InlineData("reach", -5f)]
        [InlineData("reach", float.NaN)]
        [InlineData("reach", float.PositiveInfinity)]
        public void ABadGainBandOrReachIsRefused(string which, float value)
        {
            RunConfig config = Fed(still: true);
            if (which == "gain") config.IslandFarSmellGain = value;
            else if (which == "band") config.IslandFarSmellBandMetres = value;
            else config.IslandFarSmellReachMetres = value;

            ArgumentException e = Assert.Throws<ArgumentException>(() => new World(config, seed: 5));
            _output.WriteLine(e.Message);
            Assert.Contains("IslandFarSmell is Leaves", e.Message);
            Assert.Contains(which, e.Message);

            // Off, the same numbers are not looked at.
            config.IslandFarSmell = IslandFarSmell.None;
            Assert.NotNull(new World(config, seed: 5));
        }

        /// <summary>A flat box of 160 by 40 m with the islands' influx, a leaf smell and joined seams.</summary>
        private static RunConfig RingBox(float reach)
        {
            var config = Fed(still: true, IslandFarSmell.Leaves, gain: 0.005f, band: 3f, reach: reach);
            config.WorldShape = WorldShape.Box;
            config.WorldAreaSquareMetres = 6400f;
            config.BedReliefMetres = 0f;
            config.BedTiltMetres = 0f;
            config.ReadAcrossSeams = true;
            config.Current = null;
            return config;
        }

        [Fact]
        public void AnIslandsSkirtThatWouldMeetItselfRoundASeamIsRefused()
        {
            // A skirt reaching a few metres past the island fits in the 20 cells of 2 m.
            var fits = new World(RingBox(reach: 4f), seed: 5);
            Assert.True(fits.LeafScent.Wraps);
            Assert.True(fits.HasIslandFarSmell);

            // One reaching 40 m past it does not.
            ArgumentException e = Assert.Throws<ArgumentException>(() => new World(RingBox(reach: 40f), seed: 5));
            _output.WriteLine(e.Message);
            Assert.Contains("IslandFarSmell is Leaves", e.Message);
            Assert.Contains("seam", e.Message);
        }

        // ---------------------------------------------------------------------------------
        // The stats column
        // ---------------------------------------------------------------------------------

        [Fact]
        public void TheBloomLeafAreaIsTheBandedAreaWhetherOrNotTheFarSmellIsOnAndTheHeightIsTheGainTimesIt()
        {
            foreach (IslandFarSmell far in new[] { IslandFarSmell.None, IslandFarSmell.Leaves })
            {
                RunConfig config = Fed(still: true, far, gain: 0.01f, band: 5f, reach: 4f);
                config.IslandBloom = IslandBloom.Cycles;
                World world = LeafWorld(config);
                double[] area = world.IslandBloomLeafArea();

                float[] sources = world.CopyLeafSources();
                GridField.IslandPlan plan = world.IslandInflux;
                var expected = new double[plan.IslandCount];
                for (int i = 0; i < world.LeafSourceCount; i++)
                {
                    int best = -1;
                    double least = double.MaxValue;
                    for (int k = 0; k < plan.IslandCount; k++)
                    {
                        double dx = sources[4 * i] - plan.IslandX(k), dz = sources[4 * i + 2] - plan.IslandZ(k);
                        double edge = Math.Sqrt(dx * dx + dz * dz) - plan.IslandRadius(k);
                        if (edge < least) { least = edge; best = k; }
                    }

                    if (least <= 5d) expected[best] += sources[4 * i + 3];
                }

                Assert.Equal(expected, area);
                Assert.True(area.Sum() > 0d);
                _output.WriteLine(far + ": " + string.Join(", ", area.Select(a => a.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))));

                if (far == IslandFarSmell.Leaves)
                {
                    double[] heights = world.IslandFarSmellHeights();
                    for (int k = 0; k < area.Length; k++) Assert.Equal((double)0.01f * area[k], heights[k]);
                }
                else
                {
                    Assert.Empty(world.IslandFarSmellHeights());
                }
            }
        }
    }
}
