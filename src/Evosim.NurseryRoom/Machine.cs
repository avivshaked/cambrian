using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Evosim.NurseryRoom
{
    /// <summary>
    /// One job of the machine's queue: a line of its <c>## Running</c> section that starts with <c>- </c>. What stands before its first <c> · </c> is its
    /// text; each part after one is a tag (<c>cores: a-b[,c-d]</c>, <c>card</c>, <c>room</c>) or prose, which is not read.
    /// </summary>
    public sealed class QueueJob
    {
        /// <summary>The line's text before its first <c> · </c>: what a refusal names the line by.</summary>
        public string Text;

        /// <summary>The logical processors the line claims, a bit each (bit 0 is processor 0): every one when <see cref="CoresProblem"/> says why its cores could not be read.</summary>
        public ulong Cores;

        /// <summary>Null when the line's <c>cores:</c> tag was read; else why not, said of the line ("has no cores: tag"), and the line claims every processor.</summary>
        public string CoresProblem;

        /// <summary>The job uses the graphics card. The room never does; it shows the tag and does nothing else with it.</summary>
        public bool Card;

        /// <summary>The room's own slot. The coordinator sets it, and moves the slot by changing the line's <c>cores:</c>.</summary>
        public bool Room;
    }

    /// <summary>The machine's queue as the room reads it (room.json's <c>queueFile</c>, in the main checkout): its Running jobs, or why they could not be read.</summary>
    public sealed class QueueReading
    {
        public const string RunningHeading = "## Running";

        /// <summary>The queue file as a refusal names it: repository-relative, as room.json holds it.</summary>
        public string File;
        public List<QueueJob> Running = new List<QueueJob>();

        /// <summary>Null when the Running section was read; else the reason as a refusal says it, naming the file.</summary>
        public string Problem;
        public DateTime AtUtc = DateTime.UtcNow;

        public static QueueReading Failed(string file, string why) => new QueueReading { File = file, Problem = $"the machine's queue {file} cannot be read ({why})" };

        private static readonly Regex CoresWord = new Regex(@"\Acores\b", RegexOptions.IgnoreCase);
        private static readonly Regex CoresTag = new Regex(@"\Acores:(.*)\z", RegexOptions.IgnoreCase);

        /// <summary>
        /// The Running jobs of the queue's text: from the line <c>## Running</c> to the next line that starts with <c>## </c>, each line in between that starts
        /// with <c>- </c>. Prose between the job lines is not read. The coordinator owns the file; the room only reads it.
        /// </summary>
        public static QueueReading Parse(string text, string file)
        {
            var q = new QueueReading { File = file };
            bool inRunning = false, found = false;
            foreach (string raw in (text ?? "").Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    if (found) break;   // the next section: nothing below the Running one is read
                    inRunning = found = line.TrimEnd() == RunningHeading;
                    continue;
                }
                if (inRunning && line.StartsWith("- ", StringComparison.Ordinal)) q.Running.Add(Job(line.Substring(2)));
            }
            if (!found) q.Problem = $"the machine's queue {file} has no {RunningHeading} section";
            return q;
        }

        /// <summary>
        /// One job line: its parts are split at each <c>·</c> and trimmed. A part that is <c>card</c> or <c>room</c> is that tag, and one that begins with the
        /// word <c>cores</c> is the cores tag; any other part is prose. A line with no cores tag, with more than one, or with one that does not read as
        /// <c>cores: a-b[,c-d]</c> claims every processor, and says why.
        /// </summary>
        private static QueueJob Job(string body)
        {
            string[] parts = body.Split('·').Select(p => p.Trim()).ToArray();
            var job = new QueueJob { Text = parts[0] };
            var cores = new List<string>();
            foreach (string p in parts.Skip(1))
            {
                if (p.Equals("card", StringComparison.OrdinalIgnoreCase)) job.Card = true;
                else if (p.Equals("room", StringComparison.OrdinalIgnoreCase)) job.Room = true;
                else if (CoresWord.IsMatch(p)) cores.Add(p);
            }
            Match m = cores.Count == 1 ? CoresTag.Match(cores[0]) : Match.Empty;
            if (cores.Count == 1 && m.Success && CoreMask.TryParseList(m.Groups[1].Value, out ulong mask)) { job.Cores = mask; return job; }
            job.Cores = ulong.MaxValue;
            job.CoresProblem = cores.Count == 0 ? "has no cores: tag"
                : cores.Count > 1 ? $"has {cores.Count} cores tags"
                : $"has a cores tag the room cannot read (\"{cores[0]}\"; it is cores: a-b or a, joined by commas, each from 0 to 63)";
            return job;
        }
    }

    /// <summary>Where the machine's queue comes from; the room's tests give it a value.</summary>
    public interface IQueueReader
    {
        QueueReading Read();
    }

    /// <summary>Reads the queue file on every call, as it is now (it is short, and the coordinator moves the room's slot by editing it). It never writes it.</summary>
    public sealed class QueueFileReader : IQueueReader
    {
        private readonly string _path, _shown;

        /// <param name="path">The file's full path, in the main checkout.</param>
        /// <param name="shown">The file as a refusal names it (room.json's <c>queueFile</c>).</param>
        public QueueFileReader(string path, string shown) { _path = path; _shown = shown; }

        public QueueReading Read()
        {
            string text;
            try
            {
                using (var s = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var r = new StreamReader(s, Encoding.UTF8)) text = r.ReadToEnd();
            }
            catch (Exception e) when (e is FileNotFoundException || e is DirectoryNotFoundException) { return QueueReading.Failed(_shown, "there is no such file"); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { return QueueReading.Failed(_shown, e.Message.Replace(_path, _shown)); }
            return QueueReading.Parse(text, _shown);
        }
    }

    /// <summary>The heat check's verdict (room.json's <c>heatScript</c>): OK, WARN, HOT, or UNREADABLE when it cannot say, with the line it printed or why it gave none.</summary>
    public sealed class HeatReading
    {
        public const string Ok = "OK", Warn = "WARN", Hot = "HOT", Unreadable = "UNREADABLE";

        public string Verdict;
        public string Line;
        public DateTime AtUtc = DateTime.UtcNow;

        public static HeatReading Of(string verdict, string line) => new HeatReading { Verdict = verdict, Line = line };

        /// <summary>
        /// The verdict of the script's exit code and what it printed: 0 OK, 1 WARN, 2 HOT, 3 (and any other code) UNREADABLE. A printed line that does
        /// not name the verdict its exit code gives is UNREADABLE too: PowerShell ends a script that fails with exit code 1, which is not a WARN.
        /// </summary>
        public static HeatReading FromExit(int exitCode, string output)
        {
            string line = (output ?? "").Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? "";
            if (line.Length > 300) line = line.Substring(0, 300) + "...";
            string verdict = exitCode == 0 ? Ok : exitCode == 1 ? Warn : exitCode == 2 ? Hot : null;
            if (verdict == null) return Of(Unreadable, $"the heat check exited {exitCode}" + (line.Length == 0 ? "" : ": " + line));
            if (!Regex.IsMatch(line, @"(^|\s)" + verdict + @"(\s|$)"))
                return Of(Unreadable, $"the heat check exited {exitCode}, which is {verdict}, but printed no {verdict} line" + (line.Length == 0 ? "" : ": " + line));
            return Of(verdict, line);
        }
    }

    /// <summary>Where the heat verdict comes from; the room's tests give it a value and never run the script.</summary>
    public interface IHeatReader
    {
        HeatReading Read();
    }

    /// <summary>
    /// Runs the heat check with <c>pwsh</c> (<c>scripts/temp-check.ps1</c>: one line, and an exit code for its verdict) and keeps the reading for
    /// <see cref="Keep"/>, so that a screen's poll and an agent's call do not each run it.
    /// </summary>
    public sealed class ScriptHeatReader : IHeatReader
    {
        public static readonly TimeSpan Keep = TimeSpan.FromSeconds(60);

        private readonly object _gate = new object();
        private readonly string _script, _shown;
        private HeatReading _last;

        /// <param name="script">The script's full path.</param>
        /// <param name="shown">The script as a reading names it (room.json's <c>heatScript</c>).</param>
        public ScriptHeatReader(string script, string shown) { _script = script; _shown = shown; }

        public HeatReading Read()
        {
            lock (_gate)
            {
                if (_last != null && DateTime.UtcNow - _last.AtUtc < Keep) return _last;
                _last = ReadNow();
                return _last;
            }
        }

        private HeatReading ReadNow()
        {
            if (!File.Exists(_script)) return HeatReading.Of(HeatReading.Unreadable, $"the heat check {_shown} is not there");
            try
            {
                var info = new ProcessStartInfo("pwsh")
                {
                    RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
                };
                foreach (string a in new[] { "-NoProfile", "-NonInteractive", "-File", _script }) info.ArgumentList.Add(a);
                using (Process p = Process.Start(info))
                {
                    var output = p.StandardOutput.ReadToEndAsync();
                    var error = p.StandardError.ReadToEndAsync();
                    if (!p.WaitForExit(30000))
                    {
                        try { p.Kill(true); } catch (Exception) { }
                        return HeatReading.Of(HeatReading.Unreadable, $"the heat check {_shown} did not answer within 30 s");
                    }
                    p.WaitForExit();
                    return HeatReading.FromExit(p.ExitCode, output.Result.Trim().Length > 0 ? output.Result : error.Result);
                }
            }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception || e is InvalidOperationException || e is IOException)
            {
                return HeatReading.Of(HeatReading.Unreadable, $"the heat check {_shown} could not be run with pwsh: " + e.Message);
            }
        }
    }

    /// <summary>A Running line of the queue that claims cores inside the room's slot, and which of them.</summary>
    public sealed class SlotClaim
    {
        public QueueJob Job;
        public ulong Inside;
    }

    /// <summary>
    /// The room's slot as the queue gives it now: the cores of the Running lines tagged <c>room</c>, inside room.json's <c>affinity</c> (the bound the room
    /// never leaves), less logical 4 and 5, less every core another Running line claims. What is left is free, and a launch is pinned to it.
    /// </summary>
    public sealed class Slot
    {
        /// <summary>room.json's affinity.</summary>
        public ulong Bound;

        /// <summary>The cores of the room's lines, as the queue names them.</summary>
        public ulong Room;

        /// <summary>The room's cores inside the bound, less those the room never uses (<see cref="Cap.Never"/>).</summary>
        public ulong Usable;

        /// <summary>The usable cores no other Running line claims: those a launch is pinned to.</summary>
        public ulong Free;

        /// <summary>The other Running lines that claim usable cores, in the queue's order.</summary>
        public List<SlotClaim> Claims = new List<SlotClaim>();

        /// <summary>Null when the slot is known; else why not (the queue cannot be read, or no line carries room), as a refusal says it.</summary>
        public string Problem;

        public QueueReading Queue;

        /// <summary>How the free cores come from the slot, as a refusal and the screens say it.</summary>
        public string Explain()
        {
            var sb = new StringBuilder($"the room's slot is logical {CoreMask.List(Room)}");
            if ((Room & ~Bound) != 0) sb.Append($", less {CoreMask.List(Room & ~Bound)} outside room.json's affinity {CoreMask.Format(Bound)}");
            if ((Room & Bound & Cap.Never) != 0) sb.Append($", less {CoreMask.List(Room & Bound & Cap.Never)}, which the room never uses");
            foreach (SlotClaim c in Claims)
                sb.Append($", less {CoreMask.List(c.Inside)} claimed by \"{c.Job.Text}\"" + (c.Job.CoresProblem == null ? "" : $", which {c.Job.CoresProblem} and so claims every processor"));
            return sb.ToString();
        }
    }

    /// <summary>
    /// The cap: what the machine's queue gives the room, and the heat. A launch of n threads, and an episode step of one, is refused when the queue
    /// cannot be read, when no Running line carries <c>room</c>, when the heat check reads HOT or cannot read, or when n plus the threads the room has
    /// granted that still count is more than the free cores (the owner's ruling of 2026-10-11, logbook/specs/nursery-room-queue-cap.md).
    /// </summary>
    public static class Cap
    {
        /// <summary>Logical 4 and 5, which the room never uses, whatever the queue or room.json says.</summary>
        public const ulong Never = 0x30UL;

        /// <summary>How long a launch counts as using its threads whatever its run.json says yet: a run takes a while to write it.</summary>
        public static readonly TimeSpan RecentLaunch = TimeSpan.FromMinutes(3);

        /// <summary>The room's slot from a reading of the queue, inside <paramref name="bound"/> (room.json's affinity).</summary>
        public static Slot SlotOf(QueueReading queue, ulong bound)
        {
            var s = new Slot { Bound = bound, Queue = queue };
            if (queue == null) { s.Problem = "the machine's queue was not read, so the room's slot is not known"; return s; }
            if (queue.Problem != null) { s.Problem = queue.Problem + ", so the room's slot is not known"; return s; }
            List<QueueJob> mine = queue.Running.Where(j => j.Room).ToList();
            if (mine.Count == 0)
            {
                s.Problem = $"no Running line of the machine's queue {queue.File} carries the room tag, so the room has no slot, and it starts nothing until the coordinator gives it one";
                return s;
            }
            QueueJob unread = mine.FirstOrDefault(j => j.CoresProblem != null);
            if (unread != null)
            {
                s.Problem = $"the room's line \"{unread.Text}\" in the machine's queue {queue.File} {unread.CoresProblem}, so the room's slot is not known";
                return s;
            }
            foreach (QueueJob j in mine) s.Room |= j.Cores;
            s.Usable = s.Room & bound & ~Never;
            ulong claimed = 0;
            foreach (QueueJob j in queue.Running.Where(j => !j.Room))
            {
                ulong inside = j.Cores & s.Usable;
                if (inside == 0) continue;
                s.Claims.Add(new SlotClaim { Job = j, Inside = inside });
                claimed |= inside;
            }
            s.Free = s.Usable & ~claimed;
            return s;
        }

        /// <summary>
        /// Null when a start of <paramref name="threads"/> threads may go ahead, or the refusal, which names what refused it: the queue or the room's line,
        /// the heat verdict and its line, or the free cores against the threads asked, with how the slot gives them. WARN does not refuse.
        /// </summary>
        public static string Refusal(int threads, Slot slot, HeatReading heat, int grantedThreads = 0)
        {
            if (slot.Problem != null) return $"{slot.Problem}; refusing {Threads(threads)}";
            if (heat == null || (heat.Verdict != HeatReading.Ok && heat.Verdict != HeatReading.Warn && heat.Verdict != HeatReading.Hot))
                return $"the heat check cannot say whether the machine is too hot ({heat?.Line ?? "no reading"}); refusing {Threads(threads)}";
            if (heat.Verdict == HeatReading.Hot)
                return $"the heat check reads HOT ({heat.Line}), and the room starts nothing on a hot machine; refusing {Threads(threads)}";
            int free = CoreMask.Count(slot.Free);
            int asked = threads + grantedThreads;
            if (asked <= free) return null;
            string how = grantedThreads > 0
                ? $"{Threads(threads)} asked, plus {Threads(grantedThreads)} granted to runs this room started that are still running or began in the last {(int)RecentLaunch.TotalMinutes} minutes and to episode re-steps whose process lives, is {asked},"
                : $"{Threads(threads)} asked is";
            string cores = free == 0 ? "the 0 free cores" : $"the {free} free {(free == 1 ? "core" : "cores")} (logical {CoreMask.List(slot.Free)})";
            return $"{how} more than {cores}: {slot.Explain()}";
        }

        /// <summary>A count of threads, in the singular where it is one.</summary>
        public static string Threads(int n) => n == 1 ? "1 thread" : n.ToString(CultureInfo.InvariantCulture) + " threads";
    }

    /// <summary>One run to start: the program, its arguments, where it runs, the folder it writes and the file its console output goes to.</summary>
    public sealed class LaunchSpec
    {
        public string FileName;

        /// <summary>The arguments after the program. The first is the program's own file (the nursery's dll) and is checked as a path; the rest are options and values.</summary>
        public IReadOnlyList<string> Arguments;
        public string WorkingDirectory;
        /// <summary>The run's output folder, as the program is told it (<c>--out</c>).</summary>
        public string Folder;
        /// <summary>Where the program's console output is written, outside <see cref="Folder"/>: the nursery refuses a folder that is not empty.</summary>
        public string LogFile;

        /// <summary>
        /// The logical processors the run may use, a bit each (bit 0 is processor 0), set on the process before its first instruction so that the
        /// <c>cmd</c> and the <c>dotnet</c> it starts inherit it. The room always sets it (the free cores of its slot, <see cref="Slot.Free"/>, never 0);
        /// 0 here means the caller asked for no pinning, which only the launcher's own tests do.
        /// </summary>
        public ulong Affinity;

        /// <summary>
        /// True for an episode re-step (<c>--restep</c>): the process is also started without every <c>DOTNET_*</c> variable and without
        /// <c>EVOSIM_TANK_TRACE</c>, since the nursery's numbers are those of a build with its own JIT settings (the nursery turns tiering off), and
        /// a variable of the room's session that changes the JIT, the garbage collector or the trace would be a different run. The names dropped are reported.
        /// </summary>
        public bool CleanForRestep;
    }

    /// <summary>
    /// A mask of logical processors. room.json's <c>affinity</c> is one, the bound a launched run never leaves: a hex string such as <c>0xFFFFFFCF</c>,
    /// up to 64 bits, refused when it is 0 or names a processor the machine lacks, and never trimmed to fit. The machine's queue writes its cores as a list.
    /// </summary>
    public static class CoreMask
    {
        private static readonly Regex Hex = new Regex(@"\A0[xX][0-9A-Fa-f]{1,16}\z");

        /// <summary>The mask a string names, or a thrown refusal that says why, naming the settings file <paramref name="where"/>. 0 is refused.</summary>
        public static ulong Parse(string text, string key = "affinity", string where = "room.json")
        {
            if (text == null || !Hex.IsMatch(text))
                throw new InvalidDataException($"room settings: {where}: {key} is a hex string of the logical processors a run may use, such as \"0xFFFF0000\" (0x then 1 to 16 hex digits), not \"{(text ?? "").Replace("\"", "'")}\"");
            ulong mask = ulong.Parse(text.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
            if (mask == 0) throw new InvalidDataException($"room settings: {where}: {key} is 0, which names no processor: a run pinned to none could not run");
            return mask;
        }

        /// <summary>How many processors a mask names.</summary>
        public static int Count(ulong mask) => System.Numerics.BitOperations.PopCount(mask);

        /// <summary>The mask as <see cref="Parse"/> reads it: <c>0x</c> and upper-case hex digits, with no leading zeros.</summary>
        public static string Format(ulong mask) => "0x" + mask.ToString("X", CultureInfo.InvariantCulture);

        private static readonly Regex Range = new Regex(@"\A(\d{1,2})(?:-(\d{1,2}))?\z");

        /// <summary>
        /// A list of logical processors as the machine's queue writes them, <c>16-31</c> or <c>0-3,8-11</c> (a single one is <c>a</c>; spaces are
        /// ignored), read into a mask. False when a part is not a range from 0 to 63 whose first number is not above its last, or the list is empty.
        /// </summary>
        public static bool TryParseList(string text, out ulong mask)
        {
            mask = 0;
            string plain = new string((text ?? "").Where(c => !char.IsWhiteSpace(c)).ToArray());
            if (plain.Length == 0) return false;
            foreach (string part in plain.Split(','))
            {
                Match m = Range.Match(part);
                if (!m.Success) return false;
                int a = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                int b = m.Groups[2].Success ? int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) : a;
                if (a > b || b > 63) return false;
                for (int i = a; i <= b; i++) mask |= 1UL << i;
            }
            return true;
        }

        /// <summary>A mask as the queue writes it: ranges joined by commas (<c>0-3,8-11</c>), <c>none</c> for 0.</summary>
        public static string List(ulong mask)
        {
            var parts = new List<string>();
            for (int i = 0; i < 64; i++)
            {
                if ((mask >> i & 1UL) == 0) continue;
                int j = i;
                while (j < 63 && (mask >> (j + 1) & 1UL) != 0) j++;
                parts.Add(j == i ? i.ToString(CultureInfo.InvariantCulture) : $"{i}-{j}");
                i = j;
            }
            return parts.Count == 0 ? "none" : string.Join(",", parts);
        }

        /// <summary>
        /// Null when every processor the mask names is one the system has (<paramref name="systemMask"/>, <see cref="SystemProcessors"/>: the
        /// processors the machine has, whatever this process is itself pinned to), or the refusal, which names the processors lacking.
        /// </summary>
        public static string Problem(ulong mask, ulong systemMask)
        {
            if (mask == 0) return "the affinity mask is 0, which names no processor";
            if (systemMask == 0) return "the machine's processor mask is unknown, so the affinity mask cannot be checked";
            ulong lacking = mask & ~systemMask;
            return lacking == 0 ? null : $"the affinity mask {Format(mask)} names processor(s) {string.Join(", ", Enumerable.Range(0, 64).Where(i => (lacking >> i & 1UL) != 0))}, which this machine lacks (its processors are {Format(systemMask)}, {Count(systemMask)} logical); nothing was started";
        }

        /// <summary>
        /// Null when a run of <paramref name="threads"/> threads fits on the processors the mask allows, or the refusal with both numbers: more
        /// threads than processors would queue behind each other on the cores the mask names.
        /// </summary>
        public static string ThreadsProblem(int threads, ulong mask)
        {
            int allowed = Count(mask);
            return threads <= allowed ? null
                : $"{Cap.Threads(threads)} asked, but the affinity mask {Format(mask)} allows {allowed} processors, so the run would queue behind itself; ask for {allowed} or fewer. Nothing was started";
        }
    }

    /// <summary>
    /// The processors the machine has, read once from the system, <c>GetProcessAffinityMask</c>'s second answer: the mask of every processor, which
    /// does not change when this process is itself pinned (a room started from a pinned shell still sees all of them). It is the one source of the
    /// room's logical processor count and of what an affinity mask is checked against, in the operations and in the launcher.
    /// </summary>
    public static class SystemProcessors
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetProcessAffinityMask(IntPtr process, out UIntPtr processMask, out UIntPtr systemMask);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        /// <summary>The system's mask, or a thrown failure that names what Windows said. Never 0.</summary>
        public static ulong Read()
        {
            if (!OperatingSystem.IsWindows())
                return Environment.ProcessorCount >= 64 ? ulong.MaxValue : (1UL << Environment.ProcessorCount) - 1UL;
            if (!GetProcessAffinityMask(GetCurrentProcess(), out UIntPtr process, out UIntPtr system))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "the system's processor mask could not be read (GetProcessAffinityMask)");
            ulong mask = (ulong)system;
            if (mask == 0) throw new InvalidOperationException("GetProcessAffinityMask answered a system mask of 0");
            return mask;
        }
    }

    /// <summary>What starting a run left behind: the process id the launcher knows, and whether the run was started outside the room's own job object.</summary>
    public sealed class LaunchOutcome
    {
        public int Pid;

        /// <summary>The mask the run was started pinned to (0: not pinned); the launch result and its activity row record it.</summary>
        public ulong Affinity;

        /// <summary>True when the run does not end with the room (it is outside the room's job object); false when the launcher could not break away.</summary>
        public bool Detached = true;

        /// <summary>Said in the operation's result and its activity row when something about the start is not as asked for.</summary>
        public string Note;

        /// <summary>The names (never the values) of the environment variables the run was started without, so that the row says what the run did not inherit.</summary>
        public IReadOnlyList<string> DroppedEnvironment = new string[0];
    }

    /// <summary>
    /// What a launched run does not inherit from the room's environment: <c>EVOSIM_REPO_ROOT</c> and every <c>EVOSIM_GPU_*</c> variable.
    /// The room may have been started by a session that set them for its own work; a run started on a recipe must take its repository
    /// from where it is run and its device from its own settings, never from a variable the caller's session happened to hold.
    /// </summary>
    public static class LaunchEnvironment
    {
        public static bool IsDropped(string name) => IsDropped(name, restep: false);

        /// <summary>
        /// With <paramref name="restep"/> an episode re-step's own list too: every <c>DOTNET_*</c> variable (the JIT's and the runtime's settings) and
        /// <c>EVOSIM_TANK_TRACE</c>.
        /// </summary>
        public static bool IsDropped(string name, bool restep) =>
            string.Equals(name, "EVOSIM_REPO_ROOT", StringComparison.OrdinalIgnoreCase) || name.StartsWith("EVOSIM_GPU_", StringComparison.OrdinalIgnoreCase)
            || (restep && (name.StartsWith("DOTNET_", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "EVOSIM_TANK_TRACE", StringComparison.OrdinalIgnoreCase)));

        /// <summary>The variables to pass and the names dropped, from the variables of this process (names compared without case, as Windows does).</summary>
        public static void Split(System.Collections.IDictionary variables, out SortedDictionary<string, string> kept, out List<string> dropped, bool restep = false)
        {
            kept = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            dropped = new List<string>();
            foreach (System.Collections.DictionaryEntry e in variables)
            {
                string name = (string)e.Key;
                if (IsDropped(name, restep)) dropped.Add(name); else kept[name] = (string)e.Value ?? "";
            }
            dropped.Sort(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>The environment block CreateProcess takes with CREATE_UNICODE_ENVIRONMENT: NAME=value, a NUL after each, and a final NUL.</summary>
        public static string Block(SortedDictionary<string, string> kept)
        {
            var sb = new StringBuilder();
            foreach (var kv in kept) sb.Append(kv.Key).Append('=').Append(kv.Value).Append('\0');
            sb.Append('\0');
            return sb.ToString();
        }
    }

    /// <summary>Starts a run as a process that goes on without the room. The room's tests give it a fake.</summary>
    public interface IRunLauncher
    {
        /// <summary>Starts the run and returns the process id the launcher knows (a wrapper's, not necessarily the nursery's own; run.json holds that).</summary>
        LaunchOutcome Start(LaunchSpec spec);
    }

    /// <summary>
    /// What may stand in a command line the room builds for <c>cmd.exe</c>. Every argument is checked against a short list of
    /// characters and refused when it holds another, never cleaned: a name stripped of what cmd reads as syntax is a different
    /// name, and the run would be started under it without anyone having asked.
    /// </summary>
    public static class CommandLine
    {
        /// <summary>An argument: letters, digits and . _ / \ : - only, and not ending in a backslash (which, before the closing quote, would escape it).</summary>
        public static readonly Regex Argument = new Regex(@"\A[A-Za-z0-9._/\\:-]+(?<!\\)\z");

        /// <summary>A program or file path from the room's own settings or tree: the same, and a space (a quoted path may hold one; the dotnet host lives under Program Files). It does not end in a backslash either.</summary>
        public static readonly Regex PathText = new Regex(@"\A[A-Za-z0-9._/\\: -]+(?<!\\)\z");

        /// <summary>Null when every part of the command is plain, or the first part that is not. Arguments[0] is the program's file and is checked as a path.</summary>
        public static string Problem(LaunchSpec spec)
        {
            if (string.IsNullOrEmpty(spec.FileName) || !PathText.IsMatch(spec.FileName))
                return $"the program \"{Show(spec.FileName)}\" holds a character outside A-Z a-z 0-9 . _ / \\ : - and space, or ends in a backslash, which the command line does not take; nothing was started";
            if (string.IsNullOrEmpty(spec.LogFile) || !PathText.IsMatch(spec.LogFile))
                return $"the log file \"{Show(spec.LogFile)}\" holds a character outside A-Z a-z 0-9 . _ / \\ : - and space, or ends in a backslash, which the command line does not take; nothing was started";
            for (int i = 0; i < spec.Arguments.Count; i++)
            {
                string a = spec.Arguments[i];
                if (a == null || !(i == 0 ? PathText : Argument).IsMatch(a))
                    return $"argument {i + 1} \"{Show(a)}\" holds a character outside A-Z a-z 0-9 . _ / \\ : -{(i == 0 ? " and space" : "")}, or ends in a backslash, which the command line does not take; nothing was started";
            }
            return null;
        }

        private static string Show(string s) => s == null ? "" : s.Length > 80 ? s.Substring(0, 80) + "..." : s;
    }

    /// <summary>
    /// Runs the program under <c>cmd.exe</c>, which redirects its console output to the log file and its input from NUL, so that
    /// nothing written to the console depends on a handle of the room's. The process is created outside the room's job object
    /// (<c>CREATE_BREAKAWAY_FROM_JOB</c>), with no inherited handle and no window, so that a run goes on when the room, or the
    /// session that started it, ends. The room does not wait for it and does not kill it.
    /// </summary>
    public sealed class ProcessRunLauncher : IRunLauncher
    {
        private readonly Func<ulong> _systemMask;

        /// <param name="systemMask">The processors the machine has; the default is <see cref="SystemProcessors.Read"/>, the one the room's operations check against too.</param>
        public ProcessRunLauncher(Func<ulong> systemMask = null) { _systemMask = systemMask ?? SystemProcessors.Read; }

        private const uint CreateNoWindow = 0x08000000, CreateBreakawayFromJob = 0x01000000, CreateUnicodeEnvironment = 0x00000400, CreateSuspended = 0x00000004;
        private const int ErrorAccessDenied = 5;

        /// <summary>Wraps an argument in quotes. An argument holding a quote is refused: stripping it would start a run under another name.</summary>
        public static string Quote(string s)
        {
            if (s.Contains('"')) throw new ArgumentException("a command line argument holds a quotation mark, which is refused and never removed");
            return "\"" + s + "\"";
        }

        /// <summary>The command line cmd.exe is given after /c: the outer quotes are stripped by /s.</summary>
        public static string CmdLine(LaunchSpec spec) =>
            "\"" + Quote(spec.FileName) + " " + string.Join(" ", spec.Arguments.Select(Quote)) + " > " + Quote(spec.LogFile) + " 2>&1 < NUL\"";

        /// <summary>The whole line the process is created with. /d takes no AutoRun, /s keeps the quotes, /v:off takes no delayed expansion (a ! is text).</summary>
        public static string CommandLineFor(string cmdExe, LaunchSpec spec) => "\"" + cmdExe + "\" /d /s /v:off /c " + CmdLine(spec);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct StartupInfo
        {
            public int cb;
            public string lpReserved, lpDesktop, lpTitle;
            public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
            public short wShowWindow, cbReserved2;
            public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessInformation
        {
            public IntPtr hProcess, hThread;
            public int dwProcessId, dwThreadId;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CreateProcessW")]
        private static extern bool CreateProcess(string applicationName, StringBuilder commandLine, IntPtr processAttributes, IntPtr threadAttributes,
            bool inheritHandles, uint creationFlags, IntPtr environment, string currentDirectory, ref StartupInfo startupInfo, out ProcessInformation processInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessAffinityMask(IntPtr process, UIntPtr mask);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint ResumeThread(IntPtr thread);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateProcess(IntPtr process, uint exitCode);

        public LaunchOutcome Start(LaunchSpec spec)
        {
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("the room launches runs on Windows only");
            string problem = CommandLine.Problem(spec);
            if (problem != null) throw new ArgumentException(problem);
            // The mask is checked and converted before any process is made, so that a mask that cannot be applied never leaves a suspended one.
            UIntPtr maskPointer = UIntPtr.Zero;
            if (spec.Affinity != 0)
            {
                string lacking = CoreMask.Problem(spec.Affinity, _systemMask());
                if (lacking != null) throw new ArgumentException(lacking);
                try { maskPointer = checked((UIntPtr)spec.Affinity); }
                catch (OverflowException) { throw new ArgumentException($"the affinity mask {CoreMask.Format(spec.Affinity)} does not fit this process's pointer size, so it cannot be applied; nothing was started"); }
            }
            string cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            string line = CommandLineFor(cmd, spec);
            var outcome = new LaunchOutcome();
            LaunchEnvironment.Split(Environment.GetEnvironmentVariables(), out SortedDictionary<string, string> kept, out List<string> dropped, spec.CleanForRestep);
            string block = LaunchEnvironment.Block(kept);
            outcome.DroppedEnvironment = dropped;
            if (!Create(cmd, line, spec.WorkingDirectory, block, spec.Affinity, maskPointer, breakaway: true, out int pid, out int error))
            {
                if (error != ErrorAccessDenied) throw new System.ComponentModel.Win32Exception(error);
                // The room's job object does not allow a process to leave it. Start inside it, and say so.
                if (!Create(cmd, line, spec.WorkingDirectory, block, spec.Affinity, maskPointer, breakaway: false, out pid, out error)) throw new System.ComponentModel.Win32Exception(error);
                outcome.Detached = false;
                outcome.Note = "the run could not be started outside the room's job object (breakaway was refused: access denied), so it was started inside it and ends if that job is closed with the room";
            }
            outcome.Pid = pid;
            outcome.Affinity = spec.Affinity;
            return outcome;
        }

        /// <summary>
        /// Creates the process. With an affinity it is created suspended, the mask set on it, and only then is its first thread resumed, so
        /// that the <c>cmd</c> and the <c>dotnet</c> it starts inherit the mask from their first instruction. If the mask cannot be set the
        /// process (this launch's own, never started) is ended and the failure thrown: a run is never left to go on unpinned.
        /// </summary>
        private static bool Create(string cmd, string line, string workingDirectory, string environmentBlock, ulong affinity, UIntPtr affinityPointer, bool breakaway, out int pid, out int error)
        {
            var si = new StartupInfo { cb = Marshal.SizeOf<StartupInfo>() };
            uint flags = CreateNoWindow | CreateUnicodeEnvironment | (breakaway ? CreateBreakawayFromJob : 0u) | (affinity != 0 ? CreateSuspended : 0u);
            IntPtr env = Marshal.StringToHGlobalUni(environmentBlock);
            try
            {
                // No handle is inherited (bInheritHandles false): cmd's own redirections give the run its output and input.
                bool ok = CreateProcess(cmd, new StringBuilder(line), IntPtr.Zero, IntPtr.Zero, false, flags, env, workingDirectory, ref si, out ProcessInformation pi);
                error = ok ? 0 : Marshal.GetLastWin32Error();
                pid = ok ? pi.dwProcessId : 0;
                if (ok && affinity != 0)
                {
                    string failed = null;
                    if (!SetProcessAffinityMask(pi.hProcess, affinityPointer)) failed = $"the affinity mask {CoreMask.Format(affinity)} could not be set on the new process";
                    else if (ResumeThread(pi.hThread) == uint.MaxValue) failed = "the new process's thread could not be resumed after its affinity was set";
                    if (failed != null)
                    {
                        int why = Marshal.GetLastWin32Error();
                        // The process this call just made, which has not run an instruction. If it cannot be ended it stays suspended, and the
                        // failure says so with its id, so that it is not left unnoticed.
                        if (!TerminateProcess(pi.hProcess, 1))
                            failed += $"; the new process (id {pi.dwProcessId}) could not be ended either (Windows error {Marshal.GetLastWin32Error()}), so it is still there, suspended";
                        CloseHandle(pi.hThread); CloseHandle(pi.hProcess);
                        throw new System.ComponentModel.Win32Exception(why, failed);
                    }
                }
                if (ok) { CloseHandle(pi.hThread); CloseHandle(pi.hProcess); }
                return ok;
            }
            finally { Marshal.FreeHGlobal(env); }
        }
    }
}
