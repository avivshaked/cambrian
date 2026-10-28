using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// D255: a family limb (D233) under D229's fin law. Its added mass and first moment come from the strip
    /// integral of its true slices, computed once for each degree of freedom from the developed part's own
    /// quantities, kept on its <see cref="FamilyGeometry"/> and scaled at a resize.
    /// </summary>
    public sealed class FamilyFinLawTests
    {
        private readonly ITestOutputHelper _out;

        public FamilyFinLawTests(ITestOutputHelper output) => _out = output;

        private static readonly PartShapeRegistry Registry = new PartShapeRegistry(
            new BoxShape(), new SphereShape(), new CapsuleShape(), new SuperellipsoidShape());

        private const double Rho = 1000.0;
        private const double Omega = 6.0;
        private const double Push = 0.3;

        private static readonly Vec3 UnitX = new Vec3(1, 0, 0);
        private static readonly Vec3 UnitY = new Vec3(0, 1, 0);

        // The fin bench's paddle and stick (src/Evosim.SwimProbe/FinBench.cs), copied.
        private static readonly Float3 Paddle = new Float3(0.2f, 0.01f, 0.2f);
        private static readonly Float3 Stick = new Float3(0.2f, (float)Math.Sqrt(0.002), (float)Math.Sqrt(0.002));

        private static readonly PartForm BoxLike = new PartForm(0.1f, 0.1f, 1f);
        private static readonly PartForm Egg = new PartForm(0.6f, 1f, 2f);

        private static long Bits(double v) => BitConverter.DoubleToInt64Bits(v);

        private static SolverConfig Water(bool powered = true, double push = Push, bool shaped = true)
        {
            SolverConfig config = TestBodies.Water(0.01);
            config.AddedMassCoefficient = 0.5;
            config.LimbPushCoefficient = push;
            config.PowerPushRadiansPerSecond = powered ? Omega : 0;
            config.PoseHertz = 2.0;
            config.ShapedAddedMass = shaped;
            return config;
        }

        private static Genome LimbGenome(Float3 half, PartForm? form)
        {
            Genome g = TestBodies.Chain(links: 2, power: 10f, linkHalf: half);
            if (form.HasValue)
            {
                g.Nodes[1].ShapeId = ShapeIds.Superellipsoid;
                g.Nodes[1].Form = form.Value;
            }

            return g;
        }

        private static Creature Build(Phenotype phenotype, SolverConfig config)
        {
            var body = new Creature(0, phenotype, config, Registry);
            body.PlaceAt(new Vec3(0, -20, 0), QuatD.Identity);
            return body;
        }

        private static Creature Limb(SolverConfig config, Float3 half, PartForm? form)
        {
            Phenotype adult = Developer.Develop(LimbGenome(half, form), DevelopmentLimits.Default, shapes: Registry);
            return Build(adult, config);
        }

        private static Vec3 ForceOn(Creature body, SolverConfig config, int link)
        {
            Array.Clear(body.Fext, 0, body.Fext.Length);
            Fluid.Apply(body, config);
            return Vec3.Read(body.Fext, 6 * link + 3);
        }

        /// <summary>The powered stroke's push at s = 0.5 on link 1, drag and all removed.</summary>
        private static Vec3 PoweredPush(Float3 half, PartForm? form, out double work)
        {
            SolverConfig config = Water();
            Creature body = Limb(config, half, form);
            Vec3 still = ForceOn(body, config, 1);
            double before = body.MechanicalWorkJoules;
            body.Drive.Stroke[0] = 0.5;
            Vec3 pushed = ForceOn(body, config, 1);
            work = body.MechanicalWorkJoules - before;
            return pushed - still;
        }

        // ---------------------------------------------------------------- the box limit

        [Fact]
        public void TheBoxLikeMemberOnAnAlignedLimbGivesTheBoxsAddedMassAndTheReach()
        {
            var geometry = new Superellipsoid(BoxLike);
            double worstMass = 0.0, worstLever = 0.0;

            foreach (Float3 h in new[] { Paddle, Stick, new Float3(0.05f, 0.3f, 0.02f) })
            {
                // The joint on -Y, the limb's axis +Y, the stroke along x: the width is across z.
                Float3 jointF = geometry.SurfacePoint(new Float3(0f, -1f, 0f), h);
                var joint = new Double3(jointF.X, jointF.Y, jointF.Z);
                var axis = new Double3(0, 1, 0);
                var stroke = new Double3(1, 0, 0);

                geometry.StrokeIntegrals(h, joint, axis, stroke, Superellipsoid.StrokeResolution, out double mOverRho, out double i1OverRho);

                double box = Fluid.AddedMassOf(ShapeIds.Box, h, Mat3.Identity, UnitY, UnitX, Rho, 0.5, 1.0) / Rho;
                double reach = Math.Abs((double)jointF.Y);

                double massError = mOverRho / box - 1.0;
                double leverError = i1OverRho / mOverRho / reach - 1.0;
                worstMass = Math.Max(worstMass, Math.Abs(massError));
                worstLever = Math.Max(worstLever, Math.Abs(leverError));

                _out.WriteLine($"{h}: M/rho {mOverRho:0.######} against the box's {box:0.######} ({massError:P3}); I1/M {i1OverRho / mOverRho:0.######} against the reach {reach:0.######} ({leverError:P3})");
                Assert.True(Math.Abs(massError) <= 0.01, $"{h}: M is {massError:P3} from the box's");
                Assert.True(Math.Abs(leverError) <= 0.01, $"{h}: I1/M is {leverError:P3} from the reach");
            }

            _out.WriteLine($"worst: M {worstMass:P3}, lever {worstLever:P3}");
        }

        [Fact]
        public void ThePoweredPushOfTheBoxLikeMemberIsTheBoxsToACoupleOfPercent()
        {
            foreach (Float3 h in new[] { Paddle, Stick })
            {
                Vec3 box = PoweredPush(h, null, out double boxWork);
                Vec3 family = PoweredPush(h, BoxLike, out double familyWork);
                _out.WriteLine($"{h}: push box {box.X:0.######} N, family {family.X:0.######} N ({family.X / box.X - 1.0:P3}); work box {boxWork:0.######} J, family {familyWork:0.######} J ({familyWork / boxWork - 1.0:P3})");

                Assert.True(box.X < 0 && family.X < 0, "both push toward the joint");
                Assert.True(Math.Abs(family.X / box.X - 1.0) < 0.03, $"{h}: the push is {family.X / box.X - 1.0:P3} from the box's");
                Assert.True(Math.Abs(familyWork / boxWork - 1.0) < 0.03, $"{h}: the work is {familyWork / boxWork - 1.0:P3} from the box's");
            }
        }

        [Fact]
        public void TheFamilyLimbsPushIsTheSumOfTheStrokesFirstMomentAndItsWorkLeverIsFirstMomentOverMass()
        {
            SolverConfig config = Water();
            Creature body = Limb(config, new Float3(0.2f, 0.05f, 0.2f), Egg);
            Assert.NotNull(body.FamilyAddedMass);
            Assert.NotNull(body.FamilyFirstMoment);
            Assert.True(body.FamilyAddedMass[0] > 0 && body.FamilyFirstMoment[0] > 0);

            Vec3 still = ForceOn(body, config, 1);
            double before = body.MechanicalWorkJoules;
            body.Drive.Stroke[0] = 0.5;
            Vec3 push = ForceOn(body, config, 1) - still;
            double work = body.MechanicalWorkJoules - before;

            // The hinge's axis is across the limb, so along is 0 and q = (s Omega)^2.
            double q = 0.5 * Omega * 0.5 * Omega;
            double expectedPush = Push * q * body.FamilyFirstMoment[0];
            double lever = body.FamilyFirstMoment[0] / body.FamilyAddedMass[0];
            double expectedWork = expectedPush * lever * Math.Sqrt(q) * config.StepSeconds;

            // The joint stands on the surface of a part whose frame is its centre of mass, so the limb's axis is not
            // quite the child anchor's face normal; the push is along that axis, and its size is what is held.
            _out.WriteLine($"push {push.Magnitude:0.######} N, expected {expectedPush:0.######}; work {work:0.######} J, expected {expectedWork:0.######}; lever {lever:0.#####} m against the reach 0.2");
            Assert.True(Math.Abs(push.Magnitude / expectedPush - 1.0) < 1e-9);
            Assert.True(Math.Abs(work / expectedWork - 1.0) < 1e-9);
            Assert.InRange(lever, 0.0, 0.4);
        }

        // ---------------------------------------------------------------- growth

        [Fact]
        public void ABodyBuiltFromAScaledPhenotypeAndABodyResizedToItCarryEqualBits()
        {
            SolverConfig config = Water();
            Phenotype adult = Developer.Develop(LimbGenome(new Float3(0.2f, 0.05f, 0.2f), Egg), DevelopmentLimits.Default, shapes: Registry);
            FamilyGeometry shared = adult.Parts[1].Family;
            Assert.Equal(0, shared.StrokeBuilds);

            foreach (float r in new[] { 0.4f, 0.7f, 1.2f })
            {
                Phenotype grown = adult.Scaled(r, Registry);
                Assert.Same(shared, grown.Parts[1].Family);

                Creature built = Build(grown, config);

                Creature resized = Build(adult, config);
                resized.Resize(adult.Scaled(r, Registry));

                Assert.Equal(built.Dof, resized.Dof);
                for (int d = 0; d < built.Dof; d++)
                {
                    Assert.Equal(Bits(built.FamilyAddedMass[d]), Bits(resized.FamilyAddedMass[d]));
                    Assert.Equal(Bits(built.FamilyFirstMoment[d]), Bits(resized.FamilyFirstMoment[d]));
                }

                // The values at size r are the reference's times r cubed and r to the fourth, and a fresh
                // computation at the scaled size agrees to the rounding of the scaled floats.
                PhenotypePart part = grown.Parts[1];
                Float3 jointF = part.ChildAnchorLocal;
                var toOrigin = new Double3(-jointF.X, -jointF.Y, -jointF.Z);
                Double3 axis = toOrigin * (1.0 / toOrigin.Length);
                int dof = built.DofStart[1];
                QuatD frame = QuatD.Read(built.JointFrame, 4 * 1);
                Vec3 dofAxis = frame.Rotate(Creature.AxisOf(0));
                Double3 motion = Double3.Cross(new Double3(dofAxis.X, dofAxis.Y, dofAxis.Z), axis);
                Double3 stroke = motion * (1.0 / motion.Length);
                part.Family.Geometry.StrokeIntegrals(
                    part.HalfExtents, new Double3(jointF.X, jointF.Y, jointF.Z), axis, stroke, Superellipsoid.StrokeResolution,
                    out double m, out double i1);

                Assert.True(Math.Abs(built.FamilyAddedMass[dof] / (Rho * m) - 1.0) < 1e-4, $"r {r}: M {built.FamilyAddedMass[dof] / (Rho * m)}");
                Assert.True(Math.Abs(built.FamilyFirstMoment[dof] / (Rho * i1) - 1.0) < 1e-4, $"r {r}: I1 {built.FamilyFirstMoment[dof] / (Rho * i1)}");
            }

            // One integral for the one direction the hinge has, however many bodies and sizes read it.
            Assert.Equal(1, shared.StrokeBuilds);
        }

        // ---------------------------------------------------------------- the still water

        [Fact]
        public void AFamilyTailUnderThePoweredStrokeSwimsWithFiniteWorkAndDoesNotDiverge()
        {
            SolverConfig config = Water();
            Genome g = LimbGenome(new Float3(0.2f, 0.05f, 0.2f), Egg);
            g.Nodes[1].Neurons = new[]
            {
                new NeuronDef { Op = NeuronOp.Sum, Frequency = 1f, Amplitude = 1f, Bias = 1f, Inputs = Array.Empty<NeuronInput>() },
                new NeuronDef { Op = NeuronOp.Sum, Frequency = 1f, Amplitude = 1f, Bias = 0f, Inputs = Array.Empty<NeuronInput>() },
            };
            g.Nodes[1].Drive = new DriveGenes(0f, 0.05f, 0.05f, 0.05f);

            Creature body = Build(Developer.Develop(g, DevelopmentLimits.Default, shapes: Registry), config);
            var world = new DynamicsWorld(config);
            world.Add(body);
            for (int i = 0; i < 2000; i++)
            {
                world.Step();
                Assert.True(body.IsFinite(), $"the body went non-finite at step {i}");
            }

            double toward = -body.Position[0];
            double speed = Vec3.Read(body.Velocity, 0).Magnitude;
            _out.WriteLine($"20 s at full power: {toward:0.###} m toward the head, speed {speed:0.###} m/s, work {body.MechanicalWorkJoules:0.###} J");

            Assert.True(toward > 0.2, $"the stroke moved the body only {toward} m");
            Assert.True(body.MechanicalWorkJoules > 0 && !double.IsInfinity(body.MechanicalWorkJoules) && !double.IsNaN(body.MechanicalWorkJoules));
            Assert.True(speed < 10.0, $"the body is moving at {speed} m/s");
        }

        // ---------------------------------------------------------------- the paths that must not change

        [Fact]
        public void ABodyWithNoFamilyPartAllocatesNothingAndKeepsTheShapedLinesToTheBit()
        {
            SolverConfig config = Water();
            Creature box = Limb(config, Paddle, null);
            Assert.Null(box.FamilyAddedMass);
            Assert.Null(box.FamilyFirstMoment);

            // The plate formula at the reach, as FinLawTests holds it.
            Vec3 push = PoweredPush(Paddle, null, out _);
            double wide = 2 * 0.2, length = 2 * 0.2;
            double addedMass = Rho * Math.PI / 4.0 * wide * wide * length;
            double expected = Push * addedMass * 0.2 * (0.5 * Omega) * (0.5 * Omega);
            Assert.True(Math.Abs(-push.X / expected - 1.0) < 1e-6);

            // With the fin law off a family body allocates nothing either.
            Creature off = Build(
                Developer.Develop(LimbGenome(Paddle, Egg), DevelopmentLimits.Default, shapes: Registry),
                Water(shaped: false));
            Assert.Null(off.FamilyAddedMass);
        }

        [Fact]
        public void ALoneFamilyLimbUnderTheAssemblySwitchTakesTheFamilyLines()
        {
            SolverConfig plain = Water();
            SolverConfig strokes = Water();
            strokes.StrokePushesAssembly = true;

            Creature a = Limb(plain, new Float3(0.2f, 0.05f, 0.2f), Egg);
            Creature b = Limb(strokes, new Float3(0.2f, 0.05f, 0.2f), Egg);
            Vec3 stillA = ForceOn(a, plain, 1), stillB = ForceOn(b, strokes, 1);
            a.Drive.Stroke[0] = 0.5;
            b.Drive.Stroke[0] = 0.5;
            Vec3 pushA = ForceOn(a, plain, 1) - stillA, pushB = ForceOn(b, strokes, 1) - stillB;

            Assert.NotEqual(0.0, pushA.X);
            Assert.Equal(Bits(pushA.X), Bits(pushB.X));
        }

        // ---------------------------------------------------------------- the refusals

        [Fact]
        public void AFamilyLimbUnderTheSwingPushIsRefusedAtBuildByName()
        {
            SolverConfig swing = Water(powered: false);
            Phenotype adult = Developer.Develop(LimbGenome(new Float3(0.2f, 0.05f, 0.2f), Egg), DevelopmentLimits.Default, shapes: Registry);

            ArgumentException refused = Assert.Throws<ArgumentException>(() => new Creature(0, adult, swing, Registry));
            Assert.Contains("ShapedAddedMass", refused.Message);
            Assert.Contains("D255", refused.Message);

            // Off, or with a box limb, the swing push builds as it did.
            _ = new Creature(0, adult, Water(powered: false, shaped: false), Registry);
            _ = Limb(swing, new Float3(0.2f, 0.05f, 0.2f), null);
        }

        [Fact]
        public void AFamilyPartInAnAssemblyIsRefusedAtBuildByName()
        {
            SolverConfig strokes = Water();
            strokes.StrokePushesAssembly = true;

            // A jointed box limb with a family part welded beyond it.
            Genome g = TestBodies.Chain(links: 3, power: 10f, linkHalf: new Float3(0.2f, 0.05f, 0.2f));
            MorphNode welded = g.Nodes[2];
            welded.JointType = JointType.Fixed;
            welded.CellTypeId = CellTypeIds.Structural;
            welded.Power = 0f;
            welded.JointLimits = Array.Empty<Float2>();
            welded.Neurons = Array.Empty<NeuronDef>();
            welded.ShapeId = ShapeIds.Superellipsoid;
            welded.Form = Egg;
            Phenotype body = Developer.Develop(g, DevelopmentLimits.Default, shapes: Registry);
            Assert.Equal(3, body.PartCount);

            ArgumentException refused = Assert.Throws<ArgumentException>(() => new Creature(0, body, strokes, Registry));
            Assert.Contains("StrokePushesAssembly", refused.Message);
            Assert.Contains("D255", refused.Message);

            // The same body with no family part builds under the switch.
            welded.ShapeId = ShapeIds.Box;
            welded.Form = PartForm.None;
            _ = new Creature(0, Developer.Develop(g, DevelopmentLimits.Default, shapes: Registry), strokes, Registry);
        }

        [Fact]
        public void TheAddedMassFunctionStillRefusesTheFamilyByName()
        {
            ArgumentException refused = Assert.Throws<ArgumentException>(() =>
                Fluid.AddedMassOf(ShapeIds.Superellipsoid, new Float3(1, 1, 1), Mat3.Identity, UnitY, UnitX, Rho, 0.5, 1.0));
            Assert.Contains(ShapeIds.Superellipsoid, refused.Message);
        }

        // ---------------------------------------------------------------- the buoyancy arm

        private static readonly (string Name, PartForm Form)[] ArmForms = BenchForms();

        private static (string, PartForm)[] BenchForms()
        {
            var forms = new System.Collections.Generic.List<(string, PartForm)>();
            foreach (float profile in new[] { 0.1f, 1f })
                foreach (float section in new[] { 0.1f, 2f })
                    foreach (float taper in new[] { 0.25f, 4f })
                        forms.Add(($"profile {profile} section {section} taper {taper}", new PartForm(profile, section, taper)));
            forms.Add(("box-like", new PartForm(0.1f, 0.1f, 1f)));
            forms.Add(("sphere-like", new PartForm(1f, 1f, 1f)));
            forms.Add(("capsule-like", new PartForm(0.5f, 1f, 1f)));
            return forms.ToArray();
        }

        [Fact]
        public void TheBuoyancyArmsPointAtOffsetPlusOrMinusOneIsInsideTheTrueBodyForEveryBenchForm()
        {
            int points = 0;
            foreach ((string name, PartForm form) in ArmForms)
            {
                foreach ((string halfName, Float3 half) in new[] { ("paddle", Paddle), ("stick", Stick) })
                {
                    foreach (float offset in new[] { -1f, 1f })
                    {
                        Genome g = TestBodies.Box(half);
                        g.Nodes[0].ShapeId = ShapeIds.Superellipsoid;
                        g.Nodes[0].Form = form;
                        g.Nodes[0].BuoyancyOffset = offset;
                        Phenotype body = Developer.Develop(g, DevelopmentLimits.Default, shapes: Registry);
                        var creature = new Creature(0, body, Water(), Registry);

                        int axis = creature.ThinAxis[0];
                        double arm = creature.BuoyancyArm[0];
                        Assert.True(Math.Sign(arm) == Math.Sign(offset), $"{name}: the arm keeps the offset's sign");

                        // As the mouth test does: a hair inside, for float rounding at a pole.
                        float at = (float)arm * (1f - 2e-7f);
                        var p = new Float3(axis == 0 ? at : 0f, axis == 1 ? at : 0f, axis == 2 ? at : 0f);
                        double inside = body.Parts[0].Family.Geometry.InsideFunction(p, body.Parts[0].HalfExtents);
                        points++;
                        Assert.True(inside <= 1.0 + 1e-5, $"{name} on the {halfName}, offset {offset}: F = {inside}");
                    }
                }
            }

            _out.WriteLine($"{points} arm points, all inside");
            Assert.Equal(44, points);
        }

        // ---------------------------------------------------------------- the reach

        [Fact]
        public void AFamilyLinksContactSphereIsTheSolidsReachUnderSphereContactAndFollowsAResize()
        {
            SolverConfig config = Water();
            Assert.False(config.CreatureContact);

            Phenotype adult = Developer.Develop(LimbGenome(new Float3(0.2f, 0.05f, 0.2f), new PartForm(1f, 1f, 4f)), DevelopmentLimits.Default, shapes: Registry);
            Creature body = Build(adult, config);

            double diagonal = Math.Sqrt(0.2 * 0.2 + 0.05 * 0.05 + 0.2 * 0.2);
            double reach = adult.Parts[1].Family.ReferenceSolidReach;
            Assert.Equal(reach, body.LinkReach[1]);
            Assert.True(body.LinkReach[1] > diagonal, "the half diagonal would not hold the part");

            // The root, a box, keeps its half diagonal to the bit.
            Float3 rootHalf = adult.Parts[0].HalfExtents;
            Assert.Equal(
                Bits(Math.Sqrt((double)rootHalf.X * rootHalf.X + (double)rootHalf.Y * rootHalf.Y + (double)rootHalf.Z * rootHalf.Z)),
                Bits(body.LinkReach[0]));

            Phenotype grown = adult.Scaled(0.5f, Registry);
            body.Resize(grown);
            Assert.Equal(Bits(reach * (double)grown.Parts[1].FamilyScale), Bits(body.LinkReach[1]));
        }

        // ---------------------------------------------------------------- a mirrored family limb

        [Fact]
        public void AMirroredFamilyLimbsCachedAddedMassIsTheOneTheSolversOwnGeometryGives()
        {
            double worst = 0.0;

            foreach (bool reflect in new[] { false, true })
            {
                foreach (Bool3 mirror in new[] { new Bool3(true, false, false), new Bool3(false, true, false) })
                {
                    foreach (Float3 anchor in new[] { new Float3(0f, -1f, 0f), new Float3(-0.5f, -1f, 0f) })
                    {
                        Genome g = LimbGenome(new Float3(0.2f, 0.1f, 0.15f), new PartForm(0.6f, 1f, 4f));
                        g.Nodes[0].Edges[0].Reflect = mirror;
                        g.Nodes[0].Edges[0].ChildAnchor = anchor;

                        var limits = DevelopmentLimits.Default.Clone();
                        limits.ReflectMirroredAnchors = reflect;
                        Phenotype adult = Developer.Develop(g, limits, shapes: Registry);
                        Assert.Equal(3, adult.PartCount);
                        Assert.True(adult.Parts[1].Mirrored != adult.Parts[2].Mirrored, "one of the pair is the mirror image");

                        SolverConfig config = Water();
                        var bodies = new[]
                        {
                            Build(adult, config),
                            BuildResized(adult, config, 0.6f),
                        };

                        foreach (Creature body in bodies)
                        {
                            for (int i = 1; i < body.Links; i++)
                            {
                                PhenotypePart part = body.Phenotype.Parts[i];
                                Assert.NotNull(part.Family);

                                // The solver's own anchor, the part's surface in the link frame at its present size, and
                                // the solver's dof axis.
                                Vec3 anchorV = Vec3.Read(body.ChildAnchor, 3 * i);
                                var joint = new Double3(anchorV.X, anchorV.Y, anchorV.Z);
                                Double3 axis = joint * (-1.0 / joint.Length);
                                QuatD frame = QuatD.Read(body.JointFrame, 4 * i);
                                Vec3 dofAxis = frame.Rotate(Creature.AxisOf(0));
                                Double3 motion = Double3.Cross(new Double3(dofAxis.X, dofAxis.Y, dofAxis.Z), axis);
                                Double3 stroke = motion * (1.0 / motion.Length);

                                Double3[] surface = part.Family.Geometry.SurfaceLattice(part.HalfExtents, Superellipsoid.StrokeResolution);
                                Superellipsoid.StrokeIntegrals(surface, joint, axis, stroke, out double m, out double i1);

                                int dof = body.DofStart[i];
                                double massError = body.FamilyAddedMass[dof] / (config.Density * m) - 1.0;
                                double momentError = body.FamilyFirstMoment[dof] / (config.Density * i1) - 1.0;
                                worst = Math.Max(worst, Math.Max(Math.Abs(massError), Math.Abs(momentError)));

                                string where = $"reflect {reflect}, mirror {mirror.X}/{mirror.Y}/{mirror.Z}, anchor {anchor}, link {i}, scale {part.FamilyScale}";
                                Assert.True(Math.Abs(massError) <= 0.005, $"{where}: M is {massError:P3} from the solver's geometry");
                                Assert.True(Math.Abs(momentError) <= 0.005, $"{where}: I1 is {momentError:P3} from the solver's geometry");
                            }
                        }
                    }
                }
            }

            _out.WriteLine($"worst difference between the cache and the solver's own geometry: {worst:P4}");
        }

        private static Creature BuildResized(Phenotype adult, SolverConfig config, float r)
        {
            Creature body = Build(adult, config);
            body.Resize(adult.Scaled(r, Registry));
            return body;
        }
    }
}
