using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Evosim.Core;
using Evosim.Nursery;
using Xunit;

namespace Evosim.NurseryRoom.Tests
{
    /// <summary>
    /// A short episode as <c>--restep</c> writes one, for the tank screen's tests and for a smoke server: 30 s at 10 Hz, six bodies of two links, a few
    /// events and three pieces of carrion. <b>Nothing swims</b>: the motion is made here, from waypoints, and no <c>episode.step</c> is called on a real nursery.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The four files are written in exactly the nursery's format and through the nursery's own types wherever they are public: the columns are
    /// <see cref="FrameChannel"/>s, the snow islands are <see cref="IslandScan.Run"/>'s own scan of a made-up snow, <c>request.json</c> is the room's text
    /// parsed and written back by <see cref="RestepRequest"/>, <c>episode.json</c> and the events are <see cref="Json.Writer"/>'s, the frames are a
    /// <see cref="BinaryWriter"/>'s little-endian float32 (as <c>FrameRecorder</c> writes them), and <c>result.json</c> is published by
    /// <see cref="RestepFiles.PublishResult"/>, last. What cannot be shared is <c>FrameRecorder</c> itself, which needs a live <c>EpisodeProbe</c> (a world that
    /// has been stepped) and <c>Program.RestepSwimAndWrite</c>, which is private: for those a drift test reads their source and fails when a name or a key the fixture
    /// writes is no longer in it.
    /// </para>
    /// </remarks>
    internal static class TankFixture
    {
        public const double Seconds = 30, Rate = 10, Radius = 22.6, Depth = 25;
        public const float Dt = 0.02f;
        public const int StepsPerSample = 5;   // 1 / (Rate * Dt)
        public const int EconomyEvery = 5;     // an economy step every 0.5 s: every fifth frame

        public sealed class Member
        {
            public int Slot, Pool, Body;
            public bool Landed;
            /// <summary>The slot's net as the nursery writes it ("R"), or null for a body that did not land.</summary>
            public string Net;
        }

        public sealed class Build
        {
            public string GitCommit = "4a3ae524c1f0a7e96d2b85f3a10c9e47b6d8e2f1", CoreHash = "9f1c02aa3b7d4e65", DynamicsHash = "77be31c09a2d58f4", FarmHash = "c4e8a1d2f0936b57", NurseryHash = "91c0b7d2e5a3f648";
            public bool GitDirty = false;
            public string Runtime = ".NET 8.0.8", Tiered = "False", Note = null;
        }

        public sealed class Result
        {
            public string Folder;
            public int Frames, FrameFloats;
            public List<FrameChannel> Channels;
            public List<string> EventLines;
            public int[] Links;
            public double JoulesCorpse0;
        }

        // ------------------------------------------------------------------ the world the fixture's bodies moved in

        private static readonly double[][] Islands = { new[] { 14.0, 28.0, 4.6 }, new[] { 31.0, 17.0, 5.2 }, new[] { 25.0, 35.0, 4.2 } };

        private static double Smooth(double a, double b, double x)
        {
            double t = Math.Max(0, Math.Min(1, (x - a) / (b - a)));
            return t * t * (3 - 2 * t);
        }

        /// <summary>The made-up snow, J/m3, at a point of the tank's own coordinates: three islands on the bed, laid through the column.</summary>
        public static double SnowAt(double x, double y, double z)
        {
            double best = 0;
            foreach (double[] i in Islands)
                best = Math.Max(best, 4.1 * (1 - Smooth(i[2] * .45, i[2], Math.Sqrt((x - i[0]) * (x - i[0]) + (z - i[1]) * (z - i[1])))));
            return best;
        }

        // x, z, speed m/s, then the waypoints it steers through
        private static readonly double[][] Course =
        {
            new[] { 8.0, 28.0, .40, 14, 28, 20, 25, 24, 23 },
            new[] { 36.0, 12.0, .38, 31, 17, 27, 22 },
            new[] { 17.0, 18.0, .36, 22, 22, 25, 27 },
            new[] { 30.0, 40.0, .34, 25, 35, 22, 31 },
            new[] { 10.0, 12.0, .20, 20, 14, 30, 14 },
            new[] { 22.0, 8.0, .50, 22, 20, 14, 28 },
        };

