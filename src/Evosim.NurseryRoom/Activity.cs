using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Evosim.NurseryRoom
{
    /// <summary>
    /// The room's activity log: one JSONL row for every operation that changes anything, appended and
    /// never rewritten. A row says when (UTC), which operation, with which arguments (a <c>text</c> body
    /// by its hash), the reason, who asked (<c>screen</c> or <c>mcp</c>, by the door the call came
    /// through) and how it ended (<c>ok</c>, <c>refused</c> or <c>failed</c>, and why). It holds no secret
    /// and no path outside the repository.
    /// </summary>
    public sealed class ActivityLog
    {
        // What a free-text value must never carry: a drive path (C:\x, C:/x), a UNC path (\\host\share, //host/share) and an absolute path that
        // starts at a slash (/etc/x). The slash forms are taken only where no word, colon, dot or slash comes before them, so "and/or", "1/2" and
        // "http://host" are left alone, as is a repository-relative path such as rounds/nursery/x.json.
        private static readonly System.Text.RegularExpressions.Regex DrivePath = new System.Text.RegularExpressions.Regex(@"\b[A-Za-z]:[\\/]\S*");
        private static readonly System.Text.RegularExpressions.Regex UncPath = new System.Text.RegularExpressions.Regex(@"(?<![:A-Za-z0-9_.\-])(?:\\\\|//)[^\s\\/]+(?:[\\/]\S*)?");
        private static readonly System.Text.RegularExpressions.Regex RootedPath = new System.Text.RegularExpressions.Regex(@"(?<![:A-Za-z0-9_.\-/\\])/[^\s/]+(?:/\S*)?");

        /// <summary>The longest reason an operation takes.</summary>
        public const int MaxReason = 500;

        private readonly string _path;
        private readonly object _gate = new object();

        public ActivityLog(string path) { _path = path; }

        public string Path => _path;

        /// <summary>
        /// Appends one line: the file opened for append with <c>FileShare.ReadWrite</c> so that the room's own
        /// readers and any other process can read it meanwhile, the line written in one call. A line holding
        /// a line break is refused, as <c>JsonlWriter</c> refuses one, since one embedded break makes every
        /// row after it unreadable.
        /// </summary>
        public void AppendLine(string line)
        {
            if (line == null) throw new ArgumentNullException(nameof(line));
            if (line.IndexOfAny(new[] { '\n', '\r' }) >= 0) throw new ArgumentException("an activity row is one line; this one holds a line break");
            byte[] bytes = new UTF8Encoding(false).GetBytes(line + "\n");
            lock (_gate)
            {
                string dir = System.IO.Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                // Two room processes may append at once (a smoke test beside the room): the named mutex holds the others off
                // for the open, the write and the close, so a row is never cut by another's.
                using (Held held = Hold())
                {
                    for (int attempt = 1; ; attempt++)
                    {
                        try
                        {
                            using (var s = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                                s.Write(bytes, 0, bytes.Length);
                            return;
                        }
                        catch (IOException) when (attempt < 5) { System.Threading.Thread.Sleep(100); }
                    }
                }
            }
        }

        /// <summary>The mutex every room process takes round an append: machine-wide where the session may create one, else this session's.</summary>
        public const string GlobalMutexName = "Global\\evosim-nursery-activity", LocalMutexName = "Local\\evosim-nursery-activity";

        private static System.Threading.Mutex _mutex;
        private static readonly object MutexGate = new object();

        private static System.Threading.Mutex SharedMutex()
        {
            lock (MutexGate)
            {
                if (_mutex != null) return _mutex;
                try { _mutex = new System.Threading.Mutex(false, GlobalMutexName); }
                catch (Exception e) when (e is UnauthorizedAccessException || e is IOException || e is System.Threading.WaitHandleCannotBeOpenedException)
                {
                    _mutex = new System.Threading.Mutex(false, LocalMutexName);
                }
                return _mutex;
            }
        }

        private sealed class Held : IDisposable
        {
            private readonly System.Threading.Mutex _m;
            public Held(System.Threading.Mutex m) { _m = m; }
            public void Dispose() { try { _m.ReleaseMutex(); } catch (ApplicationException) { } }
        }

        private static Held Hold()
        {
            System.Threading.Mutex m = SharedMutex();
            try
            {
                if (!m.WaitOne(TimeSpan.FromSeconds(10))) throw new IOException("another room process held the activity log's mutex for more than 10 s");
            }
            catch (System.Threading.AbandonedMutexException) { /* its holder died mid-append; this process holds it now */ }
            return new Held(m);
        }

        /// <summary>Serialises a row and appends it.</summary>
        public void Append(object row) => AppendLine(Js.Serialize(row));

        /// <summary>
        /// The rows, newest first, at most <paramref name="limit"/> of them; <paramref name="total"/> is how many the file
        /// holds and <paramref name="unreadable"/> how many of its lines were not rows.
        /// </summary>
        public List<JsonElement> ReadNewestFirst(int limit, out int total, out int unreadable)
        {
            total = 0; unreadable = 0;
            var rows = new List<JsonElement>();
            if (!File.Exists(_path)) return rows;
            string text;
            lock (_gate)
            {
                using (var s = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var r = new StreamReader(s, new UTF8Encoding(false)))
                    text = r.ReadToEnd();
            }
            foreach (string line in text.Split('\n'))
            {
                if (line.Trim().Length == 0) continue;
                try
                {
                    JsonElement e = Js.Parse(line);
                    if (e.ValueKind != JsonValueKind.Object) { unreadable++; continue; }
                    rows.Add(e);
                    total++;
                }
                catch (JsonException) { unreadable++; }
            }
            rows.Reverse();
            return rows.Count > limit ? rows.GetRange(0, limit) : rows;
        }

        /// <summary>The same, all of it, oldest first.</summary>
        public List<JsonElement> ReadAll()
        {
            List<JsonElement> r = ReadNewestFirst(int.MaxValue, out _, out _);
            r.Reverse();
            return r;
        }

        // ------------------------------------------------------------------ what goes in a row

        /// <summary>A free-text value for a row: control characters replaced, and cut to <paramref name="max"/> characters.</summary>
        public static string Clean(string s, int max)
        {
            if (s == null) return null;
            // A drive path in free text (a reason, a message that quotes what the caller sent) is never kept: a row holds no path outside the repository.
            s = RootedPath.Replace(UncPath.Replace(DrivePath.Replace(s, "(a path)"), "(a path)"), "(a path)");
            var sb = new StringBuilder(Math.Min(s.Length, max + 1));
            foreach (char c in s)
            {
                if (sb.Length >= max) { sb.Append('…'); break; }
                sb.Append(char.IsControl(c) ? ' ' : c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// The arguments as a row records them: <c>reason</c> left out (it has its own field), each hashed argument
        /// replaced by its SHA-256 (and its length), each path argument made repository-relative or marked as
        /// outside, and every other string cleaned and cut.
        /// </summary>
        public static Dictionary<string, object> ArgsOf(Operation op, JsonElement args, Func<string, string> relativePath)
        {
            var d = new Dictionary<string, object>(StringComparer.Ordinal);
            if (args.ValueKind != JsonValueKind.Object) return d;
            foreach (JsonProperty p in args.EnumerateObject())
            {
                if (p.Name == "reason") continue;
                if (op.HashArgs.Contains(p.Name) && p.Value.ValueKind == JsonValueKind.String)
                {
                    string text = p.Value.GetString();
                    d[p.Name + "Sha256"] = Hex(System.Security.Cryptography.SHA256.HashData(new UTF8Encoding(false).GetBytes(text)));
                    d[p.Name + "Chars"] = text.Length;
                    continue;
                }
                if (p.Value.ValueKind == JsonValueKind.String)
                {
                    string v = p.Value.GetString();
                    d[p.Name] = op.PathArgs.Contains(p.Name) ? Clean(relativePath(v), 300) : Clean(v, 300);
                    continue;
                }
                d[p.Name] = p.Value.ValueKind == JsonValueKind.Number || p.Value.ValueKind == JsonValueKind.True || p.Value.ValueKind == JsonValueKind.False || p.Value.ValueKind == JsonValueKind.Null
                    ? (object)Js.Parse(p.Value.GetRawText())
                    : Clean(p.Value.GetRawText(), 300);
            }
            return d;
        }

        public static string Hex(byte[] b) => string.Concat(b.Select(x => x.ToString("x2")));
    }
}
