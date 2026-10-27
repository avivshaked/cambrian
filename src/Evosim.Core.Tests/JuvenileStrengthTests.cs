using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D241's juvenile strength: with <see cref="RunConfig.JuvenileStrengthScales"/> on, a scaled body's
    /// <see cref="PhenotypePart.Power"/> goes with the cube of its length ratio, as muscle does.
    /// </summary>
    /// <remarks>
    /// <b>Written without being run</b> (the build's brief forbade it); the caller runs them in a test window.
    /// </remarks>
    public class JuvenileStrengthTests
    {
        private readonly ITestOutputHelper _output;

        public JuvenileStrengthTests(ITestOutputHelper output) => _output = output;

        // ---------------------------------------------------------------- fixtures

        /// <summary>A structural root with one hinged link on it: a body that has Power to scale.</summary>
        private static Genome Hinged(float birthFraction = 0.65f, float power = 40f)
        {
            var genome = new Genome
            {
                RootIndex = 0,
                Reproduction = new ReproductionTraits
                {
                    BroodSize = 1,
                    BirthInvestment = birthFraction / (1f - new RunConfig().NewbornReserveFraction),
                },
            };

            MorphNode root = Fixtures.Box(half: 0.3f);
            MorphNode child = Fixtures.Box(half: 0.3f, joint: JointType.Hinge);
            child.Power = power;
            root.Edges.Add(Fixtures.FaceToFace(1));
            genome.Nodes.Add(root);
            genome.Nodes.Add(child);
            return genome;
        }

        private static RunConfig Config(bool strength, float climbSeconds = 0f, float founderEnergy = 1e7f)
        {
            var config = new RunConfig
            {
                MinimumPopulation = 0,
                MaximumPopulation = 100_000,
                Light = new LightModel(200f, 12f),
                GrowthAdultSeconds = climbSeconds,
                FounderEnergyJoules = founderEnergy,
                JuvenileStrengthScales = strength,
            };

            foreach (PropertyInfo property in typeof(MutationRates).GetProperties())
            {
                if (property.PropertyType == typeof(float) && property.Name.EndsWith("Chance"))
                {
                    property.SetValue(config.Mutation, 0f);
                }
            }

            return config;
        }

        private static Phenotype Adult(RunConfig config) =>
            Developer.Develop(Hinged(), config.Development, shapes: config.Shapes);

        private static List<float> Powers(Phenotype body)
        {
            var powers = new List<float>();
            foreach (PhenotypePart part in body.Parts) powers.Add(part.Power);
            return powers;
        }

        /// <summary>Every readable field of the two bodies and their parts equal, bar Power.</summary>
        private static void AssertSameExceptPower(Phenotype a, Phenotype b)
        {
            Assert.Equal(a.PartCount, b.PartCount);

            foreach (PropertyInfo p in typeof(Phenotype).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.GetIndexParameters().Length > 0 || !p.PropertyType.IsValueType) continue;
                Assert.True(Equals(p.GetValue(a), p.GetValue(b)), "Phenotype." + p.Name + " differs");
            }

            for (int i = 0; i < a.PartCount; i++)
            {
                foreach (PropertyInfo p in typeof(PhenotypePart).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (p.Name == nameof(PhenotypePart.Power) || p.GetIndexParameters().Length > 0) continue;

                    object x = p.GetValue(a.Parts[i]);
                    object y = p.GetValue(b.Parts[i]);
                    bool same = Equals(x, y);
                    if (!same && x is IEnumerable ex && y is IEnumerable ey && !(x is string))
                    {
                        IEnumerator ix = ex.GetEnumerator(), iy = ey.GetEnumerator();
                        same = true;
                        while (same)
                        {
                            bool more = ix.MoveNext();
                            if (more != iy.MoveNext()) { same = false; break; }
                            if (!more) break;
                            same = Equals(ix.Current, iy.Current);
                        }
                    }

                    Assert.True(same, "part " + i + " " + p.Name + " differs");
                }
            }
        }

        // ---------------------------------------------------------------- Scaled

        [Theory]
        [InlineData(0.5f)]
        [InlineData(0.8663f)]
        public void ScalePowerGivesEachPartTheCubeOfTheRatioAndChangesNothingElse(float s)
        {
            RunConfig config = Config(strength: false);
            Phenotype adult = Adult(config);

            Phenotype off = adult.Scaled(s, config.Shapes);
            Phenotype on = adult.Scaled(s, config.Shapes, scalePower: true);

            Assert.True(adult.Parts[1].Power > 0f, "the fixture has no Power to scale");

            for (int i = 0; i < adult.PartCount; i++)
            {
                Assert.Equal(adult.Parts[i].Power, off.Parts[i].Power);
                Fixtures.AssertClose(adult.Parts[i].Power * s * s * s, on.Parts[i].Power, 1e-5f);
                _output.WriteLine($"part {i}: adult {adult.Parts[i].Power}, off {off.Parts[i].Power}, on {on.Parts[i].Power}");
            }

            AssertSameExceptPower(off, on);
        }

        [Fact]
        public void ScalePowerAtOneIsTheAdultItself()
        {
            RunConfig config = Config(strength: true);
            Phenotype adult = Adult(config);

            Assert.Same(adult, adult.Scaled(1f, config.Shapes, scalePower: true));
            Assert.Same(adult, adult.ScaledFor(config, 1f));
        }

        // ---------------------------------------------------------------- ScaledFor

        [Fact]
        public void ScaledForWithTheSwitchOffIsTodaysScaledFieldForField()
        {
            RunConfig config = Config(strength: false);
            Phenotype adult = Adult(config);
            const float s = 0.7f;

            Phenotype today = adult.Scaled(s, config.Shapes);
            Phenotype via = adult.ScaledFor(config, s);

            AssertSameExceptPower(today, via);
            Assert.Equal(Powers(today), Powers(via));
            Assert.Equal(Powers(adult), Powers(via));
        }

        [Fact]
        public void ScaledForWithTheSwitchOnIsTheScalePowerCopy()
        {
            RunConfig config = Config(strength: true);
            Phenotype adult = Adult(config);
            const float s = 0.7f;

            Phenotype expected = adult.Scaled(s, config.Shapes, scalePower: true);
            Phenotype via = adult.ScaledFor(config, s);

            AssertSameExceptPower(expected, via);
            Assert.Equal(Powers(expected), Powers(via));
        }

        // ---------------------------------------------------------------- the idle bill

        [Fact]
        public void AJuvenilesStandingWattsFallByExactlyTheIdleShareOfThePowerDrop()
        {
            RunConfig off = Config(strength: false);
            RunConfig on = Config(strength: true);
            Phenotype adult = Adult(off);
            const float s = 0.8663f; // about 0.65 of the adult's volume

            Phenotype juvenileOff = adult.ScaledFor(off, s);
            Phenotype juvenileOn = adult.ScaledFor(on, s);

            double wattsOff = Metabolism.StandingWatts(juvenileOff, off);
            double wattsOn = Metabolism.StandingWatts(juvenileOn, on);

            double idleDrop = 0d;
            for (int i = 0; i < juvenileOff.PartCount; i++)
            {
                PhenotypePart a = juvenileOff.Parts[i];
                PhenotypePart b = juvenileOn.Parts[i];
                if (off.CellTypes.Resolve(a.CellTypeId) is LinkCell link)
                {
                    idleDrop += (double)link.IdleWattsPerNewtonMetre * (a.Power - b.Power) *
                                Math.Max(0, a.JointType.DofCount());
                }
            }

            _output.WriteLine($"standing watts: off {wattsOff:0.#####}, on {wattsOn:0.#####}, idle drop {idleDrop:0.#####}");

            Assert.True(idleDrop > 0d, "the fixture's link has no idle bill, so nothing is tested");
            Assert.True(wattsOn < wattsOff);
            Fixtures.AssertClose(idleDrop, wattsOff - wattsOn, 1e-4 * Math.Max(1d, wattsOff));
        }

        // ---------------------------------------------------------------- resume

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

        [Fact]
        public void AJuvenileRestoredFromAStateKeepsItsScaledPower()
        {
            RunConfig config = Config(strength: true, climbSeconds: 120f);
            var world = new World(config, seed: 3);
            world.Inoculate(Hinged(0.65f), count: 1, heightY: -1f);
            Organism before = world.Living[0];

            for (int i = 0; i < 20; i++) world.Step(0.5f);

            Assert.True(before.BodyFraction > 0.65f && before.BodyFraction < 1f,
                "the body should be mid-climb, got " + before.BodyFraction);

            List<float> adultPowers = Powers(before.AdultPhenotype);
            List<float> powersBefore = Powers(before.Phenotype);
            float scale = before.Phenotype.ScaledBy;
            Assert.True(scale > 0f && scale < 1f);
            Assert.True(powersBefore[1] < adultPowers[1], "a juvenile's Power should be below its adult's");
            Fixtures.AssertClose(adultPowers[1] * scale * scale * scale, powersBefore[1], 1e-5f);

            byte[] state = StateOf(world);

            var restored = new World(config, seed: 3);
            using (var buffer = new MemoryStream(state, writable: false))
            using (var r = new BinaryReader(buffer, Encoding.UTF8))
            {
                restored.ReadState(r);
                Assert.Equal(state.Length, buffer.Position);
            }

            Organism after = null;
            foreach (Organism o in restored.Living)
            {
                if (o.Id == before.Id) after = o;
            }

            Assert.NotNull(after);
            Assert.Equal(before.Phenotype.ScaledBy, after.Phenotype.ScaledBy);
            Assert.Equal(powersBefore, Powers(after.Phenotype));
            Assert.Equal(adultPowers, Powers(after.AdultPhenotype));
            _output.WriteLine($"scale {scale}: adult {adultPowers[1]}, juvenile {powersBefore[1]}, restored {Powers(after.Phenotype)[1]}");
        }
    }
}
