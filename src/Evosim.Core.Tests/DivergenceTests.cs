using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// <see cref="World.KillDiverged"/> and <see cref="DeathCause.Diverged"/> — the world's half
    /// of the divergence spec, after logbook/0056's censored <c>r20q-s1</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What went wrong there.</b> One creature's articulation exploded at t=15,345 s of a
    /// 20,000 s arm: PhysX refused <c>{NaN, NaN, NaN}</c> forces and torques for all three of its
    /// parts nine steps running, and then <see cref="World.Observe"/> saw the non-finite height
    /// and threw. Fifteen thousand simulated seconds of a five-seed condition became a censored
    /// arm because of one body. A diverged body is now a death like any other — and the whole
    /// point of routing it through the same path is that the books must not notice the
    /// difference.
    /// </para>
    /// <para>
    /// So these tests are about the accounting, not about the physics: nothing here can make a
    /// solver diverge, and nothing here tries. They pin that a body removed by
    /// <see cref="World.KillDiverged"/> leaves §5A.2's energy audit closed and the matter
    /// identity intact, and that the lineage row says which of the two causes it was.
    /// </para>
    /// </remarks>
    public class DivergenceTests
    {
        private readonly ITestOutputHelper _output;

        public DivergenceTests(ITestOutputHelper output) => _output = output;

        /// <summary>A world with matter priced, so a body born into it locks some.</summary>
        /// <remarks>
        /// Matter has to be on for these to mean anything: with
        /// <see cref="RunConfig.MatterPerTissueJoule"/> at 0 every creature's
        /// <see cref="Organism.LockedMatter"/> is 0, and the matter half of the identity would be
        /// asserted over three zeroes.
        /// </remarks>
        private static RunConfig MatterWorld() => new RunConfig
        {
            Light = new LightModel(200f, 12f),
            MatterPerTissueJoule = 0.5f,
            InitialMatterPerCubicMetre = 1f,
        };

        /// <summary>Steps a world until something in it was born to a parent and holds matter.</summary>
        private World Populated(out Organism victim)
        {
            var world = new World(MatterWorld(), seed: 1);

            for (int i = 0; i < 400; i++) world.Step(1f);

            victim = null;
            for (int i = 0; i < world.Living.Count; i++)
            {
                Organism creature = world.Living[i];
                if (creature.LockedMatter > 0f && creature.TissueJoules > 0f)
                {
                    victim = creature;
                    break;
                }
            }

            Assert.True(
                victim != null,
                $"no living creature holds matter after {world.Births} births — the fixture " +
                "cannot test the matter half of a death it never set up.");

            return world;
        }

        [Fact]
        public void ADivergedDeathMovesExactlyWhatAStarvationOfTheSameBodyWould()
        {
            World world = Populated(out Organism victim);

            // Read off the body before it is buried: these three are the whole of what the
            // starvation path at the bottom of Metabolise moves, and a diverged body must move
            // the same three by the same amounts. Both causes reach World.Bury, which is the
            // structural half of this assertion; these numbers are the measured half.
            float energy = victim.Energy;
            float tissue = victim.TissueJoules;
            float locked = victim.LockedMatter;

            double energyOut = world.EnergyOut;
            double detritus = world.Nutrients.TotalJoules;
            double deposited = world.DetritusDepositedTotal;
            double inBodies = world.MatterInBodies;
            double freeMatter = world.Matter.TotalJoules;
            long deaths = world.Deaths;
            int alive = world.Living.Count;

            world.KillDiverged(victim);

            _output.WriteLine(
                $"buried #{victim.Id}: {energy:0.###} J reserve, {tissue:0.###} J tissue, " +
                $"{locked:0.###} matter");

            Assert.Equal(energyOut + energy, world.EnergyOut, 3);
            Assert.Equal(detritus + tissue, world.Nutrients.TotalJoules, 3);
            Assert.Equal(deposited + tissue, world.DetritusDepositedTotal, 3);
            Assert.Equal(inBodies - locked, world.MatterInBodies, 3);
            Assert.Equal(freeMatter + locked, world.Matter.TotalJoules, 3);

            Assert.Equal(deaths + 1, world.Deaths);
            Assert.Equal(alive - 1, world.Living.Count);
            Assert.DoesNotContain(victim, world.Living);

            // The body is emptied, not half-emptied: a corpse still holding tissue or matter
            // would be counted twice by anything that walks the dead.
            Assert.Equal(0f, victim.Energy);
            Assert.Equal(0f, victim.TissueJoules);
            Assert.Equal(0f, victim.LockedMatter);
        }

        [Fact]
        public void TheEnergyAuditAndTheMatterIdentityBothStillClose()
        {
            World world = Populated(out Organism victim);

            double residualBefore = world.AuditResidual;
            double matterBefore = world.StandingMatter;

            world.KillDiverged(victim);

            // Stepped on afterwards as well: a death that left the books consistent for one
            // instant and inconsistent by the next metabolic step would pass a snapshot check
            // and fail the run.
            for (int i = 0; i < 20; i++) world.Step(1f);

            double scale = Math.Max(1d, world.EnergyIn);

            _output.WriteLine(
                $"residual {residualBefore:0.######} -> {world.AuditResidual:0.######} J " +
                $"({world.AuditResidual / scale:P4}); matter {matterBefore:0.######} -> " +
                $"{world.StandingMatter:0.######}");

            Assert.True(
                Math.Abs(world.AuditResidual) / scale < 1e-6,
                $"a diverged death opened a hole in §5A.2's audit: {world.AuditResidual} J");

            Assert.True(
                Math.Abs(world.StandingMatter - matterBefore) / Math.Max(1d, matterBefore) < 1e-6,
                $"matter drifted by {world.StandingMatter - matterBefore} across a diverged death");
        }

        [Fact]
        public void TheDivergedCountIsSeparateFromTheDeathCount()
        {
            World world = Populated(out Organism victim);

            Assert.Equal(0, world.Diverged);

            long deaths = world.Deaths;
            world.KillDiverged(victim);

            Assert.Equal(1, world.Diverged);
            Assert.Equal(deaths + 1, world.Deaths);
        }

        [Fact]
        public void AWorldNothingDivergesInCountsNoneAndSaysStarvedInEveryRow()
        {
            // The default-preserving half. Nothing in Evosim.Core calls KillDiverged — only the
            // harness does, and only from a finiteness check that a healthy step never fails — so
            // a world stepped normally must be exactly the world that existed before any of this,
            // and its lineage file must not have gained a vocabulary.
            var world = new World(MatterWorld(), seed: 3);
            for (int i = 0; i < 300; i++) world.Step(1f);

            IReadOnlyList<LineageEvent> events = world.DrainLineageEvents();
            int deaths = 0;

            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Kind != LineageEventKind.Death) continue;

                deaths++;
                Assert.Equal(DeathCause.Starved, events[i].Cause);
            }

            _output.WriteLine($"{deaths} deaths, {world.Diverged} diverged");

            Assert.Equal(0, world.Diverged);
            Assert.True(deaths > 0, "a world with no deaths cannot say anything about their cause");
        }

        [Fact]
        public void TheLineageRowNamesTheCause()
        {
            // By name, not by ordinal — CLAUDE.md's serialisation rule. A row that recorded the
            // enum's number would silently re-label every historical death the day a member is
            // inserted above it.
            string diverged = LineageEvent.Death(12.5d, id: 7, DeathCause.Diverged).ToJson();
            string starved = LineageEvent.Death(12.5d, id: 7, DeathCause.Starved).ToJson();

            _output.WriteLine(diverged);

            Assert.Contains("\"c\":\"diverged\"", diverged);
            Assert.Contains("\"c\":\"starved\"", starved);
            Assert.DoesNotContain("\"c\":1", diverged);
        }

        [Fact]
        public void KillingABodyThatIsNotLivingIsRefused()
        {
            // The scene and the population coming apart is a real fault — the harness looks a
            // creature up through the body it built for it — and a silent no-op would let a run
            // continue with a corpse still being simulated.
            World world = Populated(out Organism victim);

            world.KillDiverged(victim);
            Assert.Throws<ArgumentException>(() => world.KillDiverged(victim));
            Assert.Throws<ArgumentNullException>(() => world.KillDiverged(null));
        }
    }
}
