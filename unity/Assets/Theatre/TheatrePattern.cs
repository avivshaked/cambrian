using System;
using System.Collections.Generic;
using UnityEngine;
using Evosim.Core;

namespace Evosim.Theatre
{
    /// <summary>
    /// Reaction-diffusion skin patterns (<c>EVOSIM_THEATRE_PATTERN</c>, 0 off by default): a Gray-Scott system solved
    /// on a small periodic tile and kept as an R8 texture, which <c>TheatreBody.shader</c> maps onto every part as
    /// pigment (<c>_Pattern</c>, <c>_PatternShift</c>, <c>_PatternTex</c>, <c>PatternSkin</c>, <c>Pattern*</c>). Theatre
    /// only: nothing here is read by the simulation, and the shader moves no vertex and no light.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The key.</b> A creature's pattern is a function of three things read from its genome: the multiset of its
    /// nodes' cell types, its reproductive investment (rounded to <see cref="InvestmentStep"/>) and its reserve margin
    /// (a log bin, <see cref="MarginBins"/>). Kin share all three (round 62: a child carries each unchanged in about
    /// 92% of pairs), so a lineage keeps its pattern, and a mutation that crosses a step replaces it. Investment and
    /// margin also set the regime and the scale, through a smooth map (<see cref="Regime"/>).
    /// </para>
    /// <para>
    /// <b>A library of tiles, so the solves are bounded.</b> A real window holds over a thousand distinct keys, and a
    /// solve takes about a second, so a tile per key would cost hours. A tile is instead identified by two numbers:
    /// the regime's bin along the path spots, net, stripes (<see cref="RegimeBins"/>) and a seed bin from the key's hash
    /// (<see cref="SeedBins"/>). At most <see cref="RegimeBins"/> x <see cref="SeedBins"/> tiles are ever solved, each
    /// the first time a body needs it, and each kept for the process. What makes one body differ from another that
    /// shares its tile is placement, all taken from the full key's hash: an offset into the tile (u and v), a
    /// quarter-turn of the mapping (0 to 3), and the tile's length (the regime's scale). Kin share the key, so they
    /// share tile, offset and turn; strangers almost always differ in at least one.
    /// </para>
    /// <para>
    /// <b>Where the genome comes from.</b> <c>TheatrePalette.Paint</c> is handed a phenotype, which carries neither the
    /// investment nor the margin, so a caller that holds the genome says so with <see cref="Register"/>, once a body.
    /// A body never registered takes <see cref="FallbackKey"/>: its parts' cell types and the middle of both ranges.
    /// </para>
    /// <para>
    /// <b>Determinism.</b> The seed is a fixed integer hash, the draws are the project's PCG <see cref="Rng"/>, the loop
    /// is one thread in a fixed order, and no <c>System.Random</c> is used, so a tile solves to the same bytes every
    /// time and a frame rendered twice is the same frame.
    /// </para>
    /// </remarks>
    internal static class TheatrePattern
    {
        /// <summary>The dial, read once: 0 off, 1 on. Off, nothing below is created, solved or set.</summary>
        public static readonly bool Enabled = TheatreSkin.Dial("EVOSIM_THEATRE_PATTERN", 0f, 0f, 1f) > 0f;

        // ---------------------------------------------------------------- the key

        /// <summary>
        /// Investment's step in the key, as a share of a body's tissue value. Wide enough that kin round together
        /// (round 62's clades spread 0.014 to 0.06 of the window's range), narrow enough that a different strategy
        /// differs. In (0, 0.25].
        /// </summary>
        public const float InvestmentStep = 0.05f;

        /// <summary>
        /// The reserve margin's bins in the key: log(1 + margin) over its range, in this many steps, so a margin of a few
        /// seconds and one of a few hundred are told apart, and a margin that drifts by a third stays in its bin. A log
        /// bin and not 20 s steps, which split a lineage's drift at every small margin. In [2, 100].
        /// </summary>
        public const int MarginBins = 24;

        /// <summary>
        /// Investment's range for the regime map, below and above which it clamps. Round 62's window at 22,500 s runs
        /// from 0.09 to 1.3 (5th percentile to the largest clade's median), past <c>RandomGenomeOptions</c>' 0.25 to 1,
        /// which evolution has left.
        /// </summary>
        public const float InvestmentLow = 0.05f;
        public const float InvestmentHigh = 1.3f;

