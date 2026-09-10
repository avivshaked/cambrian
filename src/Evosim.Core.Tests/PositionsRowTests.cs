using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// One line of <c>positions.jsonl</c>, the file that answers where a body is.
    /// </summary>
    /// <remarks>
    /// The format is tested here rather than through a run because it is the one part of the
    /// instrument that can be checked in a second outside the Editor, and because the thing a
    /// reader of the file depends on is the shape of the row and nothing else: two decimals, one
    /// line, five entries per body, and a refusal rather than a plausible number whenever the
    /// caller hands it something it cannot record (logbook/0083 is the incident the file exists
    /// for, and a position quietly written as 0 would be the same class of fault).
    /// </remarks>
    public class PositionsRowTests
    {
        private readonly ITestOutputHelper _output;

        public PositionsRowTests(ITestOutputHelper output) => _output = output;

        [Fact]
        public void TheRowCarriesTimeCountAndOneEntryPerBody()
        {
            string row = PositionsRow.Write(
                1234.5, 2,
                new long[] { 17, 4242 },
                new[] { 3.456f, 19.994f },
                new[] { -12.341f, -0.004f },
                new[] { 1f, 4.5f },
                new[] { PositionsRow.AbsorptiveBit, PositionsRow.JointedBit | PositionsRow.PhotosyntheticBit });

            _output.WriteLine(row);

            // Two decimals, rounded away from zero; -0.004 m is written 0 rather than "-0"; a
            // whole number keeps no decimal point at all.
            Assert.Equal(
                "{\"t\":1234.5,\"n\":2,\"b\":[[17,3.46,-12.34,1,1],[4242,19.99,0,4.5,6]]}",
                row);
        }

        [Fact]
        public void AnEmptyWorldStillWritesARow()
        {
            // A sample with nothing alive is a fact about the world, and a reader stepping through
            // the file needs the sample to exist to see it.
            Assert.Equal(
                "{\"t\":100,\"n\":0,\"b\":[]}",
                PositionsRow.Write(100d, 0, new long[0], new float[0], new float[0], new float[0], new int[0]));
        }

        [Fact]
        public void TheRowIsOneLineAndParsesBack()
        {
            // JsonlWriter refuses a row with a line break, because one embedded newline makes
            // every row after it unreadable (§9). Checked here as well, so the failure lands on
            // the format rather than four hours into a run.
            string row = PositionsRow.Write(
                7.25, 3,
                new long[] { 1, 2, 3 },
                new[] { 0.1f, 10f, 19.5f },
                new[] { -1f, -30f, -59.99f },
                new[] { 0f, 2.5f, 4.99f },
                new[] { 0, PositionsRow.AbsorptiveBit | PositionsRow.JointedBit, PositionsRow.AllBits });

            Assert.DoesNotContain("\n", row);
            Assert.DoesNotContain("\r", row);

            JsonNode parsed = Json.Parse(row);
            Assert.Equal(3, parsed["n"].AsInt());
            Assert.Equal(3, parsed["b"].Count);

            JsonNode last = parsed["b"][2];
            Assert.Equal(5, last.Count);
            Assert.Equal(3, last[0].AsInt());
            Assert.Equal(19.5, last[1].AsDouble());
            Assert.Equal(-59.99, last[2].AsDouble());
            Assert.Equal(4.99, last[3].AsDouble());
            Assert.Equal(PositionsRow.AllBits, last[4].AsInt());
        }

        [Fact]
        public void ANonFiniteRootIsRefusedRatherThanWritten()
        {
            // The harness skips a body whose root is not finite, the way MeasureHorizontalSpread
            // does; if one arrives anyway it is a diverged body about to be killed, not a
            // position, and a row that carried it would put a creature nowhere and let a reader
            // plot it.
            ArgumentException thrown = Assert.Throws<ArgumentException>(() => PositionsRow.Write(
                1d, 1,
                new long[] { 99 }, new[] { 1f }, new[] { float.NaN }, new[] { 1f }, new[] { 0 }));

            Assert.Contains("id 99", thrown.Message);
            Assert.Contains("y", thrown.Message);
        }

        [Fact]
        public void AnUndefinedFlagBitIsRefused()
        {
            // Refused rather than masked: a reader that met an unknown bit could not tell a new
            // guild from a corrupted row, and the guild is the whole reason the flags are there.
            ArgumentException thrown = Assert.Throws<ArgumentException>(() => PositionsRow.Write(
                1d, 1,
                new long[] { 7 }, new[] { 1f }, new[] { -1f }, new[] { 1f },
                new[] { PositionsRow.AllBits + 1 }));

            Assert.Contains("id 7", thrown.Message);
        }

        [Fact]
        public void AShortArrayIsRefusedRatherThanReadAsABodyAtTheOrigin()
        {
            // The five arrays are filled by the caller in one loop and a short one would put a
            // body at 0,0,0, which is a position nothing distinguishes from a real one.
            ArgumentException thrown = Assert.Throws<ArgumentException>(() => PositionsRow.Write(
                1d, 2,
                new long[] { 1, 2 }, new[] { 1f, 2f }, new[] { -1f, -2f }, new[] { 1f }, new[] { 0, 0 }));

            Assert.Contains("holds 1 entries", thrown.Message);
        }
    }
}
