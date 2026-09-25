using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Evosim.Core;

namespace Evosim.Farm
{
    /// <summary>One window a story's scene is filmed from: which run, which span, and where it goes.</summary>
    public sealed class StoryWindow
    {
        /// <summary>The story's number for the scene, from 1 across the whole film.</summary>
        public int Scene;

        /// <summary><see cref="StoryWindows.MainPart"/>, or <see cref="StoryWindows.SecondPart"/> for a time scene's second take.</summary>
        public string Part;

        public string Arm;

        /// <summary>The station the planner read the scene as, for the log.</summary>
        public string Station;

        /// <summary>The run directory, full path, as the planner resolved it.</summary>
        public string Run;

        /// <summary>The span the farm is asked for, s.</summary>
        public double From;
        public double To;
        public double Fps;

        /// <summary>
        /// The second the part's first take opens at: the chapter card's first frame when the part
        /// opens with one, else the scene's own. The director films from here and nowhere else.
        /// </summary>
        public double Start;

        /// <summary>The chapter card's length inside the part, s, or 0.</summary>
        public double Card;

        /// <summary>The scene's own length on screen inside the part, s (the card's not counted).</summary>
        public double Seconds;

        /// <summary>The out directory as the manifest writes it, relative to the manifest's directory.</summary>
        public string Out;

        /// <summary>The out directory, full path.</summary>
        public string Directory;

        /// <summary>A birth's parent, child and second as the lineage has them, or -1 and NaN when the scene is not a birth or none was found.</summary>
        public long BirthParent = -1;
        public long BirthChild = -1;
        public double BirthAt = double.NaN;

        /// <summary>What the planner decided for the scene and why, one line each.</summary>
        public readonly List<string> Notes = new List<string>();

        public bool HasBirth => BirthChild >= 0 && !double.IsNaN(BirthAt);

        public string Line() => string.Format(
            CultureInfo.InvariantCulture,
            "scene {0} {1} on {2}: {3} from {4:0.###} to {5:0.###} s at {6:0.##} fps, opening at {7:0.###} s, into {8}",
            Scene, Part, Arm, Station, From, To, Fps, Start, Out);
    }

    /// <summary>
    /// The windows a story needs, as <c>scripts/story-windows.py</c> plans them and
    /// <c>scripts/story-windows.ps1</c> has the farm record them (<c>windows.json</c>): the safari
    /// director films a story's scene from its window (<c>logbook/specs/record-and-film-spec.md</c>,
    /// B3) and never steps the world for it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Plain .NET, so a test reads the planner's file with the reader the theatre uses.</b> The
    /// theatre reaches it through the farm package, as it reaches <see cref="FilmWindowReader"/>.
    /// </para>
    /// <para>
    /// <b>A window is recorded when its verdict says so.</b> <see cref="Recorded"/> reads the
    /// window's <c>identity.jsonl</c> alone and asks that its span, its frame rate and its run
    /// are the ones the manifest asks for. A window with no verdict (killed before its end), one
    /// filmed over another span, or one from another run is not the scene's window, and the
    /// director skips the scene rather than film it from the wrong one.
    /// </para>
    /// </remarks>
    public sealed class StoryWindows
    {
        public const string FileName = "windows.json";
        public const string Format = "story-windows 1";
        public const string MainPart = "main";
        public const string SecondPart = "time-b";

        /// <summary>How far a verdict's span may sit from the manifest's and still be the same window, s.</summary>
        public const double SpanTolerance = 1e-6;

        private readonly List<StoryWindow> _windows = new List<StoryWindow>();
        private readonly List<(int scene, string arm, string why)> _skipped = new List<(int, string, string)>();

        /// <summary>The manifest's path, full.</summary>
        public string Path { get; private set; }

        /// <summary>The manifest's directory, full: every window's out directory is relative to it.</summary>
        public string Directory { get; private set; }

        /// <summary>The story the planner read, as it wrote the path.</summary>
        public string Story { get; private set; }

        public IReadOnlyList<StoryWindow> Windows => _windows;

