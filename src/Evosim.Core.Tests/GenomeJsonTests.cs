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

        /// <summary>
        /// A snapshot row can carry the body's own plan beside the genome — the module counts and
        /// the parts a bite took — and the genome reader is untouched by it. Written from 2026-09-22
        /// night so that a picture drawn from a row is the body the run was stepping (round 44
        /// seed 1's fourteen-metre leaf was invisible in every picture for want of it).
        /// </summary>
        [Fact]
        public void ARowCanCarryTheBodysPlanBesideTheGenome()
        {
            Genome genome = GenomeFactory.Random(new Rng(7));
            var counts = new int[genome.Nodes.Count];
            for (int i = 0; i < counts.Length; i++) counts[i] = genome.Nodes[i].RecursiveLimit + i;
            var lost = new System.Collections.Generic.List<int[]> { new[] { 0, 0 }, new[] { 1, 0, 2, 1 } };

            string row = GenomeJson.Write(genome, id: 42, moduleCounts: counts, lostPartPaths: lost);

            Assert.DoesNotContain("\n", row);
            Assert.Equal(42L, GenomeJson.ReadId(row));
            Assert.Equal(counts, GenomeJson.ReadModuleCounts(row));
            Assert.Equal(lost, GenomeJson.ReadLostPartPaths(row));
            Assert.Equal(GenomeJson.Write(genome), GenomeJson.Write(GenomeJson.Read(row)));

            // A row without the plan reads as none, which is every earlier recording.
            string bare = GenomeJson.Write(genome, id: 42);
            Assert.Null(GenomeJson.ReadModuleCounts(bare));
            Assert.Null(GenomeJson.ReadLostPartPaths(bare));

            // Empty is omitted, not written as an empty array: a body that never moved a count and
            // never lost a part carries nothing.
            Assert.Equal(bare, GenomeJson.Write(genome, id: 42, moduleCounts: new int[0],
                                                lostPartPaths: new System.Collections.Generic.List<int[]>()));
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
            g.Reproduction = new ReproductionTraits
            {
                BroodSize = 5, BirthInvestment = 0.4375f, ReserveMargin = 312.5f,
            };
            g.AdultScale = 1.375f;

            Genome back = GenomeJson.Read(GenomeJson.Write(g));

            Assert.Equal(5, back.Reproduction.BroodSize);
            Fixtures.AssertClose(0.4375f, back.Reproduction.BirthInvestment, 0f);
            Fixtures.AssertClose(312.5f, back.Reproduction.ReserveMargin, 0f);
            Fixtures.AssertClose(1.375f, back.AdultScale, 0f);
        }

        [Fact]
        public void TheBreedingMarginIsWrittenAndAFormatFiveGenomeIsRefusedByName()
        {
            // D098 §3's bump, and §9's rule at the file level. A format-5 genome carries no
            // margin, and the field that would be defaulted is the one the gene exists to let a
            // lineage move off — every stored creature would come back as the eager strategy
            // wearing its own name, which is the silent default this project refuses.
            Genome g = GenomeFactory.Random(new Rng(12));
            ReproductionTraits traits = g.Reproduction;
            traits.ReserveMargin = 125f;
            g.Reproduction = traits;

            string text = GenomeJson.Write(g);

            Assert.Contains("\"margin\":125", text);

            string old = text.Replace($"\"format\":{GenomeJson.FormatVersion}", "\"format\":5");
            FormatException e = Assert.Throws<FormatException>(() => GenomeJson.Read(old));

            _output.WriteLine(e.Message);
            Assert.Contains("5", e.Message);
            Assert.Contains("ReserveMargin", e.Message);

            // The name of the bump that introduced the field, which is now one of three the
            // message walks back through rather than the newest. Capitalised as the sentence
            // that carries it is — the assertion tracks the wording it is checking, which is the
            // point of checking a message by name at all.
            Assert.Contains("Format 6", e.Message);
        }

        [Fact]
        public void AGenomeMissingItsMarginIsRefusedRatherThanDefaulted()
        {
            string text = GenomeJson.Write(GenomeFactory.Random(new Rng(13)))
                .Replace("\"margin\":", "\"keep\":");

            Assert.ThrowsAny<FormatException>(() => GenomeJson.Read(text));
        }

        // ------------------------------------------------- the module gene (D106 item 2, format 7)

        [Fact]
        public void TheModuleGeneIsWrittenAndAFormatSixGenomeIsRefusedByName()
        {
            // D106's bump, and §9's rule at the file level. A format-6 genome says nothing about
            // whether a node's count is fixed or is a rule, and the field that would be defaulted
            // is the one the round exists to watch: every stored creature would come back
            // determinate wearing its own name, which is the silent default this project refuses.
            Genome g = GenomeFactory.Random(new Rng(14));
            g.Nodes[0].Growth = ModuleGrowth.Indeterminate;
            g.Nodes[0].MaxModules = 5;

            string text = GenomeJson.Write(g);

            Assert.Contains("\"growth\":\"Indeterminate\"", text);
            Assert.Contains("\"maxModules\":5", text);
            Assert.Contains($"\"format\":{GenomeJson.FormatVersion}", text);

            Genome back = GenomeJson.Read(text);
            Assert.Equal(ModuleGrowth.Indeterminate, back.Nodes[0].Growth);
            Assert.Equal(5, back.Nodes[0].MaxModules);

            string old = text.Replace($"\"format\":{GenomeJson.FormatVersion}", "\"format\":6");
            FormatException e = Assert.Throws<FormatException>(() => GenomeJson.Read(old));

            _output.WriteLine(e.Message);
            Assert.Contains("format 6", e.Message);
            Assert.Contains("Growth", e.Message);
            Assert.Contains("MaxModules", e.Message);
        }

        [Fact]
        public void AFounderIsDeterminateAtEveryNodeWithItsCeilingAtItsOwnLimit()
        {
            // Rule 6, at the one place it can be checked cheaply: the founding lottery. The gene
            // enters round 44's world by mutation alone, so a founder that drew anything else
            // would make the round's own question unaskable.
            for (ulong seed = 1; seed <= 40; seed++)
            {
                Genome g = GenomeFactory.Random(new Rng(seed));

                foreach (MorphNode node in g.Nodes)
                {
                    Assert.Equal(ModuleGrowth.Determinate, node.Growth);
                    Assert.Equal(node.RecursiveLimit, node.MaxModules);
                    // D106 item 3's founder rule (mouth-spec rule 1), which round 45's build
                    // turned on: attack and protection at zero on every node — the owner's
                    // ruling that a world starts with nothing armed — toughness at the neutral 1,
                    // and intake at the node's own cell type's cap, which is zero everywhere but
                    // a consumer. No draw is taken for any of them.
                    Assert.Equal(0f, node.Attack);
                    Assert.Equal(0f, node.Protection);
                    Assert.Equal(1f, node.Toughness);
                    Assert.Equal(
                        CellTypeRegistry.Standard.Resolve(node.CellTypeId).IntakeMax, node.Intake);
                }
            }
        }

        [Theory]
        [InlineData("\"growth\":", "\"grew\":")]
        [InlineData("\"maxModules\":", "\"maxMods\":")]
        [InlineData("\"toughness\":", "\"tough\":")]
        public void AGenomeMissingOneOfTheNewFieldsIsRefusedRatherThanDefaulted(
            string field, string renamed)
        {
            string text = GenomeJson.Write(GenomeFactory.Random(new Rng(15)))
                .Replace(field, renamed);

            Assert.ThrowsAny<FormatException>(() => GenomeJson.Read(text));
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

        // ------------------------------------------------- the snapshot row's creature id (D075)

        [Fact]
        public void TheCreatureIdIsTheFirstFieldAndSurvivesTheRoundTrip()
        {
            // First, and literally: a reader scanning a large snapshot for one creature should
            // not have to parse a whole genome to find out whose it is.
            Genome g = GenomeFactory.Random(new Rng(7));
            string row = GenomeJson.Write(g, indent: false, id: 4821L);

            Assert.StartsWith("{\"id\":4821,\"format\":", row);
            Assert.Equal(4821L, GenomeJson.ReadId(row));

            // The genome is unchanged by carrying one: the id belongs to the organism, and the
            // same recipe is shared by every creature that develops it.
            Assert.Equal(GenomeJson.Write(g), GenomeJson.Write(GenomeJson.Read(row)));
        }

        [Fact]
        public void ARowWithNoIdReadsNoId()
        {
            // A genome on its own — a founder pool entry, an inoculum, a fixture — belongs to no
            // organism, and must not come back claiming to be creature 0.
            string row = GenomeJson.Write(GenomeFactory.Random(new Rng(8)));

            Assert.DoesNotContain("\"id\":", row);
            Assert.Equal(GenomeJson.NoId, GenomeJson.ReadId(row));
        }

        [Fact]
        public void TheFormatBeforeTheIdIsRefusedByName()
        {
            // §9's refuse-rather-than-default rule, at the file level: a format-3 snapshot has no
            // ids to join on, and reading it anyway would produce a viewer full of anonymous
            // creatures that looked exactly like a viewer full of identified ones.
            string text = GenomeJson.Write(GenomeFactory.Random(new Rng(9)))
                .Replace($"\"format\":{GenomeJson.FormatVersion}", "\"format\":3");

            FormatException e = Assert.Throws<FormatException>(() => GenomeJson.Read(text));

            _output.WriteLine(e.Message);
            Assert.Contains("3", e.Message);
            Assert.Contains(GenomeJson.FormatVersion.ToString(), e.Message);
        }
    }
}
