using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// The four checks of the spike spec's "what it has to show", item 1.
    /// </summary>
    public sealed class SanityTests
    {
        private readonly ITestOutputHelper _out;

        public SanityTests(ITestOutputHelper output) => _out = output;

        /// <summary>
        /// Momentum held in still vacuum under internal drives — DESIGN §11.2's check. A
        /// creature's own joints cannot move its centre of mass, and a solver that lets them
        /// has handed evolution free thrust.
        /// </summary>
        [Fact]
        public void InternalDrivesMoveNoCentreOfMass()
        {
            // The solve's own invariant first, and it is the exact one: with no external force
            // the mass-weighted sum of the link accelerations the recursion produces is zero to
            // rounding, on every step of a hard-driven body.
            SolverConfig config = TestBodies.Vacuum(0.001);
            var world = new DynamicsWorld(config);
            Creature body = TestBodies.Build(TestBodies.Chain(4, JointType.Hinge, power: 20f), config);
            world.Add(body);

            double worstResidual = 0;
            double swing = 0;

            for (int step = 0; step < 4000; step++)
            {
                world.Step();

                Vec3 sum = Vec3.Zero;
                for (int i = 0; i < body.Links; i++)
                {
                    sum += Vec3.Read(body.Acc, 6 * i + 3) * body.Mass[i];
                }
                if (sum.Magnitude > worstResidual) worstResidual = sum.Magnitude;

                for (int i = 0; i < body.Dof; i++)
                {
                    double q = System.Math.Abs(body.Q[i]);
                    if (q > swing) swing = q;
                }
            }

            _out.WriteLine($"peak |sum m a| = {worstResidual:0.###e+00} N over mass {body.TotalMass:0.###} kg");
            _out.WriteLine($"largest joint excursion {swing:0.###} rad");

            Assert.True(swing > 0.5, $"the body never flexed (largest |q| = {swing})");
            Assert.True(worstResidual < 1e-9 * body.TotalMass,
                $"internal forces produced a net force of {worstResidual} N");

            // Then the integrated consequence, which is not exact and is not allowed to be a
            // mechanism: the centre of mass does drift, by an amount that falls with the step.
            // First order in dt is integration error; anything that did not fall with dt would
            // be free thrust.
            double coarse = Drift(0.002);
            double fine = Drift(0.0005);

            _out.WriteLine($"centre-of-mass drift over 4 s: dt 0.002 -> {coarse:0.###e+00} m, " +
                           $"dt 0.0005 -> {fine:0.###e+00} m (ratio {fine / coarse:0.###})");

            Assert.True(fine < 0.4 * coarse,
                $"the drift did not fall with the step: {coarse} then {fine}");
            Assert.True(fine < 5e-3, $"the centre of mass drifted {fine} m at dt 0.0005");
        }

        private static double Drift(double dt)
        {
            SolverConfig config = TestBodies.Vacuum(dt);
            var world = new DynamicsWorld(config);
            Creature body = TestBodies.Build(TestBodies.Chain(4, JointType.Hinge, power: 20f), config);
            world.Add(body);

            Vec3 start = body.CentreOfMass();
            int steps = (int)System.Math.Round(4.0 / dt);
            for (int step = 0; step < steps; step++) world.Step();

            return (body.CentreOfMass() - start).Magnitude;
        }

        /// <summary>
        /// Energy held by an undriven chain with no water. Nothing dissipates and nothing
        /// drives, so the kinetic energy of a flexing body is an invariant the integrator is
        /// allowed to wobble around and not to walk away from.
        /// </summary>
        [Fact]
        public void UndrivenChainHoldsItsEnergy()
        {
            SolverConfig config = TestBodies.Vacuum(0.0002);
            config.JointDriveDamping = 0;   // the one dissipation there would be
            var world = new DynamicsWorld(config);

            // Limits wide enough that the penalty springs never engage; they are a potential
            // this measurement does not count.
            Creature body = TestBodies.Build(
                TestBodies.Chain(4, JointType.Hinge, power: 1f, limit: 3.0f, neurons: false), config);

            for (int i = 0; i < body.Dof; i++) body.Qd[i] = (i % 2 == 0 ? 1.0 : -1.0) * 1.5;
            Kinematics.Refresh(body);

            world.Add(body);

            double start = body.KineticEnergy();
            double worst = 0;

            for (int step = 0; step < 25000; step++)
            {
                world.Step();
                double drift = System.Math.Abs(body.KineticEnergy() - start) / start;
                if (drift > worst) worst = drift;
            }

            _out.WriteLine($"KE {start:0.#####} J -> {body.KineticEnergy():0.#####} J over 5 s");
            _out.WriteLine($"worst relative excursion {worst:0.####%}");

            Assert.True(worst < 0.01, $"kinetic energy moved by {worst:P2}");
        }

        /// <summary>
        /// A box falling under its own excess weight reaches the speed at which the drag law
        /// balances it. Both terms are ported, so this checks the pair rather than either alone.
        /// </summary>
        [Fact]
        public void ASinkingBoxReachesTheDragLawsTerminalSpeed()
        {
            SolverConfig config = TestBodies.Water(0.002);
            config.TissueExcessDensity = 5.0;
            config.SurfaceRestoringFraction = 0;

            var half = new Float3(0.2f, 0.2f, 0.2f);
            Creature body = TestBodies.Build(TestBodies.Box(half), config);
            body.PlaceAt(new Vec3(0, -10, 0), QuatD.Identity);

            var world = new DynamicsWorld(config);
            world.Add(body);

            for (int step = 0; step < 30000; step++) world.Step();

            double volume = body.Volume[0];
            double weight = config.TissueExcessDensity * volume * config.GravityMetresPerSecondSquared;
            double area = 4.0 * half.X * half.Z;
            double k = 0.5 * config.Density * config.DragCoefficient;
            double expected = System.Math.Sqrt(weight / (k * area));

            double measured = -body.Velocity[1];

            _out.WriteLine($"excess weight {weight:0.####} N, leading face {area:0.####} m2");
            _out.WriteLine($"terminal speed: drag law {expected:0.#####} m/s, solver {measured:0.#####} m/s");

            Assert.True(System.Math.Abs(measured - expected) / expected < 0.01,
                $"terminal speed {measured} against the drag law's {expected}");

            // And sideways nothing: a box falling flat has no reason to wander.
            Assert.True(System.Math.Abs(body.Velocity[0]) < 1e-9);
            Assert.True(System.Math.Abs(body.Velocity[2]) < 1e-9);
        }

        /// <summary>
        /// A driven two-link body makes headway in water and none in vacuum — the whole point
        /// of a fluid that a paddle can push against.
        /// </summary>
        [Fact]
        public void ADrivenBodySwimsInWaterAndNotInVacuum()
        {
            Genome genome = TestBodies.Chain(
                2, JointType.Hinge, power: 15f, hz: 1.2f,
                rootHalf: new Float3(0.15f, 0.15f, 0.15f),
                linkHalf: new Float3(0.3f, 0.02f, 0.3f));

            // Both at two steps. In water the headway is a gait and stands as the step falls;
            // in vacuum there is nothing to push on, so whatever the centre of mass does is the
            // integrator's first-order drift and it falls with dt.
            double waterCoarse = Headway(genome, TestBodies.Water(0.002), 30.0, out double swing);
            double waterFine = Headway(genome, TestBodies.Water(0.0005), 30.0, out _);
            double vacuumCoarse = Headway(genome, TestBodies.Vacuum(0.002), 30.0, out double vacuumSwing);
            double vacuumFine = Headway(genome, TestBodies.Vacuum(0.0005), 30.0, out _);

            _out.WriteLine($"headway in water   dt 0.002 {waterCoarse:0.#####} m, dt 0.0005 {waterFine:0.#####} m");
            _out.WriteLine($"movement in vacuum dt 0.002 {vacuumCoarse:0.#####} m, dt 0.0005 {vacuumFine:0.#####} m");
            _out.WriteLine($"joint swing: water {swing:0.###} rad, vacuum {vacuumSwing:0.###} rad");

            Assert.True(swing > 0.2, "the joint never moved in water");
            Assert.True(vacuumSwing > 0.2, "the joint never moved in vacuum");

            Assert.True(vacuumFine < 0.5 * vacuumCoarse,
                $"the vacuum motion did not fall with the step: {vacuumCoarse} then {vacuumFine}");
            Assert.True(waterFine > 5 * vacuumFine,
                $"headway in water {waterFine} m against {vacuumFine} m of drift in vacuum");
            Assert.True(waterFine > 0.2 * waterCoarse,
                $"the headway in water fell away with the step: {waterCoarse} then {waterFine}");
        }

        private static double Headway(
            Genome genome, SolverConfig config, double seconds, out double swing)
        {
            Creature body = TestBodies.Build(genome, config);
            body.PlaceAt(new Vec3(0, -10, 0), QuatD.Identity);

            var world = new DynamicsWorld(config);
            world.Add(body);

            Vec3 start = body.CentreOfMass();
            swing = 0;

            int steps = (int)System.Math.Round(seconds / config.StepSeconds);
            for (int step = 0; step < steps; step++)
            {
                world.Step();
                double q = System.Math.Abs(body.Q[0]);
                if (q > swing) swing = q;
            }

            return (body.CentreOfMass() - start).Magnitude;
        }
    }
}
