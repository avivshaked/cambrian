using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Evosim.Core;

namespace Evosim.Nursery
{
    /// <summary>One member of a search's last generation, as <c>tank-population.jsonl</c> keeps it.</summary>
    public sealed class PopulationMember
    {
        public Genome Genome;
        public int Body, Born, Episodes;

        /// <summary>The reward's value over every episode it has swum; NaN when it never landed.</summary>
        public double Score = double.NaN;

        /// <summary>The mean net energy over its landed episodes in the last generation; NaN when none.</summary>
        public double Net = double.NaN;
    }

    /// <summary>
    /// The search's last generation, whole. The owner ruled on 2026-10-04 that every nursery run keeps it,
    /// so that a later round can take the best ten creatures per body. Written once, by the loop's last
    /// generation; it draws nothing and no other file reads it.
    /// </summary>
    public static class TankPopulation
    {
        public const string FileName = "tank-population.jsonl";

        /// <summary>One line per member, by body and then by score, best first (a member that never landed last).</summary>
        public static IEnumerable<string> Rows(int gen, IEnumerable<PopulationMember> population) =>
            population
                .OrderBy(p => p.Body)
                .ThenByDescending(p => double.IsNaN(p.Score) ? double.NegativeInfinity : p.Score)
                .Select(p => FormattableString.Invariant(
                    $"{{\"gen\":{gen},\"body\":{p.Body},\"born\":{p.Born},\"score\":{Num(p.Score)},\"episodes\":{p.Episodes},\"net\":{Num(p.Net)},\"genome\":{GenomeJson.Write(p.Genome)}}}"));

        public static void Write(string folder, int gen, IEnumerable<PopulationMember> population)
        {
            using var file = new StreamWriter(Path.Combine(folder, FileName), false, new UTF8Encoding(false));
            foreach (string row in Rows(gen, population)) file.WriteLine(row);
        }

        // The same text as Program.Num, which the tank's other rows use: null for NaN or infinity, else round-trip.
        private static string Num(double v) => !double.IsFinite(v) ? "null" : v.ToString("R", CultureInfo.InvariantCulture);
    }
}
