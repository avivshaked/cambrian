using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The intake's origin books: which corpse a mouth ate from, by whose bite made it. An instrument, so
    /// the properties tested are that it books the right class, that its classes add to what the world
    /// already reports as kept, that a checkpoint carries it, and that reading it moves nothing.
    /// </summary>
    /// <remarks>
    /// Driven by hand as <see cref="MouthTests"/> is: Core has no physics, so the tests hand the world the
    /// contact list a bite would make and step the mouth pass.
    /// </remarks>
    public class IntakeOriginTests
    {
        private readonly ITestOutputHelper _output;

        public IntakeOriginTests(ITestOutputHelper output) => _output = output;

        private static readonly float ConsumerIntake = CellTypeRegistry.Standard.Resolve(CellTypeIds.Consumer).IntakeMax;

        // ------------------------------------------------------------------------ the fixtures

        /// <summary>A childless world with a mouth's reach and a waste worth measuring.</summary>
        private static RunConfig Stage()
        {
            var config = new RunConfig
            {
                MinimumPopulation = 0,
                MaximumPopulation = 100_000,
                Light = new LightModel(400f, 12f),
                InitialMatterPerCubicMetre = 50f,
                PerOffspringOverheadJoules = 1e9f,
                HealthPerCubicMetre = 100f,
                CorpseDecayPerSecond = 0.0001f,
                IntakeReachMetres = 5f,
                IntakeWasteFraction = 0.25f,
            };

            foreach (var property in typeof(MutationRates).GetProperties())
            {
                if (property.PropertyType == typeof(float) && property.Name.EndsWith("Chance"))
                {
                    property.SetValue(config.Mutation, 0f);
                }
            }

            return config;
        }

        /// <summary>A body with a claw and a mouth: it bites and it eats, so it is a mouthed eater.</summary>
        private static Genome Hunter() =>
            Fixtures.ArmedBox(attack: 1f, intake: ConsumerIntake, cellTypeId: CellTypeIds.Consumer);

        /// <summary>A body with a mouth and no claw.</summary>
        private static Genome Scavenger() =>
            Fixtures.ArmedBox(intake: ConsumerIntake, cellTypeId: CellTypeIds.Consumer);

        private static List<CreatureContact> Touching(Organism a, int partA, Organism b, int partB) =>
            new List<CreatureContact> { new CreatureContact(a.Id, partA, b.Id, partB) };

        /// <summary>Bites the victim's root until it is buried, and returns the steps it took.</summary>
        private static int BiteUntilTheRootComesOff(World world, Organism attacker, Organism victim, int limit)
        {
            for (int step = 1; step <= limit; step++)
            {
                world.SetContacts(Touching(attacker, 0, victim, 0));
                world.ApplyMouth(1f);
                if (world.BodiesEaten > 0L) return step;
            }

            return -1;
        }

        /// <summary>A leaf, a hunter and a scavenger in one place; the hunter has killed the leaf.</summary>
        private static World AfterAKill(out Organism hunter, out Organism scavenger, out Organism leaf)
        {
            var world = new World(Stage(), seed: 3);
            world.Inoculate(Fixtures.MouthSpine(1, 0.2f), 1, -1f);
            world.Inoculate(Hunter(), 1, -1f);
            world.Inoculate(Scavenger(), 1, -1f);

            leaf = world.Living[0];
            hunter = world.Living[1];
            scavenger = world.Living[2];

            Assert.True(hunter.HasAttack && hunter.HasIntake, "the hunter was built without a claw or a mouth");
            Assert.True(!scavenger.HasAttack && scavenger.HasIntake, "the scavenger should have a mouth and no claw");

            Assert.True(BiteUntilTheRootComesOff(world, hunter, leaf, 100) > 0, "the root never came off");
            return world;
        }

        private static byte[] StateOf(World world)
        {
            using (var buffer = new MemoryStream())
            {
                using (var w = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
                {
                    world.WriteState(w);
                    w.Flush();
                }

                return buffer.ToArray();
            }
        }

        private static World Restored(RunConfig config, ulong seed, byte[] state)
        {
            var world = new World(config, seed);
            using (var buffer = new MemoryStream(state, writable: false))
            using (var r = new BinaryReader(buffer, Encoding.UTF8))
            {
                world.ReadState(r);
                Assert.Equal(state.Length, buffer.Position);
            }

            return world;
        }

        private static void AssertRelative(double expected, double actual, double relative, string what)
        {
            double tolerance = Math.Max(1e-12, relative * Math.Abs(expected));
            Assert.True(
                Math.Abs(expected - actual) <= tolerance,
                $"{what}: expected {expected:R}, got {actual:R} (tolerance {tolerance:R})");
        }

        // --------------------------------------------------------------------- the classes

        [Fact]
        public void ACorpseRemembersTheBodyWhoseBiteMadeIt()
        {
            World world = AfterAKill(out Organism hunter, out _, out Organism leaf);

            Corpse corpse = Assert.Single(world.Corpses);
            Assert.Equal(leaf.Id, corpse.CreatureId);
            Assert.Equal(hunter.Id, corpse.KilledBy);
        }

        [Fact]
        public void ABodyThatKillsABodyAndEatsItsCorpseBooksOwn()
        {
            var world = new World(Stage(), seed: 3);
            world.Inoculate(Fixtures.MouthSpine(1, 0.2f), 1, -1f);
            world.Inoculate(Hunter(), 1, -1f);
            Organism leaf = world.Living[0];
            Organism hunter = world.Living[1];

            Assert.True(BiteUntilTheRootComesOff(world, hunter, leaf, 100) > 0);
            Assert.Equal(0d, hunter.MouthJoules);

            for (int step = 0; step < 3; step++) world.ApplyMouth(0.5f);

            Assert.True(hunter.MouthJoules > 0d, "the hunter never ate");
            _output.WriteLine(
                $"own {hunter.MouthJoulesOwnKill:0.######} other {hunter.MouthJoulesOtherKill:0.######} " +
                $"carrion {hunter.MouthJoulesCarrion:0.######} of {hunter.MouthJoules:0.######} J kept");

            Assert.True(hunter.MouthJoulesOwnKill > 0d);
            Assert.Equal(0d, hunter.MouthJoulesOtherKill);
            Assert.Equal(0d, hunter.MouthJoulesCarrion);

            Assert.Equal(hunter.MouthJoulesOwnKill, world.IntakeOwnKillJoules);
            Assert.Equal(0d, world.IntakeOtherKillJoules);
            Assert.Equal(0d, world.IntakeCarrionJoules);

            // The hunter has a claw and a mouth, so the mouthed books are the same books.
            Assert.Equal(world.IntakeOwnKillJoules, world.MouthedIntakeOwnKillJoules);
            Assert.Equal(0d, world.MouthedIntakeOtherKillJoules);
            Assert.Equal(0d, world.MouthedIntakeCarrionJoules);
        }

        [Fact]
        public void ASecondMouthAtTheSameCorpseBooksOtherKill()
        {
            World world = AfterAKill(out Organism hunter, out Organism scavenger, out _);

            for (int step = 0; step < 3; step++) world.ApplyMouth(0.5f);
            Assert.NotEmpty(world.Corpses);

            _output.WriteLine(
                $"hunter own {hunter.MouthJoulesOwnKill:0.######} other {hunter.MouthJoulesOtherKill:0.######} " +
                $"carrion {hunter.MouthJoulesCarrion:0.######}; scavenger own {scavenger.MouthJoulesOwnKill:0.######} " +
                $"other {scavenger.MouthJoulesOtherKill:0.######} carrion {scavenger.MouthJoulesCarrion:0.######}");

            Assert.True(hunter.MouthJoulesOwnKill > 0d);
            Assert.Equal(0d, hunter.MouthJoulesOtherKill);
            Assert.Equal(0d, hunter.MouthJoulesCarrion);

            Assert.Equal(0d, scavenger.MouthJoulesOwnKill);
            Assert.True(scavenger.MouthJoulesOtherKill > 0d, "the scavenger's meal of the hunter's kill was not booked as another's");
            Assert.Equal(0d, scavenger.MouthJoulesCarrion);

            // The world's books split the same way, and the mouthed ones leave the scavenger out.
            AssertRelative(hunter.MouthJoulesOwnKill, world.IntakeOwnKillJoules, 1e-9, "world own");
            AssertRelative(scavenger.MouthJoulesOtherKill, world.IntakeOtherKillJoules, 1e-9, "world other");
            Assert.Equal(0d, world.IntakeCarrionJoules);

            AssertRelative(hunter.MouthJoulesOwnKill, world.MouthedIntakeOwnKillJoules, 1e-9, "mouthed own");
            Assert.Equal(0d, world.MouthedIntakeOtherKillJoules);
            Assert.Equal(0d, world.MouthedIntakeCarrionJoules);
        }

        /// <summary>
        /// Four leaves that age; stepped until one dies of age, so the corpse on the water is an age death's.
        /// Then a hunter and a scavenger are put beside it and only the mouth pass is run.
        /// </summary>
        private static World AfterAnAgeDeath(out Organism hunter, out Organism scavenger, out long agedId)
        {
            RunConfig config = Stage();
            config.WorldAreaSquareMetres = 100f;
            config.WorldDepthMetres = 20f;
            config.Light = new LightModel(64f, 12f);
            config.AgeHazardBaseSeconds = 40f;
            config.AgeingPerBirth = 1f;
            config.AgeingPerWound = 1f;
            config.CorpseDecayPerSecond = 0.01f;
            config.CorpseSinkMetresPerSecond = 0.5f;
            config.FreeFirstLinkCubicMetres = 0.01f;
            config.IntakeReachMetres = 1000f;

            var world = new World(config, seed: 11);
            world.Inoculate(Fixtures.SingleLeaf(), count: 4, heightY: -5f);

            long died = -1L;
            for (int i = 0; i < 2000 && died < 0L; i++)
            {
                world.Step(0.5f);
                foreach (LineageEvent e in world.DrainLineageEvents())
                {
                    if (e.Kind == LineageEventKind.Death && e.Cause == DeathCause.Aged && died < 0L) died = e.Id;
                }
            }

            Assert.True(died >= 0L, "nobody died of age");
            Assert.Contains(world.Corpses, c => c.CreatureId == died);

            world.Inoculate(Hunter(), 1, -5f);
            world.Inoculate(Scavenger(), 1, -5f);
            agedId = died;
            hunter = world.Living.Single(c => c.HasAttack && c.HasIntake);
            scavenger = world.Living.Single(c => !c.HasAttack && c.HasIntake);
            return world;
        }

        [Fact]
        public void ACorpseFromAnAgeDeathBooksCarrion()
        {
            World world = AfterAnAgeDeath(out Organism hunter, out Organism scavenger, out long agedId);

            Corpse aged = world.Corpses.First(c => c.CreatureId == agedId);
            Assert.Equal(-1L, aged.KilledBy);
            Assert.All(world.Corpses, c => Assert.Equal(-1L, c.KilledBy));

            for (int step = 0; step < 3; step++) world.ApplyMouth(0.5f);

            _output.WriteLine(
                $"carrion {world.IntakeCarrionJoules:0.######} J, of which mouthed {world.MouthedIntakeCarrionJoules:0.######}");

            Assert.True(hunter.MouthJoulesCarrion > 0d);
            Assert.True(scavenger.MouthJoulesCarrion > 0d);
            Assert.Equal(0d, hunter.MouthJoulesOwnKill + hunter.MouthJoulesOtherKill);
            Assert.Equal(0d, scavenger.MouthJoulesOwnKill + scavenger.MouthJoulesOtherKill);
            Assert.Equal(0d, world.IntakeOwnKillJoules);
            Assert.Equal(0d, world.IntakeOtherKillJoules);

            // The mouthed books hold the hunter and leave the scavenger out.
            AssertRelative(hunter.MouthJoulesCarrion, world.MouthedIntakeCarrionJoules, 1e-9, "mouthed carrion");
            Assert.True(world.IntakeCarrionJoules > world.MouthedIntakeCarrionJoules);
        }

        // --------------------------------------------------------------------- the sums

        /// <summary>
        /// The three classes add to what the world already reports as kept. Compared in two ways, and the
        /// tolerance of each is stated: against the sum of the bodies' own <c>MouthJoules</c>, which the
        /// world adds in the same place, the difference is only the order of the additions, so 1e-9
        /// relative (not bit equality, because the classes are separate running sums); against
        /// <c>unitsEaten x joulesPerUnit x (1 - waste)</c> the difference is the float quantisation of
        /// each step's waste, about 6e-8 of the waste, so 1e-6 relative.
        /// </summary>
        [Fact]
        public void TheThreeClassesAddToTheKeptIntakeTheWorldAlreadyReports()
        {
            RunConfig config = Stage();
            World world = AfterAKill(out Organism hunter, out Organism scavenger, out _);
            config = world.Config;

            double snowBefore = world.Nutrients.TotalJoules;
            for (int step = 0; step < 6; step++) world.ApplyMouth(0.5f);
            Assert.True(world.UnitsEaten > 0d);

            double classes = world.IntakeOwnKillJoules + world.IntakeOtherKillJoules + world.IntakeCarrionJoules;
            double bodies = world.Living.Sum(c => c.MouthJoules);
            double byFormula = world.UnitsEaten * config.JoulesPerUnit * (1d - config.IntakeWasteFraction);
            double byWaste = world.UnitsEaten * config.JoulesPerUnit - (world.Nutrients.TotalJoules - snowBefore);

            _output.WriteLine(
                $"classes {classes:R}; bodies' MouthJoules {bodies:R}; units x jpu x (1 - waste) {byFormula:R}; " +
                $"taken less the snow it left {byWaste:R}");

            AssertRelative(bodies, classes, 1e-9, "classes against the bodies' kept joules");
            AssertRelative(byFormula, classes, 1e-6, "classes against units x joulesPerUnit x (1 - waste)");
            AssertRelative(byWaste, classes, 1e-6, "classes against what was taken less the snow it left");

            // The mouthed three add to the hunter's kept joules alone.
            double mouthed = world.MouthedIntakeOwnKillJoules + world.MouthedIntakeOtherKillJoules + world.MouthedIntakeCarrionJoules;
            AssertRelative(hunter.MouthJoules, mouthed, 1e-9, "mouthed classes against the hunter's kept joules");
            Assert.True(scavenger.MouthJoules > 0d);
            Assert.True(classes > mouthed);

            // Each body's three add to its own count.
            foreach (Organism body in new[] { hunter, scavenger })
            {
                AssertRelative(
                    body.MouthJoules, body.MouthJoulesOwnKill + body.MouthJoulesOtherKill + body.MouthJoulesCarrion, 1e-9,
                    "a body's three against its own count");
            }
        }

        // --------------------------------------------------------------------- the lineage row

        [Fact]
        public void ADeathRowCarriesTheBodysThreeAndOmitsAZero()
        {
            World world = AfterAKill(out Organism hunter, out Organism scavenger, out _);
            for (int step = 0; step < 3; step++) world.ApplyMouth(0.5f);
            world.DrainLineageEvents();

            double own = hunter.MouthJoulesOwnKill, other = scavenger.MouthJoulesOtherKill;
            Assert.True(own > 0d && other > 0d);

            world.KillDiverged(hunter);
            world.KillDiverged(scavenger);

            List<LineageEvent> deaths = world.DrainLineageEvents().Where(e => e.Kind == LineageEventKind.Death).ToList();
            Assert.Equal(2, deaths.Count);

            JsonNode hunterRow = Json.Parse(deaths.Single(d => d.Id == hunter.Id).ToJson());
            JsonNode scavengerRow = Json.Parse(deaths.Single(d => d.Id == scavenger.Id).ToJson());
            _output.WriteLine(deaths.Single(d => d.Id == hunter.Id).ToJson());
            _output.WriteLine(deaths.Single(d => d.Id == scavenger.Id).ToJson());

            Assert.Equal(own, hunterRow["iok"].AsDouble());
            Assert.False(hunterRow.Has("iak"));
            Assert.False(hunterRow.Has("ic"));

            Assert.Equal(other, scavengerRow["iak"].AsDouble());
            Assert.False(scavengerRow.Has("iok"));
            Assert.False(scavengerRow.Has("ic"));

            Assert.DoesNotContain("\n", deaths[0].ToJson());
        }

        [Fact]
        public void ADeathRowOfABodyThatNeverAteIsTheRowItAlwaysWas()
        {
            LineageEvent row = LineageEvent.Death(12.5, 7, DeathCause.Starved);
            Assert.Equal("{\"e\":\"d\",\"t\":12.5,\"id\":7,\"c\":\"starved\"}", row.ToJson());
        }

        // --------------------------------------------------------------------- the checkpoint

        [Fact]
        public void ACheckpointCarriesTheKillerAndEveryBook()
        {
            World world = AfterAKill(out Organism hunter, out Organism scavenger, out _);
            for (int step = 0; step < 3; step++) world.ApplyMouth(0.5f);

            // A death row queued at the checkpoint carries its three too.
            world.KillDiverged(hunter);

            Corpse corpse = world.Corpses.First(c => c.KilledBy >= 0L);
            Assert.True(world.IntakeOwnKillJoules > 0d && world.IntakeOtherKillJoules > 0d);

            byte[] state = StateOf(world);
            World restored = Restored(world.Config, 3, state);

            Assert.Equal(World.StateVersion, restored.StateVersionRead);
            Assert.Equal(20, World.StateVersion);
            Assert.Equal(state, StateOf(restored));

            Corpse back = restored.Corpses.First(c => c.CreatureId == corpse.CreatureId);
            Assert.Equal(corpse.KilledBy, back.KilledBy);

            Assert.Equal(Bits(world.IntakeOwnKillJoules), Bits(restored.IntakeOwnKillJoules));
            Assert.Equal(Bits(world.IntakeOtherKillJoules), Bits(restored.IntakeOtherKillJoules));
            Assert.Equal(Bits(world.IntakeCarrionJoules), Bits(restored.IntakeCarrionJoules));
            Assert.Equal(Bits(world.MouthedIntakeOwnKillJoules), Bits(restored.MouthedIntakeOwnKillJoules));
            Assert.Equal(Bits(world.MouthedIntakeOtherKillJoules), Bits(restored.MouthedIntakeOtherKillJoules));
            Assert.Equal(Bits(world.MouthedIntakeCarrionJoules), Bits(restored.MouthedIntakeCarrionJoules));

            Organism scavengerBack = restored.Living.Single(c => c.Id == scavenger.Id);
            Assert.Equal(Bits(scavenger.MouthJoulesOtherKill), Bits(scavengerBack.MouthJoulesOtherKill));
            Assert.Equal(Bits(scavenger.MouthJoulesOwnKill), Bits(scavengerBack.MouthJoulesOwnKill));
            Assert.Equal(Bits(scavenger.MouthJoulesCarrion), Bits(scavengerBack.MouthJoulesCarrion));

            // The queued death row of the hunter, drained after the restore, is the row the unbroken run writes.
            string unbroken = world.DrainLineageEvents().Single(e => e.Kind == LineageEventKind.Death && e.Id == hunter.Id).ToJson();
            string resumed = restored.DrainLineageEvents().Single(e => e.Kind == LineageEventKind.Death && e.Id == hunter.Id).ToJson();
            Assert.Equal(unbroken, resumed);
            Assert.Contains("\"iok\":", resumed);

            // A resumed run then goes on booking exactly as the unbroken one does.
            for (int step = 0; step < 3; step++)
            {
                world.ApplyMouth(0.5f);
                restored.ApplyMouth(0.5f);
            }

            Assert.Equal(StateOf(world), StateOf(restored));
        }

        /// <summary>The older writer, kept for the tests of the readers of the older layouts.</summary>
        private static byte[] StateOf(World world, int layout)
        {
            System.Reflection.MethodInfo writer = typeof(World).GetMethod(
                "WriteState",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, new[] { typeof(BinaryWriter), typeof(int) }, null);

            Assert.NotNull(writer);

            using (var buffer = new MemoryStream())
            {
                using (var w = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
                {
                    writer.Invoke(world, new object[] { w, layout });
                    w.Flush();
                }

                return buffer.ToArray();
            }
        }

        [Fact]
        public void ALayoutNineteenCheckpointReadsTheKillerAsUnknownTheBooksAsZeroAndNamesItsSecond()
        {
            World world = AfterAKill(out Organism hunter, out Organism scavenger, out _);
            for (int step = 0; step < 3; step++) world.ApplyMouth(0.5f);

            // A world with some elapsed time, so that the second a restore names is not the founding's 0.
            for (int step = 0; step < 4; step++) world.Step(0.5f);
            Assert.True(world.ElapsedSeconds > 0d);
            Assert.True(world.IntakeOwnKillJoules > 0d, "the live world should have booked the hunter's meal");
            Assert.Equal(0d, world.IntakeOriginBooksFromSeconds);

            foreach (int layout in new[] { World.PreOriginStateVersion, World.PreHeldStateVersion })
            {
                byte[] older = StateOf(world, layout);
                World restored = Restored(world.Config, 3, older);

                Assert.Equal(layout, restored.StateVersionRead);
                Assert.Equal(world.ElapsedSeconds, restored.ElapsedSeconds);
                Assert.Equal(world.ElapsedSeconds, restored.IntakeOriginBooksFromSeconds);

                Assert.NotEmpty(restored.Corpses);
                Assert.All(restored.Corpses, c => Assert.Equal(-1L, c.KilledBy));

                Assert.Equal(0d, restored.IntakeOwnKillJoules);
                Assert.Equal(0d, restored.IntakeOtherKillJoules);
                Assert.Equal(0d, restored.IntakeCarrionJoules);
                Assert.Equal(0d, restored.MouthedIntakeOwnKillJoules);
                Assert.Equal(0d, restored.MouthedIntakeOtherKillJoules);
                Assert.Equal(0d, restored.MouthedIntakeCarrionJoules);

                foreach (Organism body in restored.Living)
                {
                    Assert.Equal(0d, body.MouthJoulesOwnKill);
                    Assert.Equal(0d, body.MouthJoulesOtherKill);
                    Assert.Equal(0d, body.MouthJoulesCarrion);
                }

                // What the layouts held is read as it was: the bodies' kept joules are the live world's.
                Assert.Equal(
                    Bits(world.Living.Single(c => c.Id == hunter.Id).MouthJoules),
                    Bits(restored.Living.Single(c => c.Id == hunter.Id).MouthJoules));
                Assert.Equal(Bits(world.UnitsEaten), Bits(restored.UnitsEaten));
                Assert.Equal(
                    Bits(world.Living.Single(c => c.Id == scavenger.Id).Energy),
                    Bits(restored.Living.Single(c => c.Id == scavenger.Id).Energy));
            }
        }

        [Fact]
        public void TheSecondTheBooksBeginAtSurvivesWritingAndResumingAgain()
        {
            World world = AfterAKill(out _, out _, out _);
            for (int step = 0; step < 4; step++) world.Step(0.5f);
            Assert.True(world.ElapsedSeconds > 0d);

            // A world founded here: the books begin at 0, and a layout-20 round trip keeps that.
            Assert.Equal(0d, world.IntakeOriginBooksFromSeconds);
            World same = Restored(world.Config, 3, StateOf(world));
            Assert.Equal(World.StateVersion, same.StateVersionRead);
            Assert.Equal(0d, same.IntakeOriginBooksFromSeconds);

            // A world restored from layout 19 names its second, writes it into layout 20, and a
            // third world restored from that keeps it, whatever the second at which it was written.
            World fromOlder = Restored(world.Config, 3, StateOf(world, World.PreOriginStateVersion));
            double second = fromOlder.ElapsedSeconds;
            Assert.True(second > 0d);
            Assert.Equal(second, fromOlder.IntakeOriginBooksFromSeconds);

            for (int step = 0; step < 4; step++) fromOlder.Step(0.5f);
            Assert.True(fromOlder.ElapsedSeconds > second);
            Assert.Equal(second, fromOlder.IntakeOriginBooksFromSeconds);

            byte[] again = StateOf(fromOlder);
            World resumed = Restored(world.Config, 3, again);
            Assert.Equal(World.StateVersion, resumed.StateVersionRead);
            Assert.Equal(second, resumed.IntakeOriginBooksFromSeconds);
            Assert.Equal(again, StateOf(resumed));
        }

        // --------------------------------------------------------------------- reading moves nothing

        private static string Fingerprint(World world) =>
            string.Join(
                "|",
                Bits(world.UnitsEaten), Bits(world.StandingJoules), Bits(world.Nutrients.TotalJoules),
                Bits(world.EnergyIn), Bits(world.EnergyOut), world.CorpsesEaten,
                string.Join(",", world.Living.Select(c => Bits(c.Energy) + "/" + Bits(c.MouthJoules))),
                string.Join(",", world.Corpses.Select(c => Bits(c.Joules))));

        private static long Bits(double value) => BitConverter.DoubleToInt64Bits(value);

        [Fact]
        public void AWorldSteppedWithAndWithoutReadingTheCountersIsTheSameBitForBit()
        {
            World quiet = AfterAKill(out _, out _, out _);
            World watched = AfterAKill(out Organism hunter, out Organism scavenger, out _);

            double sink = 0d;
            for (int step = 0; step < 20; step++)
            {
                quiet.ApplyMouth(0.5f);
                watched.ApplyMouth(0.5f);

                // Every reader there is, on one world only.
                sink += watched.IntakeOwnKillJoules + watched.IntakeOtherKillJoules + watched.IntakeCarrionJoules;
                sink += watched.MouthedIntakeOwnKillJoules + watched.MouthedIntakeOtherKillJoules + watched.MouthedIntakeCarrionJoules;
                sink += hunter.MouthJoulesOwnKill + hunter.MouthJoulesOtherKill + hunter.MouthJoulesCarrion;
                sink += scavenger.MouthJoulesOwnKill + scavenger.MouthJoulesOtherKill + scavenger.MouthJoulesCarrion;
                sink += watched.Corpses.Sum(c => (double)c.KilledBy);

                Assert.Equal(Fingerprint(quiet), Fingerprint(watched));
            }

            Assert.True(sink > 0d);
            Assert.Equal(StateOf(quiet), StateOf(watched));
        }
    }
}
