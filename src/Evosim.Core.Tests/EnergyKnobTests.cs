using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// Everything that costs or earns energy must be settable, saved, and in the hash —
    /// DESIGN.md §5A.10, §7.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The requirement is not "we checked".</b> §5A.10's rule is that an unmeasured number must
    /// be sweepable, and almost every number in §5A is unmeasured — so a cost baked into a class
    /// is an assumption nobody can test, and a cost that varies without reaching
    /// <see cref="RunConfig.Hash"/> is two different experiments filed under one identity. Both
    /// have happened: the consumer scavenging rate shipped as a hardcoded coefficient of 1, and
    /// <see cref="LightModel"/> — carrying §5A.2's <i>knob that decides everything</i> — was
    /// handed to the world <i>beside</i> the config rather than inside it, so every run of the
    /// §5A.2b calibration sweep, from the extinct end to the runaway end, shared one
    /// <c>configHash</c> (logbook/0013).
    /// </para>
    /// <para>
    /// <b>These work through the JSON rather than through reflection on properties</b>, and that is
    /// what makes them general. A cell type's parameters are constructor-only, so nothing can nudge
    /// them from outside — but the file is exactly the surface a person configures a run through,
    /// so mutating a value there and demanding the hash move proves the whole chain at once:
    /// writable, readable, and identifying. A knob missing from any of the three fails.
    /// </para>
    /// </remarks>
    public class EnergyKnobTests
    {
        private readonly ITestOutputHelper _output;

        public EnergyKnobTests(ITestOutputHelper output) => _output = output;

        /// <summary>Matches <c>"name": 1.5</c> in the writer's output, capturing the number.</summary>
        private static readonly Regex NumericField =
            new Regex("\"(?<name>[A-Za-z0-9_]+)\"\\s*:\\s*(?<value>-?[0-9]+(\\.[0-9]+)?([eE][-+]?[0-9]+)?)");

        [Fact]
        public void EveryTunableOnEveryCellTypeIsSavedLoadedAndHashed()
        {
            var missed = new List<string>();
            int checkedCount = 0;

            foreach (string id in CellTypeRegistry.Standard.Ids())
            {
                CellType type = CellTypeRegistry.Standard.Resolve(id);

                var writer = new Json.Writer(indent: false);
                CellTypeJson.Write(writer, type);
                string json = writer.ToString();

                string baseline = type.FullHashContribution();

                foreach (Match m in NumericField.Matches(json))
                {
                    string name = m.Groups["name"].Value;
                    float value = float.Parse(
                        m.Groups["value"].Value, CultureInfo.InvariantCulture);

                    // Moved to something different but still legal: yields live in [0, 1] and
                    // several rates must stay positive, so halving is the one nudge that is
                    // in range for every parameter any of these types takes.
                    float nudged = value == 0f ? 0.25f : value * 0.5f;

                    string mutated =
                        json.Substring(0, m.Index) +
                        $"\"{name}\": {nudged.ToString("R", CultureInfo.InvariantCulture)}" +
                        json.Substring(m.Index + m.Length);

                    CellType reloaded = CellTypeJson.Read(Json.Parse(mutated));
                    checkedCount++;

                    if (reloaded.FullHashContribution() == baseline) missed.Add($"{id}.{name}");
                }
            }

            _output.WriteLine($"{checkedCount} cell-type tunables checked across the standard registry");
            if (missed.Count > 0) _output.WriteLine("not reaching the hash: " + string.Join(", ", missed));

            // A registry that exposed nothing numeric would pass by testing nothing.
            Assert.True(checkedCount >= 12, $"only {checkedCount} tunables found — the walk has stopped working");
            Assert.Empty(missed);
        }

        [Fact]
        public void EveryCellTypeSurvivesASaveAndReload()
        {
            // The other half: a parameter that reaches the hash but is never written is a run
            // whose settings cannot be recovered from its own directory. §9's rule is that loading
            // refuses rather than defaults, and a value silently absent defeats it.
            foreach (string id in CellTypeRegistry.Standard.Ids())
            {
                CellType original = CellTypeRegistry.Standard.Resolve(id);

                var writer = new Json.Writer(indent: false);
                CellTypeJson.Write(writer, original);

                CellType reloaded = CellTypeJson.Read(Json.Parse(writer.ToString()));

                Assert.Equal(original.FullHashContribution(), reloaded.FullHashContribution());
            }
        }

        [Fact]
        public void TheLightModelReachesTheConfigHash()
        {
            // §5A.2 calls this the knob that decides everything, and for the whole of the §5A.2b
            // sweep it was invisible to §7: LightModel was passed to World alongside a config
            // rather than inside one, so a world at 4 W/m² and a world at 400 hashed identically.
            var dim = new RunConfig { Light = new LightModel(24f, 12f) };
            var bright = new RunConfig { Light = new LightModel(400f, 12f) };
            var shallow = new RunConfig { Light = new LightModel(24f, 3f) };

            Assert.NotEqual(dim.Hash(), bright.Hash());
            Assert.NotEqual(dim.Hash(), shallow.Hash());
        }

        [Fact]
        public void AWorldCannotRunOnLightItsConfigDoesNotKnowAbout()
        {
            // The structural half of the same fix. There is one place a world's light can come
            // from, so the run and the record of the run cannot disagree.
            var config = new RunConfig { Light = new LightModel(48f, 12f) };
            var world = new World(config, seed: 1);

            Assert.Same(config.Light, world.Light);
            Assert.Equal(48f, world.Field.Model.SurfaceIrradiance);

            Assert.Throws<ArgumentException>(() => new World(new RunConfig { Light = null }));
        }

        [Fact]
        public void EveryCostInTheLedgerRespondsToAConfigChange()
        {
            // End to end, on the thing that actually charges: build one creature and check that
            // each term of §5A.2's expenditure moves when its own knob moves. A tunable that is
            // settable, saved and hashed but never read would pass every other test here.
            Genome genome = Fixtures.SelfLoopSpine(3);

            float Upkeep(Action<RunConfig> tune)
            {
                var config = new RunConfig();
                tune(config);

                Phenotype body = Developer.Develop(
                    genome, config.Development, null, config.Shapes);

                return Metabolism.StandingWatts(body, config);
            }

            float baseline = Upkeep(_ => { });

            // Tissue upkeep, per cell type.
            float dearerTissue = Upkeep(c => c.CellTypes = Registry(upkeepScale: 3f));
            Assert.True(dearerTissue > baseline, "tripling cell upkeep changed nothing");

            // Neurons, and their connections, separately.
            float dearerNeurons = Upkeep(c => c.NeuralCostPerNeuronWatts *= 10f);
            float dearerSynapses = Upkeep(c => c.NeuralCostPerConnectionWatts *= 10f);

            _output.WriteLine(
                $"baseline {baseline:0.####} W, tissue x3 {dearerTissue:0.####}, " +
                $"neurons x10 {dearerNeurons:0.####}, connections x10 {dearerSynapses:0.####}");

            Assert.True(dearerNeurons >= baseline);
            Assert.True(dearerSynapses >= baseline);

            // Work is charged by the caller, so it is checked against a ledger rather than the
            // standing cost — StandingWatts is defined as the cost of doing nothing.
            var idle = new RunConfig();
            var costly = new RunConfig { WorkCostMultiplier = 4f };
            Phenotype phenotype = Developer.Develop(genome, idle.Development, null, idle.Shapes);

            float cheapWork = Metabolism.StepAt(phenotype, idle, 0f, 0f, 100f, 1f).Work;
            float dearWork = Metabolism.StepAt(phenotype, costly, 0f, 0f, 100f, 1f).Work;

            Assert.True(dearWork > cheapWork, "the work coefficient never reached the ledger");
            Assert.Equal(cheapWork * 4f, dearWork, 3);
        }

        [Fact]
        public void EveryIncomeInTheLedgerRespondsToAConfigChange()
        {
            // Income is a cost seen from the other side: it decides what a body can afford, so it
            // belongs under the same rule.
            var config = new RunConfig();
            Genome genome = Leaf();
            Phenotype body = Developer.Develop(genome, config.Development, null, config.Shapes);

            float Light(float irradiance) =>
                Metabolism.StepAt(body, config, irradiance, 0f, 0f, 1f).LightIncome;

            Assert.True(Light(100f) > Light(10f), "irradiance never reached photosynthesis");

            var efficient = new RunConfig { CellTypes = Registry(photosynthesisScale: 4f) };
            Phenotype same = Developer.Develop(genome, efficient.Development, null, efficient.Shapes);

            Assert.True(
                Metabolism.StepAt(same, efficient, 100f, 0f, 0f, 1f).LightIncome >
                Metabolism.StepAt(body, config, 100f, 0f, 0f, 1f).LightIncome,
                "photosynthetic efficiency never reached the ledger");

            // And feeding, including the loss on transfer that a consumer pays and a filter
            // feeder does not.
            var greedy = new ConsumerCell(biteRate: 20f, carrionYield: 0.8f);
            var wasteful = new ConsumerCell(biteRate: 20f, carrionYield: 0.1f);

            var context = new CellContext(1f, volume: 1f, nutrientDensity: 5f);

            CellIntake rich = greedy.Acquire(context);
            CellIntake poor = wasteful.Acquire(context);

            _output.WriteLine($"carrion yield 0.8 → {rich}, yield 0.1 → {poor}");

            Assert.True(rich.FromPool > poor.FromPool, "carrion yield never reached feeding");
            Assert.Equal(rich.PoolDrawn, poor.PoolDrawn, 4);

            // Scavenging searched water at a hardcoded rate until logbook/0013.
            var slow = new ConsumerCell(biteRate: 1e6f, scavengeRate: 0.1f);
            var fast = new ConsumerCell(biteRate: 1e6f, scavengeRate: 10f);

            Assert.True(
                fast.Acquire(context).FromPool > slow.Acquire(context).FromPool,
                "the scavenging rate never reached feeding");
        }

        /// <summary>One photosynthetic box — the simplest thing that can earn anything.</summary>
        private static Genome Leaf()
        {
            var genome = new Genome { RootIndex = 0 };
            genome.Nodes.Add(new MorphNode
            {
                CellTypeId = CellTypeIds.Photosynthetic,
                ShapeId = ShapeIds.Box,
                Dimensions = new Float3(0.2f, 0.2f, 0.2f),
                JointType = JointType.Fixed,
                JointLimits = Array.Empty<Float2>(),
                RecursiveLimit = 1,
                Neurons = Array.Empty<NeuronDef>(),
            });
            return genome;
        }

        /// <summary>The standard registry with one dial turned, for comparisons.</summary>
        private static CellTypeRegistry Registry(
            float upkeepScale = 1f, float photosynthesisScale = 1f)
        {
            return new CellTypeRegistry(
                new StructuralCell(1f * upkeepScale),
                new LinkCell(0.02f, 2.5f * upkeepScale),
                new NeuralCell(400f, 0.2f, 5f * upkeepScale),
                new PhotosyntheticCell(0.05f * photosynthesisScale, 3f * upkeepScale),
                new AbsorptiveCell(0.5f, 4f * upkeepScale),
                new ConsumerCell(20f, 6f * upkeepScale));
        }
    }
}
