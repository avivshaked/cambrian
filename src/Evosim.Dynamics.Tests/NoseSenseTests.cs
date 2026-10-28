using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// D235's nose as a body reads it: a world whose table lacks the nose steps as the build before
    /// it did, and in a world whose table lists it only nose parts smell.
    /// </summary>
    public sealed class NoseSenseTests
    {
        private readonly ITestOutputHelper _out;

        public NoseSenseTests(ITestOutputHelper output) => _out = output;

        private static readonly SensorChannel[] SmellPool =
        {
            SensorChannel.LeafScent, SensorChannel.LeafScentDirection, SensorChannel.Scent,
            SensorChannel.Chemical, SensorChannel.Flow,
        };

        // ------------------------------------------------------------------ the record

        /// <summary>
        /// The digest of <see cref="SmellingWorld"/> on the build before the nose (main at
        /// 7278dabd, recorded 2026-10-25 by running this test there).
        /// </summary>
        private const ulong RecordedDigest = 0x5970FDB5547CE540UL;

        /// <summary>
        /// Six chains whose brains read every smell, three under the per-part brain and three
        /// under the one brain, among two leaves, a corpse and a cloud of snow, for 300 steps.
        /// </summary>
        private static ulong SmellingWorld(CellTypeRegistry table, bool fields = true)
        {
            SolverConfig config = TestBodies.Water(0.01);
            config.LeafScentHalfScaleSquareMetres = 0.5;

            var leaf = new ScentField(40f, 40f, 20f, range: 8f, cell: 1f);
            leaf.FillSources(new[] { 18f, -10f, 20f, 0.6f, 23f, -9f, 21f, 0.3f }, 2);
            var carrion = new ScentField(40f, 40f, 20f, range: 8f, cell: 1f);
            carrion.FillSources(new[] { 22f, -11f, 19f, 30f }, 1);
            var snow = new GridField(1600f, 0f, 20f, 0f, 1f, 1, 1f);
            snow.Deposit(new FieldPoint(new Float3(20.5f, -10.5f, 20.5f), 0), 40f);
            snow.Deposit(new FieldPoint(new Float3(21.5f, -9.5f, 19.5f), 0), 25f);

            var world = new DynamicsWorld(config);
            for (int i = 0; i < 6; i++)
            {
                Genome g = i < 3 ? PerPartSmeller() : CentralSmeller(table, (ulong)(11 + i));
                Phenotype body = Developer.Develop(g, DevelopmentLimits.Default, cellTypes: table);
                var c = new Creature(i, body, config);
                c.PlaceAt(new Vec3(19.0 + 0.7 * i, -10.4 + 0.15 * i, 19.6 + 0.3 * (i % 3)), QuatD.Identity);
                if (fields)
                {
                    c.Senses.LeafScent = leaf;
                    c.Senses.Scent = carrion;
                    c.Senses.Nutrients = snow;
                }

                world.Add(c);
            }

            for (int s = 0; s < 300; s++) world.Step();
            return world.Digest();
        }

        /// <summary>A three-part chain whose joints are driven by sums of every smell on their own part.</summary>
        private static Genome PerPartSmeller()
        {
            Genome g = TestBodies.Chain(3, neurons: false);
            for (int n = 1; n < g.Nodes.Count; n++)
            {
                g.Nodes[n].Neurons = new[]
                {
                    new NeuronDef
                    {
                        Op = NeuronOp.Sum, Frequency = 1f, Amplitude = 1f, Bias = -0.6f,
                        Inputs = new[]
                        {
                            NeuronInput.FromSensor(SensorChannel.LeafScent, 0, 3f),
                            NeuronInput.FromSensor(SensorChannel.Scent, 0, 2f),
                            NeuronInput.FromSensor(SensorChannel.Chemical, 0, 2f),
                            NeuronInput.FromSensor(SensorChannel.LeafScentDirection, n - 1, 4f),
                        },
                    },
                };
            }

            return g;
        }

        /// <summary>A three-part chain under the one brain, wired densely from the smell pool.</summary>
        private static Genome CentralSmeller(CellTypeRegistry table, ulong seed)
        {
            Genome g = TestBodies.Chain(3, neurons: false);
            CentralWiring.Convert(g);
            var options = new CentralBrainOptions { FounderLinkShare = 1f };
            Phenotype body = Developer.Develop(g, DevelopmentLimits.Default, cellTypes: table);
            g.Brain = CentralWiring.Effective(g.Brain, body, new Rng(seed), SmellPool, options, founder: true);
            return g;
        }

        [Fact]
        public void AWorldWhoseTableLacksTheNoseStepsAsTheBuildBeforeIt()
        {
            ulong now = SmellingWorld(CellTypeRegistry.Standard);
            ulong blind = SmellingWorld(CellTypeRegistry.Standard, fields: false);
            _out.WriteLine($"digest {now:X16}, without the fields {blind:X16}");

            // The senses reach the trajectory, so the comparison below can fail.
            Assert.NotEqual(blind, now);
            Assert.Equal(RecordedDigest, now);
        }

        // ------------------------------------------------------------------ a nose world

        /// <summary>A body's senses as the farm sets them: sniffed by hand, with this seed and id.</summary>
        private static void Farmed(Creature c, long id, ulong seed = 99UL)
        {
            c.Senses.HeldSniff = true;
            c.Senses.NoiseSeed = seed;
            c.Senses.BodyId = id;
        }

        private const double Half = 0.5;

        private static CellTypeRegistry Table(NoseCell nose) => CellTypeRegistry.Standard.Appending(nose);

        /// <summary>The leaf field of the record, its two leaves near (20, -10, 20).</summary>
        private static ScentField Leaves()
        {
            var leaf = new ScentField(40f, 40f, 20f, range: 8f, cell: 1f);
            leaf.FillSources(new[] { 18f, -10f, 20f, 0.6f, 23f, -9f, 21f, 0.3f }, 2);
            return leaf;
        }

        /// <summary>
        /// A structural root and a nose of half-extents <paramref name="nose"/> on its +x face,
        /// both carrying a neuron that reads every smell, so every smell is on the mask.
        /// </summary>
        private static Creature Snout(NoseCell cell, Float3 nose, Vec3 at, string noseType = CellTypeIds.Nose)
        {
            SolverConfig config = TestBodies.Water(0.01);
            config.LeafScentHalfScaleSquareMetres = Half;
            config.Nose = cell;

            Genome g = TestBodies.Box(new Float3(0.2f, 0.2f, 0.2f));
            g.Nodes.Add(new MorphNode
            {
                Dimensions = nose, CellTypeId = noseType, ShapeId = ShapeIds.Box,
                JointType = JointType.Fixed, JointLimits = Array.Empty<Float2>(), RecursiveLimit = 1,
            });
            g.Nodes[0].Edges.Add(new MorphEdge
            {
                Child = 1, ParentAnchor = new Float3(1f, 0f, 0f), ChildAnchor = new Float3(-1f, 0f, 0f),
                Orientation = Quat.Identity, Scale = Float3.One,
            });
            foreach (MorphNode n in g.Nodes) n.Neurons = new[] { EverySmell() };

            Phenotype body = Developer.Develop(
                g, DevelopmentLimits.Default, cellTypes: cell != null ? Table(cell) : CellTypeRegistry.Standard);
            var c = new Creature(0, body, config);
            c.PlaceAt(at, QuatD.Identity);
            c.Senses.LeafScent = Leaves();
            return c;
        }

        private static NeuronDef EverySmell() => new NeuronDef
        {
            Op = NeuronOp.Sum, Frequency = 1f, Amplitude = 1f, Bias = 0f,
            Inputs = new[]
            {
                NeuronInput.FromSensor(SensorChannel.LeafScent, 0),
                NeuronInput.FromSensor(SensorChannel.LeafScentDirection, 0),
                NeuronInput.FromSensor(SensorChannel.Scent, 0),
                NeuronInput.FromSensor(SensorChannel.Chemical, 0),
            },
        };

        private static Vec3 Tip(Creature c, int part)
        {
            Float3 t = MouthPoint.Local(c.Phenotype, part);
            return Vec3.Read(c.Position, 3 * part) + Mat3.Read(c.RotationMatrix, 9 * part) * new Vec3(t.X, t.Y, t.Z);
        }

        private static readonly SensorChannel[] Smells =
            { SensorChannel.LeafScent, SensorChannel.LeafScentDirection, SensorChannel.Scent, SensorChannel.Chemical };

        [Fact]
        public void OnlyTheNoseSmellsAndItSmellsAtItsTip()
        {
            Creature c = Snout(new NoseCell(leafNoise: 0f, carrionNoise: 0f, chemicalNoise: 0f),
                new Float3(0.15f, 0.05f, 0.05f), new Vec3(20.2, -10, 20));
            var carrion = new ScentField(40f, 40f, 20f, range: 8f, cell: 1f);
            carrion.FillSources(new[] { 21f, -10f, 20f, 30f }, 1);
            // The snow in the tip's own cell, and none in the root's.
            Vec3 tip = Tip(c, 1);
            var snow = new GridField(1600f, 0f, 20f, 0f, 1f, 1, 1f);
            snow.Deposit(new FieldPoint(new Float3((float)tip.X, (float)tip.Y, (float)tip.Z), 0), 40f);
            c.Senses.Scent = carrion;
            c.Senses.Nutrients = snow;
            c.Senses.Sample();

            foreach (SensorChannel ch in Smells) Assert.Equal(0f, c.Senses.Read(0, ch, 0));

            float field = c.Senses.LeafScent.At((float)tip.X, (float)tip.Y, (float)tip.Z);
            float centre = c.Senses.LeafScent.At((float)c.Position[3], (float)c.Position[4], (float)c.Position[5]);
            float read = c.Senses.Read(1, SensorChannel.LeafScent, 0);
            _out.WriteLine($"tip {tip.X:0.###}: field {field:0.#####} (centre {centre:0.#####}), read {read:0.#####}");

            Assert.NotEqual(field, centre);
            Assert.Equal((float)(field / (field + Half)), read);
            Assert.True(c.Senses.Read(1, SensorChannel.Scent, 0) > 0f);
            Assert.True(c.Senses.Read(1, SensorChannel.Chemical, 0) > 0f);

            // The same body in a world whose table lacks the nose smells everywhere, at the centre.
            Creature old = Snout(null, new Float3(0.15f, 0.05f, 0.05f), new Vec3(20.2, -10, 20), CellTypeIds.Structural);
            old.Senses.Sample();
            Assert.Equal(centre / (centre + (float)Half), old.Senses.Read(1, SensorChannel.LeafScent, 0), 6);
            Assert.True(old.Senses.Read(0, SensorChannel.LeafScent, 0) > 0f);
        }

        /// <summary>
        /// D234: a mirrored twin's nose reads the leaf's direction in its frame reflected, as every other part
        /// reads it, so two noses mirrored in x among leaves mirrored in x read the same direction.
        /// </summary>
        [Fact]
        public void TwinNosesMirroredInXReadTheSameDirection()
        {
            var cell = new NoseCell(leafNoise: 0f, carrionNoise: 0f, chemicalNoise: 0f);
            SolverConfig config = TestBodies.Water(0.01);
            config.LeafScentHalfScaleSquareMetres = Half;
            config.Nose = cell;

            Genome g = TestBodies.Box(new Float3(0.2f, 0.2f, 0.2f));
            g.Nodes.Add(new MorphNode
            {
                Dimensions = new Float3(0.15f, 0.05f, 0.05f), CellTypeId = CellTypeIds.Nose, ShapeId = ShapeIds.Box,
                JointType = JointType.Fixed, JointLimits = Array.Empty<Float2>(), RecursiveLimit = 1,
            });
            g.Nodes[0].Edges.Add(new MorphEdge
            {
                Child = 1, ParentAnchor = new Float3(1f, 0.3f, 0.2f), ChildAnchor = new Float3(-1f, 0f, 0f),
                Orientation = Quat.FromEuler(0.2f, 0.3f, 0.1f), Scale = Float3.One, Reflect = new Bool3(true, false, false),
            });
            foreach (MorphNode n in g.Nodes) n.Neurons = new[] { EverySmell() };

            var limits = new DevelopmentLimits { MaxParts = 64, ReflectMirroredAnchors = true };
            Phenotype body = Developer.Develop(g, limits, cellTypes: Table(cell));
            Assert.Equal(3, body.Parts.Count);

            var c = new Creature(0, body, config);
            c.PlaceAsDeveloped(new Vec3(20, -10, 20), body);

            // Two leaves mirrored in the plane x = 20, which is a cell boundary of the field.
            var leaf = new ScentField(40f, 40f, 20f, range: 8f, cell: 1f);
            leaf.FillSources(new[] { 17f, -9.6f, 20.7f, 0.5f, 23f, -9.6f, 20.7f, 0.5f }, 2);
            c.Senses.LeafScent = leaf;
            c.Senses.Sample();

            Vec3 a = Tip(c, 1), b = Tip(c, 2);
            _out.WriteLine($"tips ({a.X:0.####}, {a.Y:0.####}, {a.Z:0.####}) and ({b.X:0.####}, {b.Y:0.####}, {b.Z:0.####})");
            Assert.Equal(40.0 - a.X, b.X, 5);

            float x1 = c.Senses.Read(1, SensorChannel.LeafScentDirection, 0);
            Assert.True(Math.Abs(x1) > 0.01f, $"the original reads {x1} along its x, too little to tell a flip");
            for (int axis = 0; axis < 3; axis++)
            {
                float one = c.Senses.Read(1, SensorChannel.LeafScentDirection, axis);
                float two = c.Senses.Read(2, SensorChannel.LeafScentDirection, axis);
                _out.WriteLine($"axis {axis}: original {one:0.######}, twin {two:0.######}");
                Assert.Equal(one, two, 4);
            }
        }

        [Fact]
        public void ASniffIsHeldThroughThePhysicsStepsUntilTheNext()
        {
            Creature c = Snout(new NoseCell(), new Float3(0.1f, 0.05f, 0.05f), new Vec3(20.2, -10, 20));
            Farmed(c, 7);

            // Nothing is read before the first sniff; Sample leaves a farmed nose alone.
            c.Senses.Sample();
            Assert.Equal(0f, c.Senses.Read(1, SensorChannel.LeafScent, 0));

            c.Senses.Sniff(1);
            float first = c.Senses.Read(1, SensorChannel.LeafScent, 0);
            Assert.True(first > 0f);

            c.PlaceAt(new Vec3(21.5, -9.6, 20.4), QuatD.Identity);
            c.Senses.Sample();
            Assert.Equal(first, c.Senses.Read(1, SensorChannel.LeafScent, 0));

            c.Senses.Sniff(2);
            Assert.NotEqual(first, c.Senses.Read(1, SensorChannel.LeafScent, 0));

            // The same sniff of the same body at the same place reads the same, and another body does not.
            Creature twin = Snout(new NoseCell(), new Float3(0.1f, 0.05f, 0.05f), new Vec3(21.5, -9.6, 20.4));
            Farmed(twin, 7);
            twin.Senses.Sniff(2);
            Assert.Equal(c.Senses.Read(1, SensorChannel.LeafScent, 0), twin.Senses.Read(1, SensorChannel.LeafScent, 0));

            Creature other = Snout(new NoseCell(), new Float3(0.1f, 0.05f, 0.05f), new Vec3(21.5, -9.6, 20.4));
            Farmed(other, 8);
            other.Senses.Sniff(2);
            Assert.NotEqual(c.Senses.Read(1, SensorChannel.LeafScent, 0), other.Senses.Read(1, SensorChannel.LeafScent, 0));
        }

        /// <summary>The raw counts a nose read over <paramref name="sniffs"/> sniffs, unsquashed.</summary>
        private static double[] Counts(Creature c, int sniffs, Action<Creature> before = null)
        {
            Farmed(c, 3);
            var counts = new double[sniffs];
            for (int s = 0; s < sniffs; s++)
            {
                before?.Invoke(c);
                c.Senses.Sniff(s + 1);
                c.Senses.Sample();
                double r = c.Senses.Read(1, SensorChannel.LeafScent, 0);
                counts[s] = Half * r / (1.0 - r);
            }

            return counts;
        }

        private static (double mean, double sd) Spread(double[] x)
        {
            double mean = 0;
            foreach (double v in x) mean += v;
            mean /= x.Length;
            double ss = 0;
            foreach (double v in x) ss += (v - mean) * (v - mean);
            return (mean, Math.Sqrt(ss / (x.Length - 1)));
        }

        [Fact]
        public void FourTimesTheAreaReadsWithHalfTheNoiseAroundTheField()
        {
            // The background far above the field, so the noise is the reference noise scaled by area alone.
            var cell = new NoseCell(leafNoise: 0.02f, leafBackground: 1000f);
            Creature small = Snout(cell, new Float3(0.1f, 0.05f, 0.05f), new Vec3(20.2, -10, 20));
            Creature large = Snout(cell, new Float3(0.2f, 0.1f, 0.1f), new Vec3(20.2, -10, 20));

            Assert.Equal(4.0, large.Phenotype.Parts[1].SurfaceArea / small.Phenotype.Parts[1].SurfaceArea, 4);

            (double m1, double s1) = Spread(Counts(small, 2000));
            (double m2, double s2) = Spread(Counts(large, 2000));

            Vec3 t1 = Tip(small, 1), t2 = Tip(large, 1);
            float f1 = small.Senses.LeafScent.At((float)t1.X, (float)t1.Y, (float)t1.Z);
            float f2 = large.Senses.LeafScent.At((float)t2.X, (float)t2.Y, (float)t2.Z);
            double expected = cell.Sigma(NoseSmell.Leaf, f1, small.Phenotype.Parts[1].SurfaceArea);
            _out.WriteLine($"small: mean {m1:0.#####} (field {f1:0.#####}), sd {s1:0.#####} (sigma {expected:0.#####}); " +
                           $"large: mean {m2:0.#####} (field {f2:0.#####}), sd {s2:0.#####}; ratio {s1 / s2:0.###}");

            Assert.InRange(s1 / s2, 1.85, 2.15);
            Assert.InRange(s1 / expected, 0.93, 1.07);
            Assert.True(Math.Abs(m1 - f1) < 4 * s1 / Math.Sqrt(2000));
            Assert.True(Math.Abs(m2 - f2) < 4 * s2 / Math.Sqrt(2000));
        }

        [Fact]
        public void FacingIntoTheWaterWidensTheNoseOnlyWhenTheIntakeIsOn()
        {
            void Swim(Creature c)
            {
                // Body minus water, along the nose's outward axis (+x): the nose moves into the water.
                c.RelativeVelocity[3] = 0.3;
                c.RelativeVelocity[4] = 0;
                c.RelativeVelocity[5] = 0;
            }

            var off = new NoseCell(leafNoise: 0.02f, leafBackground: 1000f);
            var on = new NoseCell(leafNoise: 0.02f, leafBackground: 1000f, facingIntake: true, intakeFullScaleMetresPerSecond: 0.3f);
            var at = new Vec3(20.2, -10, 20);
            var half = new Float3(0.1f, 0.05f, 0.05f);

            double still = Spread(Counts(Snout(on, half, at), 2000)).sd;
            double swimming = Spread(Counts(Snout(on, half, at), 2000, Swim)).sd;
            double[] offStill = Counts(Snout(off, half, at), 500);
            double[] offSwimming = Counts(Snout(off, half, at), 500, Swim);
            _out.WriteLine($"intake on: still {still:0.#####}, swimming {swimming:0.#####}, ratio {still / swimming:0.###}");

            Assert.InRange(still / swimming, Math.Sqrt(2) * 0.92, Math.Sqrt(2) * 1.08);
            Assert.Equal(offStill, offSwimming);
        }

        [Fact]
        public void TheDirectionIsTheGradientAtTheTipAndALongerNosePointsBetter()
        {
            var exact = new NoseCell(leafNoise: 0f);
            Creature c = Snout(exact, new Float3(0.1f, 0.05f, 0.05f), new Vec3(20.2, -10, 20));
            Farmed(c, 1);
            c.Senses.Sniff(1);

            Vec3 tip = Tip(c, 1);
            Float3 g = c.Senses.LeafScent.GradientAt((float)tip.X, (float)tip.Y, (float)tip.Z);
            var unit = new Vec3(g.X, g.Y, g.Z) * (1.0 / new Vec3(g.X, g.Y, g.Z).Magnitude);
            float strength = c.Senses.Read(1, SensorChannel.LeafScent, 0);
            Vec3 d = Direction(c);
            Assert.Equal(unit.X * strength, d.X, 5);
            Assert.Equal(unit.Y * strength, d.Y, 5);
            Assert.Equal(unit.Z * strength, d.Z, 5);

            // Noise on: a long thin nose and a short one of the same area, the long one's bearing closer.
            var noisy = new NoseCell(leafNoise: 0.004f, leafBackground: 1000f);
            double Error(Float3 half)
            {
                Creature n = Snout(noisy, half, new Vec3(20.2, -10, 20));
                Farmed(n, 1);
                double sum = 0;
                for (int s = 0; s < 400; s++)
                {
                    n.Senses.Sniff(s + 1);
                    Vec3 at = Tip(n, 1);
                    Float3 grad = n.Senses.LeafScent.GradientAt((float)at.X, (float)at.Y, (float)at.Z);
                    var truth = new Vec3(grad.X, grad.Y, grad.Z);
                    Vec3 read = Direction(n);
                    double cos = (truth.X * read.X + truth.Y * read.Y + truth.Z * read.Z) / (truth.Magnitude * read.Magnitude);
                    sum += Math.Acos(Math.Max(-1.0, Math.Min(1.0, cos)));
                }

                return sum / 400;
            }

            // Areas 8(0.32·0.05 + 0.05·0.05 + 0.32·0.05) = 0.276 and 8(0.1·0.1 + 0.1·0.1 + 0.1·0.1) = 0.24, close.
            double longNose = Error(new Float3(0.32f, 0.05f, 0.05f));
            double shortNose = Error(new Float3(0.1f, 0.1f, 0.1f));
            _out.WriteLine($"mean bearing error: long {longNose:0.###} rad, short {shortNose:0.###} rad");
            Assert.True(longNose < 0.6 * shortNose);
        }

        private static Vec3 Direction(Creature c) => new Vec3(
            c.Senses.Read(1, SensorChannel.LeafScentDirection, 0),
            c.Senses.Read(1, SensorChannel.LeafScentDirection, 1),
            c.Senses.Read(1, SensorChannel.LeafScentDirection, 2));

        /// <summary>
        /// The record's leaves, with the nose in the table and a nose on the end of every chain,
        /// each chain under the one brain; the noses sniff every 50 physics steps, as the farm sniffs
        /// at each metabolic step.
        /// </summary>
        private static ulong NoseWorld(NoseCell nose, ulong seed, bool fields, int bodies = 6, bool noses = true)
        {
            SolverConfig config = TestBodies.Water(0.01);
            config.LeafScentHalfScaleSquareMetres = 0.5;
            config.Nose = nose;
            CellTypeRegistry table = Table(nose);

            var leaf = Leaves();
            var world = new DynamicsWorld(config);
            for (int i = 0; i < bodies; i++)
            {
                Genome g = PerPartSmeller();
                if (noses)
                {
                    // The chain's end link carries a nose on its +x face.
                    g.Nodes.Add(new MorphNode
                    {
                        Dimensions = new Float3(0.12f, 0.04f, 0.04f), CellTypeId = CellTypeIds.Nose, ShapeId = ShapeIds.Box,
                        JointType = JointType.Fixed, JointLimits = Array.Empty<Float2>(), RecursiveLimit = 1,
                        Neurons = new[] { EverySmell() },
                    });
                    g.Nodes[2].Edges.Add(new MorphEdge
                    {
                        Child = 3, ParentAnchor = new Float3(1f, 0f, 0f), ChildAnchor = new Float3(-1f, 0f, 0f),
                        Orientation = Quat.Identity, Scale = Float3.One,
                    });
                }

                // Under the one brain, so a nose's ports reach every joint; a nose has no joint of its own.
                CentralWiring.Convert(g);
                var options = new CentralBrainOptions { FounderLinkShare = 1f };
                g.Brain = CentralWiring.Effective(
                    g.Brain, Developer.Develop(g, DevelopmentLimits.Default, cellTypes: table),
                    new Rng((ulong)(31 + i)), SmellPool, options, founder: true);

                var c = new Creature(i, Developer.Develop(g, DevelopmentLimits.Default, cellTypes: table), config);
                c.PlaceAt(new Vec3(19.0 + 0.7 * i, -10.4 + 0.15 * i, 19.6 + 0.3 * (i % 3)), QuatD.Identity);
                Farmed(c, 100 + i, seed);
                if (fields) c.Senses.LeafScent = leaf;
                world.Add(c);
            }

            for (int s = 0; s < 300; s++)
            {
                // The farm's sniff, at the top of each metabolic step's block.
                if (s % 50 == 0)
                {
                    foreach (Creature c in world.Creatures) c.Senses.Sniff(s / 50);
                }

                world.Step();
            }

            return world.Digest();
        }

        [Fact]
        public void ANoseWorldReplaysAndItsNoiseAndItsGateReachTheTrajectory()
        {
            var nose = new NoseCell(leafNoise: 0.1f);
            ulong once = NoseWorld(nose, 5UL, fields: true);
            ulong again = NoseWorld(nose, 5UL, fields: true);
            ulong reseeded = NoseWorld(nose, 6UL, fields: true);
            ulong exact = NoseWorld(new NoseCell(leafNoise: 0f), 5UL, fields: true);
            ulong blind = NoseWorld(nose, 5UL, fields: false);
            _out.WriteLine($"once {once:X16}, again {again:X16}, reseeded {reseeded:X16}, exact {exact:X16}, blind {blind:X16}");

            Assert.Equal(once, again);
            Assert.NotEqual(once, reseeded);
            Assert.NotEqual(once, exact);
            Assert.NotEqual(once, blind);

            // Bodies with no nose in a nose world smell nothing: the field changes nothing they do.
            Assert.Equal(
                NoseWorld(nose, 5UL, fields: false, noses: false),
                NoseWorld(nose, 5UL, fields: true, noses: false));
        }
    }
}
