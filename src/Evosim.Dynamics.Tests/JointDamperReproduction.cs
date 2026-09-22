using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// The case that found the explicit joint damper, kept as the guard on its repair.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>DynamicsWorld.JointTorques</c> applies <c>-JointDriveDamping * rate</c>, and until
    /// 2026-09-21 it applied it from the previous step's rate alone. A viscous damper integrated
    /// explicitly grows rather than decays once <c>c dt / I &gt; 2</c>, and
    /// <c>PhenotypeBuilder.MakeDrive</c>'s 1 N·m·s/rad at dt 0.01 makes that bound
    /// <c>I &lt; 0.005 kg·m2</c>. Round 42's thinner adult links are under it and every newborn
    /// is far under it: a 0.4 kg two-link body at a third of adult size carries about 5e-4 and
    /// sits twenty times past the bound.
    /// </para>
    /// <para>
    /// <b>Nothing in still water ever showed it.</b> The spike's stability arm read 1,000 of
    /// 1,000 at dt 0.01 because round 42's bodies hold a pose: the joint rates stay at zero and
    /// an unstable damper has nothing to amplify. One touch is the whole seed. Body 710 of round
    /// 42 seed 4's snapshot — 19.4 kg over five links with four two-degree joints — was lost 25
    /// steps after its sphere was put 0.13 m inside body 427's, alone in an otherwise empty
    /// world, its root's spin doubling every step from 0.19 rad/s. Softening the contact spring
    /// delayed the loss and did not prevent it; the limit spring's stiffness changed nothing, to
    /// the last digit, because no limit was reached; halving the step prevented it, and so did
    /// dropping the damper to 0.2.
    /// </para>
    /// <para>
    /// The repair is the one the limit spring already had: <c>-damping * (rate + qdd * dt)</c>
    /// is the same torque written against the acceleration the solve is about to produce, so
    /// <c>damping * dt</c> goes on the inward pass's diagonal beside the limit's term and the
    /// explicit part is unchanged. It is unconditionally stable in the damping, and it moves
    /// every trajectory the spike recorded.
    /// </para>
    /// </remarks>
    public sealed class JointDamperReproduction
    {
        private readonly ITestOutputHelper _out;

        public JointDamperReproduction(ITestOutputHelper output) => _out = output;

        [Fact]
        public void TheBodyTheExplicitJointDamperLostSurvivesAMinuteOfBeingTouched()
        {
            Assert.True(RunFixture.Present, RunFixture.Why);

            SolverConfig solver = RunFixture.Solver();

            // The pair, taken out of the crowded scatter that put them together and stepped
            // alone, so what this measures is two bodies touching and nothing else. Bodies 710
            // and 427 were round 42 seed 4's pair; that recording is unreadable since format 7
            // (2026-09-22), so the pair is now found in the fixture's own scatter: the deepest
            // overlap in which the lighter body is jointed, which is the shape the damper lost.
            DynamicsWorld crowd = RunFixture.Scatter(solver, 1000, threads: 1, crowd: true);
            Creature light = null, heavy = null;
            double penetration = 0;

            for (int i = 0; i < crowd.Creatures.Count; i++)
            {
                for (int j = i + 1; j < crowd.Creatures.Count; j++)
                {
                    Creature a = crowd.Creatures[i], b = crowd.Creatures[j];
                    Creature lighter = a.TotalMass <= b.TotalMass ? a : b;
                    if (lighter.Links < 2 || lighter.Dof < 1) continue;

                    double overlap = a.ContactRadius + b.ContactRadius -
                                     (a.ContactCentre - b.ContactCentre).Magnitude;
                    if (overlap <= penetration) continue;

                    penetration = overlap;
                    light = lighter;
                    heavy = lighter == a ? b : a;
                }
            }

            Assert.True(light != null, "no jointed body overlaps another in the scatter, so this tests nothing");

            _out.WriteLine(
                $"{light.Id}: {light.TotalMass:0.##} kg, {light.Links} links, {light.Dof} dof, r {light.ContactRadius:0.##}; " +
                $"{heavy.Id}: {heavy.TotalMass:0.##} kg, r {heavy.ContactRadius:0.##}; " +
                $"overlap {penetration:0.###} m");

            var world = new DynamicsWorld(solver);
            world.AddInIdOrder(heavy);
            world.AddInIdOrder(light);

            for (int step = 1; step <= 6000; step++)
            {
                world.Step();

                Assert.True(
                    light.Alive && heavy.Alive,
                    $"lost a body at step {step} — the explicit joint damper lost this pair at 25");
            }

            _out.WriteLine($"both alive at 60 s, max link speed {world.MaxLinkSpeed():0.####} m/s");
        }
    }
}
