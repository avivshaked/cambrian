using System;
using System.Globalization;
using UnityEngine;
using Evosim.Core;

namespace Evosim.Theatre
{
    // FAMILYSKIN begin
    /// <summary>
    /// A superellipsoid part (D233) drawn by the one skin (<see cref="TheatreOneSkin"/>) as a primitive of its own kind, under
    /// <c>EVOSIM_THEATRE_FAMILY_SKIN</c>, in place of the box the skin falls back to for a shape it has no case for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The dial.</b> 0 (the default) or 1, read once through <see cref="TheatreSkin.Dial"/>. At 0 nothing here runs but the
    /// read: every statement of the one skin that reaches this class is guarded on <see cref="Enabled"/>, so a frame is the
    /// one without it, to the pixel, with any other dial on or off.
    /// </para>
    /// <para>
    /// <b>The field.</b> <see cref="Solid.At"/> is the signed distance, in the part's own frame (the frame shifted to the centre
    /// of mass, as Core's <see cref="Superellipsoid"/> works in) and in the metres the skin works in. With the form's
    /// exponents ε1 (profile) and ε2 (section), the taper slope k, the shift c (<see cref="Superellipsoid.CentreOffset"/> over b),
    /// t = (y + c b) / b and f = 1 + k t, Core's inside function is
    /// <c>F = (|x/(a f)|^(2/ε2) + |z/(c f)|^(2/ε2))^(ε2/ε1) + |t|^(2/ε1)</c>, at most 1 inside and infinite beyond the ends.
    /// F is homogeneous of degree 2/ε1 in the untapered unit frame (x/(a f), t, z/(c f)), so <c>G = F^(ε1/2)</c> grows in step
    /// with length along a ray from the centre. The field is <c>(G - 1) x min(a f, b, c f)</c>: zero exactly on Core's surface,
    /// negative inside and positive outside. Like the ellipsoid kind's Quilez bound it is an estimate: it reads less than the
    /// distance where the solid is much longer than it is wide (the smallest local half extent stands in for all three), and
    /// with a section exponent ε2 above 1 it can read up to 2^(ε2/2 - 1/2) times the distance across a section's diagonal (about
    /// 1.27 at ε2 1.7), since that section's norm exceeds the round one there. The zero set is exact either way; only the
    /// smooth joins' widths read the size of the field. The skin's cull box is grown for the low reading (<see cref="Solid.Pad"/>).
    /// </para>
    /// <para>
    /// <b>Beyond the ends</b> (|t| greater than 1) Core returns infinity. The field takes the same formula with f at the nearest end
    /// (<c>1 + k sign(t)</c>, positive since |k| is at most 0.6) and the true t in the length term. That has a finite value
    /// everywhere, because |t|^(2/ε1) exceeds 1 there, so G exceeds 1 and the field is positive; it meets the inside field at the
    /// end plane (f is continuous there), and no slab distance is needed to give it a value.
    /// </para>
    /// <para>
    /// <b>No rounding of its own.</b> The skin insets every primitive by k/4 and joins them by the smooth minimum, whatever the
    /// kind; the family kind adds no rounding or offset, so a family part's skin follows Core's surface and never stands wider
    /// than it (D252).
    /// </para>
    /// </remarks>
    public static class TheatreFamilySkin
    {
        /// <summary>The dial: 0 off (the default), 1 on. Read once.</summary>
        public static readonly bool Enabled = TheatreSkin.Dial("EVOSIM_THEATRE_FAMILY_SKIN", 0f, 0f, 1f) >= 0.5f;

        /// <summary>A reference half extent under this is no solid to draw; the part keeps the box.</summary>
        private const float Smallest = 1e-6f;

        /// <summary>Cells along a cube face's edge when the first family part's field is checked against Core's surface points.</summary>
        private const int CheckSteps = 8;

        private static bool _logged;

