using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
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
    /// <para>
    /// Every test writes into a directory of its own under <c>artifacts/</c> and deletes it, which
    /// is <c>ManifestTests</c>' rule: nothing of the project's is written outside the repository,
    /// the system's temporary directory included.
    /// </para>
    /// <para>
    /// <b>Both versions, through one set of tests.</b> The round trip, the killed run, the index
    /// and the frame found by its second hold for version 1 and version 2 alike, so they are
    /// theories over the version. What only one version has (version 1's raw size, version 2's
    /// deflate and its flags) has tests of its own, and a version 1 file laid out by hand from
    /// the spec checks the reader against something the writer did not produce.
    /// </para>
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
        /// numbers, so a float that took a detour through a decimal shows up. Every combination
        /// of the three guild bits turns up across a frame.
        /// </summary>
        private static PoseBody[] Crowd(int frame, int bodies, int version)
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
                    Flags = version >= 2 ? (i + 3 * frame) % 8 : PoseStream.FlagsNotRecorded,
                    ReserveSeconds = version >= 3
                        ? i % 5 == 4 ? float.PositiveInfinity : (float)(12.345678 * i + 0.1 * frame)
                        : PoseStream.ReserveNotRecorded,
                    BreedFraction = version >= 3 ? (float)(0.0371 * i + 0.013 * frame) : PoseStream.ReserveNotRecorded,
                    Joints = joints,
                };
            }

            return crowd;
        }

        /// <summary>Writes a stream of <paramref name="frames"/> frames and returns what it wrote.</summary>
        private List<PoseFrame> WriteStream(
            int frames, int bodies, float cadence, string hash, int version = PoseStream.Version)
        {
            var written = new List<PoseFrame>();

            using (var writer = new PoseStreamWriter(Path_, cadence, hash, version))
            {
                Assert.Equal(version, writer.Version);

                for (int f = 0; f < frames; f++)
                {
                    double t = cadence * (f + 1);
                    PoseBody[] crowd = Crowd(f, bodies, version);

                    writer.BeginFrame(t);

                    foreach (PoseBody body in crowd)
                    {
                        var joints = new double[body.Joints.Length];
                        for (int d = 0; d < joints.Length; d++) joints[d] = body.Joints[d];

                        if (version >= 3)
                        {
                            writer.Body(
                                body.Id, body.X, body.Y, body.Z,
                                body.Qx, body.Qy, body.Qz, body.Qw,
                                body.BodyFraction, body.Flags, body.ReserveSeconds, body.BreedFraction, joints.Length, joints);
                        }
                        else if (version >= 2)
                        {
                            writer.Body(
                                body.Id, body.X, body.Y, body.Z,
                                body.Qx, body.Qy, body.Qz, body.Qw,
                                body.BodyFraction, body.Flags, joints.Length, joints);
                        }
                        else
                        {
                            writer.Body(
                                body.Id, body.X, body.Y, body.Z,
                                body.Qx, body.Qy, body.Qz, body.Qw,
                                body.BodyFraction, joints.Length, joints);
                        }
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

                Assert.Equal(a.Flags, b.Flags);

                Assert.Equal(
                    BitConverter.SingleToInt32Bits(a.ReserveSeconds),
                    BitConverter.SingleToInt32Bits(b.ReserveSeconds));

                Assert.Equal(
                    BitConverter.SingleToInt32Bits(a.BreedFraction),
                    BitConverter.SingleToInt32Bits(b.BreedFraction));

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
        public void HeaderCarriesTheVersionTheCadenceAndTheConfigHash()
        {
            using (var writer = new PoseStreamWriter(Path_, 0.5f, "ff557bce2685293a"))
            {
                Assert.Equal(3, writer.Version);
            }

            using (PoseStreamReader reader = PoseStreamReader.Open(Path_))
            {
                Assert.Equal(3, PoseStream.Version);
                Assert.Equal(PoseStream.Version, reader.Header.Version);
                Assert.Equal(0.5f, reader.Header.CadenceSeconds);
                Assert.Equal("ff557bce2685293a", reader.Header.ConfigHash);
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void EveryFrameComesBackBitExact(int version)
        {
            List<PoseFrame> written = WriteStream(12, 7, 0.5f, "ff557bce2685293a", version);

            using (PoseStreamReader reader = PoseStreamReader.Open(Path_))
            {
                Assert.Equal(version, reader.Header.Version);
                Assert.Equal(written.Count, reader.Frames.Length);

                for (int f = 0; f < written.Count; f++)
                {
                    AssertSameFrame(written[f], reader.Read(f));
                }
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void AFrameOfNobodyIsAFrame(int version)
        {
            using (var writer = new PoseStreamWriter(Path_, 0.5f, "abc", version))
            {
                writer.BeginFrame(0.5);
                writer.EndFrame();

                writer.BeginFrame(1.0);

                if (version >= 2) writer.Body(7, 1f, 2f, 3f, 0f, 0f, 0f, 1f, 1f, 5, 0, new double[0]);
                else writer.Body(7, 1f, 2f, 3f, 0f, 0f, 0f, 1f, 1f, 0, new double[0]);

                writer.EndFrame();
            }

            using (PoseStreamReader reader = PoseStreamReader.Open(Path_))
            {
                Assert.Equal(2, reader.Frames.Length);
                Assert.Empty(reader.Read(0).Bodies);
                Assert.Single(reader.Read(1).Bodies);
                Assert.Empty(reader.Read(1).Bodies[0].Joints);
                Assert.Equal(version >= 2 ? 5 : PoseStream.FlagsNotRecorded, reader.Read(1).Bodies[0].Flags);
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void AFrameIsFoundByItsSecond(int version)
        {
            WriteStream(10, 3, 0.5f, "abc", version);

            using (PoseStreamReader reader = PoseStreamReader.Open(Path_))
            {
                Assert.Equal(2.5, reader.At(2.5).Seconds);
                Assert.Null(reader.At(2.6));
                Assert.Equal(2.5, reader.At(2.5004).Seconds);
                Assert.Equal(-1, reader.IndexOf(1000d));
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void TheIndexOnDiskIsWrittenAndUsed(int version)
        {
            List<PoseFrame> written = WriteStream(6, 4, 0.5f, "abc", version);

            Assert.True(File.Exists(IndexPath));

            Assert.Equal(
                PoseStream.IndexHeaderBytes + PoseStream.IndexEntryBytes * written.Count,
                new FileInfo(IndexPath).Length);

            // The index carries its stream's version.
            Assert.Equal(version, File.ReadAllBytes(IndexPath)[8]);

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

        [Fact]
        public void AnIndexOfAnotherVersionIsDropped()
        {
            List<PoseFrame> written = WriteStream(5, 3, 0.5f, "abc", 2);

            byte[] index = File.ReadAllBytes(IndexPath);
            index[8] = 1;
            File.WriteAllBytes(IndexPath, index);

            using (PoseStreamReader reader = PoseStreamReader.Open(Path_))
            {
                Assert.False(reader.IndexRead);
                Assert.Equal(written.Count, reader.Frames.Length);
                AssertSameFrame(written[4], reader.Read(4));
            }
        }

        // ------------------------------------------------------------------ the killed run

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void AKilledRunIsReadToItsLastCompleteFrame(int version)
        {
            List<PoseFrame> written = WriteStream(8, 5, 0.5f, "abc", version);

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

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void AFrameWithNoTrailerIsNotAFrame(int version)
        {
            WriteStream(4, 3, 0.5f, "abc", version);
            File.Delete(IndexPath);

            // Four bytes short: every byte of the payload is there and the trailer is not.
            Truncate(Path_, new FileInfo(Path_).Length - 4);

            using (PoseStreamReader reader = PoseStreamReader.Open(Path_))
            {
                Assert.Equal(3, reader.Frames.Length);
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void AnIndexThatDisagreesWithItsFileIsDropped(int version)
        {
            List<PoseFrame> written = WriteStream(6, 4, 0.5f, "abc", version);

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

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void AnIndexOfTheWrongLengthIsDropped(int version)
        {
            WriteStream(5, 2, 0.5f, "abc", version);

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

        // ------------------------------------------------------------------ version 1 by hand

        /// <summary>
        /// A version 1 stream laid out byte by byte from the spec's tables, with no writer
        /// involved, is read: the stream every run recorded before version 2 stays readable.
        /// </summary>
        [Fact]
        public void AVersionOneFileLaidOutByHandIsRead()
        {
            var bytes = new List<byte>();

            bytes.AddRange(Encoding.ASCII.GetBytes("EVOPOSE"));
            bytes.Add(0);
            bytes.AddRange(BitConverter.GetBytes((ushort)1));
            bytes.AddRange(BitConverter.GetBytes((ushort)80));
            bytes.AddRange(BitConverter.GetBytes(0.5f));

            var hash = new byte[64];
            Encoding.ASCII.GetBytes("0861387741b94259").CopyTo(hash, 0);
            bytes.AddRange(hash);

            Assert.Equal(80, bytes.Count);

            // One frame, two bodies: a rigid one and one with two joint coordinates.
            var payload = new List<byte>();
            payload.AddRange(BitConverter.GetBytes(100.5));
            payload.AddRange(BitConverter.GetBytes(2u));

            payload.AddRange(BitConverter.GetBytes(41));
            foreach (float v in new[] { 1.25f, -30.5f, 7f, 0f, 0f, 0f, 1f, 0.75f }) payload.AddRange(BitConverter.GetBytes(v));
            payload.Add(0);

            payload.AddRange(BitConverter.GetBytes(42));
            foreach (float v in new[] { -3f, -12.125f, 9.5f, 0.5f, 0.5f, 0.5f, 0.5f, 1f }) payload.AddRange(BitConverter.GetBytes(v));
            payload.Add(2);
            payload.AddRange(BitConverter.GetBytes(0.3f));
            payload.AddRange(BitConverter.GetBytes(-0.7f));

            Assert.Equal(12 + 37 + 37 + 8, payload.Count);

            bytes.AddRange(Encoding.ASCII.GetBytes("FRAM"));
            bytes.AddRange(BitConverter.GetBytes((uint)payload.Count));
            bytes.AddRange(payload);
            bytes.AddRange(BitConverter.GetBytes((uint)payload.Count));

            File.WriteAllBytes(Path_, bytes.ToArray());

            using (PoseStreamReader reader = PoseStreamReader.Open(Path_))
            {
                Assert.Equal(1, reader.Header.Version);
                Assert.Equal("0861387741b94259", reader.Header.ConfigHash);
                Assert.False(reader.IndexRead);
                Assert.Single(reader.Frames);

                PoseFrame frame = reader.Read(0);
                Assert.Equal(100.5, frame.Seconds);
                Assert.Equal(2, frame.Bodies.Length);

                Assert.Equal(41, frame.Bodies[0].Id);
                Assert.Equal(-30.5f, frame.Bodies[0].Y);
                Assert.Equal(0.75f, frame.Bodies[0].BodyFraction);
                Assert.Equal(PoseStream.FlagsNotRecorded, frame.Bodies[0].Flags);
                Assert.Empty(frame.Bodies[0].Joints);

                Assert.Equal(42, frame.Bodies[1].Id);
                Assert.Equal(-12.125f, frame.Bodies[1].Y);
                Assert.Equal(PoseStream.FlagsNotRecorded, frame.Bodies[1].Flags);
                Assert.Equal(new[] { 0.3f, -0.7f }, frame.Bodies[1].Joints);
            }
        }

        [Fact]
        public void VersionOneIsItsOwnSize()
        {
            // 37 bytes a body plus four a degree of freedom, and twelve of framing a frame.
            Assert.Equal(49, PoseStream.BodyBytes(3, 1));
            Assert.Equal(37, PoseStream.BodyBytes(0, 1));

            WriteStream(4, 10, 0.5f, "abc", 1);

            long expected = PoseStream.HeaderBytes;

            for (int f = 0; f < 4; f++)
            {
                expected += PoseStream.FrameOverheadBytes + 12; // the framing, the time, the count

                foreach (PoseBody body in Crowd(f, 10, 1))
                {
                    expected += PoseStream.BodyBytes(body.Joints.Length, 1);
                }
            }

            Assert.Equal(expected, new FileInfo(Path_).Length);
        }

        // ------------------------------------------------------------------ version 2's own

        /// <summary>
        /// A version 2 frame is the time, the count and the raw length uncompressed, then the
        /// body records deflated, and they inflate to the records the spec lays out: 38 bytes a
        /// body and four a degree of freedom, the flags byte before the count of joints.
        /// </summary>
        [Fact]
        public void AVersionTwoFrameIsItsPrefixAndItsDeflatedBodies()
        {
            Assert.Equal(50, PoseStream.BodyBytes(3, 2));
            Assert.Equal(38, PoseStream.BodyBytes(0, 2));

            List<PoseFrame> written = WriteStream(3, 40, 0.5f, "abc", 2);

            byte[] file = File.ReadAllBytes(Path_);

            using (PoseStreamReader reader = PoseStreamReader.Open(Path_))
            {
                for (int f = 0; f < reader.Frames.Length; f++)
                {
                    PoseFrameRef frame = reader.Frames[f];
                    int at = (int)frame.Offset;

                    Assert.Equal("FRAM", Encoding.ASCII.GetString(file, at, 4));
                    Assert.Equal(frame.PayloadBytes, (int)BitConverter.ToUInt32(file, at + 4));
                    Assert.Equal(written[f].Seconds, BitConverter.ToDouble(file, at + 8));
                    Assert.Equal(40u, BitConverter.ToUInt32(file, at + 16));

                    int raw = (int)BitConverter.ToUInt32(file, at + 20);
                    int expectedRaw = 0;
                    foreach (PoseBody body in written[f].Bodies) expectedRaw += PoseStream.BodyBytes(body.Joints.Length, 2);
                    Assert.Equal(expectedRaw, raw);

                    // Deflated, and smaller for it: forty bodies of near-repeated numbers.
                    Assert.True(frame.PayloadBytes - 16 < raw, "the bodies were not compressed");

                    byte[] records = Inflate(file, at + 24, frame.PayloadBytes - 16);
                    Assert.Equal(raw, records.Length);

                    // The first body, read off the inflated bytes by the spec's offsets.
                    PoseBody first = written[f].Bodies[0];
                    Assert.Equal(first.Id, BitConverter.ToInt32(records, 0));
                    Assert.Equal(first.BodyFraction, BitConverter.ToSingle(records, 32));
                    Assert.Equal(first.Flags, records[36]);
                    Assert.Equal(first.Joints.Length, records[37]);

                    Assert.Equal(frame.PayloadBytes, (int)BitConverter.ToUInt32(file, at + 8 + frame.PayloadBytes));
                }
            }
        }

        [Fact]
        public void AVersionTwoFrameThatInflatesShortIsRefused()
        {
            WriteStream(2, 5, 0.5f, "abc", 2);

            byte[] file = File.ReadAllBytes(Path_);
            long second;

            using (PoseStreamReader reader = PoseStreamReader.Open(Path_))
            {
                second = reader.Frames[1].Offset;
            }

            // The raw length one byte longer than the deflated bodies inflate to.
            uint raw = BitConverter.ToUInt32(file, (int)second + 20);
            BitConverter.GetBytes(raw + 1).CopyTo(file, (int)second + 20);
            File.WriteAllBytes(Path_, file);

            using (PoseStreamReader reader = PoseStreamReader.Open(Path_))
            {
                Assert.Equal(2, reader.Frames.Length);
                reader.Read(0);

                InvalidDataException e = Assert.Throws<InvalidDataException>(() => reader.Read(1));
                Assert.Contains("inflate", e.Message, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void AVersionTwoFrameWhoseCountDisagreesWithItsBodiesIsRefused()
        {
            WriteStream(1, 5, 0.5f, "abc", 2);

            byte[] file = File.ReadAllBytes(Path_);
            BitConverter.GetBytes(4u).CopyTo(file, PoseStream.HeaderBytes + 16);
            File.WriteAllBytes(Path_, file);

            using (PoseStreamReader reader = PoseStreamReader.Open(Path_))
            {
                Assert.Throws<InvalidDataException>(() => reader.Read(0));
            }
        }

        /// <summary>
        /// A NaN body fraction is "not recorded": written as it is, read back as NaN and never as
        /// a size. The quiet NaN Python's <c>struct</c> writes (<c>0x7FC00000</c>, what
        /// <c>scripts/record-convert.py</c> puts in a converted stream) comes back bit for bit.
        /// </summary>
        [Fact]
        public void ANaNFractionIsReadAsNotRecorded()
        {
            float pythonNaN = BitConverter.Int32BitsToSingle(0x7FC00000);

            using (var writer = new PoseStreamWriter(Path_, 10f, "abc", 2))
            {
                writer.BeginFrame(10d);
                writer.Body(1, 1f, 2f, 3f, 0f, 0f, 0f, 1f, pythonNaN, 4, 0, new double[0]);
                writer.Body(2, 1f, 2f, 3f, 0f, 0f, 0f, 1f, PoseStream.FractionNotRecorded, 0, 1, new[] { 0.5 });
                writer.Body(3, 1f, 2f, 3f, 0f, 0f, 0f, 1f, 0.42f, 2, 0, new double[0]);
                writer.EndFrame();
            }

            using (PoseStreamReader reader = PoseStreamReader.Open(Path_))
            {
                PoseBody[] bodies = reader.Read(0).Bodies;

                Assert.False(bodies[0].FractionRecorded);
                Assert.Equal(0x7FC00000, BitConverter.SingleToInt32Bits(bodies[0].BodyFraction));
                Assert.False(bodies[1].FractionRecorded);
                Assert.True(float.IsNaN(bodies[1].BodyFraction));
                Assert.True(bodies[2].FractionRecorded);
                Assert.Equal(0.42f, bodies[2].BodyFraction);
            }

            Assert.False(PoseStream.FractionRecorded(PoseStream.FractionNotRecorded));
            Assert.True(PoseStream.FractionRecorded(1f));
        }

        [Fact]
        public void TheFlagBitsArePositionsJsonls()
        {
            Assert.Equal(PositionsRow.AbsorptiveBit, PoseStream.AbsorptiveBit);
            Assert.Equal(PositionsRow.JointedBit, PoseStream.JointedBit);
            Assert.Equal(PositionsRow.PhotosyntheticBit, PoseStream.PhotosyntheticBit);
            Assert.Equal(PositionsRow.AllBits, PoseStream.AllFlagBits);
        }

        [Fact]
        public void AVersionTwoWriterWantsFlagsAndAVersionOneWriterRefusesThem()
        {
            using (var writer = new PoseStreamWriter(Path_, 0.5f, "abc", 2))
            {
                writer.BeginFrame(0.5);

                Assert.Throws<InvalidOperationException>(
                    () => writer.Body(1, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 1f, 0, new double[0]));

                Assert.Throws<ArgumentOutOfRangeException>(
                    () => writer.Body(1, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 1f, 8, 0, new double[0]));

                Assert.Throws<ArgumentOutOfRangeException>(
                    () => writer.Body(1, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 1f, -1, 0, new double[0]));
            }

            using (var writer = new PoseStreamWriter(Path_, 0.5f, "abc", 1))
            {
                writer.BeginFrame(0.5);

                Assert.Throws<InvalidOperationException>(
                    () => writer.Body(1, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 1f, 3, 0, new double[0]));
            }

            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PoseStreamWriter(Path_, 0.5f, "abc", 4));
        }

        /// <summary>
        /// Version 3 carries each body's seconds of reserve, bit for bit, positive infinity (a body
        /// with no standing cost) and NaN (not recorded) included; a version 2 writer has no field
        /// for it and refuses the overload that takes it, and every version 2 body reads NaN.
        /// </summary>
        [Fact]
        public void VersionThreeCarriesTheReserveAndVersionTwoReadsItAsNotRecorded()
        {
            using (var writer = new PoseStreamWriter(Path_, 0.5f, "abc", 3))
            {
                writer.BeginFrame(0.5);
                writer.Body(1, 1f, 2f, 3f, 0f, 0f, 0f, 1f, 0.5f, 4, 188.25f, 0.625f, 1, new[] { 0.25 });
                writer.Body(2, 1f, 2f, 3f, 0f, 0f, 0f, 1f, 1f, 1, float.PositiveInfinity, 1.5f, 0, new double[0]);
                writer.Body(3, 1f, 2f, 3f, 0f, 0f, 0f, 1f, 1f, 1, PoseStream.ReserveNotRecorded, float.NaN, 0, new double[0]);
                writer.EndFrame();
            }

            using (PoseStreamReader reader = PoseStreamReader.Open(Path_))
            {
                PoseBody[] bodies = reader.Read(0).Bodies;
                Assert.Equal(188.25f, bodies[0].ReserveSeconds);
                Assert.Equal(0.625f, bodies[0].BreedFraction);
                Assert.Equal(1.5f, bodies[1].BreedFraction);
                Assert.True(float.IsNaN(bodies[2].BreedFraction));
                Assert.Equal(new[] { 0.25f }, bodies[0].Joints);
                Assert.True(float.IsPositiveInfinity(bodies[1].ReserveSeconds));
                Assert.True(float.IsNaN(bodies[2].ReserveSeconds));
                Assert.Equal(4, bodies[0].Flags);
            }

            using (var writer = new PoseStreamWriter(Path_, 0.5f, "abc", 2))
            {
                writer.BeginFrame(0.5);
                Assert.Throws<InvalidOperationException>(
                    () => writer.Body(1, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 1f, 1, 5f, 0.5f, 0, new double[0]));
                writer.Body(1, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 1f, 1, 0, new double[0]);
                writer.EndFrame();
            }

            using (PoseStreamReader reader = PoseStreamReader.Open(Path_))
            {
                Assert.True(float.IsNaN(reader.Read(0).Bodies[0].ReserveSeconds));
                Assert.True(float.IsNaN(reader.Read(0).Bodies[0].BreedFraction));
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
                        1, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 1f, 0, 256, new double[256]));
            }
        }

        // ------------------------------------------------------------------ the run's stream

        /// <summary>
        /// A run's own stream is version 2 and carries, for every body, the guild flags the
        /// sampler wrote to <c>positions.jsonl</c> at the same second.
        /// </summary>
        /// <remarks>
        /// The stream and the positions row are taken at the same metabolic step from the same
        /// living bodies, so every body in both has to carry the same three bits. The positions
        /// file is read whether it is plain or gzipped, so the test outlives the record's change
        /// to <c>positions.jsonl.gz</c>.
        /// </remarks>
        [Fact]
        public void TheRunsStreamCarriesTheFlagsPositionsJsonlCarries()
        {
            string runsRoot = Path.Combine(_directory, "runs");

            string[] settings = FilmWindowTests.SmallRun(runsRoot, "flags", seconds: 40, checkpointEvery: 0);
            var withStream = new List<string>(settings) { "EVOSIM_POSE_EVERY=1" };

            Assert.Equal(0, Program.Main(withStream.ToArray()));

            string runDirectory = Directory.GetDirectories(Path.Combine(runsRoot, "flags"))[0];

            Dictionary<double, Dictionary<long, int>> positions = FilmWindowTests.PositionFlags(runDirectory);
            Assert.NotEmpty(positions);

            int compared = 0;
            var seen = new HashSet<int>();

            using (PoseStreamReader reader = PoseStreamReader.Open(Path.Combine(runDirectory, PoseStream.FileName)))
            {
                Assert.Equal(PoseStream.Version, reader.Header.Version);

                for (int f = 0; f < reader.Frames.Length; f++)
                {
                    PoseFrame frame = reader.Read(f);

                    // Version 3: the farm records every body's reserve, never NaN.
                    foreach (PoseBody body in frame.Bodies)
                    {
                        Assert.False(float.IsNaN(body.ReserveSeconds));
                        Assert.False(float.IsNaN(body.BreedFraction));
                    }

                    if (!positions.TryGetValue(frame.Seconds, out Dictionary<long, int> flags)) continue;

                    foreach (PoseBody body in frame.Bodies)
                    {
                        if (!flags.TryGetValue(body.Id, out int expected)) continue;

                        Assert.Equal(expected, body.Flags);
                        seen.Add(body.Flags);
                        compared++;
                    }
                }
            }

            Assert.True(compared > 100, "only " + compared + " bodies were compared");

            // A crowd of one guild would pass with every flag written as a constant.
            Assert.True(seen.Count >= 2, "every body compared carried the same flags, " + string.Join(",", seen));
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

        private static byte[] Inflate(byte[] bytes, int start, int count)
        {
            using (var source = new MemoryStream(bytes, start, count, false))
            using (var inflate = new DeflateStream(source, CompressionMode.Decompress))
            using (var into = new MemoryStream())
            {
                inflate.CopyTo(into);
                return into.ToArray();
            }
        }

        private static void Truncate(string path, long length)
        {
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Write))
            {
                file.SetLength(length);
            }
        }
    }
}
