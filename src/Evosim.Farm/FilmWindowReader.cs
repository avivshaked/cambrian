using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Evosim.Core;

namespace Evosim.Farm
{
    /// <summary>What a film window's last line says about it (<see cref="FilmWindow"/>).</summary>
    public sealed class FilmWindowVerdict
    {
        /// <summary><c>faithful</c>, <c>cousin</c> or <c>unverified</c>, as written.</summary>
        public string Verdict;

        public string Reason;

        /// <summary>The run directory's name, as every verdict carries it.</summary>
        public string RunName;

        /// <summary>
        /// The run directory's full path, or null for a window written before the field existed.
        /// </summary>
        public string RunDirectory;

        public string Arm;
        public double From;
        public double To;
        public double Fps;
        public double PhysicsDt;

        /// <summary>The checkpoint's file name, or null when the window was founded.</summary>
        public string RestoredFrom;

        public double RestoredAt;
        public int Frames;
        public int Bodies;
        public int Births;
        public int Deaths;
        public int Bites;
        public int BirthsWithoutAGenome;
        public int Rows;
        public int RowsAgreed;
        public int RowsBefore;
        public int RowsBeforeAgreed;

        /// <summary>The second a row parted at, or NaN when none did.</summary>
        public double PartedAt = double.NaN;

        public string PartedField;

        /// <summary>The config's hash line when it differs from the run's, or null.</summary>
        public string ConfigDiffers;

        /// <summary>Every source line the verdict names: a differing hash, or a lossy checkpoint.</summary>
        public readonly List<string> SourcesDiffer = new List<string>();

        public bool EndedExtinct;

        /// <summary>Corpses written (D127), and the genome rows taken from the run for those standing at the start.</summary>
        public int Corpses;

        public int CorpseSteps;
        public int CorpseGenomes;
        public int CorpsesWithoutAGenome;

        /// <summary>
        /// D220's remains: bodies with a first row in <c>remains.jsonl.gz</c>, the later shed rows, the genome rows
        /// written there, and the run's own <c>r</c> and <c>rc</c> rows inside the window. 0 for a window that saw
        /// no remains and for one written before they were recorded.
        /// </summary>
        public int Remains;

        public int RemainsSheds;
        public int RemainsGenomes;
        public int RemainsFounded;
        public int RemainsClosed;
    }

    /// <summary>One birth, death, bite or remains row inside a window, from <c>events.jsonl</c>.</summary>
    public struct FilmWindowEvent
    {
        /// <summary>
        /// The lineage row's own <c>e</c>, whole: <c>b</c>, <c>d</c>, <c>k</c>, <c>r</c> (remains cut off a body) or
        /// <c>rc</c> (remains closed). The key to read an event by.
        /// </summary>
        public string Type;

        /// <summary>
        /// <see cref="Type"/>'s first letter, kept for readers written before the remains rows: <c>b</c>, <c>d</c>,
        /// <c>k</c> and <c>r</c> as ever, and <c>c</c> for <c>rc</c>, so the two remains rows are not one kind.
        /// </summary>
        public char Kind;

        /// <summary>The body remains were cut from (<c>r</c> rows), or -1.</summary>
        public long From;

        public double Seconds;
        public long Id;

        /// <summary>A birth's parent, or -1 (a founder, or not a birth).</summary>
        public long Parent;

        /// <summary>A bite's attacker, or -1.</summary>
        public long By;

        /// <summary>A death's cause code (<c>starved</c>, <c>eaten</c>, <c>diverged</c>), or null.</summary>
        public string Cause;

        /// <summary>The row as the run wrote it.</summary>
        public string Row;
    }

    /// <summary>A body's plan from a window's <c>plans.jsonl</c>: its module counts and lost parts.</summary>
    public sealed class FilmWindowPlan
    {
        /// <summary>The second the plan was written at: the window's start, or the metabolic step it moved on.</summary>
        public double Seconds;

        public int[] ModuleCounts;
        public List<int[]> LostPaths;
    }

    /// <summary>
    /// A corpse in a film window (D127): what does not change while it stands, and where it was at
    /// each metabolic step (<c>corpses.jsonl.gz</c>, <see cref="FilmWindow"/>'s remarks).
    /// </summary>
    public sealed class FilmWindowCorpse
    {
        public long Id;

        /// <summary>The second it was founded inside the window, or NaN for one standing at the start.</summary>
        public double Born = double.NaN;

        public double FirstJoules;

        /// <summary>The linear scale its body died at, against its adult (<c>Phenotype.ScaledBy</c>).</summary>
        public float ScaledBy = 1f;

        /// <summary>Whether the rotation below is the one its body died in. Without one it stands in the developer's frame.</summary>
        public bool HasPose;

        public float Qx;
        public float Qy;
        public float Qz;
        public float Qw = 1f;

        /// <summary>The plan it died on, or null for the genome's own minimum.</summary>
        public int[] ModuleCounts;

        public List<int[]> LostPaths;

        /// <summary>The first step second at which the window no longer holds it, or infinity.</summary>
        public double Gone = double.PositiveInfinity;

        internal readonly List<double> Seconds = new List<double>();

        /// <summary>Five per sample: x, y, z, the share of its first joules, and 1 when settled.</summary>
        internal readonly List<float> Values = new List<float>();

        public int SampleCount => Seconds.Count;

        /// <summary>The second of one sample: its founding or first sight, then each step.</summary>
        public double SampleSeconds(int k) => Seconds[k];

        internal void Add(double t, float x, float y, float z, float fraction, bool settled)
        {
            // A corpse seen with its pose at the window's opening is written twice at that second,
            // once in its facts row and once in the step row.
            if (Seconds.Count > 0 && t <= Seconds[Seconds.Count - 1] + 1e-9) return;

            Seconds.Add(t);
            Values.Add(x);
            Values.Add(y);
            Values.Add(z);
            Values.Add(fraction);
            Values.Add(settled ? 1f : 0f);
        }
    }

