using Evosim.Core;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// Round 42 seed 1's water, rebuilt from its own <c>config.json</c> without a world — the
    /// tank at 2,200 m2 and 45 m, the streams at 0.1 m/s on a 6,000 s period, over D092's bed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Built the way <c>World</c>'s constructor builds it</b>, argument for argument: the
    /// patch width is <c>sqrt(area / K)</c> — <c>NutrientField.PatchWidthMetres</c>'s own
    /// formula, which is what the world hands the field — the radius is
    /// <c>TankGeometry.RadiusFor(area)</c>, and both seeds are drawn off the run's seed through
    /// <c>Rng.SeedFor</c> at <c>World.CurrentFieldIndex</c> and <c>World.BedShapeIndex</c>. A
    /// world is not constructed because it would want an economy, a placer and a field, none of
    /// which the water reads.
    /// </para>
    /// <para>
    /// <b>The vent is left at its defaults</b>, because the run's <c>ventSpeed</c> is 0 and
    /// <c>VentActive</c> is therefore false — which is also what selects the streams' closed-form
    /// acceleration over the nine-sample stencil.
    /// </para>
    /// </remarks>
    public static class R42Tank
    {
        public const float AreaSquareMetres = 2200f;
        public const float DepthMetres = 45f;
        public const int PatchCount = 4;
        public const ulong Seed = 1UL;

        public static float Radius => TankGeometry.RadiusFor(AreaSquareMetres);

        /// <summary>
        /// The tank's axis. <b>Not the origin</b>: <see cref="TankGeometry"/> puts the water in
        /// <c>[0, 2R) x [0, 2R)</c> with the axis at <c>(R, R)</c>, so that every consumer of a
        /// length and a width sees a rectangle and the storage stays an array. A body put down at
        /// the origin in a tank of this size is outside the glass, where
        /// <c>CurrentField.VelocityAt</c> reads the nearest water it has — which is water, but
        /// not water anything is swimming in.
        /// </summary>
        public static Vec3 Axis => new Vec3(Radius, 0, Radius);

        /// <summary>A place in the tank, given as an offset from the axis.</summary>
        public static Vec3 At(double x, double y, double z) =>
            new Vec3(Radius + x, y, Radius + z);

        public static CurrentField Build()
        {
            var current = new CurrentField
            {
                Speed = 0.1f,
                PeriodSeconds = 6000f,
                CellMetres = 30f,
                HorizontalRatio = 1f,
                Rolls = true,
                RollBlinkSeconds = 3000f,
                AdvectFields = true,
                Mode = CurrentMode.Transport,
            };

            var bed = new BedShape(
                Radius, DepthMetres,
                reliefMetres: 1.5f, tiltMetres: 30f, scaleMetres: 0f,
                seed: Rng.SeedFor(Seed, World.BedShapeIndex));

            current.SetBox(
                (float)System.Math.Sqrt(AreaSquareMetres / PatchCount),
                PatchCount, DepthMetres,
                Rng.SeedFor(Seed, World.CurrentFieldIndex),
                patchesAcross: 1, shape: WorldShape.Tank, tankRadiusMetres: Radius, bed: bed);

            return current;
        }

        /// <summary>
        /// The run's fluid, with the current in it — <c>config.json</c>'s <c>fluid</c> group and
        /// its <c>world</c> shape, as <see cref="SolverConfig.From"/> would carry them.
        /// </summary>
        public static SolverConfig Config(double dt = 0.01, double waterHoldSeconds = 0.5)
        {
            return new SolverConfig
            {
                StepSeconds = dt,

                Density = 1000,
                DragCoefficient = 1.5,
                PanelsPerAxis = 2,
                AddedMassCoefficient = 0.5,
                FluidAccelerationCoefficient = 1,
                TissueExcessDensity = 0.02,
                NeutralBodyVolume = 0.25,
                SurfaceRestoringFraction = 1,
                WaterHoldSeconds = waterHoldSeconds,

                WorldDepthMetres = DepthMetres,
                FloorIsSolid = true,

                // ⚠ 0, and not the tank's radius, although the tank has one. The spike's glass
                // (Contacts.Apply) measures a body's radius from the ORIGIN, and Core's tank
                // stands at (R, R) — TankGeometry's own remark, and what CurrentField,
                // GridField and SharedVolume all use. So a body correctly inside this water is
                // 40 m from the spike's axis and 14 m outside the spike's glass, and the wall
                // spring throws it across the tank on its first step. That is package E's file
                // and package E's fix; the water does not touch it, so these tests run with the
                // glass off and say so. SolverConfig.From still carries the real radius, because
                // the radius is right and the centre is what is wrong.
                TankRadiusMetres = 0,

                Current = Build(),
                PatchCount = PatchCount,

                CreatureContact = false,
            };
        }
    }
}
