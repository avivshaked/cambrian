using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Evosim.Core
{
    /// <summary>
    /// Gzip written in members, each member carrying its own length: the compressed files of the
    /// run record from format 2 (<c>logbook/specs/record-and-film-spec.md</c>, A1 to A3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One member per flush, and concatenated members are one gzip file.</b> RFC 1952 lets a
    /// gzip file be any number of members end to end, and every standard reader (Python's
    /// <c>gzip</c>, <c>gunzip</c>, 7-Zip) reads the whole of such a file as one stream. A writer
    /// that appends a complete member at each drain therefore leaves a file whose every complete
    /// member is readable when the process is killed, which is the property the line-oriented
    /// files have and the reason they are line-oriented (<see cref="RunDirectory"/>).
    /// </para>
    /// <para>
    /// <b>Why each member carries its length.</b> A killed write leaves the last member torn: a
    /// valid prefix of a member and nothing after it. The rule is that a torn member is detected,
    /// reported and skipped, and never parsed. A standard decoder cannot keep that rule on its own:
    /// .NET's <see cref="GZipStream"/> hands back what it inflated from a cut member and stops
    /// without complaint, and neither it nor Mono's says where in the file one member ended. So
    /// every member's header carries an extra field (RFC 1952's <c>FEXTRA</c>, the device
    /// samtools' BGZF uses for the same purpose) holding the member's whole length. A reader
    /// walks the file member by member from those lengths, and a member whose length runs past
    /// the end of the file is torn by construction, before a byte of it is inflated. Readers that
    /// do not know the field skip it, as the RFC requires, so the file is still plain gzip.
    /// </para>
    /// <para>
    /// <b>The compression is <see cref="GZipStream"/>'s, untouched.</b> A member is what
    /// <see cref="GZipStream"/> writes for the bytes it is given, with the extra field inserted
    /// after its ten-byte header and the header's <c>FEXTRA</c> flag set; the deflate stream and
    /// the CRC-32 and length trailer are its own bytes. A reader takes the field out again before
    /// handing the member back to <see cref="GZipStream"/>, so the decoder sees exactly what the
    /// encoder wrote on every runtime, Mono's included.
    /// </para>
    /// <para>
    /// The field: subfield id <c>E</c>, <c>V</c>; length 8; the member's length in bytes from its
    /// first byte to the last byte of its trailer, as a little-endian unsigned 64-bit integer. So
    /// <c>XLEN</c> is 12 and the length field ends at byte 24 of the member.
    /// </para>
    /// </remarks>
    public static class GzipMembers
    {
        /// <summary>The extra subfield's first identifying byte.</summary>
        public const byte SubfieldId1 = (byte)'E';

        /// <summary>The extra subfield's second identifying byte.</summary>
        public const byte SubfieldId2 = (byte)'V';

        /// <summary>
        /// Bytes from a member's first byte to the end of its length field: gzip's own ten, the
        /// two of <c>XLEN</c>, the subfield's four and the length's eight.
        /// </summary>
        public const int HeaderBytes = 24;

        private const int PlainHeaderBytes = 10;
        private const int ExtraBytes = 12;
        private const byte FlagExtra = 0x04;

        /// <summary>
        /// One complete member holding <paramref name="count"/> bytes of <paramref name="data"/>.
        /// </summary>
        public static byte[] Member(byte[] data, int offset, int count)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (offset < 0 || count < 0 || offset + count > data.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            byte[] plain;

            using (var buffer = new MemoryStream(count / 4 + 64))
            {
                using (var gzip = new GZipStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
                {
                    gzip.Write(data, offset, count);
                }

                plain = buffer.ToArray();
            }

            // What GZipStream writes is a bare header: magic, deflate, no flags. Anything else is
            // a runtime this was not written against, and inserting a field into a header whose
            // other optional fields are unknown would make a member no reader can walk.
            if (plain.Length < PlainHeaderBytes + 8 || plain[0] != 0x1f || plain[1] != 0x8b ||
                plain[2] != 8 || plain[3] != 0)
            {
                throw new InvalidOperationException(
                    "GZipStream wrote a member header this record does not know (" +
                    (plain.Length >= 4
                        ? plain[0].ToString("x2") + " " + plain[1].ToString("x2") + " " +
                          plain[2].ToString("x2") + " " + plain[3].ToString("x2")
                        : plain.Length + " bytes") +
                    "), so the length field cannot be inserted into it.");
            }

            long total = plain.Length + 2 + ExtraBytes;
            var member = new byte[total];

            Buffer.BlockCopy(plain, 0, member, 0, PlainHeaderBytes);
            member[3] = FlagExtra;

            // XLEN, then the one subfield.
            member[10] = ExtraBytes;
            member[11] = 0;
            member[12] = SubfieldId1;
            member[13] = SubfieldId2;
            member[14] = 8;
            member[15] = 0;
            PutUInt64(member, 16, (ulong)total);

            Buffer.BlockCopy(plain, PlainHeaderBytes, member, HeaderBytes, plain.Length - PlainHeaderBytes);
            return member;
        }

        /// <summary>The same member written straight into a stream.</summary>
        public static void Append(Stream target, byte[] data, int offset, int count)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            byte[] member = Member(data, offset, count);
            target.Write(member, 0, member.Length);
        }

        /// <summary>
        /// The bytes a complete member holds, inflated.
        /// </summary>
        /// <remarks>
        /// The extra field is taken out and the header's flag cleared before the member is handed
        /// to <see cref="GZipStream"/>, so what is decoded is the encoder's own output. The
        /// trailer's length is checked against what came out, because a decoder that stops early
        /// on a damaged deflate stream would otherwise return a shorter member and nothing else.
        /// </remarks>
        public static byte[] Inflate(byte[] member, int offset, int count)
        {
            if (member == null) throw new ArgumentNullException(nameof(member));

            if (count < HeaderBytes + 8)
            {
                throw new InvalidDataException(
                    "A member is at least " + (HeaderBytes + 8) + " bytes and this one is " + count + ".");
            }

            var plain = new byte[count - 2 - ExtraBytes];
            Buffer.BlockCopy(member, offset, plain, 0, PlainHeaderBytes);
            plain[3] = 0;
            Buffer.BlockCopy(
                member, offset + HeaderBytes, plain, PlainHeaderBytes, count - HeaderBytes);

            uint promised =
                (uint)(member[offset + count - 4] | member[offset + count - 3] << 8 |
                       member[offset + count - 2] << 16 | member[offset + count - 1] << 24);

            byte[] inflated;

            try
            {
                using (var source = new MemoryStream(plain, writable: false))
                using (var gzip = new GZipStream(source, CompressionMode.Decompress))
                using (var sink = new MemoryStream())
                {
                    gzip.CopyTo(sink);
                    inflated = sink.ToArray();
                }
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                throw new InvalidDataException(
                    "A member's deflate stream does not decode: " + e.Message, e);
            }

            if ((uint)inflated.Length != promised)
            {
                throw new InvalidDataException(
                    "A member inflates to " + inflated.Length + " bytes and its trailer says " +
                    promised + ". The member is damaged.");
            }

            return inflated;
        }

        /// <summary>
        /// Reads a member's header at the start of <paramref name="head"/> and returns its whole
        /// length, or -1 when the header is not a record member's.
        /// </summary>
        /// <param name="head">At least <see cref="HeaderBytes"/> bytes from the member's start.</param>
        public static long LengthOf(byte[] head)
        {
            if (head == null || head.Length < HeaderBytes) return -1;

            if (head[0] != 0x1f || head[1] != 0x8b || head[2] != 8) return -1;
            if ((head[3] & FlagExtra) == 0 || (head[3] & ~FlagExtra) != 0) return -1;
            if (head[10] != ExtraBytes || head[11] != 0) return -1;
            if (head[12] != SubfieldId1 || head[13] != SubfieldId2) return -1;
            if (head[14] != 8 || head[15] != 0) return -1;

            ulong length = GetUInt64(head, 16);
            return length < HeaderBytes + 8 || length > long.MaxValue ? -1 : (long)length;
        }

        private static void PutUInt64(byte[] b, int at, ulong v)
        {
            for (int i = 0; i < 8; i++) b[at + i] = (byte)(v >> (8 * i));
        }

        private static ulong GetUInt64(byte[] b, int at)
        {
            ulong v = 0;
            for (int i = 7; i >= 0; i--) v = (v << 8) | b[at + i];
            return v;
        }
    }

    /// <summary>
    /// Walks a file of <see cref="GzipMembers"/> member by member, stopping at a torn last one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Opened with <see cref="FileShare.ReadWrite"/>, <see cref="JsonlWriter.ReadRows"/>'s share
    /// mode, so a live run is readable under its writer.
    /// </para>
    /// <para>
    /// <b>Torn and damaged are different, and are answered differently.</b> A last member whose
    /// length runs past the end of the file, or whose header is cut, is what a killed write leaves:
    /// it is reported in <see cref="Torn"/> and <see cref="TornNote"/> and skipped, and everything
    /// before it is read. A complete member whose header is not a record member's, or whose bytes
    /// do not inflate to what its trailer says, is not something a kill can produce, so it is
    /// refused with an <see cref="InvalidDataException"/> rather than skipped, under the rule the
    /// loaders keep.
    /// </para>
    /// </remarks>
    public sealed class GzipMemberReader : IDisposable
    {
        private FileStream _file;

        /// <summary>The file being read.</summary>
        public string Path { get; }

        /// <summary>Complete members read so far.</summary>
        public int CompleteMembers { get; private set; }

        /// <summary>The byte offset of a torn last member, or -1 when the walk found none.</summary>
        public long TornAt { get; private set; } = -1;

        /// <summary>Bytes of the torn member on disk.</summary>
        public long TornBytes { get; private set; }

        /// <summary>Bytes the torn member's header promised, or 0 when the header itself was cut.</summary>
        public long TornPromised { get; private set; }

        /// <summary>Whether the walk ended at a torn member rather than at the end of the file.</summary>
        public bool Torn => TornAt >= 0;

        /// <summary>A sentence saying what was skipped, or null when nothing was.</summary>
        public string TornNote =>
            !Torn
                ? null
                : System.IO.Path.GetFileName(Path) + ": the last member, at byte " + TornAt +
                  ", is torn (" + TornBytes + " bytes on disk" +
                  (TornPromised > 0 ? " of the " + TornPromised + " its header promises" : ", its header cut") +
                  ") and was skipped; " + CompleteMembers + " complete member(s) before it were read";

        private GzipMemberReader(string path, FileStream file)
        {
            Path = path;
            _file = file;
        }

        /// <summary>Opens a file for a walk.</summary>
        public static GzipMemberReader Open(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 16);
            return new GzipMemberReader(path, file);
        }

        /// <summary>Every complete member, inflated, in file order.</summary>
        /// <remarks>
        /// The file's length is taken once, when the walk starts, so a live writer's member that
        /// lands during the walk is neither read nor reported torn: it is the next reader's.
        /// </remarks>
        public IEnumerable<byte[]> Members()
        {
            if (_file == null) throw new ObjectDisposedException(nameof(GzipMemberReader));

            long size = _file.Length;
            long at = 0;
            var head = new byte[GzipMembers.HeaderBytes];

            CompleteMembers = 0;
            TornAt = -1;
            TornBytes = 0;
            TornPromised = 0;

            while (at < size)
            {
                if (size - at < GzipMembers.HeaderBytes)
                {
                    Tear(at, size - at, 0);
                    yield break;
                }

                _file.Position = at;
                ReadExactly(head, GzipMembers.HeaderBytes);

                long length = GzipMembers.LengthOf(head);

                if (length < 0)
                {
                    throw new InvalidDataException(
                        System.IO.Path.GetFileName(Path) + ": the member at byte " + at + " is not " +
                        "a record member (no gzip magic, or no EV length field). A file of this " +
                        "record is refused rather than read past a member whose end is unknown.");
                }

                if (at + length > size)
                {
                    Tear(at, size - at, length);
                    yield break;
                }

                var member = new byte[length];
                _file.Position = at;
                ReadExactly(member, (int)length);

                byte[] inflated;

                try
                {
                    inflated = GzipMembers.Inflate(member, 0, member.Length);
                }
                catch (InvalidDataException e)
                {
                    throw new InvalidDataException(
                        System.IO.Path.GetFileName(Path) + ": the member at byte " + at + ": " +
                        e.Message, e);
                }

                CompleteMembers++;
                at += length;

                yield return inflated;
            }
        }

        /// <summary>
        /// Every line of every complete member, in order, without its line break.
        /// </summary>
        /// <remarks>
        /// A member written by <see cref="JsonlGzWriter"/> holds whole rows and ends with a line
        /// break, so a member that does not is refused: it would be half a row, and half a row
        /// parsed as a creature is worse than no creature. Empty lines are skipped.
        /// </remarks>
        public IEnumerable<string> Lines()
        {
            var utf8 = new UTF8Encoding(false, true);

            foreach (byte[] member in Members())
            {
                if (member.Length == 0) continue;

                if (member[member.Length - 1] != (byte)'\n')
                {
                    throw new InvalidDataException(
                        System.IO.Path.GetFileName(Path) + ": a complete member does not end at " +
                        "the end of a row. The record writes whole rows to a member.");
                }

                string text = utf8.GetString(member);
                int start = 0;

                for (int i = 0; i < text.Length; i++)
                {
                    if (text[i] != '\n') continue;

                    int end = i > start && text[i - 1] == '\r' ? i - 1 : i;
                    if (end > start) yield return text.Substring(start, end - start);
                    start = i + 1;
                }
            }
        }

        /// <summary>
        /// Every line of a file, read whole, and the torn note or null.
        /// </summary>
        /// <remarks>For a small file, a snapshot's slim rows. A large one is walked with
        /// <see cref="Lines"/>.</remarks>
        public static string[] ReadLines(string path, out string tornNote)
        {
            using (GzipMemberReader reader = Open(path))
            {
                var lines = new List<string>(reader.Lines());
                tornNote = reader.TornNote;
                return lines.ToArray();
            }
        }

        private void Tear(long at, long bytes, long promised)
        {
            TornAt = at;
            TornBytes = bytes;
            TornPromised = promised;
        }

        private void ReadExactly(byte[] into, int count)
        {
            int read = 0;
            while (read < count)
            {
                int n = _file.Read(into, read, count - read);
                if (n <= 0)
                {
                    throw new EndOfStreamException(
                        System.IO.Path.GetFileName(Path) + " ended while a member was being read.");
                }

                read += n;
            }
        }

        public void Dispose()
        {
            _file?.Dispose();
            _file = null;
        }
    }

    /// <summary>
    /// Appends JSON rows to a file of <see cref="GzipMembers"/>: <see cref="JsonlWriter"/>'s rule
    /// for a row, and one member per <see cref="EndMember"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A member is the unit of survival.</b> Rows are buffered until <see cref="EndMember"/>
    /// (or every row, with <c>memberEachRow</c>) and then written as one complete member and
    /// flushed. A killed process loses the rows since the last member and nothing before it,
    /// which is the buffered <see cref="JsonlWriter"/>'s promise at a coarser grain.
    /// </para>
    /// <para>
    /// A row with a line break is refused, as <see cref="JsonlWriter.Write"/> refuses one, and for
    /// the same reason: the reader splits a member on line breaks.
    /// </para>
    /// </remarks>
    public sealed class JsonlGzWriter : IDisposable
    {
        private FileStream _file;
        private readonly MemoryStream _pending = new MemoryStream(1 << 16);
        private readonly bool _memberEachRow;
        private int _pendingRows;

        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        public string Path { get; }

        /// <summary>Rows written, counting the ones still buffered.</summary>
        public long RowCount { get; private set; }

        /// <summary>Members written.</summary>
        public long MemberCount { get; private set; }

        /// <param name="path">File to append to. Parent directories are created.</param>
        /// <param name="memberEachRow">
        /// True writes every row as its own member: one member per sample for a file written once
        /// a sample. False buffers until <see cref="EndMember"/>.
        /// </param>
        public JsonlGzWriter(string path, bool memberEachRow)
        {
            Path = path;
            _memberEachRow = memberEachRow;

            string directory = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            _file = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        }

        public void Write(string jsonRow)
        {
            if (jsonRow == null) throw new ArgumentNullException(nameof(jsonRow));

            if (jsonRow.IndexOf('\n') >= 0 || jsonRow.IndexOf('\r') >= 0)
            {
                throw new ArgumentException(
                    "A row contains a line break, which would split one record across two lines " +
                    "and make every row after it unreadable. Serialize with indent: false.",
                    nameof(jsonRow));
            }

            byte[] bytes = Utf8.GetBytes(jsonRow);
            _pending.Write(bytes, 0, bytes.Length);
            _pending.WriteByte((byte)'\n');
            _pendingRows++;
            RowCount++;

            if (_memberEachRow) EndMember();
        }

        /// <summary>
        /// Writes the buffered rows as one member and flushes it. Nothing is written when nothing
        /// is buffered, so a quiet drain adds no empty member.
        /// </summary>
        public void EndMember()
        {
            if (_file == null) throw new ObjectDisposedException(nameof(JsonlGzWriter));
            if (_pendingRows == 0) return;

            GzipMembers.Append(_file, _pending.GetBuffer(), 0, (int)_pending.Length);
            _file.Flush();

            _pending.SetLength(0);
            _pendingRows = 0;
            MemberCount++;
        }

        public void Dispose()
        {
            if (_file == null) return;

            EndMember();
            _file.Dispose();
            _file = null;
        }
    }
}
