using System;
using System.IO;
using System.Linq;
using Evosim.NurseryRoom;
using Xunit;

namespace Evosim.NurseryRoom.Tests
{
    /// <summary>
    /// The room's cap as the owner ruled it on 2026-10-11 (logbook/specs/nursery-room-queue-cap.md): the machine's queue read for the room's slot and the
    /// cores other jobs claim, the heat check's verdict, and the words of each refusal. The launch's own tests (LaunchTests) check the mask a run is pinned to.
    /// </summary>
    public class QueueCapTests
    {
        private const string File = FixedQueue.File;
        private static readonly HeatReading Ok = HeatReading.Of(HeatReading.Ok, "2026-10-11 12:00:00  OK  gpu 50C");

        private static QueueReading Parse(string text) => QueueReading.Parse(text, File);

        private static QueueJob One(string line) => Assert.Single(Parse("## Running\n\n" + line + "\n").Running);

        // ------------------------------------------------------------------ the queue's parse

        [Fact]
        public void AJobsTextIsWhatStandsBeforeItsFirstDotAndItsTagsFollow()
        {
            QueueJob j = One("- evolution-simulator-17 (video) · from 14:30, on 01's go: the 4K refilm, unity-w6 on 0-15 · cores: 0-15 · card");
            Assert.Equal("evolution-simulator-17 (video)", j.Text);
            Assert.Equal(0xFFFFUL, j.Cores);
            Assert.Null(j.CoresProblem);
            Assert.True(j.Card);
            Assert.False(j.Room);

            QueueJob room = One("- evolution-simulator-25 (room) · one job at a time, BelowNormal, off the card · cores: 20-23 · room");
            Assert.True(room.Room);
            Assert.False(room.Card);   // "off the card" is prose, not the tag
            Assert.Equal(0xF00000UL, room.Cores);
        }

        [Theory]
        [InlineData("cores: 16-31", 0xFFFF0000UL)]
        [InlineData("cores: 0-3,8-11", 0xF0FUL)]
        [InlineData("cores: 0-3, 8-11", 0xF0FUL)]          // spaces are ignored
        [InlineData("cores: 7", 0x80UL)]                   // a single processor is written a
        [InlineData("cores: 31,0", 0x80000001UL)]
        [InlineData("Cores: 63", 0x8000000000000000UL)]
        public void ACoresTagReadsRangesAndSingleProcessors(string tag, ulong mask)
        {
            QueueJob j = One("- a job · " + tag);
            Assert.Null(j.CoresProblem);
            Assert.Equal(mask, j.Cores);
        }

        [Fact]
        public void ALineWithNoCoresTagClaimsEveryProcessorAndSaysWhy()
        {
            QueueJob bare = One("- Neighbour (neighbour-37) · its Editor may open when its owner plays");
            Assert.Equal(ulong.MaxValue, bare.Cores);
            Assert.Equal("has no cores: tag", bare.CoresProblem);
            QueueJob noTags = One("- a job with no tags at all");
            Assert.Equal("a job with no tags at all", noTags.Text);
            Assert.Equal(ulong.MaxValue, noTags.Cores);
        }

        [Theory]
        [InlineData("cores: 3-1")]       // backwards
        [InlineData("cores: 64")]        // past the 64 a mask holds
        [InlineData("cores: x")]
        [InlineData("cores:")]
        [InlineData("cores: 0-3;8-11")]
        [InlineData("cores 16-19")]      // no colon
        public void ACoresTagTheRoomCannotReadClaimsEveryProcessorAndQuotesIt(string tag)
        {
            QueueJob j = One("- a job · " + tag);
            Assert.Equal(ulong.MaxValue, j.Cores);
            Assert.Equal($"has a cores tag the room cannot read (\"{tag}\"; it is cores: a-b or a, joined by commas, each from 0 to 63)", j.CoresProblem);
        }

        [Fact]
        public void TwoCoresTagsOnOneLineClaimEveryProcessor()
        {
            QueueJob j = One("- a job · cores: 0-3 · cores: 8-11");
            Assert.Equal(ulong.MaxValue, j.Cores);
            Assert.Equal("has 2 cores tags", j.CoresProblem);
        }

