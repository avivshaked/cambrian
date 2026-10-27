using System;
using System.Collections.Generic;
using System.Globalization;
using Evosim.Core;
using Evosim.Dynamics;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// D241's growth physics: what the solver does with a juvenile whose joint torque scales with its size
    /// (<see cref="RunConfig.JuvenileStrengthScales"/>), and what a climbing body's resizes do to its momentum
    /// against the single jump of today's growth (the growth design's 5d, <c>logbook/specs/growth-runway-design.md</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Written without being run</b> (the build's brief forbade it); the caller runs them in a test window. Both
    /// write their measurements to the test output, because the speeds, the works and the momentum changes are
    /// readings and not thresholds. What they assert is what is true by construction: the torque limit's ratio,
    /// a root that never moves at a resize, a world with no divergence, and a climb whose largest step is
    /// smaller than the one jump it replaces.
    /// </para>
    /// <para>
    /// The resize hook is <c>Simulation.BeforeResize</c> and <c>AfterResize</c>, two internal delegates the growth
    /// step calls around <c>Creature.Resize</c>. They are null in a run.
    /// </para>
    /// </remarks>
    [Collection(FarmProcessCollection.Name)]
    public class GrowthPhysicsTests
    {
        private readonly ITestOutputHelper _out;

        public GrowthPhysicsTests(ITestOutputHelper output) => _out = output;

        // ---------------------------------------------------------------- still water

        /// <summary>
        /// The Dynamics ledger test's still water: the campaign's coefficients, no weight, no contact, no current.
        /// </summary>
        private static SolverConfig Still(double dt) => new SolverConfig
        {
            StepSeconds = dt,
            Density = 1000,
            DragCoefficient = 1.5,
            PanelsPerAxis = 2,
            AddedMassCoefficient = 0.5,
            FluidAccelerationCoefficient = 0,
            TissueExcessDensity = 0,
            NeutralBodyVolume = 0,
            SurfaceRestoringFraction = 0,
            WorldDepthMetres = 1e6,
            TankRadiusMetres = 0,
            CreatureContact = false,
            JointDriveDamping = 1,
        };

        /// <summary>A structural root and links behind it, each link carrying one oscillator: the ledger test's swimmer.</summary>
        private static Genome Swimmer(int links = 2, float power = 15f, float hz = 1.2f)
        {
            var genome = new Genome { RootIndex = 0, AdultScale = 1f };
            genome.Reproduction = new ReproductionTraits { BroodSize = 1, BirthInvestment = 0.5f, ReserveMargin = 0f };

            genome.Nodes.Add(new MorphNode
            {
                Dimensions = new Float3(0.15f, 0.15f, 0.15f),
                CellTypeId = CellTypeIds.Structural,
                ShapeId = ShapeIds.Box,
                JointType = JointType.Fixed,
                JointLimits = Array.Empty<Float2>(),
                RecursiveLimit = 1,
            });

            for (int i = 1; i < links; i++)
            {
                genome.Nodes.Add(new MorphNode
                {
                    Dimensions = new Float3(0.3f, 0.02f, 0.3f),
                    CellTypeId = CellTypeIds.Link,
                    ShapeId = ShapeIds.Box,
                    JointType = JointType.Hinge,
                    JointLimits = new[] { new Float2(-3f, 3f) },
                    Power = power,
                    RecursiveLimit = 1,
                    Neurons = new[]
                    {
                        new NeuronDef
                        {
                            Op = NeuronOp.OscillateWave,
                            Frequency = hz,
                            Phase = i * 0.6f,
                            Amplitude = 1f,
                            Bias = 0f,
                            Inputs = Array.Empty<NeuronInput>(),
                        },
                    },
                });

                genome.Nodes[i - 1].Edges.Add(new MorphEdge
                {
                    Child = i,
                    ParentAnchor = new Float3(1f, 0f, 0f),
                    ChildAnchor = new Float3(-1f, 0f, 0f),
                    Orientation = Quat.Identity,
                    Scale = Float3.One,
                });
            }

            return genome;
        }

        private readonly struct Swim
        {
            public Swim(Creature body, double meanSpeed, double netSpeed, double workPerSecond)
            {
                Body = body;
                MeanSpeed = meanSpeed;
                NetSpeed = netSpeed;
                WorkPerSecond = workPerSecond;
            }

            public Creature Body { get; }
            public double MeanSpeed { get; }
            public double NetSpeed { get; }
            public double WorkPerSecond { get; }
        }

        /// <summary>
        /// The adult scaled to <paramref name="fraction"/> of its volume through <c>ScaledFor</c>, set swimming in
        /// still water for <paramref name="seconds"/>. The speed is the centre of mass's, sampled every step, and the
        /// work is the solver's unsigned joint work, which is what the farm hands to the metabolism.
        /// </summary>
        private static Swim SwimAt(bool strength, double fraction, double seconds)
        {
            var config = new RunConfig { JuvenileStrengthScales = strength };
            SolverConfig solver = Still(0.01);

            Phenotype adult = Developer.Develop(Swimmer(), DevelopmentLimits.Default);
            Phenotype body = adult.ScaledFor(config, (float)Math.Pow(fraction, 1d / 3d));

            var creature = new Creature(0, body, solver);
            creature.PlaceAt(new Vec3(0, -10, 0), QuatD.Identity);

            var world = new DynamicsWorld(solver);
            world.Add(creature);

            Vec3 start = creature.CentreOfMass();
            double sum = 0d;
            int steps = (int)Math.Round(seconds / solver.StepSeconds);

            for (int i = 0; i < steps; i++)
            {
                world.Step();
                sum += creature.LinearMomentum().Magnitude / creature.TotalMass;
            }

            double net = (creature.CentreOfMass() - start).Magnitude / seconds;
            return new Swim(creature, sum / steps, net, creature.MechanicalWorkJoules / seconds);
        }

        [Fact]
        public void AJuvenileAtSixtyFivePerCentSwimsInStillWaterWithSixtyFivePerCentOfTheTorqueLimit()
        {
            const double fraction = 0.65;
            const double seconds = 20d;

            Swim off = SwimAt(strength: false, fraction, seconds);
            Swim on = SwimAt(strength: true, fraction, seconds);
            Swim adult = SwimAt(strength: false, 1d, seconds);

            string Row(string name, Swim s) =>
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0,-22} link power {1,7:0.####} N.m   mean speed {2,8:0.#####} m/s   net speed {3,8:0.#####} m/s   stroke work {4,8:0.#####} W",
                    name, s.Body.Power[1], s.MeanSpeed, s.NetSpeed, s.WorkPerSecond);

            _out.WriteLine($"still water, {seconds} world s, body fraction {fraction}");
            _out.WriteLine(Row("adult", adult));
            _out.WriteLine(Row("0.65, switch off", off));
            _out.WriteLine(Row("0.65, switch on", on));

            // The solver's torque limit is Creature.Power, which the drive reads for every link (EffectorDrive.Refresh).
            Assert.True(off.Body.Power[1] > 0d, "the fixture's link has no torque limit");
            Assert.Equal(adult.Body.Power[1], off.Body.Power[1]);

            double ratio = on.Body.Power[1] / off.Body.Power[1];
            _out.WriteLine($"torque limit, on over off: {ratio:0.######}");
            Assert.InRange(ratio, fraction * (1d - 1e-4), fraction * (1d + 1e-4));
        }

        // ---------------------------------------------------------------- resizes on a climbing body

        /// <summary>One resize of the founder: the momentum either side, the mass, the root's move, the divergence count.</summary>
        private sealed class ResizeRecord
        {
            public double WorldSeconds;
            public double MassBefore, MassAfter;
            public Vec3 MomentumBefore, MomentumAfter;
            public Vec3 RootBefore;
            public double RootJump;
            public long DivergedAfter;

            public double MomentumChange => (MomentumAfter - MomentumBefore).Magnitude;
        }

        /// <summary>A jointed body that swims, born at 0.65 of its adult.</summary>
        private static Genome Climber()
        {
            Genome genome = Swimmer(links: 3, power: 15f, hz: 1.2f);

            // Dimensions a part of which weighs more than the newborn floor even at 0.65.
            foreach (MorphNode node in genome.Nodes) node.Dimensions = node.Dimensions * 1.5f;

            genome.Reproduction = new ReproductionTraits
            {
                BroodSize = 1,
                BirthInvestment = 0.65f / (1f - new RunConfig().NewbornReserveFraction),
                ReserveMargin = 0f,
            };

            return genome;
        }

        /// <summary>
        /// One founder in the small farm world at climb time <paramref name="t"/>, stepped for
        /// <paramref name="seconds"/> world seconds, every resize of the founder recorded.
        /// </summary>
        private List<ResizeRecord> Run(float t, double seconds, out long diverged, out double maxJump, out int bodies)
        {
            RunConfig config = SimulationTests.SmallWorld();
            config.MinimumPopulation = 0;
            config.GrowthAdultSeconds = t;
            config.FounderEnergyJoules = 1e6f;

            var records = new List<ResizeRecord>();
            var world = new World(config, 1UL);

            using (var sim = new Simulation(world, 1UL, 0.02f, 25, threads: 2, runDirectory: null))
            {
                long founderId = -1;
                ResizeRecord pending = null;

                sim.BeforeResize = body =>
                {
                    if (body.Id != founderId) return;

                    pending = new ResizeRecord
                    {
                        WorldSeconds = world.ElapsedSeconds,
                        MassBefore = body.TotalMass,
                        MomentumBefore = body.LinearMomentum(),
                        RootBefore = Vec3.Read(body.Position, 0),
                        RootJump = -1d,
                    };
                };

                sim.AfterResize = body =>
                {
                    if (body.Id != founderId || pending == null) return;

                    pending.MassAfter = body.TotalMass;
                    pending.MomentumAfter = body.LinearMomentum();
                    pending.RootJump = (Vec3.Read(body.Position, 0) - pending.RootBefore).Magnitude;
                    pending.DivergedAfter = world.Diverged;
                    records.Add(pending);
                    pending = null;
                };

                int metabolic = 0;
                int guard = 0;

                while (world.ElapsedSeconds < seconds)
                {
                    if (sim.Step())
                    {
                        metabolic++;

                        // After the first economy step, so the world is up before the founder is put in.
                        if (metabolic == 2)
                        {
                            world.Inoculate(Climber(), count: 1, heightY: -10f);
                            Assert.Single(world.Living);
                            founderId = world.Living[0].Id;
                        }
                    }

                    Assert.True(++guard < 400_000, "the world never reached " + seconds + " s");
                }

                diverged = world.Diverged;
                maxJump = sim.MaxResizeJumpMetres;
                bodies = world.Living.Count;
            }

            return records;
        }

        private void Print(string name, List<ResizeRecord> records)
        {
            _out.WriteLine(name + ": " + records.Count + " resizes of the founder");

            foreach (ResizeRecord r in records)
            {
                _out.WriteLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "  t {0,7:0.0} s  mass {1,8:0.####} -> {2,8:0.####} kg  |p| {3,9:0.#####} -> {4,9:0.#####}  |dp| {5,9:0.#####} kg.m/s  root jump {6:0.#########} m  diverged {7}",
                    r.WorldSeconds, r.MassBefore, r.MassAfter,
                    r.MomentumBefore.Magnitude, r.MomentumAfter.Magnitude, r.MomentumChange,
                    r.RootJump, r.DivergedAfter));
            }
        }

        [Fact]
        public void AClimbingBodysResizesAreSmallAndNeverMoveTheRoot()
        {
            const double seconds = 75d;

            List<ResizeRecord> climb = Run(60f, seconds, out long climbDiverged, out double climbJump, out int climbBodies);
            List<ResizeRecord> instant = Run(0f, seconds, out long instantDiverged, out double instantJump, out int instantBodies);

            Print("T = 60 s", climb);
            Print("T = 0", instant);
            _out.WriteLine($"T = 60: diverged {climbDiverged}, MaxResizeJumpMetres {climbJump}, bodies alive at the end {climbBodies}");
            _out.WriteLine($"T = 0: diverged {instantDiverged}, MaxResizeJumpMetres {instantJump}, bodies alive at the end {instantBodies}");

            // Not vacuous: the climb resizes the founder several times and today's growth once.
            Assert.True(climb.Count >= 3, "the climb resized the founder only " + climb.Count + " times in " + seconds + " s");
            Assert.True(instant.Count >= 1, "today's growth never resized the founder");

            // 1. The jump is exactly 0, by construction: Creature.Resize never writes the root.
            Assert.Equal(0d, climbJump);
            Assert.Equal(0d, instantJump);
            foreach (ResizeRecord r in climb) Assert.Equal(0d, r.RootJump);
            foreach (ResizeRecord r in instant) Assert.Equal(0d, r.RootJump);

            // 2. Nothing diverged.
            Assert.Equal(0L, climbDiverged);
            Assert.Equal(0L, instantDiverged);

            // 3. The climb's largest momentum change at one resize is below the single jump of today's growth.
            double largestStep = 0d;
            foreach (ResizeRecord r in climb) largestStep = Math.Max(largestStep, r.MomentumChange);

            double singleJump = instant[0].MomentumChange;
            _out.WriteLine($"largest climb step {largestStep:0.#####} kg.m/s against T = 0's single jump {singleJump:0.#####} kg.m/s");
            Assert.True(
                largestStep < singleJump,
                $"the climb's largest momentum change ({largestStep}) is not below today's single jump ({singleJump})");
        }
    }
}
