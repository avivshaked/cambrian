using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Evosim.Core;

namespace Evosim.Nursery
{
    /// <summary>
    /// What the nursery writes so that a screen can show who swam with whom and where a genome came from
    /// (design/nursery/HANDOFF.md, "What the screens need that the nursery does not write yet"): member ids, the tanks of each
    /// draw, and the rows of the three new result files. Every function here is pure: it reads what the search already holds and
    /// draws no random number of any stream, so a run writes every result file it wrote before, byte for byte, and these three beside them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The files</b> (all in the run's folder, all JSON lines, all results that repeat bit for bit):
    /// <see cref="DrawsFile"/> (the search's draws), <see cref="ExamDrawsFile"/> (the exam's draws, per brain) and
    /// <see cref="LineageFile"/> (one row per member per generation).
    /// </para>
    /// <para>
    /// <b>A member id</b> is <c>g&lt;generation&gt;.&lt;index&gt;</c>, the index in that generation's population as the search holds it
    /// (the order of <c>tank-scores.jsonl</c>'s members). An elite carried to the next generation takes the next generation's id for its
    /// new place, and its lineage row names the id it had before as its one parent. An exam genome is named by the id it had in the search;
    /// an exam with no search (a pool or founders benched as they are) names its genomes <c>p&lt;index in the pool&gt;</c>.
    /// </para>
    /// <para>
    /// <b>Seeds are written as decimal strings</b>, not numbers: they are 64 bits, and a reader that parses JSON numbers as doubles
    /// (a browser) would round them and then re-step the wrong episode.
    /// </para>
    /// </remarks>
    public static class TankLog
    {
        public const string DrawsFile = "tank-draws.jsonl";
        public const string ExamDrawsFile = "tank-exam-draws.jsonl";
        public const string BenchDrawsFile = "tank-bench-draws.jsonl";
        public const string LineageFile = "tank-lineage.jsonl";

        /// <summary>
        /// The draws file of an exam or a bench, named for it (<c>tank-&lt;name&gt;-draws.jsonl</c>): <see cref="ExamDrawsFile"/> for the exam that
        /// follows a search, <see cref="BenchDrawsFile"/> for a bench of a pool. The name is the one the rows file carries (<c>tank-&lt;name&gt;.jsonl</c>).
        /// </summary>
        public static string BenchOrExamDrawsFile(string name) => "tank-" + name + "-draws.jsonl";

        /// <summary>The salt of the shuffle that deals a draw's members into tanks (the stream <c>Rng.SeedFor(draw seed, ShuffleSalt)</c>).</summary>
        public const ulong ShuffleSalt = 0x73687566UL;

        /// <summary>The id of the member at <paramref name="index"/> of generation <paramref name="generation"/>'s population.</summary>
        public static string MemberId(int generation, int index) =>
            "g" + generation.ToString(CultureInfo.InvariantCulture) + "." + index.ToString(CultureInfo.InvariantCulture);

        /// <summary>The id an exam genome has when the exam has no search to take it from: its place in the pool.</summary>
        public static string PoolId(int index) => "p" + index.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// The tanks of every draw: for each seed, the members (indices into the population, 0 to <paramref name="population"/> - 1)
        /// shuffled by that seed's own stream and cut into tanks of <paramref name="tankSize"/>, the last one short when the
        /// size does not divide. <b>This is the one shuffle</b>: the search's and the exam's tanks are dealt by it, and the files are
        /// written from its result. It draws only from the stream of the draw's own seed and salt, and keeps no state.
        /// </summary>
        /// <returns>[draw][tank][slot] = member index.</returns>
        public static int[][][] PlanTanks(int population, int tankSize, ulong[] seeds)
        {
            var plan = new int[seeds.Length][][];
            for (int d = 0; d < seeds.Length; d++)
            {
                var order = Enumerable.Range(0, population).ToArray();
                var shuffle = new Rng(Rng.SeedFor(seeds[d], ShuffleSalt));
                for (int k = population - 1; k > 0; k--)
                {
                    int j = shuffle.Range(k + 1);
                    (order[k], order[j]) = (order[j], order[k]);
                }
                var tanks = new List<int[]>();
                for (int at = 0; at < population; at += tankSize) tanks.Add(order.Skip(at).Take(tankSize).ToArray());
                plan[d] = tanks.ToArray();
            }
            return plan;
        }