        [Fact]
        public void OnlyTheJobLinesOfTheRunningSectionAreRead()
        {
            string text = "# The machine's queue\r\n\r\nEach line: who · what · cores: a-b · card\r\n- an example above the sections · cores: 0-31\r\n\r\n"
                + "## Running\r\n\r\nSessions since the restart: prose · cores: 0-31, which is not read\r\n\r\n- one · cores: 16-19\r\n"
                + "  - an indented line is prose · cores: 0-31\r\nMore prose between the jobs.\r\n- two · cores: 0-3 · room\r\n\r\n"
                + "## Next, in order\r\n\r\n- three · cores: 24-31\r\n\r\n## Running\r\n\r\n- a second Running section is past the first's end · cores: 8-11\r\n";
            QueueReading q = Parse(text);
            Assert.Null(q.Problem);
            Assert.Equal(new[] { "one", "two" }, q.Running.Select(j => j.Text).ToArray());
            Assert.True(q.Running[1].Room);
        }

        [Fact]
        public void AQueueWithNoRunningSectionIsAProblemThatNamesTheFile()
        {
            QueueReading q = Parse("# The machine's queue\n\n## Running jobs\n\n- one · cores: 0-3 · room\n\n## Next, in order\n");
            Assert.Equal("the machine's queue scratch/machine/queue.md has no ## Running section", q.Problem);
            Assert.Empty(q.Running);
        }

