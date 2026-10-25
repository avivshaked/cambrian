using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// Contact across the box's seams, D204 (<c>RunConfig.ReadAcrossSeams</c>): two bodies on
    /// either side of a seam touch as the same two bodies touch in the middle of the box, and a
    /// pair that does not reach across a seam is pushed to the bit as it was without the setting.
    /// </summary>
    /// <remarks>
    /// The forces are read off <see cref="Creature.Fext"/> after one contact pass, called
    /// directly, as <see cref="PerPartContactTests"/> reads them. A pair across a seam and its
    /// copy in the middle are not the same bits, since the two differences of centres round
    /// differently, so they are held equal to a part in a billion.
    /// </remarks>
    public sealed class SeamContactTests
    {
        private const double Length = 300.0;
        private const double Width = 300.0;

        private readonly ITestOutputHelper _out;

        public SeamContactTests(ITestOutputHelper output) => _out = output;

        /// <summary>
        /// Across the x seam, across the z seam and across the corner, under both contact
        /// models: the push is the one the same pair gets in the middle of the box.
        /// </summary>
        [Theory]
        [InlineData(false, 1, 0)]
        [InlineData(false, 0, 1)]
        [InlineData(false, 1, 1)]
        [InlineData(true, 1, 0)]
        [InlineData(true, 0, 1)]
        [InlineData(true, 1, 1)]
        public void APairAcrossASeamIsPushedAsTheSamePairInTheMiddle(bool perPart, int acrossX, int acrossZ)
        {
            // B sits 0.25 m along x and 0.2 m along z from A; across a seam that offset is taken
            // round it, so A is just inside the far face and B just inside the near one. The two
            // spheres reach 0.47 m together, so even the corner's 0.32 m overlaps.
            var offset = new Vec3(0.25 * acrossX + 0.05 * (1 - acrossX), 0.02, 0.2 * acrossZ + 0.04 * (1 - acrossZ));

            var middleA = new Vec3(150.0, -20.0, 150.0);
            var seamA = new Vec3(
                acrossX == 1 ? Length - 0.1 : 150.0,
                -20.0,
                acrossZ == 1 ? Width - 0.12 : 150.0);
            var seamB = new Vec3(
                acrossX == 1 ? seamA.X + offset.X - Length : seamA.X + offset.X,
                seamA.Y + offset.Y,
                acrossZ == 1 ? seamA.Z + offset.Z - Width : seamA.Z + offset.Z);

            (double[] middleRowA, double[] middleRowB) = Pair(perPart, seamed: true, middleA, middleA + offset);
            (double[] seamRowA, double[] seamRowB) = Pair(perPart, seamed: true, seamA, seamB);

            _out.WriteLine("middle: A " + Format(middleRowA) + "  B " + Format(middleRowB));
            _out.WriteLine("seam:   A " + Format(seamRowA) + "  B " + Format(seamRowB));

            Assert.True(Magnitude(middleRowA) > 0, "the pair in the middle was meant to overlap and was not pushed");
            Near(middleRowA, seamRowA, "A");
            Near(middleRowB, seamRowB, "B");
        }

        /// <summary>
        /// Without the setting the same pair across the seam is a box apart and does not touch,
        /// which is the recorded box.
        /// </summary>
        [Fact]
        public void WithoutSeamsAPairAcrossTheSeamDoesNotTouch()
        {
            var a = new Vec3(Length - 0.1, -20.0, 150.0);
            var b = new Vec3(0.25, -19.98, 150.05);

            (double[] rowA, double[] rowB) = Pair(perPart: true, seamed: false, a, b);

            Assert.Equal(0.0, Magnitude(rowA));
            Assert.Equal(0.0, Magnitude(rowB));
        }

        /// <summary>
        /// A pair that does not reach across a seam takes no new float: its push with the seams
        /// joined is its push without them, to the bit, beside a seam and in the middle.
        /// </summary>
        [Theory]
        [InlineData(false, 150.0)]
        [InlineData(true, 150.0)]
        [InlineData(false, 0.6)]
        [InlineData(true, 0.6)]
        [InlineData(true, 299.2)]
        public void APairThatDoesNotReachASeamIsPushedToTheBitAsWithoutSeams(bool perPart, double x)
        {
            var a = new Vec3(x, -20.0, 150.0);
            var b = a + new Vec3(-0.3, 0.02, 0.05);

            (double[] plainA, double[] plainB) = Pair(perPart, seamed: false, a, b);
            (double[] seamA, double[] seamB) = Pair(perPart, seamed: true, a, b);

            Assert.True(Magnitude(plainA) > 0, "the pair was meant to overlap and was not pushed");
            SameBits(plainA, seamA, "A");
            SameBits(plainB, seamB, "B");
        }

        /// <summary>
        /// The grid finds a neighbour across the seam however far past the face a sphere's centre
        /// lies (a link of a body whose root is inside), and lists it once.
        /// </summary>
        [Fact]
        public void TheGridListsANeighbourAcrossTheSeamOnce()
        {
            SolverConfig config = Config(perPart: false, seamed: true);

            Creature a = TestBodies.Build(TestBodies.Box(new Float3(0.13f, 0.11f, 0.17f)), config, id: 3);
            Creature b = TestBodies.Build(TestBodies.Box(new Float3(0.19f, 0.07f, 0.12f)), config, id: 8);

            // A's centre past the far face, B's just inside the near one: 0.3 m apart round it.
            a.PlaceAt(new Vec3(Length + 0.05, -20.0, 150.0), QuatD.Identity);
            b.PlaceAt(new Vec3(-0.25 + 0.6, -20.0, 150.0), QuatD.Identity);
            a.RefreshContactSphere();
            a.CommitContactSphere();
            b.RefreshContactSphere();
            b.CommitContactSphere();

            var grid = new ContactGrid { SeamLength = Length, SeamWidth = Width };
            grid.Build(new List<Creature> { a, b });

            int[] scratch = new int[1];
            Assert.Equal(1, grid.Neighbours(0, ref scratch));
            Assert.Equal(1, scratch[0]);
            Assert.Equal(1, grid.Neighbours(1, ref scratch));
            Assert.Equal(0, scratch[0]);
        }

        /// <summary>The difference is moved by the box only past half of it, and once.</summary>
        [Fact]
        public void TheShortWayMovesOnlyPastHalfTheBox()
        {
            SolverConfig config = Config(perPart: false, seamed: true);

            var within = new Vec3(149.9, 3.0, -149.9);
            Vec3 kept = Contacts.ShortWay(within, config);
            Assert.Equal(BitConverter.DoubleToInt64Bits(within.X), BitConverter.DoubleToInt64Bits(kept.X));
            Assert.Equal(BitConverter.DoubleToInt64Bits(within.Z), BitConverter.DoubleToInt64Bits(kept.Z));

            Vec3 moved = Contacts.ShortWay(new Vec3(299.65, 3.0, -299.7), config);
            Assert.Equal(-0.35, moved.X, 9);
            Assert.Equal(3.0, moved.Y);
            Assert.Equal(0.3, moved.Z, 9);
        }

        // --------------------------------------------------------------- helpers

        private static SolverConfig Config(bool perPart, bool seamed)
        {
            SolverConfig config = TestBodies.Water(0.01);
            config.CreatureContact = true;
            config.ContactPerPart = perPart;
            if (seamed)
            {
                config.SeamLengthMetres = Length;
                config.SeamWidthMetres = Width;
            }

            return config;
        }

        private static (double[] A, double[] B) Pair(bool perPart, bool seamed, Vec3 atA, Vec3 atB)
        {
            SolverConfig config = Config(perPart, seamed);

            // PerPartContactTests' two unround boxes, turned, and moving so the damper is in the sum.
            Creature a = TestBodies.Build(TestBodies.Box(new Float3(0.13f, 0.11f, 0.17f)), config, id: 3);
            Creature b = TestBodies.Build(TestBodies.Box(new Float3(0.19f, 0.07f, 0.12f)), config, id: 8);

            a.PlaceAt(atA, QuatD.FromAxisAngle(new Vec3(0.3, 1.0, -0.2).Normalized, 0.7));
            b.PlaceAt(atB, QuatD.FromAxisAngle(new Vec3(-0.6, 0.2, 1.0).Normalized, 1.9));

            SetVelocity(a, new Vec3(0.3, -0.2, 0.1));
            SetVelocity(b, new Vec3(-0.1, 0.05, 0.4));

            var list = new List<Creature> { a, b };
            var grid = new ContactGrid { SeamLength = config.SeamLengthMetres, SeamWidth = config.SeamWidthMetres };

            if (config.ContactPerPart) grid.BuildLinks(list);
            else grid.Build(list);

            int[] scratch = new int[8];
            for (int i = 0; i < list.Count; i++)
            {
                Array.Clear(list[i].Fext, 0, list[i].Fext.Length);
                Contacts.Apply(list[i], i, grid, config, ref scratch);
            }

            return (Row(a), Row(b));
        }

        private static void SetVelocity(Creature body, Vec3 v)
        {
            for (int i = 0; i < body.Links; i++) Vec3.Write(body.Velocity, 3 * i, v);
            body.RefreshContactSphere();
            body.CommitContactSphere();
        }

        private static double[] Row(Creature body)
        {
            var row = new double[6];
            Array.Copy(body.Fext, 0, row, 0, 6);
            return row;
        }

        private static double Magnitude(double[] row) =>
            Math.Sqrt(row[3] * row[3] + row[4] * row[4] + row[5] * row[5]);

        private static void Near(double[] expected, double[] actual, string what)
        {
            double scale = Math.Max(1e-12, Magnitude(expected));
            for (int k = 0; k < expected.Length; k++)
            {
                Assert.True(
                    Math.Abs(expected[k] - actual[k]) <= 1e-9 * scale,
                    FormattableString.Invariant(
                        $"{what}, component {k}: in the middle {expected[k]:R}, across the seam {actual[k]:R}"));
            }
        }

        private static void SameBits(double[] expected, double[] actual, string what)
        {
            for (int k = 0; k < expected.Length; k++)
            {
                Assert.True(
                    BitConverter.DoubleToInt64Bits(expected[k]) == BitConverter.DoubleToInt64Bits(actual[k]),
                    FormattableString.Invariant(
                        $"{what}, component {k}: without seams {expected[k]:R}, with them {actual[k]:R}"));
            }
        }

        private static string Format(double[] row) =>
            FormattableString.Invariant($"[{row[0]:R}, {row[1]:R}, {row[2]:R} | {row[3]:R}, {row[4]:R}, {row[5]:R}]");
    }
}
