using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Evosim.Farm
{
    /// <summary>
    /// The state stream: <c>poses.bin</c>, every living body's pose at a cadence a picture can be
    /// played back at (<c>logbook/specs/state-stream-spec.md</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why a second file at all.</b> <c>poses.jsonl</c> is written once a sample, every ten
    /// simulated seconds, and at ten seconds a frame nothing moves: a body crosses a metre between
    /// rows. The Editor cannot make up the difference by re-simulating, because a farm recording
    /// does not replay under Mono. So the poses have to be recorded often enough to be played, and
    /// a row of JSON at half a second would be tens of gigabytes of text.
    /// </para>
    /// <para>
    /// <b>It records one thing the JSONL never had.</b> A body is born at a fraction of its adult
    /// size and grows (D087), and nothing in a run's files said how far. The stream carries the
    /// body fraction beside the pose, so a still can draw a body at the size it was.
    /// </para>
    /// <para>
    /// <b>A recording, not a world rule.</b> The cadence is <c>EVOSIM_POSE_EVERY</c>, default 0,
    /// which writes no file. It is bound where <c>EVOSIM_REPORT_EVERY</c> and
    /// <c>EVOSIM_DIGEST_EVERY</c> are bound, reaches <see cref="EnvSettings"/> and never
    /// <c>RunConfig</c>, so no config hash moves and no recorded run reads differently.
    /// </para>
    /// <para>
    /// <b>No dependency but <c>System.IO</c></b>, because the Unity project consumes
    /// <c>Evosim.Farm</c> as a local package and the theatre reads the stream through this class
    /// rather than through a second copy of the layout. Version 2's deflate is
    /// <c>System.IO.Compression</c>, which is part of the same standard library on both runtimes.
    /// </para>
    /// <para>
    /// <b>Two versions, and the header's version field decides which a reader parses</b>
    /// (<c>logbook/specs/state-stream-spec.md</c>, "Version 2"). Version 1 wrote each body raw.
    /// Version 2 deflates the bodies of each frame and gives every body one byte of guild flags,
    /// so a reader of the stream needs no second file to tell a leaf from a stomach. The frame's
    /// magic, its length, its time, its body count and its trailer stay uncompressed in both, so
    /// the index, the scan and the torn-frame rule are one rule for both versions. This build
    /// writes version 3 (version 2 with each body's seconds of reserve) and reads all three.
    /// </para>
    /// </remarks>
    public static class PoseStream
    {
        /// <summary>The stream's file name inside a run directory.</summary>
        public const string FileName = "poses.bin";

        /// <summary>The index's file name inside a run directory.</summary>
        public const string IndexFileName = "poses.idx";

        /// <summary>The format this build writes. It reads this one and every one before it.</summary>
        /// <remarks>
        /// Version 3 (2026-09-25) adds each body's seconds of reserve, which the theatre shades a
        /// body by: without it a film window drew every body as if fully fed, where the stepped
        /// route drew a starving body dark (the owner's review of round 48's first window clips).
        /// </remarks>
        public const int Version = 3;

        /// <summary>The oldest format this build reads.</summary>
        public const int OldestVersion = 1;

        /// <summary>Bytes before the first frame, in every version.</summary>
        public const int HeaderBytes = 80;

        /// <summary>Bytes before the index's first entry.</summary>
        public const int IndexHeaderBytes = 24;

        /// <summary>Bytes per index entry: a time and an offset.</summary>
        public const int IndexEntryBytes = 16;

        /// <summary>Bytes a body costs before its joint coordinates in a version 1 frame.</summary>
        public const int BodyFixedBytesVersion1 = 37;

        /// <summary>
        /// Bytes a body costs before its joint coordinates in a version 2 frame, once inflated:
        /// version 1's thirty-seven and the flags byte.
        /// </summary>
        public const int BodyFixedBytesVersion2 = 38;

        /// <summary>
        /// Bytes a body costs before its joint coordinates in a version 3 frame, once inflated:
        /// version 2's thirty-eight and a float32 of seconds of reserve after the flags byte.
        /// </summary>
        public const int BodyFixedBytesVersion3 = 42;

        /// <summary>
        /// Seconds of reserve the stream does not know: NaN, on every body of a version 1 or 2
        /// stream. A reader draws such a body fully fed, which is what every picture of those
        /// streams drew.
        /// </summary>
        public const float ReserveNotRecorded = float.NaN;

        /// <summary>Bytes of frame framing: the magic, the length and the length again.</summary>
        public const int FrameOverheadBytes = 12;

        /// <summary>
        /// Bytes at the head of a version 1 payload before its first body: the time and the count.
        /// </summary>
        public const int PayloadPrefixBytesVersion1 = 12;

        /// <summary>
        /// Bytes at the head of a version 2 payload before its deflated bodies: the time, the
        /// count and the bodies' length before deflating.
        /// </summary>
        public const int PayloadPrefixBytesVersion2 = 16;

        /// <summary>Flag bit 0: a part of the developed body is absorptive (a stomach).</summary>
        /// <remarks>
        /// The three bits are <c>positions.jsonl</c>'s, <c>PositionsRow</c>'s constants, and
        /// <c>PoseStreamTests</c> holds the two equal. They are declared again here rather than
        /// read from Core because this file touches nothing but the standard library.
        /// </remarks>
        public const int AbsorptiveBit = 1;

        /// <summary>Flag bit 1: the developed body has at least one actuated joint.</summary>
        public const int JointedBit = 2;

        /// <summary>Flag bit 2: a part of the developed body is photosynthetic (a leaf).</summary>
        public const int PhotosyntheticBit = 4;

        /// <summary>Every flag bit version 2 defines. A writer refuses any other.</summary>
        public const int AllFlagBits = AbsorptiveBit | JointedBit | PhotosyntheticBit;

        /// <summary>What <see cref="PoseBody.Flags"/> reads on a version 1 stream, which carries none.</summary>
        public const int FlagsNotRecorded = -1;

        /// <summary>
        /// A body fraction the stream does not know: NaN, written as float32 NaN in the body's
        /// fraction field (<c>logbook/specs/state-stream-spec.md</c>, "Not recorded").
        /// </summary>
        /// <remarks>
        /// The farm always knows a body's fraction and never writes this. A stream converted from
        /// a run's <c>poses.jsonl</c> (<c>scripts/record-convert.py</c>) does, for every body,
        /// because that file never carried one. A reader takes NaN as "not recorded" and draws the
        /// body at its adult size, which is what every picture drew before the stream existed;
        /// it never takes it as a size.
        /// </remarks>
        public const float FractionNotRecorded = float.NaN;

        /// <summary>Whether a fraction read from a stream is a recorded one, rather than NaN.</summary>
        public static bool FractionRecorded(float fraction) => !float.IsNaN(fraction);

        internal static readonly byte[] FileMagic =
        {
            (byte)'E', (byte)'V', (byte)'O', (byte)'P', (byte)'O', (byte)'S', (byte)'E', 0,
        };

        internal static readonly byte[] IndexMagic =
        {
            (byte)'E', (byte)'V', (byte)'O', (byte)'P', (byte)'O', (byte)'S', (byte)'X', 0,
        };

        internal const uint FrameMagic = 0x4D415246u; // 'F','R','A','M' little-endian

        /// <summary>Bytes the config hash occupies in the header, zero padded.</summary>
        internal const int ConfigHashBytes = 64;

        /// <summary>Whether a run directory holds a stream.</summary>
        public static bool Has(string runDirectory) =>
            !string.IsNullOrEmpty(runDirectory) &&
            File.Exists(Path.Combine(runDirectory, FileName));

        /// <summary>The stream inside a run directory, or null when there is none.</summary>
        public static string PathIn(string runDirectory)
        {
            if (string.IsNullOrEmpty(runDirectory)) return null;

            string path = Path.Combine(runDirectory, FileName);
            return File.Exists(path) ? path : null;
        }

        /// <summary>Whether this build reads a stream of this version.</summary>
        public static bool Reads(int version) => version >= OldestVersion && version <= Version;

        /// <summary>Bytes a body costs before its joint coordinates in a frame of this version.</summary>
        public static int BodyFixedBytes(int version) =>
            version >= 3 ? BodyFixedBytesVersion3 :
            version >= 2 ? BodyFixedBytesVersion2 : BodyFixedBytesVersion1;

        /// <summary>
        /// What a body of this many degrees of freedom costs in a frame of this version, before
        /// any deflating.
        /// </summary>
        public static int BodyBytes(int dof, int version) => BodyFixedBytes(version) + 4 * dof;

        /// <summary>Bytes at the head of a payload of this version before its bodies.</summary>
        public static int PayloadPrefixBytes(int version) =>
            version >= 2 ? PayloadPrefixBytesVersion2 : PayloadPrefixBytesVersion1;
    }

    /// <summary>What <c>poses.bin</c>'s header says about the run that wrote it.</summary>
    public sealed class PoseStreamHeader
    {
        /// <summary>The format version, which decides the layout a frame is parsed by.</summary>
        public int Version;

        /// <summary>
        /// The cadence the run recorded at, seconds. A film window's is its nominal frame
        /// interval, one over the frame rate, and its frames sit on physics steps near it.
        /// </summary>
        public float CadenceSeconds;

        /// <summary>The run's <c>configHash</c>, so a frame can be told from a cousin's.</summary>
        public string ConfigHash;
    }

    /// <summary>Where one frame sits in the file, and when it is.</summary>
    public struct PoseFrameRef
    {
        /// <summary>Simulated seconds.</summary>
        public double Seconds;

        /// <summary>Byte offset of the frame's magic.</summary>
        public long Offset;

        /// <summary>Bytes of payload between the length and the trailer.</summary>
        public int PayloadBytes;
    }

    /// <summary>One body's pose in one frame.</summary>
    public struct PoseBody
    {
        public int Id;
        public float X;
        public float Y;
        public float Z;

        /// <summary>The root link's attitude, x, y, z, w.</summary>
        public float Qx;

        public float Qy;
        public float Qz;
        public float Qw;

        /// <summary>
        /// Tissue over adult tissue: 1 is grown, and a newborn is a third of it. NaN
        /// (<see cref="PoseStream.FractionNotRecorded"/>) when the stream did not record one, as in
        /// a stream converted from <c>poses.jsonl</c>; test it with <see cref="FractionRecorded"/>.
        /// </summary>
        public float BodyFraction;

        /// <summary>Whether <see cref="BodyFraction"/> was recorded, rather than NaN.</summary>
        public bool FractionRecorded => PoseStream.FractionRecorded(BodyFraction);

        /// <summary>
        /// The guild flags, <see cref="PoseStream.AbsorptiveBit"/>,
        /// <see cref="PoseStream.JointedBit"/> and <see cref="PoseStream.PhotosyntheticBit"/>, or
        /// <see cref="PoseStream.FlagsNotRecorded"/> on a version 1 stream, which carries none.
        /// </summary>
        public int Flags;

        /// <summary>
        /// The organism's seconds of reserve (<c>Organism.SecondsOfReserve</c>), which the theatre
        /// shades a body by; positive infinity for a body with no standing cost, and
        /// <see cref="PoseStream.ReserveNotRecorded"/> (NaN) on a version 1 or 2 stream.
        /// </summary>
        public float ReserveSeconds;

        /// <summary>Joint coordinates in the solver's own order. Never null; empty for a rigid body.</summary>
        public float[] Joints;
    }

    /// <summary>One recorded instant.</summary>
    public sealed class PoseFrame
    {
        public double Seconds;
        public PoseBody[] Bodies;
    }

    /// <summary>
    /// The writer: a header once, then one length-framed record per recorded instant.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A frame is written in one call, and carries its length twice.</b> The payload's length
    /// sits before it and again after it, so a reader counts a frame complete only when the two
    /// agree. A killed run therefore loses its last frame and keeps every one before it, which is
    /// the same contract the JSONL writers hold and the reason neither of them rewrites a
    /// document.
    /// </para>
    /// <para>
    /// <b>The index is written at the end and is never the truth.</b> A killed run has none, and
    /// <see cref="PoseStreamReader"/> rebuilds one by scanning. It exists so that opening a
    /// finished 2 GB stream does not read 2 GB.
    /// </para>
    /// <para>
    /// <b>Version 2 deflates each frame's bodies and nothing else.</b> The body records are built
    /// raw, as version 1 built them with one more byte each, and deflated as one block when the
    /// frame closes. The time, the count and the raw length stay in front of the block, so a scan
    /// reads a frame's second without inflating anything, and each frame is its own deflate
    /// stream, so any frame inflates without the ones before it. The compressed bytes depend on
    /// the runtime's deflate and are not a thing to compare two files by; the inflated bodies are.
    /// </para>
    /// </remarks>
    public sealed class PoseStreamWriter : IDisposable
    {
        private readonly string _path;
        private readonly string _indexPath;
        private FileStream _file;

        // The frame as it is built: the framing and the payload's prefix at the head, then the
        // raw body records. Version 1 writes it as it stands; version 2 deflates the records.
        private byte[] _buffer = new byte[1 << 16];
        private int _at;
        private int _bodies;
        private bool _framing;

        // Version 2's deflated records, and the frame assembled around them, both reused.
        private MemoryStream _deflated;
        private byte[] _frame = new byte[0];

        private readonly List<double> _times = new List<double>();
        private readonly List<long> _offsets = new List<long>();

        /// <summary>Complete frames written so far.</summary>
        public int FrameCount => _times.Count;

        /// <summary>The cadence recorded in the header.</summary>
        public float CadenceSeconds { get; }

        /// <summary>The format this writer writes, <see cref="PoseStream.Version"/> unless asked otherwise.</summary>
        public int Version { get; }

        /// <summary>Opens the stream and writes its header, in this build's format.</summary>
        public PoseStreamWriter(string path, float cadenceSeconds, string configHash)
            : this(path, cadenceSeconds, configHash, PoseStream.Version)
        {
        }

        /// <summary>Opens the stream and writes its header, in the format named.</summary>
        /// <remarks>
        /// Version 1 is kept writable for the tests that prove it is still read, and for nothing
        /// else: a run writes <see cref="PoseStream.Version"/>.
        /// </remarks>
        public PoseStreamWriter(string path, float cadenceSeconds, string configHash, int version)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            if (!(cadenceSeconds > 0f))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cadenceSeconds), cadenceSeconds,
                    "A stream with no cadence is a stream that should not have been opened. " +
                    "EVOSIM_POSE_EVERY at 0 writes no file at all.");
            }

            if (!PoseStream.Reads(version))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(version), version,
                    "This build writes pose stream versions " + PoseStream.OldestVersion + " to " +
                    PoseStream.Version + ".");
            }

            _path = path;
            _indexPath = Path.ChangeExtension(path, ".idx");
            CadenceSeconds = cadenceSeconds;
            Version = version;

            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            // FileShare.Read so a picture can be taken of a live run, as the JSONL writers allow.
            _file = new FileStream(
                path, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 16);

            var header = new byte[PoseStream.HeaderBytes];
            Buffer.BlockCopy(PoseStream.FileMagic, 0, header, 0, 8);
            Put16(header, 8, (ushort)version);
            Put16(header, 10, PoseStream.HeaderBytes);
            PutFloat(header, 12, cadenceSeconds);

            byte[] hash = Encoding.ASCII.GetBytes(configHash ?? "");
            Buffer.BlockCopy(
                hash, 0, header, 16, Math.Min(hash.Length, PoseStream.ConfigHashBytes));

            _file.Write(header, 0, header.Length);
            _file.Flush();
        }

        /// <summary>Starts a frame. Every body written after it belongs to this instant.</summary>
        public void BeginFrame(double seconds)
        {
            if (_framing) throw new InvalidOperationException("A frame is already open.");

            _framing = true;
            _bodies = 0;
            _at = 8;                 // the magic and the length are patched in by EndFrame

            Reserve(PoseStream.PayloadPrefixBytes(Version));
            PutDouble(_buffer, _at, seconds);
            _at += 8;
            PutU32(_buffer, _at, 0); // the body count, patched in by EndFrame
            _at += 4;

            if (Version >= 2)
            {
                PutU32(_buffer, _at, 0); // the records' raw length, patched in by EndFrame
                _at += 4;
            }
        }

        /// <summary>One body of a version 1 stream, in the order the world holds its living.</summary>
        /// <remarks>
        /// Refused on a version 2 stream, which carries every body's guild flags: a body written
        /// without them would read as a body of no guild, which is a claim and not a gap.
        /// </remarks>
        public void Body(
            long id, float x, float y, float z,
            float qx, float qy, float qz, float qw,
            float bodyFraction, int dof, double[] joints)
        {
            if (Version >= 2)
            {
                throw new InvalidOperationException(
                    "A version " + Version + " stream carries every body's guild flags. Call the " +
                    "overload that takes them; a body written without them would read as " +
                    "belonging to no guild.");
            }

            Put(id, x, y, z, qx, qy, qz, qw, bodyFraction, PoseStream.FlagsNotRecorded,
                PoseStream.ReserveNotRecorded, dof, joints);
        }

        /// <summary>One body of a version 2 stream, with its guild flags.</summary>
        /// <param name="flags">
        /// <see cref="PoseStream.AbsorptiveBit"/>, <see cref="PoseStream.JointedBit"/> and
        /// <see cref="PoseStream.PhotosyntheticBit"/>, decided as <c>positions.jsonl</c> decides
        /// them. Any other bit is refused, for <c>PositionsRow.AllBits</c>' reason.
        /// </param>
        public void Body(
            long id, float x, float y, float z,
            float qx, float qy, float qz, float qw,
            float bodyFraction, int flags, int dof, double[] joints)
        {
            if (Version < 2)
            {
                throw new InvalidOperationException(
                    "A version 1 stream has no field for a body's guild flags, and dropping them " +
                    "here would lose them without saying so.");
            }

            if (flags < 0 || (flags & ~PoseStream.AllFlagBits) != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(flags), flags,
                    "A body's flags are the three guild bits and nothing else.");
            }

            Put(id, x, y, z, qx, qy, qz, qw, bodyFraction, flags, PoseStream.ReserveNotRecorded, dof, joints);
        }

        /// <summary>One body of a version 3 stream, with its guild flags and its seconds of reserve.</summary>
        /// <param name="reserveSeconds">
        /// <c>Organism.SecondsOfReserve</c>: positive infinity is allowed (a body with no standing
        /// cost), and NaN means not recorded.
        /// </param>
        public void Body(
            long id, float x, float y, float z,
            float qx, float qy, float qz, float qw,
            float bodyFraction, int flags, float reserveSeconds, int dof, double[] joints)
        {
            if (Version < 3)
            {
                throw new InvalidOperationException(
                    "A version " + Version + " stream has no field for a body's reserve, and dropping " +
                    "it here would lose it without saying so.");
            }

            if (flags < 0 || (flags & ~PoseStream.AllFlagBits) != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(flags), flags,
                    "A body's flags are the three guild bits and nothing else.");
            }

            Put(id, x, y, z, qx, qy, qz, qw, bodyFraction, flags, reserveSeconds, dof, joints);
        }

        /// <remarks>
        /// The joint coordinates arrive as the solver's own doubles and are narrowed here, which
        /// is the only place a number loses anything between the run and the file. A body fraction
        /// of NaN is written as it is, and means "not recorded"
        /// (<see cref="PoseStream.FractionNotRecorded"/>); the farm never writes one.
        /// </remarks>
        private void Put(
            long id, float x, float y, float z,
            float qx, float qy, float qz, float qw,
            float bodyFraction, int flags, float reserveSeconds, int dof, double[] joints)
        {
            if (!_framing) throw new InvalidOperationException("No frame is open.");

            if (id < int.MinValue || id > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(id), id,
                    "The stream carries an organism id as an int32, and this run has outgrown " +
                    "one. Widen the field and the version rather than truncating an identity.");
            }

            if (dof < 0 || dof > 255)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(dof), dof, "A body's degrees of freedom are recorded in one byte.");
            }

            Reserve(PoseStream.BodyBytes(dof, Version) + 4);

            PutU32(_buffer, _at, unchecked((uint)(int)id));
            _at += 4;

            PutFloat(_buffer, _at, x); _at += 4;
            PutFloat(_buffer, _at, y); _at += 4;
            PutFloat(_buffer, _at, z); _at += 4;

            PutFloat(_buffer, _at, qx); _at += 4;
            PutFloat(_buffer, _at, qy); _at += 4;
            PutFloat(_buffer, _at, qz); _at += 4;
            PutFloat(_buffer, _at, qw); _at += 4;

            PutFloat(_buffer, _at, bodyFraction); _at += 4;

            if (Version >= 2)
            {
                _buffer[_at] = (byte)flags;
                _at += 1;
            }

            if (Version >= 3)
            {
                PutFloat(_buffer, _at, reserveSeconds);
                _at += 4;
            }

            _buffer[_at] = (byte)dof;
            _at += 1;

            for (int d = 0; d < dof; d++)
            {
                PutFloat(_buffer, _at, (float)joints[d]);
                _at += 4;
            }

            _bodies++;
        }

        /// <summary>Closes the frame and writes it, framing and all, in one call.</summary>
        public void EndFrame()
        {
            if (!_framing) throw new InvalidOperationException("No frame is open.");
            _framing = false;

            PutU32(_buffer, 16, (uint)_bodies);

            double seconds = ReadDouble(_buffer, 8);
            long offset = _file.Position;

            if (Version >= 2)
            {
                WriteDeflated();
            }
            else
            {
                WriteRaw();
            }

            _file.Flush();

            _times.Add(seconds);
            _offsets.Add(offset);
        }

        /// <summary>Version 1's frame: the buffer as it stands, and the trailer after it.</summary>
        private void WriteRaw()
        {
            Reserve(4);

            int payload = _at - 8;

            PutU32(_buffer, 0, PoseStream.FrameMagic);
            PutU32(_buffer, 4, (uint)payload);
            PutU32(_buffer, _at, (uint)payload);

            _file.Write(_buffer, 0, _at + 4);
        }

        /// <summary>
        /// Version 2's frame: the prefix raw, the body records deflated, and the trailer, put
        /// together in one array so the file still takes the frame in one write.
        /// </summary>
        private void WriteDeflated()
        {
            int start = 8 + PoseStream.PayloadPrefixBytesVersion2;
            int raw = _at - start;

            PutU32(_buffer, 20, (uint)raw);

            if (_deflated == null) _deflated = new MemoryStream();
            else _deflated.SetLength(0);

            using (var deflate = new DeflateStream(_deflated, CompressionLevel.Optimal, true))
            {
                if (raw > 0) deflate.Write(_buffer, start, raw);
            }

            int packed = (int)_deflated.Length;
            int payload = PoseStream.PayloadPrefixBytesVersion2 + packed;
            int total = 8 + payload + 4;

            if (_frame.Length < total) _frame = new byte[Math.Max(total, 2 * _frame.Length)];

            PutU32(_frame, 0, PoseStream.FrameMagic);
            PutU32(_frame, 4, (uint)payload);
            Buffer.BlockCopy(_buffer, 8, _frame, 8, PoseStream.PayloadPrefixBytesVersion2);
            Buffer.BlockCopy(_deflated.GetBuffer(), 0, _frame, start, packed);
            PutU32(_frame, start + packed, (uint)payload);

            _file.Write(_frame, 0, total);
        }

        /// <summary>Writes <c>poses.idx</c>. Called by <see cref="Dispose"/>.</summary>
        public void WriteIndex()
        {
            var bytes = new byte[PoseStream.IndexHeaderBytes +
                                 PoseStream.IndexEntryBytes * _times.Count];

            Buffer.BlockCopy(PoseStream.IndexMagic, 0, bytes, 0, 8);
            Put16(bytes, 8, (ushort)Version);
            Put16(bytes, 10, PoseStream.IndexHeaderBytes);
            PutU32(bytes, 12, (uint)_times.Count);

            int at = PoseStream.IndexHeaderBytes;

            for (int i = 0; i < _times.Count; i++)
            {
                PutDouble(bytes, at, _times[i]); at += 8;
                PutI64(bytes, at, _offsets[i]); at += 8;
            }

            using (var index = new FileStream(
                       _indexPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                index.Write(bytes, 0, bytes.Length);
            }
        }

        public void Dispose()
        {
            if (_file == null) return;

            if (_framing)
            {
                // A frame opened and not closed is not a frame. Dropped rather than written
                // half-formed, which is what the trailer exists to make impossible anyway.
                _framing = false;
            }

            _file.Flush();
            _file.Dispose();
            _file = null;

            WriteIndex();
        }

        private void Reserve(int more)
        {
            if (_at + more <= _buffer.Length) return;

            int size = _buffer.Length;
            while (size < _at + more) size *= 2;

            Array.Resize(ref _buffer, size);
        }

        // ------------------------------------------------------------------ little-endian puts
        //
        // By hand rather than through BitConverter, which is little-endian on every machine this
        // runs on and is not contractually so.

        internal static void Put16(byte[] b, int at, ushort v)
        {
            b[at] = (byte)v;
            b[at + 1] = (byte)(v >> 8);
        }

        internal static void PutU32(byte[] b, int at, uint v)
        {
            b[at] = (byte)v;
            b[at + 1] = (byte)(v >> 8);
            b[at + 2] = (byte)(v >> 16);
            b[at + 3] = (byte)(v >> 24);
        }

        internal static void PutI64(byte[] b, int at, long v)
        {
            ulong u = unchecked((ulong)v);
            for (int i = 0; i < 8; i++) b[at + i] = (byte)(u >> (8 * i));
        }

        internal static void PutFloat(byte[] b, int at, float v)
        {
            PutU32(b, at, unchecked((uint)BitConverter.SingleToInt32Bits(v)));
        }

        internal static void PutDouble(byte[] b, int at, double v)
        {
            PutI64(b, at, BitConverter.DoubleToInt64Bits(v));
        }

        internal static double ReadDouble(byte[] b, int at)
        {
            ulong u = 0;
            for (int i = 0; i < 8; i++) u |= (ulong)b[at + i] << (8 * i);
            return BitConverter.Int64BitsToDouble(unchecked((long)u));
        }
    }

    /// <summary>
    /// The reader: a header, an index of frames, and a frame at a time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The index on disk is checked and not trusted.</b> Its magic, its version, its count
    /// against the file's length and its first and last entries against the frames they point at
    /// all have to hold; any failure falls back to a scan. A run that was killed, or one still
    /// running, has no index at all and is read by scanning every time.
    /// </para>
    /// <para>
    /// <b>A scan reads twenty bytes a frame, not a frame.</b> It walks the framing and the time
    /// and skips each payload, and it stops at the first frame whose magic, length or trailer does
    /// not hold. That stop is how a torn last write is dropped. It is the same walk for both
    /// versions, because version 2 keeps the time in front of what it deflates.
    /// </para>
    /// <para>
    /// <b>A version 2 frame is inflated when it is read and checked when it is inflated.</b> The
    /// bodies have to inflate to exactly the length the frame states, and to exactly the bodies
    /// its count says; a frame that disagrees with itself is refused rather than read short.
    /// </para>
    /// <para>
    /// <b>A NaN body fraction is read as NaN and means "not recorded"</b>
    /// (<see cref="PoseStream.FractionNotRecorded"/>). A stream converted from a run's
    /// <c>poses.jsonl</c> carries it for every body. The reader hands it over unchanged rather
    /// than as 1, so a caller can tell a grown body from one whose size nobody wrote down, and
    /// <see cref="PoseBody.FractionRecorded"/> is the test.
    /// </para>
    /// </remarks>
    public sealed class PoseStreamReader : IDisposable
    {
        private FileStream _file;
        private readonly byte[] _small = new byte[16];

        /// <summary>What the file's header says.</summary>
        public PoseStreamHeader Header { get; private set; }

        /// <summary>Every complete frame, ascending.</summary>
        public PoseFrameRef[] Frames { get; private set; }

        /// <summary>Whether the index on disk was used rather than a scan.</summary>
        public bool IndexRead { get; private set; }

        private PoseStreamReader() { }

        /// <summary>Opens a stream for reading. Refuses a file this build does not know.</summary>
        public static PoseStreamReader Open(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            var reader = new PoseStreamReader
            {
                // ReadWrite, never Read: a live run holds the file open for writing, and a picture
                // of a live arm is the thing this was built for.
                _file = new FileStream(
                    path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 16),
            };

            try
            {
                reader.Header = reader.ReadHeader();
                reader.Frames = reader.BuildIndex(Path.ChangeExtension(path, ".idx"));
            }
            catch
            {
                reader.Dispose();
                throw;
            }

            return reader;
        }

        /// <summary>Frame <c>i</c>, read whole, in the layout the header's version names.</summary>
        public PoseFrame Read(int i)
        {
            if (i < 0 || i >= Frames.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(i), i, "The stream holds " + Frames.Length + " complete frame(s).");
            }

            PoseFrameRef reference = Frames[i];

            var payload = new byte[reference.PayloadBytes];
            _file.Seek(reference.Offset + 8, SeekOrigin.Begin);
            ReadExactly(payload, payload.Length);

            var frame = new PoseFrame { Seconds = Get.Double(payload, 0) };

            long count = Get.U32(payload, 8);
            int version = Header.Version;

            byte[] records;
            int at;
            int end;

            if (version >= 2)
            {
                long raw = Get.U32(payload, 12);

                if (raw > int.MaxValue)
                {
                    throw new InvalidDataException(
                        "The frame at byte " + reference.Offset + " says its bodies are " + raw +
                        " bytes before deflating, which no frame this format writes can be.");
                }

                records = Inflate(payload, PoseStream.PayloadPrefixBytesVersion2, (int)raw, reference.Offset);
                at = 0;
                end = records.Length;
            }
            else
            {
                records = payload;
                at = PoseStream.PayloadPrefixBytesVersion1;
                end = payload.Length;
            }

            int fixedBytes = PoseStream.BodyFixedBytes(version);

            // Refused rather than trusted: a count the records cannot hold is a frame that is not
            // what its header says, and an array sized off it would be a guess.
            if (count > (end - at) / fixedBytes)
            {
                throw new InvalidDataException(
                    "The frame at byte " + reference.Offset + " says " + count + " bodies and " +
                    "holds " + (end - at) + " bytes of them.");
            }

            var bodies = new PoseBody[count];

            for (long b = 0; b < count; b++)
            {
                if (at + fixedBytes > end) throw Short(reference.Offset, count);

                var body = new PoseBody
                {
                    Id = unchecked((int)Get.U32(records, at)),
                    X = Get.Float(records, at + 4),
                    Y = Get.Float(records, at + 8),
                    Z = Get.Float(records, at + 12),
                    Qx = Get.Float(records, at + 16),
                    Qy = Get.Float(records, at + 20),
                    Qz = Get.Float(records, at + 24),
                    Qw = Get.Float(records, at + 28),
                    BodyFraction = Get.Float(records, at + 32),
                    Flags = version >= 2 ? records[at + 36] : PoseStream.FlagsNotRecorded,
                    ReserveSeconds = version >= 3 ? Get.Float(records, at + 37) : PoseStream.ReserveNotRecorded,
                };

                int dof = records[at + fixedBytes - 1];
                at += fixedBytes;

                if (at + 4 * dof > end) throw Short(reference.Offset, count);

                var joints = new float[dof];
                for (int d = 0; d < dof; d++) joints[d] = Get.Float(records, at + 4 * d);
                at += 4 * dof;

                body.Joints = joints;
                bodies[b] = body;
            }

            if (at != end)
            {
                throw new InvalidDataException(
                    "The frame at byte " + reference.Offset + " says " + count + " bodies and " +
                    "they end " + (end - at) + " bytes before its records do.");
            }

            frame.Bodies = bodies;
            return frame;
        }

        private static InvalidDataException Short(long offset, long count) =>
            new InvalidDataException(
                "The frame at byte " + offset + " says " + count + " bodies and its records end " +
                "inside one of them.");

        /// <summary>
        /// A version 2 frame's body records, inflated, and checked to be exactly as long as the
        /// frame says they are.
        /// </summary>
        private static byte[] Inflate(byte[] payload, int start, int raw, long offset)
        {
            var records = new byte[raw];
            var extra = new byte[1];

            using (var source = new MemoryStream(payload, start, payload.Length - start, false))
            using (var inflate = new DeflateStream(source, CompressionMode.Decompress))
            {
                int got = 0;

                while (got < raw)
                {
                    int n = inflate.Read(records, got, raw - got);
                    if (n <= 0) break;
                    got += n;
                }

                if (got != raw)
                {
                    throw new InvalidDataException(
                        "The frame at byte " + offset + " says its bodies inflate to " + raw +
                        " bytes and they inflate to " + got + ".");
                }

                if (inflate.Read(extra, 0, 1) > 0)
                {
                    throw new InvalidDataException(
                        "The frame at byte " + offset + " says its bodies inflate to " + raw +
                        " bytes and they inflate to more.");
                }
            }

            return records;
        }

        /// <summary>The frame at a second, or null when the stream has none there.</summary>
        public PoseFrame At(double seconds, double tolerance = 1e-3)
        {
            int found = IndexOf(seconds, tolerance);
            return found < 0 ? null : Read(found);
        }

        /// <summary>Which frame is at a second, or -1.</summary>
        public int IndexOf(double seconds, double tolerance = 1e-3)
        {
            int best = -1;
            double closest = double.MaxValue;

            for (int i = 0; i < Frames.Length; i++)
            {
                double gap = Math.Abs(Frames[i].Seconds - seconds);
                if (gap > tolerance || gap >= closest) continue;

                closest = gap;
                best = i;
            }

            return best;
        }

        /// <summary>Every frame's second, ascending.</summary>
        public double[] Seconds()
        {
            var seconds = new double[Frames.Length];
            for (int i = 0; i < Frames.Length; i++) seconds[i] = Frames[i].Seconds;
            return seconds;
        }

        public void Dispose()
        {
            _file?.Dispose();
            _file = null;
        }

        // ------------------------------------------------------------------ opening

        private PoseStreamHeader ReadHeader()
        {
            var header = new byte[PoseStream.HeaderBytes];

            _file.Seek(0, SeekOrigin.Begin);

            if (_file.Length < PoseStream.HeaderBytes)
            {
                throw new InvalidDataException(
                    "A pose stream is at least " + PoseStream.HeaderBytes + " bytes of header, " +
                    "and this file is " + _file.Length + ".");
            }

            ReadExactly(header, header.Length);

            for (int i = 0; i < PoseStream.FileMagic.Length; i++)
            {
                if (header[i] == PoseStream.FileMagic[i]) continue;

                throw new InvalidDataException(
                    "This is not a pose stream: the first eight bytes are " +
                    Describe(header, 8) + " and a stream's are EVOPOSE and a zero.");
            }

            int version = Get.U16(header, 8);

            if (!PoseStream.Reads(version))
            {
                throw new InvalidDataException(
                    "The stream is version " + version + " and this build reads versions " +
                    PoseStream.OldestVersion + " to " + PoseStream.Version + ". A stream is " +
                    "refused rather than read with a field guessed at, under the rule the config " +
                    "reader follows.");
            }

            int headerBytes = Get.U16(header, 10);

            if (headerBytes != PoseStream.HeaderBytes)
            {
                throw new InvalidDataException(
                    "The header says it is " + headerBytes + " bytes and version " + version +
                    "'s header is " + PoseStream.HeaderBytes + ".");
            }

            int end = 16;
            while (end < 16 + PoseStream.ConfigHashBytes && header[end] != 0) end++;

            return new PoseStreamHeader
            {
                Version = version,
                CadenceSeconds = Get.Float(header, 12),
                ConfigHash = Encoding.ASCII.GetString(header, 16, end - 16),
            };
        }

        private PoseFrameRef[] BuildIndex(string indexPath)
        {
            PoseFrameRef[] fromDisk = TryReadIndex(indexPath);

            if (fromDisk != null)
            {
                IndexRead = true;
                return fromDisk;
            }

            IndexRead = false;
            return Scan();
        }

        /// <summary>The index on disk, or null when it is missing or does not check out.</summary>
        private PoseFrameRef[] TryReadIndex(string indexPath)
        {
            if (!File.Exists(indexPath)) return null;

            byte[] bytes;

            try
            {
                using (var index = new FileStream(
                           indexPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (index.Length < PoseStream.IndexHeaderBytes) return null;

                    bytes = new byte[index.Length];

                    int got = 0;
                    while (got < bytes.Length)
                    {
                        int n = index.Read(bytes, got, bytes.Length - got);
                        if (n <= 0) return null;
                        got += n;
                    }
                }
            }
            catch (IOException)
            {
                return null;
            }

            for (int i = 0; i < PoseStream.IndexMagic.Length; i++)
            {
                if (bytes[i] != PoseStream.IndexMagic[i]) return null;
            }

            // The index is the stream's own: one written beside a stream of another version is
            // not this stream's index, whatever its entries say.
            if (Get.U16(bytes, 8) != Header.Version) return null;
            if (Get.U16(bytes, 10) != PoseStream.IndexHeaderBytes) return null;

            long count = Get.U32(bytes, 12);

            if (bytes.Length != PoseStream.IndexHeaderBytes + PoseStream.IndexEntryBytes * count)
            {
                return null;
            }

            var frames = new PoseFrameRef[count];

            for (long i = 0; i < count; i++)
            {
                int at = PoseStream.IndexHeaderBytes + (int)(PoseStream.IndexEntryBytes * i);

                frames[i] = new PoseFrameRef
                {
                    Seconds = Get.Double(bytes, at),
                    Offset = Get.I64(bytes, at + 8),
                };
            }

            // The first and the last, checked against the frames they claim. An index that
            // disagrees with its file is worse than no index, so it is dropped for the scan.
            if (count > 0)
            {
                if (!Measure(ref frames[0])) return null;
                if (!Measure(ref frames[count - 1])) return null;
            }

            // Every other entry's payload length, which the entry does not carry. Cheap: it is a
            // seek and eight bytes a frame, against a scan's twenty.
            for (long i = 1; i < count - 1; i++)
            {
                if (!Measure(ref frames[i])) return null;
            }

            return frames;
        }

        /// <summary>
        /// Fills in a frame reference's payload length and checks its framing. False when the
        /// offset does not hold a complete frame at the time the entry claims.
        /// </summary>
        private bool Measure(ref PoseFrameRef reference)
        {
            if (reference.Offset < PoseStream.HeaderBytes ||
                reference.Offset + PoseStream.FrameOverheadBytes > _file.Length)
            {
                return false;
            }

            _file.Seek(reference.Offset, SeekOrigin.Begin);
            if (!TryRead(_small, 16)) return false;

            if (Get.U32(_small, 0) != PoseStream.FrameMagic) return false;

            long payload = Get.U32(_small, 4);

            if (payload < PoseStream.PayloadPrefixBytes(Header.Version) ||
                reference.Offset + 8 + payload + 4 > _file.Length)
            {
                return false;
            }

            if (Math.Abs(Get.Double(_small, 8) - reference.Seconds) > 1e-9) return false;

            _file.Seek(reference.Offset + 8 + payload, SeekOrigin.Begin);
            if (!TryRead(_small, 4)) return false;
            if (Get.U32(_small, 0) != payload) return false;

            reference.PayloadBytes = (int)payload;
            return true;
        }

        /// <summary>Every complete frame, found by walking the file.</summary>
        public PoseFrameRef[] Scan()
        {
            var frames = new List<PoseFrameRef>();
            long at = PoseStream.HeaderBytes;

            while (at + PoseStream.FrameOverheadBytes <= _file.Length)
            {
                _file.Seek(at, SeekOrigin.Begin);
                if (!TryRead(_small, 16)) break;

                if (Get.U32(_small, 0) != PoseStream.FrameMagic) break;

                long payload = Get.U32(_small, 4);
                if (payload < PoseStream.PayloadPrefixBytes(Header.Version) ||
                    at + 8 + payload + 4 > _file.Length)
                {
                    break;
                }

                // The sixteen bytes just read are the magic, the length and the payload's first
                // eight, which are the frame's time. Taken before the trailer read overwrites it.
                double seconds = Get.Double(_small, 8);

                _file.Seek(at + 8 + payload, SeekOrigin.Begin);
                if (!TryRead(_small, 4)) break;
                if (Get.U32(_small, 0) != payload) break;

                frames.Add(new PoseFrameRef
                {
                    Seconds = seconds,
                    Offset = at,
                    PayloadBytes = (int)payload,
                });

                at += 8 + payload + 4;
            }

            return frames.ToArray();
        }

        private void ReadExactly(byte[] into, int count)
        {
            if (!TryRead(into, count))
            {
                throw new EndOfStreamException(
                    "The stream ends " + count + " bytes short of what its framing promised.");
            }
        }

        private bool TryRead(byte[] into, int count)
        {
            int got = 0;

            while (got < count)
            {
                int n = _file.Read(into, got, count - got);
                if (n <= 0) return false;
                got += n;
            }

            return true;
        }

        private static string Describe(byte[] bytes, int count)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < count; i++) sb.Append(bytes[i].ToString("x2")).Append(' ');
            return sb.ToString().TrimEnd();
        }

        /// <summary>Little-endian gets, the mirror of the writer's puts.</summary>
        internal static class Get
        {
            public static int U16(byte[] b, int at) => b[at] | (b[at + 1] << 8);

            public static uint U32(byte[] b, int at) =>
                (uint)(b[at] | (b[at + 1] << 8) | (b[at + 2] << 16) | (b[at + 3] << 24));

            public static long I64(byte[] b, int at)
            {
                ulong u = 0;
                for (int i = 0; i < 8; i++) u |= (ulong)b[at + i] << (8 * i);
                return unchecked((long)u);
            }

            public static float Float(byte[] b, int at) =>
                BitConverter.Int32BitsToSingle(unchecked((int)U32(b, at)));

            public static double Double(byte[] b, int at) =>
                BitConverter.Int64BitsToDouble(I64(b, at));
        }
    }
}
