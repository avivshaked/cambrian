using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// A small tank with three round reefs, shared by the reef tests: 1,600 m² and 12 m deep on a
    /// flat floor, caps 3 m in radius and 2 m thick at 3 m, stems 1 m, a fade of 3 m, the axes on
    /// a ring 8 m from the tank's axis. Built by hand, so the rock is a known shape; the placer's
    /// config (<see cref="Config"/>) draws its own.
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

        /// <summary>
        /// Two irregular reefs whose caps overlap, at different depths, with lobes: a cap 5 m in
        /// mean radius at 3 m with its axis 4 m west of the tank's and one 4 m at 3.5 m with its
        /// axis 2.5 m east, so the caps share a lens over the tank's axis.
        /// </summary>
        internal static ReefGeometry Overlapping(float fade = Fade)
        {
            var reefs = new[]
            {
                new ReefGeometry.Reef
                {
                    X = Radius - 4d, Z = Radius, CapRadius = 5d, CapDepth = 3d, StemRadius = 1.25d,
                    A2 = 0.12d, A3 = 0.1d, A4 = 0.08d, P2 = 0.4d, P3 = 2.1d, P4 = 5.0d, NoiseSeed = 11u,
                },
                new ReefGeometry.Reef
                {
                    X = Radius + 2.5d, Z = Radius, CapRadius = 4d, CapDepth = 3.5d, StemRadius = 1d,
                    A2 = 0.05d, A3 = 0.14d, A4 = 0.06d, P2 = 3.3d, P3 = 0.9d, P4 = 1.7d, NoiseSeed = 12u,
                },
            };

            return new ReefGeometry(Thickness, fade, reefs);
        }

        /// <summary>
        /// The small tank's placer config: a quarter of the surface in caps 3 to 4 m in mean
        /// radius at 3 m ± 0.5, 2 m thick, stems a quarter of their caps, a fade of 3 m.
        /// </summary>
        internal static RunConfig Config(float cover = 0.25f)
        {
            var c = new RunConfig
            {
                SharedSpace = true,
                WorldShape = WorldShape.Tank,
                FieldModel = MatterField.Grid,
                WorldAreaSquareMetres = Area,
                HorizontalPatches = Patches,
                WorldDepthMetres = Depth,
                ReefCover = cover,
            };

            if (cover > 0f)
            {
                c.ReefCapRadiusMinMetres = 3f;
                c.ReefCapRadiusMaxMetres = 4f;
                c.ReefCapDepthMetres = CapDepth;
                c.ReefCapDepthJitterMetres = 0.5f;
                c.ReefCapThicknessMetres = Thickness;
                c.ReefStemRadiusFraction = 0.25f;
                c.ReefFadeMetres = Fade;
            }

            return c;
        }

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

        /// <summary>
        /// Round 46's tank with the reef group at the round's ruling: a quarter of the surface in
        /// caps 6 to 16 m at 3 m ± 1, 2 m thick, stems a quarter of their caps, a fade of 8 m.
        /// </summary>
        internal static RunConfig Round46(float cover = 0.25f, float fade = 8f) => new RunConfig
        {
            SharedSpace = true,
            WorldShape = WorldShape.Tank,
            FieldModel = MatterField.Grid,
            WorldAreaSquareMetres = BedBeachTests.Area,
            WorldDepthMetres = BedBeachTests.Depth,
            ReefCover = cover,
            ReefFadeMetres = fade,
        };

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, Lazy<BedShape>> Beaches =
            new System.Collections.Concurrent.ConcurrentDictionary<ulong, Lazy<BedShape>>();

        /// <summary>
        /// Round 46's floor for a seed, built once for every reef test: the bed's own lattice
        /// measurements are seconds each, and the placement is the thing under test.
        /// </summary>
        internal static BedShape Beach(ulong seed) =>
            Beaches.GetOrAdd(seed, s => new Lazy<BedShape>(() => BedBeachTests.Beach(s))).Value;

        /// <summary>The flat blob's edge at an angle, m: where the flat top meets the rim.</summary>
        internal static double FlatEdge(ReefGeometry reefs, int reef, double theta)
        {
            const double H = 1e-6;
            double o = reefs.OutlineRadius(reef, theta);
            double slope = (reefs.OutlineRadius(reef, theta + H) - reefs.OutlineRadius(reef, theta - H)) / (2d * H);
            double c = o / Math.Sqrt(o * o + slope * slope);
            return o - 0.5d * reefs.CapThicknessMetres / c;
        }

        /// <summary>The horizontal distance from a point to the nearest flat blob's edge along the radial, m.</summary>
        internal static double SeamDistance(ReefGeometry reefs, double x, double z)
        {
            double best = double.MaxValue;

            for (int i = 0; i < reefs.Count; i++)
            {
                double dx = x - reefs.CentreX(i), dz = z - reefs.CentreZ(i);
                double rho = Math.Sqrt(dx * dx + dz * dz);
                best = Math.Min(best, Math.Abs(rho - FlatEdge(reefs, i, Math.Atan2(dz, dx))));
            }

            return best;
        }
    }

    /// <summary>
    /// The reefs' shape, dials, placement and refusals — <c>logbook/specs/reef-spec.md</c> §1 to
    /// §3 and the owner's ruling of 2026-09-23 night (a cover, random radii, outlines that are not
    /// circles, overlap allowed).
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
                ("cover 0 with a dial moved", () =>
                {
                    RunConfig c = ReefTank.Config(cover: 0f);
                    c.ReefFadeMetres = 5f;
                    ReefGeometry.Refuse(c, r);
                }, "ReefCover is 0 and the reef group sets ReefFadeMetres 5"),
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
                }, "A reef needs a least cap radius"),
                ("a range upside down", () =>
                {
                    RunConfig c = ReefTank.Config();
                    c.ReefCapRadiusMaxMetres = 2f;
                    ReefGeometry.Refuse(c, r);
                }, "A reef needs a least cap radius"),
                ("a ball, not a table", () =>
                {
                    RunConfig c = ReefTank.Config();
                    c.ReefCapRadiusMinMetres = 1.5f;
                    ReefGeometry.Refuse(c, r);
                }, "cannot be 2 m thick"),
                ("a stem wider than the cap", () =>
                {
                    RunConfig c = ReefTank.Config();
                    c.ReefStemRadiusFraction = 0.5f;
                    ReefGeometry.Refuse(c, r);
                }, "could stand out past the cap"),
                ("a jittered cap through the surface on a stem", () =>
                {
                    RunConfig c = ReefTank.Config();
                    c.ReefCapDepthJitterMetres = 2.7f;
                    ReefGeometry.Refuse(c, r);
                }, "could rise to"),
                ("a cap in mid-water with no stem", () =>
                {
                    RunConfig c = ReefTank.Config();
                    c.ReefStemRadiusFraction = 0f;
                    ReefGeometry.Refuse(c, r);
                }, "nothing holds it"),
                ("no room under the cap", () =>
                {
                    RunConfig c = ReefTank.Config();
                    c.ReefCapDepthMetres = 8.5f;
                    ReefGeometry.Refuse(c, r);
                }, "of water under it"),
                ("no cap fits the tank", () =>
                {
                    RunConfig c = ReefTank.Config();
                    c.ReefCapRadiusMinMetres = 16f;
                    c.ReefCapRadiusMaxMetres = 16f;
                    ReefGeometry.Refuse(c, r);
                }, "no reef fits"),
                ("the ceiling reached first", () =>
                {
                    RunConfig c = ReefTank.Config(cover: 0.5f);
                    c.ReefMaxCount = 3;
                    ReefGeometry.Place(c, r, null, Rng.SeedFor(1UL, World.ReefPlacementIndex));
                }, "reached the ceiling of 3 reefs"),
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
                ("an outline past its swing", () =>
                {
                    var reef = new ReefGeometry.Reef { X = 20d, Z = 20d, CapRadius = 5d, CapDepth = 3d, StemRadius = 1d, A2 = 0.2d, A3 = 0.2d, A4 = 0.1d };
                    new ReefGeometry(2f, 3f, new[] { reef });
                }, "past 0.4"),
                ("a stem past an outline's flat", () =>
                {
                    var reef = new ReefGeometry.Reef { X = 20d, Z = 20d, CapRadius = 3d, CapDepth = 3d, StemRadius = 1.5d, A2 = 0.3d };
                    new ReefGeometry(2f, 3f, new[] { reef });
                }, "wider than its cap's flat underside"),
                ("a cover of 1", () => new RunConfig { ReefCover = 1f }, "under 1"),
                ("a ceiling of 0", () => new RunConfig { ReefMaxCount = 0 }, "at least one reef"),
                ("a negative dimension", () => new RunConfig { ReefCapRadiusMinMetres = -1f }, "finite and not negative"),
                ("an infinite dimension", () => new RunConfig { ReefFadeMetres = float.PositiveInfinity }, "finite and not negative"),
                ("a rough outline past 1", () => new RunConfig { ReefOutlineRoughness = 1.5f }, "0 to 1"),
                ("a stem fraction of 1", () => new RunConfig { ReefStemRadiusFraction = 1f }, "under 1"),
            };

            foreach (var c in cases)
            {
                string message = Refusal(c.Act);
                _output.WriteLine($"{c.Name}: {message}");
                Assert.Contains(c.Expect, message);
            }
        }

        [Fact]
        public void ACapTooWideForTheGlassIsRefusedNotShrunk()
        {
            // A tank 10 m in radius, caps drawn from 3 to 9 m: the refusals pass (a 3 m cap fits),
            // and a draw past 8 m has nowhere to stand 2 m inside the glass. Some seed of the first
            // fifty draws one before the cover is met, and is refused by name.
            RunConfig c = ReefTank.Config();
            c.ReefCapRadiusMaxMetres = 9f;
            c.ReefOutlineRoughness = 0f;

            string message = null;
            ulong refused = 0;

            for (ulong seed = 1; seed <= 50 && message == null; seed++)
            {
                Exception e = Record.Exception(
                    () => ReefGeometry.Place(c, 10f, null, Rng.SeedFor(seed, World.ReefPlacementIndex)));
                if (e != null) { message = e.Message; refused = seed; }
            }

            Assert.NotNull(message);
            _output.WriteLine($"seed {refused}: {message}");
            Assert.Contains("found no spot for reef", message);
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

            c.ReefStemRadiusFraction = 0.5f;
            Assert.Throws<ArgumentException>(() => ReefGeometry.Refuse(c, ReefTank.Radius, worldRules: false));
        }

        [Fact]
        public void ANoReefConfigIsRefusedNothingAndDrawsNothing()
        {
            // Cover 0 with every dial at its default: no refusal, no reef, no draw — Place returns
            // before it builds the stream — and the world carries no rock anywhere.
            RunConfig c = ReefTank.Config(cover: 0f);
            ReefGeometry.Refuse(c, ReefTank.Radius);
            Assert.Null(ReefGeometry.Place(c, ReefTank.Radius, null, 1UL));
            Assert.Equal("no reef", ReefGeometry.HeaderToken(c, null));

            var plain = new RunConfig();
            Assert.Equal(0f, plain.ReefCover);
            Assert.Equal(plain.Hash(), new RunConfig { ReefCover = 0f }.Hash());

            var world = new World(ReefTank.Config(cover: 0f), seed: 5);
            Assert.Null(world.Reefs);
            Assert.Null(world.Field.Reefs);
        }

        [Fact]
        public void TheHeaderTokenNamesTheDialsAndTheDraw()
        {
            RunConfig c = ReefTank.Round46();
            Assert.Equal(
                "reefs ? cover 0.25 (? got) cap r=6-16 m rough 0.15 at 3 m ±1 t=2 m stem 0.25 fade 8 m",
                ReefGeometry.HeaderToken(c, null));

            ReefGeometry reefs = ReefGeometry.Place(
                c, BedBeachTests.Radius, ReefTank.Beach(1UL), Rng.SeedFor(1UL, World.ReefPlacementIndex));
            string token = ReefGeometry.HeaderToken(c, reefs);

            _output.WriteLine(token);
            Assert.StartsWith("reefs " + reefs.Count + " cover 0.25 (", token);
            Assert.EndsWith(" got) cap r=6-16 m rough 0.15 at 3 m ±1 t=2 m stem 0.25 fade 8 m", token);
        }

        [Fact]
        public void PlacementIsTheSeedsAndKeepsItsRules()
        {
            RunConfig c = ReefTank.Config();
            float radius = ReefTank.Radius;

            for (ulong seed = 1; seed <= 20; seed++)
            {
                ulong stream = Rng.SeedFor(seed, World.ReefPlacementIndex);
                ReefGeometry a = ReefGeometry.Place(c, radius, null, stream);
                ReefGeometry b = ReefGeometry.Place(c, radius, null, stream);

                Assert.Equal(a.Count, b.Count);
                Assert.InRange(a.Count, 1, c.ReefMaxCount);

                for (int i = 0; i < a.Count; i++)
                {
                    ReefGeometry.Reef ra = a.ReefAt(i), rb = b.ReefAt(i);
                    Assert.Equal(ra.X, rb.X);
                    Assert.Equal(ra.Z, rb.Z);
                    Assert.Equal(ra.CapRadius, rb.CapRadius);
                    Assert.Equal(ra.A3, rb.A3);
                    Assert.Equal(ra.NoiseSeed, rb.NoiseSeed);

                    Assert.InRange(ra.CapRadius, 3d, 4d);
                    Assert.InRange(ra.CapDepth, 2.5d, 3.5d);
                    Assert.Equal(0.25d * ra.CapRadius, ra.StemRadius, 12);
                    Assert.Equal(-ra.CapDepth, a.CapTopY(i));
                    Assert.Equal(-(ra.CapDepth + 2d), a.CapUndersideY(i), 12);

                    // The whole outline two metres inside the glass.
                    for (int k = 0; k < 64; k++)
                    {
                        double theta = 2d * Math.PI * k / 64d;
                        double o = a.OutlineRadius(i, theta);
                        double px = ra.X + o * Math.Cos(theta), pz = ra.Z + o * Math.Sin(theta);
                        Assert.True(
                            TankGeometry.Inside(px, pz, radius, ReefGeometry.GlassClearanceMetres - 1e-6),
                            $"seed {seed} reef {i}'s outline is within {ReefGeometry.GlassClearanceMetres} m of the glass");
                    }
                }

                Assert.True(Math.Abs(a.CoverGot - 0.25d) < 0.05d, $"seed {seed} covered {a.CoverGot}");

                if (seed == 1)
                {
                    _output.WriteLine(
                        $"seed 1: {a.Count} reefs covering {a.CoverGot:0.000} of a tank {radius:0.00} m in radius; the first " +
                        $"at ({a.CentreX(0):0.00}, {a.CentreZ(0):0.00}), r0 {a.CapRadius(0):0.00} m, top at {-a.CapTopY(0):0.00} m");
                }
            }
        }

        [Fact]
        public void ACoverOfAQuarterOnRound46sTankLandsOnItsTarget()
        {
            // The brief's reading: round 46's tank (22,000 m², 45 m, the beach at tilt 96 m with a
            // 1 m shore), the ruled dials, five seeds. The cover lands within 0.03 of the target,
            // the count is recorded, and every cap stands over water deep enough under its centre
            // and eight points of its outline.
            RunConfig c = ReefTank.Round46();

            for (ulong seed = 1; seed <= 5; seed++)
            {
                BedShape bed = ReefTank.Beach(seed);
                ReefGeometry reefs = ReefGeometry.Place(
                    c, BedBeachTests.Radius, bed, Rng.SeedFor(seed, World.ReefPlacementIndex));

                Assert.True(Math.Abs(reefs.CoverGot - 0.25d) <= 0.03d, $"seed {seed}: got {reefs.CoverGot}");
                Assert.Equal(0.25d, reefs.CoverTarget, 6);
                Assert.InRange(reefs.Count, 2, c.ReefMaxCount);

                double shallowestMargin = double.MaxValue, rMin = double.MaxValue, rMax = 0d, dMin = double.MaxValue, dMax = 0d;

                for (int i = 0; i < reefs.Count; i++)
                {
                    ReefGeometry.Reef r = reefs.ReefAt(i);
                    double need = r.CapDepth + 2d + ReefGeometry.RoomUnderCapMetres;
                    double shallowest = -bed.FloorY(r.X, r.Z);

                    for (int k = 0; k < 8; k++)
                    {
                        double a = k * Math.PI / 4d;
                        double o = reefs.OutlineRadius(i, a);
                        shallowest = Math.Min(shallowest, -bed.FloorY(r.X + o * Math.Cos(a), r.Z + o * Math.Sin(a)));
                    }

                    Assert.True(shallowest >= need, $"seed {seed} reef {i}: {shallowest} m of water against {need}");
                    shallowestMargin = Math.Min(shallowestMargin, shallowest - need);
                    rMin = Math.Min(rMin, r.CapRadius);
                    rMax = Math.Max(rMax, r.CapRadius);
                    dMin = Math.Min(dMin, r.CapDepth);
                    dMax = Math.Max(dMax, r.CapDepth);
                }

                _output.WriteLine(
                    $"seed {seed}: {reefs.Count} reefs cover {reefs.CoverGot:0.0000} of the tank's columns (target 0.25); " +
                    $"r0 {rMin:0.0} to {rMax:0.0} m, tops at {dMin:0.00} to {dMax:0.00} m; the tightest cap has " +
                    $"{shallowestMargin:0.00} m of water to spare");
            }
        }

        [Fact]
        public void AnOutlineStaysWithinItsBoundsAndEveryCapDiffers()
        {
            // At the ruled roughness and at the dial's ceiling: every drawn outline within
            // [0.6, 1.4] of r0 at 720 angles, the amplitudes' sum at most 0.4, and no two reefs of
            // a draw with the same outline.
            foreach (float rough in new[] { 0.15f, 1f })
            {
                RunConfig c = ReefTank.Round46();
                c.ReefOutlineRoughness = rough;
                c.ReefStemRadiusFraction = 0.1f;

                double lowest = double.MaxValue, highest = 0d;
                int reefsSeen = 0;

                for (ulong seed = 1; seed <= 5; seed++)
                {
                    ReefGeometry reefs = ReefGeometry.Place(
                        c, BedBeachTests.Radius, ReefTank.Beach(seed), Rng.SeedFor(seed, World.ReefPlacementIndex));

                    for (int i = 0; i < reefs.Count; i++)
                    {
                        ReefGeometry.Reef r = reefs.ReefAt(i);
                        Assert.True(r.A2 + r.A3 + r.A4 <= ReefGeometry.OutlineSwing + 1e-12);
                        Assert.False(r.Round);

                        for (int k = 0; k < 720; k++)
                        {
                            double share = reefs.OutlineRadius(i, 2d * Math.PI * k / 720d) / r.CapRadius;
                            lowest = Math.Min(lowest, share);
                            highest = Math.Max(highest, share);
                            Assert.InRange(share, 0.6d - 1e-12, 1.4d + 1e-12);
                        }

                        for (int j = 0; j < i; j++)
                        {
                            Assert.NotEqual(reefs.ReefAt(j).A2, r.A2);
                            Assert.NotEqual(reefs.ReefAt(j).NoiseSeed, r.NoiseSeed);
                        }

                        reefsSeen++;
                    }
                }

                _output.WriteLine(
                    $"roughness {rough}: {reefsSeen} reefs over five seeds, outlines from {lowest:0.000} to {highest:0.000} of r0");
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

            (double worstG, double worstH, int n) = Derivatives(reefs, 0, 6d, new Rng(3UL));

            _output.WriteLine($"round: {n} points, worst gradient error {worstG:0.0e+0}, worst Hessian error {worstH:0.0e+0}");
            Assert.True(worstG < 1e-5, $"gradient {worstG}");
            Assert.True(worstH < 1e-3, $"Hessian {worstH}");
        }

        [Fact]
        public void AnIrregularCapsDistanceHasItsOwnDerivativesAndItsOutlineIsTheRim()
        {
            ReefGeometry reefs = ReefTank.Overlapping();

            for (int reef = 0; reef < reefs.Count; reef++)
            {
                ReefGeometry.Reef r = reefs.ReefAt(reef);
                double midY = r.CapDepth + 1d;
                double worstRim = 0d, worstUnit = 0d;

                for (int k = 0; k < 360; k++)
                {
                    double theta = 2d * Math.PI * k / 360d;
                    double o = reefs.OutlineRadius(reef, theta);
                    double x = r.X + o * Math.Cos(theta), z = r.Z + o * Math.Sin(theta);

                    // The rim at the mid-plane is the outline, to the rounding of the arithmetic,
                    // and the gradient there is a unit normal. Points inside the other cap's lens
                    // are the union's and are skipped.
                    ReefGeometry.Distance d = reefs.OfReef(reef, x, -midY, z);
                    worstRim = Math.Max(worstRim, Math.Abs(d.S));
                    worstUnit = Math.Max(worstUnit, Math.Abs(Math.Sqrt(d.Gx * d.Gx + d.Gy * d.Gy + d.Gz * d.Gz) - 1d));

                    // Just inside and just outside the outline at the mid-plane.
                    Assert.True(reefs.OfReef(reef, r.X + (o - 1e-3) * Math.Cos(theta), -midY, r.Z + (o - 1e-3) * Math.Sin(theta)).S < 0d);
                    Assert.True(reefs.OfReef(reef, r.X + (o + 1e-3) * Math.Cos(theta), -midY, r.Z + (o + 1e-3) * Math.Sin(theta)).S > 0d);
                    Assert.True(reefs.InsideOutline(reef, r.X + (o - 1e-3) * Math.Cos(theta), r.Z + (o - 1e-3) * Math.Sin(theta)));
                    Assert.False(reefs.InsideOutline(reef, r.X + (o + 1e-3) * Math.Cos(theta), r.Z + (o + 1e-3) * Math.Sin(theta)));
                }

                _output.WriteLine(
                    $"reef {reef}: on its outline at the mid-plane the distance is at most {worstRim:0.0e+0} m from 0 and " +
                    $"the gradient's length at most {worstUnit:0.0e+0} from 1");
                Assert.True(worstRim < 1e-9);
                Assert.True(worstUnit < 1e-9);

                (double worstG, double worstH, int n) = Derivatives(reefs, reef, 10d, new Rng(41UL + (ulong)reef));
                _output.WriteLine(
                    $"reef {reef}: {n} points off the seams, worst gradient error {worstG:0.0e+0}, worst Hessian error {worstH:0.0e+0}");
                Assert.True(n > 300);
                Assert.True(worstG < 1e-5, $"gradient {worstG}");
                Assert.True(worstH < 1e-3, $"Hessian {worstH}");
            }
        }

        /// <summary>
        /// One reef's gradient and Hessian against central differences of its own distance, at
        /// points outside the rock and off the seams (the flat blob's edge, the stem's side and
        /// top, the axis), where the point's nearest reef is this one by a margin.
        /// </summary>
        private static (double WorstG, double WorstH, int N) Derivatives(ReefGeometry reefs, int reef, double spread, Rng rng)
        {
            ReefGeometry.Reef r = reefs.ReefAt(reef);
            double midY = -(r.CapDepth + 0.5d * reefs.CapThicknessMetres);
            double worstG = 0d, worstH = 0d;
            const double H = 1e-5;
            int n = 0;

            for (int i = 0; i < 4000; i++)
            {
                double x = r.X + (rng.NextFloat() * 2d - 1d) * spread;
                double z = r.Z + (rng.NextFloat() * 2d - 1d) * spread;
                double y = -rng.NextFloat() * 12d;

                double s = reefs.SignedDistance(x, y, z, out int nearest, out ReefGeometry.Distance d);
                if (nearest != reef || s < 0.05d) continue;

                bool clear = true;
                for (int other = 0; other < reefs.Count && clear; other++)
                {
                    if (other != reef && reefs.OfReef(other, x, y, z).S < s + 0.1d) clear = false;
                }

                if (!clear) continue;

                double dx = x - r.X, dz = z - r.Z;
                double rho = Math.Sqrt(dx * dx + dz * dz);
                if (rho < 0.05d) continue;
                if (Math.Abs(rho - ReefTank.FlatEdge(reefs, reef, Math.Atan2(dz, dx))) < 0.05d) continue;
                if (Math.Abs(rho - r.StemRadius) < 0.05d) continue;
                if (Math.Abs(y - midY) < 0.05d) continue;

                Assert.Equal(s, d.S, 12);

                double gx = (reefs.SignedDistance(x + H, y, z) - reefs.SignedDistance(x - H, y, z)) / (2d * H);
                double gy = (reefs.SignedDistance(x, y + H, z) - reefs.SignedDistance(x, y - H, z)) / (2d * H);
                double gz = (reefs.SignedDistance(x, y, z + H) - reefs.SignedDistance(x, y, z - H)) / (2d * H);

                worstG = Math.Max(worstG, Math.Abs(gx - d.Gx) + Math.Abs(gy - d.Gy) + Math.Abs(gz - d.Gz));

                reefs.SignedDistance(x + H, y, z, out _, out ReefGeometry.Distance px);
                reefs.SignedDistance(x - H, y, z, out _, out ReefGeometry.Distance mx);
                reefs.SignedDistance(x, y + H, z, out _, out ReefGeometry.Distance py);
                reefs.SignedDistance(x, y - H, z, out _, out ReefGeometry.Distance my);
                reefs.SignedDistance(x, y, z + H, out _, out ReefGeometry.Distance pz);
                reefs.SignedDistance(x, y, z - H, out _, out ReefGeometry.Distance mz);

                double hxx = (px.Gx - mx.Gx) / (2d * H);
                double hxy = (py.Gx - my.Gx) / (2d * H);
                double hyy = (py.Gy - my.Gy) / (2d * H);
                double hxz = (pz.Gx - mz.Gx) / (2d * H);
                double hzz = (pz.Gz - mz.Gz) / (2d * H);
                double hyz = (pz.Gy - mz.Gy) / (2d * H);

                worstH = Math.Max(
                    worstH,
                    Math.Abs(hxx - d.Hxx) + Math.Abs(hxy - d.Hxy) + Math.Abs(hyy - d.Hyy) +
                    Math.Abs(hxz - d.Hxz) + Math.Abs(hzz - d.Hzz) + Math.Abs(hyz - d.Hyz));
                n++;
            }

            return (worstG, worstH, n);
        }

        [Fact]
        public void TheUnionIsInsideAnyAndTheNearestOfAll()
        {
            // A point inside both caps' rock, where they overlap over the tank's axis: inside, its
            // distance the lesser of the two, no light, no water.
            ReefGeometry reefs = ReefTank.Overlapping();
            double x = ReefTank.Radius, z = ReefTank.Radius;
            double y = -4.4d; // in both slabs: 3 to 5 m and 3.5 to 5.5 m

            double a = reefs.OfReef(0, x, y, z).S;
            double b = reefs.OfReef(1, x, y, z).S;
            Assert.True(a < 0d && b < 0d, $"the point is in both caps ({a}, {b})");

            double s = reefs.SignedDistance(x, y, z, out int nearest, out _);
            Assert.True(reefs.Inside(x, y, z));
            Assert.Equal(Math.Min(a, b), s);
            Assert.Equal(a <= b ? 0 : 1, nearest);
            Assert.Equal(0f, reefs.LightTransmission((float)x, (float)y, (float)z));
            Assert.Equal(0d, reefs.FadeAt(x, y, z));
            Assert.True(reefs.Fade(x, y, z, out ReefGeometry.Distance f));
            Assert.Equal(0d, f.S);

            // The union's minimum is the same whatever order the reefs are handed in.
            var reversed = new ReefGeometry(
                reefs.CapThicknessMetres, reefs.FadeMetres, new[] { reefs.ReefAt(1), reefs.ReefAt(0) });

            var rng = new Rng(77UL);
            for (int i = 0; i < 3000; i++)
            {
                double px = ReefTank.Radius + (rng.NextFloat() * 2d - 1d) * 14d;
                double pz = ReefTank.Radius + (rng.NextFloat() * 2d - 1d) * 14d;
                double py = -rng.NextFloat() * 12d;

                double both = Math.Min(reefs.OfReef(0, px, py, pz).S, reefs.OfReef(1, px, py, pz).S);
                Assert.Equal(both, reefs.SignedDistance(px, py, pz));
                Assert.Equal(both, reversed.SignedDistance(px, py, pz));
                Assert.Equal(both < 0d, reefs.Inside(px, py, pz));

                // A bounded read is exact under its limit and at or past the limit otherwise.
                double bounded = reefs.SignedDistance(px, py, pz, 0.5d, out _, out _);
                if (both < 0.5d) Assert.Equal(both, bounded);
                else Assert.True(bounded >= 0.5d && bounded <= both + 1e-12);
            }

            _output.WriteLine($"at the lens: reef 0 reads {a:0.###} m, reef 1 {b:0.###} m; the union {s:0.###} m");
        }

        [Fact]
        public void TheProductFadeIsOneBeyondEveryFadeAndZeroInEveryRock()
        {
            ReefGeometry reefs = ReefTank.Overlapping(fade: 3f);
            var rng = new Rng(91UL);
            int beyond = 0, rock = 0, between = 0;

            for (int i = 0; i < 20000; i++)
            {
                double x = ReefTank.Radius + (rng.NextFloat() * 2d - 1d) * 20d;
                double z = ReefTank.Radius + (rng.NextFloat() * 2d - 1d) * 20d;
                double y = -rng.NextFloat() * 12d;

                double a = reefs.OfReef(0, x, y, z).S, b = reefs.OfReef(1, x, y, z).S;
                double g = reefs.FadeAt(x, y, z);
                bool touched = reefs.Fade(x, y, z, out ReefGeometry.Distance f);

                if (a >= 3d && b >= 3d)
                {
                    Assert.Equal(1d, g);
                    Assert.False(touched);
                    beyond++;
                }
                else if (a <= 0d || b <= 0d)
                {
                    Assert.Equal(0d, g);
                    rock++;
                }
                else
                {
                    // The product of the two quintics, and the analytic record agrees with it.
                    double qa = Quintic(Math.Min(1d, a / 3d)), qb = Quintic(Math.Min(1d, b / 3d));
                    Assert.Equal(qa * qb, g, 12);
                    Assert.Equal(g, f.S, 12);
                    between++;
                }
            }

            _output.WriteLine($"{beyond} points beyond both fades, {rock} in rock, {between} in a fade");
            Assert.True(beyond > 1000 && rock > 100 && between > 1000);
        }

        private static double Quintic(double xi) => xi * xi * xi * (10d + xi * (-15d + 6d * xi));
    }
}
