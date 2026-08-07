using System;

namespace Evosim.Core
{
    /// <summary>
    /// How much light reaches a given depth — DESIGN.md §5A.4.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Light is the world's only primary energy input (§5A.2)</b>, so this is where every
    /// joule in the ecosystem comes from. Nutrients are recycled dead matter and predation moves
    /// energy sideways; nothing else adds any.
    /// </para>
    /// <para>
    /// <b>Beer–Lambert, because the alternative has no shape.</b> Irradiance falls exponentially
    /// with depth: each metre of water removes a fixed <i>fraction</i>, not a fixed amount. A
    /// linear falloff would reach exactly zero at a particular depth and be meaningless below it,
    /// which puts a hard floor under the world at a place we chose. Exponential decay has no such
    /// edge — it gets arbitrarily dark without ever becoming a different regime — so how deep the
    /// photosynthetic niche reaches is set by the economics of §5A.2 rather than by a constant
    /// here.
    /// </para>
    /// <para>
    /// This is the cheapest source of <b>spatial heterogeneity</b> in the design, and §5A.4 wants
    /// heterogeneity because it is what stops one strategy winning everywhere: the surface is the
    /// photosynthetic niche and the depths are the detritus niche, maintained by geometry rather
    /// than by tuning.
    /// </para>
    /// <para>
    /// No diurnal cycle yet. §5A.4 wants one and it is what would make diel vertical migration
    /// possible, but a cycle turns the calibration question in §5A.2 from "does light cover
    /// upkeep" into "does light cover upkeep averaged over a period, and can anything survive the
    /// trough" — two unknowns at once, before either has been measured alone.
    /// </para>
    /// </remarks>
    public sealed class LightModel
    {
        /// <summary>Irradiance just below the surface, W/m².</summary>
        /// <remarks>
        /// ⚠ Unmeasured (§5A.10), and half of the ratio §5A.2 calls the knob that decides
        /// everything. Its absolute value means little on its own — what matters is this against
        /// <see cref="PhotosyntheticCell.Efficiency"/> and against cell upkeep.
        /// </remarks>
        [Tunable("light", Unit = "W/m2")]
        public float SurfaceIrradiance
        {
            get => _surfaceIrradiance;
            set => _surfaceIrradiance = Require(value, nameof(SurfaceIrradiance), NoLight);
        }

        private float _surfaceIrradiance;

        /// <summary>
        /// Depth over which irradiance falls to 1/e of its surface value, in metres.
        /// </summary>
        /// <remarks>
        /// Clear ocean water attenuates over tens of metres; coastal water over a few. The value
        /// here is a world-design choice rather than a physical measurement: it sets how far a
        /// creature must travel to leave the lit zone, and therefore how much vertical structure
        /// the world has to offer. ⚠ Unmeasured (§5A.10).
        /// </remarks>
        [Tunable("light", Unit = "m")]
        public float AttenuationDepth
        {
            get => _attenuationDepth;
            set => _attenuationDepth = Require(value, nameof(AttenuationDepth), NoDepth);
        }

        private float _attenuationDepth;

        public LightModel(float surfaceIrradiance = 400f, float attenuationDepth = 12f)
        {
            SurfaceIrradiance = surfaceIrradiance;
            AttenuationDepth = attenuationDepth;
        }

        /// <remarks>
        /// Settable as well as constructable, because §5A.10's rule is that an unmeasured number
        /// must be sweepable, and both of these are unmeasured. Validated on the way in either
        /// route, so a config loaded from a hand-edited file cannot introduce a world with no sun.
        /// </remarks>
        private static float Require(float value, string name, string why)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, why);
            }
            return value;
        }

        private const string NoLight =
            "A world with no light has no primary energy input and nothing can live in it.";

        private const string NoDepth =
            "Light must fall off over some distance, or depth means nothing and the world has " +
            "no vertical structure at all.";

        /// <summary>
        /// Irradiance at a world height, W/m². Y is up, so the surface is 0 and depths are
        /// negative.
        /// </summary>
        /// <remarks>
        /// Above the surface reads as the surface value rather than more: a creature that
        /// breached would otherwise gain energy by leaving the water, which is a free-energy
        /// source of exactly the kind §11.2 exists to catch. Nothing can breach today — buoyancy
        /// is neutral and there is no air — but the clamp costs one comparison and removes the
        /// question.
        /// </remarks>
        public float IrradianceAt(float heightY)
        {
            if (heightY >= 0f) return SurfaceIrradiance;

            return SurfaceIrradiance * (float)Math.Exp(heightY / AttenuationDepth);
        }

        /// <summary>Depth, in metres, below which irradiance is under the given fraction.</summary>
        /// <remarks>
        /// For reporting rather than for simulation: "the lit zone is N metres deep" is the sort
        /// of statement a run summary should be able to make without the reader deriving it.
        /// </remarks>
        public float DepthForFraction(float fraction)
        {
            if (!(fraction > 0f) || fraction >= 1f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fraction), fraction, "Must be in (0, 1).");
            }

            return -AttenuationDepth * (float)Math.Log(fraction);
        }

        public string HashContribution() =>
            string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "surface={0:R},attenuation={1:R}", SurfaceIrradiance, AttenuationDepth);

        public override string ToString() =>
            $"{SurfaceIrradiance:0} W/m² at surface, 1/e at {AttenuationDepth:0.#} m";
    }
}
