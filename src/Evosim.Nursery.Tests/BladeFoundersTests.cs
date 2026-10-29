using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Evosim.Core;
using Evosim.Dynamics;
using Evosim.Farm;
using Evosim.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Nursery.Tests
{
    /// <summary>
    /// D236's ten blade founders (<c>inocula/r59-direction-blades/</c>, written by <c>scripts/reads/r59-blades.py</c>): each of round 59's
    /// direction tops with its limb's capsule replaced by a box of the same volume, flat across the stroke. The tests read the committed files
    /// and check them against Core's own volume, development, joint frame and fin law, and against the readers the trickle and the nursery use.
    /// </summary>
    [Collection("restep")]
    public class BladeFoundersTests : IDisposable
    {
        private readonly ITestOutputHelper _out;
        private readonly string _dir = RestepFixtures.NewDir("blades");

        public BladeFoundersTests(ITestOutputHelper output) => _out = output;

        public void Dispose() => Directory.Delete(_dir, true);

        private const string Tops = "inocula/r59-direction-tops/";
        private const string Blades = "inocula/r59-direction-blades/";

        /// <summary>Round 58's link limit (<c>GenomeFactory.MaxLinkHalfExtent</c>): the half-length the capsule has, and a ceiling on every half-extent of the box (the coordinator's ruling for D236).</summary>
        private const float LinkLimit = 0.26f;

        /// <summary>The tops' <c>pool.tsv</c> order, best eater first, which the blades' lists follow.</summary>
        private static readonly string[] Parents =
        {
            "direction-sparse-g33-47.json", "direction-sparse-g22-27.json", "direction-sparse-g33-30.json", "direction-sparse-g33-37.json",
            "direction-sparse-g31-19.json", "direction-sparse-g30-40.json", "direction-sparse-g16-2.json", "direction-sparse-g14-31.json",
            "direction-sparse-g12-35.json", "direction-sparse-g15-36.json",
        };

        // What TricklePoolFiles.PrepareList pins for each list. The tops' is the one their README records; the other two were computed with a Python
        // copy of TricklePoolFiles.HashOf that reproduces the tops' (scratch/curriculum/piece5/pool_hash.py), so these two are the first time Core's
        // own reader meets them.
        private const string TopsPoolHash = "ec0baadf18d518e17434d1d75196a3e9e21e326fdc55b2f2b9b60f6250c4077d";
        private const string BladesPoolHash = "70ee22dab352b074715b67de957999b79e0def753ef238610cb89957b646f096";
        private const string MixedPoolHash = "0a10451e224dba144dcf5375ce18f03824e39ffa9c60ac1bccd857e976f9762f";

        private static string Full(string relative) => Path.Combine(RestepFixtures.Root, relative.Replace('/', Path.DirectorySeparatorChar));

        /// <summary>A committed file's text with LF line ends: how it is stored, and how origins.tsv hashed it, whatever the checkout did.</summary>
        private static string Lf(string relative) => File.ReadAllText(Full(relative)).Replace("\r\n", "\n");

        private static string BladeOf(string parent) => parent.Substring(0, parent.Length - ".json".Length) + "-blade.json";

        private static string Sha256(string text)
        {
            using (var sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(new UTF8Encoding(false).GetBytes(text)).Select(b => b.ToString("x2")));
        }

        /// <summary>CapsuleShape.Volume (StandardShapes.cs), in double on the float half-extents: the figure the box is matched to.</summary>
        private static double CapsuleVolume(Float3 h)
        {
            double r = (Math.Abs((double)h.X) + Math.Abs((double)h.Z)) / 2.0;
            double span = Math.Max(0.0, Math.Abs((double)h.Y) - r);
            return Math.PI * r * r * 2.0 * span + 4.0 / 3.0 * Math.PI * r * r * r;
        }

        /// <summary>BoxShape.Volume (StandardShapes.cs), in double.</summary>
        private static double BoxVolume(Float3 h) => 8.0 * Math.Abs((double)h.X) * Math.Abs((double)h.Y) * Math.Abs((double)h.Z);

        private static void Near(double expected, double actual, int digits) => Assert.Equal(expected, actual, digits);

        public static IEnumerable<object[]> ParentFiles() => Parents.Select(p => new object[] { p });

        [Fact]
        public void TheTenAreTheTopsPoolInItsOrder()
        {
            string[] listed = Lf(Tops + "pool.tsv").Split('\n').Skip(1).Where(l => l.Length > 0).Select(l => l.Split('\t')[0]).ToArray();
            Assert.Equal(listed, Parents);
            foreach (string p in Parents) Assert.True(File.Exists(Full(Blades + BladeOf(p))), BladeOf(p) + " is missing");
            // Every .json but lineage.json (curriculum-pool.py's map of the nursery pool's lines to founder lineages) is a genome.
            string[] onDisk = Directory.GetFiles(Full(Blades), "*.json").Select(Path.GetFileName).Where(f => f != "lineage.json")
                .OrderBy(f => f, StringComparer.Ordinal).ToArray();
            Assert.Equal(Parents.Select(BladeOf).OrderBy(f => f, StringComparer.Ordinal).ToArray(), onDisk);
        }

        // ------------------------------------------------------------------ the genome

        [Theory]
        [MemberData(nameof(ParentFiles))]
        public void ABladeLoadsAtTodaysFormatAndDevelopsToItsParentsPartCount(string parent)
        {
            string parentText = Lf(Tops + parent), bladeText = Lf(Blades + BladeOf(parent));
            Genome parentGenome = GenomeJson.Read(parentText), blade = GenomeJson.Read(bladeText);

            // Today's format, one line and a newline, and exactly as GenomeJson writes it.
            Assert.Contains("\"format\":" + GenomeJson.CentralFormatVersion.ToString(CultureInfo.InvariantCulture) + ",", bladeText);
            Assert.NotNull(blade.Brain);
            Assert.Equal(1, bladeText.Count(c => c == '\n'));
            Assert.EndsWith("\n", bladeText);
            Assert.Equal(bladeText, GenomeJson.Write(blade) + "\n");
            Assert.Empty(blade.Validate());

            Phenotype was = Developer.Develop(parentGenome), now = Developer.Develop(blade);
            Assert.Equal(2, was.PartCount);
            Assert.Equal(was.PartCount, now.PartCount);
            Assert.Equal(was.TotalDof, now.TotalDof);
            Assert.Equal(1, now.TotalDof);
            for (int i = 0; i < now.PartCount; i++)
            {
                Assert.Equal(was.Parts[i].CellTypeId, now.Parts[i].CellTypeId);
                Assert.Equal(was.Parts[i].JointType, now.Parts[i].JointType);
                Assert.Equal(was.Parts[i].ParentIndex, now.Parts[i].ParentIndex);
            }
        }

        [Theory]
        [MemberData(nameof(ParentFiles))]
        public void ABladeDiffersFromItsParentOnlyInTheLimbsShapeAndDimensions(string parent)
        {
            string parentText = Lf(Tops + parent), bladeText = Lf(Blades + BladeOf(parent));
            Genome parentGenome = GenomeJson.Read(parentText), blade = GenomeJson.Read(bladeText);

            Assert.Equal(ShapeIds.Capsule, parentGenome.Nodes[1].ShapeId);
            Assert.Equal(ShapeIds.Box, blade.Nodes[1].ShapeId);
            Assert.NotEqual(parentText, bladeText);

            // Put the parent's shape and dimensions back into the blade: if anything else had changed, the text would not be the parent's.
            blade.Nodes[1].ShapeId = parentGenome.Nodes[1].ShapeId;
            blade.Nodes[1].Dimensions = parentGenome.Nodes[1].Dimensions;
            Assert.Equal(parentText, GenomeJson.Write(blade) + "\n");
        }

        // ------------------------------------------------------------------ the volume and the bounds

        [Theory]
        [MemberData(nameof(ParentFiles))]
        public void TheBoxHasTheCapsulesVolumeAndStaysWithinTheBounds(string parent)
        {
            Genome parentGenome = GenomeJson.Read(Lf(Tops + parent)), blade = GenomeJson.Read(Lf(Blades + BladeOf(parent)));
            Float3 capsule = parentGenome.Nodes[1].Dimensions, box = blade.Nodes[1].Dimensions;

            double vCapsule = CapsuleVolume(capsule), vBox = BoxVolume(box);
            _out.WriteLine(FormattableString.Invariant($"capsule {capsule.X:R} x {capsule.Y:R} x {capsule.Z:R}: {vCapsule:R} m3; box {box.X:R} x {box.Y:R} x {box.Z:R}: {vBox:R} m3; relative {Math.Abs(vBox / vCapsule - 1):0.###e+0}"));

            // The figure to 1e-9. A float cannot be matched finer, so this is Core's formula in double on the stored floats.
            Assert.InRange(Math.Abs(vBox / vCapsule - 1), 0d, 1e-9);

            // The transcription above is Core's: against the registry's own single-precision volumes, and against the developed parts'.
            PartShape capsuleShape = PartShapeRegistry.Standard.Resolve(ShapeIds.Capsule), boxShape = PartShapeRegistry.Standard.Resolve(ShapeIds.Box);
            Assert.InRange(Math.Abs(capsuleShape.Volume(capsule) / vCapsule - 1), 0d, 1e-6);
            Assert.InRange(Math.Abs(boxShape.Volume(box) / vBox - 1), 0d, 1e-6);

            Phenotype was = Developer.Develop(parentGenome), now = Developer.Develop(blade);
            Assert.Equal(ShapeIds.Capsule, was.Parts[1].ShapeId);
            Assert.Equal(ShapeIds.Box, now.Parts[1].ShapeId);
            Assert.InRange(Math.Abs(now.Parts[1].Volume / (double)was.Parts[1].Volume - 1), 0d, 1e-6);

            // Every half-extent at most the link limit, the half-length included; every one positive and finite (Genome.Validate), at least the
            // development floor (so Development neither raises nor prunes it), and the volume inside development's bounds. The part is built at
            // the genome's numbers.
            Assert.True(box.Y <= LinkLimit, $"half-length {box.Y} exceeds {LinkLimit}");
            foreach (float h in new[] { box.X, box.Y, box.Z, now.Parts[1].HalfExtents.X, now.Parts[1].HalfExtents.Y, now.Parts[1].HalfExtents.Z })
                Assert.True(h <= LinkLimit, $"half-extent {h} exceeds the link limit {LinkLimit}");
            float floor = DevelopmentLimits.Default.MinPartHalfExtent;
            foreach (float h in new[] { box.X, box.Y, box.Z })
                Assert.True(h >= floor && !float.IsNaN(h) && !float.IsInfinity(h), $"half-extent {h} is under the development floor {floor}");
            Assert.Equal(box.X, now.Parts[1].HalfExtents.X);
            Assert.Equal(box.Y, now.Parts[1].HalfExtents.Y);
            Assert.Equal(box.Z, now.Parts[1].HalfExtents.Z);
            Assert.InRange(now.Parts[1].Volume, DevelopmentLimits.Default.MinPartVolume, DevelopmentLimits.Default.MaxPartVolume);

            // Flat: the flattest the link limit allows at this volume, width over thickness about 15 to 1.
            Assert.InRange(box.Z / box.X, 14.5f, 15.5f);
        }

        // ------------------------------------------------------------------ the orientation

        /// <summary>
        /// In the solver, standing upright (as <c>ThePlateGrowsAMouthedRootAndOneLimbOnAVerticalHinge</c> reads the swimmer): the hinge axis is the
        /// joint frame's x carried to the world, the limb's length lies along its y, and the swing is the hinge axis crossed with the length. The
        /// box's extent along the hinge axis is its width, along the swing its thickness. D229's fin law takes a box's width as its caliper along
        /// (limb axis x motion), and the motion is (hinge axis x limb axis), so that is the extent along the hinge axis; the test then asks the law.
        /// </summary>
        [Theory]
        [MemberData(nameof(ParentFiles))]
        public void TheWidthLiesAlongTheHingeAxisAndTheThicknessAlongTheSwing(string parent)
        {
            Genome parentGenome = GenomeJson.Read(Lf(Tops + parent)), blade = GenomeJson.Read(Lf(Blades + BladeOf(parent)));
            Phenotype was = Developer.Develop(parentGenome), now = Developer.Develop(blade);

            var creature = new Creature(0, now, new SolverConfig());
            creature.PlaceAt(new Vec3(0, -5, 0), QuatD.Identity);
            Mat3 link = Mat3.Read(creature.RotationMatrix, 9);
            Vec3 hinge = (link * QuatD.Read(creature.JointFrame, 4).Rotate(new Vec3(1, 0, 0))).Normalized;
            Vec3 ex = link * new Vec3(1, 0, 0), ey = link * new Vec3(0, 1, 0), ez = link * new Vec3(0, 0, 1);
            Vec3 swing = Vec3.Cross(hinge, ey).Normalized;

            Float3 h = now.Parts[1].HalfExtents;
            double Caliper(Vec3 d) => 2.0 * (Math.Abs(Vec3.Dot(d, ex)) * h.X + Math.Abs(Vec3.Dot(d, ey)) * h.Y + Math.Abs(Vec3.Dot(d, ez)) * h.Z);

            double width = Caliper(hinge), thickness = Caliper(swing), length = Caliper(ey);
            _out.WriteLine(
                FormattableString.Invariant($"hinge ({hinge.X:0.###}, {hinge.Y:0.###}, {hinge.Z:0.###}) swing ({swing.X:0.###}, {swing.Y:0.###}, {swing.Z:0.###}) limb ({ey.X:0.###}, {ey.Y:0.###}, {ey.Z:0.###}): ") +
                FormattableString.Invariant($"width {width:R} thickness {thickness:R} length {length:R}"));

            // The hinge stands vertical, the limb lies level, and the box's own axes are the hinge, the swing and the length.
            Assert.True(Math.Abs(hinge.Y) > 0.999, $"the hinge axis is not vertical: y {hinge.Y}");
            Assert.True(Math.Abs(ey.Y) < 1e-6, $"the limb does not lie level: y {ey.Y}");
            Assert.InRange(Math.Abs(Vec3.Dot(hinge, ez)), 0.999999, 1.000001);
            Assert.InRange(Math.Abs(Vec3.Dot(swing, ex)), 0.999999, 1.000001);

            // So the width is the z extent, the thickness the x extent, and the length the y extent.
            Assert.InRange(width, 2.0 * h.Z * 0.999999, 2.0 * h.Z * 1.000001);
            Assert.InRange(thickness, 2.0 * h.X * 0.999999, 2.0 * h.X * 1.000001);
            Assert.InRange(length, 2.0 * h.Y * 0.999999, 2.0 * h.Y * 1.000001);
            Assert.InRange(width / thickness, 14.5, 15.5);
            Assert.True(width > 10 * thickness);

            // The fin law's own answer for a stroke of this joint (Fluid.AddedMassOf: axis the limb's, direction the motion = hinge x axis).
            // A box: rho * pi/4 * w^2 * L. Against the capsule it replaces, and against the same box turned so the thin side faces the hinge.
            const double Rho = 1000.0, Coefficient = 0.5;
            Vec3 motion = Vec3.Cross(hinge, ey).Normalized;
            double bladeMass = Fluid.AddedMassOf(now.Parts[1].ShapeId, now.Parts[1].HalfExtents, link, ey, motion, Rho, Coefficient, now.Parts[1].Volume);
            Assert.InRange(bladeMass / (Rho * Math.PI / 4.0 * width * width * length), 1 - 1e-6, 1 + 1e-6);

            double capsuleMass = Fluid.AddedMassOf(was.Parts[1].ShapeId, was.Parts[1].HalfExtents, link, ey, motion, Rho, Coefficient, was.Parts[1].Volume);
            double turnedMass = Fluid.AddedMassOf(ShapeIds.Box, new Float3(h.Z, h.Y, h.X), link, ey, motion, Rho, Coefficient, now.Parts[1].Volume);
            _out.WriteLine(FormattableString.Invariant($"fin-law added mass: blade {bladeMass:0.###} kg, capsule {capsuleMass:0.###} kg, blade turned thin side to the hinge {turnedMass:0.###} kg"));
            Assert.True(bladeMass > 10.0 * capsuleMass, $"the blade's added mass {bladeMass} is not ten times the capsule's {capsuleMass}");
            Assert.True(turnedMass < bladeMass / 100.0, $"turned the other way the box still takes {turnedMass} of {bladeMass}");
        }

        // ------------------------------------------------------------------ the trunk

        [Theory]
        [MemberData(nameof(ParentFiles))]
        public void TheDevelopedLimbHangsFromTheSameFaceAndDoesNotOverlapTheTrunk(string parent)
        {
            Genome parentGenome = GenomeJson.Read(Lf(Tops + parent)), blade = GenomeJson.Read(Lf(Blades + BladeOf(parent)));
            Phenotype was = Developer.Develop(parentGenome), now = Developer.Develop(blade);
            PhenotypePart trunk = now.Parts[0], limb = now.Parts[1], oldLimb = was.Parts[1];

            // The trunk is untouched, and the limb stands where the capsule stood: the joint's two anchors and the limb's centre.
            Assert.Equal(was.Parts[0].HalfExtents.X, trunk.HalfExtents.X);
            Assert.Equal(was.Parts[0].HalfExtents.Y, trunk.HalfExtents.Y);
            Assert.Equal(was.Parts[0].HalfExtents.Z, trunk.HalfExtents.Z);
            Near(oldLimb.ParentAnchorLocal.X, limb.ParentAnchorLocal.X, 6);
            Near(oldLimb.ParentAnchorLocal.Y, limb.ParentAnchorLocal.Y, 6);
            Near(oldLimb.ParentAnchorLocal.Z, limb.ParentAnchorLocal.Z, 6);
            Near(oldLimb.ChildAnchorLocal.X, limb.ChildAnchorLocal.X, 6);
            Near(oldLimb.ChildAnchorLocal.Y, limb.ChildAnchorLocal.Y, 6);
            Near(oldLimb.ChildAnchorLocal.Z, limb.ChildAnchorLocal.Z, 6);
            Near(oldLimb.Position.X, limb.Position.X, 5);
            Near(oldLimb.Position.Y, limb.Position.Y, 5);
            Near(oldLimb.Position.Z, limb.Position.Z, 5);

            // It hangs from the trunk's +z face (the edge's parent anchor is (0, 0, 1)): its centre is the trunk's half-depth and its own half-length out.
            Float3 outward = trunk.Rotation.Rotate(new Float3(0f, 0f, 1f));
            float centre = Float3.Dot(limb.Position - trunk.Position, outward);
            Assert.InRange(centre, trunk.HalfExtents.Z + limb.HalfExtents.Y - 1e-5f, trunk.HalfExtents.Z + limb.HalfExtents.Y + 1e-5f);

            // Every corner of the blade lies on the far side of that face, or on it.
            float lowest = float.MaxValue;
            foreach (float sx in new[] { -1f, 1f })
            foreach (float sy in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
            {
                Float3 corner = limb.Position + limb.Rotation.Rotate(new Float3(sx * limb.HalfExtents.X, sy * limb.HalfExtents.Y, sz * limb.HalfExtents.Z));
                lowest = Math.Min(lowest, Float3.Dot(corner - trunk.Position, outward));
            }
            Assert.True(lowest >= trunk.HalfExtents.Z - 1e-5f, $"a corner of the blade lies {trunk.HalfExtents.Z - lowest} m inside the trunk's face");

            // And Core's own box test, which the world refuses births on: touching at most. The blade meets the trunk's face, so the separating-axis
            // test may call it touching at float precision; what it may not report is a real depth.
            BoxOverlap.Obb a = BoxOverlap.Obb.From(trunk), b = BoxOverlap.Obb.From(limb);
            bool hit = BoxOverlap.Obb.Intersect(a, b, out float depth);
            _out.WriteLine(hit ? FormattableString.Invariant($"touching: depth {depth:0.###e+0} m") : "apart");
            if (hit) Assert.True(depth < 1e-4f, $"the blade overlaps the trunk by {depth} m");
        }

        // ------------------------------------------------------------------ origins.tsv

        [Fact]
        public void OriginsRecordsEachFilesHashItsParentsHashAndItsDimensions()
        {
            string[] lines = Lf(Blades + "origins.tsv").Split('\n').Where(l => l.Length > 0).ToArray();
            string[] head = lines[0].Split('\t');
            Assert.Equal(11, lines.Length);

            int Col(string name) => Array.IndexOf(head, name);
            for (int i = 1; i < lines.Length; i++)
            {
                string[] c = lines[i].Split('\t');
                Assert.Equal(head.Length, c.Length);
                string file = c[Col("file")], parent = c[Col("parent")];
                Assert.Equal(BladeOf(parent), file);
                Assert.Equal(Sha256(Lf(Blades + file)), c[Col("sha256")]);
                Assert.Equal(Sha256(Lf(Tops + parent)), c[Col("parent sha256")]);

                Genome parentGenome = GenomeJson.Read(Lf(Tops + parent)), blade = GenomeJson.Read(Lf(Blades + file));
                float F(string name) => float.Parse(c[Col(name)], CultureInfo.InvariantCulture);
                Assert.Equal(parentGenome.Nodes[1].Dimensions.X, F("capsule x"));
                Assert.Equal(parentGenome.Nodes[1].Dimensions.Y, F("capsule y"));
                Assert.Equal(parentGenome.Nodes[1].Dimensions.Z, F("capsule z"));
                Assert.Equal(blade.Nodes[1].Dimensions.X, F("box x thickness"));
                Assert.Equal(blade.Nodes[1].Dimensions.Y, F("box y length"));
                Assert.Equal(blade.Nodes[1].Dimensions.Z, F("box z width"));
                Assert.InRange(Math.Abs(double.Parse(c[Col("box volume m3")], CultureInfo.InvariantCulture) / CapsuleVolume(parentGenome.Nodes[1].Dimensions) - 1), 0d, 1e-9);
            }
        }

        // ------------------------------------------------------------------ the pools

        [Fact]
        public void PoolTsvReadsThroughTheTricklesReaderAsTheTenAtEqualWeight()
        {
            TricklePoolFiles.Pool tops = TricklePoolFiles.PrepareList(Full(Tops + "pool.tsv"));
            TricklePoolFiles.Pool pool = TricklePoolFiles.PrepareList(Full(Blades + "pool.tsv"));

            Assert.Equal(TopsPoolHash, tops.Hash);   // the reader and the pinned figure agree on a pool whose hash the tops' README records
            Assert.Equal(10, pool.Genomes.Count);
            Assert.Equal(10, pool.Lines.Count);
            Assert.All(pool.Weights, w => Assert.Equal(0.1, w));
            Assert.Equal(new[] { "run", "id", "body", "ownEaten" }, pool.OriginNames);
            for (int i = 0; i < 10; i++)
            {
                Assert.Equal(Lf(Blades + BladeOf(Parents[i])), pool.Lines[i] + "\n");
                Assert.Equal(Parents[i].Replace("direction-sparse-", "").Replace(".json", "").Replace('-', '.') + "-blade", pool.Origins[i][1]);
                Assert.Equal("0", pool.Origins[i][2]);
                Assert.Equal("", pool.Origins[i][3]);
            }
            Assert.Equal(BladesPoolHash, pool.Hash);
        }

        [Fact]
        public void PoolMixedReadsThroughTheSameReaderAsTheTwentyAtEqualWeight()
        {
            TricklePoolFiles.Pool tops = TricklePoolFiles.PrepareList(Full(Tops + "pool.tsv"));
            TricklePoolFiles.Pool blades = TricklePoolFiles.PrepareList(Full(Blades + "pool.tsv"));
            TricklePoolFiles.Pool mixed = TricklePoolFiles.PrepareList(Full(Blades + "pool-mixed.tsv"));

            Assert.Equal(20, mixed.Genomes.Count);
            Assert.True(mixed.Genomes.Count <= RunConfig.MaxTricklePool);
            Assert.All(mixed.Weights, w => Assert.Equal(0.05, w));
            Assert.Equal(tops.OriginNames, mixed.OriginNames);
            Assert.Equal(tops.Lines.Concat(blades.Lines).ToArray(), mixed.Lines.ToArray());
            Assert.Equal(MixedPoolHash, mixed.Hash);
        }

        // ------------------------------------------------------------------ the nursery

        [LocalDataTreeFact("rounds/nursery/pools/leaves-3f6c390cd561cb09.tsv.gz")]
        public void TheNurseryTakesTheTenAsABredPoolBesideLeaves()
        {
            Genome[] blades = Parents.Select(p => GenomeJson.Read(Lf(Blades + BladeOf(p)))).ToArray();

            // The feeder rule's two questions, asked of the developed body: the blades carry no absorptive or photosynthetic tissue.
            foreach (Genome g in blades)
                Assert.DoesNotContain(Developer.Develop(g).Parts, p => p.CellTypeId == CellTypeIds.Absorptive || p.CellTypeId == CellTypeIds.Photosynthetic);

            // A nursery pool is a body, a tab and a genome a line; the pool reader walks every line and the refusal finds none.
            string pool = LeafTankFixtures.WriteBredPool(_dir, blades.Length, k => blades[k]);
            string[] rows = File.ReadAllLines(Full(pool));
            Assert.Equal(10, rows.Length);
            Assert.Empty(PoolLines.Refused(rows, _ => null));

            // And the nursery's own check, beside leaves: it refuses a bred body that feeds otherwise before anything swims, and swims these.
            const string leaves = "{\"pool\": \"" + LeafTankFixtures.LeafPool + "\", \"count\": 4, \"spentUnitsPerCubicMetre\": 0}";
            (string config, string hash) = LeafTankFixtures.WriteConfig(_dir, LeafTankFixtures.Config());
            string recipe = LeafTankFixtures.Write(_dir, "blades.json", LeafTankFixtures.RecipeJson(config, hash, pool, 5, leaves, 0.5));
            RestepOutcome o = LeafTankFixtures.Swim(_dir, recipe, 10, 5, 0, null);
            Assert.Equal(5, o.Members.Length);
            Assert.All(o.Members, m => Assert.True(m.Landed));
        }
    }
}