        /// <summary>
        /// The margin bin where the scale starts to grow: about 60 s (bin 15 of 24). Round 62's margins are 140 to 640 s
        /// (bins 18 to 24), so from bin 0 the scale would use only its top quarter.
        /// </summary>
        public const int MarginScaleLowBin = 15;

        /// <summary>The margin's upper end for the log bins, in seconds (<c>RandomGenomeOptions.MaxReserveMargin</c>).</summary>
        public const float MarginHigh = 600f;

        /// <summary>The investment a body with no registered genome takes, in the middle of its range.</summary>
        public const float FallbackInvestment = 0.625f;

        /// <summary>The margin a body with no registered genome takes, in seconds.</summary>
        public const float FallbackMargin = 60f;

        /// <summary>
        /// The most ids the registry holds before it is emptied (16 bytes each, so 3 MB). A long run births more
        /// than this; a body dropped from the registry and still alive is painted from its fallback key until it is
        /// registered again, which the caller does every sweep.
        /// </summary>
        private const int RegistryLimit = 200000;

        /// <summary>What a pattern is drawn from: the cell types, and the two reproductive numbers in steps.</summary>
        public readonly struct Key : IEquatable<Key>
        {
            /// <summary>FNV-1a of the sorted cell-type ids of the genome's nodes.</summary>
            public readonly uint Types;

            /// <summary>Investment over <see cref="InvestmentStep"/>, rounded.</summary>
            public readonly int Investment;

            /// <summary>The margin's log bin, 0 to <see cref="MarginBins"/>.</summary>
            public readonly int Margin;

            public Key(uint types, int investment, int margin)
            {
                Types = types;
                Investment = investment;
                Margin = margin;
            }

            public bool Equals(Key other) =>
                Types == other.Types && Investment == other.Investment && Margin == other.Margin;

            public override bool Equals(object obj) => obj is Key other && Equals(other);

            public override int GetHashCode() => unchecked((int)Hash());

            /// <summary>A fixed integer mix of the three fields (SplitMix64's finalizer): the key's hash.</summary>
            public ulong Hash()
            {
                unchecked
                {
                    ulong h = 0x9E3779B97F4A7C15UL * ((ulong)Types + 1UL);
                    h ^= (ulong)(uint)Investment * 0xBF58476D1CE4E5B9UL;
                    h ^= (ulong)(uint)Margin * 0x94D049BB133111EBUL;
                    h ^= h >> 30; h *= 0xBF58476D1CE4E5B9UL;
                    h ^= h >> 27; h *= 0x94D049BB133111EBUL;
                    h ^= h >> 31;
                    return h;
                }
            }
        }

        /// <summary>
        /// What the palette needs for one key: the library tile's texture and cover, and the body's own scale, offset
        /// and quarter-turn. Made once per key and kept.
        /// </summary>
        public sealed class Solved
        {
            /// <summary>The tile's field, R8, 128 x 128, repeat, trilinear, mip maps built; the minority phase, 0 to 1.</summary>
            public Texture2D Texture;

            /// <summary>The share of the tile the shader's thresholds colour; the colour a far body fades to.</summary>
            public float Cover;

            /// <summary>The tile's length as a multiple of the base share of a creature's size, from the regime.</summary>
            public float ScaleMultiple;

            /// <summary>The offset into the tile, u and v in [0, 1), from the key's hash.</summary>
            public float OffsetU, OffsetV;

            /// <summary>The quarter-turns of the mapping, 0 to 3 (as a float, for the shader), from the key's hash.</summary>
            public float Rotation;

            /// <summary>The library tile this key uses, so a destroyed texture can be rebuilt.</summary>
            public int Tile;
        }

        // ---------------------------------------------------------------- the regime map

        /// <summary>
        /// The three anchors in (feed F, kill k), settled by <c>scratch/theatre-realism/pattern-agent/gs_check.py</c>:
        /// spots, a labyrinth of stripes, and a net with holes. Pearson's 1993 spots near (0.035, 0.065) die out on this
        /// grid and seed; (0.030, 0.062) gives clean spots, the others are as quoted.
        /// </summary>
        private const float SpotsFeed = 0.030f, SpotsKill = 0.062f;
        private const float StripesFeed = 0.060f, StripesKill = 0.062f;
        private const float NetFeed = 0.039f, NetKill = 0.058f;

        /// <summary>The tile's length range as a multiple of the base share: tight stripes to broad ones. In (0, 4].</summary>
        private const float ScaleLow = 0.7f;
        private const float ScaleHigh = 1.4f;

