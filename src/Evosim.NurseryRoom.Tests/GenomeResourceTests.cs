using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Evosim.Core;
using Evosim.Nursery;
using Evosim.NurseryRoom;
using Xunit;

namespace Evosim.NurseryRoom.Tests
{
    /// <summary>
    /// The genome resources (nursery://genomes/{run}, nursery://genome/{run}/{id}) on synthetic run folders: a body and a brain computed with Core's
    /// own code, the ports named from Core, a port that names nothing, a lineage that is there or is not, and the change list of a mutated copy.
    /// Nothing swims: a run folder here is a few files of a few lines.
    /// </summary>
    public class GenomeResourceTests : IDisposable
    {
        private readonly string _dir = Where.Fresh("genomes");
        public void Dispose() => Directory.Delete(_dir, true);

        // ------------------------------------------------------------------ a synthetic genome

        internal static MorphNode Box(float half, JointType joint)
        {
            var node = new MorphNode
            {
                Dimensions = new Float3(half, half, half),
                JointType = joint,
                RecursiveLimit = 1,
                CellTypeId = joint == JointType.Fixed ? CellTypeIds.Structural : CellTypeIds.Link,
                Power = joint == JointType.Fixed ? 0f : 50f,
            };
            node.ResampleJointLimits(new Float2(-1f, 1f));
            return node;
        }

        /// <summary>A root with two hinge limbs, a one-brain: the first limb's angle and the second limb's smell drive the second limb's bend and the first's power.</summary>
        internal static Genome Synthetic(float weight = 0.5f)
        {
            var g = new Genome { RootIndex = 0 };
            g.Nodes.Add(Box(0.5f, JointType.Fixed));
            g.Nodes.Add(Box(0.3f, JointType.Hinge));
            g.Nodes.Add(Box(0.3f, JointType.Hinge));
            g.Nodes[0].Edges.Add(new MorphEdge { Child = 1, ParentAnchor = new Float3(1f, 0f, 0f), ChildAnchor = new Float3(-1f, 0f, 0f) });
            g.Nodes[0].Edges.Add(new MorphEdge { Child = 2, ParentAnchor = new Float3(-1f, 0f, 0f), ChildAnchor = new Float3(1f, 0f, 0f) });
            CentralWiring.Convert(g);
            Phenotype body = Developer.Develop(g);
            foreach (BrainOutput o in CentralWiring.Outputs(body)) g.Brain.Outputs.Add(new BrainUnit { Id = o.Id, Bias = 0.1f });
            g.Brain.Links.Add(new BrainLink { From = Port(body, 1, SensorChannel.JointAngle), To = Out(body, 1, BrainOutputKind.Power), Weight = weight });
            g.Brain.Links.Add(new BrainLink { From = Port(body, 2, SensorChannel.Chemical), To = Out(body, 2, BrainOutputKind.Bend), Weight = -0.25f });
            Assert.Empty(g.Brain.Validate());
            return g;
        }

        internal static ulong Port(Phenotype body, int part, SensorChannel channel) => BrainIds.Port(body.Parts[part].PathId, channel, 0);

        internal static ulong Out(Phenotype body, int part, BrainOutputKind kind) => BrainIds.Output(body.Parts[part].PathId, 0, kind);

        internal static string Row(Genome g) => GenomeJson.Write(g);

        // ------------------------------------------------------------------ a synthetic run

        /// <summary>
        /// Three generations of three members. Tops (by value): g0.1, g1.0, g2.0. Lineage: g1.0 is a child of g0.1; g2.0 a child of g1.7, which the run does not keep.
        /// The exam swam g2.0 and g2.5.
        /// </summary>
        internal sealed class RunMaker
        {
            public Genome G0 = Synthetic(), G1, G2, Exam2;
            public bool Scores = true, Lineage = true, Plan = true, Tsv = true;
            public string Dir;

            public RunMaker(string dir) { Dir = dir; G1 = Synthetic(0.75f); G2 = Synthetic(0.9f); Exam2 = Synthetic(-0.4f); }

            private static string Gen(int gen, int body, int born, double score) =>
                "{\"gen\":" + gen + ",\"seconds\":" + (gen * 60 + 60) + ",\"top\":{\"score\":" + score.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ",\"episodes\":4,\"body\":" + body + ",\"born\":" + born + "}}";

            private static string Members(int gen, double[] values, int topBody, int topBorn)
            {
                var sb = new StringBuilder("{\"gen\":" + gen + ",\"reward\":\"Net\",\"members\":[");
                for (int i = 0; i < values.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    bool top = values[i] == values.Max() && values.ToList().IndexOf(values[i]) == i;
                    sb.Append("{\"body\":" + (top ? topBody : 9) + ",\"born\":" + (top ? topBorn : 9) + ",\"episodes\":4,\"value\":" + values[i].ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ",\"parts\":{}}");
                }
                return sb.Append("]}").ToString();
            }