    /// <summary>A corpse at one second: its place, and the share of its first joules it still holds.</summary>
    public struct FilmWindowCorpsePlace
    {
        public FilmWindowCorpse Corpse;
        public float X;
        public float Y;
        public float Z;
        public float Fraction;
        public bool Settled;
    }

    /// <summary>The parts a remains body held from a second on: indices into the body its plan develops to.</summary>
    public sealed class FilmWindowRemainsMap
    {
        /// <summary>The second the row was written at.</summary>
        public double Seconds;

        /// <summary>Ascending part indices, or null for the whole body.</summary>
        public int[] Map;
    }

    /// <summary>
    /// A remains body in a film window (D220): a dead body or a cut-off limb, from <c>remains.jsonl.gz</c>. What
    /// does not change while it lies there is here; the parts it still holds change with each shed row.
    /// </summary>
    public sealed class FilmWindowRemains
    {
        public long Id;

        /// <summary>The body a limb was cut from, or -1 for a body that is itself the remains.</summary>
        public long From = -1;

        /// <summary>The second its window began; remains of remains keep the original's.</summary>
        public double Since = double.NaN;

        /// <summary>Whether it was standing when the window opened.</summary>
        public bool Start;

        /// <summary>The linear scale its body stood at (<c>Phenotype.ScaledBy</c>); 1 is the adult.</summary>
        public float ScaledBy = 1f;

        /// <summary>The plan it was developed on, or null for the genome's own minimum.</summary>
        public int[] ModuleCounts;

        public List<int[]> LostPaths;

        /// <summary>The first row's second, then each shed row's, in time order.</summary>
        public readonly List<FilmWindowRemainsMap> Maps = new List<FilmWindowRemainsMap>();
    }

    /// <summary>
    /// A film window read back: the verdict, the frames, the genomes, the plans and the events,
    /// for a player that draws the window without stepping anything
    /// (<c>logbook/specs/record-and-film-spec.md</c>, B2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Plain .NET, so the theatre and a test read it alike.</b> The theatre reaches it through
    /// the farm package, as it reaches <see cref="PoseStreamReader"/>, and <c>FilmWindowTests</c>
    /// reads what a window wrote with it. Nothing here knows about Unity.
    /// </para>
    /// <para>
    /// <b>The provenance word is the verdict's.</b> <see cref="ProvenanceWord"/> says FAITHFUL only
    /// when the verdict line says <c>faithful</c>, COUSIN when it says <c>cousin</c>, and UNVERIFIED
    /// for an unverified window and for one with no verdict line at all, which is a window killed
    /// before its end. A player never decides faithfulness itself: the farm compared the rows and
    /// the player has nothing to compare.
    /// </para>
    /// <para>
    /// <b>A plan is at or before the frame.</b> The window writes a body's plan at the start where
    /// it differs from the genome's minimum and at every metabolic step it moves, and a frame at a
    /// physics step after that metabolic step draws the body the step built. So the plan a frame
    /// wants is the newest one written at or before the frame's second, and a body with no plan
    /// row at or before it develops at its genome's minimum (<see cref="PlanAt"/> returns null).
    /// </para>
    /// </remarks>
    public sealed class FilmWindowReader : IDisposable
    {
        private const double Eps = 1e-6;

        private readonly Dictionary<long, string> _genomes = new Dictionary<long, string>();
        private readonly Dictionary<long, List<FilmWindowPlan>> _plans = new Dictionary<long, List<FilmWindowPlan>>();
        private readonly List<FilmWindowEvent> _events = new List<FilmWindowEvent>();
        private readonly Dictionary<long, int> _birthOf = new Dictionary<long, int>();
        private readonly Dictionary<long, int> _deathOf = new Dictionary<long, int>();
        private readonly List<string> _notes = new List<string>();
        private readonly Dictionary<long, FilmWindowCorpse> _corpses = new Dictionary<long, FilmWindowCorpse>();
        private readonly List<FilmWindowCorpse> _corpseList = new List<FilmWindowCorpse>();
        private readonly List<double> _corpseSteps = new List<double>();
        private readonly Dictionary<long, string> _remainsGenomes = new Dictionary<long, string>();
        private readonly Dictionary<long, FilmWindowRemains> _remains = new Dictionary<long, FilmWindowRemains>();
        private readonly List<FilmWindowRemains> _remainsList = new List<FilmWindowRemains>();

        /// <summary>The window's directory, full path.</summary>
        public string Directory { get; }

        /// <summary>The verdict, or null when <c>identity.jsonl</c> ends without one.</summary>
        public FilmWindowVerdict Verdict { get; private set; }

        /// <summary>The frames, open for the life of the reader.</summary>
        public PoseStreamReader Stream { get; private set; }

        /// <summary>Things a reader of the window is owed: a torn member, an unreadable row.</summary>
        public IReadOnlyList<string> Notes => _notes;

        /// <summary>Every event, in the window's order.</summary>
        public IReadOnlyList<FilmWindowEvent> Events => _events;

        /// <summary>Every id with a genome row.</summary>
        public IEnumerable<long> GenomeIds => _genomes.Keys;

        public int GenomeCount => _genomes.Count;

        public int PlanRows { get; private set; }

        public int FrameCount => Stream.Frames.Length;

        private FilmWindowReader(string directory)
        {
            Directory = directory;
        }

        /// <summary>Whether a directory holds a film window: its pose stream is the one file every window has.</summary>
        public static bool IsWindow(string directory) =>
            !string.IsNullOrEmpty(directory) &&
            File.Exists(Path.Combine(directory, FilmWindow.PosesFileName));

