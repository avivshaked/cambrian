using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D124, the owner's ruling for round 49 (2026-09-25): a founder starts with at most
    /// <see cref="RunConfig.FounderReserveCapFraction"/> of its own breeding gate, purse and
    /// endowment together, after its growth, so that it has to earn the rest.
    /// </summary>
    public class FounderReserveCapTests
    {
        private readonly ITestOutputHelper _output;

        public FounderReserveCapTests(ITestOutputHelper output) => _output = output;

        private const float MetabolicStep = 0.5f;
        private const float Endowment = 600f;

        /// <summary>A hash of the right shape; Core never recomputes it (the farm does).</summary>
        private static readonly string AnyHash = new string('c', 64);

        /// <summary>The one-part stomach round 48's pool sent in, genome format 9.</summary>
        private static Genome Stomach() =>
            GenomeJson.Read(File.ReadAllText(
                Path.Combine(AppContext.BaseDirectory, "fixtures", "pool", "r45s1-stomach-100.json")));

        /// <summary>
        /// FoundingTrickleTests' small world at round 48's prices (a 50 J overhead floor, twice the
        /// child's tissue above it, a 600 s endowment), with the stomach as the trickle's pool at
        /// <paramref name="share"/>.
        /// </summary>
        private static RunConfig Config(float cap, float share = 0f)
        {
            var config = new RunConfig
            {
                Light = new LightModel(120f, 12f),
                MinimumPopulation = 20,
                MaximumPopulation = 100_000,
                FloorClosesAfterSeconds = 50f,
                FoundingTricklePerSecond = 0.4f,
                FounderEndowmentSeconds = Endowment,
                PerOffspringOverheadJoules = 50f,
                PerOffspringOverheadPerTissueJoule = 2f,
                FounderReserveCapFraction = cap,
            };

            if (share > 0f)
            {
                config.FoundingTricklePoolCount = 1;
                config.FoundingTricklePoolHash = AnyHash;
                config.FoundingTricklePoolShare = share;
            }

            return config;
        }

        private static World WorldOf(RunConfig config, ulong seed) =>
            config.FoundingTricklePoolCount > 0
                ? new World(config, seed, new[] { Stomach() })
                : new World(config, seed);

        [Fact]
        public void TheCapDefaultsToTheRecordedWorld()
        {
            Assert.Equal(0f, new RunConfig().FounderReserveCapFraction);
            Assert.Throws<ArgumentOutOfRangeException>(() => new RunConfig { FounderReserveCapFraction = -0.1f });
            Assert.Throws<ArgumentOutOfRangeException>(() => new RunConfig { FounderReserveCapFraction = float.NaN });
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new RunConfig { FounderReserveCapFraction = float.PositiveInfinity });
        }

        /// <summary>
        /// A cap no founder reaches computes the cap for every founder and cuts nothing, so its
        /// world is the world with the cap off, row for row and joule for joule: the cap's
        /// arithmetic moves nothing it does not cut.
        /// </summary>
        [Fact]
        public void ACapNoFounderReachesChangesNothing()
        {
            const ulong Seed = 7UL;
            var off = WorldOf(Config(0f, share: 0.5f), Seed);
            var high = WorldOf(Config(1000f, share: 0.5f), Seed);

            for (int step = 0; step < 800; step++)
            {
                off.Step(MetabolicStep);
                high.Step(MetabolicStep);

                List<string> a = Rows(off), b = Rows(high);
                Assert.Equal(a, b);
            }

            Assert.Equal(off.Living.Count, high.Living.Count);
            for (int i = 0; i < off.Living.Count; i++) Assert.Equal(off.Living[i].Energy, high.Living[i].Energy);

            Assert.Equal(0L, high.FoundersCapped);
            Assert.Equal(0d, high.FounderJoulesCapped);
            _output.WriteLine($"{off.Births} births, {off.PoolSpawns} pool founders, {off.Living.Count} alive in both");
        }

        /// <summary>
        /// Every founder, as it stands before its first metabolic step, holds the lesser of the
        /// recorded start (purse and endowment) and its cap; its lineage row carries the cut and
        /// the endowment after it; and the pool's stomach, whose start round 48 put above its own
        /// gate, is cut.
        /// </summary>
        [Fact]
        public void EveryFounderStartsAtTheLesserOfItsStartAndItsCap()
        {
            const float Fraction = 0.9f;
            RunConfig config = Config(Fraction, share: 0.5f);
            var world = WorldOf(config, 11UL);

            int founders = 0, cut = 0, poolCut = 0;
            double largest = 0d;

            for (int step = 0; step < 800; step++)
            {
                world.Step(MetabolicStep);

                var living = new Dictionary<long, Organism>();
                foreach (Organism o in world.Living) living[o.Id] = o;

                foreach (LineageEvent e in world.DrainLineageEvents())
                {
                    if (e.Kind != LineageEventKind.Birth || e.Source == FounderSource.None) continue;
                    if (!living.TryGetValue(e.Id, out Organism f)) continue;

                    founders++;

                    double endowment = (double)Endowment * Metabolism.StandingWatts(f.Phenotype, config);
                    double uncapped = config.FounderEnergyJoules * e.BirthFraction + endowment;
                    double cap = world.FounderStartCap(
                        f.Genome, f.AdultPhenotype, Metabolism.TissueJoules(f.AdultPhenotype, config), f.TissueJoules);
                    double expected = Math.Min(uncapped, cap);
                    double cutHere = Math.Max(0d, uncapped - cap);

                    Assert.Equal(expected, f.Energy, 9);
                    Assert.Equal(cutHere, e.CapCutJoules, 9);
                    Assert.Equal(Math.Max(0d, endowment - cutHere), e.EndowmentJoules, 9);

                    if (cutHere > 0d)
                    {
                        cut++;
                        if (e.Source == FounderSource.Pool) poolCut++;
                        Assert.Contains("\"capcut\":", e.ToJson());
                        largest = Math.Max(largest, cutHere);

                        // The cap itself: after its growth the founder holds f of its adult gate.
                        double gate = Organism.BreedingGate(
                            f.Genome.Reproduction, Metabolism.TissueJoules(f.AdultPhenotype, config),
                            Metabolism.StandingWatts(f.AdultPhenotype, config), config);
                        double growth = Metabolism.TissueJoules(f.AdultPhenotype, config) - f.TissueJoules;
                        Assert.Equal(Fraction * gate + growth, cap, 9);

                        if (e.Source == FounderSource.Pool && poolCut == 1)
                        {
                            _output.WriteLine(
                                $"pool stomach {e.Id}: bf {e.BirthFraction:0.####}, purse {config.FounderEnergyJoules * e.BirthFraction:0.##} J, " +
                                $"endowment {endowment:0.##} J, growth {growth:0.##} J, adult gate {gate:0.##} J, cap {cap:0.##} J, " +
                                $"cut {cutHere:0.##} J, start {f.Energy:0.##} J");
                        }
                    }
                    else
                    {
                        Assert.DoesNotContain("\"capcut\"", e.ToJson());
                    }
                }
            }

            _output.WriteLine(
                $"{founders} founders seen, {cut} cut ({poolCut} from the pool), largest cut {largest:0.##} J; " +
                $"world counts {world.FoundersCapped} and {world.FounderJoulesCapped:0.##} J");

            Assert.True(poolCut > 0, "the pool's stomach was never cut, so the test says nothing about it");
            Assert.True(world.FoundersCapped >= cut);
        }

        /// <summary>
        /// Round 48's failure, in the small world: with the cap off, the pool's stomach breeds
        /// within a second of landing, on the gift; at 0.9 no pool founder does, because the last
        /// tenth of its gate cannot be earned in a second.
        /// </summary>
        [Fact]
        public void APoolStomachUnderTheCapHasNoChildOnItsGift()
        {
            int Quick(float cap)
            {
                var world = WorldOf(Config(cap, share: 1f), 5UL);
                var poolBorn = new Dictionary<long, double>();
                int quick = 0;

                for (int step = 0; step < 1_200; step++)
                {
                    world.Step(MetabolicStep);

                    foreach (LineageEvent e in world.DrainLineageEvents())
                    {
                        if (e.Kind != LineageEventKind.Birth) continue;
                        if (e.Source == FounderSource.Pool) poolBorn[e.Id] = e.ElapsedSeconds;
                        else if (e.Source == FounderSource.None &&
                                 poolBorn.TryGetValue(e.ParentId, out double born) &&
                                 e.ElapsedSeconds - born <= 1.0 + 1e-9)
                        {
                            quick++;
                        }
                    }
                }

                _output.WriteLine($"cap {cap}: {world.PoolSpawns} pool founders, {quick} bred within 1 s of landing");
                return quick;
            }

            Assert.True(Quick(0f) > 0, "with the cap off no pool stomach bred at landing, so the premise is not round 48's");
            Assert.Equal(0, Quick(0.9f));
        }

        /// <summary>
        /// Both books close over a grid world with the floor and the trickle both founding under
        /// the cap: what the cap cuts was never created, so neither book sees it.
        /// </summary>
        [Fact]
        public void BothBooksCloseWithTheCapOn()
        {
            var config = new RunConfig
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
                FloorClosesAfterSeconds = 500f,
                FoundingTricklePerSecond = 0.2f,
                FounderEndowmentSeconds = Endowment,
                PerOffspringOverheadJoules = 50f,
                PerOffspringOverheadPerTissueJoule = 2f,
                FounderReserveCapFraction = 0.9f,
            };

            var world = new World(config, seed: 3);
            for (int step = 0; step < 6_000; step++) world.Step(MetabolicStep);

            double identity = world.MatterResidual;

            _output.WriteLine(
                $"alive {world.Living.Count}, births {world.Births}, floor {world.FloorSpawns}, trickle " +
                $"{world.TrickleSpawns}, capped {world.FoundersCapped} for {world.FounderJoulesCapped:0.##} J; " +
                $"audit {world.AuditResidual:R} of {world.EnergyIn:0} J in; matter identity {identity:R}");

            Assert.True(world.FoundersCapped > 0, "the cap cut no founder, so the books were not tested under it");
            Assert.True(
                Math.Abs(world.AuditResidual) <= 1e-6 * Math.Max(1d, world.EnergyIn),
                $"audit residual {world.AuditResidual:R}");
            Assert.True(
                Math.Abs(identity) <= 1e-6 * (world.MatterInitialTotal + world.MatterInfluxedTotal),
                $"matter identity {identity:R}");
        }

        /// <summary>
        /// A capped world round-trips through its state: the two counters and a queued row's cut
        /// are carried, and the restored world carries on as the original. A world with the cap
        /// off writes the bytes it wrote before the cap existed (the same length as one whose cap
        /// is on, less the two counters and a double on every queued row).
        /// </summary>
        [Fact]
        public void ACappedWorldRoundTripsThroughItsState()
        {
            RunConfig config = Config(0.9f, share: 0.5f);
            const ulong Seed = 13UL;

            var original = WorldOf(config, Seed);
            for (int i = 0; i < 2_000 && original.FoundersCapped < 2; i++) original.Step(MetabolicStep);
            Assert.True(original.FoundersCapped >= 2, "the cap cut fewer than two founders");

            byte[] state = StateOf(original);
            var restored = WorldOf(config, Seed);

            using (var buffer = new MemoryStream(state, writable: false))
            using (var r = new BinaryReader(buffer, Encoding.UTF8))
            {
                restored.ReadState(r);
                Assert.Equal(state.Length, buffer.Position);
            }

            Assert.Equal(original.FoundersCapped, restored.FoundersCapped);
            Assert.Equal(original.FounderJoulesCapped, restored.FounderJoulesCapped);

            var after = new List<string>();
            var again = new List<string>();
            for (int i = 0; i < 300; i++)
            {
                original.Step(MetabolicStep);
                restored.Step(MetabolicStep);
                after.AddRange(Rows(original));
                again.AddRange(Rows(restored));
            }

            Assert.Equal(after, again);
            Assert.Equal(original.FoundersCapped, restored.FoundersCapped);

            // The off world's layout: stepped the same way, its state carries neither counter.
            var off = WorldOf(Config(0f, share: 0.5f), Seed);
            var high = WorldOf(Config(1000f, share: 0.5f), Seed);
            for (int i = 0; i < 20; i++)
            {
                off.Step(MetabolicStep);
                high.Step(MetabolicStep);
            }

            int queued = CountQueued(StateOf(off), off);
            Assert.Equal(
                StateOf(off).Length + sizeof(long) + sizeof(double) + queued * sizeof(double),
                StateOf(high).Length);
            _output.WriteLine($"{after.Count} rows after the restore; {queued} rows queued in the layout check");
        }

        /// <summary>How many lineage rows a world has queued: drained from a restored copy of its state.</summary>
        private static int CountQueued(byte[] state, World like)
        {
            var copy = new World(like.Config, like.Seed, like.TricklePool);
            using (var buffer = new MemoryStream(state, writable: false))
            using (var r = new BinaryReader(buffer, Encoding.UTF8))
            {
                copy.ReadState(r);
            }

            int n = 0;
            foreach (LineageEvent _ in copy.DrainLineageEvents()) n++;
            return n;
        }

        private static List<string> Rows(World world)
        {
            var rows = new List<string>();
            foreach (LineageEvent e in world.DrainLineageEvents()) rows.Add(e.ToJson());
            return rows;
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
    }
}
