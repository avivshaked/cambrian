using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Evosim.Theatre
{
    /// <summary>
    /// Sends one take's frames, in order, to one ffmpeg process that encodes them on the graphics
    /// card's encoder and writes the take's mp4 itself: no PNG is written. The safari opens a
    /// stream at each take's start under <c>EVOSIM_THEATRE_STREAM=nvenc</c> and closes it at the
    /// take's end; while one is open, <see cref="SnapshotCamera.CapturePlaced"/> hands its frames
    /// here instead of to <see cref="FrameWriter"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why.</b> The PNG encode was about 270 ms of a 4K frame and set the pace of filming, and
    /// <c>scripts/theatre-safari.ps1</c> then decoded every PNG again to encode the take with
    /// libx264. The owner ruled on 2026-09-28 that the frames need not go to disk.
    /// </para>
    /// <para>
    /// <b>One writer thread, so the frames stay in order.</b> The PNG writer's two threads may
    /// finish out of order, which a file per frame does not mind and a pipe does. The guarantees
    /// are <see cref="FrameWriter"/>'s: at most <see cref="MostInFlight"/> frames are queued, the
    /// producer waits rather than drops, the first failure is kept and thrown by the next
    /// <see cref="Enqueue"/> and by <see cref="Close"/>, and a wait that sees nothing finish for
    /// <see cref="StuckSeconds"/> declares the stream stuck and kills ffmpeg.
    /// </para>
    /// <para>
    /// <b>The mp4 is only ever whole.</b> ffmpeg writes <c>take-N.partial.mp4</c>; <see cref="Close"/>
    /// closes its stdin, waits for it to finish the file, and renames it to <c>take-N.mp4</c> only
    /// when ffmpeg exited 0. A take that ends early, a failure and the domain's unloading all
    /// close the stream the same way. An Editor killed outright leaves the partial file, which
    /// nothing reads.
    /// </para>
    /// <para>
    /// <b>The pixels as the PNG path's clips carry them.</b> The frame goes over as RGBA, bottom
    /// row first as the texture holds it; ffmpeg's <c>vflip</c> turns it (a pointer flip, no copy),
    /// and the card converts RGB to YUV 4:2:0. The card's conversion is BT.601 at limited range,
    /// which is what libx264 from the PNGs has always produced, and the colour description is then
    /// set to "unspecified", as libx264's clips carry none: the two clips decode the same in any
    /// player (measured by <c>scripts/tests/theatre-stream/test_stream_encode.py</c>).
    /// </para>
    /// <para>
    /// <b>Sixteen bytes before the first frame.</b> A redirected stdin is a <see cref="StreamWriter"/>
    /// whose encoding may have a byte-order mark, which it would write ahead of the frames and
    /// shift every pixel by three bytes. The stream flushes the writer once, so any mark is
    /// written then, pads it to <see cref="HeaderBytes"/> with zeros, and ffmpeg skips that many
    /// bytes (<c>-skip_initial_bytes</c>). The log says how long the mark was.
    /// </para>
    /// </remarks>
    public static class FrameStream
    {
        /// <summary>The switch, read by the safari: <c>nvenc</c> streams, unset, <c>0</c> or <c>off</c> writes PNGs.</summary>
        public const string Variable = "EVOSIM_THEATRE_STREAM";

        /// <summary>The value of <see cref="Variable"/> that streams.</summary>
        public const string On = "nvenc";

        /// <summary>Frames queued or being written at once, at most. The producer waits past it.</summary>
        public const int MostInFlight = 4;

        /// <summary>
        /// How long a wait may go without one frame reaching ffmpeg, and how long ffmpeg may take to
        /// finish the mp4 after its stdin closes, seconds, before the stream is declared stuck.
        /// </summary>
        public const double StuckSeconds = 120d;

        /// <summary>Bytes ahead of the first frame: a byte-order mark if the pipe's writer has one, then zeros.</summary>
        public const int HeaderBytes = 16;

        /// <summary>
        /// ffmpeg's arguments: {0} the width, {1} the height, {2} the frame rate, {3} the partial
        /// mp4. <c>scripts/tests/theatre-stream/test_stream_encode.py</c> reads this literal and runs
        /// it, and <c>scripts/theatre-safari.ps1</c>'s <c>$streamCodec</c> must carry the same
        /// encoder options, so the time station's crossfade joins a streamed take without a
        /// re-encode of the take.
        /// </summary>
        public const string ArgumentsTemplate =
            "-hide_banner -nostats -loglevel error -y -f rawvideo -pix_fmt rgba -s {0}x{1} -framerate {2} -skip_initial_bytes 16 -i - " +
            "-vf vflip -c:v h264_nvenc -preset p7 -tune hq -profile:v high -rc constqp -qp 16 -bf 3 -b_ref_mode middle -g 250 -pix_fmt rgba " +
            "-bsf:v h264_metadata=colour_primaries=2:transfer_characteristics=2:matrix_coefficients=2 -r {2} -movflags +faststart -f mp4 \"{3}\"";

        private sealed class Job
        {
            public Color32[] Pixels;
            public SnapshotCamera.StageTimes Times;
            public Action<Color32[]> Done;
        }

        private static readonly object Gate = new object();
        private static readonly Queue<Job> Queue = new Queue<Job>();
        private static readonly Queue<string> ErrorTail = new Queue<string>();

        private static bool _open;
        private static bool _stopping;
        private static string _path, _partial;
        private static int _fps, _width, _height;
        private static Process _process;
        private static Stream _stdin;
        private static Thread _thread;
        private static byte[] _bytes;
        private static int _inFlight;
        private static long _finished;
        private static int _taken, _written;
        private static string _fault;
        private static bool _hooked;

        /// <summary>Whether a take's stream is open: the camera's frames go here while it is.</summary>
        public static bool IsOpen
        {
            get { lock (Gate) return _open; }
        }

        /// <summary>The last closed take's line for the log: the file, the frames, the mark, the time to finish.</summary>
        public static string LastSummary { get; private set; } = "";

        /// <summary>Frames written to ffmpeg since the domain loaded.</summary>
        public static long Written { get; private set; }

        /// <summary>
        /// Opens a take's stream. ffmpeg starts with the first frame, at that frame's size, so a
        /// take that keeps no frame makes no mp4. A stale <paramref name="mp4Path"/> and its partial
        /// file are deleted now.
        /// </summary>
        public static void Open(string mp4Path, int fps)
        {
            if (string.IsNullOrEmpty(mp4Path)) throw new ArgumentException("a stream needs its mp4's path", nameof(mp4Path));
            if (fps < 1) throw new ArgumentOutOfRangeException(nameof(fps), "a frame rate from 1");

            lock (Gate)
            {
                if (_open) throw new InvalidOperationException("a take's stream is open already (" + _path + "): close it first");

                string directory = Path.GetDirectoryName(mp4Path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                _path = mp4Path;
                _partial = Path.Combine(directory ?? "", Path.GetFileNameWithoutExtension(mp4Path) + ".partial.mp4");
                if (File.Exists(_path)) File.Delete(_path);
                if (File.Exists(_partial)) File.Delete(_partial);

                _fps = fps;
                _width = _height = 0;
                _process = null;
                _stdin = null;
                _thread = null;
                _stopping = false;
                _inFlight = 0;
                _taken = _written = 0;
                _fault = null;
                lock (ErrorTail) ErrorTail.Clear();
                _open = true;

                Hook();
            }
        }

        /// <summary>
        /// Hands a frame to the take's ffmpeg. Blocks while <see cref="MostInFlight"/> frames are in
        /// flight, and throws the first failure any earlier frame met.
        /// </summary>
        /// <param name="pixels">
        /// The frame, bottom row first as a texture holds it. Only read; the caller must not write
        /// into it again until <paramref name="done"/> has handed it back.
        /// </param>
        /// <param name="done">Called on the stream's thread once the array is no longer read.</param>
        public static void Enqueue(Color32[] pixels, int width, int height, SnapshotCamera.StageTimes times, Action<Color32[]> done)
        {
            if (pixels == null) throw new ArgumentNullException(nameof(pixels));
            if (pixels.Length != width * height) throw new ArgumentException("the frame holds " + pixels.Length + " pixels, not " + width + "x" + height);

            long started = Stopwatch.GetTimestamp();

            lock (Gate)
            {
                if (!_open) throw new InvalidOperationException("no take's stream is open");
                Surface(clear: false);

                if (_process == null)
                {
                    Launch(width, height);
                }
                else if (width != _width || height != _height)
                {
                    throw new IOException(string.Format(CultureInfo.InvariantCulture,
                        "the frame stream for {0} was started at {1}x{2} and was handed a {3}x{4} frame", _path, _width, _height, width, height));
                }

                if (!WaitWhile(() => _inFlight >= MostInFlight))
                {
                    Stuck("a free slot");
                }
                Surface(clear: false);

                Queue.Enqueue(new Job { Pixels = pixels, Times = times, Done = done });
                _inFlight++;
                _taken++;
                Monitor.PulseAll(Gate);
            }

            WaitedMs += Milliseconds(Stopwatch.GetTimestamp() - started);
        }

        /// <summary>Wall time the producer spent handing frames over since the domain loaded, ms.</summary>
        public static double WaitedMs { get; private set; }

        /// <summary>
        /// Closes the take's stream: waits for every frame to reach ffmpeg, closes its stdin, waits
        /// for it to finish the mp4 and renames the file to its own name. Throws the first failure
        /// any frame met, a stuck wait, or ffmpeg's non-zero exit, with the end of what ffmpeg
        /// printed; the failure is cleared by being thrown, and the partial file is left beside the
        /// take. Does nothing when no stream is open.
        /// </summary>
        /// <returns>The frames written to the mp4.</returns>
        public static int Close()
        {
            Process process;
            Stream stdin;
            Thread thread;
            bool drained;

            lock (Gate)
            {
                if (!_open) return 0;

                drained = _process == null || WaitWhile(() => _inFlight > 0);
                if (!drained && _fault == null)
                {
                    _fault = string.Format(CultureInfo.InvariantCulture,
                        "the frame stream finished nothing for {0:0} s while waiting for the take's last frames, with {1} in flight", StuckSeconds, _inFlight);
                }

                _stopping = true;
                Monitor.PulseAll(Gate);
                process = _process;
                stdin = _stdin;
                thread = _thread;
            }

            long closing = Stopwatch.GetTimestamp();
            int exit = 0;
            bool exited = true;

            if (process != null)
            {
                // A stuck ffmpeg is killed first, which breaks the pipe and frees a writer blocked in it.
                if (!drained) Kill(process);

                try
                {
                    stdin?.Close();
                }
                catch (Exception e)
                {
                    Fault("closing ffmpeg's stdin threw " + e.GetType().Name + ": " + e.Message);
                }

                try
                {
                    exited = process.WaitForExit((int)(1000d * StuckSeconds));
                    if (!exited)
                    {
                        Fault(string.Format(CultureInfo.InvariantCulture, "ffmpeg did not finish the mp4 within {0:0} s of its input closing, and was killed", StuckSeconds));
                        Kill(process);
                        process.WaitForExit(10000);
                    }
                    else
                    {
                        // The overload without a timeout also waits for the error stream's last lines.
                        process.WaitForExit();
                        exit = process.ExitCode;
                    }
                }
                catch (Exception e)
                {
                    Fault("waiting for ffmpeg threw " + e.GetType().Name + ": " + e.Message);
                }

                if (thread != null && !thread.Join(10000)) Fault("the frame stream's thread did not stop");
                if (exited && exit != 0) Fault("ffmpeg exited with code " + exit.ToString(CultureInfo.InvariantCulture));

                try { process.Dispose(); } catch (Exception) { /* nothing left to release */ }
            }

            double finishMs = Milliseconds(Stopwatch.GetTimestamp() - closing);
            string fault;
            int written, taken;
            string path = _path, partial = _partial;

            lock (Gate)
            {
                fault = _fault;
                written = _written;
                taken = _taken;
            }

            if (fault == null && process != null)
            {
                try
                {
                    var made = new FileInfo(partial);
                    if (!made.Exists || made.Length == 0) throw new IOException("ffmpeg exited 0 and left no file at " + partial);
                    if (File.Exists(path)) File.Delete(path);
                    File.Move(partial, path);
                }
                catch (Exception e)
                {
                    fault = e.GetType().Name + ": " + e.Message;
                }
            }

            if (fault != null)
            {
                string tail;
                lock (ErrorTail) tail = string.Join(" | ", ErrorTail.ToArray());
                if (tail.Length > 0) fault += "; ffmpeg said: " + tail;
            }

            LastSummary = process == null
                ? "stream: no frame kept, no mp4 for " + path
                : string.Format(CultureInfo.InvariantCulture,
                    "stream: {0} of {1} frame(s) to {2}, {3}x{4} at {5} fps, h264_nvenc; ffmpeg finished {6:0.0} s after its input closed{7}",
                    written, taken, fault == null ? path : partial, _width, _height, _fps, finishMs / 1000d,
                    fault == null ? "" : "; FAILED");

            lock (Gate)
            {
                _open = false;
                _stopping = false;
                _process = null;
                _stdin = null;
                _thread = null;
                _fault = null;
                Queue.Clear();
                _inFlight = 0;
            }

            if (fault != null) throw new IOException("the take's mp4 was not made (" + path + "): " + fault);
            return written;
        }

        // ---------------------------------------------------------------- inside the gate

        private static void Launch(int width, int height)
        {
            _width = width;
            _height = height;
            _bytes = _bytes != null && _bytes.Length == width * height * 4 ? _bytes : new byte[width * height * 4];

            var info = new ProcessStartInfo(Ffmpeg(), string.Format(CultureInfo.InvariantCulture, ArgumentsTemplate, width, height, _fps, _partial))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = false,
            };

            // Where the runtime has it, the pipe's writer gets an encoding with no byte-order mark;
            // the header below covers a runtime that has not.
            PropertyInfo encoding = typeof(ProcessStartInfo).GetProperty("StandardInputEncoding");
            if (encoding != null && encoding.CanWrite) encoding.SetValue(info, new UTF8Encoding(false), null);

            Process process;
            try
            {
                process = new Process { StartInfo = info };
                process.ErrorDataReceived += (sender, line) =>
                {
                    if (string.IsNullOrEmpty(line.Data)) return;
                    lock (ErrorTail)
                    {
                        ErrorTail.Enqueue(line.Data);
                        while (ErrorTail.Count > 12) ErrorTail.Dequeue();
                    }
                };
                process.Start();
                process.BeginErrorReadLine();
            }
            catch (Exception e)
            {
                throw new IOException("ffmpeg (" + info.FileName + ") did not start for " + _path + ": " + e.GetType().Name + ": " + e.Message, e);
            }

            int mark;
            try
            {
                // Any mark the writer has is written by this flush, before any frame, and never again.
                StreamWriter writer = process.StandardInput;
                writer.Flush();
                mark = writer.Encoding.GetPreamble().Length;
                if (mark > HeaderBytes) throw new IOException("the pipe's writer has a " + mark + "-byte mark, more than the " + HeaderBytes + " ffmpeg skips");

                _stdin = writer.BaseStream;
                _stdin.Write(new byte[HeaderBytes - mark], 0, HeaderBytes - mark);
                _stdin.Flush();
            }
            catch (Exception e)
            {
                Kill(process);
                throw new IOException("ffmpeg's stdin could not be prepared for " + _path + ": " + e.GetType().Name + ": " + e.Message, e);
            }

            _process = process;
            _thread = new Thread(Work) { IsBackground = true, Name = "Theatre frame stream" };
            _thread.Start();

            UnityEngine.Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[Theatre] stream: ffmpeg {0} started for {1} at {2}x{3}, {4} fps, h264_nvenc constqp 16; the pipe's writer had a {5}-byte mark, padded to {6}",
                process.Id, _partial, width, height, _fps, mark, HeaderBytes));
        }

        /// <summary>Monitor.Wait on the gate until the condition clears (true) or nothing finishes for too long (false).</summary>
        private static bool WaitWhile(Func<bool> condition)
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

                if (Milliseconds(Stopwatch.GetTimestamp() - lastProgress) > 1000d * StuckSeconds) return false;
            }

            return true;
        }

        private static void Stuck(string what)
        {
            if (_fault == null)
            {
                _fault = string.Format(CultureInfo.InvariantCulture,
                    "the frame stream finished nothing for {0:0} s while waiting for {1}, with {2} frame(s) in flight", StuckSeconds, what, _inFlight);
            }

            Surface(clear: false);
        }

        private static void Surface(bool clear)
        {
            if (_fault == null) return;

            string fault = _fault;
            if (clear) _fault = null;
            throw new IOException("a frame was not streamed: " + fault);
        }

        private static void Fault(string what)
        {
            lock (Gate)
            {
                if (_fault == null) _fault = what;
            }
        }

        private static void Kill(Process process)
        {
            try
            {
                if (!process.HasExited) process.Kill();
            }
            catch (Exception)
            {
                // Gone already, or not ours to kill: the exit code and the fault say the rest.
            }
        }

        private static string Ffmpeg()
        {
            string path = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string directory in path.Split(Path.PathSeparator))
            {
                string trimmed = directory.Trim().Trim('"');
                if (trimmed.Length == 0) continue;
                try
                {
                    string candidate = Path.Combine(trimmed, "ffmpeg.exe");
                    if (File.Exists(candidate)) return candidate;
                }
                catch (ArgumentException)
                {
                    // A malformed PATH entry names nothing.
                }
            }

            return "ffmpeg";
        }

        /// <summary>
        /// Whatever ends the domain (the Editor quitting, a reload, leaving Play mode) closes an
        /// open stream first, so its mp4 is finished rather than cut off.
        /// </summary>
        private static void Hook()
        {
            if (_hooked) return;
            _hooked = true;

            Application.quitting += CloseOnTheWayOut;
            AppDomain.CurrentDomain.DomainUnload += (sender, args) => CloseOnTheWayOut();
            AppDomain.CurrentDomain.ProcessExit += (sender, args) => CloseOnTheWayOut();
        }

        private static void CloseOnTheWayOut()
        {
            try
            {
                if (!IsOpen) return;
                Close();
                UnityEngine.Debug.LogWarning("[Theatre] stream: closed on the way out: " + LastSummary);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[Theatre] stream: closing on the way out: " + e.Message);
            }
        }

        // ---------------------------------------------------------------- the stream's thread

        private static void Work()
        {
            while (true)
            {
                Job job;
                Stream stdin;
                byte[] bytes;

                lock (Gate)
                {
                    while (Queue.Count == 0 && !_stopping) Monitor.Wait(Gate);
                    if (Queue.Count == 0) return;
                    job = Queue.Dequeue();
                    stdin = _stdin;
                    bytes = _bytes;
                }

                string failure = null;
                double copyMs = 0d, writeMs = 0d;

                try
                {
                    long started = Stopwatch.GetTimestamp();
                    int length = job.Pixels.Length * 4;
                    if (bytes == null || bytes.Length != length) throw new IOException("the frame's buffer is the wrong size");

                    GCHandle pinned = GCHandle.Alloc(job.Pixels, GCHandleType.Pinned);
                    try
                    {
                        Marshal.Copy(pinned.AddrOfPinnedObject(), bytes, 0, length);
                    }
                    finally
                    {
                        pinned.Free();
                    }

                    long copied = Stopwatch.GetTimestamp();
                    stdin.Write(bytes, 0, length);

                    copyMs = Milliseconds(copied - started);
                    writeMs = Milliseconds(Stopwatch.GetTimestamp() - copied);
                }
                catch (ThreadAbortException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    failure = "writing a frame to ffmpeg threw " + e.GetType().Name + ": " + e.Message;
                }

                if (failure == null) job.Times?.AddWriter(copyMs, writeMs);

                try
                {
                    job.Done?.Invoke(job.Pixels);
                }
                catch (Exception e)
                {
                    if (failure == null) failure = "handing the pixels back threw " + e.GetType().Name + ": " + e.Message;
                }

                lock (Gate)
                {
                    _inFlight--;
                    _finished++;
                    if (failure == null)
                    {
                        _written++;
                        Written++;
                    }
                    else if (_fault == null)
                    {
                        _fault = failure;
                    }

                    Monitor.PulseAll(Gate);
                }
            }
        }

        private static double Milliseconds(long ticks) => 1000d * ticks / Stopwatch.Frequency;
    }
}
