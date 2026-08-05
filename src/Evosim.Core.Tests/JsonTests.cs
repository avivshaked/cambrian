using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>The hand-written JSON layer of DESIGN.md §9.</summary>
    public class JsonTests
    {
        private readonly ITestOutputHelper _output;

        public JsonTests(ITestOutputHelper output) => _output = output;

        [Fact]
        public void FloatsSurviveARoundTripExactly()
        {
            // The requirement the whole format rests on. A float written with fewer digits than
            // it needs reads back as a *different* number, so a genome saved and reloaded would
            // develop into a slightly different body while still claiming to be the original —
            // and every measurement taken from it would be filed under the wrong genome.
            float[] awkward =
            {
                0.1f, 1f / 3f, 1e-30f, 3.4028235e38f, -1.17549435e-38f,
                0.30000001192092896f, 123456.789f, float.Epsilon, 0f, -0f,
            };

            var w = new Json.Writer();
            w.BeginObject();
            w.BeginArray("v");
            foreach (float f in awkward) w.Value(f);
            w.EndArray();
            w.EndObject();

            JsonNode parsed = Json.Parse(w.ToString());

            for (int i = 0; i < awkward.Length; i++)
            {
                float back = parsed["v"][i].AsFloat();
                Assert.True(
                    BitConverter.SingleToInt32Bits(awkward[i]) == BitConverter.SingleToInt32Bits(back),
                    $"{awkward[i]:R} came back as {back:R}");
            }
        }

        [Fact]
        public void NestedStructureRoundTrips()
        {
            var w = new Json.Writer();
            w.BeginObject()
                .Field("name", "creature")
                .Field("alive", true)
                .Field("parts", 7)
                .Field("energy", 12.5f);

            w.BeginArray("children");
            w.BeginObject().Field("id", 1).EndObject();
            w.BeginObject().Field("id", 2).EndObject();
            w.EndArray();

            w.BeginObject("nested").Field("deep", "yes").EndObject();
            w.EndObject();

            string text = w.ToString();
            _output.WriteLine(text);

            JsonNode n = Json.Parse(text);

            Assert.Equal("creature", n["name"].AsString());
            Assert.True(n["alive"].AsBool());
            Assert.Equal(7, n["parts"].AsInt());
            Fixtures.AssertClose(12.5f, n["energy"].AsFloat(), 1e-9f);
            Assert.Equal(2, n["children"].Count);
            Assert.Equal(2, n["children"][1]["id"].AsInt());
            Assert.Equal("yes", n["nested"]["deep"].AsString());
        }

        [Fact]
        public void EmptyObjectsAndArraysRoundTrip()
        {
            var w = new Json.Writer();
            w.BeginObject();
            w.BeginArray("nothing").EndArray();
            w.BeginObject("empty").EndObject();
            w.EndObject();

            JsonNode n = Json.Parse(w.ToString());

            Assert.Equal(0, n["nothing"].Count);
            Assert.Equal(0, n["empty"].Count);
        }

        [Fact]
        public void StringsWithAwkwardCharactersRoundTrip()
        {
            string nasty = "quote\" backslash\\ newline\n tab\t control unicode ✓";

            var w = new Json.Writer();
            w.BeginObject().Field("s", nasty).EndObject();

            Assert.Equal(nasty, Json.Parse(w.ToString())["s"].AsString());
        }

        [Fact]
        public void AMissingFieldThrowsAndSaysWhatWasThere()
        {
            // Rather than defaulting. A field silently defaulted on load is the failure mode
            // this whole layer exists to avoid.
            JsonNode n = Json.Parse("{ \"a\": 1, \"b\": 2 }");

            FormatException e = Assert.Throws<FormatException>(() => n["missing"]);

            _output.WriteLine(e.Message);
            Assert.Contains("missing", e.Message);
            Assert.Contains("a", e.Message);
        }

        [Fact]
        public void AWrongTypeThrowsRatherThanCoercing()
        {
            JsonNode n = Json.Parse("{ \"a\": \"not a number\" }");
            Assert.Throws<FormatException>(() => n["a"].AsFloat());
        }

        [Fact]
        public void NonFiniteNumbersAreRefusedAtWriteTime()
        {
            // JSON has no NaN or Infinity. Writing one and silently substituting something else
            // would hide the fact that something upstream produced a broken value.
            var w = new Json.Writer();
            w.BeginObject();

            Assert.Throws<ArgumentException>(() => w.Field("bad", float.NaN));
            Assert.Throws<ArgumentException>(() => w.Field("bad", float.PositiveInfinity));
        }

        [Theory]
        [InlineData("{")]
        [InlineData("{ \"a\" 1 }")]
        [InlineData("[1, 2")]
        [InlineData("{ \"a\": }")]
        [InlineData("{} {}")]
        [InlineData("\"unterminated")]
        public void MalformedDocumentsAreRejected(string text)
        {
            Assert.ThrowsAny<FormatException>(() => Json.Parse(text));
        }

        [Fact]
        public void ScientificNotationParses()
        {
            JsonNode n = Json.Parse("{ \"a\": 1.5e-7, \"b\": -2E+3 }");

            Fixtures.AssertClose(1.5e-7f, n["a"].AsFloat(), 1e-12f);
            Fixtures.AssertClose(-2000f, n["b"].AsFloat(), 1e-6f);
        }

        [Fact]
        public void OptionalFieldsFallBackOnlyWhenAsked()
        {
            JsonNode n = Json.Parse("{ \"present\": 5 }");

            Fixtures.AssertClose(5f, n.OptionalFloat("present", 99f), 1e-9f);
            Fixtures.AssertClose(99f, n.OptionalFloat("absent", 99f), 1e-9f);
        }
    }
}