            public string Make()
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(Path.Combine(Dir, "tank-best.jsonl"), Row(G0) + "\n" + Row(G1) + "\n" + Row(G2) + "\n");
                File.WriteAllText(Path.Combine(Dir, "tank-generations.jsonl"), Gen(0, 0, 0, 5) + "\n" + Gen(1, 0, 1, 7) + "\n" + Gen(2, 0, 2, 8) + "\n");
                if (Scores)
                    File.WriteAllText(Path.Combine(Dir, "tank-scores.jsonl"), Members(0, new[] { 1.0, 5, 2 }, 0, 0) + "\n" + Members(1, new[] { 7.0, 3, 3 }, 0, 1) + "\n" + Members(2, new[] { 8.0, 1, 2 }, 0, 2) + "\n");
                if (Lineage)
                    File.WriteAllText(Path.Combine(Dir, TankLog.LineageFile),
                        TankLog.LineageRow(0, "g0.0", 0, 0, new string[0], false) + "\n" + TankLog.LineageRow(0, "g0.1", 0, 0, new string[0], false) + "\n" +
                        TankLog.LineageRow(1, "g1.0", 0, 1, new[] { "g0.1" }, false) + "\n" + TankLog.LineageRow(2, "g2.0", 0, 2, new[] { "g1.7" }, false) + "\n");
                if (Plan)
                    File.WriteAllText(Path.Combine(Dir, "run.json"), "{\"status\":\"completed\",\"exam\":{\"name\":\"exam\",\"brains\":[{\"label\":\"own\",\"kind\":\"own\"}],\"genomes\":[{\"id\":\"g2.0\",\"body\":0},{\"id\":\"g2.5\",\"body\":1}],\"episodes\":2,\"salt\":\"1\",\"seeds\":[\"1\",\"2\"]}}");
                if (Tsv)
                    File.WriteAllText(Path.Combine(Dir, "tank-exam-genomes.tsv"), "0\t" + Row(G2) + "\n1\t" + Row(Exam2) + "\n");
                return Dir;
            }
        }

        private ResourceResolver Resolver(string folder)
        {
            string rel = Path.GetRelativePath(Where.Root, folder).Replace('\\', '/');
            return new ResourceResolver(new RoomContext(Where.Root, Where.Settings(rel), Where.Root, Path.Combine(_dir, "none.jsonl")));
        }

        private static JsonElement Read(ResourceResolver r, string uri) => Js.Parse(Js.Serialize(r.Read(uri)));

        private (ResourceResolver r, string run, RunMaker m) Run(Action<RunMaker> tweak = null)
        {
            string folder = Path.Combine(_dir, "run" + Guid.NewGuid().ToString("N").Substring(0, 6));
            var m = new RunMaker(folder);
            tweak?.Invoke(m);
            m.Make();
            return (Resolver(folder), Path.GetFileName(folder), m);
        }

        // ------------------------------------------------------------------ the tests

        [Fact]
        public void ASyntheticGenomesBodyAndBrainAreWhatCoreDevelopsAndWires()
        {
            (ResourceResolver r, string run, RunMaker m) = Run();
            JsonElement g = Read(r, $"nursery://genome/{run}/g0.1");

            // The body is what Developer grows, part by part.
            Phenotype core = Developer.Develop(m.G0);
            JsonElement plan = g.GetProperty("bodyPlan");
            Assert.Equal(core.PartCount, plan.GetProperty("partCount").GetInt32());
            Assert.Equal(3, plan.GetProperty("parts").GetArrayLength());
            for (int i = 0; i < core.PartCount; i++)
            {
                JsonElement p = plan.GetProperty("parts")[i];
                PhenotypePart c = core.Parts[i];
                Assert.Equal(c.ShapeId, p.GetProperty("shape").GetString());
                Assert.Equal(c.HalfExtents.X, (float)p.GetProperty("halfExtents").GetProperty("x").GetDouble(), 5);
                Assert.Equal(c.Position.X, (float)p.GetProperty("position").GetProperty("x").GetDouble(), 5);
                Assert.Equal(c.Rotation.W, (float)p.GetProperty("rotation").GetProperty("w").GetDouble(), 5);
                if (i == 0) { Assert.Equal(JsonValueKind.Null, p.GetProperty("parent").ValueKind); Assert.Equal(JsonValueKind.Null, p.GetProperty("joint").ValueKind); }
                else
                {
                    Assert.Equal(c.ParentIndex, p.GetProperty("parent").GetInt32());
                    JsonElement j = p.GetProperty("joint");
                    Assert.Equal("Hinge", j.GetProperty("type").GetString());
                    Assert.Equal(1, j.GetProperty("dof").GetInt32());
                    Assert.Equal(-1.0, j.GetProperty("limits")[0].GetProperty("min").GetDouble(), 5);
                    Assert.Equal(1.0, j.GetProperty("limits")[0].GetProperty("max").GetDouble(), 5);
                }
            }

            // The brain: four outputs (a power and a bend for each hinge), two links, the ports of Core's default pool and the one link names outside it.
            JsonElement b = g.GetProperty("brain");
            Assert.Equal("central", b.GetProperty("model").GetString());
            Assert.Equal(4, b.GetProperty("outputs").GetArrayLength());
            Assert.Equal(2, b.GetProperty("links").GetArrayLength());
            Assert.Equal(0, b.GetProperty("hidden").GetArrayLength());
            Assert.Equal(1, b.GetProperty("links")[0].GetProperty("weight").GetDouble() * 2, 5);
            Assert.Equal("+", b.GetProperty("links")[0].GetProperty("sign").GetString());
            Assert.Equal("-", b.GetProperty("links")[1].GetProperty("sign").GetString());
            // Outputs come in Core's order.
            Assert.Equal(CentralWiring.Outputs(core).Select(o => BrainIds.Hex(o.Id)).ToArray(), b.GetProperty("outputs").EnumerateArray().Select(o => o.GetProperty("id").GetString()).ToArray());
        }

