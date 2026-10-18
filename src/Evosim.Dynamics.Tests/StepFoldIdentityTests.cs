using System.IO;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// The step after its two folds (the contact commit into the next grid build, and the water
    /// sample into each body's own step) against the step before them, to the bit: a word pinned
    /// on the build before the folds, at 1 and at 4 threads, under both contact models.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What the word covers.</b> The trajectory's digest after every step; the five census
    /// totals; and, half way and at the end, every body's checkpoint record
    /// (<see cref="Creature.WriteState"/>, which holds the committed contact sphere, its pending
    /// twin and the water arrays) with the forces, the torques and the overlap list beside it. So
    /// a commit that reached the grid a step late, a reader that saw a sphere before it was
    /// committed, or a water sample taken at another pose or another clock all move it.
    /// </para>
    /// <para>
    /// <b>The world is built to touch everything the folds touch</b>: moving water sampled per
    /// link with its acceleration felt, a shaped bed with a third of the bodies placed into it, the
    /// glass, and the rest packed close enough to overlap. The checkpoint record half way also
    /// forces the commit early on the folded build, which must change nothing that follows.
    /// </para>
    /// <para>
    /// <b>The words were taken on <c>61e3bb1</c></b>, the commit the folds were built on, by this
    /// test with the words left blank. A later change that moves the trajectory re-pins them and
    /// says why in its commit.
    /// </para>
    /// </remarks>
    public sealed class StepFoldIdentityTests
    {
        private const int Bodies = 150;
        private const int Steps = 400;

        private readonly ITestOutputHelper _out;

        public StepFoldIdentityTests(ITestOutputHelper output) => _out = output;

        [Theory]
        [InlineData(false, 0xbfd2d051846472c8UL)]
        [InlineData(true, 0xad4e8a1c3066ee1dUL)]
        public void TheFoldedStepIsTheRecordedStep(bool perPart, ulong pinned)
        {
            ulong one = Run(perPart, 1, out long pairs, out long bedOrGlass, out int lost);
            ulong four = Run(perPart, 4, out _, out _, out _);

            _out.WriteLine($"per part {perPart}");
            _out.WriteLine($"word 1 thread  {one:x16}");
            _out.WriteLine($"word 4 threads {four:x16}");
            _out.WriteLine($"overlapping pairs summed {pairs}; bodies against the bed or glass summed {bedOrGlass}; lost {lost}");

            Assert.Equal(one, four);
            Assert.Equal(pinned, one);

            // A word is equal for a world where nothing touched, too.
            Assert.True(pairs > 0, "no two bodies overlapped");
            Assert.True(bedOrGlass > 0, "no body touched the bed or the glass");
            Assert.Equal(0, lost);
        }

        private static ulong Run(bool perPart, int threads, out long pairs, out long bedOrGlass, out int lost)
        {
            SolverConfig config = R42Tank.Config(0.01, waterHoldSeconds: 0.0);
            config.CreatureContact = true;
            config.ContactPerPart = perPart;
            config.ContactInstrument = true;
            config.ContactEvents = true;
            config.TankRadiusMetres = R42Tank.Radius;
            config.Bed = new BedShape(
                R42Tank.Radius, R42Tank.DepthMetres,
                reliefMetres: 1.5f, tiltMetres: 30f, scaleMetres: 0f,
                seed: Rng.SeedFor(R42Tank.Seed, World.BedShapeIndex));

            var world = new DynamicsWorld(config) { Threads = threads };
            var rng = new Rng(20261003);

            for (int i = 0; i < Bodies; i++)
            {
                int links = 2 + (i % 3);
                JointType joint = (i % 3) == 0 ? JointType.Hinge
                    : (i % 3) == 1 ? JointType.Universal
                    : JointType.Spherical;

                Genome genome = TestBodies.Chain(
                    links, joint, power: 8f + i % 5, hz: 0.5f + 0.1f * (i % 7));

                Creature body = TestBodies.Build(genome, config, i);

                double x = (rng.NextFloat() - 0.5) * 5.0;
                double z = (rng.NextFloat() - 0.5) * 5.0;

                // A third on the floor, a few centimetres into it; the rest packed in mid-water.
                double y = i % 3 == 0
                    ? config.Bed.FloorY(R42Tank.Radius + x, R42Tank.Radius + z) + 0.15
                    : -20.0 - rng.NextFloat() * 2.5;

                body.PlaceAt(
                    R42Tank.At(x, y, z),
                    QuatD.FromAxisAngle(
                        new Vec3(rng.NextFloat() - 0.5, rng.NextFloat() - 0.5, rng.NextFloat() - 0.5),
                        rng.NextFloat() * 6.28));

                body.Patch = i % config.PatchCount;

                world.AddInIdOrder(body);
            }

            ulong word = 14695981039346656037UL;

            for (int step = 1; step <= Steps; step++)
            {
                world.Step();
                Mix(ref word, world.Digest());

                if (step == Steps / 2 || step == Steps) Mix(ref word, Records(world));
            }

            Mix(ref word, (ulong)world.OverlapPairs);
            Mix(ref word, (ulong)world.OverlapPairsJointed);
            Mix(ref word, (ulong)world.OverlapPairsHeld);
            Mix(ref word, (ulong)world.OverlapBodies);
            Mix(ref word, (ulong)world.BedOrGlassBodies);

            pairs = world.OverlapPairs;
            bedOrGlass = world.BedOrGlassBodies;
            lost = world.NonFiniteBodies();
            return word;
        }

        /// <summary>Every body's checkpoint record, forces, torques and overlap list, hashed.</summary>
        private static ulong Records(DynamicsWorld world)
        {
            using var stream = new MemoryStream();
            using (var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                foreach (Creature c in world.Creatures)
                {
                    // In the layout the word was pinned on: round 60's card block, which a body the CPU
                    // engine stepped writes empty, is not the step's and is left out.
                    c.WriteState(w, cardBlock: false);
                    foreach (double v in c.Fext) w.Write(v);
                    foreach (double v in c.Tau) w.Write(v);
                    w.Write(c.TouchedBedOrGlass);
                    w.Write(c.OverlapCount);
                    for (int k = 0; k < c.OverlapCount; k++) w.Write(c.OverlapId(k));
                }
            }

            ulong hash = 14695981039346656037UL;
            foreach (byte b in stream.ToArray())
            {
                hash ^= b;
                hash *= 1099511628211UL;
            }

            return hash;
        }

        private static void Mix(ref ulong hash, ulong value)
        {
            for (int b = 0; b < 8; b++)
            {
                hash ^= (value >> (8 * b)) & 0xFF;
                hash *= 1099511628211UL;
            }
        }
    }
}