        /// <summary>One slot's root path: a position and a heading at each of the <paramref name="frames"/> sample times.</summary>
        private static (double[] x, double[] z, double[] h) RootPathOf(int slot, int frames)
        {
            double[] c = Course[slot % Course.Length];
            double x = c[0] + (slot / Course.Length) * 2.0, z = c[1], speed = c[2];
            var wp = new List<(double x, double z)>();
            for (int i = 3; i + 1 < c.Length; i += 2) wp.Add((c[i], c[i + 1]));
            int w = 0;
            double h = Math.Atan2(wp[0].z - z, wp[0].x - x);
            var px = new double[frames]; var pz = new double[frames]; var ph = new double[frames];
            for (int k = 0; k < frames; k++)
            {
                if (Math.Sqrt((wp[w].x - x) * (wp[w].x - x) + (wp[w].z - z) * (wp[w].z - z)) < 1.5 && w < wp.Count - 1) w++;
                double want = Math.Atan2(wp[w].z - z, wp[w].x - x), turn = Math.Atan2(Math.Sin(want - h), Math.Cos(want - h));
                h += Math.Max(-.09, Math.Min(.09, turn));
                x += Math.Cos(h) * speed / Rate; z += Math.Sin(h) * speed / Rate;
                px[k] = x; pz[k] = z; ph[k] = h;
            }
            return (px, pz, ph);
        }

        private sealed class Bite
        {
            public double T, Joules; public int Corpse, Emptied; public int[] Candidates; public int? Earner;
        }

        // ------------------------------------------------------------------ the writer

        private static void Add(List<FrameChannel> l, string name, string unit) => l.Add(new FrameChannel { Name = name, Unit = unit });