        [Fact]
        public void PortsAreNamedFromCoreForTwoSensesOnTwoParts()
        {
            (ResourceResolver r, string run, RunMaker m) = Run();
            JsonElement b = Read(r, $"nursery://genome/{run}/g0.1").GetProperty("brain");
            Phenotype core = Developer.Develop(m.G0);

            // Core's own ports for its default pool, in its order, come first; each is named by part and sense.
            List<BrainPort> pool = CentralWiring.Ports(core, SensorChannels.DefaultPool);
            JsonElement[] ports = b.GetProperty("ports").EnumerateArray().ToArray();
            for (int i = 0; i < pool.Count; i++)
            {
                Assert.Equal(BrainIds.Hex(pool[i].Id), ports[i].GetProperty("id").GetString());
                Assert.Equal(pool[i].Part, ports[i].GetProperty("part").GetInt32());
                Assert.Equal(pool[i].Channel.ToString(), ports[i].GetProperty("channel").GetString());
                Assert.True(ports[i].GetProperty("inPool").GetBoolean());
            }

            // The two senses the genome's links read: a joint angle on part 1 and a smell on part 2 (not in the default pool: listed after it, flagged).
            JsonElement angle = ports.Single(p => p.GetProperty("id").GetString() == BrainIds.Hex(Port(core, 1, SensorChannel.JointAngle)));
            JsonElement smell = ports.Single(p => p.GetProperty("id").GetString() == BrainIds.Hex(Port(core, 2, SensorChannel.Chemical)));
            Assert.Equal(1, angle.GetProperty("part").GetInt32());
            Assert.Equal("JointAngle", angle.GetProperty("channel").GetString());
            Assert.Equal(1, angle.GetProperty("links").GetInt32());
            Assert.StartsWith("part 1 (", angle.GetProperty("name").GetString());
            Assert.EndsWith("JointAngle", angle.GetProperty("name").GetString());
            Assert.Equal(2, smell.GetProperty("part").GetInt32());
            Assert.Equal("Chemical", smell.GetProperty("channel").GetString());
            Assert.False(smell.GetProperty("inPool").GetBoolean());
            Assert.True(smell.GetProperty("exists").GetBoolean());
            Assert.Equal(1, smell.GetProperty("links").GetInt32());
            // The links name their ends by the same ids, and say what each end is.
            JsonElement link = b.GetProperty("links")[1];
            Assert.Equal(BrainIds.Hex(Port(core, 2, SensorChannel.Chemical)), link.GetProperty("from").GetString());
            Assert.Equal("port", link.GetProperty("fromKind").GetString());
            Assert.Equal("output", link.GetProperty("toKind").GetString());
        }

        [Fact]
        public void APortThatNamesNothingInTheGrownBodyIsListedAsSuchAndNotDropped()
        {
            const ulong Ghost = 0xDEADBEEF12345678UL, GhostOut = 0x0BADF00D87654321UL;
            (ResourceResolver r, string run, RunMaker m) = Run(mk =>
            {
                Phenotype body = Developer.Develop(mk.G0);
                mk.G0.Brain.Links.Add(new BrainLink { From = Ghost, To = Out(body, 1, BrainOutputKind.Bend), Weight = 0.3f });
                // An output of a joint this body does not have.
                mk.G0.Brain.Outputs.Add(new BrainUnit { Id = GhostOut, Bias = 0.2f });
            });
            JsonElement b = Read(r, $"nursery://genome/{run}/g0.1").GetProperty("brain");
            JsonElement ghost = b.GetProperty("ports").EnumerateArray().Single(p => p.GetProperty("id").GetString() == BrainIds.Hex(Ghost));
            Assert.False(ghost.GetProperty("exists").GetBoolean());
            Assert.Equal(JsonValueKind.Null, ghost.GetProperty("part").ValueKind);
            Assert.Contains("names nothing", ghost.GetProperty("name").GetString());
            Assert.Equal(1, ghost.GetProperty("links").GetInt32());
            Assert.Equal(3, b.GetProperty("links").GetArrayLength());       // the link is kept too
            Assert.Equal("nothing", b.GetProperty("links").EnumerateArray().Single(l => l.GetProperty("from").GetString() == BrainIds.Hex(Ghost)).GetProperty("fromKind").GetString());
            JsonElement outs = b.GetProperty("outputs");
            Assert.Equal(5, outs.GetArrayLength());
            JsonElement gone = outs.EnumerateArray().Single(o => o.GetProperty("id").GetString() == BrainIds.Hex(GhostOut));
            Assert.False(gone.GetProperty("exists").GetBoolean());
            Assert.Contains("names nothing", gone.GetProperty("name").GetString());
        }

