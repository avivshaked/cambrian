using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Evosim.Core;

namespace Evosim.Farm
{
    /// <summary>
    /// The complete state of a world at one simulated second: <c>checkpoints/NNNNNNNNN.ckpt</c>
    /// inside a run directory (<c>logbook/specs/checkpoint-spec.md</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What it is for.</b> Two things: a killed run resumes from the last one rather than from
    /// nothing, and the theatre can one day take a recorded second and carry the world on live
    /// from it rather than replaying a recording. Both need the same thing — every number the
    /// next step reads — and a state stream, a snapshot and a positions row are each a picture
    /// of part of it.
    /// </para>
    /// <para>
    /// <b>A recording, not a world rule.</b> The cadence is <c>EVOSIM_CHECKPOINT_EVERY</c> in
    /// simulated seconds, default 0, which writes no directory at all. It is bound beside
    /// <c>EVOSIM_POSE_EVERY</c> and <c>EVOSIM_REPORT_EVERY</c>, reaches <see cref="EnvSettings"/>
    /// and never <see cref="RunConfig"/>, so no config hash moves.
    /// </para>
    /// <para>
    /// <b>The file is written whole or not at all.</b> The payload is built in memory, written to
    /// a neighbouring temporary file and moved over the final name, so a reader never catches
    /// half a document; and the file carries the payload's length twice and a digest of it
    /// between, so a copy truncated by a full disk or a killed <c>cp</c> is refused rather than
    /// read as a world with four billion creatures in it. That is the state stream's trailer
    /// rule with a digest added, because a checkpoint is read once and acted on where a frame is
    /// one of sixty thousand.
    /// </para>
    /// </remarks>
    public static class Checkpoint
    {
        /// <summary>The directory checkpoints live in, inside a run directory.</summary>
        public const string DirectoryName = "checkpoints";

        /// <summary>The format this build writes and the only one it reads.</summary>
        /// <remarks>
        /// 2 since 2026-09-22, with <c>World.StateVersion</c>: a creature's reserve, tissue and
        /// adult tissue are doubles in the payload now. The world's own version would catch it,
        /// and this one is bumped beside it so the refusal happens at the file's header rather
        /// than a few hundred kilobytes in.
        /// </remarks>
        /// <remarks>
        /// 3 with the refusal split (2026-09-22 night, <c>World.StateVersion</c> 5): the row's
        /// baselines carry two more counters, so a version-2 payload would put the first mouth
        /// baseline where the split's second is read. Round 45's checkpoints are version 2.
        /// </remarks>
        public const int Version = 3;

        /// <summary>Bytes before the header's own fields.</summary>
        public const int MagicBytes = 12;

        internal static readonly byte[] FileMagic =
        {
            (byte)'E', (byte)'V', (byte)'O', (byte)'C', (byte)'K', (byte)'P', (byte)'T', 0,
        };

        /// <summary>The checkpoints directory inside a run directory.</summary>
        public static string DirectoryIn(string runDirectory) =>
            Path.Combine(runDirectory, DirectoryName);

        /// <summary>Where a checkpoint at a simulated second is written.</summary>
        /// <remarks>
        /// Zero-padded to nine digits so a directory listing sorts chronologically, which is the
        /// snapshot naming rule and the same reason for it.
        /// </remarks>
        public static string PathFor(string runDirectory, double simulatedSeconds) =>
            Path.Combine(
                DirectoryIn(runDirectory),
                string.Format(CultureInfo.InvariantCulture, "{0:000000000}.ckpt", (long)simulatedSeconds));

        /// <summary>Every checkpoint in a run directory, ascending by the second in its name.</summary>
        public static string[] In(string runDirectory)
        {
            string directory = DirectoryIn(runDirectory);
            if (!Directory.Exists(directory)) return Array.Empty<string>();

            string[] files = Directory.GetFiles(directory, "*.ckpt");
            Array.Sort(files, StringComparer.Ordinal);
            return files;
        }

        /// <summary>The last checkpoint in a run directory, or null when there is none.</summary>
        public static string Latest(string runDirectory)
        {
            string[] files = In(runDirectory);
            return files.Length == 0 ? null : files[files.Length - 1];
        }