        /// <summary>
        /// Reads a window. Throws when the directory has no pose stream, which is the one thing a
        /// player cannot do without; every other file missing is a note and an empty answer.
        /// </summary>
        public static FilmWindowReader Open(string directory)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("No window directory.");

            string full = Path.GetFullPath(directory);
            string poses = Path.Combine(full, FilmWindow.PosesFileName);

            if (!File.Exists(poses))
            {
                throw new FileNotFoundException(
                    full + " holds no " + FilmWindow.PosesFileName + ", so it is not a film window.", poses);
            }

            var reader = new FilmWindowReader(full);

            try
            {
                reader.Stream = PoseStreamReader.Open(poses);
                reader.ReadVerdict();
                reader.ReadGenomes();
                reader.ReadPlans();
                reader.ReadEvents();
                reader.ReadCorpses();
                reader.ReadRemains();
            }
            catch
            {
                reader.Dispose();
                throw;
            }

            return reader;
        }

        // ---------------------------------------------------------------- the verdict

        /// <summary>
        /// FAITHFUL, COUSIN or UNVERIFIED: the verdict's own word, and UNVERIFIED when there is no
        /// verdict or it says something this build does not know.
        /// </summary>
        public string ProvenanceWord => WordOf(Verdict?.Verdict);

        /// <summary>The word for a verdict string, by the rule in the class remarks.</summary>
        public static string WordOf(string verdict) =>
            verdict == FilmWindow.Faithful ? "FAITHFUL"
            : verdict == FilmWindow.Cousin ? "COUSIN"
            : "UNVERIFIED";

        /// <summary>Whether the verdict says faithful. The only test a caller may use to claim it.</summary>
        public bool Faithful => Verdict != null && Verdict.Verdict == FilmWindow.Faithful;

        private void ReadVerdict() => Verdict = ReadVerdictIn(Directory, _notes);