        /// <summary>One row of <see cref="DrawsFile"/>: a draw of a generation, its episode seed and its tanks in order, each the member ids that swam in it.</summary>
        public static string DrawRow(int generation, int draw, ulong seed, int[][] tanks, IReadOnlyList<string> ids)
        {
            var w = new Json.Writer(indent: false);
            w.BeginObject();
            w.Field("gen", generation);
            DrawBody(w, draw, seed, tanks, ids);
            return w.ToString();
        }

        /// <summary>
        /// One row of <see cref="ExamDrawsFile"/> (or <see cref="BenchDrawsFile"/>): the same for one of the exam's brains, named by its label and by its
        /// index in the exam's brains (<c>b</c>, the order of the plan's <c>brains</c> in run.json), since two brains may carry one label.
        /// </summary>
        public static string ExamDrawRow(string brain, int brainIndex, int draw, ulong seed, int[][] tanks, IReadOnlyList<string> ids)
        {
            var w = new Json.Writer(indent: false);
            w.BeginObject();
            w.Field("brain", brain);
            w.Field("b", brainIndex);
            DrawBody(w, draw, seed, tanks, ids);
            return w.ToString();
        }

        private static void DrawBody(Json.Writer w, int draw, ulong seed, int[][] tanks, IReadOnlyList<string> ids)
        {
            w.Field("draw", draw);
            w.Field("seed", seed.ToString(CultureInfo.InvariantCulture));
            w.BeginArray("tanks");
            foreach (int[] tank in tanks)
            {
                w.BeginArray();
                foreach (int member in tank) w.Value(ids[member]);
                w.EndArray();
            }
            w.EndArray();
            w.EndObject();
        }

        /// <summary>
        /// One row of <see cref="LineageFile"/>: a member of a generation, its body and birth generation, the ids of its parents in the
        /// generation before (none in generation 0; one for a mutated copy or an elite; two for a crossover that passed a route, the base
        /// parent first) and whether it is an elite carried unchanged.
        /// </summary>
        public static string LineageRow(int generation, string id, int body, int born, IReadOnlyList<string> parents, bool elite)
        {
            var w = new Json.Writer(indent: false);
            w.BeginObject();
            w.Field("gen", generation);
            w.Field("id", id);
            w.Field("body", body);
            w.Field("born", born);
            w.BeginArray("parents");
            foreach (string p in parents) w.Value(p);
            w.EndArray();
            w.Field("elite", elite);
            w.EndObject();
            return w.ToString();
        }

        /// <summary>The one parent of a mutated copy, and of an elite carried unchanged (itself, a generation ago).</summary>
        public static string[] CopyParents(string parentId) => new[] { parentId };

        /// <summary>
        /// The parents a child of a crossover step has: the base parent and the donor when a route was passed
        /// (<see cref="CrossoverRecord.Transferred"/>), the base parent alone when the crossover was not drawn or was refused (the child is
        /// then the base's copy, mutated or not).
        /// <para>
        /// The base and the donor can be one member: the tournament that picks the donor may pick the member that was the base, and the crossover then
        /// ran a genome against itself. Both ids are kept, the same one twice, as the honest record of what ran (a reader sees a crossover whose two
        /// parents are one member, and a row with the id once would read as a copy).
        /// </para>
        /// </summary>
        public static string[] CrossParents(string baseId, string donorId, CrossoverRecord record) =>
            record != null && record.Transferred ? new[] { baseId, donorId } : new[] { baseId };

        /// <summary>Writes one row a line and flushes, so that a live reader sees the generation that is swimming.</summary>
        public static void Append(TextWriter writer, string row)
        {
            writer.WriteLine(row);
            writer.Flush();
        }
    }
}