        /// <summary>The scenes the planner could not give a window, and why.</summary>
        public IReadOnlyList<(int scene, string arm, string why)> Skipped => _skipped;

        private StoryWindows() { }

        /// <summary>
        /// Reads a manifest from its file or from the directory that holds <see cref="FileName"/>.
        /// Throws, naming the file, on anything that is not the planner's form.
        /// </summary>
        public static StoryWindows Read(string pathOrDirectory)
        {
            if (string.IsNullOrWhiteSpace(pathOrDirectory)) throw new ArgumentException("No story windows named.");

            string path = System.IO.Directory.Exists(pathOrDirectory)
                ? System.IO.Path.Combine(pathOrDirectory, FileName)
                : pathOrDirectory;
            path = System.IO.Path.GetFullPath(path);

            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "No " + FileName + " at " + path + ": plan the windows first (scripts/story-windows.py).", path);
            }

            JsonNode root = Json.Parse(File.ReadAllText(path));

            if (root.Kind != JsonNode.NodeKind.Object || !root.Has("format") ||
                root["format"].Kind != JsonNode.NodeKind.String || root["format"].AsString() != Format)
            {
                throw new FormatException(path + " is not a '" + Format + "' manifest.");
            }

            var manifest = new StoryWindows
            {
                Path = path,
                Directory = System.IO.Path.GetDirectoryName(path),
                Story = Text(root, "story"),
            };

            if (!root.Has("windows") || root["windows"].Kind != JsonNode.NodeKind.Array)
            {
                throw new FormatException(path + " has no 'windows' list.");
            }

            int k = 0;
            foreach (JsonNode w in root["windows"].Items())
            {
                k++;
                try
                {
                    manifest._windows.Add(WindowOf(w, manifest.Directory));
                }
                catch (Exception e)
                {
                    throw new FormatException(path + ": window " + k + " is not readable: " + e.Message, e);
                }
            }

            if (root.Has("skipped") && root["skipped"].Kind == JsonNode.NodeKind.Array)
            {
                foreach (JsonNode s in root["skipped"].Items())
                {
                    if (s.Kind != JsonNode.NodeKind.Object) continue;
                    int scene = s.Has("scene") && s["scene"].Kind == JsonNode.NodeKind.Number ? s["scene"].AsInt() : -1;
                    manifest._skipped.Add((scene, Text(s, "arm"), Text(s, "why") ?? "no reason given"));
                }
            }

