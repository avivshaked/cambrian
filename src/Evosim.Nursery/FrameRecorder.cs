using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Text;
using Evosim.Core;

namespace Evosim.Nursery
{
    /// <summary>One column of <c>frames.bin</c>: its name and its unit.</summary>
    public sealed class FrameChannel
    {
        public string Name, Unit;
    }

    /// <summary>
    /// The scan that tells where the snow's islands are, and the thresholds a body's snow is judged against. <see cref="Run"/> is a function of the tank's
    /// size and of a reader of the snow, so that it can be tested on a world that has been laid and never stepped.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Where the snow lies.</b> The tank lays it in one of two ways (<c>Tank.cs</c>, before the bodies are inoculated). With islands (the pilot's
    /// recipe, <c>brain2.json</c>: wavelength 20 m, cover 0.1), <c>World.LaySnowIslands</c> puts it through the whole water column, uniform down
    /// the column to the bed, so any height in the water finds it. Without them, <c>World.LaySnow</c> puts it in the lowest layers of every column.
    /// The scan is read in the lowest layer, <see cref="HeightAboveBed"/> above the bed, which holds the snow in both.
    /// </para>
    /// <para>
    /// <b>Patches.</b> A world of several patches (the pilot's config has four) reads its snow by position: the grid's density read takes the cell the
    /// point falls in and ignores the point's patch. So one scan, read at patch 0, is the scan of every patch, and it is read once.
    /// </para>
    /// </remarks>
    public sealed class IslandScan
    {
        public const int Grid = 33;
        public const double HeightAboveBed = 0.5, EnterShare = 0.25, ExitShare = 0.10;

        public bool Read;
        public double Richest = double.NaN, EnterAt = double.NaN, ExitAt = double.NaN;

        /// <summary>The scan's points inside the glass, and those at or above <see cref="EnterAt"/> (the islands' share of the bed, as the scan sees it).</summary>
        public int Points, PointsInIsland;

        public int Patches;

        /// <summary>The rule in words, or why no scan was read.</summary>
        public string Rule = "not read";

        /// <param name="tankRadius">The glass's radius, m; 0 for a world without round glass (no scan is read).</param>
        /// <param name="worldDepth">The water's depth, m, so that the bed is at <c>-worldDepth</c>.</param>
        /// <param name="patches">The world's horizontal patches; only said in the rule, since one scan serves all of them.</param>
        /// <param name="snowAt">The edible snow's density, J/m3, at (x, y, z) in the tank's own coordinates, where the glass is centred on (r, r).</param>
        public static IslandScan Run(double tankRadius, double worldDepth, int patches, Func<double, double, double, double> snowAt)
        {
            var scan = new IslandScan { Patches = patches };
            double r = tankRadius;
            if (!(r > 0d))
            {
                scan.Rule = "not read: the world has no round glass";
                return scan;
            }
            double y = -worldDepth + HeightAboveBed, best = 0d;
            var seen = new List<double>();
            for (int a = 0; a < Grid; a++)
                for (int b = 0; b < Grid; b++)
                {
                    double dx = (a / (double)(Grid - 1) * 2d - 1d) * r, dz = (b / (double)(Grid - 1) * 2d - 1d) * r;
                    if (dx * dx + dz * dz > r * r) continue;
                    double d = snowAt(r + dx, y, r + dz);
                    seen.Add(d);
                    if (d > best) best = d;
                }
            scan.Points = seen.Count;
            if (!(best > 0d))
            {
                scan.Rule = "not read: the scan found no snow";
                return scan;
            }
            scan.Read = true;
            scan.Richest = best;
            scan.EnterAt = EnterShare * best;
            scan.ExitAt = ExitShare * best;
            foreach (double d in seen) if (d >= scan.EnterAt) scan.PointsInIsland++;
            scan.Rule = string.Format(CultureInfo.InvariantCulture,
                "a body enters an island when the edible snow at its place rises to {0:R} J/m3 (a quarter of {1:R}, the richest of a {2} by {2} scan of the tank at {3:R} m above the bed, " +
                "the lowest layer, where the snow lies whether it was laid in islands through the column or in layers on the bed; read once before any step, and used for every one of the " +
                "{4} patch(es), since the grid reads snow by position and not by patch) after being at or below {5:R} (a tenth)",
                scan.EnterAt, best, Grid, HeightAboveBed, patches, scan.ExitAt);
            return scan;
        }
    }

