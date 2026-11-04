using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Evosim.Core;
using Evosim.Sim;
using Debug = UnityEngine.Debug;

namespace Evosim.Theatre
{
    /// <summary>
    /// A nursery tank's recorded frames played back: every swimmer's and every leaf's link poses
    /// at every frame where the nursery's solver put them, drawn from the pool's genomes, with
    /// nothing stepped (<c>Evosim.Nursery --restep</c> wrote the files; this is the theatre's
    /// counterpart of <see cref="FilmWindowWorld"/> for them).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The files.</b> A tank folder holds <c>episode.json</c> (the channel list, the members,
    /// the request), <c>frames.bin</c> (float32 little-endian, frame-major, one frame is
    /// <c>frameFloats</c> values in the channel order), <c>events.jsonl</c>, <c>result.json</c>
    /// and, when the restep recorded them, <c>episode-config.json</c> (the episode's whole
    /// <see cref="RunConfig"/>), <c>leaves.json</c> and <c>leaf-frames.bin</c>. Every channel is
    /// found by its name and never by its offset, because two tanks differ in width.
    /// </para>
    /// <para>
    /// <b>A recorded pose is the view's pose.</b> A link's recorded place is the origin of its own
    /// frame, which is the developed part's centre, and its recorded attitude is the link's world
    /// rotation. <see cref="LiveWorldView.Place"/> hangs a body from its root at <c>root</c> and
    /// puts part <c>i</c> at <c>positions[i]</c> turned by <c>rotations[i]</c>, so the root is link
    /// 0's place, <c>positions[i]</c> is link i's place less link 0's, and <c>rotations[i]</c> is
    /// link i's quaternion. Where <see cref="FilmWindowWorld"/> rebuilds those from joint
    /// coordinates (<see cref="RecordedPoses.Apply"/>), the nursery recorded the poses
    /// themselves, so no joint is solved here.
    /// </para>
    /// <para>
    /// <b>Time.</b> A film second that lands on a recorded frame (within <c>_tolerance</c>, 0.2 ms, which
    /// is about ten times the error of the float32 clock the tank records) shows that frame exactly. Between
    /// two frames the earlier frame's places are lerped to the later one's and its attitudes
    /// slerped, and <see cref="Interpolated"/> turns true and stays true. A body missing from
    /// either frame is shown at the earlier frame's pose or not at all, never blended. Until a film has
    /// taken over (<see cref="NoteFilmRate"/>) the world shows nothing the runner's own playback clock
    /// asks for, because that clock runs while the film sets up and would move the film's first second
    /// off the recorded frames; the world then shows the second it opened at, and the film starts there.
    /// </para>
    /// <para>
    /// <b>Ids.</b> A swimmer's id is its slot. A leaf's id is <see cref="LeafIdOffset"/> plus its
    /// id in <c>leaves.json</c>, so no leaf shares a key with a swimmer in the view, the palette
    /// or the pattern cache. Swimmers come first in <see cref="IdAt"/>'s order and the leaves
    /// after them.
    /// </para>
    /// <para>
    /// <b>What it cannot show.</b> Every body is painted sated (the recorded reserve is joules, and
    /// the tint wants seconds of upkeep, which the frame does not carry). A swimmer is drawn at its
    /// developed size, and the open reports how far the recorded link spacing is from it (a swimmer
    /// measures 0.985 to 1.000). A leaf with two links or more is drawn at its recorded size, its
    /// recorded link spacing over its developed spacing at each frame, to the nearest hundredth,
    /// because a leaf can still be growing in the tank; the frames carry no tissue or body fraction
    /// for a leaf, so the spacing is the only size they give. A leaf with one link has no spacing and
    /// is drawn at developed size, and the open says so.
    /// </para>
    /// </remarks>
    public sealed class NurseryEpisodeWorld : IDisposable, IFilmWorld
    {
        /// <summary>Added to a leaf's id in <c>leaves.json</c> to make its id here, clear of the swimmers' slots.</summary>
        public const long LeafIdOffset = 1000L;

        /// <summary>The root a relative <c>pool</c>, <c>recipe</c> or <c>world.config</c> path in the tank is read against.</summary>
        public const string RootVariable = "EVOSIM_THEATRE_NURSERY_ROOT";

        private sealed class Body
        {
            public long Id;
            public bool IsLeaf;
            public string Name;

            public float[] Data;
            public int FrameFloats;

            /// <summary>Channel of <c>alive</c> (swimmers), or -1 for a leaf, whose only sign of life is a pose that is not NaN.</summary>
            public int Alive = -1;

            /// <summary>Channel of <c>reserve</c>, or -1.</summary>
            public int Reserve = -1;

            /// <summary>Seven channels a link: x, y, z, qx, qy, qz, qw.</summary>
            public int[] Channel;

            public int Links;
            public Genome Genome;
            public Phenotype Phenotype;
            public bool Absorptive;
            public bool Photosynthetic;

            public Vector3[] World, WorldLater, Positions;
            public Quaternion[] Turn, TurnLater, Rotations;

            // nursery2: a leaf at its recorded size, and a swimmer named by its pool row.
            /// <summary>The sum of the developed link distances from link 0, which a recorded spacing is read against; 0 for a single-link body.</summary>
            public double DevelopedSpacing;

            /// <summary>A leaf's phenotype scaled to a hundredth of its developed size, by that hundredth.</summary>
            public Dictionary<int, Phenotype> Sized;

            /// <summary>The row of the request's pool a swimmer was made from (<c>members[].pool</c>), or -1 for a leaf.</summary>
            public int PoolIndex = -1;
        }

        private struct Drawn
        {
            public long Id;
            public Body Body;
            public Vector3 Centre;
            public double ReserveJoules;

            /// <summary>The phenotype the body is drawn as: its own, or for a leaf the one scaled to its recorded size.</summary>
            public Phenotype Phenotype;
        }

        private readonly List<Body> _swimmers = new List<Body>();
        private readonly List<Body> _leaves = new List<Body>();
        private readonly Dictionary<long, Body> _byId = new Dictionary<long, Body>();
        private readonly List<Drawn> _drawn = new List<Drawn>();
        private readonly Dictionary<long, int> _indexOf = new Dictionary<long, int>();
        private readonly List<string> _leafRefusals = new List<string>();

        private float[] _frames;
        private int _frameFloats;
        private double _rate;
        private double _tolerance;
        private string _caption;
        private string _directory;

        private SimulationMode _previousMode;
        private bool _modeSet;
        private bool _rateNoted;
        private int _incomplete;

        // nursery2: the film's hold on the runner's clock, and the leaves outside the sizes a leaf may be drawn at.
        private bool _filmDrives;
        private bool _filmStarted;
        private int _heldShows;
        private int _leafOutOfRange;

        // ---------------------------------------------------------------- what is on screen

        /// <summary>The tank folder.</summary>
        public string TankDirectory => _directory;

        /// <summary>The bodies, drawn through the live mode's view, fed rather than synced.</summary>
        public LiveWorldView View { get; private set; }

        /// <summary>A record built for the tank: its config, its seed (the episode's), its folder. See <see cref="MakeRecord"/>.</summary>
        public RunRecord Record { get; private set; }

        /// <summary>The tank's floor, built from the config as a farm window builds it (the relief is the config's).</summary>
        public BedShape Bed { get; private set; }

        /// <summary>Null: the tank has no reef (<c>reefCover</c> 0).</summary>
        public ReefGeometry Reefs { get; private set; }

        /// <summary>Null: <see cref="TheatreCoralRock.PlaceFor"/> returns none for a config whose reef shape is not coral.</summary>
        public CoralRock Coral { get; private set; }

