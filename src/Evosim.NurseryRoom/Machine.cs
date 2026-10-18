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
    /// <summary>The machine's load as <c>% Processor Utility</c>, as Task Manager reports it, or why it could not be read.</summary>
    public sealed class LoadReading
    {
        /// <summary>0 to 100 (a counter can read above 100 on a machine in turbo; it is kept as read), or null.</summary>
        public double? Percent;
        public string Problem;
        public DateTime AtUtc = DateTime.UtcNow;

        public static LoadReading Of(double percent) => new LoadReading { Percent = percent };
        public static LoadReading Failed(string problem) => new LoadReading { Problem = problem };
    }

    /// <summary>Where the load comes from; the room's tests give it a value.</summary>
    public interface ILoadReader
    {
        LoadReading Read();

        /// <summary>Forgets a reading kept for a few seconds: a launch has just changed the load, and the next reading must see it.</summary>
        void Invalidate();
    }

    /// <summary>
    /// Reads <c>\Processor Information(_Total)\% Processor Utility</c> with <c>typeperf</c>, which ships with
    /// Windows: the counter is a rate, so two samples a second apart are taken and the second is the reading.
    /// A reading is kept for a few seconds so that a screen's poll and an agent's call do not each wait a second.
    /// </summary>
    public sealed class TypeperfLoadReader : ILoadReader
    {
        public const string Counter = "\\Processor Information(_Total)\\% Processor Utility";

        private readonly object _gate = new object();
        private LoadReading _last;
        private readonly TimeSpan _keep;

        public TypeperfLoadReader(TimeSpan? keep = null) { _keep = keep ?? TimeSpan.FromSeconds(5); }

        public LoadReading Read()
        {
            lock (_gate)
            {
                if (_last != null && DateTime.UtcNow - _last.AtUtc < _keep) return _last;
                _last = ReadNow();
                return _last;
            }
        }

        public void Invalidate()
        {
            lock (_gate) _last = null;
        }

        private static LoadReading ReadNow()
        {
            if (!OperatingSystem.IsWindows()) return LoadReading.Failed("the load counter is read on Windows only");
            try
            {
                var info = new ProcessStartInfo("typeperf")
                {
                    RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
                };
                info.ArgumentList.Add(Counter);
                info.ArgumentList.Add("-sc");
                info.ArgumentList.Add("2");
                using (Process p = Process.Start(info))
                {
                    var output = p.StandardOutput.ReadToEndAsync();
                    var error = p.StandardError.ReadToEndAsync();
                    if (!p.WaitForExit(15000))
                    {
                        try { p.Kill(); } catch (Exception) { }
                        return LoadReading.Failed("typeperf did not answer within 15 s");
                    }
                    p.WaitForExit();
                    double? v = Parse(output.Result);
                    if (v.HasValue) return LoadReading.Of(v.Value);
                    string said = (error.Result + " " + output.Result).Trim();
                    return LoadReading.Failed("typeperf gave no reading of " + Counter + (said.Length == 0 ? "" : ": " + (said.Length > 160 ? said.Substring(0, 160) : said)));
                }
            }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception || e is InvalidOperationException || e is IOException)
            {
                return LoadReading.Failed("typeperf could not be run: " + e.Message);
            }
        }

        /// <summary>
        /// The last sample of typeperf's CSV: rows of quoted fields, a time and then the value. The first sample of a rate
        /// counter is not a rate, so with only one sample the answer is null. A comma used as the decimal mark is taken as one.
        /// </summary>
        public static double? Parse(string csv)
        {
            int rows = 0;
            double? last = null;
            foreach (string line in (csv ?? "").Split('\n'))
            {
                MatchCollection fields = Regex.Matches(line, "\"([^\"]*)\"");
                if (fields.Count < 2 || line.Contains("PDH-CSV")) continue;
                rows++;
                string v = fields[fields.Count - 1].Groups[1].Value.Trim().Replace(',', '.');
                last = double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) && double.IsFinite(d) ? d : (double?)null;
            }
            return rows >= 2 ? last : null;
        }
    }

    /// <summary>The cap: the threads a launch asks for, plus the logical processors already in use, may not pass it.</summary>
    public static class Cap
    {
        /// <summary>How long a launch counts as using its threads whatever the counter says: the counter is a rate, and a run takes a while to show in it.</summary>
        public static readonly TimeSpan RecentLaunch = TimeSpan.FromMinutes(3);

        /// <summary>The logical processors a load reading says are in use, as a share of <paramref name="logical"/>, rounded up.</summary>
        public static int InUse(double percent, int logical) =>
            (int)Math.Ceiling(Math.Clamp(percent, 0d, 100d) / 100d * logical - 1e-9);

        /// <summary>
        /// The logical processors in use for the cap: the larger of what the counter reads and what the room itself granted
        /// (<paramref name="grantedThreads"/>: the threads of runs it started that are still running or began in the last
        /// <see cref="RecentLaunch"/>), so that a run just launched is counted before the counter has seen it, and never twice.
        /// </summary>
        public static int InUse(double percent, int logical, int grantedThreads) => Math.Max(InUse(percent, logical), grantedThreads);

        /// <summary>
        /// A reading that cannot be a load: not a number, below 0, or above 100 times the logical count. A counter can read
        /// somewhat above 100 on a machine in turbo, which is a reading; the garbage a broken counter returns is not.
        /// </summary>
        public static bool Plausible(double percent, int logical) =>
            !double.IsNaN(percent) && !double.IsInfinity(percent) && percent >= 0 && percent <= 100.0 * Math.Max(1, logical);

        /// <summary>Why a reading cannot be used for the cap (none, or not a possible load), or null when it can; the screens show a reading the cap would refuse as unreadable.</summary>
        public static string Unreadable(LoadReading load, int logical)
        {
            if (load == null || !load.Percent.HasValue) return load?.Problem ?? "no reading";
            double p = load.Percent.Value;
            if (Plausible(p, logical)) return null;
            string shown = double.IsFinite(p) ? p.ToString("0.#", CultureInfo.InvariantCulture) : p.ToString(CultureInfo.InvariantCulture);
            return $"the counter read {shown} % Processor Utility, which is not a possible reading on {logical} logical processors";
        }

        /// <summary>Null when the launch fits, or the refusal with its numbers.</summary>
        public static string Refusal(int threads, int cap, int logical, LoadReading load, int grantedThreads = 0)
        {
            if (load == null || !load.Percent.HasValue)
                return $"the machine's load cannot be read ({load?.Problem ?? "no reading"}), so the cap of {cap} of {logical} logical processors cannot be checked; refusing {Threads(threads)}";
            double percent = load.Percent.Value;
            string shown = double.IsFinite(percent) ? percent.ToString("0.#", CultureInfo.InvariantCulture) : percent.ToString(CultureInfo.InvariantCulture);
            if (!Plausible(percent, logical))
                return $"the machine's load reads {shown} % Processor Utility, which is not a possible reading on {logical} logical processors (it is below 0 or above {100 * logical}), so the cap of {cap} cannot be checked; refusing {Threads(threads)}";
            int read = InUse(percent, logical);
            int used = Math.Max(read, grantedThreads);
            if (threads + used <= cap) return null;
            // The load is said as Task Manager says it, never above 100 (the counter can pass it under boost); the cap rule above reads the raw value.
            string said = percent > 100 ? $"100 % Processor Utility, the counter read {shown}" : $"{shown} % Processor Utility";
            string how = grantedThreads > read
                ? $"{Threads(grantedThreads)} granted to runs this room started that are still running or began in the last {(int)RecentLaunch.TotalMinutes} minutes, and to episode re-steps whose process lives (the counter reads {read}, {said}, and the larger counts)"
                : $"{read} of {logical} logical processors in use ({said}, rounded up)";
            return $"{Threads(threads)} asked, plus {how} is {threads + used}, past the cap of {cap} of {logical} logical processors";
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
        /// <c>cmd</c> and the <c>dotnet</c> it starts inherit it. The room always sets it (room.json's <c>affinity</c>, never 0); 0 here means the
        /// caller asked for no pinning, which only the launcher's own tests do.
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
    /// The mask of logical processors a launched run may use (room.json's <c>affinity</c>): a hex string such as <c>0xFFFF0000</c>, up to 64 bits.
    /// Refused when it is 0, or names a processor the machine lacks; never trimmed to fit.
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
        /// threads than processors would queue behind each other on the cores the mask names, and the cap counts them as if they ran.
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
