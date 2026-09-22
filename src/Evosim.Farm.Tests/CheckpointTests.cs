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
