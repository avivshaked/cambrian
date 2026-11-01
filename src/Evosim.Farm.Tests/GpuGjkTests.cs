using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using SM3 = Evosim.Farm.Gpu.Sgl.M3;
using SV3 = Evosim.Farm.Gpu.Sgl.V3;
using SWholeStep = Evosim.Farm.Gpu.Sgl.WholeStep;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// The card's single-precision GJK (Kernels.F, GjkDistance and the routines under it), run on the host CPU on the
    /// inputs of the census dump of r64t-t-6203 (scratch/card-dump, 22,500.5 s): bodies 66874 and 70081, link 0 each, two
    /// flat cores 0.41 m apart that the card called "inside". The host arithmetic never fuses a multiply and an add, and
    /// the card's PTX does unless the template says otherwise, so the host cannot show the contraction itself. These tests
    /// show what the template does about its effect: the held-vertex test sees the support point the card computes (the
    /// kernel writes the GJK's products uncontracted), and a tetrahedron too flat to trust is not "inside".
    /// </summary>
    public class GpuGjkTests
    {
        private static readonly Type W = typeof(SWholeStep);
        private const BindingFlags All = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;

        private static MethodInfo M(string name) => W.GetMethod(name, All) ?? throw new MissingMethodException(name);
        private static int C(string name) => (int)W.GetField(name, All).GetValue(null);

        // The dump's "pair" row where this = 66874, the lower id and so the first core: the second core sits at minus
        // the sphere test's difference, as TrueShapePair arranges it.
        private const float MarginA = 0.18448352813720703f, MarginB = 0.04627169668674469f;

        private static readonly SV3 HalfA = new SV3(0.11199533939361572f, 0f, 0.05556827783584595f);
        private static readonly SV3 HalfB = new SV3(0.14487624168395996f, 0f, 0.29996174573898315f);
        private static readonly SM3 RotA = new SM3(
            -0.4724644422531128f, -0.6638033390045166f, -0.5797781944274902f,
            0.6492398977279663f, 0.18274831771850586f, -0.7383025884628296f,
            0.596041202545166f, -0.725236713886261f, 0.3446253538131714f);
        private static readonly SM3 RotB = new SM3(1, 0, 0, 0, 1, 0, 0, 0, 1);
        private static readonly SV3 CentreA = SV3.Zero;
        private static readonly SV3 CentreB = SV3.Zero - new SV3(-0.5414701104164124f, 0.3025156855583191f, -0.05074639618396759f);

        /// <summary>The host's distance for these inputs, and the double solver's 0.41222807.</summary>
        private const float HostDistance = 0.41222808f;

        [Fact]
        public void TheDumpedPairIsApartOnTheHostAtTheHostsDistance()
        {
            var s = new float[C("GjkSimplex")];
            object[] args = { HalfA, RotA, CentreA, HalfB, RotB, CentreB, s, 0f, null, null };
            bool apart = (bool)M("GjkDistance").Invoke(null, args);

            Assert.True(apart, "the cores are 0.41 m apart; the dump's card ended 'inside' with distance 0");
            Assert.InRange((float)args[7], HostDistance - 1e-7f, HostDistance + 1e-7f);
        }

        /// <summary>
        /// The host's loop up to the point the card diverged: the simplex after pass 1 (three vertices) and the support
        /// point pass 2 computes, which the host finds already held. Read through the kernel's own routines.
        /// </summary>
        private static (float[] Simplex, int N, SV3 WA, SV3 WB) PassTwo(out string ended, out int pass)
        {
            MethodInfo reduce = M("GjkReduce"), support = M("GjkSupport"), add = M("GjkAddVertex"), vertex = M("GjkVertex"), held = M("GjkHeld");
            int wBlock = C("GjkW"), lBlock = C("GjkL");
            var s = new float[C("GjkSimplex")];

            SV3 first = CentreB - CentreA;
            object[] a = { s, 0, support.Invoke(null, new object[] { HalfA, RotA, CentreA, first }),
                           support.Invoke(null, new object[] { HalfB, RotB, CentreB, SV3.Zero - first }) };
            add.Invoke(null, a);
            int n = (int)a[1];
            s[lBlock] = 1f;

            ended = "cap";
            for (pass = 0; pass < 32; pass++)
            {
                object[] r = { s, n, null };
                if (!(bool)reduce.Invoke(null, r)) { ended = "inside"; break; }
                n = (int)r[1];
                SV3 v = (SV3)r[2];
                SV3 wa = (SV3)support.Invoke(null, new object[] { HalfA, RotA, CentreA, SV3.Zero - v });
                SV3 wb = (SV3)support.Invoke(null, new object[] { HalfB, RotB, CentreB, v });
                SV3 w = wa - wb;
                if ((bool)held.Invoke(null, new object[] { s, n, w })) { ended = "held"; return (s, n, wa, wb); }
                object[] aa = { s, n, wa, wb };
                add.Invoke(null, aa);
                n = (int)aa[1];
            }

            return (s, 0, default, default);
        }

        [Fact]
        public void TheHostsTraceEndsOnAHeldVertexAtPassTwo()
        {
            var (_, n, _, _) = PassTwo(out string ended, out int pass);
            Assert.Equal("held", ended);
            Assert.Equal(2, pass);
            Assert.Equal(3, n);
        }

        /// <summary>
        /// The card's pass 2, by hand: the support point a contracted product leaves differs from the held vertex in its
        /// last bits, so the exact held-vertex test misses and the loop adds a fourth vertex, a copy of the held one in
        /// all but a few ulps. That tetrahedron has no volume, and the origin is 0.41 m from it. Every such nudge of every
        /// held vertex, on each axis and in each direction, is reduced to a nearest point, never to "the origin is inside".
        /// </summary>
        [Fact]
        public void AFourthVertexThatIsAHeldOneToWithinAFewUlpsIsNeverInside()
        {
            MethodInfo reduce = M("GjkReduce"), add = M("GjkAddVertex"), vertex = M("GjkVertex");
            int aBlock = C("GjkA"), bBlock = C("GjkB");

            var (baseSimplex, n, _, _) = PassTwo(out _, out _);
            Assert.Equal(3, n);

            int tried = 0, inside = 0;
            for (int k = 0; k < 3; k++)
            {
                SV3 heldA = (SV3)vertex.Invoke(null, new object[] { baseSimplex, aBlock, k });
                SV3 heldB = (SV3)vertex.Invoke(null, new object[] { baseSimplex, bBlock, k });

                for (int nudge = 0; nudge < 27; nudge++)
                {
                    // A's support point moved on each axis by -1, 0 or +1 ulp, as a contracted sum differs from the rounded one.
                    int dx = nudge % 3 - 1, dy = nudge / 3 % 3 - 1, dz = nudge / 9 - 1;
                    if (dx == 0 && dy == 0 && dz == 0) continue;
                    SV3 moved = new SV3(Ulps(heldA.X, dx), Ulps(heldA.Y, dy), Ulps(heldA.Z, dz));

                    var s = (float[])baseSimplex.Clone();
                    object[] aa = { s, 3, moved, heldB };
                    add.Invoke(null, aa);
                    int count = (int)aa[1];

                    object[] r = { s, count, null };
                    bool nearest = (bool)reduce.Invoke(null, r);
                    tried++;
                    if (!nearest) inside++;
                }
            }

            Assert.True(tried > 0);
            Assert.True(inside == 0, inside + " of " + tried + " near-duplicate fourth vertices were taken for 'the origin is inside'");
        }

        /// <summary>
        /// Near-flat tetrahedra found by search (scratch/gjk-fix/work/search): three vertices on a plane 0.1 to 0.5 m from
        /// the origin, the fourth inside their triangle and off the plane by a micrometre or less. The origin is outside each
        /// by the plane's distance. Float sign tests on the faces of such a tetrahedron are noise, and these were the
        /// ones where the noise agreed on all four faces, so GjkTetrahedron called the origin enclosed. Each row is the
        /// four vertices, twelve floats.
        /// </summary>
        public static IEnumerable<object[]> FlatTetrahedra()
        {
            yield return new object[] { new[] {
                -0.46923187f, 0.2513538f, 0.0958111f, -0.09717794f, 0.43312785f, 0.121181384f,
                -0.21768655f, 0.46924737f, -0.09842329f, -0.23618235f, 0.39338866f, 0.04900936f } };
            yield return new object[] { new[] {
                -0.06753061f, 0.2986339f, -0.024135182f, 0.292481f, 0.17105924f, -0.11411851f,
                0.3067869f, 0.17192514f, -0.14654174f, 0.038101293f, 0.26204443f, -0.05463228f } };
            yield return new object[] { new[] {
                0.06300326f, 0.5734886f, -0.057685867f, -0.5732417f, 0.28512365f, -0.012659871f,
                -0.37378925f, 0.39149716f, -0.04875669f, -0.33594942f, 0.39440492f, -0.031837747f } };
        }

        [Theory]
        [MemberData(nameof(FlatTetrahedra))]
        public void AFlatTetrahedronWithTheOriginWellOutsideIsNotInside(float[] vertices)
        {
            MethodInfo reduce = M("GjkReduce"), add = M("GjkAddVertex");
            var s = new float[C("GjkSimplex")];
            int n = 0;
            for (int k = 0; k < 4; k++)
            {
                // The difference's vertex is what the tests read; A's and B's are the weights' rest and are not looked at here.
                object[] aa = { s, n, new SV3(vertices[3 * k], vertices[3 * k + 1], vertices[3 * k + 2]), SV3.Zero };
                add.Invoke(null, aa);
                n = (int)aa[1];
            }

            Assert.Equal(4, n);

            // The plane through the first three vertices, in double: the origin's distance from it.
            double[] p0 = D(vertices, 0), p1 = D(vertices, 1), p2 = D(vertices, 2);
            double[] u = Sub(p1, p0), v = Sub(p2, p0);
            double[] nrm = { u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0] };
            double len = Math.Sqrt(nrm[0] * nrm[0] + nrm[1] * nrm[1] + nrm[2] * nrm[2]);
            double plane = Math.Abs(-(p0[0] * nrm[0] + p0[1] * nrm[1] + p0[2] * nrm[2]) / len);
            Assert.True(plane > 0.1);

            object[] r = { s, n, null };
            bool nearest = (bool)reduce.Invoke(null, r);

            Assert.True(nearest, "the origin is " + plane + " m from this flat tetrahedron and was called inside it");

            // And the nearest point is no nearer than the plane (the tetrahedron lies in it to a micrometre).
            SV3 point = (SV3)r[2];
            double distance = Math.Sqrt((double)point.X * point.X + (double)point.Y * point.Y + (double)point.Z * point.Z);
            Assert.InRange(distance, plane - 1e-4, plane + 1.0);
        }

        private static double[] D(float[] v, int k) => new double[] { v[3 * k], v[3 * k + 1], v[3 * k + 2] };
        private static double[] Sub(double[] a, double[] b) => new[] { a[0] - b[0], a[1] - b[1], a[2] - b[2] };

        private static float Ulps(float x, int steps)
        {
            for (; steps > 0; steps--) x = MathF.BitIncrement(x);
            for (; steps < 0; steps++) x = MathF.BitDecrement(x);
            return x;
        }
    }
}
