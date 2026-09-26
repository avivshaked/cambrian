using System;
using System.Collections.Generic;
using Evosim.Core;
using Evosim.Dynamics.Placement;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// The round 48 founding ruling's depth (owner, 2026-09-24,
    /// <see cref="RunConfig.FoundersFollowFoodDepth"/>): a founder accepted in a column is set at
    /// the richest cell of its food there, through the farm's own placer.
    /// </summary>
    /// <remarks>
    /// A grid box 24 × 6 × 24 m (1 m snow cells, 3 m matter cells) with its food in one 3 × 3 m
    /// region: snow in the 1 m layer at 10 to 11 m, with a tenth as much at 2 to 3 m, and
    /// dissolved matter in the 3 m layer at 6 to 9 m, with a tenth as much at 0 to 3 m. The world
    /// and the placer are wired as <c>Evosim.Farm.Simulation</c> wires them, flat bed included.
    /// </remarks>
    public sealed class FounderDepthPlacementTests
    {
        private readonly ITestOutputHelper _output;

        public FounderDepthPlacementTests(ITestOutputHelper output) => _output = output;

        private const float Drawn = -2f;
        private const int Copies = 8;

        private static RunConfig GridBox(bool atDepth) => new RunConfig
        {
            Light = new LightModel(100f, 12f),
            SharedSpace = true,
            FieldModel = MatterField.Grid,
            WorldAreaSquareMetres = 144f,
            HorizontalPatches = 4,
            WorldDepthMetres = 24f,
            NutrientMixingDiffusivity = 0.2f,
            HorizontalMixingDiffusivity = 0.2f,
            MatterMixingDiffusivity = 0.2f,
            InitialMatterPerCubicMetre = 0f,
            MinimumPopulation = 0,
            MaximumPopulation = 100_000,
            FoundersFollowFood = true,
            FoundersFollowFoodDepth = atDepth,
        };

        private static (World World, SharedVolume Volume) Build(bool atDepth, ulong seed) =>
            Build(GridBox(atDepth), seed, pool: null);

        private static (World World, SharedVolume Volume) Build(
            RunConfig config, ulong seed, IReadOnlyList<Genome> pool)
        {
            var world = new World(config, seed, pool);

            var volume = new SharedVolume(
                Math.Max(1, (int)config.HorizontalPatches), world.Nutrients.PatchWidthMetres,
                config.WorldDepthMetres, seed, config.OffspringDispersalMetres,
                Math.Max(1, (int)config.PatchesAcross), config.WorldShape, world.TankRadiusMetres);

            world.Placement = volume;
            volume.Floor = new PlacementFloor(config.WorldDepthMetres, world.Bed, world.Reefs);

            for (int ix = 0; ix < 3; ix++)
            {
                for (int iz = 0; iz < 3; iz++)
                {
                    world.Nutrients.Deposit(new FieldPoint(new Float3(ix + 0.5f, -10.5f, iz + 0.5f), 0), 10f);
                    world.Nutrients.Deposit(new FieldPoint(new Float3(ix + 0.5f, -2.5f, iz + 0.5f), 0), 1f);
                }
            }

            world.Matter.Deposit(new FieldPoint(new Float3(1.5f, -7.5f, 1.5f), 0), 10f);
            world.Matter.Deposit(new FieldPoint(new Float3(1.5f, -1.5f, 1.5f), 0), 1f);

            return (world, volume);
        }

        private static Genome Cube(string cellType)
        {
            var g = new Genome();
            g.Nodes.Add(new MorphNode
            {
                CellTypeId = cellType,
                ShapeId = ShapeIds.Box,
                Dimensions = new Float3(0.2f, 0.2f, 0.2f),
                JointType = JointType.Fixed,
                JointLimits = Array.Empty<Float2>(),
                RecursiveLimit = 1,
                Neurons = Array.Empty<NeuronDef>(),
            });
            g.RootIndex = 0;
            return g;
        }

        /// <summary>Plants <see cref="Copies"/> of a body and returns where each landed.</summary>
        private List<Float3> Plant(string cellType, bool atDepth, ulong seed)
        {
            (World world, SharedVolume volume) = Build(atDepth, seed);

            world.Inoculate(Cube(cellType), Copies, heightY: Drawn);
            Assert.Equal(Copies, world.Living.Count);

            var landed = new List<Float3>();
            foreach (Organism o in world.Living)
            {
                Assert.True(volume.TryTakePlacement(o.Id, out Float3 at), $"no placement for {o.Id}");

                // The height the world admitted the body at is the one it was placed at.
                Assert.Equal(at.Y, o.HeightY);
                landed.Add(at);
            }

            float lo = float.MaxValue, hi = float.MinValue;
            foreach (Float3 p in landed) { lo = Math.Min(lo, p.Y); hi = Math.Max(hi, p.Y); }
            _output.WriteLine($"{cellType}, depth rule {(atDepth ? "on" : "off")}: heights {lo:0.###} to {hi:0.###} m");

            return landed;
        }

        [Fact]
        public void AStomachLandsInTheRichestSnowLayer()
        {
            foreach (Float3 p in Plant(CellTypeIds.Absorptive, atDepth: true, seed: 41UL))
            {
                Assert.True(p.X < 3f && p.Z < 3f, $"outside the snow region at ({p.X}, {p.Z})");
                Assert.InRange(p.Y, -11f, -10f);
            }
        }

        [Fact]
        public void ALeafLandsInTheRichestMatterLayer()
        {
            foreach (Float3 p in Plant(CellTypeIds.Photosynthetic, atDepth: true, seed: 42UL))
            {
                Assert.True(p.X < 3f && p.Z < 3f, $"outside the matter region at ({p.X}, {p.Z})");
                Assert.InRange(p.Y, -9f, -6f);
            }
        }

        [Fact]
        public void AMixotrophTakesTheFieldWithTheLargerShareTheSnowOnATie()
        {
            // Both fields hold all their stock in the one region, so both shares are 1 there and
            // the tie goes to the snow, as D116's acceptance reads it.
            (World world, SharedVolume volume) = Build(atDepth: true, seed: 46UL);

            Genome g = Cube(CellTypeIds.Absorptive);
            g.Nodes.Add(Cube(CellTypeIds.Photosynthetic).Nodes[0]);
            g.Nodes[0].Edges.Add(new MorphEdge
            {
                Child = 1,
                ParentAnchor = new Float3(1f, 0f, 0f),
                ChildAnchor = new Float3(-1f, 0f, 0f),
                Orientation = Quat.Identity,
                Scale = Float3.One,
            });

            world.Inoculate(g, 4, heightY: Drawn);
            Assert.Equal(4, world.Living.Count);

            foreach (Organism o in world.Living)
            {
                Assert.True(volume.TryTakePlacement(o.Id, out Float3 at));
                Assert.InRange(at.Y, -11f, -10f);
            }
        }

        [Fact]
        public void WithTheRuleOffEveryFounderKeepsTheDrawnDepth()
        {
            foreach (Float3 p in Plant(CellTypeIds.Absorptive, atDepth: false, seed: 43UL))
            {
                Assert.True(p.X < 3f && p.Z < 3f, $"D116 still chooses the column: ({p.X}, {p.Z})");
                Assert.Equal(Drawn, p.Y);
            }
        }

        [Fact]
        public void ABodyThatEatsNothingKeepsTheDrawnDepth()
        {
            foreach (Float3 p in Plant(CellTypeIds.Structural, atDepth: true, seed: 44UL))
            {
                Assert.Equal(Drawn, p.Y);
            }
        }

        [Fact]
        public void WithTheRuleOffThePlacerIsHandedNoDepth()
        {
            (World world, SharedVolume volume) = Build(atDepth: false, seed: 45UL);
            world.Inoculate(Cube(CellTypeIds.Absorptive), 0, heightY: Drawn);

            Assert.NotNull(volume.FounderAcceptance);
            Assert.Null(volume.FounderDepth);
        }

        // ------------------------------------------------------ round 50's income depth

        private static RunConfig IncomeBox()
        {
            RunConfig config = GridBox(atDepth: true);
            config.FoundersFollowIncomeDepth = true;
            return config;
        }

        /// <summary>
        /// The box with the matter's richest cell moved deep: 30 more units in the 3 m layer at 18
        /// to 21 m, where the light is about a fifth of the surface's, so that the round 48 rule
        /// and the income rule part.
        /// </summary>
        private static (World World, SharedVolume Volume) DeepMatter(RunConfig config, ulong seed)
        {
            (World world, SharedVolume volume) = Build(config, seed, pool: null);
            world.Matter.Deposit(new FieldPoint(new Float3(1.5f, -19.5f, 1.5f), 0), 30f);
            return (world, volume);
        }

        /// <summary>
        /// A body's income a second, light and food together, at the centre of the box's 1 m
        /// layer <paramref name="iy"/>, priced as the metabolic pass prices it. Patch 0: the
        /// matter region lies in the box's first patch.
        /// </summary>
        private static double IncomeAt(World world, Phenotype body, float x, float z, int iy)
        {
            float y = -(iy + 0.5f);
            var at = new FieldPoint(new Float3(x, y, z), 0);

            EnergyLedger ledger = Metabolism.StepAt(
                body, world.Config, world.Field.IrradianceAt(y, 0, x, z),
                world.Nutrients.EdibleDensityAt(at), world.Matter.DensityAt(at), 0f, 1f);

            return ledger.LightIncome + (double)ledger.FoodIncome;
        }

        /// <summary>Plants leaves in the deep-matter box and returns where each landed, with its body.</summary>
        private List<(Float3 At, Phenotype Body, World World)> PlantDeepLeaves(RunConfig config, ulong seed)
        {
            (World world, SharedVolume volume) = DeepMatter(config, seed);
            world.Inoculate(Cube(CellTypeIds.Photosynthetic), Copies, heightY: Drawn);
            Assert.Equal(Copies, world.Living.Count);

            var landed = new List<(Float3, Phenotype, World)>();
            foreach (Organism o in world.Living)
            {
                Assert.True(volume.TryTakePlacement(o.Id, out Float3 at), $"no placement for {o.Id}");
                Assert.True(at.X < 3f && at.Z < 3f, $"outside the matter region at ({at.X}, {at.Z})");
                landed.Add((at, o.Phenotype, world));
            }

            return landed;
        }

        [Fact]
        public void ALeafByIncomeLandsInTheCellThatPricesItHighest()
        {
            foreach ((Float3 at, Phenotype body, World world) in PlantDeepLeaves(IncomeBox(), 51UL))
            {
                int best = -1;
                double most = 0d;
                var line = new System.Text.StringBuilder();

                for (int iy = 0; iy < 24; iy++)
                {
                    double income = IncomeAt(world, body, at.X, at.Z, iy);
                    if (income > most)
                    {
                        most = income;
                        best = iy;
                    }

                    line.Append(FormattableString.Invariant($" {iy}:{income:0.####}"));
                }

                _output.WriteLine(FormattableString.Invariant($"landed at {at.Y:0.###} m; income by layer:{line}"));
                Assert.True(best >= 0, "the leaf prices at nothing in every layer");
                Assert.InRange(at.Y, -(best + 1f), -(float)best);
            }
        }

        [Fact]
        public void WhereTheRichestMatterIsDarkTheIncomeRuleSetsALeafShallower()
        {
            // The round 48 rule sets a leaf in the richest matter, 18 to 21 m down; round 49's
            // trickle leaves died in the same arrangement at the bed.
            foreach ((Float3 at, _, _) in PlantDeepLeaves(GridBox(atDepth: true), 54UL))
            {
                Assert.InRange(at.Y, -21f, -18f);
            }

            foreach ((Float3 at, _, _) in PlantDeepLeaves(IncomeBox(), 54UL))
            {
                Assert.True(at.Y > -18f, FormattableString.Invariant($"a leaf by income landed at {at.Y} m"));
            }
        }

        [Fact]
        public void AStomachIsPlacedByTheRound48RuleWithTheIncomeRuleOn()
        {
            (World world, SharedVolume volume) = Build(IncomeBox(), 52UL, pool: null);
            world.Inoculate(Cube(CellTypeIds.Absorptive), Copies, heightY: Drawn);
            Assert.Equal(Copies, world.Living.Count);

            foreach (Organism o in world.Living)
            {
                Assert.True(volume.TryTakePlacement(o.Id, out Float3 at), $"no placement for {o.Id}");
                Assert.InRange(at.Y, -11f, -10f);
            }
        }

        [Fact]
        public void TheIncomeRuleIsRefusedWithoutTheDepthRule()
        {
            RunConfig config = GridBox(atDepth: false);
            config.FoundersFollowIncomeDepth = true;

            Assert.Throws<ArgumentException>(() => new World(config, 53UL, null));
        }

        // ------------------------------------------------------ round 49's landing readings

        /// <summary>
        /// The box with the founding trickle drawing every founder from a pool of a stomach
        /// (index 0) and a leaf (index 1), one founder a half-second step on average after the
        /// floor closes at 0.5 s.
        /// </summary>
        private static (World World, SharedVolume Volume) PoolBox(ulong seed) =>
            Build(TrickleConfig(poolCount: 2), seed, PoolOfTwo());

        private static RunConfig TrickleConfig(int poolCount)
        {
            RunConfig config = GridBox(atDepth: true);
            config.FloorClosesAfterSeconds = 0.5f;
            config.FoundingTricklePerSecond = 2f;
            config.FoundingTricklePoolShare = 1f;
            config.FoundingTricklePoolCount = poolCount;

            // Any string of a SHA-256's shape: Core does not recompute it (D117).
            config.FoundingTricklePoolHash = new string('a', 64);
            return config;
        }

        private static Genome[] PoolOfTwo() =>
            new[] { Cube(CellTypeIds.Absorptive), Cube(CellTypeIds.Photosynthetic) };

        /// <summary>
        /// The column mean asked of the field cell by cell, as a check of
        /// <see cref="GridField.MeanEdibleDensityInColumn"/> that does not call it: the box has no
        /// mask, so every layer of the column is water.
        /// </summary>
        private static double ColumnMean(GridField field, float x, float z)
        {
            double sum = 0d;
            for (int iy = 0; iy < field.LayerCount; iy++)
            {
                float y = -(iy + 0.5f) * field.CellMetres;
                sum += field.EdibleDensityAt(new FieldPoint(new Float3(x, y, z), 0));
            }

            return sum / field.LayerCount;
        }

        [Fact]
        public void AFounderSetAtItsFoodRecordsTheDensityItLandedInAndItIsAtLeastItsColumnsMean()
        {
            // 0120's F4 asked it of the first absorptive.jsonl row, five seconds and ten meals
            // after landing; the row now carries it from the instant of admission. The trickle
            // spawns at the end of World.Step, after every pass that moves the fields, so the
            // fields as the step leaves them are the fields each founder was read against.
            (World world, SharedVolume volume) = PoolBox(47UL);
            var snow = (GridField)world.Nutrients;
            var matter = (GridField)world.Matter;

            int stomachs = 0, leaves = 0, children = 0;

            for (int step = 0; step < 60; step++)
            {
                world.Step(0.5f);

                foreach (LineageEvent e in world.DrainLineageEvents())
                {
                    if (e.Kind != LineageEventKind.Birth) continue;

                    // A founder's child is not a founder, and its row is the row it always was.
                    if (e.Source == FounderSource.None)
                    {
                        children++;
                        Assert.DoesNotContain("\"fsnow\":", e.ToJson());
                        Assert.DoesNotContain("\"fmat\":", e.ToJson());
                        continue;
                    }

                    Assert.Equal(FounderSource.Pool, e.Source);

                    Assert.True(volume.TryTakePlacement(e.Id, out Float3 at), $"no placement for {e.Id}");
                    var point = new FieldPoint(at, 0);
                    string row = e.ToJson();

                    if (e.PoolIndex == 0)
                    {
                        stomachs++;

                        Assert.Equal(snow.EdibleDensityAt(point), e.LandingSnowDensity);
                        Assert.True(e.LandingSnowDensity > 0f, $"{e.Id} landed in water with no snow");
                        Assert.True(
                            e.LandingSnowDensity >= e.LandingSnowColumnDensity,
                            $"{e.Id}: fsnow {e.LandingSnowDensity} under fcol {e.LandingSnowColumnDensity}");

                        double mean = ColumnMean(snow, at.X, at.Z);
                        Assert.True(
                            Math.Abs(mean - e.LandingSnowColumnDensity) <= 1e-6 * Math.Max(mean, 1e-9),
                            $"{e.Id}: fcol {e.LandingSnowColumnDensity} against the cells' mean {mean}");

                        Assert.True(float.IsNaN(e.LandingMatterDensity));
                        Assert.Contains("\"fsnow\":", row);
                        Assert.Contains("\"fcol\":", row);
                        Assert.DoesNotContain("\"fmat\":", row);
                    }
                    else
                    {
                        leaves++;

                        Assert.Equal(matter.EdibleDensityAt(point), e.LandingMatterDensity);
                        Assert.True(e.LandingMatterDensity > 0f, $"{e.Id} landed in water with no matter");
                        Assert.True(
                            e.LandingMatterDensity >= e.LandingMatterColumnDensity,
                            $"{e.Id}: fmat {e.LandingMatterDensity} under fmcol {e.LandingMatterColumnDensity}");

                        double mean = ColumnMean(matter, at.X, at.Z);
                        Assert.True(
                            Math.Abs(mean - e.LandingMatterColumnDensity) <= 1e-6 * Math.Max(mean, 1e-9),
                            $"{e.Id}: fmcol {e.LandingMatterColumnDensity} against the cells' mean {mean}");

                        Assert.True(float.IsNaN(e.LandingSnowDensity));
                        Assert.Contains("\"fmat\":", row);
                        Assert.DoesNotContain("\"fsnow\":", row);
                    }

                    if (stomachs + leaves <= 4) _output.WriteLine(row);
                }
            }

            _output.WriteLine($"{stomachs} stomachs and {leaves} leaves read at landing, {children} children");
            Assert.True(stomachs > 0, "no stomach was admitted");
            Assert.True(leaves > 0, "no leaf was admitted");
        }

        [Fact]
        public void AnInoculantAndAFounderThatEatsNothingCarryNoLandingReading()
        {
            // An inoculant is placed by the same rule and is not a founder; its row is the row
            // it always was. A structural founder eats neither food.
            (World world, SharedVolume volume) = Build(atDepth: true, seed: 48UL);
            world.Inoculate(Cube(CellTypeIds.Absorptive), 2, heightY: Drawn);

            foreach (LineageEvent e in world.DrainLineageEvents())
            {
                Assert.True(float.IsNaN(e.LandingSnowDensity));
                Assert.DoesNotContain("\"fsnow\":", e.ToJson());
            }

            (World inert, _) = Build(
                TrickleConfig(poolCount: 1), 49UL, new[] { Cube(CellTypeIds.Structural) });
            int founders = 0;

            for (int step = 0; step < 20; step++)
            {
                inert.Step(0.5f);

                foreach (LineageEvent e in inert.DrainLineageEvents())
                {
                    if (e.Kind != LineageEventKind.Birth) continue;
                    founders++;

                    string row = e.ToJson();
                    Assert.DoesNotContain("\"fsnow\":", row);
                    Assert.DoesNotContain("\"fmat\":", row);
                }
            }

            Assert.True(founders > 0, "no structural founder was admitted");
        }

        [Fact]
        public void ACheckpointCarriesAFoundersLandingReadings()
        {
            // StateVersion 12: a founder row queued before a checkpoint is the same row after
            // the restore, its landing readings included.
            //
            // The queue cannot be read without being drained, so a first world finds the step at
            // which a stomach is first admitted, and a second of the same seed stops there with
            // the row still queued. Draining changes no trajectory (World.DrainLineageEvents).
            int first = -1;
            (World scout, _) = PoolBox(50UL);

            for (int step = 0; step < 60 && first < 0; step++)
            {
                scout.Step(0.5f);

                foreach (LineageEvent e in scout.DrainLineageEvents())
                {
                    if (!float.IsNaN(e.LandingSnowDensity)) first = step;
                }
            }

            Assert.True(first >= 0, "no stomach founder was admitted");

            (World world, _) = PoolBox(50UL);

            for (int step = 0; step <= first; step++)
            {
                if (step > 0) world.DrainLineageEvents();
                world.Step(0.5f);
            }

            byte[] state = StateOf(world);

            var restored = new World(TrickleConfig(poolCount: 2), 50UL, PoolOfTwo());
            using (var buffer = new System.IO.MemoryStream(state, writable: false))
            using (var r = new System.IO.BinaryReader(buffer, System.Text.Encoding.UTF8))
            {
                restored.ReadState(r);
                Assert.Equal(state.Length, buffer.Position);
            }

            Assert.Equal(state, StateOf(restored));

            var before = new List<string>();
            foreach (LineageEvent e in world.DrainLineageEvents()) before.Add(e.ToJson());

            var after = new List<string>();
            foreach (LineageEvent e in restored.DrainLineageEvents()) after.Add(e.ToJson());

            Assert.Contains(before, row => row.Contains("\"fsnow\":"));
            Assert.Equal(before, after);
        }

        private static byte[] StateOf(World world)
        {
            using (var buffer = new System.IO.MemoryStream())
            {
                using (var w = new System.IO.BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
                {
                    world.WriteState(w);
                    w.Flush();
                }

                return buffer.ToArray();
            }
        }

        [Fact]
        public void TheDepthRuleWithoutD116IsRefused()
        {
            RunConfig config = GridBox(atDepth: true);
            config.FoundersFollowFood = false;

            Assert.Throws<ArgumentException>(() => new World(config, 1UL));
        }
    }
}
