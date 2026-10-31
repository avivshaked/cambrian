using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Evosim.Core;
using Evosim.Nursery;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Evosim.NurseryRoom.Tests.E2e
{
    /// <summary>
    /// What the end-to-end flows stand on: a room on synthetic files, in a folder of the build output (nothing a flow does reaches the source tree), with the fake launcher and a fixed
    /// load. The registry holds five entries: the pilot-shaped one the tank flow reads (1), and four small runs the run screen is read on:
    /// <list type="bullet">
    /// <item><b>2 alpha</b>: six brains; both genomes read only Chemical, so the Flow knockout leaves them unchanged and its column is greyed whole.</item>
    /// <item><b>3 beta</b>: five brains, none of them a Flow knockout (the control that did not swim a brain).</item>
    /// <item><b>4 gamma</b>: six brains; its genomes read Chemical and Flow, so nothing in it is greyed.</item>
    /// <item><b>5 delta</b>: six brains; one genome reads Flow and one does not, so the Flow knockout is greyed on one genome of two.</item>
    /// </list>
    /// The recipes are under rounds/nursery and in a git repository made in the room's root (a real one, whose states the recipe screen reads); a trial recipe is under the trials folder
    /// and a recipe whose world this build cannot load beside them. Disposing deletes the folder, the repository included.
    /// </summary>
    internal sealed class E2eWorld : IDisposable
    {
        public const string Alpha = "2", Beta = "3", Gamma = "4", Delta = "5";
        public const string Committed = "rounds/nursery/brain2-w11.json";
        public const string ChangedRecipe = "rounds/nursery/e2e-delta.json", UntrackedRecipe = "rounds/nursery/e2e-untracked.json";
        public const string TrialRecipe = "scratch/nursery-trials/e2e-trial.json";
        public const string Unloadable = EpisodeFixture.UnloadableRecipe;

        public static readonly string[] SixBrains = { "own", "frozen", "full", "brainless", "knockout Chemical", "knockout Flow" };
        public static readonly string[] FiveBrains = { "own", "frozen", "full", "brainless", "knockout Chemical" };

        public readonly EpisodeFixture Ep;
        /// <summary>The room the flows host: the fixture's, with the registry where a real room has it (rounds/nursery/registry.jsonl of the main checkout), so that screens show a short path.</summary>
        public readonly RoomContext Room;
        public Fx Fx => Ep.Fx;
        public string Root => Ep.Fx.Root;

        public E2eWorld()
        {
            Ep = new EpisodeFixture("scratch/nursery/live");
            string nursery = Path.Combine(Root, "rounds", "nursery");
            string brain2 = File.ReadAllText(Path.Combine(nursery, "brain2-w11.json"));
            foreach (string name in new[] { "beta", "gamma", "delta" })
                File.WriteAllText(Path.Combine(nursery, "e2e-" + name + ".json"), brain2.Replace("\"name\": \"brain2-w11\"", "\"name\": \"e2e-" + name + "\""));
            Directory.CreateDirectory(Path.Combine(Root, "scratch", "nursery-trials"));
            File.WriteAllText(Path.Combine(Root, "scratch", "nursery-trials", "e2e-trial.json"), brain2.Replace("\"name\": \"brain2-w11\"", "\"name\": \"e2e-trial\""));
            EpisodeFixture.WriteUnloadableWorld(Root);

            // A run still going, listed by room.json and not in the registry: its record says running, so STOP is offered. Its folder is old, so that it is listed last.
            string live = Path.Combine(Fx.Main, "scratch", "nursery", "live");
            Directory.CreateDirectory(live);
            File.WriteAllText(Path.Combine(live, "run.json"), "{\"status\":\"running\"}");
            Directory.SetCreationTimeUtc(live, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            // Genomes: alpha reads Chemical only; gamma reads Chemical and Flow; delta has one of each; beta reads both.
            Genome chem = Central(SensorChannel.Chemical), both = Central(SensorChannel.Chemical, SensorChannel.Flow);
            MakeRun("alpha", "rounds/nursery/brain2-w11.json", "2026-10-01T09:00:00Z", "completed", SixBrains, new[] { chem, chem }, new[] { 0, 1 }, 1.0);
            MakeRun("beta", "rounds/nursery/e2e-beta.json", "2026-10-02T09:00:00Z", "completed", FiveBrains, new[] { both, both }, new[] { 0, 1 }, 0.8);
            MakeRun("gamma", "rounds/nursery/e2e-gamma.json", "2026-10-03T09:00:00Z", "completed", SixBrains, new[] { both, both }, new[] { 1, 2 }, 0.5);
            MakeRun("delta", "rounds/nursery/e2e-delta.json", "2026-10-04T09:00:00Z", "failed", SixBrains, new[] { chem, both }, new[] { 0, 1 }, 1.2);

            // A real repository in the room's root: every recipe committed, then the flows change or add files to make the other states.
            Git("init", "-q");
            Git("add", "-A");
            RecipeGitTests.Commit(Root, "pre-register the e2e recipes");

            string registry = Path.Combine(Fx.Main, "rounds", "nursery", "registry.jsonl");
            Directory.CreateDirectory(Path.GetDirectoryName(registry));
            File.Copy(Fx.Registry, registry);
            Room = new RoomContext(Root, Fx.Room.Settings, Fx.Main, registry, Path.Combine(Fx.Dir, "activity.jsonl"), Ep.Launcher, Fx.Queue, Fx.Heat, 32, _ => Fx.Build, null, Ep.Procs, Ep.Copier);
        }

        public string Full(string rel) => Fx.Full(rel);

        public string Git(params string[] args) => RecipeGitTests.Git(Root, args);

        /// <summary>The recipe's file changed since its commit (a trailing newline: it still parses).</summary>
        public void MakeChanged(string rel) => File.AppendAllText(Full(rel), "\n");

        /// <summary>A recipe file git has never seen.</summary>
        public void MakeUntracked(string rel) => File.WriteAllText(Full(rel), File.ReadAllText(Full(Committed)).Replace("\"name\": \"brain2-w11\"", "\"name\": \"e2e-untracked\""));

        // ------------------------------------------------------------------ the genomes

        /// <summary>A one-brain genome whose links read exactly these channels.</summary>
        public static Genome Central(params SensorChannel[] reads)
        {
            Genome g = GenomeResourceTests.Synthetic();
            g.Brain.Links.Clear();
            Phenotype body = Developer.Develop(g);
            foreach (SensorChannel ch in reads)
                g.Brain.Links.Add(new BrainLink { From = GenomeResourceTests.Port(body, 1, ch), To = GenomeResourceTests.Out(body, 1, BrainOutputKind.Power), Weight = 0.5f });
            Assert.Empty(g.Brain.Validate());
            return g;
        }

        private static string R(double v) => v.ToString("R", CultureInfo.InvariantCulture);

        // ------------------------------------------------------------------ a run

        /// <summary>
        /// Writes a completed run's files in the main checkout's scratch/nursery (its generations, its scores, its exam of these brains on these genomes, its genomes, the top genome of each
        /// generation and a log) and enters it in the registry. <paramref name="effect"/> scales how much each control costs a genome that it changes.
        /// </summary>
        private void MakeRun(string name, string recipePath, string started, string status, string[] brains, Genome[] genomes, int[] bodies, double effect)
        {
            string folder = Path.Combine(Fx.Main, "scratch", "nursery", name);
            Directory.CreateDirectory(folder);
            var enc = new UTF8Encoding(false);
            const int Gens = 12;

            var gens = new List<string>();
            var scores = new List<string>();
            for (int g = 0; g < Gens; g++)
            {
                double mean = 200 + 30 * g + 15 * Math.Sin(g * 1.3), best = mean + 150 + 12 * g;
                int body = (g * 3 + 1) % 5;
                gens.Add("{\"gen\":" + g + ",\"seconds\":" + (g * 60 + 60) + ",\"landed\":96,\"netMean\":" + R(mean) + ",\"netBest\":" + R(best) + ",\"alive\":0.95,\"workWatts\":" + R(0.08 - 0.002 * g) +
                    ",\"path\":" + R(60 + 2 * g) + ",\"fresh\":" + R(0.9 + 0.01 * g) + ",\"top\":{\"score\":" + R(best) + ",\"episodes\":16,\"body\":" + body + ",\"born\":" + g + ",\"nodes\":2}," +
                    "\"made\":{\"children\":90,\"crossovers\":47,\"transferred\":47,\"added\":0,\"refused\":{},\"ends\":{\"sense\":47,\"oscillator\":0,\"loop\":0,\"unresolved\":0},\"routeLength\":1,\"unread\":0,\"baseBetter\":0.63,\"dMean\":0.2,\"mutated\":0.7,\"neuronsMean\":0.03,\"neuronsMax\":1}," +
                    "\"brain\":{\"hidden\":" + R(0.03 * g) + ",\"hiddenMax\":" + (1 + g / 4) + ",\"links\":" + R(82 + g) + ",\"linksMax\":" + (116 + g) + ",\"topHidden\":0,\"topLinks\":" + (48 + g) + "}}");
                var members = new List<string>();
                for (int m = 0; m < 4; m++)
                    members.Add("{\"body\":" + (m == 0 ? body : 9) + ",\"born\":" + (m == 0 ? g : 0) + ",\"episodes\":4,\"value\":" + R(m == 0 ? best : mean - 40 * m) + ",\"parts\":{\"food\":" + R(m == 0 ? 0.6 * best : 20) + ",\"move\":" + R(m == 0 ? 0.4 * best : 10) + "}}");
                scores.Add("{\"gen\":" + g + ",\"reward\":\"Net\",\"members\":[" + string.Join(",", members) + "]}");
            }
            File.WriteAllLines(Path.Combine(folder, "tank-generations.jsonl"), gens, enc);
            File.WriteAllLines(Path.Combine(folder, "tank-scores.jsonl"), scores, enc);
            File.WriteAllLines(Path.Combine(folder, "tank-best.jsonl"), Enumerable.Range(0, Gens).Select(g => GenomeJson.Write(genomes[g % genomes.Length])), enc);
            File.WriteAllLines(Path.Combine(folder, "tank-exam-genomes.tsv"), genomes.Select((g, i) => i + "\t" + GenomeJson.Write(g)), enc);
            File.WriteAllLines(Path.Combine(folder, "nursery.log"), new[] { "gen 0 done", "gen 1 done", "exam: own on genome 0", "completed" }, enc);

            // The exam: a genome's draws of each brain are its own less what the control costs it. A control that leaves the genome as it is (a knockout of a channel it reads no
            // port of) costs it nothing, so its draws are exactly the own brain's: zero by construction.
            var rows = new List<string>();
            for (int b = 0; b < brains.Length; b++)
                for (int g = 0; g < genomes.Length; g++)
                {
                    var per = new double[8];
                    for (int d = 0; d < per.Length; d++)
                    {
                        double own = 100 + 12 * g + 8 * Math.Sin(1.7 * d + g);
                        per[d] = b == 0 || Unchanged(brains[b], genomes[g]) ? own : own - effect * Cost(brains[b]) * (1 + .1 * g) + 1.5 * Math.Sin(2.3 * d + 3 * b + g);
                    }
                    rows.Add("{\"brain\":\"" + brains[b] + "\",\"genome\":" + g + ",\"body\":" + bodies[g] + ",\"net\":" + R(per.Average()) + ",\"per\":[" + string.Join(",", per.Select(R)) + "]}");
                }
            File.WriteAllLines(Path.Combine(folder, "tank-exam.jsonl"), rows, enc);

            RegistryEntry.Append(Fx.Registry, new RegistryEntry
            {
                Recipe = "e2e-" + name, RecipeHash = "aa", RecipePath = recipePath, ConfigHash = "c", GitCommit = "g", CoreHash = "c", DynamicsHash = "d", FarmHash = "f", NurseryHash = "n",
                Out = folder, Status = status, StartedAtUtc = started, EndedAtUtc = started.Replace("09:00", "10:00"), Rerunnable = true,
            });
        }

        private static double Cost(string brain) => brain == "frozen" ? 20 : brain == "full" ? 15 : brain == "brainless" ? 25 : brain == "knockout Chemical" ? 12 : 9;

        /// <summary>The Flow knockout leaves a genome whose links read no Flow port as it is.</summary>
        private static bool Unchanged(string brain, Genome g)
        {
            if (brain != "knockout Flow") return false;
            Phenotype body = Developer.Develop(g);
            ulong flow = GenomeResourceTests.Port(body, 1, SensorChannel.Flow);
            return !g.Brain.Links.Any(l => l.From == flow);
        }

        // ------------------------------------------------------------------ the room

        /// <summary>What the git reading of a recipe says: <c>real</c> asks git in the room's repository, the others are made up (the states real git cannot give).</summary>
        public string GitMode = "real";

        private GitResult GitFake(string dir, IReadOnlyList<string> args, int ms)
        {
            switch (GitMode)
            {
                case "outside": return new GitResult { ExitCode = 128, Error = "fatal: not a git repository (or any of the parent directories): .git" };
                case "unreadable": return new GitResult { Failure = "timed out after 10 s" };
                default: return RecipeGit.RunGit(dir, args, ms);
            }
        }

        /// <summary>Hosts the room in this process, on a free port, with the fake launcher the fixture holds and git as <see cref="GitMode"/> says.</summary>
        public RoomHost Host() => new RoomHost(Room, GitFake);

        public void Dispose() => RecipeGitTests.Remove(Fx.Dir);   // a folder that holds a repository: its object files are read-only
    }

    /// <summary>The room, served from this process on a free loopback port by the program's own <c>Program.Create</c>.</summary>
    internal sealed class RoomHost : IDisposable
    {
        private readonly WebApplication _app;
        public int Port { get; }
        public string Base => "http://127.0.0.1:" + Port;
        public RoomContext Room { get; }

        public RoomHost(RoomContext room, GitRunner git = null)
        {
            Room = room;
            Exception last = null;
            for (int attempt = 0; attempt < 5 && _app == null; attempt++)
            {
                // The room's own port is in its settings (the host filter reads it): a free one is put there before the app is made.
                var probe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
                probe.Start();
                int port = ((System.Net.IPEndPoint)probe.LocalEndpoint).Port;
                probe.Stop();
                room.Settings.Port = port;
                try
                {
                    WebApplication app = Program.Create(room, Where.WwwRoot, git);
                    app.Start();
                    _app = app;
                    Port = port;
                }
                catch (Exception e) when (e is IOException || e is InvalidOperationException) { last = e; }
            }
            if (_app == null) throw new InvalidOperationException("the room could not be hosted on a free port", last);
        }

        public void Dispose()
        {
            _app.StopAsync().GetAwaiter().GetResult();
            _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
