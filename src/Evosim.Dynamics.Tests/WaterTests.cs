using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// Package C's acceptance: the current reaches the solver, D100's hold is the farm's hold,
    /// and a body that displaces water goes where the water goes.
    /// </summary>
    public sealed class WaterTests
    {
        private readonly ITestOutputHelper _out;

        public WaterTests(ITestOutputHelper output) => _out = output;

        /// <summary>
        /// A small neutrally buoyant one-part body, released moving with the water, against a
        /// tracer integrated straight off <see cref="CurrentField.VelocityAt"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Why it should track at all, and why only with D090's term on.</b> The acceleration
        /// force is <c>c·(ρV + m_added)·Du/Dt</c>, and at neutral buoyancy the mass it is
        /// dividing into is <c>ρV + m_added</c> exactly — tissue and water are both 1,000 kg/m3
        /// — so at <c>fluidAccel</c> 1 the body's acceleration <i>is</i> the parcel's, whatever
        /// the drag does. Drag then only bleeds the difference the start and the integrator
        /// leave. With the term off the body keeps its own straight line through every turn the
        /// water makes, which is D090's centrifuge, and the error would be metres.
        /// </para>
        /// <para>
        /// <b>Per link, not held</b>, so the comparison is against the water at the body rather
        /// than against a sample up to half a second old; the held case is measured beside it and
        /// reported, because that is what round 42 ran.
        /// </para>
        /// </remarks>
        [Fact]
        public void ABodyInTheStreamsFollowsATracer()
        {
            double perLink = Tracer(hold: 0, accelerating: true, out double path);
            double heldShort = Tracer(hold: 0.05, accelerating: true, out _);
            double heldLong = Tracer(hold: 0.5, accelerating: true, out _);
            double dragOnly = Tracer(hold: 0, accelerating: false, out _);

            _out.WriteLine($"path length over 1,000 s      {path:0.###} m");
            _out.WriteLine($"per-link, fluidAccel on       {perLink:0.####} m from the tracer");
            _out.WriteLine($"held 0.05 s, fluidAccel on    {heldShort:0.####} m");
            _out.WriteLine($"held 0.5 s, fluidAccel on     {heldLong:0.####} m   (round 42's own hold)");
            _out.WriteLine($"per-link, fluidAccel off      {dragOnly:0.####} m   (D090's centrifuge)");

            Assert.True(path > 20, $"the water barely moved the tracer ({path} m of path)");

            // The acceptance: a few centimetres over a thousand seconds and ninety metres of
            // path. What is left is discretisation — at dt 0.001 the same run reads 0.007 m.
            Assert.True(perLink < 0.15,
                $"the body parted from its parcel of water by {perLink} m over 1,000 s");

            Assert.True(dragOnly > 20 * perLink,
                "with D090's term off the body tracked the water as well as with it on, " +
                "which means the term is not reaching the solver");

            // D100's hold is a cheapening and it costs the tracking: the shorter hold must sit
            // between the two, which is what says the plumbing is right and the drift is the
            // rule's rather than the port's.
            Assert.True(heldShort < heldLong,
                $"a 0.05 s hold drifted {heldShort} m and a 0.5 s hold {heldLong} m");
            Assert.True(heldShort > perLink);
        }

        private double Tracer(double hold, bool accelerating, out double pathLength)
        {
            SolverConfig config = R42Tank.Config(0.01, hold);
            if (!accelerating) config.FluidAccelerationCoefficient = 0;

            CurrentField current = config.Current;

            // Small enough to be inside NeutralBodyVolume, so D064 makes it exactly neutral.
            Creature body = TestBodies.Build(TestBodies.Box(new Float3(0.1f, 0.1f, 0.1f)), config);
            Vec3 start = R42Tank.At(3.0, -20.0, 2.0);
            body.PlaceAt(start, QuatD.Identity);

            // Released moving with the water, which is what "released in the streams" means: a
            // body put down at rest spends its first minute catching up, and that transient is
            // the drag law's, not the current's.
            Float3 u0 = current.VelocityAt((float)start.X, (float)start.Y, (float)start.Z, 0d);
            Vec3.Write(body.Velocity, 0, new Vec3(u0.X, u0.Y, u0.Z));

            var world = new DynamicsWorld(config);
            world.Add(body);

            Vec3 tracer = start;
            double dt = config.StepSeconds;
            double clock = 0;
            pathLength = 0;

            for (int step = 0; step < 100000; step++)
            {
                // The same ordering the world uses: sample at the place and the clock the step
                // starts from, then advance.
                Float3 u = current.VelocityAt(
                    (float)tracer.X, (float)tracer.Y, (float)tracer.Z, clock);

                var move = new Vec3(u.X, u.Y, u.Z) * dt;
                tracer += move;
                pathLength += move.Magnitude;

                world.Step();
                clock += dt;
            }

            return (Vec3.Read(body.Position, 0) - tracer).Magnitude;
        }

        /// <summary>
        /// D100's hold and the per-link path are the same arithmetic when the hold expires every
        /// step, for a body with one link — and the same body with several links shows the hold
        /// doing what it exists to do.
        /// </summary>
        [Fact]
        public void TheHeldPathIsThePerLinkPathWhenTheHoldExpiresEveryStep()
        {
            ulong onePartFree = Run(TestBodies.Box(new Float3(0.12f, 0.12f, 0.12f)), 0);
            ulong onePartHeld = Run(TestBodies.Box(new Float3(0.12f, 0.12f, 0.12f)), 0.005);

            _out.WriteLine($"one part, hold 0      {onePartFree:x16}");
            _out.WriteLine($"one part, hold 0.005  {onePartHeld:x16}");

            Assert.Equal(onePartFree, onePartHeld);

            Genome chain = TestBodies.Chain(4, JointType.Hinge, power: 8f, hz: 0.7f);
            ulong chainFree = Run(chain, 0);
            ulong chainHeld = Run(chain, 0.005);
            ulong chainHeldLong = Run(chain, 0.5);

            _out.WriteLine($"four parts, hold 0      {chainFree:x16}");
            _out.WriteLine($"four parts, hold 0.005  {chainHeld:x16}");
            _out.WriteLine($"four parts, hold 0.5    {chainHeldLong:x16}");

            // A held body feeds every link the root's parcel, so a multi-link body must differ
            // even when the hold expires every step. If it did not, the hold would be reading
            // the water per link and D100 would not be implemented.
            Assert.NotEqual(chainFree, chainHeld);
            Assert.NotEqual(chainHeld, chainHeldLong);

            ulong Run(Genome genome, double hold)
            {
                SolverConfig config = R42Tank.Config(0.01, hold);
                Creature body = TestBodies.Build(genome, config);
                body.PlaceAt(R42Tank.At(4.0, -18.0, -3.0), QuatD.Identity);

                var world = new DynamicsWorld(config);
                world.Add(body);

                for (int step = 0; step < 3000; step++) world.Step();
                return world.Digest();
            }
        }

        /// <summary>
        /// The hold is keyed to the world clock and to nothing else, and a body samples only its
        /// own cache.
        /// </summary>
        [Fact]
        public void TheHoldIsKeyedToTheWorldClock()
        {
            SolverConfig config = R42Tank.Config(0.01, 0.5);
            Creature body = TestBodies.Build(
                TestBodies.Box(new Float3(0.12f, 0.12f, 0.12f)), config);
            body.PlaceAt(R42Tank.At(2.0, -12.0, 1.0), QuatD.Identity);

            var world = new DynamicsWorld(config);
            world.Add(body);

            Assert.Equal(double.NegativeInfinity, body.WaterSampledAt);

            world.Step();
            Assert.Equal(0d, body.WaterSampledAt);

            for (int step = 0; step < 49; step++) world.Step();
            Assert.Equal(0d, body.WaterSampledAt);      // still inside the first hold

            world.Step();                                // t = 0.50 s exactly
            Assert.Equal(0.5d, body.WaterSampledAt, 12);

            _out.WriteLine($"held water {body.HeldWater.X:0.#####}, {body.HeldWater.Y:0.#####}, " +
                           $"{body.HeldWater.Z:0.#####} m/s at t = {body.WaterSampledAt} s");
            _out.WriteLine($"held acceleration {body.HeldWaterAcceleration.X:0.#####}, " +
                           $"{body.HeldWaterAcceleration.Y:0.#####}, " +
                           $"{body.HeldWaterAcceleration.Z:0.#####} m/s2");

            Assert.True(
                body.HeldWater.X != 0f || body.HeldWater.Y != 0f || body.HeldWater.Z != 0f,
                "the current never reached the body");
        }

        /// <summary>
        /// A drag pass with a current in it is drag against the <i>relative</i> velocity: a body
        /// held still in moving water must feel what a body swimming through still water at the
        /// same speed feels, which is the whole of D036's physics.
        /// </summary>
        [Fact]
        public void DragIsAgainstTheWaterAndNotTheWorld()
        {
            SolverConfig config = R42Tank.Config(0.01, 0);
            Creature body = TestBodies.Build(
                TestBodies.Box(new Float3(0.2f, 0.2f, 0.2f)), config);
            body.PlaceAt(R42Tank.At(1.0, -22.0, -1.0), QuatD.Identity);

            var world = new DynamicsWorld(config);
            world.Add(body);
            world.Step();

            Vec3 water = Vec3.Read(body.Water, 0);
            Vec3 relative = Vec3.Read(body.RelativeVelocity, 0);

            _out.WriteLine($"water at the body {water.X:0.#####}, {water.Y:0.#####}, {water.Z:0.#####} m/s");
            _out.WriteLine($"relative velocity {relative.X:0.#####}, {relative.Y:0.#####}, {relative.Z:0.#####} m/s");

            Assert.True(water.Magnitude > 0, "no current reached the drag pass");

            // The body started at rest in the world, so on the first step its velocity relative
            // to the water is exactly minus the water — the sign that says drag is advection.
            Assert.True((relative + water).Magnitude < 1e-12);
        }
    }
}
