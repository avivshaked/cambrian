using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Evosim.Core;
using Evosim.Dynamics;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// D268 in the farm: a checkpoint of a world whose anchoring cell lets the brain hold the grip carries the pull record
    /// and the let-go count (<c>GRPL</c>) and restores to the same world, the fidelity check passes with it, a switch-off
    /// checkpoint carries no such block, and the report, the stats row and <c>positions.jsonl</c> say what is held.
    /// </summary>
    [Collection(FarmProcessCollection.Name)]
    public class AnchorBrainFarmTests
    {
        private readonly ITestOutputHelper _out;

        public AnchorBrainFarmTests(ITestOutputHelper output) => _out = output;

        private const int StepsPerMetabolic = 25;

        private sealed class Pair : IDisposable
        {
            public World World;
            public Simulation Sim;

            public void Dispose() => Sim?.Dispose();
        }

        private static Pair Found(bool brain)
        {
            var world = new World(GripCheckpointTests.GrippingWorld(brain), 1UL);

            return new Pair
            {
                World = world,
                Sim = new Simulation(world, 1UL, 0.02f, StepsPerMetabolic, threads: 2, runDirectory: null),
            };
        }

        private static byte[] Checkpoint(Pair pair)
        {
            pair.Sim.SettleBeforeCheckpoint();

            using (var buffer = new MemoryStream())
            {
                using (var w = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
                {
                    pair.World.WriteState(w);
                    pair.Sim.WriteState(w);
                    w.Flush();
                }

                return buffer.ToArray();
            }
        }

        private static Pair Restore(byte[] state, bool brain)
        {
            Pair pair = Found(brain);

            using (var buffer = new MemoryStream(state, writable: false))
            using (var r = new BinaryReader(buffer, Encoding.UTF8))
            {
                pair.World.ReadState(r);
                pair.Sim.ReadState(r);

                Assert.Equal(state.Length, buffer.Position);
            }

            return pair;
        }

        private static int GripsHeld(Simulation sim)
        {
            int held = 0;
            foreach (Creature body in sim.Dynamics.Creatures) held += body.GripsHeld;
            return held;
        }

        /// <summary>Steps a metabolic step at a time until at least three grips are held and one has been paid for.</summary>
        private static void StepUntilGripping(Pair live, out int metabolicSteps)
        {
            for (metabolicSteps = 1; metabolicSteps <= 120; metabolicSteps++)
            {
                for (int i = 0; i < StepsPerMetabolic; i++) live.Sim.Step();
                if (GripsHeld(live.Sim) >= 3 && live.World.GripJoulesTotal > 0) return;
            }

            throw new Xunit.Sdk.XunitException(
                "No body gripped within 120 metabolic steps: " + live.World.Living.Count + " living, " +
                GripsHeld(live.Sim) + " grips held, " + live.World.GripJoulesTotal.ToString("R") + " J paid.");
        }

        private static bool Contains(byte[] haystack, string tag)
        {
            byte[] needle = Encoding.ASCII.GetBytes(tag);
            for (int i = 0; i + needle.Length <= haystack.Length; i++)
            {
                int k = 0;
                while (k < needle.Length && haystack[i + k] == needle[k]) k++;
                if (k == needle.Length) return true;
            }

            return false;
        }

        // ------------------------------------------------------------------ the checkpoint

        [Fact]
        public void ASwitchOnCheckpointCarriesTheGrplBlockAndASwitchOffOneDoesNot()
        {
            byte[] on;
            using (Pair live = Found(brain: true))
            {
                StepUntilGripping(live, out _);
                on = Checkpoint(live);
            }

            byte[] off;
            using (Pair live = Found(brain: false))
            {
                StepUntilGripping(live, out _);
                off = Checkpoint(live);
            }

            Assert.True(Contains(on, "GRPL"), "a switch-on checkpoint holds no GRPL block");
            Assert.True(Contains(on, "GRIP"));
            Assert.False(Contains(off, "GRPL"), "a switch-off checkpoint holds a GRPL block");
            Assert.True(Contains(off, "GRIP"));
        }

        [Fact]
        public void AResumeAtTheMidpointEqualsTheStraightRunGrplIncluded()
        {
            byte[] state;
            using (Pair live = Found(brain: true))
            {
                StepUntilGripping(live, out _);

                // Values the stepping would not reach soon, so a reader that dropped the block shows.
                foreach (Creature body in live.Sim.Dynamics.Creatures)
                {
                    if (body.GripPull == null) continue;
                    for (int i = 0; i < body.Links; i++) if (body.Anchors[i]) body.GripPull[i] = 0.37 + 0.01 * i;
                    body.GripsLetGo = 7;
                }

                state = Checkpoint(live);
            }

            using (Pair first = Restore(state, brain: true))
            using (Pair second = Restore(state, brain: true))
            {
                Assert.Equal(state, Checkpoint(first));

                int restored = 0;
                foreach (Creature body in first.Sim.Dynamics.Creatures)
                {
                    if (body.GripPull == null) continue;
                    restored++;
                    Assert.Equal(7L, body.GripsLetGo);
                    for (int i = 0; i < body.Links; i++) if (body.Anchors[i]) Assert.Equal(0.37 + 0.01 * i, body.GripPull[i]);
                }

                Assert.True(restored > 0, "no body carried a pull record, so nothing was tested");

                // And two restores step the same bodies, over four metabolic steps, and end in the same state.
                for (int i = 0; i < 4 * StepsPerMetabolic; i++)
                {
                    first.Sim.Step();
                    second.Sim.Step();
                    Assert.Equal(first.Sim.Dynamics.Digest(), second.Sim.Dynamics.Digest());
                }

                Assert.Equal(Checkpoint(first), Checkpoint(second));
            }
        }

        [Fact]
        public void AResumeFromAMidpointMatchesTheStraightRunStepForStep()
        {
            byte[] state;
            using (Pair straight = Found(brain: true))
            {
                StepUntilGripping(straight, out _);
                state = Checkpoint(straight);

                // The straight run goes on from the very world the checkpoint was taken of.
                using (Pair resumed = Restore(state, brain: true))
                {
                    for (int i = 0; i < 3 * StepsPerMetabolic; i++)
                    {
                        straight.Sim.Step();
                        resumed.Sim.Step();
                        Assert.Equal(straight.Sim.Dynamics.Digest(), resumed.Sim.Dynamics.Digest());
                    }

                    Assert.Equal(Checkpoint(straight), Checkpoint(resumed));
                }
            }
        }

        [Fact]
        public void TheFidelityCheckPassesWithTheBrainHoldingTheGrip()
        {
            int metabolicSteps;
            using (Pair live = Found(brain: true)) StepUntilGripping(live, out metabolicSteps);
            double seconds = metabolicSteps * StepsPerMetabolic * 0.02;

            string root = SimulationTests.Scratch("grip-brain-fidelity");
            string run = Path.Combine(root, "run");
            string scratch = Path.Combine(root, "check");

            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            Directory.CreateDirectory(run);

            File.WriteAllText(Path.Combine(run, "config.json"), RunConfigJson.Write(GripCheckpointTests.GrippingWorld(true)));
            File.WriteAllText(Path.Combine(run, "run.json"), "{\"seed\": 1}\n");

            var console = new StringWriter();
            TextWriter was = Console.Out;
            int result;

            try
            {
                Console.SetOut(console);
                result = CheckpointFidelity.Run(run, seconds, scratch, threads: 2);
            }
            finally
            {
                Console.SetOut(was);
            }

            _out.WriteLine(console.ToString());
            Assert.Equal(0, result);
        }

        // ------------------------------------------------------------------ what the run says

        private static (JsonNode stats, Dictionary<long, int> flags, int tableCells, IReadOnlyList<string> columns)
            Sample(bool brain)
        {
            string scratch = SimulationTests.Scratch(brain ? "grip-cols-on" : "grip-cols-off");
            RunConfig config = GripCheckpointTests.GrippingWorld(brain);
            var world = new World(config, 1UL);
            RunDirectory dir = RunDirectory.Create(scratch, config, DateTime.UtcNow);
            string runPath = dir.Path;
            int cells;
            IReadOnlyList<string> columns;

            using (dir)
            using (var sim = new Simulation(world, 1UL, 0.02f, StepsPerMetabolic, threads: 2, runDirectory: dir.Path))
            {
                for (int m = 0; m < 120 && GripsHeld(sim) < 3; m++)
                {
                    for (int i = 0; i < StepsPerMetabolic; i++) sim.Step();
                }

                var report = new Report(Path.Combine(scratch, "report.md"), sim.Config);
                columns = report.Columns;
                string row = new Sampler().Write(sim, dir, report.Columns);
                cells = row.Split('|').Length - 2;
            }

            string[] rows = JsonlWriter.ReadRows(Path.Combine(runPath, "stats.jsonl"));
            JsonNode stats = Json.Parse(rows[rows.Length - 1]);
            Dictionary<long, int> flags = FilmWindowTests.PositionFlags(runPath).Values.Last();
            return (stats, flags, cells, columns);
        }

        [Fact]
        public void TheReportTheStatsRowAndThePositionsSayWhatIsHeldOnlyWhereTheTableHoldsTheCell()
        {
            (JsonNode on, Dictionary<long, int> flagsOn, int cellsOn, IReadOnlyList<string> columnsOn) = Sample(brain: true);

            foreach (string c in Report.GripColumns)
            {
                Assert.True(on.Has(c), "the stats row has no " + c);
                Assert.Contains(c, columnsOn);
            }

            Assert.Equal(columnsOn.Count, cellsOn);
            Assert.True(on["gripsHeld"].AsDouble() >= 3);
            Assert.True(on["gripsFormed"].AsDouble() >= on["gripsHeld"].AsDouble());
            Assert.True(on["gripJoules"].AsDouble() > 0);

            // Before the patch columns, after everything else.
            Assert.Equal(Report.GripColumns.Length, columnsOn.Count - columnsOn.ToList().IndexOf(Report.GripColumns[0]) - 1);
            Assert.StartsWith("p", columnsOn[columnsOn.Count - 1]);
            Assert.Equal("gripJoules", columnsOn[columnsOn.Count - 2]);

            // Positions: the anchored bit is set on bodies that hold a grip, and on them alone.
            int anchored = flagsOn.Values.Count(f => (f & PositionsRow.AnchoredBit) != 0);
            _out.WriteLine(anchored + " of " + flagsOn.Count + " bodies flagged as anchored; " + on["gripsHeld"].AsDouble() + " grips held");
            Assert.True(anchored > 0);

            // The same world with the cell and no switch writes the same columns (the cell is what puts them in).
            (JsonNode off, _, int cellsOff, IReadOnlyList<string> columnsOff) = Sample(brain: false);
            Assert.Equal(columnsOff.Count, cellsOff);
            Assert.True(off.Has("gripsHeld"));
            Assert.Equal(columnsOn.Count, columnsOff.Count);

            // A world without the cell has none of it.
            RunConfig without = GripCheckpointTests.GrippingWorld(false);
            without.CellTypes = CellTypeRegistry.Standard;
            Assert.DoesNotContain("gripsHeld", Sampler.Columns(without));
            Assert.Equal(new Report("x", without).Columns.Count, Sampler.Columns(without).Count);
            Assert.Equal(
                Report.GripColumns.Length,
                Sampler.Columns(GripCheckpointTests.GrippingWorld(false)).Count - Sampler.Columns(without).Count);
        }

        [Fact]
        public void TheAnchoredBitIsTheStreamsToo()
        {
            Assert.Equal(16, PositionsRow.AnchoredBit);
            Assert.Equal(PositionsRow.AnchoredBit, PoseStream.AnchoredBit);
            Assert.Equal(PositionsRow.AllBits, PoseStream.AllFlagBits);
            Assert.Equal(31, PositionsRow.AllBits);
        }
    }
}
