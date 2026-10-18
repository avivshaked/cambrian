using System.Collections.Generic;
using System.IO;
using System.Text;
using Evosim.Core;
using Evosim.Dynamics;
using Evosim.Farm.Gpu;
using Xunit;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// r59-pos64's measurement, not a guard: free bodies drifting at 0.01, 0.05 and 0.2 mm/s along x near
    /// x = 150 m, with no drag, no weight, no water acceleration and no drive, stepped 1,000 steps at dt 0.01
    /// on the CPU solver and on the engine in single on ILGPU's CPU accelerator. Each body's x displacement
    /// goes to the checkout's scratch/r59-tools/drift-&lt;tag&gt;.txt, the tag from EVOSIM_DRIFT_TAG. About six
    /// minutes, most of it building the 170 m tank's fields.
    /// </summary>
    public class DriftDiagnostic
    {
        private static readonly double[] Speeds = { 1e-5, 5e-5, 2e-4 };

        /// <summary>The checkout's scratch/r59-tools, from the test assembly's artifacts folder.</summary>
        internal static string OutDir()
        {
            string dir = Path.GetFullPath(Path.Combine(System.AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                                                       "scratch", "r59-tools"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        [Fact]
        [Trait("Category", "Slow")]
        public void SlowBodiesDrift()
        {
            string tag = System.Environment.GetEnvironmentVariable("EVOSIM_DRIFT_TAG") ?? "run";
            Drift(new[] { "cpu solver", "accelerator single" }, $"drift-{tag}.txt");
        }

        /// <summary>
        /// D168's read-back on the card itself: the carry survives the driver's own compile when the slow bodies
        /// move there as on the CPU. Folded away, the sum would be the plain one, and a body this slow at x = 150 m
        /// would not move at all. Into scratch/r59-tools/drift-card.txt. It touches the card, so it runs only with
        /// EVOSIM_DRIFT_CARD=1, in a window the coordinator holds, and returns at once otherwise.
        /// </summary>
        [Fact]
        [Trait("Category", "Card")]
        public void SlowBodiesDriftOnTheCard()
        {
            if (System.Environment.GetEnvironmentVariable("EVOSIM_DRIFT_CARD") != "1") return;

            double[] moved = Drift(new[] { "card single" }, "drift-card.txt");
            for (int k = 0; k < Speeds.Length; k++)
            {
                double expect = Speeds[k] * 1000 * 0.01;
                Assert.True(System.Math.Abs(moved[k] - expect) < 0.01 * expect,
                    $"at {Speeds[k] * 1000} mm/s the body moved {moved[k] * 1000} mm on the card, not {expect * 1000} mm");
            }
        }

        private static double[] Drift(string[] engines, string file)
        {
            var settings = new Dictionary<string, string>
            {
                { "EVOSIM_AREA", "90800" },
                { "EVOSIM_DT", "0.01" }, { "EVOSIM_CURRENT", "0.0001" },
            };
            var log = new StringBuilder();
            const int steps = 1000;
            var last = new double[Speeds.Length];

            foreach (string engine in engines)
            {
                (World core, SolverConfig solver) = GpuTank.Build(true, false, extra: settings);
                solver.DragCoefficient = 0;
                solver.TissueExcessDensity = 0;
                solver.FluidAccelerationCoefficient = 0;
                solver.CreatureContact = false;
                var world = new DynamicsWorld(solver) { Threads = 1 };
                Vec3 axis = GpuTank.Axis(core);

                var start = new double[Speeds.Length];
                for (int k = 0; k < Speeds.Length; k++)
                {
                    var at = new Vec3(150.0, -10.0, axis.Z + 5.0 * k);
                    Creature c = GpuTank.Body(k + 1, world.Config, core, new Rng(500UL + (ulong)k), at);
                    c.Drive.PowerScale = 0f;
                    for (int l = 0; l < c.Links; l++)
                    {
                        c.Velocity[3 * l] = Speeds[k];
                        c.Velocity[3 * l + 1] = 0;
                        c.Velocity[3 * l + 2] = 0;
                        c.Spin[3 * l] = 0; c.Spin[3 * l + 1] = 0; c.Spin[3 * l + 2] = 0;
                    }
                    world.AddInIdOrder(c);
                    start[k] = c.BasePosition.X;
                }

                GpuBackend backend = null;
                if (engine != "cpu solver")
                {
                    backend = new GpuBackend(world.Config, new GpuOptions
                    {
                        Device = engine == "card single" ? "cuda" : "cpu", Precision = "single", CpuThreads = 4,
                    });
                    world.UseBackend(backend);
                }

                for (int b = 0; b < steps / 50; b++) world.StepBlock(50);

                log.AppendLine($"== {engine}, {steps} steps at dt 0.01 (axis x {axis.X:F1}), started at x {start[0]:R}");
                for (int k = 0; k < Speeds.Length; k++)
                {
                    Creature c = world.Creatures[k];
                    double moved = c.BasePosition.X - start[k];
                    last[k] = moved;
                    double expect = Speeds[k] * steps * 0.01;
                    log.AppendLine($"  {Speeds[k] * 1000:G3} mm/s: base moved {moved * 1000:F5} mm (drift alone {expect * 1000:F5} mm), " +
                                   $"root link moved {(c.Position[0] - start[k]) * 1000:F5} mm, vx now {c.Velocity[0] * 1000:G4} mm/s");
                }
                backend?.Dispose();
            }

            File.WriteAllText(Path.Combine(OutDir(), file), log.ToString());
            return last;
        }
    }
}
