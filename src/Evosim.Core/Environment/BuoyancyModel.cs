using System;

namespace Evosim.Core
{
    /// <summary>
    /// Size-dependent buoyancy — D064. How much of <see cref="FluidConfig.TissueExcessDensity"/>
    /// a body of a given volume actually feels.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Stokes-shaped.</b> A small enough particle effectively does not sink: viscosity wins over
    /// weight, and the terminal rate falls away with size until holding station costs nothing.
    /// §5.2's constant excess density hands every body the same sink whatever its size, so a
    /// one-cell founder and a large multi-part body pay the same, which is the opposite of what a
    /// water column does. This scales the excess density with volume instead of scaling the drag,
    /// so the existing excess-density × drag mechanism carries the whole rule with one knob.
    /// </para>
    /// <para>
    /// ⚠ <b>The 2/3 exponent is a modelling choice, not a citation.</b> It is inference: volume to
    /// the 2/3 is an area, so <c>(V0/V)^(2/3)</c> reads as a surface-to-volume ratio relative to the
    /// neutral body, which is the shape of the real Stokes trade-off — but this is not derived from
    /// a Stokes-regime result and these bodies are in quadratic drag, not Stokes flow. It is a
    /// curve with the right qualitative behaviour and one free parameter, and nothing more.
    /// </para>
    /// <para>
    /// The knob is <see cref="FluidConfig.NeutralBodyVolume"/>. At 0 the rule is off and the factor
    /// is exactly 1, so every earlier run's behaviour is unchanged. For <c>V &lt;= V0</c> the factor
    /// is 0 — neutral, no sink at all. At <c>V = 8·V0</c> it is 0.75, and it converges to 1 as
    /// <c>V → ∞</c>, so a large body feels today's constant.
    /// </para>
    /// <para>
    /// D049/D050's lift is untouched and nets against the result exactly as it does today:
    /// <c>netDensity = rho_eff · (1 - lift)</c>.
    /// </para>
    /// </remarks>
    public static class BuoyancyModel
    {
        /// <summary>
        /// The fraction of <see cref="FluidConfig.TissueExcessDensity"/> a body of
        /// <paramref name="bodyVolume"/> m³ feels, given a neutral volume of
        /// <paramref name="neutralBodyVolume"/> m³.
        /// </summary>
        /// <remarks>
        /// <c>max(0, 1 - (V0 / V)^(2/3))</c>. Returns exactly <c>1</c> when
        /// <paramref name="neutralBodyVolume"/> is 0 or negative — the off state — and <c>0</c> for
        /// a non-positive volume when the rule is on.
        /// </remarks>
        public static float ExcessDensityFactor(float bodyVolume, float neutralBodyVolume)
        {
            // Off. Exactly 1, so a run with the knob unset is bit-identical to one from before
            // D064 existed — the call site skips the multiplication entirely as well.
            if (neutralBodyVolume <= 0f) return 1f;

            // A body with no volume has no weight to feel; also keeps the division defined.
            if (bodyVolume <= 0f) return 0f;

            if (bodyVolume <= neutralBodyVolume) return 0f;

            double factor = 1d - Math.Pow(neutralBodyVolume / (double)bodyVolume, 2d / 3d);
            return factor <= 0d ? 0f : (float)factor;
        }
    }
}
