using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D074's open matter budget — <see cref="RunConfig.MatterInfluxPerSecond"/>,
    /// <see cref="RunConfig.MatterInfluxAt"/> and <see cref="RunConfig.MatterBurialPerSecond"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Matter has had no source since D048 wrote it: a stock seeded at construction, locked into
    /// bodies at conception and given back at death, with a total that never moved. Energy has
    /// never been run that way — light in at the surface, respiration out — and the asymmetry is
    /// what leaves a world holding full stomachs in full water and still refusing them children
    /// (logbook/0054's failing seed). D074 gives matter the same open shape: rivers and dust or a
    /// hydrothermal plume in, sediment out.
    /// </para>
    /// <para>
    /// Four things have to hold. The closed budget — both knobs at 0 — has to be the world every
    /// earlier arm ran in, step for step, or the record stops replaying. The deposit has to land
    /// where the route says it lands, and nowhere else. Burial has to take free matter off the
    /// floor and nothing else: not detritus, not more than the floor holds, and not from an empty
    /// floor. And the identity that replaces D048's conservation —
    /// <c>initial + influxed − buried == free + locked</c> — has to close in a world that is
    /// actually living in it, because an identity only asserted over an empty world is an
    /// identity about arithmetic rather than about the ecology.
    /// </para>
    /// </remarks>
    public class OpenMatterBudgetTests
    {
        private readonly ITestOutputHelper _output;

        public OpenMatterBudgetTests(ITestOutputHelper output) => _output = output;

        // ---------------------------------------------------------------------------------
        // 1. The closed budget is the world that already existed.
        // ---------------------------------------------------------------------------------

        [Fact]
        public void BothKnobsAtZeroChangeNothing()
        {
            // The D052/D055 shape ConceptionOrderTests uses, for two floats and an enum: a run
            // that never heard of D074 must be bit-identical to one that names its defaults. The
            // route is named too, because at influx 0 Surface and Vent are the same world and a
            // reader has to be able to trust that they are.
            Assert.Equal(0f, new RunConfig().MatterInfluxPerSecond);
            Assert.Equal(0f, new RunConfig().MatterBurialPerSecond);
            Assert.Equal(MatterInflux.Surface, new RunConfig().MatterInfluxAt);

            var untouched = new World(new RunConfig(), seed: 9);
            var named = new World(
                new RunConfig
                {
                    MatterInfluxPerSecond = 0f,
                    MatterInfluxAt = MatterInflux.Surface,
                    MatterBurialPerSecond = 0f,
                },
                seed: 9);

            Assert.Equal(untouched.Config.Hash(), named.Config.Hash());

            for (int i = 0; i < 200; i++)
            {
                untouched.Step(1f);
                named.Step(1f);
            }

            _output.WriteLine(Describe("untouched", untouched));
            _output.WriteLine(Describe("named 0   ", named));

            Assert.Equal(untouched.Births, named.Births);
            Assert.Equal(untouched.Deaths, named.Deaths);
            Assert.Equal(untouched.FloorSpawns, named.FloorSpawns);
            Assert.Equal(untouched.ConceptionsBlockedByMatter, named.ConceptionsBlockedByMatter);
            Assert.Equal(untouched.EnergyIn, named.EnergyIn);
            Assert.Equal(untouched.EnergyOut, named.EnergyOut);
            Assert.Equal(untouched.StandingJoules, named.StandingJoules);
            Assert.Equal(untouched.StandingMatter, named.StandingMatter);
            Assert.Equal(LivingIds(untouched), LivingIds(named));

            // And the counters stayed shut: a closed budget that quietly moved a unit either way
            // would still pass every comparison above, because both worlds would move it.
            Assert.Equal(0d, untouched.MatterInfluxedTotal);
            Assert.Equal(0d, untouched.MatterBuriedTotal);
            Assert.Equal(untouched.MatterInitialTotal, untouched.StandingMatter, 6);
        }

        // ---------------------------------------------------------------------------------
        // 2. The deposit lands where the route says.
        // ---------------------------------------------------------------------------------

        [Fact]
        public void SurfaceInfluxIsSharedEquallyOverTheTopLayerOfEveryPatch()
        {
            // Four patches, one still second, and a field that neither sinks, stirs nor decays, so
            // what is in a cell after the step is exactly what was put there. The world's income
            // is a total and not a total per patch — raising K divides the deposit rather than
            // multiplying the world's supply — which is the assertion the sum below makes.
            const int Patches = 4;
            const float Influx = 3f;
            const float Seconds = 0.5f;

            var world = new World(Still(Patches, influx: Influx), seed: 3);
            world.Step(Seconds);

            double expectedTotal = Influx * Seconds;
            double perPatch = expectedTotal / Patches;

            for (int patch = 0; patch < Patches; patch++)
            {
                Assert.Equal(perPatch, world.Matter.StockInLayer(0, patch), 5);

                // Nowhere else. A route that landed in the right patch and the wrong layer would
                // pass a total-only check and be a different world.
                for (int layer = 1; layer < world.Matter.LayerCount; layer++)
                {
                    Assert.Equal(0d, world.Matter.StockInLayer(layer, patch));
                }
            }

            _output.WriteLine(
                FormattableString.Invariant(
                    $"surface: {world.MatterInfluxedTotal:0.######} influxed over {Patches} patches, ") +
                FormattableString.Invariant($"{world.Matter.TotalJoules:0.######} standing"));

            Assert.Equal(expectedTotal, world.MatterInfluxedTotal, 5);
            Assert.Equal(expectedTotal, world.Matter.TotalJoules, 5);
        }

        [Fact]
        public void VentInfluxLandsWholeInTheVentPatchAtTheVentsDepth()
        {
            // The other route, and the reason it is a knob rather than a constant: the whole
            // income enters at the bottom of one column instead of being spread over the top of
            // every one. The coordinates are D067's own — a second pair of vent fields would be a
            // second thing to keep in agreement with WorldDepthMetres.
            const int Patches = 4;
            const int VentPatch = 2;
            const float Influx = 3f;
            const float Seconds = 0.5f;

            RunConfig config = Still(Patches, influx: Influx);
            config.MatterInfluxAt = MatterInflux.Vent;
            config.Current.VentPatch = VentPatch;
            config.Current.VentDepthMetres = config.WorldDepthMetres;

            var world = new World(config, seed: 3);
            world.Step(Seconds);

            double expectedTotal = Influx * Seconds;

            // LayerOf clamps at the floor, so the vent's depth — the floor, by D067's own
            // validation — is the deepest layer there is.
            int ventLayer = world.Matter.LayerOf(-config.Current.VentDepthMetres);
            Assert.Equal(world.Matter.LayerCount - 1, ventLayer);

            _output.WriteLine(
                FormattableString.Invariant(
                    $"vent: layer {ventLayer} of patch {VentPatch} holds ") +
                FormattableString.Invariant($"{world.Matter.StockInLayer(ventLayer, VentPatch):0.######}"));

            Assert.Equal(expectedTotal, world.Matter.StockInLayer(ventLayer, VentPatch), 5);

            for (int patch = 0; patch < Patches; patch++)
            {
                for (int layer = 0; layer < world.Matter.LayerCount; layer++)
                {
                    if (patch == VentPatch && layer == ventLayer) continue;
                    Assert.Equal(0d, world.Matter.StockInLayer(layer, patch));
                }
            }

            Assert.Equal(expectedTotal, world.MatterInfluxedTotal, 5);
        }

        [Fact]
        public void AVentInfluxWithNowhereToLandIsRefusedAtConstruction()
        {
            // §9's "refuses rather than defaults", applied where the cost of defaulting is the
            // world's entire matter income arriving in a patch that does not exist. ValidateVent
            // does not cover this: it returns early while the plume is off, and an influx at the
            // vent's coordinates with no plume is a perfectly sensible world.
            RunConfig config = Still(patches: 2, influx: 1f);
            config.MatterInfluxAt = MatterInflux.Vent;
            config.Current.VentPatch = 5;

            ArgumentException e = Assert.Throws<ArgumentException>(() => new World(config, seed: 3));
            _output.WriteLine(e.Message);
            Assert.Contains("VentPatch", e.Message);
        }

        // ---------------------------------------------------------------------------------
        // 3. Burial takes free matter off the floor, and nothing else.
        // ---------------------------------------------------------------------------------

        [Fact]
        public void BurialTakesItsFractionFromTheFloorLayerAndNoOther()
        {
            // A seeded column, one still second, and a rate small enough that the answer is a
            // fraction rather than the clamp. Every layer but the last has to be untouched — a
            // sink that drained the whole column would be a different mechanism wearing this
            // one's name, and the standing total alone could not tell them apart.
            const float Rate = 0.25f;
            const float Seconds = 1f;

            RunConfig config = Still(patches: 2, influx: 0f);
            config.InitialMatterPerCubicMetre = 1f;
            config.MatterBurialPerSecond = Rate;

            var world = new World(config, seed: 3);

            int floor = world.Matter.LayerCount - 1;
            double perCell = world.MatterInitialTotal / (world.Matter.LayerCount * 2);
            Assert.True(perCell > 0d, "the column was seeded");

            world.Step(Seconds);

            for (int patch = 0; patch < 2; patch++)
            {
                for (int layer = 0; layer < floor; layer++)
                {
                    Assert.Equal(perCell, world.Matter.StockInLayer(layer, patch), 5);
                }

                Assert.Equal(perCell * (1d - Rate * Seconds), world.Matter.StockInLayer(floor, patch), 5);
            }

            _output.WriteLine(
                FormattableString.Invariant(
                    $"burial: {world.MatterBuriedTotal:0.######} of {world.MatterInitialTotal:0.######} ") +
                FormattableString.Invariant($"gone, {world.Matter.TotalJoules:0.######} left"));

            Assert.Equal(perCell * Rate * Seconds * 2, world.MatterBuriedTotal, 5);
            Assert.Equal(
                world.MatterInitialTotal - world.MatterBuriedTotal, world.Matter.TotalJoules, 5);
        }

        [Fact]
        public void BurialNeverTakesMoreThanTheFloorHolds()
        {
            // A step long enough to ask for four times the floor's stock. The floor empties and
            // the counter records what was there, not what was asked for — anything else and the
            // identity would report a world that had buried matter it never had.
            RunConfig config = Still(patches: 1, influx: 0f);
            config.InitialMatterPerCubicMetre = 1f;
            config.MatterBurialPerSecond = 4f;

            var world = new World(config, seed: 3);

            int floor = world.Matter.LayerCount - 1;
            double floorBefore = world.Matter.StockInLayer(floor, 0);

            world.Step(1f);

            _output.WriteLine(
                FormattableString.Invariant(
                    $"clamped: floor held {floorBefore:0.######}, buried {world.MatterBuriedTotal:0.######}"));

            Assert.Equal(0d, world.Matter.StockInLayer(floor, 0), 6);
            Assert.Equal(floorBefore, world.MatterBuriedTotal, 5);
            Assert.True(world.MatterBuriedTotal <= world.MatterInitialTotal);
        }

        [Fact]
        public void BurialTakesNothingFromAnEmptyFloorAndNothingFromDetritus()
        {
            // Two negatives in one world, because they fail the same way: a sink that invented a
            // withdrawal from an empty cell, or one that reached into the wrong pool, would both
            // show up only as a number that did not add up somewhere else. Matter and detritus
            // are different substances that happen to share a field type (§5A.2's audit is over
            // energy and would not see a matter leak at all).
            RunConfig config = Still(patches: 1, influx: 0f);
            config.InitialMatterPerCubicMetre = 0f;
            config.MatterBurialPerSecond = 0.5f;

            var world = new World(config, seed: 3);

            int floor = world.Nutrients.LayerCount - 1;
            float floorY = -((floor + 0.5f) * world.Nutrients.LayerMetres);
            world.Nutrients.Deposit(floorY, 1000f, 0);
            double detritusBefore = world.Nutrients.TotalJoules;

            world.Step(1f);

            _output.WriteLine(
                FormattableString.Invariant(
                    $"empty floor: buried {world.MatterBuriedTotal:0.######}, ") +
                FormattableString.Invariant(
                    $"detritus {detritusBefore:0.###} -> {world.Nutrients.TotalJoules:0.###}"));

            Assert.Equal(0d, world.MatterBuriedTotal);
            Assert.Equal(0d, world.Matter.TotalJoules);
            Assert.Equal(detritusBefore, world.Nutrients.TotalJoules, 6);
        }

        // ---------------------------------------------------------------------------------
        // 4. The identity that replaces D048's conservation.
        // ---------------------------------------------------------------------------------

        [Fact]
        public void TheIdentityClosesInALivingWorld()
        {
            // The whole point of the two counters, asserted where it can actually fail: five
            // hundred seconds of the reference world with the budget open, creatures being born,
            // locking matter away and dying back into the column, while an influx feeds the
            // surface and the floor is drained. Every other thing matter does — settling, mixing,
            // advection, remineralisation, conception, death — moves it between cells and bodies,
            // so these two are the only holes in the wall and the identity is what says so.
            var config = new RunConfig
            {
                MatterInfluxPerSecond = 1f,
                MatterBurialPerSecond = 0.05f,

                // A price per child, because the reference config's is 0 and a world in which
                // conception costs no matter never locks any — which would leave the most
                // important half of the identity untested. One unit a child is D048's own shape,
                // flat rather than per joule of tissue, so what is locked does not depend on how
                // big the child turned out.
                MatterPerCreature = 1f,
            };

            var world = new World(config, seed: 9);
            for (int i = 0; i < 500; i++) world.Step(1f);

            double expected =
                world.MatterInitialTotal + world.MatterInfluxedTotal - world.MatterBuriedTotal;
            double actual = world.Matter.TotalJoules + world.MatterInBodies;

            _output.WriteLine(Describe("open budget", world));
            _output.WriteLine(
                FormattableString.Invariant(
                    $"initial {world.MatterInitialTotal:0.###} + in {world.MatterInfluxedTotal:0.###} ") +
                FormattableString.Invariant(
                    $"- buried {world.MatterBuriedTotal:0.###} = {expected:0.###} against ") +
                FormattableString.Invariant(
                    $"{world.Matter.TotalJoules:0.###} free + {world.MatterInBodies:0.###} locked"));

            // The world has to have been alive for the assertion to mean anything — an identity
            // that only ever held over an empty column is an identity about arithmetic. Bodies
            // holding matter at the end is the part that matters most: burial reaching into a
            // living creature's locked share is precisely what this cannot be allowed to do.
            Assert.True(world.Births > 0, "nothing was born");
            Assert.True(world.Deaths > 0, "nothing died");
            Assert.True(world.MatterInBodies > 0d, "no matter was locked in a body");
            Assert.True(world.MatterInfluxedTotal > 0d, "the influx never fired");
            Assert.True(world.MatterBuriedTotal > 0d, "the burial never fired");

            Assert.True(
                Math.Abs(expected - actual) <= 1e-3 * Math.Max(1d, Math.Abs(expected)),
                FormattableString.Invariant($"identity open by {expected - actual:0.######}"));
        }

        [Fact]
        public void AnOpenBudgetIsADifferentWorldFromAClosedOne()
        {
            // logbook/0007 and logbook/0008's rule: identical numbers across a configuration
            // change mean the change never reached the thing it configures. A knob that was read
            // and then dropped would produce exactly the reassuring agreement this asserts
            // against.
            var closed = new World(new RunConfig(), seed: 9);
            var open = new World(
                new RunConfig { MatterInfluxPerSecond = 1f, MatterBurialPerSecond = 0.05f },
                seed: 9);

            for (int i = 0; i < 200; i++)
            {
                closed.Step(1f);
                open.Step(1f);
            }

            _output.WriteLine(Describe("closed", closed));
            _output.WriteLine(Describe("open  ", open));

            Assert.NotEqual(closed.Config.Hash(), open.Config.Hash());
            Assert.NotEqual(closed.StandingMatter, open.StandingMatter);
        }

        // ---------------------------------------------------------------------------------
        // 5. Three tunables like every other.
        // ---------------------------------------------------------------------------------

        [Fact]
        public void AllThreeReachTheHashAndTheFile()
        {
            // The two reflection guards cover this generically; these say what the generic ones
            // mean for D074 in particular — three knobs, one of them the second scalar enum on
            // RunConfig and therefore the second whose file value is a word rather than a number.
            var closed = new RunConfig();
            var open = new RunConfig
            {
                MatterInfluxPerSecond = 0.6f,
                MatterInfluxAt = MatterInflux.Vent,
                MatterBurialPerSecond = 0.01f,
            };

            Assert.NotEqual(closed.Hash(), open.Hash());

            // Each on its own, so a knob that reached neither the hash nor the file could not hide
            // behind the other two.
            Assert.NotEqual(
                closed.Hash(), new RunConfig { MatterInfluxPerSecond = 0.6f }.Hash());
            Assert.NotEqual(
                closed.Hash(), new RunConfig { MatterInfluxAt = MatterInflux.Vent }.Hash());
            Assert.NotEqual(
                closed.Hash(), new RunConfig { MatterBurialPerSecond = 0.01f }.Hash());

            string text = RunConfigJson.Write(open);
            _output.WriteLine(Line(text, "matterInfluxPerSecond"));
            _output.WriteLine(Line(text, "matterInfluxAt"));
            _output.WriteLine(Line(text, "matterBurialPerSecond"));

            Assert.Contains("\"matterInfluxAt\": \"Vent\"", text);

            RunConfig back = RunConfigJson.Read(text, out string mismatch);
            Assert.Null(mismatch);
            Assert.Equal(0.6f, back.MatterInfluxPerSecond);
            Assert.Equal(MatterInflux.Vent, back.MatterInfluxAt);
            Assert.Equal(0.01f, back.MatterBurialPerSecond);
            Assert.Equal(open.Hash(), back.Hash());
        }

        [Fact]
        public void AnUnknownInfluxRouteIsRefusedOnLoad()
        {
            // §9 again. A route the file names and the run did not take is a run filed under
            // settings it never had — and the two routes put the world's whole matter income at
            // opposite ends of the column, so nothing downstream would look merely slightly off.
            string text = RunConfigJson.Write(new RunConfig())
                .Replace("\"matterInfluxAt\": \"Surface\"", "\"matterInfluxAt\": \"Rivers\"");

            FormatException e = Assert.Throws<FormatException>(() => RunConfigJson.Read(text));

            _output.WriteLine(e.Message);
            Assert.Contains("Rivers", e.Message);
            Assert.Contains("Surface", e.Message);
            Assert.Contains("Vent", e.Message);
        }

        // ---------------------------------------------------------------------------------
        // The still world.
        // ---------------------------------------------------------------------------------

        /// <summary>
        /// A world with nothing in it and nothing moving: no founders, no sinking, no stirring, no
        /// decay.
        /// </summary>
        /// <remarks>
        /// So that a cell's contents after a step are exactly what the influx or the burial put
        /// there or took out, with no transport term to subtract first. The population floor is
        /// held shut for AbsorptiveLogTests' reason and one of its own — a founder trickle would
        /// draw matter out of the very layers these tests are reading.
        /// </remarks>
        private static RunConfig Still(int patches, float influx) => new RunConfig
        {
            MinimumPopulation = 0,
            MaximumPopulation = 100_000,

            HorizontalPatches = patches,
            InitialMatterPerCubicMetre = 0f,
            MatterSinkMetresPerSecond = 0f,
            MatterMixingDiffusivity = 0f,
            MatterRemineralisationPerSecond = 0f,
            NutrientSinkMetresPerSecond = 0f,
            NutrientMixingDiffusivity = 0f,
            NutrientRemineralisationPerSecond = 0f,
            HorizontalMixingDiffusivity = 0f,

            MatterInfluxPerSecond = influx,
        };

        private static string LivingIds(World world)
        {
            var ids = new System.Collections.Generic.List<string>(world.Living.Count);
            foreach (Organism creature in world.Living) ids.Add(creature.Id.ToString());
            return string.Join(",", ids);
        }

        private static string Describe(string label, World world) =>
            FormattableString.Invariant(
                $"{label}: alive {world.Living.Count}, births {world.Births}, deaths {world.Deaths}, ") +
            FormattableString.Invariant(
                $"free {world.Matter.TotalJoules:0.###}, locked {world.MatterInBodies:0.###}, ") +
            FormattableString.Invariant(
                $"in {world.MatterInfluxedTotal:0.###}, buried {world.MatterBuriedTotal:0.###}");

        private static string Line(string text, string key)
        {
            foreach (string line in text.Split('\n'))
            {
                if (line.Contains(key)) return line.Trim();
            }

            return $"({key} not in the file)";
        }
    }
}
