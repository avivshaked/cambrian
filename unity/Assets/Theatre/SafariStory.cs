using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Evosim.Core;

namespace Evosim.Theatre
{
    /// <summary>
    /// A writer's shot list for the safari (story mode): the scenes of one film across a round's
    /// runs, each a station at a second on a subject, with its own length and captions. A run's
    /// director films the story's scenes for that run, in the story's order, in place of the
    /// template's trip (<c>EVOSIM_THEATRE_SAFARI_STORY</c> and <c>EVOSIM_THEATRE_SAFARI_STORY_RUN</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The shot list is another agent's, written in parallel</b>, so this reader is written the
    /// way the guide's is: it takes the spellings a writer is likely to use for each field, never
    /// throws on a field it does not know, and says in its notes everything it could not map,
    /// which the host logs. The form it expects is
    /// <c>{"run", "title", "scenes": [{"n", "run", "act", "station", "second" (or "from" and "to"),
    /// "subject", "seconds", "captions": [{"at", "text"}], "why", "sources"}]}</c>, where <c>n</c>
    /// counts from 1 across the whole film and <c>seconds</c> is the length on screen.
    /// </para>
    /// <para>
    /// <b>What a scene becomes.</b> The station by its name. <c>NEW:&lt;kind&gt;</c> is a held
    /// title card (<see cref="SafariStation.Card"/>) when its kind, or failing that its
    /// description, is a title, a card or a chapter, and otherwise the nearest station by the
    /// words it uses. The subject is the root id its text carries, matched to the guide's clade by
    /// its founder. A root the guide has no card for gets a clade built from the lineage, a body
    /// id is followed inside its clade, and <c>world</c> is a world scene. A subject that cannot
    /// be placed is filmed as the world. Every scene but a birth is flexible within the
    /// director's snap rules, and a birth keeps its rehearsal near the story's second. The
    /// story's captions replace the guide's facts, and the corner still says COUSIN on every
    /// frame. Each such decision is one line of the notes.
    /// </para>
    /// </remarks>
    public sealed class SafariStory
    {
        /// <summary>One scene as the file gives it, before it meets a run.</summary>
        public sealed class Shot
        {
            /// <summary>The story's number, from 1 across the whole film; the file's order when the scene has none.</summary>
            public int Number;
            /// <summary>The scene's place in the file, which breaks a tie between two equal numbers.</summary>
            public int Position;
            public string Run;
            public string Act;
            public string StationText;
            /// <summary>A title the scene names for itself (<c>title</c>), or null.</summary>
            public string Title;
            /// <summary>A <c>NEW:</c> scene's description, or a scene's own <c>description</c>, or null.</summary>
            public string Description;
            public double Second = double.NaN;
            public double From = double.NaN;
            public double To = double.NaN;
            public string SubjectText;
            /// <summary>A clade's root named as such (<c>root 48048</c>), or -1.</summary>
            public long Root = -1;
            /// <summary>A body named as such (<c>body 51234</c>), or -1.</summary>
            public long Body = -1;
            /// <summary>A number the subject carries with no word to say which it is, or -1.</summary>
            public long Loose = -1;
            /// <summary>The guide's own number for the clade (<c>guide clade 42</c>), a fallback after the root, or -1.</summary>
            public long GuideIndex = -1;
            /// <summary>A birth's parent named in the subject (<c>parent body 44820</c>), or -1.</summary>
            public long ParentBody = -1;
            /// <summary>The chapter the scene opens (<c>chapter</c>), or null.</summary>
            public string Chapter;
            /// <summary>The writer's own switch: false holds the scene at its second; null leaves it to the station.</summary>
            public bool? Flexible;
            /// <summary>The writer's canopy switch, or null.</summary>
            public bool? Canopy;
            /// <summary>The writer's reason for the scene, read only for a request the director can honour (the canopy).</summary>
            public string Why;
            /// <summary>The name the subject carries (<c>Phyllina vetrasis</c>), or null.</summary>
            public string Name;
            public bool World;
            /// <summary>The length on screen, s, or NaN for the station's own.</summary>
            public double Seconds = double.NaN;
            /// <summary>The story's captions, or null when it gave none.</summary>
            public List<SafariCaption> Captions;
        }

        public string Path { get; private set; }
        public string Title { get; private set; }
        /// <summary>The top level's run, which a scene with none of its own belongs to, or null.</summary>
        public string Run { get; private set; }
        public IReadOnlyList<Shot> Shots => _shots;
        /// <summary>What the reader could not map or had to guess, one line each, for the log.</summary>
        public readonly List<string> Notes = new List<string>();

        private readonly List<Shot> _shots = new List<Shot>();

        /// <summary>The runs the story's scenes name, in the story's order.</summary>
        public IEnumerable<string> Runs => _shots.Select(s => s.Run ?? Run ?? "(none)").Distinct();

        // ---------------------------------------------------------------- reading

        private static readonly string[] SceneListKeys = { "scenes", "shots", "shot_list", "shotList", "shotlist", "list", "film" };

        private static readonly string[] NumberKeys = { "n", "number", "scene_n", "sceneNumber", "scene_number", "no", "num", "index", "order", "scene" };
        private static readonly string[] RunKeys = { "run", "arm", "seed", "run_name", "runName", "arm_name" };
        private static readonly string[] ActKeys = { "act", "part", "act_name" };
        private static readonly string[] ChapterKeys = { "chapter", "chapter_title", "chapterTitle", "chapter_card" };
        private static readonly string[] FlexibleKeys = { "flexible", "flex", "movable", "may_move" };
        private static readonly string[] CanopyKeys = { "canopy", "canopy_shot" };
        private static readonly string[] WhyKeys = { "why", "reason" };
        private static readonly string[] StationKeys = { "station", "kind", "type", "shot", "shot_type", "shotType" };
        private static readonly string[] SecondKeys = { "second", "at", "t", "time", "run_second", "sim_second", "at_s", "second_s", "when" };
        private static readonly string[] FromKeys = { "from", "start", "from_s", "start_s", "second_from" };
        private static readonly string[] ToKeys = { "to", "end", "until", "to_s", "end_s", "second_to" };
        private static readonly string[] SpanKeys = { "span", "window", "range", "seconds_span" };
        private static readonly string[] SubjectKeys = { "subject", "who", "focus", "target", "clade" };
        private static readonly string[] RootKeys = { "root", "root_id", "rootId", "founder", "founder_id", "founderId" };
        private static readonly string[] BodyKeys = { "body", "body_id", "bodyId" };
        private static readonly string[] LengthKeys = { "seconds", "length", "duration", "screen_seconds", "screenSeconds", "screen_s", "screen", "len", "length_s", "duration_s", "on_screen" };
        private static readonly string[] CaptionKeys = { "captions", "caption", "lines", "narration", "subtitles", "text" };
        private static readonly string[] TitleKeys = { "title", "card_title", "cardTitle", "heading" };
        private static readonly string[] DescriptionKeys = { "description", "desc", "new", "note", "notes", "what" };