        /// <summary>Writes <c>frames.bin</c>, <c>events.jsonl</c>, <c>episode.json</c> and, last, <c>result.json</c> into <paramref name="folder"/>.</summary>
        public static Result Write(string folder, RestepRequest request, string episodeSeed, string controlLabel, string controlKind, IReadOnlyList<Member> members, Build build = null)
        {
            build = build ?? new Build();
            CultureInfo inv = CultureInfo.InvariantCulture;
            Directory.CreateDirectory(folder);
            int n = members.Count, N = (int)Math.Round(Seconds * Rate);
            IslandScan scan = IslandScan.Run(Radius, Depth, 4, SnowAt);
            double enterAt = scan.EnterAt, exitAt = scan.ExitAt;

            var paths = new (double[] x, double[] z, double[] h)[n];
            for (int s = 0; s < n; s++) paths[s] = RootPathOf(s, N);
            (double x, double z) PlaceOf(int s, double t) => (paths[s].x[(int)Math.Round(t * Rate) - 1], paths[s].z[(int)Math.Round(t * Rate) - 1]);
            bool Has(int s) => s < n && members[s].Landed;

            // the carrion: placed at the start; a body passes two of the pieces and takes from them
            var corpses = new List<(double x, double z, double j)>();
            corpses.Add(Has(0) ? (PlaceOf(0, 21.0).x, PlaceOf(0, 21.0).z, 40.0) : (18.0, 26.0, 40.0));
            corpses.Add(Has(2) ? (PlaceOf(2, 12.5).x, PlaceOf(2, 12.5).z, 40.0) : (27.0, 22.0, 40.0));
            corpses.Add((34.0, 30.0, 40.0));
            var bites = new List<Bite>();
            if (Has(2))
            {
                bites.Add(new Bite { T = 12.5, Joules = 25, Corpse = 1, Candidates = new[] { 2 }, Earner = 2 });
                bites.Add(new Bite { T = 13.0, Joules = 15, Corpse = 1, Emptied = 1, Candidates = new[] { 2 }, Earner = 2 });
            }
            if (Has(0)) bites.Add(new Bite { T = 21.0, Joules = 40, Corpse = 0, Emptied = 1, Candidates = Has(3) ? new[] { 0, 3 } : new[] { 0 }, Earner = null });

            // the columns, as FrameRecorder.BuildLayout names them, from the first frame's bodies
            var channels = new List<FrameChannel>();
            Add(channels, "t", "s");
            int[] links = members.Select(m => m.Landed ? 2 : 0).ToArray();
            const int Hidden = 2;
            string[] axes = { "x", "y", "z", "qx", "qy", "qz", "qw" };
            for (int i = 0; i < n; i++)
            {
                string p = "m" + i.ToString(inv) + ".";
                Add(channels, p + "alive", "flag");
                Add(channels, p + "reserve", "J");
                Add(channels, p + "tissue", "J");
                Add(channels, p + "gestation", "J");
                foreach (string l in new[] { "food", "light", "work", "upkeep", "neural", "handling", "exuded" }) Add(channels, p + "ledger." + l, "J per economy step");
                Add(channels, p + "snow", "J/m3");
                for (int j = 0; j < links[i]; j++)
                    for (int k = 0; k < 7; k++) Add(channels, p + "link" + j.ToString(inv) + "." + axes[k], k < 3 ? "m" : "quaternion");
                if (links[i] > 0)
                {
                    Add(channels, p + "drive0", "power, -1 to 1");
                    Add(channels, p + "bend0", "bend, -1 to 1");
                    for (int k = 0; k < Hidden; k++) Add(channels, p + "hidden" + k.ToString(inv), "unit output");
                    Add(channels, p + "sense." + SensorChannel.OrientationUp + ".p0", "normalised, about -1 to 1");
                    Add(channels, p + "sense." + SensorChannel.JointAngle + ".p1.dof0", "normalised, about -1 to 1");
                }
            }
            for (int c = 0; c < corpses.Count; c++)
            {
                string p = "corpse" + c.ToString(inv) + ".";
                Add(channels, p + "x", "m"); Add(channels, p + "y", "m"); Add(channels, p + "z", "m"); Add(channels, p + "joules", "J");
            }

            // the frames
            // slot 5 starts so poor that its upkeep (0.83 J an economy step) empties it at the 45th step, t = 22.5 s
            var reserve = members.Select((m, s) => s == 5 ? 37.25 : 200.0).ToArray();
            var ledger = new double[n][];
            for (int i = 0; i < n; i++) ledger[i] = new double[7];
            var snow = new double[n];
            var alive = members.Select(m => m.Landed).ToArray();
            var inIsland = new bool[n];
            var corpseJ = corpses.Select(c => c.j).ToArray();
            var events = new List<(double t, string line)>();
            string framesPath = Path.Combine(folder, "frames.bin");
            int floats = channels.Count;
            using (var fs = new FileStream(framesPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 16))
            using (var bw = new BinaryWriter(fs))
            {
                for (int k = 0; k < N; k++)
                {
                    double t = (k + 1) / Rate;
                    int written = 0;
                    void Put(double v) { bw.Write((float)v); written++; }

                    if ((k + 1) % EconomyEvery == 0)
                    {
                        // the economy step: carrion first, then each body in turn, as FrameRecorder.Metabolic reads them
                        foreach (Bite b in bites.Where(x => Math.Abs(x.T - t) < 1e-9))
                        {
                            corpseJ[b.Corpse] -= b.Joules;
                            var w = new Json.Writer(indent: false);
                            w.BeginObject();
                            w.Field("t", t); w.Field("type", "carrionEaten"); w.Field("joules", b.Joules); w.Field("corpsesEmptied", (long)b.Emptied);
                            if (b.Candidates.Length == 1) w.Field("member", b.Candidates[0]); else w.Raw("member", "null");
                            w.Raw("candidates", "[" + string.Join(",", b.Candidates) + "]");
                            w.Field("memberIsInferred", true);
                            w.EndObject();
                            events.Add((t, w.ToString()));
                        }
                        for (int i = 0; i < n; i++)
                        {
                            if (!alive[i]) continue;
                            double food = 0;
                            double sn = links[i] > 0 ? SnowAt(paths[i].x[k], -Depth + .5, paths[i].z[k]) : 0;
                            snow[i] = sn;
                            if (sn >= enterAt) food += 2.2 * .5;
                            foreach (Bite b in bites.Where(x => Math.Abs(x.T - t) < 1e-9 && x.Earner == i)) food += b.Joules;
                            double work = .15 + .03 * i, upkeep = .45, neural = .02, handling = .01, exuded = .05;
                            ledger[i] = new[] { food, 0, work, upkeep, neural, handling, exuded };
                            reserve[i] += food - work - upkeep - neural - handling - exuded;
                            if (reserve[i] <= 0)
                            {
                                alive[i] = false;
                                var w = new Json.Writer(indent: false);
                                w.BeginObject(); w.Field("t", t); w.Field("type", "death"); w.Field("member", i); w.EndObject();
                                events.Add((t, w.ToString()));
                                continue;
                            }
                            if (i == 4 && Math.Abs(t - 27.5) < 1e-9)
                            {
                                var w = new Json.Writer(indent: false);
                                w.BeginObject(); w.Field("t", t); w.Field("type", "birthHeld"); w.Field("member", i); w.Field("funds", 262.4); w.Field("gate", 260.0); w.EndObject();
                                events.Add((t, w.ToString()));
                            }
                            if (links[i] > 0)
                            {
                                if (!inIsland[i] && sn >= enterAt)
                                {
                                    inIsland[i] = true;
                                    var w = new Json.Writer(indent: false);
                                    w.BeginObject(); w.Field("t", t); w.Field("type", "islandEntered"); w.Field("member", i); w.Field("snow", sn); w.EndObject();
                                    events.Add((t, w.ToString()));
                                }
                                else if (inIsland[i] && sn <= exitAt) inIsland[i] = false;
                            }
                        }
                    }

                    Put((float)t);
                    for (int i = 0; i < n; i++)
                    {
                        bool a = alive[i];
                        Put(a ? 1 : 0);
                        double[] l = ledger[i];
                        Put(a ? reserve[i] : double.NaN); Put(a ? 120 : double.NaN); Put(a ? 0 : double.NaN);
                        for (int q = 0; q < 7; q++) Put(a ? l[q] : double.NaN);
                        Put(a ? snow[i] : double.NaN);
                        if (links[i] == 0) continue;
                        double x = paths[i].x[k], z = paths[i].z[k], h = paths[i].h[k], y = -Depth + .7 + .12 * i;
                        double bend = .55 * Math.Sin(1.9 * t + i);
                        double ang = h + Math.PI + bend;
                        double[][] pose =
                        {
                            new[] { x, y, z, 0, Math.Sin(-h / 2), 0, Math.Cos(-h / 2) },
                            new[] { x + .35 * Math.Cos(ang), y, z + .35 * Math.Sin(ang), 0, Math.Sin(-(h + bend) / 2), 0, Math.Cos(-(h + bend) / 2) },
                        };
                        for (int j = 0; j < 2; j++) for (int q = 0; q < 7; q++) Put(a ? pose[j][q] : double.NaN);
                        Put(a ? .6 * Math.Sin(1.9 * t + i) : double.NaN);
                        Put(a ? bend / .8 : double.NaN);
                        for (int q = 0; q < Hidden; q++) Put(a ? Math.Sin(t * (1.3 + .4 * q) + i) : double.NaN);
                        Put(a ? .3 * Math.Cos(h) : double.NaN);
                        Put(a ? bend / .8 : double.NaN);
                    }
                    for (int c = 0; c < corpses.Count; c++)
                    {
                        Put(corpses[c].x); Put(-Depth + .05); Put(corpses[c].z); Put(Math.Max(0, corpseJ[c]));
                    }
                    if (written != floats) throw new InvalidOperationException($"a frame wrote {written} values and the layout names {floats} channels");
                }
            }

            // events.jsonl: in time order, one line each, "\n" between and after
            string eventsPath = Path.Combine(folder, "events.jsonl");
            List<string> eventLines = events.OrderBy(e => e.t).Select(e => e.line).ToList();
            File.WriteAllText(eventsPath, eventLines.Count == 0 ? "" : string.Join("\n", eventLines) + "\n", new UTF8Encoding(false));

            // episode.json: the same sequence of fields as Program.RestepSwimAndWrite
            var ep = new Json.Writer(indent: true);
            ep.BeginObject();
            ep.Field("format", 1);
            ep.Field("program", "Evosim.Nursery --restep");
            ep.Raw("request", request.ToJson());
            ep.BeginObject("build");
            ep.Field("gitCommit", build.GitCommit);
            ep.Field("gitDirty", build.GitDirty);
            ep.Field("coreHash", build.CoreHash);
            ep.Field("dynamicsHash", build.DynamicsHash);
            ep.Field("farmHash", build.FarmHash);
            ep.Field("nurseryHash", build.NurseryHash);
            ep.Field("runtime", build.Runtime);
            ep.Field("tieredCompilation", build.Tiered);
            ep.Field("note", build.Note);
            ep.EndObject();
            ep.Field("recipeHash", request.RecipeHash);
            ep.Field("configHash", "c0ffee0123456789");
            ep.Field("episodeConfigHash", "87d66e98fed542c2");
            ep.Field("episodeSeed", episodeSeed);
            ep.BeginObject("control");
            ep.Field("index", request.Brain);
            ep.Field("label", controlLabel);
            ep.Field("kind", controlKind);
            ep.EndObject();
            ep.BeginArray("members");
            foreach (Member m in members)
            {
                ep.BeginObject();
                ep.Field("slot", m.Slot); ep.Field("pool", m.Pool); ep.Field("body", m.Body); ep.Field("landed", m.Landed); ep.Field("links", links[m.Slot]);
                ep.EndObject();
            }
            ep.EndArray();
            ep.Field("seconds", Seconds);
            ep.Field("dt", Dt);
            ep.Field("sampleRate", Rate);
            ep.Field("stepsPerSample", StepsPerSample);
            ep.Field("frames", N);
            ep.Field("frameFloats", channels.Count);
            ep.Field("encoding", "float32 little-endian, frame-major: frame k is frameFloats values, channel order as listed; the first channel is the physics clock t");
            ep.Field("islandRule", scan.Rule);
            ep.BeginObject("islands");
            ep.Field("read", scan.Read);
            ep.Field("rule", scan.Rule);
            ep.Field("grid", IslandScan.Grid);
            ep.Field("scanHeightAboveBed", IslandScan.HeightAboveBed);
            ep.Field("enterShare", IslandScan.EnterShare);
            ep.Field("exitShare", IslandScan.ExitShare);
            ep.Field("patches", scan.Patches);
            ep.Field("points", scan.Points);
            ep.Field("pointsInIsland", scan.PointsInIsland);
            if (scan.Read) { ep.Field("richest", scan.Richest); ep.Field("enterAt", scan.EnterAt); ep.Field("exitAt", scan.ExitAt); }
            else { ep.Raw("richest", "null"); ep.Raw("enterAt", "null"); ep.Raw("exitAt", "null"); }
            ep.EndObject();
            ep.Field("finalSolverDigest", 0x1badc0ffee0ddf00UL.ToString("x16", inv));
            var lines = new List<string>();
            foreach (FrameChannel c in channels)
            {
                var one = new Json.Writer(indent: false);
                one.BeginObject(); one.Field("name", c.Name); one.Field("unit", c.Unit); one.EndObject();
                lines.Add("    " + one);
            }
            ep.Raw("channels", "[\n" + string.Join(",\n", lines) + "\n  ]");
            ep.EndObject();
            File.WriteAllText(Path.Combine(folder, "episode.json"), ep.ToString().Replace("\r\n", "\n") + "\n", new UTF8Encoding(false));

            // result.json last: its presence means a complete episode
            var r = new Json.Writer(indent: true);
            r.BeginObject();
            r.Field("complete", true);
            r.Field("episodeSeed", episodeSeed);
            r.Field("control", controlLabel);
            r.BeginArray("members");
            foreach (Member m in members)
            {
                r.BeginObject();
                r.Field("slot", m.Slot); r.Field("pool", m.Pool); r.Field("landed", m.Landed);
                if (m.Landed)
                {
                    r.Field("net", m.Net);
                    r.Field("bits", BitConverter.DoubleToInt64Bits(double.Parse(m.Net, inv)).ToString("x16", inv));
                }
                else { r.Raw("net", "null"); r.Raw("bits", "null"); }
                r.Field("survived", alive[m.Slot]);
                r.EndObject();
            }
            r.EndArray();
            r.EndObject();
            RestepFiles.PublishResult(folder, r.ToString().Replace("\r\n", "\n") + "\n");

            return new Result { Folder = folder, Frames = N, FrameFloats = channels.Count, Channels = channels, EventLines = eventLines, Links = links, JoulesCorpse0 = corpses[0].j };
        }

