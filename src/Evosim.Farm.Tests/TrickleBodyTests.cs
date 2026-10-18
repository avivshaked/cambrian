using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// A trickle founder is put into physics at once (2026-10-10). The harness's reconcile count
    /// left D115's trickle out until then, so a founder admitted in a step with no birth, death or
    /// floor spawn had no body until one of those happened.
    /// </summary>
    public class TrickleBodyTests
    {
        private readonly ITestOutputHelper _out;

        public TrickleBodyTests(ITestOutputHelper output) => _out = output;

        /// <summary>
        /// The sparse world the bodiless instrument was read in: the small box, dim, the floor closing
        /// at 5 s and the trickle every 2 s, where no body breeds and most metabolic steps are quiet.
        /// </summary>
        private static RunConfig Sparse() =>
            EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(new Dictionary<string, string>
            {
                { "EVOSIM_AREA", "100" },
                { "EVOSIM_DEPTH", "20" },
                { "EVOSIM_FOUNDER_DEPTH", "20" },
                { "EVOSIM_PATCHES", "1" },
                { "EVOSIM_SHARED_SPACE", "1" },
                { "EVOSIM_FIELD", "grid" },
                { "EVOSIM_FIELD_CELL", "2" },
                { "EVOSIM_FIELD_MATTER_CELL", "5" },
                { "EVOSIM_IRRADIANCE", "40" },
                { "EVOSIM_MATTER_BUDGET", "300" },
                { "EVOSIM_TISSUE_ENERGY", "500" },
                { "EVOSIM_RHO", "100" },
                { "EVOSIM_OVERHEAD", "100" },
                { "EVOSIM_DT", "0.02" },
                { "EVOSIM_FLOOR_CLOSES", "5" },
                { "EVOSIM_TRICKLE", "0.5" },
            })));

        /// <summary>
        /// Over 300 s of the sparse world, a trickle founder arrives in many steps where nothing else
        /// enters or leaves, and the harness never meets a living creature without a body at a
        /// metabolic step. Before the fix the same world read 108 such creatures, the longest 12.5 s.
        /// </summary>
        [Fact]
        public void ATrickleFounderInAQuietStepHasABodyByTheNextMetabolicStep()
        {
            var world = new World(Sparse(), 1UL);

            using (var sim = new Simulation(world, 1UL, 0.02f, 25, threads: 2, runDirectory: null))
            {
                long births = 0, deaths = 0, floor = 0, trickle = 0;
                int alive = 0, quiet = 0;

                while (world.ElapsedSeconds < 300d)
                {
                    if (!sim.Step()) continue;

                    // A founder of the trickle's, admitted, with nothing else entering or leaving.
                    if (world.TrickleSpawns > trickle && world.Births == births && world.Deaths == deaths &&
                        world.FloorSpawns == floor && world.Living.Count > alive)
                    {
                        quiet++;
                    }

                    births = world.Births;
                    deaths = world.Deaths;
                    floor = world.FloorSpawns;
                    trickle = world.TrickleSpawns;
                    alive = world.Living.Count;
                }

                _out.WriteLine(
                    $"{world.TrickleSpawns} trickle founders, {quiet} in a quiet step; bodiless {sim.BodilessCreatures} " +
                    $"creatures, {sim.BodilessCreatureSteps} creature-steps, the longest {sim.LongestBodilessSeconds} s");

                Assert.True(quiet > 10, "too few trickle founders arrived in a quiet step for the test to say anything");
                Assert.Equal(0L, sim.BodilessCreatures);
                Assert.Equal(0d, sim.LongestBodilessSeconds);
            }
        }
    }
}
