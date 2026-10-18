using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// D127's corpse, as the farm records it for a picture: the pose its body died in, the body's
    /// size and plan, all handed over as the solver lets the body go.
    /// </summary>
    public class CorpsePoseTests
    {
        [Fact]
        public void EveryCorpseOfABodyTheSolverSteppedCarriesItsPoseAndItsSize()
        {
            Dictionary<string, string> env = new Dictionary<string, string>
            {
                { "EVOSIM_AREA", "100" },
                { "EVOSIM_DEPTH", "20" },
                { "EVOSIM_FOUNDER_DEPTH", "20" },
                { "EVOSIM_PATCHES", "1" },
                { "EVOSIM_SHARED_SPACE", "1" },
                { "EVOSIM_FIELD", "grid" },
                { "EVOSIM_FIELD_CELL", "2" },
                { "EVOSIM_FIELD_MATTER_CELL", "5" },
                { "EVOSIM_IRRADIANCE", "200" },
                { "EVOSIM_MATTER_BUDGET", "300" },
                { "EVOSIM_TISSUE_ENERGY", "500" },
                { "EVOSIM_RHO", "100" },
                { "EVOSIM_OVERHEAD", "100" },
                { "EVOSIM_DT", "0.02" },
                { "EVOSIM_CORPSE_DECAY", "0.001" },
                { "EVOSIM_CORPSE_SINK", "0.05" },
                { "EVOSIM_CORPSE_SETTLE", "1" },
                { "EVOSIM_AGE_BASE", "10" },
            };

            RunConfig config = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(env)));
            var world = new World(config, 1UL);

            using (var sim = new Simulation(world, 1UL, 0.02f, 25, threads: 2, runDirectory: null))
            {
                // A physics step each, the economy every 25th: 100 s of a world whose median age
                // at death is 10 s.
                for (int i = 0; i < 5_000; i++) sim.Step();

                // One more physics step, whose reconcile lets go of the bodies the last metabolic
                // step killed and hands their corpses the pose; it runs no economy of its own.
                long deathsBefore = world.Deaths;
                sim.Step();
                long lastStepDeaths = world.Deaths - deathsBefore;

                int bodies = 0, posed = 0;
                foreach (Corpse corpse in world.Corpses)
                {
                    if (!(corpse.ScaledBy > 0f)) continue;
                    bodies++;
                    if (!corpse.HasPose) continue;
                    posed++;

                    Quat q = corpse.Rotation;
                    double norm = Math.Sqrt(q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W);
                    Assert.InRange(norm, 0.999, 1.001);
                }

                Assert.True(world.DeathsByAge > 0, "nobody died of age");
                Assert.True(bodies > 10, "too few corpses to read: " + bodies);
                Assert.True(bodies - posed <= lastStepDeaths, $"{bodies - posed} of {bodies} corpses carry no pose");
            }
        }
    }
}