    /// <summary>
    /// The watcher <c>--restep</c> swims an episode with: it records what <see cref="EpisodeProbe"/> offers, a frame every
    /// <see cref="StepsPerSample"/> physics steps into <c>frames.bin</c>, and the episode's events into <c>events.jsonl</c>. It only reads, and
    /// it holds no random stream.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The file.</b> Little-endian float32, frame-major: a frame is <see cref="Channels"/>.Count floats in the channels' order, and the first
    /// channel is <c>t</c>, the tank's clock in seconds (<see cref="EpisodeProbe.ClockSeconds"/>: world seconds, which at a pace factor of 1 are the
    /// physics clock's), so no frame's time is assumed. A frame is taken on every physics step that is a multiple of <see cref="StepsPerSample"/>, so
    /// the first is at t = 1 / rate and not at t = 0: at the start no body has a solver yet, and so no pose to read. The rate is per world second
    /// (D240), so a step count a frame is k / (rate × dt) at a pace factor of k.
    /// </para>
    /// <para>
    /// <b>The layout is read from the first frame's bodies</b> (their links, their degrees of freedom, the channels their brains read, the units of
    /// their brains), and fixed from then on: a body rebuilt on another plan mid-episode has its extra links dropped and its missing ones written as
    /// NaN, and the ones that stay are the first frame's. <b>NaN is what a channel with nothing to read is written as:</b> for a dead body, and for a
    /// body that landed nothing, its reserve, tissue, gestation, ledger, snow, link poses, drive, bend and hidden units; for any body, a sense on a
    /// link it no longer has. The <c>alive</c> channel is 1 or 0 and is never NaN.
    /// </para>
    /// <para>
    /// <b>What a channel's value is.</b> The poses, the senses, the brain's outputs and units are those of the physics step the frame is taken on.
    /// The energy, the ledger and the snow at the body are the last economy step's, so they change every 0.5 s of simulated time and not at
    /// the frame rate. The ledger channels are what the body earned and spent in that one economy step, not running sums.
    /// </para>
    /// <para>
    /// <b>The events</b> are read on economy steps. A <c>carrionEaten</c> is a step in which the world's mouths took charged units from corpses; its
    /// <c>member</c> is the one body whose food income was above 0 that step, and is null when there were none or several (snow is food too), so it
    /// is an inference and <c>candidates</c> lists the bodies it could be. An <c>islandEntered</c> is a body whose snow density rose above a quarter of
    /// the richest snow the watcher found on a scan of the bed at the start, after being below a tenth of it (the scan and the rule are in
    /// <see cref="IslandRule"/>). A <c>death</c> is a body that has left the world. A <c>birthHeld</c> is a body whose funds have risen to its breeding
    /// gate, which the episode would have turned into a birth had births not been held.
    /// </para>
    /// </remarks>
    public sealed class FrameRecorder : ITankWatcher, IDisposable
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class MemberLayout
        {
            public int Links, Dofs, Units, OutputUnits;
            public bool Central;
            public SenseSpec[] Senses = Array.Empty<SenseSpec>();
        }

        private readonly string _framesPath, _eventsPath;
        private readonly int _stepsPerSample;
        private readonly double _sampleRate;

        private FileStream _framesFile;
        private BinaryWriter _frames;
        private StreamWriter _events;
        private MemberLayout[] _layout;
        private double _prevUnits;
        private long _prevCorpsesEmptied;
        private double[] _placedJoules;
        private bool[] _alive, _inIsland, _gateOpen;
        private double _islandReference = double.NaN, _enterAt = double.NaN, _exitAt = double.NaN;