        /// <summary>
        /// The regime and scale of a key. Investment (normalised over <see cref="InvestmentLow"/> to
        /// <see cref="InvestmentHigh"/>) is the place along the path spots, net, stripes (<see cref="PathPoint"/>), and
        /// margin (its log bin, normalised over <see cref="MarginScaleLowBin"/> to <see cref="MarginBins"/>) the scale.
        /// The (feed, kill) it gives is then put on the nearest bin of the path (<see cref="BinOf"/>).
        /// Investment alone, because round 62's margins are all 140 to 640 s, high on the log scale: a margin weight puts
        /// every body in the middle bins, 3 to 6, where the clades look alike, while along investment the big clades
        /// spread from spots to stripes (pattern_regimes.py, h6202 at 22,500 s).
        /// </summary>
        public static void Regime(Key key, out float feed, out float kill, out float scaleMultiple)
        {
            float investment = key.Investment * InvestmentStep;
            float along = Mathf.Clamp01((investment - InvestmentLow) / (InvestmentHigh - InvestmentLow));

            PathPoint(along, out feed, out kill);

            float margin = Mathf.Clamp01((key.Margin - MarginScaleLowBin) / (float)(MarginBins - MarginScaleLowBin));
            scaleMultiple = Mathf.Lerp(ScaleLow, ScaleHigh, margin);
        }

        // ---------------------------------------------------------------- the library

        /// <summary>
        /// The bins along the path spots, net, stripes, from t = 0 to 1 inclusive. Nine, so that the waypoints (a quarter
        /// apart, the net at one half) fall on bins 0, 2, 4, 6 and 8, the three anchors are sampled exactly, and the
        /// bins between are midpoints (bins-sheet.png). A multiple of four plus one; in [5, 33].
        /// </summary>
        public const int RegimeBins = 9;

        /// <summary>
        /// The seed bins per regime bin, from the key's hash: tiles that share a regime and differ in noise. In [1, 16].
        /// The library is at most <see cref="RegimeBins"/> x this, 36 tiles, each solved only if a body asks for it.
        /// </summary>
        public const int SeedBins = 4;

        /// <summary>
        /// How much a unit of kill counts against a unit of feed when a (feed, kill) is put on the nearest path bin: the
        /// anchors span 0.03 in feed and 0.004 in kill, so kill is scaled by about their ratio. In (0, 20].
        /// </summary>
        private const float KillWeight = 7.5f;

        /// <summary>
        /// Two waypoints that bend the path round the dead ground beside the net. A straight line from the net to the
        /// stripes crosses (0.044, 0.059), where the field settles flat and no pattern forms (gs_check.py, bins-sheet.png);
        /// these are live (a field with structure) and keep k at 0.060 or over. One between spots and net, one between
        /// net and stripes.
        /// </summary>
        private const float BendFeed = 0.0345f, BendKill = 0.060f;
        private const float ShoulderFeed = 0.044f, ShoulderKill = 0.0605f;

        /// <summary>The path's waypoints in order: spots, the bend, the net, the shoulder, stripes.</summary>
        private static void Waypoint(int index, out float feed, out float kill)
        {
            switch (index)
            {
                case 0: feed = SpotsFeed; kill = SpotsKill; break;
                case 1: feed = BendFeed; kill = BendKill; break;
                case 2: feed = NetFeed; kill = NetKill; break;
                case 3: feed = ShoulderFeed; kill = ShoulderKill; break;
                default: feed = StripesFeed; kill = StripesKill; break;
            }
        }

        /// <summary>The path's point at t in [0, 1]: the five waypoints a quarter apart, spots at 0, the net at one half, stripes at one.</summary>
        private static void PathPoint(float t, out float feed, out float kill)
        {
            float along = Mathf.Clamp01(t) * 4f;
            int segment = Mathf.Min((int)along, 3);
            float f = along - segment;

            Waypoint(segment, out float f0, out float k0);
            Waypoint(segment + 1, out float f1, out float k1);

            feed = Mathf.Lerp(f0, f1, f);
            kill = Mathf.Lerp(k0, k1, f);
        }

