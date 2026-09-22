using System;
using System.Collections.Generic;
using System.IO;
using Evosim.Core;
using Evosim.Farm;
using Xunit;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// The state stream's acceptance: what was written comes back bit for bit, a killed run is
    /// readable to its last complete frame, and a file this build does not know is refused rather
    /// than read (<c>logbook/specs/state-stream-spec.md</c>).
    /// </summary>
    /// <remarks>
    /// Every test writes into a directory of its own under <c>artifacts/</c> and deletes it, which
    /// is <c>ManifestTests</c>' rule: nothing of the project's is written outside the repository,
    /// the system's temporary directory included.
    /// </remarks>
    public class PoseStreamTests : IDisposable
    {
        private readonly string _directory;

        public PoseStreamTests()
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

        private string Path_ => Path.Combine(_directory, PoseStream.FileName);

        private string IndexPath => Path.Combine(_directory, PoseStream.IndexFileName);

        // ------------------------------------------------------------------ the fixture

        /// <summary>
        /// A deterministic crowd: ids, places, attitudes and joint coordinates that are not round
        /// numbers, so a float that took a detour through a decimal shows up.
        /// </summary>
        private static PoseBody[] Crowd(int frame, int bodies)
        {
            var crowd = new PoseBody[bodies];

            for (int i = 0; i < bodies; i++)
            {
                int dof = (i + frame) % 4;
                var joints = new float[dof];

                for (int d = 0; d < dof; d++)
                {
                    joints[d] = (float)(0.1234567 * (d + 1) - 0.017 * i + 0.003 * frame);
                }

                crowd[i] = new PoseBody
                {
                    Id = 1000 * frame + i,
                    X = (float)(0.3333333 * i - 1.7),
                    Y = (float)(-12.345678 - 0.011 * frame),
                    Z = (float)(9.87654321 + 0.25 * i),
                    Qx = (float)(0.1 * i - 0.3),
                    Qy = (float)(0.2 + 0.05 * frame),
                    Qz = -0.4472136f,
                    Qw = 0.7071068f,
                    BodyFraction = (float)(0.3123 + 0.01 * i),
                    Joints = joints,
                };
            }

            return crowd;
        }

        /// <summary>Writes a stream of <paramref name="frames"/> frames and returns what it wrote.</summary>
        private List<PoseFrame> WriteStream(int frames, int bodies, float cadence, string hash)
        {
            var written = new List<PoseFrame>();

            using (var writer = new PoseStreamWriter(Path_, cadence, hash))
            {
                for (int f = 0; f < frames; f++)
                {
                    double t = cadence * (f + 1);
                    PoseBody[] crowd = Crowd(f, bodies);

                    writer.BeginFrame(t);

                    foreach (PoseBody body in crowd)
                    {
                        var joints = new double[body.Joints.Length];
                        for (int d = 0; d < joints.Length; d++) joints[d] = body.Joints[d];

                        writer.Body(
                            body.Id, body.X, body.Y, body.Z,
                            body.Qx, body.Qy, body.Qz, body.Qw,
                            body.BodyFraction, joints.Length, joints);
                    }

                    writer.EndFrame();
                    written.Add(new PoseFrame { Seconds = t, Bodies = crowd });
                }

                Assert.Equal(frames, writer.FrameCount);
            }

            return written;
        }

        private static void AssertSameFrame(PoseFrame expected, PoseFrame actual)
        {
            Assert.Equal(expected.Seconds, actual.Seconds);
            Assert.Equal(expected.Bodies.Length, actual.Bodies.Length);

            for (int i = 0; i < expected.Bodies.Length; i++)
            {
                PoseBody a = expected.Bodies[i];
                PoseBody b = actual.Bodies[i];

                Assert.Equal(a.Id, b.Id);

                // Bit-exact, not approximate: a float32 written and read back through this layout
                // must be the same float32, and a tolerance would hide a decimal detour.
                Assert.Equal(BitConverter.SingleToInt32Bits(a.X), BitConverter.SingleToInt32Bits(b.X));
                Assert.Equal(BitConverter.SingleToInt32Bits(a.Y), BitConverter.SingleToInt32Bits(b.Y));
                Assert.Equal(BitConverter.SingleToInt32Bits(a.Z), BitConverter.SingleToInt32Bits(b.Z));
                Assert.Equal(BitConverter.SingleToInt32Bits(a.Qx), BitConverter.SingleToInt32Bits(b.Qx));
                Assert.Equal(BitConverter.SingleToInt32Bits(a.Qy), BitConverter.SingleToInt32Bits(b.Qy));
                Assert.Equal(BitConverter.SingleToInt32Bits(a.Qz), BitConverter.SingleToInt32Bits(b.Qz));
                Assert.Equal(BitConverter.SingleToInt32Bits(a.Qw), BitConverter.SingleToInt32Bits(b.Qw));

                Assert.Equal(
                    BitConverter.SingleToInt32Bits(a.BodyFraction),
                    BitConverter.SingleToInt32Bits(b.BodyFraction));

                Assert.Equal(a.Joints.Length, b.Joints.Length);

                for (int d = 0; d < a.Joints.Length; d++)
                {
                    Assert.Equal(
                        BitConverter.SingleToInt32Bits(a.Joints[d]),
                        BitConverter.SingleToInt32Bits(b.Joints[d]));
                }
            }
        }

        // ------------------------------------------------------------------ the round trip

        [Fact]
        public void HeaderCarriesTheCadenceAndTheConfigHash()
        {
            WriteStream(3, 2, 0.5f, "ff557bce2685293a");

            using (PoseStreamReader reader = PoseStreamReader.Open(Path_))
            {
                Assert.Equal(PoseStream.Version, reader.Header.Version);
                Assert.Equal(0.5f, reader.Header.CadenceSeconds);
                Assert.Equal("ff557bce2685293a", reader.Header.ConfigHash);
            }
        }

        [Fact]
        public void EveryFrameComesBackBitExact()
        {
            List<PoseFrame> written = WriteStream(12, 7, 0.5f, "ff557bce2685293a");

            using (PoseStreamReader reader = PoseStreamReader.Open(Path_))
            {
                Assert.Equal(written.Count, reader.Frames.Length);

                for (int f = 0; f < written.Count; f++)
                {
                    AssertSameFrame(written[f], reader.Read(f));
                }
            }
        }

        [Fact]
        public void AFrameOfNobodyIsAFrame()
        {
            using (var writer = new PoseStreamWriter(Path_, 0.5f, "abc"))
            {
                writer.BeginFrame(0.5);
                writer.EndFrame();

                writer.BeginFrame(1.0);
                writer.Body(7, 1f, 2f, 3f, 0f, 0f, 0f, 1f, 1f, 0, new double[0]);
                writer.EndFrame();
            }

            using (PoseStreamReader reader = PoseStreamReader.Open(Path_))
            {
                Assert.Equal(2, reader.Frames.Length);
                Assert.Empty(reader.Read(0).Bodies);
                Assert.Single(reader.Read(1).Bodies);
                Assert.Empty(reader.Read(1).Bodies[0].Joints);
            }
        }

        [Fact]
        public void AFrameIsFoundByItsSecond()
        {
            WriteStream(10, 3, 0.5f, "abc");

            using (PoseStreamReader reader = PoseStreamReader.Open(Path_))
            {
                Assert.Equal(2.5, reader.At(2.5).Seconds);
                Assert.Null(reader.At(2.6));
                Assert.Equal(2.5, reader.At(2.5004).Seconds);
                Assert.Equal(-1, reader.IndexOf(1000d));
            }
        }

        [Fact]
        public void TheIndexOnDiskIsWrittenAndUsed()
        {
            List<PoseFrame> written = WriteStream(6, 4, 0.5f, "abc");

            Assert.True(File.Exists(IndexPath));

            Assert.Equal(
                PoseStream.IndexHeaderBytes + PoseStream.IndexEntryBytes * written.Count,
                new FileInfo(IndexPath).Length);

            using (PoseStreamReader reader = PoseStreamReader.Open(Path_))
            {
                Assert.True(reader.IndexRead);
                Assert.Equal(written.Count, reader.Frames.Length);

                PoseFrameRef[] scanned = reader.Scan();
                Assert.Equal(scanned.Length, reader.Frames.Length);

                for (int i = 0; i < scanned.Length; i++)
                {
                    Assert.Equal(scanned[i].Seconds, reader.Frames[i].Seconds);
                    Assert.Equal(scanned[i].Offset, reader.Frames[i].Offset);
                    Assert.Equal(scanned[i].PayloadBytes, reader.Frames[i].PayloadBytes);
                }
            }
        }

        // ------------------------------------------------------------------ the killed run

        [Fact]
        public void AKilledRunIsReadToItsLastCompleteFrame()
        {
            List<PoseFrame> written = WriteStream(8, 5, 0.5f, "abc");

            long whole;
            long lastFrameStart;

            using (PoseStreamReader reader = PoseStreamReader.Open(Path_))
            {
                PoseFrameRef last = reader.Frames[reader.Frames.Length - 1];
                lastFrameStart = last.Offset;
                whole = last.Offset + 8 + last.PayloadBytes + 4;
            }

            // The index is what an orderly end leaves, and a killed run has none.
            File.Delete(IndexPath);

            // Half of the last frame, which is what a process killed mid-write leaves behind.
            Truncate(Path_, lastFrameStart + (whole - lastFrameStart) / 2);

            using (PoseStreamReader reader = PoseStreamReader.Open(Path_))
            {
                Assert.False(reader.IndexRead);
                Assert.Equal(written.Count - 1, reader.Frames.Length);

                for (int f = 0; f < reader.Frames.Length; f++)
                {
                    AssertSameFrame(written[f], reader.Read(f));
                }
            }
        }

        [Fact]
        public void AFrameWithNoTrailerIsNotAFrame()
        {
            WriteStream(4, 3, 0.5f, "abc");
            File.Delete(IndexPath);

            // Four bytes short: every byte of the payload is there and the trailer is not.
            Truncate(Path_, new FileInfo(Path_).Length - 4);

            using (PoseStreamReader reader = PoseStreamReader.Open(Path_))
            {
                Assert.Equal(3, reader.Frames.Length);
            }
        }

        [Fact]
        public void AnIndexThatDisagreesWithItsFileIsDropped()
        {
            List<PoseFrame> written = WriteStream(6, 4, 0.5f, "abc");

            // The index of a longer stream against a file that was cut short: the last entry
            // points past the end, and the whole index has to go rather than half of it stand.
            long keep;

            using (PoseStreamReader reader = PoseStreamReader.Open(Path_))
            {
                PoseFrameRef third = reader.Frames[3];
                keep = third.Offset + 8 + third.PayloadBytes + 4;
            }

            Truncate(Path_, keep);

            using (PoseStreamReader reader = PoseStreamReader.Open(Path_))
            {
                Assert.False(reader.IndexRead);
                Assert.Equal(4, reader.Frames.Length);
                AssertSameFrame(written[3], reader.Read(3));
            }
        }

        [Fact]
        public void AnIndexOfTheWrongLengthIsDropped()
        {
            WriteStream(5, 2, 0.5f, "abc");

            byte[] index = File.ReadAllBytes(IndexPath);
            var shortened = new byte[index.Length - 3];
            Array.Copy(index, shortened, shortened.Length);
            File.WriteAllBytes(IndexPath, shortened);

            using (PoseStreamReader reader = PoseStreamReader.Open(Path_))
            {
                Assert.False(reader.IndexRead);
                Assert.Equal(5, reader.Frames.Length);
            }
        }

        // ------------------------------------------------------------------ the refusals

        [Fact]
        public void ABadMagicIsRefused()
        {
            WriteStream(2, 2, 0.5f, "abc");

            byte[] bytes = File.ReadAllBytes(Path_);
            bytes[3] = (byte)'X';
            File.WriteAllBytes(Path_, bytes);

            InvalidDataException e = Assert.Throws<InvalidDataException>(
                () => PoseStreamReader.Open(Path_));

            Assert.Contains("not a pose stream", e.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AnUnknownVersionIsRefused()
        {
            WriteStream(2, 2, 0.5f, "abc");

            byte[] bytes = File.ReadAllBytes(Path_);
            bytes[8] = 9;
            File.WriteAllBytes(Path_, bytes);

            InvalidDataException e = Assert.Throws<InvalidDataException>(
                () => PoseStreamReader.Open(Path_));

            Assert.Contains("version 9", e.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AFileTooShortToHoldAHeaderIsRefused()
        {
            File.WriteAllBytes(Path_, new byte[] { 1, 2, 3 });

            Assert.Throws<InvalidDataException>(() => PoseStreamReader.Open(Path_));
        }

        [Fact]
        public void ACadenceOfZeroIsNotAStream()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PoseStreamWriter(Path_, 0f, "abc"));
        }

        [Fact]
        public void ADegreeOfFreedomCountOverAByteIsRefused()
        {
            using (var writer = new PoseStreamWriter(Path_, 0.5f, "abc"))
            {
                writer.BeginFrame(0.5);

                Assert.Throws<ArgumentOutOfRangeException>(
                    () => writer.Body(
                        1, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 1f, 256, new double[256]));
            }
        }

        // ------------------------------------------------------------------ the size

        [Fact]
        public void TheSizeIsWhatTheSpecSaysItIs()
        {
            // 37 bytes a body plus four a degree of freedom, and twelve of framing a frame.
            Assert.Equal(49, PoseStream.BodyBytes(3));
            Assert.Equal(37, PoseStream.BodyBytes(0));

            WriteStream(4, 10, 0.5f, "abc");

            long expected = PoseStream.HeaderBytes;

            for (int f = 0; f < 4; f++)
            {
                expected += PoseStream.FrameOverheadBytes + 12; // the framing, the time, the count

                foreach (PoseBody body in Crowd(f, 10))
                {
                    expected += PoseStream.BodyBytes(body.Joints.Length);
                }
            }

            Assert.Equal(expected, new FileInfo(Path_).Length);
        }

        // ------------------------------------------------------------------ the cadence binding

        [Fact]
        public void TheCadenceIsBoundAndReachesNoConfigHash()
        {
            Dictionary<string, string> plain = EnvBindingTests.Round42Seed1();

            var withStream = new Dictionary<string, string>(plain)
            {
                { "EVOSIM_POSE_EVERY", "0.5" },
            };

            EnvSettings a = EnvBinding.Read(EnvBinding.Of(plain));
            EnvSettings b = EnvBinding.Read(EnvBinding.Of(withStream));

            Assert.Equal(0f, a.PoseEvery);
            Assert.Equal(0.5f, b.PoseEvery);

            // The whole point of binding it here rather than on RunConfig: the world is the same
            // world, filed under the same hash, with the recording on or off.
            Assert.Equal(EnvBinding.BuildConfig(a).Hash(), EnvBinding.BuildConfig(b).Hash());

            // And it is a setting this build reads, so it raises no warning about a name nobody
            // binds — the check that catches a launcher's typo.
            Assert.Empty(EnvBinding.UnknownNames(new List<string> { "EVOSIM_POSE_EVERY" }));
        }

        [Fact]
        public void ACadenceUnderTheMetabolicStepIsRaisedToIt()
        {
            Assert.Equal(0f, new EnvSettings { PoseEvery = 0f }.ResolvePoseEvery());
            Assert.Equal(0.5f, new EnvSettings { PoseEvery = 0.1f }.ResolvePoseEvery());
            Assert.Equal(0.5f, new EnvSettings { PoseEvery = 0.5f }.ResolvePoseEvery());
            Assert.Equal(10f, new EnvSettings { PoseEvery = 10f }.ResolvePoseEvery());
        }

        [Fact]
        public void TheManifestRecordsTheCadenceAndTheFrameCount()
        {
            var manifest = new RunManifest { ArmName = "posesmoke", PoseEverySeconds = 0.5d };

            string running = Manifest.Render(manifest, null);
            Assert.Contains("\"poseEverySeconds\": 0.5", running, StringComparison.Ordinal);

            string ended = Manifest.Render(
                manifest, new RunEnding { Status = "ended", Reason = "budget", PoseFrames = 600 });

            Assert.Contains("\"poseFrames\": 600", ended, StringComparison.Ordinal);
        }

        // ------------------------------------------------------------------

        private static void Truncate(string path, long length)
        {
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Write))
            {
                file.SetLength(length);
            }
        }
    }
}