        /// <summary>
        /// Whether this part is drawn as a family primitive: the dial is on, the part has a family, and its reference half extents
        /// and scale are positive (the same test <see cref="TheatreFamilyMesh.Wants"/> makes, without that dial).
        /// </summary>
        public static bool Wants(PhenotypePart part)
        {
            if (!Enabled || part == null) return false;

            FamilyGeometry family = part.Family;
            if (family == null) return false;

            Float3 r = family.ReferenceHalfExtents;

            return Mathf.Abs(r.X) > Smallest && Mathf.Abs(r.Y) > Smallest && Mathf.Abs(r.Z) > Smallest
                   && part.FamilyScale > 0f;
        }

        /// <summary>
        /// The part's solid at half extents <paramref name="h"/> (metres, absolute, the collider's own), or null when the part is not
        /// a family part or a half extent is under <see cref="Smallest"/>.
        /// </summary>
        public static Solid SolidOf(PhenotypePart part, Vector3 h)
        {
            if (!Wants(part)) return null;
            if (!(h.x > Smallest && h.y > Smallest && h.z > Smallest)) return null;

            return new Solid(part.Family.Geometry, new Float3(h.x, h.y, h.z));
        }

        /// <summary>
        /// The two lines the log prints, once, at the first family primitive built: what the kind is, then the largest
        /// |field| of the solid over Core's <see cref="Superellipsoid.SurfacePointAndNormal"/> points (the cube lattice
        /// <see cref="TheatreFamilyMesh"/> walks, <see cref="CheckSteps"/> by <see cref="CheckSteps"/> cells to a face), in
        /// millimetres: zero up to the rounding of the float and of the shift's float.
        /// </summary>
        public static void LogFirst(Solid solid, PhenotypePart part, Float3 h)
        {
            if (_logged) return;
            _logged = true;

            Superellipsoid geometry = part.Family.Geometry;

            Debug.Log(
                "[Theatre] EVOSIM_THEATRE_FAMILY_SKIN=1: a superellipsoid part with a family is a primitive of its own kind in the one skin " +
                "(G = F^(e1/2) of Core's inside function, field (G - 1) x min(a f, b, c f), collider kind 3 at levels 5 and 6, no paddle, " +
                "no drawn-box taper or bend)");

            double worst = 0.0;
            int points = 0;

            for (int axis = 0; axis < 3; axis++)
            {
                for (int side = 0; side < 2; side++)
                {
                    double sign = side == 0 ? 1.0 : -1.0;

                    for (int i = 0; i <= CheckSteps; i++)
                    {
                        for (int j = 0; j <= CheckSteps; j++)
                        {
                            Double3 cube = CubePoint(axis, sign, -1.0 + 2.0 * i / CheckSteps, -1.0 + 2.0 * j / CheckSteps);
                            Double3 p = geometry.SurfacePointAndNormal(cube, h, out Double3 _);
                            double d = Math.Abs(solid.At((float)p.X, (float)p.Y, (float)p.Z));

                            if (d > worst) worst = d;
                            points++;
                        }
                    }
                }
            }

            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[Theatre] family skin check: form {0}, half extents ({1:0.0000}, {2:0.0000}, {3:0.0000}) m, " +
                "largest |field| over Core's {4} surface points {5:0.000} mm",
                geometry.Form, h.X, h.Y, h.Z, points, 1000.0 * worst));
        }

        private static Double3 CubePoint(int axis, double sign, double u, double v)
        {
            double x = 0.0, y = 0.0, z = 0.0;

            Set(ref x, ref y, ref z, axis, sign);
            Set(ref x, ref y, ref z, (axis + 1) % 3, u);
            Set(ref x, ref y, ref z, (axis + 2) % 3, v);

            return new Double3(x, y, z);
        }

        private static void Set(ref double x, ref double y, ref double z, int axis, double value)
        {
            if (axis == 0) x = value;
            else if (axis == 1) y = value;
            else z = value;
        }

        /// <summary>
        /// One family part's field, at the half extents the part is drawn at: Core's form and the constants the distance needs,
        /// worked out once.
        /// </summary>
        public sealed class Solid
        {
            /// <summary>Samples of t in [-1, 1] the widest slice (<see cref="SideReach"/>) is found over.</summary>
            private const int ReachSamples = 128;

            /// <summary>The half extents, metres.</summary>
            public readonly double A, B, C;

            /// <summary>The taper's slope k along t.</summary>
            public readonly double Slope;

            /// <summary>The centre of mass's offset over b: the shifted frame's y is b (t - Centre).</summary>
            public readonly double Centre;

            /// <summary>
            /// The half extents of a box about the frame's origin that holds the whole solid: the widest slice across, the
            /// farthest pole along Y.
            /// </summary>
            public readonly Vector3 Extent;

            /// <summary>
            /// How many cull margins the cull box grows by on every side. The field can read less than the distance by up to the
            /// solid's aspect, so a primitive is left out of a sample only beyond its box grown by the margin times this.
            /// </summary>
            public readonly float Pad;

            /// <summary>The widest slice over the half extent across: a joint's face on a side stands at this times the half extent.</summary>
            public readonly float SideReach;

            /// <summary>How far the +Y pole and the -Y pole stand from the frame's origin along Y, metres.</summary>
            public readonly float PoleHigh, PoleLow;

            private readonly double _sectionPower;      // 2/ε2
            private readonly double _sectionToProfile;  // ε2/ε1
            private readonly double _profilePower;      // 2/ε1
            private readonly double _halfProfile;       // ε1/2

            internal Solid(Superellipsoid geometry, Float3 h)
            {
                PartForm form = geometry.Form;

                A = Math.Abs((double)h.X);
                B = Math.Abs((double)h.Y);
                C = Math.Abs((double)h.Z);

                Slope = geometry.TaperSlope;
                Centre = geometry.CentreOffset(h) / B;

                double e1 = form.Profile, e2 = form.Section;
                _sectionPower = 2.0 / e2;
                _sectionToProfile = e2 / e1;
                _profilePower = 2.0 / e1;
                _halfProfile = e1 / 2.0;

                double wide = 1.0 + Math.Abs(Slope);
                Extent = new Vector3((float)(A * wide), (float)(B * (1.0 + Math.Abs(Centre))), (float)(C * wide));

                double reach = 0.0;
                for (int i = 0; i <= ReachSamples; i++)
                {
                    double t = -1.0 + 2.0 * i / ReachSamples;
                    double s = 1.0 - Math.Pow(Math.Abs(t), _profilePower);
                    double w = s > 0.0 ? Math.Pow(s, _halfProfile) : 0.0;
                    reach = Math.Max(reach, (1.0 + Slope * t) * w);
                }

                SideReach = (float)reach;
                PoleHigh = (float)(B * (1.0 - Centre));
                PoleLow = (float)(B * (1.0 + Centre));

                // The aspect the cull box is padded for: the farthest reach over the smallest half extent at the narrow end.
                double narrow = 1.0 - Math.Abs(Slope);
                double least = Math.Min(A * narrow, Math.Min(B, C * narrow));
                double most = Math.Max(Extent.x, Math.Max(Extent.y, Extent.z));
                Pad = (float)Math.Min(12.0, Math.Max(0.0, 1.8 * most / Math.Max(least, 1e-9) - 1.0));
            }

            /// <summary>
            /// The field at a point of the part's own frame, metres: <c>(G - 1) x min(a f, b, c f)</c>, with t the true position along
            /// the length and f taken at t clamped to [-1, 1] (so at the nearest end beyond it). Zero on Core's surface.
            /// </summary>
            public float At(float lx, float ly, float lz)
            {
                double t = (ly + Centre * B) / B;
                double tc = t < -1.0 ? -1.0 : t > 1.0 ? 1.0 : t;
                double f = 1.0 + Slope * tc;

                double ux = lx / (A * f), uz = lz / (C * f);
                double r = Math.Pow(Math.Abs(ux), _sectionPower) + Math.Pow(Math.Abs(uz), _sectionPower);
                double inside = Math.Pow(r, _sectionToProfile) + Math.Pow(Math.Abs(t), _profilePower);

                // Capped so a far sample stays a finite number the blends can subtract.
                double g = Math.Min(Math.Pow(inside, _halfProfile), 1e6);
                double scale = Math.Min(A * f, Math.Min(B, C * f));

                return (float)((g - 1.0) * scale);
            }
        }
    }
    // FAMILYSKIN end
}
