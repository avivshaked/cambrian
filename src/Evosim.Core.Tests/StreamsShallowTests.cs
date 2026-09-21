using System;
using System.Reflection;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The water in a tank too shallow for D088's balance —
    /// <c>logbook/specs/streams-shallow-spec.md</c>, the owner's ruling of 2026-09-21.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What the ruling changed and what it must not have touched.</b> Where the balance has a
    /// root the field is the one every recorded tank ran in, and the first test here is the guard
    /// on that: round 42's own geometry at 45 m and at 30 m, flat and over its bed, holding the
    /// amplitude, the scale and the transport bound to the bit against numbers taken from the
    /// build before the change. Where it has none — a tank flatter than about <c>R/1.308</c>, which
    /// is 20.2 m at this footprint — the field is built at <c>λ·k_max</c> instead of at 1 and the
    /// rest of these read what that gives: the ratio it aimed at, the knob it still carries, and
    /// the four properties that make a prescribed field water in a walled cylinder rather than a
    /// pattern drawn inside one.
    /// </para>
    /// <para>
    /// <b>Read on lattices of their own</b>, coprime with the construction's and stepped through
    /// time by a different irrational, for <c>StreamsTests</c>' reason: a test that re-took the
    /// field's own points would confirm the arithmetic and nothing else. The one exception is
    /// <see cref="TheCeiling"/>, which re-runs the construction's own pass by reflection because
    /// the number it wants — the ceiling the ruling takes a fraction of — is a local inside
    /// <c>BuildStreams</c> and the assertion on it is at the last bit.
    /// </para>
    /// </remarks>
    public class StreamsShallowTests
    {
        private readonly ITestOutputHelper _output;

        public StreamsShallowTests(ITestOutputHelper output) => _output = output;

        // Round 42's tank, from runs/r42-s1/<run>/config.json: 2,200 m2 over four rings, 0.1 m/s
        // on a 6,000 s period, a bed at relief 1.5 m and tilt 30 m with no scale, seed 1. The
        // depths are the two the round has run (45 and its 30 m screen) and the shelf the owner
        // asked for (20), which is 2.5 m clear of the depth BedShape itself refuses.
        private const float Area = 2200f;
        private const int Rings = 4;
        private const float Speed = 0.1f;
        private const float Period = 6000f;
        private const float Relief = 1.5f;
        private const float Tilt = 30f;
        private const ulong RunSeed = 1UL;

        private const float Shallow = 20f;
        private const float Round42Depth = 45f;

        /// <summary>The fraction of the ceiling the owner ruled — <c>CurrentField</c>'s own λ.</summary>
        private const double Lambda = 0.76d;

        private static float Radius => TankGeometry.RadiusFor(Area);

        private static BedShape Bed(float depth) =>
            new BedShape(
                Radius, depth, Relief, Tilt, 0f, Rng.SeedFor(RunSeed, World.BedShapeIndex));

        private static CurrentField Water(
            float depth, BedShape bed = null, float speed = Speed, ulong seed = RunSeed)
        {
            var field = new CurrentField
            {
                Mode = CurrentMode.Transport,
                Speed = speed,
                PeriodSeconds = Period,
                AdvectFields = true,
            };

            field.SetBox(
                (float)Math.Sqrt(Area / Rings), Rings, depth,
                Rng.SeedFor(seed, World.CurrentFieldIndex),
                patchesAcross: 1, shape: WorldShape.Tank, tankRadiusMetres: Radius, bed: bed);

            return field;
        }

        // ------------------------------------------------------------------- the deep tank stands

        /// <summary>
        /// Round 42's water is the water round 42 ran in — the same amplitude, the same scale and
        /// the same transport bound, to the bit.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The literals are the build before the relaxation.</b> "Before" is not available at
        /// test time, so they were read off the unmodified field on 2026-09-21 — round-tripped
        /// with <c>R</c> — and are asserted here at exact equality rather than to a tolerance. A
        /// tolerance would pass on the day the balanced branch acquired an extra operation, which
        /// is the one day this test exists to fail on: every seed of round 42 is a chaotic
        /// realisation of its own water, so a change in the last bit of the amplitude is a
        /// different run and not a rounding of the same one.
        /// </para>
        /// <para>
        /// <b>Both floors, because they are two passes.</b> The balance is solved on the flat field
        /// and the bed's own pass runs after it (D092), so a bedded tank shares the flat one's
        /// amplitude and scale and has a bound of its own; pinning all four cases is what says the
        /// relaxation entered neither pass.
        /// </para>
        /// </remarks>
        [Fact]
        public void Round42sWaterIsUnmoved()
        {
            foreach ((float depth, bool bedded, double beta, float scale, float bound) in new[]
                     {
                         (45f, false, 4.860719623199337d, 0.72932625f, 1.1221404f),
                         (45f, true, 4.860719623199337d, 0.72932625f, 1.1367488f),
                         (30f, false, 5.880069888569802d, 0.6028926f, 1.1173462f),
                         (30f, true, 5.880069888569802d, 0.6028926f, 1.2331623f),
                     })
            {
                CurrentField field = Water(depth, bedded ? Bed(depth) : null);

                _ = field.StreamsComponentRms;

                double builtBeta = (double)Private(field, "_streamsOverturning");
                float builtScale = (float)Private(field, "_streamsScale");
                float builtBound = field.MaximumTransportSpeed;

                _output.WriteLine(FormattableString.Invariant(
                    $"depth {depth,3:0.} m, {(bedded ? "round 42's bed" : "flat bed"),-14}: beta {builtBeta:R}, scale {builtScale:R}, bound {builtBound:R} m/s, axes v:h {field.StreamsAxisRatio:R}"));

                Assert.Equal(beta, builtBeta);
                Assert.Equal(scale, builtScale);
                Assert.Equal(bound, builtBound);

                // And the readout says so: 1 exactly, not 1 to a tolerance, because this tank's
                // balance has a root and the relaxation never ran.
                Assert.Equal(1d, field.StreamsAxisRatio);
            }
        }

        // ------------------------------------------------------------------ the shallow tank runs

        /// <summary>
        /// A 20 m tank builds, at the ruled fraction of its own ceiling, and still carries the
        /// knob.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Three readings, and the third is read against a control.</b> The ratio the field
        /// says it aimed at is held against <c>λ</c> times the ceiling re-measured from the
        /// construction's own pass, at the last bit. The total RMS is held to 2% of the knob,
        /// because the scale is fitted after the amplitude and therefore does not care what the
        /// amplitude came out as. And the ratio the water actually has is measured on a lattice
        /// of this test's own and held to 3% of the target over a flat floor, which is the gap
        /// between a 21x21x19 reading and the 22x16x64 one the field fitted on: the coarse
        /// lattice reads the vertical high by 1.4% at 45 m and 1.3% at 20 m, so 3% is the
        /// instrument's own accuracy and not a band chosen to fit.
        /// </para>
        /// <para>
        /// <b>Over a shaped floor it is held against the deep tank instead</b>, for
        /// <c>BedStreamsTests.TheRmsSpeedIsStillTheKnob</c>'s reason. The floor-following map is
        /// not axis-preserving — it stretches the water over a rise and squeezes it in a hollow —
        /// and it lifts the measured vertical on every tank that has one: at round 42's own 45 m
        /// the ratio reads 1.016 flat and 1.050 bedded, at 30 m 1.014 and 1.035, at this 20 m
        /// 1.013 and 1.034. So the honest question is not whether a bedded shallow tank sits
        /// within 3% of its target — no bedded tank does, including the two the round has run —
        /// but whether the relaxation costs the map anything, and the assertion is that the
        /// shallow tank's bed bias is no worse than the same bed's bias at 45 m.
        /// </para>
        /// </remarks>
        [Fact]
        public void AShallowTankBuildsAtTheRuledFractionOfItsCeiling()
        {
            double shallowFlat = 0d, shallowBedded = 0d;

            foreach (bool bedded in new[] { false, true })
            {
                BedShape bed = bedded ? Bed(Shallow) : null;
                CurrentField field = Water(Shallow, bed);

                double ceiling = TheCeiling(field);
                double ratio = field.StreamsAxisRatio;
                double beta = (double)Private(field, "_streamsOverturning");

                Reading read = Measure(field, Shallow, bed, ManyInstants);
                double measured = read.Y / read.Horizontal;

                if (bedded) shallowBedded = measured; else shallowFlat = measured;

                _output.WriteLine(FormattableString.Invariant(
                    $"depth {Shallow} m, {(bedded ? "round 42's bed" : "flat bed"),-14}: ceiling {ceiling:0.0000}, target {ratio:0.0000}, beta {beta:0.0000}, bound {field.MaximumTransportSpeed:0.0000} m/s"));
                _output.WriteLine(FormattableString.Invariant(
                    $"  measured per axis x {read.X:0.00000}, y {read.Y:0.00000}, z {read.Z:0.00000} m/s — v:h {measured:0.0000}, which is {measured / ratio:0.0000} of the target; RMS {read.Rms:0.00000} m/s at a knob of {Speed}"));

                // The rule, at the bit: λ times the ceiling the pass measured, and nothing
                // written down. A literal ceiling here would make this a test of a fit.
                Assert.Equal(Lambda * ceiling, ratio, 1e-12);

                // The knob still names the RMS: the scale is fitted after the amplitude, so a
                // relaxed tank's water is as fast as a balanced one's.
                Assert.Equal((double)Speed, read.Rms, 0.02d * Speed);

                // What the water has, rather than what it aimed at — over the floor the balance
                // was solved on.
                if (!bedded) Assert.Equal(ratio, measured, 0.03d * ratio);
            }

            // The control: round 42's own depth, which builds without the relaxation, measured on
            // this same lattice with and without the same bed. What is compared is the bias the
            // map introduces, so the coarse lattice's own bias divides out of both sides.
            Reading deepFlat = Measure(Water(Round42Depth), Round42Depth, null, ManyInstants);
            Reading deepBedded = Measure(
                Water(Round42Depth, Bed(Round42Depth)), Round42Depth, Bed(Round42Depth),
                ManyInstants);

            double shallowBias = shallowBedded / shallowFlat;
            double deepBias = (deepBedded.Y / deepBedded.Horizontal) /
                              (deepFlat.Y / deepFlat.Horizontal);

            _output.WriteLine(FormattableString.Invariant(
                $"the bed's own lift of the measured v:h — {shallowBias:0.0000} at {Shallow} m against {deepBias:0.0000} at {Round42Depth} m, where the balance has a root"));

            Assert.True(shallowBias <= deepBias + 0.01d, FormattableString.Invariant(
                $"the map costs the relaxed tank {shallowBias:0.000} against {deepBias:0.000} at a depth that balances"));
        }

        [Fact]
        public void TheShallowStreamsAreDivergenceFree()
        {
            CurrentField field = Water(Shallow);

            // StreamsTests' step and inset, for its reasons: small enough that the truncation is
            // under the tolerance, large enough that the float the sampler returns is not the
            // whole answer, and set in far enough that the stencil never straddles the glass.
            const double H = 0.05;

            var rng = new Rng(7UL);
            double worst = 0d;
            double worstGradient = 0d;
            double rms = Measure(field, Shallow, null).Rms;

            for (int i = 0; i < 1000; i++)
            {
                (float x, float y, float z) = Interior(rng, Shallow, inset: 4d * H);
                double t = rng.NextFloat() * 4d * Period;

                double dudx = (Vx(field, x + H, y, z, t) - Vx(field, x - H, y, z, t)) / (2d * H);
                double dvdy = (Vy(field, x, y + H, z, t) - Vy(field, x, y - H, z, t)) / (2d * H);
                double dwdz = (Vz(field, x, y, z + H, t) - Vz(field, x, y, z - H, t)) / (2d * H);

                double divergence = Math.Abs(dudx + dvdy + dwdz);
                double gradient = Math.Abs(dudx) + Math.Abs(dvdy) + Math.Abs(dwdz);

                if (divergence > worst) worst = divergence;
                if (gradient > worstGradient) worstGradient = gradient;
            }

            _output.WriteLine(FormattableString.Invariant(
                $"depth {Shallow} m: worst |div v| {worst:0.0000e+0} /s over 1,000 points, against gradients of up to {worstGradient:0.0000} /s and an RMS of {rms:0.0000} m/s"));

            Assert.True(worst < 1e-3 * rms, FormattableString.Invariant(
                $"worst divergence {worst:0.0000e+0} /s"));
        }

        [Fact]
        public void NothingFlowsThroughTheGlassOfAShallowTank()
        {
            CurrentField field = Water(Shallow);

            var rng = new Rng(11UL);
            double worst = 0d;

            for (int i = 0; i < 200; i++)
            {
                double theta = 2d * Math.PI * rng.NextFloat();
                float y = -rng.NextFloat() * Shallow;
                double t = rng.NextFloat() * 4d * Period;

                float x = (float)(Radius + Radius * Math.Cos(theta));
                float z = (float)(Radius + Radius * Math.Sin(theta));

                Float3 v = field.VelocityAt(x, y, z, t);

                double radial = v.X * Math.Cos(theta) + v.Z * Math.Sin(theta);
                worst = Math.Max(worst, Math.Abs(radial));
            }

            _output.WriteLine(FormattableString.Invariant(
                $"depth {Shallow} m: worst radial velocity at the glass {worst:0.0000e+0} m/s"));

            Assert.True(worst < 1e-6, FormattableString.Invariant(
                $"worst radial velocity {worst:0.0000e+0} m/s"));
        }

        [Fact]
        public void NothingFlowsThroughTheSurfaceOrTheBedOfAShallowTank()
        {
            CurrentField field = Water(Shallow);

            var rng = new Rng(13UL);

            for (int i = 0; i < 200; i++)
            {
                (float x, _, float z) = Interior(rng, Shallow);
                double t = rng.NextFloat() * 10d * Period;

                // Exactly, not nearly, for logbook/0022's reason — and the larger amplitude a
                // relaxed tank carries is exactly the thing that would turn a 1e-16 at the
                // waterline into a lift.
                Assert.Equal(0f, field.VelocityAt(x, 0f, z, t).Y);
                Assert.Equal(0f, field.VelocityAt(x, -Shallow, z, t).Y);

                Assert.Equal(0f, field.VelocityAt(x, 3f, z, t).Y);
                Assert.Equal(0f, field.VelocityAt(x, -Shallow - 12f, z, t).Y);
            }
        }

        /// <summary>
        /// The grid carries a uniform field through a shallow tank without disturbing it, and
        /// every live cell's faces still sum to zero.
        /// </summary>
        /// <remarks>
        /// <b>The check <c>logbook/specs/transport-conserves-spec.md</c> was written for, asked of
        /// the new water.</b> Conservation, positivity and dry cells can all hold on a field that
        /// is wrong — the campaign's own transporter kept its total to 1e-15 while turning 1
        /// unit/m³ into 0.36 to 2.45 — so a constant-field check belongs beside every conservation
        /// check. The tolerance is <c>ConservativeTransportTests</c>' own 1e-9 on the worst cell.
        /// A shallow tank is where this could plausibly break: the relaxed amplitude is the
        /// largest any tank carries, and the potential the grid reads its edge circulations from
        /// is linear in it.
        /// </remarks>
        /// <remarks>
        /// Marked <c>Slow</c> on its cost rather than on its kind: 2,200 m² of 1 m cells 20 m deep
        /// is 128,000 open faces, and the four cases take 38 s where the deep tank's own case in
        /// <c>ConservativeTransportTests</c> takes seconds over 6,000 cells. It guards a rule, so
        /// it belongs in the run before a commit that touches the water —
        /// <c>core-test.ps1 -All -Filter StreamsShallowTests</c> — and not in the filtered loop.
        /// </remarks>
        [Trait("Category", "Slow")]
        [Fact]
        public void AUniformConcentrationStaysUniformInAShallowTank()
        {
            const float Dt = 0.5f;

            _output.WriteLine("  cell  mixing  steps  |  min        max        sd/mean  max|c-1|  total err  faces");

            // The campaign's two grids and their own rates: 1 m detritus cells stirred at
            // 0.02 m²/s and 5 m matter cells at 2 m²/s. 600 steps is 300 s, which is half the
            // window the fault was measured over and all this many live cells can afford in a
            // filtered run; the fault it guards against is a per-step one and shows in tens.
            foreach ((float cell, float diffusivity, int steps) in new[]
                     {
                         (1f, 0.02f, 600), (5f, 2f, 1200),
                     })
            foreach (bool mixing in new[] { false, true })
            {
                var grid = new GridField(
                    Area, 0f, Shallow, 0f, 0f, Rings, cell,
                    patchesAcross: 1, shape: WorldShape.Tank, tankRadiusMetres: Radius);

                CurrentField field = Water(Shallow);

                grid.SeedUniform(1f);
                double initial = grid.Recount();

                for (int step = 1; step <= steps; step++)
                {
                    if (mixing) grid.Mix(Dt, diffusivity);
                    grid.Advect(field, step * (double)Dt, Dt, grid.PatchWidthMetres);
                }

                (double min, double max, double mean, double cv) = Spread(grid);
                double drift = (grid.Recount() - initial) / initial;
                double worst = Math.Max(Math.Abs(max - 1d), Math.Abs(min - 1d));

                // Conservation's other half, on the arithmetic rather than inferred from it: an
                // edge belongs to two of a cell's faces with opposite sign, so the six signed
                // fluxes cancel term for term whatever the edge values are.
                (int _, double _, double _, double _, int open, double net) =
                    grid.MeasureFaceFluxes(field, 61d, Dt);

                _output.WriteLine(FormattableString.Invariant(
                    $"  {cell,4} {(mixing ? diffusivity.ToString("0.##") : "off"),7} {steps,6}  |  {min,-10:0.000000} {max,-10:0.000000} {cv,-8:0.0%} {worst,-9:0.0e+0} {drift,9:0.0e+0}  {open,6} open, worst net {net:0.0e+0}"));

                Assert.Equal(1.0, mean, 9);
                Assert.True(worst < 1e-9, FormattableString.Invariant(
                    $"at {cell} m and mixing {(mixing ? "on" : "off")} the density ran {min:R} to {max:R} where a uniform field must stay at 1"));
                Assert.Equal(0d, net);
            }
        }

        /// <summary>
        /// A body with a body's lag keeps its spread in a shallow tank once it feels the water's
        /// own acceleration — D090's reading, asked where the water turns most.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Why a shallow tank is the hard case.</b> The drift of a lagging body is
        /// <c>(τ/r)[⟨u_θ²⟩ − ∂_r(r⟨u_r²⟩)]</c> and its area mean is <c>(2τ/R²)∫⟨u_θ²⟩dr</c>, so
        /// what gathers a population at the glass is horizontal motion on curved streamlines —
        /// and a relaxed tank puts a larger share of its energy there than a balanced one by
        /// construction, the vertical being a quarter weaker. <c>StreamsTests.ABodyRidesTheWater</c>
        /// reads the 60 m tank; this reads the 20 m one, and the assertion is the same spec band.
        /// </para>
        /// <para>
        /// Marked <c>Slow</c> for that test's reason: 200 tracers through 100,000 steps, twice,
        /// with two samples of the field and two of its derivative per tracer per step.
        /// </para>
        /// </remarks>
        [Trait("Category", "Slow")]
        [Fact]
        public void ABodyRidesTheWaterInAShallowTank()
        {
            CurrentField field = Water(Shallow);

            foreach (double carry in new[] { 0d, 1d })
            {
                Drift drift = Tracers(field, tau: 2d, seconds: 2000d, window: 1000d, carry: carry);

                _output.WriteLine(FormattableString.Invariant(
                    $"depth {Shallow} m at {Speed} m/s, τ = 2 s, c = {carry}: ring shares centre to rim {drift.Shares[0]:0.000} / {drift.Shares[1]:0.000} / {drift.Shares[2]:0.000} / {drift.Shares[3]:0.000} (uniform in area is 0.250 each), rim quarter {drift.RimShare:0.000}; mean radius {drift.MeanRadius:0.000} m ({2d * Radius / 3d:0.000} m if uniform), radial drift {drift.RadialDrift:+0.000;-0.000} m; {drift.Reflections:n0} reflections off the glass"));

                // The streams spec's band, with the force on. With it off the number is the
                // centrifuge's and is reported rather than asserted, as D090's own reading is.
                if (carry > 0d) Assert.InRange(drift.RimShare, 0.2, 0.3);
            }
        }

        // ------------------------------------------------------------------------ the second pass

        /// <summary>
        /// The ceiling <c>BuildStreams</c> measured, <c>sqrt(2V/H1)</c>, re-run through the field's
        /// own private pass.
        /// </summary>
        /// <remarks>
        /// <b>Reflection, for <c>StreamsShallowProbe</c>'s reason and with its guarantee.</b> The
        /// coefficients are locals inside <c>BuildStreams</c> and nothing stores them, but
        /// everything they are measured from survives the build: the term arrays, the phases and
        /// the streams' own weight. So this walks the same lattice through the same
        /// <c>StreamsUnit</c> with the same envelope override, in the same accumulation order, and
        /// gets the same doubles — which is what lets the assertion on the ruled fraction be at
        /// the last bit rather than at a tolerance that would hide an arithmetic change.
        /// </remarks>
        private static double TheCeiling(CurrentField field)
        {
            _ = field.StreamsComponentRms;

            MethodInfo walkInfo = typeof(CurrentField).GetMethod(
                "Walk", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo unitInfo = typeof(CurrentField).GetMethod(
                "StreamsUnit", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.NotNull(walkInfo);
            Assert.NotNull(unitInfo);

            var walk = (Func<Action<float, float, float, double>, int>)walkInfo.CreateDelegate(
                typeof(Func<Action<float, float, float, double>, int>), field);
            var unit = (Func<double, double, double, double, double, Float3>)unitInfo.CreateDelegate(
                typeof(Func<double, double, double, double, double, Float3>), field);

            SetPrivate(field, "_envelopeOverride", Math.Sqrt(0.59375d));

            double h1 = 0d, vertical = 0d;

            int counted = walk((x, y, z, t) =>
            {
                Float3 rest = unit(x, y, z, t, 0d);
                Float3 whole = unit(x, y, z, t, 1d);

                double overX = whole.X - rest.X;
                double overZ = whole.Z - rest.Z;

                h1 += overX * overX + overZ * overZ;
                vertical += (double)whole.Y * whole.Y;
            });

            // Cleared exactly as the production method clears it on every exit, so a field this
            // touched is the field a caller would have got.
            SetPrivate(field, "_envelopeOverride", 0d);

            return Math.Sqrt(2d * (vertical / counted) / (h1 / counted));
        }

        private static object Private(CurrentField field, string name) =>
            typeof(CurrentField)
                .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(field);

        private static void SetPrivate(CurrentField field, string name, object value) =>
            typeof(CurrentField)
                .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(field, value);

        // ------------------------------------------------------------------------- the instrument

        private const int ManyInstants = 401;

        private struct Reading
        {
            public double Rms;
            public double X;
            public double Y;
            public double Z;
            public double Fastest;

            /// <summary>The per-axis horizontal RMS, which is what the vertical is balanced against.</summary>
            public double Horizontal => Math.Sqrt(0.5d * (X * X + Z * Z));
        }

        /// <summary>
        /// The field's RMS and its per-axis RMS on a lattice of this test's own —
        /// <c>StreamsTests.Measure</c> with the depth and the floor as arguments.
        /// </summary>
        /// <remarks>
        /// Coprime with the construction's lattice (21 and 19 against 22 and 16) and stepped
        /// through time by <c>√3</c> turns where the field steps by <c>√2</c>. Over a shaped floor
        /// each column is sampled from its own bed to the surface and weighted by its depth, so
        /// the average is over cubic metres of the water that exists rather than over columns.
        /// </remarks>
        private static Reading Measure(
            CurrentField field, float depth, BedShape bed, int instants = 41)
        {
            const int Horizontal = 21;
            const int Vertical = 19;

            double sumX = 0d, sumY = 0d, sumZ = 0d, fastest = 0d, weight = 0d;

            for (int it = 0; it < instants; it++)
            {
                double t = Period * Math.Sqrt(3d) * it;

                for (int ix = 0; ix < Horizontal; ix++)
                {
                    float x = (float)((ix + 0.5) * 2d * Radius / Horizontal);

                    for (int iz = 0; iz < Horizontal; iz++)
                    {
                        float z = (float)((iz + 0.5) * 2d * Radius / Horizontal);
                        if (!TankGeometry.Inside(x, z, Radius)) continue;

                        double column = bed == null ? depth : -bed.FloorY(x, z);
                        if (!(column > 0d)) continue;

                        double w = column / depth;

                        for (int iy = 0; iy < Vertical; iy++)
                        {
                            float y = -(float)((iy + 0.5) * column / Vertical);

                            Float3 v = field.VelocityAt(x, y, z, t);

                            sumX += w * (double)v.X * v.X;
                            sumY += w * (double)v.Y * v.Y;
                            sumZ += w * (double)v.Z * v.Z;
                            weight += w;

                            double speed = v.Magnitude;
                            if (speed > fastest) fastest = speed;
                        }
                    }
                }
            }

            return new Reading
            {
                Rms = Math.Sqrt((sumX + sumY + sumZ) / weight),
                X = Math.Sqrt(sumX / weight),
                Y = Math.Sqrt(sumY / weight),
                Z = Math.Sqrt(sumZ / weight),
                Fastest = fastest,
            };
        }

        /// <summary>A point inside the circle, uniform in area, at an interior depth.</summary>
        private static (float X, float Y, float Z) Interior(Rng rng, float depth, double inset = 0d)
        {
            float r = (float)((Radius - inset) * Math.Sqrt(rng.NextFloat()));
            double theta = 2d * Math.PI * rng.NextFloat();

            return (
                (float)(Radius + r * Math.Cos(theta)),
                -1f - rng.NextFloat() * (depth - 2f),
                (float)(Radius + r * Math.Sin(theta)));
        }

        /// <summary>min, max and sd/mean of the density over the live cells.</summary>
        private static (double Min, double Max, double Mean, double Cv) Spread(GridField field)
        {
            double volume = field.CellVolume;
            double min = double.MaxValue, max = double.MinValue, sum = 0d, square = 0d;
            int n = 0;

            for (int ix = 0; ix < field.CellsX; ix++)
            for (int iy = 0; iy < field.CellsY; iy++)
            for (int iz = 0; iz < field.CellsZ; iz++)
            {
                if (!field.IsLive(ix, iy, iz)) continue;

                double density = field.JoulesAt(ix, iy, iz) / volume;
                if (density < min) min = density;
                if (density > max) max = density;
                sum += density;
                square += density * density;
                n++;
            }

            double mean = sum / n;
            return (min, max, mean, Math.Sqrt(Math.Max(0d, square / n - mean * mean)) / mean);
        }

        private static double Vx(CurrentField f, double x, double y, double z, double t) =>
            f.VelocityAt((float)x, (float)y, (float)z, t).X;

        private static double Vy(CurrentField f, double x, double y, double z, double t) =>
            f.VelocityAt((float)x, (float)y, (float)z, t).Y;

        private static double Vz(CurrentField f, double x, double y, double z, double t) =>
            f.VelocityAt((float)x, (float)y, (float)z, t).Z;

        // ---------------------------------------------------------------------------- the tracers

        private const int TracerCount = 200;
        private const double TracerStep = 0.02d;

        private struct Drift
        {
            public double[] Shares;
            public double RimShare;
            public double RadialDrift;
            public double MeanRadius;
            public int Reflections;
        }

        /// <summary>
        /// <c>StreamsTests.Tracers</c> at this class's depth: 200 tracers under
        /// <c>dv/dt = (u − v)/τ + c·Du/Dt</c>, integrated by the midpoint rule at the physics step,
        /// reflected off the glass and clamped at the surface and the bed.
        /// </summary>
        /// <remarks>
        /// The scheme is the midpoint rule and not Euler for the reason
        /// <c>StreamsTests.TheTracerReadingDependsOnTheSchemeAndTheStep</c> records: Euler loses
        /// volume preservation at first order and invents most of a rim share out of its own
        /// truncation. The derivative is the field's own <c>AccelerationAt</c>, which in a tank is
        /// the closed form, so a tracer here feels what a part feels in a run.
        /// </remarks>
        private static Drift Tracers(
            CurrentField field, double tau, double seconds, double window, double carry)
        {
            var rng = new Rng(31UL);

            var x = new double[TracerCount];
            var y = new double[TracerCount];
            var z = new double[TracerCount];
            var vx = new double[TracerCount];
            var vy = new double[TracerCount];
            var vz = new double[TracerCount];

            var mx = new double[TracerCount];
            var my = new double[TracerCount];
            var mz = new double[TracerCount];
            var nx = new double[TracerCount];
            var ny = new double[TracerCount];
            var nz = new double[TracerCount];

            double startRadius = 0d;

            for (int i = 0; i < TracerCount; i++)
            {
                double r = Radius * Math.Sqrt(rng.NextFloat());
                double theta = 2d * Math.PI * rng.NextFloat();

                x[i] = Radius + r * Math.Cos(theta);
                z[i] = Radius + r * Math.Sin(theta);
                y[i] = -rng.NextFloat() * Shallow;

                startRadius += r;
            }

            startRadius /= TracerCount;

            var rings = new double[4];
            int samples = 0;
            double radiusSum = 0d;
            int reflections = 0;

            int steps = (int)(seconds / TracerStep);
            int every = Math.Max(1, (int)(100d / TracerStep));

            for (int number = 1; number <= steps; number++)
            {
                double t = (number - 1) * TracerStep;

                // Pass one, all at t, so that every sample inside a pass is at one instant and the
                // field's per-instant memo is built twice a step rather than twice a tracer.
                for (int i = 0; i < TracerCount; i++)
                {
                    Float3 u = field.VelocityAt((float)x[i], (float)y[i], (float)z[i], t);

                    Float3 a = carry > 0d
                        ? field.AccelerationAt((float)x[i], (float)y[i], (float)z[i], t)
                        : Float3.Zero;

                    nx[i] = vx[i] + (u.X - vx[i]) * TracerStep / (2d * tau) + carry * a.X * TracerStep / 2d;
                    ny[i] = vy[i] + (u.Y - vy[i]) * TracerStep / (2d * tau) + carry * a.Y * TracerStep / 2d;
                    nz[i] = vz[i] + (u.Z - vz[i]) * TracerStep / (2d * tau) + carry * a.Z * TracerStep / 2d;

                    mx[i] = x[i] + vx[i] * TracerStep / 2d;
                    my[i] = y[i] + vy[i] * TracerStep / 2d;
                    mz[i] = z[i] + vz[i] * TracerStep / 2d;
                }

                // Pass two, all at t + dt/2: the midpoint slope, and the step.
                double half = t + TracerStep / 2d;

                for (int i = 0; i < TracerCount; i++)
                {
                    Float3 u = field.VelocityAt((float)mx[i], (float)my[i], (float)mz[i], half);

                    Float3 a = carry > 0d
                        ? field.AccelerationAt((float)mx[i], (float)my[i], (float)mz[i], half)
                        : Float3.Zero;

                    x[i] += nx[i] * TracerStep;
                    y[i] += ny[i] * TracerStep;
                    z[i] += nz[i] * TracerStep;

                    vx[i] += (u.X - nx[i]) * TracerStep / tau + carry * a.X * TracerStep;
                    vy[i] += (u.Y - ny[i]) * TracerStep / tau + carry * a.Y * TracerStep;
                    vz[i] += (u.Z - nz[i]) * TracerStep / tau + carry * a.Z * TracerStep;

                    Settle(i);
                }

                if (number * TracerStep <= seconds - window || number % every != 0) continue;

                samples++;

                for (int i = 0; i < TracerCount; i++)
                {
                    double dx = x[i] - Radius;
                    double dz = z[i] - Radius;
                    double fraction = (dx * dx + dz * dz) / ((double)Radius * Radius);

                    int ring = (int)(fraction * 4d);
                    if (ring > 3) ring = 3;
                    if (ring < 0) ring = 0;

                    rings[ring]++;
                    radiusSum += Math.Sqrt(dx * dx + dz * dz);
                }
            }

            void Settle(int i)
            {
                double dx = x[i] - Radius;
                double dz = z[i] - Radius;
                double r = Math.Sqrt(dx * dx + dz * dz);

                if (r > Radius && r > 0d)
                {
                    double unitX = dx / r, unitZ = dz / r;
                    double inside = 2d * Radius - r;

                    x[i] = Radius + inside * unitX;
                    z[i] = Radius + inside * unitZ;

                    double radial = vx[i] * unitX + vz[i] * unitZ;
                    vx[i] -= 2d * radial * unitX;
                    vz[i] -= 2d * radial * unitZ;

                    reflections++;
                }

                if (y[i] > 0d)
                {
                    y[i] = 0d;
                    if (vy[i] > 0d) vy[i] = 0d;
                }
                else if (y[i] < -Shallow)
                {
                    y[i] = -Shallow;
                    if (vy[i] < 0d) vy[i] = 0d;
                }
            }

            double held = samples * (double)TracerCount;

            var shares = new double[4];
            for (int k = 0; k < 4; k++) shares[k] = rings[k] / held;

            double meanRadius = radiusSum / held;

            return new Drift
            {
                Shares = shares,
                RimShare = shares[3],
                RadialDrift = meanRadius - startRadius,
                MeanRadius = meanRadius,
                Reflections = reflections,
            };
        }
    }
}