        [Fact]
        public void ALineageRowIsReadWhenTheRunHasTheFileAndTheResourceSaysSoWhenItDoesNot()
        {
            (ResourceResolver r, string run, _) = Run();
            JsonElement with = Read(r, $"nursery://genome/{run}/g1.0");
            JsonElement lin = with.GetProperty("lineage");
            Assert.True(lin.GetProperty("present").GetBoolean());
            Assert.False(lin.GetProperty("predatesLineage").GetBoolean());
            JsonElement row = lin.GetProperty("row");
            Assert.Equal("g1.0", row.GetProperty("id").GetString());
            Assert.Equal(new[] { "g0.1" }, row.GetProperty("parents").EnumerateArray().Select(x => x.GetString()).ToArray());
            Assert.Equal(1, row.GetProperty("born").GetInt32());
            Assert.False(row.GetProperty("elite").GetBoolean());
            Assert.True(with.GetProperty("parents")[0].GetProperty("held").GetBoolean());

            (ResourceResolver r2, string run2, _) = Run(mk => mk.Lineage = false);
            JsonElement without = Read(r2, $"nursery://genome/{run2}/g1.0");
            Assert.False(without.GetProperty("lineage").GetProperty("present").GetBoolean());
            Assert.True(without.GetProperty("lineage").GetProperty("predatesLineage").GetBoolean());
            Assert.Contains("predates lineage", without.GetProperty("lineage").GetProperty("note").GetString());
            Assert.Equal(JsonValueKind.Null, without.GetProperty("lineage").GetProperty("row").ValueKind);
            Assert.Equal(0, without.GetProperty("parents").GetArrayLength());
            Assert.Equal(JsonValueKind.Null, without.GetProperty("changes").GetProperty("against").ValueKind);
            Assert.True(Read(r2, $"nursery://genomes/{run2}").GetProperty("predatesLineage").GetBoolean());
        }

        [Fact]
        public void TheChangeListOfAMutatedCopyNamesWhatChangedByPath()
        {
            (ResourceResolver r, string run, RunMaker m) = Run(mk =>
            {
                // g1.0 is g0.1 with: a link's weight changed, a link added, a hidden unit added, a node's size changed, a third edge on the root.
                Genome child = mk.G0.Clone();
                Phenotype body = Developer.Develop(child);
                child.Brain.Links[0].Weight = 0.75f;
                child.Brain.Links.Add(new BrainLink { From = Port(body, 1, SensorChannel.Depth), To = Out(body, 2, BrainOutputKind.Power), Weight = 0.2f });
                child.Nodes[1].Dimensions = new Float3(0.35f, 0.3f, 0.3f);
                child.Nodes[0].Edges.Add(new MorphEdge { Child = 1, ParentAnchor = new Float3(0f, 1f, 0f), ChildAnchor = new Float3(0f, -1f, 0f) });
                child.NumberEdges();
                mk.G1 = child;
            });
            JsonElement ch = Read(r, $"nursery://genome/{run}/g1.0").GetProperty("changes");
            Assert.Equal("g0.1", ch.GetProperty("against").GetString());
            Assert.False(ch.GetProperty("identical").GetBoolean());
            var byPath = ch.GetProperty("changes").EnumerateArray().ToDictionary(c => c.GetProperty("path").GetString());

            Phenotype core = Developer.Develop(m.G0);
            string link0 = "brain.links[" + BrainIds.Hex(Port(core, 1, SensorChannel.JointAngle)) + ">" + BrainIds.Hex(Out(core, 1, BrainOutputKind.Power)) + "].w";
            Assert.Equal("changed", byPath[link0].GetProperty("kind").GetString());
            Assert.Equal("0.5", byPath[link0].GetProperty("from").GetString());
            Assert.Equal("0.75", byPath[link0].GetProperty("to").GetString());
            Assert.Contains("part 1 (", byPath[link0].GetProperty("label").GetString());   // the ends are named

            string added = "brain.links[" + BrainIds.Hex(Port(core, 1, SensorChannel.Depth)) + ">" + BrainIds.Hex(Out(core, 2, BrainOutputKind.Power)) + "]";
            Assert.Equal("added", byPath[added].GetProperty("kind").GetString());
            Assert.Equal("w 0.2", byPath[added].GetProperty("to").GetString());

            Assert.Equal("changed", byPath["nodes[1].dimensions.x"].GetProperty("kind").GetString());
            // The root's third edge is added by its id.
            Assert.Contains(byPath.Keys, k => k.StartsWith("nodes[0].edges[") && byPath[k].GetProperty("kind").GetString() == "added");
            Assert.False(ch.GetProperty("truncated").GetBoolean());
        }

