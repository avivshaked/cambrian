using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D233's genes piece: the family's form in the genome file (formats 13 and 14), in mutation
    /// (<see cref="MutationRates.FormChance"/>, entry and exit), in the world's shape list
    /// (<see cref="RunConfig.ShapeIds"/>) and in the species distance. The old formats and the old
    /// stream must be what they were, to the byte and to the draw.
    /// </summary>
    public class ShapeGenesTests
    {
        private readonly ITestOutputHelper _output;

        public ShapeGenesTests(ITestOutputHelper output) => _output = output;

        private static readonly PartForm Egg = new PartForm(0.6f, 1f, 1.5f);

        // ---------------------------------------------------------------- fixtures

        private static Genome FamilyGenome(PartForm form, bool withChild)
        {
            var genome = new Genome { RootIndex = 0 };

            MorphNode root = Fixtures.Box(0.3f);
            root.ShapeId = ShapeIds.Superellipsoid;
            root.Form = form;
            root.Dimensions = new Float3(0.2f, 0.45f, 0.15f);
            genome.Nodes.Add(root);

            if (withChild)
            {
                genome.Nodes.Add(Fixtures.Box(0.15f));

                MorphEdge edge = Fixtures.FaceToFace(1);
                edge.ParentAnchor = new Float3(0.6f, 0.8f, 0.4f);
                root.Edges.Add(edge);
            }

            return genome;
        }

        /// <summary>The family genome with a node of another shape in place of its root.</summary>
        private static Genome TwinWithShape(Genome family, string shapeId)
        {
            Genome twin = family.Clone();
            twin.Nodes[0].ShapeId = shapeId;
            twin.Nodes[0].Form = PartForm.None;
            return twin;
        }

        /// <summary>Every chance at 0, so a test turns on only the operator it is about.</summary>
        private static MutationRates SilentRates()
        {
            var rates = new MutationRates();
            foreach (PropertyInfo property in typeof(MutationRates).GetProperties())
            {
                if (property.PropertyType == typeof(float) && property.Name.EndsWith("Chance"))
                {
                    property.SetValue(rates, 0f);
                }
            }
            return rates;
        }

        // ---------------------------------------------------------------- formats 13 and 14

        [Fact]
        public void AFamilyGenomeIsFormat13AndRoundTripsToIdenticalText()
        {
            Genome genome = FamilyGenome(Egg, withChild: true);

            Assert.Equal(GenomeJson.FamilyFormatVersion, GenomeJson.FormatFor(genome));

            string text = GenomeJson.Write(genome);
            _output.WriteLine(text.Length + " characters");

            Assert.Contains("\"format\":13", text);
            Assert.Contains("\"shape\":\"superellipsoid\",\"form\":{\"profile\":", text);

            Genome back = GenomeJson.Read(text);
            Assert.Equal(Egg, back.Nodes[0].Form);
            Assert.Equal(ShapeIds.Superellipsoid, back.Nodes[0].ShapeId);
            Assert.True(back.Nodes[1].Form.IsNone, "the named node of a family genome carries no form");
            Assert.Equal(text, GenomeJson.Write(back));
        }

        [Fact]
        public void AFamilyGenomeWithTheOneBrainIsFormat14AndRoundTrips()
        {
            Genome genome = FamilyGenome(Egg, withChild: true);
            genome.Brain = new CentralBrain();

            Assert.Equal(GenomeJson.FamilyCentralFormatVersion, GenomeJson.FormatFor(genome));

            string text = GenomeJson.Write(genome);
            Assert.Contains("\"format\":14", text);

            Genome back = GenomeJson.Read(text);
            Assert.NotNull(back.Brain);
            Assert.Equal(Egg, back.Nodes[0].Form);
            Assert.Equal(text, GenomeJson.Write(back));
        }

        [Fact]
        public void AGenomeWithNoFamilyNodeIsStillFormat11Or12WithNoFormKey()
        {
            Genome plain = Fixtures.SingleBox();
            string text = GenomeJson.Write(plain);

            Assert.Equal(GenomeJson.FormatVersion, GenomeJson.FormatFor(plain));
            Assert.Contains("\"format\":11", text);
            Assert.DoesNotContain("\"form\"", text);
            Assert.Equal(text, GenomeJson.Write(GenomeJson.Read(text)));

            Genome central = Fixtures.SingleBox();
            central.Brain = new CentralBrain();
            string centralText = GenomeJson.Write(central);

            Assert.Equal(GenomeJson.CentralFormatVersion, GenomeJson.FormatFor(central));
            Assert.Contains("\"format\":12", centralText);
            Assert.DoesNotContain("\"form\"", centralText);
            Assert.Equal(centralText, GenomeJson.Write(GenomeJson.Read(centralText)));
        }

        [Fact]
        public void EveryRandomGenomeStillWritesFormat11AndRoundTrips()
        {
            for (ulong seed = 1; seed <= 100; seed++)
            {
                Genome genome = GenomeFactory.Random(new Rng(seed));
                string text = GenomeJson.Write(genome);

                Assert.Contains("\"format\":11", text);
                Assert.DoesNotContain("\"form\"", text);
                Assert.Equal(text, GenomeJson.Write(GenomeJson.Read(text)));
            }
        }

        [Fact]
        public void EveryGenomeFileUnderInoculaReadsAndWritesBack()
        {
            string root = Path.GetFullPath(Path.Combine(
                Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "inocula"));

            if (!Directory.Exists(root))
            {
                _output.WriteLine($"no inocula at {root}; skipped");
                return;
            }

            int genomes = 0, identical = 0, refused = 0;
            foreach (string file in Directory.GetFiles(root, "*.json", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);

                JsonNode node;
                try { node = Json.Parse(text); }
                catch (Exception) { continue; }

                // A config or some other file kept beside the genomes is not one.
                if (!node.Has("format") || !node.Has("nodes")) continue;

                int format = node["format"].AsInt();
                Genome genome;
                try { genome = GenomeJson.Read(text); }
                catch (FormatException)
                {
                    // A genome of a format this build never read is not what this test is about.
                    Assert.True(format < GenomeJson.FormatVersion, $"{file}: format {format} refused");
                    refused++;
                    continue;
                }

                genomes++;
                Assert.Equal(format, GenomeJson.FormatFor(genome));
                Assert.DoesNotContain("\"form\"", text);

                string written = GenomeJson.Write(
                    genome, indent: false, id: GenomeJson.ReadId(text),
                    moduleCounts: GenomeJson.ReadModuleCounts(text),
                    lostPartPaths: GenomeJson.ReadLostPartPaths(text));

                // The inocula were written by many builds, and the older ones printed a float with more
                // digits than the shortest text that reads back to it (0.434093744 where this writer
                // prints 0.43409374), so a stored file is not always this writer's text. What must hold:
                // every one reads, keeps its format, and this writer's text comes back from itself byte
                // for byte. The count of files already in this writer's text is written for the reader.
                string again = GenomeJson.Write(
                    GenomeJson.Read(written), indent: false, id: GenomeJson.ReadId(written),
                    moduleCounts: GenomeJson.ReadModuleCounts(written),
                    lostPartPaths: GenomeJson.ReadLostPartPaths(written));
                Assert.True(written == again, $"{file}: this writer's text did not come back from itself");
                if (text.Trim() == written) identical++;
            }

            _output.WriteLine($"{genomes} genomes read, {identical} already in this writer's text, {refused} of older formats");
        }

        // ---------------------------------------------------------------- refusals

        private static string FamilyText() => GenomeJson.Write(FamilyGenome(Egg, withChild: true));

        private static string RemoveForm(string text) =>
            Regex.Replace(text, "\"form\":\\{[^}]*\\},", "");

        private static FormatException ReadRefusal(string text) =>
            Assert.Throws<FormatException>(() => GenomeJson.Read(text));

        [Fact]
        public void Format11RefusesAFormKey()
        {
            // A named node of format 11 carrying a form key.
            string plain = GenomeJson.Write(Fixtures.SingleBox());
            string withForm = plain.Replace(
                "\"shape\":\"box\",", "\"shape\":\"box\",\"form\":{\"profile\":0.5,\"section\":1,\"taper\":1},");
            Assert.NotEqual(plain, withForm);

            FormatException e = ReadRefusal(withForm);
            Assert.Contains("Node 0", e.Message);
            Assert.Contains("cannot carry a form", e.Message);
        }

        [Fact]
        public void Format11And12RefuseAFamilyShapeId()
        {
            string plain = GenomeJson.Write(Fixtures.SingleBox());
            string family = plain.Replace("\"shape\":\"box\"", "\"shape\":\"superellipsoid\"");
            Assert.NotEqual(plain, family);

            FormatException e = ReadRefusal(family);
            Assert.Contains("Node 0", e.Message);
            Assert.Contains("takes a form, which a format-11", e.Message);

            Genome central = Fixtures.SingleBox();
            central.Brain = new CentralBrain();
            string centralFamily = GenomeJson.Write(central).Replace(
                "\"shape\":\"box\"", "\"shape\":\"superellipsoid\"");

            e = ReadRefusal(centralFamily);
            Assert.Contains("format-12", e.Message);
        }

        [Fact]
        public void Format13RefusesAFamilyNodeWithNoForm()
        {
            string text = RemoveForm(FamilyText());
            Assert.DoesNotContain("\"form\"", text);

            FormatException e = ReadRefusal(text);
            Assert.Contains("Node 0", e.Message);
            Assert.Contains("takes a form and the node carries none", e.Message);
        }

        [Fact]
        public void Format13RefusesANamedNodeWithAForm()
        {
            string text = FamilyText();

            // The second node is the box; give it a form.
            string withForm = text.Replace(
                "\"shape\":\"box\",", "\"shape\":\"box\",\"form\":{\"profile\":0.5,\"section\":1,\"taper\":1},");
            Assert.NotEqual(text, withForm);

            FormatException e = ReadRefusal(withForm);
            Assert.Contains("Node 1", e.Message);
            Assert.Contains("takes no form and the node carries one", e.Message);
        }

        [Fact]
        public void Format13RefusesAFormOutsideTheRanges()
        {
            string text = FamilyText();

            foreach (string outside in new[]
            {
                "\"profile\":2,\"section\":1,\"taper\":1.5",
                "\"profile\":0.6,\"section\":0.01,\"taper\":1.5",
                "\"profile\":0.6,\"section\":1,\"taper\":9",
            })
            {
                string bad = Regex.Replace(
                    text, "\"profile\":[^}]*\\}", outside + "}");

                FormatException e = ReadRefusal(bad);
                Assert.Contains("Node 0", e.Message);
                Assert.Contains("outside the ranges", e.Message);
            }
        }

        [Fact]
        public void Format13And14RefuseAGenomeWithNeitherExtension()
        {
            // D241: 13 and 14 hold either extension, so the key the gene is written under is present (as
            // null) and the refusal is the one for a file that holds neither a family node nor the gene.
            string plain = GenomeJson.Write(Fixtures.SingleBox());
            FormatException e = ReadRefusal(
                plain.Replace("\"format\":11", "\"format\":13")
                     .Replace("\"lifespan\":1,", "\"lifespan\":1,\"growthReserve\":null,"));
            Assert.Contains("it has neither", e.Message);

            Genome central = Fixtures.SingleBox();
            central.Brain = new CentralBrain();
            e = ReadRefusal(
                GenomeJson.Write(central).Replace("\"format\":12", "\"format\":14")
                     .Replace("\"lifespan\":1,", "\"lifespan\":1,\"growthReserve\":null,"));
            Assert.Contains("it has neither", e.Message);
        }

        [Fact]
        public void TheUnknownFormatMessageNamesFormats13And14()
        {
            string text = FamilyText().Replace("\"format\":13", "\"format\":99");

            FormatException e = ReadRefusal(text);
            Assert.Contains("13", e.Message);
            Assert.Contains("14", e.Message);
        }

        [Fact]
        public void TheWriterRefusesWhatTheReaderRefuses()
        {
            Genome none = FamilyGenome(PartForm.None, withChild: false);
            InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => GenomeJson.Write(none));
            Assert.Contains("node 0", e.Message);
            Assert.Contains("carries none", e.Message);

            Genome outside = FamilyGenome(new PartForm(0.6f, 1f, 9f), withChild: false);
            e = Assert.Throws<InvalidOperationException>(() => GenomeJson.Write(outside));
            Assert.Contains("outside the ranges", e.Message);

            Genome named = Fixtures.SingleBox();
            named.Nodes[0].Form = Egg;
            e = Assert.Throws<InvalidOperationException>(() => GenomeJson.Write(named));
            Assert.Contains("takes no form", e.Message);
        }

        // ---------------------------------------------------------------- mutation

        [Fact]
        public void AtFormChanceZeroAFamilyNodeKeepsItsFormAndTakesNoExtraDraw()
        {
            Genome family = FamilyGenome(Egg, withChild: true);
            Genome twin = TwinWithShape(family, ShapeIds.Box);

            MutationRates rates = SilentRates();
            Assert.Equal(0f, rates.FormChance);

            for (ulong seed = 1; seed <= 50; seed++)
            {
                var rngFamily = new Rng(seed);
                var rngTwin = new Rng(seed);

                Genome child = Mutator.Mutate(family, rngFamily, rates, shapes: PartShapeRegistry.Catalogue);
                Mutator.Mutate(twin, rngTwin, rates, shapes: PartShapeRegistry.Catalogue);

                Assert.Equal(Egg, child.Nodes[0].Form);
                Assert.Equal(rngTwin.NextUInt(), rngFamily.NextUInt());
            }
        }

        [Fact]
        public void AFormChanceOnAPartThatTakesNoFormDrawsNothing()
        {
            Genome box = TwinWithShape(FamilyGenome(Egg, withChild: true), ShapeIds.Box);

            MutationRates off = SilentRates();
            MutationRates on = SilentRates();
            on.FormChance = 1f;

            for (ulong seed = 1; seed <= 50; seed++)
            {
                var rngOff = new Rng(seed);
                var rngOn = new Rng(seed);

                Mutator.Mutate(box, rngOff, off, shapes: PartShapeRegistry.Catalogue);
                Mutator.Mutate(box, rngOn, on, shapes: PartShapeRegistry.Catalogue);

                Assert.Equal(rngOff.NextUInt(), rngOn.NextUInt());
            }
        }

        [Fact]
        public void AtFormChanceOneTheFormChangesAndStaysInRange()
        {
            Genome family = FamilyGenome(Egg, withChild: false);

            MutationRates rates = SilentRates();
            rates.FormChance = 1f;

            for (ulong seed = 1; seed <= 200; seed++)
            {
                var rng = new Rng(seed);
                Genome child = Mutator.Mutate(family, rng, rates, shapes: PartShapeRegistry.Catalogue);

                PartForm form = child.Nodes[0].Form;
                Assert.NotEqual(Egg, form);
                Assert.True(form.InRange, $"seed {seed}: {form}");
                Assert.Equal(Egg, family.Nodes[0].Form);

                // Three Gaussians were drawn on top of the stream a form-less child takes, and the
                // draw sits where the design puts it, so the stream is not the silent one's.
                var silent = new Rng(seed);
                Mutator.Mutate(family, silent, SilentRates(), shapes: PartShapeRegistry.Catalogue);
                Assert.NotEqual(silent.NextUInt(), rng.NextUInt());
            }
        }

        /// <summary>
        /// Single steps of every gene from one starting form, one Mutate each, for the range tests.
        /// </summary>
        private static List<PartForm> Steps(PartForm start, int count)
        {
            Genome family = FamilyGenome(start, withChild: false);

            MutationRates rates = SilentRates();
            rates.FormChance = 1f;

            var forms = new List<PartForm>(count);
            for (int seed = 1; seed <= count; seed++)
            {
                forms.Add(Mutator.Mutate(
                    family, new Rng((ulong)seed), rates, shapes: PartShapeRegistry.Catalogue).Nodes[0].Form);
            }
            return forms;
        }

        private static double MeanLogDisplacement(List<PartForm> forms, Func<PartForm, float> gene, float start) =>
            forms.Average(f => Math.Log(gene(f)) - Math.Log(start));

        [Fact]
        public void ARangeEndIsReflectedBackInAndAStepFromTheCentreIsSymmetric()
        {
            const int Count = 100000;

            var low = new PartForm(PartForm.ProfileMin, PartForm.SectionMin, PartForm.TaperMin);
            var high = new PartForm(PartForm.ProfileMax, PartForm.SectionMax, PartForm.TaperMax);

            List<PartForm> fromLow = Steps(low, Count);
            List<PartForm> fromHigh = Steps(high, Count);

            Assert.All(fromLow, f => Assert.True(f.InRange, f.ToString()));
            Assert.All(fromHigh, f => Assert.True(f.InRange, f.ToString()));

            // Reflection, not clamping: a step off the low end lands inside, on average above it, and
            // a step off the high end below it. A clamp would pile every outward step on the end.
            Assert.True(MeanLogDisplacement(fromLow, f => f.Profile, low.Profile) > 0.05);
            Assert.True(MeanLogDisplacement(fromLow, f => f.Section, low.Section) > 0.05);
            Assert.True(MeanLogDisplacement(fromLow, f => f.Taper, low.Taper) > 0.05);
            Assert.True(MeanLogDisplacement(fromHigh, f => f.Profile, high.Profile) < -0.05);
            Assert.True(MeanLogDisplacement(fromHigh, f => f.Section, high.Section) < -0.05);
            Assert.True(MeanLogDisplacement(fromHigh, f => f.Taper, high.Taper) < -0.05);
            Assert.True(fromLow.Count(f => f.Profile == PartForm.ProfileMin) < Count / 100,
                "a clamp would leave a large share of steps on the end");

            // From the middle of each range, on the log, a step is as likely up as down: the mean of
            // 100,000 steps of standard deviation 0.15 is within 0.003 six times in a hundred million.
            float profile = (float)Math.Exp((Math.Log(PartForm.ProfileMin) + Math.Log(PartForm.ProfileMax)) / 2d);
            float section = (float)Math.Exp((Math.Log(PartForm.SectionMin) + Math.Log(PartForm.SectionMax)) / 2d);
            float taper = (float)Math.Exp((Math.Log(PartForm.TaperMin) + Math.Log(PartForm.TaperMax)) / 2d);

            List<PartForm> fromCentre = Steps(new PartForm(profile, section, taper), Count);
            Assert.All(fromCentre, f => Assert.True(f.InRange, f.ToString()));

            double meanProfile = MeanLogDisplacement(fromCentre, f => f.Profile, profile);
            double meanSection = MeanLogDisplacement(fromCentre, f => f.Section, section);
            double meanTaper = MeanLogDisplacement(fromCentre, f => f.Taper, taper);
            _output.WriteLine($"mean log step from the centre: profile {meanProfile:F5}, section {meanSection:F5}, taper {meanTaper:F5}");

            Assert.True(Math.Abs(meanProfile) < 0.003, $"profile {meanProfile}");
            Assert.True(Math.Abs(meanSection) < 0.003, $"section {meanSection}");
            Assert.True(Math.Abs(meanTaper) < 0.003, $"taper {meanTaper}");
        }

        [Fact]
        public void EnteringTheFamilyTakesTheNearestMemberWithNoDraw()
        {
            var expected = new Dictionary<string, PartForm>
            {
                [ShapeIds.Box] = new PartForm(0.1f, 0.1f, 1f),
                [ShapeIds.Sphere] = new PartForm(1f, 1f, 1f),
                [ShapeIds.Capsule] = new PartForm(0.5f, 1f, 1f),
            };

            MutationRates rates = SilentRates();
            rates.ShapeChance = 1f;

            foreach (KeyValuePair<string, PartForm> pair in expected)
            {
                Genome parent = TwinWithShape(FamilyGenome(Egg, withChild: false), pair.Key);

                int entered = 0;
                for (ulong seed = 1; seed <= 60; seed++)
                {
                    var rng = new Rng(seed);
                    Genome child = Mutator.Mutate(parent, rng, rates, shapes: PartShapeRegistry.Catalogue);
                    MorphNode node = child.Nodes[0];

                    Assert.NotEqual(pair.Key, node.ShapeId);
                    if (node.ShapeId == ShapeIds.Superellipsoid)
                    {
                        Assert.Equal(pair.Value, node.Form);
                        entered++;
                    }
                    else
                    {
                        Assert.True(node.Form.IsNone);
                    }

                    // No draw for the form: a world without the family takes the same number of
                    // draws from the same seed (one pick, whatever the length of its list), whether or
                    // not this one's pick entered the family.
                    var standard = new Rng(seed);
                    Mutator.Mutate(parent, standard, rates, shapes: PartShapeRegistry.Standard);
                    Assert.Equal(standard.NextUInt(), rng.NextUInt());
                }

                _output.WriteLine($"{pair.Key}: entered the family {entered} times in 60");
                Assert.True(entered > 0, $"{pair.Key} never entered the family");
            }
        }

        [Fact]
        public void LeavingTheFamilyClearsTheForm()
        {
            Genome parent = FamilyGenome(Egg, withChild: true);

            MutationRates rates = SilentRates();
            rates.ShapeChance = 1f;

            var landed = new HashSet<string>();
            for (ulong seed = 1; seed <= 60; seed++)
            {
                Genome child = Mutator.Mutate(parent, new Rng(seed), rates, shapes: PartShapeRegistry.Catalogue);
                MorphNode root = child.Nodes[0];

                Assert.NotEqual(ShapeIds.Superellipsoid, root.ShapeId);
                Assert.True(root.Form.IsNone, "the form must go with the shape");
                landed.Add(root.ShapeId);
            }

            Assert.Equal(3, landed.Count);
        }

        [Fact]
        public void TheShapeDrawPicksFromTheWorldsList()
        {
            // A world that allows two shapes never draws a third, and a family world's draw reaches
            // the family. With the default list the draws are the three shapes' as they were.
            Genome parent = Fixtures.SingleBox();

            MutationRates rates = SilentRates();
            rates.ShapeChance = 1f;

            var two = new PartShapeRegistry(new BoxShape(), new SphereShape());
            for (ulong seed = 1; seed <= 40; seed++)
            {
                Assert.Equal(ShapeIds.Sphere, Mutator.Mutate(parent, new Rng(seed), rates, shapes: two).Nodes[0].ShapeId);
            }

            var seen = new HashSet<string>();
            for (ulong seed = 1; seed <= 80; seed++)
            {
                seen.Add(Mutator.Mutate(parent, new Rng(seed), rates, shapes: PartShapeRegistry.Catalogue).Nodes[0].ShapeId);
            }
            Assert.Contains(ShapeIds.Superellipsoid, seen);

            for (ulong seed = 1; seed <= 40; seed++)
            {
                string byDefault = Mutator.Mutate(parent, new Rng(seed), rates).Nodes[0].ShapeId;
                string byStandard = Mutator.Mutate(parent, new Rng(seed), rates, shapes: PartShapeRegistry.Standard).Nodes[0].ShapeId;
                Assert.Equal(byStandard, byDefault);
                Assert.NotEqual(ShapeIds.Superellipsoid, byDefault);
            }
        }

        // ---------------------------------------------------------------- config

        [Fact]
        public void AFourShapeConfigSavesAndReloadsToTheSameHashAndShapes()
        {
            var config = new RunConfig
            {
                ShapeIds = new[] { ShapeIds.Box, ShapeIds.Sphere, ShapeIds.Capsule, ShapeIds.Superellipsoid },
            };

            string text = RunConfigJson.Write(config);
            Assert.Contains("\"shapeIds\"", text);

            RunConfig back = RunConfigJson.Read(text, out string mismatch);

            Assert.Null(mismatch);
            Assert.Equal(config.Hash(), back.Hash());
            Assert.Equal(config.ShapeIds, back.ShapeIds);
            Assert.Equal(new[] { "box", "sphere", "capsule", "superellipsoid" }, back.Shapes.Ids().ToArray());
            Assert.Equal(config.Shapes.HashContribution(), back.Shapes.HashContribution());

            // And the default list is the default world's, in the default world's hash.
            Assert.Equal(PartShapeRegistry.Standard.HashContribution(), new RunConfig().Shapes.HashContribution());
            Assert.NotEqual(new RunConfig().Hash(), config.Hash());
        }

        [Fact]
        public void AnUnknownOrRepeatedShapeIdIsRefused()
        {
            var unknown = new RunConfig { ShapeIds = new[] { ShapeIds.Box, "dodecahedron" } };
            ArgumentException e = Assert.Throws<ArgumentException>(() => unknown.Shapes);
            Assert.Contains("dodecahedron", e.Message);
            Assert.Contains(ShapeIds.Superellipsoid, e.Message);
            Assert.Throws<ArgumentException>(() => unknown.Hash());

            var repeated = new RunConfig { ShapeIds = new[] { ShapeIds.Box, ShapeIds.Box } };
            e = Assert.Throws<ArgumentException>(() => repeated.Shapes);
            Assert.Contains("Duplicate", e.Message);

            Assert.Throws<ArgumentException>(() => new RunConfig { ShapeIds = Array.Empty<string>() }.Shapes);
            Assert.Throws<ArgumentNullException>(() => new RunConfig { ShapeIds = null });

            // A file naming an id the build does not know stops the load, rather than loading the
            // standard three under another list's hash.
            var four = new RunConfig
            {
                ShapeIds = new[] { ShapeIds.Box, ShapeIds.Sphere, ShapeIds.Capsule, ShapeIds.Superellipsoid },
            };
            string text = RunConfigJson.Write(four).Replace("\"superellipsoid\"", "\"dodecahedron\"");
            Assert.Throws<ArgumentException>(() => RunConfigJson.Read(text));
        }

        [Fact]
        public void TheBuiltListIsCachedAndRebuiltWhenTheIdsChange()
        {
            var config = new RunConfig();

            PartShapeRegistry first = config.Shapes;
            Assert.Same(first, config.Shapes);
            Assert.Equal(3, first.Count);

            // The array's contents, edited in place, are what is compared, not its reference.
            config.ShapeIds[2] = ShapeIds.Superellipsoid;
            PartShapeRegistry second = config.Shapes;
            Assert.NotSame(first, second);
            Assert.Equal(new[] { "box", "sphere", "superellipsoid" }, second.Ids().ToArray());
            Assert.Same(second, config.Shapes);

            // The registry already handed out is not edited under its holder.
            Assert.Equal(new[] { "box", "sphere", "capsule" }, first.Ids().ToArray());
        }

        // ---------------------------------------------------------------- species distance

        [Fact]
        public void OppositeCornersOfTheFormAreOneUnitEqualFormsZeroAndAFamilyNodeAgainstABoxOne()
        {
            Genome low = FamilyGenome(
                new PartForm(PartForm.ProfileMin, PartForm.SectionMin, PartForm.TaperMin), withChild: false);
            Genome high = FamilyGenome(
                new PartForm(PartForm.ProfileMax, PartForm.SectionMax, PartForm.TaperMax), withChild: false);

            // The parameter term alone.
            Fixtures.AssertClose(1f, SpeciesDistance.Between(low, high, 0f, 0f, 1f, 0f), 1e-5f);
            Assert.Equal(0f, SpeciesDistance.Between(low, low.Clone(), 0f, 0f, 1f, 0f));
            Assert.Equal(0f, SpeciesDistance.Between(low, low.Clone(), 0f, 1f, 1f, 0f));

            // A family node against a named one is the shape mismatch of the topology term, and no
            // form term besides.
            Genome box = TwinWithShape(low, ShapeIds.Box);
            Assert.Equal(1f, SpeciesDistance.Between(low, box, 0f, 1f, 0f, 0f));
            Assert.Equal(0f, SpeciesDistance.Between(low, box, 0f, 0f, 1f, 0f));
        }
    }
}