        /// <summary>The path bin nearest a (feed, kill), by distance with kill weighted by <see cref="KillWeight"/>.</summary>
        private static int BinOf(float feed, float kill)
        {
            int best = 0;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < RegimeBins; i++)
            {
                PathPoint(i / (float)(RegimeBins - 1), out float pathFeed, out float pathKill);
                float df = feed - pathFeed;
                float dk = (kill - pathKill) * KillWeight;
                float distance = df * df + dk * dk;

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        // ---------------------------------------------------------------- the solver

        /// <summary>The grid's side, a power of two (the wrap is a mask). The texture is this wide.</summary>
        private const int Side = 128;

        /// <summary>Explicit Euler steps at dt 1. About 6,000 settle every regime above (gs_check.py).</summary>
        private const int Steps = 6000;

        /// <summary>The diffusion rates of u and v, and the time step: the dimensionless Gray-Scott form. Du, Dv dt under a quarter keep it stable.</summary>
        private const float DiffusionU = 0.16f, DiffusionV = 0.08f, TimeStep = 1f;

        /// <summary>The seeded squares that start the pattern: how many, and their side in cells. Where they land is the seed's.</summary>
        private const int SquareCount = 6;
        private const int SquareSide = 10;

        /// <summary>The noise added to the start: u within plus or minus half this, v from zero up to <see cref="NoiseV"/>.</summary>
        private const float NoiseU = 0.02f, NoiseV = 0.01f;

        /// <summary>A field whose highest v is under this has died out (the regime sits outside the pattern's range): drawn as no pattern.</summary>
        private const float DieOut = 1e-3f;

        /// <summary>
        /// The thresholds the shader maps v through (<c>PatternLow</c>, <c>PatternHigh</c>), repeated here to count the
        /// cover. They must equal the shader's. The field is v over its own highest value, so they are shares of it.
        /// </summary>
        private const float ThresholdLow = 0.35f, ThresholdHigh = 0.65f;

        /// <summary>
        /// The most of a tile the pigment may cover. A field covering more (a net, whose holes are the minority) is
        /// stored inverted, so the pigment is always the smaller phase and the body's own colour stays the ground.
        /// In (0, 1).
        /// </summary>
        private const float MaxCover = 0.5f;

        private static float[] _u, _v, _u2, _v2;
        private static byte[] _bytes;

        /// <summary>Solves one (feed, kill, seed) into <paramref name="bytes"/> (Side x Side, 0 to 255) and returns the cover.</summary>
        private static float Solve(float feed, float kill, ulong seed, byte[] bytes)
        {
            int cells = Side * Side;
            const int mask = Side - 1;

            if (_u == null)
            {
                _u = new float[cells]; _v = new float[cells];
                _u2 = new float[cells]; _v2 = new float[cells];
            }

            float feedKill = feed + kill;

            float[] u = _u, v = _v, u2 = _u2, v2 = _v2;
            var rng = new Rng(seed);

            for (int i = 0; i < cells; i++) { u[i] = 1f; v[i] = 0f; }

            for (int s = 0; s < SquareCount; s++)
            {
                int x0 = rng.Range(Side);
                int y0 = rng.Range(Side);

                for (int dy = 0; dy < SquareSide; dy++)
                {
                    for (int dx = 0; dx < SquareSide; dx++)
                    {
                        int at = ((y0 + dy) & mask) * Side + ((x0 + dx) & mask);
                        u[at] = 0.5f;
                        v[at] = 0.25f;
                    }
                }
            }

            for (int i = 0; i < cells; i++)
            {
                u[i] += (rng.NextFloat() - 0.5f) * NoiseU;
                v[i] += rng.NextFloat() * NoiseV;
            }

            for (int step = 0; step < Steps; step++)
            {
                for (int y = 0; y < Side; y++)
                {
                    int row = y * Side;
                    int up = ((y - 1) & mask) * Side;
                    int down = ((y + 1) & mask) * Side;

                    for (int x = 0; x < Side; x++)
                    {
                        int i = row + x;
                        int l = row + ((x - 1) & mask);
                        int r = row + ((x + 1) & mask);

                        float uc = u[i];
                        float vc = v[i];
                        float lapU = u[up + x] + u[down + x] + u[l] + u[r] - 4f * uc;
                        float lapV = v[up + x] + v[down + x] + v[l] + v[r] - 4f * vc;
                        float reaction = uc * vc * vc;

                        u2[i] = uc + TimeStep * (DiffusionU * lapU - reaction + feed * (1f - uc));
                        v2[i] = vc + TimeStep * (DiffusionV * lapV + reaction - feedKill * vc);
                    }
                }

                float[] swap = u; u = u2; u2 = swap;
                swap = v; v = v2; v2 = swap;
            }

            float highest = 0f;
            for (int i = 0; i < cells; i++) if (v[i] > highest) highest = v[i];

            if (!(highest > DieOut))
            {
                for (int i = 0; i < cells; i++) bytes[i] = 0;
                return 0f;
            }

            float cover = 0f;
            for (int i = 0; i < cells; i++) cover += Threshold(Mathf.Clamp01(v[i] / highest));
            cover /= cells;

            bool invert = cover > MaxCover;
            if (invert) cover = 0f;

            for (int i = 0; i < cells; i++)
            {
                float field = Mathf.Clamp01(v[i] / highest);
                if (invert)
                {
                    field = 1f - field;
                    cover += Threshold(field);
                }

                bytes[i] = (byte)Mathf.RoundToInt(field * 255f);
            }

            if (invert) cover /= cells;
            return cover;
        }

        /// <summary>The shader's <c>smoothstep(PatternLow, PatternHigh, v)</c>.</summary>
        private static float Threshold(float field)
        {
            float t = Mathf.Clamp01((field - ThresholdLow) / (ThresholdHigh - ThresholdLow));
            return t * t * (3f - 2f * t);
        }

        // ---------------------------------------------------------------- keys and the caches

        private static Dictionary<long, Key> _registered;
        private static Dictionary<Key, Solved> _placed;

        /// <summary>The library: one slot per (regime bin, seed bin), null until a body asks for it. Made on the first ask.</summary>
        private static Texture2D[] _tileTexture;
        private static float[] _tileCover;

        private static int _tilesSolved;
        private static long _solveMilliseconds;

        /// <summary>
        /// Says which genome a body was grown from. Cheap and idempotent: the key is made the first time an id is
        /// seen and never again, and with the dial off the call returns at once. A caller with a genome calls it
        /// before the body is first painted (<c>LiveWorldView</c>'s sweep, <c>SnapshotWorld</c>'s load).
        /// </summary>
        public static void Register(long id, Genome genome)
        {
            if (!Enabled || genome == null) return;

            if (_registered == null) _registered = new Dictionary<long, Key>();
            if (_registered.ContainsKey(id)) return;
            if (_registered.Count >= RegistryLimit) _registered.Clear();

            _registered[id] = KeyOf(genome);
        }

        /// <summary>A registered body's key, or false.</summary>
        public static bool TryRegistered(long id, out Key key)
        {
            key = default;
            return _registered != null && _registered.TryGetValue(id, out key);
        }

        /// <summary>The key of a genome: its nodes' cell types, its investment and its margin's log bin.</summary>
        public static Key KeyOf(Genome genome)
        {
            var types = new List<string>(genome.Nodes.Count);
            for (int i = 0; i < genome.Nodes.Count; i++) types.Add(genome.Nodes[i].CellTypeId);

            return new Key(
                TypesHash(types),
                Mathf.RoundToInt(genome.Reproduction.BirthInvestment / InvestmentStep),
                MarginBin(genome.Reproduction.ReserveMargin));
        }

        /// <summary>The key of a body with no registered genome: its parts' cell types and the middle of both ranges.</summary>
        public static Key FallbackKey(Phenotype phenotype)
        {
            var types = new List<string>(phenotype.PartCount);
            for (int i = 0; i < phenotype.PartCount; i++) types.Add(phenotype.Parts[i].CellTypeId);

            return new Key(
                TypesHash(types),
                Mathf.RoundToInt(FallbackInvestment / InvestmentStep),
                MarginBin(FallbackMargin));
        }

        /// <summary>A margin in seconds as its log bin, 0 to <see cref="MarginBins"/>.</summary>
        private static int MarginBin(float seconds)
        {
            float share = Mathf.Log(1f + Mathf.Max(0f, seconds)) / Mathf.Log(1f + MarginHigh);
            return Mathf.Clamp(Mathf.RoundToInt(share * MarginBins), 0, MarginBins);
        }

        /// <summary>FNV-1a over the sorted ids, a separator between them: the multiset, in a fixed order.</summary>
        private static uint TypesHash(List<string> types)
        {
            types.Sort(StringComparer.Ordinal);

            unchecked
            {
                uint h = 2166136261u;

                for (int i = 0; i < types.Count; i++)
                {
                    string id = types[i] ?? "";
                    for (int c = 0; c < id.Length; c++) h = (h ^ id[c]) * 16777619u;
                    h = (h ^ 0x1Fu) * 16777619u;
                }

                return h;
            }
        }

        /// <summary>
        /// The pattern of a key: its library tile (solved on the first ask and kept for the process, and rebuilt if Unity
        /// has destroyed the texture, as a reload with the domain kept does), and its own scale, offset and quarter-turn.
        /// Made once per key and kept.
        /// </summary>
        public static Solved SolvedFor(Key key)
        {
            if (_placed == null) _placed = new Dictionary<Key, Solved>();

            if (!_placed.TryGetValue(key, out Solved solved))
            {
                Regime(key, out float feed, out float kill, out float scaleMultiple);
                ulong hash = key.Hash();

                // The hash's bits, apart: 24 for each offset, 8 for the seed bin, 2 for the turn.
                solved = new Solved
                {
                    ScaleMultiple = scaleMultiple,
                    OffsetU = (uint)(hash >> 40) * (1f / 16777216f),
                    OffsetV = (uint)((hash >> 16) & 0xFFFFFFUL) * (1f / 16777216f),
                    Rotation = (float)(int)(hash & 3UL),
                    Tile = BinOf(feed, kill) * SeedBins + (int)(((hash >> 8) & 0xFFUL) % (ulong)SeedBins),
                };

                _placed[key] = solved;
            }

            if (solved.Texture == null) Fill(solved);
            return solved;
        }

        // skinmap:begin
        // ---------------------------------------------------------------- a pattern by the skin vector (EVOSIM_THEATRE_SKIN_MAP)

        /// <summary>The steps the scale's place between its ends is cut into for the cache: a step is a hundredth of the range of the tile's length, which no eye sees.</summary>
        private const int MapScaleSteps = 256;

        private static Dictionary<(Key, int, int), Solved> _placedForMap;

        /// <summary>
        /// The pattern of a body under <c>EVOSIM_THEATRE_SKIN_MAP</c>: the regime from the skin vector's place along the path spots,
        /// net, stripes in [0, 1] (in place of <see cref="Regime"/>'s normalised investment), put on the nearest bin of the path
        /// as <see cref="Regime"/> puts its own, and the tile's length multiple from the vector's place between
        /// <see cref="ScaleLow"/> and <see cref="ScaleHigh"/> (in place of the margin's bin). The tile's seed bin, the offset and
        /// the quarter-turn still come from the key's hash, the noise phase the plan keeps. Made once per (key, regime bin, scale
        /// step) and kept.
        /// </summary>
        public static Solved SolvedForMap(Key key, float along, float scaleAlong)
        {
            PathPoint(Mathf.Clamp01(along), out float feed, out float kill);
            int bin = BinOf(feed, kill);
            int step = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(scaleAlong) * MapScaleSteps), 0, MapScaleSteps);

