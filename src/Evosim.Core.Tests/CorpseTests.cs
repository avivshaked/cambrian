using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// A death as a particle rather than as a deposit. Rule 6 of <c>fable-propose-grid.md</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two things are being held here at once. The first is that
    /// <see cref="RunConfig.CorpseDecayPerSecond"/> at 0 is the world every launcher on file ran:
    /// nothing is founded, the deposit lands in the dead body's own cell on the step it dies, and
    /// the two books close exactly as they did. The second is that above 0 the corpse is a third
    /// standing account and not a hole between the water and the living, which is what §5A.2's
    /// audit and D074's matter identity are asked here.
    /// </para>
    /// <para>
    /// <b>Most of these run a world with nothing edible in it.</b> A refuge as thick as the world
    /// with an edible fraction of 0 (D055, and its arm-C dial) means no mouth can price a joule,
    /// so the detritus field's only inflow is a death and its only outflow is nothing at all. That
    /// is what lets a per-layer stock be compared against a dead body's tissue to the last float,
    /// rather than against a number feeding has already taken a share of.
    /// </para>
    /// </remarks>
    public class CorpseTests
    {
        private readonly ITestOutputHelper _output;

        public CorpseTests(ITestOutputHelper output) => _output = output;

        /// <summary>
        /// A dark world where nothing can be eaten and nothing moves: the floor keeps founding
        /// creatures, they starve, and the only thing that ever touches the water is a death.
        /// </summary>
        private static RunConfig Larder(float decay)
        {
            var config = new RunConfig
            {
                Light = new LightModel(1e-6f, 1f),
                MinimumPopulation = 20,

                // Nothing sinks, mixes, leaks off the floor or drifts, so a joule deposited stays
                // in the layer it was deposited in and a corpse stays where it was founded. Every
                // one of these is already the default; they are written out because the tests
                // below read a stock as if nothing but a death had touched it, and that has to be
                // a stated condition rather than an inherited one.
                NutrientSinkMetresPerSecond = 0f,
                MatterSinkMetresPerSecond = 0f,
                NutrientMixingDiffusivity = 0f,
                MatterMixingDiffusivity = 0f,
                HorizontalMixingDiffusivity = 0f,

                // D055's refuge over the whole column at arm C's fraction of 0: nothing is edible
                // anywhere, so DetritusTakenTotal stays at 0 and the field's arithmetic is the
                // deaths alone.
                FloorRefugeMetres = 60f,
                RefugeEdibleFraction = 0f,

                CorpseDecayPerSecond = decay,
            };

            return config;
        }

        [Fact]
        public void ADeathAtDecayZeroDepositsAtOnceAndFoundsNothing()
        {
            // The bit-identity clause. At 0 there is no particle at any instant, the deposit is
            // charged to DetritusDepositedTotal on the step the body dies rather than in
            // instalments, and it lands in the layer the body died in.
            var config = Larder(decay: 0f);
            Assert.Equal(0f, new RunConfig().CorpseDecayPerSecond);

            var world = new World(config, seed: 1);
            var before = new Dictionary<long, (int Layer, float Tissue)>();
            var layerGain = new Dictionary<int, double>();
            int deathSteps = 0;

            for (int step = 0; step < 400; step++)
            {
                before.Clear();
                layerGain.Clear();

                foreach (Organism creature in world.Living)
                {
                    before[creature.Id] = (world.Nutrients.LayerOf(creature.HeightY), creature.TissueJoules);
                }

                var stockBefore = new double[world.Nutrients.LayerCount];
                for (int i = 0; i < stockBefore.Length; i++) stockBefore[i] = world.Nutrients.StockInLayer(i);

                double depositedBefore = world.DetritusDepositedTotal;
                long deathsBefore = world.Deaths;

                world.Step(1f);

                Assert.Empty(world.Corpses);
                Assert.Equal(0d, world.CorpseJoules);
                Assert.Equal(0d, world.CorpseMatter);

                if (world.Deaths == deathsBefore) continue;
                deathSteps++;

                // Who is gone, and what each of them was holding when the step began.
                var alive = new HashSet<long>();
                foreach (Organism creature in world.Living) alive.Add(creature.Id);

                double expected = 0d;
                foreach (KeyValuePair<long, (int Layer, float Tissue)> entry in before)
                {
                    if (alive.Contains(entry.Key)) continue;
                    expected += entry.Value.Tissue;
                    layerGain.TryGetValue(entry.Value.Layer, out double sum);
                    layerGain[entry.Value.Layer] = sum + entry.Value.Tissue;
                }

                // Charged on the step of the death, not spread over the steps after it.
                Assert.Equal(expected, world.DetritusDepositedTotal - depositedBefore, 4);

                // And charged to the dead body's own layer. Nothing else in this world moves a
                // joule, so every layer's stock is its old stock plus exactly what died in it.
                for (int layer = 0; layer < stockBefore.Length; layer++)
                {
                    layerGain.TryGetValue(layer, out double gain);
                    Assert.Equal(stockBefore[layer] + gain, world.Nutrients.StockInLayer(layer), 4);
                }
            }

            _output.WriteLine(
                $"{world.Deaths} deaths over {deathSteps} steps; " +
                $"deposited {world.DetritusDepositedTotal:0.###} J, taken {world.DetritusTakenTotal:0.###} J");

            Assert.True(world.Deaths > 0, "nothing died, so the deposit path was never exercised");
            Assert.True(deathSteps > 0, "no step carried a death");
            Assert.Equal(0d, world.DetritusTakenTotal);
            Assert.True(
                Math.Abs(world.AuditResidual) <= 1e-6 * Math.Max(1d, world.StandingJoules),
                $"audit residual {world.AuditResidual:R}");
        }

        [Fact]
        public void ACorpseHalvesInTheStatedHalfLife()
        {
            // 0.005 per second is ln 2 / 0.005 = 138.6 s, which is the arithmetic the tunable's
            // remark states. The scheme is a fraction of the remainder per step rather than a
            // continuous exponential, so at a half-second step the discrete answer is 0.4999 of
            // the original and not 0.5 exactly; a per cent covers that gap several times over and
            // would not cover a rate applied twice or applied to the original rather than to the
            // remainder.
            const float Rate = 0.005f;
            const float Dt = 0.5f;

            var world = new World(Larder(decay: Rate), seed: 1);

            Corpse corpse = null;
            for (int step = 0; step < 2_000 && corpse == null; step++)
            {
                world.Step(Dt);
                if (world.Corpses.Count > 0) corpse = world.Corpses[0];
            }

            Assert.NotNull(corpse);
            Assert.True(corpse.Joules > 0d, "a corpse was founded holding nothing");

            double opening = corpse.Joules;
            double openingAge = corpse.AgeSeconds;
            double halfLife = Math.Log(2d) / Rate;
            int steps = (int)Math.Round(halfLife / Dt);

            for (int step = 0; step < steps; step++) world.Step(Dt);

            double ratio = corpse.Joules / opening;

            _output.WriteLine(
                $"corpse of {corpse.CreatureId}: {opening:0.####} J -> {corpse.Joules:0.####} J " +
                $"over {steps * Dt:0.#} s, ratio {ratio:0.#####}; age {corpse.AgeSeconds:0.#} s");

            // The corpse was already one step old when this test first saw it: Bury founds it in
            // Metabolise and the transport pass of that same step has already carried it once.
            Assert.Equal(steps * Dt, corpse.AgeSeconds - openingAge, 3);
            Assert.True(Math.Abs(ratio - 0.5d) <= 0.01d, $"ratio {ratio:R} is not a half-life");
        }

        [Fact]
        public void ACorpseSinksAtTheSinkSpeed()
        {
            // The corpse is detritus that has not dissolved yet, so it falls at the detritus
            // field's own speed and stops on the floor rather than passing through it. The decay
            // is set small enough to leave the particle alone over the twenty steps read here:
            // this test is about where it is, not what it is worth.
            const float Dt = 0.5f;
            const float Sink = 0.5f;

            RunConfig config = Larder(decay: 1e-6f);
            config.NutrientSinkMetresPerSecond = Sink;

            var world = new World(config, seed: 1);

            Corpse corpse = null;
            for (int step = 0; step < 2_000 && corpse == null; step++)
            {
                world.Step(Dt);
                if (world.Corpses.Count > 0) corpse = world.Corpses[0];
            }

            Assert.NotNull(corpse);

            // A founder with no parent is admitted at its patch's centre, so this is 10 m and not
            // 0 in a 400 m2 one-patch world. What matters is that nothing moves it sideways: this
            // world has no current, so the horizontal pair must be exactly where the death left
            // them however many steps the corpse falls for.
            float x = corpse.Position.X;
            float z = corpse.Position.Z;

            float floor = -config.WorldDepthMetres;
            for (int step = 0; step < 40; step++)
            {
                float y = corpse.Position.Y;
                float expected = y - Sink * Dt;
                if (expected < floor) expected = floor;

                world.Step(Dt);

                Assert.Equal(expected, corpse.Position.Y, 4);
                Assert.Equal(x, corpse.Position.X);
                Assert.Equal(z, corpse.Position.Z);
            }

            _output.WriteLine($"corpse of {corpse.CreatureId} settled to {corpse.Position.Y:0.###} m");
        }

        [Fact]
        public void ACorpseRidesTheCurrentAndWrapsAtTheSeam()
        {
            // D066's water carries a corpse exactly as it carries a vertex: the velocity is
            // sampled at the position the step began at, applied over the step, and the two rings
            // are folded rather than clipped. The expected position is computed here from
            // CurrentField itself, so the test says "the corpse goes where the water goes"
            // rather than restating one particular flow.
            //
            // The speed is far above anything a run would use, and deliberately: a founder is
            // admitted at its patch's centre, the ring here is 20 m round, and a flow of the
            // campaign's 0.3 m/s would rock the corpse back and forth in the middle of it and
            // never reach a seam. At 60 m/s a step carries it further than the whole ring, so the
            // fold is read on most steps rather than hoped for.
            const float Dt = 0.5f;

            RunConfig config = Larder(decay: 1e-6f);
            config.SharedSpace = true;
            config.Current = new CurrentField
            {
                Speed = 60f,
                CellMetres = 10f,
                PeriodSeconds = 600f,
                Rolls = false,
                AdvectFields = true,
            };

            var world = new World(config, seed: 1);
            float length = world.Nutrients.PatchWidthMetres * world.Nutrients.PatchCount;
            float width = world.Nutrients.PatchWidthMetres;

            Corpse corpse = null;
            for (int step = 0; step < 2_000 && corpse == null; step++)
            {
                world.Step(Dt);
                if (world.Corpses.Count > 0) corpse = world.Corpses[0];
            }

            Assert.NotNull(corpse);

            bool wrapped = false;
            for (int step = 0; step < 60; step++)
            {
                Float3 p = corpse.Position;
                int patch = corpse.Patch;

                world.Step(Dt);

                Float3 v = config.Current.VelocityAt(
                    p.Y, world.ElapsedSeconds, patch, world.Nutrients.PatchCount);

                float rawX = p.X + v.X * Dt;
                float rawZ = p.Z + v.Z * Dt;
                float expectedX = Wrap(rawX, length);
                float expectedZ = Wrap(rawZ, width);
                float expectedY = p.Y + v.Y * Dt;
                if (expectedY > 0f) expectedY = 0f;
                else if (expectedY < -config.WorldDepthMetres) expectedY = -config.WorldDepthMetres;

                Assert.Equal(expectedX, corpse.Position.X, 4);
                Assert.Equal(expectedY, corpse.Position.Y, 4);
                Assert.Equal(expectedZ, corpse.Position.Z, 4);

                Assert.InRange(corpse.Position.X, 0f, length);
                Assert.InRange(corpse.Position.Z, 0f, width);

                // The fold happened whenever the unwrapped step left the ring. The assertion
                // above has already checked where it landed; this only records that the case was
                // reached at all, so a current too gentle to cross a seam cannot pass this test
                // by never testing anything.
                if (rawX < 0f || rawX >= length) wrapped = true;
            }

            _output.WriteLine(
                $"corpse of {corpse.CreatureId} at {corpse.Position.X:0.##}, " +
                $"{corpse.Position.Y:0.##}, {corpse.Position.Z:0.##} on a ring of {length:0.#} m");

            Assert.True(wrapped, "the corpse never crossed the seam, so the fold was never read");
        }

        private static float Wrap(float v, float extent)
        {
            if (v >= 0f && v < extent) return v;
            float folded = v - extent * (float)Math.Floor(v / extent);
            if (folded >= extent || folded < 0f) folded = 0f;
            return folded;
        }

        [Fact]
        public void TheLastCrumbIsDepositedAndTheCorpseIsRemoved()
        {
            // Without a floor a corpse halves for ever and the standing account fills with
            // objects holding nothing. The last instalment is therefore the whole remainder,
            // and this reads it directly: the world is emptied of the living, run down to one
            // corpse, and that corpse's disappearance is matched against what the water gained
            // on the same step. Nothing else in this world moves a joule.
            RunConfig config = Larder(decay: 0.5f);
            config.FloorClosesAfterSeconds = 5f;

            var world = new World(config, seed: 1);

            for (int step = 0; step < 4_000; step++)
            {
                world.Step(1f);
                if (world.Living.Count == 0 && world.Corpses.Count <= 1) break;
            }

            Assert.Empty(world.Living);
            Assert.Single(world.Corpses);

            Corpse last = world.Corpses[0];
            double held = last.Joules;
            double water = world.Nutrients.TotalJoules;
            double standing = world.StandingJoules;

            _output.WriteLine(
                $"last corpse of {last.CreatureId} holds {held:R} J after {last.AgeSeconds:0} s");

            Assert.True(held < 1e-6, $"the last corpse still holds {held:R} J, so it is not on the floor value");

            world.Step(1f);

            _output.WriteLine(
                $"water {water:R} -> {world.Nutrients.TotalJoules:R} J; " +
                $"standing {standing:R} -> {world.StandingJoules:R} J; " +
                $"audit residual {world.AuditResidual:R}");

            Assert.Empty(world.Corpses);
            Assert.Equal(0d, world.CorpseJoules);
            Assert.Equal(0d, world.CorpseMatter);

            // What the corpse held is what the water gained, and the world's standing total did
            // not move at all: the crumb changed account, it did not leave.
            Assert.Equal(held, world.Nutrients.TotalJoules - water, 9);
            Assert.Equal(standing, world.StandingJoules, 9);
            Assert.True(
                Math.Abs(world.AuditResidual) <= 1e-6 * Math.Max(1d, world.StandingJoules),
                $"audit residual {world.AuditResidual:R}");
        }

        [Fact]
        public void AGridWorldWithCorpsesClosesItsAuditAndItsMatterIdentity()
        {
            // GridFieldTests.AGridWorldClosesItsAuditAndItsMatterIdentity with rule 6 turned on
            // at the proposal's own rate. Everything else is that test verbatim, so a failure here
            // and a pass there is the corpse and nothing else: founders, feeding, exudation,
            // death, matter drawn at conception and returned at death, an influx on the plume's
            // cells, burial, sinking, mixing and a rolling current, over 1,500 simulated seconds.
            //
            // The corpse is a third standing account in both books at once: joules in §5A.2's
            // audit, matter in D074's identity. A corpse that leaked would open one or the
            // other, and a corpse counted twice would open them the other way.
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
                MatterInfluxPerSecond = 0.05f,
                MatterBurialPerSecond = 0.001f,
                NutrientMixingDiffusivity = 0.2f,
                HorizontalMixingDiffusivity = 0.2f,
                MatterMixingDiffusivity = 0.2f,
                CorpseDecayPerSecond = 0.005f,
                Current = new CurrentField
                {
                    Speed = 0.1f, CellMetres = 10f, PeriodSeconds = 600f, Rolls = true,
                    AdvectFields = true, RollBlinkSeconds = 300f, VentDepthMetres = Depth,
                },
            };

            var world = new World(config, seed: 3);
            int mostCorpses = 0;

            for (int step = 0; step < 3_000; step++)
            {
                world.Step(0.5f);
                if (world.Corpses.Count > mostCorpses) mostCorpses = world.Corpses.Count;
            }

            double identity =
                world.MatterInitialTotal + world.MatterInfluxedTotal - world.MatterBuriedTotal - world.StandingMatter;

            _output.WriteLine(
                $"alive {world.Living.Count}, births {world.Births}, deaths {world.Deaths}; " +
                $"corpses {world.Corpses.Count} (peak {mostCorpses}) holding " +
                $"{world.CorpseJoules:0.###} J and {world.CorpseMatter:0.###} matter; " +
                $"audit residual {world.AuditResidual:R} of {world.EnergyIn:0} J in; " +
                $"matter identity {identity:R} of {world.MatterInitialTotal:0}");

            Assert.True(world.Births > 0, "nothing was born, so the economy was not exercised");
            Assert.True(world.Deaths > 0, "nothing died, so no corpse was ever founded");
            Assert.True(mostCorpses > 0, "no corpse was founded in a world with deaths and decay on");
            Assert.True(
                Math.Abs(world.AuditResidual) <= 1e-6 * Math.Max(1d, world.EnergyIn),
                $"audit residual {world.AuditResidual:R}");
            Assert.True(
                Math.Abs(identity) <= 1e-6 * world.MatterInitialTotal,
                $"matter identity {identity:R}");

            // The running totals both grids report must still be the sum of their own cells: a
            // corpse deposits through Deposit like anything else, and a deposit that missed the
            // running total would show here and nowhere else.
            var detritus = (GridField)world.Nutrients;
            var matter = (GridField)world.Matter;
            Assert.Equal(detritus.TotalJoules, detritus.Recount(), 6);
            Assert.Equal(matter.TotalJoules, matter.Recount(), 6);
        }
    }
}
