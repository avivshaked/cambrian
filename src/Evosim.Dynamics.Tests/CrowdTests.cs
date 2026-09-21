using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// The soft push in a crowd, which is the normal condition of these worlds and not an edge
    /// of them: round 42 holds about nine hundred bodies in the top ten metres of a 26 m tank and
    /// drops every newborn, a third of its adult size, within five metres of its parent.
    /// </summary>
    /// <remarks>
    /// Both of these lost bodies before <see cref="ContactLaw"/>: two hundred of round 42's
    /// bodies at a third of adult size packed into four metres lost 78 of 200 in 30 s, and a
    /// thousand at round 42's own density lost one in sixty. The law's three bounds — a contact
    /// never pulls, one pair may not add more than a metre a second of separation in a step, and
    /// a whole contact set may not change a body's speed by more than that in a step — are what
    /// these measure.
    /// </remarks>
    public sealed class CrowdTests
    {
        private readonly ITestOutputHelper _out;

        public CrowdTests(ITestOutputHelper output) => _out = output;

        /// <summary>Two hundred newborn-sized bodies inside four metres, for a minute.</summary>
        [Fact]
        public void APackedCrowdOfNewbornsLosesNobody()
        {
            Assert.True(RunFixture.Present, "round 42 seed 4 is not on this machine");

            SolverConfig solver = RunFixture.Solver();
            DynamicsWorld world = RunFixture.Pack(solver, 200, side: 4.0, fraction: 0.3f, threads: 8);

            Survive(world, seconds: 60);
        }

        /// <summary>
        /// A thousand bodies in round 42's own slab — a 26 m disc, ten metres deep — for ten
        /// minutes.
        /// </summary>
        [Fact]
        [Trait("Category", "Slow")]
        public void RoundFortyTwosOwnDensityLosesNobodyInTenMinutes()
        {
            Assert.True(RunFixture.Present, "round 42 seed 4 is not on this machine");

            SolverConfig solver = RunFixture.Solver();

            // The scatter's own draw, admitted crowded: at this density the placer would mostly
            // accept anyway, and the question is what the push does when it does not.
            DynamicsWorld world = RunFixture.Scatter(
                solver, 1000, threads: 24, drop: 10.0, crowd: true);

            Survive(world, seconds: 600);
        }

        private void Survive(DynamicsWorld world, double seconds)
        {
            int steps = (int)(seconds / world.Config.StepSeconds);
            int worstStep = -1;

            for (int step = 1; step <= steps; step++)
            {
                world.Step();

                if (world.NonFiniteBodies() == 0) continue;

                worstStep = step;
                break;
            }

            _out.WriteLine(
                $"{world.Creatures.Count} bodies, {steps} steps, " +
                $"{world.OverlapPairs} overlapping pairs, {world.OverlapBodies} body-steps touching, " +
                $"max link speed {world.MaxLinkSpeed():0.###} m/s");

            if (worstStep < 0) return;

            for (int i = 0; i < world.Creatures.Count; i++)
            {
                Creature lost = world.Creatures[i];
                if (lost.Alive) continue;

                _out.WriteLine(
                    $"lost {lost.Id} at step {worstStep}: {lost.Links} links, {lost.Dof} dof, " +
                    $"{lost.TotalMass:0.###} kg");
            }

            Assert.Equal(0, world.NonFiniteBodies());
        }
    }
}