        /// <summary>
        /// A window's verdict read alone, without its frames: what a planner asks of a window it
        /// may not have to film again (<see cref="StoryWindows.Recorded"/>). Null when the
        /// directory holds no <c>identity.jsonl</c> or the file ends without a verdict, each said
        /// in <paramref name="notes"/> when a list is given.
        /// </summary>
        public static FilmWindowVerdict ReadVerdictIn(string directory, List<string> notes = null)
        {
            string path = Path.Combine(directory ?? "", FilmWindow.IdentityFileName);

            if (!File.Exists(path))
            {
                notes?.Add("no " + FilmWindow.IdentityFileName + ", so the window has no verdict");
                return null;
            }

            string last = null;

            foreach (string line in SharedLines(path))
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0 || !trimmed.EndsWith("}", StringComparison.Ordinal)) continue;
                if (trimmed.IndexOf("\"verdict\"", StringComparison.Ordinal) >= 0) last = trimmed;
            }

            if (last == null)
            {
                notes?.Add(FilmWindow.IdentityFileName + " ends without a verdict: the window stopped before its end");
                return null;
            }

            JsonNode v = Json.Parse(last);
            var verdict = new FilmWindowVerdict
            {
                Verdict = Str(v, "verdict"),
                Reason = Str(v, "reason"),
                RunName = Str(v, "run"),
                RunDirectory = Str(v, "runDirectory"),
                Arm = Str(v, "arm"),
                From = Num(v, "from"),
                To = Num(v, "to"),
                Fps = Num(v, "fps"),
                PhysicsDt = Num(v, "physicsDt"),
                RestoredFrom = Str(v, "restoredFrom"),
                RestoredAt = Num(v, "restoredAt"),
                Frames = Int(v, "frames"),
                Bodies = Int(v, "bodies"),
                Births = Int(v, "births"),
                Deaths = Int(v, "deaths"),
                Bites = Int(v, "bites"),
                BirthsWithoutAGenome = Int(v, "birthsWithoutAGenome"),
                Rows = Int(v, "rows"),
                RowsAgreed = Int(v, "rowsAgreed"),
                RowsBefore = Int(v, "rowsBefore"),
                RowsBeforeAgreed = Int(v, "rowsBeforeAgreed"),
                PartedAt = Num(v, "partedAt"),
                PartedField = Str(v, "partedField"),
                ConfigDiffers = Str(v, "configDiffers"),
                EndedExtinct = v.Has("endedExtinct") && v["endedExtinct"].Kind == JsonNode.NodeKind.Bool && v["endedExtinct"].AsBool(),
                Corpses = Int(v, "corpses"),
                CorpseSteps = Int(v, "corpseSteps"),
                CorpseGenomes = Int(v, "corpseGenomes"),
                CorpsesWithoutAGenome = Int(v, "corpsesWithoutAGenome"),
                Remains = Int(v, "remains"),
                RemainsSheds = Int(v, "remainsSheds"),
                RemainsGenomes = Int(v, "remainsGenomes"),
                RemainsFounded = Int(v, "remainsFounded"),
                RemainsClosed = Int(v, "remainsClosed"),
            };

            if (v.Has("sourcesDiffer") && v["sourcesDiffer"].Kind == JsonNode.NodeKind.Array)
            {
                foreach (JsonNode line in v["sourcesDiffer"].Items())
                {
                    if (line.Kind == JsonNode.NodeKind.String) verdict.SourcesDiffer.Add(line.AsString());
                }
            }

            return verdict;
        }

        private static string Str(JsonNode v, string name) =>
            v.Has(name) && v[name].Kind == JsonNode.NodeKind.String ? v[name].AsString() : null;

        private static double Num(JsonNode v, string name) =>
            v.Has(name) && v[name].Kind == JsonNode.NodeKind.Number ? v[name].AsDouble() : double.NaN;

        private static int Int(JsonNode v, string name) =>
            v.Has(name) && v[name].Kind == JsonNode.NodeKind.Number ? v[name].AsInt() : 0;

        // ---------------------------------------------------------------- the frames

        /// <summary>The second a frame was recorded at.</summary>
        public double SecondOf(int frame) => Stream.Frames[frame].Seconds;

        /// <summary>One frame, read from the stream.</summary>
        public PoseFrame ReadFrame(int frame) => Stream.Read(frame);

        /// <summary>
        /// The last frame recorded at or before a second, or -1 when the second is before the first
        /// frame. A frame sits up to one physics step after its nominal second, so a second asked
        /// for by its nominal value finds the frame before it; <see cref="NearestFrame"/> is the
        /// one a caller naming a nominal second wants.
        /// </summary>
        public int FrameAtOrBefore(double seconds)
        {
            PoseFrameRef[] frames = Stream.Frames;
            int lo = 0, hi = frames.Length - 1, found = -1;

            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;

                if (frames[mid].Seconds <= seconds + Eps)
                {
                    found = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            return found;
        }

        /// <summary>The frame nearest a second, clamped to the window, or -1 for an empty stream.</summary>
        public int NearestFrame(double seconds)
        {
            int count = FrameCount;
            if (count == 0) return -1;

            int before = FrameAtOrBefore(seconds);
            if (before < 0) return 0;
            if (before >= count - 1) return count - 1;

            return seconds - SecondOf(before) <= SecondOf(before + 1) - seconds ? before : before + 1;
        }

        // ---------------------------------------------------------------- the paths

        /// <summary>One body's root at every frame that holds it, and the last flags it carried.</summary>
        private sealed class Track
        {
            public readonly List<int> Frames = new List<int>();
            public readonly List<float> Xyz = new List<float>();
            public int Flags = PoseStream.FlagsNotRecorded;
        }

        private Dictionary<long, Track> _tracks;

        /// <summary>
        /// Whether any frame holds a body, and the first and last frame that do. Reads every
        /// frame once, the first time a path is asked for (<see cref="TryRootAt"/>).
        /// </summary>
        public bool TryFramesOf(long id, out int first, out int last)
        {
            EnsureTracks();

            if (_tracks.TryGetValue(id, out Track track) && track.Frames.Count > 0)
            {
                first = track.Frames[0];
                last = track.Frames[track.Frames.Count - 1];
                return true;
            }

            first = last = -1;
            return false;
        }

        /// <summary>
        /// Where a body's root stood at a second, as the frames have it: linear between the two
        /// frames around the second, and held at the body's first frame before it and at its last
        /// after it, so a body that dies inside the window stays where it died. False when no
        /// frame holds the body.
        /// </summary>
        /// <remarks>
        /// This is how a camera plan looks ahead in a window: the path the farm's solver took,
        /// read from the frames, where the live mode carries the velocity at the plan's second
        /// forward in a straight line. The first call reads every frame of the window once and
        /// keeps every body's root, 16 bytes a body a frame.
        /// </remarks>
        public bool TryRootAt(long id, double seconds, out float x, out float y, out float z)
        {
            EnsureTracks();
            x = y = z = float.NaN;

            if (!_tracks.TryGetValue(id, out Track track) || track.Frames.Count == 0) return false;

            List<int> frames = track.Frames;
            int n = frames.Count;

            if (seconds <= SecondOf(frames[0]) + Eps) { RootAt(track, 0, out x, out y, out z); return true; }
            if (seconds >= SecondOf(frames[n - 1]) - Eps) { RootAt(track, n - 1, out x, out y, out z); return true; }

            // The last of the body's frames at or before the second, and the one after it.
            int lo = 0, hi = n - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (SecondOf(frames[mid]) <= seconds) lo = mid;
                else hi = mid;
            }

            double t0 = SecondOf(frames[lo]), t1 = SecondOf(frames[hi]);
            float u = t1 - t0 > 1e-9 ? (float)((seconds - t0) / (t1 - t0)) : 0f;
            int a = 3 * lo, b = 3 * hi;
            List<float> p = track.Xyz;
            x = p[a] + (p[b] - p[a]) * u;
            y = p[a + 1] + (p[b + 1] - p[a + 1]) * u;
            z = p[a + 2] + (p[b + 2] - p[a + 2]) * u;
            return true;
        }

        private static void RootAt(Track track, int k, out float x, out float y, out float z)
        {
            x = track.Xyz[3 * k];
            y = track.Xyz[3 * k + 1];
            z = track.Xyz[3 * k + 2];
        }

        /// <summary>
        /// The guild flags a body carried at the last frame that held it
        /// (<see cref="PoseStream.AbsorptiveBit"/>, <see cref="PoseStream.JointedBit"/>,
        /// <see cref="PoseStream.PhotosyntheticBit"/>), or false when no frame holds it or the
        /// stream did not record them.
        /// </summary>
        public bool TryFlagsOf(long id, out int flags)
        {
            EnsureTracks();
            flags = PoseStream.FlagsNotRecorded;
            if (!_tracks.TryGetValue(id, out Track track) || track.Flags < 0) return false;
            flags = track.Flags;
            return true;
        }

        private void EnsureTracks()
        {
            if (_tracks != null) return;
            _tracks = new Dictionary<long, Track>();

            for (int f = 0; f < FrameCount; f++)
            {
                PoseFrame frame = ReadFrame(f);

                for (int i = 0; i < frame.Bodies.Length; i++)
                {
                    PoseBody body = frame.Bodies[i];

                    if (!_tracks.TryGetValue(body.Id, out Track track))
                    {
                        track = new Track();
                        _tracks[body.Id] = track;
                    }

                    track.Frames.Add(f);
                    track.Xyz.Add(body.X);
                    track.Xyz.Add(body.Y);
                    track.Xyz.Add(body.Z);
                    if (body.Flags >= 0) track.Flags = body.Flags;
                }
            }
        }

        // ---------------------------------------------------------------- the genomes

        /// <summary>A body's genome row, the run's own form, or false when the window has none for it.</summary>
        public bool TryGenome(long id, out string row) => _genomes.TryGetValue(id, out row);

        private void ReadGenomes()
        {
            string path = Path.Combine(Directory, FilmWindow.GenomesFileName);

            if (!File.Exists(path))
            {
                _notes.Add("no " + FilmWindow.GenomesFileName + ", so no body can be developed");
                return;
            }

            IEnumerable<string> lines = GzipMemberReader.ReadLines(path, out string torn);
            int unreadable = 0;

            foreach (string line in lines)
            {
                if (line.Length == 0) continue;

                long id;

                try
                {
                    id = GenomeJson.ReadId(line);
                }
                catch (Exception)
                {
                    unreadable++;
                    continue;
                }

                if (id < 0)
                {
                    unreadable++;
                    continue;
                }

                if (!_genomes.ContainsKey(id)) _genomes[id] = line;
            }

            if (torn != null) _notes.Add(FilmWindow.GenomesFileName + ": " + torn);
            if (unreadable > 0) _notes.Add(unreadable + " genome row(s) carry no readable id and are not drawn");
        }

        // ---------------------------------------------------------------- the plans

        /// <summary>
        /// The plan a body stood on at a second: the newest row written at or before it, or null
        /// when none was, which is the genome's own minimum.
        /// </summary>
        public FilmWindowPlan PlanAt(long id, double seconds)
        {
            if (!_plans.TryGetValue(id, out List<FilmWindowPlan> plans)) return null;

            FilmWindowPlan found = null;

            for (int i = 0; i < plans.Count; i++)
            {
                if (plans[i].Seconds > seconds + Eps) break;
                found = plans[i];
            }

            return found;
        }

        /// <summary>Every plan row for a body, in the order written, or an empty list.</summary>
        public IReadOnlyList<FilmWindowPlan> PlansOf(long id) =>
            _plans.TryGetValue(id, out List<FilmWindowPlan> plans) ? plans : (IReadOnlyList<FilmWindowPlan>)Array.Empty<FilmWindowPlan>();

        private void ReadPlans()
        {
            string path = Path.Combine(Directory, FilmWindow.PlansFileName);
            if (!File.Exists(path)) return;

            foreach (string line in SharedLines(path))
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0 || !trimmed.EndsWith("}", StringComparison.Ordinal)) continue;

                JsonNode row = Json.Parse(trimmed);
                long id = (long)row["id"].AsDouble();

                var counts = new List<int>();
                foreach (JsonNode count in row["moduleCounts"].Items()) counts.Add(count.AsInt());

                var lost = new List<int[]>();
                foreach (JsonNode lostPath in row["lostPaths"].Items())
                {
                    var steps = new List<int>();
                    foreach (JsonNode step in lostPath.Items()) steps.Add(step.AsInt());
                    lost.Add(steps.ToArray());
                }

                var plan = new FilmWindowPlan
                {
                    Seconds = row["t"].AsDouble(),
                    ModuleCounts = counts.ToArray(),
                    LostPaths = lost,
                };

                if (!_plans.TryGetValue(id, out List<FilmWindowPlan> plans))
                {
                    plans = new List<FilmWindowPlan>();
                    _plans[id] = plans;
                }

                plans.Add(plan);
                PlanRows++;
            }

            // Written in time order already; sorted anyway, stably, so a reader never depends on it.
            foreach (List<FilmWindowPlan> plans in _plans.Values)
            {
                StableSortBySecond(plans);
            }
        }

        private static void StableSortBySecond(List<FilmWindowPlan> plans)
        {
            for (int i = 1; i < plans.Count; i++)
            {
                FilmWindowPlan item = plans[i];
                int j = i - 1;

                while (j >= 0 && plans[j].Seconds > item.Seconds)
                {
                    plans[j + 1] = plans[j];
                    j--;
                }

                plans[j + 1] = item;
            }
        }

        // ---------------------------------------------------------------- the events

        /// <summary>A body's birth inside the window, or false when it was alive at the start.</summary>
        public bool TryBirthOf(long id, out FilmWindowEvent birth)
        {
            if (_birthOf.TryGetValue(id, out int index))
            {
                birth = _events[index];
                return true;
            }

            birth = default;
            return false;
        }

        /// <summary>A body's death inside the window, or false when it outlived it.</summary>
        public bool TryDeathOf(long id, out FilmWindowEvent death)
        {
            if (_deathOf.TryGetValue(id, out int index))
            {
                death = _events[index];
                return true;
            }

            death = default;
            return false;
        }

        /// <summary>
        /// How many children a body had inside the window up to a second: its births in the
        /// window at or before it. A child born before the window opened is not in the window's
        /// files, so the count starts at the window's start.
        /// </summary>
        public int ChildrenOf(long parent, double through)
        {
            int n = 0;

            for (int i = 0; i < _events.Count; i++)
            {
                FilmWindowEvent e = _events[i];
                if (e.Kind == 'b' && e.Parent == parent && e.Seconds <= through + Eps) n++;
            }

            return n;
        }

        /// <summary>The events in (from, to], in the window's order.</summary>
        public IEnumerable<FilmWindowEvent> EventsBetween(double from, double to)
        {
            for (int i = 0; i < _events.Count; i++)
            {
                FilmWindowEvent e = _events[i];
                if (e.Seconds > from + Eps && e.Seconds <= to + Eps) yield return e;
            }
        }

        private void ReadEvents()
        {
            string path = Path.Combine(Directory, FilmWindow.EventsFileName);
            if (!File.Exists(path)) return;

            foreach (string line in SharedLines(path))
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0 || !trimmed.EndsWith("}", StringComparison.Ordinal)) continue;

                JsonNode row = Json.Parse(trimmed);
                string kind = Str(row, "e");
                if (string.IsNullOrEmpty(kind)) continue;

                var e = new FilmWindowEvent
                {
                    Type = kind,

                    // 'rc' is not 'r': the whole kind decides, and the letter is kept for older readers.
                    Kind = kind == "rc" ? 'c' : kind[0],
                    From = row.Has("from") && row["from"].Kind == JsonNode.NodeKind.Number ? (long)row["from"].AsDouble() : -1L,
                    Seconds = row["t"].AsDouble(),
                    Id = (long)row["id"].AsDouble(),
                    Parent = row.Has("p") && row["p"].Kind == JsonNode.NodeKind.Number ? (long)row["p"].AsDouble() : -1L,
                    By = row.Has("by") && row["by"].Kind == JsonNode.NodeKind.Number ? (long)row["by"].AsDouble() : -1L,
                    Cause = Str(row, "c"),
                    Row = trimmed,
                };

                if (e.Kind == 'b' && !_birthOf.ContainsKey(e.Id)) _birthOf[e.Id] = _events.Count;
                if (e.Kind == 'd' && !_deathOf.ContainsKey(e.Id)) _deathOf[e.Id] = _events.Count;

                _events.Add(e);
            }
        }

        // ---------------------------------------------------------------- the corpses (D127)

        /// <summary>Corpses the window holds, founded inside it or standing at its start.</summary>
        public int CorpseCount => _corpseList.Count;

        /// <summary>Every corpse the window holds, in the order their facts were written.</summary>
        public IReadOnlyList<FilmWindowCorpse> Corpses => _corpseList;

        /// <summary>A corpse's facts, or false when the window has none for the id.</summary>
        public bool TryCorpse(long id, out FilmWindowCorpse corpse) => _corpses.TryGetValue(id, out corpse);

        /// <summary>
        /// Every corpse standing at a second, with its place and the share of its first joules it
        /// still holds, into a list the caller keeps.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A corpse founded inside the window is drawn from its founding second, its first sample. A
        /// frame holds the world's living bodies, and the body left them at that metabolic step,
        /// so the frame there is the first without it. One standing at the start is drawn from the
        /// first frame, and every corpse is gone from its <see cref="FilmWindowCorpse.Gone"/> second.
        /// </para>
        /// <para>
        /// The world moves a corpse only at a metabolic step, so between two steps it has no place of
        /// its own. The straight line between them is the player's, and it keeps a sinking corpse
        /// from jumping a few centimetres twice a second. After its last sample it holds still.
        /// </para>
        /// </remarks>
        public void CorpsesAt(double seconds, List<FilmWindowCorpsePlace> into)
        {
            if (into == null) return;
            into.Clear();

            for (int h = 0; h < _corpseList.Count; h++)
            {
                FilmWindowCorpse corpse = _corpseList[h];
                List<double> times = corpse.Seconds;
                int n = times.Count;
                if (n == 0) continue;

                if (seconds < times[0] - Eps) continue;

                if (seconds >= corpse.Gone - Eps) continue;

                int k = LastAtOrBefore(times, seconds);
                if (k < 0) k = 0;

                List<float> v = corpse.Values;
                int a = 5 * k;

                var place = new FilmWindowCorpsePlace
                {
                    Corpse = corpse,
                    X = v[a],
                    Y = v[a + 1],
                    Z = v[a + 2],
                    Fraction = v[a + 3],
                    Settled = v[a + 4] > 0.5f,
                };

                if (k + 1 < n && seconds > times[k])
                {
                    int b = a + 5;
                    float s = (float)((seconds - times[k]) / (times[k + 1] - times[k]));

                    place.X += s * (v[b] - v[a]);
                    place.Y += s * (v[b + 1] - v[a + 1]);
                    place.Z += s * (v[b + 2] - v[a + 2]);
                    place.Fraction += s * (v[b + 3] - v[a + 3]);
                }

                into.Add(place);
            }
        }

        /// <summary>The index of the last second at or before a time, or -1 when there is none.</summary>
        private static int LastAtOrBefore(List<double> times, double seconds)
        {
            int lo = 0;
            int hi = times.Count - 1;
            int found = -1;

            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;

                if (times[mid] <= seconds + Eps)
                {
                    found = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            return found;
        }

        private void ReadCorpses()
        {
            string path = Path.Combine(Directory, FilmWindow.CorpsesFileName);

            // A window written before the word changed (2026-09-28) holds the same rows under the
            // old name. Refused rather than read as a window with no corpses, which is what a
            // missing file means; a window is recorded again from its run's checkpoint in minutes.
            if (File.Exists(Path.Combine(Directory, "husks.jsonl.gz")))
            {
                throw new InvalidDataException(
                    "This window was written before its corpses file was renamed from husks.jsonl.gz to " +
                    FilmWindow.CorpsesFileName + " (2026-09-28), so its corpses would read as none. Record " +
                    "it again from the run's checkpoint with --film-window.");
            }

            if (!File.Exists(path)) return;

            int orphans = 0;
            int unreadable = 0;

            using (GzipMemberReader reader = GzipMemberReader.Open(path))
            {
                foreach (string line in reader.Lines())
                {
                    try
                    {
                        if (line.StartsWith(StepHead, StringComparison.Ordinal)) orphans += ReadCorpseStep(line);
                        else if (line.StartsWith(CorpseHead, StringComparison.Ordinal)) ReadCorpseFacts(line);
                        else unreadable++;
                    }
                    catch (FormatException)
                    {
                        unreadable++;
                    }
                }

                if (reader.Torn) _notes.Add(FilmWindow.CorpsesFileName + ": " + reader.TornNote);
            }

            // A corpse is gone at the first step after its last sample: the world dissolved it, or a
            // mouth took the last of it, at that metabolic step.
            foreach (FilmWindowCorpse corpse in _corpseList)
            {
                if (corpse.Seconds.Count == 0) continue;

                double last = corpse.Seconds[corpse.Seconds.Count - 1];
                int k = LastAtOrBefore(_corpseSteps, last);

                if (k + 1 < _corpseSteps.Count) corpse.Gone = _corpseSteps[k + 1];
            }

            if (orphans > 0) _notes.Add(orphans + " corpse place(s) in a step row with no facts row, not drawn");
            if (unreadable > 0) _notes.Add(unreadable + " corpse row(s) this build cannot read, not drawn");
        }

        private const string StepHead = "{\"k\":\"step\"";
        private const string CorpseHead = "{\"k\":\"corpse\"";

        /// <summary>
        /// One step row, read by hand: a few thousand corpses of six numbers each, where a tree of
        /// nodes a number would cost more than the rest of the window. Returns the places whose corpse
        /// has no facts row, which the writer never leaves.
        /// </summary>
        private int ReadCorpseStep(string line)
        {
            int tAt = line.IndexOf("\"t\":", StringComparison.Ordinal);
            int hAt = line.IndexOf("\"h\":[", StringComparison.Ordinal);
            if (tAt < 0 || hAt < 0) throw new FormatException("a step row without its second or its places");

            int tEnd = line.IndexOf(',', tAt);
            double t = double.Parse(line.Substring(tAt + 4, tEnd - tAt - 4), NumberStyles.Float, CultureInfo.InvariantCulture);

            if (_corpseSteps.Count == 0 || t > _corpseSteps[_corpseSteps.Count - 1]) _corpseSteps.Add(t);

            int open = hAt + 5;
            int close = line.LastIndexOf(']');
            if (close <= open) return 0;

            string[] cells = line.Substring(open, close - open).Split(',');
            int orphans = 0;

            for (int i = 0; i + 5 < cells.Length; i += 6)
            {
                long id = long.Parse(cells[i], NumberStyles.Integer, CultureInfo.InvariantCulture);

                if (!_corpses.TryGetValue(id, out FilmWindowCorpse corpse))
                {
                    orphans++;
                    continue;
                }

                corpse.Add(t, Float(cells[i + 1]), Float(cells[i + 2]), Float(cells[i + 3]), Float(cells[i + 4]), cells[i + 5] == "1");
            }

            return orphans;
        }

        private static float Float(string text) =>
            float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);

        private void ReadCorpseFacts(string line)
        {
            JsonNode row = Json.Parse(line);
            long id = (long)row["id"].AsDouble();
            if (_corpses.ContainsKey(id)) return;

            var corpse = new FilmWindowCorpse
            {
                Id = id,
                Born = Num(row, "born"),
                FirstJoules = Num(row, "first"),
                ScaledBy = row.Has("scaledBy") && row["scaledBy"].Kind == JsonNode.NodeKind.Number ? (float)row["scaledBy"].AsDouble() : 1f,
                HasPose = row.Has("pose") && row["pose"].Kind == JsonNode.NodeKind.Bool && row["pose"].AsBool(),
            };

            if (corpse.HasPose && row.Has("rot") && row["rot"].Count == 4)
            {
                JsonNode q = row["rot"];
                corpse.Qx = (float)q[0].AsDouble();
                corpse.Qy = (float)q[1].AsDouble();
                corpse.Qz = (float)q[2].AsDouble();
                corpse.Qw = (float)q[3].AsDouble();
            }

            var counts = new List<int>();
            if (row.Has("moduleCounts")) foreach (JsonNode count in row["moduleCounts"].Items()) counts.Add(count.AsInt());

            var lost = new List<int[]>();
            if (row.Has("lostPaths"))
            {
                foreach (JsonNode lostPath in row["lostPaths"].Items())
                {
                    var steps = new List<int>();
                    foreach (JsonNode step in lostPath.Items()) steps.Add(step.AsInt());
                    lost.Add(steps.ToArray());
                }
            }

            // Empty is the genome's own minimum, as the plans' first row reads it.
            corpse.ModuleCounts = counts.Count > 0 ? counts.ToArray() : null;
            corpse.LostPaths = lost.Count > 0 ? lost : null;

            if (row.Has("at") && row["at"].Count == 5)
            {
                JsonNode at = row["at"];
                corpse.Add(
                    row["t"].AsDouble(),
                    (float)at[0].AsDouble(), (float)at[1].AsDouble(), (float)at[2].AsDouble(),
                    (float)at[3].AsDouble(), at[4].AsDouble() > 0.5d);
            }

            _corpses[id] = corpse;
            _corpseList.Add(corpse);
        }

        // ---------------------------------------------------------------- the remains (D220)

        /// <summary>Remains bodies the window holds, standing at its start, dead inside it or cut off inside it.</summary>
        public int RemainsCount => _remainsList.Count;

        /// <summary>Every remains body, in the order its first row was written.</summary>
        public IReadOnlyList<FilmWindowRemains> Remains => _remainsList;

        /// <summary>A remains body's facts, or false when the window has none for the id.</summary>
        public bool TryRemains(long id, out FilmWindowRemains remains) => _remains.TryGetValue(id, out remains);

        /// <summary>
        /// A remains body's genome row: the window's own genomes file first, then the rows of
        /// <c>remains.jsonl.gz</c> for the bodies that file has none for. Apart from <see cref="TryGenome"/>, which
        /// answers from the genomes file alone as it always did.
        /// </summary>
        public bool TryRemainsGenome(long id, out string row) =>
            _genomes.TryGetValue(id, out row) || _remainsGenomes.TryGetValue(id, out row);

        private const string RemainsHead = "{\"k\":\"remains\"";
        private const string ShedHead = "{\"k\":\"shed\"";

        private void ReadRemains()
        {
            string path = Path.Combine(Directory, FilmWindow.RemainsFileName);

            // A window written before the remains were recorded has no such file, which reads as no remains rows.
            if (!File.Exists(path)) return;

            int orphans = 0;
            int unreadable = 0;
            int unreadableGenomes = 0;

            using (GzipMemberReader reader = GzipMemberReader.Open(path))
            {
                foreach (string line in reader.Lines())
                {
                    if (line.Length == 0) continue;

                    try
                    {
                        if (line.StartsWith(RemainsHead, StringComparison.Ordinal))
                        {
                            ReadRemainsRow(line);
                        }
                        else if (line.StartsWith(ShedHead, StringComparison.Ordinal))
                        {
                            orphans += ReadShedRow(line);
                        }
                        else if (line.StartsWith("{\"k\":", StringComparison.Ordinal))
                        {
                            // A row kind a later build wrote: skipped, as a reader that does not know it must.
                        }
                        else
                        {
                            long id = GenomeJson.ReadId(line);

                            if (id < 0) unreadableGenomes++;
                            else if (!_remainsGenomes.ContainsKey(id)) _remainsGenomes[id] = line;
                        }
                    }
                    catch (Exception e) when (e is FormatException || e is InvalidDataException || e is KeyNotFoundException)
                    {
                        unreadable++;
                    }
                }

                if (reader.Torn) _notes.Add(FilmWindow.RemainsFileName + ": " + reader.TornNote);
            }

            if (orphans > 0) _notes.Add(orphans + " shed row(s) in " + FilmWindow.RemainsFileName + " with no remains row, not read");
            if (unreadable > 0) _notes.Add(unreadable + " remains row(s) this build cannot read, not drawn");
            if (unreadableGenomes > 0) _notes.Add(unreadableGenomes + " remains genome row(s) carry no readable id and are not drawn");
        }

        private static int[] MapOfRow(JsonNode row)
        {
            if (!row.Has("map") || row["map"].Kind != JsonNode.NodeKind.Array) return null;

            var map = new List<int>();
            foreach (JsonNode part in row["map"].Items()) map.Add(part.AsInt());
            return map.ToArray();
        }

        private void ReadRemainsRow(string line)
        {
            JsonNode row = Json.Parse(line);
            long id = (long)row["id"].AsDouble();

            if (_remains.ContainsKey(id)) return;

            var remains = new FilmWindowRemains
            {
                Id = id,
                From = row.Has("from") && row["from"].Kind == JsonNode.NodeKind.Number ? (long)row["from"].AsDouble() : -1L,
                Since = Num(row, "since"),
                Start = row.Has("start") && row["start"].Kind == JsonNode.NodeKind.Bool && row["start"].AsBool(),
                ScaledBy = row.Has("scaledBy") && row["scaledBy"].Kind == JsonNode.NodeKind.Number ? (float)row["scaledBy"].AsDouble() : 1f,
            };

            var counts = new List<int>();
            if (row.Has("moduleCounts")) foreach (JsonNode count in row["moduleCounts"].Items()) counts.Add(count.AsInt());

            var lost = new List<int[]>();
            if (row.Has("lostPaths"))
            {
                foreach (JsonNode lostPath in row["lostPaths"].Items())
                {
                    var steps = new List<int>();
                    foreach (JsonNode step in lostPath.Items()) steps.Add(step.AsInt());
                    lost.Add(steps.ToArray());
                }
            }

            // Empty is the genome's own minimum, as the plans' first row and a corpse's read it.
            remains.ModuleCounts = counts.Count > 0 ? counts.ToArray() : null;
            remains.LostPaths = lost.Count > 0 ? lost : null;

            remains.Maps.Add(new FilmWindowRemainsMap { Seconds = row["t"].AsDouble(), Map = MapOfRow(row) });

            _remains[id] = remains;
            _remainsList.Add(remains);
        }

        private int ReadShedRow(string line)
        {
            JsonNode row = Json.Parse(line);
            long id = (long)row["id"].AsDouble();

            if (!_remains.TryGetValue(id, out FilmWindowRemains remains)) return 1;

            double t = row["t"].AsDouble();
            List<FilmWindowRemainsMap> maps = remains.Maps;

            // Written in time order already; inserted in it anyway, so a reader never depends on that.
            int at = maps.Count;
            while (at > 0 && maps[at - 1].Seconds > t) at--;
            maps.Insert(at, new FilmWindowRemainsMap { Seconds = t, Map = MapOfRow(row) });
            return 0;
        }

        // ---------------------------------------------------------------- housekeeping

        /// <summary>
        /// A text file's lines, read under a writer's share mode, so a window still being written
        /// is readable (<c>JsonlWriter.ReadRows</c>' reason).
        /// </summary>
        private static IEnumerable<string> SharedLines(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream, new System.Text.UTF8Encoding(false)))
            {
                string line;
                while ((line = reader.ReadLine()) != null) yield return line;
            }
        }

        /// <summary>One line for a log: what the window is and what it holds.</summary>
        public string Describe()
        {
            FilmWindowVerdict v = Verdict;
            var inv = CultureInfo.InvariantCulture;

            string span = FrameCount > 0
                ? string.Format(inv, "{0:0.###} to {1:0.###} s", SecondOf(0), SecondOf(FrameCount - 1))
                : "no frame";

            return string.Format(
                inv,
                "{0}: {1} frame(s), {2}, {3} genome(s), {4} plan row(s), {5} event(s), {8} corpse(s){9}; {6}{7}",
                Directory, FrameCount, span, GenomeCount, PlanRows, _events.Count,
                ProvenanceWord,
                v == null ? " (no verdict)" : " (" + v.Reason + ")", _corpseList.Count,
                _remainsList.Count > 0 ? ", " + _remainsList.Count + " remains" : "");
        }

        public void Dispose()
        {
            Stream?.Dispose();
            Stream = null;
        }
    }
}
