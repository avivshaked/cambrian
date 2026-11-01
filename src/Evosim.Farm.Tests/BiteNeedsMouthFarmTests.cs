using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Evosim.Core;
using Evosim.Dynamics;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// D275 on the farm: the switch and its header, a bar held side-on and tip-on to a plate through the solver's own
    /// census, the switch-off identity against the tree before the build, a resume, and the host's cost of the measure.
    /// </summary>
    [Collection(FarmProcessCollection.Name)]
    public class BiteNeedsMouthFarmTests
    {
        private readonly ITestOutputHelper _out;

        public BiteNeedsMouthFarmTests(ITestOutputHelper output) => _out = output;

        /// <summary>
        /// <see cref="TrueShapeBiteScenario.Digest"/> with the switch off, taken on the tree before this build: <c>main</c>
        /// at <c>e3a7d101</c>, exported with <c>git archive</c> to a folder of its own, where
        /// <c>TrueShapeBiteScenario.cs</c> and a test that printed its digest were built and run. This build prints the same.
        /// </summary>
        private const string PinnedSwitchOffDigest = "XUby342AMMLrd5lHLMg2NDv8/sWermCGIRlD/nSdyy8=";

        // ------------------------------------------------------------------ the switch

        private static string Header(EnvSettings s, RunConfig c)
        {
            var world = new World(c, s.Seed);
            SpaceFacts space = SpaceFacts.Of(
                world,
                hasWall: c.SharedSpace && c.WorldShape == WorldShape.Tank,
                hasFloor: c.SharedSpace,
                threads: 24,
                inoculumHashShort: null);
            return Report.HeaderLine(s, c, space, "9.9.9.9");
        }

        /// <summary>
        /// <c>EVOSIM_BITE_NEEDS_MOUTH</c> reaches the config, is off when unset, is shown in the header only when on, and
        /// the world refuses it on without contact eating or without a true contact shape, with a message that names D275.
        /// </summary>
        [Fact]
        public void TheSwitchIsBoundShownOnlyWhenOnAndRefusedWithoutContactEatingOrTheTrueShape()
        {
            var block = EnvBindingTests.Round42Seed1();
            block["EVOSIM_CONTACT_EATING"] = "1";
            block["EVOSIM_CONTACT_PER_PART"] = "1";
            block["EVOSIM_CONTACT_SHAPE"] = "trueshape";

            EnvSettings offSettings = EnvBinding.Read(EnvBinding.Of(block));
            RunConfig off = EnvBinding.BuildConfig(offSettings);
            Assert.False(off.BiteNeedsMouth);

            block["EVOSIM_BITE_NEEDS_MOUTH"] = "1";
            EnvSettings settings = EnvBinding.Read(EnvBinding.Of(block));
            RunConfig config = EnvBinding.BuildConfig(settings);
            Assert.True(config.BiteNeedsMouth);
            Assert.NotEqual(off.Hash(), config.Hash());
            Assert.Empty(EnvBinding.UnknownNames(block.Keys));

            Assert.DoesNotContain("a bite needs the mouth", Header(offSettings, off));
            Assert.Contains(" · a bite needs the mouth", Header(settings, config));

            // Unset and "0" are the same world.
            var plain = EnvBindingTests.Round42Seed1();
            string unset = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(plain))).Hash();
            plain["EVOSIM_BITE_NEEDS_MOUTH"] = "0";
            Assert.Equal(unset, EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(plain))).Hash());

            // Without contact eating.
            var noEating = EnvBindingTests.Round42Seed1();
            noEating["EVOSIM_CONTACT_PER_PART"] = "1";
            noEating["EVOSIM_CONTACT_SHAPE"] = "trueshape";
            noEating["EVOSIM_BITE_NEEDS_MOUTH"] = "1";
            RunConfig withoutEating = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(noEating)));
            Assert.True(withoutEating.BiteNeedsMouth);
            Assert.False(withoutEating.ContactEating);
            ArgumentException eating = Assert.Throws<ArgumentException>(() => new World(withoutEating, 1UL));
            Assert.Contains("BiteNeedsMouth", eating.Message);
            Assert.Contains("D275", eating.Message);

            // Contact eating on, below the true shape.
            var spheres = EnvBindingTests.Round42Seed1();
            spheres["EVOSIM_CONTACT_EATING"] = "1";
            spheres["EVOSIM_BITE_NEEDS_MOUTH"] = "1";
            RunConfig withoutShape = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(spheres)));
            Assert.Equal(ContactShapeForm.Spheres, withoutShape.ContactShape);
            ArgumentException shape = Assert.Throws<ArgumentException>(() => new World(withoutShape, 1UL));
            Assert.Contains("BiteNeedsMouth", shape.Message);
            Assert.Contains("D275", shape.Message);
        }

        // ------------------------------------------------------------------ a bar held to a plate

        // A bar with its mouth at the centre of its +x end, and a flat plate: shapes whose true surface is not a ball,
        // since a cube's core is a point and its margin the whole half-extent.
        private static Genome Bar()
        {
            Genome genome = RemainsFixtures.Hunter();
            genome.Nodes[0].Dimensions = new Float3(0.4f, 0.1f, 0.1f);
            return genome;
        }

        private static Genome Plate()
        {
            Genome genome = RemainsFixtures.Claw();
            MorphNode node = genome.Nodes[0];
            node.Attack = 0f;
            node.Dimensions = new Float3(0.5f, 0.05f, 0.5f);
            node.Toughness = 4f;
            return genome;
        }

        private static RunConfig Config(bool needsMouth) =>
            TrueShapeBiteScenario.Config(c => c.BiteNeedsMouth = needsMouth);

        private static readonly Vec3 PlateAt = new Vec3(0, -2.5, 0);

        /// <summary>
        /// Holds the bar against the plate before the census, lying on its side along the plate's top (its mouth a bar
        /// width off the plate: a ratio of about 2) or stood on its end with its mouth in the plate (a ratio under 0).
        /// </summary>
        private static void Hold(Simulation sim, long bar, long plate, bool tip)
        {
            if (!sim.TryPose(bar, out Creature b) || !sim.TryPose(plate, out Creature p)) return;

            p.PlaceAt(PlateAt, QuatD.Identity);

            if (tip)
            {
                b.PlaceAt(PlateAt + new Vec3(0, 0.05 + 0.4 - 0.002, 0), QuatD.FromAxisAngle(new Vec3(0, 0, 1), -Math.PI / 2.0));
            }
            else
            {
                b.PlaceAt(PlateAt + new Vec3(0, 0.05 + 0.1 - 0.002, 0), QuatD.Identity);
            }
        }

        private static (long Bar, long Plate) Ids(RemainsFixtures.Pair pair) =>
            (pair.World.Living.Single(c => c.HasIntake).Id, pair.World.Living.Single(c => !c.HasIntake).Id);

        /// <summary>Steps <paramref name="steps"/> metabolic steps with the bar held before every census.</summary>
        private static void Run(
            RemainsFixtures.Pair pair, long bar, long plate, int from, int steps, Func<int, bool> tipAt, Action<int> each = null)
        {
            int metabolic = from;
            while (metabolic < from + steps)
            {
                if ((pair.Sim.Steps + 1) % pair.Sim.StepsPerMetabolicStep == 0) Hold(pair.Sim, bar, plate, tipAt(metabolic));
                if (!pair.Sim.Step()) continue;

                metabolic++;
                each?.Invoke(metabolic);
            }
        }

        [Fact]
        public void SideBrushRefusedTipTouchLands()
        {
            // Side-on, with the switch on: the plate keeps its health, the bite columns stay flat, and the refusals rise.
            using (RemainsFixtures.Pair side = RemainsFixtures.Arena(Config(needsMouth: true), Bar(), Plate()))
            {
                (long bar, long plate) = Ids(side);
                Organism leaf = side.World.Living.Single(c => c.Id == plate);
                Organism eater = side.World.Living.Single(c => c.Id == bar);
                double energy0 = eater.Energy;

                bool ate = false;
                Run(side, bar, plate, 0, 30, _ => false, _ => ate |= side.World.TookMatterThisStep.Count > 0);

                _out.WriteLine(
                    $"side-on: refused {side.World.MouthBlowsRefused}, landed {side.World.MouthBlows}, plate health " +
                    (leaf.PartHealth == null ? "never wounded" : leaf.PartHealth[0].ToString("0.####")));

                Assert.True(side.World.MouthBlowsRefused > 0L, "no blow was refused: the bar never touched the plate");
                Assert.Equal(0L, side.World.MouthBlows);
                Assert.Equal(0L, side.World.MouthBlowsFinishing);
                Assert.True(leaf.PartHealth == null || leaf.PartHealth[0] == 1f, "the plate was wounded");
                Assert.False(ate, "the bar's mouth took matter");
                Assert.Equal(0d, side.World.BiteKeptFromOthersJoules);
                Assert.Equal(0d, side.World.BiteWasteJoules);
                Assert.Equal(0u, eater.TookMatterMask);
                Assert.True(eater.Energy <= energy0 + 1e-9, "the bar's reserve grew without a meal");
            }

            // With the switch off the same hold wounds the plate: the side of the bar bites.
            using (RemainsFixtures.Pair off = RemainsFixtures.Arena(Config(needsMouth: false), Bar(), Plate()))
            {
                (long bar, long plate) = Ids(off);
                Run(off, bar, plate, 0, 30, _ => false);
                Assert.True(off.World.MouthBlows > 0L, "with the switch off the side-on bar should bite");
                Assert.Equal(0L, off.World.MouthBlowsRefused);
            }

            // Tip-on, with the switch on: a wound, a meal and an ate bit in the pose stream.
            string poseFile = Path.Combine(SimulationTests.Scratch("bite-needs-mouth-tip"), "poses.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(poseFile));
            if (File.Exists(poseFile)) File.Delete(poseFile);

            RunConfig config = Config(needsMouth: true);
            using (RemainsFixtures.Pair tip = RemainsFixtures.Arena(config, Bar(), Plate()))
            {
                (long bar, long plate) = Ids(tip);
                Organism eater = tip.World.Living.Single(c => c.Id == bar);
                bool ate = false;

                using (PoseRecorder poses = PoseRecorder.AtPath(poseFile, 0.5f, config.Hash()))
                {
                    Run(tip, bar, plate, 0, 12, _ => true, m =>
                    {
                        ate |= tip.World.TookMatterThisStep.Count > 0;
                        poses.Observe(tip.Sim);
                        if (m % 2 == 0) poses.WriteFrame(tip.Sim, tip.World.ElapsedSeconds);
                    });
                }

                _out.WriteLine(
                    $"tip-on: refused {tip.World.MouthBlowsRefused}, landed {tip.World.MouthBlows}, kept " +
                    $"{tip.World.BiteKeptFromOthersJoules:0.###} J, wasted {tip.World.BiteWasteJoules:0.###} J");

                Assert.True(tip.World.MouthBlows > 0L, "the tip-on bar never landed a blow");
                Assert.Equal(0L, tip.World.MouthBlowsRefused);
                Assert.True(tip.World.BiteKeptFromOthersJoules + tip.World.BiteWasteJoules > 0d, "no meal was offered");
                Assert.True(tip.World.BiteKeptFromOthersJoules > 0d, "the bar kept nothing of the meal");
                Assert.True(ate, "no step recorded the mouth taking matter");

                uint mask = 0u;
                using (PoseStreamReader reader = PoseStreamReader.Open(poseFile))
                {
                    for (int f = 0; f < reader.Frames.Length; f++)
                    {
                        foreach (PoseBody b in reader.Read(f).Bodies.Where(body => body.Id == bar)) mask |= b.AteMask;
                    }
                }

                Assert.NotEqual(0u, mask);
                Assert.True(eater.Energy > 0d);
            }
        }

        // ------------------------------------------------------------------ the switch off is the tree before

        [Fact]
        public void SwitchOffIdentity()
        {
            string digest = TrueShapeBiteScenario.Digest("bite-needs-mouth-off", null, out World world, out string summary);
            _out.WriteLine("switch-off digest " + digest);
            _out.WriteLine(summary);

            // The scenario has to bite, or equal bytes say nothing.
            Assert.True(world.MouthBlows > 0L, "no mouth blew: the scenario tests nothing");
            Assert.True(world.PartsKilled > 0L, "no part was bitten off: the scenario tests nothing");
            Assert.False(world.Config.BiteNeedsMouth);

            string stats = File.ReadAllText(Path.Combine(TrueShapeBiteScenario.LastRunPath, "stats.jsonl"));
            Assert.DoesNotContain("mouthBlowsRefused", stats);

            Assert.Equal(PinnedSwitchOffDigest, digest);
        }

        [Fact]
        public void TheSwitchOnIsAnotherWorldThatRefusesAndWritesItsColumn()
        {
            string digest = TrueShapeBiteScenario.Digest(
                "bite-needs-mouth-on", c => c.BiteNeedsMouth = true, out World world, out string summary);
            _out.WriteLine("switch-on digest " + digest);
            _out.WriteLine(summary + $"; refused {world.MouthBlowsRefused}");

            Assert.NotEqual(PinnedSwitchOffDigest, digest);
            Assert.True(world.MouthBlowsRefused > 0L, "nothing was refused");

            string[] rows = File.ReadAllLines(Path.Combine(TrueShapeBiteScenario.LastRunPath, "stats.jsonl"));
            Assert.Contains("\"mouthBlowsRefused\":", rows[rows.Length - 1]);
        }

        // ------------------------------------------------------------------ a resume

        [Fact]
        public void ASwitchOnWorldResumedFromItsCheckpointEqualsTheUnbrokenRun()
        {
            // Side-on on even metabolic steps and tip-on on odd. The plate dies of its second landed blow, so the cut falls after the
            // first refusal and the first blow, and the stretch after it holds a refusal, a blow and the kill.
            Func<int, bool> tipAt = m => m % 2 == 1;
            const int total = 12, cut = 2;

            byte[] Payload(RemainsFixtures.Pair pair)
            {
                pair.Sim.SettleBeforeCheckpoint();
                using (var buffer = new MemoryStream())
                {
                    using (var w = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
                    {
                        pair.World.WriteState(w);
                        pair.Sim.WriteState(w);
                        w.Flush();
                    }

                    return buffer.ToArray();
                }
            }

            RunConfig config = Config(needsMouth: true);

            using (RemainsFixtures.Pair whole = RemainsFixtures.Arena(config, Bar(), Plate()))
            {
                (long bar, long plate) = Ids(whole);
                Run(whole, bar, plate, 0, total, tipAt);

                byte[] first;
                ulong solver;
                long refused, landed;

                using (RemainsFixtures.Pair head = RemainsFixtures.Arena(Config(needsMouth: true), Bar(), Plate()))
                {
                    Run(head, bar, plate, 0, cut, tipAt);
                    first = Payload(head);
                }

                using (RemainsFixtures.Pair tail = RemainsFixtures.Found(Config(needsMouth: true)))
                {
                    using (var buffer = new MemoryStream(first, writable: false))
                    using (var r = new BinaryReader(buffer, Encoding.UTF8))
                    {
                        tail.World.ReadState(r);
                        tail.Sim.ReadState(r);
                        Assert.Equal(first.Length, buffer.Position);
                    }

                    Run(tail, bar, plate, cut, total - cut, tipAt);

                    refused = tail.World.MouthBlowsRefused;
                    landed = tail.World.MouthBlows;
                    solver = tail.Sim.Dynamics.Digest();
                    Assert.Equal(Payload(whole), Payload(tail));
                }

                _out.WriteLine($"refused {refused}, landed {landed}");
                Assert.True(refused > 0L && landed > 0L, "the stretch must both refuse and land");
                Assert.Equal(whole.World.MouthBlowsRefused, refused);
                Assert.Equal(whole.World.MouthBlows, landed);
                Assert.Equal(whole.Sim.Dynamics.Digest(), solver);
            }
        }

        // ------------------------------------------------------------------ the host's cost

        /// <summary>
        /// A forked body whose seven parts all eat, so every contact measures seven mouths against seven parts a side.
        /// </summary>
        private static Genome EatingFork()
        {
            Genome genome = RemainsFixtures.Fork();
            MorphNode link = genome.Nodes[1];
            link.CellTypeId = CellTypeIds.Consumer;
            link.JointType = JointType.Fixed;
            link.Power = 0f;
            link.ResampleJointLimits(new Float2(-1f, 1f));
            link.Intake = CellTypeRegistry.Standard.Resolve(CellTypeIds.Consumer).IntakeMax;
            link.Attack = 1f;
            return genome;
        }

        [Fact]
        public void TheMeasureCostsTheHostMicrosecondsPerContactSideAndNothingWithTheSwitchOff()
        {
            const int pairs = 6, steps = 60;

            double Run(bool needsMouth, out long sides, out int contactSteps)
            {
                RunConfig config = Config(needsMouth);
                config.MaximumPopulation = 1000;
                using (RemainsFixtures.Pair p = RemainsFixtures.Found(config))
                {
                    for (int i = 0; i < pairs; i++)
                    {
                        p.World.Inoculate(EatingFork(), count: 1, heightY: -2.5f);
                        p.World.Inoculate(RemainsFixtures.Fork(), count: 1, heightY: -2.5f);
                    }

                    p.Sim.SettleBeforeCheckpoint();

                    int metabolic = 0;
                    contactSteps = 0;
                    while (metabolic < steps)
                    {
                        if ((p.Sim.Steps + 1) % p.Sim.StepsPerMetabolicStep == 0 && metabolic >= 2)
                        {
                            var eaters = new List<long>();
                            var victims = new List<long>();
                            foreach (Organism o in p.World.Living)
                            {
                                if (o.HasIntake) eaters.Add(o.Id);
                                else if (o.Phenotype.PartCount >= 2) victims.Add(o.Id);
                            }

                            for (int i = 0; i < eaters.Count && i < victims.Count; i++)
                            {
                                RemainsFixtures.Press(p.Sim, eaters[i], victims[i], 1 + i % 3);
                            }
                        }

                        if (!p.Sim.Step()) continue;
                        metabolic++;
                        contactSteps++;
                    }

                    sides = p.Sim.MouthSidesMeasured;
                    return p.Sim.MouthMeasureSeconds;
                }
            }

            double offSeconds = Run(needsMouth: false, out long offSides, out int offSteps);
            Assert.Equal(0L, offSides);
            Assert.Equal(0d, offSeconds);

            double onSeconds = Run(needsMouth: true, out long onSides, out int onSteps);
            Assert.True(onSides > 0L, "no side was measured");

            double perStepMicroseconds = onSeconds / onSteps * 1e6;
            double perSideMicroseconds = onSeconds / onSides * 1e6;
            _out.WriteLine(
                $"host cost of the measure: {onSides} sides measured in {onSteps} metabolic steps, " +
                $"{onSeconds * 1e3:0.###} ms in all; {perStepMicroseconds:0.##} us per metabolic step " +
                $"({pairs} eating forks of seven eaters against seven parts), {perSideMicroseconds:0.##} us per side measured");

            Assert.True(perSideMicroseconds < 5000d, "a side took over 5 ms: the screen is not working");
        }
    }
}
