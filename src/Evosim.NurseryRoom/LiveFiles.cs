using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Evosim.NurseryRoom
{
    /// <summary>
    /// A file a running nursery may still be writing, read by lines and only forward. It keeps a byte
    /// offset; each <see cref="Poll"/> opens the file with <c>FileShare.ReadWrite</c> (a running
    /// nursery holds it for writing, and <c>File.ReadAllLines</c> throws on it), reads the bytes added
    /// since the last poll, and hands back the complete lines among them. A last line with no newline
    /// yet is left unread and read again, whole, on the poll after the writer finishes it. A file
    /// that shrinks, or whose creation time changed, was replaced: the offset goes back to 0 and
    /// <see cref="PollResult.Reset"/> says so. The bytes added are read in 1 MB chunks, a line that
    /// straddles two chunks carried across, so a large delta (a log that grew while nobody looked) is
    /// never one allocation.
    /// </summary>
    public sealed class LiveLines
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        /// <summary>The most bytes read in one go.</summary>
        public const int ChunkBytes = 1 << 20;

        private DateTime? _created;

        public string Path { get; }

        /// <summary>Bytes consumed: the end of the last complete line read.</summary>
        public long Offset { get; private set; }

        /// <summary>Non-blank lines handed back so far.</summary>
        public int Lines { get; private set; }

        /// <summary>The file's last-write time at the last poll, or null while it does not exist.</summary>
        public DateTime? ModifiedUtc { get; private set; }

        public long Length { get; private set; }

        /// <summary>Why the last poll could not read, or null.</summary>
        public string Problem { get; private set; }

        public LiveLines(string path) { Path = path; }

        public readonly struct PollResult
        {
            public readonly List<string> Lines;
            public readonly bool Reset;
            public PollResult(List<string> lines, bool reset) { Lines = lines; Reset = reset; }
        }

        public PollResult Poll()
        {
            var lines = new List<string>();
            bool reset = false;
            Problem = null;
            try
            {
                var info = new FileInfo(Path);
                if (!info.Exists)
                {
                    ModifiedUtc = null; Length = 0; _created = null;
                    if (Offset != 0) { Offset = 0; Lines = 0; reset = true; }
                    return new PollResult(lines, reset);
                }
                ModifiedUtc = info.LastWriteTimeUtc;
                // A file deleted and written again keeps no offset worth having, even if it is longer now.
                if (_created.HasValue && info.CreationTimeUtc != _created.Value && Offset != 0) { Offset = 0; Lines = 0; reset = true; }
                _created = info.CreationTimeUtc;
                using (var s = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    Length = s.Length;
                    if (s.Length < Offset) { Offset = 0; Lines = 0; reset = true; }
                    if (s.Length == Offset) return new PollResult(lines, reset);
                    long pos = Offset, stop = s.Length;
                    s.Seek(pos, SeekOrigin.Begin);
                    var buffer = new byte[(int)Math.Min(ChunkBytes, stop - pos)];
                    var carry = new MemoryStream();   // the front of a line begun in an earlier chunk
                    while (pos < stop)
                    {
                        int read = 0, want = (int)Math.Min(buffer.Length, stop - pos);
                        while (read < want)
                        {
                            int n = s.Read(buffer, read, want - read);
                            if (n <= 0) break;
                            read += n;
                        }
                        if (read <= 0) break;
                        int lineStart = pos == 0 && read >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF ? 3 : 0;
                        for (int i = lineStart; i < read; i++)
                        {
                            if (buffer[i] != (byte)'\n') continue;
                            string line;
                            if (carry.Length == 0)
                            {
                                int end = i;
                                if (end > lineStart && buffer[end - 1] == (byte)'\r') end--;
                                line = Utf8.GetString(buffer, lineStart, end - lineStart);
                            }
                            else
                            {
                                carry.Write(buffer, lineStart, i - lineStart);
                                byte[] whole = carry.GetBuffer();
                                int end = (int)carry.Length;
                                if (end > 0 && whole[end - 1] == (byte)'\r') end--;
                                line = Utf8.GetString(whole, 0, end);
                                carry.SetLength(0);
                            }
                            if (line.Trim().Length > 0) { lines.Add(line); Lines++; }
                            lineStart = i + 1;
                            Offset = pos + lineStart;
                        }
                        carry.Write(buffer, lineStart, read - lineStart);   // an unfinished line waits for the next chunk, or the next poll
                        pos += read;
                    }
                }
            }
            catch (IOException e) { Problem = e.GetType().Name + ": " + e.Message; }
            catch (UnauthorizedAccessException e) { Problem = e.GetType().Name + ": " + e.Message; }
            return new PollResult(lines, reset);
        }
    }

    /// <summary>
    /// A JSON-lines file read incrementally: each complete new line is parsed once and the parsed row
    /// kept (a clone, so it owns its memory). A line that does not parse is counted and skipped, so one
    /// bad row never hides the rows after it.
    /// </summary>
    public sealed class LiveRows
    {
        private readonly LiveLines _file;
        private readonly List<JsonElement> _rows = new List<JsonElement>();

        public LiveRows(string path) { _file = new LiveLines(path); }

        public string Path => _file.Path;
        public IReadOnlyList<JsonElement> Rows => _rows;
        public int Bad { get; private set; }
        public DateTime? ModifiedUtc => _file.ModifiedUtc;
        public long Length => _file.Length;
        public long Offset => _file.Offset;
        public string Problem => _file.Problem;
        public bool Exists => _file.ModifiedUtc.HasValue;

        /// <summary>Reads what has been added and returns the new rows; calls <paramref name="onReset"/> first when the file was replaced.</summary>
        public List<JsonElement> Poll(Action onReset = null)
        {
            LiveLines.PollResult r = _file.Poll();
            if (r.Reset) { _rows.Clear(); Bad = 0; onReset?.Invoke(); }
            var added = new List<JsonElement>();
            foreach (string line in r.Lines)
            {
                try
                {
                    using (JsonDocument d = JsonDocument.Parse(line))
                    {
                        JsonElement row = d.RootElement.Clone();
                        _rows.Add(row);
                        added.Add(row);
                    }
                }
                catch (JsonException) { Bad++; }
            }
            return added;
        }
    }

    /// <summary>A whole small JSON file (run.json, recipe.json), re-read only when its size or time changed.</summary>
    public sealed class LiveDocument
    {
        private long _length = -1;
        private DateTime _stamp;

        public string Path { get; }
        public JsonElement? Value { get; private set; }
        public DateTime? ModifiedUtc { get; private set; }
        public string Problem { get; private set; }

        public LiveDocument(string path) { Path = path; }

        /// <summary>True when the content changed (or appeared, or vanished) since the last call.</summary>
        public bool Poll()
        {
            Problem = null;
            try
            {
                var info = new FileInfo(Path);
                if (!info.Exists)
                {
                    bool had = Value.HasValue;
                    Value = null; ModifiedUtc = null; _length = -1;
                    return had;
                }
                ModifiedUtc = info.LastWriteTimeUtc;
                if (info.Length == _length && info.LastWriteTimeUtc == _stamp) return false;
                string text;
                using (var s = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var r = new StreamReader(s, new UTF8Encoding(false)))
                    text = r.ReadToEnd();
                using (JsonDocument d = JsonDocument.Parse(text.TrimStart('﻿')))
                    Value = d.RootElement.Clone();
                _length = info.Length; _stamp = info.LastWriteTimeUtc;
                return true;
            }
            catch (JsonException e) { Problem = "does not parse: " + e.Message; return false; }
            catch (IOException e) { Problem = e.GetType().Name + ": " + e.Message; return false; }
            catch (UnauthorizedAccessException e) { Problem = e.GetType().Name + ": " + e.Message; return false; }
        }
    }
}