        [Fact]
        public void AnEliteWhoseParentIsItselfAGenerationAgoDiffersFromNothing()
        {
            (ResourceResolver r, string run, _) = Run(mk => mk.G1 = mk.G0);
            // Written after Make, over the maker's own lineage: g1.0 is an elite, its one parent itself a generation ago.
            File.WriteAllText(Path.Combine(_dir, run, TankLog.LineageFile),
                TankLog.LineageRow(0, "g0.1", 0, 0, new string[0], false) + "\n" + TankLog.LineageRow(1, "g1.0", 0, 0, new[] { "g0.1" }, true) + "\n");
            JsonElement g = Read(r, $"nursery://genome/{run}/g1.0");
            Assert.Equal("itself, a generation ago", g.GetProperty("parents")[0].GetProperty("role").GetString());
            Assert.True(g.GetProperty("changes").GetProperty("identical").GetBoolean());
            Assert.Equal(0, g.GetProperty("changes").GetProperty("count").GetInt32());
        }

        [Fact]
        public void AParentTheRunDoesNotKeepIsNamedAndNoChangeListIsGuessed()
        {
            (ResourceResolver r, string run, _) = Run();
            JsonElement ch = Read(r, $"nursery://genome/{run}/g2.0").GetProperty("changes");   // its parent is g1.7: not a top, not an exam genome
            Assert.Equal(JsonValueKind.Null, ch.GetProperty("against").ValueKind);
            Assert.Equal("g1.7", ch.GetProperty("parent").GetString());
            Assert.Contains("not kept", ch.GetProperty("reason").GetString());
            Assert.False(Read(r, $"nursery://genome/{run}/g2.0").GetProperty("parents")[0].GetProperty("held").GetBoolean());
            // A founder has no parent to compare with.
            JsonElement founder = Read(r, $"nursery://genome/{run}/g0.1").GetProperty("changes");
            Assert.Contains("founder", founder.GetProperty("reason").GetString());
        }

        [Fact]
        public void ATopGenomeIsNamedByItsMemberIdOnlyWhenTheFilesTieIt()
        {
            (ResourceResolver r, string run, _) = Run();
            JsonElement list = Read(r, $"nursery://genomes/{run}");
            JsonElement[] gs = list.GetProperty("genomes").EnumerateArray().ToArray();
            // Three tops, tied through tank-scores.jsonl; the exam's g2.0 is the third top (one genome, held once), g2.5 is the exam's other.
            Assert.Equal(new[] { "g0.1", "g1.0", "g2.0", "g2.5" }, gs.Select(x => x.GetProperty("id").GetString()).ToArray());
            Assert.Contains("best.g1", gs[1].GetProperty("aliases").EnumerateArray().Select(x => x.GetString()));
            Assert.Equal(new[] { "top", "exam" }, gs[2].GetProperty("kinds").EnumerateArray().Select(x => x.GetString()).ToArray());
            Assert.Contains("exam.0", gs[2].GetProperty("aliases").EnumerateArray().Select(x => x.GetString()));
            Assert.Equal(new[] { "exam" }, gs[3].GetProperty("kinds").EnumerateArray().Select(x => x.GetString()).ToArray());
            // Every alias reads the same genome.
            Assert.Equal("g1.0", Read(r, $"nursery://genome/{run}/best.g1").GetProperty("id").GetString());
            Assert.Equal("g2.0", Read(r, $"nursery://genome/{run}/exam.0").GetProperty("id").GetString());
            Assert.Equal("g2.5", Read(r, $"nursery://genome/{run}/exam.1").GetProperty("id").GetString());

            // With no scores file nothing ties a top to a member: it is best.g<generation>, never an id it might not have.
            (ResourceResolver r2, string run2, _) = Run(mk => mk.Scores = false);
            JsonElement[] bare = Read(r2, $"nursery://genomes/{run2}").GetProperty("genomes").EnumerateArray().ToArray();
            Assert.Equal(new[] { "best.g0", "best.g1", "best.g2", "g2.0", "g2.5" }, bare.Select(x => x.GetProperty("id").GetString()).ToArray());
            Assert.Equal(JsonValueKind.Null, bare[0].GetProperty("memberId").ValueKind);
            Assert.Contains("not tied", bare[0].GetProperty("idNote").GetString());
            Assert.Contains(Read(r2, $"nursery://genomes/{run2}").GetProperty("notes").EnumerateArray().Select(x => x.GetString()), n => n.Contains("tank-scores.jsonl"));
        }

