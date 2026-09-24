using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The round 48 founding ruling's endowment (owner, 2026-09-24):
    /// <see cref="RunConfig.FounderEndowmentSeconds"/> seconds of a founder's own standing cost,
    /// on top of the recorded purse, created where the purse is and booked in both books.
    /// </summary>
    public class FounderEndowmentTests
    {
        private readonly ITestOutputHelper _output;

        public FounderEndowmentTests(ITestOutputHelper output) => _output = output;

        private const float MetabolicStep = 0.5f;

        private static RunConfig Cells(float endowment) => new RunConfig
        {
            Light = new LightModel(120f, 12f),
            MinimumPopulation = 20,
            MaximumPopulation = 100_000,
            FounderEndowmentSeconds = endowment,
        };

        [Fact]
        public void TheEndowmentDefaultsToTheRecordedWorld()
        {
            Assert.Equal(0f, new RunConfig().FounderEndowmentSeconds);
            Assert.Throws<ArgumentOutOfRangeException>(() => new RunConfig { FounderEndowmentSeconds = -1f });
            Assert.Throws<ArgumentOutOfRangeException>(() => new RunConfig { FounderEndowmentSeconds = float.NaN });
        }

        /// <summary>
        /// The floor's founders, as they stand before their first metabolic step: the reserve is
        /// the recorded one plus exactly the endowment, and the lineage row says how much.
        /// </summary>
        [Fact]
        public void AFoundersFirstReserveIsTheRecordedOnePlusTheEndowment()
        {
            const float Seconds = 600f;

            var off = new World(Cells(0f), seed: 5);
            var on = new World(Cells(Seconds), seed: 5);

            // Founders arrive at the end of a step (EnforceFloor after Metabolise), so the first
            // step's founders have not yet been billed when it returns.
            off.Step(MetabolicStep);
            on.Step(MetabolicStep);

            Assert.NotEmpty(off.Living);
            Assert.Equal(off.Living.Count, on.Living.Count);

            var endowments = new Dictionary<long, double>();
            foreach (LineageEvent e in on.DrainLineageEvents())
            {
                if (e.Kind == LineageEventKind.Birth) endowments[e.Id] = e.EndowmentJoules;
            }

            foreach (LineageEvent e in off.DrainLineageEvents())
            {
                Assert.Equal(0d, e.EndowmentJoules);
                Assert.DoesNotContain("\"endow\"", e.ToJson());
            }

            for (int i = 0; i < on.Living.Count; i++)
            {
                Organism with = on.Living[i];
                Organism without = off.Living[i];

                Assert.Equal(without.Id, with.Id);

                double expected = (double)Seconds * Metabolism.StandingWatts(with.Phenotype, on.Config);
                Assert.True(expected > 0d, "a founder with no standing cost is endowed with nothing");

                Assert.Equal(without.Energy + expected, with.Energy, 9);
                Assert.Equal(expected, endowments[with.Id], 9);

                _output.WriteLine(
                    $"founder {with.Id}: {without.Energy:0.###} J recorded + {expected:0.###} J endowed");
            }

            // The row carries it only when it is there.
            LineageEvent row = LineageEvent.Birth(
                1d, 1L, -1L, BirthKind.Floor, 0, 0u, false, false, true, 0, 1f, 1f, 0f, 0,
                false, false, false, FounderSource.Floor, -1, endowmentJoules: 12.5d);
            Assert.Contains("\"endow\":12.5", row.ToJson());
        }

        /// <summary>
        /// Both books close over 3,000 s of a grid world whose floor closes at 500 s and whose
        /// trickle then runs, every founder of both doors endowed: the endowment is income both
        /// books see, as the purse is.
        /// </summary>
        [Fact]
        public void BothBooksCloseWithTheEndowmentOn()
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
                FounderEndowmentSeconds = 600f,
            };

            var world = new World(config, seed: 3);

            int floorEndowed = 0, trickleEndowed = 0;
            for (int step = 0; step < 6_000; step++)
            {
                world.Step(MetabolicStep);

                foreach (LineageEvent e in world.DrainLineageEvents())
                {
                    if (e.Kind != LineageEventKind.Birth || e.Source == FounderSource.None) continue;

                    Assert.True(e.EndowmentJoules > 0d, $"founder {e.Id} ({e.Source}) was not endowed");
                    if (e.Source == FounderSource.Floor) floorEndowed++;
                    else trickleEndowed++;
                }
            }

            double identity = world.MatterResidual;

            _output.WriteLine(
                $"alive {world.Living.Count}, births {world.Births}, floor {world.FloorSpawns} " +
                $"({floorEndowed} endowed), trickle {world.TrickleSpawns} ({trickleEndowed} endowed); " +
                $"audit {world.AuditResidual:R} of {world.EnergyIn:0} J in; matter identity " +
                $"{identity:R} of {world.MatterInitialTotal:0} + {world.MatterInfluxedTotal:0.###} in");

            Assert.True(floorEndowed > 0, "the floor founded nobody");
            Assert.True(trickleEndowed > 0, "the trickle founded nobody");
            Assert.True(
                Math.Abs(world.AuditResidual) <= 1e-6 * Math.Max(1d, world.EnergyIn),
                $"audit residual {world.AuditResidual:R}");
            Assert.True(
                Math.Abs(identity) <= 1e-6 * (world.MatterInitialTotal + world.MatterInfluxedTotal),
                $"matter identity {identity:R}");
        }
    }
}
