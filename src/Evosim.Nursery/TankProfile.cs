using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Evosim.Dynamics;
using Evosim.Farm;

namespace Evosim.Nursery
{
    /// <summary>
    /// Where a tank search's wall time goes, written only when <c>EVOSIM_NURSERY_PROFILE=1</c>
    /// (logbook/specs/nursery-speed.md).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One row per pool of tanks</b> (a generation's swim, or one exam control's), in
    /// <c>profile.jsonl</c> beside <c>timing.jsonl</c>: the pool's wall and process CPU, and for
    /// every tank job its thread, when it started and ended against the pool's start, and how its
    /// episode split between setup, the world, the physics (by the solver's five phases, and the
    /// bodies' phase by the water at their links, senses, brain and the rest) and the harness. The start and end times are
    /// what show the tail at a generation's end, where threads wait on the last tanks.
    /// </para>
    /// <para>
    /// <b>Off, nothing is written and nothing is timed beyond what already was.</b> On, the clock
    /// is read and nothing is drawn, so a run with it on is the same realisation as one with it
    /// off: the nursery's digest check proves that. The file holds wall times, so it is not a
    /// result, and the registry leaves it out of a run's digests as it does <c>timing.jsonl</c>.
    /// </para>
    /// </remarks>
    internal static class TankProfile
    {
        public const string FileName = "profile.jsonl";

        /// <summary>True when the run was started with <c>EVOSIM_NURSERY_PROFILE=1</c>.</summary>
        public static readonly bool On = Environment.GetEnvironmentVariable("EVOSIM_NURSERY_PROFILE") == "1";

        /// <summary>
        /// The job the episode on this thread fills: set by <c>SwimTanks</c> around each tank and
        /// read by <c>TankEpisode</c>, so the episode's signature and the swim's call stay as they
        /// are without the profile. Null on every thread when the profile is off.
        /// </summary>
        [ThreadStatic] public static Job Current;

        private static readonly object Gate = new object();
        private static int _pools;

        /// <summary>One tank job's reading, filled by its own thread.</summary>
        public sealed class Job
        {
            public int Draw, Thread, Bodies;
            public long Start, End, Setup;
            public long PhysicsMs, WorldMs, HarnessMs, Steps;
            public readonly long[] Phases = new long[5];
            public readonly long[] Body = new long[5];

            /// <summary>Simulated seconds at which the tank's last landed body died; NaN while any lived.</summary>
            public double AllDeadAt = double.NaN;

            /// <summary>Reads what the episode's harness and solver counted.</summary>
            public void Take(Simulation sim)
            {
                PhysicsMs = sim.WallPhysicsMs;
                WorldMs = sim.WallWorldMs;
                HarnessMs = sim.WallHarnessMs;
                Steps = sim.Dynamics.Steps;
                Array.Copy(sim.Dynamics.PhaseTicks, Phases, Phases.Length);
                Array.Copy(sim.Dynamics.BodyTicks, Body, Body.Length);
            }
        }

        /// <summary>A pool of tank jobs, timed from its construction.</summary>
        public sealed class Pool
        {
            public readonly Job[] Jobs;
            private readonly long _start = Stopwatch.GetTimestamp();
            private readonly TimeSpan _cpu = Process.GetCurrentProcess().TotalProcessorTime;
            private readonly int _generation, _draws, _threads;
            private readonly ulong _salt;

            public Pool(int jobs, int generation, int draws, ulong salt, int threads)
            {
                Jobs = new Job[jobs];
                for (int j = 0; j < jobs; j++) Jobs[j] = new Job();
                _generation = generation;
                _draws = draws;
                _salt = salt;
                _threads = threads;

                // The solver's split of a body's step: set before any world in this process steps.
                DynamicsWorld.ProfileBodySteps = true;
            }

            /// <summary>Marks job <paramref name="j"/> started on this thread.</summary>
            public Job Begin(int j, int draw)
            {
                Job job = Jobs[j];
                job.Draw = draw;
                job.Thread = Environment.CurrentManagedThreadId;
                job.Start = Stopwatch.GetTimestamp() - _start;
                return job;
            }

            public void End(Job job) => job.End = Stopwatch.GetTimestamp() - _start;

            /// <summary>Appends the pool's row to the run's profile.</summary>
            public void Write(string folder)
            {
                long wall = Stopwatch.GetTimestamp() - _start;
                double cpu = (Process.GetCurrentProcess().TotalProcessorTime - _cpu).TotalMilliseconds;
                double f = Stopwatch.Frequency / 1000.0;
                var inv = CultureInfo.InvariantCulture;

                var row = new StringBuilder();
                lock (Gate)
                {
                    row.Append(string.Format(inv,
                        "{{\"pool\":{0},\"gen\":{1},\"draws\":{2},\"salt\":{3},\"threads\":{4},\"wallMs\":{5:0.0},\"cpuMs\":{6:0.0},\"jobs\":[",
                        _pools++, _generation, _draws, _salt, _threads, wall / f, cpu));
                    for (int j = 0; j < Jobs.Length; j++)
                    {
                        Job x = Jobs[j];
                        if (j > 0) row.Append(',');
                        row.Append(string.Format(inv,
                            "{{\"j\":{0},\"draw\":{1},\"thread\":{2},\"bodies\":{3},\"startMs\":{4:0.0},\"endMs\":{5:0.0},\"setupMs\":{6:0.0},",
                            j, x.Draw, x.Thread, x.Bodies, x.Start / f, x.End / f, x.Setup / f));
                        row.Append(string.Format(inv,
                            "\"worldMs\":{0},\"physicsMs\":{1},\"harnessMs\":{2},\"steps\":{3},",
                            x.WorldMs, x.PhysicsMs, x.HarnessMs, x.Steps));
                        row.Append(string.Format(inv,
                            "\"gridMs\":{0:0.0},\"pinMs\":{1:0.0},\"bodiesMs\":{2:0.0},\"commitMs\":{3:0.0},\"afterMs\":{4:0.0},",
                            x.Phases[0] / f, x.Phases[1] / f, x.Phases[2] / f, x.Phases[3] / f, x.Phases[4] / f));
                        row.Append(string.Format(inv,
                            "\"waterMs\":{5:0.0},\"senseMs\":{0:0.0},\"brainMs\":{1:0.0},\"restMs\":{2:0.0},\"bodySteps\":{3},\"allDeadAt\":{4}}}",
                            x.Body[0] / f, x.Body[1] / f, x.Body[2] / f, x.Body[3],
                            double.IsNaN(x.AllDeadAt) ? "null" : x.AllDeadAt.ToString("0.0", inv), x.Body[4] / f));
                    }
                    row.Append("]}");

                    File.AppendAllText(Path.Combine(folder, FileName), row + "\n", new UTF8Encoding(false));
                }
            }
        }
    }
}
