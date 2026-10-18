using System;
using System.Collections.Generic;
using System.IO;
using Evosim.Core;
using Evosim.Farm;
using Xunit;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// D127's corpses in a film window: the farm writes every corpse standing in the window, and the
    /// reader hands each one back from the frame after its body's last until the step it leaves.
    /// </summary>
    /// <remarks>
    /// One recording for the class: <see cref="FilmWindowTests"/>' small world with corpses that sink
    /// at a metre a second, so that they settle on the bed inside a minute, and D128's hazard at a
    /// base of 15 s, so that bodies die of age from the first quarter minute on. A window is filmed
    /// at fifty frames a second, which is every physics step at 0.02 s.
    /// </remarks>
    [Collection(FarmProcessCollection.Name)]
    public class FilmWindowCorpseTests : IClassFixture<FilmWindowCorpseTests.CorpseRun>
    {
        private readonly CorpseRun _run;

        public FilmWindowCorpseTests(CorpseRun run)
        {
            _run = run;
        }

        public sealed class CorpseRun : IDisposable
        {
            public string Root { get; }
            public string RunDirectory { get; }

            public CorpseRun()
            {
                Root = Path.Combine(AppContext.BaseDirectory, "corpse-film-tests", Guid.NewGuid().ToString("N"));
                string runsRoot = Path.Combine(Root, "runs");

                var settings = new List<string>(FilmWindowTests.SmallRun(runsRoot, "corpses", seconds: 60, checkpointEvery: 20))
                {
                    "EVOSIM_CORPSE_DECAY=0.02", "EVOSIM_CORPSE_SINK=1", "EVOSIM_CORPSE_SETTLE=1",
                    "EVOSIM_CORPSE_FLOOR=0.05", "EVOSIM_AGE_BASE=15", "EVOSIM_AGE_DOUBLING=0.2",
                };

                int exit = Program.Main(settings.ToArray());
                if (exit != 0) throw new InvalidOperationException("The recording exited " + exit + ".");

                RunDirectory = Directory.GetDirectories(Path.Combine(runsRoot, "corpses"))[0];
            }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(Root)) Directory.Delete(Root, true);
                }
                catch (IOException)
                {
                }
            }
        }

        private FilmWindow.Result Film(double from, double to, string name, out string outDirectory)
        {
            outDirectory = Path.Combine(_run.Root, "windows", name + "-" + Guid.NewGuid().ToString("N"));

            return FilmWindow.Film(
                new FilmWindow.Options
                {
                    Run = _run.RunDirectory,
                    FromSeconds = from,
                    ToSeconds = to,
                    Out = outDirectory,
                    FramesPerSecond = 50d,
                    Threads = 2,
                },
                setting => null,
                TextWriter.Null);
        }

        /// <summary>
        /// A window from 35 to 45 s writes every corpse once, with a genome for its body: those
        /// standing at the start from the run's own file, since they died before the window. It
        /// writes a step row at every metabolic step, and stays faithful, because a corpse is read
        /// after the step and nothing the world steps on reads it back.
        /// </summary>
        [Fact]
        public void AWindowWritesEveryCorpseOnceWithItsGenomeAndStaysFaithful()
        {
            FilmWindow.Result result = Film(35d, 45d, "every", out string outDirectory);

            Assert.Equal(FilmWindow.Faithful, result.Verdict);
            Assert.True(result.Corpses > 0, "no corpse in the window");
            Assert.True(result.CorpseGenomes > 0, "no corpse stood at the start");
            Assert.Equal(0, result.CorpsesWithoutAGenome);

            // 35.0, 35.5, ... 45.0: the opening and every metabolic step to the end, once each.
            Assert.Equal(21, result.CorpseSteps);

            using (FilmWindowReader window = FilmWindowReader.Open(outDirectory))
            {
                Assert.Equal(result.Corpses, window.CorpseCount);
                Assert.Equal(result.Corpses, window.Verdict.Corpses);
                Assert.Equal(result.CorpseGenomes, window.Verdict.CorpseGenomes);

                int settled = 0;
                int deaths = 0;

                foreach (FilmWindowCorpse corpse in window.Corpses)
                {
                    Assert.True(window.TryGenome(corpse.Id, out _), "corpse " + corpse.Id + " has no genome row");
                    Assert.True(corpse.SampleCount > 0, "corpse " + corpse.Id + " has no place");
                    Assert.True(corpse.FirstJoules > 0d);
                    Assert.True(corpse.ScaledBy > 0f && corpse.ScaledBy <= 1f, "corpse " + corpse.Id + " scaled by " + corpse.ScaledBy);

                    // A corpse founded inside the window is its body's death row, at the same second.
                    // One founded at the opening second is not: a death at the start second is
                    // outside the window's events, because the body is already absent there.
                    if (!double.IsNaN(corpse.Born) && corpse.Born > 35d + 1e-6)
                    {
                        Assert.True(window.TryDeathOf(corpse.Id, out FilmWindowEvent death),
                            "corpse " + corpse.Id + " founded at " + corpse.Born + " has no death row");
                        Assert.Equal(corpse.Born, death.Seconds, 6);
                        deaths++;
                    }

                    settled += CheckTrack(window, corpse);
                }

                Assert.True(settled > 0, "no corpse settled in the window");
                Assert.True(deaths > 0, "no corpse was founded inside the window");
            }
        }

        /// <summary>
        /// One corpse's samples read back through <see cref="FilmWindowReader.CorpsesAt"/>: in the small
        /// world, which has no current, it only sinks and then lies still on the floor, and it never
        /// gains joules or holds less than its floor. Returns 1 when it settled.
        /// </summary>
        private static int CheckTrack(FilmWindowReader window, FilmWindowCorpse corpse)
        {
            var places = new List<FilmWindowCorpsePlace>();
            float lastY = float.PositiveInfinity;
            float lastFraction = float.PositiveInfinity;
            bool wasSettled = false;
            float restX = 0f, restY = 0f, restZ = 0f;

            for (int k = 0; k < corpse.SampleCount; k++)
            {
                double at = corpse.SampleSeconds(k);
                window.CorpsesAt(at, places);
                FilmWindowCorpsePlace place = places.Find(p => p.Corpse.Id == corpse.Id);
                Assert.True(place.Corpse != null, "corpse " + corpse.Id + " not standing at its own sample " + at);

                Assert.True(place.Y <= lastY + 1e-3f, "corpse " + corpse.Id + " rose at " + at);
                Assert.True(place.Fraction <= lastFraction + 1e-4f, "corpse " + corpse.Id + " grew at " + at);
                Assert.True(place.Fraction >= 0.05f - 1e-4f, "corpse " + corpse.Id + " under its floor at " + at);

                if (wasSettled)
                {
                    Assert.True(place.Settled);
                    Assert.Equal(restX, place.X, 3);
                    Assert.Equal(restY, place.Y, 3);
                    Assert.Equal(restZ, place.Z, 3);
                }
                else if (place.Settled)
                {
                    wasSettled = true;
                    restX = place.X;
                    restY = place.Y;
                    restZ = place.Z;

                    // A box has no bed shape and no reef: the floor is the world's depth.
                    Assert.Equal(-20f, place.Y, 3);
                }

                lastY = place.Y;
                lastFraction = place.Fraction;
            }

            return wasSettled ? 1 : 0;
        }

        /// <summary>
        /// At fifty frames a second, which is every physics step, a corpse founded inside the window
        /// stands in every frame from its founding second until the step it leaves, and never in a
        /// frame that holds its body: the body's last frame is the one just before.
        /// </summary>
        [Fact]
        public void ACorpseTakesOverFromItsBodyWithNoGapAndNoOverlap()
        {
            FilmWindow.Result result = Film(40d, 44d, "frames", out string outDirectory);
            Assert.Equal(FilmWindow.Faithful, result.Verdict);

            using (FilmWindowReader window = FilmWindowReader.Open(outDirectory))
            {
                var founded = new List<FilmWindowCorpse>();
                foreach (FilmWindowCorpse corpse in window.Corpses)
                {
                    if (!double.IsNaN(corpse.Born)) founded.Add(corpse);
                }

                Assert.True(founded.Count > 0, "no body died inside the window");

                var places = new List<FilmWindowCorpsePlace>();
                var lastBodyFrame = new Dictionary<long, int>();

                for (int f = 0; f < window.FrameCount; f++)
                {
                    PoseFrame frame = window.ReadFrame(f);

                    var bodies = new HashSet<long>();
                    foreach (PoseBody body in frame.Bodies)
                    {
                        bodies.Add(body.Id);
                        lastBodyFrame[body.Id] = f;
                    }

                    window.CorpsesAt(frame.Seconds, places);
                    var corpses = new HashSet<long>();
                    foreach (FilmWindowCorpsePlace place in places) corpses.Add(place.Corpse.Id);

                    foreach (long id in corpses)
                    {
                        Assert.False(bodies.Contains(id), "creature " + id + " is drawn alive and as a corpse at " + frame.Seconds);
                    }

                    foreach (FilmWindowCorpse corpse in founded)
                    {
                        bool due = frame.Seconds >= corpse.Born - 1e-6 && frame.Seconds < corpse.Gone - 1e-6;
                        Assert.True(due == corpses.Contains(corpse.Id),
                            "corpse " + corpse.Id + " (founded " + corpse.Born + ", gone " + corpse.Gone + ") is " +
                            (due ? "missing" : "drawn") + " at " + frame.Seconds);
                    }
                }

                int handedOver = 0;

                foreach (FilmWindowCorpse corpse in founded)
                {
                    if (!lastBodyFrame.TryGetValue(corpse.Id, out int last) || last + 1 >= window.FrameCount) continue;

                    Assert.True(window.SecondOf(last) < corpse.Born - 1e-6);
                    Assert.True(window.SecondOf(last + 1) >= corpse.Born - 1e-6,
                        "corpse " + corpse.Id + " founded at " + corpse.Born + " after its body's last frame at " + window.SecondOf(last));
                    handedOver++;
                }

                Assert.True(handedOver > 0, "no body was framed before its death");
            }
        }

        /// <summary>A world whose dead leave no corpse writes no corpse file, and its verdict counts none.</summary>
        [Fact]
        public void AWorldWithoutCorpsesWritesNoCorpseFile()
        {
            string root = Path.Combine(_run.Root, "no-corpses");
            string runsRoot = Path.Combine(root, "runs");

            int exit = Program.Main(FilmWindowTests.SmallRun(runsRoot, "plain", seconds: 12, checkpointEvery: 0));
            Assert.Equal(0, exit);

            string run = Directory.GetDirectories(Path.Combine(runsRoot, "plain"))[0];
            string outDirectory = Path.Combine(root, "window");

            FilmWindow.Result result = FilmWindow.Film(
                new FilmWindow.Options { Run = run, FromSeconds = 5d, ToSeconds = 7d, Out = outDirectory, FramesPerSecond = 10d, Threads = 2 },
                setting => null,
                TextWriter.Null);

            Assert.Equal(FilmWindow.Faithful, result.Verdict);
            Assert.Equal(0, result.Corpses);
            Assert.Equal(0, result.CorpseSteps);
            Assert.False(File.Exists(Path.Combine(outDirectory, FilmWindow.CorpsesFileName)));

            using (FilmWindowReader window = FilmWindowReader.Open(outDirectory))
            {
                Assert.Equal(0, window.CorpseCount);
            }
        }
    }
}
