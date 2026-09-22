using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Evosim.Core;
using Evosim.Dynamics;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// Dynamics' half of a checkpoint: bodies written to bytes and read back into bodies built
    /// from the same phenotypes carry on from the same instant, to the last bit
    /// (<c>logbook/specs/checkpoint-spec.md</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The assertion is the solver's own digest, which is what every determinism test here uses:
    /// a hash over every link's place, attitude, spin and velocity. Two worlds whose digests agree
    /// after a thousand steps agree about every number the solver integrates, and a state that
    /// forgot a velocity or a joint rate parts within one step of the restore.
    /// </para>
    /// <para>
    /// Water and a driven joint are both in it deliberately. A body in a vacuum with no muscle has
    /// almost no cross-step state beyond its pose, and would pass with half the fields missing; a
    /// driven body in water carries a brain's recurrent state, a drive's torque history and a held
    /// water sample as well.
    /// </para>
    /// </remarks>
    public class CreatureStateTests
    {
        private readonly ITestOutputHelper _output;

        public CreatureStateTests(ITestOutputHelper output) => _output = output;

        private static Genome Swimmer() => TestBodies.Chain(4, JointType.Hinge, power: 20f);

        private static DynamicsWorld Built(SolverConfig config, out Creature body)
        {
            var world = new DynamicsWorld(config);
            body = world.Add(TestBodies.Build(Swimmer(), config, id: 7));
            return world;
        }

        private static byte[] StateOf(DynamicsWorld world, Creature body)
        {
            using (var buffer = new MemoryStream())
            {
                using (var w = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
                {
                    world.WriteState(w);
                    body.WriteState(w);
                    w.Flush();
                }

                return buffer.ToArray();
            }
        }

        private static void Restore(DynamicsWorld world, Creature body, byte[] state)
        {
            using (var buffer = new MemoryStream(state, writable: false))
            using (var r = new BinaryReader(buffer, Encoding.UTF8))
            {
                world.ReadState(r);
                body.ReadState(r);

                Assert.Equal(state.Length, buffer.Position);
            }
        }

        // ------------------------------------------------------------------ the round trip

        [Fact]
        public void ARestoredBodySwimsOnIdentically()
        {
            SolverConfig config = TestBodies.Water(0.01);

            DynamicsWorld first = Built(config, out Creature moving);
            for (int step = 0; step < 500; step++) first.Step();

            byte[] state = StateOf(first, moving);

            // A world and a body of the same shape and nothing else: built from the same genome,
            // never stepped, standing where the builder put them.
            DynamicsWorld second = Built(config, out Creature restored);
            Assert.NotEqual(first.Digest(), second.Digest());

            Restore(second, restored, state);

            Assert.Equal(first.Digest(), second.Digest());
            Assert.Equal(first.Steps, second.Steps);
            Assert.Equal(first.ElapsedSeconds, second.ElapsedSeconds);

            for (int step = 0; step < 1000; step++)
            {
                first.Step();
                second.Step();
            }

            ulong a = first.Digest();
            ulong b = second.Digest();

            _output.WriteLine($"after the restore and 1,000 steps: {a:x16} against {b:x16}");

            Assert.Equal(a, b);
        }

        [Fact]
        public void ARestoredBodyWritesTheSameStateAgain()
        {
            SolverConfig config = TestBodies.Water(0.01);

            DynamicsWorld first = Built(config, out Creature moving);
            for (int step = 0; step < 500; step++) first.Step();

            byte[] state = StateOf(first, moving);

            DynamicsWorld second = Built(config, out Creature restored);
            Restore(second, restored, state);

            Assert.Equal(state, StateOf(second, restored));
        }

        [Fact]
        public void TheBrainsMemoryIsCarried()
        {
            SolverConfig config = TestBodies.Water(0.01);

            DynamicsWorld first = Built(config, out Creature moving);
            for (int step = 0; step < 500; step++) first.Step();

            DynamicsWorld second = Built(config, out Creature restored);
            Restore(second, restored, StateOf(first, moving));

            // A recurrent neuron's output depends on what it output last, so a brain restored
            // without its memory drives the same body differently from the first step. This is the
            // one piece of cross-step state that is neither a pose nor a velocity, and a digest
            // taken immediately after a restore cannot see it: only stepping can.
            Assert.NotEmpty(Neurons(moving));

            for (int step = 0; step < 50; step++)
            {
                first.Step();
                second.Step();
            }

            List<float> expected = Neurons(moving);
            List<float> actual = Neurons(restored);

            Assert.Equal(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++) Assert.Equal(expected[i], actual[i]);
        }

        /// <summary>Every neuron's last output, part by part, in a fixed order.</summary>
        private static List<float> Neurons(Creature body)
        {
            var outputs = new List<float>();

            for (int part = 0; part < body.Phenotype.Parts.Count; part++)
            {
                for (int neuron = 0; neuron < 16; neuron++)
                {
                    outputs.Add(body.Brain.Output(part, neuron));
                }
            }

            // Only the ones that exist read anything but zero, and a brain with nothing awake in
            // it would make this test pass for the wrong reason.
            outputs.RemoveAll(value => value == 0f);

            return outputs;
        }

        // ------------------------------------------------------------------ the refusals

        [Fact]
        public void ABodyOfAnotherShapeIsRefused()
        {
            SolverConfig config = TestBodies.Water(0.01);

            DynamicsWorld first = Built(config, out Creature moving);
            for (int step = 0; step < 50; step++) first.Step();

            byte[] state = StateOf(first, moving);

            // Same id, a different number of links: the state's own guard, because a body read
            // into the wrong shape would take joint coordinates from whichever links happened to
            // line up and look like a plausible creature.
            var other = new DynamicsWorld(config);
            Creature wrong = other.Add(
                TestBodies.Build(TestBodies.Chain(2, JointType.Hinge, power: 20f), config, id: 7));

            using (var buffer = new MemoryStream(state, writable: false))
            using (var r = new BinaryReader(buffer, Encoding.UTF8))
            {
                other.ReadState(r);
                Assert.Throws<InvalidDataException>(() => wrong.ReadState(r));
            }
        }

        [Fact]
        public void ASectionTagThatDoesNotMatchIsRefused()
        {
            SolverConfig config = TestBodies.Water(0.01);

            DynamicsWorld first = Built(config, out Creature moving);
            for (int step = 0; step < 50; step++) first.Step();

            byte[] state = StateOf(first, moving);
            state[0] = (byte)'X';

            DynamicsWorld second = Built(config, out Creature restored);

            using (var buffer = new MemoryStream(state, writable: false))
            using (var r = new BinaryReader(buffer, Encoding.UTF8))
            {
                Assert.Throws<InvalidDataException>(() => second.ReadState(r));
            }
        }
    }
}
