using System;
using System.Collections.Generic;
using System.IO;
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
    /// rather than through a second copy of the layout.
    /// </para>
    /// </remarks>
    public static class PoseStream
    {
        /// <summary>The stream's file name inside a run directory.</summary>
        public const string FileName = "poses.bin";

        /// <summary>The index's file name inside a run directory.</summary>
        public const string IndexFileName = "poses.idx";

        /// <summary>The format this build writes and the only one it reads.</summary>
        public const int Version = 1;

        /// <summary>Bytes before the first frame.</summary>
        public const int HeaderBytes = 80;

        /// <summary>Bytes before the index's first entry.</summary>
        public const int IndexHeaderBytes = 24;

        /// <summary>Bytes per index entry: a time and an offset.</summary>
        public const int IndexEntryBytes = 16;

        /// <summary>Bytes a body costs before its joint coordinates.</summary>
        public const int BodyFixedBytes = 37;

        /// <summary>Bytes of frame framing: the magic, the length and the length again.</summary>
        public const int FrameOverheadBytes = 12;

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

        /// <summary>What a body of this many degrees of freedom costs in a frame.</summary>
        public static int BodyBytes(int dof) => BodyFixedBytes + 4 * dof;
    }

    /// <summary>What <c>poses.bin</c>'s header says about the run that wrote it.</summary>
    public sealed class PoseStreamHeader
    {
        /// <summary>The format version. This build reads <see cref="PoseStream.Version"/> alone.</summary>
        public int Version;

        /// <summary>The cadence the run recorded at, seconds.</summary>
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

        /// <summary>Tissue over adult tissue: 1 is grown, and a newborn is a third of it.</summary>
        public float BodyFraction;

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
    /// </remarks>
    public sealed class PoseStreamWriter : IDisposable
    {
        private readonly string _path;
        private readonly string _indexPath;
        private FileStream _file;

        private byte[] _buffer = new byte[1 << 16];
        private int _at;
        private int _bodies;
        private bool _framing;

        private readonly List<double> _times = new List<double>();
        private readonly List<long> _offsets = new List<long>();

        /// <summary>Complete frames written so far.</summary>
        public int FrameCount => _times.Count;

        /// <summary>The cadence recorded in the header.</summary>
        public float CadenceSeconds { get; }

        /// <summary>Opens the stream and writes its header.</summary>
        public PoseStreamWriter(string path, float cadenceSeconds, string configHash)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            if (!(cadenceSeconds > 0f))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cadenceSeconds), cadenceSeconds,
                    "A stream with no cadence is a stream that should not have been opened. " +
                    "EVOSIM_POSE_EVERY at 0 writes no file at all.");
            }

            _path = path;
            _indexPath = Path.ChangeExtension(path, ".idx");
            CadenceSeconds = cadenceSeconds;

            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            // FileShare.Read so a picture can be taken of a live run, as the JSONL writers allow.
            _file = new FileStream(
                path, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 16);

            var header = new byte[PoseStream.HeaderBytes];
            Buffer.BlockCopy(PoseStream.FileMagic, 0, header, 0, 8);
            Put16(header, 8, (ushort)PoseStream.Version);
            Put16(header, 10, PoseStream.HeaderBytes);
            PutFloat(header, 12, cadenceSeconds);

            byte[] hash = Encoding.ASCII.GetBytes(configHash ?? "");
            Buffer.BlockCopy(
                hash, 0, header, 16, Math.Min(hash.Length, PoseStream.ConfigHashBytes));

            _file.Write(header, 0, header.Length);
            _file.Flush();
        }

        /// <summary>Starts a frame. Every <see cref="Body"/> after it belongs to this instant.</summary>
        public void BeginFrame(double seconds)
        {
            if (_framing) throw new InvalidOperationException("A frame is already open.");

            _framing = true;
            _bodies = 0;
            _at = 8;                 // the magic and the length are patched in by EndFrame

            Reserve(12);
            PutDouble(_buffer, _at, seconds);
            _at += 8;
            PutU32(_buffer, _at, 0); // the body count, patched in by EndFrame
            _at += 4;
        }

        /// <summary>One body, in the order the world holds its living.</summary>
        /// <remarks>
        /// The joint coordinates arrive as the solver's own doubles and are narrowed here, which
        /// is the only place a number loses anything between the run and the file.
        /// </remarks>
        public void Body(
            long id, float x, float y, float z,
            float qx, float qy, float qz, float qw,
            float bodyFraction, int dof, double[] joints)
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

            Reserve(PoseStream.BodyBytes(dof) + 4);

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

            int payload = _at - 8;

            PutU32(_buffer, 0, PoseStream.FrameMagic);
            PutU32(_buffer, 4, (uint)payload);
            PutU32(_buffer, _at, (uint)payload);

            long offset = _file.Position;

            _file.Write(_buffer, 0, _at + 4);
            _file.Flush();

            double seconds = ReadDouble(_buffer, 8);
            _times.Add(seconds);
            _offsets.Add(offset);
        }

        /// <summary>Writes <c>poses.idx</c>. Called by <see cref="Dispose"/>.</summary>
        public void WriteIndex()
        {
            var bytes = new byte[PoseStream.IndexHeaderBytes +
                                 PoseStream.IndexEntryBytes * _times.Count];

            Buffer.BlockCopy(PoseStream.IndexMagic, 0, bytes, 0, 8);
            Put16(bytes, 8, (ushort)PoseStream.Version);
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
    /// not hold. That stop is how a torn last write is dropped.
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

        /// <summary>Frame <c>i</c>, read whole.</summary>
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
            int at = 12;

            var bodies = new PoseBody[count];

            for (long b = 0; b < count; b++)
            {
                var body = new PoseBody
                {
                    Id = unchecked((int)Get.U32(payload, at)),
                    X = Get.Float(payload, at + 4),
                    Y = Get.Float(payload, at + 8),
                    Z = Get.Float(payload, at + 12),
                    Qx = Get.Float(payload, at + 16),
                    Qy = Get.Float(payload, at + 20),
                    Qz = Get.Float(payload, at + 24),
                    Qw = Get.Float(payload, at + 28),
                    BodyFraction = Get.Float(payload, at + 32),
                };

                int dof = payload[at + 36];
                at += PoseStream.BodyFixedBytes;

                var joints = new float[dof];
                for (int d = 0; d < dof; d++) joints[d] = Get.Float(payload, at + 4 * d);
                at += 4 * dof;

                body.Joints = joints;
                bodies[b] = body;
            }

            frame.Bodies = bodies;
            return frame;
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

            if (version != PoseStream.Version)
            {
                throw new InvalidDataException(
                    "The stream is version " + version + " and this build reads version " +
                    PoseStream.Version + ". A stream is refused rather than read with a field " +
                    "guessed at, under the rule the config reader follows.");
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

            if (Get.U16(bytes, 8) != PoseStream.Version) return null;
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

            if (payload < 12 ||
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
                if (payload < 12 || at + 8 + payload + 4 > _file.Length) break;

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