        /// <summary>The columns, in order, once the first frame has been taken.</summary>
        public List<FrameChannel> Channels { get; } = new List<FrameChannel>();

        public int FrameCount { get; private set; }

        public int StepsPerSample => _stepsPerSample;

        public double SampleRate => _sampleRate;

        /// <summary>The solver's state hash at the end of the episode, set by <see cref="End"/>.</summary>
        public ulong FinalDigest { get; private set; }

        public bool Ended { get; private set; }

        /// <summary>How the islands were told, for <c>episode.json</c>: the richest snow the scan found, the two thresholds, and what the scan was.</summary>
        public string IslandRule { get; private set; } = "not read";

        /// <summary>The scan itself (its numbers, for <c>episode.json</c>); null before <see cref="Begin"/>.</summary>
        public IslandScan Islands { get; private set; }

        /// <summary>Per member, the link count of the first frame's layout (0 for a body that was never read).</summary>
        public int[] LinksOfMember => Array.ConvertAll(_layout ?? Array.Empty<MemberLayout>(), m => m.Links);

        public FrameRecorder(string framesPath, string eventsPath, double sampleRate, int stepsPerSample)
        {
            _framesPath = framesPath;
            _eventsPath = eventsPath;
            _sampleRate = sampleRate;
            _stepsPerSample = stepsPerSample;
        }

        // ------------------------------------------------------------------ the four calls

