using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// Package B's acceptance: a driven two-link body in still water for 600 s, with the signed
    /// joint work, the change in kinetic energy and the energy drag took held against each other.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The identity, written out.</b> In reduced coordinates there are no constraint forces to
    /// do work, so every joule that reaches the body comes through a generalised force:
    /// <c>ΔKE = W_drive + W_passive + W_drag</c>, and <c>DissipatedJoules</c> is <c>−W_drag</c> by
    /// its own definition. So
    /// <c>R = SignedWork − ΔKE − Dissipated + PassiveWork</c> is zero up to the integrator, and
    /// <c>|R| / |SignedWork|</c> is the number the package is accepted on.
    /// </para>
    /// <para>
    /// <b>The passive term is in there because it is real and the farm cannot see it.</b>
    /// <c>PhenotypeBuilder.MakeDrive</c> gives every joint 1 N·m·s/rad of drive damping so that
    /// undriven joints settle instead of ringing, and PhysX does that work inside its own solver.
    /// Dropped from the balance it is not a tolerance, it is the answer: at this body's stroke it
    /// is a fifth of the drive's own work. The limit springs are the other half of the term, and
    /// so is the implicit limit's own <c>−L·q̈</c>: adding <c>(c + k·dt)·dt</c> to a joint's
    /// <c>D</c> is the rotor-inertia trick, which is the same thing as a generalised torque
    /// proportional to the acceleration, and it does work. Left out, a chain resting on narrow
    /// stops reads a residual fourteen times its own drive work.
    /// </para>
    /// </remarks>
    public sealed class LedgerTests
    {
        private readonly ITestOutputHelper _out;

        public LedgerTests(ITestOutputHelper output) => _out = output;

        /// <summary>Still water with the campaign's coefficients, no weight and no contact.</summary>
        private static SolverConfig Still(double dt)
        {
            SolverConfig config = TestBodies.Water(dt);
            config.AddedMassCoefficient = 0.5;
            config.SurfaceRestoringFraction = 0;
            config.TissueExcessDensity = 0;
            config.CreatureContact = false;
            return config;
        }

        private static Genome Swimmer() => TestBodies.Chain(
            2, JointType.Hinge, power: 15f, hz: 1.2f,
            rootHalf: new Float3(0.15f, 0.15f, 0.15f),
            linkHalf: new Float3(0.3f, 0.02f, 0.3f),
            limit: 3.0f);

        private readonly struct Balance
        {
            public Balance(
                double signed, double unsigned, double dissipated, double passive,
                double kinetic, long dragLimited, double peakAngle, double limit)
            {
                Signed = signed;
                Unsigned = unsigned;
                Dissipated = dissipated;
                Passive = passive;
                Kinetic = kinetic;
                DragLimited = dragLimited;
                PeakAngle = peakAngle;
                Limit = limit;
            }

            public double Signed { get; }
            public double Unsigned { get; }
            public double Dissipated { get; }
            public double Passive { get; }
            public double Kinetic { get; }
            public long DragLimited { get; }
            public double PeakAngle { get; }
            public double Limit { get; }

            /// <summary>The residual with the passive torques accounted.</summary>
            public double Residual => Signed - Kinetic - Dissipated + Passive;

            /// <summary>The residual without them — what the term is worth.</summary>
            public double RawResidual => Signed - Kinetic - Dissipated;

            public double Relative => System.Math.Abs(Residual) / System.Math.Abs(Signed);
            public double RawRelative => System.Math.Abs(RawResidual) / System.Math.Abs(Signed);
        }

        private static Balance Swim(double dt, double seconds, float limit = 3.0f)
        {
            SolverConfig config = Still(dt);
            Genome genome = TestBodies.Chain(
                2, JointType.Hinge, power: 15f, hz: 1.2f,
                rootHalf: new Float3(0.15f, 0.15f, 0.15f),
                linkHalf: new Float3(0.3f, 0.02f, 0.3f),
                limit: limit);

            Creature body = TestBodies.Build(genome, config);
            body.PlaceAt(new Vec3(0, -10, 0), QuatD.Identity);

            var world = new DynamicsWorld(config);
            world.Add(body);

            double before = body.KineticEnergy();
            double peak = 0;

            int steps = (int)System.Math.Round(seconds / dt);
            for (int step = 0; step < steps; step++)
            {
                world.Step();
                for (int j = 0; j < body.Dof; j++)
                {
                    double q = System.Math.Abs(body.Q[j]);
                    if (q > peak) peak = q;
                }
            }

            return new Balance(
                body.SignedWorkJoules,
                body.MechanicalWorkJoules,
                body.DissipatedJoules,
                body.PassiveJointWorkJoules,
                body.KineticEnergy() - before,
                body.DragImpulsesLimited,
                peak, limit);
        }

        /// <summary>
        /// The acceptance itself: 600 s at dt 0.01, the step the historical record replays at.
        /// </summary>
        [Fact]
        public void TheEnergyBalanceClosesOverTenMinutesOfSwimming()
        {
            Balance b = Swim(0.01, 600.0);

            _out.WriteLine($"signed joint work   {b.Signed:0.####} J");
            _out.WriteLine($"unsigned            {b.Unsigned:0.####} J");
            _out.WriteLine($"dissipated by drag  {b.Dissipated:0.####} J");
            _out.WriteLine($"passive joint work  {b.Passive:0.####} J  (damper, limit springs, and the implicit limit's -L q̈)");
            _out.WriteLine($"largest excursion   {b.PeakAngle:0.###} rad against a stop at {b.Limit:0.###}");
            _out.WriteLine($"change in KE        {b.Kinetic:0.######} J");
            _out.WriteLine($"drag limiter binds  {b.DragLimited}");
            _out.WriteLine($"residual            {b.Residual:0.######} J  -> {b.Relative:0.####e+00} of the work");
            _out.WriteLine($"residual unaccounted{b.RawResidual,12:0.####} J  -> {b.RawRelative:0.####e+00}");

            Assert.True(b.Unsigned > 0, "the body never worked");
            Assert.Equal(0, b.DragLimited);

            // The drag's work must be a loss. A positive number here would be drag feeding a
            // creature, which is §11.2's failure arriving through the water.
            Assert.True(b.Dissipated > 0, $"drag added {-b.Dissipated} J to the body");

            // And the damper's work must be a loss too, for the same reason.
            Assert.True(b.Passive < 0, $"the drive damper produced {b.Passive} J");

            Assert.True(b.Relative < 1e-3,
                $"the energy balance is out by {b.Relative:0.###e+00} of the work " +
                $"({b.Residual} J against {b.Signed} J)");
        }

        /// <summary>
        /// The residual is the integrator's and not a leak: it falls with the step.
        /// </summary>
        /// <remarks>
        /// Sixty seconds rather than six hundred, because what is being read here is the slope
        /// and not the number — and because the finest step is eight times the work of the
        /// acceptance run.
        /// </remarks>
        [Fact]
        public void TheResidualFallsWithTheStep()
        {
            double[] steps = { 0.008, 0.004, 0.002, 0.001 };
            var relative = new double[steps.Length];

            for (int i = 0; i < steps.Length; i++)
            {
                Balance b = Swim(steps[i], 60.0);
                relative[i] = b.Relative;
                _out.WriteLine(
                    $"dt {steps[i]:0.0000}: work {b.Signed,9:0.###} J, residual {b.Residual,11:0.######} J " +
                    $"-> {b.Relative:0.###e+00}");
            }

            for (int i = 1; i < steps.Length; i++)
            {
                Assert.True(relative[i] < relative[i - 1],
                    $"the residual did not fall from dt {steps[i - 1]} to dt {steps[i]}: " +
                    $"{relative[i - 1]} then {relative[i]}");
            }
        }

        /// <summary>
        /// With the stops narrow enough to be hit, the limit springs are the passive term and the
        /// balance still closes — which is what says the term is the whole of the passive work
        /// and not a fitted correction for the damper.
        /// </summary>
        [Fact]
        public void TheLimitSpringsWorkIsAccountedToo()
        {
            Balance b = Swim(0.01, 120.0, limit: 0.15f);

            _out.WriteLine($"signed joint work  {b.Signed:0.####} J");
            _out.WriteLine($"dissipated by drag {b.Dissipated:0.####} J");
            _out.WriteLine($"passive joint work {b.Passive:0.####} J  (damper and limit springs)");
            _out.WriteLine($"largest excursion  {b.PeakAngle:0.###} rad against a stop at {b.Limit:0.###}");
            _out.WriteLine($"residual           {b.Residual:0.######} J -> {b.Relative:0.####e+00}");

            Assert.True(b.PeakAngle > b.Limit, "the stops were never reached, so this test is idle");
            Assert.True(b.Relative < 1e-2,
                $"the balance with the stops engaged is out by {b.Relative:0.###e+00}");
        }

        /// <summary>
        /// The unsigned total is the one the metabolism is billed from, and it is drained rather
        /// than read: two drains of one interval must not bill the same joule twice.
        /// </summary>
        [Fact]
        public void WorkIsDrainedOnceAndDissipationIsSummedInBodyOrder()
        {
            SolverConfig config = Still(0.01);
            var world = new DynamicsWorld(config);

            for (int i = 0; i < 4; i++)
            {
                Creature body = TestBodies.Build(Swimmer(), config, i);
                body.PlaceAt(new Vec3(3.0 * i, -10, 0), QuatD.Identity);
                world.Add(body);
            }

            for (int step = 0; step < 500; step++) world.Step();

            double first = 0;
            for (int i = 0; i < world.Creatures.Count; i++)
            {
                first += world.Creatures[i].DrainMechanicalWork();
            }

            double again = 0;
            for (int i = 0; i < world.Creatures.Count; i++)
            {
                again += world.Creatures[i].DrainMechanicalWork();
            }

            _out.WriteLine($"first drain {first:0.####} J, immediate second drain {again:0.####} J");
            _out.WriteLine($"total on the bodies {world.MechanicalWorkJoules():0.####} J");

            Assert.True(first > 0);
            Assert.Equal(0, again);
            Assert.Equal(world.MechanicalWorkJoules(), first, 9);

            double drained = world.DrainDissipated();
            _out.WriteLine($"dissipated {drained:0.####} J");
            Assert.Equal(world.DissipatedJoules(), drained, 9);
            Assert.Equal(0, world.DrainDissipated(), 12);

            for (int step = 0; step < 100; step++) world.Step();
            Assert.True(world.DrainDissipated() > 0);
        }

        /// <summary>
        /// The ledgers are per body and never shared, so the thread count cannot reach them:
        /// four threads must produce the same totals as one, to the bit.
        /// </summary>
        [Fact]
        public void TheLedgersDoNotDependOnTheThreadCount()
        {
            (double work, double dissipated, ulong digest) Run(int threads)
            {
                SolverConfig config = Still(0.01);
                var world = new DynamicsWorld(config) { Threads = threads };

                for (int i = 0; i < 24; i++)
                {
                    Creature body = TestBodies.Build(Swimmer(), config, i);
                    body.PlaceAt(new Vec3(3.0 * i, -10, 0), QuatD.Identity);
                    world.Add(body);
                }

                for (int step = 0; step < 1000; step++) world.Step();

                return (world.MechanicalWorkJoules(), world.DissipatedJoules(), world.Digest());
            }

            var one = Run(1);
            var four = Run(4);

            _out.WriteLine($"1 thread : work {one.work:R} J, dissipated {one.dissipated:R} J");
            _out.WriteLine($"4 threads: work {four.work:R} J, dissipated {four.dissipated:R} J");

            Assert.Equal(one.digest, four.digest);
            Assert.Equal(one.work, four.work);
            Assert.Equal(one.dissipated, four.dissipated);
        }
    }
}
