using System;
using System.IO;
using System.Linq;
using Evosim.Core;
using Evosim.Nursery;
using Xunit;

namespace Evosim.Nursery.Tests
{
    /// <summary>Where the restep tests find the repository, and the small files they write beside the test program (under artifacts/, which is inside the repository).</summary>
    internal static class RestepFixtures
    {
        public static readonly string Root = Recipe.RepoRootAbove(AppContext.BaseDirectory);
        public static readonly string Brain2 = Path.Combine(Root, "rounds", "nursery", "brain2-w8.json");

        /// <summary>The bodies of the pilot's six exam genomes, in pool order.</summary>
        public static readonly int[] PilotBodies = { 0, 1, 3, 4, 8, 10 };

        /// <summary>The pilot's exam, draw 0: its episode seed, and the tank of all six in slot order (pool indices): the room's pinned values of the formula.</summary>
        public const string PilotSeed0 = "4691847893231451394";
        public static readonly int[] PilotTank0 = { 1, 2, 0, 5, 3, 4 };

        public static string Relative(string full) => Path.GetRelativePath(Root, full).Replace("\\", "/");

        public static string NewDir(string stem)
        {
            string dir = Path.Combine(AppContext.BaseDirectory, stem + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        // ------------------------------------------------------------------ synthetic genomes: a root and two hinges, each with a one brain

        private static MorphNode Box(float half, JointType joint)
        {
            var node = new MorphNode
            {
                Dimensions = new Float3(half, half, half),
                JointType = joint,
                RecursiveLimit = 1,
                CellTypeId = joint == JointType.Fixed ? CellTypeIds.Structural : CellTypeIds.Link,
                Power = joint == JointType.Fixed ? 0f : 50f,
            };
            node.ResampleJointLimits(new Float2(-1f, 1f));
            return node;
        }

        private static readonly SensorChannel[] Ports = { SensorChannel.JointAngle, SensorChannel.Depth, SensorChannel.Chemical };

        public static Genome Founder(ulong seed)
        {
            var g = new Genome { RootIndex = 0 };
            g.Nodes.Add(Box(0.5f, JointType.Fixed));
            g.Nodes.Add(Box(0.3f, JointType.Hinge));
            g.Nodes.Add(Box(0.3f, JointType.Hinge));
            g.Nodes[0].Edges.Add(new MorphEdge { Child = 1, ParentAnchor = new Float3(1f, 0f, 0f), ChildAnchor = new Float3(-1f, 0f, 0f) });
            g.Nodes[0].Edges.Add(new MorphEdge { Child = 2, ParentAnchor = new Float3(-1f, 0f, 0f), ChildAnchor = new Float3(1f, 0f, 0f) });
            CentralWiring.Convert(g);
            g.Brain = CentralWiring.Effective(g.Brain, Developer.Develop(g), new Rng(seed), Ports, new CentralBrainOptions(), founder: true);
            return g;
        }

        /// <summary>A pool file of six synthetic genomes in the pilot's body order: a body index, a tab, a genome a line. Returns its repository-relative path.</summary>
        public static string WritePool(string dir)
        {
            string path = Path.Combine(dir, "pool.tsv");
            File.WriteAllLines(path, PilotBodies.Select((b, i) => b.ToString() + "\t" + GenomeJson.Write(Founder((ulong)i + 1))));
            return Relative(path);
        }

        public static string RecipeHashOfBrain2() => Recipe.Load(Brain2).Hash();

        /// <summary>A request as JSON text. <paramref name="extra"/> is members of the object written after the others, with its own leading comma.</summary>
        public static string RequestJson(string recipe, string recipeHash, string pool, int[] members, int brain = 0, string extra = "",
            string seed = "0x1", string salt = "0x62656e6368", int generation = 0, int episode = 0, int tank = 0) =>
            "{\"recipe\": \"" + recipe + "\", \"recipeHash\": \"" + recipeHash + "\", \"pool\": \"" + pool + "\", \"seed\": \"" + seed + "\", \"salt\": \"" + salt +
            "\", \"generation\": " + generation + ", \"episode\": " + episode + ", \"brain\": " + brain + ", \"tank\": " + tank + ", \"members\": [" + string.Join(", ", members) + "]" + extra + "}";

        /// <summary>
        /// The members of tank <paramref name="tank"/> of exam episode <paramref name="episode"/> over <paramref name="pool"/> genomes in tanks of
        /// <paramref name="tankSize"/>, by the nursery's own deal and the pilot's seed and salt.
        /// </summary>
        public static int[] Deal(int pool, int tankSize, int episode, int tank) =>
            TankLog.PlanTanks(pool, tankSize, new[] { Rng.SeedFor(1UL ^ 0x62656E6368UL, (ulong)episode) })[0][tank];

        /// <summary>The pilot's draw 0 under a control, as a request on <paramref name="pool"/>.</summary>
        public static string PilotRequest(string pool, int brain = 0, string extra = "") =>
            RequestJson("rounds/nursery/brain2-w8.json", RecipeHashOfBrain2(), pool, PilotTank0, brain, extra);

        public static string WriteRequest(string dir, string json, string name = "request.json")
        {
            string path = Path.Combine(dir, name);
            File.WriteAllText(path, json);
            return path;
        }

        /// <summary>Runs <c>--restep</c> with these arguments in the repository; returns the exit code and what it said.</summary>
        public static (int code, string stdout, string stderr) Run(params string[] args)
        {
            var o = new StringWriter();
            var e = new StringWriter();
            int code = Restep.Run(args, Root, o, e);
            return (code, o.ToString(), e.ToString());
        }

        /// <summary>The folder (the repository, or any folder above it) that holds <paramref name="relative"/>, or null.</summary>
        public static string FindAbove(string relative)
        {
            for (string d = Root; d != null; d = Path.GetDirectoryName(d))
                if (File.Exists(Path.Combine(d, relative.Replace('/', Path.DirectorySeparatorChar)))) return d;
            return null;
        }

        /// <summary>
        /// The main checkout's registry file: the first <c>rounds/nursery/registry.jsonl</c> in a folder <b>above</b> this tree's root (a worktree lies under
        /// the main checkout's scratch/), or null. Never this tree's own copy of the file.
        /// </summary>
        public static string MainRegistry()
        {
            string own = Path.GetFullPath(Root).TrimEnd(Path.DirectorySeparatorChar);
            for (string d = Path.GetDirectoryName(own); d != null; d = Path.GetDirectoryName(d))
            {
                string file = Path.Combine(d, "rounds", "nursery", "registry.jsonl");
                if (File.Exists(file)) return file;
            }
            return null;
        }

        /// <summary>
        /// Makes <paramref name="link"/> a link to the folder <paramref name="target"/>: a symbolic link where the system lets this user make one, and
        /// otherwise a junction (Windows, which needs no privilege). Fails the test loudly if neither can be made, since the tests that use it are
        /// about what a link does.
        /// </summary>
        public static void MakeFolderLink(string link, string target)
        {
            try { Directory.CreateSymbolicLink(link, target); return; }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { /* not allowed to: try a junction */ }
            if (OperatingSystem.IsWindows())
            {
                var info = new System.Diagnostics.ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                info.ArgumentList.Add("/c");
                info.ArgumentList.Add("mklink");
                info.ArgumentList.Add("/J");
                info.ArgumentList.Add(link);
                info.ArgumentList.Add(target);
                using (var p = System.Diagnostics.Process.Start(info))
                {
                    p.StandardOutput.ReadToEnd();
                    p.StandardError.ReadToEnd();
                    p.WaitForExit();
                }
            }
            Assert.True(Directory.Exists(link) && (File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0, "a link to " + target + " could not be made at " + link);
        }

        /// <summary>Removes a link made by <see cref="MakeFolderLink"/>, and only the link: the folder it points to is left.</summary>
        public static void RemoveFolderLink(string link)
        {
            if (Directory.Exists(link) && (File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0) Directory.Delete(link, false);
        }
    }

    /// <summary>A fact that reads a trial file the repository does not track (scratch/ is gitignored). Skipped, with the file named, where it is absent; never silent.</summary>
    public sealed class PresentFactAttribute : FactAttribute
    {
        public PresentFactAttribute(params string[] relativeFiles)
        {
            foreach (string f in relativeFiles)
                if (RestepFixtures.FindAbove(f) == null) { Skip = "skipped: " + f + " is not in this clone or above it"; return; }
        }
    }

    /// <summary>
    /// A fact that swims an episode, in a child process of the Release nursery. Skipped unless <c>EVOSIM_NURSERY_SWIM=1</c> is set, so that no default
    /// run swims; run on purpose with <c>--filter "Category=Swim"</c> and the variable set. Also skipped, saying why, where the Release nursery is absent
    /// or older than its sources (<see cref="RestepProcess.Problem"/>), where the pool it reads is not in this tree, where another file it reads is
    /// absent, or (<paramref name="mainRegistry"/>) where the main checkout's registry is.
    /// </summary>
    public sealed class SwimFactAttribute : FactAttribute
    {
        public const string Variable = "EVOSIM_NURSERY_SWIM";

        /// <param name="pool">The pool the request names, which the child reads from this tree (a request's paths stay under the repository root).</param>
        /// <param name="elsewhere">Files read by the test itself, in this clone or any folder above it.</param>
        /// <param name="mainRegistry">Whether the test reads the main checkout's <c>rounds/nursery/registry.jsonl</c>.</param>
        public SwimFactAttribute(string pool, string[] elsewhere = null, bool mainRegistry = false)
        {
            if (Environment.GetEnvironmentVariable(Variable) != "1")
            {
                Skip = "skipped: this test swims an episode. Set " + Variable + "=1 and run with --filter \"Category=Swim\" (the command is in RestepSwimTests.cs's header)";
                return;
            }
            string problem = RestepProcess.Problem();
            if (problem != null) { Skip = "skipped: " + problem; return; }
            if (!File.Exists(Path.Combine(RestepFixtures.Root, pool.Replace('/', Path.DirectorySeparatorChar))))
            {
                Skip = "skipped: " + pool + " is not in this tree (" + RestepFixtures.Root + "); the child reads the pool from the repository root";
                return;
            }
            foreach (string f in elsewhere ?? new string[0])
                if (RestepFixtures.FindAbove(f) == null) { Skip = "skipped: " + f + " is not in this clone or above it"; return; }
            if (mainRegistry && RestepFixtures.MainRegistry() == null)
                Skip = "skipped: the main checkout's rounds/nursery/registry.jsonl is absent (looked in every folder above this tree's root)";
        }
    }

    /// <summary>
    /// The swim tests' child process: the Release <c>Evosim.Nursery.dll</c>, started by Unity's <c>dotnet.exe</c> with the environment the room gives a re-step
    /// (no <c>DOTNET_*</c> setting, no <c>EVOSIM_TANK_TRACE</c>), because the pilot was swum by that program with tiered compilation off, and a swim inside
    /// the xunit host (tiering on, a Debug build) proves nothing either way.
    /// </summary>
    internal static class RestepProcess
    {
        public const string DllVariable = "EVOSIM_NURSERY_DLL", DotnetVariable = "EVOSIM_NURSERY_DOTNET";
        public const string UnityDotnet = @"C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Data\DotNetSdk\dotnet.exe";

        /// <summary>The Release nursery the test names (<c>EVOSIM_NURSERY_DLL</c>), by default the one this tree builds.</summary>
        public static string Dll =>
            Environment.GetEnvironmentVariable(DllVariable) is string d && d.Length > 0 ? d
            : Path.Combine(RestepFixtures.Root, "artifacts", "Evosim.Nursery", "bin", "Release", "net8.0", "Evosim.Nursery.dll");

        public static string Dotnet =>
            Environment.GetEnvironmentVariable(DotnetVariable) is string d && d.Length > 0 ? d : UnityDotnet;

        /// <summary>Why a swim cannot be made now, or null: the program or the host is absent, or the program is older than the sources it was built from.</summary>
        public static string Problem()
        {
            if (!File.Exists(Dll)) return Dll + " is absent: build it first with `dotnet build src/Evosim.Nursery -c Release -m:1`";
            if (!File.Exists(Dotnet)) return Dotnet + " is absent: set " + DotnetVariable + " to the dotnet.exe of Unity's SDK";
            DateTime built = File.GetLastWriteTimeUtc(Dll);
            foreach (string project in new[] { "Evosim.Nursery", "Evosim.Core", "Evosim.Dynamics", "Evosim.Farm" })
            {
                string folder = Path.Combine(RestepFixtures.Root, "src", project);
                if (!Directory.Exists(folder)) continue;
                foreach (string source in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
                    if (File.GetLastWriteTimeUtc(source) > built)
                        return Dll + " is older than " + Path.GetRelativePath(RestepFixtures.Root, source) + ": build it again with `dotnet build src/Evosim.Nursery -c Release -m:1`";
            }
            return null;
        }

        /// <summary>Starts the program with <paramref name="args"/> in the repository, and waits for it (up to <paramref name="minutes"/>). The child's output is read as it comes.</summary>
        public static (int code, string stdout, string stderr) Run(int minutes, params string[] args)
        {
            var info = new System.Diagnostics.ProcessStartInfo(Dotnet)
            {
                WorkingDirectory = RestepFixtures.Root, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
            };
            info.ArgumentList.Add(Dll);
            foreach (string a in args) info.ArgumentList.Add(a);
            foreach (string name in info.Environment.Keys.ToArray())
                if (name.StartsWith("DOTNET_", StringComparison.OrdinalIgnoreCase) || name.StartsWith("EVOSIM_GPU_", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "EVOSIM_TANK_TRACE", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "EVOSIM_REPO_ROOT", StringComparison.OrdinalIgnoreCase))
                    info.Environment.Remove(name);

            using (var p = System.Diagnostics.Process.Start(info))
            {
                var o = new System.Text.StringBuilder();
                var e = new System.Text.StringBuilder();
                p.OutputDataReceived += (s, x) => { if (x.Data != null) lock (o) o.AppendLine(x.Data); };
                p.ErrorDataReceived += (s, x) => { if (x.Data != null) lock (e) e.AppendLine(x.Data); };
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                if (!p.WaitForExit(minutes * 60_000))
                {
                    try { p.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                    throw new TimeoutException($"the nursery did not finish in {minutes} minutes: {args.FirstOrDefault()} {string.Join(" ", args.Skip(1))}");
                }
                p.WaitForExit();   // lets the output readers drain
                return (p.ExitCode, o.ToString(), e.ToString());
            }
        }
    }
}
