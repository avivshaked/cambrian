using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The tank's water — <c>logbook/specs/streams-spec.md</c>,
    /// <c>fable-propose-streams.md</c> (ruled 2026-09-12): a spectrum of streams and no swirl
    /// about the axis, held to the properties that make it water in a walled cylinder rather than
    /// a pattern drawn inside one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Four properties, two readings and a tracer.</b> Divergence-free, so it neither creates
    /// nor destroys the stock it carries; no radial flow at the glass, so nothing piles against
    /// the wall or is asked to pass through it; no vertical flow at the surface or the bed, so the
    /// lift of logbook/0022 cannot recur; and the RMS at the knob, so a launcher's number means
    /// what it says. The readings are the dead-pocket fraction — the share of live cells where the
    /// water is under a tenth of its own RMS, which decides whether a body can find still water to
    /// sit in — and the speed in the half metre nearest the glass, which the gyre took to zero and
    /// which is where round 37's population lived.
    /// </para>
    /// <para>
    /// <b>The tracer is the check the ruling turns on, and it took a second force to pass it.</b>
    /// The spec asks that 200 passive tracers with a body's drag response still be spread
    /// uniformly in area after a round's length. On §5.2's drag alone they are not: they gather at
    /// the glass, at a rim share of 0.83 at τ = 2 s and 0.91 at τ = 0.5 s where uniform is 0.25,
    /// and the retired gyre reads 0.98 under the same instrument.
    /// <see cref="APerfectTracerKeepsItsSpread"/> is what separates the two possible
    /// causes, and it acquits the field: a tracer that carries <i>no</i> lag keeps its spread
    /// exactly, as an incompressible flow obliges it to. So what gathered the bodies was the lag
    /// itself — drag was the only term tying a body to the water, and a body that lags on a
    /// curved streamline drifts outward. The fix is therefore a force and not a field:
    /// <c>logbook/specs/water-carries-spec.md</c>'s fluid acceleration term, at which the same
    /// bodies in the same water hold the band. <see cref="ABodyRidesTheWater"/> carries every
    /// reading with the term off and on, and the doc comment there has the derivation, the reason
    /// the wall-layer check and the tracer check could not both be met without it, and the
    /// consequence for the design.
    /// </para>
    /// <para>
    /// <b>Measured on a lattice of this test's own</b>, coprime with the one the field balances
    /// itself on and stepped through time by a different irrational, so these are second opinions
    /// rather than restatements.
    /// </para>
    /// </remarks>
    public class StreamsTests
    {
        private readonly ITestOutputHelper _output;

        public StreamsTests(ITestOutputHelper output) => _output = output;

        // The aquarium's first round: 100 m2 of footprint, which is a radius of 5.642 m, 60 m
        // deep, four rings of equal area. Read at the geometry the runs use rather than at a tidy
        // cylinder, because a tank ten times deeper than it is wide is a real property of this
        // water and not a rounding of it.
        private const float Area = 100f;
        private const int Rings = 4;
        private const float Depth = 60f;
        private const float Speed = 0.3f;
        private const float Period = 6000f;
        private const ulong Seed = 20260911UL;

        private static float Radius => TankGeometry.RadiusFor(Area);

        private static CurrentField Streams(float speed = Speed, ulong seed = Seed)
        {
            var field = new CurrentField
            {
                Mode = CurrentMode.Transport,
                Speed = speed,
                PeriodSeconds = Period,
                AdvectFields = true,
            };

            field.SetBox(
                (float)Math.Sqrt(Area / Rings), Rings, Depth, seed,
                patchesAcross: 1, shape: WorldShape.Tank, tankRadiusMetres: Radius);

            return field;
        }

        /// <summary>A point inside the circle, uniform in area, at an interior depth.</summary>
        private static (float X, float Y, float Z) Interior(Rng rng, double inset = 0d)
        {
            float r = (float)((Radius - inset) * Math.Sqrt(rng.NextFloat()));
            double theta = 2d * Math.PI * rng.NextFloat();

            return (
                (float)(Radius + r * Math.Cos(theta)),
                -1f - rng.NextFloat() * (Depth - 2f),
                (float)(Radius + r * Math.Sin(theta)));
        }

        [Fact]
        public void TheStreamsAreDivergenceFree()
        {
            CurrentField field = Streams();

            // A step small enough that a central difference's truncation is well under the
            // tolerance and large enough that the float the sampler returns is not the whole of the
            // answer, and set in far enough from the glass that the stencil stays in the water:
            // outside the wall the field reads the water at the wall on purpose, and a difference
            // across that clamp measures the clamp.
            const double H = 0.05;

            var rng = new Rng(7UL);
            double worst = 0d;
            double worstGradient = 0d;
            double rms = Measure(field).Rms;

            for (int i = 0; i < 1000; i++)
            {
                (float x, float y, float z) = Interior(rng, inset: 4d * H);
                double t = rng.NextFloat() * 4d * Period;

                double dudx = (Vx(field, x + H, y, z, t) - Vx(field, x - H, y, z, t)) / (2d * H);
                double dvdy = (Vy(field, x, y + H, z, t) - Vy(field, x, y - H, z, t)) / (2d * H);
                double dwdz = (Vz(field, x, y, z + H, t) - Vz(field, x, y, z - H, t)) / (2d * H);

                double divergence = Math.Abs(dudx + dvdy + dwdz);
                double gradient = Math.Abs(dudx) + Math.Abs(dvdy) + Math.Abs(dwdz);

                if (divergence > worst) worst = divergence;
                if (gradient > worstGradient) worstGradient = gradient;
            }

            _output.WriteLine(
                $"worst |div v| {worst:0.0000e+0} /s over 1,000 points, against gradients of up " +
                $"to {worstGradient:0.0000} /s and an RMS of {rms:0.0000} m/s");

            // The spec's bound: under 1e-3 of the RMS per metre. A field that was divergence-free
            // only where a test happened to look would sit at the size of one of those gradients
            // rather than at the size of the arithmetic.
            Assert.True(worst < 1e-3 * rms, $"worst divergence {worst:0.0000e+0} /s");
        }

        [Fact]
        public void NothingFlowsThroughTheGlass()
        {
            CurrentField field = Streams();

            var rng = new Rng(11UL);
            double worst = 0d;

            for (int i = 0; i < 200; i++)
            {
                double theta = 2d * Math.PI * rng.NextFloat();
                float y = -rng.NextFloat() * Depth;
                double t = rng.NextFloat() * 4d * Period;

                float x = (float)(Radius + Radius * Math.Cos(theta));
                float z = (float)(Radius + Radius * Math.Sin(theta));

                Float3 v = field.VelocityAt(x, y, z, t);

                // The radial component: the wall's own normal. The azimuthal and vertical ones are
                // free to be anything, because the glass is a vertical cylinder and water sliding
                // along it is water in a tank.
                double radial = v.X * Math.Cos(theta) + v.Z * Math.Sin(theta);
                worst = Math.Max(worst, Math.Abs(radial));
            }

            _output.WriteLine($"worst radial velocity at the glass {worst:0.0000e+0} m/s");

            Assert.True(worst < 1e-6, $"worst radial velocity {worst:0.0000e+0} m/s");
        }

        [Fact]
        public void NothingFlowsThroughTheSurfaceOrTheBed()
        {
            CurrentField field = Streams();

            var rng = new Rng(13UL);

            for (int i = 0; i < 200; i++)
            {
                (float x, _, float z) = Interior(rng);
                double t = rng.NextFloat() * 10d * Period;

                // Exactly, not nearly: Math.Sin(-Math.PI) is -1.2e-16 and a vertical velocity of
                // 1.2e-16 at the waterline is still a velocity, integrated over a run into the
                // lift that put a whole population six metres into the air (logbook/0022).
                Assert.Equal(0f, field.VelocityAt(x, 0f, z, t).Y);
                Assert.Equal(0f, field.VelocityAt(x, -Depth, z, t).Y);

                // And outside the water, where a body that overshot the surface or a parcel at the
                // bed reads the nearest face.
                Assert.Equal(0f, field.VelocityAt(x, 3f, z, t).Y);
                Assert.Equal(0f, field.VelocityAt(x, -Depth - 12f, z, t).Y);
            }
        }

        /// <summary>
        /// The knob is the RMS speed and the three axes carry a third of it each.
        /// </summary>
        /// <remarks>
        /// <b>The knob names the time-mean RMS, and this field's own RMS moves.</b> Two dozen
        /// breathing envelopes wander in and out of step, so the instantaneous RMS runs about 0.75
        /// to 1.27 of its mean; the construction averages the envelopes in closed form
        /// (<c>CurrentField.BuildStreams</c>) and this reading averages them by taking many
        /// instants, which is why it takes more than the gyre's eleven.
        /// </remarks>
        [Fact]
        public void TheRmsSpeedIsTheKnobAndTheAxesAreBalanced()
        {
            foreach (float speed in new[] { 0.05f, 0.3f, 1.2f })
            {
                CurrentField field = Streams(speed);

                Reading read = Measure(field, ManyInstants);
                (float eddies, float overturning) = field.StreamsComponentRms;

                _output.WriteLine(
                    $"knob {speed} m/s: RMS {read.Rms:0.0000} m/s, fastest sample {read.Fastest:0.0000} m/s " +
                    $"({read.Fastest / read.Rms:0.00}x the RMS), bound {field.MaximumTransportSpeed:0.0000} m/s " +
                    $"({field.MaximumTransportSpeed / read.Fastest:0.00}x the fastest sample); " +
                    $"per axis x {read.X:0.0000}, y {read.Y:0.0000}, z {read.Z:0.0000} m/s; " +
                    $"parts streams {eddies:0.0000}, overturning {overturning:0.0000} m/s; " +
                    $"the instant's own RMS runs {read.LeastInstant / read.Rms:0.000} to " +
                    $"{read.MostInstant / read.Rms:0.000} of the mean as the envelopes breathe; " +
                    $"dead pockets {read.Dead:0.000}");

                // The spec's 1%, which is tighter than the transport field's 5% because this scale
                // is measured rather than derived, and a measurement that disagreed with a second
                // lattice by more than that would mean the first lattice was too coarse for the
                // shapes it was measuring.
                Assert.Equal((double)speed, read.Rms, 0.01 * speed);

                // The owner's ruling of 2026-09-10 applied to the tank's water: up and down, left
                // and right, in all directions really.
                double most = Math.Max(read.X, Math.Max(read.Y, read.Z));
                double least = Math.Min(read.X, Math.Min(read.Y, read.Z));

                Assert.True(
                    most < 1.2 * least,
                    $"the axes are {most / least:0.00}x apart: x {read.X:0.0000}, y {read.Y:0.0000}, z {read.Z:0.0000} m/s");

                // The Courant check GridField.Advect makes is against this, and it has to be a
                // ceiling: a bound under the true maximum would pass a step the fastest water
                // crosses more than half a cell in.
                Assert.True(
                    field.MaximumTransportSpeed > read.Fastest,
                    $"the bound {field.MaximumTransportSpeed} m/s is under the fastest sample {read.Fastest} m/s");
            }
        }

        [Fact]
        public void TheDeadPocketFractionIsReported()
        {
            CurrentField field = Streams();

            Reading read = Measure(field, ManyInstants);

            _output.WriteLine(
                $"dead pockets: {read.Dead:0.000} of the live volume moves slower than a tenth of " +
                $"the RMS ({0.1 * read.Rms:0.0000} m/s of {read.Rms:0.0000}), fastest " +
                $"{read.Fastest:0.0000} m/s — at {Area} m2 and {Depth} m over {Rings} rings.");

            // Not an assertion about the number, which is the owner's to read: an assertion that
            // the field is not mostly still, which would mean the tank had a current in a ring
            // round its rim and nothing anywhere else.
            Assert.True(read.Dead < 0.5, $"{read.Dead:0.000} of the tank is dead water");
        }

        /// <summary>
        /// The half metre nearest the glass moves — the spec's wall-layer check.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The gyre's swirl and both of its overturning components went to zero at the wall, so the
        /// water a body pressed against the glass felt was the eddies' azimuthal flow and nothing
        /// else, and round 37 measured 0.025 m/s at the bodies where the knob said 0.1. A wall
        /// layer that does not move is a place a body can be held: it arrives by the centrifuge and
        /// then has nothing to carry it back out. Half the RMS is the spec's bar.
        /// </para>
        /// <para>
        /// <b>It passes at about the RMS itself, and that is a reading as well as a pass.</b> Every
        /// stream's radial part carries <c>(1 − s²)</c> and vanishes at the glass while its
        /// azimuthal part carries <c>f'(s)</c>, which is <i>largest</i> there — <c>∓2</c> for every
        /// <c>m</c> — so the wall is where this field is fastest and the flow there is almost
        /// entirely azimuthal. That is a wall jet, and it is the same water that pins a
        /// drag-coupled body to the glass: on drag alone, passing this check is what made
        /// <see cref="ABodyRidesTheWater"/>'s check unpassable, the two being one inequality read
        /// from either end. The fluid acceleration force is what unties them — it removes the
        /// <c>τ</c> the drift is proportional to — so this field can now be fast at the glass
        /// without the glass collecting the population.
        /// </para>
        /// </remarks>
        [Fact]
        public void TheWallLayerMoves()
        {
            CurrentField field = Streams();

            double rms = Measure(field).Rms;

            var rng = new Rng(23UL);
            double sum = 0d;
            double slowest = double.MaxValue;

            for (int i = 0; i < 500; i++)
            {
                // Uniform in the annulus between R - 0.5 m and the glass, at a random depth and a
                // random phase: the layer is thin, so sampling it uniformly in area would put
                // almost nothing in it.
                double theta = 2d * Math.PI * rng.NextFloat();
                double r = Radius - 0.5 * rng.NextFloat();
                float y = -0.5f - rng.NextFloat() * (Depth - 1f);
                double t = rng.NextFloat() * 8d * Period;

                float x = (float)(Radius + r * Math.Cos(theta));
                float z = (float)(Radius + r * Math.Sin(theta));

                double speed = field.VelocityAt(x, y, z, t).Magnitude;

                sum += speed;
                if (speed < slowest) slowest = speed;
            }

            double mean = sum / 500;

            _output.WriteLine(
                $"the wall layer: mean speed {mean:0.0000} m/s within 0.5 m of the glass over 500 " +
                $"points and phases, which is {mean / rms:0.00}x the field's RMS of {rms:0.0000} m/s; " +
                $"slowest sample {slowest:0.0000} m/s");

            Assert.True(
                mean > 0.5 * rms,
                $"the wall layer moves at {mean / rms:0.00}x the RMS, under the spec's half");
        }

        [Fact]
        public void TwoSeedsAreTwoDrawsOfTheWaterAndOneSeedReplays()
        {
            CurrentField first = Streams(seed: 1UL);
            CurrentField second = Streams(seed: 2UL);
            CurrentField again = Streams(seed: 1UL);

            var rng = new Rng(19UL);
            double apart = 0d;
            double same = 0d;

            for (int i = 0; i < 500; i++)
            {
                (float x, float y, float z) = Interior(rng);
                double t = rng.NextFloat() * 4d * Period;

                apart = Math.Max(apart, (first.VelocityAt(x, y, z, t) - second.VelocityAt(x, y, z, t)).Magnitude);
                same = Math.Max(same, (first.VelocityAt(x, y, z, t) - again.VelocityAt(x, y, z, t)).Magnitude);
            }

            _output.WriteLine($"two seeds differ by up to {apart:0.0000} m/s; one seed repeats to {same:0.0000e+0} m/s");

            // Five seeds of a round have to be five draws of the water as well as of the genome,
            // or they are not replicates (SetBox's remarks).
            Assert.True(apart > 0.01 * Speed, "two seeds drew the same water");
            Assert.Equal(0d, same);
        }

        /// <summary>
        /// A tracer that carries no lag keeps its spread — the field transports uniformly, and the
        /// instrument the next test reads is sound.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>This is a stronger statement than the pointwise divergence check, and a different
        /// one.</b> An incompressible flow preserves volume, so a cloud of fluid particles
        /// started uniform in area stays uniform for ever, at every instant, however the water
        /// folds. A field that was divergence-free at a thousand sampled points and not between
        /// them would fail here; so would an integrator that reported the field's own transport
        /// wrongly.
        /// </para>
        /// <para>
        /// <b>It is also the control that makes the body-like reading mean something.</b> With it,
        /// the rim share that <see cref="ABodyRidesTheWater"/> reads can only be the
        /// lag; without it the two candidate causes — the water and the instrument — are not
        /// separated. It earns its place in the default run on that: it is the one tracer run whose
        /// answer is known in advance from a theorem, so it is the one that can fail informatively.
        /// </para>
        /// </remarks>
        [Fact]
        public void APerfectTracerKeepsItsSpread()
        {
            CurrentField field = Streams();

            Drift drift = Tracers(
                (x, y, z, t) => field.VelocityAt(x, y, z, t),
                tau: 0d, seconds: 1000d, window: 500d);

            _output.WriteLine(
                $"a perfect tracer, {TracerCount} of them over 1,000 s at a {TracerStep} s step: " +
                $"ring shares centre to rim {drift.Shares[0]:0.000} / {drift.Shares[1]:0.000} / " +
                $"{drift.Shares[2]:0.000} / {drift.Shares[3]:0.000} against 0.250 each, rim quarter " +
                $"{drift.RimShare:0.000}, mean radius {drift.MeanRadius:0.000} m " +
                $"({2d * Radius / 3d:0.000} m if uniform in area), radial drift " +
                $"{drift.RadialDrift:+0.000;-0.000} m, {drift.Reflections:n0} reflections off the glass");

            // The spec's band, which a fluid particle satisfies by Liouville's theorem rather than
            // by luck. The tolerance is the finite sample of 200 tracers, not the field.
            Assert.InRange(drift.RimShare, 0.2, 0.3);
        }

        /// <summary>
        /// A tracer with a body's drag response gathers at the glass on drag alone, and rides the
        /// water once it feels the water's own acceleration — the reading the term was built for.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The spec's check, and it now passes — but only with the second force.</b>
        /// <c>logbook/specs/streams-spec.md</c> asks that the rim quarter hold between 0.2 and 0.3
        /// of the tracers. On §5.2's drag alone it holds about 0.85, where D089's gyre holds 0.80
        /// to 0.86 under the same instrument: the streams were not a materially better tank for a
        /// lagging body than the gyre was, which is what made the case for
        /// <c>fable-propose-water-carries.md</c>. With
        /// <see cref="FluidConfig.FluidAccelerationCoefficient"/> at 1 — <c>c</c> below — the same
        /// bodies in the same water hold the band, and so do the gyre's. The two columns are
        /// printed side by side so the record carries both.
        /// </para>
        /// <para>
        /// <b><c>c</c> = 1 is the neutral body, and that is an assumption worth stating.</b> The
        /// tracer's equation of motion here is <c>dv/dt = (u − v)/τ + c·Du/Dt</c>, which is the
        /// Morison term divided by the body's own effective mass on the assumption that the mass
        /// it displaces equals the mass it has — neutral buoyancy, which is what §5.2 hands every
        /// creature, and which makes the added mass cancel between the two sides. A body denser
        /// than the water it displaces feels a smaller fraction and still drifts;
        /// <c>FluidConfig.TissueExcessDensity</c> is where that would come from, and this
        /// instrument does not carry it.
        /// </para>
        /// <para>
        /// <b>The cause is not the field but the coupling, and the identity is short.</b> For a
        /// statistically stationary, θ-symmetric field in a disc, with no flux through the glass or
        /// the two faces, a particle whose velocity relaxes to the water on a timescale <c>τ</c>
        /// drifts radially at <c>(τ/r)[⟨u_θ²⟩ − ∂_r(r⟨u_r²⟩)]</c> to first order in <c>τ</c>
        /// (expand <c>v ≈ u − τ Du/Dt</c>, average over <c>θ</c> and depth, and use
        /// <c>∇·u = 0</c>). Two consequences, and both are visible in the readings below.
        /// </para>
        /// <para>
        /// <b>Over the area, the drift is outward for any current with any azimuthal motion in
        /// it.</b> Integrate the identity over the disc and the second term telescopes to
        /// <c>r⟨u_r²⟩</c> at the glass, which is zero because nothing flows through the glass, so
        /// the area-mean drift is <c>(2τ/R²)∫⟨u_θ²⟩dr</c> — positive unless the water never turns
        /// about the axis anywhere. Removing the gyre's one swirl removed the <c>m</c> = 0 term of
        /// that integral and left twenty-four others.
        /// </para>
        /// <para>
        /// <b>At the glass, a wall layer that moves and a rim that does not gather are mutually
        /// exclusive.</b> At <c>r = R</c> the identity reads
        /// <c>(τ/R)⟨u_θ²⟩(R) − τ·∂_r⟨u_r²⟩|_R</c>, and <c>⟨u_r²⟩</c> is non-negative and zero at
        /// the glass, so its derivative there cannot be positive: both terms push outward. The
        /// drift at the wall is therefore strictly outward unless the water is still there to first
        /// order — which is precisely <see cref="TheWallLayerMoves"/>'s failure mode, and what the
        /// gyre's dead wall layer was. The spec asks for both checks and no prescribed field can
        /// pass both.
        /// </para>
        /// <para>
        /// <b>The swirl-free reading is the identity's own control, and it is bimodal.</b> The
        /// overturning cells alone are axisymmetric — <c>⟨u_θ²⟩</c> is zero everywhere — so the
        /// area-mean drift is zero and no tracer ever touches the glass, not once in a whole run.
        /// What is left is the local term: <c>r⟨u_r²⟩ ∝ r³(1 − s)⁴</c> rises to <c>s</c> = 3/7 and
        /// falls after it, so the drift is inward inside that radius and outward beyond it, and the
        /// tracers duly pile at both ends and empty the two middle rings. The identity is not a
        /// story about the rim; it is a statement that a lagging body cannot be evenly spread in a
        /// prescribed current, and where it ends up is read off <c>⟨u_r²⟩</c> and <c>⟨u_θ²⟩</c>.
        /// </para>
        /// <para>
        /// <b>What fixes it is not a current but a force, and that is why no field could.</b> Real
        /// water holds a parcel on a curved streamline through its own pressure gradient, and a
        /// body immersed in it feels the same gradient — the second term of the Morison equation,
        /// <c>(ρV + m_added)·Du/Dt</c>, which §5.2 did not have. Put it in and the first-order slip
        /// the identity is built on cancels identically: <c>v = u</c> solves the equation of motion
        /// above, so the body is a tracer to first order in <c>τ</c> and what is left is
        /// <c>O(τ²)</c>. The identity does not care which current it is, so the fix could not have
        /// been one; <c>logbook/specs/water-carries-spec.md</c> is the build and
        /// <c>FluidAccelerationTests</c> holds the term itself.
        /// </para>
        /// <para>
        /// <b>The wall layer and the rim are no longer mutually exclusive either</b>, which is the
        /// second thing the term buys. The identity's outward drift is <c>τ</c> times a positive
        /// quantity, and the term removes the <c>τ</c>: a field can now move at the glass —
        /// <see cref="TheWallLayerMoves"/> — without that motion sweeping the population into it.
        /// Both of the streams spec's checks are met at once, by two rules rather than by one.
        /// </para>
        /// <para>
        /// Marked <c>Slow</c>: seven runs of 5,000 s at a 0.02 s step, 200 tracers apiece and two
        /// passes a step, which is 400 samples of the field per step and hundreds of millions
        /// across the test, before the carried term is counted at all. The two streams runs that
        /// carry it now pay a closed form rather than nine samples
        /// (<c>logbook/specs/streams-analytic-spec.md</c>, about a quarter of what the stencil
        /// cost); the gyre's carried run still differences its field, because the old
        /// construction is kept for one reading and not given a derivative. The assertion it
        /// makes is a property of a world rule, so it is worth the money, but not on every
        /// filtered run.
        /// </para>
        /// </remarks>
        [Trait("Category", "Slow")]
        [Fact]
        public void ABodyRidesTheWater()
        {
            CurrentField field = Streams();

            foreach (double tau in new[] { 0.5d, 2d })
            {
                foreach (double carry in new[] { 0d, 1d })
                {
                    // The field's own AccelerationAt, which in a tank is the closed form
                    // (logbook/specs/streams-analytic-spec.md) — so this reading is of the route
                    // the farm runs, and it is the check that swapping the stencil for algebra did
                    // not move the property the term was added for. The stencil's own reading of
                    // the same four runs is the D090 build's, and it is not restated here: the
                    // two routes agree to 0.15% of the RMS acceleration, which FluidAccelerationTests
                    // measures, and a rim share is not sensitive at that scale.
                    Drift drift = Tracers(
                        (x, y, z, t) => field.VelocityAt(x, y, z, t), tau, 5000d, 2000d,
                        carry: carry,
                        acceleration: field.AccelerationAt);

                    Report(
                        FormattableString.Invariant(
                            $"the streams at {Speed} m/s, τ = {tau} s, c = {carry}"),
                        drift);

                    // The streams spec's band, reachable now that a body feels the water's turns
                    // as well as its speed. Asserted at both response times, because the failure
                    // it replaces was worse at the shorter one — the drift is first order in τ and
                    // the gathering was not (0.91 at 0.5 s against 0.83 at 2 s), so a term that
                    // only worked for a slow body would be no fix at all.
                    if (carry > 0d) Assert.InRange(drift.RimShare, 0.2, 0.3);
                }
            }

            var gyre = new OldGyre(Radius, Depth, Period, Speed, Seed);

            foreach (double carry in new[] { 0d, 1d })
            {
                Report(
                    FormattableString.Invariant(
                        $"D089's gyre at {Speed} m/s, τ = 2 s, c = {carry}"),
                    Tracers((x, y, z, t) => gyre.At(x, y, z, t), 2d, 5000d, 2000d, carry: carry));
            }

            double scale = SwirlFreeScale(Speed);

            Report(
                FormattableString.Invariant($"the overturning alone at {Speed} m/s, τ = 2 s, c = 0"),
                Tracers((x, y, z, t) => SwirlFree(x, y, z, t, scale), 2d, 5000d, 2000d));

            void Report(string what, Drift drift)
            {
                _output.WriteLine(
                    $"{what}: ring shares centre to rim {drift.Shares[0]:0.000} / " +
                    $"{drift.Shares[1]:0.000} / {drift.Shares[2]:0.000} / {drift.Shares[3]:0.000} " +
                    $"(uniform in area is 0.250 each), rim quarter {drift.RimShare:0.000}; mean " +
                    $"radius {drift.MeanRadius:0.000} m ({2d * Radius / 3d:0.000} m if uniform), " +
                    $"radial drift {drift.RadialDrift:+0.000;-0.000} m; {drift.Reflections:n0} " +
                    $"reflections off the glass, {drift.SurfaceClamps:n0} at the surface, " +
                    $"{drift.BedClamps:n0} at the bed");

                Assert.InRange(drift.RimShare, 0d, 1d);
            }
        }

        /// <summary>
        /// What the tracer reading costs in scheme and in step: forward Euler invents most of a
        /// rim share out of its own truncation error, and the midpoint rule does not.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The first build of this instrument was Euler at the spec's 0.5 s step, and it
        /// reported a field that gathers when the field does not.</b> A perfect tracer's answer is
        /// 0.250 by theorem; Euler at 0.5 s reads about 0.78 of the rim quarter, at 0.1 s about
        /// 0.48, at 0.02 s about 0.29. The reason is the position and not the velocity: the
        /// relaxation is comfortable at <c>dt/τ</c> = 0.25, but the fastest water in this field
        /// runs nine times its RMS, so half a second carries a tracer more than a metre through a
        /// tank 5.6 m in radius, and a scheme that moves it on the velocity where it <i>was</i>
        /// stops preserving volume at first order. The midpoint rule reads 0.265 at the same 0.5 s
        /// step and 0.251 at 0.02 s.
        /// </para>
        /// <para>
        /// <b>So the spec's step was never the problem and its scheme was unstated.</b> Every
        /// tracer number in this class is taken by the midpoint rule at the physics step — which
        /// is cheap insurance rather than a necessity — and this test is the record of what the
        /// obvious implementation would have reported instead. It is the shape of thing CLAUDE.md's
        /// "suspiciously good results are usually a broken measurement" warns about, arriving with
        /// the sign flipped: a suspiciously <i>bad</i> result, from a broken measurement, about a
        /// field that would have been blamed for it.
        /// </para>
        /// <para>
        /// Marked <c>Slow</c> for <see cref="ABodyLikeTracerGathersAtTheRim"/>'s reason, and it
        /// asserts only the convergence it exists to show.
        /// </para>
        /// </remarks>
        [Trait("Category", "Slow")]
        [Fact]
        public void TheTracerReadingDependsOnTheSchemeAndTheStep()
        {
            CurrentField field = Streams();

            double finestMidpoint = 0d;

            foreach (bool euler in new[] { true, false })
            {
                foreach (double step in new[] { 0.5d, 0.1d, 0.02d })
                {
                    Drift drift = Tracers(
                        (x, y, z, t) => field.VelocityAt(x, y, z, t),
                        tau: 0d, seconds: 1000d, window: 500d, step: step, euler: euler);

                    _output.WriteLine(
                        $"a perfect tracer, {(euler ? "Euler" : "midpoint")} at a {step} s step: ring " +
                        $"shares {drift.Shares[0]:0.000} / {drift.Shares[1]:0.000} / " +
                        $"{drift.Shares[2]:0.000} / {drift.Shares[3]:0.000}, rim quarter " +
                        $"{drift.RimShare:0.000} where the theorem says 0.250, mean radius " +
                        $"{drift.MeanRadius:0.000} m, {drift.Reflections:n0} reflections off the glass");

                    if (!euler && step == 0.02d) finestMidpoint = drift.RimShare;
                }
            }

            // The scheme the class uses, at the step the class uses, on the one field whose answer
            // is known in advance.
            Assert.InRange(finestMidpoint, 0.23, 0.27);
        }

        // -------------------------------------------------------------------------- the tracers

        private const int TracerCount = 200;
        private const double TracerStep = 0.02d;

        private struct Drift
        {
            public double[] Shares;
            public double RimShare;
            public double RadialDrift;
            public double MeanRadius;
            public int Reflections;
            public int SurfaceClamps;
            public int BedClamps;
        }

        /// <summary>
        /// Integrates <see cref="TracerCount"/> passive tracers through a field and returns what
        /// share of them the four rings of equal area hold, averaged over the last
        /// <paramref name="window"/> seconds.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>First-order drag response and the water's own acceleration, integrated by the
        /// midpoint rule.</b>
        /// <c>dv/dt = (u − v)/τ + c·Du/Dt</c>, the streams spec's equation plus
        /// <c>logbook/specs/water-carries-spec.md</c>'s term — <c>c</c> is
        /// <paramref name="carry"/>, 0 is drag alone and every reading taken before
        /// 2026-09-12, and 1 is a neutrally buoyant body, for which the Morison force divided by
        /// the body's effective mass is the water's acceleration exactly (see
        /// <see cref="ABodyRidesTheWater"/> on that assumption). The derivative comes from
        /// <paramref name="acceleration"/> when the caller has a field that can give it —
        /// <c>CurrentField.AccelerationAt</c>, which for the streams is now closed form
        /// (<c>logbook/specs/streams-analytic-spec.md</c>) — and otherwise from
        /// <see cref="CurrentField.MaterialDerivative"/> differencing whatever field was handed
        /// in, at nine samples a call. Either way it is the farm's own route and not a copy of
        /// it, which is the property that matters: what a tracer feels here and what a part feels
        /// in a run are one function.
        /// And <c>τ = 0</c> means the tracer
        /// <i>is</i> the water — a fluid particle, whose distribution an incompressible flow cannot
        /// change. The point is not to integrate the water accurately, since the physics does that
        /// with PhysX and a quadratic drag, but to carry the one property a prescribed field can
        /// get wrong: that a body lags, and a lagging body leaves a curved streamline. The midpoint
        /// rule rather than Euler because volume preservation is the whole measurement here and
        /// Euler loses it at first order — badly enough to invent most of a rim share on its own,
        /// which is what <see cref="TheTracerReadingDependsOnTheSchemeAndTheStep"/> records.
        /// </para>
        /// <para>
        /// <b>Two passes over the tracers per step, not one</b>, so that every sample of the field
        /// inside a pass is at one instant. The field caches each instant's phases
        /// (<c>CurrentField.EnsureInstant</c>), and a loop that asked for <c>t</c> and
        /// <c>t + dt/2</c> alternately would rebuild 27 terms' worth of trigonometry twice per
        /// tracer instead of twice per step.
        /// </para>
        /// <para>
        /// <b>The response time.</b> §5.2's drag is quadratic, so there is no single τ:
        /// linearising about the flow speed <c>u</c> for a part of mass <c>m</c> presenting area
        /// <c>A</c> gives <c>τ = m/(ρ·C_d·A·u)</c>. At <see cref="FluidConfig"/>'s defaults —
        /// ρ = 1000, C_d = 1.5, added mass 0, which is what every recorded config carries — a
        /// founder-scale part 0.2 m across (A = 0.04 m², V = 0.008 m³, m ≈ 8 kg at tissue near the
        /// water's density) comes out at 1.3 s in 0.1 m/s water and 0.44 s in 0.3 m/s. So the
        /// spec's 2 s is an assumed response time rather than a derived one, and the readings are
        /// taken at 0.5 s as well.
        /// </para>
        /// <para>
        /// <b>Reflected at the glass, clamped at the surface and the bed</b>, which is what the
        /// world does: the wall is a collider (<c>Evosim.Sim.TankWall</c>) and the two faces are
        /// D077's restoring boundary and the sea floor. The reflection count is reported, because
        /// the field has no radial flow at the glass — so every reflection is a tracer that arrived
        /// there on its own inertia, which is the centrifuge's own signature.
        /// </para>
        /// <para>
        /// Tracers start uniform in area over the disc at uniform random depth, which is the
        /// distribution the founders are placed in (<c>SharedVolume</c>) and the one a field that
        /// mixes should preserve.
        /// </para>
        /// </remarks>
        private static Drift Tracers(
            Func<float, float, float, double, Float3> water,
            double tau, double seconds, double window, double step = TracerStep, bool euler = false,
            double carry = 0d,
            Func<float, float, float, double, Float3> acceleration = null)
        {
            var rng = new Rng(31UL);

            // The field's own Du/Dt where it has one, the stencil on the sampler where it does
            // not — see the remarks. Bound once rather than tested per tracer per step.
            Func<float, float, float, double, Float3> derivative = acceleration ??
                ((px, py, pz, pt) => CurrentField.MaterialDerivative(water, px, py, pz, pt));

            var x = new double[TracerCount];
            var y = new double[TracerCount];
            var z = new double[TracerCount];
            var vx = new double[TracerCount];
            var vy = new double[TracerCount];
            var vz = new double[TracerCount];

            // The midpoint rule's half-step state.
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
                y[i] = -rng.NextFloat() * Depth;

                startRadius += r;
            }

            startRadius /= TracerCount;

            bool perfect = !(tau > 0d);

            var rings = new double[4];
            int samples = 0;
            double radiusSum = 0d;
            int reflections = 0, surfaceClamps = 0, bedClamps = 0;

            int steps = (int)(seconds / step);
            int every = Math.Max(1, (int)(100d / step));

            for (int number = 1; number <= steps; number++)
            {
                double t = (number - 1) * step;

                // Pass one, all at t: the slope at the tracer, and either the whole step (Euler)
                // or the half-step state (the midpoint rule).
                for (int i = 0; i < TracerCount; i++)
                {
                    Float3 u = water((float)x[i], (float)y[i], (float)z[i], t);

                    // The water's own acceleration, through the same route the fluid model uses —
                    // the field's AccelerationAt, or the stencil on the sampler for a field that
                    // has no closed form — rather than a copy of it, so that what a tracer feels
                    // here and what a part feels in the farm are one function.
                    Float3 a = carry > 0d && !perfect
                        ? derivative((float)x[i], (float)y[i], (float)z[i], t)
                        : Float3.Zero;

                    if (euler)
                    {
                        if (perfect)
                        {
                            vx[i] = u.X; vy[i] = u.Y; vz[i] = u.Z;
                        }
                        else
                        {
                            vx[i] += (u.X - vx[i]) * step / tau + carry * a.X * step;
                            vy[i] += (u.Y - vy[i]) * step / tau + carry * a.Y * step;
                            vz[i] += (u.Z - vz[i]) * step / tau + carry * a.Z * step;
                        }

                        x[i] += vx[i] * step;
                        y[i] += vy[i] * step;
                        z[i] += vz[i] * step;

                        Settle(i);
                        continue;
                    }

                    if (perfect)
                    {
                        nx[i] = u.X; ny[i] = u.Y; nz[i] = u.Z;
                    }
                    else
                    {
                        nx[i] = vx[i] + (u.X - vx[i]) * step / (2d * tau) + carry * a.X * step / 2d;
                        ny[i] = vy[i] + (u.Y - vy[i]) * step / (2d * tau) + carry * a.Y * step / 2d;
                        nz[i] = vz[i] + (u.Z - vz[i]) * step / (2d * tau) + carry * a.Z * step / 2d;
                    }

                    mx[i] = x[i] + (perfect ? u.X : vx[i]) * step / 2d;
                    my[i] = y[i] + (perfect ? u.Y : vy[i]) * step / 2d;
                    mz[i] = z[i] + (perfect ? u.Z : vz[i]) * step / 2d;
                }

                // Pass two, all at t + dt/2: the midpoint slope, and the step.
                double half = t + step / 2d;

                if (!euler)
                {
                    for (int i = 0; i < TracerCount; i++)
                    {
                        Float3 u = water((float)mx[i], (float)my[i], (float)mz[i], half);

                        if (perfect)
                        {
                            x[i] += u.X * step;
                            y[i] += u.Y * step;
                            z[i] += u.Z * step;
                        }
                        else
                        {
                            Float3 a = carry > 0d
                                ? derivative((float)mx[i], (float)my[i], (float)mz[i], half)
                                : Float3.Zero;

                            x[i] += nx[i] * step;
                            y[i] += ny[i] * step;
                            z[i] += nz[i] * step;

                            vx[i] += (u.X - nx[i]) * step / tau + carry * a.X * step;
                            vy[i] += (u.Y - ny[i]) * step / tau + carry * a.Y * step;
                            vz[i] += (u.Z - nz[i]) * step / tau + carry * a.Z * step;
                        }

                        Settle(i);
                    }
                }

                if (number * step <= seconds - window || number % every != 0) continue;

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

            // The glass and the two faces, applied once per tracer per step by whichever scheme
            // took the step.
            void Settle(int i)
            {
                double dx = x[i] - Radius;
                double dz = z[i] - Radius;
                double r = Math.Sqrt(dx * dx + dz * dz);

                if (r > Radius && r > 0d)
                {
                    // Mirror the overshoot back inside and turn the radial component of the
                    // velocity round; the azimuthal component slides along the glass.
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
                    surfaceClamps++;
                }
                else if (y[i] < -Depth)
                {
                    y[i] = -Depth;
                    if (vy[i] < 0d) vy[i] = 0d;
                    bedClamps++;
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
                SurfaceClamps = surfaceClamps,
                BedClamps = bedClamps,
            };
        }

        /// <summary>
        /// Three axisymmetric overturning cells and nothing else, at <paramref name="scale"/> —
        /// the identity's control in <see cref="ABodyLikeTracerGathersAtTheRim"/>.
        /// </summary>
        /// <remarks>
        /// The same stream functions <c>CurrentField</c> uses, with fixed phases rather than seeded
        /// ones, because the only property being read is that no azimuthal flow exists anywhere.
        /// Written here rather than as a mode of the field: a current with no horizontal stirring
        /// is not a world anybody wants to run, only a term to measure against.
        /// </remarks>
        private static Float3 SwirlFree(float x, float y, float z, double seconds, double scale)
        {
            double depth = Depth, radius = Radius;
            double t = 2d * Math.PI * seconds / Period;

            if (y > 0f) y = 0f;
            else if (y < -depth) y = (float)-depth;

            bool atFace = y >= 0d || y <= -depth;

            double dx = x - radius, dz = z - radius;
            double r = Math.Sqrt(dx * dx + dz * dz);
            double s = Math.Min(1d, r / radius);

            double radial = 0d, vy = 0d;

            for (int q = 1; q <= 3; q++)
            {
                double ky = q * Math.PI / depth;
                double amplitude = (1d / q) * Math.Cos((1d + q * 0.6180339887498949d) * t + q);

                radial -= amplitude * r * (1d - s) * (1d - s) * ky * Math.Cos(ky * y);
                if (!atFace) vy += 2d * amplitude * (1d - s) * (1d - 2d * s) * Math.Sin(ky * y);
            }

            double cos = r > 0d ? dx / r : 1d, sin = r > 0d ? dz / r : 0d;

            return new Float3(
                (float)(radial * cos * scale), (float)(vy * scale), (float)(radial * sin * scale));
        }

        /// <summary>What <see cref="SwirlFree"/> must be multiplied by for an RMS of the knob.</summary>
        private static double SwirlFreeScale(double speed)
        {
            var rng = new Rng(5UL);
            double sum = 0d;

            for (int i = 0; i < 20000; i++)
            {
                (float x, float y, float z) = Interior(rng);
                Float3 v = SwirlFree(x, y, z, rng.NextFloat() * 9d * Period, 1d);

                sum += (double)v.X * v.X + (double)v.Y * v.Y + (double)v.Z * v.Z;
            }

            return speed / Math.Sqrt(sum / 20000);
        }

        // --------------------------------------------------------------------- the second opinion

        private const int ManyInstants = 401;

        private struct Reading
        {
            public double Rms;
            public double X;
            public double Y;
            public double Z;
            public double Fastest;
            public double Dead;
            public double LeastInstant;
            public double MostInstant;
        }

        /// <summary>
        /// The field's RMS, its per-axis RMS, its fastest sample, its dead-pocket fraction and the
        /// range its instantaneous RMS covers, on a lattice of this test's own.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Coprime with the field's construction lattice — 21 and 19 against its 22 and 16 — and
        /// stepped through time by <c>√3</c> turns where the field steps by <c>√2</c>, so no sample
        /// lands where the field measured itself and no pair of terms holds the relative phase the
        /// construction held it at. A test that re-took the same points would confirm the
        /// arithmetic and nothing else.
        /// </para>
        /// <para>
        /// <b>The instants are a parameter because the field's RMS is a function of time.</b> Two
        /// dozen envelopes breathing out of step take the instantaneous RMS between about 0.75 and
        /// 1.27 of its mean, so a reading that has to land within 1% of the knob needs a few
        /// hundred instants (<see cref="ManyInstants"/>), while a reading that is only the
        /// denominator of a ratio — the divergence bound, the wall layer — is happy with 41.
        /// </para>
        /// </remarks>
        private static Reading Measure(CurrentField field, int instants = 41)
        {
            const int Horizontal = 21;
            const int Vertical = 19;

            double sumX = 0d, sumY = 0d, sumZ = 0d, fastest = 0d;
            int taken = 0;
            int still = 0;
            double least = double.MaxValue, most = 0d;

            var speeds = new double[Horizontal * Horizontal * Vertical * instants];

            for (int it = 0; it < instants; it++)
            {
                double t = Period * Math.Sqrt(3d) * it;

                double instantSum = 0d;
                int instantTaken = 0;

                for (int iy = 0; iy < Vertical; iy++)
                {
                    float y = -(float)((iy + 0.5) * Depth / Vertical);

                    for (int ix = 0; ix < Horizontal; ix++)
                    {
                        float x = (float)((ix + 0.5) * 2d * Radius / Horizontal);

                        for (int iz = 0; iz < Horizontal; iz++)
                        {
                            float z = (float)((iz + 0.5) * 2d * Radius / Horizontal);
                            if (!TankGeometry.Inside(x, z, Radius)) continue;

                            Float3 v = field.VelocityAt(x, y, z, t);

                            double square = (double)v.X * v.X + (double)v.Y * v.Y + (double)v.Z * v.Z;

                            sumX += (double)v.X * v.X;
                            sumY += (double)v.Y * v.Y;
                            sumZ += (double)v.Z * v.Z;

                            double speed = v.Magnitude;
                            speeds[taken] = speed;
                            if (speed > fastest) fastest = speed;

                            instantSum += square;
                            instantTaken++;
                            taken++;
                        }
                    }
                }

                double instant = Math.Sqrt(instantSum / instantTaken);
                if (instant < least) least = instant;
                if (instant > most) most = instant;
            }

            double rms = Math.Sqrt((sumX + sumY + sumZ) / taken);

            for (int i = 0; i < taken; i++)
            {
                if (speeds[i] < 0.1 * rms) still++;
            }

            return new Reading
            {
                Rms = rms,
                X = Math.Sqrt(sumX / taken),
                Y = Math.Sqrt(sumY / taken),
                Z = Math.Sqrt(sumZ / taken),
                Fastest = fastest,
                Dead = (double)still / taken,
                LeastInstant = least,
                MostInstant = most,
            };
        }

        private static double Vx(CurrentField f, double x, double y, double z, double t) =>
            f.VelocityAt((float)x, (float)y, (float)z, t).X;

        private static double Vy(CurrentField f, double x, double y, double z, double t) =>
            f.VelocityAt((float)x, (float)y, (float)z, t).Y;

        private static double Vz(CurrentField f, double x, double y, double z, double t) =>
            f.VelocityAt((float)x, (float)y, (float)z, t).Z;
    }

    /// <summary>
    /// D089's gyre, transcribed — a swirl about the axis, two overturning cells with a linear wall
    /// factor and two horizontal eddies, normalised the way <c>CurrentField.BuildStreams</c>
    /// normalises the field that replaced it.
    /// </summary>
    /// <remarks>
    /// Here and not in Core, so that the simulation carries no retired field: its only caller is
    /// <see cref="StreamsTests.ABodyLikeTracerGathersAtTheRim"/>, which exists to put the number
    /// the ruling of 2026-09-12 was made on beside the number the new field reads. It is a little
    /// over the hundred lines <c>logbook/specs/streams-spec.md</c> budgets for it, and the build
    /// report says so. <c>logbook/specs/tank-spec.md</c> is its contract.
    /// </remarks>
    internal sealed class OldGyre
    {
        private const double Incommensurate = 0.6180339887498949;

        private readonly double _radius;
        private readonly double _depth;
        private readonly double _period;
        private readonly float _speed;

        private readonly double[] _eddyPhase = new double[2];
        private readonly double[] _eddyRate = new double[2];
        private readonly double[] _cellPhase = new double[2];
        private readonly double[] _cellRate = new double[2];

        private double _swirlWeight = 1d;
        private double _eddyWeight = 1d;
        private readonly double _overturning;
        private readonly double _scale;

        public OldGyre(double radius, double depth, double period, float speed, ulong seed)
        {
            _radius = radius;
            _depth = depth;
            _period = period;
            _speed = speed;

            var rng = new Rng(seed);

            for (int m = 0; m < 2; m++)
            {
                _eddyPhase[m] = 2d * Math.PI * rng.NextFloat();
                _eddyRate[m] = Rate(m);
            }

            for (int q = 0; q < 2; q++)
            {
                _cellPhase[q] = 2d * Math.PI * rng.NextFloat();
                _cellRate[q] = Rate(2 + q);
            }

            double rawSwirl = 0d, rawEddies = 0d;
            int counted = Walk((x, y, z, t) =>
            {
                Float3 swirl = Unit(x, y, z, t, 0d, eddies: false);
                Float3 both = Unit(x, y, z, t, 0d);

                rawSwirl += (double)swirl.X * swirl.X + (double)swirl.Z * swirl.Z;

                double eddyX = both.X - swirl.X, eddyZ = both.Z - swirl.Z;
                rawEddies += eddyX * eddyX + eddyZ * eddyZ;
            });

            _swirlWeight = Math.Sqrt(counted / rawSwirl);
            _eddyWeight = Math.Sqrt(counted / rawEddies);

            double h0 = 0d, h1 = 0d, cross = 0d, vertical = 0d;

            Walk((x, y, z, t) =>
            {
                Float3 rest = Unit(x, y, z, t, 0d);
                Float3 whole = Unit(x, y, z, t, 1d);

                double overX = whole.X - rest.X, overZ = whole.Z - rest.Z;

                h0 += (double)rest.X * rest.X + (double)rest.Z * rest.Z;
                h1 += overX * overX + overZ * overZ;
                cross += rest.X * overX + rest.Z * overZ;
                vertical += (double)whole.Y * whole.Y;
            });

            double a = (vertical - 0.5 * h1) / counted;
            double b = -cross / counted;
            double c = -0.5 * h0 / counted;

            _overturning = (-b + Math.Sqrt(b * b - 4d * a * c)) / (2d * a);

            double meanSquare =
                (h0 + 2d * _overturning * cross +
                 _overturning * _overturning * (h1 + vertical)) / counted;

            _scale = 1d / Math.Sqrt(meanSquare);

            static double Rate(int j) => ((j & 1) == 0 ? 1d : -1d) * (1d + j * Incommensurate);
        }

        /// <summary>The RMS speed the construction was scaled to, m/s — the knob.</summary>
        public double Rms => _speed;

        public Float3 At(float x, float y, float z, double seconds) =>
            Unit(x, y, z, 2d * Math.PI * seconds / _period, _overturning) * (float)(_speed * _scale);

        private int Walk(Action<float, float, float, double> visit)
        {
            const int Horizontal = 22, Vertical = 16, Phases = 32;
            int taken = 0;

            for (int p = 0; p < Phases; p++)
            {
                double t = 2d * Math.PI * p / Incommensurate;

                for (int iy = 0; iy < Vertical; iy++)
                {
                    float y = -(float)((iy + 0.5) * _depth / Vertical);

                    for (int ix = 0; ix < Horizontal; ix++)
                    {
                        float x = (float)((ix + 0.5) * 2d * _radius / Horizontal);

                        for (int iz = 0; iz < Horizontal; iz++)
                        {
                            float z = (float)((iz + 0.5) * 2d * _radius / Horizontal);
                            if (!TankGeometry.Inside(x, z, _radius)) continue;

                            visit(x, y, z, t);
                            taken++;
                        }
                    }
                }
            }

            return taken;
        }

        private Float3 Unit(double x, double y, double z, double t, double overturning, bool eddies = true)
        {
            if (y > 0d) y = 0d;
            else if (y < -_depth) y = -_depth;

            bool atFace = y >= 0d || y <= -_depth;

            double dx = x - _radius, dz = z - _radius;
            double r = Math.Sqrt(dx * dx + dz * dz);
            double s = r / _radius;
            if (s > 1d) s = 1d;

            double theta = r > 0d ? Math.Atan2(dz, dx) : 0d;

            double radial = 0d, vy = 0d;
            double azimuthal = _swirlWeight * s * (1d - s * s) * (1d + 0.5d * Math.Cos(Math.PI * y / _depth));

            if (overturning != 0d)
            {
                for (int q = 0; q < 2; q++)
                {
                    double ky = (q + 1) * Math.PI / _depth;
                    double amplitude = overturning * Math.Cos(_cellRate[q] * t + _cellPhase[q]);

                    radial -= amplitude * r * (1d - s) * ky * Math.Cos(ky * y);
                    if (!atFace) vy += amplitude * (2d - 3d * s) * Math.Sin(ky * y);
                }
            }

            if (eddies)
            {
                for (int e = 0; e < 2; e++)
                {
                    int m = e + 1;
                    double ky = m * Math.PI / _depth;
                    double profile = atFace ? 0d : Math.Sin(ky * y);
                    if (profile == 0d) continue;

                    double chi = m * theta + _eddyPhase[e] + _eddyRate[e] * t;

                    double sPow = Math.Pow(s, m - 1);
                    double overR = sPow * (1d - s * s) / _radius;
                    double slope = (m * sPow - (m + 2) * sPow * s * s) / _radius;

                    radial -= _eddyWeight * m * overR * Math.Sin(chi) * profile;
                    azimuthal -= _eddyWeight * slope * Math.Cos(chi) * profile;
                }
            }

            double cos = r > 0d ? dx / r : 1d;
            double sin = r > 0d ? dz / r : 0d;

            return new Float3(
                (float)(radial * cos - azimuthal * sin),
                (float)vy,
                (float)(radial * sin + azimuthal * cos));
        }
    }
}