        /// <summary>
        /// The checkpoint at a second, or the closest one at or before it.
        /// </summary>
        /// <remarks>
        /// At or before rather than nearest: a resume from a second the run never checkpointed
        /// has to land on a world that existed, and the one after it is a world the resumed run
        /// has not simulated yet.
        /// </remarks>
        public static string At(string runDirectory, double seconds)
        {
            string best = null;

            foreach (string path in In(runDirectory))
            {
                if (SecondsInName(path) > seconds + 1e-6) break;
                best = path;
            }

            return best;
        }

        /// <summary>The second a checkpoint's file name says it stands at.</summary>
        public static double SecondsInName(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            return double.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out double s)
                ? s
                : double.NaN;
        }

        /// <summary>
        /// Turns what a launcher said into the one file a resume will read.
        /// </summary>
        /// <param name="what">
        /// A <c>.ckpt</c> file, a run directory holding a <c>checkpoints/</c>, or an arm
        /// directory holding run directories.
        /// </param>
        /// <param name="atSeconds">
        /// The second to start from, or 0 for the last checkpoint there is. A second the run
        /// never wrote a checkpoint at resolves to the last one at or before it, and the run
        /// says which second it actually found.
        /// </param>
        /// <remarks>
        /// An arm directory is resolved to its newest run directory that holds a checkpoint. Run
        /// directories are named for the instant they started and their settings hash, so newest
        /// is the last in ordinal order; an arm with several runs in it is the case a resume most
        /// often means, and picking the one that was interrupted is what a reader expects. The
        /// choice is printed rather than made silently.
        /// </remarks>
        public static string Resolve(string what, double atSeconds)
        {
            if (string.IsNullOrEmpty(what)) return null;

            if (File.Exists(what) && what.EndsWith(".ckpt", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(what);
            }

            if (!Directory.Exists(what))
            {
                throw new DirectoryNotFoundException(
                    "EVOSIM_RESUME names '" + what + "', which is neither a .ckpt file nor a " +
                    "directory. It wants a checkpoint, a run directory or an arm directory.");
            }

            string runDirectory = Path.GetFullPath(what);

            if (!Directory.Exists(DirectoryIn(runDirectory)))
            {
                string[] candidates = Directory.GetDirectories(runDirectory);
                Array.Sort(candidates, StringComparer.Ordinal);

                string found = null;
                for (int i = candidates.Length - 1; i >= 0; i--)
                {
                    if (Latest(candidates[i]) == null) continue;
                    found = candidates[i];
                    break;
                }

                if (found == null)
                {
                    throw new DirectoryNotFoundException(
                        "No run under '" + what + "' holds a " + DirectoryName + "/ directory. " +
                        "A run records one only when EVOSIM_CHECKPOINT_EVERY was set above 0.");
                }

                runDirectory = found;
            }

            string path = atSeconds > 0f ? At(runDirectory, atSeconds) : Latest(runDirectory);

            if (path == null)
            {
                throw new FileNotFoundException(
                    atSeconds > 0f
                        ? "No checkpoint at or before " +
                          atSeconds.ToString("0.#", CultureInfo.InvariantCulture) + " s in '" +
                          runDirectory + "'. The earliest is " +
                          SecondsInName(In(runDirectory)[0]).ToString("0.#", CultureInfo.InvariantCulture) + " s."
                        : "No checkpoint in '" + runDirectory + "'.");
            }

            return path;
        }

        internal static ulong Digest(byte[] bytes, int count)
        {
            ulong hash = 14695981039346656037UL;

            for (int i = 0; i < count; i++)
            {
                hash ^= bytes[i];
                hash *= 1099511628211UL;
            }

            return hash;
        }
    }

    /// <summary>What a checkpoint says about the run that wrote it, before its payload.</summary>
    public sealed class CheckpointHeader
    {
        public int Version;

        /// <summary>The simulated second the world stands at.</summary>
        public double Seconds;

        /// <summary>The world's seed. Every per-creature seed derives from it.</summary>
        public ulong Seed;