        [Fact]
        public void ATopThatTheScoresRowDoesNotAgreeWithIsNotTied()
        {
            // The generation row says the top scored 5 on body 0; the scores row's best member is a different one. Nothing is tied.
            (ResourceResolver r, string run, _) = Run(mk => { });
            File.WriteAllText(Path.Combine(_dir, run, "tank-generations.jsonl"),
                "{\"gen\":0,\"top\":{\"score\":5.5,\"episodes\":4,\"body\":0,\"born\":0}}\n{\"gen\":1,\"top\":{\"score\":7,\"episodes\":4,\"body\":0,\"born\":1}}\n{\"gen\":2,\"top\":{\"score\":8,\"episodes\":4,\"body\":0,\"born\":2}}\n");
            JsonElement[] gs = Read(r, $"nursery://genomes/{run}").GetProperty("genomes").EnumerateArray().ToArray();
            Assert.Equal("best.g0", gs[0].GetProperty("id").GetString());
            Assert.Equal("g1.0", gs[1].GetProperty("id").GetString());
        }

        [Fact]
        public void AnExamWithNoPlanNamesItsGenomesByTheirLineInTheFile()
        {
            (ResourceResolver r, string run, _) = Run(mk => mk.Plan = false);
            JsonElement[] gs = Read(r, $"nursery://genomes/{run}").GetProperty("genomes").EnumerateArray().ToArray();
            Assert.Equal(new[] { "g0.1", "g1.0", "g2.0", "exam.0", "exam.1" }, gs.Select(x => x.GetProperty("id").GetString()).ToArray());
            Assert.Contains("wrote no exam plan", gs[4].GetProperty("idNote").GetString());
        }

        [Fact]
        public void AnUnknownGenomeAndABadAddressAreRefused()
        {
            (ResourceResolver r, string run, _) = Run();
            Assert.Throws<ResourceNotFound>(() => r.Read($"nursery://genome/{run}/g9.9"));
            Assert.Throws<ResourceNotFound>(() => r.Read("nursery://genome/no-such-run/g0.1"));
            Assert.Throws<ResourceRefused>(() => r.Read("nursery://genome/" + run));
            Assert.Throws<ResourceRefused>(() => r.Read("nursery://genomes/"));
        }

        [Fact]
        public void TheGenomeResourcesAreOfferedToAnAgentToo()
        {
            (ResourceResolver r, string run, _) = Run();
            Assert.Contains(r.Templates.Select(t => t.UriTemplate), t => t == "nursery://genome/{run}/{id}");
            Assert.Contains(r.Templates.Select(t => t.UriTemplate), t => t == "nursery://genomes/{run}");
            Assert.Contains(r.List().Select(x => x.Uri), u => u == "nursery://genomes/" + run);
            Assert.Equal(r.Templates.Select(t => t.UriTemplate).ToArray(), McpSurface.Templates(r).Select(t => t.UriTemplate).ToArray());
        }

        [Fact]
        public void TheGenomesGraphIsItsOwnNodesAndEdgesNothingGrown()
        {
            (ResourceResolver r, string run, MorphingRun m) = Graphed();
            JsonElement gr = Read(r, $"nursery://genome/{run}/g0.1").GetProperty("graph");
            Assert.Equal(m.G0.RootIndex, gr.GetProperty("root").GetInt32());
            JsonElement[] nodes = gr.GetProperty("nodes").EnumerateArray().ToArray();
            Assert.Equal(m.G0.Nodes.Count, nodes.Length);
            for (int i = 0; i < nodes.Length; i++)
            {
                MorphNode core = m.G0.Nodes[i];
                Assert.Equal(i, nodes[i].GetProperty("index").GetInt32());
                Assert.Equal(i == m.G0.RootIndex, nodes[i].GetProperty("root").GetBoolean());
                Assert.Equal(core.CellTypeId, nodes[i].GetProperty("cellType").GetString());
                Assert.Equal(core.ShapeId, nodes[i].GetProperty("shape").GetString());
                Assert.Equal(core.RecursiveLimit, nodes[i].GetProperty("recursiveLimit").GetInt32());
                Assert.Equal(core.Edges.Count, nodes[i].GetProperty("edges").GetInt32());
            }
            JsonElement[] edges = gr.GetProperty("edges").EnumerateArray().ToArray();
            Assert.Equal(m.G0.Nodes.Sum(n => n.Edges.Count), edges.Length);
            // The first edge of the root is a mirrored one and the second a terminal one: both flags are read from the genome's own edge.
            JsonElement mirrored = edges[0], terminal = edges[1];
            Assert.Equal(0, mirrored.GetProperty("from").GetInt32());
            Assert.Equal(1, mirrored.GetProperty("to").GetInt32());
            Assert.True(mirrored.GetProperty("reflect").GetProperty("x").GetBoolean());
            Assert.False(mirrored.GetProperty("reflect").GetProperty("y").GetBoolean());
            Assert.Equal(2, mirrored.GetProperty("copies").GetInt32());
            Assert.False(mirrored.GetProperty("terminalOnly").GetBoolean());
            Assert.True(terminal.GetProperty("terminalOnly").GetBoolean());
            Assert.Equal(1, terminal.GetProperty("copies").GetInt32());
            // A central genome's edges carry the ids the genome stores.
            Assert.Equal(BrainIds.Hex(m.G0.Nodes[0].Edges[0].Id), mirrored.GetProperty("id").GetString());
        }