            if (_placedForMap == null) _placedForMap = new Dictionary<(Key, int, int), Solved>();

            if (!_placedForMap.TryGetValue((key, bin, step), out Solved solved))
            {
                ulong hash = key.Hash();

                solved = new Solved
                {
                    ScaleMultiple = Mathf.Lerp(ScaleLow, ScaleHigh, step / (float)MapScaleSteps),
                    OffsetU = (uint)(hash >> 40) * (1f / 16777216f),
                    OffsetV = (uint)((hash >> 16) & 0xFFFFFFUL) * (1f / 16777216f),
                    Rotation = (float)(int)(hash & 3UL),
                    Tile = bin * SeedBins + (int)(((hash >> 8) & 0xFFUL) % (ulong)SeedBins),
                };

                _placedForMap[(key, bin, step)] = solved;
            }

            if (solved.Texture == null) Fill(solved);
            return solved;
        }

        // skinmap:end
        // skin2:begin
        // ---------------------------------------------------------------- a pattern by clade (EVOSIM_THEATRE_SKIN2)

        /// <summary>The share of clades that wear each pattern kind, by a hash of the clade: spots, stripes, a fine mottle, nearly plain.</summary>
        private const float CladeSpotsEnd = 0.30f, CladeStripesEnd = 0.55f, CladeMottleEnd = 0.78f;

