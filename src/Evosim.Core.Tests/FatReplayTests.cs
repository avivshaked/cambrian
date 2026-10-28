using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D247: with every new switch off and fat absent from the table, a world steps bit for bit as it did at
    /// <c>caff05ad</c>, the commit fat was built on. The two digests below were recorded by running these same
    /// scenarios, line for line, on that commit's source (they use nothing this build added), and are checked here.
    /// </summary>
    public class FatReplayTests
    {
        private readonly ITestOutputHelper _output;

        public FatReplayTests(ITestOutputHelper output) => _output = output;

        // Recorded on caff05ad. A change to either is a change to the world the switches left alone.
        private const string PinnedEconomy = "137ea8dc6c57c077bae49641";
        private const string PinnedGut = "2e1e130f1f5557ff77623b57";

        private static string Digest(StringBuilder text)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()));
                return BitConverter.ToString(hash, 0, 12).Replace("-", "").ToLowerInvariant();
            }
        }

        private static string R(double v) => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

        private static Genome Box(string cellType, ReproductionTraits reproduction)
        {
            Genome genome = Fixtures.SingleLeaf(0.2f, cellType);
            genome.AdultScale = 1f;
            genome.Reproduction = reproduction;
            return genome;
        }

        /// <summary>A mixed population under D098's seconds cap, lump and gestating breeders, run through births and deaths.</summary>
        private static StringBuilder Economy()
        {
            var config = new RunConfig
            {
                Light = new LightModel(60f, 12f),
                MinimumPopulation = 0,
                PerOffspringOverheadJoules = 20f,
                InitialMatterPerCubicMetre = 50f,
                ReserveCapSeconds = 600f,
                SenescenceDoublingSeconds = 3000f,
                CorpseDecayPerSecond = 0.01f,
            };

            var lump = new ReproductionTraits { BroodSize = 1, BirthInvestment = 0.5f, ReserveMargin = 10f };
            var gestating = new ReproductionTraits
            {
                BroodSize = 1, BirthInvestment = 0.5f, Mode = ReproductionMode.Gestation, GestationShare = 0.5f,
            };

            var world = new World(config, seed: 5);
            world.Inoculate(Box(CellTypeIds.Photosynthetic, lump), 4, -0.5f);
            world.Inoculate(Box(CellTypeIds.Photosynthetic, gestating), 2, -0.5f);
            world.Inoculate(Box(CellTypeIds.Absorptive, lump), 2, -0.5f);

            // Births held for 300 s so the hoards reach the cap and the trim runs, then released.
            world.BirthsHeld = true;

            var text = new StringBuilder();
            for (int step = 1; step <= 1000; step++)
            {
                if (step == 601) world.BirthsHeld = false;
                world.Step(0.5f);
                if (step % 50 != 0) continue;

                double reserves = 0d, accounts = 0d;
                foreach (Organism b in world.Living) { reserves += b.Energy; accounts += b.GestationJoules; }
                text.Append(step).Append(' ').Append(world.Living.Count).Append(' ').Append(world.Births).Append(' ')
                    .Append(world.Deaths).Append(' ').Append(R(world.EnergyIn)).Append(' ').Append(R(reserves)).Append(' ')
                    .Append(R(accounts)).Append(' ').Append(R(world.ReserveTrimmedTotal)).Append(' ')
                    .Append(R(world.DetritusExudedTotal)).Append(' ').Append(R(world.Nutrients.TotalJoules)).Append('\n');
            }

            Assert.True(world.ReserveTrimmedTotal > 0d, "the seconds cap never fired, so the scenario does not exercise it");
            Assert.True(world.Births > 0L, "nothing was born");
            return text;
        }

        /// <summary>A kill into a small mouth's gut, digested over many steps, at the cap off and at 100 seconds.</summary>
        private static StringBuilder Gut()
        {
            var text = new StringBuilder();

            foreach (float cap in new[] { 0f, 100f })
            {
                RunConfig config = ContactEatingTests.Stage(on: true);
                config.KillSwallowsBody = true;
                config.PerOffspringOverheadJoules = 1f;
                config.ReserveCapSeconds = cap;

                var world = new World(config, seed: 3);
                world.Inoculate(ContactEatingTests.Branching(), 1, -1f);
                world.Inoculate(ContactEatingTests.Hunter(ContactEatingTests.ConsumerIntake * 0.001f), 1, -1f);
                Organism hunter = world.Living[1];

                world.SetContacts(ContactEatingTests.Touching(hunter, 0, world.Living[0], 0));
                world.ApplyMouth(100f);

                for (int step = 1; step <= 60; step++)
                {
                    world.Step(0.5f);
                    text.Append(cap).Append(' ').Append(step).Append(' ').Append(R(hunter.Energy)).Append(' ')
                        .Append(R(hunter.GutJoules)).Append(' ').Append(R(world.GutDigestedJoules)).Append(' ')
                        .Append(R(world.GutWasteJoules)).Append(' ').Append(R(world.GutCapCutJoules)).Append(' ')
                        .Append(world.GutCapBound).Append(' ').Append(R(world.ReserveTrimmedTotal)).Append('\n');
                }

                Assert.True(world.GutDigestedJoules > 0d, "nothing was digested");
            }

            return text;
        }

        [Fact]
        public void TheSwitchesOffAndNoFatReplayTheEconomyOfCaff05adToTheBit()
        {
            string digest = Digest(Economy());
            _output.WriteLine("economy " + digest);
            Assert.Equal(PinnedEconomy, digest);
        }

        [Fact]
        public void TheSwitchesOffAndNoFatReplayTheGutOfCaff05adToTheBit()
        {
            string digest = Digest(Gut());
            _output.WriteLine("gut " + digest);
            Assert.Equal(PinnedGut, digest);
        }
    }
}