        /// <summary>Physics steps taken.</summary>
        public long PhysicsSteps;

        public float PhysicsStepSeconds;
        public int StepsPerMetabolicStep;

        /// <summary>The run's <c>configHash</c>: what world this is.</summary>
        public string ConfigHash = string.Empty;

        /// <summary>The three source digests <c>run.json</c> carries: what build wrote it.</summary>
        public string CoreHash = string.Empty;

        public string DynamicsHash = string.Empty;
        public string FarmHash = string.Empty;
        public string EngineVersion = string.Empty;

        /// <summary>The arm and the run directory it was written in, for the resumed manifest.</summary>
        public string SourceArm = string.Empty;

        public string SourceRun = string.Empty;

        /// <summary>
        /// The four recording cadences the run was writing at: report rows, state-stream frames,
        /// digests and checkpoints.
        /// </summary>
        /// <remarks>
        /// They are here because a resume continues a run rather than starting a new one, and a
        /// continuation whose rows fall on other seconds is not comparable with what came before
        /// it. They are not world settings, so they are in the header and never in the config;
        /// a launcher that names one of them by hand still wins, which is what
        /// <see cref="EnvSettings.Provided"/> is for.
        /// </remarks>
        public int ReportEvery;

        public double PoseEverySeconds;
        public long DigestEverySteps;
        public double CheckpointEverySeconds;

        /// <summary>Bytes of state after the header.</summary>
        public long PayloadBytes;

        /// <summary>FNV-1a over those bytes.</summary>
        public ulong PayloadDigest;

        /// <summary>
        /// Whether this checkpoint was written by the build now reading it.
        /// </summary>
        /// <remarks>
        /// All four, because all four decide what a step does: the config is the world, Core is
        /// the economy and the development, Dynamics is the solver and the farm is the loop
        /// around them. A checkpoint from another build may still load — the layout has a version
        /// of its own — and what it produces from the first step on is a cousin of the recording
        /// rather than its continuation, which is the theatre's own word for the same thing.
        /// </remarks>
        public bool Matches(string configHash, string coreHash, string dynamicsHash, string farmHash) =>
            Same(ConfigHash, configHash) && Same(CoreHash, coreHash) &&
            Same(DynamicsHash, dynamicsHash) && Same(FarmHash, farmHash);

        /// <summary>Which of the four differ, for a refusal that says what is wrong.</summary>
        public IReadOnlyList<string> Differences(
            string configHash, string coreHash, string dynamicsHash, string farmHash)
        {
            var differences = new List<string>();

            if (!Same(ConfigHash, configHash)) differences.Add(Line("configHash", ConfigHash, configHash));
            if (!Same(CoreHash, coreHash)) differences.Add(Line("coreHash", CoreHash, coreHash));
            if (!Same(DynamicsHash, dynamicsHash)) differences.Add(Line("dynamicsHash", DynamicsHash, dynamicsHash));
            if (!Same(FarmHash, farmHash)) differences.Add(Line("farmHash", FarmHash, farmHash));

            return differences;
        }

        private static bool Same(string a, string b) =>
            string.Equals(a ?? string.Empty, b ?? string.Empty, StringComparison.Ordinal);

        private static string Line(string name, string recorded, string running) =>
            name + ": checkpoint " + Short(recorded) + ", this build " + Short(running);

        private static string Short(string hash) =>
            string.IsNullOrEmpty(hash) ? "(none)" : hash.Length > 12 ? hash.Substring(0, 12) + "…" : hash;
    }

    /// <summary>
    /// The writer: a header, the payload whole, and the payload's length and digest after it.
    /// </summary>
    public static class CheckpointWriter
    {
        /// <summary>
        /// Writes one checkpoint. The payload is built in memory first, so the file on disk is
        /// never a partial document and the header can carry the payload's own length.
        /// </summary>
        /// <returns>Bytes written.</returns>
        public static long Write(string path, CheckpointHeader header, Action<BinaryWriter> payload)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            if (header == null) throw new ArgumentNullException(nameof(header));
            if (payload == null) throw new ArgumentNullException(nameof(payload));