        [Fact]
        public void AQueueFileThatIsNotThereCannotBeRead()
        {
            string dir = Where.Fresh("queue");
            try
            {
                QueueReading q = new QueueFileReader(Path.Combine(dir, "queue.md"), File).Read();
                Assert.Equal("the machine's queue scratch/machine/queue.md cannot be read (there is no such file)", q.Problem);
                System.IO.File.WriteAllText(Path.Combine(dir, "queue.md"), "## Running\n\n- the room · cores: 20-23 · room\n");
                Assert.Equal(0xF00000UL, Assert.Single(new QueueFileReader(Path.Combine(dir, "queue.md"), File).Read().Running).Cores);
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void AListIsWrittenAsTheQueueWritesIt()
        {
            Assert.Equal("16-31", CoreMask.List(0xFFFF0000UL));
            Assert.Equal("0-3,6-31", CoreMask.List(0xFFFFFFCFUL));
            Assert.Equal("7", CoreMask.List(0x80UL));
            Assert.Equal("0,2,63", CoreMask.List(0x8000000000000005UL));
            Assert.Equal("none", CoreMask.List(0UL));
        }

        // ------------------------------------------------------------------ the slot

        [Fact]
        public void TheFreeCoresAreTheSlotInsideTheBoundLessFourAndFiveLessWhatOtherLinesClaim()
        {
            QueueReading q = Parse("## Running\n\n- the room · cores: 0-15 · room\n- neighbour · cores: 0-3\n- speed · cores: 12-19\n- far · cores: 24-31\n");
            Slot s = Cap.SlotOf(q, ulong.MaxValue);
            Assert.Equal(0xFFFFUL, s.Room);
            Assert.Equal(0xFFCFUL, s.Usable);
            Assert.Equal(0x0FC0UL, s.Free);   // 6-11
            Assert.Equal(new[] { "neighbour", "speed" }, s.Claims.Select(c => c.Job.Text).ToArray());   // far claims nothing inside the slot
            Assert.Equal("12-15", CoreMask.List(s.Claims[1].Inside));
            Assert.Equal("the room's slot is logical 0-15, less 4-5, which the room never uses, less 0-3 claimed by \"neighbour\", less 12-15 claimed by \"speed\"", s.Explain());

            // with room.json's bound of every processor but 4 and 5, the two are outside it and said so
            Assert.Equal("the room's slot is logical 0-15, less 4-5 outside room.json's affinity 0xFFFFFFCF, less 0-3 claimed by \"neighbour\", less 12-15 claimed by \"speed\"",
                Cap.SlotOf(q, 0xFFFFFFCFUL).Explain());
        }

        [Fact]
        public void TwoRoomLinesMakeOneSlotAndNeitherClaimsAgainstTheOther()
        {
            Slot s = Cap.SlotOf(Parse("## Running\n\n- the room · cores: 16-19 · room\n- the room's second slot · cores: 24-27 · room\n"), 0xFFFFFFCFUL);
            Assert.Equal("16-19,24-27", CoreMask.List(s.Free));
            Assert.Empty(s.Claims);
        }

        // ------------------------------------------------------------------ each refusal's words

        [Fact]
        public void EachRefusalNamesWhatRefusedIt()
        {
            ulong bound = 0xFFFFFFCFUL;
            Assert.Equal("the machine's queue scratch/machine/queue.md cannot be read (there is no such file), so the room's slot is not known; refusing 4 threads",
                Cap.Refusal(4, Cap.SlotOf(QueueReading.Failed(File, "there is no such file"), bound), Ok));
            Assert.Equal("the machine's queue scratch/machine/queue.md has no ## Running section, so the room's slot is not known; refusing 4 threads",
                Cap.Refusal(4, Cap.SlotOf(Parse("# nothing\n"), bound), Ok));
            Assert.Equal("no Running line of the machine's queue scratch/machine/queue.md carries the room tag, so the room has no slot, and it starts nothing until the coordinator gives it one; refusing 1 thread",
                Cap.Refusal(1, Cap.SlotOf(Parse("## Running\n\n- speed · cores: 16-19\n"), bound), Ok));
            Assert.Equal("the room's line \"the room\" in the machine's queue scratch/machine/queue.md has 2 cores tags, so the room's slot is not known; refusing 1 thread",
                Cap.Refusal(1, Cap.SlotOf(Parse("## Running\n\n- the room · cores: 16-19 · cores: 20-23 · room\n"), bound), Ok));

            Slot slot = Cap.SlotOf(Parse("## Running\n\n- the room · cores: 20-23 · room\n- speed · cores: 16-21\n"), bound);
            Assert.Equal("the heat check reads HOT (2026-10-11 12:00:00  HOT  gpu 86C), and the room starts nothing on a hot machine; refusing 2 threads",
                Cap.Refusal(2, slot, HeatReading.Of(HeatReading.Hot, "2026-10-11 12:00:00  HOT  gpu 86C")));
            Assert.Equal("the heat check cannot say whether the machine is too hot (the heat check exited 3); refusing 2 threads",
                Cap.Refusal(2, slot, HeatReading.FromExit(3, "")));
            Assert.Equal("the heat check cannot say whether the machine is too hot (no reading); refusing 2 threads", Cap.Refusal(2, slot, null));
            Assert.Null(Cap.Refusal(2, slot, HeatReading.Of(HeatReading.Warn, "WARN")));   // WARN does not refuse
            Assert.Equal("3 threads asked is more than the 2 free cores (logical 22-23): the room's slot is logical 20-23, less 20-21 claimed by \"speed\"",
                Cap.Refusal(3, slot, Ok));
            Assert.Null(Cap.Refusal(2, slot, Ok));
        }

        // ------------------------------------------------------------------ the heat check

        [Theory]
        [InlineData(0, "2026-10-11 12:00:00  OK  gpu 50C 120W", "OK")]
        [InlineData(1, "2026-10-11 12:00:00  WARN  gpu 81C  [GPU 81 C]", "WARN")]
        [InlineData(2, "2026-10-11 12:00:00  HOT  gpu 86C  [GPU 86 C]", "HOT")]
        public void TheHeatChecksExitCodeIsItsVerdictWithItsLine(int code, string line, string verdict)
        {
            HeatReading h = HeatReading.FromExit(code, "\r\n" + line + "\r\n");
            Assert.Equal(verdict, h.Verdict);
            Assert.Equal(line, h.Line);
        }

        [Fact]
        public void AHeatCheckThatCannotSayIsUnreadable()
        {
            Assert.Equal(HeatReading.Unreadable, HeatReading.FromExit(3, "").Verdict);
            Assert.Equal("the heat check exited 7: something", HeatReading.FromExit(7, "something").Line);
            // PowerShell ends a script that fails with exit code 1, which is not a WARN
            HeatReading failed = HeatReading.FromExit(1, "Get-Counter : The specified object was not found on the computer.");
            Assert.Equal(HeatReading.Unreadable, failed.Verdict);
            Assert.Equal("the heat check exited 1, which is WARN, but printed no WARN line: Get-Counter : The specified object was not found on the computer.", failed.Line);
        }

        [Fact]
        public void AHeatScriptThatIsNotThereIsUnreadableAndNothingIsRun()
        {
            string dir = Where.Fresh("heat");
            try
            {
                HeatReading h = new ScriptHeatReader(Path.Combine(dir, "temp-check.ps1"), "scripts/temp-check.ps1").Read();
                Assert.Equal(HeatReading.Unreadable, h.Verdict);
                Assert.Equal("the heat check scripts/temp-check.ps1 is not there", h.Line);
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void TheRoomsOwnSettingsNameAQueueAndAHeatScriptTheTestsNeverRun()
        {
            RoomSettings s = Where.Settings();
            Assert.Equal("scratch/machine/queue.md", s.QueueFile);
            Assert.Equal(Where.NoHeatScript, s.HeatScript);
            Assert.False(System.IO.File.Exists(Path.Combine(Where.Root, Where.NoHeatScript.Replace('/', Path.DirectorySeparatorChar))));
        }
    }
}
