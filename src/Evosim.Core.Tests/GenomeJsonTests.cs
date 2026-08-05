using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>Genome serialization — DESIGN.md §9.</summary>
    public class GenomeJsonTests
    {
        private readonly ITestOutputHelper _output;

        public GenomeJsonTests(ITestOutputHelper output) => _output = output;

        [Fact]
        public void RoundTripsEveryRandomGenome()
        {
            // Compares the re-serialized text rather than a hand-written list of fields, and
            // that is the point: a field added to MorphNode and forgotten in GenomeJson fails
            // here immediately. A checklist test would pass forever and let the format rot.
            for (ulong seed = 1; seed <= 300; seed++)
            {
                Genome original = GenomeFactory.Random(new Rng(seed));

                string first = GenomeJson.Write(original);
                string second = GenomeJson.Write(GenomeJson.Read(first));

                Assert.True(first == second, $"seed {seed} did not survive a round trip");
            }
        }

        [Fact]
        public void ReloadedGenomesDevelopIntoIdenticalBodies()
        {
            // The check that matters more than text equality: a genome that survives the round
            // trip on paper but grows into a different body would be measured as though it were
            // the original, and nothing downstream could tell.
            for (ulong seed = 1; seed <= 100; seed++)
            {
                var limits = DevelopmentLimits.Default;
                Genome original = GenomeFactory.RandomViable(
                    new Rng(seed), RandomGenomeOptions.Default, limits, minParts: 3);

                Phenotype a = Developer.Develop(original, limits);
                Phenotype b = Developer.Develop(GenomeJson.Read(GenomeJson.Write(original)), limits);

                Assert.Equal(a.PartCount, b.PartCount);
                Assert.Equal(a.TotalDof, b.TotalDof);

                for (int i = 0; i < a.PartCount; i++)
                {
                    Assert.Equal(a.Parts[i].CellTypeId, b.Parts[i].CellTypeId);
                    Assert.Equal(a.Parts[i].JointType, b.Parts[i].JointType);
                    Fixtures.AssertClose(a.Parts[i].Power, b.Parts[i].Power, 0f);
                    Fixtures.AssertClose(a.Parts[i].Position.X, b.Parts[i].Position.X, 0f);
                    Fixtures.AssertClose(a.Parts[i].Position.Y, b.Parts[i].Position.Y, 0f);
                    Fixtures.AssertClose(a.Parts[i].Position.Z, b.Parts[i].Position.Z, 0f);
                    Fixtures.AssertClose(a.Parts[i].HalfExtents.X, b.Parts[i].HalfExtents.X, 0f);
                }
            }
        }

        [Fact]
        public void ReloadedGenomesStayValid()
        {
            for (ulong seed = 1; seed <= 100; seed++)
            {
                Genome reloaded = GenomeJson.Read(GenomeJson.Write(GenomeFactory.Random(new Rng(seed))));
                Assert.Empty(reloaded.Validate());
            }
        }

        [Fact]
        public void ACompactGenomeIsExactlyOneLine()
        {
            // Because it is one row of lineage.jsonl. An embedded newline would split one
            // creature across two rows and corrupt every row after it.
            string text = GenomeJson.Write(GenomeFactory.Random(new Rng(1)));

            Assert.DoesNotContain("\n", text);
            Assert.DoesNotContain("\r", text);
            _output.WriteLine($"one genome, compact: {text.Length} bytes");
            _output.WriteLine(text.Substring(0, Math.Min(300, text.Length)) + "...");
        }

        [Fact]
        public void AnIndentedGenomeIsReadableAndStillParses()
        {
            Genome g = GenomeFactory.Random(new Rng(7));
            string pretty = GenomeJson.Write(g, indent: true);

            _output.WriteLine(pretty.Substring(0, Math.Min(800, pretty.Length)) + "\n...");

            Assert.Contains("\n", pretty);
            Assert.Equal(GenomeJson.Write(g), GenomeJson.Write(GenomeJson.Read(pretty)));
        }

        [Fact]
        public void EnumsAreStoredByNameNotOrdinal()
        {
            // Genomes outlive code. Inserting a member into an enum renumbers everything after
            // it, so a stored ordinal silently comes to mean something else; a name that no
            // longer resolves fails loudly instead.
            string text = GenomeJson.Write(GenomeFactory.Random(new Rng(3)));

            Assert.Contains("\"joint\":", text);
            Assert.DoesNotContain("\"joint\":0", text);
            Assert.True(
                text.Contains("Hinge") || text.Contains("Spherical") || text.Contains("Twist") ||
                text.Contains("Universal") || text.Contains("Fixed"),
                "no joint type name found in the serialized genome");
        }

        [Fact]
        public void AnUnknownEnumNameIsRefused()
        {
            // Corrupts whichever joint type this genome happens to carry, rather than assuming a
            // particular one — seed 1 need not contain a hinge, and a test that silently matches
            // nothing passes without testing anything.
            string original = GenomeJson.Write(GenomeFactory.Random(new Rng(1)));

            int at = original.IndexOf("\"joint\":\"", StringComparison.Ordinal);
            Assert.True(at >= 0, "no joint field found — the format changed under this test");

            int valueStart = at + "\"joint\":\"".Length;
            int valueEnd = original.IndexOf('"', valueStart);
            string text = original.Substring(0, valueStart) + "Elbow" + original.Substring(valueEnd);

            FormatException e = Assert.Throws<FormatException>(() => GenomeJson.Read(text));
            _output.WriteLine(e.Message);
        }

        [Fact]
        public void AMissingFieldIsRefusedRatherThanDefaulted()
        {
            string text = GenomeJson.Write(GenomeFactory.Random(new Rng(1)))
                .Replace("\"power\":", "\"pwr\":");

            Assert.ThrowsAny<FormatException>(() => GenomeJson.Read(text));
        }

        [Fact]
        public void AFutureFormatVersionIsRefusedWithAUsefulMessage()
        {
            string text = GenomeJson.Write(GenomeFactory.Random(new Rng(1)))
                .Replace($"\"format\":{GenomeJson.FormatVersion}", "\"format\":99");

            FormatException e = Assert.Throws<FormatException>(() => GenomeJson.Read(text));

            _output.WriteLine(e.Message);
            Assert.Contains("99", e.Message);
        }

        [Fact]
        public void ReproductionTraitsSurviveTheRoundTrip()
        {
            Genome g = GenomeFactory.Random(new Rng(11));
            g.Reproduction = new ReproductionTraits { BroodSize = 5, OffspringEndowment = 123.75f };

            Genome back = GenomeJson.Read(GenomeJson.Write(g));

            Assert.Equal(5, back.Reproduction.BroodSize);
            Fixtures.AssertClose(123.75f, back.Reproduction.OffspringEndowment, 0f);
        }

        [Fact]
        public void TypicalGenomeSizeIsRecorded()
        {
            // Not an assertion, a measurement — it is what decides whether storing whole genomes
            // per birth is affordable, and therefore whether the diff-and-keyframe scheme is
            // needed at all. Roughly 40,000 births an hour is the working estimate.
            long total = 0;
            for (ulong seed = 1; seed <= 100; seed++)
            {
                total += GenomeJson.Write(GenomeFactory.Random(new Rng(seed))).Length;
            }

            double mean = total / 100.0;
            _output.WriteLine($"mean compact genome: {mean:0} bytes");
            _output.WriteLine($"at 40,000 births/hour, whole genomes cost {mean * 40000 / 1e6:0.#} MB/hour");
        }
    }
}