            byte[] bytes;
            int count;

            using (var buffer = new MemoryStream(1 << 20))
            {
                using (var w = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
                {
                    payload(w);
                    w.Flush();
                }

                bytes = buffer.GetBuffer();
                count = (int)buffer.Length;
            }

            header.PayloadBytes = count;
            header.PayloadDigest = Checkpoint.Digest(bytes, count);

            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            string temporary = path + ".tmp";

            using (var file = new FileStream(
                       temporary, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16))
            using (var w = new BinaryWriter(file, System.Text.Encoding.UTF8))
            {
                w.Write(Checkpoint.FileMagic);
                w.Write((ushort)Checkpoint.Version);
                w.Write((ushort)Checkpoint.MagicBytes);

                w.Write(header.Seconds);
                w.Write(header.Seed);
                w.Write(header.PhysicsSteps);
                w.Write(header.PhysicsStepSeconds);
                w.Write(header.StepsPerMetabolicStep);

                w.Write(header.ConfigHash ?? string.Empty);
                w.Write(header.CoreHash ?? string.Empty);
                w.Write(header.DynamicsHash ?? string.Empty);
                w.Write(header.FarmHash ?? string.Empty);
                w.Write(header.EngineVersion ?? string.Empty);
                w.Write(header.SourceArm ?? string.Empty);
                w.Write(header.SourceRun ?? string.Empty);

                w.Write(header.ReportEvery);
                w.Write(header.PoseEverySeconds);
                w.Write(header.DigestEverySteps);
                w.Write(header.CheckpointEverySeconds);

                w.Write(header.PayloadBytes);
                w.Write(header.PayloadDigest);

                w.Write(bytes, 0, count);

                // The trailer: the length again, then the magic again. Either alone would catch a
                // truncation; both together also catch a file that was overwritten from the front
                // by something else of the same length.
                w.Write(header.PayloadBytes);
                w.Write(Checkpoint.FileMagic);
            }

            // Moved over the final name rather than written at it, so nothing ever opens a
            // checkpoint that is still being written — the manifest's own protocol.
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);

