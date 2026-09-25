using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Evosim.Theatre
{
    /// <summary>
    /// The seven stations the owner accepted (safari-spec.md item 6), each a plan template, and
    /// the card: a held title from above that only a story's shot list asks for
    /// (<see cref="SafariStory"/>).
    /// </summary>
    public enum SafariStation { Arrival, Descent, Portrait, Floor, Birth, Colony, Time, Card }

    /// <summary>The trip's heuristics (item 7).</summary>
    public enum SafariHeuristic { Top10, Guild, Depth, Age, Firsts }

    /// <summary>One caption: a sentence and when in the scene it appears, seconds from the scene's start.</summary>
    public sealed class SafariCaption
    {
        public double Offset;
        public string Text;
        /// <summary>How long it stays, s: four, the owner's rule.</summary>
        public double Seconds = SafariCaptions.OnScreenSeconds;
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
        /// <summary>
        /// A chapter card before the scene, the disc from above, held, with the chapter's title:
        /// the template gives one to a colony, and a story to any scene that carries a chapter.
        /// </summary>
        public bool ChapterCard;
        /// <summary>The chapter's number from 1 and its title, when <see cref="ChapterCard"/> is set.</summary>
        public int Chapter = -1;
        public string ChapterTitle;
        /// <summary>
        /// The facts the trip gave this scene (<see cref="SafariCaptions.Assign"/>), in the order
        /// its captions say them; no fact is given twice to one clade, and no world fact twice to
        /// one trip.
        /// </summary>
        public readonly List<SafariFact> Facts = new List<SafariFact>();

        // ---------------------------------------------------------------- a story's scene

        /// <summary>The shot list's number for a scene from a story, from 1 across the whole film; -1 for the template's.</summary>
        public int StoryNumber = -1;
        /// <summary>The arm a story's scene is filmed on, for its clip's name.</summary>
        public string StoryArm;
        /// <summary>The story's act, for the log.</summary>
        public string Act;
        /// <summary>
        /// A story's captions, which take the place of the guide's facts and the template's
        /// explanatory lines; <see cref="SafariCaptions.Fill"/> copies them in whatever second the
        /// scene is filmed at, and the director writes none of its own over them. Null for the
        /// template's scenes.
        /// </summary>
        public List<SafariCaption> StoryCaptions;
        /// <summary>
        /// For a birth, the clade whose member the rehearsal waits on for a child: -1 is the
        /// clade's parent clade (the template's birth, the clade's own founding), and a story's
        /// birth far from its clade's founding names the clade itself.
        /// </summary>
        public long BirthFrom = -1;
        /// <summary>
        /// For a story's birth, the parent body its writer named, or -1: when it is alive where
        /// the rehearsal starts, its child is taken before any other in the line.
        /// </summary>
        public long BirthParentBody = -1;
        /// <summary>A story's own canopy switch for its arrival or descent; null leaves it to <see cref="SafariOptions.Canopy"/>.</summary>
        public bool? Canopy;
        /// <summary>How much a story's held card darkens its picture under the captions, 0 to 1 (0.6 dimmed, 1 black).</summary>
        public float Dim;

        /// <summary>True for a scene a story's shot list asked for.</summary>
        public bool FromStory => StoryNumber >= 0;

        /// <summary>The clade a birth's parent belongs to (<see cref="BirthFrom"/>), or -1.</summary>
        public long BirthLine => BirthFrom >= 0 ? BirthFrom : Clade?.ParentClade ?? -1;

        /// <summary>The subject as the scene list says it.</summary>
        public string Subject => Clade != null ? Clade.Name : Body >= 0 ? "body " + Body : "the world";

        /// <summary>
        /// A short name for a directory: 03-portrait-ostrea-lenta; a story's scene carries its
        /// number and its arm first, story-07-r48-s1-portrait-ostrea-lenta, so the clips of every
        /// run sort into the story's order.
        /// </summary>
        public string Slug
        {
            get
            {
                string subject = Clade != null ? Clade.Name
                    : Station == SafariStation.Card && !string.IsNullOrEmpty(ChapterTitle) ? ChapterTitle
                    : Body >= 0 ? "body " + Body.ToString(CultureInfo.InvariantCulture) : "world";
                string words = Words(subject);
                if (words.Length > 40) words = words.Substring(0, 40).TrimEnd('-');
                if (words.Length == 0) words = "world";
                string station = Station.ToString().ToLowerInvariant();

                if (FromStory)
                {
                    return "story-" + StoryNumber.ToString("00", CultureInfo.InvariantCulture) + "-" +
                           Words(StoryArm ?? "run") + "-" + station + "-" + words;
                }

                return (Index + 1).ToString("00", CultureInfo.InvariantCulture) + "-" + station + "-" + words;
            }
        }

        /// <summary>Lower case letters and digits, every other run of characters one dash.</summary>
        private static string Words(string text)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char ch in text.ToLowerInvariant())
            {
                if (ch < 128 && char.IsLetterOrDigit(ch)) sb.Append(ch);
                else if (sb.Length > 0 && sb[sb.Length - 1] != '-') sb.Append('-');
            }
            return sb.ToString().Trim('-');
        }

        public string Line() => string.Format(CultureInfo.InvariantCulture,
            "{0,2}. {1,-8} {2,-28} at {3,8:0.#} s{4}{5}{6}", Index + 1, Station, Subject, At,
            double.IsNaN(SecondAt) ? "" : string.Format(CultureInfo.InvariantCulture, " and {0:0.#} s", SecondAt),
            ChapterCard && ChapterTitle != null ? "  [chapter " + Chapter + ": " + ChapterTitle + "]" : "",
            FromStory
                ? string.Format(CultureInfo.InvariantCulture, "  [story {0}{1}, {2:0.#} s on screen{3}]", StoryNumber,
                    string.IsNullOrEmpty(Act) ? "" : ", " + Act, Seconds,
                    Station == SafariStation.Card ? ": " + ChapterTitle : "")
                : "");
    }

    /// <summary>
    /// Builds a trip from a guide (item 7): arrival and descent, then for each chosen clade a
    /// birth where one can be sought, its portrait and its colony, the floor once and time once.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>In time order by default</b> (the safari review of 2026-09-24): the clades are visited by
    /// their best second, so the film's clock runs forward, in the guide's chapters (the founding,
    /// the first eater, the middle, the takeover), each opened by a card from above on its first
    /// colony. <c>EVOSIM_THEATRE_SAFARI_ORDER=score</c> keeps the guide's ranking instead, the
    /// order every trip had before.
    /// </para>
    /// <para>
    /// <b>A birth for every split clade founded after the first checkpoint</b>, before its
    /// portrait, since the founding comes before the peak; until the review a birth went only to
    /// every other clade and replaced its colony, so no birth reached round 47's trip. A colony of
    /// one member (a peak of one) is refused: a pull-back on one body says nothing a portrait did
    /// not, and the floor takes its place when the floor is still to come.
    /// </para>
    /// </remarks>
    public static class SafariTripBuilder
    {
        /// <summary>How many clades a trip visits.</summary>
        public const int CladesPerTrip = 10;

        public const double ArrivalSeconds = 10d;
        public const double PortraitSeconds = 20d;
        public const double ColonySeconds = 20d;
        public const double ChapterSeconds = 8d;

        /// <summary>
        /// The floor's length. Ten seconds, from twenty: round 47 seed 2's reshoot showed twenty
        /// seconds of empty sand at 5,000 s, which is what the floor is in that world and too long
        /// to watch; ten still carries the caption and the truck across the hollow.
        /// </summary>
        public const double FloorSeconds = 10d;

        public const double BirthLeadSeconds = 8d;
        public const double BirthTailSeconds = 10d;
        public const double TimeTakeSeconds = 8d;

        /// <summary>True unless <c>EVOSIM_THEATRE_SAFARI_ORDER</c> asks for the score's order (<c>score</c> or <c>rank</c>).</summary>
        public static bool TimeOrder
        {
            get
            {
                string order = (Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SAFARI_ORDER") ?? "").Trim().ToLowerInvariant();
                return order != "score" && order != "rank";
            }
        }

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

        /// <summary>True when a clade can be seen being born: a split founded after the first checkpoint.</summary>
        public static bool Birthable(SafariClade c, double firstCheckpoint) =>
            c.ParentClade >= 0 && !double.IsNaN(firstCheckpoint) && c.FoundedAt > firstCheckpoint;

        /// <summary>True when the clade's colony is worth a scene: more than one member at its peak, or a peak the guide did not write.</summary>
        public static bool ColonyWorthFilming(SafariClade c) => c.PeakCount < 0 || c.PeakCount > 1;

        /// <summary>
        /// A trip: arrival and descent, then for each clade a birth where one can be sought, its
        /// portrait and its colony, the floor after the middle clade and time before the last.
        /// </summary>
        /// <param name="checkpointSeconds">The seconds the run holds checkpoints at, ascending.</param>
        /// <param name="runSeconds">The run's length, for the time station's two seconds.</param>
        /// <param name="sortByTime">Time order in chapters (the default), or the order given.</param>
        public static List<SafariScene> Build(
            SafariGuide guide, IReadOnlyList<SafariClade> clades, IReadOnlyList<double> checkpointSeconds,
            double runSeconds, bool sortByTime = true)
        {
            var scenes = new List<SafariScene>();
            List<SafariClade> order = sortByTime
                ? clades.OrderBy(k => k.BestSecond).ThenBy(k => k.FoundedAt).ThenBy(k => k.Founder).ToList()
                : clades.ToList();

            double firstAt = order.Count > 0 ? order[0].BestSecond : (checkpointSeconds.Count > 0 ? checkpointSeconds[0] : 0d);
            double firstCheckpoint = checkpointSeconds.Count > 0 ? checkpointSeconds[0] : double.NaN;

            scenes.Add(new SafariScene { Station = SafariStation.Arrival, At = firstAt, Seconds = ArrivalSeconds });
            scenes.Add(new SafariScene { Station = SafariStation.Descent, At = firstAt, Seconds = 0d });

            int floorAfter = Math.Max(0, order.Count / 2 - 1);
            int timeBefore = Math.Max(0, order.Count - 1);
            bool floorPlaced = false;

            // Chapters: by the guide's chapters in time order; one card for the whole trip in the
            // score's order, as before.
            int lastChapter = int.MinValue;
            bool cardPending = true;
            int cardChapter = -1;
            string cardTitle = null;

            for (int i = 0; i < order.Count; i++)
            {
                SafariClade c = order[i];

                if (sortByTime && guide != null && guide.Chapters.Count > 0)
                {
                    int ch = guide.ChapterOf(c.BestSecond);
                    if (ch != lastChapter)
                    {
                        lastChapter = ch;
                        cardPending = ch >= 0;
                        cardChapter = ch + 1;
                        cardTitle = ch >= 0 ? guide.Chapters[ch].Title : null;
                    }
                }

                if (order.Count > 1 && i == timeBefore) scenes.Add(TimeScene(checkpointSeconds, runSeconds));

                if (Birthable(c, firstCheckpoint))
                {
                    scenes.Add(new SafariScene
                    {
                        Station = SafariStation.Birth, Clade = c, At = c.FoundedAt, Flexible = false,
                        Seconds = BirthLeadSeconds + BirthTailSeconds,
                    });
                }

                scenes.Add(new SafariScene { Station = SafariStation.Portrait, Clade = c, At = c.BestSecond, Seconds = PortraitSeconds, Body = c.Exemplar });

                if (ColonyWorthFilming(c))
                {
                    var colony = new SafariScene
                    {
                        Station = SafariStation.Colony, Clade = c, At = c.BestSecond,
                        Seconds = ColonySeconds + (cardPending ? ChapterSeconds : 0d), ChapterCard = cardPending,
                    };
                    if (cardPending)
                    {
                        colony.Chapter = cardChapter;
                        colony.ChapterTitle = cardTitle;
                    }
                    scenes.Add(colony);
                    cardPending = false;
                }
                else if (!floorPlaced)
                {
                    // The refused colony's place goes to the floor, so two portraits never meet.
                    scenes.Add(new SafariScene { Station = SafariStation.Floor, At = c.BestSecond, Seconds = FloorSeconds });
                    floorPlaced = true;
                }

                if (i == floorAfter && !floorPlaced)
                {
                    scenes.Add(new SafariScene { Station = SafariStation.Floor, At = c.BestSecond, Seconds = FloorSeconds });
                    floorPlaced = true;
                }
            }

            if (order.Count == 1) scenes.Add(TimeScene(checkpointSeconds, runSeconds));
            if (order.Count == 0)
            {
                scenes.Add(new SafariScene { Station = SafariStation.Floor, At = firstAt, Seconds = FloorSeconds });
                scenes.Add(TimeScene(checkpointSeconds, runSeconds));
            }

            NoTwoPortraits(scenes);
            for (int i = 0; i < scenes.Count; i++) scenes[i].Index = i;
            SafariCaptions.Assign(scenes, guide);
            return scenes;
        }

        /// <summary>
        /// Moves a portrait that follows a portrait (a refused colony leaves its clade's portrait
        /// alone) to the first later place between two scenes that are not portraits, moving the
        /// lone portrait rather than one with its own colony or birth beside it.
        /// </summary>
        private static void NoTwoPortraits(List<SafariScene> scenes)
        {
            bool Lone(SafariScene p) => !scenes.Any(s => s != p && s.Clade == p.Clade && s.Station != SafariStation.Portrait);
            bool IsPortrait(int k) => k >= 0 && k < scenes.Count && scenes[k].Station == SafariStation.Portrait;

            for (int guard = 0; guard < 4 * scenes.Count; guard++)
            {
                int i = Enumerable.Range(1, Math.Max(0, scenes.Count - 1)).FirstOrDefault(k => IsPortrait(k) && IsPortrait(k - 1));
                if (i <= 0) return;

                int from = Lone(scenes[i - 1]) ? i - 1 : i;
                SafariScene moving = scenes[from];
                scenes.RemoveAt(from);

                int to = -1;
                for (int pos = from + 1; pos <= scenes.Count; pos++)
                {
                    if (!IsPortrait(pos - 1) && !IsPortrait(pos)) { to = pos; break; }
                }
                if (to < 0)
                {
                    scenes.Insert(from, moving);
                    return;
                }
                scenes.Insert(to, moving);
            }
        }

        /// <summary>A trip of one clade (the picker): a birth if one can be sought, its portrait, its colony.</summary>
        public static List<SafariScene> One(SafariClade c, IReadOnlyList<double> checkpointSeconds, SafariGuide guide = null)
        {
            var scenes = new List<SafariScene>();

            double firstCheckpoint = checkpointSeconds.Count > 0 ? checkpointSeconds[0] : double.NaN;
            if (Birthable(c, firstCheckpoint))
            {
                scenes.Add(new SafariScene
                {
                    Station = SafariStation.Birth, Clade = c, At = c.FoundedAt, Flexible = false,
                    Seconds = BirthLeadSeconds + BirthTailSeconds,
                });
            }

            scenes.Add(new SafariScene { Station = SafariStation.Portrait, Clade = c, At = c.BestSecond, Seconds = PortraitSeconds, Body = c.Exemplar });
            if (ColonyWorthFilming(c))
                scenes.Add(new SafariScene { Station = SafariStation.Colony, Clade = c, At = c.BestSecond, Seconds = ColonySeconds });
            for (int i = 0; i < scenes.Count; i++) scenes[i].Index = i;
            SafariCaptions.Assign(scenes, guide);
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
    /// The captions (item 10, and the safari review of 2026-09-24): one fact a caption, each a
    /// sentence the guide wrote from its own numbers, four seconds on screen with a second's gap.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The facts are the guide's.</b> <c>scripts/guide.py</c> writes each card's facts ranked
    /// by interest, every number in a fact's text one of its own slots; this class only chooses
    /// which fact goes where and when. A portrait says what the clade is, what is special about
    /// it, what it does and its fate; a colony how many there were, the world then and what came
    /// of it; a birth where the line came from. No clade hears the same sentence twice and no
    /// world fact is said twice in a trip. A card the guide wrote no facts for falls back to the
    /// few sentences the card's own numbers make.
    /// </para>
    /// <para>
    /// <b>Facts about the body on screen are the director's</b>, from the live organism: a
    /// restored world is a cousin, and its bodies' ages and children are its own
    /// (<see cref="SafariDirector"/>). The bitmap font has an apostrophe, so a caption keeps its
    /// apostrophes.
    /// </para>
    /// </remarks>
    public static class SafariCaptions
    {
        /// <summary>How long a caption stays, s.</summary>
        public const double OnScreenSeconds = 4d;

        /// <summary>The gap between two captions, s.</summary>
        public const double GapSeconds = 1d;

        /// <summary>The first caption's offset into its take, s.</summary>
        public const double FirstOffset = 0.5d;

        /// <summary>The offset of the n-th caption from 0 in a run of captions starting at <paramref name="from"/>.</summary>
        public static double Slot(int n, double from = 0d) => from + FirstOffset + n * (OnScreenSeconds + GapSeconds);

        public static string Seconds(double s) => Grouped(Math.Round(s)) + " s";

        /// <summary>The run's clock or a duration: seconds under an hour, hours and minutes over it (guide.py's `clock`).</summary>
        public static string Clock(double s)
        {
            if (double.IsNaN(s)) return "an unknown time";
            if (s < 3600d) return Seconds(s);
            int h = (int)Math.Floor(s / 3600d);
            int m = (int)Math.Round((s - 3600d * h) / 60d);
            if (m == 60) { h++; m = 0; }
            return h.ToString(CultureInfo.InvariantCulture) + " h " + m.ToString("00", CultureInfo.InvariantCulture) + " min";
        }

        /// <summary>A whole number grouped by thousands with a comma: 12,400.</summary>
        public static string Grouped(double n) => n.ToString("#,0", CultureInfo.InvariantCulture);

        /// <summary>The guild in plain words: the guide's, or made from the flags when the guide is older.</summary>
        public static string Guild(SafariClade c)
        {
            if (!string.IsNullOrWhiteSpace(c.GuildPlain)) return c.GuildPlain;
            string what = c.Photosynthetic ? "a leaf" : c.Absorptive ? "a stomach" : "a body with no leaf";
            if (c.Photosynthetic && c.Absorptive) what = "a leaf with a stomach";
            if (c.Jointed) what = what.StartsWith("a ", StringComparison.Ordinal) ? "a jointed " + what.Substring(2) : "jointed " + what;
            return what;
        }

        // ---------------------------------------------------------------- assigning the facts

        /// <summary>
        /// Gives each scene its facts, in trip order: the portrait's four, the birth's two, the
        /// colony's three, the floor's and the descent's world facts. Called by the builder.
        /// </summary>
        public static void Assign(IReadOnlyList<SafariScene> scenes, SafariGuide guide)
        {
            var byClade = new Dictionary<SafariClade, HashSet<string>>();
            var trip = new HashSet<string>(StringComparer.Ordinal);

            HashSet<string> UsedBy(SafariClade c)
            {
                if (!byClade.TryGetValue(c, out HashSet<string> set)) byClade[c] = set = new HashSet<string>(StringComparer.Ordinal);
                return set;
            }

            SafariFact Take(SafariScene scene, IEnumerable<SafariFact> pool, params string[] roles)
            {
                HashSet<string> used = scene.Clade != null ? UsedBy(scene.Clade) : null;
                foreach (SafariFact f in pool.Where(f => roles.Contains(f.Role)).OrderByDescending(f => f.Interest))
                {
                    if (used != null && used.Contains(f.Text)) continue;
                    if (trip.Contains(f.Text)) continue;
                    used?.Add(f.Text);
                    trip.Add(f.Text);
                    scene.Facts.Add(f);
                    return f;
                }
                return null;
            }

            IEnumerable<SafariFact> world = guide?.WorldFacts ?? (IEnumerable<SafariFact>)Array.Empty<SafariFact>();

            foreach (SafariScene scene in scenes)
            {
                scene.Facts.Clear();
                SafariClade c = scene.Clade;
                List<SafariFact> own = c?.Facts ?? new List<SafariFact>();

                switch (scene.Station)
                {
                    case SafariStation.Descent:
                        foreach (SafariFact f in world.Where(f => f.Role == "descent").OrderBy(f => f.DepthMetres))
                            if (trip.Add(f.Text)) scene.Facts.Add(f);
                        break;

                    case SafariStation.Floor:
                        Take(scene, world, "floor");
                        Take(scene, world, "floor");
                        break;

                    case SafariStation.Portrait:
                        if (own.Count == 0) break;
                        // "what" is said in the caption with the name, and never again for the clade.
                        UsedBy(c).Add(own.FirstOrDefault(f => f.Role == "what")?.Text ?? "");
                        Take(scene, own, "special", "origin");
                        Take(scene, own, "does");
                        Take(scene, own, "fate");
                        break;

                    case SafariStation.Birth:
                        if (own.Count == 0) break;
                        Take(scene, own, "origin");
                        break;

                    case SafariStation.Colony:
                        if (own.Count == 0) break;
                        Take(scene, own, "count");
                        if (Take(scene, own, "world") == null) Take(scene, world, "world");
                        if (Take(scene, own, "fate") == null) Take(scene, own, "special", "does", "origin");
                        Take(scene, own, "fate");
                        break;
                }
            }
        }

        // ---------------------------------------------------------------- filling the captions

        /// <summary>Fills a scene's captions from what is known before it plays.</summary>
        /// <param name="alive">The recording's living count at a second, or -1 when it has none.</param>
        public static void Fill(SafariScene scene, SafariGuide guide, string arm, Func<double, int> alive,
            float depthMetres, float radiusMetres)
        {
            scene.Captions.Clear();
            scene.Refill = () => Fill(scene, guide, arm, alive, depthMetres, radiusMetres);
            SafariClade c = scene.Clade;

            // A story's scene says what its writer wrote, at the writer's offsets, and nothing
            // else: no fact, no count and no explanation of the corner's word, which every frame
            // still carries.
            if (scene.StoryCaptions != null)
            {
                foreach (SafariCaption s in scene.StoryCaptions)
                    scene.Captions.Add(new SafariCaption { Offset = s.Offset, Text = s.Text, Seconds = s.Seconds });
                return;
            }

            // Every second a caption states is the second the clip is filmed at (the scene's At,
            // which the director moves to a checkpoint and then writes the captions again), or
            // is said to be another second. The arm's code is never in the frame.
            bool moved = !double.IsNaN(scene.AskedAt) && Math.Abs(scene.AskedAt - scene.At) > 0.5d;

            switch (scene.Station)
            {
                case SafariStation.Arrival:
                {
                    int n = alive(scene.At);
                    Add(scene, Slot(0), (n >= 0 ? Grouped(n) + " bodies alive, " : "") + Clock(scene.At) + " into the run.");
                    // The cousin word, explained once: every frame carries it in the corner.
                    Add(scene, Slot(1), "COUSIN means a replay from a saved second: close to the run, not the run.");
                    break;
                }

                case SafariStation.Descent:
                    // The depth marks are timed to the dolly when the director plans it.
                    Add(scene, Slot(0), radiusMetres > 0f
                        ? string.Format(CultureInfo.InvariantCulture, "The tank is {0:0} m deep and {1:0} m across.", depthMetres, 2f * radiusMetres)
                        : string.Format(CultureInfo.InvariantCulture, "The water is {0:0} m deep.", depthMetres));
                    break;

                case SafariStation.Portrait:
                {
                    Add(scene, Slot(0), c.Name + ": " + Guild(c) + ".");
                    if (scene.Facts.Count > 0)
                    {
                        for (int i = 0; i < scene.Facts.Count && i < 3; i++) Add(scene, Slot(i + 1), scene.Facts[i].Text);
                        break;
                    }

                    // An older guide: the card's own numbers.
                    Add(scene, Slot(1), "Founded " + Clock(c.FoundedAt) + " into the run" + FromClause(c, guide) + ".");
                    if (c.PeakCount >= 0)
                        Add(scene, Slot(2), "At its peak, " + Grouped(c.PeakCount) + " alive at " + Clock(c.PeakAt) + ".");
                    if (c.AliveAtEnd == true) Add(scene, Slot(3), "Still alive at the end of the run.");
                    else if (!double.IsNaN(c.ExtinctAt)) Add(scene, Slot(3), "Gone by " + Clock(c.ExtinctAt) + ".");
                    break;
                }

                case SafariStation.Birth:
                {
                    SafariFact origin = scene.Facts.FirstOrDefault(f => f.Role == "origin");
                    if (origin != null) Add(scene, Slot(0), origin.Text);
                    else
                    {
                        SafariClade parent = c.ParentClade >= 0 ? guide?.Find(c.ParentClade) : null;
                        Add(scene, Slot(0), "It began " + Clock(c.FoundedAt) + " into the run" +
                                            (parent != null ? ", from " + parent.Name : "") + ".");
                    }
                    // The birth itself is captioned by the director when the rehearsal has found it,
                    // at the lead; the scene's last caption comes after it.
                    SafariFact more = scene.Facts.FirstOrDefault(f => f.Role != "origin");
                    if (more != null) Add(scene, SafariTripBuilder.BirthLeadSeconds + Slot(1), more.Text);
                    break;
                }

                case SafariStation.Colony:
                {
                    // The chapter card plays first, from above, with its title.
                    double from = 0d;
                    if (scene.ChapterCard)
                    {
                        if (!string.IsNullOrEmpty(scene.ChapterTitle))
                            Add(scene, Slot(0), "Chapter " + scene.Chapter.ToString(CultureInfo.InvariantCulture) + ": " + scene.ChapterTitle + ".");
                        from = SafariTripBuilder.ChapterSeconds;
                    }

                    if (scene.Facts.Count > 0)
                    {
                        for (int i = 0; i < scene.Facts.Count && i < 4; i++) Add(scene, Slot(i, from), scene.Facts[i].Text);
                        break;
                    }

                    var facts = new List<string>();
                    if (c.MembersEver >= 0) facts.Add(Grouped(c.MembersEver) + " members ever");
                    if (c.GenerationDepth >= 0) facts.Add(c.GenerationDepth + " generations deep");
                    Add(scene, Slot(0, from), c.Name + (facts.Count > 0 ? ": " + string.Join(", ", facts) + "." : "."));
                    if (c.AliveAtEnd == true) Add(scene, Slot(1, from), "It was alive at the end of the run.");
                    else if (!double.IsNaN(c.ExtinctAt)) Add(scene, Slot(1, from), "It was gone by " + Clock(c.ExtinctAt) + ".");
                    if (moved && c.PeakCount >= 0 && !double.IsNaN(c.PeakAt))
                        Add(scene, Slot(2, from), "Filmed at " + Clock(scene.At) + "; its peak was " + Grouped(c.PeakCount) + " alive at " + Clock(c.PeakAt) + ".");
                    break;
                }

                case SafariStation.Floor:
                    if (scene.Facts.Count > 0)
                    {
                        for (int i = 0; i < scene.Facts.Count && i < 3; i++) Add(scene, Slot(i), scene.Facts[i].Text);
                    }
                    else
                    {
                        Add(scene, Slot(0), string.Format(CultureInfo.InvariantCulture, "The bed, about {0:0} m below the surface.", depthMetres));
                    }
                    break;

                case SafariStation.Time:
                {
                    int a = alive(scene.At);
                    int b = alive(scene.SecondAt);
                    Add(scene, Slot(0), Clock(scene.At) + " into the run" + (a >= 0 ? ": " + Grouped(a) + " alive." : "."));
                    Add(scene, SafariTripBuilder.TimeTakeSeconds + Slot(0), Clock(scene.SecondAt) + " into the run" + (b >= 0 ? ": " + Grouped(b) + " alive." : "."));
                    break;
                }

                case SafariStation.Card:
                    if (!string.IsNullOrEmpty(scene.ChapterTitle)) Add(scene, Slot(0), scene.ChapterTitle);
                    break;
            }
        }

        private static string FromClause(SafariClade c, SafariGuide guide)
        {
            if (c.ParentClade < 0) return ", from a random founder";
            SafariClade parent = guide?.Find(c.ParentClade);
            return parent != null ? ", from " + parent.Name : "";
        }

        public static void Add(SafariScene scene, double offset, string text) =>
            scene.Captions.Add(new SafariCaption { Offset = offset, Text = text });

        /// <summary>
        /// Puts a caption at the first free slot at or after an offset, so a caption the
        /// director writes late never lies over one already there; false when none is free
        /// before the scene's end.
        /// </summary>
        public static bool AddFree(SafariScene scene, double offset, string text, double sceneSeconds)
        {
            double at = offset;
            for (int tries = 0; tries < 64; tries++)
            {
                bool clash = scene.Captions.Any(c => at < c.Offset + c.Seconds + GapSeconds && c.Offset < at + OnScreenSeconds + GapSeconds);
                if (!clash)
                {
                    if (sceneSeconds > 0d && at + OnScreenSeconds > sceneSeconds + 1e-6) return false;
                    Add(scene, at, text);
                    return true;
                }
                at += 0.5d;
            }
            return false;
        }

        /// <summary>Replaces the caption at an offset (within half a second) with another sentence, or adds it.</summary>
        public static void Replace(SafariScene scene, double offset, string text)
        {
            SafariCaption there = scene.Captions.FirstOrDefault(c => Math.Abs(c.Offset - offset) < 0.5d);
            if (there != null) there.Text = text;
            else Add(scene, offset, text);
        }

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
