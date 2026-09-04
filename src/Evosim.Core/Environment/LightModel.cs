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
    /// <b>There is a diurnal cycle, and it is off by default.</b> §5A.4 wants one, and it is what
    /// makes diel vertical migration expressible. The objection to adding it was that a cycle
    /// turns §5A.2's calibration question from "does light cover upkeep" into "does light cover
    /// upkeep <i>averaged over a period</i>, and can anything survive the trough" — two unknowns
    /// at once, before either had been measured alone.
    /// </para>
    /// <para>
    /// <b>That objection is answered by making the cycle mean-preserving</b>, not by deferring it.
    /// <see cref="SurfaceIrradiance"/> stays the daily <i>mean</i> and
    /// <see cref="DayNightAmplitude"/> modulates around it, so 0 reproduces the acyclic world
    /// exactly and turning it up does not move the world's energy budget by one joule. One
    /// unknown, and it has a defined zero.
    /// </para>
    /// <para>
    /// <b>What a cycle does and does not buy.</b> It does not on its own move the best place to
    /// be: irradiance is monotonically decreasing in depth at every hour, so the surface always
    /// wins on light alone. What it moves is the <i>balance</i> against the other income.
    /// Detritus sinks (§5A.2c), so nutrients are deep and light is shallow, and a creature earning
    /// from both has an optimum somewhere between. Darken the surface and that optimum descends;
    /// brighten it and it rises. A migrator tracks it and a sitter takes the average of both —
    /// which is diel vertical migration, arrived at from the economics rather than installed.
    /// <i>(Author's inference. Real DVM is usually attributed to predation risk, which this world
    /// does not have yet.)</i>
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

        /// <summary>
        /// How far surface irradiance swings either side of its mean over a day, as a fraction —
        /// DESIGN.md §5A.4. 0 is a world with no night.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The cycle is mean-preserving by construction</b>, so this dial does not change how
        /// much energy the world receives in a day, only when it arrives. At 1 the surface runs
        /// from dark to twice the mean; at 0 it sits at the mean forever and every number measured
        /// under the acyclic model still means what it meant.
        /// </para>
        /// <para>
        /// A sinusoid rather than a clamped one. <c>max(0, sin)</c> would give a true half-day
        /// night, and it averages to 1/π of its peak — so switching it on at a fixed
        /// <see cref="SurfaceIrradiance"/> would quietly cut the world's income to a third and
        /// present as a diurnal effect. Preserving the mean is what keeps this one unknown rather
        /// than two.
        /// </para>
        /// <para>⚠ Unmeasured (§5A.10). Default 0: no night until a run asks for one.</para>
        /// </remarks>
        [Tunable("light")]
        public float DayNightAmplitude
        {
            get => _dayNightAmplitude;
            set => _dayNightAmplitude = value >= 0f && value <= 1f
                ? value
                : throw new ArgumentOutOfRangeException(
                    nameof(DayNightAmplitude), value,
                    "Must be in [0, 1]. Above 1 the surface goes dark for part of the day and " +
                    "the cycle stops preserving its own mean, which is the whole reason it can " +
                    "be switched on without recalibrating the world.");
        }

        private float _dayNightAmplitude;

        /// <summary>Length of one day, in simulated seconds — DESIGN.md §5A.4.</summary>
        /// <remarks>
        /// <b>The period a creature has to outlive, and to anticipate.</b> Too short and no body
        /// can cross a meaningful part of the water column within one, so the cycle is noise that
        /// can only be averaged over; too long and a run contains no days at all. It only means
        /// anything read against how far a creature can actually swim in one, which today is very
        /// little. ⚠ Unmeasured (§5A.10).
        /// </remarks>
        [Tunable("light", Unit = "s")]
        public float DayLengthSeconds
        {
            get => _dayLengthSeconds;
            set => _dayLengthSeconds = Require(value, nameof(DayLengthSeconds), NoDay);
        }

        private float _dayLengthSeconds = 200f;

        public LightModel(float surfaceIrradiance = 400f, float attenuationDepth = 12f)
        {
            SurfaceIrradiance = surfaceIrradiance;
            AttenuationDepth = attenuationDepth;
        }

        /// <summary>
        /// The multiplier on <see cref="SurfaceIrradiance"/> at a point in time. 1 with no cycle.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A pure function of absolute elapsed seconds, and deliberately not a mutable phase this
        /// object carries. <b>This class is configuration</b> — every other member on it is a
        /// <c>[Tunable]</c>, it is what §7 hashes, and where the world has got to is not part of
        /// how the world was set up. <see cref="LightField"/> already holds the light's per-step
        /// state and holds this too.
        /// </para>
        /// <para>
        /// Absolute seconds rather than a delta, so a caller that skips a step or calls twice
        /// cannot walk the sun out of phase with the world that is paying for it. That failure
        /// would present as a slow trend nobody chose, which is the kind this project keeps
        /// paying for.
        /// </para>
        /// </remarks>
        public float DayFactorAt(double elapsedSeconds) =>
            DayNightAmplitude <= 0f
                ? 1f
                : (float)(1.0 + DayNightAmplitude *
                    Math.Sin(2.0 * Math.PI * elapsedSeconds / DayLengthSeconds));

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

        private const string NoDay =
            "A day of zero or infinite length is not a cycle, and the sun would sit at one " +
            "instant of it forever.";

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

        // HashContribution() was removed rather than extended. Nothing called it: the two
        // properties it hand-wrote both carry [Tunable] and reach 7's hash through
        // ConfigSchema (D027), so it was a second copy of the same declaration free to
        // diverge. Adding the two knobs above to it would have made that copy look
        // maintained.

        public override string ToString() =>
            FormattableString.Invariant(
                $"{SurfaceIrradiance:0} W/m² at surface, 1/e at {AttenuationDepth:0.#} m");
    }
}
