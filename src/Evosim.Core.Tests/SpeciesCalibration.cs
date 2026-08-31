using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// Finds D057's θ by measuring it — the same "measure, don't guess" protocol
    /// <see cref="CalibrationSweep"/> uses for §5A.2's ratio, aimed at
    /// <see cref="SpeciesDistance"/> instead: mutate a reference world's founders the standard
    /// number of times and report the distance distribution a human places θ against, rather
    /// than guessing a number and hoping.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Reporting, not asserting a placement.</b> Nothing here decides θ — D057 is explicit
    /// that the number is calibrated by a human reading this distribution, several
    /// typical-mutation-lengths out from the bulk of it and at or below whatever a
    /// cell-type-changing mutation measures (so the ≥θ commitment in
    /// <see cref="RunConfig.SpeciesCellTypeWeight"/>'s doc comment is achievable without also
    /// catching every harmless parameter tweak). Run with <c>-ShowOutput</c> to see the table:
    /// <c>./scripts/core-test.ps1 -Filter SpeciesCalibration -ShowOutput</c>.
    /// </para>
    /// <para>
    /// Small and fast rather than skipped, like <see cref="CalibrationSweep"/>: every genome
    /// mutated here is a handful of nodes with no physics attached, so a few thousand trials
    /// costs milliseconds and there is no reason to keep it out of the default fast lane.
    /// </para>
    /// </remarks>
    public class SpeciesCalibration
    {
        private readonly ITestOutputHelper _output;

        public SpeciesCalibration(ITestOutputHelper output) => _output = output;

        /// <summary>Founder-generation genomes of a freshly seeded reference world.</summary>
        /// <remarks>Same shape as <c>WorldTests.FounderGenomes</c>, returning genomes rather than
        /// their serialized form since nothing here needs to compare across processes.</remarks>
        private static List<Genome> ReferenceFounders(ulong seed)
        {
            var config = new RunConfig { Light = new LightModel(300f, 12f), MinimumPopulation = 40 };
            var world = new World(config, seed);

            for (int i = 0; i < 60; i++) world.Step(1f);

            var genomes = new List<Genome>();
            foreach (Organism creature in world.Living) genomes.Add(creature.Genome);
            return genomes;
        }

        [Fact]
        public void SingleMutationDistancesInAReferenceWorld()
        {
            List<Genome> founders = ReferenceFounders(seed: 1);
            Assert.NotEmpty(founders);

            MutationRates standardRates = MutationRates.Default;
            CellTypeRegistry cellTypes = CellTypeRegistry.Standard;
            RandomGenomeOptions genomeOptions = RandomGenomeOptions.Default;
            var defaults = new RunConfig();

            // ~80 trials per founder against ~40 founders is a few thousand — the brief's own
            // figure — and small enough that this stays in the fast lane (class remarks).
            const int TrialsPerFounder = 80;

            var atDefaultWeights = new List<float>();
            var atAllTermWeights = new List<float>();
            ulong index = 0;

            for (int f = 0; f < founders.Count; f++)
            {
                Genome parent = founders[f];

                for (int t = 0; t < TrialsPerFounder; t++)
                {
                    ulong seed = Rng.SeedFor(1UL, index++);
                    Genome child = Mutator.Mutate(parent, new Rng(seed), standardRates, cellTypes, genomeOptions);

                    // The world's own weights — brain drift excluded by RunConfig's default —
                    // and, alongside it, every term switched on, which is what a round deciding
                    // whether to raise SpeciesBrainWeight would actually want to see.
                    atDefaultWeights.Add(SpeciesDistance.Between(
                        parent, child,
                        defaults.SpeciesCellTypeWeight, defaults.SpeciesTopologyWeight,
                        defaults.SpeciesParameterWeight, defaults.SpeciesBrainWeight));

                    atAllTermWeights.Add(SpeciesDistance.Between(parent, child, 1f, 1f, 1f, 1f));
                }
            }

            atDefaultWeights.Sort();
            atAllTermWeights.Sort();

            _output.WriteLine(
                $"{founders.Count} reference founders x {TrialsPerFounder} mutations each = " +
                $"{atDefaultWeights.Count} single-mutation trials, standard MutationRates.");
            _output.WriteLine("");
            _output.WriteLine("| weights | min | median | p90 | max |");
            _output.WriteLine("|---|---|---|---|---|");
            _output.WriteLine(Row("world default (brain off)", atDefaultWeights));
            _output.WriteLine(Row("all four terms at 1", atAllTermWeights));

            // A cell-type-changing mutation specifically, D057's own example: a one-node genome
            // so "one node's type changed" is unambiguous, with the cell-type operator forced on
            // — everything else stays at MutationRates.Default, so this still reads as an
            // ordinary birth rather than a term computed in isolation.
            var cellTypeRates = new MutationRates { CellTypeChance = 1f };
            var cellTypeDistances = new List<float>();
            Genome oneNode = Fixtures.SingleBox();

            for (int t = 0; t < 200; t++)
            {
                ulong seed = Rng.SeedFor(2UL, (ulong)t);
                Genome child = Mutator.Mutate(oneNode, new Rng(seed), cellTypeRates, cellTypes, genomeOptions);

                cellTypeDistances.Add(SpeciesDistance.Between(
                    oneNode, child,
                    defaults.SpeciesCellTypeWeight, defaults.SpeciesTopologyWeight,
                    defaults.SpeciesParameterWeight, defaults.SpeciesBrainWeight));
            }
            cellTypeDistances.Sort();

            _output.WriteLine(Row("cell-type mutation only", cellTypeDistances));
            _output.WriteLine("");
            _output.WriteLine(
                "Place SpeciesDriftThreshold several typical-mutation-lengths past the bulk of " +
                "the first row, and at or below the cell-type row's low end — that is what makes " +
                "SpeciesCellTypeWeight's >=theta commitment (RunConfig's doc comment) hold without " +
                "also catching ordinary parameter drift.");

            Assert.True(atDefaultWeights.Count > 0);
        }

        /// <summary>One row: label plus min/median/p90/max of a sorted distance list.</summary>
        private static string Row(string label, List<float> sorted) =>
            $"| {label} | {Percentile(sorted, 0f):0.####} | {Percentile(sorted, 0.5f):0.####} | " +
            $"{Percentile(sorted, 0.9f):0.####} | {Percentile(sorted, 1f):0.####} |";

        private static float Percentile(List<float> sorted, float fraction)
        {
            if (sorted.Count == 0) return 0f;

            int index = (int)Math.Round(fraction * (sorted.Count - 1));
            return sorted[Math.Max(0, Math.Min(sorted.Count - 1, index))];
        }
    }
}
