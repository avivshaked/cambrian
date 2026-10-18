using System;
using Evosim.Core;

namespace Evosim.Nursery
{
    /// <summary>What a knockout would cut from a genome, asked of the nursery's own knockout so that no reader can disagree with the exam.</summary>
    public static class KnockoutReach
    {
        private static readonly SensorChannel[] EveryChannel = (SensorChannel[])Enum.GetValues(typeof(SensorChannel));

        /// <summary>
        /// How many sense links (a central brain's) or sensor inputs (a per-part brain's) a knockout of <paramref name="channels"/> would cut from
        /// <paramref name="genome"/>; null means every channel, which is the <c>brainless</c> control. Zero means the control leaves the genome exactly
        /// as it is. It runs the exam's own knockout (<c>Program.KnockoutOf</c>, and <c>KnockoutCentral</c> for a central brain), developing the body under
        /// <paramref name="limits"/> and <paramref name="shapes"/>, which a central brain's ports are read from.
        /// </summary>
        public static int Count(
            Genome genome, SensorChannel[] channels, DevelopmentLimits limits, PartShapeRegistry shapes,
            CellTypeRegistry cellTypes = null)
        {
            // A per-part brain's knockout has no null: every channel is listed. A central brain's takes null as every port.
            SensorChannel[] listed = channels ?? (genome.Brain != null ? null : EveryChannel);
            Program.KnockoutOf(genome, listed, g => Developer.Develop(g, limits, null, shapes, cellTypes: cellTypes), out int cut);
            return cut;
        }
    }
}