        // ------------------------------------------------------------------ for a room built on EpisodeFixture

        /// <summary>
        /// Writes the finished files of the episode that <c>episode.step</c> has just started for brain <paramref name="b"/>, draw <paramref name="d"/> and tank
        /// <paramref name="t"/> (so that <c>request.json</c> is the room's own text), and ends the fake process. <paramref name="net"/> gives each slot's net
        /// (<see cref="EpisodeFixture.E"/> for the run's own, null: did not land).
        /// </summary>
        public static Result Swim(EpisodeFixture f, int b, int d, int t, Func<int, int, string> net, Build build = null)
        {
            ExamPlanView plan = f.Plan();
            List<int> tank = plan.Draws[d].Tanks[t];
            var members = tank.Select((pool, slot) => new Member { Slot = slot, Pool = pool, Body = plan.Genomes[pool].Body, Net = net(slot, pool), Landed = net(slot, pool) != null }).ToList();
            string folder = f.Cache(b, d, t);
            RestepRequest request = RestepRequest.Parse(File.ReadAllText(Path.Combine(folder, "request.json")));
            Result r = Write(folder, request, plan.Draws[d].Seed, plan.Brains[b].Label, plan.Brains[b].Kind, members, build);
            f.Procs.Alive.Clear();
            return r;
        }
    }

    /// <summary>A fact that starts the smoke server (on port 5180, never the room's own 5179) and is skipped unless <c>NURSERY_ROOM_SMOKE_STOP</c> names the stop file that ends it.</summary>
    public sealed class SmokeFactAttribute : FactAttribute
    {
        public const string StopVariable = "NURSERY_ROOM_SMOKE_STOP";

        public SmokeFactAttribute()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(StopVariable)))
                Skip = "skipped: the smoke server is started only when " + StopVariable + " names the file whose appearing stops it";
        }
    }
}