        /// <summary>Keys a scene may carry that the director has no use for: read past, never noted.</summary>
        private static readonly string[] Unused = { "sources", "source", "refs", "references", "id", "transition", "mood", "music", "comment" };

        private static readonly HashSet<string> KnownSceneKeys = new HashSet<string>(
            NumberKeys.Concat(RunKeys).Concat(ActKeys).Concat(StationKeys).Concat(SecondKeys).Concat(FromKeys).Concat(ToKeys)
                .Concat(SpanKeys).Concat(SubjectKeys).Concat(RootKeys).Concat(BodyKeys).Concat(LengthKeys).Concat(CaptionKeys)
                .Concat(TitleKeys).Concat(DescriptionKeys).Concat(ChapterKeys).Concat(FlexibleKeys).Concat(CanopyKeys).Concat(WhyKeys)
                .Concat(Unused), StringComparer.Ordinal);

        /// <summary>Reads a shot list, or refuses a file that is not JSON or holds no scenes.</summary>
        public static SafariStory Read(string path, out string refusal)
        {
            refusal = null;
            JsonNode root;
            try
            {
                root = Json.Parse(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                refusal = "the story at '" + path + "' does not parse: " + e.Message;
                return null;
            }

            var story = new SafariStory { Path = path };
            try
            {
                story.Build(root);
            }
            catch (Exception e)
            {
                refusal = "the story at '" + path + "' is not a shot list: " + e.GetType().Name + ": " + e.Message;
                return null;
            }

            if (story._shots.Count == 0)
            {
                refusal = "the story at '" + path + "' has no scenes: it wants a 'scenes' array (or one of " +
                          string.Join(", ", SceneListKeys.Skip(1).Select(k => "'" + k + "'")) + "), or to be an array itself.";
                return null;
            }

            return story;
        }

        private void Build(JsonNode root)
        {
            var scenes = new List<(JsonNode node, string act)>();

            if (root.Kind == JsonNode.NodeKind.Array)
            {
                foreach (JsonNode s in root.Items()) scenes.Add((s, null));
            }
            else if (root.Kind == JsonNode.NodeKind.Object)
            {
                Title = Text(FirstOf(root, "title", "name"));
                Run = Text(FirstOf(root, RunKeys));

                JsonNode list = FirstOf(root, SceneListKeys);
                if (list != null && list.Kind == JsonNode.NodeKind.Array)
                {
                    foreach (JsonNode s in list.Items()) scenes.Add((s, null));
                }

                // A story written in acts: each act's scenes, carrying the act's name.
                JsonNode acts = FirstOf(root, "acts");
                if (acts != null && acts.Kind == JsonNode.NodeKind.Array)
                {
                    foreach (JsonNode act in acts.Items())
                    {
                        if (act.Kind != JsonNode.NodeKind.Object) continue;
                        string name = Text(FirstOf(act, "act", "name", "title"));
                        JsonNode inner = FirstOf(act, SceneListKeys);
                        if (inner == null || inner.Kind != JsonNode.NodeKind.Array) continue;
                        foreach (JsonNode s in inner.Items()) scenes.Add((s, name));
                    }
                }

                var known = new HashSet<string>(SceneListKeys.Concat(RunKeys), StringComparer.Ordinal)
                {
                    "title", "name", "acts", "why", "sources", "source", "notes", "note", "description", "round", "author", "version",
                    "date", "logline", "summary", "runs", "provisional", "screen_seconds", "chapter_cards",
                };
                string[] unread = root.Keys().Where(k => !known.Contains(k)).ToArray();
                if (unread.Length > 0) Notes.Add("top-level keys not read: " + string.Join(", ", unread));
            }

            var unknown = new SortedDictionary<string, List<int>>(StringComparer.Ordinal);

            for (int i = 0; i < scenes.Count; i++)
            {
                JsonNode node = scenes[i].node;
                if (node.Kind != JsonNode.NodeKind.Object)
                {
                    Notes.Add("scene " + (i + 1) + " in the file is not an object and is skipped");
                    continue;
                }

                try
                {
                    Shot shot = ReadShot(node, i);
                    if (shot.Act == null) shot.Act = scenes[i].act;
                    _shots.Add(shot);
                    foreach (string k in node.Keys())
                    {
                        if (KnownSceneKeys.Contains(k)) continue;
                        if (!unknown.TryGetValue(k, out List<int> where)) unknown[k] = where = new List<int>();
                        where.Add(shot.Number);
                    }
                }
                catch (Exception e)
                {
                    Notes.Add("scene " + (i + 1) + " in the file could not be read and is skipped: " + e.GetType().Name + ": " + e.Message);
                }
            }

            if (unknown.Count > 0)
            {
                Notes.Add("scene keys not read: " + string.Join("; ", unknown.Select(kv =>
                    kv.Key + " (scene" + (kv.Value.Count > 1 ? "s " : " ") + string.Join(", ", kv.Value.Take(8)) + (kv.Value.Count > 8 ? ", ..." : "") + ")")));
            }

            foreach (var twice in _shots.GroupBy(s => s.Number).Where(g => g.Count() > 1))
                Notes.Add("the number " + twice.Key + " is given to " + twice.Count() + " scenes: they play in the file's order");
        }

        private Shot ReadShot(JsonNode node, int position)
        {
            var shot = new Shot { Position = position, Number = position + 1 };

            if (TryWhole(FirstOf(node, NumberKeys), out int n)) shot.Number = n;
            else if (FirstOf(node, NumberKeys) != null) Notes.Add("scene " + (position + 1) + " in the file: its number is not a number, so it takes its place in the file");

            shot.Run = Text(FirstOf(node, RunKeys));
            shot.Act = Text(FirstOf(node, ActKeys));
            shot.StationText = Text(FirstOf(node, StationKeys));
            shot.Title = Text(FirstOf(node, TitleKeys));
            shot.Description = Text(FirstOf(node, DescriptionKeys));
            shot.Chapter = Text(FirstOf(node, ChapterKeys));
            shot.Flexible = Switch(FirstOf(node, FlexibleKeys));
            shot.Canopy = Switch(FirstOf(node, CanopyKeys));
            shot.Why = Text(FirstOf(node, WhyKeys));
            if (FirstOf(node, FlexibleKeys) != null && shot.Flexible == null)
                Notes.Add("scene " + shot.Number + ": 'flexible' is neither true nor false, and is not read");

            // The second, or a span: "second" may itself hold a span.
            JsonNode second = FirstOf(node, SecondKeys);
            if (TrySpan(second, out double a, out double b)) { shot.From = a; shot.To = b; }
            else if (TryNumber(second, out double s)) shot.Second = s;

            if (TryNumber(FirstOf(node, FromKeys), out double from)) shot.From = from;
            if (TryNumber(FirstOf(node, ToKeys), out double to)) shot.To = to;
            if (TrySpan(FirstOf(node, SpanKeys), out a, out b)) { shot.From = a; shot.To = b; }
            if (double.IsNaN(shot.Second) && !double.IsNaN(shot.From)) shot.Second = shot.From;

            if (TryNumber(FirstOf(node, LengthKeys), out double length) && length > 0d) shot.Seconds = length;

            ReadSubject(node, shot);
            shot.Captions = ReadCaptions(FirstOf(node, CaptionKeys), shot.Number);
            return shot;
        }

        // ---------------------------------------------------------------- the subject

        private static readonly Regex RootWord = new Regex(@"\b(?:root|founder)(?:\s*(?:id|body))?\s*[:#=]?\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        /// <summary>A body named as one, and not as a founder's, a parent's or a child's (<c>founder body 43</c> is a root).</summary>
        private static readonly Regex BodyWord = new Regex(@"(?<!\b(?:founder|parent|child|root)\s+)\bbody(?:\s*id)?\s*[:#=]?\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex ParentBodyWord = new Regex(@"\bparent\s+body\s*[:#=]?\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex ChildBodyWord = new Regex(@"\bchild\s+body\s*[:#=]?\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex GuideIndexWord = new Regex(@"\bguide\s+clade\s*[:#=]?\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        /// <summary>A clade's name as the guide writes it: a capitalised genus, a species and an optional numeral.</summary>
        private static readonly Regex Binomial = new Regex(@"\b([A-Z][a-z]+ [a-z]{2,}(?: [IVX]+\b)?)", RegexOptions.CultureInvariant);
        private static readonly Regex LooseNumber = new Regex(@"(?<![A-Za-z0-9.,])(\d+)(?![0-9.,])", RegexOptions.CultureInvariant);
        private static readonly string[] WorldWords = { "world", "the world", "tank", "the tank", "the whole tank", "everyone", "everything", "none", "nobody", "n/a", "-" };

        private void ReadSubject(JsonNode node, Shot shot)
        {
            JsonNode subject = FirstOf(node, SubjectKeys);

            // An object: {"clade": name, "root": id, "body": id} in any of their spellings.
            if (subject != null && subject.Kind == JsonNode.NodeKind.Object)
            {
                if (TryNumber(FirstOf(subject, RootKeys), out double r)) shot.Root = (long)Math.Round(r);
                if (TryNumber(FirstOf(subject, BodyKeys), out double bo)) shot.Body = (long)Math.Round(bo);
                if (TryNumber(FirstOf(subject, "id"), out double id) && shot.Root < 0 && shot.Body < 0) shot.Loose = (long)Math.Round(id);
                shot.SubjectText = Text(FirstOf(subject, "name", "clade", "text", "label"));
                if (shot.SubjectText != null) ParseSubjectText(shot.SubjectText, shot);
                return;
            }

            if (subject != null && subject.Kind == JsonNode.NodeKind.Number)
            {
                shot.Loose = (long)Math.Round(subject.AsDouble());
                shot.SubjectText = shot.Loose.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                shot.SubjectText = Text(subject);
                if (shot.SubjectText != null) ParseSubjectText(shot.SubjectText, shot);
            }

            // Ids beside the subject rather than in it; a body named by its own key is the one the
            // scene follows, whatever the subject's prose mentions.
            if (shot.Root < 0 && TryNumber(FirstOf(node, RootKeys), out double root)) shot.Root = (long)Math.Round(root);
            if (TryNumber(FirstOf(node, BodyKeys), out double body)) { shot.Body = (long)Math.Round(body); shot.World = false; }
            if (subject == null && shot.Root < 0 && shot.Body < 0) shot.World = true;
        }

        /// <summary>
        /// A subject's text read for what it names: a root id (<c>root 48048</c>), a body id
        /// (<c>body 512</c>), a bare number, a name, or the world.
        /// </summary>
        private static void ParseSubjectText(string text, Shot shot)
        {
            string t = text.Trim();
            string lower = t.ToLowerInvariant();

            Match root = RootWord.Match(t);
            Match body = BodyWord.Match(t);
            Match parent = ParentBodyWord.Match(t);
            Match guide = GuideIndexWord.Match(t);
            if (root.Success) shot.Root = long.Parse(root.Groups[1].Value, CultureInfo.InvariantCulture);
            if (body.Success) shot.Body = long.Parse(body.Groups[1].Value, CultureInfo.InvariantCulture);
            if (parent.Success) shot.ParentBody = long.Parse(parent.Groups[1].Value, CultureInfo.InvariantCulture);
            if (guide.Success) shot.GuideIndex = long.Parse(guide.Groups[1].Value, CultureInfo.InvariantCulture);

            // The world, when the text opens with a world word and names no id by its kind; a
            // number in "the world at 5,000 s" is a second, not an id.
            bool worldWord = WorldWords.Any(w => lower == w || lower.StartsWith(w + " ", StringComparison.Ordinal) ||
                                                 lower.StartsWith(w + ",", StringComparison.Ordinal) || lower.StartsWith(w + "(", StringComparison.Ordinal) ||
                                                 lower.StartsWith(w + ":", StringComparison.Ordinal));
            if (worldWord && !root.Success && !body.Success)
            {
                shot.World = true;
                return;
            }

            // The name: a genus and species as the guide writes them, the first in the text ("from
            // Frondium nisimocrax" after it is the parent's); else, for a short subject, what is
            // left once the brackets and the ids are gone.
            Match binomial = Binomial.Match(Regex.Replace(t, @"\([^)]*\)", " "));
            if (!binomial.Success) binomial = Binomial.Match(t);
            if (binomial.Success) shot.Name = binomial.Groups[1].Value;
            else
            {
                string name = Regex.Replace(t, @"\([^)]*\)", " ");
                foreach (Regex r in new[] { RootWord, BodyWord, ParentBodyWord, ChildBodyWord, GuideIndexWord }) name = r.Replace(name, " ");
                name = LooseNumber.Replace(name, " ");
                name = Regex.Replace(name, @"\s+", " ").Trim(' ', ',', ';', ':', '-', '.', '/');
                if (name.Length > 0 && name.Length <= 40 && name.Split(' ').Length <= 4 && char.IsUpper(name[0]) &&
                    !WorldWords.Contains(name.ToLowerInvariant()))
                    shot.Name = name;
            }

            // A bare number only when nothing else names the subject: "Gastrella cidutis III (guide
            // clade 180)" carries 180, which is the guide's number and not a body.
            if (!root.Success && !body.Success && !guide.Success && !parent.Success && shot.Name == null)
            {
                Match loose = LooseNumber.Match(t);
                if (loose.Success) shot.Loose = long.Parse(loose.Groups[1].Value, CultureInfo.InvariantCulture);
            }
        }

        // ---------------------------------------------------------------- the captions

        private List<SafariCaption> ReadCaptions(JsonNode node, int number)
        {
            if (node == null || node.Kind == JsonNode.NodeKind.Null) return null;
            var captions = new List<SafariCaption>();

            if (node.Kind == JsonNode.NodeKind.String)
            {
                if (!string.IsNullOrWhiteSpace(node.AsString()))
                    captions.Add(new SafariCaption { Offset = SafariCaptions.Slot(0), Text = Printable(node.AsString(), number) });
                return captions;
            }

            if (node.Kind != JsonNode.NodeKind.Array)
            {
                Notes.Add("scene " + number + ": its captions are neither a list nor a sentence, and are not read");
                return null;
            }

            int i = 0;
            foreach (JsonNode c in node.Items())
            {
                string text;
                double at = SafariCaptions.Slot(i);
                double seconds = SafariCaptions.OnScreenSeconds;

                if (c.Kind == JsonNode.NodeKind.String) text = c.AsString();
                else if (c.Kind == JsonNode.NodeKind.Object)
                {
                    text = Text(FirstOf(c, "text", "line", "caption", "words", "say"));
                    if (TryNumber(FirstOf(c, "at", "offset", "t", "start", "from", "time", "at_s"), out double o)) at = o;
                    else Notes.Add("scene " + number + ": caption " + (i + 1) + " has no 'at' and is put at " + at.ToString("0.#", CultureInfo.InvariantCulture) + " s");
                    if (TryNumber(FirstOf(c, "for", "seconds", "duration", "length", "hold"), out double d) && d > 0d) seconds = d;
                }
                else text = null;

                i++;
                if (string.IsNullOrWhiteSpace(text))
                {
                    Notes.Add("scene " + number + ": caption " + i + " has no text and is skipped");
                    continue;
                }

                captions.Add(new SafariCaption { Offset = Math.Max(0d, at), Text = Printable(text, number), Seconds = seconds });
            }

            return captions;
        }

        /// <summary>The caption font's characters (<see cref="SnapshotCamera"/>'s table), letters taken upper case as it draws them.</summary>
        private const string FontCharacters = " ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.,:;-+=_/()[]%!?'\\*#\u00B7";

        /// <summary>
        /// A caption in the characters the burned-in font has: typographic dashes, quotes and
        /// ellipses as their plain forms, and a note naming anything still outside the font,
        /// which draws as a hollow box.
        /// </summary>
        private string Printable(string text, int number)
        {
            var sb = new StringBuilder(text.Length + 8);
            foreach (char ch in text)
            {
                switch (ch)
                {
                    case '\u2014': sb.Append(" - "); break;                      // em dash
                    case '\u2013': case '\u2212': case '\u2010': case '\u2011': sb.Append('-'); break;
                    case '\u2018': case '\u2019': case '\u201B': case '\u2032': sb.Append('\''); break;
                    case '\u201C': case '\u201D': case '\u201E': case '\u2033': case '"': sb.Append('\''); break;
                    case '\u2026': sb.Append("..."); break;
                    case '\u00D7': sb.Append('x'); break;
                    case '\u00B2': sb.Append('2'); break;
                    case '\u00B3': sb.Append('3'); break;
                    case '&': sb.Append("and"); break;
                    case '\u00A0': case '\t': case '\n': case '\r': sb.Append(' '); break;
                    default: sb.Append(ch); break;
                }
            }

            string plain = Regex.Replace(sb.ToString(), @" {2,}", " ").Trim();
            string missing = new string(plain.ToUpperInvariant().Where(ch => FontCharacters.IndexOf(ch) < 0).Distinct().ToArray());
            if (missing.Length > 0)
                Notes.Add("scene " + number + ": the caption font has no '" + missing + "', which draws as a hollow box: \"" + plain + "\"");
            return plain;
        }

        // ---------------------------------------------------------------- the station

        /// <summary>The words that send an unknown or NEW kind to a station, checked in this order.</summary>
        private static readonly (SafariStation station, string[] words)[] Nearest =
        {
            (SafariStation.Birth, new[] { "birth", "born", "bud", "budding", "offspring", "newborn", "child", "split" }),
            (SafariStation.Time, new[] { "time", "timelapse", "time-lapse", "then and now", "before and after", "compare", "comparison" }),
            (SafariStation.Floor, new[] { "floor", "bed", "sand", "seabed", "bottom", "sediment", "detritus", "snow", "corpse", "corpses", "grave", "graveyard" }),
            (SafariStation.Descent, new[] { "descent", "descend", "descending", "dive", "diving", "sink", "sinking", "depth", "depths" }),
            (SafariStation.Colony, new[] { "colony", "colonies", "crowd", "swarm", "population", "census", "flock", "school", "herd", "cluster", "pull-back", "pullback", "pull back" }),
            (SafariStation.Portrait, new[] { "portrait", "close", "closeup", "close-up", "orbit", "follow", "macro", "detail", "anatomy", "face", "swim", "swimmer", "joint", "joints", "individual" }),
            (SafariStation.Arrival, new[] { "arrival", "establishing", "overview", "tank", "glass", "surface", "aerial", "canopy", "window", "wide" }),
        };

        private static readonly string[] CardWords = { "title", "card", "chapter", "intertitle", "credit", "credits", "caption card", "text card" };

        private static bool HasWord(string text, string word) =>
            !string.IsNullOrEmpty(text) &&
            Regex.IsMatch(text, @"(?<![a-z])" + Regex.Escape(word) + @"(?![a-z])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>A station's name, or null.</summary>
        private static SafariStation? Named(string word)
        {
            switch ((word ?? "").Trim().ToLowerInvariant())
            {
                case "arrival": case "arrive": return SafariStation.Arrival;
                case "descent": case "descend": case "dive": return SafariStation.Descent;
                case "portrait": return SafariStation.Portrait;
                case "floor": case "bed": return SafariStation.Floor;
                case "birth": return SafariStation.Birth;
                case "colony": return SafariStation.Colony;
                case "time": case "timelapse": case "time-lapse": return SafariStation.Time;
                case "card": case "title": case "chapter": case "title card": case "chapter card": return SafariStation.Card;
                default: return null;
            }
        }

        /// <summary>
        /// The station a scene is filmed as, and the note that says why when it is not the
        /// scene's own word. <paramref name="hasSubject"/> is whether it names a clade or a body.
        /// </summary>
        private static SafariStation StationOf(Shot shot, bool hasSubject, out string note, out string description)
        {
            note = null;
            description = shot.Description;
            string raw = (shot.StationText ?? "").Trim();

            SafariStation? named = Named(raw);
            if (named.HasValue) return named.Value;

            // A station's name with more after it ("Portrait, close"): the name.
            Match first = Regex.Match(raw, @"^[A-Za-z]+");
            if (first.Success && !raw.StartsWith("new", StringComparison.OrdinalIgnoreCase))
            {
                named = Named(first.Value);
                if (named.HasValue)
                {
                    note = "'" + raw + "' is read as " + named.Value;
                    return named.Value;
                }
            }

            // NEW:<kind> with a description after it (a dash, a colon, a bracket or a comma).
            string kind = raw;
            bool isNew = raw.StartsWith("new", StringComparison.OrdinalIgnoreCase) &&
                         (raw.Length == 3 || !char.IsLetter(raw[3]));
            if (isNew)
            {
                string rest = raw.Substring(3).TrimStart(' ', ':', '-', '\u2014', '\u2013');
                Match m = Regex.Match(rest, @"^(?<kind>[^\u2014\u2013:(,;]*?)\s*(?:[\u2014\u2013:(,;]|\s-\s|$)(?<desc>.*)$", RegexOptions.Singleline);
                kind = m.Success ? m.Groups["kind"].Value.Trim() : rest.Trim();
                string desc = m.Success ? m.Groups["desc"].Value.Trim().TrimEnd(')').Trim() : "";
                if (desc.Length > 0) description = string.IsNullOrEmpty(description) ? desc : desc + "; " + description;

                named = Named(kind);
                if (named.HasValue && named.Value != SafariStation.Card)
                {
                    note = "NEW:" + kind + " is the " + named.Value + " station by its name";
                    return named.Value;
                }
            }

            string label = isNew ? "NEW:" + kind : raw.Length > 0 ? "'" + raw + "'" : "a scene with no station";

            // A title, a card or a chapter, by the kind first and the description after it.
            if (CardWords.Any(w => HasWord(kind, w)))
            {
                note = label + " is a title card by its kind";
                return SafariStation.Card;
            }

            foreach (var (station, words) in Nearest)
            {
                string hit = words.FirstOrDefault(w => HasWord(kind, w));
                if (hit != null) { note = label + " is filmed as the nearest station, " + station + " (its kind says '" + hit + "')"; return station; }
            }

            string about = description; // a lambda cannot read an out parameter
            if (CardWords.Any(w => HasWord(about, w)))
            {
                note = label + " is a title card by its description";
                return SafariStation.Card;
            }

            foreach (var (station, words) in Nearest)
            {
                string hit = words.FirstOrDefault(w => HasWord(about, w));
                if (hit != null) { note = label + " is filmed as the nearest station, " + station + " (its description says '" + hit + "')"; return station; }
            }

            SafariStation fallback = hasSubject ? SafariStation.Portrait : SafariStation.Arrival;
            note = label + " names no station and no word of one: filmed as " + fallback + ", the nearest for " +
                   (hasSubject ? "a clade or a body" : "the world");
            return fallback;
        }

        // ---------------------------------------------------------------- the trip

        /// <summary>True when a story's run names this arm: the arm itself, or its seed (<c>s1</c>, <c>seed 1</c>, <c>1</c>).</summary>
        public static bool SameRun(string storyRun, string arm)
        {
            if (string.IsNullOrWhiteSpace(storyRun) || string.IsNullOrWhiteSpace(arm)) return false;
            string a = storyRun.Trim().ToLowerInvariant(), b = arm.Trim().ToLowerInvariant();
            if (a == b) return true;
            if (Regex.Replace(a, @"[\s_/]+", "-") == b) return true;
            Match seed = Regex.Match(a, @"^(?:seed\s*|s)?(\d+)$");
            return seed.Success && b.EndsWith("-s" + seed.Groups[1].Value, StringComparison.Ordinal);
        }

        /// <summary>
        /// The trip for one arm: the story's scenes for it in the story's order, each made a
        /// scene the director can play, with a note for everything that was mapped rather than
        /// read.
        /// </summary>
        /// <param name="lineage">The run's clade map, asked for only when a subject is not in the guide.</param>
        /// <param name="checkpoints">The run's checkpoint seconds, ascending.</param>
        /// <param name="runSeconds">The run's length, for a time scene given one second.</param>
        public List<SafariScene> Trip(string arm, SafariGuide guide, Func<SafariClades> lineage,
            IReadOnlyList<double> checkpoints, double runSeconds, out List<string> notes)
        {
            notes = new List<string>();
            var scenes = new List<SafariScene>();
            var built = new Dictionary<long, SafariClade>();
            SafariClades map = null;
            SafariClades Lineage() => map ?? (map = lineage?.Invoke());

            List<Shot> mine = _shots
                .Where(s => SameRun(s.Run ?? Run, arm))
                .OrderBy(s => s.Number).ThenBy(s => s.Position).ToList();

            int unowned = _shots.Count(s => string.IsNullOrWhiteSpace(s.Run) && string.IsNullOrWhiteSpace(Run));
            if (unowned > 0) notes.Add(unowned + " scene(s) name no run and are filmed by none: " +
                                       string.Join(", ", _shots.Where(s => string.IsNullOrWhiteSpace(s.Run)).Select(s => s.Number)));

            foreach (Shot shot in mine)
            {
                try
                {
                    SafariScene scene = SceneOf(shot, arm, guide, Lineage, built, checkpoints, runSeconds, notes);
                    if (scene != null) scenes.Add(scene);
                }
                catch (Exception e)
                {
                    notes.Add("story " + shot.Number + ": skipped, it could not be made a scene: " + e.GetType().Name + ": " + e.Message);
                }
            }

            // A scene with no second (a card, most often) is filmed where its neighbour is, so it
            // costs no seek of its own: the next scene's second, or the last one's.
            for (int i = 0; i < scenes.Count; i++)
            {
                SafariScene s = scenes[i];
                if (!double.IsNaN(s.At)) continue;
                double near = scenes.Skip(i + 1).Select(k => k.At).FirstOrDefault(k => !double.IsNaN(k));
                if (near == 0d || double.IsNaN(near))
                    near = scenes.Take(i).Select(k => k.At).LastOrDefault(k => !double.IsNaN(k));
                if (near == 0d || double.IsNaN(near)) near = checkpoints.Count > 0 ? checkpoints[0] : 0d;
                s.At = near;
                notes.Add(string.Format(CultureInfo.InvariantCulture, "story {0}: no second given, so it is filmed at its neighbour's, {1:0.#} s", s.StoryNumber, near));
                if (s.Station == SafariStation.Time && double.IsNaN(s.SecondAt)) s.SecondAt = LastAfter(checkpoints, s.At, runSeconds);
            }

            for (int i = 0; i < scenes.Count; i++) scenes[i].Index = i;
            return scenes;
        }

        private static double LastAfter(IReadOnlyList<double> checkpoints, double at, double runSeconds)
        {
            double last = checkpoints.Count > 0 ? checkpoints[checkpoints.Count - 1] : runSeconds;
            return last > at + 1d ? last : Math.Max(at + 1d, runSeconds);
        }

        private SafariScene SceneOf(Shot shot, string arm, SafariGuide guide, Func<SafariClades> lineage,
            Dictionary<long, SafariClade> built, IReadOnlyList<double> checkpoints, double runSeconds, List<string> notes)
        {
            string who = "story " + shot.Number;
            double second = shot.Second;

            // The subject first, since the nearest station for a NEW kind depends on whether it has one.
            SafariClade clade = null;
            long body = -1;
            if (!shot.World) Place(shot, guide, lineage, built, second, notes, who, out clade, out body);
            bool hasSubject = clade != null || body >= 0;
            bool askedForSubject = !shot.World && (shot.Root >= 0 || shot.Body >= 0 || shot.Loose >= 0 || shot.Name != null);

            SafariStation station = StationOf(shot, hasSubject || askedForSubject, out string stationNote, out string description);
            if (stationNote != null) notes.Add(who + ": " + stationNote);

            // A station that needs a clade, without one, is filmed as the world.
            bool needsClade = station == SafariStation.Colony || station == SafariStation.Birth ||
                              (station == SafariStation.Portrait && body < 0);
            if (needsClade && clade == null)
            {
                notes.Add(who + ": WARNING: a " + station + " of '" + (shot.SubjectText ?? "no subject") +
                          "', which could not be placed in a clade, is filmed as a world scene (an Arrival)");
                station = SafariStation.Arrival;
            }

            if (station == SafariStation.Card) { clade = null; body = -1; }

            var scene = new SafariScene
            {
                Station = station,
                Clade = clade,
                Body = body >= 0 ? body : station == SafariStation.Portrait && clade != null ? clade.Exemplar : -1,
                At = second,
                Flexible = station != SafariStation.Birth && shot.Flexible != false,
                StoryNumber = shot.Number,
                StoryArm = arm,
                Act = shot.Act,
            };

            // The length on screen: the story's, or the station's own.
            double own = StationSeconds(station);
            scene.Seconds = !double.IsNaN(shot.Seconds) ? shot.Seconds : own;
            if (double.IsNaN(shot.Seconds))
                notes.Add(string.Format(CultureInfo.InvariantCulture, "{0}: no length given, so the station's own, {1}", who,
                    own > 0d ? own.ToString("0.#", CultureInfo.InvariantCulture) + " s" : "the dolly's"));

            if (station == SafariStation.Time)
            {
                if (!double.IsNaN(shot.From) && !double.IsNaN(shot.To) && shot.To > shot.From)
                {
                    scene.At = shot.From;
                    scene.SecondAt = shot.To;
                }
                else if (!double.IsNaN(second))
                {
                    scene.SecondAt = LastAfter(checkpoints, second, runSeconds);
                    notes.Add(string.Format(CultureInfo.InvariantCulture,
                        "{0}: a time scene with one second; its second take is the run's last checkpoint, {1:0.#} s", who, scene.SecondAt));
                }
            }
            else if (!double.IsNaN(shot.To) && !double.IsNaN(shot.From))
            {
                notes.Add(string.Format(CultureInfo.InvariantCulture,
                    "{0}: a span of {1:0.#} to {2:0.#} s on a {3}, which plays at one second: filmed from {1:0.#} s", who, shot.From, shot.To, station));
            }

            if (shot.Flexible == false)
            {
                notes.Add(station == SafariStation.Card
                    ? who + ": 'flexible: false' on a card, which is held wherever it opens: not read"
                    : string.Format(CultureInfo.InvariantCulture, "{0}: held at {1:0.#} s by its writer: never moved to a checkpoint", who, second));
            }
            if (shot.Flexible == true && station == SafariStation.Birth)
                notes.Add(who + ": 'flexible: true' on a birth, whose rehearsal keeps it near its second: not read");

            // The canopy, for an arrival or a descent: the writer's switch, or the word in the
            // scene's station, description or reason (round 48's scene 2 asks in its "why").
            if (station == SafariStation.Arrival || station == SafariStation.Descent)
            {
                if (shot.Canopy.HasValue) scene.Canopy = shot.Canopy;
                else
                {
                    string where = HasWord(shot.StationText, "canopy") ? "station"
                        : HasWord(description, "canopy") ? "description"
                        : HasWord(shot.Why, "canopy") ? "reason" : null;
                    if (where != null)
                    {
                        scene.Canopy = true;
                        notes.Add(who + ": the canopy shot, which the scene's " + where + " asks for");
                    }
                }
            }
            else if (shot.Canopy == true)
            {
                notes.Add(who + ": the canopy is asked for on a " + station + ", which has none: not read");
            }

            if (station == SafariStation.Birth && shot.ParentBody >= 0)
            {
                scene.BirthParentBody = shot.ParentBody;
                notes.Add(who + ": the rehearsal takes a child of body " + shot.ParentBody + ", the parent the subject names, when it is alive");
            }

            // A held card's picture, darkened when its writer asks for that ("dimmed", "black").
            if (station == SafariStation.Card)
            {
                string asked = (shot.StationText ?? "") + " " + (description ?? "");
                scene.Dim = HasWord(asked, "dim") || HasWord(asked, "dimmed") || HasWord(asked, "darkened") ? DimmedCard
                    : HasWord(asked, "black") ? 1f : 0f;
                if (scene.Dim > 0f)
                    notes.Add(string.Format(CultureInfo.InvariantCulture, "{0}: the card's disc is {1}", who,
                        scene.Dim >= 1f ? "black" : "dimmed to " + (1f - scene.Dim).ToString("0.##", CultureInfo.InvariantCulture) + " of itself"));
            }

            if (station == SafariStation.Birth && clade != null)
            {
                // The template's birth is the clade's own founding, a child of its parent clade.
                // A story's birth far from the founding is a birth inside the clade.
                double wait = new SafariOptions().MostBirthWaitSeconds;
                bool founding = clade.ParentClade >= 0 && !double.IsNaN(second) && Math.Abs(second - clade.FoundedAt) <= wait;
                scene.BirthFrom = founding ? -1 : clade.Founder;
                notes.Add(string.Format(CultureInfo.InvariantCulture, founding
                        ? "{0}: the birth of {1} itself, founded at {2:0.#} s: the rehearsal waits for a child of its parent clade"
                        : "{0}: a birth inside {1} (founded at {2:0.#} s, {3}): the rehearsal waits for a child of one of its members",
                    who, clade.Name, clade.FoundedAt,
                    clade.ParentClade < 0 ? "with no parent clade" : "too far from the story's second for its founding"));
            }

            // The captions: the story's, and for a card its title as well.
            scene.StoryCaptions = shot.Captions ?? new List<SafariCaption>();
            if (station == SafariStation.Card)
            {
                string title = scene.StoryCaptions.Select(k => k.Text).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t))
                               ?? shot.Title ?? description;
                if (string.IsNullOrWhiteSpace(title))
                {
                    title = "";
                    notes.Add(who + ": WARNING: a title card with no caption, title or description: it is held with no words");
                }
                scene.ChapterTitle = title;
                if (scene.StoryCaptions.Count == 0 && title.Length > 0)
                {
                    scene.StoryCaptions.Add(new SafariCaption
                    {
                        Offset = SafariCaptions.FirstOffset,
                        Text = Printable(title, shot.Number),
                        Seconds = Math.Max(SafariCaptions.OnScreenSeconds, scene.Seconds - 2d * SafariCaptions.FirstOffset),
                    });
                }
            }
            else if (shot.Captions == null)
            {
                notes.Add(who + ": no captions: the scene plays with none");
            }

            // A chapter: the chapter card's 8 s before the scene, its title held on it, and the
            // scene's own captions after it. Chapters are counted across the whole story.
            if (!string.IsNullOrWhiteSpace(shot.Chapter))
            {
                if (station == SafariStation.Card)
                {
                    notes.Add(who + ": a chapter on a card, which is a title already: the chapter's title is not added");
                }
                else
                {
                    double card = SafariTripBuilder.ChapterSeconds;
                    scene.ChapterCard = true;
                    scene.Chapter = ChapterNumber(shot);
                    scene.ChapterTitle = shot.Chapter.Trim();
                    foreach (SafariCaption c in scene.StoryCaptions) c.Offset += card;
                    scene.StoryCaptions.Insert(0, new SafariCaption
                    {
                        Offset = SafariCaptions.FirstOffset,
                        Text = Printable("Chapter " + scene.Chapter.ToString(CultureInfo.InvariantCulture) + ": " + scene.ChapterTitle.TrimEnd('.') + ".", shot.Number),
                        Seconds = card - 2d * SafariCaptions.FirstOffset,
                    });
                }
            }

            if (scene.Seconds > 0d)
            {
                // The scene's length is its own; a chapter card's seconds come before it.
                double end = scene.Seconds + (scene.ChapterCard ? SafariTripBuilder.ChapterSeconds : 0d);
                foreach (SafariCaption c in scene.StoryCaptions.Where(k => k.Offset >= end))
                    notes.Add(string.Format(CultureInfo.InvariantCulture, "{0}: WARNING: the caption at {1:0.#} s comes after the scene's {2:0.#} s and is never shown: \"{3}\"",
                        who, c.Offset, end, c.Text));
            }

            return scene;
        }

        /// <summary>How much a card asked to be dimmed is darkened, 0 to 1.</summary>
        public const float DimmedCard = 0.6f;

        /// <summary>A chapter's number from 1, counted over every scene of the story that opens one, in the story's order.</summary>
        private int ChapterNumber(Shot shot)
        {
            int n = 0;
            foreach (Shot s in _shots.OrderBy(k => k.Number).ThenBy(k => k.Position))
            {
                if (string.IsNullOrWhiteSpace(s.Chapter)) continue;
                n++;
                if (s == shot) return n;
            }
            return n;
        }

        /// <summary>A true or false the writer gave: a boolean, 0 or 1, or the words; null for anything else.</summary>
        private static bool? Switch(JsonNode n)
        {
            if (n == null) return null;
            if (n.Kind == JsonNode.NodeKind.Bool) return n.AsBool();
            if (n.Kind == JsonNode.NodeKind.Number) return n.AsDouble() != 0d;
            if (n.Kind != JsonNode.NodeKind.String) return null;
            switch (n.AsString().Trim().ToLowerInvariant())
            {
                case "true": case "yes": case "on": case "1": return true;
                case "false": case "no": case "off": case "0": return false;
                default: return null;
            }
        }

        /// <summary>The station's own length on screen, s, as the template trip gives it (0: the descent's dolly decides).</summary>
        public static double StationSeconds(SafariStation station)
        {
            switch (station)
            {
                case SafariStation.Arrival: return SafariTripBuilder.ArrivalSeconds;
                case SafariStation.Descent: return 0d;
                case SafariStation.Portrait: return SafariTripBuilder.PortraitSeconds;
                case SafariStation.Floor: return SafariTripBuilder.FloorSeconds;
                case SafariStation.Birth: return SafariTripBuilder.BirthLeadSeconds + SafariTripBuilder.BirthTailSeconds;
                case SafariStation.Colony: return SafariTripBuilder.ColonySeconds;
                case SafariStation.Time: return 2d * SafariTripBuilder.TimeTakeSeconds;
                case SafariStation.Card: return SafariTripBuilder.ChapterSeconds;
                default: return 10d;
            }
        }

        /// <summary>
        /// The clade and the body a subject names: a root id by the guide's founder, else a clade
        /// built from the lineage; a body id inside its clade; a name by the guide's names.
        /// </summary>
        private static void Place(Shot shot, SafariGuide guide, Func<SafariClades> lineage, Dictionary<long, SafariClade> built,
            double second, List<string> notes, string who, out SafariClade clade, out long body)
        {
            clade = null;
            body = shot.Body;
            long root = shot.Root;

            // A bare number: a root when a clade is founded by it, a body otherwise.
            if (root < 0 && body < 0 && shot.Loose >= 0)
            {
                SafariClades map = lineage();
                long k = -1;
                bool recorded = map != null && map.TryRecorded(shot.Loose, out _, out _, out _, out k);
                if (guide?.Find(shot.Loose) != null) root = shot.Loose;
                else if (recorded && k == shot.Loose) root = shot.Loose;
                else
                {
                    body = shot.Loose;
                    notes.Add(who + ": the number " + shot.Loose + " founds no clade, so it is read as a body");
                }
            }

            if (root >= 0)
            {
                clade = guide?.Find(root);
                if (clade == null)
                {
                    SafariClades map = lineage();
                    if (map != null && map.TryRecorded(root, out _, out _, out _, out long k))
                    {
                        if (k == root) clade = Built(root, shot.Name, second, map, built, notes, who);
                        else
                        {
                            // A root that is a body of another clade by the lineage's walk (the
                            // guide's walk too): the scene follows it inside that clade.
                            clade = guide?.Find(k) ?? Built(k, null, second, map, built, notes, who);
                            if (body < 0) body = root;
                            notes.Add(who + ": root " + root + " founds no clade in the lineage; it is a body of " +
                                      (clade?.Name ?? "the clade founded by body " + k) + ", which the scene follows");
                        }
                    }
                    else
                    {
                        notes.Add(who + ": root " + root + " is in neither the guide nor the lineage");
                    }
                }
            }

            if (clade == null && body >= 0)
            {
                SafariClades map = lineage();
                if (map != null && map.TryRecorded(body, out _, out _, out _, out long k))
                    clade = guide?.Find(k) ?? Built(k, null, second, map, built, notes, who);
                else
                    notes.Add(who + ": body " + body + " is not in the lineage: the scene looks for it by id alone");
            }

            // The guide's own number for the clade, when no root placed it.
            if (clade == null && shot.GuideIndex >= 0 && guide != null)
            {
                clade = guide.FindByIndex(shot.GuideIndex);
                if (clade != null)
                    notes.Add(who + ": placed by the guide's clade " + shot.GuideIndex + " (" + clade.Name + ", founder " + clade.Founder + "), with no founder named");
                else
                    notes.Add(who + ": the guide has no clade numbered " + shot.GuideIndex);
            }
            else if (clade != null && shot.GuideIndex >= 0 && guide?.FindByIndex(shot.GuideIndex) is SafariClade numbered && numbered != clade)
            {
                notes.Add(who + ": WARNING: founder " + clade.Founder + " is " + clade.Name + ", and the guide's clade " + shot.GuideIndex +
                          " is " + numbered.Name + ": the founder is used");
            }

            if (clade == null && shot.Name != null && guide != null)
            {
                clade = guide.FindByName(shot.Name);
                if (clade != null && (root >= 0 || body >= 0))
                    notes.Add(who + ": '" + shot.Name + "' found in the guide by its name alone (founder " + clade.Founder + ")");
                else if (clade == null && root < 0 && body < 0)
                    notes.Add(who + ": no clade named '" + shot.Name + "' in the guide, and no id to look for");
            }

            if (clade != null && shot.Name != null && !string.Equals(Plain(shot.Name), Plain(clade.Name), StringComparison.OrdinalIgnoreCase))
                notes.Add(who + ": the story calls it '" + shot.Name + "' and the guide '" + clade.Name + "' (founder " + clade.Founder + "): the guide's name is used");
        }

        private static string Plain(string name) => Regex.Replace(name ?? "", @"\s+", " ").Trim();

        /// <summary>
        /// A clade the guide has no card for, from the lineage: its founder's birth second, flags
        /// and parent clade, named by the story (or by its founder), best at the story's second.
        /// </summary>
        private static SafariClade Built(long founder, string name, double second, SafariClades map,
            Dictionary<long, SafariClade> built, List<string> notes, string who)
        {
            if (built.TryGetValue(founder, out SafariClade c)) return c;
            if (map == null || !map.TryRecorded(founder, out long parent, out double at, out byte flags, out _)) return null;

            long parentClade = -1;
            if (parent >= 0 && map.TryRecorded(parent, out _, out _, out _, out long pc)) parentClade = pc;

            c = new SafariClade
            {
                Founder = founder,
                Name = string.IsNullOrWhiteSpace(name) ? "the clade of body " + founder.ToString(CultureInfo.InvariantCulture) : name,
                FoundedAt = at,
                ParentClade = parentClade,
                ParentBody = parent,
                Absorptive = (flags & 1) != 0,
                Jointed = (flags & 2) != 0,
                Photosynthetic = (flags & 4) != 0,
                BestSecond = double.IsNaN(second) ? at : second,
                PeakAt = double.IsNaN(second) ? at : second,
            };
            built[founder] = c;
            notes.Add(string.Format(CultureInfo.InvariantCulture,
                "{0}: the clade founded by body {1} has no card in the guide: built from the lineage (founded at {2:0.#} s, {3}, parent clade {4})",
                who, founder, at, c.Guild, parentClade >= 0 ? parentClade.ToString(CultureInfo.InvariantCulture) : "none"));
            return c;
        }

        // ---------------------------------------------------------------- values

        private static readonly Regex NumberInText = new Regex(@"-?(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d+)?", RegexOptions.CultureInvariant);

        private static List<double> Numbers(string text)
        {
            var list = new List<double>();
            if (string.IsNullOrEmpty(text)) return list;
            foreach (Match m in NumberInText.Matches(text))
            {
                if (double.TryParse(m.Value.Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) list.Add(v);
            }
            return list;
        }

        /// <summary>A scene's number: a whole number, or a string of digits ("7", "#7"); false for anything else.</summary>
        private static bool TryWhole(JsonNode n, out int value)
        {
            value = -1;
            if (n == null) return false;
            double v;
            if (n.Kind == JsonNode.NodeKind.Number) v = n.AsDouble();
            else if (n.Kind == JsonNode.NodeKind.String && Regex.IsMatch(n.AsString(), @"^\s*#?\d{1,5}\s*$"))
                v = double.Parse(n.AsString().Trim().TrimStart('#'), NumberStyles.Float, CultureInfo.InvariantCulture);
            else return false;
            if (v < 0d || v >= 100000d) return false;
            value = (int)Math.Round(v);
            return true;
        }

        /// <summary>A number, or the first number in a string ("5,000 s"); false for anything else.</summary>
        private static bool TryNumber(JsonNode n, out double value)
        {
            value = double.NaN;
            if (n == null) return false;
            if (n.Kind == JsonNode.NodeKind.Number) { value = n.AsDouble(); return true; }
            if (n.Kind != JsonNode.NodeKind.String) return false;
            List<double> all = Numbers(n.AsString());
            if (all.Count == 0) return false;
            value = all[0];
            return true;
        }

        /// <summary>Two seconds: [a, b], {"from", "to"}, or a string with two numbers ("5,000 to 7,500").</summary>
        private static bool TrySpan(JsonNode n, out double from, out double to)
        {
            from = to = double.NaN;
            if (n == null) return false;

            if (n.Kind == JsonNode.NodeKind.Array && n.Count == 2 &&
                TryNumber(n[0], out from) && TryNumber(n[1], out to)) return true;

            if (n.Kind == JsonNode.NodeKind.Object &&
                TryNumber(FirstOf(n, FromKeys), out from) && TryNumber(FirstOf(n, ToKeys), out to)) return true;

            if (n.Kind == JsonNode.NodeKind.String)
            {
                string text = n.AsString();
                if (!Regex.IsMatch(text, @"\bto\b|\.\.|\u2013|\u2014|\d\s*-\s*\d", RegexOptions.IgnoreCase)) return false;
                // A hyphen between two numbers is a range here, not a sign.
                List<double> all = Numbers(Regex.Replace(text, @"(\d)\s*-\s*(\d)", "$1 to $2"));
                if (all.Count == 2) { from = all[0]; to = all[1]; return true; }
            }

            from = to = double.NaN;
            return false;
        }

        private static string Text(JsonNode n)
        {
            if (n == null) return null;
            if (n.Kind == JsonNode.NodeKind.String) return string.IsNullOrWhiteSpace(n.AsString()) ? null : n.AsString().Trim();
            if (n.Kind == JsonNode.NodeKind.Number) return n.AsDouble().ToString("R", CultureInfo.InvariantCulture);
            return null;
        }

        private static JsonNode FirstOf(JsonNode host, params string[] keys)
        {
            if (host == null || host.Kind != JsonNode.NodeKind.Object) return null;
            foreach (string k in keys)
                if (host.Has(k) && host[k].Kind != JsonNode.NodeKind.Null) return host[k];
            return null;
        }
    }
}