        private sealed class MorphingRun { public Genome G0; }

        /// <summary>A run whose one genome has a mirrored edge and a terminal-only edge on its root, so that the graph has both flags to say.</summary>
        private (ResourceResolver, string, MorphingRun) Graphed()
        {
            Genome gen = Synthetic();
            gen.Nodes[0].Edges[0] = new MorphEdge { Child = 1, ParentAnchor = new Float3(1f, 0f, 0f), ChildAnchor = new Float3(-1f, 0f, 0f), Reflect = new Bool3(true, false, false), Id = gen.Nodes[0].Edges[0].Id };
            gen.Nodes[0].Edges[1] = new MorphEdge { Child = 2, ParentAnchor = new Float3(-1f, 0f, 0f), ChildAnchor = new Float3(1f, 0f, 0f), TerminalOnly = true, Id = gen.Nodes[0].Edges[1].Id };
            (ResourceResolver r, string run, RunMaker mk) = Run(x => x.G0 = gen);
            return (r, run, new MorphingRun { G0 = gen });
        }

        [Fact]
        public void TheGenomeTextIsServedAsTheRunStoresIt()
        {
            (ResourceResolver r, string run, RunMaker m) = Run();
            (string name, string text) = r.GenomeText(run, "g1.0");
            Assert.Equal(Row(m.G1), text);   // the stored line, byte for byte
            Assert.Equal(run + "-g1.0.json", name);
            // An exam genome is the part of its line after the tab; an alias answers for the same genome.
            Assert.Equal(Row(m.Exam2), r.GenomeText(run, "exam.1").text);
            Assert.Equal(Row(m.G1), r.GenomeText(run, "best.g1").text);
            Assert.Throws<ResourceNotFound>(() => r.GenomeText(run, "g9.9"));
            Assert.Throws<ResourceNotFound>(() => r.GenomeText("no-such-run", "g0.1"));
        }

        [Fact]
        public void TheResourcesNameFoldersAndFilesRelativeToTheRepositoryRoot()
        {
            (ResourceResolver r, string run, _) = Run();
            string rel = Path.GetRelativePath(Where.Root, Path.Combine(_dir, run)).Replace('\\', '/');
            Assert.Equal(rel, Read(r, $"nursery://genomes/{run}").GetProperty("folder").GetString());
            Assert.Equal(rel, Read(r, $"nursery://run/{run}").GetProperty("folder").GetString());
            JsonElement reg = Read(r, "nursery://registry");
            Assert.False(Path.IsPathRooted(reg.GetProperty("file").GetString()), "the registry's file is named relative to the root");
            foreach (JsonElement item in reg.GetProperty("items").EnumerateArray())
            {
                string folder = item.GetProperty("folder").GetString();
                Assert.False(folder != null && (Path.IsPathRooted(folder) || folder.Contains(':')), $"{folder} is a full path");
            }
            // And nothing anywhere in the three resources holds this machine's drive.
            string drive = Path.GetPathRoot(Where.Root);
            foreach (string uri in new[] { $"nursery://genomes/{run}", $"nursery://run/{run}", "nursery://registry" })
                Assert.DoesNotContain(drive.Replace("\\", "\\\\"), Js.Serialize(r.Read(uri)), StringComparison.OrdinalIgnoreCase);
        }

        // ------------------------------------------------------------------ a real genome, read whole