        /// <summary>The word every label carries.</summary>
        public string ProvenanceWord => "nursery replay";

        /// <summary>The first line of every label: the word, the caption and, once a blended frame has been shown, the interpolation clause.</summary>
        public string ProvenanceLine =>
            "NURSERY REPLAY · " + _caption + (Interpolated ? " · interpolated between recorded frames" : "");

        /// <summary>What the film's first line says this is, with the interpolation clause once it has been used.</summary>
        public string Caption => _caption;

        /// <summary>True from the first frame shown between two recorded ones, and for good; false again when a film takes over (<see cref="NoteFilmRate"/>).</summary>
        public bool Interpolated { get; private set; }

        /// <summary>The earlier recorded frame of the pose on screen, or -1 before the first show.</summary>
        public int FrameIndex { get; private set; } = -1;

        /// <inheritdoc />
        public double Second { get; private set; } = double.NaN;

        public int FrameCount { get; private set; }

        public double FirstSecond => FrameCount > 0 ? T(0) : double.NaN;

        public double LastSecond => FrameCount > 0 ? T(FrameCount - 1) : double.NaN;

        /// <summary>Recorded frames a second.</summary>
        public double SampleRate => _rate;

        /// <summary>The physics step the tank ran at, s (<c>dt</c> in <c>episode.json</c>).</summary>
        public double StepSeconds { get; private set; }

        public int SwimmerCount => _swimmers.Count;

        public int LeafCount => _leaves.Count;

        /// <summary>Leaves in <c>leaves.json</c> not drawn because their developed part count is not their links.</summary>
        public int LeavesRefused => _leafRefusals.Count;

        /// <summary>Bodies in the last frame that were present but had a NaN link, left out.</summary>
        public int Incomplete => _incomplete;

        public long FramesShown { get; private set; }

        /// <summary>The skin's palette. Set before the first frame.</summary>
        public TheatrePalette Palette
        {
            get => View.Palette;
            set
            {
                View.Palette = value;
                View.VelocityOf = VelocityOf;
                View.SecondOf = () => Second;
            }
        }

        public bool ColourByCellType
        {
            get => View.ColourByCellType;
            set => View.ColourByCellType = value;
        }

        private NurseryEpisodeWorld() { }

        // ---------------------------------------------------------------- opening

        /// <summary>Whether a directory holds a nursery tank (an <c>episode.json</c> and a <c>frames.bin</c>).</summary>
        public static bool IsTank(string directory) =>
            !string.IsNullOrEmpty(directory) &&
            File.Exists(Path.Combine(directory, "episode.json")) &&
            File.Exists(Path.Combine(directory, "frames.bin"));

        /// <summary>Opens a tank folder. Null, with the reason, when it cannot be drawn.</summary>
        public static NurseryEpisodeWorld Open(string directory, out string refusal)
        {
            refusal = null;
            var world = new NurseryEpisodeWorld();

            try
            {
                world.Load(directory);
            }
            catch (InvalidOperationException e)
            {
                refusal = e.Message;
            }
            catch (Exception e)
            {
                refusal = "the tank at " + directory + " cannot be opened: " + e.GetType().Name + ": " + e.Message;
            }

            if (refusal != null)
            {
                world.Dispose();
                return null;
            }

            return world;
        }

        private static Exception Refuse(string why) => new InvalidOperationException(why);

        private double T(int frame) => _frames[(long)frame * _frameFloats];

        private void Load(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                throw Refuse("the nursery tank folder '" + directory + "' does not exist (EVOSIM_THEATRE_NURSERY).");
            }

            _directory = directory;
            var inv = CultureInfo.InvariantCulture;

            string episodePath = Path.Combine(directory, "episode.json");
            string framesPath = Path.Combine(directory, "frames.bin");
            if (!File.Exists(episodePath)) throw Refuse("no episode.json in " + directory + ".");
            if (!File.Exists(framesPath)) throw Refuse("no frames.bin in " + directory + ".");

            JsonNode episode = Json.Parse(File.ReadAllText(episodePath));
            int format = episode["format"].AsInt();
            if (format != 1) throw Refuse("episode.json is format " + format + ", this build reads 1.");

            _rate = episode["sampleRate"].AsDouble();
            FrameCount = episode["frames"].AsInt();
            _frameFloats = episode["frameFloats"].AsInt();
            StepSeconds = episode["dt"].AsDouble();

            if (!(_rate > 0d) || FrameCount < 1 || _frameFloats < 1 || !(StepSeconds > 0d))
            {
                throw Refuse("episode.json gives sampleRate " + _rate + ", " + FrameCount + " frame(s) of " + _frameFloats + " float(s), dt " + StepSeconds + ".");
            }

            _tolerance = Math.Min(2e-4d, 0.01d / _rate);

            // nursery2: the snap tolerance is the one above: a second within it of a recorded frame shows that frame,
            // 0.2 ms at the usual rates and never over 1% of a sample period. The tank's clock is float32 and its
            // frames sit off their exact multiples of the period by up to 2.1e-5 s (the ulp of a 300 s clock is
            // 3.1e-5 s), so the tolerance is about ten times the worst error the files carry, and 0.2 ms of a 1 m/s swimmer is
            // 0.2 mm. A film that starts on a recorded frame at a rate that divides the recorded one therefore never
            // blends. A film is running in this process when theatre-film.ps1 has set its shots; that decides whether
            // the runner's clock is held until the film takes over (ShowAt).
            _filmDrives = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EVOSIM_THEATRE_FILM_SHOTS"));

            // ---- channels, by name
            var channels = new Dictionary<string, int>(StringComparer.Ordinal);
            int count = 0;
            foreach (JsonNode channel in episode["channels"].Items())
            {
                channels[channel["name"].AsString()] = count++;
            }

            if (count != _frameFloats) throw Refuse("episode.json lists " + count + " channel(s) and says a frame is " + _frameFloats + " floats.");
            if (!channels.TryGetValue("t", out int clock) || clock != 0) throw Refuse("episode.json's first channel is not t.");

            _frames = ReadFloats(framesPath, (long)FrameCount * _frameFloats, "frames.bin");

            for (int k = 1; k < FrameCount; k++)
            {
                if (!(T(k) > T(k - 1)))
                {
                    throw Refuse("frames.bin's clock does not rise at frame " + k + " (" + T(k - 1) + " then " + T(k) + ").");
                }
            }

            // ---- the config
            RunConfig config = LoadConfig(episode, out string configHow, out List<string> filled);

            string wantedHash = episode.OptionalString("episodeConfigHash", null);
            string hash = config.Hash();
            string hashSaid = wantedHash == null
                ? "episode.json names no episodeConfigHash"
                : string.Equals(wantedHash, hash, StringComparison.OrdinalIgnoreCase)
                    ? "agrees with episode.json's episodeConfigHash"
                    : "differs from episode.json's episodeConfigHash " + wantedHash +
                      (filled.Count > 0
                          ? ", as it must: this Core has the tunable(s) " + string.Join(", ", filled.ToArray()) +
                            " that the file's writer lacked, inserted at their documented off values, and the hash covers them"
                          : ", and nothing was inserted, so the file is not the episode's");
            Debug.Log("[Theatre] nursery: config from " + configHow + "; the hash this Core computes, " + hash + ", " + hashSaid);

            // ---- the record, the floor
            JsonNode request = episode["request"];
            string seedText = episode["episodeSeed"].AsString();
            if (!ulong.TryParse(seedText, NumberStyles.None, inv, out ulong seed))
            {
                throw Refuse("episode.json's episodeSeed '" + seedText + "' is not an unsigned whole number.");
            }

            string folder = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            Record = MakeRecord(directory, config, seed, (float)StepSeconds, "nursery-" + folder, hash);