        /// <summary>The tile's length as a multiple of the regime's own, and the strength of the pigment, by kind. A fine mottle is a net at under half the tile; nearly plain is faint and broad.</summary>
        private const float CladeMottleTile = 0.45f, CladeMottleStrength = 0.85f;
        private const float CladePlainTile = 1.4f, CladePlainStrength = 0.12f;

        /// <summary>
        /// What a clade wears: a regime bin of the library by its kind (spots on 0 or 1, stripes on 7 or 8, a fine mottle on
        /// 3, 4 or 5, nearly plain on 0), a seed bin, a multiple of the tile's length and of the pigment's strength, and two
        /// fractions in [0, 1) the shader reads as <c>_PatternShift.z</c> and <c>.w</c> (the margin's colour and the
        /// pigment's pair). All from the clade's number alone, so every body of a clade wears the same.
        /// </summary>
        public readonly struct CladeStyle
        {
            /// <summary>0 spots, 1 stripes, 2 a fine mottle, 3 nearly plain.</summary>
            public readonly int Kind;

            /// <summary>The library's regime bin, 0 to <see cref="RegimeBins"/> - 1, and the seed bin, 0 to <see cref="SeedBins"/> - 1.</summary>
            public readonly int Bin, SeedBin;

            /// <summary>The tile's length times this, and the pigment's strength times this.</summary>
            public readonly float TileMultiple, Strength;

            /// <summary>The two fractions for the shader: the margin's colour, then the pigment's pair.</summary>
            public readonly float Hash1, Hash2;

            public CladeStyle(int kind, int bin, int seedBin, float tileMultiple, float strength, float hash1, float hash2)
            {
                Kind = kind;
                Bin = bin;
                SeedBin = seedBin;
                TileMultiple = tileMultiple;
                Strength = strength;
                Hash1 = hash1;
                Hash2 = hash2;
            }
        }

