using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Evosim.Core;
using Evosim.Nursery;
using Evosim.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Nursery.Tests
{
    /// <summary>
    /// The leaf tank (logbook/specs/nursery-leaf-tank-plan.md, step 2): the recipe's optional <c>leaves</c> section, the leaf pool's reader,
    /// and the leaves laid in the nursery's own <c>TankEpisode</c>, swum in-process through <see cref="Restep"/> on a small tank whose world
    /// config the test writes on this build (LeafTankFixtures.cs).
    /// </summary>
    [Collection("restep")]
    public class LeafTankTests : IDisposable
    {
        private readonly ITestOutputHelper _out;
        private readonly string _dir = RestepFixtures.NewDir("leaf-tank");

        public LeafTankTests(ITestOutputHelper output) => _out = output;

        public void Dispose() => Directory.Delete(_dir, true);

        private static string LeavesJson(int count = 12, double spent = 0.5, string pool = LeafTankFixtures.LeafPool) =>
            "{\"pool\": \"" + pool + "\", \"count\": " + count + ", \"spentUnitsPerCubicMetre\": " + spent.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "}";

        /// <summary>A recipe on the test's tank, its three bred bodies and the leaves given (null: the section left out); its path.</summary>
        private string Recipe(string leaves, double seconds = 2, Func<int, Genome> bred = null, string name = "recipe.json", bool today = false,
            string overrides = null, double? purse = null, string search = null)
        {
            (string config, string hash) = LeafTankFixtures.WriteConfig(_dir, LeafTankFixtures.Config(leaves: !today));
            string pool = LeafTankFixtures.WriteBredPool(_dir, 3, bred);
            return LeafTankFixtures.Write(_dir, name, LeafTankFixtures.RecipeJson(config, hash, pool, 3, leaves, seconds, purseSeconds: purse, overrides: overrides, search: search));
        }

        // ------------------------------------------------------------------ the section

        /// <summary>
        /// Every recipe on file, and the hash the nursery build before the section gave it (HEAD ff3fecbb's Evosim.Nursery.dll, and r59 step 6's 4e9b22b2 for the -w4 and leaf-drag recipes, D185's move's --check for the -w5 ones, round 62's move's --check for brain2-w7, reading each
        /// with <c>Recipe.Read</c>). A recipe without leaves is written without the section, so each hashes as it did.
        /// </summary>
        [Theory]
        [InlineData("accept-1gen-w2.json", "1b72fa555e25784c90e7add6ca6d889cd411c505a9a5823753a52a41c7e8dc52")]
        [InlineData("accept-1gen-w3.json", "00c465e86196062d5ad1f5bac31ad676cbba55770669fd388e7983f5432d9920")]
        [InlineData("accept-1gen-w4.json", "1f08cc818420a0e5d49a19f3fe69c49c4312fbf70ade509ed50e3625382579cf")]
        [InlineData("accept-1gen-w5.json", "5aa64b216361f1b5de167452ab549f2cbd6838f29539d0c01421f4c934393984")]
        [InlineData("accept-1gen.json", "3f88588a33a08dea123da23d04766c2b622d88f136a90a5b205953ba483bf309")]
        [InlineData("accept-stop-w2.json", "cf7b0bc14f52dadc56e57b67e406a5160f36df8a22c0404626028e3d19b070dd")]
        [InlineData("accept-stop-w3.json", "fc48769806b6554c6bb4d94dfc6160245d865f35bc5d80fb8b11ff68b5ec619f")]
        [InlineData("accept-stop-w4.json", "a3f364db3411181eda7908ada1f5fe1aa87463389d552d77cb9a268cd384f3ed")]
        [InlineData("accept-stop-w5.json", "3e8eea9fd266d732d2e7e75b7a4394b2e8b9ffccd2b4ad14d943bc78a8823f58")]
        [InlineData("accept-stop.json", "36bfe51a2142ca439e91807dd82d8059b126a7e0492061a86a48b24f787ae050")]
        [InlineData("brain2-nocross-w2.json", "8fba9674d6653a7ed7178e0767ba4306ede3c8cfc9df873cd10e916e65509f89")]
        [InlineData("brain2-nocross-w3.json", "956ba80df390f3f4050432be6293c4421b8aab4a19fbb0fc7481d30502e3be1d")]
        [InlineData("brain2-nocross-w4.json", "dd2f5190667baa40d228827aac2ccbf816e70e4752840177f6f8a4f57a137018")]
        [InlineData("brain2-nocross-w5.json", "53956c9ac8c1385ad7443e51b00c9cd98b3f19bdcd18eb0ee012b5f115f4dbe8")]
        [InlineData("brain2-nocross.json", "907fdf7ce3ba3eee5592d968afd12c32eb80a178e4cb65c8a302d572743c312a")]
        [InlineData("brain2-nodrive-w2.json", "5ec7503af7378603c74f472a5046e0b331fde29c19469866fbe8bd85e3a1000d")]
        [InlineData("brain2-nodrive-w3.json", "a45d5f9c824dcd29759bdf5aeb3f8be13fed4c6cc12d9274d1d7311603adcc71")]
        [InlineData("brain2-nodrive-w4.json", "c791e4950620bca5983b200d922c9ac9973b909793adfce99ed0c951b0bce1dc")]
        [InlineData("brain2-nodrive-w5.json", "3d36e18c42cf4be869e18500cdbbe689dae65330c0e0e3ca4c1f3f705b9a521b")]
        [InlineData("brain2-nodrive.json", "f7a83c9d65f1ce6695fffb6d150b3852a313ce2b9a712ce2e7466647e0bf2770")]
        [InlineData("brain2-rep2-w2.json", "693ac233e0bc8729e012efa21dc754de6b13b5fa1a992c6ddd0811ee9d2242b9")]
        [InlineData("brain2-rep2-w3.json", "c77426334dc51780612ec4550f65d6bb5aeb7c29c92e48baaf248a327317ced7")]
        [InlineData("brain2-rep2-w4.json", "af9ac2c8c136ce8044faf3bc67bcb13696320ce655740dd2e2f95bc926dc5797")]
        [InlineData("brain2-rep2-w5.json", "04bb5ba5b2b041292611bdcc33193a0431895b57bb089ce90dc4712a0d95fd30")]
        [InlineData("brain2-rep2.json", "52a4d23ae49d85851313bbccddadff110ae663bd871396cae0acc04d8a81090b")]
        [InlineData("brain2-w2.json", "8336b6b458a16d6b6654c1c55e5ed33ebe1374da49bd50691a7c101facf2a7fd")]
        [InlineData("brain2-w3-t16-hold.json", "4656727a1a8b3c59b322015449fee94c376fae9ac1a3f69fc524293115c1d796")]
        [InlineData("brain2-w3.json", "63b5e4e588e937c3e510ffb979087915ee49de14e6e2d7742a60dc878bea61ee")]
        [InlineData("brain2-w4-t16-hold.json", "6efbed2430c4f011854b5f102748da976ca35d42fb6a5430b43a97d326d5a26c")]
        [InlineData("brain2-w5-t16-hold.json", "aeac25e1db1b58d8800de4a72e00884fdefb717f389eeb2221c9fdafed19fec0")]
        [InlineData("brain2-w4.json", "a9bb4c84eb47a38f0187ff9ff2949a7d7d7b745ef531ee6304c00ab2565070f2")]
        [InlineData("brain2-w5.json", "b45716afab91a524d8e42954a71bd491f2ae56dc298421ad93255547253f5001")]
        [InlineData("brain2-w7.json", "742c8ec75fd53c7f0e27c0f39b1a9fd1b6d207ac09595f0e07ec8f70b6f1ef62")]
        [InlineData("tank-demo-w2.json", "26070372d689917c92f6c76a8e4ad6b5a21f7c52a99cc409311537634e9c31bd")]
        [InlineData("brain2-w8.json", "bd02cb488e297d605c4345f284cc34925c2afdf4ecfdd982556f0d1e03b30822")]
        [InlineData("brain2-w9.json", "8f1c10f7c78d15c18ad27dc9357c5554abd262feabdd4c12070e3b095db49e65")]
        [InlineData("brain2-w10.json", "6f83a22a1cc53d04799ec308446c3ca445fe7353e241956c3d427d5a63cba1df")]
        [InlineData("brain2.json", "ef0743270c7c48a3ea671320892302b23482e63432391e0e54d3e4ec4a19d61a")]
        [InlineData("leaf-drag-off.json", "fb0fc19381dc0782b6bba7548beca7a9276881ba642d0200019a95af97f83f5a")]
        [InlineData("leaf-drag-skin-0p05.json", "c48e7a64a7eb66a8ed757f9c48ed6194686ee691d632ccabf5204a594feb1648")]
        [InlineData("leaf-drag-skin-0p5.json", "7abf8a6aab99544397cd61c2cddae3d233f723673a656ccefd82a4e40d5b21b3")]
        [InlineData("leaf-drag-spin-0p05.json", "34383099db69f1e0357d0d2641c0651ef578137bc541dc6fa12ea9d3c592086b")]
        [InlineData("leaf-drag-spin-0p5.json", "2f037de190f9f03905c375b90d724a25fde2b08af3253df6c2c0efa6c5b7e8dd")]
        public void ARecipeOnFileHashesAsItDidBeforeTheSection(string file, string hash)
        {
            Recipe r = Nursery.Recipe.Read(Path.Combine(RestepFixtures.Root, "rounds", "nursery", file));
            Assert.Null(r.Leaves);
            Assert.DoesNotContain("\"leaves\"", r.Canonical());
            Assert.Equal(hash, r.Hash());
        }

        /// <summary>Every leaf recipe a run has registered, and the hash the registry recorded for it: D182's patches are written only when set, so each hashes as it did.</summary>
        [Theory]
        [InlineData("leaf-strength.json", "649c2dac2627cb12141696954c5d764381d9e22777d053a454d1c0ec5a63b56b")]
        [InlineData("leaf-direction.json", "19fcb14ed1760d3ad89f45d85d49b6357dcad4c81239da08196a586eb08750df")]
        [InlineData("leaf-strength-tops.json", "caa6c66284e707ef787826599881cc970fab67f0a3eb7f3e134529344d4db265")]
        [InlineData("leaf-direction-tops.json", "3fd5463c3a7a4bb6c474c6bb5fa0fb9cdaa71c5add63dc7756195dbecf08e608")]
        [InlineData("leaf-direction-sparse.json", "a129fa9f1191d156eff3ff3196a9e6ee453ad48e480ad4599cac92639cf2e703")]
        [InlineData("leaf-strength-sparse.json", "9e2806f52c3b88dcdd70c82e728ad6b0f2ca4f2fa9bb2d35d3cebd77a3f91b05")]
        [InlineData("leaf-direction-sparse3.json", "3e2b149d23c24017df83a72df7fefea77efabf0bddabfbd7a641e6ec3a0c85f3")]
        [InlineData("leaf-direction-bodies-smoke.json", "09332aef1cd0ac65f1efe25765ffe0d0e60dc267e822f59c2ef11f675c04831b")]
        [InlineData("leaf-direction-return.json", "3c3f3e9cf62aa8e57279aeb7212d08bee5697415bbc9d8ca6d8eb507c1c479f4")]
        [InlineData("leaf-direction-return-s2.json", "7a6d76f9878829e02bd7dbfcdb4679eae8a783f22c1bc9a3c7672179563139c0")]
        public void ARegisteredLeafRecipeHashesAsItDidBeforeThePatches(string file, string hash)
        {
            Recipe r = Nursery.Recipe.Read(Path.Combine(RestepFixtures.Root, "rounds", "nursery", file));
            Assert.NotNull(r.Leaves);
            Assert.Null(r.Leaves.Patches);
            Assert.DoesNotContain("\"patches\"", r.Canonical());
            Assert.Equal(hash, r.Hash());
        }

        [Fact]
        public void EveryRecipeOnFileIsInTheListAbove()
        {
            string[] files = Directory.GetFiles(Path.Combine(RestepFixtures.Root, "rounds", "nursery"), "*.json")
                .Where(f => Path.GetFileName(f) != "room.json" && Nursery.Recipe.Read(f).Leaves == null)
                .Select(Path.GetFileName).OrderBy(f => f, StringComparer.Ordinal).ToArray();
            string[] listed = typeof(LeafTankTests).GetMethod(nameof(ARecipeOnFileHashesAsItDidBeforeTheSection))
                .GetCustomAttributes(typeof(InlineDataAttribute), false).Cast<InlineDataAttribute>()
                .Select(a => (string)a.GetData(null).First()[0]).OrderBy(f => f, StringComparer.Ordinal).ToArray();
            Assert.Equal(files, listed);
        }

        [LocalDataTreeFact("rounds/nursery/pools/leaves-3f6c390cd561cb09.tsv.gz")]
        public void TheSectionReadsBackToItselfAndMovesTheHash()
        {
            Recipe without = Nursery.Recipe.Read(Recipe(null));
            Recipe with = Nursery.Recipe.Read(Recipe(LeavesJson(), name: "leaves.json"));

            Assert.NotNull(with.Leaves);
            Assert.Equal(LeafTankFixtures.LeafPool, with.Leaves.PoolPath);
            Assert.Equal(12, with.Leaves.Count);
            Assert.Equal(0.5, with.Leaves.SpentUnitsPerCubicMetre);
            Assert.NotEqual(without.Hash(), with.Hash());
            Assert.Contains("\"leaves\"", with.Canonical());
            Assert.DoesNotContain("\"leaves\"", without.Canonical());

            string text = with.Canonical();
            Recipe again = Nursery.Recipe.Read(LeafTankFixtures.Write(_dir, "canonical.json", text));
            Assert.Equal(text, again.Canonical());
            Assert.Equal(with.Hash(), again.Hash());

            // Each of its numbers moves the hash.
            Assert.NotEqual(with.Hash(), Nursery.Recipe.Read(Recipe(LeavesJson(count: 13), name: "c13.json")).Hash());
            Assert.NotEqual(with.Hash(), Nursery.Recipe.Read(Recipe(LeavesJson(spent: 0.25), name: "s25.json")).Hash());

            // Null is the section left out: no leaves, and the same text.
            Recipe nulled = Nursery.Recipe.Read(Recipe("null", name: "null.json"));
            Assert.Null(nulled.Leaves);
            Assert.Equal(without.Canonical(), nulled.Canonical());
        }

        [LocalDataTreeTheory("rounds/nursery/pools/leaves-3f6c390cd561cb09.tsv.gz")]
        [InlineData("{\"pool\": \"" + LeafTankFixtures.LeafPool + "\", \"spentUnitsPerCubicMetre\": 0}", "leaves is missing count")]
        [InlineData("{\"pool\": \"" + LeafTankFixtures.LeafPool + "\", \"count\": 1, \"spentUnitsPerCubicMetre\": 0, \"extra\": 1}", "leaves has unknown extra")]
        [InlineData("{\"pool\": \"" + LeafTankFixtures.LeafPool + "\", \"count\": 0, \"spentUnitsPerCubicMetre\": 0}", "leaves.count must be 1 or more")]
        [InlineData("{\"pool\": \"" + LeafTankFixtures.LeafPool + "\", \"count\": 1.5, \"spentUnitsPerCubicMetre\": 0}", "leaves.count is a whole number")]
        [InlineData("{\"pool\": \"" + LeafTankFixtures.LeafPool + "\", \"count\": 2895, \"spentUnitsPerCubicMetre\": 0}", "holds 2894 leaves")]
        [InlineData("{\"pool\": \"" + LeafTankFixtures.LeafPool + "\", \"count\": 1, \"spentUnitsPerCubicMetre\": -1}", "leaves.spentUnitsPerCubicMetre must be 0 or more")]
        [InlineData("{\"pool\": \"" + LeafTankFixtures.LeafPool + "\", \"count\": 1, \"spentUnitsPerCubicMetre\": \"1\"}", "leaves.spentUnitsPerCubicMetre is a number")]
        [InlineData("{\"pool\": null, \"count\": 1, \"spentUnitsPerCubicMetre\": 0}", "leaves.pool names the leaf pool")]
        [InlineData("{\"pool\": \"rounds/nursery/pools/97d4a4fc12a91017.tsv\", \"count\": 1, \"spentUnitsPerCubicMetre\": 0}", "a leaf pool is gzipped text")]
        [InlineData("{\"pool\": \"rounds/nursery/pools/none.tsv.gz\", \"count\": 1, \"spentUnitsPerCubicMetre\": 0}", "which does not exist")]
        [InlineData("{\"pool\": \"C:/leaves.tsv.gz\", \"count\": 1, \"spentUnitsPerCubicMetre\": 0}", "paths are relative to the repository root")]
        [InlineData("{\"pool\": \"../leaves.tsv.gz\", \"count\": 1, \"spentUnitsPerCubicMetre\": 0}", "leaves the repository")]
        [InlineData("3", "leaves is an object")]
        public void AFaultInTheSectionIsRefused(string leaves, string said)
        {
            string path = Recipe(leaves);
            var e = Assert.Throws<InvalidDataException>(() => Nursery.Recipe.Read(path));
            Assert.Contains(said, e.Message);
        }

        private static string PatchedJson(int count = 12, string patches = "{\"count\": 3, \"radius\": 4.5, \"ring\": 6.5}") =>
            "{\"pool\": \"" + LeafTankFixtures.LeafPool + "\", \"count\": " + count + ", \"spentUnitsPerCubicMetre\": 0, \"patches\": " + patches + "}";

        [LocalDataTreeFact("rounds/nursery/pools/leaves-3f6c390cd561cb09.tsv.gz")]
        public void ThePatchesReadBackToThemselvesAndMoveTheHash()
        {
            Recipe plain = Nursery.Recipe.Read(Recipe(LeavesJson(spent: 0), name: "plain.json"));
            Recipe patched = Nursery.Recipe.Read(Recipe(PatchedJson(), name: "patched.json"));

            Assert.Null(plain.Leaves.Patches);
            Assert.Equal(3, patched.Leaves.Patches.Count);
            Assert.Equal(4.5, patched.Leaves.Patches.RadiusMetres);
            Assert.Equal(6.5, patched.Leaves.Patches.RingMetres);
            Assert.NotEqual(plain.Hash(), patched.Hash());
            Assert.DoesNotContain("\"patches\"", plain.Canonical());

            string text = patched.Canonical();
            Recipe again = Nursery.Recipe.Read(LeafTankFixtures.Write(_dir, "patched-canonical.json", text));
            Assert.Equal(text, again.Canonical());
            Assert.Equal(patched.Hash(), again.Hash());

            // Each of its numbers moves the hash; null is the field left out.
            Assert.NotEqual(patched.Hash(), Nursery.Recipe.Read(Recipe(PatchedJson(patches: "{\"count\": 2, \"radius\": 4.5, \"ring\": 6.5}"), name: "p2.json")).Hash());
            Assert.NotEqual(patched.Hash(), Nursery.Recipe.Read(Recipe(PatchedJson(patches: "{\"count\": 3, \"radius\": 4, \"ring\": 6.5}"), name: "r4.json")).Hash());
            Assert.NotEqual(patched.Hash(), Nursery.Recipe.Read(Recipe(PatchedJson(patches: "{\"count\": 3, \"radius\": 4.5, \"ring\": 6}"), name: "g6.json")).Hash());
            Assert.Equal(plain.Canonical(), Nursery.Recipe.Read(Recipe(PatchedJson(patches: "null"), name: "pnull.json")).Canonical());
        }

        [LocalDataTreeTheory("rounds/nursery/pools/leaves-3f6c390cd561cb09.tsv.gz")]
        [InlineData("{\"count\": 3, \"radius\": 4.5}", "leaves.patches is missing ring")]
        [InlineData("{\"count\": 3, \"radius\": 4.5, \"ring\": 6.5, \"turn\": 0}", "leaves.patches has unknown turn")]
        [InlineData("{\"count\": 0, \"radius\": 4.5, \"ring\": 6.5}", "leaves.patches.count must be 1 or more")]
        [InlineData("{\"count\": 13, \"radius\": 4.5, \"ring\": 6.5}", "more than the 12 leaves")]
        [InlineData("{\"count\": 3, \"radius\": 0, \"ring\": 6.5}", "leaves.patches.radius must be more than 0 m")]
        [InlineData("{\"count\": 3, \"radius\": 4.5, \"ring\": -1}", "leaves.patches.ring must be 0 m or more")]
        [InlineData("{\"count\": 2, \"radius\": 4.5, \"ring\": 0}", "the patches lie on one another")]
        [InlineData("{\"count\": 1.5, \"radius\": 4.5, \"ring\": 6.5}", "leaves.patches.count is a whole number")]
        [InlineData("3", "leaves.patches is an object")]
        public void AFaultInThePatchesIsRefused(string patches, string said)
        {
            string path = Recipe(PatchedJson(patches: patches));
            var e = Assert.Throws<InvalidDataException>(() => Nursery.Recipe.Read(path));
            Assert.Contains(said, e.Message);
        }

        [Fact]
        public void APatchCentreLiesOnTheRingAndThePatchesAreEvenlySpaced()
        {
            for (int p = 0; p < 3; p++)
            {
                (double x, double z) = LeafPatches.Centre(10, 6, 3, 0.4, p);
                Assert.Equal(6, Math.Sqrt((x - 10) * (x - 10) + (z - 10) * (z - 10)), 9);
                (double nx, double nz) = LeafPatches.Centre(10, 6, 3, 0.4, (p + 1) % 3);
                Assert.Equal(6 * Math.Sqrt(3), Math.Sqrt((x - nx) * (x - nx) + (z - nz) * (z - nz)), 9);
            }
            (double ox, double oz) = LeafPatches.Centre(10, 6, 3, 0, 0);
            Assert.Equal((16d, 10d), (Math.Round(ox, 9), Math.Round(oz, 9)));
        }

        [Fact]
        public void TheLayoutReadsTheGapTheGlassAndTheSmellFreeWater()
        {
            var layout = new LeafPatches.Layout();
            layout.Centres.Add((5, 10));
            layout.Centres.Add((15, 10));
            layout.Leaves.Add((5f, 10f, 1f, 0));
            layout.Leaves.Add((15f, 10f, 1f, 1));
            layout.Leaves.Add((6f, 10f, 1f, 0));
            (string line, double gap, double glass, double free) = LeafPatches.Describe("seed", layout, 10, 2);
            _out.WriteLine(line);
            Assert.Equal(7, gap, 6);                    // 15 - 6 less two radii
            Assert.Equal(4, glass, 6);                  // the leaf at x 15: 10 - 5 - 1
            Assert.Contains("leaves 2/1", line);
            // Three discs of 2 m, two overlapping, over a disc of 10 m: about (pi 4 + pi 4 + the second's lune) / pi 100 is smelt.
            Assert.InRange(free, 0.88, 0.93);
        }

        [LocalDataTreeFact("rounds/nursery/pools/leaves-3f6c390cd561cb09.tsv.gz")]
        public void TheLeavesLieInTheirPatchesAndAreTheRowsDrawnWithoutThem()
        {
            BodyView[] plain = SwimStart(Recipe(LeavesJson(count: 12, spent: 0), seconds: 0.5, name: "plain.json"), 0).Leaves;
            BodyView[] patched = SwimStart(Recipe(PatchedJson(), seconds: 0.5, name: "patched.json"), 0).Leaves;
            Assert.Equal(12, patched.Length);

            // The same rows: the same leaves at the same heights, grown the same (the test tank's bed is flat).
            Assert.Equal(plain.Select(l => (l.Y, l.Tissue)).OrderBy(t => t), patched.Select(l => (l.Y, l.Tissue)).OrderBy(t => t));

            // Some turn of the ring puts every leaf's centre inside a patch, four to a patch.
            double tank = Math.Sqrt(LeafTankFixtures.Config().WorldAreaSquareMetres / Math.PI);
            int[] best = null;
            for (int step = 0; step < 36000 && best == null; step++)
            {
                double turn = 2d * Math.PI * step / 36000;
                var counts = new int[3];
                bool all = true;
                foreach (BodyView leaf in patched)
                {
                    int hit = -1;
                    for (int p = 0; p < 3 && hit < 0; p++)
                    {
                        (double cx, double cz) = LeafPatches.Centre(tank, 6.5, 3, turn, p);
                        if ((leaf.X - cx) * (leaf.X - cx) + (leaf.Z - cz) * (leaf.Z - cz) <= 4.5 * 4.5 + 1e-3) hit = p;
                    }
                    if (hit < 0) { all = false; break; }
                    counts[hit]++;
                }
                if (all) best = counts;
            }
            Assert.NotNull(best);
            Assert.Equal(new[] { 4, 4, 4 }, best);
        }

        // ------------------------------------------------------------------ the pool

        [LocalDataTreeFact("rounds/nursery/pools/leaves-3f6c390cd561cb09.tsv.gz")]
        public void TheCommittedPoolReadsEveryRow()
        {
            string path = Path.Combine(RestepFixtures.Root, LeafTankFixtures.LeafPool.Replace('/', Path.DirectorySeparatorChar));
            LeafPool pool = LeafPool.Read(path);
            Assert.Equal(2894, pool.Leaves.Count);
            Assert.Equal(2894, LeafPool.CountRows(path));
            Assert.Equal(154074L, pool.Leaves[0].Id);
            Assert.Equal(-10.74f, pool.Leaves[0].HeightY);
            Assert.Equal(1f, pool.Leaves[0].BodyFraction);
            Assert.All(pool.Leaves, l => Assert.InRange(l.HeightY, -19.2f, -6.88f));
            Assert.All(pool.Leaves, l => Assert.Contains(l.Genome.Nodes, n => n.CellTypeId == CellTypeIds.Photosynthetic));
            // The pool is made by the bodies' flags (positions-read.py's guild), so a genome may carry an absorptive node its body never grows.
        }

        private string Gz(params string[] lines)
        {
            string path = Path.Combine(_dir, "pool-" + Guid.NewGuid().ToString("N") + ".tsv.gz");
            using (var file = File.Create(path))
            using (var gz = new GZipStream(file, CompressionLevel.Fastest))
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(string.Join("\n", lines) + "\n");
                gz.Write(bytes, 0, bytes.Length);
            }
            return path;
        }

        private static readonly string Genome = GenomeJson.Write(RestepFixtures.Founder(1));

        [Theory]
        [InlineData("7\t-3\t1", "line 2 has 3 fields")]
        [InlineData("x\t-3\t1\tG", "line 2: the id \"x\" is not a whole number")]
        [InlineData("1\t-3\t1\tG", "line 2: the id 1 is on line 1 too")]
        [InlineData("7\t0.5\t1\tG", "line 2: the height \"0.5\"")]
        [InlineData("7\tNaN\t1\tG", "line 2: the height \"NaN\"")]
        [InlineData("7\t-3\t0\tG", "line 2: the body fraction \"0\"")]
        [InlineData("7\t-3\t1.5\tG", "line 2: the body fraction \"1.5\"")]
        [InlineData("7\t-3\t1\t{\"format\": 1}", "line 2: the genome does not load")]
        [InlineData("", "line 2 is empty")]
        public void AFaultInAPoolRowIsRefusedNamingItsLine(string second, string said)
        {
            string path = Gz("1\t-2.5\t0.75\t" + Genome, second.Replace("\tG", "\t" + Genome), "3\t-4\t1\t" + Genome);
            var e = Assert.Throws<InvalidDataException>(() => LeafPool.Read(path));
            Assert.Contains(said, e.Message);
        }

        [Fact]
        public void AWellMadePoolReadsAsWritten()
        {
            LeafPool pool = LeafPool.Read(Gz("1\t-2.5\t0.75\t" + Genome, "3\t-4\t1\t" + Genome));
            Assert.Equal(new[] { 1L, 3L }, pool.Leaves.Select(l => l.Id));
            Assert.Equal(new[] { -2.5f, -4f }, pool.Leaves.Select(l => l.HeightY));
            Assert.Equal(new[] { 0.75f, 1f }, pool.Leaves.Select(l => l.BodyFraction));
            Assert.Equal(Genome, GenomeJson.Write(pool.Leaves[1].Genome));
            Assert.Throws<InvalidDataException>(() => LeafPool.Read(Gz()));
        }

        // ------------------------------------------------------------------ the tank

        /// <summary>The leaves of an episode as the watcher saw them before the first step, with the rest of the water.</summary>
        private sealed class Start
        {
            public int Living, Members, Placed, Corpses;
            public double Snow, Spent;
            public BodyView[] Leaves;
        }

        private Start SwimStart(string recipe, int episode)
        {
            var start = new Start();
            var seen = new LeafTankFixtures.Seen
            {
                OnBegin = p =>
                {
                    start.Living = p.LivingCount;
                    start.Members = p.MemberCount;
                    start.Placed = p.PlacedCorpseCount;
                    start.Corpses = p.CorpseCount;
                    start.Snow = p.SnowJoules;
                    start.Spent = p.SpentUnits;
                    start.Leaves = p.Others().ToArray();
                    return true;
                },
            };
            LeafTankFixtures.Swim(_dir, recipe, 3, 3, episode, seen);
            Assert.True(seen.Took);
            return start;
        }

        [LocalDataTreeFact("rounds/nursery/pools/leaves-3f6c390cd561cb09.tsv.gz")]
        public void ATankWithLeavesHoldsTheBredBodiesTheLeavesAndNothingElse()
        {
            Start with = SwimStart(Recipe(LeavesJson(count: 12, spent: 0.5), seconds: 0.5), 0);
            Start without = SwimStart(Recipe(null, seconds: 0.5, name: "none.json"), 0);

            Assert.Equal(3, with.Members);
            Assert.Equal(3 + 12, with.Living);
            Assert.Equal(12, with.Leaves.Length);
            Assert.Equal(0, with.Placed);
            Assert.Equal(0, with.Corpses);
            Assert.Equal(0d, with.Snow);
            Assert.Equal(3, without.Living);
            Assert.Empty(without.Leaves);

            // Every leaf is a leaf, in the water, distinct, and paid the growth it owed.
            RunConfig config = LeafTankFixtures.Config();
            double radius = Math.Sqrt(config.WorldAreaSquareMetres / Math.PI);
            foreach (BodyView leaf in with.Leaves)
            {
                _out.WriteLine(FormattableString.Invariant($"leaf {leaf.Id} at ({leaf.X:0.00}, {leaf.Y:0.00}, {leaf.Z:0.00}) reserve {leaf.Reserve:0.0} tissue {leaf.Tissue:0.0} of {leaf.AdultTissue:0.0}"));
                Assert.True(leaf.Photosynthetic && !leaf.Absorptive);
                Assert.True(Math.Sqrt((leaf.X - radius) * (leaf.X - radius) + (leaf.Z - radius) * (leaf.Z - radius)) <= radius);
                Assert.InRange(leaf.Y, -config.WorldDepthMetres, 0d);
                Assert.True(leaf.Reserve + leaf.Tissue >= leaf.AdultTissue, "a leaf was not paid its growth");
            }
            Assert.Equal(12, with.Leaves.Select(l => (l.X, l.Z)).Distinct().Count());

            // The water's spent units: the recipe's density over the live water, on top of what the world laid itself.
            double volume = Math.PI * radius * radius * config.WorldDepthMetres;
            Assert.InRange((with.Spent - without.Spent) / (0.5 * volume), 0.9, 1.1);
        }

        [LocalDataTreeFact("rounds/nursery/pools/leaves-3f6c390cd561cb09.tsv.gz")]
        public void TheLeavesAreTheSameForOneSeedAndDifferBetweenSeeds()
        {
            string recipe = Recipe(LeavesJson(count: 12, spent: 0), seconds: 0.5);
            BodyView[] a = SwimStart(recipe, 0).Leaves, again = SwimStart(recipe, 0).Leaves, other = SwimStart(recipe, 1).Leaves;
            Assert.Equal(a.Select(l => (l.X, l.Y, l.Z, l.Tissue)), again.Select(l => (l.X, l.Y, l.Z, l.Tissue)));
            Assert.Empty(a.Select(l => (l.X, l.Z)).Intersect(other.Select(l => (l.X, l.Z))));
            Assert.NotEqual(a.Select(l => l.Tissue).OrderBy(t => t), other.Select(l => l.Tissue).OrderBy(t => t));
        }

        /// <summary>
        /// The test's tank without leaves, swum on the build before the section (HEAD ff3fecbb, the same tank and recipe, read by the same
        /// watcher): each member's landing, survival, start, end, net, path, work and upkeep, then its root's place at the first physics step.
        /// A recipe without leaves draws nothing new and places nothing new, so it gives these to the bit. Work and upkeep are re-recorded
        /// with main's ledger read (294c08a5): these founders carry no absorptive tissue, so the build before it read both as 0.
        /// </summary>
        private static readonly string[][] Today =
        {
            new[]
            {
                "True False 716.0000205039978 0 -716.0000205039978 13.426213022532952 28.845340728759766 5.387131690979004 | 4.284372329711914 -8.630369804700415 6.789320468902588",
                "True True 716.0000205039978 649.5957141172503 -66.40430638674752 14.64557630274106 59.169715881347656 7.234591007232666 | 3.468064308166504 -8.630371197693583 17.058122634887695",
                "True False 716.0000205039978 0 -716.0000205039978 10.881377360957613 47.26533889770508 1.4160001277923584 | 6.141165733337402 -8.630346770331045 9.837831497192383",
            },
            new[]
            {
                "True False 716.0000205039978 0 -716.0000205039978 8.438166647128583 47.26533889770508 1.4160001277923584 | 8.574249267578125 -8.630346770331045 12.48803424835205",
                "True False 716.0000205039978 0 -716.0000205039978 13.86554595142158 28.845340728759766 5.387131690979004 | 3.497745990753174 -8.630369804700415 14.700685501098633",
                "True True 716.0000205039978 649.5957141172503 -66.40430638674752 9.019156183841988 59.169715881347656 7.234591007232666 | 14.003950119018555 -8.630371197693583 2.752346992492676",
            },
        };

        [Fact]
        public void ARecipeWithoutLeavesSwimsTodaysTank()
        {
            string recipe = Recipe(null, today: true);
            for (int e = 0; e < Today.Length; e++)
            {
                var seen = new LeafTankFixtures.Seen();
                RestepOutcome o = LeafTankFixtures.Swim(_dir, recipe, 3, 3, e, seen);
                string[] got = o.Members.Select((m, k) => LeafTankFixtures.Line(m) + " | " + LeafTankFixtures.Line(seen.FirstRoots[k])).ToArray();
                foreach (string line in got) _out.WriteLine(line);
                Assert.Equal(Today[e], got);
            }
        }

        // ------------------------------------------------------------------ D241: placed grown under the growth climb

        // The nursery's world with the climb on, by the recipe's own override (world.overrides), as a recipe turns it on. The config file keeps T = 0,
        // so its hash is the recorded fixtures'.
        private const string Climb = "{\"GrowthAdultSeconds\": 120}";

        /// <summary>The synthetic founder as a juvenile, born at 0.65 of its adult (as the Core fixtures set it), so that "grown" has growth to place.</summary>
        private static Genome Juvenile(int k)
        {
            Genome g = RestepFixtures.Founder((ulong)k + 1);
            g.Reproduction = new ReproductionTraits { BroodSize = 1, BirthInvestment = 0.65f / (1f - new RunConfig().NewbornReserveFraction) };
            return g;
        }

        /// <summary>What each member held after its purse and before its first step.</summary>
        private sealed class Held
        {
            public double[] Reserve, Tissue, Adult, Fraction, Watts;
        }

        private Held SwimHeld(string recipe)
        {
            var held = new Held();
            var seen = new LeafTankFixtures.Seen
            {
                OnBegin = p =>
                {
                    int n = p.MemberCount;
                    held.Reserve = new double[n];
                    held.Tissue = new double[n];
                    held.Adult = new double[n];
                    held.Fraction = new double[n];
                    held.Watts = new double[n];
                    for (int m = 0; m < n; m++)
                    {
                        held.Reserve[m] = p.Reserve(m);
                        held.Tissue[m] = p.Tissue(m);
                        held.Adult[m] = p.AdultTissue(m);
                        held.Fraction[m] = p.BodyFraction(m);
                        held.Watts[m] = p.AdultStandingWatts(m);
                    }
                    return true;
                },
            };
            LeafTankFixtures.Swim(_dir, recipe, 3, 3, 0, seen);
            Assert.True(seen.Took);
            return held;
        }

        [Fact]
        public void UnderTheClimbAGrownBredBodyIsAdultBeforeItsFirstStep()
        {
            // A purse far above the founder's endowment, so that the purse rule gives (target less the reserve is positive) and the reserve is the target.
            const double purse = 10000;
            Held zero = SwimHeld(Recipe(null, seconds: 0.5, bred: Juvenile, name: "zero.json", purse: purse));
            Held climb = SwimHeld(Recipe(null, seconds: 0.5, bred: Juvenile, name: "climb.json", purse: purse, overrides: Climb));

            Assert.Equal(3, zero.Tissue.Length);
            Assert.Equal(3, climb.Tissue.Length);
            for (int m = 0; m < 3; m++)
            {
                _out.WriteLine(FormattableString.Invariant(
                    $"member {m}: T=0 tissue {zero.Tissue[m]:0.0} of {zero.Adult[m]:0.0} (fraction {zero.Fraction[m]:0.000}), reserve {zero.Reserve[m]:0.0}; climb tissue {climb.Tissue[m]:0.0} of {climb.Adult[m]:0.0} (fraction {climb.Fraction[m]}), reserve {climb.Reserve[m]:0.0}"));

                // At T = 0 nothing changed: the body is its newborn, and the growth it owes is in its reserve.
                Assert.True(zero.Fraction[m] < 1d, "the fixture's body is not a juvenile, so this test places nothing");
                Assert.True(zero.Tissue[m] < zero.Adult[m]);
                Assert.True(zero.Reserve[m] > purse * zero.Watts[m], "T = 0 paid the owed tissue on top of the purse");

                // Under the climb it is the adult already, and the reserve is the purse seconds of the adult's standing watts alone.
                Assert.Equal(climb.Adult[m], climb.Tissue[m]);
                Assert.Equal(1d, climb.Fraction[m]);
                Assert.Equal(zero.Adult[m], climb.Adult[m]);
                Assert.InRange(climb.Reserve[m] / (purse * climb.Watts[m]), 1d - 1e-9, 1d + 1e-9);
            }
        }

        [LocalDataTreeFact("rounds/nursery/pools/leaves-3f6c390cd561cb09.tsv.gz")]
        public void UnderTheClimbALaidLeafIsAdultAndHoldsNoReserveGiven()
        {
            Start zero = SwimStart(Recipe(LeavesJson(count: 12, spent: 0), seconds: 0.5, name: "zero.json"), 0);
            Start climb = SwimStart(Recipe(LeavesJson(count: 12, spent: 0), seconds: 0.5, name: "climb.json", overrides: Climb), 0);

            // The same leaves in the same places: the climb moves no draw.
            Assert.Equal(12, climb.Leaves.Length);
            Assert.Equal(zero.Leaves.Select(l => (l.X, l.Z)), climb.Leaves.Select(l => (l.X, l.Z)));

            int juveniles = 0;
            for (int i = 0; i < 12; i++)
            {
                BodyView z = zero.Leaves[i], c = climb.Leaves[i];
                Assert.Equal(z.AdultTissue, c.AdultTissue);
                Assert.Equal(c.AdultTissue, c.Tissue);

                // What T = 0 gives a leaf as reserve to grow with, the climb gives as tissue: the two hold the same joules in all.
                Assert.Equal(z.Reserve + z.Tissue, c.Reserve + c.Tissue, 6);
                if (z.Tissue < z.AdultTissue)
                {
                    juveniles++;
                    Assert.True(c.Reserve < z.Reserve, "a laid leaf was given reserve under the climb");
                }
            }
            Assert.True(juveniles > 0, "no leaf of the draw is born smaller than its adult, so this test places nothing");
        }

        [Fact]
        public void TheStartIsGivenOnlyUnderTheClimbAndOnlyToAGenomeWithoutTheGene()
        {
            RunConfig on = LeafTankFixtures.Config(growthAdultSeconds: 120f);
            on.GrowthReserveStartSeconds = 45f;
            RunConfig off = LeafTankFixtures.Config();

            // With the climb, a gene-less genome is given the start, in place, and the written pool line carries it.
            Genome bare = RestepFixtures.Founder(1);
            Assert.Null(bare.GrowthReserveSeconds);
            Assert.Same(bare, GrowthStart.Give(bare, on));
            Assert.Equal(45f, bare.GrowthReserveSeconds);
            Assert.Equal(45f, GenomeJson.Read(GenomeJson.Write(bare)).GrowthReserveSeconds);

            // A genome with its own value keeps it.
            Genome own = RestepFixtures.Founder(2);
            own.GrowthReserveSeconds = 7f;
            GrowthStart.Give(own, on);
            Assert.Equal(7f, own.GrowthReserveSeconds);

            // At T = 0 the genome is left alone, to the byte.
            Genome plain = RestepFixtures.Founder(3);
            string text = GenomeJson.Write(plain);
            GrowthStart.Give(plain, off);
            Assert.Null(plain.GrowthReserveSeconds);
            Assert.Equal(text, GenomeJson.Write(plain));
        }

        /// <summary>
        /// A one-generation search under the climb, run as the program (a child process of the Release build, like the other swims: it is skipped
        /// unless EVOSIM_NURSERY_SWIM=1, and run with <c>--filter "Category=Swim"</c>). Its bred pool has two gene-less bodies and a third that
        /// carries 7.5 s; every member of the last generation, and every genome the exam is written from, carries the gene: the start
        /// (the config's 30 s) for the first two and 7.5 s for the third.
        /// </summary>
        [SwimFact(LeafTankFixtures.LeafPool)]
        [Trait("Category", "Swim")]
        public void UnderTheClimbTheSearchMembersCarryTheStartAndAGenomeKeepsItsOwn()
        {
            const string search = "{\"population\": 3, \"draws\": 1, \"generations\": 1, \"elites\": 1, \"tournament\": 2, \"perBody\": false, " +
                "\"evolve\": \"brain\", \"evolveEnvelope\": false, \"crossover\": null}";
            Genome Bred(int k)
            {
                Genome g = RestepFixtures.Founder((ulong)k + 1);
                if (k == 1) g.GrowthReserveSeconds = 7.5f;
                return g;
            }
            string recipe = Recipe(null, seconds: 0.5, bred: Bred, name: "search.json", overrides: Climb, search: search);
            string outDir = Path.Combine(_dir, "search-out");

            (int code, string stdout, string stderr) = RestepProcess.Run(15, "--recipe", recipe, "--out", outDir, "--threads", "1");
            _out.WriteLine(stdout);
            Assert.True(code == 0, "the search: " + stderr);

            // The last generation, whole: tank-population.jsonl, one member a line, its genome the last field.
            string[] members = File.ReadAllLines(Path.Combine(outDir, TankPopulation.FileName)).Where(l => l.Length > 0).ToArray();
            Assert.Equal(3, members.Length);
            foreach (string row in members)
            {
                int body = (int)Json.Parse(row)["body"].AsDouble();
                int at = row.IndexOf("\"genome\":", StringComparison.Ordinal) + "\"genome\":".Length;
                Genome genome = GenomeJson.Read(row.Substring(at, row.Length - at - 1));
                Assert.Equal(body == 1 ? 7.5f : 30f, genome.GrowthReserveSeconds);
            }

            // The pool the exam swims, as it was written: each line a body index, a tab and a genome.
            string[] exam = File.ReadAllLines(Path.Combine(outDir, "tank-exam-genomes.tsv")).Where(l => l.Length > 0).ToArray();
            Assert.NotEmpty(exam);
            foreach (string line in exam)
            {
                int tab = line.IndexOf('\t');
                int body = int.Parse(line.Substring(0, tab), System.Globalization.CultureInfo.InvariantCulture);
                Genome genome = GenomeJson.Read(line.Substring(tab + 1));
                Assert.Equal(body == 1 ? 7.5f : 30f, genome.GrowthReserveSeconds);
            }
        }

        // ------------------------------------------------------------------ the guard

        /// <summary>The synthetic founder with its root made of <paramref name="cell"/>.</summary>
        private static Genome RootOf(string cell, int k)
        {
            Genome g = RestepFixtures.Founder((ulong)k + 1);
            g.Nodes[g.RootIndex].CellTypeId = cell;
            return g;
        }

        [Fact]
        public void APoolsRefusedLinesAreNamedByTheirNumberInTheFile()
        {
            string Line(Genome g) => "0\t" + GenomeJson.Write(g);
            string[] lines = { Line(RootOf(CellTypeIds.Absorptive, 0)), Line(RestepFixtures.Founder(2)), "", Line(RootOf(CellTypeIds.Photosynthetic, 3)) };
            var refused = PoolLines.Refused(lines, g => g.Nodes[g.RootIndex].CellTypeId == CellTypeIds.Structural ? null : g.Nodes[g.RootIndex].CellTypeId);
            Assert.Equal(new[] { (1, CellTypeIds.Absorptive), (4, CellTypeIds.Photosynthetic) }, refused);
            Assert.Empty(PoolLines.Refused(lines, _ => null));
        }

        [LocalDataTreeTheory("rounds/nursery/pools/leaves-3f6c390cd561cb09.tsv.gz")]
        [InlineData(CellTypeIds.Photosynthetic, "carries photosynthetic tissue")]
        [InlineData(CellTypeIds.Absorptive, "carries absorptive tissue")]
        public void ABredBodyThatFeedsOtherwiseIsRefusedBesideLeaves(string cell, string said)
        {
            string recipe = Recipe(LeavesJson(count: 4, spent: 0), seconds: 0.5, bred: k => k == 1 ? RootOf(cell, k) : RestepFixtures.Founder((ulong)k + 1));
            var e = Assert.Throws<InvalidDataException>(() => LeafTankFixtures.Swim(_dir, recipe, 3, 3, 0, null));
            Assert.Contains(said, e.Message);
            Assert.Contains("the recipe's leaves refuse it", e.Message);

            // Without leaves the same bodies swim.
            string plain = Recipe(null, seconds: 0.5, bred: k => k == 1 ? RootOf(cell, k) : RestepFixtures.Founder((ulong)k + 1), name: "plain.json");
            Assert.Equal(3, LeafTankFixtures.Swim(_dir, plain, 3, 3, 0, null).Members.Length);
        }
    }
}
