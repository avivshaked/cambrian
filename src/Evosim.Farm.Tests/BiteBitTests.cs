using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Evosim.Core;
using Xunit;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// The recorded bite bit: per body per pose frame, a mask of the eating parts that took matter at any metabolic
    /// step since the previous frame. The mouth pass is driven by hand, as <c>RemainsSolverTests</c> drives it: Core
    /// has no physics of its own, so the test hands it the contact list a bite would make, runs its mouth pass, and
    /// gives the recorder the step.
    /// </summary>
    [Collection(FarmProcessCollection.Name)]
    public class BiteBitTests : IDisposable
    {
        private readonly string _root =
            Path.Combine(AppContext.BaseDirectory, "bite-bit-tests", Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_root)) Directory.Delete(_root, true);
            }
            catch (IOException)
            {
            }
        }

        private string StreamPath(string name) => Path.Combine(_root, name, PoseStream.FileName);

        private static RemainsFixtures.Pair Arena() =>
            RemainsFixtures.Arena(RemainsFixtures.Config(true), RemainsFixtures.Hunter(), RemainsFixtures.Fork());

        private static Organism HunterOf(RemainsFixtures.Pair p) => p.World.Living.Single(c => c.HasIntake);

        private static Organism VictimOf(RemainsFixtures.Pair p) => p.World.Living.Single(c => !c.HasIntake);

        /// <summary>One metabolic step's mouth pass in which the hunter's mouth (part 0) bites the victim's part 1.</summary>
        private static void BiteStep(RemainsFixtures.Pair p)
        {
            p.World.SetContacts(new List<CreatureContact>
            {
                new CreatureContact(HunterOf(p).Id, 0, VictimOf(p).Id, 1),
            });
            p.World.ApplyMouth(0.25f);
        }

        /// <summary>One metabolic step's mouth pass with no contact at all.</summary>
        private static void QuietStep(RemainsFixtures.Pair p) => p.World.ApplyMouth(0.25f);

        private static uint MaskIn(PoseFrame frame, long id) => frame.Bodies.Single(b => b.Id == id).AteMask;

        // ------------------------------------------------------------------ Core's record, through the farm's eyes

        [Fact]
        public void TheMaskIsSetAfterAStepWhereTheMouthAteAndClearAfterAStepWhereItDidNot()
        {
            using (RemainsFixtures.Pair p = Arena())
            {
                Organism hunter = HunterOf(p);
                Organism victim = VictimOf(p);

                BiteStep(p);
                Assert.Equal(1u, hunter.TookMatterMask);
                Assert.Equal(0u, victim.TookMatterMask);
                Assert.Single(p.World.TookMatterThisStep);
                Assert.Same(hunter, p.World.TookMatterThisStep[0]);

                QuietStep(p);
                Assert.Equal(0u, hunter.TookMatterMask);
                Assert.Empty(p.World.TookMatterThisStep);
            }
        }

        // ------------------------------------------------------------------ the recorder's OR

        [Fact]
        public void AFrameCarriesTheBitOfAnyStepSinceThePreviousFrameAndTheNextFrameStartsClear()
        {
            using (RemainsFixtures.Pair p = Arena())
            {
                Organism hunter = HunterOf(p);
                Organism victim = VictimOf(p);
                string path = StreamPath("or");

                using (PoseRecorder recorder = PoseRecorder.AtPath(path, 0.5f, "abc"))
                {
                    // Frame 0: the bite is two steps before the frame, with a quiet step between. An OR that kept only
                    // the last step's mask would lose it.
                    BiteStep(p);
                    recorder.Observe(p.Sim);
                    QuietStep(p);
                    recorder.Observe(p.Sim);
                    QuietStep(p);
                    recorder.Observe(p.Sim);
                    recorder.WriteFrame(p.Sim, 1.0);

                    // Frame 1: nothing ate since frame 0.
                    QuietStep(p);
                    recorder.Observe(p.Sim);
                    recorder.WriteFrame(p.Sim, 2.0);

                    // Frame 2: the bite is on the very step before the frame, and observed twice (harmless).
                    BiteStep(p);
                    recorder.Observe(p.Sim);
                    recorder.Observe(p.Sim);
                    recorder.WriteFrame(p.Sim, 3.0);
                }

                using (PoseStreamReader reader = PoseStreamReader.Open(path))
                {
                    Assert.Equal(PoseStream.Version, reader.Header.Version);
                    Assert.Equal(3, reader.Frames.Length);

                    Assert.Equal(1u, MaskIn(reader.Read(0), hunter.Id));
                    Assert.Equal(0u, MaskIn(reader.Read(0), victim.Id));
                    Assert.Equal(0u, MaskIn(reader.Read(1), hunter.Id));
                    Assert.Equal(1u, MaskIn(reader.Read(2), hunter.Id));
                    Assert.Equal(0u, MaskIn(reader.Read(2), victim.Id));
                }
            }
        }

        [Fact]
        public void SampleObservesTheStepItIsCalledAfter()
        {
            using (RemainsFixtures.Pair p = Arena())
            {
                Organism hunter = HunterOf(p);
                string path = StreamPath("sample");

                using (PoseRecorder recorder = PoseRecorder.AtPath(path, 0.5f, "abc"))
                {
                    // Sample is what the run's loop calls after every metabolic step. The first call writes the frame
                    // at the world's clock (0 s), so it must already have seen this step's bite.
                    BiteStep(p);
                    recorder.Sample(p.Sim);
                }

                using (PoseStreamReader reader = PoseStreamReader.Open(path))
                {
                    Assert.Equal(1, reader.Frames.Length);
                    Assert.Equal(1u, MaskIn(reader.Read(0), hunter.Id));
                }
            }
        }

        // ------------------------------------------------------------------ the record moves nothing

        /// <summary>
        /// The world and the harness after the same scripted bites and the same physics steps, with the recorder
        /// observing every metabolic step and writing frames, or absent.
        /// </summary>
        private byte[] Trajectory(bool recording)
        {
            using (RemainsFixtures.Pair p = Arena())
            {
                PoseRecorder recorder = recording ? PoseRecorder.AtPath(StreamPath("traj-" + Guid.NewGuid().ToString("N")), 0.5f, "abc") : null;

                try
                {
                    for (int i = 0; i < 6; i++)
                    {
                        if (i % 2 == 0) BiteStep(p);
                        else QuietStep(p);

                        if (recorder != null)
                        {
                            recorder.Observe(p.Sim);
                            recorder.WriteFrame(p.Sim, 0.5 * (i + 1));
                        }
                    }

                    for (int i = 0; i < 25 * 40; i++)
                    {
                        if (!p.Sim.Step()) continue;

                        if (recorder != null)
                        {
                            recorder.Sample(p.Sim);
                        }
                    }
                }
                finally
                {
                    recorder?.Dispose();
                }

                p.Sim.SettleBeforeCheckpoint();

                using (var buffer = new MemoryStream())
                {
                    using (var w = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
                    {
                        p.World.WriteState(w);
                        p.Sim.WriteState(w);
                        w.Flush();
                    }

                    return buffer.ToArray();
                }
            }
        }

        [Fact]
        public void ARunWithTheRecorderObservingIsTheSameRunAsOneWithout()
        {
            byte[] without = Trajectory(recording: false);
            byte[] with = Trajectory(recording: true);

            Assert.Equal(without.Length, with.Length);
            Assert.True(without.SequenceEqual(with), "recording the bite bit moved the world or the harness");
        }
    }
}
