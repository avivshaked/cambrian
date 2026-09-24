using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Evosim.Theatre
{
    /// <summary>The seven stations the owner accepted (safari-spec.md item 6), each a plan template.</summary>
    public enum SafariStation { Arrival, Descent, Portrait, Floor, Birth, Colony, Time }

    /// <summary>The trip's heuristics (item 7).</summary>
    public enum SafariHeuristic { Top10, Guild, Depth, Age, Firsts }

    /// <summary>One caption: a sentence and when in the scene it appears, seconds from the scene's start.</summary>
    public sealed class SafariCaption
    {
        public double Offset;
        public string Text;
        /// <summary>How long it stays, s: four, the owner's rule.</summary>
        public double Seconds = 4d;
    }

    /// <summary>
    /// A station applied to a subject at a second (item 6): what the director plays.
    /// </summary>
    /// <remarks>
    /// <c>Plan</c> is the scene's camera plan once the world is at its second: the plans are
    /// checked against the bed and the bodies at the scene's second (item 9), so they cannot be
    /// made before the world is there, and the director fills the field when it plans.
    /// </remarks>
    public sealed class SafariScene
    {
        public int Index;
        public SafariStation Station;
        /// <summary>The clade, or null for the world.</summary>
        public SafariClade Clade;
        /// <summary>A named body the scene follows, when one is wanted; -1 lets the director choose.</summary>
        public long Body = -1;
        /// <summary>The second the scene opens at.</summary>
        public double At;
        /// <summary>The second the trip asked for, when the director moved the scene off it; NaN otherwise.</summary>
        public double AskedAt = double.NaN;
        /// <summary>Writes the captions again from the scene's facts (set by <see cref="SafariCaptions.Fill"/>), for a scene the director moved.</summary>
        public Action Refill;
        /// <summary>For Time, the second of the second take; NaN otherwise.</summary>
        public double SecondAt = double.NaN;
        /// <summary>True when the second may move to a checkpoint or to the world already on screen.</summary>
        public bool Flexible = true;
        /// <summary>The scene's length at its second, s (a birth adds its wait).</summary>
        public double Seconds;
        /// <summary>The plan the director made, for the log and the scene list; empty until planned.</summary>
        public string Plan = "";
        public readonly List<SafariCaption> Captions = new List<SafariCaption>();
        /// <summary>The chapter card, once per trip: the colony's disc from above.</summary>
        public bool ChapterCard;

        /// <summary>The subject as the scene list says it.</summary>
        public string Subject => Clade != null ? Clade.Name : Body >= 0 ? "body " + Body : "the world";

        /// <summary>A short name for a directory: 03-portrait-ostrea-lenta.</summary>
        public string Slug
        {
            get
            {
                string subject = Clade != null ? Clade.Name : "world";
                var sb = new System.Text.StringBuilder();
                foreach (char ch in subject.ToLowerInvariant())
                {
                    if (char.IsLetterOrDigit(ch)) sb.Append(ch);
                    else if (sb.Length > 0 && sb[sb.Length - 1] != '-') sb.Append('-');
                }
                return (Index + 1).ToString("00", CultureInfo.InvariantCulture) + "-" +
                       Station.ToString().ToLowerInvariant() + "-" + sb.ToString().Trim('-');
            }
        }

        public string Line() => string.Format(CultureInfo.InvariantCulture,
            "{0,2}. {1,-8} {2,-28} at {3,8:0.#} s{4}", Index + 1, Station, Subject, At,
            double.IsNaN(SecondAt) ? "" : string.Format(CultureInfo.InvariantCulture, " and {0:0.#} s", SecondAt));
    }

    /// <summary>
    /// Builds a trip from a guide (item 7): arrival and descent, then a portrait and a birth or a
    /// colony for each chosen clade, the floor once and time once, the stations alternating.
    /// </summary>
    public static class SafariTripBuilder
    {
        /// <summary>How many clades a trip visits.</summary>
        public const int CladesPerTrip = 10;

        public const double ArrivalSeconds = 10d;
        public const double PortraitSeconds = 20d;
        public const double ColonySeconds = 20d;
        public const double ChapterSeconds = 8d;
        public const double FloorSeconds = 20d;
        public const double BirthLeadSeconds = 8d;
        public const double BirthTailSeconds = 10d;
        public const double TimeTakeSeconds = 8d;

        public static bool TryParse(string text, out SafariHeuristic heuristic)
        {
            heuristic = SafariHeuristic.Top10;
            if (string.IsNullOrWhiteSpace(text)) return true;
            switch (text.Trim().ToLowerInvariant())
            {
                case "top10": case "top": case "top-10": heuristic = SafariHeuristic.Top10; return true;
                case "guild": case "guilds": heuristic = SafariHeuristic.Guild; return true;
                case "depth": case "depth-band": case "band": heuristic = SafariHeuristic.Depth; return true;
                case "age": case "clade-age": heuristic = SafariHeuristic.Age; return true;
                case "firsts": case "first": heuristic = SafariHeuristic.Firsts; return true;
                default: return false;
            }
        }

        /// <summary>The clades a heuristic chooses, in the order the trip visits them.</summary>
        public static List<SafariClade> Choose(SafariGuide guide, SafariHeuristic heuristic, int count = CladesPerTrip)
        {
            List<SafariClade> ranked = guide.Ranking.Select(guide.Find).Where(c => c != null).ToList();
            var chosen = new List<SafariClade>();

            void TakeFrom(IEnumerable<SafariClade> source)
            {
                foreach (SafariClade c in source)
                {
                    if (chosen.Count >= count) return;
                    if (!chosen.Contains(c)) chosen.Add(c);
                }
            }

            switch (heuristic)
            {
                case SafariHeuristic.Guild:
                    // The best-ranked clade of each guild first, then the next of each, and so on.
                    List<SafariClade> byGuild = ranked.GroupBy(k => k.Guild)
                        .SelectMany(g => g.Select((k, n) => (clade: k, turn: n)))
                        .OrderBy(e => e.turn).ThenBy(e => e.clade.Rank)
                        .Select(e => e.clade).ToList();
                    TakeFrom(byGuild);
                    break;

                case SafariHeuristic.Depth:
                    // Bands by the card's median depth, shallow to deep, the best of each band in
                    // turn; a card without a depth goes last.
                    List<SafariClade> byBand = ranked.GroupBy(DepthBand)
                        .SelectMany(g => g.Select((k, n) => (clade: k, turn: n, band: g.Key)))
                        .OrderBy(e => e.turn).ThenBy(e => e.band).ThenBy(e => e.clade.Rank)
                        .Select(e => e.clade).ToList();
                    TakeFrom(byBand);
                    break;

                case SafariHeuristic.Age:
                    // Ten spread over the run's history, oldest first, among the picker's clades.
                    List<SafariClade> pickable = guide.Picker.Select(guide.Find).Where(c => c != null)
                        .OrderBy(c => c.FoundedAt).ThenBy(c => c.Founder).ToList();
                    if (pickable.Count == 0) pickable = ranked.OrderBy(c => c.FoundedAt).ToList();
                    if (pickable.Count <= count) TakeFrom(pickable);
                    else
                    {
                        for (int i = 0; i < count; i++)
                        {
                            int k = (int)Math.Round(i * (pickable.Count - 1) / (double)(count - 1));
                            if (!chosen.Contains(pickable[k])) chosen.Add(pickable[k]);
                        }
                    }
                    break;

                case SafariHeuristic.Firsts:
                    TakeFrom(ranked.Where(c => c.Firsts.Count > 0));
                    break;

                default:
                    TakeFrom(ranked);
                    break;
            }

            return chosen;
        }

        /// <summary>0 surface film (above 5 m), 1 lit band (5 to 15 m), 2 middle (15 to 30 m), 3 deep, 4 unknown.</summary>
        public static int DepthBand(SafariClade c)
        {
            if (double.IsNaN(c.MedianDepth)) return 4;
            double d = Math.Abs(c.MedianDepth);
            return d < 5d ? 0 : d < 15d ? 1 : d < 30d ? 2 : 3;
        }

        /// <summary>
        /// A trip: arrival and descent, then for each clade a portrait and a birth or a colony, the
        /// floor after the middle clade and time before the last, never two portraits in a row.
        /// </summary>
        /// <param name="firstCheckpoint">The earliest checkpoint's second, for deciding whether a birth can be sought; NaN when there is none.</param>
        /// <param name="runSeconds">The run's length, for the time station's two seconds.</param>
        /// <param name="checkpointSeconds">The seconds the run holds checkpoints at, ascending.</param>
        public static List<SafariScene> Build(
            SafariGuide guide, IReadOnlyList<SafariClade> clades, IReadOnlyList<double> checkpointSeconds,
            double runSeconds, bool sortByTime = false)
        {
            var scenes = new List<SafariScene>();
            List<SafariClade> order = sortByTime ? clades.OrderBy(k => k.BestSecond).ToList() : clades.ToList();

            double firstAt = order.Count > 0 ? order[0].BestSecond : (checkpointSeconds.Count > 0 ? checkpointSeconds[0] : 0d);
            double firstCheckpoint = checkpointSeconds.Count > 0 ? checkpointSeconds[0] : double.NaN;

            scenes.Add(new SafariScene { Station = SafariStation.Arrival, At = firstAt, Seconds = ArrivalSeconds });
            scenes.Add(new SafariScene { Station = SafariStation.Descent, At = firstAt, Seconds = 0d });

            int floorAfter = Math.Max(0, order.Count / 2 - 1);
            int timeBefore = Math.Max(0, order.Count - 1);
            bool chapterShown = false;

            for (int i = 0; i < order.Count; i++)
            {
                SafariClade c = order[i];

                if (order.Count > 1 && i == timeBefore) scenes.Add(TimeScene(checkpointSeconds, runSeconds));

                scenes.Add(new SafariScene { Station = SafariStation.Portrait, Clade = c, At = c.BestSecond, Seconds = PortraitSeconds, Body = c.Exemplar });

                // A birth where the clade has a parent to be born from and a checkpoint before its
                // founding to seek from; alternating with the colony so the two stations share the
                // trip, and a colony wherever a birth cannot be sought.
                bool birthable = c.ParentClade >= 0 && !double.IsNaN(firstCheckpoint) && c.FoundedAt > firstCheckpoint;
                if (birthable && i % 2 == 0)
                {
                    scenes.Add(new SafariScene
                    {
                        Station = SafariStation.Birth, Clade = c, At = c.FoundedAt, Flexible = false,
                        Seconds = BirthLeadSeconds + BirthTailSeconds,
                    });
                }
                else
                {
                    scenes.Add(new SafariScene
                    {
                        Station = SafariStation.Colony, Clade = c, At = c.BestSecond,
                        Seconds = ColonySeconds + (chapterShown ? 0d : ChapterSeconds), ChapterCard = !chapterShown,
                    });
                    chapterShown = true;
                }

                if (i == floorAfter) scenes.Add(new SafariScene { Station = SafariStation.Floor, At = c.BestSecond, Seconds = FloorSeconds });
            }

            if (order.Count == 1) scenes.Add(TimeScene(checkpointSeconds, runSeconds));
            if (order.Count == 0)
            {
                scenes.Add(new SafariScene { Station = SafariStation.Floor, At = firstAt, Seconds = FloorSeconds });
                scenes.Add(TimeScene(checkpointSeconds, runSeconds));
            }

            for (int i = 0; i < scenes.Count; i++) scenes[i].Index = i;
            return scenes;
        }

        /// <summary>A trip of one clade (the picker): its portrait, a birth if one can be sought, its colony.</summary>
        public static List<SafariScene> One(SafariClade c, IReadOnlyList<double> checkpointSeconds)
        {
            var scenes = new List<SafariScene>
            {
                new SafariScene { Station = SafariStation.Portrait, Clade = c, At = c.BestSecond, Seconds = PortraitSeconds, Body = c.Exemplar },
            };

            double firstCheckpoint = checkpointSeconds.Count > 0 ? checkpointSeconds[0] : double.NaN;
            if (c.ParentClade >= 0 && !double.IsNaN(firstCheckpoint) && c.FoundedAt > firstCheckpoint)
            {
                scenes.Add(new SafariScene
                {
                    Station = SafariStation.Birth, Clade = c, At = c.FoundedAt, Flexible = false,
                    Seconds = BirthLeadSeconds + BirthTailSeconds,
                });
            }

            scenes.Add(new SafariScene { Station = SafariStation.Colony, Clade = c, At = c.BestSecond, Seconds = ColonySeconds });
            for (int i = 0; i < scenes.Count; i++) scenes[i].Index = i;
            return scenes;
        }

        /// <summary>
        /// Time: the same shot at two checkpoint seconds far apart, a quarter of the way into the
        /// run and its last checkpoint, so neither take needs a seek.
        /// </summary>
        private static SafariScene TimeScene(IReadOnlyList<double> checkpoints, double runSeconds)
        {
            double a, b;
            if (checkpoints.Count >= 2)
            {
                b = checkpoints[checkpoints.Count - 1];
                a = checkpoints[0];
                foreach (double s in checkpoints) { if (s >= 0.25d * b) { a = s; break; } }
                if (a >= b) a = checkpoints[0];
            }
            else
            {
                a = 0.25d * runSeconds;
                b = runSeconds;
            }

            return new SafariScene
            {
                Station = SafariStation.Time, At = a, SecondAt = b, Flexible = false, Seconds = 2d * TimeTakeSeconds,
            };
        }
    }

    /// <summary>
    /// The captions' templates (item 10): one short sentence each, built from the card's facts and
    /// the recording's numbers, never interpreting. A template whose fact the guide did not write
    /// is left out rather than filled.
    /// </summary>
    /// <remarks>
    /// Written without apostrophes and in the bitmap font's alphabet, so a burnt-in caption on a
    /// headless frame prints every character it was given.
    /// </remarks>
    public static class SafariCaptions
    {
        public static string Seconds(double s) => Grouped(Math.Round(s)) + " s";

        /// <summary>A whole number grouped by thousands with a comma: 12,400.</summary>
        public static string Grouped(double n) => n.ToString("#,0", CultureInfo.InvariantCulture);

        public static string Guild(SafariClade c)
        {
            var parts = new List<string>();
            if (c.Photosynthetic) parts.Add("photosynthetic");
            if (c.Absorptive) parts.Add("absorptive");
            if (c.Jointed) parts.Add("jointed");
            return parts.Count == 0 ? "structural only" : string.Join(", ", parts);
        }

        /// <summary>Fills a scene's captions from what is known before it plays.</summary>
        /// <param name="alive">The recording's living count at a second, or -1 when it has none.</param>
        public static void Fill(SafariScene scene, SafariGuide guide, string arm, Func<double, int> alive,
            float depthMetres, float radiusMetres)
        {
            scene.Captions.Clear();
            scene.Refill = () => Fill(scene, guide, arm, alive, depthMetres, radiusMetres);
            SafariClade c = scene.Clade;

            // Every second a caption states is the second the clip is filmed at (the scene's At,
            // which the director moves to a checkpoint and then writes the captions again), or
            // is said to be another second.
            bool moved = !double.IsNaN(scene.AskedAt) && Math.Abs(scene.AskedAt - scene.At) > 0.5d;

            switch (scene.Station)
            {
                case SafariStation.Arrival:
                {
                    int n = alive(scene.At);
                    Add(scene, 1d, (arm ?? "The run") + " at " + Seconds(scene.At) +
                                   (n >= 0 ? ", " + Grouped(n) + " alive in the recording." : "."));
                    break;
                }

                case SafariStation.Descent:
                    Add(scene, 1d, radiusMetres > 0f
                        ? string.Format(CultureInfo.InvariantCulture, "The tank is {0:0} m deep and {1:0} m across.", depthMetres, 2f * radiusMetres)
                        : string.Format(CultureInfo.InvariantCulture, "The water is {0:0} m deep.", depthMetres));
                    break;

                case SafariStation.Portrait:
                {
                    string from = FromClause(c, guide);
                    Add(scene, 1d, c.Name + ", " + Guild(c) + ", founded at " + Seconds(c.FoundedAt) + from + ".");
                    if (c.PeakCount >= 0)
                    {
                        // Filmed at its peak, the peak's second is the clip's; filmed elsewhere,
                        // the caption says where first.
                        bool atPeak = !double.IsNaN(c.PeakAt) && Math.Abs(c.PeakAt - scene.At) <= 0.5d;
                        Add(scene, 7d, (atPeak ? "Its peak was " : "Filmed at " + Seconds(scene.At) + "; its peak was ") +
                                       Grouped(c.PeakCount) + " alive at " + Seconds(c.PeakAt) +
                                       (double.IsNaN(c.PeakShare) ? "." : string.Format(CultureInfo.InvariantCulture, ", {0:0.#}% of the living.", 100d * c.PeakShare)));
                    }
                    else if (moved)
                    {
                        Add(scene, 7d, "Filmed at " + Seconds(scene.At) + "; the trip chose " + Seconds(scene.AskedAt) + ".");
                    }
                    if (!double.IsNaN(c.MedianDepth))
                    {
                        Add(scene, 13d, string.Format(CultureInfo.InvariantCulture, "Its median depth was {0:0.#} m.", Math.Abs(c.MedianDepth)));
                    }
                    break;
                }

                case SafariStation.Birth:
                {
                    SafariClade parent = c.ParentClade >= 0 ? guide.Find(c.ParentClade) : null;
                    string split = string.IsNullOrWhiteSpace(c.Split) ? "" : "; the split " + Trimmed(c.Split);
                    Add(scene, 1d, "Founded at " + Seconds(c.FoundedAt) +
                                   (parent != null ? " from " + parent.Name : "") + split + ".");
                    // The second caption names what the cousin gave; the director writes it when
                    // the birth arrives (SafariDirector), because it is not known before.
                    break;
                }

                case SafariStation.Colony:
                {
                    var facts = new List<string>();
                    if (c.MembersEver >= 0) facts.Add(Grouped(c.MembersEver) + " members ever");
                    if (c.GenerationDepth >= 0) facts.Add(c.GenerationDepth + " generations deep");
                    Add(scene, 1d, c.Name + (facts.Count > 0 ? ": " + string.Join(", ", facts) + "." : "."));
                    if (c.AliveAtEnd == true) Add(scene, 7d, "It was alive at the end of the run.");
                    else if (!double.IsNaN(c.ExtinctAt)) Add(scene, 7d, "It was gone by " + Seconds(c.ExtinctAt) + ".");
                    if (moved)
                    {
                        Add(scene, 13d, "Filmed at " + Seconds(scene.At) +
                                        (c.PeakCount >= 0 && !double.IsNaN(c.PeakAt)
                                            ? "; its peak was " + Grouped(c.PeakCount) + " alive at " + Seconds(c.PeakAt) + "."
                                            : "; the trip chose " + Seconds(scene.AskedAt) + "."));
                    }
                    break;
                }

                case SafariStation.Floor:
                    Add(scene, 1d, string.Format(CultureInfo.InvariantCulture, "The bed, about {0:0} m below the surface.", depthMetres));
                    break;

                case SafariStation.Time:
                {
                    int a = alive(scene.At);
                    int b = alive(scene.SecondAt);
                    Add(scene, 1d, Seconds(scene.At) + (a >= 0 ? ": " + Grouped(a) + " alive." : "."));
                    Add(scene, SafariTripBuilder.TimeTakeSeconds + 0.5d, Seconds(scene.SecondAt) + (b >= 0 ? ": " + Grouped(b) + " alive." : "."));
                    break;
                }
            }
        }

        private static string FromClause(SafariClade c, SafariGuide guide)
        {
            if (c.ParentClade < 0) return " from a random founder";
            SafariClade parent = guide.Find(c.ParentClade);
            return parent != null ? " from " + parent.Name : "";
        }

        private static string Trimmed(string s)
        {
            s = s.Trim().TrimEnd('.');
            if (s.StartsWith("the split ", StringComparison.OrdinalIgnoreCase)) s = s.Substring(10);
            if (s.Length > 0 && char.IsUpper(s[0]) && (s.Length < 2 || !char.IsUpper(s[1]))) s = char.ToLowerInvariant(s[0]) + s.Substring(1);
            return s.Replace('\'', ' ');
        }

        public static void Add(SafariScene scene, double offset, string text) =>
            scene.Captions.Add(new SafariCaption { Offset = offset, Text = text.Replace('\'', ' ') });

        /// <summary>The caption showing at an offset into the scene, or null.</summary>
        public static SafariCaption At(SafariScene scene, double offset)
        {
            SafariCaption showing = null;
            foreach (SafariCaption c in scene.Captions)
            {
                if (offset >= c.Offset && offset < c.Offset + c.Seconds) showing = c;
            }
            return showing;
        }
    }
}
