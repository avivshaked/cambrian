using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;

// Prints one line per argument ("arg N: value"), then one per EVOSIM_ variable it holds ("env NAME"), then its own process affinity
// ("affinity: 0xMASK", the logical processors it may use, as the launcher's pinning left it), then the working directory.
for (int i = 0; i < args.Length; i++) Console.WriteLine("arg " + (i + 1) + ": " + args[i]);
foreach (string name in Environment.GetEnvironmentVariables().Keys.Cast<string>().Where(k => k.StartsWith("EVOSIM_", StringComparison.OrdinalIgnoreCase)).OrderBy(k => k, StringComparer.Ordinal))
    Console.WriteLine("env " + name);
using (Process self = Process.GetCurrentProcess())
    Console.WriteLine("affinity: 0x" + ((ulong)(long)self.ProcessorAffinity).ToString("X"));
Console.WriteLine("cwd: " + Environment.CurrentDirectory);
return 0;
