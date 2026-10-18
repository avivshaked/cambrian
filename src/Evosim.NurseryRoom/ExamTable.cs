using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Evosim.NurseryRoom
{
    /// <summary>
    /// The exam table, computed from <c>tank-exam.jsonl</c>. Columns are the brain labels in the order
    /// the file first names them, so a new control appears with no change here; a row is one genome
    /// (its body beside it); a cell is the genome's own brain less that brain, paired.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The basis.</b> <c>per</c> in a row is the net of each held-out draw; <c>score</c> is the
    /// reward's value. They are one number only when the reward is Net. So the cells are differences of
    /// what the run selected for: when the run's reward is <c>Net</c>, or the run has no recipe (it
    /// predates recipes, when the nursery ranked by net), a cell is the mean of the draw-by-draw
    /// differences of <c>per</c>, with that difference's own standard error over the draws both landed.
    /// For any other reward a cell is the difference of the two <c>score</c>s: one number a genome, so
    /// no standard error and no tint, and the room does not make one up. A Net run whose rows lack
    /// <c>per</c> falls back to <c>score</c>, then <c>net</c>, on the same terms.
    /// </para>
    /// <para>
    /// <b>The pooled row and the own column</b> use the basis of the cells: genome by genome on
    /// <c>score</c> when the basis is <c>score</c>, on <c>net</c> otherwise, with the standard error
    /// across genomes, as the nursery's own log line takes it.
    /// </para>
    /// <para>A cell clears two standard errors when its difference is more than twice its standard error.</para>
    /// </remarks>
    public static class ExamTable
    {
        public sealed class Cell
        {
            public string Brain;
            public double? Net, Score, Fresh;
            /// <summary>Own less this brain; null for own itself and for a brain the genome has no row for.</summary>
            public double? Diff, Se;
            public int N;
            public bool Clears;
        }

        public sealed class Body
        {
            public int Genome;
            /// <summary>The row's <c>body</c>, or null when the rows carry none: the genome's number is not a body's.</summary>
            public int? BodyIndex;
            public double? OwnNet, OwnScore, OwnFresh;
            /// <summary>The own brain's net of each held-out draw (the row's <c>per</c>), with the draws that did not land left out; null when the row has none.</summary>
            public double[] OwnPer;
            public List<Cell> Cells = new List<Cell>();
        }

        public sealed class Result
        {
            public List<string> Brains = new List<string>();
            public string Own;
            /// <summary>What the cells are differences of: <c>per</c> (net per draw, paired), <c>score</c> (the reward's value) or <c>net</c>.</summary>
            public string Basis;
            public bool HasScore, HasFresh;
            /// <summary>The most draws any row's <c>per</c> holds (0 when no row has one): how many episodes of each brain the exam swam, for the tank screen's draw picker.</summary>
            public int Draws;
            public List<Body> Bodies = new List<Body>();
            public List<Cell> Pooled = new List<Cell>();
        }

        /// <param name="rows">The exam file's rows.</param>
        /// <param name="ownLabel">The own brain's label: the recipe's first control. Null takes <c>own</c>, or the first brain the file names.</param>
        /// <param name="rewardIsNet">True when the run's reward is Net or the run has no recipe; false pairs on <c>score</c>.</param>
        public static Result Build(IReadOnlyList<JsonElement> rows, string ownLabel = null, bool rewardIsNet = true)
        {
            var r = new Result();
            var byGenome = new SortedDictionary<int, Dictionary<string, JsonElement>>();
            var bodyOf = new Dictionary<int, int>();
            foreach (JsonElement row in rows)
            {
                string brain = Js.Str(row, "brain");
                double? genome = Js.Num(row, "genome");
                if (brain == null || !genome.HasValue) continue;
                if (!r.Brains.Contains(brain)) r.Brains.Add(brain);
                int g = (int)genome.Value;
                if (!byGenome.TryGetValue(g, out var m)) byGenome[g] = m = new Dictionary<string, JsonElement>();
                m[brain] = row;
                double? body = Js.Num(row, "body");
                if (body.HasValue) bodyOf[g] = (int)body.Value;
                if (Js.Num(row, "score").HasValue) r.HasScore = true;
                if (Js.Num(row, "fresh").HasValue) r.HasFresh = true;
                JsonElement? per = Js.At(row, "per");
                if (per.HasValue && per.Value.ValueKind == JsonValueKind.Array) r.Draws = Math.Max(r.Draws, per.Value.GetArrayLength());
            }
            if (r.Brains.Count == 0) return r;
            string wanted = ownLabel ?? "own";
            r.Own = r.Brains.Contains(wanted) ? wanted : r.Brains[0];
            r.Basis = !rewardIsNet ? "score"
                : rows.Any(x => Js.At(x, "per").HasValue) ? "per"
                : r.HasScore ? "score" : "net";

            foreach (var kv in byGenome)
            {
                var b = new Body { Genome = kv.Key, BodyIndex = bodyOf.TryGetValue(kv.Key, out int bi) ? bi : (int?)null };
                kv.Value.TryGetValue(r.Own, out JsonElement own);
                bool hasOwn = kv.Value.ContainsKey(r.Own);
                if (hasOwn)
                {
                    b.OwnNet = Js.Num(own, "net"); b.OwnScore = Js.Num(own, "score"); b.OwnFresh = Js.Num(own, "fresh");
                    double?[] draws = Per(own);
                    if (draws != null) b.OwnPer = draws.Where(x => x.HasValue).Select(x => x.Value).ToArray();
                }
                foreach (string brain in r.Brains)
                {
                    var c = new Cell { Brain = brain };
                    if (kv.Value.TryGetValue(brain, out JsonElement row))
                    {
                        c.Net = Js.Num(row, "net"); c.Score = Js.Num(row, "score"); c.Fresh = Js.Num(row, "fresh");
                        if (brain != r.Own && hasOwn) Pair(own, row, c, r.Basis);
                    }
                    b.Cells.Add(c);
                }
                r.Bodies.Add(b);
            }

            foreach (string brain in r.Brains)
            {
                var c = new Cell { Brain = brain };
                if (brain != r.Own)
                {
                    var diffs = new List<double>();
                    foreach (var kv in byGenome)
                    {
                        if (!kv.Value.TryGetValue(r.Own, out JsonElement own) || !kv.Value.TryGetValue(brain, out JsonElement row)) continue;
                        double? a = PooledValue(own, r.Basis), x = PooledValue(row, r.Basis);
                        if (a.HasValue && x.HasValue) diffs.Add(a.Value - x.Value);
                    }
                    Summarise(diffs, c);
                }
                r.Pooled.Add(c);
            }
            return r;
        }

        /// <summary>One genome's number under a basis, for the pooled row: the reward's value under <c>score</c>, otherwise net (the mean of its draws).</summary>
        private static double? PooledValue(JsonElement row, string basis) =>
            basis == "score" ? Js.Num(row, "score") : Js.Num(row, "net") ?? Js.Num(row, "score");

        private static void Pair(JsonElement own, JsonElement ctl, Cell c, string basis)
        {
            if (basis == "per")
            {
                var po = Per(own); var pc = Per(ctl);
                if (po != null && pc != null && po.Length == pc.Length)
                {
                    var diffs = new List<double>();
                    for (int i = 0; i < po.Length; i++)
                        if (po[i].HasValue && pc[i].HasValue) diffs.Add(po[i].Value - pc[i].Value);
                    if (diffs.Count > 0) { Summarise(diffs, c); return; }
                }
            }
            // One number a genome: the reward's value (basis score), or what the Net rows without draws hold.
            double? a = basis == "score" ? Js.Num(own, "score") : Js.Num(own, "score") ?? Js.Num(own, "net");
            double? x = basis == "score" ? Js.Num(ctl, "score") : Js.Num(ctl, "score") ?? Js.Num(ctl, "net");
            if (a.HasValue && x.HasValue) { c.Diff = a.Value - x.Value; c.Se = null; c.N = 1; c.Clears = false; }
        }

        private static double?[] Per(JsonElement row)
        {
            JsonElement? per = Js.At(row, "per");
            if (!per.HasValue || per.Value.ValueKind != JsonValueKind.Array) return null;
            return per.Value.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.Number && e.TryGetDouble(out double d) && double.IsFinite(d) ? d : (double?)null).ToArray();
        }

        /// <summary>The mean of the differences and its standard error (n - 1 in the variance); no standard error from fewer than two.</summary>
        public static void Summarise(IReadOnlyList<double> diffs, Cell c)
        {
            c.N = diffs.Count;
            if (diffs.Count == 0) return;
            double mean = diffs.Average();
            c.Diff = mean;
            if (diffs.Count < 2) return;
            double ss = diffs.Sum(d => (d - mean) * (d - mean));
            double se = Math.Sqrt(ss / (diffs.Count - 1) / diffs.Count);
            c.Se = se;
            c.Clears = Math.Abs(mean) > 2 * se;
        }
    }
}