            return new FileInfo(path).Length;
        }
    }

    /// <summary>
    /// The reader: the header, then the payload, with the length, the digest and both magics
    /// checked before a single field of state is handed out.
    /// </summary>
    public sealed class CheckpointReader : IDisposable
    {
        private MemoryStream _payload;

        /// <summary>What the file's header says.</summary>
        public CheckpointHeader Header { get; private set; }

        /// <summary>The payload, positioned at its first byte.</summary>
        public BinaryReader Reader { get; private set; }

        private CheckpointReader() { }

        /// <summary>
        /// Opens a checkpoint. Refuses a file this build does not know, and a file the disk
        /// did not finish.
        /// </summary>
        public static CheckpointReader Open(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            byte[] all = File.ReadAllBytes(path);

            if (all.Length < Checkpoint.MagicBytes + 8)
            {
                throw new InvalidDataException(
                    "A checkpoint is at least a header and this file is " + all.Length +
                    " bytes. It was not finished.");
            }

            for (int i = 0; i < Checkpoint.FileMagic.Length; i++)
            {
                if (all[i] == Checkpoint.FileMagic[i]) continue;

                throw new InvalidDataException(
                    "This is not a checkpoint: its first eight bytes are not EVOCKPT and a zero.");
            }

            var stream = new MemoryStream(all, writable: false);
            var r = new BinaryReader(stream, System.Text.Encoding.UTF8);

            r.ReadBytes(8);
            int version = r.ReadUInt16();
            int magicBytes = r.ReadUInt16();

            if (version != Checkpoint.Version)
            {
                throw new InvalidDataException(
                    "The checkpoint is version " + version + " and this build reads version " +
                    Checkpoint.Version + ". A checkpoint is refused rather than read with a " +
                    "field guessed at, under the rule the config reader follows.");
            }

            if (magicBytes != Checkpoint.MagicBytes)
            {
                throw new InvalidDataException(
                    "The checkpoint's magic block says it is " + magicBytes + " bytes and " +
                    "version " + version + "'s is " + Checkpoint.MagicBytes + ".");
            }

            CheckpointHeader header;

            // The header's own strings are length-prefixed, so a file cut off inside them runs the
            // reader off the end. That is a truncation like any other and is reported as one: an
            // end-of-stream from deep inside a reader says nothing a person can act on.
            try
            {
                header = new CheckpointHeader
                {
                    Version = version,
                    Seconds = r.ReadDouble(),
                    Seed = r.ReadUInt64(),
                    PhysicsSteps = r.ReadInt64(),
                    PhysicsStepSeconds = r.ReadSingle(),
                    StepsPerMetabolicStep = r.ReadInt32(),
                    ConfigHash = r.ReadString(),
                    CoreHash = r.ReadString(),
                    DynamicsHash = r.ReadString(),
                    FarmHash = r.ReadString(),
                    EngineVersion = r.ReadString(),
                    SourceArm = r.ReadString(),
                    SourceRun = r.ReadString(),
                    ReportEvery = r.ReadInt32(),
                    PoseEverySeconds = r.ReadDouble(),
                    DigestEverySteps = r.ReadInt64(),
                    CheckpointEverySeconds = r.ReadDouble(),
                };

                header.PayloadBytes = r.ReadInt64();
                header.PayloadDigest = r.ReadUInt64();
            }
            catch (Exception e) when (e is EndOfStreamException || e is IOException ||
                                      e is ArgumentException || e is FormatException)
            {
                stream.Dispose();

                throw new InvalidDataException(
                    "The checkpoint's header ends before it is finished, in " + all.Length +
                    " bytes. The file was truncated.", e);
            }

            long payloadStart = stream.Position;

            if (header.PayloadBytes < 0 ||
                payloadStart + header.PayloadBytes + 8 + Checkpoint.FileMagic.Length > all.Length)
            {
                stream.Dispose();

                throw new InvalidDataException(
                    "The checkpoint's header promises " + header.PayloadBytes + " bytes of state " +
                    "and the file holds " + (all.Length - payloadStart) + " after the header. It " +
                    "was truncated, and a truncated checkpoint is refused rather than read as a " +
                    "world that stops halfway through its population.");
            }

            long trailerAt = payloadStart + header.PayloadBytes;
            stream.Position = trailerAt;

            long repeated = r.ReadInt64();
            if (repeated != header.PayloadBytes)
            {
                stream.Dispose();

                throw new InvalidDataException(
                    "The checkpoint's trailer says " + repeated + " bytes of state and its header " +
                    "says " + header.PayloadBytes + ". The two disagree, so the file is not the " +
                    "one that was written.");
            }

            for (int i = 0; i < Checkpoint.FileMagic.Length; i++)
            {
                if (r.ReadByte() == Checkpoint.FileMagic[i]) continue;

                stream.Dispose();

                throw new InvalidDataException(
                    "The checkpoint's closing magic is missing: the file was not finished.");
            }

            ulong digest = Checkpoint.Digest(
                Slice(all, (int)payloadStart, (int)header.PayloadBytes), (int)header.PayloadBytes);

            if (digest != header.PayloadDigest)
            {
                stream.Dispose();

                throw new InvalidDataException(
                    "The checkpoint's state does not hash to what its header says it should. " +
                    "Some of the bytes changed between the write and this read.");
            }

            stream.Position = payloadStart;

            return new CheckpointReader { Header = header, _payload = stream, Reader = r };
        }

        private static byte[] Slice(byte[] source, int at, int count)
        {
            var slice = new byte[count];
            Buffer.BlockCopy(source, at, slice, 0, count);
            return slice;
        }

        /// <summary>Reads a checkpoint's header alone, for a tool that only wants to look.</summary>
        public static CheckpointHeader ReadHeader(string path)
        {
            using (CheckpointReader reader = Open(path)) return reader.Header;
        }

        public void Dispose()
        {
            Reader?.Dispose();
            _payload?.Dispose();
            Reader = null;
            _payload = null;
        }
    }
}
