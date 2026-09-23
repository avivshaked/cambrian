using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Evosim.Core;

namespace Evosim.Theatre
{
    /// <summary>
    /// One clade as the guide describes it: the card's facts the director reads, and nothing it
    /// does not.
    /// </summary>
    /// <remarks>
    /// A clade is known to the director by its <b>founder's body id</b>, never by the guide's own
    /// numbering: the id is what the lineage and a restored world both carry, so a clade in the
    /// guide and a body on screen meet on it (<see cref="SafariClades"/>). Every optional fact is
    /// NaN, null or empty when the guide did not write it, and a caption template that needs it
    /// is skipped rather than filled with a default.
    /// </remarks>
    public sealed class SafariClade
    {
        public long Founder;
        public string Name;
        public int Rank;
        public double Score;
        public double FoundedAt;
        /// <summary>The parent clade's founder id, or -1 for a clade founded by a founder with no parent.</summary>
        public long ParentClade = -1;
        /// <summary>The founder's own parent body, or -1.</summary>
        public long ParentBody = -1;
        public bool Absorptive, Jointed, Photosynthetic;
        public double BestSecond;

        public int MembersEver = -1;
        public int PeakCount = -1;
        public double PeakAt = double.NaN;
        public double PeakShare = double.NaN;
        public int GenerationDepth = -1;
        public bool? AliveAtEnd;
        public double ExtinctAt = double.NaN;
        public string Split;
        public double MedianDepth = double.NaN;
        public double MedianRadius = double.NaN;
        public double MedianAboveFloor = double.NaN;
        public double AdultVolume = double.NaN;
        public int Parts = -1;
        public double Speed = double.NaN;
        public long Exemplar = -1;
        public readonly List<string> Firsts = new List<string>();

        /// <summary>The flag triple as the guild's short word: a, j, p for each flag held.</summary>
        public string Guild =>
            (Absorptive ? "a" : "-") + (Jointed ? "j" : "-") + (Photosynthetic ? "p" : "-");

        /// <summary>Alive at a second, from the founding and the extinction, as the guide knows it.</summary>
        public bool AliveAt(double seconds) =>
            seconds >= FoundedAt && (double.IsNaN(ExtinctAt) || seconds < ExtinctAt);

        /// <summary>A stable hash of the founder id, for choosing a plan's family.</summary>
        public int Hash
        {
            get
            {
                ulong x = (ulong)Founder * 0x9E3779B97F4A7C15UL;
                x ^= x >> 31;
                return (int)(x & 0x7FFFFFFF);
            }
        }
    }

    /// <summary>
    /// A run's <c>guide/guide.json</c>, read for the director (safari-spec.md item 5).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The guide is another agent's, written in parallel</b> (<c>scripts/guide.py</c>), so this
    /// reader is written against the spec's item 5 and not against a file: it asks for the few
    /// keys the director cannot work without, accepts the camelCase and snake_case spellings of
    /// each, ignores every key it does not know, and refuses a guide that lacks a required one
    /// with a message naming every missing key and every spelling it would have taken.
    /// </para>
    /// <para>
    /// <b>Required at the top</b>: <c>clades</c> (an array of cards), and the ranking (clade
    /// founder ids by score; or, when absent, each card's <c>rank</c> decides it). <b>Required on
    /// each card</b>: the founder's body id, the name, the rank or score, the founding second,
    /// the parent clade (null for none), the three flags, and the best second. The picker list
    /// is optional: without it, every card with ten members ever (or every card, when the card
    /// does not say) is in the picker, which is item 4's rule.
    /// </para>
    /// </remarks>
    public sealed class SafariGuide
    {
        public string Path { get; private set; }
        public string Arm { get; private set; }
        public IReadOnlyList<SafariClade> Clades => _clades;
        /// <summary>Founder ids in the guide's ranking order, best first.</summary>
        public IReadOnlyList<long> Ranking { get; private set; }
        /// <summary>Founder ids the picker offers.</summary>
        public IReadOnlyList<long> Picker { get; private set; }
        /// <summary>Keys this reader did not use, for the log (tolerated, never an error).</summary>
        public IReadOnlyList<string> Ignored { get; private set; }

        private readonly List<SafariClade> _clades = new List<SafariClade>();
        private readonly Dictionary<long, SafariClade> _byFounder = new Dictionary<long, SafariClade>();

        public SafariClade Find(long founder) => _byFounder.TryGetValue(founder, out SafariClade c) ? c : null;

