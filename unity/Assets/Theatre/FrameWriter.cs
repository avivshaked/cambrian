using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Evosim.Theatre
{
    /// <summary>
    /// Turns a film's or a safari's frames into PNG files off the main thread: a queue of at most
    /// <see cref="MostInFlight"/> frames and <see cref="ThreadCount"/> threads that encode and
    /// write them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this exists.</b> A safari frame of round 47 seed 2 at 1920x1080 cost about 200 ms of
    /// wall time on 2026-09-24, and the render and its read-back were about 33 ms of it. Most of
    /// the rest was the main thread under Mono encoding the PNG and writing it, which nothing
    /// else was waiting on. The encode reads only the frame's own pixels, so it can run beside
    /// the next frame's stepping and drawing.
    /// </para>
    /// <para>
    /// <b>Bounded, and it waits rather than drops.</b> The producer blocks while
    /// <see cref="MostInFlight"/> frames are queued or being encoded, so a slow disk slows the
    /// film instead of filling the memory with frames. A frame that fails to encode or to write
    /// is never dropped quietly: the first failure is kept, the next <see cref="Enqueue"/>
    /// throws it, and <see cref="Flush"/> throws it after every frame still in flight has
    /// finished. The hosts flush at the end of every take and before they write anything that
    /// lists frames (the safari's <c>captions.tsv</c> and <c>scenes.tsv</c>) or quit.
    /// </para>
    /// <para>
    /// <b>The encoder is the one <see cref="Texture2D.EncodeToPNG"/> uses.</b>
    /// <see cref="ImageConversion.EncodeArrayToPNG"/> is documented as safe on any thread, and it
    /// is handed the same bytes and the same graphics format as the texture the synchronous path
    /// encodes, so a frame's pixels are the same whichever path wrote it.
    /// <c>EVOSIM_THEATRE_DOWNSAMPLE_CHECK=1</c> encodes the first frames both ways and says
    /// whether they agree; <c>EVOSIM_THEATRE_SYNC_ENCODE=1</c> puts the encode back on the main
    /// thread.
    /// </para>
    /// </remarks>
    public static class FrameWriter
    {
        /// <summary>Frames queued or being encoded at once, at most. The producer waits past it.</summary>
        public const int MostInFlight = 4;

        /// <summary>
        /// Threads encoding and writing: two, so that one slow encode does not hold the queue full,
        /// and no more, because the world's own stepping wants the machine's cores.
        /// </summary>
        public const int ThreadCount = 2;

        /// <summary>
        /// How long a wait may go without one frame finishing before the writer is declared stuck,
        /// seconds. A frame takes well under a second to encode, so this is a dead thread and
        /// never a slow one.
        /// </summary>
        public const double StuckSeconds = 120d;

        private sealed class Job
        {
            public Color32[] Pixels;
            public GraphicsFormat Format;
            public int Width;
            public int Height;
            public string Path;
            public SnapshotCamera.StageTimes Times;
            public Action<Color32[]> Done;
        }

        private static readonly object Gate = new object();
        private static readonly Queue<Job> Queue = new Queue<Job>();
        private static Thread[] _threads;
        private static int _inFlight;
        private static long _finished;
        private static string _fault;

        /// <summary>Frames written since the domain loaded.</summary>
        public static long Written { get; private set; }

        /// <summary>Wall time the producer spent waiting for a free slot since the domain loaded, ms.</summary>
        public static double WaitedMs { get; private set; }

        /// <summary>
        /// Hands a frame to the writer. Blocks while <see cref="MostInFlight"/> frames are in
        /// flight, and throws the first failure any earlier frame met.
        /// </summary>
        /// <param name="pixels">
        /// The frame, bottom row first as a texture holds it. The writer only reads it; the caller
        /// must not write into it again until <paramref name="done"/> has handed it back.
        /// </param>
        /// <param name="done">Called on the writer's thread once the array is no longer read.</param>
        public static void Enqueue(
            Color32[] pixels, GraphicsFormat format, int width, int height, string path,
            SnapshotCamera.StageTimes times, Action<Color32[]> done)
        {
            if (pixels == null) throw new ArgumentNullException(nameof(pixels));
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("a frame needs a path", nameof(path));

            var job = new Job
            {
                Pixels = pixels, Format = format, Width = width, Height = height, Path = path,
                Times = times, Done = done,
            };

            long started = Stopwatch.GetTimestamp();

            lock (Gate)
            {
                Surface(clear: false);
                StartThreads();

                WaitWhile(() => _inFlight >= MostInFlight, "a free slot");
                Surface(clear: false);

                Queue.Enqueue(job);
                _inFlight++;
                Monitor.PulseAll(Gate);

                WaitedMs += Milliseconds(Stopwatch.GetTimestamp() - started);
            }
        }

        /// <summary>
        /// Waits until every frame handed over has been written, then throws the first failure
        /// any of them met (once: the failure is cleared by being thrown here).
        /// </summary>
        public static void Flush()
        {
            lock (Gate)
            {
                WaitWhile(() => _inFlight > 0, "the last frames to be written");
                Surface(clear: true);
            }
        }

        /// <summary>Frames queued or being encoded now.</summary>
        public static int InFlight
        {
            get { lock (Gate) return _inFlight; }
        }

        // ---------------------------------------------------------------- inside the gate

        /// <summary>Monitor.Wait on the gate until the condition clears or nothing finishes for too long.</summary>
        private static void WaitWhile(Func<bool> condition, string what)
        {
            long finishedWas = _finished;
            long lastProgress = Stopwatch.GetTimestamp();

            while (condition())
            {
                Monitor.Wait(Gate, 1000);

                if (_finished != finishedWas)
                {
                    finishedWas = _finished;
                    lastProgress = Stopwatch.GetTimestamp();
                    continue;
                }

                if (Milliseconds(Stopwatch.GetTimestamp() - lastProgress) > 1000d * StuckSeconds)
                {
                    if (_fault == null)
                    {
                        _fault = string.Format(
                            System.Globalization.CultureInfo.InvariantCulture,
                            "the frame writer finished nothing for {0:0} s while waiting for {1}, with {2} frame(s) in flight",
                            StuckSeconds, what, _inFlight);
                    }

                    Surface(clear: false);
                }
            }
        }

        private static void Surface(bool clear)
        {
            if (_fault == null) return;

            string fault = _fault;
            if (clear) _fault = null;
            throw new IOException("a frame was not written: " + fault);
        }

        private static void StartThreads()
        {
            if (_threads != null) return;

            _threads = new Thread[ThreadCount];
            for (int i = 0; i < _threads.Length; i++)
            {
                _threads[i] = new Thread(Work)
                {
                    IsBackground = true,
                    Name = "Theatre frame writer " + (i + 1),
                };
                _threads[i].Start();
            }
        }

        // ---------------------------------------------------------------- the writer's threads

        private static void Work()
        {
            while (true)
            {
                Job job;
                lock (Gate)
                {
                    while (Queue.Count == 0) Monitor.Wait(Gate);
                    job = Queue.Dequeue();
                }

                string failure = null;
                double encodeMs = 0d, writeMs = 0d;

                try
                {
                    long started = Stopwatch.GetTimestamp();
                    byte[] png = ImageConversion.EncodeArrayToPNG(
                        job.Pixels, job.Format, (uint)job.Width, (uint)job.Height);
                    long encoded = Stopwatch.GetTimestamp();

                    if (png == null || png.Length == 0) throw new IOException("the encoder returned no bytes");

                    File.WriteAllBytes(job.Path, png);

                    encodeMs = Milliseconds(encoded - started);
                    writeMs = Milliseconds(Stopwatch.GetTimestamp() - encoded);
                }
                catch (ThreadAbortException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    failure = job.Path + ": " + e.GetType().Name + ": " + e.Message;
                }

                if (failure == null) job.Times?.AddWriter(encodeMs, writeMs);

                try
                {
                    job.Done?.Invoke(job.Pixels);
                }
                catch (Exception e)
                {
                    if (failure == null) failure = job.Path + ": handing the pixels back threw " + e.GetType().Name + ": " + e.Message;
                }

                lock (Gate)
                {
                    _inFlight--;
                    _finished++;
                    if (failure == null) Written++;
                    else if (_fault == null) _fault = failure;
                    Monitor.PulseAll(Gate);
                }
            }
        }

        private static double Milliseconds(long ticks) => 1000d * ticks / Stopwatch.Frequency;
    }
}
