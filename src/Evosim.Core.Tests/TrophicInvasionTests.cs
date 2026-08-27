using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// Can a second trophic level appear once the larder can feed one? — DESIGN.md §5A.3, §8.
    /// </summary>
    /// <remarks>
    /// <para>
    /// §8 demotes MAP-Elites on the grounds that under endogenous selection its
    /// innovation-protection role "passes to ecological niches — spatial and trophic — maintained
    /// by the depth/light gradient and cell-type mutation". That is a claim about a mutation rate,
    /// and this measures it.
    /// </para>
    /// <para>
    /// Why it matters now rather than in the abstract: senescence (D038) fills the water with
    /// corpses, so nutrient density where creatures live rose from 0.18 to 2.1–4.3 J/m³ and the
    /// gap to the absorptive break-even of 8 J/m³ closed from forty-fourfold to two- or
    /// fourfold. The trophic niche is opening. Every run measured so far has zero absorptive
    /// creatures alive to enter it (logbook/0024).
    /// </para>
    /// </remarks>
    public class TrophicInvasionTests
    {
        private readonly ITestOutputHelper _output;

        public TrophicInvasionTests(ITestOutputHelper output) => _output = output;

        /// <summary>A plain photosynthetic body, which is what the surviving population is.</summary>
        private static Genome Photosynthesiser()
        {
            var genome = Fixtures.SingleBox();
            genome.Nodes[0].CellTypeId = CellTypeIds.Photosynthetic;
            return genome;
        }

        private static bool HasAbsorptive(Genome genome)
        {
            for (int i = 0; i < genome.Nodes.Count; i++)
            {
                if (genome.Nodes[i].CellTypeId == CellTypeIds.Absorptive) return true;
            }
            return false;
        }

        [Fact]
        public void ReinvasionByMutationIsRareEnoughToNotHappenInARun()
        {
            // The rate that decides whether §8's bet can pay. A niche that opens and is never
            // entered is not a niche the world can use, and "cell-type mutation maintains the
            // trophic niche" is only true if the arrival rate is fast against a run.
            //
            // Measured on single-generation mutants of a photosynthesiser, which is the actual
            // question: given one birth, what is the chance the child can eat? Compounding over
            // generations does not help here, because a mutant that cannot pay its way dies
            // before it breeds — the lineage does not accumulate the trait, it re-rolls it.
            const int Births = 200_000;

            var rates = MutationRates.Default;
            var rng = new Rng(20260826);
            Genome parent = Photosynthesiser();

            int arrivals = 0;
            for (int i = 0; i < Births; i++)
            {
                if (HasAbsorptive(Mutator.Mutate(parent, rng, rates))) arrivals++;
            }

            double perBirth = (double)arrivals / Births;
            double birthsPerArrival = arrivals > 0 ? Births / (double)arrivals : double.PositiveInfinity;

            _output.WriteLine(
                $"CellTypeChance {rates.CellTypeChance}: {arrivals} absorptive mutants in " +
                $"{Births:N0} births — {perBirth:P4}, one per {birthsPerArrival:N0} births");

            // Not an assertion that the rate is wrong — it is the rate the design asked for. The
            // assertion is that it is slow against the runs actually being done, which is the
            // fact §8's bet depends on and which nothing else states. A run producing 1,000–3,000
            // births cannot expect an arrival; if this ever becomes false, the reasoning in
            // logbook/0024 needs revisiting rather than quietly continuing to be cited.
            Assert.True(
                birthsPerArrival > 1_000d,
                $"one absorptive mutant per {birthsPerArrival:N0} births is frequent enough that " +
                "re-invasion is no longer the bottleneck, and logbook/0024's diagnosis is stale");
        }

        [Fact]
        public void TheFounderDrawIsTheOnlyFastSourceOfAbsorptiveCreatures()
        {
            // Where absorptive creatures actually come from: the founder draw, at one in four
            // (RandomGenomeOptions.FounderCellTypes), which is three orders of magnitude faster
            // than the mutation route above.
            //
            // That is the ordering problem in logbook/0024. The floor is the only thing that
            // draws founders, and D021 makes it fire only to hold the population up — so it goes
            // silent exactly when the world becomes self-sustaining, which is thousands of
            // seconds before the larder is rich enough to feed anything absorptive. The world's
            // supply of consumers is switched off by its own success.
            var options = new RandomGenomeOptions();
            var rng = new Rng(4242);

            const int Draws = 4_000;
            int withAbsorptive = 0;

            for (int i = 0; i < Draws; i++)
            {
                if (HasAbsorptive(GenomeFactory.Founder(rng, options))) withAbsorptive++;
            }

            double share = (double)withAbsorptive / Draws;
            _output.WriteLine($"founder draw: {share:P1} of {Draws:N0} carry an absorptive part");

            Assert.True(
                share > 0.05d,
                $"only {share:P1} of founders carry an absorptive part, so the floor is not the " +
                "fast source logbook/0024 says it is");
        }
    }
}