            try
            {
                SnapshotWorld.BuildFloor(Record, out BedShape bed, out ReefGeometry reefs);
                Bed = bed;
                Reefs = reefs;
                Coral = TheatreCoralRock.PlaceFor(Record, out _);
            }
            catch (Exception e)
            {
                throw Refuse("the tank's floor cannot be drawn from its config: " + e.GetType().Name + ": " + e.Message);
            }

            _caption = CaptionFor(request.OptionalString("recipe", null));

            // ---- the swimmers
            string pool = request.OptionalString("pool", null);
            if (string.IsNullOrEmpty(pool)) throw Refuse("episode.json's request names no pool, so there are no genomes to draw the bodies from.");

            string root = Environment.GetEnvironmentVariable(RootVariable);
            if (string.IsNullOrWhiteSpace(root))
            {
                throw Refuse(RootVariable + " is not set: the pool '" + pool + "' is a path relative to the repository root it was written in.");
            }

            string poolPath = Path.Combine(root, pool.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(poolPath)) throw Refuse("the pool " + poolPath + " does not exist.");

            var poolRows = new List<string>();
            foreach (string line in File.ReadAllLines(poolPath))
            {
                if (line.Trim().Length > 0) poolRows.Add(line);
            }

            foreach (JsonNode member in episode["members"].Items())
            {
                int slot = member["slot"].AsInt();
                int poolIndex = member["pool"].AsInt();
                int links = member["links"].AsInt();
                string name = "swimmer slot " + slot + " (pool " + poolIndex + ")";

                if (poolIndex < 0 || poolIndex >= poolRows.Count)
                {
                    throw Refuse(name + ": the pool " + poolPath + " holds " + poolRows.Count + " row(s).");
                }

                string row = poolRows[poolIndex];
                int tab = row.IndexOf('\t');
                if (tab < 0) throw Refuse(name + ": the pool row has no tab between its body index and its genome.");

                var body = new Body
                {
                    Id = slot,
                    Name = name,
                    Data = _frames,
                    FrameFloats = _frameFloats,
                    Links = links,
                    Channel = new int[7 * links],
                };

                string prefix = "m" + slot.ToString(inv) + ".";
                body.Alive = Channel(channels, prefix + "alive");
                body.Reserve = Channel(channels, prefix + "reserve");
                string[] axes = { "x", "y", "z", "qx", "qy", "qz", "qw" };

                for (int i = 0; i < links; i++)
                {
                    for (int a = 0; a < 7; a++) body.Channel[7 * i + a] = Channel(channels, prefix + "link" + i.ToString(inv) + "." + axes[a]);
                }

                DevelopBody(body, row.Substring(tab + 1), config, true);
                body.PoolIndex = poolIndex;
                _swimmers.Add(body);
                _byId[body.Id] = body;
            }

            if (_swimmers.Count == 0) throw Refuse("episode.json lists no members.");

            // ---- the leaves
            string leavesPath = Path.Combine(directory, "leaves.json");
            if (File.Exists(leavesPath)) LoadLeaves(leavesPath, config, episode);
            else Debug.Log("[Theatre] nursery: no leaves.json in " + directory + ", so no leaf is drawn.");

            // ---- ready
            View = new LiveWorldView(config, "Nursery Tank");

            _previousMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            _modeSet = true;

            Debug.Log(string.Format(
                inv,
                "[Theatre] NURSERY REPLAY · {0}: {1} frames at {2:0.###}/s ({3:0.###} s to {4:0.###} s, the physics step {5} s), {6} swimmer(s), {7} leaf(ves) drawn and {8} refused; " +
                "bed relief {9} m, reef cover {10}; nothing is simulated, every body stands where the nursery's solver put it",
                _caption, FrameCount, _rate, FirstSecond, LastSecond, StepSeconds, _swimmers.Count, _leaves.Count, _leafRefusals.Count,
                config.BedReliefMetres, config.ReefCover));

            foreach (string said in _leafRefusals) Debug.LogWarning("[Theatre] nursery: " + said);
            Debug.Log("[Theatre] nursery: " + GrowthReport());
            Debug.Log("[Theatre] nursery: " + LeafSizeReport());
            Debug.Log("[Theatre] nursery: " + RestPoseReport());
        }

        private static int Channel(Dictionary<string, int> channels, string name)
        {
            if (!channels.TryGetValue(name, out int at)) throw Refuse("episode.json lists no channel '" + name + "'.");
            return at;
        }

        private static float[] ReadFloats(string path, long expected, string label)
        {
            if (!BitConverter.IsLittleEndian) throw Refuse(label + " is little-endian float32 and this machine is not.");

            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.LongLength != expected * 4L)
            {
                throw Refuse(label + " is " + bytes.LongLength + " bytes and the episode says " + expected + " float32 (" + expected * 4L + " bytes).");
            }

