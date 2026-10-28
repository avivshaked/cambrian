using System;
using System.Collections.Generic;
using System.Text;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D255, step 2's geometry: the strip integral of a family part's true slices
    /// (<see cref="Superellipsoid.StrokeIntegrals(Double3[], Double3, Double3, Double3, out double, out double)"/>),
    /// its resolution against a fine reference, and its scaling law.
    /// </summary>
    public class FamilyStrokeIntegralTests
    {
        private readonly ITestOutputHelper _output;

        public FamilyStrokeIntegralTests(ITestOutputHelper output) => _output = output;

        /// <summary>The fine reference: points along a face edge of the lattice.</summary>
        private const int ReferenceResolution = 256;

        private static readonly int[] Candidates = { 16, 24, 32, 48, 64 };

        // The fin bench's paddle and stick (src/Evosim.SwimProbe/FinBench.cs), copied.
        private static readonly Float3 Paddle = new Float3(0.2f, 0.01f, 0.2f);
        private static readonly Float3 Stick = new Float3(0.2f, (float)Math.Sqrt(0.002), (float)Math.Sqrt(0.002));

        private static readonly Float3[] Anchors =
        {
            new Float3(0f, -1f, 0f),       // the joint on -Y
            new Float3(-1f, 0f, 0f),       // on -X
            new Float3(-0.6f, -0.8f, 0f),  // tilted
        };

        private static Double3 Unit(Double3 v) => v * (1.0 / v.Length);

        // The three stroke directions of a limb: the cross products of its axis with x, y and z, a zero one skipped.
        private static IEnumerable<Double3> Strokes(Double3 axis)
        {
            foreach (Double3 e in new[] { new Double3(1, 0, 0), new Double3(0, 1, 0), new Double3(0, 0, 1) })
            {
                Double3 motion = Double3.Cross(axis, e);
                if (motion.Length > 1e-12) yield return Unit(motion);
            }
        }

        private static Double3 First(IEnumerable<Double3> strokes)
        {
            foreach (Double3 s in strokes) return s;
            throw new InvalidOperationException("no stroke");
        }

        private static Double3 AxisOf(Float3 joint) =>
            Unit(new Double3(-(double)joint.X, -(double)joint.Y, -(double)joint.Z));

        [Fact]
        public void TheBuildResolutionHoldsTheStrokeIntegralsWithinTwoPercentOfTheFineReferenceOverTheBenchForms()
        {
            var worst = new double[Candidates.Length];
            var worstAt = new string[Candidates.Length];
            int cases = 0;

            foreach ((string name, PartForm form) in FamilyPathsTests.BenchForms())
            {
                var geometry = new Superellipsoid(form);
                foreach ((string halfName, Float3 h) in new[] { ("paddle", Paddle), ("stick", Stick) })
                {
                    Double3[] fine = geometry.SurfaceLattice(h, ReferenceResolution);
                    var lattices = new Double3[Candidates.Length][];
                    for (int r = 0; r < Candidates.Length; r++) lattices[r] = geometry.SurfaceLattice(h, Candidates[r]);

                    for (int a = 0; a < Anchors.Length; a++)
                    {
                        Float3 jointF = geometry.SurfacePoint(Anchors[a], h);
                        var joint = new Double3(jointF.X, jointF.Y, jointF.Z);
                        Double3 axis = AxisOf(jointF);

                        foreach (Double3 stroke in Strokes(axis))
                        {
                            Superellipsoid.StrokeIntegrals(fine, joint, axis, stroke, out double mRef, out double iRef);
                            Assert.True(mRef > 0.0 && iRef > 0.0, $"{name} on the {halfName}, anchor {a}: the reference is {mRef}, {iRef}");
                            cases++;

                            for (int r = 0; r < Candidates.Length; r++)
                            {
                                Superellipsoid.StrokeIntegrals(lattices[r], joint, axis, stroke, out double m, out double i1);
                                double error = Math.Max(Math.Abs(m / mRef - 1.0), Math.Abs(i1 / iRef - 1.0));
                                if (error > worst[r])
                                {
                                    worst[r] = error;
                                    worstAt[r] = $"{name} on the {halfName}, anchor {a}, stroke ({stroke.X:0.##}, {stroke.Y:0.##}, {stroke.Z:0.##})";
                                }
                            }
                        }
                    }
                }
            }

            int chosen = -1;
            var table = new StringBuilder();
            for (int r = 0; r < Candidates.Length; r++)
            {
                table.AppendLine($"R = {Candidates[r]}: worst error {worst[r]:P3} at {worstAt[r]}");
                if (chosen < 0 && worst[r] <= 0.02) chosen = Candidates[r];
            }

            _output.WriteLine($"{cases} cases against R = {ReferenceResolution}.\n{table}smallest R within 2 %: {chosen}");
            Assert.True(chosen > 0, "no candidate resolution holds the gate\n" + table);
            Assert.Equal(chosen, Superellipsoid.StrokeResolution);
        }

        [Fact]
        public void TheIntegralsScaleAsTheCubeAndTheFourthPowerOfTheSize()
        {
            var geometry = new Superellipsoid(new PartForm(0.6f, 1f, 4f));
            var h = new Float3(0.1f, 0.3f, 0.07f);
            var doubled = new Float3(0.2f, 0.6f, 0.14f);

            Float3 jointF = geometry.SurfacePoint(new Float3(-0.6f, -0.8f, 0f), h);
            Float3 jointD = geometry.SurfacePoint(new Float3(-0.6f, -0.8f, 0f), doubled);
            Double3 axis = AxisOf(jointF);
            Double3 stroke = First(Strokes(axis));

            geometry.StrokeIntegrals(h, new Double3(jointF.X, jointF.Y, jointF.Z), axis, stroke, Superellipsoid.StrokeResolution, out double m1, out double i1);
            geometry.StrokeIntegrals(doubled, new Double3(jointD.X, jointD.Y, jointD.Z), AxisOf(jointD), stroke, Superellipsoid.StrokeResolution, out double m2, out double i2);

            Assert.True(Math.Abs(m2 / m1 - 8.0) < 1e-4, $"M ratio {m2 / m1}");
            Assert.True(Math.Abs(i2 / i1 - 16.0) < 1e-4, $"I1 ratio {i2 / i1}");
        }

        [Fact]
        public void TheIntegralsAreTheSameBitsOnEveryCall()
        {
            var geometry = new Superellipsoid(new PartForm(0.3f, 0.7f, 2.5f));
            Float3 h = Paddle;
            Float3 jointF = geometry.SurfacePoint(new Float3(-0.6f, -0.8f, 0f), h);
            var joint = new Double3(jointF.X, jointF.Y, jointF.Z);
            Double3 axis = AxisOf(jointF);

            foreach (Double3 stroke in Strokes(axis))
            {
                geometry.StrokeIntegrals(h, joint, axis, stroke, Superellipsoid.StrokeResolution, out double m1, out double i1);
                geometry.StrokeIntegrals(h, joint, axis, stroke, Superellipsoid.StrokeResolution, out double m2, out double i2);
                Assert.Equal(BitConverter.DoubleToInt64Bits(m1), BitConverter.DoubleToInt64Bits(m2));
                Assert.Equal(BitConverter.DoubleToInt64Bits(i1), BitConverter.DoubleToInt64Bits(i2));
            }
        }

        [Fact]
        public void AStrokeAlongTheAxisHasNothingAcrossItAndGivesZero()
        {
            var geometry = new Superellipsoid(new PartForm(1f, 1f, 1f));
            var h = new Float3(0.1f, 0.3f, 0.07f);
            Float3 jointF = geometry.SurfacePoint(new Float3(0f, -1f, 0f), h);
            Double3 axis = AxisOf(jointF);

            geometry.StrokeIntegrals(h, new Double3(jointF.X, jointF.Y, jointF.Z), axis, axis, Superellipsoid.StrokeResolution, out double m, out double i1);
            Assert.Equal(0.0, m);
            Assert.Equal(0.0, i1);
        }
    }

}
