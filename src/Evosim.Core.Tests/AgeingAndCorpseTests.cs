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
    /// Round 51's three rules: death by age (D128), the corpse that sinks, settles and dissolves
    /// (D127), and the free first link (D127).
    /// </summary>
    /// <remarks>
    /// Every rule is off at its default, and the rest of the suite holds that: it ran unchanged on
    /// this build, goldens included. What is asked here is what each rule does when it is on, and
    /// that both books still close and a checkpoint still carries the world.
    /// </remarks>
    public class AgeingAndCorpseTests
    {
        private readonly ITestOutputHelper _output;

        public AgeingAndCorpseTests(ITestOutputHelper output) => _output = output;

        /// <summary>A small lit world with a floor, as the checkpoint tests stage one.</summary>
        private static RunConfig Stage(float ageBase = 0f, float birth = 0f, float wound = 0f) => new RunConfig
        {
            MinimumPopulation = 12,
            MaximumPopulation = 2_000,
            WorldAreaSquareMetres = 100f,
            WorldDepthMetres = 20f,
            Light = new LightModel(64f, 12f),
            AgeHazardBaseSeconds = ageBase,
            AgeingPerBirth = birth,
            AgeingPerWound = wound,
        };

        /// <summary>A dark world where everything starves: the only thing a death makes is a corpse.</summary>
        private static RunConfig Dark(float decay, float sink, bool settles, float floor) => new RunConfig
        {
            Light = new LightModel(1e-6f, 1f),
            MinimumPopulation = 20,
            WorldDepthMetres = 20f,
            RemineralisationPerSecond = 0f,
            CorpseDecayPerSecond = decay,
            CorpseSinkMetresPerSecond = sink,
            CorpseSettles = settles,
            CorpseLeavesBelowFraction = floor,
        };

        private static void AssertBooksClose(World world)
        {
            Assert.True(
                Math.Abs(world.AuditResidual) <= 1e-6 * Math.Max(1d, world.StandingJoules),
                $"audit residual {world.AuditResidual:R}");
            Assert.True(
                Math.Abs(world.MatterResidual) <= 1e-6 * Math.Max(1d, world.MatterInitialTotal),
                $"matter residual {world.MatterResidual:R}");
        }

        // ------------------------------------------------------------------ death by age

        [Fact]
        public void WithTheHazardOffNoClockMovesAndNobodyDiesOfAge()
        {
            Assert.Equal(0f, new RunConfig().AgeHazardBaseSeconds);

            var world = new World(Stage(), seed: 3);
            world.Inoculate(Fixtures.SingleLeaf(), count: 4, heightY: -5f);
            for (int i = 0; i < 120; i++) world.Step(0.5f);

            Assert.NotEmpty(world.Living);
            Assert.All(world.Living, c => Assert.Equal(0d, c.AgeClock));
            Assert.All(world.Living, c => Assert.Equal(0d, c.AgeHazard));
            Assert.Equal(0L, world.DeathsByAge);
        }

        [Fact]
        public void TheWearAndTheHazardAreRefusedTogetherAndAPushNeedsAHazard()
        {
            RunConfig both = Stage(ageBase: 100f);
            both.SenescenceDoublingSeconds = 3_000f;
            Assert.Throws<ArgumentException>(() => new World(both, seed: 1));

            Assert.Throws<ArgumentException>(() => new World(Stage(birth: 1f), seed: 1));
            Assert.Throws<ArgumentException>(() => new World(Stage(wound: 1f), seed: 1));

            RunConfig repair = Stage();
            repair.AgeRepairExponent = 1f;
            Assert.Throws<ArgumentException>(() => new World(repair, seed: 1));

            RunConfig flat = Stage(ageBase: 100f);
            flat.AgeHazardDoublingFraction = 0f;
            Assert.Throws<ArgumentException>(() => new World(flat, seed: 1));

            // And the rule on its own is a world.
            Assert.NotNull(new World(Stage(ageBase: 100f, birth: 1f, wound: 1f), seed: 1));
        }

        [Fact]
        public void BodiesDieOfAgeSolventAndNearTheBaseAndTheBooksClose()
        {
            const float Base = 40f;
            var world = new World(Stage(ageBase: Base), seed: 7);
            world.Inoculate(Fixtures.SingleLeaf(), count: 4, heightY: -5f);

            var born = new Dictionary<long, double>();
            var ages = new List<double>();
            var reserves = new List<double>();

            for (int i = 0; i < 800; i++)
            {
                world.Step(0.5f);
                foreach (LineageEvent e in world.DrainLineageEvents())
                {
                    if (e.Kind == LineageEventKind.Birth) born[e.Id] = e.ElapsedSeconds;
                    else if (e.Cause == DeathCause.Aged && born.TryGetValue(e.Id, out double b))
                    {
                        ages.Add(e.ElapsedSeconds - b);
                        reserves.Add(e.ReserveJoulesAtDeath);
                    }
                }

                AssertBooksClose(world);
            }

            Assert.True(world.DeathsByAge >= 20, "too few deaths by age to read: " + world.DeathsByAge);

            ages.Sort();
            _output.WriteLine(FormattableString.Invariant(
                $"deaths by age {world.DeathsByAge} of {world.Deaths}; ages p10 {ages[ages.Count / 10]:0.0} median {ages[ages.Count / 2]:0.0} p90 {ages[ages.Count * 9 / 10]:0.0} s"));

            // The hazard's median is the base and its 90th percentile 1.34 of it at a doubling of a
            // fifth. A starvation takes some bodies before their time, which can only pull the
            // ages of those that reach their threshold down. Gompertz's hazard is not zero at
            // birth (K ln 2 / D), so a death by age can come at any age and none is asserted.
            double median = ages[ages.Count / 2];
            double p90 = ages[ages.Count * 9 / 10];
            Assert.InRange(median, 0.5 * Base, 1.5 * Base);
            Assert.InRange(p90, 1.0 * Base, 1.7 * Base);

            // Every one of them died holding a reserve, which is the rule's point.
            Assert.All(reserves, r => Assert.True(r > 0d));
        }

        [Fact]
        public void TheClockRunsWithTheAgeAndABirthPushesItAhead()
        {
            // A base so long that nobody dies of age: the clock is read, not the deaths.
            var world = new World(Stage(ageBase: 1e6f, birth: 1f), seed: 5);
            world.Inoculate(Fixtures.SingleLeaf(), count: 4, heightY: -5f);
            for (int i = 0; i < 600; i++) world.Step(0.5f);

            Assert.Contains(world.Living, c => c.Children > 0);

            foreach (Organism c in world.Living)
            {
                if (c.Children > 0)
                {
                    // At least one push for a child a tenth of the parent: a tenth of a doubling,
                    // 0.2 of the base times 0.1, far past any rounding of the age.
                    Assert.True(c.AgeClock > c.Age + 100.0, $"{c.Id}: clock {c.AgeClock} age {c.Age}");
                }
                else
                {
                    Assert.True(Math.Abs(c.AgeClock - c.Age) < 1e-2, $"{c.Id}: clock {c.AgeClock} age {c.Age}");
                }
            }
        }

        [Fact]
        public void TheThresholdIsTheBodysOwnAndAnExponentialDraw()
        {
            var world = new World(Stage(ageBase: 40f), seed: 9);
            world.Inoculate(Fixtures.SingleLeaf(), count: 4, heightY: -5f);
            world.Step(0.5f);

            var thresholds = world.Living.Select(c => world.AgeThreshold(c)).ToList();
            Assert.Equal(thresholds, world.Living.Select(c => world.AgeThreshold(c)).ToList());
            Assert.All(thresholds, t => Assert.True(t > 0d && !double.IsInfinity(t)));
            Assert.True(thresholds.Distinct().Count() > thresholds.Count / 2, "the draws do not differ by body");
        }

        // ------------------------------------------------------------------ the corpse

        [Fact]
        public void ACorpseSinksAtItsOwnRateSettlesOnTheBedAndStays()
        {
            RunConfig config = Dark(decay: 0.001f, sink: 1f, settles: true, floor: 0f);
            var world = new World(config, seed: 2);

            for (int i = 0; i < 200; i++) world.Step(0.5f);

            List<Corpse> settled = world.Corpses.Where(c => c.Settled).ToList();
            Assert.NotEmpty(settled);
            Assert.All(settled, c => Assert.Equal(-config.WorldDepthMetres, c.Position.Y));

            var where = settled.ToDictionary(c => c.CreatureId, c => c.Position);
            for (int i = 0; i < 20; i++) world.Step(0.5f);

            foreach (Corpse c in world.Corpses)
            {
                if (where.TryGetValue(c.CreatureId, out Float3 was)) Assert.Equal(was, c.Position);
            }

            AssertBooksClose(world);
        }

        [Fact]
        public void ACorpseLeavesWhenItFallsUnderItsShareOfWhatItWas()
        {
            RunConfig config = Dark(decay: 0.1f, sink: 0f, settles: false, floor: 0.5f);
            var world = new World(config, seed: 2);

            for (int i = 0; i < 200; i++)
            {
                world.Step(0.5f);
                foreach (Corpse c in world.Corpses)
                {
                    Assert.True(c.Joules >= 0.5 * c.FirstJoules * (1d - 1e-9), $"{c}");

                    // Half is gone after ln 2 / 0.1 = 6.9 s of instalments.
                    Assert.True(c.AgeSeconds <= 8.0, $"{c}");
                }
            }

            Assert.True(world.Deaths > 0);
            AssertBooksClose(world);
        }

        [Fact]
        public void ASettledCorpseDecaysAtTheSettledRateAndAFallingOneAtTheSinkingRate()
        {
            // D131: slow on the way down, fast from the step it lands.
            RunConfig config = Dark(decay: 0.0001f, sink: 0.5f, settles: true, floor: 0f);
            config.CorpseSettledDecayPerSecond = 0.05f;
            var world = new World(config, seed: 2);

            int falling = 0, settled = 0;
            for (int i = 0; i < 200; i++)
            {
                var before = world.Corpses.ToDictionary(c => c.CreatureId, c => c.Joules);
                world.Step(0.5f);
                foreach (Corpse c in world.Corpses)
                {
                    if (!before.TryGetValue(c.CreatureId, out double was) || was <= 0d) continue;
                    double expected = c.Settled ? 1d - 0.05 * 0.5 : 1d - 0.0001 * 0.5;
                    Assert.True(Math.Abs(c.Joules / was - expected) < 1e-5,
                        $"{c}: kept {c.Joules / was:R} of its joules, expected {expected:R}");
                    if (c.Settled) settled++; else falling++;
                }
            }

            Assert.True(falling > 0 && settled > 0, $"falling {falling}, settled {settled}");
            AssertBooksClose(world);
        }

        [Fact]
        public void ASettledRateOfZeroIsTheSinkingRate()
        {
            RunConfig at0 = Dark(decay: 0.001f, sink: 0.5f, settles: true, floor: 0f);
            RunConfig same = Dark(decay: 0.001f, sink: 0.5f, settles: true, floor: 0f);
            same.CorpseSettledDecayPerSecond = 0.001f;
            var a = new World(at0, seed: 3);
            var b = new World(same, seed: 3);
            for (int i = 0; i < 200; i++) { a.Step(0.5f); b.Step(0.5f); }

            Assert.Contains(a.Corpses, c => c.Settled);
            Assert.Equal(a.Corpses.Select(c => c.Joules), b.Corpses.Select(c => c.Joules));
            Assert.Equal(a.DetritusDepositedTotal, b.DetritusDepositedTotal);
        }

        [Fact]
        public void ASettledRateIsRefusedWhereNoCorpseSettles()
        {
            RunConfig drifting = Dark(decay: 0.001f, sink: 0.5f, settles: false, floor: 0f);
            drifting.CorpseSettledDecayPerSecond = 0.005f;
            Assert.Throws<ArgumentException>(() => new World(drifting, seed: 1));

            RunConfig none = Dark(decay: 0f, sink: 0.5f, settles: true, floor: 0f);
            none.CorpseSettledDecayPerSecond = 0.005f;
            Assert.Throws<ArgumentException>(() => new World(none, seed: 1));

            Assert.Throws<ArgumentOutOfRangeException>(() => new RunConfig { CorpseSettledDecayPerSecond = -1f });
        }

        [Fact]
        public void ACorpseOverAReefSettlesOnItsCapAndAnyOtherOnTheBed()
        {
            // D127: a corpse settles on the first reef cap under it or on the bed. A Core world has
            // no placer, so the bodies are put where the farm would report them: half a metre
            // under the surface over the middle of a cap, or over open water. With no current a
            // corpse falls straight down, so the first half must settle on that cap's top and the
            // second on the bed.
            RunConfig config = ReefTank.Config();
            config.Light = new LightModel(1e-6f, 1f);
            config.MinimumPopulation = 20;
            config.RemineralisationPerSecond = 0f;
            config.CorpseDecayPerSecond = 0.001f;
            config.CorpseSinkMetresPerSecond = 1f;
            config.CorpseSettles = true;
            config.CorpseLeavesBelowFraction = 0f;

            var world = new World(config, seed: 4);
            Assert.NotNull(world.Reefs);
            Assert.True(world.Reefs.Count > 0);

            ReefGeometry reefs = world.Reefs;
            float capX = (float)reefs.CentreX(0), capZ = (float)reefs.CentreZ(0);
            Assert.True(reefs.InsideOutline(0, capX, capZ));
            float capTop = (float)Enumerable.Range(0, reefs.Count)
                .Where(r => reefs.InsideOutline(r, capX, capZ))
                .Max(r => reefs.CapTopY(r));

            // Open water: the first point on a ring round the tank's axis outside every outline.
            float c = ReefTank.Radius, openX = float.NaN, openZ = float.NaN;
            for (int k = 0; k < 360 && float.IsNaN(openX); k++)
            {
                float x = c + 0.7f * c * (float)Math.Cos(k * Math.PI / 180d);
                float z = c + 0.7f * c * (float)Math.Sin(k * Math.PI / 180d);
                if (!Enumerable.Range(0, reefs.Count).Any(r => reefs.InsideOutline(r, x, z)))
                {
                    openX = x;
                    openZ = z;
                }
            }

            Assert.False(float.IsNaN(openX), "no open water on the ring");

            for (int i = 0; i < 400; i++)
            {
                foreach (Organism body in world.Living)
                {
                    world.Observe(
                        body,
                        body.Id % 2 == 0 ? new Float3(capX, -0.5f, capZ) : new Float3(openX, -0.5f, openZ),
                        0f);
                }

                world.Step(0.5f);
            }

            List<Corpse> settled = world.Corpses.Where(h => h.Settled).ToList();
            List<Corpse> onCap = settled.Where(h => h.CreatureId % 2 == 0).ToList();
            List<Corpse> onBed = settled.Where(h => h.CreatureId % 2 != 0).ToList();

            _output.WriteLine(
                $"cap top {capTop:0.00} m at ({capX:0.0}, {capZ:0.0}), open water at ({openX:0.0}, {openZ:0.0}); " +
                $"{onCap.Count} corpses settled over the cap and {onBed.Count} over open water");

            Assert.NotEmpty(onCap);
            Assert.NotEmpty(onBed);
            Assert.All(onCap, h => Assert.Equal(capTop, h.Position.Y));
            Assert.All(onBed, h => Assert.Equal(
                world.Bed != null ? (float)world.Bed.FloorY(h.Position.X, h.Position.Z) : -config.WorldDepthMetres,
                h.Position.Y));
            AssertBooksClose(world);
        }

        [Fact]
        public void ACorpseOverATiltedBedSettlesOnTheBedBelowTheMeanDepth()
        {
            // D127 on a tilted bed. The world's depth is the bed's mean, so the deep side lies
            // below it; round 51's smoke held 621 corpses at exactly -45 m over a bed down to -93 m,
            // unsettled, because the drift clamped at the depth before the settle test. Bodies are
            // put where the farm would report them, half a metre under the surface over a column
            // whose bed is below the depth, and every corpse there must come to rest on that bed.
            RunConfig config = ReefTank.Config(cover: 0f);
            config.BedTiltMetres = 8f;
            config.Light = new LightModel(1e-6f, 1f);
            config.MinimumPopulation = 20;
            config.RemineralisationPerSecond = 0f;
            config.CorpseDecayPerSecond = 0.001f;
            config.CorpseSinkMetresPerSecond = 1f;
            config.CorpseSettles = true;
            config.CorpseLeavesBelowFraction = 0f;

            var world = new World(config, seed: 4);
            Assert.NotNull(world.Bed);

            // The deepest point on a ring round the tank's axis, well inside the glass.
            float c = ReefTank.Radius, deepX = c, deepZ = c;
            double deepY = world.Bed.FloorY(c, c);
            for (int k = 0; k < 360; k++)
            {
                float x = c + 0.7f * c * (float)Math.Cos(k * Math.PI / 180d);
                float z = c + 0.7f * c * (float)Math.Sin(k * Math.PI / 180d);
                double y = world.Bed.FloorY(x, z);
                if (y < deepY)
                {
                    deepY = y;
                    deepX = x;
                    deepZ = z;
                }
            }

            Assert.True(deepY < -config.WorldDepthMetres - 1d, $"the bed's deepest point on the ring is {deepY:0.00} m");

            for (int i = 0; i < 400; i++)
            {
                foreach (Organism body in world.Living) world.Observe(body, new Float3(deepX, -0.5f, deepZ), 0f);
                world.Step(0.5f);
            }

            List<Corpse> corpses = world.Corpses.ToList();
            List<Corpse> settled = corpses.Where(h => h.Settled).ToList();

            _output.WriteLine(
                $"bed {deepY:0.00} m at ({deepX:0.0}, {deepZ:0.0}) under a depth of {config.WorldDepthMetres} m; " +
                $"{settled.Count} of {corpses.Count} corpses settled");

            Assert.NotEmpty(settled);

            // Every corpse older than the fall has landed: none hangs at the depth in open water.
            float fall = (config.WorldDepthMetres + 8f) / config.CorpseSinkMetresPerSecond + 1f;
            Assert.All(corpses.Where(h => h.AgeSeconds > fall), h => Assert.True(h.Settled, $"{h} is still falling at {h.Position.Y} m"));
            Assert.All(settled, h => Assert.Equal((float)world.Bed.FloorY(h.Position.X, h.Position.Z), h.Position.Y));
            Assert.All(settled, h => Assert.True(h.Position.Y < -config.WorldDepthMetres));
            AssertBooksClose(world);
        }

        // ------------------------------------------------------------------ the free first link

        private static Phenotype JointedAdult(RunConfig config, out int link)
        {
            for (ulong seed = 1; seed < 500; seed++)
            {
                Genome g = GenomeFactory.Founder(new Rng(seed), config.Genome, null, config.CellTypes);
                Phenotype adult = Developer.Develop(g, config.Development, null, config.Shapes);
                link = Metabolism.FreeLinkIndex(adult);
                if (link > 0) return adult;
            }

            throw new InvalidOperationException("no founder in 500 seeds carries a joint");
        }

        [Fact]
        public void TheFirstLinkHoldsNoEnergyUpToItsAllowanceAndAScaledBodyStaysLinear()
        {
            var priced = new RunConfig();
            Phenotype adult = JointedAdult(priced, out int link);
            PhenotypePart part = adult.Parts[link];
            double linkJoules = (double)part.Volume *
                                priced.CellTypes.Resolve(part.CellTypeId).TissueEnergyPerCubicMetre;

            double full = Metabolism.TissueJoules(adult, priced);

            var whole = new RunConfig { FreeFirstLinkCubicMetres = 1e6f };
            Fixtures.AssertClose(full - linkJoules, Metabolism.TissueJoules(adult, whole), full * 1e-9);

            var half = new RunConfig { FreeFirstLinkCubicMetres = part.Volume * 0.5f };
            Fixtures.AssertClose(full - 0.5 * linkJoules, Metabolism.TissueJoules(adult, half), full * 1e-6);

            // Born at half the length, an eighth of the tissue, the allowance scaled with it.
            Phenotype small = adult.Scaled(0.5f, priced.Shapes);
            Fixtures.AssertClose(
                0.125 * Metabolism.TissueJoules(adult, half), Metabolism.TissueJoules(small, half),
                full * 1e-5);

            // And the standing cost falls with it.
            Assert.True(Metabolism.StandingWatts(adult, whole) < Metabolism.StandingWatts(adult, priced));
        }

        // ------------------------------------------------------------------ the lifespan gene

        [Fact]
        public void TheLifespanRoundTripsAndAFormatNineGenomeIsRefused()
        {
            Genome g = Fixtures.SingleLeaf();
            g.LifespanScale = 1.5f;
            string text = GenomeJson.Write(g);

            Assert.Contains("\"lifespan\":1.5", text);
            Assert.Equal(1.5f, GenomeJson.Read(text).LifespanScale);
            Assert.Equal(text, GenomeJson.Write(GenomeJson.Read(text)));

            string old = text.Replace("\"format\":" + GenomeJson.FormatVersion, "\"format\":9");
            FormatException e = Assert.Throws<FormatException>(() => GenomeJson.Read(old));
            Assert.Contains("Format 10", e.Message);

            g.LifespanScale = 0f;
            Assert.NotEmpty(g.Validate());
        }

        [Fact]
        public void TheLifespanMutatesOnlyWhenItsChanceIsAboveZero()
        {
            Genome parent = Fixtures.SingleLeaf();
            MutationRates off = MutationRates.Default;
            MutationRates on = MutationRates.Default;
            on.LifespanChance = 1f;

            int moved = 0;
            for (ulong seed = 1; seed <= 50; seed++)
            {
                Assert.Equal(1f, Mutator.Mutate(parent, new Rng(seed), off).LifespanScale);
                if (Mutator.Mutate(parent, new Rng(seed), on).LifespanScale != 1f) moved++;
            }

            Assert.True(moved >= 45, "moved " + moved + " of 50");
        }

        [Fact]
        public void ALongerLifeCostsRepairAndALifespanOfOneCostsNothing()
        {
            var config = new RunConfig { AgeRepairExponent = 1f };
            var none = new RunConfig();

            Genome g = Fixtures.SingleLeaf();
            Phenotype one = Developer.Develop(g, config.Development, null, config.Shapes);
            Assert.Equal(Metabolism.StandingWatts(one, none), Metabolism.StandingWatts(one, config));

            g.LifespanScale = 2f;
            Phenotype two = Developer.Develop(g, config.Development, null, config.Shapes);
            Assert.True(Metabolism.StandingWatts(two, config) > Metabolism.StandingWatts(two, none) * 1.5f);
        }

        // ------------------------------------------------------------------ the checkpoint

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

        private static World Restored(RunConfig config, ulong seed, byte[] state)
        {
            var world = new World(config, seed);
            using (var buffer = new MemoryStream(state, writable: false))
            using (var r = new BinaryReader(buffer, Encoding.UTF8))
            {
                world.ReadState(r);
                Assert.Equal(state.Length, buffer.Position);
            }

            return world;
        }

        private static RunConfig Aged()
        {
            RunConfig config = Stage(ageBase: 40f, birth: 1f, wound: 1f);
            config.CorpseDecayPerSecond = 0.01f;
            config.CorpseSinkMetresPerSecond = 0.5f;
            config.CorpseSettles = true;
            config.CorpseLeavesBelowFraction = 0.05f;
            config.FreeFirstLinkCubicMetres = 0.01f;
            return config;
        }

        [Fact]
        public void ACheckpointCarriesTheClocksTheCorpsesAndTheCount()
        {
            var world = new World(Aged(), seed: 11);
            world.Inoculate(Fixtures.SingleLeaf(), count: 4, heightY: -5f);
            for (int i = 0; i < 240; i++) world.Step(0.5f);

            Assert.True(world.DeathsByAge > 0, "nobody died of age");
            Assert.Contains(world.Corpses, c => c.Settled);
            Assert.All(world.Living, c => Assert.True(c.AgeClock > 0d && c.AgeHazard > 0d));

            // A pose, as the farm stamps one, carried and put back.
            Corpse first = world.Corpses.First(c => c.ScaledBy > 0f);
            first.SetPose(new Quat(0.5f, 0.5f, 0.5f, 0.5f));

            byte[] state = StateOf(world);
            World restored = Restored(Aged(), 11, state);
            Assert.Equal(state, StateOf(restored));
            Assert.Equal(world.DeathsByAge, restored.DeathsByAge);

            Corpse back = restored.Corpses.First(c => c.CreatureId == first.CreatureId);
            Assert.True(back.HasPose);
            Assert.Equal(first.Rotation, back.Rotation);
            Assert.Equal(first.ScaledBy, back.ScaledBy);
            Assert.Equal(first.FirstJoules, back.FirstJoules);
            Assert.Equal(first.Settled, back.Settled);

            for (int i = 0; i < 240; i++)
            {
                world.Step(0.5f);
                restored.Step(0.5f);
            }

            Assert.Equal(StateOf(world), StateOf(restored));
            AssertBooksClose(world);
        }
    }
}
