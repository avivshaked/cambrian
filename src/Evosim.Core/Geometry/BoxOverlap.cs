using System;

namespace Evosim.Core
{
    /// <summary>
    /// Whether two oriented boxes occupy the same space, and by how much — the separating-axis
    /// test, and the box a developed part presents to it. The owner's ruling of 2026-09-19.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What it is for.</b> Round 41c's knot and round 41d's sprawl are bodies whose developed
    /// parts stand inside their own non-adjacent parts (logbook/0107). PhysX resolves every such
    /// pair on every step and never separates them, which is the physics bill; the light economy
    /// paid the same body twice over as well, which D099 closed. This is the geometry the world
    /// asks before it lets such a body be born.
    /// </para>
    /// <para>
    /// <b>Ported out of the probe, not written twice.</b> <c>src/Evosim.Overlap</c> sized the
    /// problem with exactly this arithmetic; it now calls this, so the number the probe reports
    /// of a snapshot and the number the world refuses a birth on cannot drift apart — the same
    /// reason <see cref="ConvexHull"/> came here from the same probe a day earlier.
    /// </para>
    /// <para>
    /// <b>Deterministic, and no dependencies.</b> Fifteen candidate axes, single-precision
    /// throughout, every loop over indices in the caller's order. Two runs over the same pair
    /// give the same bits, which is what §7 asks of anything a trajectory can branch on.
    /// </para>
    /// </remarks>
    public static class BoxOverlap
    {
        /// <summary>An oriented box: centre, three unit axes, half-extents.</summary>
        public readonly struct Obb
        {
            /// <summary>Centre, world frame.</summary>
            public readonly Float3 C;

            /// <summary>The box's own axes, unit length.</summary>
            public readonly Float3 U0, U1, U2;

            /// <summary>Half-extents along <see cref="U0"/>, <see cref="U1"/>, <see cref="U2"/>. Never negative.</summary>
            public readonly Float3 E;

            public Obb(Float3 c, Float3 u0, Float3 u1, Float3 u2, Float3 e)
            {
                C = c; U0 = u0; U1 = u1; U2 = u2; E = e;
            }

            /// <summary>
            /// The box a developed part presents. Every shape is taken as a box of the part's
            /// half-extents — a sphere and a capsule included, which over-states them, since both
            /// are inscribed in that box.
            /// </summary>
            /// <remarks>
            /// Over-stating is the safe direction for a rule that refuses a birth: a round part
            /// judged as its bounding box is judged a little more harshly than it deserves, where
            /// asking the shape would let a pair of spheres whose boxes interpenetrate through.
            /// It is also what the probe measured the problem with, so the world refuses what the
            /// probe counted.
            /// </remarks>
            public static Obb From(PhenotypePart p)
            {
                if (p == null) throw new ArgumentNullException(nameof(p));

                Quat q = p.Rotation;
                return new Obb(
                    p.Position,
                    q.Rotate(new Float3(1f, 0f, 0f)),
                    q.Rotate(new Float3(0f, 1f, 0f)),
                    q.Rotate(new Float3(0f, 0f, 1f)),
                    new Float3(Math.Abs(p.HalfExtents.X), Math.Abs(p.HalfExtents.Y), Math.Abs(p.HalfExtents.Z)));
            }

            private Float3 Axis(int i) => i == 0 ? U0 : (i == 1 ? U1 : U2);

            /// <summary>
            /// Separating-axis test over the 15 candidate axes. <paramref name="depth"/> is the
            /// least overlap found, in metres — the minimum-translation penetration — and is
            /// meaningless when the answer is false.
            /// </summary>
            public static bool Intersect(in Obb a, in Obb b, out float depth)
            {
                const float Eps = 1e-6f;
                depth = float.MaxValue;

                var r = new float[3, 3];
                var absR = new float[3, 3];
                for (int i = 0; i < 3; i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        r[i, j] = Float3.Dot(a.Axis(i), b.Axis(j));
                        absR[i, j] = Math.Abs(r[i, j]) + Eps;
                    }
                }

                Float3 d = b.C - a.C;
                var t = new float[3] { Float3.Dot(d, a.U0), Float3.Dot(d, a.U1), Float3.Dot(d, a.U2) };
                var ea = new float[3] { a.E.X, a.E.Y, a.E.Z };
                var eb = new float[3] { b.E.X, b.E.Y, b.E.Z };

                // A's three face normals.
                for (int i = 0; i < 3; i++)
                {
                    float ra = ea[i];
                    float rb = eb[0] * absR[i, 0] + eb[1] * absR[i, 1] + eb[2] * absR[i, 2];
                    float over = ra + rb - Math.Abs(t[i]);
                    if (over <= 0f) return false;
                    if (over < depth) depth = over;
                }

                // B's three face normals.
                for (int j = 0; j < 3; j++)
                {
                    float ra = ea[0] * absR[0, j] + ea[1] * absR[1, j] + ea[2] * absR[2, j];
                    float rb = eb[j];
                    float tj = Math.Abs(t[0] * r[0, j] + t[1] * r[1, j] + t[2] * r[2, j]);
                    float over = ra + rb - tj;
                    if (over <= 0f) return false;
                    if (over < depth) depth = over;
                }

                // The nine edge-edge cross products. Their ra, rb and distance all carry a factor
                // of the axis length, so the overlap is divided by it to come back to metres; a
                // near-parallel pair is skipped, which is what the face axes above already cover.
                for (int i = 0; i < 3; i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        int i1 = (i + 1) % 3, i2 = (i + 2) % 3;
                        int j1 = (j + 1) % 3, j2 = (j + 2) % 3;

                        float ra = ea[i1] * absR[i2, j] + ea[i2] * absR[i1, j];
                        float rb = eb[j1] * absR[i, j2] + eb[j2] * absR[i, j1];
                        float tt = Math.Abs(t[i2] * r[i1, j] - t[i1] * r[i2, j]);
                        float over = ra + rb - tt;
                        if (over <= 0f) return false;

                        float len = (float)Math.Sqrt(Math.Max(0f, 1f - r[i, j] * r[i, j]));
                        if (len < 1e-4f) continue;
                        float scaled = over / len;
                        if (scaled < depth) depth = scaled;
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// The depth two boxes have to interpenetrate by before the overlap counts, metres —
        /// <paramref name="depthFraction"/> times the smallest half-extent of the smaller box.
        /// </summary>
        /// <remarks>
        /// Relative to the smaller box, and to its thinnest axis, so that the same fraction means
        /// the same thing for a body of any size: a half-metre link and a centimetre one are both
        /// asked to be buried by the same share of themselves. That also makes the rule invariant
        /// under <see cref="Phenotype.Scaled"/> — a newborn is its adult times one factor, and
        /// both the depth and this threshold carry that factor — so a body is judged the same
        /// whether it is asked at birth or at full size.
        /// </remarks>
        public static float DepthThreshold(in Obb a, in Obb b, float depthFraction)
        {
            Float3 ea = a.E, eb = b.E;
            Float3 smaller = ea.BoxVolume <= eb.BoxVolume ? ea : eb;
            float thinnest = Math.Min(Math.Abs(smaller.X), Math.Min(Math.Abs(smaller.Y), Math.Abs(smaller.Z)));
            return depthFraction * thinnest;
        }
    }
}