        public SafariClade FindByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            string wanted = name.Trim();
            foreach (SafariClade c in _clades)
            {
                if (string.Equals(c.Name, wanted, StringComparison.OrdinalIgnoreCase)) return c;
            }
            return null;
        }

        // ---------------------------------------------------------------- where it lives

        /// <summary>
        /// The guide beside a run: <c>&lt;run&gt;/guide/guide.json</c>, or the file named by
        /// <c>EVOSIM_THEATRE_SAFARI_GUIDE</c>, which wins. Null and a reason when there is none.
        /// </summary>
        public static string Locate(string runDirectory, out string why)
        {
            why = null;
            string named = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SAFARI_GUIDE");
            if (!string.IsNullOrWhiteSpace(named))
            {
                string p = named.Trim().Trim('"');
                if (File.Exists(p)) return p;
                why = "EVOSIM_THEATRE_SAFARI_GUIDE names '" + p + "', which does not exist.";
                return null;
            }

            if (string.IsNullOrWhiteSpace(runDirectory))
            {
                why = "no run directory, so no guide beside it.";
                return null;
            }

            string run;
            try { run = RunRecord.ResolveRunDirectory(runDirectory); }
            catch (Exception e) { why = e.Message; return null; }

            string path = System.IO.Path.Combine(System.IO.Path.Combine(run ?? runDirectory, "guide"), "guide.json");
            if (File.Exists(path)) return path;

            why = "no guide at '" + path + "': run scripts/guide.py <arm> first, or name one with " +
                  "EVOSIM_THEATRE_SAFARI_GUIDE.";
            return null;
        }

        // ---------------------------------------------------------------- reading

        private static readonly string[] FounderKeys = { "founder", "founderId", "founder_id" };
        private static readonly string[] NameKeys = { "name", "binomial" };
        private static readonly string[] RankKeys = { "rank" };
        private static readonly string[] ScoreKeys = { "score", "interest", "interesting" };
        private static readonly string[] FoundedKeys = { "foundedAt", "founded_at", "founded" };
        private static readonly string[] ParentCladeKeys = { "parentClade", "parent_clade", "fromClade", "from_clade" };
        private static readonly string[] FlagsKeys = { "flags", "guild" };
        private static readonly string[] BestKeys = { "bestSecond", "best_second", "best" };