            return manifest;
        }

        private static StoryWindow WindowOf(JsonNode w, string directory)
        {
            var window = new StoryWindow
            {
                Scene = w["scene"].AsInt(),
                Part = Text(w, "part") ?? MainPart,
                Arm = Text(w, "arm"),
                Station = Text(w, "station"),
                Run = Text(w, "run"),
                From = w["from"].AsDouble(),
                To = w["to"].AsDouble(),
                Fps = w["fps"].AsDouble(),
                Start = w["start"].AsDouble(),
                Card = w.Has("card") && w["card"].Kind == JsonNode.NodeKind.Number ? w["card"].AsDouble() : 0d,
                Seconds = w.Has("seconds") && w["seconds"].Kind == JsonNode.NodeKind.Number ? w["seconds"].AsDouble() : double.NaN,
                Out = Text(w, "out"),
            };

            if (string.IsNullOrEmpty(window.Run)) throw new FormatException("no run");
            if (string.IsNullOrEmpty(window.Out)) throw new FormatException("no out directory");
            if (!(window.To > window.From)) throw new FormatException("the span is empty");

            window.Directory = System.IO.Path.GetFullPath(
                System.IO.Path.IsPathRooted(window.Out) ? window.Out : System.IO.Path.Combine(directory, window.Out));

            if (w.Has("birth") && w["birth"].Kind == JsonNode.NodeKind.Object)
            {
                JsonNode b = w["birth"];
                if (b.Has("parent") && b["parent"].Kind == JsonNode.NodeKind.Number) window.BirthParent = (long)b["parent"].AsDouble();
                if (b.Has("child") && b["child"].Kind == JsonNode.NodeKind.Number) window.BirthChild = (long)b["child"].AsDouble();
                if (b.Has("at") && b["at"].Kind == JsonNode.NodeKind.Number) window.BirthAt = b["at"].AsDouble();
            }

            if (w.Has("notes") && w["notes"].Kind == JsonNode.NodeKind.Array)
            {
                foreach (JsonNode n in w["notes"].Items())
                {
                    if (n.Kind == JsonNode.NodeKind.String) window.Notes.Add(n.AsString());
                }
            }

            return window;
        }

        /// <summary>A scene's window for one part, on one arm when named, or null.</summary>
        public StoryWindow Find(int scene, string part, string arm = null)
        {
            foreach (StoryWindow w in _windows)
            {
                if (w.Scene != scene || !string.Equals(w.Part, part ?? MainPart, StringComparison.Ordinal)) continue;
                if (!string.IsNullOrEmpty(arm) && !string.IsNullOrEmpty(w.Arm) && !string.Equals(w.Arm, arm, StringComparison.OrdinalIgnoreCase)) continue;
                return w;
            }

            return null;
        }

        /// <summary>Why the planner gave a scene no window, or null when it did not skip it.</summary>
        public string SkippedWhy(int scene, string arm = null)
        {
            foreach ((int s, string a, string why) in _skipped)
            {
                if (s != scene) continue;
                if (!string.IsNullOrEmpty(arm) && !string.IsNullOrEmpty(a) && !string.Equals(a, arm, StringComparison.OrdinalIgnoreCase)) continue;
                return why;
            }

            return null;
        }

        /// <summary>
        /// Null when the window's directory holds a film window whose verdict names the span, the
        /// frame rate and the run the manifest asks for; otherwise why it is not the scene's
        /// window. The verdict read is handed back either way (null when there is none).
        /// </summary>
        public static string Recorded(StoryWindow window, out FilmWindowVerdict verdict)
        {
            verdict = null;
            if (window == null) return "no window";

            if (!FilmWindowReader.IsWindow(window.Directory))
            {
                return System.IO.Directory.Exists(window.Directory)
                    ? window.Directory + " holds no " + FilmWindow.PosesFileName + ": the window was not recorded, or stopped before its first frame"
                    : "no window at " + window.Directory + ": record it with scripts/story-windows.ps1";
            }

            verdict = FilmWindowReader.ReadVerdictIn(window.Directory);
            if (verdict == null) return window.Directory + " has no verdict: the farm stopped before the window's end";

            var inv = CultureInfo.InvariantCulture;

            if (!(Math.Abs(verdict.From - window.From) <= SpanTolerance) || !(Math.Abs(verdict.To - window.To) <= SpanTolerance))
            {
                return string.Format(inv, "{0} was filmed from {1:0.###} to {2:0.###} s and the scene asks {3:0.###} to {4:0.###} s",
                    window.Directory, verdict.From, verdict.To, window.From, window.To);
            }

            if (!(Math.Abs(verdict.Fps - window.Fps) <= SpanTolerance))
            {
                return string.Format(inv, "{0} was filmed at {1:0.###} fps and the scene asks {2:0.###}", window.Directory, verdict.Fps, window.Fps);
            }

            string asked = RunName(window.Run);
            if (!string.IsNullOrEmpty(verdict.RunName) && !string.Equals(verdict.RunName, asked, StringComparison.Ordinal))
            {
                return window.Directory + " was filmed from run " + verdict.RunName + " and the scene asks " + asked;
            }

            return null;
        }

        /// <summary>A run directory's own name, as a verdict's <c>run</c> carries it.</summary>
        public static string RunName(string runDirectory) =>
            System.IO.Path.GetFileName((runDirectory ?? "").TrimEnd('/', '\\'));

        private static string Text(JsonNode node, string name) =>
            node.Has(name) && node[name].Kind == JsonNode.NodeKind.String ? node[name].AsString() : null;
    }
}