            var floats = new float[expected];
            Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
            return floats;
        }

        /// <summary>Develops a body on the config's cell-type table and refuses one whose parts are not its links.</summary>
        private void DevelopBody(Body body, string genomeText, RunConfig config, bool swimmer)
        {
            try
            {
                body.Genome = GenomeJson.Read(genomeText);

                // The row's own plan when it carries one (a pool row does not); the lost parts are not applied.
                int[] counts = GenomeJson.ReadModuleCounts(genomeText);
                body.Phenotype = Developer.Develop(
                    body.Genome, config.Development, null, config.Shapes, counts, null, config.CellTypes);
            }
            catch (Exception e)
            {
                throw Refuse(body.Name + ": the genome cannot be read or developed: " + e.GetType().Name + ": " + e.Message);
            }

            if (body.Phenotype.PartCount != body.Links)
            {
                throw Refuse(
                    body.Name + ": develops into " + body.Phenotype.PartCount + " part(s) and the recording has " + body.Links +
                    " link(s); link index is part index, so the pose cannot be laid on it.");
            }

            body.Absorptive = Carries(body.Phenotype, CellTypeIds.Absorptive);
            body.Photosynthetic = Carries(body.Phenotype, CellTypeIds.Photosynthetic);
            body.World = new Vector3[body.Links];
            body.WorldLater = new Vector3[body.Links];
            body.Positions = new Vector3[body.Links];
            body.Turn = new Quaternion[body.Links];
            body.TurnLater = new Quaternion[body.Links];
            body.Rotations = new Quaternion[body.Links];
            body.DevelopedSpacing = DevelopedSpacingOf(body);
        }

        private static bool Carries(Phenotype phenotype, string cellTypeId)
        {
            foreach (PhenotypePart part in phenotype.Parts)
            {
                if (part.CellTypeId == cellTypeId) return true;
            }

            return false;
        }

        // ---------------------------------------------------------------- the leaves

        /// <summary>
        /// <c>leaves.json</c>: per leaf its <c>id</c>, <c>column</c> (the float where its links start in
        /// <c>leaf-frames.bin</c>), <c>links</c> and a <c>genome</c> that is a JSON object and not a
        /// string, so each is cut out of the file's text. <c>leaf-frames.bin</c> has the swimmers'
        /// frames, at the same <c>t</c> (assumed: the file carries none), and a leaf's slot is NaN
        /// when it is gone or its link count differs from its first frame's.
        /// </summary>
        private void LoadLeaves(string path, RunConfig config, JsonNode episode)
        {
            string text = File.ReadAllText(path);
            JsonNode file = Json.Parse(text);

            int leafFloats = file["leafFrameFloats"].AsInt();
            string framesName = file["leafFrames"].AsString();
            if (string.IsNullOrEmpty(framesName) || Path.GetFileName(framesName) != framesName)
            {
                throw Refuse("leaves.json names leafFrames '" + framesName + "', which is not a file beside it.");
            }

            string framesPath = Path.Combine(_directory, framesName);
            if (!File.Exists(framesPath)) throw Refuse("leaves.json names " + framesName + ", which is not in " + _directory + ".");

            float[] data = ReadFloats(framesPath, (long)FrameCount * leafFloats, framesName);

            JsonNode list = file["leaves"];
            List<string> genomes = GenomeTexts(text);
            if (genomes.Count != list.Count)
            {
                throw Refuse("leaves.json lists " + list.Count + " leaf(ves) and holds " + genomes.Count + " genome object(s).");
            }

            for (int n = 0; n < list.Count; n++)
            {
                JsonNode one = list[n];
                int id = one["id"].AsInt();
                int column = one["column"].AsInt();
                int links = one["links"].AsInt();
                string name = "leaf " + id;

                if (column < 0 || links < 1 || column + 7 * links > leafFloats)
                {
                    throw Refuse(name + ": column " + column + " with " + links + " link(s) does not fit a frame of " + leafFloats + " floats.");
                }

                var body = new Body
                {
                    Id = LeafIdOffset + id,
                    IsLeaf = true,
                    Name = name,
                    Data = data,
                    FrameFloats = leafFloats,
                    Links = links,
                    Channel = new int[7 * links],
                };

                for (int c = 0; c < body.Channel.Length; c++) body.Channel[c] = column + c;

                try
                {
                    DevelopBody(body, genomes[n], config, false);
                }
                catch (InvalidOperationException e)
                {
                    // A leaf that does not fit is left out and said; the swimmers are the film.
                    _leafRefusals.Add(e.Message);
                    continue;
                }

                if (_byId.ContainsKey(body.Id)) throw Refuse(name + ": its id " + body.Id + " is already taken.");
                _leaves.Add(body);
                _byId[body.Id] = body;
            }

            Debug.Log("[Theatre] nursery: leaves.json read, " + _leaves.Count + " of " + list.Count + " leaves drawable; their ids here are " + LeafIdOffset +
                      " plus theirs; a leaf is hidden on any frame where one of its floats is NaN (gone, or a different link count); " +
                      "leaf-frames.bin is taken to share frames.bin's clock, frame for frame");
        }

        /// <summary>The raw text of every <c>"genome": {...}</c> object in the file, in order.</summary>
        private static List<string> GenomeTexts(string text)
        {
            var found = new List<string>();
            var start = new Regex("\"genome\"\\s*:\\s*\\{");

            foreach (Match m in start.Matches(text))
            {
                int open = m.Index + m.Length - 1;
                int depth = 0;
                bool inString = false;
                int end = -1;

                for (int i = open; i < text.Length; i++)
                {
                    char c = text[i];

                    if (inString)
                    {
                        if (c == '\\') i++;
                        else if (c == '"') inString = false;
                        continue;
                    }

                    if (c == '"') inString = true;
                    else if (c == '{') depth++;
                    else if (c == '}' && --depth == 0) { end = i; break; }
                }

                if (end < 0) throw Refuse("leaves.json has a genome object that never closes.");
                found.Add(text.Substring(open, end - open + 1));
            }

            return found;
        }

        // ---------------------------------------------------------------- the config

        /// <summary>
        /// The episode's whole config: <c>episode-config.json</c> when the tank has it, otherwise the
        /// recipe's world config with the recipe's overrides, the closed floor and the physics step,
        /// as <c>Evosim.Nursery</c>'s <c>Recipe.EpisodeConfig</c> builds it (copied: Unity does not
        /// reference the nursery).
        /// </summary>
        private RunConfig LoadConfig(JsonNode episode, out string how, out List<string> filled)
        {
            filled = new List<string>();
            string own = Path.Combine(_directory, "episode-config.json");
            RunConfig config;

            if (File.Exists(own))
            {
                how = "episode-config.json";
                config = ReadRunConfig(File.ReadAllText(own), filled);
            }
            else
            {
                string root = Environment.GetEnvironmentVariable(RootVariable);
                if (string.IsNullOrWhiteSpace(root))
                {
                    throw Refuse("the tank has no episode-config.json, so the config is built from its recipe, and " + RootVariable + " is not set.");
                }

                JsonNode request = episode["request"];
                string recipePath = request.OptionalString("recipe", null);
                if (string.IsNullOrEmpty(recipePath)) throw Refuse("the tank has no episode-config.json and episode.json's request names no recipe.");

                string recipeFile = Path.Combine(root, recipePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(recipeFile)) throw Refuse("the recipe " + recipeFile + " does not exist.");

                JsonNode recipe = Json.Parse(File.ReadAllText(recipeFile));
                JsonNode world = recipe["world"];
                string configFile = Path.Combine(root, world["config"].AsString().Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(configFile)) throw Refuse("the recipe's world.config " + configFile + " does not exist.");

                how = "recipe " + recipePath + " (world.config, overrides, floor, step)";
                config = ReadRunConfig(File.ReadAllText(configFile), filled);

                if (world.Has("overrides")) ApplyOverrides(config, world["overrides"]);

                JsonNode setup = recipe["setup"];
                if (setup.Has("floorClosed") && setup["floorClosed"].AsBool()) config.MinimumPopulation = 0;

                config.PhysicsStepSeconds = setup["dt"].AsFloat();
            }

            if (config.PaceFactor != 1f)
            {
                throw Refuse("the config has PaceFactor " + config.PaceFactor.ToString(CultureInfo.InvariantCulture) +
                             ", and a nursery tank steps 0.5 s over dt physics steps to a metabolic step with no pace factor.");
            }

            return config;
        }

        /// <summary>
        /// Reads a config text with Core's strict reader after inserting the two tunables that the
        /// writers of these files lacked, <c>mouth.killSwallowsBody</c> (false) and
        /// <c>physics.paceFactor</c> (1, the value of every world recorded before it): both off, and
        /// both documented as the recorded world. Any other missing field is refused, all listed.
        /// </summary>
        private static RunConfig ReadRunConfig(string text, List<string> filled)
        {
            JsonNode root = Json.Parse(text);
            var missing = new List<string>();

            foreach (TunableEntry entry in ConfigSchema.Of(new RunConfig()))
            {
                if (root.Has(entry.Group) && root[entry.Group].Has(entry.Key)) continue;

                string path = entry.Group + "." + entry.Key;

                if (path == "mouth.killSwallowsBody") text = InsertField(text, "mouth", "killSwallowsBody", "false", filled);
                else if (path == "physics.paceFactor") text = InsertField(text, "physics", "paceFactor", "1", filled);
                else missing.Add(path);
            }

            if (missing.Count > 0)
            {
                throw Refuse("the config lacks " + missing.Count + " field(s) that this build's Core requires and that are not the two it knows how to fill: " +
                             string.Join(", ", missing.ToArray()) + ".");
            }

            return RunConfigJson.Read(text, out _);
        }

        private static string InsertField(string text, string group, string key, string value, List<string> filled)
        {
            var open = new Regex("\"" + group + "\"\\s*:\\s*\\{");
            MatchCollection hits = open.Matches(text);
            if (hits.Count != 1) throw Refuse("the config has " + hits.Count + " \"" + group + "\" objects, so '" + key + "' cannot be inserted.");

            filled.Add(group + "." + key + "=" + value);
            Match hit = hits[0];
            return text.Substring(0, hit.Index + hit.Length) + "\"" + key + "\":" + value + "," + text.Substring(hit.Index + hit.Length);
        }

        private static void ApplyOverrides(RunConfig config, JsonNode overrides)
        {
            var byPath = new Dictionary<string, TunableEntry>(StringComparer.Ordinal);
            foreach (TunableEntry entry in ConfigSchema.Of(config)) byPath[entry.Path] = entry;

            foreach (string key in overrides.Keys())
            {
                if (!byPath.TryGetValue(key, out TunableEntry found)) throw Refuse("the recipe's override '" + key + "' is not a tunable path.");

                try
                {
                    found.Set(OverrideValue(found.ValueType, overrides[key], key));
                }
                catch (InvalidOperationException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    throw Refuse("the recipe's override '" + key + "' was refused by the config: " + (e.InnerException ?? e).Message);
                }
            }
        }

        private static object OverrideValue(Type t, JsonNode v, string at)
        {
            if (t == typeof(float)) return Finite(v.AsFloat(), at);
            if (t == typeof(double)) return Finite(v.AsDouble(), at);

            if (t == typeof(int))
            {
                double d = v.AsDouble();
                if (d != Math.Floor(d)) throw Refuse("the recipe's override '" + at + "' is not a whole number.");
                return checked((int)d);
            }

            if (t == typeof(bool)) return v.AsBool();
            if (t == typeof(string)) return v.AsString();
            if (t.IsEnum) return EnumNamed(t, v.AsString(), at);

            if (t == typeof(string[]))
            {
                var list = new List<string>();
                foreach (JsonNode item in v.Items()) list.Add(item.AsString());
                return list.ToArray();
            }

            Type element = ConfigSchema.EnumElementOf(t);

            if (element != null)
            {
                var items = new List<JsonNode>(v.Items());
                Array array = Array.CreateInstance(element, items.Count);
                for (int i = 0; i < items.Count; i++) array.SetValue(EnumNamed(element, items[i].AsString(), at), i);
                return array;
            }

            throw Refuse("the recipe's override '" + at + "' is a tunable of type " + t.Name + ", which this reader cannot set.");
        }

        private static float Finite(float f, string at) =>
            float.IsNaN(f) || float.IsInfinity(f) ? throw Refuse("the recipe's override '" + at + "' is not finite.") : f;

        private static double Finite(double d, string at) =>
            double.IsNaN(d) || double.IsInfinity(d) ? throw Refuse("the recipe's override '" + at + "' is not finite.") : d;

        private static object EnumNamed(Type t, string name, string at)
        {
            if (name == null || Array.IndexOf(Enum.GetNames(t), name) < 0)
            {
                throw Refuse("the recipe's override '" + at + "' is '" + name + "', not one of " + string.Join(", ", Enum.GetNames(t)) + ".");
            }

            return Enum.Parse(t, name);
        }

        /// <summary>
        /// The caption under the provenance word. The nose-graft film's is the owner's wording;
        /// any other recipe is named and nothing more is claimed.
        /// </summary>
        private static string CaptionFor(string recipePath)
        {
            string name = string.IsNullOrEmpty(recipePath) ? "" : Path.GetFileNameWithoutExtension(recipePath);
            if (name == "nose-graft-film") return "grafted nose, not evolved; nursery tank, round-60 world";
            return "nursery tank, recipe " + (name.Length > 0 ? name : "unnamed");
        }

        /// <summary>
        /// A <see cref="RunRecord"/> for a tank. The class has no public way to make one outside a
        /// run directory, and its readers want a run's <c>run.json</c>, which a tank does not have,
        /// so the few properties the theatre reads are set through reflection: the config, the
        /// episode's seed (which is the seed of the tank's <c>World</c>, so the bed is the bed it
        /// ran over), the folder, the arm name, the hash, the step. The sample list is empty.
        /// </summary>
        private static RunRecord MakeRecord(string directory, RunConfig config, ulong seed, float dt, string arm, string hash)
        {
            var record = (RunRecord)Activator.CreateInstance(typeof(RunRecord), true);
            SetProperty(record, "Path", directory);
            SetProperty(record, "Config", config);
            SetProperty(record, "Seed", seed);
            SetProperty(record, "PhysicsDtSeconds", dt);
            SetProperty(record, "ArmName", arm);
            SetProperty(record, "ConfigHash", hash);
            SetProperty(record, "Status", "nursery replay");
            SetProperty(record, "Engine", "dynamics");
            SetProperty(record, "Samples", new RunSample[0]);
            return record;
        }

        private static void SetProperty(RunRecord record, string name, object value)
        {
            PropertyInfo property = typeof(RunRecord).GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo setter = property?.GetSetMethod(true);
            if (setter == null) throw Refuse("RunRecord has no settable property '" + name + "': it changed after this reader was written.");
            setter.Invoke(record, new[] { value });
        }

        // ---------------------------------------------------------------- the reports

        /// <summary>
        /// How far the recorded link spacing is from the developed body's: the sum of every link's
        /// distance from link 0 over the same sum in the development, at a spread of frames. A body
        /// that grew would drift from the first frame to the last; a swinging hinge moves it both ways.
        /// </summary>
        private string GrowthReport()
        {
            var sb = new StringBuilder("recorded link spacing over the developed body's (swimmers are drawn at developed size, leaves at their recorded size): ");
            sb.Append(SpacingOf(_swimmers, "swimmers"));
            if (_leaves.Count > 0) sb.Append("; ").Append(SpacingOf(_leaves, "leaves"));
            return sb.ToString();
        }

        private string SpacingOf(List<Body> bodies, string label)
        {
            var inv = CultureInfo.InvariantCulture;
            double lo = double.MaxValue, hi = double.MinValue, worstTrend = 0d;
            int measured = 0, single = 0;

            foreach (Body b in bodies)
            {
                if (b.Links < 2) { single++; continue; }

                double first = double.NaN, last = double.NaN;
                int step = Math.Max(1, FrameCount / 8);

                for (int k = 0; k < FrameCount; k += step)
                {
                    double r = SpacingRatio(b, k);
                    if (double.IsNaN(r)) continue;
                    if (double.IsNaN(first)) first = r;
                    last = r;
                    lo = Math.Min(lo, r);
                    hi = Math.Max(hi, r);
                }

                double lastFrame = SpacingRatio(b, FrameCount - 1);
                if (!double.IsNaN(lastFrame)) { last = lastFrame; lo = Math.Min(lo, lastFrame); hi = Math.Max(hi, lastFrame); }

                if (double.IsNaN(first) || double.IsNaN(last)) continue;
                measured++;
                worstTrend = Math.Max(worstTrend, Math.Abs(last - first));
            }

            if (measured == 0) return label + ": no body with two links to measure (" + single + " single-link)";

            return string.Format(
                inv, "{0}: {1} measured, ratio {2:0.000} to {3:0.000}, largest first-to-last change {4:0.000}, so {5} ({6} single-link bodies unmeasured)",
                label, measured, lo, hi, worstTrend, worstTrend > 0.02d ? "some of them may grow" : "they do not grow", single);
        }

        private double SpacingRatio(Body b, int k)
        {
            if (!Present(b, k)) return double.NaN;

            ReadPose(b, k, b.World, b.Turn);
            double recorded = 0d, developed = 0d;
            Vector3 devRoot = b.Phenotype.Parts[0].Position.ToVector3();

            for (int i = 1; i < b.Links; i++)
            {
                recorded += (b.World[i] - b.World[0]).magnitude;
                developed += (b.Phenotype.Parts[i].Position.ToVector3() - devRoot).magnitude;
            }

            return developed > 1e-6d ? recorded / developed : double.NaN;
        }

        /// <summary>The largest angle between each link's recorded attitude relative to link 0 and the developed rest attitude, at the first frame the body is present.</summary>
        private string RestPoseReport()
        {
            float worst = 0f;
            int checkedBodies = 0;

            foreach (Body b in _swimmers)
            {
                int k = -1;
                for (int f = 0; f < FrameCount && k < 0; f++) if (Present(b, f)) k = f;
                if (k < 0 || b.Links < 2) continue;

                ReadPose(b, k, b.World, b.Turn);
                Quaternion rest0 = b.Phenotype.Parts[0].Rotation.ToQuaternion();
                checkedBodies++;

                for (int i = 1; i < b.Links; i++)
                {
                    Quaternion recorded = Quaternion.Inverse(b.Turn[0]) * b.Turn[i];
                    Quaternion developed = Quaternion.Inverse(rest0) * b.Phenotype.Parts[i].Rotation.ToQuaternion();
                    worst = Mathf.Max(worst, Quaternion.Angle(recorded, developed));
                }
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "rest-pose check on {0} swimmer(s): the recorded link attitudes at their first frame sit within {1:0.#} degrees of the developed rest attitudes " +
                "(a joint that has already moved accounts for some of it; a large number would mean the quaternion convention differs)",
                checkedBodies, worst);
        }

        // ---------------------------------------------------------------- playback

        /// <summary>
        /// Shows the pose at a second: a recorded frame exactly when the second lands on one, and
        /// otherwise the two around it blended (<see cref="Interpolated"/>). Clamped to the
        /// recording. False for an empty one.
        /// </summary>
        public bool ShowAt(double second)
        {
            if (FrameCount == 0 || double.IsNaN(second)) return false;

            // nursery2: while a film sets up, the runner's own playback clock runs and asks for seconds that are
            // not the film's; the world keeps the frame it opened on until the film has taken over.
            if (_filmDrives && !_filmStarted && FramesShown > 0)
            {
                _heldShows++;
                return true;
            }

            int k = FrameAtOrBefore(second);

            if (k < 0)
            {
                Compose(0, 0, 0f, T(0), false);
                return true;
            }

            if (k >= FrameCount - 1 || second - T(k) <= _tolerance)
            {
                Compose(k, k, 0f, T(k), false);
                return true;
            }

            float u = (float)((second - T(k)) / (T(k + 1) - T(k)));
            Compose(k, k + 1, u, second, true);
            return true;
        }

        /// <summary>Shows one recorded frame exactly.</summary>
        public void Show(int frame)
        {
            if (frame < 0 || frame >= FrameCount)
            {
                throw new ArgumentOutOfRangeException(nameof(frame), frame, "The tank holds " + FrameCount + " frame(s).");
            }

            Compose(frame, frame, 0f, T(frame), false);
        }

        /// <summary>The last recorded frame at or before a second, allowing the exactness tolerance; -1 before the first.</summary>
        public int FrameAtOrBefore(double second)
        {
            int lo = 0, hi = FrameCount - 1, found = -1;
            double limit = second + _tolerance;

            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                if (T(mid) <= limit) { found = mid; lo = mid + 1; }
                else hi = mid - 1;
            }

            return found;
        }

        /// <summary>
        /// Logs once whether the film's frame rate divides the recorded one: if it does, every film
        /// frame lands on a recorded frame; if not, some are blended.
        /// </summary>
        /// <param name="filmFps">The film's frames a second.</param>
        /// <param name="speed">Seconds of the recording a second of film.</param>
        public void NoteFilmRate(double filmFps, double speed)
        {
            if (_rateNoted) return;
            _rateNoted = true;

            // nursery2: from here the film asks for every second it shows; whatever was shown before it is not film.
            _filmStarted = true;
            Interpolated = false;

            if (FrameCount > 0 && !double.IsNaN(Second))
            {
                int near = Math.Max(0, FrameAtOrBefore(Second));
                if (near + 1 < FrameCount && Math.Abs(T(near + 1) - Second) < Math.Abs(T(near) - Second)) near++;
                double off = Math.Abs(T(near) - Second);

                Debug.Log(string.Format(
                    CultureInfo.InvariantCulture,
                    "[Theatre] nursery: the film starts at {0:0.######} s, {1:0.######} s from recorded frame {2} (t {3:0.######} s); the snap tolerance is {4:0.######} s, so {5}; " +
                    "the runner's playback clock asked for {6} other second(s) while the film set up, and none was shown",
                    Second, off, near, T(near), _tolerance,
                    off <= _tolerance ? "the start is on a recorded frame" : "the start is OFF the recorded frames and film frames are blended",
                    _heldShows));
            }

            double perFrame = _rate * speed / filmFps;
            bool divides = perFrame >= 1d - 1e-9 && Math.Abs(perFrame - Math.Round(perFrame)) < 1e-6;

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "[Theatre] nursery: the film runs at {0:0.###} fps and the tank was recorded at {1:0.###}/s: {2}",
                filmFps, _rate,
                divides
                    ? "the film's rate divides the recorded rate, so every film frame shows a recorded frame exactly"
                    : "the film's rate does not divide the recorded rate, so film frames between recorded ones are blended (lerped places, slerped attitudes) and the caption says so"));
        }

        private void Compose(int ka, int kb, float u, double second, bool blending)
        {
            FrameIndex = ka;
            Second = second;
            FramesShown++;

            _drawn.Clear();
            _indexOf.Clear();
            _incomplete = 0;

            View.BeginFrame();
            DrawAll(_swimmers, ka, kb, u, blending);
            DrawAll(_leaves, ka, kb, u, blending);
            View.EndFrame();
        }

        private void DrawAll(List<Body> bodies, int ka, int kb, float u, bool blending)
        {
            for (int n = 0; n < bodies.Count; n++)
            {
                Body b = bodies[n];

                if (!Present(b, ka))
                {
                    if (b.Alive >= 0 && b.Data[(long)ka * b.FrameFloats + b.Alive] > 0.5f) _incomplete++;
                    continue;
                }

                ReadPose(b, ka, b.World, b.Turn);

                if (blending && kb != ka && Present(b, kb))
                {
                    ReadPose(b, kb, b.WorldLater, b.TurnLater);

                    for (int i = 0; i < b.Links; i++)
                    {
                        b.World[i] = Vector3.Lerp(b.World[i], b.WorldLater[i], u);
                        b.Turn[i] = Quaternion.Slerp(b.Turn[i], b.TurnLater[i], u);
                    }

                    Interpolated = true;
                }

                Vector3 root = b.World[0];
                for (int i = 0; i < b.Links; i++)
                {
                    b.Positions[i] = b.World[i] - root;
                    b.Rotations[i] = b.Turn[i];
                }

                // nursery2: a leaf drawn at the size its recorded pose has; a swimmer at its developed size, as before.
                Phenotype drawnAs = b.IsLeaf ? LeafSized(b) : b.Phenotype;

                // The skin pattern's key, from the genome (returns at once with its dial off).
                TheatrePattern.Register(b.Id, b.Genome);
                TheatreSkinMap.Register(b.Id, b.Genome, Record.Config); // EVOSIM_THEATRE_SKIN_MAP; returns at once with it off

                // Sated (the class remarks), whole and fully opaque: a tank has no births held on screen.
                if (!View.Place(b.Id, b.Phenotype, drawnAs, root, b.Positions, b.Rotations, 1f, 1f, 1f)) continue;

                _indexOf[b.Id] = _drawn.Count;
                _drawn.Add(new Drawn
                {
                    Id = b.Id,
                    Body = b,
                    Phenotype = drawnAs,
                    Centre = CentreOf(b, drawnAs, root),
                    ReserveJoules = b.Reserve >= 0 ? b.Data[(long)ka * b.FrameFloats + b.Reserve] : double.NaN,
                });
            }
        }

        private static bool Present(Body b, int frame)
        {
            long o = (long)frame * b.FrameFloats;

            if (b.Alive >= 0 && !(b.Data[o + b.Alive] > 0.5f)) return false;

            for (int i = 0; i < b.Channel.Length; i++)
            {
                float v = b.Data[o + b.Channel[i]];
                if (float.IsNaN(v) || float.IsInfinity(v)) return false;
            }

            return true;
        }

        private static void ReadPose(Body b, int frame, Vector3[] positions, Quaternion[] attitudes)
        {
            long o = (long)frame * b.FrameFloats;

            for (int i = 0; i < b.Links; i++)
            {
                int c = 7 * i;
                positions[i] = new Vector3(b.Data[o + b.Channel[c]], b.Data[o + b.Channel[c + 1]], b.Data[o + b.Channel[c + 2]]);

                float x = b.Data[o + b.Channel[c + 3]], y = b.Data[o + b.Channel[c + 4]];
                float z = b.Data[o + b.Channel[c + 5]], w = b.Data[o + b.Channel[c + 6]];
                float length = Mathf.Sqrt(x * x + y * y + z * z + w * w);

                attitudes[i] = length > 1e-6f
                    ? new Quaternion(x / length, y / length, z / length, w / length)
                    : Quaternion.identity;
            }
        }

        /// <summary>The volume-weighted centre of a posed body, in world metres.</summary>
        private static Vector3 CentreOf(Body b, Phenotype phenotype, Vector3 root)
        {
            Vector3 sum = Vector3.zero;
            float volume = 0f;

            for (int i = 0; i < b.Links; i++)
            {
                float v = Mathf.Max(1e-9f, phenotype.Parts[i].Volume);
                sum += b.Positions[i] * v;
                volume += v;
            }

            return root + (volume > 1e-9f ? sum / volume : Vector3.zero);
        }

        // ---------------------------------------------------------------- a leaf's recorded size, and the swimmers a film names

        /// <summary>The least and most hundredths of its developed size a leaf is drawn at; a spacing outside is left at developed size and counted.</summary>
        private const int LeastLeafStep = 10, MostLeafStep = 300;

        /// <summary>The sum of a body's developed link distances from link 0, which a recorded spacing is read against; 0 for a body of one link.</summary>
        private static double DevelopedSpacingOf(Body b)
        {
            if (b.Links < 2) return 0d;

            double sum = 0d;
            Vector3 devRoot = b.Phenotype.Parts[0].Position.ToVector3();

            for (int i = 1; i < b.Links; i++) sum += (b.Phenotype.Parts[i].Position.ToVector3() - devRoot).magnitude;

            return sum;
        }

        /// <summary>
        /// The hundredths of its developed size that a body's pose in <c>World</c> has: the recorded link
        /// spacing over the developed one, to the nearest hundredth. 100 for a body with no spacing to read.
        /// </summary>
        private static int SizeStepOf(Body b)
        {
            if (b.Links < 2 || !(b.DevelopedSpacing > 1e-6d)) return 100;

            double recorded = 0d;
            for (int i = 1; i < b.Links; i++) recorded += (b.World[i] - b.World[0]).magnitude;

            double ratio = recorded / b.DevelopedSpacing;
            if (double.IsNaN(ratio) || double.IsInfinity(ratio)) return 100;

            return (int)Math.Round(ratio * 100d);
        }

        /// <summary>A leaf's phenotype at the size its pose on screen (<c>World</c>) has, cached by hundredth so a growing leaf is rebuilt only when the size moves a step.</summary>
        private Phenotype LeafSized(Body b)
        {
            int step = SizeStepOf(b);
            if (step == 100) return b.Phenotype;

            if (step < LeastLeafStep || step > MostLeafStep)
            {
                _leafOutOfRange++;
                return b.Phenotype;
            }

            if (b.Sized == null) b.Sized = new Dictionary<int, Phenotype>();
            if (b.Sized.TryGetValue(step, out Phenotype found)) return found;

            Phenotype scaled;

            try
            {
                scaled = b.Phenotype.Scaled(step / 100f, Record.Config.Shapes);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Theatre] nursery: " + b.Name + " could not be scaled to " + step + "% of its developed size, so it is drawn at developed size: " + e.Message);
                scaled = b.Phenotype;
            }

            b.Sized[step] = scaled;
            return scaled;
        }

        /// <summary>One line for the log: how the leaves are sized, and the scale range applied over the whole recording.</summary>
        private string LeafSizeReport()
        {
            int multi = 0, single = 0, atOne = 0, scaledFrames = 0, outside = 0, seen = 0;
            int lo = int.MaxValue, hi = int.MinValue;

            foreach (Body b in _leaves)
            {
                if (b.Links < 2 || !(b.DevelopedSpacing > 1e-6d)) { single++; continue; }

                multi++;

                for (int k = 0; k < FrameCount; k++)
                {
                    if (!Present(b, k)) continue;

                    ReadPose(b, k, b.World, b.Turn);
                    int step = SizeStepOf(b);
                    seen++;

                    if (step < LeastLeafStep || step > MostLeafStep) { outside++; continue; }
                    if (step == 100) atOne++;
                    else scaledFrames++;

                    lo = Math.Min(lo, step);
                    hi = Math.Max(hi, step);
                }
            }

            string singles = single + " single-link leaf(ves) have no spacing and the tank records nothing else of a leaf's size (leaf-frames.bin holds poses only), so they are drawn at developed size";
            if (multi == 0 || seen == outside) return "leaves at their recorded size: no leaf with two links or more has a spacing to read; " + singles;

            return string.Format(
                CultureInfo.InvariantCulture,
                "leaves at their recorded size: {0} leaf(ves) with two links or more are scaled by their recorded link spacing over the developed spacing, at each frame to the nearest 0.01, " +
                "scale {1:0.00} to {2:0.00} over the recording ({3} of {4} leaf-frames away from 1.00, {5} outside {6:0.00} to {7:0.00} and left at developed size); {8}",
                multi, lo / 100d, hi / 100d, scaledFrames, seen, outside, LeastLeafStep / 100d, MostLeafStep / 100d, singles);
        }

        /// <summary>
        /// The body id of the swimmer made from a row of the request's pool (<c>members[].pool</c> in
        /// <c>episode.json</c>): a swimmer's id is its slot (<c>members[].slot</c>). False when no member came from that row.
        /// </summary>
        public bool TryMemberBody(int poolIndex, out long id)
        {
            id = -1L;

            foreach (Body b in _swimmers)
            {
                if (b.PoolIndex != poolIndex) continue;

                id = b.Id;
                return true;
            }

            return false;
        }

        /// <summary>The members as a film's refusal names them: slot, pool row and body id.</summary>
        public string MembersLine()
        {
            var sb = new StringBuilder();

            foreach (Body b in _swimmers)
            {
                if (sb.Length > 0) sb.Append("; ");
                sb.Append("pool member ").Append(b.PoolIndex).Append(" is body ").Append(b.Id);
            }

            return sb.ToString();
        }

        /// <summary>The index of a swimmer's nose part (its cell type is the nose's), or -1.</summary>
        public int NosePartIndex(long id)
        {
            if (!_byId.TryGetValue(id, out Body b)) return -1;

            for (int i = 0; i < b.Phenotype.PartCount; i++)
            {
                if (b.Phenotype.Parts[i].CellTypeId == CellTypeIds.Nose) return i;
            }

            return -1;
        }

        /// <summary>
        /// A drawn swimmer's nose in the pose on screen: the sensing tip in world metres (<c>MouthPoint.Local</c>, the
        /// point the pit is drawn at and the smell is read from, mapped through the part's recorded pose), the nose
        /// part's world attitude, the tip in the part's own frame, and the body's root (link 0). False when the body
        /// is not drawn or has no nose part.
        /// </summary>
        public bool TryNose(long id, out Vector3 tip, out Quaternion turn, out Vector3 localTip, out Vector3 root)
        {
            tip = Vector3.zero;
            turn = Quaternion.identity;
            localTip = Vector3.zero;
            root = Vector3.zero;

            if (!_indexOf.ContainsKey(id) || !_byId.TryGetValue(id, out Body b)) return false;

            int part = NosePartIndex(id);
            if (part < 0) return false;

            localTip = MouthPoint.Local(b.Phenotype, part).ToVector3();
            turn = b.Turn[part];
            root = b.World[0];
            tip = b.World[part] + turn * localTip;
            return true;
        }

        /// <summary>Dresses every body the palette has not reached yet: what a still needs before it is taken.</summary>
        public int DressAll() => View.DressUndressed();

        // ---------------------------------------------------------------- the frame (ITheatreFrame)

        public int BodyCount => _drawn.Count;

        public Vector3 PositionOf(int index) => _drawn[index].Centre;

        public Phenotype PhenotypeOf(int index) => _drawn[index].Phenotype;

        public bool AbsorptiveAt(int index) => _drawn[index].Body.Absorptive;

        public bool PhotosyntheticAt(int index) => _drawn[index].Body.Photosynthetic;

        /// <summary>A swimmer's slot, or a leaf's id plus <see cref="LeafIdOffset"/>; swimmers come first.</summary>
        public long IdAt(int index) => _drawn[index].Id;

        public bool TryIndexOf(long id, out int index) => _indexOf.TryGetValue(id, out index);

        /// <summary>A drawn body's root transform, or null: what a camera follows.</summary>
        public Transform RootOf(long id) => View.RootOf(id);

        // ---------------------------------------------------------------- the film's world (IFilmWorld)

        /// <summary>
        /// A body's root velocity at the frame on screen: the central difference of its recorded
        /// root over the frames either side (one-sided at the ends of the recording). Zero when the
        /// body is missing from either, or the id is not a body of the tank.
        /// </summary>
        public Vector3 VelocityOf(long id)
        {
            if (FrameIndex < 0 || !_byId.TryGetValue(id, out Body b)) return Vector3.zero;

            int k0 = Math.Max(0, FrameIndex - 1), k1 = Math.Min(FrameCount - 1, FrameIndex + 1);
            if (k1 <= k0) return Vector3.zero;
            if (!RootAtFrame(b, k0, out Vector3 a) || !RootAtFrame(b, k1, out Vector3 c)) return Vector3.zero;

            Vector3 v = (c - a) / (float)(T(k1) - T(k0));
            return IsFinite(v) ? v : Vector3.zero;
        }

        /// <summary>
        /// Where the recorded path carries a body's root from the frame on screen in
        /// <paramref name="seconds"/>: the recording's own path, held at its last frame past its end.
        /// Zero when the body is not a body of the tank or is not recorded at either end.
        /// </summary>
        public Vector3 DisplacementOf(long id, float seconds)
        {
            if (double.IsNaN(Second) || !_byId.TryGetValue(id, out Body b)) return Vector3.zero;
            if (!RootAtSecond(b, Second, out Vector3 a) || !RootAtSecond(b, Second + seconds, out Vector3 c)) return Vector3.zero;

            Vector3 d = c - a;
            return IsFinite(d) ? d : Vector3.zero;
        }

        /// <summary>
        /// A swimmer's reserve in joules at the frame on screen; no children are counted. False
        /// for a leaf (its reserve is not recorded) and for a body the frame does not draw.
        /// </summary>
        public bool TryAccount(long id, out double reserve, out int children)
        {
            reserve = double.NaN;
            children = 0;

            if (!_indexOf.TryGetValue(id, out int index)) return false;
            if (_drawn[index].Body.IsLeaf) return false;

            reserve = _drawn[index].ReserveJoules;
            return true;
        }

        /// <summary>An episode draws no remains, so none.</summary>
        public bool TryRemains(long id, out FilmRemains remains)
        {
            remains = default;
            return false;
        }

        /// <summary>An episode's close shot is never planned on a body born after it opens, so none.</summary>
        public bool TryUnborn(long id, out FilmUnborn unborn)
        {
            unborn = default;
            return false;
        }

        private bool RootAtFrame(Body b, int frame, out Vector3 root)
        {
            root = Vector3.zero;
            if (!Present(b, frame)) return false;

            long o = (long)frame * b.FrameFloats;
            root = new Vector3(b.Data[o + b.Channel[0]], b.Data[o + b.Channel[1]], b.Data[o + b.Channel[2]]);
            return true;
        }

        private bool RootAtSecond(Body b, double second, out Vector3 root)
        {
            root = Vector3.zero;
            if (FrameCount == 0) return false;

            second = Math.Max(FirstSecond, Math.Min(LastSecond, second));
            int k = Math.Max(0, FrameAtOrBefore(second));

            if (!RootAtFrame(b, k, out root)) return false;
            if (k >= FrameCount - 1 || second - T(k) <= _tolerance) return true;

            if (RootAtFrame(b, k + 1, out Vector3 later))
            {
                root = Vector3.Lerp(root, later, (float)((second - T(k)) / (T(k + 1) - T(k))));
            }

            return true;
        }

        private static bool IsFinite(Vector3 v) =>
            !float.IsNaN(v.x) && !float.IsInfinity(v.x) &&
            !float.IsNaN(v.y) && !float.IsInfinity(v.y) &&
            !float.IsNaN(v.z) && !float.IsInfinity(v.z);

        /// <summary>
        /// Two lines: what this is and how far to trust it, then which tank, when and how much is
        /// drawn. The first line is the provenance word, the caption and, once a blended frame has
        /// been shown, "interpolated between recorded frames".
        /// </summary>
        public string LabelFor(string view, string look)
        {
            string first = ProvenanceLine;

            int swimmers = 0, leaves = 0;
            foreach (Drawn d in _drawn)
            {
                if (d.Body.IsLeaf) leaves++;
                else swimmers++;
            }

            string extra = "";
            if (_leafRefusals.Count > 0) extra += "  " + _leafRefusals.Count + " leaves unmatched";
            if (_incomplete > 0) extra += "  " + _incomplete + " incomplete";

            return first + "\n" + string.Format(
                CultureInfo.InvariantCulture,
                "{0}  t={1:0.###}s  frame {2}/{3}  {4} swimmers, {5} leaves  {6}  {7}  recorded pose, leaves at recorded size{8}",
                Record.ArmName, Second, FrameIndex + 1, FrameCount, swimmers, leaves, view, look, extra);
        }

        /// <summary>One line for a log: the frame on screen and its counts.</summary>
        public string Describe() =>
            string.Format(
                CultureInfo.InvariantCulture,
                "nursery tank {0} t={1:0.###} s frame {2}/{3}: {4} drawn ({5} swimmers and {6} leaves in the tank), {7} built and {8} removed so far, {9} incomplete{10}",
                Record != null ? Record.ArmName : "?", Second, FrameIndex + 1, FrameCount, _drawn.Count, _swimmers.Count, _leaves.Count,
                View.Built, View.Removed, _incomplete, Interpolated ? "; interpolated between recorded frames" : "");

        // ---------------------------------------------------------------- housekeeping

        public void Dispose()
        {
            View?.Dispose();
            View = null;

            _drawn.Clear();
            _indexOf.Clear();
            _byId.Clear();
            _swimmers.Clear();
            _leaves.Clear();
            _frames = null;

            if (_modeSet)
            {
                Physics.simulationMode = _previousMode;
                _modeSet = false;
            }
        }
    }
}