        /// <summary>Reads a guide, or refuses it with every missing key named.</summary>
        public static SafariGuide Read(string path, out string refusal)
        {
            refusal = null;
            JsonNode root;

            try
            {
                root = Json.Parse(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                refusal = "the guide at '" + path + "' does not parse: " + e.Message;
                return null;
            }

            try
            {
                return Build(root, path, out refusal);
            }
            catch (Exception e)
            {
                refusal = "the guide at '" + path + "' is not a guide: " + e.GetType().Name + ": " + e.Message;
                return null;
            }
        }

        /// <summary>Reads a guide from text; the tests' door.</summary>
        public static SafariGuide Parse(string text, string path, out string refusal)
        {
            refusal = null;
            try { return Build(Json.Parse(text), path, out refusal); }
            catch (Exception e) { refusal = e.GetType().Name + ": " + e.Message; return null; }
        }

        private static SafariGuide Build(JsonNode root, string path, out string refusal)
        {
            refusal = null;
            var missing = new List<string>();
            var ignored = new HashSet<string>(StringComparer.Ordinal);

            if (root.Kind != JsonNode.NodeKind.Object)
            {
                refusal = "the guide at '" + path + "' is not a JSON object.";
                return null;
            }

            JsonNode clades = Get(root, missing, "the top level", "clades", "cards");

            var guide = new SafariGuide { Path = path };
            guide.Arm = Optional(root, "arm")?.AsString();

            var known = new HashSet<string>(StringComparer.Ordinal)
            {
                "clades", "cards", "arm", "ranking", "rank", "order", "picker", "pickerList", "picker_list",
            };
            foreach (string k in root.Keys()) if (!known.Contains(k)) ignored.Add(k);

            if (clades != null)
            {
                if (clades.Kind != JsonNode.NodeKind.Array)
                {
                    refusal = "the guide at '" + path + "': 'clades' is not an array.";
                    return null;
                }

                int index = 0;
                foreach (JsonNode card in clades.Items())
                {
                    SafariClade c = Card(card, index, missing, ignored);
                    if (c != null)
                    {
                        if (guide._byFounder.ContainsKey(c.Founder))
                        {
                            refusal = "the guide at '" + path + "' holds two cards for the clade founded by body " + c.Founder + ".";
                            return null;
                        }

                        guide._clades.Add(c);
                        guide._byFounder[c.Founder] = c;
                    }

                    index++;
                }
            }

            // The ranking: an array of founder ids, or of cards' ids by whichever key. Absent, the
            // cards' own rank (or score) decides it, which is the same ranking said once.
            JsonNode ranking = Optional(root, "ranking") ?? Optional(root, "order");
            List<long> order = null;
            if (ranking != null && ranking.Kind == JsonNode.NodeKind.Array)
            {
                order = new List<long>();
                foreach (JsonNode r in ranking.Items())
                {
                    long id = r.Kind == JsonNode.NodeKind.Number
                        ? (long)Math.Round(r.AsDouble())
                        : r.Kind == JsonNode.NodeKind.Object && FirstOf(r, FounderKeys) != null
                            ? (long)Math.Round(FirstOf(r, FounderKeys).AsDouble())
                            : long.MinValue;
                    if (id != long.MinValue && guide._byFounder.ContainsKey(id)) order.Add(id);
                }
            }

            bool everyCardRanked = guide._clades.All(c => c.Rank > 0 || !double.IsNaN(c.Score));
            if (order == null && !everyCardRanked)
            {
                missing.Add("the top level: 'ranking' (an array of founder ids), or a 'rank' or 'score' on every card");
            }

            if (missing.Count > 0)
            {
                refusal = "the guide at '" + path + "' is missing required keys: " + string.Join("; ", missing) + ".";
                return null;
            }

            if (order == null || order.Count == 0)
            {
                order = guide._clades
                    .OrderBy(c => c.Rank > 0 ? c.Rank : int.MaxValue)
                    .ThenByDescending(c => double.IsNaN(c.Score) ? double.NegativeInfinity : c.Score)
                    .ThenBy(c => c.Founder)
                    .Select(c => c.Founder).ToList();
            }

            // Anything the ranking left out goes after it, in founder order: the trip builder
            // must be able to reach every card.
            var seen = new HashSet<long>(order);
            foreach (SafariClade card in guide._clades.OrderBy(k => k.Founder)) if (seen.Add(card.Founder)) order.Add(card.Founder);
            for (int i = 0; i < order.Count; i++)
            {
                SafariClade c = guide._byFounder[order[i]];
                if (c.Rank <= 0) c.Rank = i + 1;
            }
            guide.Ranking = order;

            JsonNode picker = Optional(root, "picker") ?? Optional(root, "pickerList") ?? Optional(root, "picker_list");
            var pick = new List<long>();
            if (picker != null && picker.Kind == JsonNode.NodeKind.Array)
            {
                foreach (JsonNode r in picker.Items())
                {
                    long id = r.Kind == JsonNode.NodeKind.Number ? (long)Math.Round(r.AsDouble())
                        : r.Kind == JsonNode.NodeKind.Object && FirstOf(r, FounderKeys) != null
                            ? (long)Math.Round(FirstOf(r, FounderKeys).AsDouble()) : long.MinValue;
                    if (id != long.MinValue && guide._byFounder.ContainsKey(id) && !pick.Contains(id)) pick.Add(id);
                }
            }
            else
            {
                foreach (long id in order)
                {
                    SafariClade c = guide._byFounder[id];
                    if (c.MembersEver < 0 || c.MembersEver >= 10) pick.Add(id);
                }
            }

            guide.Picker = pick;
            guide.Ignored = ignored.OrderBy(k => k, StringComparer.Ordinal).ToList();
            return guide;
        }

        private static SafariClade Card(JsonNode card, int index, List<string> missing, HashSet<string> ignored)
        {
            string where = "clades[" + index.ToString(CultureInfo.InvariantCulture) + "]";

            if (card.Kind != JsonNode.NodeKind.Object)
            {
                missing.Add(where + ": not an object");
                return null;
            }

            int before = missing.Count;

            JsonNode founder = Get(card, missing, where, FounderKeys);
            JsonNode name = Get(card, missing, where, NameKeys);
            JsonNode founded = Get(card, missing, where, FoundedKeys);
            JsonNode parent = GetNullable(card, missing, where, ParentCladeKeys);
            JsonNode flags = Get(card, missing, where, FlagsKeys);
            JsonNode best = Get(card, missing, where, BestKeys);
            JsonNode rank = FirstOf(card, RankKeys);
            JsonNode score = FirstOf(card, ScoreKeys);

            if (missing.Count > before) return null;

            var c = new SafariClade
            {
                Founder = (long)Math.Round(founder.AsDouble()),
                Name = name.AsString(),
                FoundedAt = founded.AsDouble(),
                ParentClade = parent == null || parent.Kind == JsonNode.NodeKind.Null ? -1 : (long)Math.Round(parent.AsDouble()),
                BestSecond = best.AsDouble(),
                Rank = rank != null && rank.Kind == JsonNode.NodeKind.Number ? rank.AsInt() : 0,
                Score = score != null && score.Kind == JsonNode.NodeKind.Number ? score.AsDouble() : double.NaN,
            };

            if (!ReadFlags(flags, c))
            {
                missing.Add(where + ": 'flags' as {abs, jnt, pho} (0/1 or true/false) or a guild word such as 'a-p'");
                return null;
            }

            // Everything below is optional: read if written, NaN or -1 if not.
            c.ParentBody = Long(card, -1, "parentId", "parent_id", "parentBody", "parent_body");
            c.MembersEver = Int(card, -1, "membersEver", "members_ever", "members");
            JsonNode peak = FirstOf(card, "peak");
            if (peak != null && peak.Kind == JsonNode.NodeKind.Object)
            {
                c.PeakCount = Int(peak, -1, "count", "alive", "n");
                c.PeakAt = Double(peak, double.NaN, "at", "t", "second");
                c.PeakShare = Double(peak, double.NaN, "share", "shareOfLiving", "share_of_living");
            }
            else
            {
                c.PeakCount = Int(card, -1, "peakCount", "peak_count");
                c.PeakAt = Double(card, double.NaN, "peakAt", "peak_at");
                c.PeakShare = Double(card, double.NaN, "peakShare", "peak_share");
            }
            if (double.IsNaN(c.PeakAt)) c.PeakAt = c.BestSecond;

            c.GenerationDepth = Int(card, -1, "generationDepth", "generation_depth", "generations");
            JsonNode alive = FirstOf(card, "aliveAtEnd", "alive_at_end");
            if (alive != null && alive.Kind == JsonNode.NodeKind.Bool) c.AliveAtEnd = alive.AsBool();
            c.ExtinctAt = Double(card, double.NaN, "extinctAt", "extinct_at");
            if (!double.IsNaN(c.ExtinctAt) && c.AliveAtEnd == null) c.AliveAtEnd = false;

            JsonNode split = FirstOf(card, "split", "splitChanged", "split_changed", "change");
            if (split != null)
            {
                if (split.Kind == JsonNode.NodeKind.String) c.Split = split.AsString();
                else if (split.Kind == JsonNode.NodeKind.Object)
                {
                    JsonNode text = FirstOf(split, "text", "summary", "changed");
                    if (text != null && text.Kind == JsonNode.NodeKind.String) c.Split = text.AsString();
                }
            }

            c.MedianDepth = Median(card, "depth", "medianDepth", "median_depth");
            c.MedianRadius = Median(card, "radius", "medianRadius", "median_radius");
            c.MedianAboveFloor = Median(card, "aboveFloor", "medianAboveFloor", "above_floor");

            JsonNode body = FirstOf(card, "body");
            JsonNode bodyHost = body != null && body.Kind == JsonNode.NodeKind.Object ? body : card;
            c.AdultVolume = Double(bodyHost, double.NaN, "adultVolume", "adult_volume", "volume");
            c.Parts = Int(bodyHost, -1, "parts", "partCount", "part_count");
            c.Speed = Double(card, double.NaN, "speed", "meanSpeed", "mean_speed");
            c.Exemplar = Long(card, -1, "exemplar", "exemplarId", "exemplar_id", "bestBody", "best_body");

            JsonNode firsts = FirstOf(card, "firsts");
            if (firsts == null && body != null && body.Kind == JsonNode.NodeKind.Object) firsts = FirstOf(body, "firsts");
            if (firsts != null && firsts.Kind == JsonNode.NodeKind.Array)
            {
                foreach (JsonNode f in firsts.Items())
                {
                    if (f.Kind == JsonNode.NodeKind.String) c.Firsts.Add(f.AsString());
                    else if (f.Kind == JsonNode.NodeKind.Object && FirstOf(f, "text", "name", "what") is JsonNode t && t.Kind == JsonNode.NodeKind.String)
                        c.Firsts.Add(t.AsString());
                }
            }

            return c;
        }

        private static bool ReadFlags(JsonNode flags, SafariClade c)
        {
            if (flags.Kind == JsonNode.NodeKind.String)
            {
                string w = flags.AsString() ?? "";
                if (w.Length != 3) return false;
                c.Absorptive = w[0] == 'a' || w[0] == 'A' || w[0] == '1';
                c.Jointed = w[1] == 'j' || w[1] == 'J' || w[1] == '1';
                c.Photosynthetic = w[2] == 'p' || w[2] == 'P' || w[2] == '1';
                return true;
            }

            if (flags.Kind == JsonNode.NodeKind.Array && flags.Count == 3)
            {
                return Flag(flags[0], out c.Absorptive) && Flag(flags[1], out c.Jointed) && Flag(flags[2], out c.Photosynthetic);
            }

            if (flags.Kind != JsonNode.NodeKind.Object) return false;

            JsonNode a = FirstOf(flags, "abs", "absorptive");
            JsonNode j = FirstOf(flags, "jnt", "jointed");
            JsonNode p = FirstOf(flags, "pho", "photosynthetic");
            return a != null && j != null && p != null &&
                   Flag(a, out c.Absorptive) && Flag(j, out c.Jointed) && Flag(p, out c.Photosynthetic);
        }

        private static bool Flag(JsonNode n, out bool value)
        {
            value = false;
            if (n.Kind == JsonNode.NodeKind.Bool) { value = n.AsBool(); return true; }
            if (n.Kind == JsonNode.NodeKind.Number) { value = n.AsDouble() != 0d; return true; }
            return false;
        }

        // ---------------------------------------------------------------- key helpers

        private static JsonNode FirstOf(JsonNode host, params string[] keys)
        {
            if (host == null || host.Kind != JsonNode.NodeKind.Object) return null;
            foreach (string k in keys) if (host.Has(k)) return host[k];
            return null;
        }

        private static JsonNode Optional(JsonNode host, string key) => FirstOf(host, key);

        private static JsonNode Get(JsonNode host, List<string> missing, string where, params string[] keys)
        {
            JsonNode n = FirstOf(host, keys);
            if (n == null || n.Kind == JsonNode.NodeKind.Null)
            {
                missing.Add(where + ": '" + keys[0] + "'" +
                            (keys.Length > 1 ? " (or " + string.Join(", ", keys.Skip(1).Select(k => "'" + k + "'")) + ")" : ""));
                return null;
            }
            return n;
        }

        /// <summary>A key that must be present and may be null (the parent clade of a founder's clade).</summary>
        private static JsonNode GetNullable(JsonNode host, List<string> missing, string where, params string[] keys)
        {
            foreach (string k in keys) if (host.Has(k)) return host[k];
            missing.Add(where + ": '" + keys[0] + "' (null for a clade with no parent" +
                        (keys.Length > 1 ? "; or " + string.Join(", ", keys.Skip(1).Select(k => "'" + k + "'")) : "") + ")");
            return null;
        }

        private static double Double(JsonNode host, double fallback, params string[] keys)
        {
            JsonNode n = FirstOf(host, keys);
            return n != null && n.Kind == JsonNode.NodeKind.Number ? n.AsDouble() : fallback;
        }

        private static int Int(JsonNode host, int fallback, params string[] keys)
        {
            JsonNode n = FirstOf(host, keys);
            return n != null && n.Kind == JsonNode.NodeKind.Number ? (int)Math.Round(n.AsDouble()) : fallback;
        }

        private static long Long(JsonNode host, long fallback, params string[] keys)
        {
            JsonNode n = FirstOf(host, keys);
            return n != null && n.Kind == JsonNode.NodeKind.Number ? (long)Math.Round(n.AsDouble()) : fallback;
        }

        /// <summary>A median written as a number, or as an object with a <c>median</c> key.</summary>
        private static double Median(JsonNode host, params string[] keys)
        {
            JsonNode n = FirstOf(host, keys);
            if (n == null) return double.NaN;
            if (n.Kind == JsonNode.NodeKind.Number) return n.AsDouble();
            if (n.Kind == JsonNode.NodeKind.Object) return Double(n, double.NaN, "median", "p50");
            return double.NaN;
        }
    }
}
