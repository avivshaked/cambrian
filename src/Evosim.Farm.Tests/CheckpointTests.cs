using System;
using System.IO;
using System.Text;
using Evosim.Core;
using Xunit;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// The checkpoint file's own acceptance: a header that survives a round trip, a file the disk
    /// did not finish refused rather than read, and a build change named rather than silently
    /// carried (<c>logbook/specs/checkpoint-spec.md</c>).
    /// </summary>
    /// <remarks>
    /// The world's own state is not exercised here. What a checkpoint carries is tested where it
    /// lives, in Core's and Dynamics' round trips, and whether the whole of it is enough is the
    /// acceptance run the spec describes, which is a farm run and not a unit test. What is left
    /// for this file is the container: the layout, the guards and the refusals.
    /// </remarks>
    public class CheckpointTests : IDisposable
    {
        private readonly string _directory;

        public CheckpointTests()
        {
            _directory = Path.Combine(
                AppContext.BaseDirectory, "test-out", Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(_directory);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
            }
            catch (IOException)
            {
            }
        }

        private static CheckpointHeader Header() => new CheckpointHeader
        {
            Version = Checkpoint.Version,
            Seconds = 1234.5,
            Seed = 9876543210123456789UL,
            PhysicsSteps = 246913L,
            PhysicsStepSeconds = 0.01f,
            StepsPerMetabolicStep = 50,
            ConfigHash = "ff557bce11223344",
            CoreHash = "aaaabbbbccccdddd",
            DynamicsHash = "0123456789abcdef",
            FarmHash = "fedcba9876543210",
            EngineVersion = "dynamics 1",
            SourceArm = "ckA",
            SourceRun = "20260922-1200-ff557bce",
        };

        private static void Payload(BinaryWriter w)
        {
            StateIo.Tag(w, "PAYL");
            w.Write(3.14159265358979);
            w.Write(-0.1f);
            w.Write(42L);
            StateIo.Tag(w, "PEND");
        }

        private string Write(CheckpointHeader header)
        {
            string path = Checkpoint.PathFor(_directory, header.Seconds);
            CheckpointWriter.Write(path, header, Payload);
            return path;
        }

        // ------------------------------------------------------------------ the round trip

        [Fact]
        public void HeaderSurvivesTheRoundTrip()
        {
            CheckpointHeader written = Header();
            string path = Write(written);

            CheckpointHeader read = CheckpointReader.ReadHeader(path);

            Assert.Equal(Checkpoint.Version, read.Version);
            Assert.Equal(written.Seconds, read.Seconds);
            Assert.Equal(written.Seed, read.Seed);
            Assert.Equal(written.PhysicsSteps, read.PhysicsSteps);
            Assert.Equal(written.PhysicsStepSeconds, read.PhysicsStepSeconds);
            Assert.Equal(written.StepsPerMetabolicStep, read.StepsPerMetabolicStep);
            Assert.Equal(written.ConfigHash, read.ConfigHash);
            Assert.Equal(written.CoreHash, read.CoreHash);
            Assert.Equal(written.DynamicsHash, read.DynamicsHash);
            Assert.Equal(written.FarmHash, read.FarmHash);
            Assert.Equal(written.EngineVersion, read.EngineVersion);
            Assert.Equal(written.SourceArm, read.SourceArm);
            Assert.Equal(written.SourceRun, read.SourceRun);
            Assert.Equal(written.PayloadBytes, read.PayloadBytes);
            Assert.Equal(written.PayloadDigest, read.PayloadDigest);
        }

        [Fact]
        public void PayloadComesBackExactly()
        {
            string path = Write(Header());

            using (CheckpointReader reader = CheckpointReader.Open(path))
            {
                BinaryReader r = reader.Reader;

                StateIo.Tag(r, "PAYL");
                Assert.Equal(3.14159265358979, r.ReadDouble());
                Assert.Equal(-0.1f, r.ReadSingle());
                Assert.Equal(42L, r.ReadInt64());
                StateIo.Tag(r, "PEND");
            }
        }

        [Fact]
        public void TheNameIsTheSecondZeroPadded()
        {
            string path = Checkpoint.PathFor(_directory, 400.0);

            Assert.Equal("000000400.ckpt", Path.GetFileName(path));
            Assert.Equal(400d, Checkpoint.SecondsInName(path));
        }

        [Fact]
        public void NothingIsLeftBehindWhenTheWriteFinishes()
        {
            string path = Write(Header());

            Assert.True(File.Exists(path));
            Assert.False(File.Exists(path + ".tmp"));
        }

        // ------------------------------------------------------------------ the refusals

        [Theory]
        [InlineData(1)]
        [InlineData(9)]
        [InlineData(64)]
        public void ATruncatedCheckpointIsRefused(int cut)
        {
            string path = Write(Header());

            byte[] all = File.ReadAllBytes(path);
            byte[] torn = new byte[all.Length - cut];
            Buffer.BlockCopy(all, 0, torn, 0, torn.Length);
            File.WriteAllBytes(path, torn);

            // Cleanly: an InvalidDataException that says what is wrong, never an
            // EndOfStreamException from a reader that believed the header's length, and never a
            // world with four billion creatures in it.
            var thrown = Assert.Throws<InvalidDataException>(() => CheckpointReader.Open(path));
            Assert.False(string.IsNullOrWhiteSpace(thrown.Message));
        }

        [Fact]
        public void AnEmptyFileIsRefused()
        {
            string path = Checkpoint.PathFor(_directory, 0);
            Directory.CreateDirectory(Checkpoint.DirectoryIn(_directory));
            File.WriteAllBytes(path, Array.Empty<byte>());

            Assert.Throws<InvalidDataException>(() => CheckpointReader.Open(path));
        }

        [Fact]
        public void SomethingElseEntirelyIsRefused()
        {
            string path = Checkpoint.PathFor(_directory, 0);
            Directory.CreateDirectory(Checkpoint.DirectoryIn(_directory));
            File.WriteAllBytes(path, Encoding.UTF8.GetBytes(new string('x', 512)));

            Assert.Throws<InvalidDataException>(() => CheckpointReader.Open(path));
        }

        [Fact]
        public void AByteChangedInsideThePayloadIsRefused()
        {
            string path = Write(Header());

            byte[] all = File.ReadAllBytes(path);
            all[all.Length - 32] ^= 0x01;
            File.WriteAllBytes(path, all);

            Assert.Throws<InvalidDataException>(() => CheckpointReader.Open(path));
        }

        [Fact]
        public void AnotherVersionIsRefused()
        {
            string path = Write(Header());

            byte[] all = File.ReadAllBytes(path);
            all[8] = (byte)(Checkpoint.Version + 1);
            File.WriteAllBytes(path, all);

            InvalidDataException thrown =
                Assert.Throws<InvalidDataException>(() => CheckpointReader.Open(path));

            Assert.Contains("version", thrown.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ------------------------------------------------------------------ versions 4, 5 and 6

        /// <summary>A payload the size and repetitiveness of a world's, in miniature.</summary>
        private static void LargePayload(BinaryWriter w)
        {
            StateIo.Tag(w, "PAYL");
            for (int i = 0; i < 20000; i++)
            {
                w.Write((double)(i % 97) * 0.125);
                w.Write(i);
            }
            StateIo.Tag(w, "PEND");
        }

        private static void CheckLargePayload(BinaryReader r)
        {
            StateIo.Tag(r, "PAYL");
            for (int i = 0; i < 20000; i++)
            {
                Assert.Equal((double)(i % 97) * 0.125, r.ReadDouble());
                Assert.Equal(i, r.ReadInt32());
            }
            StateIo.Tag(r, "PEND");
        }

        /// <summary>FNV-1a over a payload, written here from the spec rather than borrowed.</summary>
        private static ulong Fnv1a(byte[] bytes)
        {
            ulong hash = 14695981039346656037UL;
            foreach (byte b in bytes)
            {
                hash ^= b;
                hash *= 1099511628211UL;
            }
            return hash;
        }

        /// <summary>
        /// A version-4 file, laid out by hand as round 48's build wrote it: the header, the
        /// payload's length and digest, the payload as it is, and the trailer. This build's writer
        /// writes version 6 only, so a test of the lossy reader makes its own file.
        /// </summary>
        private static string WriteVersionFour(string path, CheckpointHeader header, Action<BinaryWriter> payload)
        {
            byte[] body;
            using (var buffer = new MemoryStream())
            {
                using (var w = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
                {
                    payload(w);
                }
                body = buffer.ToArray();
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            using (var file = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var w = new BinaryWriter(file, Encoding.UTF8))
            {
                w.Write(Encoding.ASCII.GetBytes("EVOCKPT"));
                w.Write((byte)0);
                w.Write((ushort)4);
                w.Write((ushort)12);
                w.Write(header.Seconds);
                w.Write(header.Seed);
                w.Write(header.PhysicsSteps);
                w.Write(header.PhysicsStepSeconds);
                w.Write(header.StepsPerMetabolicStep);
                w.Write(header.ConfigHash);
                w.Write(header.CoreHash);
                w.Write(header.DynamicsHash);
                w.Write(header.FarmHash);
                w.Write(header.EngineVersion);
                w.Write(header.SourceArm);
                w.Write(header.SourceRun);
                w.Write(header.ReportEvery);
                w.Write(header.PoseEverySeconds);
                w.Write(header.DigestEverySteps);
                w.Write(header.CheckpointEverySeconds);
                w.Write((long)body.Length);
                w.Write(Fnv1a(body));
                w.Write(body);
                w.Write((long)body.Length);
                w.Write(Encoding.ASCII.GetBytes("EVOCKPT"));
                w.Write((byte)0);
            }

            return path;
        }

        [Fact]
        public void VersionSixStoresThePayloadGzippedAndDigestsItAsWritten()
        {
            CheckpointHeader written = Header();
            written.Version = Checkpoint.Version;

            string path = Checkpoint.PathFor(_directory, written.Seconds);
            CheckpointWriter.Write(path, written, LargePayload);

            byte[] all = File.ReadAllBytes(path);
            Assert.Equal(6, all[8]);
            Assert.True(written.StoredBytes < written.PayloadBytes, "the stored payload is not smaller");
            Assert.True(all.Length < written.PayloadBytes, "the file is not smaller than its payload");

            using (CheckpointReader reader = CheckpointReader.Open(path))
            {
                Assert.Equal(6, reader.Header.Version);
                Assert.False(reader.Header.ReadLossily);
                Assert.Equal(written.PayloadBytes, reader.Header.PayloadBytes);
                Assert.Equal(written.StoredBytes, reader.Header.StoredBytes);
                Assert.Equal(written.PayloadDigest, reader.Header.PayloadDigest);

                CheckLargePayload(reader.Reader);
            }
        }

        /// <summary>
        /// A header that asks for no version gets this build's, and one that asks for the lossy
        /// version, the refused one or any other is refused at the write: the payload is this
        /// build's layout, and under another number a reader would take it for another.
        /// </summary>
        [Theory]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(7)]
        public void OnlyVersionSixIsWritten(int version)
        {
            CheckpointHeader unnamed = Header();
            unnamed.Version = 0;
            CheckpointWriter.Write(Path.Combine(_directory, "unnamed.ckpt"), unnamed, Payload);
            Assert.Equal(Checkpoint.Version, unnamed.Version);

            CheckpointHeader written = Header();
            written.Version = version;

            Assert.Throws<ArgumentException>(() => CheckpointWriter.Write(
                Checkpoint.PathFor(_directory, written.Seconds), written, Payload));
        }

        /// <summary>
        /// Round 48's version 4, laid out by hand, is read with its payload as it is, digested the
        /// same as version 6's, and named as read lossily, so that a resume refuses it unless told
        /// to take a cousin and the theatre labels it; the version before it is refused.
        /// </summary>
        [Fact]
        public void VersionFourIsReadLossilyAndTheOneBeforeItIsRefused()
        {
            CheckpointHeader six = Header();
            CheckpointWriter.Write(Path.Combine(_directory, "six.ckpt"), six, LargePayload);

            string path = WriteVersionFour(Path.Combine(_directory, "four.ckpt"), Header(), LargePayload);

            using (CheckpointReader reader = CheckpointReader.Open(path))
            {
                CheckpointHeader h = reader.Header;

                Assert.Equal(Checkpoint.LossyVersion, h.Version);
                Assert.True(h.ReadLossily);
                Assert.Equal(h.PayloadBytes, h.StoredBytes);
                Assert.Equal(six.PayloadBytes, h.PayloadBytes);
                Assert.Equal(six.PayloadDigest, h.PayloadDigest);
                Assert.False(h.Matches(h.ConfigHash, h.CoreHash, h.DynamicsHash, h.FarmHash));
                Assert.StartsWith(
                    "checkpointVersion:",
                    Assert.Single(h.Differences(h.ConfigHash, h.CoreHash, h.DynamicsHash, h.FarmHash)));

                CheckLargePayload(reader.Reader);
            }

            byte[] all = File.ReadAllBytes(path);
            all[8] = (byte)(Checkpoint.LossyVersion - 1);
            File.WriteAllBytes(path, all);

            InvalidDataException thrown =
                Assert.Throws<InvalidDataException>(() => CheckpointReader.Open(path));

            Assert.Contains("version", thrown.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Version 5 was two layouts on two branches and no run kept one, so a file that says 5
        /// is refused by name, whichever of the two it holds: the payload stored as it is, or
        /// stored gzipped behind a stored length.
        /// </summary>
        [Fact]
        public void VersionFiveIsRefusedWhicheverLayoutItHolds()
        {
            string plain = WriteVersionFour(Path.Combine(_directory, "plain.ckpt"), Header(), Payload);
            byte[] all = File.ReadAllBytes(plain);
            all[8] = (byte)Checkpoint.RefusedVersion;
            File.WriteAllBytes(plain, all);

            InvalidDataException thrown =
                Assert.Throws<InvalidDataException>(() => CheckpointReader.Open(plain));
            Assert.Contains("version 5", thrown.Message, StringComparison.Ordinal);

            string gzipped = Path.Combine(_directory, "gzipped.ckpt");
            CheckpointWriter.Write(gzipped, Header(), Payload);
            all = File.ReadAllBytes(gzipped);
            all[8] = (byte)Checkpoint.RefusedVersion;
            File.WriteAllBytes(gzipped, all);

            thrown = Assert.Throws<InvalidDataException>(() => CheckpointReader.Open(gzipped));
            Assert.Contains("version 5", thrown.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ADamagedCompressedPayloadIsRefused()
        {
            CheckpointHeader written = Header();
            written.Version = Checkpoint.Version;

            string path = Checkpoint.PathFor(_directory, written.Seconds);
            CheckpointWriter.Write(path, written, LargePayload);

            byte[] all = File.ReadAllBytes(path);

            // The middle of the stored member: after the header, before the trailer's 16 bytes.
            long storedStart = all.Length - 16 - written.StoredBytes;
            all[storedStart + written.StoredBytes / 2] ^= 0x21;
            File.WriteAllBytes(path, all);

            Assert.Throws<InvalidDataException>(() => CheckpointReader.Open(path));
        }

        // ------------------------------------------------------------------ the four hashes

        [Fact]
        public void TheSameBuildMatches()
        {
            CheckpointHeader h = Header();

            Assert.True(h.Matches(h.ConfigHash, h.CoreHash, h.DynamicsHash, h.FarmHash));
            Assert.Empty(h.Differences(h.ConfigHash, h.CoreHash, h.DynamicsHash, h.FarmHash));
        }

        [Fact]
        public void EachOfTheFourIsNamedWhenItDiffers()
        {
            CheckpointHeader h = Header();

            Assert.Contains(
                "configHash",
                Assert.Single(h.Differences("other", h.CoreHash, h.DynamicsHash, h.FarmHash)));

            Assert.Contains(
                "coreHash",
                Assert.Single(h.Differences(h.ConfigHash, "other", h.DynamicsHash, h.FarmHash)));

            Assert.Contains(
                "dynamicsHash",
                Assert.Single(h.Differences(h.ConfigHash, h.CoreHash, "other", h.FarmHash)));

            Assert.Contains(
                "farmHash",
                Assert.Single(h.Differences(h.ConfigHash, h.CoreHash, h.DynamicsHash, "other")));

            Assert.Equal(4, h.Differences("a", "b", "c", "d").Count);
            Assert.False(h.Matches("a", "b", "c", "d"));
        }

        // ------------------------------------------------------------------ finding one

        [Fact]
        public void ResolveTakesTheLastCheckpointOrTheOneAtOrBeforeASecond()
        {
            foreach (double seconds in new[] { 200d, 400d, 600d })
            {
                CheckpointHeader h = Header();
                h.Seconds = seconds;
                Write(h);
            }

            Assert.Equal(3, Checkpoint.In(_directory).Length);

            Assert.Equal(600d, Checkpoint.SecondsInName(Checkpoint.Latest(_directory)));
            Assert.Equal(600d, Checkpoint.SecondsInName(Checkpoint.Resolve(_directory, 0)));
            Assert.Equal(400d, Checkpoint.SecondsInName(Checkpoint.Resolve(_directory, 400)));

            // At or before, never after: a second the run never checkpointed has to land on a
            // world that existed, and the one after it has not been simulated yet.
            Assert.Equal(400d, Checkpoint.SecondsInName(Checkpoint.Resolve(_directory, 599)));

            Assert.Throws<FileNotFoundException>(() => Checkpoint.Resolve(_directory, 100));
        }

        [Fact]
        public void ResolveWalksAnArmDirectoryToItsNewestRun()
        {
            string arm = Path.Combine(_directory, "arm");
            string older = Path.Combine(arm, "20260922-1000-aaaa");
            string newer = Path.Combine(arm, "20260922-1400-bbbb");

            foreach (string run in new[] { older, newer })
            {
                CheckpointHeader h = Header();
                h.Seconds = 200;
                CheckpointWriter.Write(Checkpoint.PathFor(run, h.Seconds), h, Payload);
            }

            string found = Checkpoint.Resolve(arm, 0);

            Assert.StartsWith(Path.GetFullPath(newer), Path.GetFullPath(found), StringComparison.Ordinal);
        }

        [Fact]
        public void ResolveRefusesWhatItCannotFind()
        {
            Assert.Null(Checkpoint.Resolve(null, 0));
            Assert.Null(Checkpoint.Resolve("", 0));

            Assert.Throws<DirectoryNotFoundException>(
                () => Checkpoint.Resolve(Path.Combine(_directory, "not-here"), 0));

            // A directory with no checkpoints anywhere under it says so, rather than resolving to
            // nothing and founding a world the launcher did not ask for.
            string empty = Path.Combine(_directory, "empty");
            Directory.CreateDirectory(empty);
            Assert.Throws<DirectoryNotFoundException>(() => Checkpoint.Resolve(empty, 0));
        }

        // ------------------------------------------------------------------ the cadence

        [Fact]
        public void TheCadenceIsOffByDefaultAndNeverFinerThanAMetabolicStep()
        {
            var settings = new EnvSettings();

            Assert.Equal(0f, settings.ResolveCheckpointEvery());

            settings.CheckpointEvery = 200f;
            Assert.Equal(200f, settings.ResolveCheckpointEvery());

            settings.CheckpointEvery = 0.001f;
            Assert.Equal(EnvBinding.MetabolicStepSeconds, settings.ResolveCheckpointEvery());
        }
    }
}
