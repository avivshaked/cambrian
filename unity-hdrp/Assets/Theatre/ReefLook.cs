using System;
using Evosim.Core;

namespace Evosim.Theatre
{
    /// <summary>
    /// What the theatre needs to know about one mushroom reef to draw it as rock, read from the
    /// world's <see cref="ReefGeometry"/> in exactly one place (<see cref="From"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One contract.</b> The rock look (<see cref="TheatreReefRock"/>, the skin's reef
    /// holders, the rock shader's per-reef properties) reads this struct and nothing else from the
    /// geometry. D118 makes the reefs many, of random size and with irregular outlines; when the
    /// geometry grows its per-reef members, the only code that changes is the marked block in
    /// <see cref="From"/>, and the meshes already take the outline as a function of angle.
    /// </para>
    /// <para>
    /// <b>The frame.</b> A reef's own frame has its origin on the axis at the cap's mid-plane,
    /// y up, x and z the world's. The skin places each reef's holder there, unrotated, so a
    /// mesh's object space and the shader's object-space position are this frame, and the rock's
    /// noise is fixed to the reef rather than to the world.
    /// </para>
    /// </remarks>
    public readonly struct ReefLook
    {
        /// <summary>The axis's x in the world, m.</summary>
        public readonly float CentreX;

        /// <summary>The axis's z in the world, m.</summary>
        public readonly float CentreZ;

        /// <summary>The height of the cap's top, m (negative under the surface).</summary>
        public readonly float CapTopY;

        /// <summary>The height of the cap's underside, m.</summary>
        public readonly float CapUndersideY;

        /// <summary>
        /// The cap's radius at an angle about the axis, m: the angle in radians measured from +x
        /// toward +z, the radius the rock's analytic surface reaches at that angle (the rim's
        /// outermost point). Constant until D118's geometry lands.
        /// </summary>
        public readonly Func<float, float> Outline;

        /// <summary>The stem's radius, m; 0 for a floating island.</summary>
        public readonly float StemRadius;

        /// <summary>The seed the rock's noise is offset by, so no two reefs carry the same relief.</summary>
        public readonly int Seed;

        public ReefLook(
            float centreX, float centreZ, float capTopY, float capUndersideY,
            Func<float, float> outline, float stemRadius, int seed)
        {
            CentreX = centreX;
            CentreZ = centreZ;
            CapTopY = capTopY;
            CapUndersideY = capUndersideY;
            Outline = outline ?? throw new ArgumentNullException(nameof(outline));
            StemRadius = stemRadius;
            Seed = seed;
        }

        /// <summary>The cap's thickness, m.</summary>
        public float CapThickness => CapTopY - CapUndersideY;

        /// <summary>The height of the cap's mid-plane, m: the reef frame's origin.</summary>
        public float MidY => 0.5f * (CapTopY + CapUndersideY);

        /// <summary>
        /// The largest radius the outline reaches, sampled at <paramref name="samples"/> angles.
        /// </summary>
        public float MaxRadius(int samples = 64)
        {
            float max = 0f;
            for (int k = 0; k < samples; k++)
            {
                float r = Outline(2f * (float)Math.PI * k / samples);
                if (r > max) max = r;
            }
            return max;
        }

        /// <summary>The seed as a number in [0, 1), for a shader.</summary>
        public float SeedUnit => (float)((uint)Seed % 1000003u) / 1000003f;

        /// <summary>The look of reef <paramref name="i"/> of <paramref name="reefs"/>.</summary>
        public static ReefLook From(ReefGeometry reefs, int i)
        {
            if (reefs == null) throw new ArgumentNullException(nameof(reefs));
            if (i < 0 || i >= reefs.Count) throw new ArgumentOutOfRangeException(nameof(i));

            float x = (float)reefs.CentreX(i);
            float z = (float)reefs.CentreZ(i);

            // ==================================================================================
            // MERGE POINT (D118, the per-reef geometry). Today every reef shares one set of
            // dimensions and the outline is a circle. When ReefGeometry carries the five
            // per-reef members, replace the five lines below with:
            //
            //     Func<float, float> outline = theta => (float)reefs.OutlineRadius(i, theta);
            //     float capTop    = (float)reefs.CapTopY(i);
            //     float underside = (float)reefs.CapUndersideY(i);
            //     float stem      = (float)reefs.StemRadius(i);
            //     int seed        = reefs.NoiseSeed(i);
            //
            // and nothing else in the rock look changes.
            // ----------------------------------------------------------------------------------
            // D118's geometry, merged 2026-09-23 night: the per-reef outline, depths, stem and
            // the seed the reef stream drew, so each rock's relief is a property of the seed.
            Func<float, float> outline = theta => (float)reefs.OutlineRadius(i, theta);
            float capTop = (float)reefs.CapTopY(i);
            float underside = (float)reefs.CapUndersideY(i);
            float stem = (float)reefs.StemRadius(i);
            int seed = unchecked((int)reefs.NoiseSeed(i));
            // ==================================================================================

            return new ReefLook(x, z, capTop, underside, outline, stem, seed);
        }

        /// <summary>
        /// A seed from the reef's index and its centre, so a reef keeps its relief across every
        /// frame of one run and two runs with reefs in different places do not share it.
        /// </summary>
        private static int SeedOf(int i, float x, float z)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)i) * 16777619u;
                h = (h ^ (uint)(int)Math.Round(x * 1000d)) * 16777619u;
                h = (h ^ (uint)(int)Math.Round(z * 1000d)) * 16777619u;
                h ^= h >> 15; h *= 0x2c1b3c6du; h ^= h >> 12;
                return (int)(h & 0x7fffffff);
            }
        }
    }
}
