using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;

// A sampling profile's reader: every Thread/Sample event of the .NET SampleProfiler, its stack
// resolved through the run's own rundown; prints exclusive and inclusive shares by method, over
// the samples whose stack passes through a frame matching the filter (Evosim by default), so the
// thread pool's idle waits are left out.
string path = args[0];
string filter = args.Length > 1 ? args[1] : "Evosim";
int top = args.Length > 2 ? int.Parse(args[2]) : 60;
string etlx = TraceLog.CreateFromEventPipeDataFile(path);
using var log = new TraceLog(etlx);
var excl = new Dictionary<string, long>();
var incl = new Dictionary<string, long>();
var paths = new Dictionary<string, long>();
long kept = 0, all = 0;
foreach (TraceEvent ev in log.Events)
{
    if (ev.ProviderName != "Microsoft-DotNETCore-SampleProfiler") continue;
    all++;
    var stack = ev.CallStack();
    if (stack == null) continue;
    var frames = new List<string>();
    for (var s = stack; s != null; s = s.Caller)
    {
        var m = s.CodeAddress.Method;
        string name = m != null ? m.FullMethodName : (s.CodeAddress.ModuleName ?? "?") + "!?";
        frames.Add(name);
    }
    if (!frames.Any(f => f.Contains(filter))) continue;
    kept++;
    string leaf = frames[0];
    excl[leaf] = excl.GetValueOrDefault(leaf) + 1;
    foreach (var f in frames.Distinct()) incl[f] = incl.GetValueOrDefault(f) + 1;
    // the innermost Evosim frame and its Evosim caller, for a two-level view
    var ev2 = frames.Where(f => f.Contains(filter)).Take(2).ToList();
    string key = string.Join("  <-  ", ev2);
    paths[key] = paths.GetValueOrDefault(key) + 1;
}
Console.WriteLine($"samples {all}, through '{filter}' {kept}");
Console.WriteLine("== exclusive (leaf frame)");
foreach (var kv in excl.OrderByDescending(k => k.Value).Take(top)) Console.WriteLine($"{100.0 * kv.Value / kept,6:0.0}%  {kv.Key}");
Console.WriteLine("== inclusive (" + filter + " frames)");
foreach (var kv in incl.Where(k => k.Key.Contains(filter)).OrderByDescending(k => k.Value).Take(top)) Console.WriteLine($"{100.0 * kv.Value / kept,6:0.0}%  {kv.Key}");
Console.WriteLine("== innermost two " + filter + " frames");
foreach (var kv in paths.OrderByDescending(k => k.Value).Take(top)) Console.WriteLine($"{100.0 * kv.Value / kept,6:0.0}%  {kv.Key}");