        /// <summary>SplitMix64's finalizer, with the increment: a fixed integer mix, the same in every process.</summary>
        public static ulong CladeMix(ulong x)
        {
            unchecked
            {
                x += 0x9E3779B97F4A7C15UL;
                x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
                x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
                return x ^ (x >> 31);
            }
        }

        /// <summary>The style of a clade, from its number alone; nothing is solved.</summary>
        public static CladeStyle StyleOf(ulong clade)
        {
            ulong m = CladeMix(clade);
            ulong m2 = CladeMix(m ^ 0xD1B54A32D192ED03UL);
            ulong m3 = CladeMix(m2 + 1UL);

            float pick = (uint)(m >> 40) * (1f / 16777216f);
            int variant = (int)((m2 >> 8) & 0xFFUL);
            int seedBin = (int)(((m >> 8) & 0xFFUL) % (ulong)SeedBins);

            float hash1 = (uint)(m3 >> 40) * (1f / 16777216f);
            float hash2 = (uint)((m3 >> 16) & 0xFFFFFFUL) * (1f / 16777216f);

            if (pick < CladeSpotsEnd) return new CladeStyle(0, variant & 1, seedBin, 1f, 1f, hash1, hash2);
            if (pick < CladeStripesEnd) return new CladeStyle(1, 7 + (variant & 1), seedBin, 1f, 1f, hash1, hash2);
            if (pick < CladeMottleEnd) return new CladeStyle(2, 3 + (variant >> 1) % 3, seedBin, CladeMottleTile, CladeMottleStrength, hash1, hash2);
            return new CladeStyle(3, 0, seedBin, CladePlainTile, CladePlainStrength, hash1, hash2);
        }

        private static Dictionary<(Key, ulong), Solved> _placedForClade;

        /// <summary>
        /// The pattern a body wears under <c>EVOSIM_THEATRE_SKIN2</c>: the library tile its clade's style names (solved on the
        /// first ask and kept, as <see cref="SolvedFor"/>'s are), and the scale, offset and quarter-turn of its own key, so
        /// kin share a pattern and a body still sits differently on it from a stranger of its clade. The scale is the key's
        /// (the margin's) times the style's. Made once per (key, clade) and kept.
        /// </summary>
        public static Solved SolvedForClade(Key key, ulong clade, out CladeStyle style)
        {
            style = StyleOf(clade);

            if (_placedForClade == null) _placedForClade = new Dictionary<(Key, ulong), Solved>();

            if (!_placedForClade.TryGetValue((key, clade), out Solved solved))
            {
                Regime(key, out _, out _, out float scaleMultiple);
                ulong hash = key.Hash();

                solved = new Solved
                {
                    ScaleMultiple = scaleMultiple * style.TileMultiple,
                    OffsetU = (uint)(hash >> 40) * (1f / 16777216f),
                    OffsetV = (uint)((hash >> 16) & 0xFFFFFFUL) * (1f / 16777216f),
                    Rotation = (float)(int)(hash & 3UL),
                    Tile = style.Bin * SeedBins + style.SeedBin,
                };

                _placedForClade[(key, clade)] = solved;
            }

            if (solved.Texture == null) Fill(solved);
            return solved;
        }

