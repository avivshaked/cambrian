using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// A small tank with three reefs, shared by the reef tests: 1,600 m² and 12 m deep on a flat
    /// floor, caps 3 m in radius and 2 m thick at 3 m, stems 1 m, a fade of 3 m, the axes on a
    /// ring 8 m from the tank's axis (13.9 m apart, against the 12 m the spacing rule asks).
    /// </summary>
    internal static class ReefTank
    {
        internal const float Area = 1600f;
        internal const float Depth = 12f;
        internal const int Patches = 4;
        internal const float Cell = 1f;
        internal const float CapRadius = 3f;
        internal const float CapDepth = 3f;
        internal const float Thickness = 2f;
        internal const float Stem = 1f;
        internal const float Fade = 3f;
        internal const float Period = 6000f;
        internal const ulong RunSeed = 20260924UL;

        internal static float Radius => TankGeometry.RadiusFor(Area);

        internal static ReefGeometry Reefs(float fade = Fade, float stem = Stem, float capDepth = CapDepth)
        {
            var xs = new double[3];
            var zs = new double[3];

            for (int k = 0; k < 3; k++)
            {
                double a = 0.3d + 2d * Math.PI * k / 3d;
                xs[k] = Radius + 8d * Math.Cos(a);
                zs[k] = Radius + 8d * Math.Sin(a);
            }

            return new ReefGeometry(CapRadius, capDepth, Thickness, stem, fade, xs, zs);
        }

        internal static RunConfig Config(int count = 3) => new RunConfig
        {
            SharedSpace = true,
            WorldShape = WorldShape.Tank,
            FieldModel = MatterField.Grid,
            WorldAreaSquareMetres = Area,
            HorizontalPatches = Patches,
            WorldDepthMetres = Depth,
            ReefCount = count,
            ReefCapRadiusMetres = count > 0 ? CapRadius : 0f,
            ReefCapDepthMetres = count > 0 ? CapDepth : 0f,
            ReefCapThicknessMetres = count > 0 ? Thickness : 0f,
            ReefStemRadiusMetres = count > 0 ? Stem : 0f,
            ReefFadeMetres = count > 0 ? Fade : 0f,
        };

        internal static GridField Grid(ReefGeometry reefs, float sink = 0f) =>
            new GridField(
                Area, sink, Depth, 0f, 0f, Patches, Cell,
                patchesAcross: 1, shape: WorldShape.Tank, tankRadiusMetres: Radius, reefs: reefs);

        internal static CurrentField Streams(ReefGeometry reefs, float speed = 0.1f)
        {
            var field = new CurrentField
            {
                Mode = CurrentMode.Transport,
                Speed = speed,
                PeriodSeconds = Period,
                AdvectFields = true,
            };

            field.SetBox(
                (float)Math.Sqrt(Area / Patches), Patches, Depth,
                Rng.SeedFor(RunSeed, World.CurrentFieldIndex),
                patchesAcross: 1, shape: WorldShape.Tank, tankRadiusMetres: Radius);

            field.SetReefs(reefs);
            return field;
        }
    }

    /// <summary>
    /// The reefs' shape, dials and refusals — <c>logbook/specs/reef-spec.md</c> §1 to §3.
    /// </summary>
    public class ReefGeometryTests
    {
        private readonly ITestOutputHelper _output;

        public ReefGeometryTests(ITestOutputHelper output) => _output = output;

        private static string Refusal(Action act)
        {
            Exception e = Record.Exception(act);
            Assert.NotNull(e);
            return e.Message;
        }

        [Fact]
        public void EveryRefusalNamesItsRule()
        {
            float r = ReefTank.Radius;

            (string Name, Action Act, string Expect)[] cases =
            {
                ("count 0 with dials", () =>
                {
                    RunConfig c = ReefTank.Config();
                    c.ReefCount = 0;
                    ReefGeometry.Refuse(c, r);
                }, "ReefCount is 0"),
                ("a box", () =>
                {
                    RunConfig c = ReefTank.Config();
                    c.WorldShape = WorldShape.Box;
                    ReefGeometry.Refuse(c, r);
                }, "WorldShape is Box"),
                ("not a grid", () =>
                {
                    RunConfig c = ReefTank.Config();
                    c.FieldModel = MatterField.Cells;
                    ReefGeometry.Refuse(c, r);
                }, "FieldModel is Cells"),
                ("no fade", () =>
                {
                    RunConfig c = ReefTank.Config();
                    c.ReefFadeMetres = 0f;
                    ReefGeometry.Refuse(c, r);
                }, "A reef needs a cap radius"),
                ("a ball, not a table", () =>
                {
                    RunConfig c = ReefTank.Config();
                    c.ReefCapRadiusMetres = 0.9f;
                    c.ReefStemRadiusMetres = 0f;
                    c.ReefCapDepthMetres = 0f;
                    ReefGeometry.Refuse(c, r);
                }, "cannot be 2 m thick"),
                ("a stem wider than the cap", () =>
                {
                    RunConfig c = ReefTank.Config();
                    c.ReefStemRadiusMetres = 2.5f;
                    ReefGeometry.Refuse(c, r);
                }, "wider than the cap's flat underside"),
                ("a cap at the surface on a stem", () =>
                {
                    RunConfig c = ReefTank.Config();
                    c.ReefCapDepthMetres = 0.2f;
                    ReefGeometry.Refuse(c, r);
                }, "would break the"),
                ("a cap in mid-water with no stem", () =>
                {
                    RunConfig c = ReefTank.Config();
                    c.ReefStemRadiusMetres = 0f;
                    ReefGeometry.Refuse(c, r);
                }, "nothing holds it"),
                ("no room under the cap", () =>
                {
                    RunConfig c = ReefTank.Config();
                    c.ReefCapDepthMetres = 9f;
                    ReefGeometry.Refuse(c, r);
                }, "of water under it"),
                ("the fades do not fit the tank", () =>
                {
                    RunConfig c = ReefTank.Config();
                    c.ReefCount = 6;
                    c.ReefFadeMetres = 6f;
                    ReefGeometry.Refuse(c, r);
                }, "a ring of 6"),
                ("water handed reefs in a box", () =>
                {
                    var field = new CurrentField { Mode = CurrentMode.Transport, Speed = 0.1f, PeriodSeconds = 6000f };
                    field.SetBox(20f, 4, ReefTank.Depth, 1UL);
                    field.SetReefs(ReefTank.Reefs());
                }, "Reefs were handed to water in a Box"),
                ("a box grid handed reefs", () =>
                {
                    new GridField(1600f, 0f, ReefTank.Depth, 0f, 0f, 4, 1f, patchesAcross: 1, reefs: ReefTank.Reefs());
                }, "Reefs were handed to a box"),
                ("a negative count", () => new RunConfig { ReefCount = -1 }, "not negative"),
                ("a negative dimension", () => new RunConfig { ReefCapRadiusMetres = -1f }, "finite and not negative"),
                ("an infinite dimension", () => new RunConfig { ReefFadeMetres = float.PositiveInfinity }, "finite and not negative"),
            };

            foreach (var c in cases)
            {
                string message = Refusal(c.Act);
                _output.WriteLine($"{c.Name}: {message}");
                Assert.Contains(c.Expect, message);
            }
        }

        [Fact]
        public void TheWorldRefusesAReefInABox()
        {
            RunConfig c = ReefTank.Config();
            c.WorldShape = WorldShape.Box;
            string message = Refusal(() => new World(c, seed: 1));
            _output.WriteLine(message);
            Assert.Contains("reef", message);
        }

        [Fact]
        public void ThePictureReaderSkipsTheWorldRules()
        {
            // The theatre's picture reader has no field model; worldRules: false skips the box and
            // field refusals and nothing else.
            RunConfig c = ReefTank.Config();
            c.FieldModel = MatterField.Cells;
            ReefGeometry.Refuse(c, ReefTank.Radius, worldRules: false);

            c.ReefStemRadiusMetres = 2.5f;
            Assert.Throws<ArgumentException>(() => ReefGeometry.Refuse(c, ReefTank.Radius, worldRules: false));
        }

        [Fact]
        public void ANoReefConfigIsRefusedNothingAndDrawsNothing()
        {
            RunConfig c = ReefTank.Config(count: 0);
            ReefGeometry.Refuse(c, ReefTank.Radius);
            Assert.Null(ReefGeometry.Place(c, ReefTank.Radius, null, 1UL));
            Assert.Equal("no reef", ReefGeometry.HeaderToken(c));
        }

        [Fact]
        public void TheHeaderTokenNamesTheDials()
        {
            var c = new RunConfig
            {
                ReefCount = 3,
                ReefCapRadiusMetres = 4f,
                ReefCapDepthMetres = 3f,
                ReefCapThicknessMetres = 2f,
                ReefStemRadiusMetres = 1f,
                ReefFadeMetres = 15f,
            };

            Assert.Equal("reefs 3 cap r=4 m at 3 m t=2 m stem r=1 m fade 15 m", ReefGeometry.HeaderToken(c));
        }

        [Fact]
        public void PlacementIsTheSeedsAndKeepsItsRules()
        {
            RunConfig c = ReefTank.Config();
            float radius = ReefTank.Radius;
            double spacing = ReefGeometry.Spacing(c.ReefCapRadiusMetres, c.ReefFadeMetres);

            for (ulong seed = 1; seed <= 20; seed++)
            {
                ulong stream = Rng.SeedFor(seed, World.ReefPlacementIndex);
                ReefGeometry a = ReefGeometry.Place(c, radius, null, stream);
                ReefGeometry b = ReefGeometry.Place(c, radius, null, stream);

                Assert.Equal(3, a.Count);

                for (int i = 0; i < a.Count; i++)
                {
                    Assert.Equal(a.CentreX(i), b.CentreX(i));
                    Assert.Equal(a.CentreZ(i), b.CentreZ(i));

                    double dx = a.CentreX(i) - radius, dz = a.CentreZ(i) - radius;
                    Assert.True(
                        Math.Sqrt(dx * dx + dz * dz) <= radius - c.ReefCapRadiusMetres - ReefGeometry.GlassClearanceMetres + 1e-9,
                        "a cap closer to the glass than its radius and 5 m");

                    for (int j = 0; j < i; j++)
                    {
                        double ex = a.CentreX(i) - a.CentreX(j), ez = a.CentreZ(i) - a.CentreZ(j);
                        Assert.True(Math.Sqrt(ex * ex + ez * ez) >= spacing, "two reefs closer than the spacing");
                    }
                }

                if (seed == 1)
                {
                    _output.WriteLine(
                        $"seed 1: {a.CentreX(0):0.00},{a.CentreZ(0):0.00}  {a.CentreX(1):0.00},{a.CentreZ(1):0.00}  " +
                        $"{a.CentreX(2):0.00},{a.CentreZ(2):0.00} in a tank of radius {radius:0.00} m");
                }
            }
        }

        [Fact]
        public void PlacementReadsTheFloorUnderTheWholeCap()
        {
            // Round 46's floor: the shelf is 1 m deep and the tilt 96 m across the tank, so a large
            // share of the disc is too shallow for a cap at 3 m, 2 m thick, with 2 m under it.
            BedShape bed = BedBeachTests.Beach(1UL);
            var c = new RunConfig
            {
                SharedSpace = true,
                WorldShape = WorldShape.Tank,
                FieldModel = MatterField.Grid,
                WorldAreaSquareMetres = BedBeachTests.Area,
                WorldDepthMetres = BedBeachTests.Depth,
                ReefCount = 3,
                ReefCapRadiusMetres = 4f,
                ReefCapDepthMetres = 3f,
                ReefCapThicknessMetres = 2f,
                ReefStemRadiusMetres = 1f,
                ReefFadeMetres = 15f,
            };

            ReefGeometry reefs = ReefGeometry.Place(
                c, BedBeachTests.Radius, bed, Rng.SeedFor(1UL, World.ReefPlacementIndex));

            for (int i = 0; i < reefs.Count; i++)
            {
                double x = reefs.CentreX(i), z = reefs.CentreZ(i);
                double shallowest = -bed.FloorY(x, z);

                for (int k = 0; k < 8; k++)
                {
                    double a = k * Math.PI / 4d;
                    shallowest = Math.Min(shallowest, -bed.FloorY(x + 4d * Math.Cos(a), z + 4d * Math.Sin(a)));
                }

                _output.WriteLine($"reef {i} at ({x:0.0}, {z:0.0}): the floor under the cap is at least {shallowest:0.00} m down");
                Assert.True(shallowest >= 3d + 2d + ReefGeometry.RoomUnderCapMetres);
            }
        }

        [Fact]
        public void TheDistanceIsTheShape()
        {
            ReefGeometry reefs = ReefTank.Reefs();
            double cx = reefs.CentreX(0), cz = reefs.CentreZ(0);

            // On the cap's top and underside, on the rim, on the stem's side, and well away.
            Assert.Equal(0d, reefs.SignedDistance(cx + 1.5d, -3d, cz), 9);
            Assert.Equal(0d, reefs.SignedDistance(cx + 2d, -5d, cz), 9);
            Assert.Equal(0d, reefs.SignedDistance(cx + 3d, -4d, cz), 9);
            Assert.Equal(0d, reefs.SignedDistance(cx + 1d, -9d, cz), 9);
            Assert.Equal(-1d, reefs.SignedDistance(cx, -4d, cz), 9);
            Assert.Equal(2d, reefs.SignedDistance(cx, -1d, cz), 9);

            Assert.True(reefs.Inside(cx, -4d, cz));
            Assert.True(reefs.Inside(cx, -11d, cz));
            Assert.False(reefs.Inside(cx + 2d, -8d, cz));
            Assert.False(reefs.Inside(cx, -2.5d, cz));

            // The gradient and Hessian against differences, at points off the seams.
            var rng = new Rng(3UL);
            double worstG = 0d, worstH = 0d;
            const double H = 1e-5;

            for (int i = 0; i < 2000; i++)
            {
                double x = cx + (rng.NextFloat() * 2d - 1d) * 6d;
                double z = cz + (rng.NextFloat() * 2d - 1d) * 6d;
                double y = -rng.NextFloat() * 12d;

                double s = reefs.SignedDistance(x, y, z, out int reef, out ReefGeometry.Distance d);
                if (reef != 0 || s < 0.05d) continue;
                if (Math.Min(reefs.OfReef(1, x, y, z).S, reefs.OfReef(2, x, y, z).S) < s + 0.1d) continue;

                double rho = Math.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz));
                if (rho < 0.05d) continue;
                if (Math.Abs(rho - 2d) < 0.05d || Math.Abs(rho - 1d) < 0.05d) continue;
                if (Math.Abs(y + 4d) < 0.05d) continue;

                Assert.Equal(s, d.S, 12);

                double gx = (reefs.SignedDistance(x + H, y, z) - reefs.SignedDistance(x - H, y, z)) / (2d * H);
                double gy = (reefs.SignedDistance(x, y + H, z) - reefs.SignedDistance(x, y - H, z)) / (2d * H);
                double gz = (reefs.SignedDistance(x, y, z + H) - reefs.SignedDistance(x, y, z - H)) / (2d * H);

                worstG = Math.Max(worstG, Math.Abs(gx - d.Gx) + Math.Abs(gy - d.Gy) + Math.Abs(gz - d.Gz));

                reefs.SignedDistance(x + H, y, z, out _, out ReefGeometry.Distance px);
                reefs.SignedDistance(x - H, y, z, out _, out ReefGeometry.Distance mx);
                reefs.SignedDistance(x, y + H, z, out _, out ReefGeometry.Distance py);
                reefs.SignedDistance(x, y - H, z, out _, out ReefGeometry.Distance my);

                double hxx = (px.Gx - mx.Gx) / (2d * H);
                double hxy = (py.Gx - my.Gx) / (2d * H);
                double hyy = (py.Gy - my.Gy) / (2d * H);

                worstH = Math.Max(worstH, Math.Abs(hxx - d.Hxx) + Math.Abs(hxy - d.Hxy) + Math.Abs(hyy - d.Hyy));
            }

            _output.WriteLine($"worst gradient error {worstG:0.0e+0}, worst Hessian error {worstH:0.0e+0}");
            Assert.True(worstG < 1e-5, $"gradient {worstG}");
            Assert.True(worstH < 1e-3, $"Hessian {worstH}");
        }
    }
}
