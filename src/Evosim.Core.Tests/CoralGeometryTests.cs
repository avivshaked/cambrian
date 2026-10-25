using System;
using System.Collections.Generic;
using System.Linq;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// Round 62's coral, step 1 of <c>logbook/specs/coral-build.md</c>: the tunables and their
    /// refusals, the island test, the colonies' growth, their placement on the islands and in the
    /// desert, the caps, and one coral for one seed.
    /// </summary>
    public class CoralGeometryTests
    {
        private readonly ITestOutputHelper _output;

        public CoralGeometryTests(ITestOutputHelper output) => _output = output;

        private const float Area = 12000f;
        private const float Depth = 20f;
        private const float Margin = 8f;

        private static float Radius => TankGeometry.RadiusFor(Area);

        /// <summary>A tank about 62 m in radius with islands on 2 m matter cells and the coral at its start values.</summary>
        private static RunConfig Tank(ReefShape shape = ReefShape.Coral, float relief = 1.5f, float tilt = 6f) => new RunConfig
        {
            Light = new LightModel(100f, 12f),
            SharedSpace = true,
            WorldShape = WorldShape.Tank,
            FieldModel = MatterField.Grid,
            WorldAreaSquareMetres = Area,
            HorizontalPatches = 4,
            WorldDepthMetres = Depth,
            FieldCellMetres = 1f,
            FieldMatterCellMetres = 2f,
            BedReliefMetres = relief,
            BedTiltMetres = tilt,
            MatterBudgetUnits = 1000f,
            MatterIslandWavelengthMetres = 25f,
            MatterIslandCover = 0.15f,
            MatterIslandDepthMetres = 6f,
            MatterIslandGlassMarginMetres = Margin,
            MatterInfluxPerSecond = 1f,
            MatterInfluxAt = MatterInflux.Islands,
            ReefShape = shape,
        };

        private static BedShape Bed(RunConfig c, ulong landscape) =>
            c.BedReliefMetres > 0f || c.BedTiltMetres > 0f
                ? new BedShape(Radius, c.WorldDepthMetres, c.BedReliefMetres, c.BedTiltMetres, c.BedScaleMetres,
                    Rng.SeedFor(landscape, World.BedShapeIndex))
                : null;

        private static CoralGeometry Coral(RunConfig c, ulong landscape) =>
            CoralGeometry.PlaceFromLandscape(c, Radius, Bed(c, landscape), landscape);

        private static CoralIslandColumns Columns(RunConfig c, ulong landscape) =>
            CoralIslandColumns.Build(
                Radius, c.FieldMatterCellMetres, c.WorldDepthMetres, Bed(c, landscape),
                new GradientNoise(Rng.SeedFor(landscape, World.IslandMapIndex), c.MatterIslandWavelengthMetres).Fbm,
                c.MatterIslandCover, c.MatterIslandGlassMarginMetres);

        private static string Refusal(Action act) => Assert.ThrowsAny<ArgumentException>(act).Message;

        // ------------------------------------------------------------------ the tunables

        [Fact]
        public void TheDefaultsAreTheSpecsStartValues()
        {
            var c = new RunConfig();
            Assert.Equal(ReefShape.Mushroom, c.ReefShape);
            Assert.Equal(0.85f, c.CoralIslandShare);
            Assert.Equal(0.2f, c.CoralIslandRoof);
            Assert.Equal(200, c.CoralMaxColonies);
            Assert.Equal(60, c.CoralMaxPiecesPerColony);
            Assert.Equal(6000, c.CoralMaxPieces);
            Assert.Equal(3f, c.CoralTrunkHeightMinMetres);
            Assert.Equal(8f, c.CoralTrunkHeightMaxMetres);
            Assert.Equal(0.35f, c.CoralTrunkRadiusMinMetres);
            Assert.Equal(0.6f, c.CoralTrunkRadiusMaxMetres);
            Assert.Equal(2, c.CoralChildrenMin);
            Assert.Equal(3, c.CoralChildrenMax);
            Assert.Equal(35f, c.CoralTiltMeanDegrees);
            Assert.Equal(15f, c.CoralTiltSpreadDegrees);
            Assert.Equal(0.75f, c.CoralLengthFactor);
            Assert.Equal(0.7f, c.CoralRadiusFactor);
            Assert.Equal(4, c.CoralMaxDepth);
            Assert.Equal(0.12f, c.CoralMinRadiusMetres);
            Assert.Equal(0.3f, c.CoralPlateChance);
            Assert.Equal(0.8f, c.CoralPlateRadiusMinMetres);
            Assert.Equal(2.5f, c.CoralPlateRadiusMaxMetres);
            Assert.Equal(0.25f, c.CoralPlateThicknessMetres);
            Assert.Equal(0.15f, c.CoralFilletMetres);
            Assert.Equal(8f, c.CoralFadeMetres);
            Assert.Equal(0.25f, c.CoralFadeFloor);
            Assert.Equal(0.3f, c.CoralTransmission);
            Assert.Equal(4f, c.CoralGridCellMetres);

            CoralGeometry.Refuse(c, Radius);
        }

        [Fact]
        public void AMushroomWorldRefusesEveryCoralDialOffItsDefault()
        {
            RunConfig c = Tank(ReefShape.Mushroom);
            CoralGeometry.Refuse(c, Radius);
            Assert.Null(Coral(c, 1UL));

            c.CoralIslandRoof = 0.3f;
            c.CoralMaxDepth = 5;
            string message = Refusal(() => CoralGeometry.Refuse(c, Radius));
            _output.WriteLine(message);
            Assert.Contains("CoralIslandRoof 0.3", message);
            Assert.Contains("CoralMaxDepth 5", message);

            // The world refuses it too, before a reef or an island is drawn.
            Assert.Contains("coral group sets", Refusal(() => new World(c, seed: 1)));
        }

        [Fact]
        public void EveryDialOfTheCoralGroupIsRefusedAtMushroom()
        {
            // Through the schema, so a dial added to the group and left out of the refusal fails here.
            int checkedCount = 0;
            foreach (TunableEntry entry in ConfigSchema.Of(new RunConfig()))
            {
                if (entry.Group != "coral" || entry.Key == "reefShape") continue;

                var c = new RunConfig();
                TunableEntry dial = ConfigSchema.Of(c).Single(e => e.Path == entry.Path);
                switch (dial.Get())
                {
                    case float f: dial.Set(f > 0.5f ? f * 0.5f : f + 0.1f); break;
                    case int i: dial.Set(i + 1); break;
                    default: throw new InvalidOperationException(entry.Path + " is a " + entry.ValueType.Name + ", which this test cannot nudge.");
                }

                string message = Refusal(() => CoralGeometry.Refuse(c, Radius));
                Assert.Contains(entry.Path + " ", message);
                checkedCount++;
            }

            Assert.Equal(26, checkedCount);
        }

        [Fact]
        public void CoralRefusesTheMushroomsAWorldWithoutIslandsAndADialOutOfRange()
        {
            RunConfig c = Tank();
            CoralGeometry.Refuse(c, Radius);

            c.ReefCover = 0.1f;
            Assert.Contains("replaces the mushrooms", Refusal(() => CoralGeometry.Refuse(c, Radius)));

            c = Tank();
            c.MatterIslandWavelengthMetres = 0f;
            Assert.Contains("no islands", Refusal(() => CoralGeometry.Refuse(c, Radius)));

            c = Tank();
            c.WorldShape = WorldShape.Box;
            Assert.Contains("WorldShape is Box", Refusal(() => CoralGeometry.Refuse(c, Radius)));
            CoralGeometry.Refuse(c, Radius, worldRules: false);

            c = Tank();
            c.CoralIslandShare = 0.75f;
            Assert.Contains("under D194's 0.8", Refusal(() => CoralGeometry.Refuse(c, Radius)));

            c = Tank();
            c.CoralTiltSpreadDegrees = 40f;
            Assert.Contains("leaves 0 to 180", Refusal(() => CoralGeometry.Refuse(c, Radius)));

            c = Tank();
            c.CoralTrunkHeightMaxMetres = 2f;
            Assert.Contains("trunk's height", Refusal(() => CoralGeometry.Refuse(c, Radius)));

            c = Tank();
            c.CoralChildrenMin = 4;
            Assert.Contains("children run 4 to 3", Refusal(() => CoralGeometry.Refuse(c, Radius)));
        }

        // ------------------------------------------------------------------ the island test

        [Fact]
        public void TheIslandTestIsTheSeedingsInAWorldWithoutRock()
        {
            RunConfig c = Tank(ReefShape.Mushroom);

            foreach (ulong seed in new[] { 6201UL, 6202UL, 6203UL })
            {
                var world = new World(c, seed: seed);
                var grid = (GridField)world.Matter;
                CoralIslandColumns columns = CoralIslandColumns.Build(
                    world.TankRadiusMetres, c.FieldMatterCellMetres, c.WorldDepthMetres, world.Bed,
                    world.IslandMap.Fbm, c.MatterIslandCover, c.MatterIslandGlassMarginMetres);

                Assert.Equal(grid.CellsX, columns.CellsAcross);
                Assert.Equal(world.IslandThreshold, columns.Threshold);

                int islandColumns = 0, differ = 0;
                for (int ix = 0; ix < grid.CellsX; ix++)
                for (int iz = 0; iz < grid.CellsZ; iz++)
                {
                    bool seeded = world.IslandInflux.IsIslandColumn(ix, iz);
                    if (seeded) islandColumns++;
                    if (seeded != columns.IsIslandColumn(ix, iz)) differ++;

                    // A column the bed fills or the glass leaves out is never desert.
                    if (grid.LowestLiveLayer(ix, iz) < 0) Assert.Equal(CoralIslandColumns.Ineligible, columns.IslandOf(ix, iz));
                }

                _output.WriteLine($"seed {seed}: {islandColumns} island columns in {columns.IslandCount} islands, {differ} differ");
                Assert.True(islandColumns > 0);
                Assert.Equal(0, differ);
            }
        }

        [Fact]
        public void IslandsAreJoinedAcrossTheirSidesAndCorners()
        {
            float r = Radius;

            // Two hills 50 m apart on a flat floor, and the cover their tops.
            float Map(float x, float z)
            {
                double a = ((x - (r - 25f)) * (x - (r - 25f)) + (z - r) * (z - r)) / 64d;
                double b = ((x - (r + 25f)) * (x - (r + 25f)) + (z - r) * (z - r)) / 64d;
                return (float)(Math.Exp(-a) + Math.Exp(-b));
            }

            CoralIslandColumns columns = CoralIslandColumns.Build(r, 2f, Depth, null, Map, 0.02f, Margin);
            Assert.Equal(2, columns.IslandCount);

            int n = columns.CellsAcross;
            var seen = new HashSet<int>();
            for (int i = 0; i < columns.IslandCount; i++)
            {
                IReadOnlyList<int> list = columns.IslandColumns(i);
                Assert.Equal(list.OrderBy(v => v), list);

                foreach (int column in list)
                {
                    Assert.True(seen.Add(column));
                    int ix = column / n, iz = column % n;
                    Assert.Equal(i, columns.IslandOf(ix, iz));

                    foreach ((int jx, int jz) in new[] { (ix - 1, iz - 1), (ix - 1, iz), (ix - 1, iz + 1), (ix, iz - 1), (ix, iz + 1), (ix + 1, iz - 1), (ix + 1, iz), (ix + 1, iz + 1) })
                    {
                        int other = columns.IslandOf(jx, jz);
                        Assert.True(other < 0 || other == i, $"island {i} touches island {other}");
                    }
                }
            }

            foreach (int column in columns.DesertColumns)
            {
                Assert.False(seen.Contains(column));
                Assert.Equal(CoralIslandColumns.Desert, columns.IslandOf(column / n, column % n));
            }

            // The first island is the one whose first column comes first: the western hill.
            Assert.True(columns.IslandColumns(0)[0] < columns.IslandColumns(1)[0]);
        }

        // ------------------------------------------------------------------ the colonies

        [Fact]
        public void EveryColonyKeepsTheGrowthRules()
        {
            RunConfig c = Tank();
            const ulong landscape = 6201UL;
            BedShape bed = Bed(c, landscape);
            CoralGeometry coral = CoralGeometry.PlaceFromLandscape(c, Radius, bed, landscape);

            double reachLimit = Radius - Margin;
            int plates = 0, tips = 0, deepest = 0;
            double tiltMin = 180d, tiltMax = 0d;

            Assert.True(coral.Count > 0);

            for (int k = 0; k < coral.Count; k++)
            {
                CoralGeometry.Colony colony = coral.ColonyAt(k);
                Assert.InRange(colony.PieceCount, 1, c.CoralMaxPiecesPerColony);

                // The trunk: vertical, from the bed at the colony's centre, in its ranges.
                CoralGeometry.Piece trunk = coral.PieceAt(colony.FirstPiece);
                Assert.Equal(CoralGeometry.PieceKind.Capsule, trunk.Kind);
                Assert.Equal(-1, trunk.Parent);
                Assert.Equal(0, trunk.Depth);
                Assert.Equal(colony.X, trunk.AX);
                Assert.Equal(colony.X, trunk.BX);
                Assert.Equal(colony.Z, trunk.AZ);
                Assert.Equal(colony.Z, trunk.BZ);
                Assert.Equal(bed.FloorY(colony.X, colony.Z), trunk.AY);
                Assert.Equal(colony.BaseY, trunk.AY);
                Assert.InRange(trunk.BY - trunk.AY, 3d, 8d);
                Assert.InRange(trunk.Radius, 0.35d, 0.6d);

                var children = new int[colony.PieceCount];
                var capsuleChildren = new int[colony.PieceCount];

                for (int i = 0; i < colony.PieceCount; i++)
                {
                    CoralGeometry.Piece p = coral.PieceAt(colony.FirstPiece + i);
                    Assert.Equal(k, p.Colony);
                    deepest = Math.Max(deepest, p.Depth);

                    // Never within 2 m of the surface, never inside the glass margin.
                    double top = p.Kind == CoralGeometry.PieceKind.Plate ? p.AY + p.HalfThickness : Math.Max(p.AY, p.BY) + p.Radius;
                    Assert.True(top <= -2d + 1e-12, $"piece {i} of colony {k} reaches {top} m");
                    double reach = p.Kind == CoralGeometry.PieceKind.Plate
                        ? FromAxis(p.AX, p.AZ) + p.Radius + p.HalfThickness
                        : Math.Max(FromAxis(p.AX, p.AZ), FromAxis(p.BX, p.BZ)) + p.Radius;
                    Assert.True(reach <= reachLimit + 1e-9, $"piece {i} of colony {k} reaches {reach} m from the axis");

                    if (i == 0) continue;

                    // Breadth first: a parent comes before its children, and the depth is one more.
                    Assert.InRange(p.Parent, 0, i - 1);
                    CoralGeometry.Piece parent = coral.PieceAt(colony.FirstPiece + p.Parent);
                    Assert.Equal(CoralGeometry.PieceKind.Capsule, parent.Kind);
                    Assert.Equal(parent.Depth + 1, p.Depth);
                    Assert.True(p.Depth <= c.CoralMaxDepth + 1);
                    Assert.Equal(parent.BX, p.AX);
                    Assert.Equal(parent.BY, p.AY);
                    Assert.Equal(parent.BZ, p.AZ);
                    children[p.Parent]++;

                    if (p.Kind == CoralGeometry.PieceKind.Plate)
                    {
                        plates++;
                        Assert.InRange(p.Radius, 0.8d, 2.5d);
                        Assert.Equal(0.125d, p.HalfThickness, 12);
                        continue;
                    }

                    capsuleChildren[p.Parent]++;
                    Assert.True(p.Depth <= c.CoralMaxDepth);

                    // Shorter by 0.75, thinner by 0.7, at least 0.12 m, tilted 20 to 50 degrees.
                    double pl = Length(parent), cl = Length(p);
                    Assert.Equal(pl * c.CoralLengthFactor, cl, 9);
                    Assert.Equal(parent.Radius * c.CoralRadiusFactor, p.Radius, 12);
                    Assert.True(p.Radius >= 0.12f);

                    double dot = ((parent.BX - parent.AX) * (p.BX - p.AX) + (parent.BY - parent.AY) * (p.BY - p.AY) +
                                  (parent.BZ - parent.AZ) * (p.BZ - p.AZ)) / (pl * cl);
                    double tilt = Math.Acos(Math.Max(-1d, Math.Min(1d, dot))) * 180d / Math.PI;
                    tiltMin = Math.Min(tiltMin, tilt);
                    tiltMax = Math.Max(tiltMax, tilt);
                    Assert.InRange(tilt, 20d - 1e-6, 50d + 1e-6);
                    Assert.Equal(dot, p.TiltCos, 9);
                    Assert.Equal(1d, p.TiltCos * p.TiltCos + p.TiltSin * p.TiltSin, 12);
                    Assert.Equal(1d, p.AzimuthCos * p.AzimuthCos + p.AzimuthSin * p.AzimuthSin, 12);
                }

                for (int i = 0; i < colony.PieceCount; i++)
                {
                    CoralGeometry.Piece p = coral.PieceAt(colony.FirstPiece + i);
                    Assert.True(children[i] <= c.CoralChildrenMax);

                    if (p.Kind == CoralGeometry.PieceKind.Plate)
                    {
                        // A plate sits only on a tip: its parent grew no capsule.
                        Assert.Equal(0, capsuleChildren[p.Parent]);
                        Assert.Equal(0, children[i]);
                    }
                    else if (capsuleChildren[i] == 0)
                    {
                        tips++;
                    }
                }
            }

            _output.WriteLine(
                $"{coral.Count} colonies, {coral.PieceCount} pieces, {plates} plates on {tips} tips, deepest {deepest}, " +
                $"tilts {tiltMin:0.#}-{tiltMax:0.#} degrees");
            Assert.True(plates > 0);
        }

        [Fact]
        public void IslandColoniesStandOnIslandsAndDesertOnesInTheDesertToTheirCanopies()
        {
            RunConfig c = Tank();

            foreach (ulong landscape in new[] { 6201UL, 6202UL, 6203UL })
            {
                CoralGeometry coral = Coral(c, landscape);
                CoralIslandColumns columns = Columns(c, landscape);
                Assert.Equal(columns.IslandCount, coral.IslandCount);

                int onIsland = 0, inDesert = 0;
                for (int k = 0; k < coral.Count; k++)
                {
                    CoralGeometry.Colony colony = coral.ColonyAt(k);
                    int under = columns.IslandAt(colony.X, colony.Z);

                    if (colony.OnIsland)
                    {
                        onIsland++;
                        Assert.Equal(colony.Island, under);
                    }
                    else
                    {
                        inDesert++;
                        Assert.Equal(CoralIslandColumns.Desert, under);
                    }
                }

                // Every island is filled to its roof or closed by a colony that would have
                // overshot by more than half its own canopy. A small island can be roofed past
                // that by a neighbour's spill, so the sum is what is held to the target.
                double target = 0d, got = 0d;
                var roofs = new List<string>();
                for (int i = 0; i < coral.IslandCount; i++)
                {
                    target += coral.IslandTargetSamples(i);
                    got += coral.IslandGotSamples(i);
                    roofs.Add($"{coral.IslandColumnCount(i)}:{coral.IslandRoofGot(i):0.##}");
                }

                _output.WriteLine("  canopy columns:roof by island " + string.Join(" ", roofs));

                double desertShare = coral.DesertGotSamples / (got + coral.DesertGotSamples);
                _output.WriteLine(
                    $"landscape {landscape}: {coral.IslandCount} islands, {onIsland} island and {inDesert} desert colonies, " +
                    $"{coral.PieceCount} pieces; island canopy {got * CoralGeometry.SampleSquareMetres:0} of " +
                    $"{target * CoralGeometry.SampleSquareMetres:0} m2, desert {coral.DesertGotSamples * CoralGeometry.SampleSquareMetres:0} of " +
                    $"{coral.DesertTargetSamples * CoralGeometry.SampleSquareMetres:0} m2 ({desertShare:0.###} of the canopy)");

                Assert.True(onIsland > 0);
                Assert.InRange(got / target, 0.6d, 1.4d);
                Assert.Equal((1d - c.CoralIslandShare) / c.CoralIslandShare * target, coral.DesertTargetSamples, 6);
                Assert.InRange(desertShare, 0d, 0.35d);
            }
        }

        [Fact]
        public void ARoundSizedTankHoldsItsCoralUnderTheCaps()
        {
            // Round 60's tank and islands (r = 167 m, 25 m deep, 5 m matter cells, islands at a
            // 90 m wavelength over 5% of the columns, 30 m off the glass) on a flat floor: a bed this
            // wide takes minutes to build, and the small tank's tests read the coral on a bed.
            var c = new RunConfig
            {
                WorldShape = WorldShape.Tank,
                FieldModel = MatterField.Grid,
                WorldAreaSquareMetres = 88000f,
                WorldDepthMetres = 25f,
                FieldMatterCellMetres = 5f,
                MatterIslandWavelengthMetres = 90f,
                MatterIslandCover = 0.05f,
                MatterIslandGlassMarginMetres = 30f,
                ReefShape = ReefShape.Coral,
            };
            float radius = TankGeometry.RadiusFor(c.WorldAreaSquareMetres);

            foreach (ulong landscape in new[] { 6101UL, 6102UL, 6103UL })
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                CoralGeometry coral = CoralGeometry.PlaceFromLandscape(c, radius, null, landscape);
                double seconds = watch.Elapsed.TotalSeconds;

                double target = 0d, got = 0d;
                int columns = 0;
                for (int i = 0; i < coral.IslandCount; i++)
                {
                    target += coral.IslandTargetSamples(i);
                    got += coral.IslandGotSamples(i);
                    columns += coral.IslandColumnCount(i);
                }

                int island = Enumerable.Range(0, coral.Count).Count(k => coral.ColonyAt(k).OnIsland);
                double desertShare = coral.DesertGotSamples / (got + coral.DesertGotSamples);
                _output.WriteLine(
                    $"landscape {landscape}: {coral.IslandCount} islands of {columns} m2, {island} island and " +
                    $"{coral.Count - island} desert colonies, {coral.PieceCount} pieces; roof {got / (16d * columns):0.###}, " +
                    $"desert {desertShare:0.###} of the canopy; placed in {seconds:0.##} s");

                Assert.InRange(coral.Count, 1, c.CoralMaxColonies);
                Assert.InRange(coral.PieceCount, 1, c.CoralMaxPieces);
                Assert.InRange(got / target, 0.8d, 1.2d);
                Assert.InRange(desertShare, 0.05d, 0.3d);
            }
        }

        [Fact]
        public void TheCapsAreRefusedNotTruncated()
        {
            RunConfig c = Tank();
            c.CoralMaxColonies = 1;
            string message = Refusal(() => Coral(c, 6201UL));
            _output.WriteLine(message);
            Assert.Contains("ceiling of 1 coral colonies", message);

            c = Tank();
            c.CoralMaxPieces = 10;
            message = Refusal(() => Coral(c, 6201UL));
            _output.WriteLine(message);
            Assert.Contains("past the ceiling of 10", message);

            // The per-colony cap is where growth stops.
            c = Tank();
            c.CoralMaxPiecesPerColony = 4;
            CoralGeometry small = Coral(c, 6201UL);
            for (int k = 0; k < small.Count; k++) Assert.InRange(small.ColonyAt(k).PieceCount, 1, 4);
            Assert.Contains(Enumerable.Range(0, small.Count), k => small.ColonyAt(k).PieceCount == 4);
        }

        [Fact]
        public void OneSeedIsOneCoral()
        {
            RunConfig c = Tank();

            CoralGeometry a = Coral(c, 6201UL);
            CoralGeometry b = Coral(c, 6201UL);
            Assert.Equal(a.Digest(), b.Digest());
            Assert.Equal(a.PieceCount, b.PieceCount);

            // The landscape's seed draws it, by the island map's index and the reef stream's.
            var map = new GradientNoise(Rng.SeedFor(6201UL, World.IslandMapIndex), c.MatterIslandWavelengthMetres);
            CoralGeometry direct = CoralGeometry.Place(
                c, Radius, Bed(c, 6201UL), map.Fbm, Rng.SeedFor(6201UL, World.ReefPlacementIndex));
            Assert.Equal(a.Digest(), direct.Digest());

            // Another seed is another coral, and so is another dial.
            Assert.NotEqual(a.Digest(), Coral(c, 6202UL).Digest());
            c.CoralPlateChance = 0.5f;
            Assert.NotEqual(a.Digest(), Coral(c, 6201UL).Digest());

            _output.WriteLine($"landscape 6201: {a.Count} colonies, {a.PieceCount} pieces, digest {a.Digest():x16}");
        }

        [Fact]
        public void AFlatFloorRootsEveryTrunkAtTheTanksDepth()
        {
            RunConfig c = Tank(relief: 0f, tilt: 0f);
            CoralGeometry coral = Coral(c, 6201UL);
            Assert.True(coral.Count > 0);
            for (int k = 0; k < coral.Count; k++) Assert.Equal(-(double)Depth, coral.PieceAt(coral.ColonyAt(k).FirstPiece).AY);
        }

        [Fact]
        public void HandBuiltCoralIsRefusedWhenItsPiecesDoNotHangTogether()
        {
            var trunk = new CoralGeometry.Piece
            {
                Kind = CoralGeometry.PieceKind.Capsule, Parent = -1, AX = 10, AY = -20, AZ = 10, BX = 10, BY = -15, BZ = 10, Radius = 0.5,
            };
            var plate = new CoralGeometry.Piece
            {
                Kind = CoralGeometry.PieceKind.Plate, Parent = 0, Depth = 1, AX = 10, AY = -15, AZ = 10, BX = 10, BY = -15, BZ = 10,
                Radius = 1.5, HalfThickness = 0.125,
            };
            var colony = new CoralGeometry.Colony { X = 10, Z = 10, BaseY = -20, Island = -1, FirstPiece = 0, PieceCount = 2 };

            var coral = new CoralGeometry(0.15f, new[] { colony }, new[] { trunk, plate });
            coral.Bounds(0, out double minX, out double minY, out _, out double maxX, out double maxY, out _);
            Assert.Equal(10d - 1.625d, minX, 12);
            Assert.Equal(10d + 1.625d, maxX, 12);
            Assert.Equal(-20.5d, minY, 12);
            Assert.Equal(-14.5d, maxY, 12);

            CoralGeometry.Piece orphan = plate;
            orphan.Parent = 1;
            Assert.ThrowsAny<ArgumentException>(() => new CoralGeometry(0.15f, new[] { colony }, new[] { trunk, orphan }));

            CoralGeometry.Piece thick = trunk;
            thick.HalfThickness = 0.1;
            Assert.ThrowsAny<ArgumentException>(() => new CoralGeometry(0.15f, new[] { colony }, new[] { thick, plate }));
        }

        private static double Length(CoralGeometry.Piece p)
        {
            double x = p.BX - p.AX, y = p.BY - p.AY, z = p.BZ - p.AZ;
            return Math.Sqrt(x * x + y * y + z * z);
        }

        private static double FromAxis(double x, double z)
        {
            double dx = x - Radius, dz = z - Radius;
            return Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
