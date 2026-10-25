using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Evosim.Core;
using Evosim.Farm;
using Xunit;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// The report's format, which is a contract with four scripts.
    /// </summary>
    /// <remarks>
    /// <c>analyse-arm.ps1</c> builds its name-to-index map from the table's header row;
    /// <c>parse-arm.ps1</c> splits the settings line on <c>" · "</c> and matches the pace line by
    /// a regular expression; <c>pace-survey.py</c> matches the same line; <c>watch-round.py</c>
    /// reads the first two cells of a row and looks for <c>**Ended</c>. A port that broke any of
    /// them would break the round reads without breaking a run, which is the worst way to break
    /// something.
    /// </remarks>
    public class ReportTests
    {
        /// <summary>
        /// The pace line, matched by the two regular expressions that read it in the wild.
        /// </summary>
        /// <remarks>
        /// <c>pace-survey.py</c>'s and <c>parse-arm.ps1</c>'s, transcribed. Note what the second
        /// one will not match: its simulated-seconds group is digits and commas, so a run that
        /// ends between whole seconds has never parsed. That is the Unity build's behaviour and it
        /// is kept — a port is not the place to change what a script can read.
        /// </remarks>
        private const string PaceSurveyPattern =
            @"(\d+) physics steps · ([\d.]+) simulated seconds · (\d+) births · ([\d.]+) min wall clock \(([\d.]+)x real time\)";

        private const string ParseArmPattern =
            @"(?<steps>[\d,]+)\s+physics steps\s*·\s*(?<sim>[\d,]+)\s+simulated seconds\s*·\s*(?<births>[\d,]+)\s+births\s*·\s*(?<wall>[\d.]+)\s+min wall clock\s*\((?<mult>[\d.]+)x real time\)";

        /// <summary>
        /// Round 42's footer, to the character — bar the space line, which names a different
        /// census.
        /// </summary>
        /// <remarks>
        /// The recorded line reads <c>contact pairs per physics step 463.6537 · sea bed mesh
        /// collider, lowest rock -69.16 m · floor pairs per physics step 4.3598</c>. This engine
        /// counts overlapping part pairs rather than PhysX contact pairs, tests a heightfield
        /// analytically rather than colliding against a mesh, and counts bodies on the bed or the
        /// glass rather than collider pairs — so all three are renamed for what they are, by the
        /// same rule that renamed four of the table's columns. Every other line, and the pace line
        /// in particular, is byte-for-byte the recorded one.
        /// </remarks>
        private const string Round42Footer =
@"**Ended:** budget reached.

Drag impulses limited: 0 (the coarse-step stabiliser; 0 means every step's drag was applied as computed)

Drive impulses limited: 0 (the joint-torque cap; 0 means every drive torque was applied as computed)

Shared space: on · wraps 0 · crowded stillbirths 189 · overlap pairs per physics step 463.6537 · sea bed relief, lowest rock on the disc -69.16 m · bed or glass bodies per physics step 4.3598

3000000 physics steps · 30000 simulated seconds · 8823 births · 825.9 min wall clock (0.6x real time).
wall split: physics 37%, world 9%, harness 54%, writers 0%, other 0%
harness split: reconcile 0%, finite 0%, read 27%, control 22%, fluid 32%, contacts 0%, settle 3%, trace 14%, metabolise 0%, growth 0%, other 0%
fluid split: gather 13%, water 15%, compute 42%, apply 31%
fluid per link-step: 1.4 µs (5,960,466,800 link-steps).
harness per body-step: 11.5 µs (2,309,857,800 body-steps).

**Fastest creature seen at any point: 1.2429 m/s, at t=14.5 s.**";

        // ------------------------------------------------------------------ the header

        /// <summary>
        /// Round 42's own settings line, rebuilt from its environment — the two ported tokens and
        /// D102's apart.
        /// </summary>
        /// <remarks>
        /// The world is constructed for it, because the space token is read off the world the run
        /// produced rather than off the launcher: the tank's radius, the patch width and the whole
        /// bed token are derived there, and a header built from the environment alone could
        /// describe a box the simulation does not have.
        /// </remarks>
        [Fact]
        public void Round42SettingsLineIsTheRecordedOneWithTheTwoPortedTokens()
        {
            string recordedReport = RecordedRound42Report();
            if (recordedReport == null)
            {
                Console.WriteLine("runs/r42-s1.md is not on this machine (run reports are gitignored).");
                return;
            }

            string recordedLine = null;
            foreach (string line in File.ReadAllLines(recordedReport))
            {
                if (line.Contains("configHash")) { recordedLine = line; break; }
            }

            Assert.NotNull(recordedLine);

            string expected = recordedLine
                .Replace("Unity 6000.5.6f1", "engine=dynamics 9.9.9.9")
                .Replace("· physics jobs 0", "· threads 24")

                // D102's token, which round 42's report was written before. Inserted in the slot
                // EvolutionRun prints it in — between the current and the rolls — so that the rest
                // of the line is still compared character for character against the recording.
                .Replace(" · rolls ", " · axes v:h 1.00 · rolls ")

                // The season's token, which the recording was written before: printed always,
                // off included, straight after the day's; and D151's weather token after it, the
                // same way.
                .Replace(" · current ", " · season ±0 over 30000 s · weather off · current ")

                // The reach bound's token (fable-propose-body-reach.md), which only this engine
                // prints, in its slot after the self-overlap fraction.
                .Replace(" · selfOverlap 0.1 · ", " · selfOverlap 0.1 · reach off · ")

                // D109's three tokens after the reach bound's (the islands, the founder rule and
                // the shade map, all off in the recorded world), and the matter grid's own stirring
                // before the area — a hard default of 2 m²/s that no launcher could name until the
                // islands needed it lower, so the recording never printed it.
                .Replace(" · reach off · ", " · reach off · floors per part · matter uniform · founders anywhere · endowment off · founder cap off · shade off · ")

                // D110's token, beside the silhouette cap it shares the shadow with; averaged in
                // every recorded world.
                .Replace(" · silhouette on · ", " · silhouette on · light averaged · buoyancy offset off · ")
                .Replace(" · area ", " · matter-mix 2 m2/s · area ")

                // D115's token, beside the floor it follows; off in every recorded world.
                .Replace(" · ceiling ", " · trickle off · ceiling ")

                // The round 48 senescence ruling's words: which sides the wear takes, D038's two
                // in every recorded world.
                .Replace(" · senescence 3000 s ", " · senescence 3000 s on upkeep and intake ")

                // The reefs' token (logbook/specs/reef-spec.md §1), closing the space token after
                // the bed and its shore; no reef in every recorded world.
                .Replace(" dispersal=", ", no reef dispersal=")

                // D106's, the same way: appended at the end of the line, before the hash, which is
                // where both engines print it — first the module gene's token and then the
                // mouth's. And the hash itself, which a new tunable moves whatever its default
                // (§9) — round 42 ran under ff557bce2685293a, the module gene filed the same world
                // under 11602ab76c1e2a19, the mouth's thirteen knobs and four caps under
                // 4cbb170c61668098, the reach bound under 5b93c47344df9e67, D109's five
                // island tunables under c862fd2c510b82e9, D110's light by exposure under
                // 5a456a9e7b2518d3, D111's buoyancy offset price and round 46's rules under
                // 5062a25baa35c6e1, D117's three pool tunables under 64f1aaa6b3e170f2 alone
                // and the reefs' six dials (logbook/specs/reef-spec.md) under 9036f75aa700b784
                // alone, and the two together, merged 2026-09-23 night, under 679f831c59c6f1af,
                // and the reef group redesigned under the owner's cover ruling (cover 0 here)
                // under 256078e816861b27, and the round 48 rulings' eight (the founders' depth
                // off, their endowment 0, senescence wearing intake, the rigid-group floors off,
                // the overhead's per-tissue factor at 0, the two gestation rates at 0 and the
                // share range 0.5 to 0.5) under 53f8234cb554f0ba, and D124's founder cap (0) under
                // this. The endowment's token, off, follows the founder rule's and the cap's follows it; the floors' token follows the reach; the reproduction token
                // sits before the hash.
                .Replace(
                    " · configHash ",
                    " · modules add=0 drop=0 after=0 mut=0" + MouthToken + SupportToken + ContactToken + ReproductionToken + Round51Token + " · configHash ")
                .Replace("`ff557bce2685293a`", "`dc1e08eaa7dc2b0d`");

            Assert.Equal(expected, Round42HeaderLine(threads: 24, engineVersion: "9.9.9.9"));
        }

        /// <summary>
        /// The tokens a reader verifies an arm by, without the recording — the test that still
        /// says something on a machine with no <c>runs/</c>.
        /// </summary>
        [Fact]
        public void TheSettingsLineNamesTheWorldItWasLaunchedWith()
        {
            string line = Round42HeaderLine(threads: 24, engineVersion: "9.9.9.9");

            Assert.StartsWith("engine=dynamics 9.9.9.9 · dt=0.01 · metabolic step 0.5 s · seed 1 ", line);
            Assert.Contains(" · threads 24 · driveLimit >0.01", line);

            // D102, beside the current and before the rolls, as EvolutionRun prints it: round
            // 42's tank balances, so its water needed no relaxation.
            Assert.Contains(" · axes v:h 1.00 · rolls unread in transport", line);
            Assert.Contains(
                " · space tank r=26.46 m (2200 m2), depth 45, wall, bed relief 1.5 m tilt 30 m " +
                "scale 17.64 m (hollows 0, ridges 1, range 1.50 m, steepest 37° bands 16°, " +
                "bound clear), no reef dispersal=5 m", line);
            Assert.Contains(" · senses jointangle,jointrate,up,depth,chemical,energy,flow", line);
            Assert.Contains(" · field grid h=1 mh=1.8 merge=0.25 cap=100000 q=0.125 cell=1 mcell=5", line);

            // D106's, last before the hash and all at their defaults: a reader verifying an arm
            // has to be able to see from the header alone that the module gene is off and that
            // nothing bites, eats, heals or is charged for an attribute.
            Assert.Contains(" · modules add=0 drop=0 after=0 mut=0" + MouthToken + SupportToken + ContactToken + ReproductionToken + Round51Token + " · configHash", line);

            // The reach bound, off: a reader has to see from the header that no body was cut.
            Assert.Contains(" · selfOverlap 0.1 · reach off · ", line);

            // D109, all off: the matter uniform, founders anywhere, no shade map, and the matter
            // grid stirring at the rate every recorded world stirred at.
            Assert.Contains(" · reach off · floors per part · matter uniform · founders anywhere · endowment off · founder cap off · shade off · ", line);
            Assert.Contains(" · matter-mix 2 m2/s · area 2200 m2 · ", line);

            Assert.EndsWith(" · configHash `dc1e08eaa7dc2b0d`", line);

            // D110, off: the light is the orientation average, printed beside the cap.
            Assert.Contains(" · silhouette on · light averaged · ", line);

            // D111, off: no price, so no torque and no genome floating off its centre.
            Assert.Contains(" · light averaged · buoyancy offset off · selfOverlap 0.1 · ", line);

            // D115, off: the floor closes and nothing follows it.
            Assert.Contains(" · floor closes 3000 s · trickle off · ceiling ", line);

            // D151, off: the smooth year prints as it did, and the weather says it is off.
            Assert.Contains(" · season ±0 over 30000 s · weather off · current ", line);

            // parse-arm.ps1 splits on ' · ' and asks for a token by prefix; nothing may arrive
            // with an empty name or a separator inside a value.
            foreach (string token in line.Split(new[] { " · " }, StringSplitOptions.None))
            {
                Assert.False(string.IsNullOrWhiteSpace(token));
            }

            // The Unity build's two tokens are gone, and neither may be quietly present.
            Assert.DoesNotContain("Unity ", line);
            Assert.DoesNotContain("physics jobs", line);
        }

        // ------------------------------------------------------------------ the table

        /// <summary>
        /// The table's shape: the base columns plus one per patch, and nothing reordered.
        /// </summary>
        [Fact]
        public void TheTableCarriesOneColumnPerPatchAfterTheBaseSet()
        {
            var config = new RunConfig { HorizontalPatches = 4 };
            var report = new Report(Path.Combine(AppContext.BaseDirectory, "unused.md"), config);

            Assert.Equal(Report.BaseColumns.Length + 4, report.Columns.Count);
            Assert.Equal("t (s)", report.Columns[0]);
            Assert.Equal("reach m", report.Columns[Report.BaseColumns.Length - 3]);
            Assert.Equal("**trickle**", report.Columns[Report.BaseColumns.Length - 2]);
            Assert.Equal("max reach", report.Columns[Report.BaseColumns.Length - 1]);
            Assert.Equal("p0", report.Columns[Report.BaseColumns.Length]);
            Assert.Equal("p3", report.Columns[Report.BaseColumns.Length + 3]);

            string header = report.TableHeader();
            Assert.StartsWith("| t (s) | alive | births | deaths | **jointed** |", header);

            // analyse-arm.ps1 finds the header row by this pattern and splits it on '|'.
            Assert.Matches(@"^\| *t \(s\)", header);
            Assert.Equal(report.Columns.Count, header.Split('\n')[0].Split('|').Length - 2);
        }

        /// <summary>
        /// D159: a migrating world's table carries the two immigrant columns between the base set
        /// and the patches, its settings line ends on the exchange, and a world that does not
        /// migrate has neither, so its report is what it was.
        /// </summary>
        [Fact]
        public void AMigratingWorldsReportCarriesTheExchangeAndNoOtherDoes()
        {
            var config = new RunConfig { HorizontalPatches = 2 };
            string path = Path.Combine(AppContext.BaseDirectory, "unused.md");
            var plain = new Report(path, config);
            var migrating = new Report(path, config, migrating: true);

            Assert.Equal(plain.Columns.Count + 2, migrating.Columns.Count);
            Assert.Equal("**imm**", migrating.Columns[Report.BaseColumns.Length]);
            Assert.Equal("imm refused", migrating.Columns[Report.BaseColumns.Length + 1]);
            Assert.Equal("p0", migrating.Columns[Report.BaseColumns.Length + 2]);
            Assert.DoesNotContain("**imm**", plain.Columns);
            Assert.Equal(plain.Columns, Sampler.Columns(config));
            Assert.Equal(migrating.Columns, Sampler.Columns(config, migrating: true));

            var env = EnvBindingTests.Round42Seed1();
            EnvSettings lone = EnvBinding.Read(EnvBinding.Of(env));
            env["EVOSIM_MIGRATE_EVERY"] = "600";
            env["EVOSIM_MIGRATE_COUNT"] = "3";
            EnvSettings group = EnvBinding.Read(EnvBinding.Of(env));
            RunConfig built = EnvBinding.BuildConfig(lone);
            Assert.Equal(built.Hash(), EnvBinding.BuildConfig(group).Hash());

            var world = new World(built, lone.Seed);
            SpaceFacts space = SpaceFacts.Of(world, hasWall: false, hasFloor: built.SharedSpace, threads: 1,
                inoculumHashShort: null);

            string loneLine = Report.HeaderLine(lone, built, space, "9.9.9.9");
            string groupLine = Report.HeaderLine(group, built, space, "9.9.9.9");

            Assert.EndsWith("`", loneLine);
            Assert.DoesNotContain("exchange", loneLine);
            Assert.Equal(loneLine + " · exchange every 600 s x 3", groupLine);
        }

        /// <summary>A row whose width is not the table's is refused rather than written.</summary>
        [Fact]
        public void ARowMustMatchItsHeader()
        {
            var config = new RunConfig { HorizontalPatches = 1 };
            var report = new Report(Path.Combine(AppContext.BaseDirectory, "unused.md"), config);

            Assert.Throws<ArgumentException>(() => report.AppendRow(new[] { "1", "2" }));
        }

        /// <summary>
        /// The four columns the overlap instrument renamed, and their places.
        /// </summary>
        /// <remarks>
        /// <b>A rename by ruling, not by drift</b> (fable-propose-own-solver.md's change 4, work
        /// package G). <c>contacts</c>, <c>pairs/body</c>, <c>pairs jnt %</c> and <c>stuck %</c>
        /// counted PhysX's contact manifolds between colliders; this engine counts overlapping
        /// bounding spheres between creatures, one per pair, and a body cannot overlap itself. The
        /// places are the same four so that nothing after them moves and a positional reader keeps
        /// working; the names are different so that a reader comparing the two engines' rows is
        /// stopped rather than handed a different measurement under the old name.
        /// </remarks>
        private static readonly (string Recorded, string Ported)[] RenamedColumns =
        {
            ("contacts", "overlaps"),
            ("pairs/body", "ovl/body"),
            ("pairs jnt %", "ovl jnt %"),
            ("stuck %", "ovl held %"),
        };

        /// <summary>
        /// D106's twelve, appended at the end of the base set and in this order — the module
        /// gene's five and then the mouth's seven — and the refusal split's two after them.
        /// </summary>
        /// <remarks>
        /// <b>Appended, never inserted.</b> Every reader of a run report that is not
        /// <c>analyse-arm.ps1</c> finds its columns by counting, and logbook/0044's misread — float
        /// tissue reported as the food chain — is what a shifted column costs. So a new instrument
        /// goes on the end, where a reader that has never heard of it stops before reaching it.
        /// </remarks>
        private static readonly string[] AppendedColumns =
        {
            "modules", "mod add", "mod drop", "mod refused", "indet %",
            "attack %", "intake %", "prot %", "killed", "eaten", "corpse eat", "heal J",
            "ref shape", "ref reserve",

            // D110's readout, a dash in every recorded world.
            "expo",

            // D111's, the same.
            "float off",

            // D113's two: the support watts, a dash in every recorded world, and the reach.
            "support W", "reach m",

            // D115's window of trickle founders, 0 in every recorded world.
            "**trickle**",

            // Round 46's K6b: the farthest living part from its root.
            "max reach",
        };

        /// <summary>
        /// D106 items 1, 3 and 4's header token at every default — the recorded world's values,
        /// which is what makes it the string a ported line is compared against.
        /// </summary>
        private const string MouthToken =
            " · mouth hp=1 heal=0/s@1J reach=0 waste=0 prices atk=0 ink=0 prt=0 tgh=0 mut=0";

        /// <summary>D113's header token at the recorded world's price, after the mouth's.</summary>
        private const string SupportToken = " · support off";
        // D114: after the support price and before the hash, the model the census counted under.
        private const string ContactToken = " · contact per body";

        /// <summary>
        /// The ruling of 2026-09-24's token, after the contact token and before the hash: the
        /// overhead flat at its floor (<c>x0</c>) and no lineage able to gestate.
        /// </summary>
        private const string ReproductionToken = " · overhead scale x0 floor 100 J · gestation off";

        /// <summary>
        /// Round 51's three tokens (D127, D128), after the reproduction token and before the hash,
        /// at the values that change nothing: every part priced, a corpse sinking at the snow's
        /// rate and drifting with the old floor, and no death by age.
        /// </summary>
        private const string Round51Token = " · free link off · corpse sink snow drifts floor 1e-6 J settled decay as sinking · age off";

        /// <summary>
        /// The recorded table's header row, column for column but for the four that were renamed
        /// and the five D106 appended.
        /// </summary>
        [Fact]
        public void TheTableHeaderIsTheRecordedOneBarTheFourRenamedColumns()
        {
            string recordedReport = RecordedRound42Report();
            if (recordedReport == null)
            {
                Console.WriteLine("runs/r42-s1.md is not on this machine (run reports are gitignored).");
                return;
            }

            string recorded = null;
            foreach (string line in File.ReadAllLines(recordedReport))
            {
                if (line.StartsWith("| t (s) |", StringComparison.Ordinal)) { recorded = line; break; }
            }

            Assert.NotNull(recorded);

            RunConfig config = EnvBinding.BuildConfig(
                EnvBinding.Read(EnvBinding.Of(EnvBindingTests.Round42Seed1())));

            var report = new Report(Path.Combine(AppContext.BaseDirectory, "unused.md"), config);

            // The two lines are separated by Environment.NewLine, as the Unity build separates
            // them; the recorded file's own line endings are the platform's too, and
            // File.ReadAllLines has already taken them off.
            string ported = report.TableHeader().Split('\n')[0].TrimEnd('\r');

            string[] recordedCells = recorded.Split('|');
            string[] all = ported.Split('|');

            // D106's twelve, at the end of the base set — which is not the end of the row, because
            // the per-patch columns come after it. So they are found in their own slots, taken
            // out, and the rest of the row is compared against the record cell for cell, which is
            // what says nothing else moved.
            int at = Report.BaseColumns.Length - AppendedColumns.Length + 1;

            for (int k = 0; k < AppendedColumns.Length; k++)
            {
                Assert.Equal(AppendedColumns[k], all[at + k].Trim());
            }

            var kept = new List<string>(all.Length - AppendedColumns.Length);
            for (int i = 0; i < all.Length; i++)
            {
                if (i >= at && i < at + AppendedColumns.Length) continue;
                kept.Add(all[i]);
            }

            string[] portedCells = kept.ToArray();

            Assert.Equal(recordedCells.Length, portedCells.Length);

            for (int i = 0; i < recordedCells.Length; i++)
            {
                string was = recordedCells[i].Trim();
                string now = portedCells[i].Trim();

                if (was == now) continue;

                // The only differences allowed are the four the instrument renamed, each in the
                // place its predecessor stood.
                bool renamed = false;
                for (int r = 0; r < RenamedColumns.Length; r++)
                {
                    if (was != RenamedColumns[r].Recorded) continue;

                    Assert.Equal(RenamedColumns[r].Ported, now);
                    renamed = true;
                    break;
                }

                Assert.True(
                    renamed,
                    "column " + i + " reads '" + now + "' where the record reads '" + was +
                    "'. Only the four columns of the overlap instrument may differ, and each " +
                    "only in the place its predecessor stood.");
            }
        }

        // ------------------------------------------------------------------ the footer

        /// <summary>Round 42's footer, rebuilt from its own numbers.</summary>
        [Fact]
        public void TheFooterIsByteCompatibleWithTheRecordedOne()
        {
            RunConfig config = EnvBinding.BuildConfig(
                EnvBinding.Read(EnvBinding.Of(EnvBindingTests.Round42Seed1())));

            var report = new Report(Path.Combine(AppContext.BaseDirectory, "unused.md"), config);
            report.Footer(
                config, Round42Readings(), "budget reached",
                wallClockMs: 49551306, writersMs: 3208,
                bestSpeed: 1.242867112159729, bestSpeedAtSeconds: 14.5);

            string footer = report.Text.Replace("\r\n", "\n").Trim('\n');
            Assert.Equal(Round42Footer.Replace("\r\n", "\n"), footer);
        }

        /// <summary>And the same footer, against the bytes the Unity build actually wrote.</summary>
        /// <remarks>
        /// <b>Compared line by line, so that the one ruled departure is named rather than
        /// glossed.</b> The space line reports three different measurements here and is renamed
        /// for them (see <see cref="Round42Footer"/>); every other line has to be the recorded
        /// bytes, which is what a line-by-line comparison says and a whole-string one would not.
        /// </remarks>
        [Fact]
        public void TheFooterIsByteCompatibleWithTheFileOnDisk()
        {
            string recordedReport = RecordedRound42Report();
            if (recordedReport == null)
            {
                Console.WriteLine("runs/r42-s1.md is not on this machine (run reports are gitignored).");
                return;
            }

            string text = File.ReadAllText(recordedReport).Replace("\r\n", "\n");
            int start = text.IndexOf("**Ended:**", StringComparison.Ordinal);
            int end = text.IndexOf("**Fastest creature seen at any point:", StringComparison.Ordinal);
            Assert.True(start > 0 && end > start);

            end = text.IndexOf('\n', end);
            string recorded = text.Substring(start, end - start);

            string[] was = recorded.Split('\n');
            string[] now = Round42Footer.Replace("\r\n", "\n").Split('\n');

            Assert.Equal(was.Length, now.Length);

            for (int i = 0; i < was.Length; i++)
            {
                if (was[i].StartsWith("Shared space:", StringComparison.Ordinal))
                {
                    // The one ruled departure, and it may not spread: the line still has to carry
                    // the same counts in the same places, under names that say what they count.
                    Assert.StartsWith("Shared space: on · wraps 0 · crowded stillbirths 189 · ", now[i]);
                    Assert.Contains("463.6537", now[i]);
                    Assert.Contains("-69.16 m", now[i]);
                    Assert.Contains("4.3598", now[i]);
                    Assert.DoesNotContain("contact pairs", now[i]);
                    Assert.DoesNotContain("mesh collider", now[i]);
                    Assert.DoesNotContain("floor pairs", now[i]);
                    continue;
                }

                Assert.Equal(was[i], now[i]);
            }
        }

        /// <summary>The pace line, as the two scripts read it.</summary>
        [Fact]
        public void ThePaceLineMatchesTheRegularExpressionsThatReadIt()
        {
            RunConfig config = EnvBinding.BuildConfig(
                EnvBinding.Read(EnvBinding.Of(EnvBindingTests.Round42Seed1())));

            var report = new Report(Path.Combine(AppContext.BaseDirectory, "unused.md"), config);
            report.Footer(
                config, Round42Readings(), "budget reached",
                wallClockMs: 49551306, writersMs: 3208,
                bestSpeed: 1.242867112159729, bestSpeedAtSeconds: 14.5);

            string text = report.Text;

            Match pace = Regex.Match(text, PaceSurveyPattern);
            Assert.True(pace.Success, "pace-survey.py would not find the pace line");
            Assert.Equal("3000000", pace.Groups[1].Value);
            Assert.Equal("30000", pace.Groups[2].Value);
            Assert.Equal("8823", pace.Groups[3].Value);
            Assert.Equal("825.9", pace.Groups[4].Value);
            Assert.Equal("0.6", pace.Groups[5].Value);

            Match parse = Regex.Match(text, ParseArmPattern);
            Assert.True(parse.Success, "parse-arm.ps1 would not find the pace line");
            Assert.Equal("825.9", parse.Groups["wall"].Value);

            // watch-round.py's ending test, and parse-arm.ps1's.
            Assert.Matches(@"\*\*Ended:\*\*\s*(?<reason>[^\r\n]+?)\.?\s*(\r?\n|$)", text);
            Assert.Matches(
                @"\*\*Fastest creature seen at any point:\s*(?<speed>[\d.]+)\s*m/s,\s*at t=(?<t>[\d.]+)\s*s\.\*\*",
                text);
        }

        /// <summary>
        /// A world with no bed and no crowd prints dashes rather than zeros.
        /// </summary>
        /// <remarks>
        /// 0 pairs and no floor are different facts and a number alone cannot say which — the
        /// distinction CLAUDE.md's species column exists to warn about.
        /// </remarks>
        [Fact]
        public void AnInstrumentThatIsOffPrintsADashAndNotAZero()
        {
            var config = new RunConfig { SharedSpace = false, HorizontalPatches = 1 };
            var report = new Report(Path.Combine(AppContext.BaseDirectory, "unused.md"), config);
            var readings = new FakeReadings
            {
                PhysicsSteps = 1000,
                ElapsedSeconds = 10,
                HasSharedVolume = false,
                HasFloor = false,
                HarnessPhases = new[] { "read", "other" },
                HarnessPhaseMs = new long[] { 1, 1 },
            };

            report.Footer(config, readings, "extinct at t=10 s, and the floor could not refill it",
                wallClockMs: 1000, writersMs: 0, bestSpeed: 0, bestSpeedAtSeconds: 0);

            string text = report.Text;
            Assert.Contains("Shared space: off · wraps 0 · crowded stillbirths 0 · overlap pairs per physics step —", text);
            Assert.Contains("· sea bed none · bed or glass bodies per physics step —", text);
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// The round 48 rulings' three tokens with the rulings on, on round 42's launch otherwise:
        /// the founders at their food's depth (D116 on beneath it), the endowment in seconds, and
        /// senescence on upkeep alone; and D124's founder cap, which round 49 adds to them.
        /// </summary>
        [Fact]
        public void TheRound48RulingsAreNamedInTheHeader()
        {
            Dictionary<string, string> env = EnvBindingTests.Round42Seed1();
            env["EVOSIM_FOUNDERS_FOLLOW_FOOD"] = "1";
            env["EVOSIM_FOUNDERS_FOLLOW_FOOD_DEPTH"] = "1";
            env["EVOSIM_FOUNDER_ENDOWMENT"] = "600";
            env["EVOSIM_SENESCENCE_WEARS_INTAKE"] = "0";
            env["EVOSIM_FOUNDER_RESERVE_CAP"] = "0.9";

            EnvSettings settings = EnvBinding.Read(EnvBinding.Of(env));
            RunConfig config = EnvBinding.BuildConfig(settings);

            Assert.True(config.FoundersFollowFoodDepth);
            Assert.Equal(600f, config.FounderEndowmentSeconds);
            Assert.False(config.SenescenceWearsIntake);
            Assert.Equal(0.9f, config.FounderReserveCapFraction);

            var world = new World(config, settings.Seed);
            SpaceFacts space = SpaceFacts.Of(
                world,
                hasWall: config.SharedSpace && config.WorldShape == WorldShape.Tank,
                hasFloor: config.SharedSpace,
                threads: 1,
                inoculumHashShort: null);

            string line = Report.HeaderLine(settings, config, space, "9.9.9.9");

            Assert.Contains(" · founders in their food at its depth · endowment 600 s · founder cap 0.9 of the gate · shade off · ", line);
            Assert.Contains(" · senescence 3000 s on upkeep · ", line);

            // Unset, the switch reads on: D038's two sides, the recorded world.
            EnvSettings unset = EnvBinding.Read(EnvBinding.Of(EnvBindingTests.Round42Seed1()));
            Assert.True(EnvBinding.BuildConfig(unset).SenescenceWearsIntake);
        }

        /// <summary>
        /// Round 51's rulings (D127, D128) switched on through the environment reach the config
        /// and are named in the header, and the hazard refuses D038's wear beside it.
        /// </summary>
        [Fact]
        public void TheRound51RulingsAreNamedInTheHeader()
        {
            Dictionary<string, string> env = EnvBindingTests.Round42Seed1();
            env["EVOSIM_SENESCENCE"] = "0";
            env["EVOSIM_FREE_LINK"] = "0.01";
            env["EVOSIM_CORPSE_DECAY"] = "0.000385";
            env["EVOSIM_CORPSE_SINK"] = "0.05";
            env["EVOSIM_CORPSE_SETTLE"] = "1";
            env["EVOSIM_CORPSE_FLOOR"] = "0.05";
            env["EVOSIM_AGE_BASE"] = "3000";
            env["EVOSIM_AGE_DOUBLING"] = "0.2";
            env["EVOSIM_AGE_BIRTH"] = "1";
            env["EVOSIM_AGE_WOUND"] = "1";

            EnvSettings settings = EnvBinding.Read(EnvBinding.Of(env));
            RunConfig config = EnvBinding.BuildConfig(settings);

            Assert.Equal(0.01f, config.FreeFirstLinkCubicMetres);
            Assert.Equal(0.05f, config.CorpseSinkMetresPerSecond);
            Assert.True(config.CorpseSettles);
            Assert.Equal(0.05f, config.CorpseLeavesBelowFraction);
            Assert.Equal(3000f, config.AgeHazardBaseSeconds);
            Assert.Equal(0.2f, config.AgeHazardDoublingFraction);
            Assert.Equal(1f, config.AgeingPerBirth);
            Assert.Equal(1f, config.AgeingPerWound);
            Assert.Equal(0f, config.AgeRepairExponent);
            Assert.Equal(1f, config.Genome.MinLifespan);
            Assert.Equal(1f, config.Genome.MaxLifespan);
            Assert.Equal(0f, config.Mutation.LifespanChance);

            var world = new World(config, settings.Seed);
            SpaceFacts space = SpaceFacts.Of(
                world,
                hasWall: config.SharedSpace && config.WorldShape == WorldShape.Tank,
                hasFloor: config.SharedSpace,
                threads: 1,
                inoculumHashShort: null);

            string line = Report.HeaderLine(settings, config, space, "9.9.9.9");

            Assert.Contains(
                " · free link 0.01 m3 · corpse sink 0.05 m/s settles floor 0.05 settled decay as sinking · age base 3000 s " +
                "doubling x0.2 birth 1 wound 1 repair ^0 lifespan 1-1 at 0 · configHash ", line);

            // D128 turns D038's wear off while the hazard is on, so a launcher naming both is refused.
            env["EVOSIM_SENESCENCE"] = "3000";
            RunConfig both = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(env)));
            Assert.Throws<ArgumentException>(() => new World(both, settings.Seed));
        }

        /// <summary>
        /// D212: the spoilage dial reaches the config from <c>EVOSIM_CORPSE_HALF_LIFE</c> and the header names it
        /// only when it is above 0, so every recorded header reads as it did.
        /// </summary>
        [Fact]
        public void TheSpoilHalfLifeIsBoundAndShownOnlyWhenOn()
        {
            string off = Round42HeaderLine(threads: 24, engineVersion: "9.9.9.9");
            Assert.DoesNotContain("spoil half-life", off);

            var block = EnvBindingTests.Round42Seed1();
            block["EVOSIM_CORPSE_HALF_LIFE"] = "500";
            EnvSettings settings = EnvBinding.Read(EnvBinding.Of(block));
            RunConfig config = EnvBinding.BuildConfig(settings);
            Assert.Equal(500f, config.CorpseHalfLifeSeconds);

            var world = new World(config, settings.Seed);
            SpaceFacts space = SpaceFacts.Of(
                world,
                hasWall: config.SharedSpace && config.WorldShape == WorldShape.Tank,
                hasFloor: config.SharedSpace,
                threads: 24,
                inoculumHashShort: null);
            string on = Report.HeaderLine(settings, config, space, "9.9.9.9");
            Assert.Contains(" · spoil half-life 500 s · ", on);
            Assert.NotEqual(EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(EnvBindingTests.Round42Seed1()))).Hash(), config.Hash());

            block["EVOSIM_CORPSE_HALF_LIFE"] = "-1";
            Assert.ThrowsAny<Exception>(() => EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(block))));
        }

        /// <summary>
        /// D219, D220: the contact-eating dials reach the config from <c>EVOSIM_CONTACT_EATING</c> and
        /// <c>EVOSIM_REMAINS_WINDOW</c>, and the header names them only when the first is on.
        /// </summary>
        [Fact]
        public void TheContactEatingDialsAreBoundAndShownOnlyWhenOn()
        {
            string off = Round42HeaderLine(threads: 24, engineVersion: "9.9.9.9");
            Assert.DoesNotContain("remains window", off);

            RunConfig recorded = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(EnvBindingTests.Round42Seed1())));
            Assert.False(recorded.ContactEating);
            Assert.Equal(60f, recorded.RemainsWindowSeconds);

            var block = EnvBindingTests.Round42Seed1();
            block["EVOSIM_CONTACT_EATING"] = "1";
            block["EVOSIM_REMAINS_WINDOW"] = "90";
            EnvSettings settings = EnvBinding.Read(EnvBinding.Of(block));
            RunConfig config = EnvBinding.BuildConfig(settings);
            Assert.True(config.ContactEating);
            Assert.Equal(90f, config.RemainsWindowSeconds);
            Assert.NotEqual(recorded.Hash(), config.Hash());

            var world = new World(config, settings.Seed);
            SpaceFacts space = SpaceFacts.Of(
                world,
                hasWall: config.SharedSpace && config.WorldShape == WorldShape.Tank,
                hasFloor: config.SharedSpace,
                threads: 24,
                inoculumHashShort: null);
            string on = Report.HeaderLine(settings, config, space, "9.9.9.9");
            Assert.Contains(" · bites feed, remains window 90 s · ", on);

            block["EVOSIM_REMAINS_WINDOW"] = "-1";
            Assert.ThrowsAny<Exception>(() => EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(block))));
        }

        /// <summary>
        /// D224: <c>EVOSIM_BITE_TAKES_STORE</c> reaches the config, is off when unset, is shown in the header only when
        /// on, and the world refuses it on with contact eating off.
        /// </summary>
        [Fact]
        public void TheBiteTakesStoreDialIsBoundShownOnlyWhenOnAndRefusedWithContactEatingOff()
        {
            var block = EnvBindingTests.Round42Seed1();
            block["EVOSIM_CONTACT_EATING"] = "1";

            EnvSettings offSettings = EnvBinding.Read(EnvBinding.Of(block));
            RunConfig off = EnvBinding.BuildConfig(offSettings);
            Assert.False(off.BiteTakesStore);

            block["EVOSIM_BITE_TAKES_STORE"] = "1";
            EnvSettings settings = EnvBinding.Read(EnvBinding.Of(block));
            RunConfig config = EnvBinding.BuildConfig(settings);
            Assert.True(config.BiteTakesStore);
            Assert.NotEqual(off.Hash(), config.Hash());

            string Header(EnvSettings s, RunConfig c)
            {
                var world = new World(c, s.Seed);
                SpaceFacts space = SpaceFacts.Of(
                    world,
                    hasWall: c.SharedSpace && c.WorldShape == WorldShape.Tank,
                    hasFloor: c.SharedSpace,
                    threads: 24,
                    inoculumHashShort: null);
                return Report.HeaderLine(s, c, space, "9.9.9.9");
            }

            Assert.DoesNotContain("a bite takes the store", Header(offSettings, off));
            Assert.Contains(" · a bite takes the store", Header(settings, config));

            var refused = EnvBindingTests.Round42Seed1();
            refused["EVOSIM_BITE_TAKES_STORE"] = "1";
            RunConfig withoutContact = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(refused)));
            Assert.True(withoutContact.BiteTakesStore);
            Assert.False(withoutContact.ContactEating);
            Assert.Throws<ArgumentException>(() => new World(withoutContact, 1UL));
        }

        /// <summary>
        /// D226: <c>EVOSIM_BITE_DAMAGE_SCALE</c> reaches the config, is 1 when unset, is shown in the header only when
        /// not 1, and the world refuses it away from 1 with contact eating off.
        /// </summary>
        [Fact]
        public void TheBiteDamageScaleDialIsBoundShownOnlyWhenNotOneAndRefusedWithContactEatingOff()
        {
            var block = EnvBindingTests.Round42Seed1();
            block["EVOSIM_CONTACT_EATING"] = "1";

            EnvSettings offSettings = EnvBinding.Read(EnvBinding.Of(block));
            RunConfig off = EnvBinding.BuildConfig(offSettings);
            Assert.Equal(1f, off.BiteDamageScale);

            block["EVOSIM_BITE_DAMAGE_SCALE"] = "4";
            EnvSettings settings = EnvBinding.Read(EnvBinding.Of(block));
            RunConfig config = EnvBinding.BuildConfig(settings);
            Assert.Equal(4f, config.BiteDamageScale);
            Assert.NotEqual(off.Hash(), config.Hash());

            string Header(EnvSettings s, RunConfig c)
            {
                var world = new World(c, s.Seed);
                SpaceFacts space = SpaceFacts.Of(
                    world,
                    hasWall: c.SharedSpace && c.WorldShape == WorldShape.Tank,
                    hasFloor: c.SharedSpace,
                    threads: 24,
                    inoculumHashShort: null);
                return Report.HeaderLine(s, c, space, "9.9.9.9");
            }

            Assert.DoesNotContain("bite damage", Header(offSettings, off));
            Assert.Contains(" · bite damage ×4", Header(settings, config));

            var refused = EnvBindingTests.Round42Seed1();
            refused["EVOSIM_BITE_DAMAGE_SCALE"] = "4";
            RunConfig withoutContact = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(refused)));
            Assert.Equal(4f, withoutContact.BiteDamageScale);
            Assert.False(withoutContact.ContactEating);
            Assert.Throws<ArgumentException>(() => new World(withoutContact, 1UL));

            var zero = EnvBindingTests.Round42Seed1();
            zero["EVOSIM_CONTACT_EATING"] = "1";
            zero["EVOSIM_BITE_DAMAGE_SCALE"] = "0";
            Assert.ThrowsAny<Exception>(() => new World(EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(zero))), 1UL));
        }

        private static string Round42HeaderLine(int threads, string engineVersion)
        {
            EnvSettings settings = EnvBinding.Read(EnvBinding.Of(EnvBindingTests.Round42Seed1()));
            RunConfig config = EnvBinding.BuildConfig(settings);

            var world = new World(config, settings.Seed);
            SpaceFacts space = SpaceFacts.Of(
                world,
                hasWall: config.SharedSpace && config.WorldShape == WorldShape.Tank,
                hasFloor: config.SharedSpace,
                threads: threads,
                inoculumHashShort: null);

            return Report.HeaderLine(settings, config, space, engineVersion);
        }

        /// <summary>Round 42 seed 1's own readings, from its <c>run.json</c>.</summary>
        private static IHarnessReadings Round42Readings() => new FakeReadings
        {
            PhysicsSteps = 3000000,
            ElapsedSeconds = 30000,
            Births = 8823,
            Alive = 934,
            DragImpulsesLimited = 0,
            DriveImpulsesLimited = 0,
            HasSharedVolume = true,
            Wraps = 0,
            Crowded = 189,

            // overlapPairsPerStep 463.653692 over 3,000,000 steps.
            ContactPairs = 1390961076,
            HasFloor = true,
            FloorHasRelief = true,
            FloorLowestTopY = -69.16,

            // 4.3598 pairs a step.
            FloorContactPairs = 13079400,
            WallPhysicsMs = 18222500,
            WallWorldMs = 4684679,
            WallHarnessMs = 26638293,
            HarnessPhases = new[]
            {
                "reconcile", "finite", "read", "control", "fluid", "contacts",
                "settle", "trace", "metabolise", "growth", "other",
            },
            HarnessPhaseMs = new long[]
            {
                6272, 15949, 7320536, 5961472, 8557059, 119368,
                912158, 3701212, 41906, 960, 1401,
            },
            HarnessBodySteps = 2309857800,
            HarnessMicrosecondsPerBodyStep = 26638293 * 1000d / 2309857800d,
            WallFluidGatherMs = 1075764,
            WallFluidWaterMs = 1242089,
            WallFluidComputeMs = 3622617,
            WallFluidApplyMs = 2615636,
            FluidLinkSteps = 5960466800,
            FluidMicrosecondsPerLinkStep = 8556106 * 1000d / 5960466800d,
        };

        /// <summary>
        /// What work package G will provide for real. Fields, so a test states only what it means
        /// to state and every other reading is the zero it would be in a world without one.
        /// </summary>
        private sealed class FakeReadings : IHarnessReadings
        {
            public long PhysicsSteps { get; set; }
            public double ElapsedSeconds { get; set; }
            public long Births { get; set; }
            public int Alive { get; set; }
            public long Diverged { get; set; }
            public double MaxSpeed { get; set; }
            public long DragImpulsesLimited { get; set; }
            public long DriveImpulsesLimited { get; set; }
            public double MatterInfluxedTotal { get; set; }
            public double MatterBuriedTotal { get; set; }
            public bool HasSharedVolume { get; set; }
            public long Wraps { get; set; }
            public long Crowded { get; set; }
            public long ContactPairs { get; set; }
            public long ContactPairsJointed { get; set; }
            public long ContactPairsPersistent { get; set; }
            public long ContactBodies { get; set; }
            public long FloorContactPairs { get; set; }
            public bool HasFloor { get; set; }
            public bool FloorHasRelief { get; set; }
            public double FloorLowestTopY { get; set; }
            public double MaxJointMassRatio { get; set; }
            public long BodiesOverMassRatio10 { get; set; }
            public long WallPhysicsMs { get; set; }
            public long WallWorldMs { get; set; }
            public long WallHarnessMs { get; set; }
            public IReadOnlyList<string> HarnessPhases { get; set; } = new string[0];
            public IReadOnlyList<long> HarnessPhaseMs { get; set; } = new long[0];
            public long HarnessBodySteps { get; set; }
            public double HarnessMicrosecondsPerBodyStep { get; set; }
            public long WallExposureMs { get; set; }
            public long WallLedgerMs { get; set; }
            public long WallFluidGatherMs { get; set; }
            public long WallFluidWaterMs { get; set; }
            public long WallFluidComputeMs { get; set; }
            public long WallFluidApplyMs { get; set; }
            public long FluidLinkSteps { get; set; }
            public double FluidMicrosecondsPerLinkStep { get; set; }
        }

        /// <summary>The recorded round 42 seed 1 report, or null where it is not on this machine.</summary>
        private static string RecordedRound42Report()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, "runs", "r42-s1.md");
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }

            return null;
        }
    }
}
