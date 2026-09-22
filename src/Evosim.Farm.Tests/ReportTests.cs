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

                // D106's, the same way: appended at the end of the line, before the hash, which is
                // where both engines print it — first the module gene's token and then the
                // mouth's. And the hash itself, which a new tunable moves whatever its default
                // (§9) — round 42 ran under ff557bce2685293a, the module gene filed the same world
                // under 11602ab76c1e2a19, and the mouth's thirteen knobs and four caps file it
                // under this.
                .Replace(
                    " · configHash ",
                    " · modules add=0 drop=0 after=0 mut=0" + MouthToken + " · configHash ")
                .Replace("`ff557bce2685293a`", "`fa6cdceda4ab17b6`");

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
                "bound clear) dispersal=5 m", line);
            Assert.Contains(" · senses jointangle,jointrate,up,depth,chemical,energy,flow", line);
            Assert.Contains(" · field grid h=1 mh=1.8 merge=0.25 cap=100000 q=0.125 cell=1 mcell=5", line);

            // D106's, last before the hash and all at their defaults: a reader verifying an arm
            // has to be able to see from the header alone that the module gene is off and that
            // nothing bites, eats, heals or is charged for an attribute.
            Assert.Contains(" · modules add=0 drop=0 after=0 mut=0" + MouthToken + " · configHash", line);

            Assert.EndsWith(" · configHash `fa6cdceda4ab17b6`", line);

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
            Assert.Equal("heal J", report.Columns[Report.BaseColumns.Length - 1]);
            Assert.Equal("p0", report.Columns[Report.BaseColumns.Length]);
            Assert.Equal("p3", report.Columns[Report.BaseColumns.Length + 3]);

            string header = report.TableHeader();
            Assert.StartsWith("| t (s) | alive | births | deaths | **jointed** |", header);

            // analyse-arm.ps1 finds the header row by this pattern and splits it on '|'.
            Assert.Matches(@"^\| *t \(s\)", header);
            Assert.Equal(report.Columns.Count, header.Split('\n')[0].Split('|').Length - 2);
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
        /// gene's five and then the mouth's seven.
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
        };

        /// <summary>
        /// D106 items 1, 3 and 4's header token at every default — the recorded world's values,
        /// which is what makes it the string a ported line is compared against.
        /// </summary>
        private const string MouthToken =
            " · mouth hp=1 heal=0/s@1J reach=0 waste=0 prices atk=0 ink=0 prt=0 tgh=0 mut=0";

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