        private static void AssertPilotGenome(JsonElement g, string text)
        {
            using JsonDocument file = JsonDocument.Parse(text);
            JsonElement fb = file.RootElement.GetProperty("brain");
            // The file's genome has two nodes: an absorptive box and a twist link on a capsule; one joint, one degree of freedom.
            Assert.Equal(2, g.GetProperty("bodyPlan").GetProperty("partCount").GetInt32());
            Assert.Equal(1, g.GetProperty("bodyPlan").GetProperty("joints").GetInt32());
            JsonElement[] parts = g.GetProperty("bodyPlan").GetProperty("parts").EnumerateArray().ToArray();
            Assert.Equal("box", parts[0].GetProperty("shape").GetString());
            Assert.Equal("capsule", parts[1].GetProperty("shape").GetString());
            Assert.Equal("Twist", parts[1].GetProperty("joint").GetProperty("type").GetString());
            Assert.Equal(-0.58871317, parts[1].GetProperty("joint").GetProperty("limits")[0].GetProperty("min").GetDouble(), 6);
            Assert.True(parts[1].GetProperty("radius").GetDouble() > 0);

            // Every output and every link of the file is in the resource, with its weight; the hidden units too.
            JsonElement b = g.GetProperty("brain");
            Assert.Equal(fb.GetProperty("outputs").GetArrayLength(), b.GetProperty("outputs").GetArrayLength());
            Assert.Equal(fb.GetProperty("hidden").GetArrayLength(), b.GetProperty("hidden").GetArrayLength());
            Assert.Equal(fb.GetProperty("links").GetArrayLength(), b.GetProperty("links").GetArrayLength());
            for (int i = 0; i < fb.GetProperty("links").GetArrayLength(); i++)
            {
                JsonElement fl = fb.GetProperty("links")[i], rl = b.GetProperty("links")[i];
                Assert.Equal(fl.GetProperty("from").GetString(), rl.GetProperty("from").GetString());
                Assert.Equal(fl.GetProperty("to").GetString(), rl.GetProperty("to").GetString());
                Assert.Equal(fl.GetProperty("w").GetDouble(), rl.GetProperty("weight").GetDouble(), 6);
            }
            // Every port a link reads is listed, and none is dropped: each id the links name appears in the ports, found or flagged.
            var listed = new HashSet<string>(b.GetProperty("ports").EnumerateArray().Select(p => p.GetProperty("id").GetString()));
            foreach (JsonElement l in fb.GetProperty("links").EnumerateArray())
            {
                string from = l.GetProperty("from").GetString();
                bool unit = fb.GetProperty("outputs").EnumerateArray().Concat(fb.GetProperty("hidden").EnumerateArray()).Any(u => u.GetProperty("id").GetString() == from);
                if (!unit) Assert.Contains(from, listed);
            }
            Assert.Equal(g.GetProperty("brain").GetProperty("counts").GetProperty("links").GetInt32(), fb.GetProperty("links").GetArrayLength());
        }

        [Fact]
        public void PilotDeepBrain2sFirstTopGenomeIsReadWholeFromItsFixture()
        {
            string fixture = File.ReadAllText(Path.Combine(Where.Root, "src", "Evosim.NurseryRoom.Tests", "fixtures", "pilot-deep-brain2-best-g0.json")).Trim();
            string folder = Path.Combine(_dir, "pilot-fixture");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "tank-best.jsonl"), fixture + "\n");
            JsonElement g = Read(Resolver(folder), "nursery://genome/pilot-fixture/best.g0");
            Assert.Equal("best.g0", g.GetProperty("id").GetString());
            AssertPilotGenome(g, fixture);
            Assert.True(g.GetProperty("lineage").GetProperty("predatesLineage").GetBoolean());
        }

        /// <summary>
        /// The finished run's own file, opened without a lock and written nowhere. Reported as skipped, in the run's output, when the run is not in this clone
        /// (then the fixture test above reads the same line); it never passes silently.
        /// </summary>
        [RealRunFact("pilot-deep-brain2/tank-best.jsonl")]
        public void PilotDeepBrain2sFirstTopGenomeIsReadWholeFromItsRealFile()
        {
            string real = Path.Combine(RealRunFactAttribute.Find("pilot-deep-brain2/tank-best.jsonl"), "scratch", "nursery", "pilot-deep-brain2", "tank-best.jsonl");
            string folder = Path.GetDirectoryName(real);
            string first = GenomeSet.ReadLines(real)[0];
            string rel = Path.GetRelativePath(Where.Root, folder).Replace('\\', '/');
            var room = new RoomContext(Where.Root, Where.Settings(rel), Where.Root, Path.Combine(_dir, "none.jsonl"));
            var resolver = new ResourceResolver(room);
            JsonElement list = Read(resolver, "nursery://genomes/pilot-deep-brain2");
            Assert.True(list.GetProperty("count").GetInt32() >= 100, "one top genome a generation, and the exam's");
            Assert.True(list.GetProperty("predatesLineage").GetBoolean());
            Assert.Equal("best.g0", list.GetProperty("genomes")[0].GetProperty("id").GetString());
            JsonElement g = Read(resolver, "nursery://genome/pilot-deep-brain2/best.g0");
            AssertPilotGenome(g, first);
            // The real file's first line is the fixture's.
            Assert.Equal(first, File.ReadAllText(Path.Combine(Where.Root, "src", "Evosim.NurseryRoom.Tests", "fixtures", "pilot-deep-brain2-best-g0.json")).Trim());
            // Its exam genomes are read from the tsv, by line.
            Assert.Contains(list.GetProperty("genomes").EnumerateArray(), x => x.GetProperty("id").GetString() == "exam.0");
        }
    }
}
