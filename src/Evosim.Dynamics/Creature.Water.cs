using Evosim.Core;

namespace Evosim.Dynamics
{
    /// <summary>
    /// Package C: what one body knows about the water it is in — the acceleration each link
    /// feels, and D100's held sample. Ported from the gather phase of
    /// <c>Evosim.Sim.FluidEnvironment.Apply</c> and from <c>CreatureInstance</c>'s three water
    /// fields.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Written only by its owner.</b> <see cref="HeldWater"/> and the clock beside it are a
    /// per-body cache, filled by <see cref="Water.Sample"/> on this body alone, so nothing here
    /// is shared and the hold cannot make a trajectory depend on who sampled first.
    /// </para>
    /// <para>
    /// <b>The cache is keyed to the world clock, never a wall clock</b>, which is the farm's own
    /// rule: a hold keyed on real time would make the water a function of machine load (§7). A
    /// body that has never been sampled holds negative infinity, so the test fires on its first
    /// sample; a resize keeps the cache, because a body that changed size is still in the same
    /// water.
    /// </para>
    /// </remarks>
    public sealed partial class Creature
    {
        /// <summary>
        /// The water's own acceleration at each link, world axes, m/s2 — 3 per link. Zero
        /// everywhere unless D090's term is on, in which case <see cref="Water.Sample"/> fills it.
        /// </summary>
        public double[] WaterAcceleration;

        /// <summary>
        /// D061's patch this body is booked into, which is all
        /// <see cref="CurrentMode.Rolls"/> reads of a horizontal position (§6.3). The farm's
        /// <c>Organism.Patch</c>; the world sets it, and it stays 0 in a harness with one patch.
        /// </summary>
        public int Patch;

        /// <summary>The velocity held from the root's last sample — D100's <c>HeldWater</c>.</summary>
        public Float3 HeldWater;

        /// <summary>The acceleration taken in the same breath as <see cref="HeldWater"/>.</summary>
        public Float3 HeldWaterAcceleration;

        /// <summary>The world clock at that sample, seconds. Negative infinity until the first.</summary>
        public double WaterSampledAt = double.NegativeInfinity;

        private void InitWater()
        {
            WaterAcceleration = new double[3 * Links];
        }
    }
}
