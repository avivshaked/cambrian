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
    /// Reproduction paid as it goes, and an overhead that scales with the child — the owner's
    /// ruling of 2026-09-24 on round 47's dissection.
    /// </summary>
    /// <remarks>
    /// <b>Both books are the subject</b>, as they were for growth: the gestation account is a
    /// second store of charged matter inside a body, so the way this fails is a joule or a unit
    /// that appears or vanishes when the account fills, pays for a child or goes to a corpse. The
    /// closure test below runs every leg at once; the others say what each leg is supposed to be.
    /// </remarks>
    public class GestationTests
    {
        private readonly ITestOutputHelper _output;

        public GestationTests(ITestOutputHelper output) => _output = output;

        /// <summary>One unjointed leaf box that pays for its children as stated.</summary>
        private static Genome Leaf(
            ReproductionMode mode, float share, float investment = 0.5f, int brood = 1,
            float halfExtent = 0.3f)
        {
            var genome = new Genome
            {
                RootIndex = 0,
                AdultScale = 1f,
                Reproduction = new ReproductionTraits
                {
                    BroodSize = brood,
                    BirthInvestment = investment,
                    Mode = mode,
                    GestationShare = share,
                },
            };

            genome.Nodes.Add(new MorphNode
            {
                CellTypeId = CellTypeIds.Photosynthetic,
                ShapeId = ShapeIds.Box,
                Dimensions = new Float3(halfExtent, halfExtent, halfExtent),
                JointType = JointType.Fixed,
                JointLimits = Array.Empty<Float2>(),
                RecursiveLimit = 1,
                Neurons = Array.Empty<NeuronDef>(),
            });

            return genome;
        }

        /// <summary>An empty stage with no mutation, so a child is its parent's twin.</summary>
        private static RunConfig Stage(float surfaceIrradiance = 200f)
        {
            var config = new RunConfig
            {
                MinimumPopulation = 0,
                MaximumPopulation = 100_000,
                Light = new LightModel(surfaceIrradiance, 12f),
            };

            foreach (var property in typeof(MutationRates).GetProperties())
            {
                if (property.PropertyType == typeof(float) && property.Name.EndsWith("Chance"))
                {
                    property.SetValue(config.Mutation, 0f);
                }
            }

            return config;
        }

        // ---------------------------------------------------------------- the price

        [Fact]
        public void AtAPerTissueFactorOfZeroTheOverheadIsTheFlatFloorBitForBit()
        {
            // Every recorded config carries the factor at 0 once it is read by this build, and
            // its price must be the price it was recorded at to the last bit, or no world replays.
            var config = new RunConfig { PerOffspringOverheadJoules = 100f };
            var rng = new Rng(5);

            for (int i = 0; i < 1000; i++)
            {
                double childTissue = rng.Range(0f, 5000f);
                Assert.Equal((double)100f, config.OverheadFor(childTissue));

                var traits = new ReproductionTraits
                {
                    BroodSize = 1 + rng.Range(8),
                    BirthInvestment = rng.Range(0.05f, 1f),
                };
                double parentTissue = rng.Range(0f, 5000f);

                Assert.Equal(
                    traits.CostJoules(parentTissue, config.PerOffspringOverheadJoules),
                    traits.CostJoules(parentTissue, config));
            }
        }

        [Fact]
        public void TheOverheadIsTheLargerOfTheFloorAndTheFactorTimesTheChild()
        {
            var config = new RunConfig
            {
                PerOffspringOverheadJoules = 10f,
                PerOffspringOverheadPerTissueJoule = 2f,
            };

            Assert.Equal(10d, config.OverheadFor(1d));
            Assert.Equal(10d, config.OverheadFor(5d));
            Assert.Equal(24d, config.OverheadFor(12d));

            // The gate's estimate takes each child at its share less the newborn reserve, the most
            // a child can be born with, so it is never below the price a conception charges.
            var traits = new ReproductionTraits { BroodSize = 2, BirthInvestment = 0.5f };
            double parentTissue = 400d;
            double childAtMost = 0.5 * parentTissue / 2 * (1d - config.NewbornReserveFraction);

            Assert.Equal(
                0.5 * parentTissue + 2 * Math.Max(10d, 2d * childAtMost),
                traits.CostJoules(parentTissue, config), 9);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => config.PerOffspringOverheadPerTissueJoule = -1f);
        }

        // ---------------------------------------------------------------- paying as it goes

        /// <summary>
        /// Steps a world holding one gestating leaf until its first child, and returns what the
        /// birth step moved: the account before, the joules banked on the step, the account after,
        /// and the parent's reserve before.
        /// </summary>
        private (World world, Organism parent, Organism child, double accountBefore,
            double banked, double reserveBefore) FirstGestationBirth(RunConfig config, Genome genome)
        {
            var world = new World(config, seed: 3);
            world.Inoculate(genome, count: 1, heightY: -1f);
            Organism parent = world.Living[0];

            for (int step = 0; step < 20_000; step++)
            {
                double accountBefore = parent.GestationJoules;
                double reserveBefore = parent.Energy;
                double gestatedBefore = world.GestatedTotal;

                world.Step(1f);

                if (world.Births > 0)
                {
                    Assert.Equal(2, world.Living.Count);
                    return (world, parent, world.Living[1], accountBefore,
                        world.GestatedTotal - gestatedBefore, reserveBefore);
                }
            }

            throw new Xunit.Sdk.XunitException("the gestating parent never bred");
        }

        [Fact]
        public void AGestatingParentBreedsFromItsAccountWhenItsReserveIsBelowThePrice()
        {
            // Share 1: every joule the leaf clears goes to the account, so its reserve never
            // climbs above what it was born with and can never hold a lump's price. It breeds
            // anyway, and the account pays exactly the child's body, its first reserve and the
            // overhead, while the reserve pays nothing.
            RunConfig config = Stage();
            Genome genome = Leaf(ReproductionMode.Gestation, share: 1f);

            var (world, parent, child, accountBefore, banked, reserveBefore) =
                FirstGestationBirth(config, genome);

            double lumpGate = parent.ReproductionThreshold(config);
            double spent = accountBefore + banked - parent.GestationJoules;
            double price = child.TissueJoules + child.Energy + config.PerOffspringOverheadJoules;

            _output.WriteLine(
                $"t={world.ElapsedSeconds:0} s: reserve {reserveBefore:0.###} J against a lump gate " +
                $"of {lumpGate:0.###} J; account {accountBefore:0.###} J + {banked:0.###} J banked, " +
                $"paid {spent:0.###} J for a child of {child.TissueJoules:0.###} J tissue and " +
                $"{child.Energy:0.###} J reserve");

            Assert.True(reserveBefore < lumpGate, "the reserve could have paid a lump, so this proves nothing");
            Assert.Equal(1L, world.GestationBirths);
            Assert.Equal(1L, world.Births);
            Fixtures.AssertClose(price, spent, price * 1e-9);
            Assert.True(parent.GestationJoules >= 0d);

            // The reserve did not pay: after the step it is what the step left, never less than
            // the price below what it began at.
            Assert.True(parent.Energy > 0d);
            Assert.True(parent.Energy > reserveBefore - price * 0.5);

            Assert.True(Math.Abs(world.AuditResidual) <= 1e-9 * Math.Max(1d, world.EnergyIn),
                $"audit residual {world.AuditResidual:R}");
            Assert.True(Math.Abs(world.MatterResidual) <= 1e-9 * Math.Max(1d, world.StandingMatterUnits),
                $"matter residual {world.MatterResidual:R}");
        }

        [Fact]
        public void TheLumpTwinOfTheSameLeafNeverBreedsWhereTheGestatingOneDoes()
        {
            // The ruling's reason, as a pair: the same body and the same light, one paying in a
            // lump and one as it goes. A reserve cap stands in for round 47's senescence window:
            // it holds the lump's reserve under the price for ever, and the gestating twin, whose
            // account the cap does not touch, saves the price and breeds.
            RunConfig Make()
            {
                RunConfig c = Stage();
                c.PerOffspringOverheadJoules = 5000f;
                c.ReserveCapSeconds = 600f;
                return c;
            }

            RunConfig lumpConfig = Make();
            RunConfig gestationConfig = Make();

            var lump = new World(lumpConfig, seed: 3);
            lump.Inoculate(Leaf(ReproductionMode.Lump, share: 1f), count: 1, heightY: -1f);

            var gestating = new World(gestationConfig, seed: 3);
            gestating.Inoculate(Leaf(ReproductionMode.Gestation, share: 1f), count: 1, heightY: -1f);

            for (int step = 0; step < 60_000 && gestating.Births == 0; step++)
            {
                lump.Step(1f);
                gestating.Step(1f);
            }

            _output.WriteLine(
                $"t={gestating.ElapsedSeconds:0} s: gestating births {gestating.Births}, " +
                $"lump births {lump.Births}, lump reserve {lump.Living[0].Energy:0.#} J");

            Assert.Equal(1L, gestating.GestationBirths);
            Assert.Equal(0L, lump.Births);
            Assert.Equal(0L, lump.GestationBirths);
            Assert.True(lump.Living[0].Energy < lump.Living[0].ReproductionThreshold(lumpConfig),
                "the lump twin could pay, so the cap did not bind");
            Assert.True(lump.ReserveTrimmedTotal > 0d, "the cap never bit on the lump twin");
        }

        [Fact]
        public void AScaledOverheadIsChargedOnTheChildsTissueAtBirth()
        {
            RunConfig config = Stage();
            config.PerOffspringOverheadJoules = 1f;
            config.PerOffspringOverheadPerTissueJoule = 3f;

            var (world, parent, child, accountBefore, banked, _) =
                FirstGestationBirth(config, Leaf(ReproductionMode.Gestation, share: 0.8f));

            double spent = accountBefore + banked - parent.GestationJoules;
            double overhead = (float)Math.Max(1d, 3d * child.TissueJoules);
            double price = child.TissueJoules + child.Energy + overhead;

            _output.WriteLine(
                $"child tissue {child.TissueJoules:0.###} J: overhead {overhead:0.###} J, " +
                $"paid {spent:0.###} J against {price:0.###} J");

            Assert.True(overhead > 1d, "the floor bound, so the factor was not tested");
            Fixtures.AssertClose(price, spent, price * 1e-9);
            Assert.True(Math.Abs(world.AuditResidual) <= 1e-9 * Math.Max(1d, world.EnergyIn));
        }

        [Fact]
        public void UpkeepNeverDrawsOnTheAccountAndABodyStarvesHoldingIt()
        {
            // Senescence raises the upkeep until every step's net is negative: from then on
            // nothing is banked, the reserve alone pays, and the body starves with its account
            // untouched. The account never falls on any step the body is alive.
            RunConfig config = Stage();
            config.PerOffspringOverheadJoules = 1e9f;   // it banks and never breeds
            config.SenescenceDoublingSeconds = 150f;
            config.CorpseDecayPerSecond = 1e-6f;

            var world = new World(config, seed: 3);
            world.Inoculate(Leaf(ReproductionMode.Gestation, share: 0.5f), count: 1, heightY: -1f);
            Organism body = world.Living[0];

            double last = 0d;
            int step = 0;
            for (; step < 100_000 && world.Living.Count > 0; step++)
            {
                last = body.GestationJoules;
                world.Step(1f);
                if (world.Living.Count > 0) Assert.True(body.GestationJoules >= last);
            }

            _output.WriteLine($"starved at {step} s holding {last:0.###} J in its account");

            Assert.Empty(world.Living);
            Assert.True(last > 0d, "nothing was ever banked");
            Assert.Single(world.Corpses);
            Assert.True(world.Corpses[0].Joules >= last);
            Assert.True(Math.Abs(world.AuditResidual) <= 1e-9 * Math.Max(1d, world.EnergyIn));
        }

        // ---------------------------------------------------------------- death

        [Fact]
        public void DeathSendsTheAccountToTheCorpseWithTheReserveAndTheTissue()
        {
            RunConfig config = Stage();
            config.PerOffspringOverheadJoules = 1e9f;   // it banks and never breeds
            config.CorpseDecayPerSecond = 1e-6f;        // so the corpse is an object to read

            var world = new World(config, seed: 3);
            world.Inoculate(Leaf(ReproductionMode.Gestation, share: 0.5f), count: 1, heightY: -1f);
            Organism body = world.Living[0];

            for (int step = 0; step < 600; step++) world.Step(1f);

            double account = body.GestationJoules;
            double worth = body.TissueJoules + Math.Max(0d, body.Energy) + account;
            double standingBefore = world.StandingJoules;

            Assert.True(account > 0d, "nothing was banked, so the corpse would prove nothing");

            world.KillDiverged(body);

            Assert.Empty(world.Living);
            Assert.Single(world.Corpses);
            Assert.Equal(worth, world.Corpses[0].Joules);
            Assert.Equal(0d, body.GestationJoules);

            // A death moves joules between accounts and creates none.
            Fixtures.AssertClose(standingBefore, world.StandingJoules, standingBefore * 1e-12);
            Assert.True(Math.Abs(world.AuditResidual) <= 1e-9 * Math.Max(1d, world.EnergyIn));
        }

        [Fact]
        public void TheAccountIsStandingInBothBooksAndInTheBodiesTotal()
        {
            RunConfig config = Stage();
            config.PerOffspringOverheadJoules = 1e9f;

            var world = new World(config, seed: 3);
            world.Inoculate(Leaf(ReproductionMode.Gestation, share: 0.5f), count: 1, heightY: -1f);
            Organism body = world.Living[0];

            for (int step = 0; step < 300; step++) world.Step(1f);

            Assert.True(body.GestationJoules > 0d);
            Assert.Equal(
                body.Energy + body.TissueJoules + body.GestationJoules, world.StandingJoulesInBodies);
            Assert.Equal(body.GestationJoules, world.GestationJoulesInBodies);
            Assert.True(world.GestatedTotal >= body.GestationJoules);

            Assert.True(Math.Abs(world.AuditResidual) <= 1e-9 * Math.Max(1d, world.EnergyIn));
            Assert.True(Math.Abs(world.MatterResidual) <= 1e-9 * Math.Max(1d, world.StandingMatterUnits));
        }

        // ---------------------------------------------------------------- the books, whole

        [Fact]
        public void AGridWorldWithGestatingLineagesClosesItsAuditAndItsMatterIdentity()
        {
            // GrowthTests' closure test with the gene switched on: founders are lump breeders and
            // half their children flip, so both kinds of parent, the account filling, children
            // paid from it, deaths with a full account, corpses, an influx, burial and a rolling
            // current all run at once, and the overhead scales with the child above its floor.
            const float Area = 144f;
            const int Patches = 4;
            const float Depth = 24f;

            var config = new RunConfig
            {
                Light = new LightModel(100f, 12f),
                SharedSpace = true,
                FieldModel = MatterField.Grid,
                WorldAreaSquareMetres = Area,
                HorizontalPatches = Patches,
                WorldDepthMetres = Depth,
                FloorClosesAfterSeconds = 1000f,
                MatterInfluxPerSecond = 0.05f,
                MatterBurialPerSecond = 0.001f,
                CorpseDecayPerSecond = 0.01f,
                ReserveCapSeconds = 600f,
                NutrientMixingDiffusivity = 0.2f,
                HorizontalMixingDiffusivity = 0.2f,
                MatterMixingDiffusivity = 0.2f,
                PerOffspringOverheadJoules = 10f,
                PerOffspringOverheadPerTissueJoule = 0.5f,
                Current = new CurrentField
                {
                    Speed = 0.1f, CellMetres = 10f, PeriodSeconds = 600f, Rolls = true,
                    AdvectFields = true, RollBlinkSeconds = 300f, VentDepthMetres = Depth,
                },
            };

            config.Mutation.GestationModeChance = 0.5f;
            config.Mutation.GestationShareChance = 0.08f;

            var world = new World(config, seed: 3);
            long gestatingRows = 0;
            double worstAudit = 0d, worstMatter = 0d;

            for (int step = 0; step < 3_000; step++)
            {
                world.Step(0.5f);

                foreach (LineageEvent e in world.DrainLineageEvents())
                {
                    if (e.Kind == LineageEventKind.Birth &&
                        e.ReproductionMode == ReproductionMode.Gestation) gestatingRows++;
                }

                if (step % 100 == 99)
                {
                    worstAudit = Math.Max(worstAudit,
                        Math.Abs(world.AuditResidual) / Math.Max(1d, world.EnergyIn));
                    worstMatter = Math.Max(worstMatter,
                        Math.Abs(world.MatterResidual) / world.MatterInitialTotal);
                }
            }

            _output.WriteLine(
                $"alive {world.Living.Count}, births {world.Births} of which gestation " +
                $"{world.GestationBirths}, deaths {world.Deaths}, corpses {world.Corpses.Count}; " +
                $"banked {world.GestatedTotal:0.#} J, held {world.GestationJoulesInBodies:0.#} J; " +
                $"gestating birth rows {gestatingRows}; " +
                $"audit residual {world.AuditResidual:R} of {world.EnergyIn:0} J in (worst {worstAudit:R}); " +
                $"matter identity {world.MatterResidual:R} of {world.MatterInitialTotal:0} (worst {worstMatter:R})");

            Assert.True(world.Births > 0, "nothing was born, so the economy was not exercised");
            Assert.True(gestatingRows > 0, "no gestating lineage was ever born");
            Assert.True(world.GestationBirths > 0, "no child was ever paid for from an account");
            Assert.True(world.GestatedTotal > 0d);

            Assert.True(worstAudit <= 1e-6, $"audit residual reached {worstAudit:R} of the joules in");
            Assert.True(worstMatter <= 1e-6, $"matter identity reached {worstMatter:R} of the stock");
            Assert.True(
                Math.Abs(world.AuditResidual) <= 1e-6 * Math.Max(1d, world.EnergyIn),
                $"audit residual {world.AuditResidual:R}");
            Assert.True(
                Math.Abs(world.MatterResidual) <= 1e-6 * world.MatterInitialTotal,
                $"matter identity {world.MatterResidual:R}");
        }

        // ---------------------------------------------------------------- the gene

        [Fact]
        public void AtItsDefaultsTheGeneDrawsNothingAndEveryFounderIsALumpBreeder()
        {
            var options = new RandomGenomeOptions();
            for (ulong seed = 0; seed < 50; seed++)
            {
                Genome founder = GenomeFactory.Founder(new Rng(seed), options);
                Assert.Equal(ReproductionMode.Lump, founder.Reproduction.Mode);
                Assert.Equal(0.5f, founder.Reproduction.GestationShare);

                // Zero chances: a child's reproduction traits are its parent's plus only the draws
                // the recorded operators made — the mode and the share never move.
                Genome child = Mutator.Mutate(founder, new Rng(seed + 1000));
                Assert.Equal(ReproductionMode.Lump, child.Reproduction.Mode);
                Assert.Equal(0.5f, child.Reproduction.GestationShare);
            }
        }

        [Fact]
        public void TheModeFlipsAndTheShareWalksInsideItsBoundsWhenTheChancesAreOn()
        {
            var rates = new MutationRates { GestationModeChance = 1f, GestationShareChance = 1f };
            Genome parent = Leaf(ReproductionMode.Lump, share: 0.9f);

            int gestating = 0;
            for (ulong seed = 0; seed < 200; seed++)
            {
                Genome child = Mutator.Mutate(parent, new Rng(seed), rates);
                Assert.Equal(ReproductionMode.Gestation, child.Reproduction.Mode);
                Assert.True(child.Reproduction.GestationShare > 0f && child.Reproduction.GestationShare <= 1f);
                gestating++;

                Genome back = Mutator.Mutate(child, new Rng(seed + 7), rates);
                Assert.Equal(ReproductionMode.Lump, back.Reproduction.Mode);
            }

            Assert.Equal(200, gestating);
        }

        [Fact]
        public void AGestatingGenomeWithNoShareIsRefused()
        {
            Genome g = Leaf(ReproductionMode.Gestation, share: 0f);
            Assert.NotEmpty(g.Validate());

            Genome lump = Leaf(ReproductionMode.Lump, share: 0f);
            Assert.Empty(lump.Validate());

            Assert.NotEmpty(Leaf(ReproductionMode.Lump, share: 1.5f).Validate());
        }

        [Fact]
        public void TheModeAndTheShareRoundTripByNameAndAFormatEightGenomeIsRefused()
        {
            Genome g = Leaf(ReproductionMode.Gestation, share: 0.375f);
            string text = GenomeJson.Write(g);

            Assert.Contains("\"mode\":\"Gestation\"", text);
            Assert.Contains("\"gestation\":0.375", text);
            Assert.Contains("\"format\":9", text);

            Genome back = GenomeJson.Read(text);
            Assert.Equal(ReproductionMode.Gestation, back.Reproduction.Mode);
            Assert.Equal(0.375f, back.Reproduction.GestationShare);
            Assert.Equal(text, GenomeJson.Write(back));

            string old = text.Replace("\"format\":9", "\"format\":8");
            FormatException e = Assert.Throws<FormatException>(() => GenomeJson.Read(old));
            _output.WriteLine(e.Message);
            Assert.Contains("Format 9", e.Message);

            // A current-format file missing either field is refused rather than defaulted.
            Assert.ThrowsAny<Exception>(() => GenomeJson.Read(text.Replace("\"mode\":", "\"kind\":")));
            Assert.ThrowsAny<Exception>(() => GenomeJson.Read(text.Replace("\"gestation\":", "\"g\":")));
        }

        [Fact]
        public void TheBirthRowCarriesTheModeAndTheShare()
        {
            RunConfig config = Stage();
            var world = new World(config, seed: 3);
            world.Inoculate(Leaf(ReproductionMode.Gestation, share: 0.625f), count: 1, heightY: -1f);

            LineageEvent? found = null;
            foreach (LineageEvent e in world.DrainLineageEvents())
            {
                if (e.Kind == LineageEventKind.Birth) found = e;
            }

            Assert.True(found.HasValue, "no birth row was queued");
            LineageEvent birth = found.Value;
            Assert.Equal(ReproductionMode.Gestation, birth.ReproductionMode);
            Assert.Equal(0.625f, birth.GestationShare);

            string row = birth.ToJson();
            Assert.Contains("\"gm\":1", row);
            Assert.Contains("\"gs\":0.625", row);
        }

        // ---------------------------------------------------------------- the checkpoint

        [Fact]
        public void ACheckpointCarriesTheAccountAndTheCountersAndGoesOnBeingTheSameWorld()
        {
            RunConfig Make()
            {
                RunConfig c = Stage();
                c.PerOffspringOverheadJoules = 5f;
                return c;
            }

            var world = new World(Make(), seed: 3);
            world.Inoculate(Leaf(ReproductionMode.Gestation, share: 0.7f), count: 2, heightY: -1f);

            for (int step = 0; step < 3000 && world.GestationBirths == 0; step++) world.Step(1f);
            for (int step = 0; step < 50; step++) world.Step(1f);

            Assert.True(world.GestationBirths > 0, "no gestation birth before the checkpoint");
            Assert.Contains(world.Living, c => c.GestationJoules > 0d);

            byte[] state = StateOf(world);
            World restored = Restored(Make(), 3, state);

            Assert.Equal(world.GestationBirths, restored.GestationBirths);
            Assert.Equal(world.GestatedTotal, restored.GestatedTotal);
            Assert.Equal(world.GestationJoulesInBodies, restored.GestationJoulesInBodies);
            Assert.Equal(state, StateOf(restored));

            for (int step = 0; step < 300; step++)
            {
                world.Step(1f);
                restored.Step(1f);
            }

            Assert.Equal(StateOf(world), StateOf(restored));
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
    }
}
