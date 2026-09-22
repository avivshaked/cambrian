using System;
using System.Collections.Generic;

namespace Evosim.Theatre
{
    /// <summary>
    /// The honesty check, as a thing rather than as a method: at every recorded sample time, is
    /// this world the recorded world?
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The five columns are <see cref="RunSample"/>'s and the rule is
    /// <see cref="TheatreReplay"/>'s.</b> <c>alive</c>, <c>births</c> and <c>deaths</c> are the
    /// population's whole history in three integers; <c>auditResidual</c> is a double accumulated
    /// over millions of steps and is therefore the strictest available test of bit-identity; and
    /// <c>meanHeight</c> is the only positional quantity a stats row carries, so a replay whose
    /// physics parted in the third decimal shows here first and in the counts much later, or
    /// never. Compared exactly, including the two doubles: <c>Json.Writer</c> writes them with
    /// "R", the shortest string that parses back to the same bits, so a stored sample and a live
    /// one are comparable to the last bit.
    /// </para>
    /// <para>
    /// <b>It exists because there are two engines now.</b> <see cref="TheatreReplay"/> keeps its
    /// own copy of this logic, untouched, so that a recording the Editor made replays through
    /// exactly the code that has always replayed it; <see cref="TheatreDynamicsReplay"/> uses
    /// this. The next hand through here should make the first call the second, once a dynamics
    /// replay has been run against enough recordings to have earned the churn.
    /// </para>
    /// <para>
    /// <b>It is told the census and decides nothing about the world.</b> Nothing here steps,
    /// drains or writes: the caller hands it a census taken at a metabolic step and it says
    /// whether the record had something to say about that instant.
    /// </para>
    /// </remarks>
    public sealed class ReplayIdentity
    {
        private readonly IReadOnlyList<RunSample> _samples;
        private int _next;

        /// <summary>Samples compared and found equal.</summary>
        public int Matched { get; private set; }

        /// <summary>Samples compared, equal or not — the denominator of a verdict so far.</summary>
        public int Compared { get; private set; }

        /// <summary>Samples in the record the replay stepped past without landing on.</summary>
        public int Skipped { get; private set; }

        /// <summary>The first disagreement with the record, or null while there is none.</summary>
        public string FirstMismatch { get; private set; }

        /// <summary>How many samples the record holds.</summary>
        public int Recorded => _samples.Count;

        /// <summary>The five columns, in the order <see cref="FirstDifference"/> asks them.</summary>
        public static readonly string[] Columns =
        {
            "alive", "births", "deaths", "audit", "meanHeight",
        };

        private readonly int[] _columnMatched = new int[Columns.Length];

        /// <summary>
        /// How many of the compared samples each column agreed on, in <see cref="Columns"/> order.
        /// </summary>
        /// <remarks>
        /// <b>Because "0 of 30" is the least informative true thing that can be said.</b> The
        /// verdict is all five columns or nothing, and it has to be — a replay that agrees about
        /// the population and not about the joules is not the run. But which columns held is the
        /// whole of the diagnosis when it does not: three counts identical across every sample
        /// while a double parts in the tenth decimal is a rounding difference amplifying, and a
        /// count that parts is a different world. Every column is asked of every compared sample,
        /// where <see cref="FirstDifference"/> stops at the first.
        /// </remarks>
        public IReadOnlyList<int> ColumnMatched => _columnMatched;

        public ReplayIdentity(IReadOnlyList<RunSample> samples)
        {
            _samples = samples ?? throw new ArgumentNullException(nameof(samples));
        }

        /// <summary>
        /// Offers the census taken at this metabolic step to the record. Returns true when the
        /// record held a sample at this instant and it was compared.
        /// </summary>
        /// <param name="census">The live world, counted as the run's own report counted it.</param>
        /// <param name="difference">
        /// The first column that disagreed, or null when every one of the five agreed.
        /// </param>
        /// <param name="recorded">The sample that was compared, when one was.</param>
        public bool Offer(WorldCensus census, out string difference, out RunSample recorded)
        {
            difference = null;
            recorded = default;

            if (_next >= _samples.Count) return false;

            double t = census.T;

            // A sample the replay has stepped past without landing on. Both clocks advance by the
            // metabolic step from zero, so this should not happen; if it does, the run's samples
            // are on a different grid and saying so is more use than silently skipping them.
            while (_next < _samples.Count && _samples[_next].T < t - 1e-6)
            {
                Skipped++;
                _next++;
            }

            if (_next >= _samples.Count) return false;

            recorded = _samples[_next];
            if (Math.Abs(recorded.T - t) > 1e-6) return false;

            _next++;
            Compared++;

            if (recorded.Alive == census.Alive) _columnMatched[0]++;
            if (recorded.Births == census.Births) _columnMatched[1]++;
            if (recorded.Deaths == census.Deaths) _columnMatched[2]++;
            if (recorded.AuditResidual == census.AuditResidual) _columnMatched[3]++;
            if (recorded.MeanHeight == census.MeanHeight) _columnMatched[4]++;

            difference = FirstDifference(recorded, census);

            if (difference == null)
            {
                Matched++;
                return true;
            }

            if (FirstMismatch == null)
            {
                FirstMismatch = $"t={recorded.T:0.#}: {difference}";
            }

            return true;
        }

        /// <summary>The first of the five columns that disagrees, or null.</summary>
        public static string FirstDifference(RunSample recorded, WorldCensus census)
        {
            if (recorded.Alive != census.Alive)
            {
                return $"alive {recorded.Alive} recorded, {census.Alive} here";
            }

            if (recorded.Births != census.Births)
            {
                return $"births {recorded.Births} recorded, {census.Births} here";
            }

            if (recorded.Deaths != census.Deaths)
            {
                return $"deaths {recorded.Deaths} recorded, {census.Deaths} here";
            }

            if (recorded.AuditResidual != census.AuditResidual)
            {
                return $"audit {recorded.AuditResidual:R} recorded, {census.AuditResidual:R} here";
            }

            if (recorded.MeanHeight != census.MeanHeight)
            {
                return $"mean depth {recorded.MeanHeight:R} recorded, {census.MeanHeight:R} here";
            }

            return null;
        }

        /// <summary>One line for a HUD or a log: what the check currently says.</summary>
        public string Line()
        {
            if (_samples.Count == 0) return "identity: no recorded samples to check against";

            if (FirstMismatch != null)
            {
                return $"identity: MISMATCH — {FirstMismatch} ({Matched} matched before it)";
            }

            if (Matched == 0)
            {
                return $"identity: no sample reached yet (first at t={_samples[0].T:0.#})";
            }

            string skipped = Skipped > 0 ? $", {Skipped} skipped" : "";
            return $"identity: {Matched} of {_samples.Count} samples match{skipped}";
        }

        /// <summary>The per-column tally as one line: <c>alive 30/30, births 30/30, …</c>.</summary>
        public string ByColumn()
        {
            var sb = new System.Text.StringBuilder();

            for (int i = 0; i < Columns.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(Columns[i]).Append(' ').Append(_columnMatched[i]).Append('/').Append(Compared);
            }

            return sb.ToString();
        }
    }
}
