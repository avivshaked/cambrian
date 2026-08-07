using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// Finds §5A.2's knob by sweeping it — DESIGN.md §5A.2b, §5A.6b, D023.
    /// </summary>
    /// <remarks>
    /// <para>
    /// §5A.2 states the problem as a callout: <i>"The ratio of basal metabolism to peak
    /// photosynthesis. If sunlight alone covers upkeep anywhere in the world, nothing there ever
    /// has to move, and the world becomes a photosynthetic mat. It has to not quite cover it."</i>
    /// Nobody knows the value and nobody can reason to it.
    /// </para>
    /// <para>
    /// <b>It does not have to be known — only located.</b> Below the transition nothing
    /// reproduces and the floor carries the world; above it a population establishes and holds.
    /// Sweeping makes the transition announce itself.
    /// </para>
    /// <para>
    /// <b>The first version of this sweep gave a confidently wrong answer, twice over, and both
    /// mistakes are worth keeping in view</b> (logbook/0011). It ran each world for 4,000 s and
    /// reported the setting at 48 W/m² as a stable floor-fed world; that world was on a clean
    /// exponential and blew past the ceiling at t=5,303 s. <b>A truncated run cannot tell a steady
    /// state from an exponential caught early</b>, so every run here is long enough for the
    /// population to turn over many times, and stability is asserted rather than assumed. And it
    /// searched for a transition that did not exist at all: without <see cref="LightField"/>,
    /// income does not depend on how many others there are, so a population above break-even grows
    /// without bound at <i>every</i> setting. The knob decided only how fast.
    /// </para>
    /// </remarks>
    public class CalibrationSweep
    {
        private readonly ITestOutputHelper _output;

        public CalibrationSweep(ITestOutputHelper output) => _output = output;

        private const float StepSeconds = 1f;
        private const int Steps = 20000;

        /// <remarks>
        /// Far above anything the light budget supports, because this is a runaway detector and
        /// not a population target. Set near the carrying capacity it stops the world at settings
        /// that were merely dense — 96 W/m² tripped a ceiling of 1,500 while being perfectly
        /// bounded, and read as a runaway on the first pass.
        /// </remarks>
        private const int Ceiling = 50000;

        private static RunConfig Config() => new RunConfig
        {
            MinimumPopulation = 30,
            MaximumPopulation = Ceiling,
            FloorSpawnsPerStep = 2,
        };

        private static float ShadowArea(World world)
        {
            float area = 0f;
            for (int i = 0; i < world.Living.Count; i++)
            {
                area += world.Living[i].Phenotype.TotalLitArea;
            }
            return area;
        }

        private readonly struct Result
        {
            public bool Runaway { get; }
            public WorldSample Sample { get; }
            public float Shadow { get; }
            public int PeakPopulation { get; }

            public Result(bool runaway, WorldSample sample, float shadow, int peak)
            {
                Runaway = runaway;
                Sample = sample;
                Shadow = shadow;
                PeakPopulation = peak;
            }

            /// <summary>
            /// A lineage established and outlived its founders — the world is doing something.
            /// </summary>
            /// <remarks>
            /// Keyed on depth rather than on population, because the floor holds the population up
            /// whatever happens: a dead world and a living one both report thirty creatures. Depth
            /// separates them, since only reproduction produces it.
            /// </remarks>
            public bool Reproducing => !Runaway && Sample.MedianDepth > 1;
        }

        private static Result RunOne(float surfaceIrradiance, ulong seed)
        {
            var world = new World(Config(), new LightModel(surfaceIrradiance, 12f), seed);
            int peak = 0;

            try
            {
                for (int i = 0; i < Steps; i++)
                {
                    world.Step(StepSeconds);
                    if (world.Living.Count > peak) peak = world.Living.Count;
                }
            }
            catch (PopulationRunawayException)
            {
                return new Result(true, WorldStats.Sample(world), ShadowArea(world), peak);
            }

            return new Result(false, WorldStats.Sample(world), ShadowArea(world), peak);
        }

        [Fact]
        public void TheTransitionExistsAndThisIsWhereItIs()
        {
            // Stops at 48 W/m², and that is a deliberate division of labour rather than a gap.
            // What this test locates is the transition, which lives down here in worlds of tens
            // to hundreds of creatures. Everything above it costs a hundred times as much to step
            // and was only ever witnessing that nothing runs away — a claim
            // BiomassIsCappedByLightRatherThanByUs now makes properly, by watching the quantity
            // light actually caps instead of waiting for a ceiling that might never be reached.
            float[] irradiances = { 4f, 8f, 16f, 24f, 32f, 48f };

            _output.WriteLine(
                $"{Steps} s per world, attenuation 1/e at 12 m, floor 30, ceiling {Ceiling}, " +
                "3 seeds each.");
            _output.WriteLine("");
            _output.WriteLine(
                "| W/m² | reproducing | pop | peak | depth med/max | shadow m² | ×world | floor | births |");
            _output.WriteLine("|---|---|---|---|---|---|---|---|---|");

            float worldArea = Config().WorldAreaSquareMetres;
            var reproducingAt = new List<float>();
            var outcomes = new List<bool>();
            bool anyRunaway = false;

            foreach (float irradiance in irradiances)
            {
                for (ulong seed = 1; seed <= 3; seed++)
                {
                    Result r = RunOne(irradiance, seed);
                    if (r.Reproducing) reproducingAt.Add(irradiance);
                    if (r.Runaway) anyRunaway = true;
                    outcomes.Add(r.Reproducing);

                    _output.WriteLine(
                        $"| {irradiance:0.#} | {(r.Runaway ? "**RUNAWAY**" : r.Reproducing ? "yes" : "no")} | " +
                        $"{r.Sample.Population} | {r.PeakPopulation} | " +
                        $"{r.Sample.MedianDepth}/{r.Sample.MaxDepth} | {r.Shadow:0} | " +
                        $"{r.Shadow / worldArea:0.#} | {r.Sample.FloorSpawns} | {r.Sample.Births} |");
                }
            }

            _output.WriteLine("");

            if (reproducingAt.Count > 0)
            {
                _output.WriteLine(
                    $"**lowest irradiance that establishes a lineage: {reproducingAt[0]:0.#} W/m²**");
            }

            // Identical outcomes across a 100x sweep would mean the knob is not reaching what it
            // configures, which is the failure this project has already had twice and which looks
            // exactly like "the parameter does not matter" (logbook/0007, logbook/0008).
            Assert.NotEmpty(reproducingAt);
            Assert.True(
                reproducingAt.Count < outcomes.Count,
                "every setting established a lineage, so the sweep straddles no transition — " +
                "extend it downwards until something fails to reproduce");

            // The property light competition exists to provide (§5A.2b). Before it, every world
            // above break-even grew without bound; a runaway now means the cap is not binding.
            Assert.False(
                anyRunaway,
                $"a world exceeded {Ceiling} creatures. Light is finite, so income per creature " +
                "must fall as the population rises — if it did not, LightField is not reaching " +
                "the metabolic path.");
        }

        [Fact]
        public void BiomassIsCappedByLightRatherThanByUs()
        {
            // §5A.2b's whole claim, asserted on the quantity it is actually about. In steady
            // state the world's metabolic burn equals its light income, and burn is proportional
            // to tissue — so *living biomass* is what the finite sun caps. Population is not: two
            // worlds of the same biomass can hold forty giants or eight thousand motes, and after
            // §5A.2c made bodies cost something to build, the same world moved from one to the
            // other. Watching population made a converged world look like a runaway (logbook/0012).
            var config = new RunConfig
            {
                MinimumPopulation = 30,
                MaximumPopulation = Ceiling,
                FloorSpawnsPerStep = 2,

                // A quarter of the default aperture, purely so this finishes: the claim is a
                // ratio between two windows of the same run and does not care how big the world
                // is. A smaller sun feeds proportionally less life, which is itself the point.
                WorldAreaSquareMetres = 100f,
            };

            var world = new World(config, new LightModel(96f, 12f), seed: 1);

            for (int i = 0; i < 10000; i++) world.Step(1f);
            float early = Biomass(world);
            int earlyPopulation = world.Living.Count;

            for (int i = 0; i < 10000; i++) world.Step(1f);
            float late = Biomass(world);

            _output.WriteLine(
                $"t=10000: {earlyPopulation} creatures, {early:0.#} m³ of tissue");
            _output.WriteLine(
                $"t=20000: {world.Living.Count} creatures, {late:0.#} m³ of tissue");
            _output.WriteLine(
                $"population x{(float)world.Living.Count / Math.Max(1, earlyPopulation):0.##}, " +
                $"biomass x{late / Math.Max(1e-6f, early):0.##}");
            _output.WriteLine(
                $"detritus {world.Nutrients.TotalJoules:0} J, of which " +
                $"{world.Nutrients.StockInLayer(world.Nutrients.LayerCount - 1) / Math.Max(1.0, world.Nutrients.TotalJoules):P0} " +
                "has settled on the floor and nothing lives there yet");

            Assert.True(early > 0f, "nothing was alive to measure");

            // Doubling the elapsed time must not double the standing tissue. Loose, because this
            // is a stochastic world and the claim is "bounded", not "converged to three figures".
            Assert.InRange(late / early, 0.5f, 1.5f);

            // And the books still close with a whole food web running through them.
            Assert.True(
                Math.Abs(world.AuditResidual) / Math.Max(1.0, world.EnergyIn) < 1e-4,
                $"energy is not conserved: {world.AuditResidual:0.###} J unaccounted for");
        }

        private static float Biomass(World world)
        {
            float volume = 0f;
            for (int i = 0; i < world.Living.Count; i++)
            {
                volume += world.Living[i].Phenotype.TotalVolume;
            }
            return volume;
        }

        [Fact]
        public void SizeDecidesWhoCanLiveOnLight()
        {
            // A consequence of the geometry that nobody put in and that constrains the whole
            // design: income scales with surface area and upkeep with volume, so income/upkeep
            // falls as 1/size. There is a largest creature that can pay for itself on light, and
            // it is set by the surface-area-to-volume law rather than by anything we chose.
            var config = new RunConfig();
            var light = new LightModel(24f, 12f);

            _output.WriteLine("| half-extent m | lit area m² | volume m³ | income W | upkeep W | ratio |");
            _output.WriteLine("|---|---|---|---|---|---|");

            float previousRatio = float.MaxValue;

            foreach (float h in new[] { 0.05f, 0.1f, 0.15f, 0.2f, 0.3f, 0.4f, 0.6f, 1.0f })
            {
                Phenotype p = Developer.Develop(
                    Sheet(h, h, h), config.Development, null, config.Shapes);

                EnergyLedger ledger = Metabolism.Step(p, config, light, 0f, 0f, 0f, 1f);
                float ratio = ledger.Income / Math.Max(1e-9f, ledger.Expenditure);

                _output.WriteLine(
                    $"| {h:0.##} | {p.Parts[0].LitArea:0.####} | {p.Parts[0].Volume:0.####} | " +
                    $"{ledger.Income:0.###} | {ledger.Expenditure:0.###} | **{ratio:0.##}** |");

                Assert.True(ratio < previousRatio, "income/upkeep must fall as a body grows");
                previousRatio = ratio;
            }

            _output.WriteLine("");
            _output.WriteLine(
                "Ratio below 1 means the body cannot pay for itself on light at this irradiance. " +
                "Nothing imposes that limit — it is surface area against volume, and it is why " +
                "a photosynthetic strategy favours small or flat bodies without anyone saying so.");
        }

        [Fact]
        public void FlatnessPaysAndThatIsWhyThicknessHasAFloor()
        {
            // The other half of the same law, and the one that bit. Holding volume fixed and
            // flattening a body raises its lit area without raising its upkeep at all, so income
            // per joule of upkeep grows without limit as thickness falls. Left unbounded, evolution
            // takes it to the end: shadow areas reached 10^37 m² in a 400 m² world (logbook/0011).
            var config = new RunConfig();
            var light = new LightModel(24f, 12f);

            _output.WriteLine("| thickness m | lit area m² | volume m³ | income/upkeep |");
            _output.WriteLine("|---|---|---|---|");

            const float Volume = 0.216f;   // the 0.3 m cube above, reshaped
            float previousRatio = 0f;
            float thinnest = 0f;

            foreach (float t in new[] { 0.3f, 0.1f, 0.03f, 0.01f, 0.003f, 0.001f })
            {
                // Half-extents (t, w, w) with 8*t*w*w == Volume.
                float w = (float)Math.Sqrt(Volume / (8f * t));

                Phenotype p = Developer.Develop(
                    Sheet(t, w, w), config.Development, null, config.Shapes);

                EnergyLedger ledger = Metabolism.Step(p, config, light, 0f, 0f, 0f, 1f);
                float ratio = ledger.Income / Math.Max(1e-9f, ledger.Expenditure);

                _output.WriteLine(
                    $"| {t:0.####} | {p.Parts[0].LitArea:0.###} | {p.Parts[0].Volume:0.####} | " +
                    $"**{ratio:0.##}** |");

                if (t >= config.Development.MinPartHalfExtent)
                {
                    Assert.True(ratio > previousRatio, "flattening at fixed volume must pay");
                    previousRatio = ratio;
                }

                thinnest = p.Parts[0].HalfExtents.X;
            }

            _output.WriteLine("");
            _output.WriteLine(
                $"Thickness is clamped at {config.Development.MinPartHalfExtent:0.###} m, so the " +
                $"thinnest body built was {thinnest:0.###} m and the ratio stops climbing there. " +
                "Nothing in the economy stops it — an area-proportional upkeep would not either, " +
                "since income and that cost both scale linearly with area and their difference " +
                "still grows. What actually bounds a body is that the world's light runs out " +
                "(§5A.2b); this floor only keeps the arithmetic representable on the way.");

            Assert.Equal(config.Development.MinPartHalfExtent, thinnest, 5);
        }

        /// <summary>A single photosynthetic box of the given half-extents.</summary>
        private static Genome Sheet(float x, float y, float z)
        {
            var genome = new Genome { RootIndex = 0 };
            genome.Nodes.Add(new MorphNode
            {
                CellTypeId = CellTypeIds.Photosynthetic,
                ShapeId = ShapeIds.Box,
                Dimensions = new Float3(x, y, z),
                JointType = JointType.Fixed,
                JointLimits = Array.Empty<Float2>(),
                RecursiveLimit = 1,
                Neurons = Array.Empty<NeuronDef>(),
            });
            return genome;
        }
    }
}
