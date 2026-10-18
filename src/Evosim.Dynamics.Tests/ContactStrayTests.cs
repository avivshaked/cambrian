using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// The contact grid's reach (<c>logbook/specs/r57-build/build-brief.md</c>): a sphere far
    /// enough out that a cell index would wrap is named by the build, its body handed to the
    /// world's caller and taken out of the step before anything reads the grid, the same at any
    /// thread count, and the run goes on. Round 56 seed 2 crashed in the grid on such a body
    /// (body 86464 at 28,179.5 s, 2026-10-05).
    /// </summary>
    public sealed class ContactStrayTests
    {
        private readonly ITestOutputHelper _out;

        public ContactStrayTests(ITestOutputHelper output) => _out = output;

        private sealed class Outcome
        {
            public ulong Digest;
            public readonly List<string> Told = new List<string>();
            public int Lost;
        }

        /// <summary>
        /// 200 packed bodies, stepped 20 times; then two of them stray, by <paramref name="stray"/>;
        /// then one step, which must tell of both and lose both, and 100 more.
        /// </summary>
        private static Outcome Run(int threads, bool perPart, Action<Creature> stray)
        {
            var config = new SolverConfig
            {
                StepSeconds = 0.01,
                Density = 1000,
                DragCoefficient = 1.5,
                PanelsPerAxis = 2,
                AddedMassCoefficient = 0.5,
                FluidAccelerationCoefficient = 1,
                TissueExcessDensity = 0.02,
                NeutralBodyVolume = 0.25,
                SurfaceRestoringFraction = 1,
                WorldDepthMetres = 15,
                TankRadiusMetres = 0,
                CreatureContact = true,
                ContactPerPart = perPart,
                FloorIsSolid = true,
            };

            var world = new DynamicsWorld(config) { Threads = threads };
            var outcome = new Outcome();
            world.StrayHandler = (body, s) => outcome.Told.Add(
                FormattableString.Invariant($"body {body.Id} link {s.Link} at x {s.X:R} r {s.Radius:R}"));

            var rng = new Rng(20261006);

            for (int i = 0; i < 200; i++)
            {
                int links = 2 + (i % 3);
                JointType joint = (i % 2) == 0 ? JointType.Hinge : JointType.Universal;
                Creature body = TestBodies.Build(TestBodies.Chain(links, joint, power: 8f), config, i);

                body.PlaceAt(
                    new Vec3((rng.NextFloat() - 0.5) * 6.0, -3.0 - rng.NextFloat() * 4.0, (rng.NextFloat() - 0.5) * 6.0),
                    QuatD.Identity);

                world.Add(body);
            }

            for (int step = 0; step < 20; step++) world.Step();

            // Two bodies far apart in the list, so at four threads they land in different slabs.
            stray(world.Creatures[37]);
            stray(world.Creatures[163]);

            world.Step();

            Assert.False(world.Creatures[37].Alive, "the first stray body was stepped on");
            Assert.False(world.Creatures[163].Alive, "the second stray body was stepped on");
            Assert.False(world.Creatures[37].ContactActive);
            Assert.False(world.Creatures[163].ContactActive);

            for (int step = 0; step < 100; step++) world.Step();

            outcome.Lost = world.NonFiniteBodies();
            outcome.Digest = world.Digest();
            return outcome;
        }

        private void AtOneAndFourThreads(bool perPart, Action<Creature> stray, string expectFirst)
        {
            Outcome one = Run(1, perPart, stray);
            Outcome four = Run(4, perPart, stray);

            foreach (string line in one.Told) _out.WriteLine(line);
            _out.WriteLine($"lost {one.Lost}, digest {one.Digest:x16} at 1 thread, {four.Digest:x16} at 4");

            Assert.Equal(2, one.Told.Count);
            Assert.StartsWith(expectFirst, one.Told[0]);
            Assert.StartsWith("body 163 ", one.Told[1]);
            Assert.Equal(one.Told, four.Told);
            Assert.Equal(one.Digest, four.Digest);

            // The two strays and nobody else: the crowd went on being stepped.
            Assert.Equal(2, one.Lost);
            Assert.Equal(2, four.Lost);
        }

        [Fact]
        public void ALinkPlacedAt1e12MetresIsLostAtThatStepAndTheRunGoesOn()
        {
            // Per-part contact, round 56's: one link of the body thrown 1e12 m along x.
            AtOneAndFourThreads(perPart: true, body =>
            {
                body.Position[3] = 1e12;
                body.RefreshContactSphere();
                body.CommitContactSphere();
            }, "body 37 link 1 at x 1000000000000 ");
        }

        [Fact]
        public void ABodyPlacedAt1e12MetresIsLostUnderTheBodySphereToo()
        {
            AtOneAndFourThreads(perPart: false, body =>
                body.PlaceAt(new Vec3(1e12, -5.0, 0.0), QuatD.Identity),
                "body 37 link -1 at x 1000000000000 ");
        }

        [Fact]
        public void ANaNSphereIsLostRatherThanPassedAndLeavesTheCellAlone()
        {
            // A NaN centre passed the grid before without crashing; now it is a stray, and it is
            // left out of the cell's mean so that the crowd's grid is the grid it would have been.
            AtOneAndFourThreads(perPart: true, body =>
            {
                body.Position[3] = double.NaN;
                body.RefreshContactSphere();
                body.CommitContactSphere();
            }, "body 37 link 1 at x NaN ");
        }

        [Fact]
        public void TheReachTestCatchesFarCentresHugeRadiiNaNAndInfinities()
        {
            Assert.False(ContactGrid.OutOfReach(10, -5, 10, 1, 1));
            Assert.False(ContactGrid.OutOfReach(-9.9e8, 0, 0, 1, 1));
            Assert.True(ContactGrid.OutOfReach(1e12, 0, 0, 1, 1));
            Assert.True(ContactGrid.OutOfReach(0, -1e12, 0, 1, 1));
            Assert.True(ContactGrid.OutOfReach(0, 0, 0, 2e9, 1));
            Assert.True(ContactGrid.OutOfReach(double.NaN, 0, 0, 1, 1));
            Assert.True(ContactGrid.OutOfReach(0, 0, 0, double.NaN, 1));
            Assert.True(ContactGrid.OutOfReach(0, double.PositiveInfinity, 0, 1, 1));
            Assert.True(ContactGrid.OutOfReach(0, 0, 0, double.PositiveInfinity, 1));
        }
    }
}
