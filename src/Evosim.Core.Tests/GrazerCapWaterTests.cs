using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The gripping grazer control's water read (<c>logbook/specs/grazer-grip-control-plan.md</c>): how fast the
    /// water moves just above a reef cap's table, against open water at the same height, in round 57's world on
    /// seed 1, the control's seed, so its reefs are the control's. A measurement, not a guard: it prints its
    /// table, and asserts only that the reefs' fade slows the water over the caps.
    /// </summary>
    public sealed class GrazerCapWaterTests
    {
        private readonly ITestOutputHelper _out;

        public GrazerCapWaterTests(ITestOutputHelper output) => _out = output;

        private static readonly float[] Heights = { 0.1f, 0.3f, 0.57f, 1f, 2f };

        [Fact]
        [Trait("Category", "Slow")]
        public void TheWaterAboveACapsTableIsSlowerThanOpenWaterAtItsHeight()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "fixtures", "r57-s2-config.json");
            RunConfig config = RunConfigJson.Read(File.ReadAllText(path), out string mismatch);
            Assert.Null(mismatch);
            config.FoundingTricklePoolShare = 0f;
            config.FoundingFloorPoolShare = 0f;
            config.FoundingTricklePoolCount = 0;
            config.FoundingTricklePoolHash = "";
            config.FoundingTricklePerSecond = 0f;
            config.MinimumPopulation = 0;

            var world = new World(config, seed: 1);
            ReefGeometry reefs = world.Reefs;
            CurrentField current = world.Config.Current;
            Assert.NotNull(reefs);
            float r = world.TankRadiusMetres;
            _out.WriteLine(FormattableString.Invariant(
                $"tank radius {r:0.0} m, {reefs.Count} reefs, cap thickness {reefs.CapThicknessMetres} m"));

            // Over each cap: its centre and four points at half its outline's radius. Open water: points of the
            // tank at the same heights, outside every cap's outline, from a fixed grid.
            var caps = new List<(int reef, double x, double z, double top)>();
            for (int k = 0; k < reefs.Count; k++)
            {
                if (reefs.IsIsland(k)) continue;
                double cx = reefs.CentreX(k), cz = reefs.CentreZ(k), top = reefs.CapTopY(k);
                caps.Add((k, cx, cz, top));
                for (int q = 0; q < 4; q++)
                {
                    double th = q * Math.PI / 2d;
                    double rr = 0.5d * reefs.OutlineRadius(k, th);
                    caps.Add((k, cx + rr * Math.Cos(th), cz + rr * Math.Sin(th), top));
                }
            }

            caps = caps.Where(c => reefs.InsideOutline(c.reef, c.x, c.z)).ToList();
            _out.WriteLine(FormattableString.Invariant(
                $"{caps.Count} points over {caps.Select(c => c.reef).Distinct().Count()} caps, tops from {caps.Min(c => c.top):0.00} to {caps.Max(c => c.top):0.00} m"));

            var open = new List<(double x, double z)>();
            for (double gx = 0.05; gx < 1; gx += 0.1)
            {
                for (double gz = 0.05; gz < 1; gz += 0.1)
                {
                    double x = 2d * r * gx, z = 2d * r * gz;
                    if ((x - r) * (x - r) + (z - r) * (z - r) > 0.8d * r * 0.8d * r) continue;
                    bool over = false;
                    for (int k = 0; k < reefs.Count && !over; k++) over = reefs.InsideOutline(k, x, z);
                    if (!over) open.Add((x, z));
                }
            }

            _out.WriteLine($"{open.Count} open-water points");
            _out.WriteLine("height over the table | over the caps: mean, max speed (m/s), mean fade | open water at the same y: mean, max speed");

            double capAtGrazer = 0, openAtGrazer = 0;
            foreach (float h in Heights)
            {
                double capSum = 0, capMax = 0, fadeSum = 0, openSum = 0, openMax = 0;
                int capN = 0, openN = 0;
                for (double t = 12000; t <= 18000; t += 500)
                {
                    foreach (var c in caps)
                    {
                        float y = (float)(c.top + h);
                        double s = Speed(current.VelocityAt((float)c.x, y, (float)c.z, t));
                        capSum += s; capMax = Math.Max(capMax, s); capN++;
                        fadeSum += reefs.FadeAt(c.x, y, c.z);
                        foreach (var o in open)
                        {
                            double so = Speed(current.VelocityAt((float)o.x, y, (float)o.z, t));
                            openSum += so; openMax = Math.Max(openMax, so); openN++;
                        }
                    }
                }

                double capMean = capSum / capN, openMean = openSum / openN;
                if (h == 0.57f) { capAtGrazer = capMean; openAtGrazer = openMean; }
                _out.WriteLine(FormattableString.Invariant(
                    $"{h:0.00} m | {capMean:0.00000}, {capMax:0.00000}, {fadeSum / capN:0.0000} | {openMean:0.00000}, {openMax:0.00000}"));
            }

            Assert.True(capAtGrazer < openAtGrazer, "the water over the caps is not slower than open water");
        }

        private static double Speed(Float3 v) => Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
    }
}
