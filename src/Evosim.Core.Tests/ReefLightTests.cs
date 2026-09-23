using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The caps' shadow — <c>logbook/specs/reef-spec.md</c> §4, test 5: directly under a cap the
    /// irradiance is the open water's times the cap's transmission; beside the cap it is the open
    /// water's; the stem casts nothing. Printed in W/m² at round 46's light (200 W/m² at the
    /// surface, a 6 m attenuation depth), which the owner reads.
    /// </summary>
    public class ReefLightTests
    {
        private readonly ITestOutputHelper _output;

        public ReefLightTests(ITestOutputHelper output) => _output = output;

        [Fact]
        public void UnderTheCapTheLightIsShadedAndBesideItIsNot()
        {
            // Round 46's reef: a cap 4 m in radius, 2 m thick with its top at 3 m, on a 1 m stem.
            var reefs = new ReefGeometry(4f, 3f, 2f, 1f, 15f, new[] { 60d }, new[] { 60d });

            var field = new LightField(new LightModel(200f, 6f), 22000f, 1f) { Reefs = reefs };
            var open = new LightField(new LightModel(200f, 6f), 22000f, 1f);

            float cx = 60f, cz = 60f;
            float besideX = cx + 4f + 2f;
            float stemSideX = cx + 1.5f;

            float surface = field.IrradianceAt(-0.001f, 0, cx, cz);
            float onTop = field.IrradianceAt(-2.99f, 0, cx, cz);
            Assert.Equal(open.IrradianceAt(-2.99f, 0, cx, cz), onTop);

            _output.WriteLine(
                $"cap transmission {ReefGeometry.CapTransmission:0.0000} (opaque; the canopy arithmetic would give e^-1 = {LightField.Transmitted(1d):0.0000}); surface {surface:0.00} W/m², " +
                $"on the cap's top at 2.99 m {onTop:0.00} W/m²");

            foreach (float depth in new[] { 5.5f, 8f, 15f })
            {
                float y = -depth;
                float under = field.IrradianceAt(y, 0, cx, cz);
                float underRim = field.IrradianceAt(y, 0, cx + 3.9f, cz);
                float stemSide = field.IrradianceAt(y, 0, stemSideX, cz);
                float beside = field.IrradianceAt(y, 0, besideX, cz);
                float water = open.IrradianceAt(y, 0, cx, cz);

                _output.WriteLine(
                    $"at {depth} m: open water {water:0.00} W/m²; under the cap's centre {under:0.00}, under its rim " +
                    $"{underRim:0.00}, beside the stem {stemSide:0.00}; beside the cap (2 m past its rim) {beside:0.00}");

                Assert.Equal(water * ReefGeometry.CapTransmission, under, 4);
                Assert.Equal(under, underRim);
                Assert.Equal(under, stemSide);
                Assert.Equal(water, beside);
            }

            Assert.Equal(0f, ReefGeometry.CapTransmission);
        }

        [Fact]
        public void TheWorldHandsItsReefsToTheLight()
        {
            RunConfig config = ReefTank.Config();
            var world = new World(config, seed: 3);

            Assert.Same(world.Reefs, world.Field.Reefs);

            float cx = (float)world.Reefs.CentreX(0), cz = (float)world.Reefs.CentreZ(0);
            float under = world.Field.IrradianceAt(-6f, 0, cx, cz);

            // Beside it: the first point east of the axis outside every cap's outline.
            float bx = cx;
            while (world.Reefs.LightTransmission(bx, -6f, cz) < 1f) bx += 0.25f;
            float beside = world.Field.IrradianceAt(-6f, 0, bx, cz);

            _output.WriteLine(
                $"small tank at 6 m, {world.Reefs.Count} reefs: under a cap {under:0.00} W/m², {bx - cx:0.00} m east " +
                $"of its axis and clear of every cap {beside:0.00} W/m²");
            Assert.True(under < beside);
        }

        [Fact]
        public void AnIrregularCapShadesItsOwnOutlineAndOverlapsShadeOnce()
        {
            // Under a lobed cap the water is dark exactly inside the outline and below that cap's
            // own top; the lens where two caps overlap is dark the same, not darker; outside every
            // outline the water is the open water's.
            ReefGeometry reefs = ReefTank.Overlapping();
            var field = new LightField(new LightModel(200f, 6f), ReefTank.Area, 1f) { Reefs = reefs };
            var open = new LightField(new LightModel(200f, 6f), ReefTank.Area, 1f);

            int dark = 0, lit = 0;

            for (int reef = 0; reef < reefs.Count; reef++)
            {
                ReefGeometry.Reef r = reefs.ReefAt(reef);
                float below = (float)(-r.CapDepth - 0.01d);
                float above = (float)(-r.CapDepth + 0.01d);

                for (int k = 0; k < 72; k++)
                {
                    double theta = 2d * Math.PI * k / 72d;
                    double o = reefs.OutlineRadius(reef, theta);
                    float ix = (float)(r.X + (o - 0.05d) * Math.Cos(theta)), iz = (float)(r.Z + (o - 0.05d) * Math.Sin(theta));
                    float ox = (float)(r.X + (o + 0.05d) * Math.Cos(theta)), oz = (float)(r.Z + (o + 0.05d) * Math.Sin(theta));

                    Assert.Equal(0f, field.IrradianceAt(below, 0, ix, iz));
                    dark++;

                    // Just over this cap's top the point is lit, unless a higher cap covers it.
                    bool shadedFromAbove = false;
                    for (int other = 0; other < reefs.Count; other++)
                    {
                        if (other != reef && reefs.CapTopY(other) > above && reefs.InsideOutline(other, ix, iz)) shadedFromAbove = true;
                    }

                    if (!shadedFromAbove)
                    {
                        Assert.Equal(open.IrradianceAt(above, 0, ix, iz), field.IrradianceAt(above, 0, ix, iz));
                    }

                    bool underOther = false;
                    for (int other = 0; other < reefs.Count; other++)
                    {
                        if (other != reef && reefs.InsideOutline(other, ox, oz)) underOther = true;
                    }

                    if (underOther) continue;
                    Assert.Equal(open.IrradianceAt(-8f, 0, ox, oz), field.IrradianceAt(-8f, 0, ox, oz));
                    lit++;
                }
            }

            float lensX = ReefTank.Radius, lensZ = ReefTank.Radius;
            Assert.True(reefs.InsideOutline(0, lensX, lensZ) && reefs.InsideOutline(1, lensX, lensZ));
            Assert.Equal(0f, field.IrradianceAt(-8f, 0, lensX, lensZ));
            Assert.Equal(ReefGeometry.CapTransmission, reefs.LightTransmission(lensX, -8f, lensZ));

            _output.WriteLine($"{dark} points just inside an outline dark below its cap and lit above it; {lit} just outside lit as open water");
        }
    }
}