        // skin2:end
        /// <summary>Gives a placement its library tile's texture and cover, solving the tile if it is not yet.</summary>
        private static void Fill(Solved solved)
        {
            int tiles = RegimeBins * SeedBins;

            if (_tileTexture == null)
            {
                _tileTexture = new Texture2D[tiles];
                _tileCover = new float[tiles];
            }

            int tile = solved.Tile;

            if (_tileTexture[tile] == null)
            {
                if (_bytes == null) _bytes = new byte[Side * Side];

                int bin = tile / SeedBins;
                int seedBin = tile % SeedBins;
                PathPoint(bin / (float)(RegimeBins - 1), out float feed, out float kill);

                // The tile's seed is a fixed mix of its two numbers alone, so the library is the same in every process.
                ulong seed = 0x9E3779B97F4A7C15UL * (ulong)(tile + 1);
                seed ^= seed >> 29;
                seed *= 0xBF58476D1CE4E5B9UL;
                seed ^= seed >> 32;

                var clock = System.Diagnostics.Stopwatch.StartNew();
                float cover = Solve(feed, kill, seed, _bytes);
                clock.Stop();

                var texture = new Texture2D(Side, Side, TextureFormat.R8, true, true)
                {
                    name = "TheatrePattern",
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Trilinear,
                    hideFlags = HideFlags.HideAndDontSave,
                };

                texture.SetPixelData(_bytes, 0);
                texture.Apply(true, true);

                _tileTexture[tile] = texture;
                _tileCover[tile] = cover;
                _tilesSolved++;
                _solveMilliseconds += clock.ElapsedMilliseconds;

                Debug.Log(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "[Theatre] pattern tile {0} (regime {1}, seed {2}; F {3:0.0000} k {4:0.0000}) cover {5:0.00} solved in {6} ms; " +
                    "{7} tiles solved of at most {8}, {9} ms in all.",
                    tile, bin, seedBin, feed, kill, cover, clock.ElapsedMilliseconds,
                    _tilesSolved, tiles, _solveMilliseconds));
            }

            solved.Texture = _tileTexture[tile];
            solved.Cover = _tileCover[tile];
        }

        /// <summary>Tiles solved in this process and the milliseconds the solves took, for the film's <c>timing: dress</c> line.</summary>
        public static void SolveTally(out int tiles, out long milliseconds)
        {
            tiles = _tilesSolved;
            milliseconds = _solveMilliseconds;
        }
    }

    /// <summary>
    /// Running sums of what the dressing of a film's bodies spends, read by the film driver's <c>timing: dress</c> line
    /// (<c>TheatreFilm</c>). It only reads the clock and adds; nothing a frame draws depends on it.
    /// </summary>
    public static class DressTimers
    {
        private static readonly double MillisecondsPerTick = 1000d / System.Diagnostics.Stopwatch.Frequency;

        /// <summary>Time the one skins' builds took (<c>TheatreOneSkin.Build</c>), ms, and how many were built.</summary>
        public static double OneSkinMs;
        public static int OneSkins;

        /// <summary>Time <c>TheatrePalette.Paint</c> took in all, ms, and the calls.</summary>
        public static double PaintMs;
        public static int Paints;

        /// <summary>Time <c>OneSkin.Dress</c> took in all, ms.</summary>
        public static double SkinDressMs;

        /// <summary>Inside the paint: the pattern field lookup (tile solves included), the arc bend's log calls, the nose pit and snout, the mouth, ms.</summary>
        public static double PatternLookupMs, ArcBendMs, NoseMs, MouthMs;

        /// <summary>The pattern tiles solved in this process and the milliseconds the solves took.</summary>
        public static void PatternTiles(out int tiles, out long milliseconds) => TheatrePattern.SolveTally(out tiles, out milliseconds);

        /// <summary>The clock now, to hand back to <see cref="Since"/>.</summary>
        public static long Now() => System.Diagnostics.Stopwatch.GetTimestamp();

        /// <summary>Milliseconds from a <see cref="Now"/> to now.</summary>
        public static double Since(long start) => (System.Diagnostics.Stopwatch.GetTimestamp() - start) * MillisecondsPerTick;
    }
}