        public void Begin(EpisodeProbe probe)
        {
            int n = probe.MemberCount;
            _alive = new bool[n];
            _inIsland = new bool[n];
            _gateOpen = new bool[n];
            for (int i = 0; i < n; i++) _alive[i] = probe.Landed(i);

            _placedJoules = new double[probe.PlacedCorpseCount];
            for (int c = 0; c < _placedJoules.Length; c++) _placedJoules[c] = probe.PlacedCorpse(c).Joules;
            _prevUnits = probe.UnitsEaten;
            _prevCorpsesEmptied = probe.CorpsesEmptied;

            ScanForIslands(probe);

            _framesFile = new FileStream(_framesPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 16);
            _frames = new BinaryWriter(_framesFile);
            _events = new StreamWriter(new FileStream(_eventsPath, FileMode.Create, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { NewLine = "\n" };
        }

        public void Physics(EpisodeProbe probe)
        {
            if (probe.Steps % _stepsPerSample == 0) Frame(probe);
        }

        public void Metabolic(EpisodeProbe probe)
        {
            double t = probe.EconomySeconds;
            int n = probe.MemberCount;

            // Carrion: units taken from corpses this economy step.
            double units = probe.UnitsEaten;
            if (units > _prevUnits)
            {
                var candidates = new List<int>();
                for (int i = 0; i < n; i++)
                    if (_alive[i] && probe.Alive(i) && probe.Ledger(i).FoodIncome > 0f) candidates.Add(i);
                var w = new Json.Writer(indent: false);
                w.BeginObject();
                w.Field("t", t);
                w.Field("type", "carrionEaten");
                w.Field("joules", (units - _prevUnits) * probe.JoulesPerUnit);
                w.Field("corpsesEmptied", probe.CorpsesEmptied - _prevCorpsesEmptied);
                if (candidates.Count == 1) w.Field("member", candidates[0]); else w.Raw("member", "null");
                w.Raw("candidates", "[" + string.Join(",", candidates) + "]");
                w.Field("memberIsInferred", true);
                w.EndObject();
                _events.WriteLine(w.ToString());
            }
            _prevUnits = units;
            _prevCorpsesEmptied = probe.CorpsesEmptied;

            for (int i = 0; i < n; i++)
            {
                if (!_alive[i]) continue;
                if (!probe.Alive(i))
                {
                    _alive[i] = false;
                    var w = new Json.Writer(indent: false);
                    w.BeginObject();
                    w.Field("t", t);
                    w.Field("type", "death");
                    w.Field("member", i);
                    w.EndObject();
                    _events.WriteLine(w.ToString());
                    continue;
                }

                double gate = probe.BreedingGate(i), funds = probe.BreedingFunds(i);
                bool open = gate > 0d && funds >= gate;
                if (open && !_gateOpen[i])
                {
                    var w = new Json.Writer(indent: false);
                    w.BeginObject();
                    w.Field("t", t);
                    w.Field("type", "birthHeld");
                    w.Field("member", i);
                    w.Field("funds", funds);
                    w.Field("gate", gate);
                    w.EndObject();
                    _events.WriteLine(w.ToString());
                }
                _gateOpen[i] = open;

                if (!double.IsNaN(_islandReference))
                {
                    double snow = probe.SnowAtBody(i);
                    if (!double.IsNaN(snow))
                    {
                        if (!_inIsland[i] && snow >= _enterAt)
                        {
                            _inIsland[i] = true;
                            var w = new Json.Writer(indent: false);
                            w.BeginObject();
                            w.Field("t", t);
                            w.Field("type", "islandEntered");
                            w.Field("member", i);
                            w.Field("snow", snow);
                            w.EndObject();
                            _events.WriteLine(w.ToString());
                        }
                        else if (_inIsland[i] && snow <= _exitAt) _inIsland[i] = false;
                    }
                }
            }

            if (probe.Steps % _stepsPerSample == 0) Frame(probe);
        }

        public void End(EpisodeProbe probe)
        {
            FinalDigest = probe.SolverDigest();
            Ended = true;
            Close();
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(_framesPath)), "leaves.json"),
                LeavesJson(_leafOrder.ConvertAll(id => _leaves[id]), _lastFrameSeconds, Math.Max(0, _leafFrameFloats)), new UTF8Encoding(false));
        }

        // ------------------------------------------------------------------ the leaves

        /// <summary>One body that is not a member, as the frames saw it: first and last, and how far its root went from where it was first seen.</summary>
        public sealed class LeafTrack
        {
            public long Id;
            public string GenomeText;
            public double FirstSeconds, LastSeconds, MaxDriftMetres;
            public LinkPose[] First, Last;

            /// <summary>Where its poses start in a frame of <c>leaf-frames.bin</c>, in floats; -1 for a body first seen after the first frame, which the file does not carry.</summary>
            public int Column = -1;

            /// <summary>The links the file carries for it: those of its first frame.</summary>
            public int Links;
        }

        private readonly Dictionary<long, LeafTrack> _leaves = new Dictionary<long, LeafTrack>();
        private readonly List<long> _leafOrder = new List<long>();
        private double _lastFrameSeconds = double.NaN;
        private BinaryWriter _leafFrames;
        private int _leafFrameFloats = -1;

        /// <summary>
        /// At every frame, every body that is not a member and that the solver holds (the leaf tank's leaves): its first pose, its last, and the
        /// farthest its root has gone from the first; and, for the bodies of the first frame, every link's pose into <c>leaf-frames.bin</c>, a
        /// frame for each of <c>frames.bin</c>'s, NaN for a body gone or with another number of links. A read of the probe; nothing is written to the world.
        /// </summary>
        private void TrackLeaves(EpisodeProbe probe)
        {
            double t = probe.PhysicsSeconds;
            _lastFrameSeconds = t;
            var now = new Dictionary<long, LinkPose[]>();
            foreach (OtherBodyPose o in probe.OtherPoses())
            {
                if (o.Poses.Length == 0) continue;
                now[o.Id] = o.Poses;
                if (!_leaves.TryGetValue(o.Id, out LeafTrack track))
                {
                    _leaves[o.Id] = new LeafTrack { Id = o.Id, GenomeText = probe.GenomeTextOf(o.Id), FirstSeconds = t, LastSeconds = t, First = o.Poses, Last = o.Poses, Links = o.Poses.Length };
                    _leafOrder.Add(o.Id);
                    continue;
                }

                track.Last = o.Poses;
                track.LastSeconds = t;
                double dx = o.Poses[0].X - track.First[0].X, dy = o.Poses[0].Y - track.First[0].Y, dz = o.Poses[0].Z - track.First[0].Z;
                track.MaxDriftMetres = Math.Max(track.MaxDriftMetres, Math.Sqrt(dx * dx + dy * dy + dz * dz));
            }

            if (_leafFrameFloats < 0)
            {
                _leafFrameFloats = 0;
                foreach (long id in _leafOrder)
                {
                    _leaves[id].Column = _leafFrameFloats;
                    _leafFrameFloats += 7 * _leaves[id].Links;
                }

                string path = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(_framesPath)), "leaf-frames.bin");
                _leafFrames = new BinaryWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 16));
            }

            if (_leafFrames == null) return;
            foreach (long id in _leafOrder)
            {
                LeafTrack track = _leaves[id];
                if (track.Column < 0) continue;
                bool here = now.TryGetValue(id, out LinkPose[] poses) && poses.Length == track.Links;
                for (int l = 0; l < track.Links; l++)
                {
                    if (!here)
                    {
                        for (int k = 0; k < 7; k++) _leafFrames.Write(float.NaN);
                        continue;
                    }

                    LinkPose p = poses[l];
                    _leafFrames.Write((float)p.X); _leafFrames.Write((float)p.Y); _leafFrames.Write((float)p.Z);
                    _leafFrames.Write((float)p.Qx); _leafFrames.Write((float)p.Qy); _leafFrames.Write((float)p.Qz); _leafFrames.Write((float)p.Qw);
                }
            }
        }

        /// <summary>
        /// <c>leaves.json</c>: each body that was not a member, in the order the frames first saw it, with its genome, its links' poses when first
        /// and last seen (x, y, z, then the quaternion x, y, z, w, world metres, as the frames write a member's), and, for one gone before the
        /// last frame, the last frame it was seen in: it left the world between that frame and the next.
        /// </summary>
        public static string LeavesJson(IReadOnlyList<LeafTrack> leaves, double lastFrameSeconds, int leafFrameFloats = 0)
        {
            var w = new Json.Writer(indent: true);
            w.BeginObject();
            w.Field("format", 1);
            w.Field("note", "every living body that was not a member, read at each frame: its genome, its links' poses (x, y, z, qx, qy, qz, qw) when first and last seen, the farthest its root went from the first, and, if it left the world, the last frame it was in");
            w.Field("leafFrames", "leaf-frames.bin");
            w.Field("leafFrameFloats", leafFrameFloats);
            w.Field("leafFramesEncoding", "float32 little-endian, frame-major, one frame for each of frames.bin's at the same t: for each leaf with a column, from that column, each of its links' x, y, z, qx, qy, qz, qw; NaN when it is gone");
            w.Field("leafCount", leaves.Count);
            w.BeginArray("leaves");
            foreach (LeafTrack l in leaves)
            {
                w.BeginObject();
                w.Field("id", l.Id);
                w.Field("firstSeen", l.FirstSeconds);
                w.Field("lastSeen", l.LastSeconds);
                if (l.LastSeconds < lastFrameSeconds) w.Field("goneAfter", l.LastSeconds); else w.Raw("goneAfter", "null");
                w.Field("maxDriftMetres", l.MaxDriftMetres);
                w.Field("column", l.Column);
                w.Field("links", l.Links);
                w.Raw("first", Poses(l.First));
                w.Raw("last", Poses(l.Last));
                w.Raw("genome", l.GenomeText);
                w.EndObject();
            }
            w.EndArray();
            w.EndObject();
            return w.ToString().Replace("\r\n", "\n") + "\n";
        }

        private static string Poses(LinkPose[] poses)
        {
            var sb = new StringBuilder("[");
            for (int i = 0; i < poses.Length; i++)
            {
                LinkPose p = poses[i];
                if (i > 0) sb.Append(", ");
                sb.Append('[').Append(string.Join(", ", new[] { p.X, p.Y, p.Z, p.Qx, p.Qy, p.Qz, p.Qw }.Select(v => v.ToString("R", Inv)))).Append(']');
            }
            return sb.Append(']').ToString();
        }

        public void Dispose() => Close();

        private void Close()
        {
            _frames?.Flush();
            _frames?.Dispose();
            _framesFile?.Dispose();
            _events?.Flush();
            _events?.Dispose();
            _frames = null;
            _framesFile = null;
            _events = null;
            _leafFrames?.Flush();
            _leafFrames?.Dispose();
            _leafFrames = null;
        }

        // ------------------------------------------------------------------ the islands

        /// <summary>
        /// The scan of the bed for snow islands, read once at the start before any body has eaten any (<see cref="IslandScan.Run"/>). A body is "in an
        /// island" from a quarter of the richest snow the scan found up to a tenth of it down. The one scan serves every patch of the world: the grid
        /// reads the snow by position, not by patch.
        /// </summary>
        private void ScanForIslands(EpisodeProbe probe)
        {
            Islands = IslandScan.Run(probe.TankRadiusMetres, probe.WorldDepthMetres, probe.HorizontalPatches, (x, y, z) => probe.SnowAt(x, y, z));
            IslandRule = Islands.Rule;
            if (!Islands.Read) return;
            _islandReference = Islands.Richest;
            _enterAt = Islands.EnterAt;
            _exitAt = Islands.ExitAt;
        }

        // ------------------------------------------------------------------ one frame

        private void Frame(EpisodeProbe probe)
        {
            if (_layout == null) BuildLayout(probe);
            TrackLeaves(probe);

            int written = 0;
            Put(ref written, (float)probe.ClockSeconds);
            for (int i = 0; i < _layout.Length; i++) WriteMember(probe, i, _layout[i], ref written);
            for (int c = 0; c < _placedJoules.Length; c++)
            {
                CorpseView v = probe.PlacedCorpse(c);
                Put(ref written, (float)v.X);
                Put(ref written, (float)v.Y);
                Put(ref written, (float)v.Z);
                Put(ref written, (float)v.Joules);
            }
            if (written != Channels.Count)
                throw new InvalidOperationException($"a frame wrote {written} values and the layout names {Channels.Count} channels");
            FrameCount++;
        }

        private void Put(ref int written, float value)
        {
            _frames.Write(value);
            written++;
        }

        private void WriteMember(EpisodeProbe probe, int i, MemberLayout m, ref int written)
        {
            bool alive = probe.Alive(i);
            Put(ref written, alive ? 1f : 0f);
            Put(ref written, alive ? (float)probe.Reserve(i) : float.NaN);
            Put(ref written, alive ? (float)probe.Tissue(i) : float.NaN);
            Put(ref written, alive ? (float)probe.Gestation(i) : float.NaN);
            EnergyLedger l = probe.Ledger(i);
            Put(ref written, alive ? l.FoodIncome : float.NaN);
            Put(ref written, alive ? l.LightIncome : float.NaN);
            Put(ref written, alive ? l.Work : float.NaN);
            Put(ref written, alive ? l.Upkeep : float.NaN);
            Put(ref written, alive ? l.Neural : float.NaN);
            Put(ref written, alive ? l.Handling : float.NaN);
            Put(ref written, alive ? l.Exuded : float.NaN);
            Put(ref written, alive ? (float)probe.SnowAtBody(i) : float.NaN);

            for (int j = 0; j < m.Links; j++)
            {
                if (alive && probe.TryLinkPose(i, j, out LinkPose p))
                {
                    Put(ref written, (float)p.X); Put(ref written, (float)p.Y); Put(ref written, (float)p.Z);
                    Put(ref written, (float)p.Qx); Put(ref written, (float)p.Qy); Put(ref written, (float)p.Qz); Put(ref written, (float)p.Qw);
                }
                else for (int k = 0; k < 7; k++) Put(ref written, float.NaN);
            }

            for (int d = 0; d < m.Dofs; d++) Put(ref written, alive ? probe.Drive(i, d) : float.NaN);
            for (int d = 0; d < m.Dofs; d++) Put(ref written, alive ? probe.Bend(i, d) : float.NaN);

            int hidden = m.Units - m.OutputUnits;
            if (hidden > 0)
            {
                if (alive && probe.TryBrainUnits(i, out float[] units, out _, out int outputs) && units.Length - outputs >= hidden)
                    for (int k = 0; k < hidden; k++) Put(ref written, units[outputs + k]);
                else for (int k = 0; k < hidden; k++) Put(ref written, float.NaN);
            }

            // A sense on a link the body no longer has (a body rebuilt on another plan, or one that has left the world) is NaN, never the solver's cached value.
            int linksNow = alive ? probe.LinkCount(i) : 0;
            for (int s = 0; s < m.Senses.Length; s++)
                Put(ref written, m.Senses[s].Part < linksNow ? probe.Sense(i, m.Senses[s]) : float.NaN);
        }

        private void BuildLayout(EpisodeProbe probe)
        {
            int n = probe.MemberCount;
            _layout = new MemberLayout[n];
            Channels.Add(new FrameChannel { Name = "t", Unit = "s" });
            for (int i = 0; i < n; i++)
            {
                var m = new MemberLayout();
                _layout[i] = m;
                string p = "m" + i.ToString(Inv) + ".";
                if (probe.LinkCount(i) > 0)
                {
                    m.Links = Math.Max(probe.LinkCount(i), probe.AdultPartCount(i));
                    m.Dofs = probe.OutputCount(i);
                    if (probe.TryBrainUnits(i, out float[] units, out bool central, out int outputUnits))
                    {
                        m.Units = units.Length;
                        m.OutputUnits = outputUnits;
                        m.Central = central;
                    }
                    m.Senses = new List<SenseSpec>(probe.SenseSpecs(i)).ToArray();
                }

                Add(p + "alive", "flag");
                Add(p + "reserve", "J");
                Add(p + "tissue", "J");
                Add(p + "gestation", "J");
                Add(p + "ledger.food", "J per economy step");
                Add(p + "ledger.light", "J per economy step");
                Add(p + "ledger.work", "J per economy step");
                Add(p + "ledger.upkeep", "J per economy step");
                Add(p + "ledger.neural", "J per economy step");
                Add(p + "ledger.handling", "J per economy step");
                Add(p + "ledger.exuded", "J per economy step");
                Add(p + "snow", "J/m3");
                string[] axes = { "x", "y", "z", "qx", "qy", "qz", "qw" };
                for (int j = 0; j < m.Links; j++)
                    for (int k = 0; k < 7; k++) Add(p + "link" + j.ToString(Inv) + "." + axes[k], k < 3 ? "m" : "quaternion");
                for (int d = 0; d < m.Dofs; d++) Add(p + "drive" + d.ToString(Inv), "power, -1 to 1");
                for (int d = 0; d < m.Dofs; d++) Add(p + "bend" + d.ToString(Inv), "bend, -1 to 1");
                for (int k = 0; k < m.Units - m.OutputUnits; k++)
                    Add(p + (m.Central ? "hidden" : "neuron") + k.ToString(Inv), "unit output");
                foreach (SenseSpec s in m.Senses)
                {
                    string name = p + "sense." + s.Channel + ".p" + s.Part.ToString(Inv);
                    if (s.Channel == SensorChannel.Flow || s.Channel == SensorChannel.LeafScentDirection) name += ".axis" + s.Index.ToString(Inv);
                    else if (s.Channel == SensorChannel.JointAngle || s.Channel == SensorChannel.JointAngularVelocity || s.Channel == SensorChannel.Stroke
                             || s.Channel == SensorChannel.Hold || s.Channel == SensorChannel.Ramp) name += ".dof" + s.Index.ToString(Inv);
                    Add(name, "normalised, about -1 to 1");
                }
            }
            for (int c = 0; c < _placedJoules.Length; c++)
            {
                string p = "corpse" + c.ToString(Inv) + ".";
                Add(p + "x", "m");
                Add(p + "y", "m");
                Add(p + "z", "m");
                Add(p + "joules", "J");
            }
        }

        private void Add(string name, string unit) => Channels.Add(new FrameChannel { Name = name, Unit = unit });
    }
}
