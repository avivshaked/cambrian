using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// D162's grip in the solver (round 59, step 4): forming on the touch of the rock, holding,
    /// breaking, the decay and the reserve rule, the price, and the solver's stability under it.
    /// </summary>
    public sealed class GripTests
    {
        private readonly ITestOutputHelper _out;

        public GripTests(ITestOutputHelper output) => _out = output;

        private static readonly CellTypeRegistry Table =
            CellTypeRegistry.Standard.Appending(new AnchoringCell());

        private static SolverConfig StillWorld(double dt = 0.01, AnchoringCell cell = null, double tank = 0)
        {
            SolverConfig config = TestBodies.Water(dt);
            config.WorldDepthMetres = 12;
            config.FloorIsSolid = true;
            config.CreatureContact = true;
            config.ContactPerPart = true;
            config.TankRadiusMetres = tank;
            config.TissueExcessDensity = 50;
            config.Anchoring = cell ?? new AnchoringCell();
            return config;
        }

        private static Genome Anchored(Float3 half, string cellType = CellTypeIds.Anchoring)
        {
            Genome g = TestBodies.Box(half);
            g.Nodes[0].CellTypeId = cellType;
            return g;
        }

        private static Creature Build(Genome g, SolverConfig config, int id = 0) =>
            new Creature(id, Developer.Develop(g, DevelopmentLimits.Default, cellTypes: Table), config);

        /// <summary>Put down with its sphere 1 cm into the flat bed.</summary>
        private static Creature OnTheBed(Genome g, SolverConfig config, double x = 0, double z = 0, int id = 0)
        {
            Creature body = Build(g, config, id);
            body.PlaceAt(new Vec3(x, 0, z), QuatD.Identity);
            body.RefreshContactSphere();
            double radius = body.LinkContactRadius[0];
            body.PlaceAt(new Vec3(x, -config.WorldDepthMetres + radius - 0.01, z), QuatD.Identity);
            body.RefreshContactSphere();
            body.CommitContactSphere();
            return body;
        }

        private sealed class Reserve : IReserveSource
        {
            public float SecondsOfReserve { get; set; } = float.PositiveInfinity;
        }

        // ------------------------------------------------------------------ forming and paying

        [Fact]
        public void ALinkGripsOnItsFirstTouchAndPaysOnceUntilTheGripBreaks()
        {
            SolverConfig config = StillWorld();
            Creature body = OnTheBed(Anchored(new Float3(0.05f, 0.05f, 0.05f)), config);
            var world = new DynamicsWorld(config);
            world.Add(body);

            AnchoringCell cell = config.Anchoring;
            double price = cell.BuildPrice(cell.BreakingForce((float)body.Volume[0]));

            world.Step();
            Assert.True(body.GripHeld[0], "the link touched the bed and did not grip");
            for (int i = 0; i < 300; i++) world.Step();

            Assert.True(body.GripHeld[0]);
            Assert.Equal(1L, body.GripsFormed);
            Assert.Equal(price, body.GripOwedJoules, 15);
            Assert.Equal(price, body.DrainGripOwed(), 15);
            Assert.Equal(0d, body.GripOwedJoules);

            // A break, and a grip formed again on the next step, at the full price again (D165).
            Vec3.Write(body.GripAnchor, 0, Vec3.Read(body.GripAnchor, 0) + new Vec3(0, 0, 10));
            world.Step();
            Assert.Equal(1L, body.GripsBroken);
            Assert.False(body.GripHeld[0]);
            world.Step();
            Assert.True(body.GripHeld[0]);
            Assert.Equal(2L, body.GripsFormed);
            Assert.Equal(price, body.DrainGripOwed(), 15);

            _out.WriteLine($"a 10 cm anchoring cube: breaking force {cell.BreakingForce((float)body.Volume[0]):0.#} N, price {price:0.#######} J a grip");
        }

        [Fact]
        public void NothingGripsInMidWaterAtTheGlassOrWithoutTheCell()
        {
            SolverConfig config = StillWorld(tank: 6);
            config.TissueExcessDensity = 0;

            // Mid-water.
            Creature floating = Build(Anchored(new Float3(0.05f, 0.05f, 0.05f)), config);
            floating.PlaceAt(new Vec3(config.TankAxisX, -6, config.TankAxisZ), QuatD.Identity);

            // Pressed 1 cm into the glass at mid-depth.
            Creature atGlass = Build(Anchored(new Float3(0.05f, 0.05f, 0.05f)), config, 1);
            double radius = atGlass.LinkContactRadius[0];
            atGlass.PlaceAt(new Vec3(config.TankAxisX + 6 - radius + 0.01, -6, config.TankAxisZ), QuatD.Identity);

            // On the bed, of structural tissue.
            Creature plain = OnTheBed(Anchored(new Float3(0.05f, 0.05f, 0.05f), CellTypeIds.Structural), config,
                config.TankAxisX - 2, config.TankAxisZ, 2);

            var world = new DynamicsWorld(config);
            world.Add(floating);
            world.Add(atGlass);
            world.Add(plain);
            for (int i = 0; i < 5; i++) world.Step();

            Assert.False(floating.GripHeld[0]);
            Assert.False(atGlass.GripHeld[0], "the glass was gripped");
            Assert.Null(plain.GripHeld);
            Assert.Equal(0L, floating.GripsFormed + atGlass.GripsFormed);
        }

        [Fact]
        public void AWorldWithoutTheCellOrWithoutAnAnchoringPartStepsAsBefore()
        {
            ulong Run(bool withCell)
            {
                SolverConfig config = StillWorld();
                config.Anchoring = withCell ? new AnchoringCell() : null;
                var world = new DynamicsWorld(config);
                for (int i = 0; i < 6; i++)
                {
                    Creature body = new Creature(i, Developer.Develop(TestBodies.Chain(2 + i % 2), DevelopmentLimits.Default), config);
                    body.PlaceAt(new Vec3(-1.5 + 0.6 * i, -11.0 + 0.2 * i, 0.4 * (i % 3)), QuatD.Identity);
                    world.Add(body);
                }

                for (int s = 0; s < 400; s++) world.Step();
                return world.Digest();
            }

            Assert.Equal(Run(false), Run(true));
        }

        // ------------------------------------------------------------------ breaking

        /// <summary>
        /// The break, read exactly: a held link displaced by <c>d</c> from its place demands
        /// <c>m ω² d</c>, and lets go just above the breaking force and holds just below it, at
        /// two cell sizes whose forces stand as their volumes to the 2/3.
        /// </summary>
        [Theory]
        [InlineData(0.05f)]
        [InlineData(0.1f)]
        public void TheGripBreaksJustAboveItsForceAndHoldsJustBelow(float half)
        {
            AnchoringCell cell = new AnchoringCell(tenacityNewtonsPerSquareMetre: 200f);
            SolverConfig config = StillWorld(cell: cell);
            config.TissueExcessDensity = 0;

            foreach (double share in new[] { 0.99, 1.01 })
            {
                Creature body = OnTheBed(Anchored(new Float3(half, half, half)), config);
                var world = new DynamicsWorld(config);
                world.Add(body);
                world.Step();
                Assert.True(body.GripHeld[0]);

                // Still, and pulled from the side by a displaced anchor alone.
                Array.Clear(body.Velocity, 0, body.Velocity.Length);
                Array.Clear(body.Spin, 0, body.Spin.Length);
                double stiffness = body.Mass[0] * config.ContactOmega * config.ContactOmega;
                double breaking = cell.BreakingForce((float)body.Volume[0]) * body.GripStrength[0];
                double d = share * breaking / stiffness;
                Vec3 at = Vec3.Read(body.Position, 0);
                Vec3.Write(body.GripAnchor, 0, at + new Vec3(d, 0, 0));
                body.RockTouched[0] = false;

                Array.Clear(body.Fext, 0, body.Fext.Length);
                Anchor.Apply(body, config, config.StepSeconds);

                _out.WriteLine($"half {half} m: breaking {breaking:0.###} N, displaced {d * 1000:0.###} mm at {share} of it: " +
                               (body.GripHeld[0] ? "held" : "broke"));
                Assert.Equal(share < 1, body.GripHeld[0]);
            }
        }

        // ------------------------------------------------------------------ weakening

        [Fact]
        public void TheStrengthDecaysOnItsTimeAndTheReserveRuleWeakensAndReleases()
        {
            var cell = new AnchoringCell(decaySeconds: 10f, reserveThresholdSeconds: 100f, reserveLagSeconds: 1f);
            SolverConfig config = StillWorld(cell: cell);
            Creature body = OnTheBed(Anchored(new Float3(0.05f, 0.05f, 0.05f)), config);
            var reserve = new Reserve { SecondsOfReserve = 1000f };
            body.Senses.Reserve = reserve;

            var world = new DynamicsWorld(config);
            world.Add(body);
            world.Step();
            for (int i = 0; i < 499; i++) world.Step();

            // 500 steps held, of which the decay applied on each: exp(-5 / 10).
            Assert.True(body.GripHeld[0]);
            Assert.Equal(Math.Exp(-500 * 0.01 / 10.0), body.GripStrength[0], 9);

            // Fed: the factor is 1. Below the threshold within the lag: still 1. Past it: the share.
            Assert.Equal(1.0, Anchor.ReserveFactor(body, cell, 0.01));
            reserve.SecondsOfReserve = 40f;
            body.ReserveLowSeconds = 0;
            Assert.Equal(1.0, Anchor.ReserveFactor(body, cell, 0.5));
            Assert.Equal(1.0, Anchor.ReserveFactor(body, cell, 0.5));
            Assert.Equal(0.4, Anchor.ReserveFactor(body, cell, 0.5), 6);
            reserve.SecondsOfReserve = 500f;
            Assert.Equal(1.0, Anchor.ReserveFactor(body, cell, 0.01));
            Assert.Equal(0d, body.ReserveLowSeconds);

            // An empty reserve lets go at once, lag or none, and nothing grips again while it is.
            reserve.SecondsOfReserve = 0f;
            world.Step();
            Assert.False(body.GripHeld[0]);
            long formed = body.GripsFormed;
            for (int i = 0; i < 10; i++) world.Step();
            Assert.Equal(formed, body.GripsFormed);
        }

        // ------------------------------------------------------------------ holding in a current

        [Fact]
        public void AGrippingLinkHoldsInACurrentThatCarriesItsTwinAway()
        {
            SolverConfig config = R42Tank.Config();
            config.CreatureContact = true;
            config.ContactPerPart = true;
            config.Anchoring = new AnchoringCell();

            Creature held = Build(Anchored(new Float3(0.1f, 0.05f, 0.1f)), config, 0);
            Creature free = Build(Anchored(new Float3(0.1f, 0.05f, 0.1f), CellTypeIds.Structural), config, 1);
            double radius = held.LinkContactRadius[0];
            double y = -R42Tank.DepthMetres + radius - 0.01;
            held.PlaceAt(R42Tank.At(-2, 0, 0) + new Vec3(0, y, 0), QuatD.Identity);
            free.PlaceAt(R42Tank.At(2, 0, 0) + new Vec3(0, y, 0), QuatD.Identity);

            var world = new DynamicsWorld(config);
            world.Add(held);
            world.Add(free);

            Vec3 heldStart = Vec3.Read(held.Position, 0), freeStart = Vec3.Read(free.Position, 0);
            double heldMost = 0;
            for (int s = 0; s < 6000; s++)
            {
                world.Step();
                if (s > 10) heldMost = Math.Max(heldMost, (Vec3.Read(held.Position, 0) - heldStart).Magnitude);
            }

            double freeMoved = (Vec3.Read(free.Position, 0) - freeStart).Magnitude;
            _out.WriteLine($"60 s in round 42's water: the held body moved at most {heldMost * 1000:0.##} mm, its twin {freeMoved:0.###} m");

            Assert.True(held.GripHeld[0], "the grip did not hold");
            Assert.True(heldMost < 0.01, "the held body drifted");
            Assert.True(freeMoved > 10 * heldMost, "the water did not carry the twin away, so nothing was tested");
        }

        // ------------------------------------------------------------------ stability

        /// <summary>
        /// The design's first doubt: a held large body and a held long arm, displaced from their
        /// places, ring down rather than up, at both steps the farm uses.
        /// </summary>
        [Theory]
        [InlineData(0.01)]
        [InlineData(0.02)]
        public void AHeldLargeBodyAndALongArmRingDown(double dt)
        {
            foreach (bool arm in new[] { false, true })
            {
                SolverConfig config = StillWorld(dt);
                Genome g = arm
                    ? TestBodies.Chain(6, power: 1e-6f, rootHalf: new Float3(0.1f, 0.1f, 0.1f), linkHalf: new Float3(0.25f, 0.04f, 0.04f))
                    : Anchored(new Float3(0.5f, 0.5f, 0.5f));
                if (arm) g.Nodes[0].CellTypeId = CellTypeIds.Anchoring;

                Creature body = OnTheBed(g, config);
                var world = new DynamicsWorld(config);
                world.Add(body);
                // Twenty seconds to settle on the bed under its own weight before the knock, so the
                // energy read after it is the grip's ringing and not the arm still falling.
                for (int i = 0; i < (int)Math.Round(20 / dt); i++) world.Step();
                Assert.True(body.GripHeld[0]);

                // Knocked 2 cm off its place, and left.
                Vec3.Write(body.GripAnchor, 0, Vec3.Read(body.GripAnchor, 0) + new Vec3(0.02, 0, 0.0));

                int steps = (int)Math.Round(1.0 / dt);
                double first = 0, last = 0, previous = double.PositiveInfinity;
                bool grew = false;
                var series = new List<double>();
                for (int second = 0; second < 20; second++)
                {
                    double peak = 0;
                    for (int s = 0; s < steps; s++)
                    {
                        world.Step();
                        Assert.True(body.Alive, "the held body diverged");
                        peak = Math.Max(peak, body.KineticEnergy());
                    }

                    if (second == 0) first = peak;
                    if (second >= 2 && peak > 1.05 * previous + 1e-9) grew = true;
                    previous = peak;
                    last = peak;
                    series.Add(peak);
                }

                _out.WriteLine("  peaks by second: " + string.Join(", ", series.ConvertAll(e => e.ToString("0.##e0"))));

                _out.WriteLine($"dt {dt}, {(arm ? "a six-link arm" : "a 1 m cube")}: peak kinetic energy {first:0.###e0} J in the first second, {last:0.###e0} J in the twentieth; held {body.GripHeld[0]}");
                Assert.False(grew, "the held body's energy grew from one second to the next");
                Assert.True(last < 0.01 * first + 1e-9, "the held body did not ring down");
            }
        }

        // ------------------------------------------------------------------ identity and carrying

        [Fact]
        public void TheGripIsTheSameAtAnyThreadCount()
        {
            ulong Run(int threads)
            {
                SolverConfig config = R42Tank.Config();
                config.CreatureContact = true;
                config.ContactPerPart = true;
                config.Anchoring = new AnchoringCell(tenacityNewtonsPerSquareMetre: 50f);

                var world = new DynamicsWorld(config) { Threads = threads };
                for (int i = 0; i < 8; i++)
                {
                    Genome g = TestBodies.Chain(2 + i % 3);
                    g.Nodes[0].CellTypeId = CellTypeIds.Anchoring;
                    Creature body = Build(g, config, i);
                    double radius = body.LinkContactRadius[0];
                    body.PlaceAt(R42Tank.At(-3 + 0.8 * i, -R42Tank.DepthMetres + radius - 0.01 + 0.05 * (i % 2), 0.5 * (i % 3)), QuatD.Identity);
                    world.Add(body);
                }

                long formed = 0;
                for (int s = 0; s < 1500; s++) world.Step();
                foreach (Creature c in world.Creatures) formed += c.GripsFormed;
                _out.WriteLine($"{threads} thread(s): {formed} grips formed");
                return world.Digest();
            }

            Assert.Equal(Run(1), Run(16));
        }

        [Fact]
        public void ARebuiltBodyKeepsItsGripsAndOwesNothingNew()
        {
            SolverConfig config = StillWorld();
            Genome g = Anchored(new Float3(0.05f, 0.05f, 0.05f));
            Creature before = OnTheBed(g, config);
            var world = new DynamicsWorld(config);
            world.Add(before);
            world.Step();
            Assert.True(before.GripHeld[0]);
            double owed = before.GripOwedJoules;

            Creature after = Build(g, config);
            after.AdoptStateFrom(before, new[] { 0 });

            Assert.True(after.GripHeld[0]);
            Assert.Equal(before.GripStrength[0], after.GripStrength[0]);
            Assert.Equal(Vec3.Read(before.GripAnchor, 0), Vec3.Read(after.GripAnchor, 0));
            Assert.Equal(1L, after.GripsFormed);
            Assert.Equal(owed, after.GripOwedJoules);
        }

        [Fact]
        public void AnAnchoringBodyNeedsPerPartContact()
        {
            SolverConfig config = StillWorld();
            config.ContactPerPart = false;
            Assert.Throws<ArgumentException>(() => Build(Anchored(new Float3(0.05f, 0.05f, 0.05f)), config));
        }
    }
}
