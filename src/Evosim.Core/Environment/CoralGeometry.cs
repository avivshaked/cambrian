using System;
using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>
    /// Round 62's branching coral (D194, <c>logbook/specs/coral-build.md</c>): colonies grown as
    /// trees of capsules and plates from points on the bed, most of them on the islands.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What this class holds, and what it does not yet.</b> The colonies as grown and placed:
    /// every piece's ends, radius and thickness, in ascending order within each colony, and the
    /// canopy each island reached. The distance, the contact, the hull's fade, the canopy map
    /// and the static grid read these pieces, and are step 2 of the build spec; nothing in the
    /// world reads this class yet, and <see cref="World"/> refuses
    /// <see cref="ReefShape.Coral"/> until they exist.
    /// </para>
    /// <para>
    /// <b>A colony.</b> The trunk is a vertical capsule from the bed's height at the colony's
    /// centre, its height and radius uniform in their ranges. Each capsule's top spawns
    /// <see cref="RunConfig.CoralChildrenMin"/> to <see cref="RunConfig.CoralChildrenMax"/>
    /// children, each tilted from its parent's axis by a drawn angle and turned about it by a
    /// drawn azimuth, shorter by <see cref="RunConfig.CoralLengthFactor"/> and thinner by
    /// <see cref="RunConfig.CoralRadiusFactor"/>. Growth is breadth first, so a colony's pieces
    /// are in order of depth and the piece cap trims the outermost tips. A child is not grown past
    /// <see cref="RunConfig.CoralMaxDepth"/>, under <see cref="RunConfig.CoralMinRadiusMetres"/>,
    /// past <see cref="RunConfig.CoralMaxPiecesPerColony"/>, within
    /// <see cref="SurfaceClearanceMetres"/> of the surface, or nearer the glass than the margin.
    /// A capsule that grew no child is a tip, and carries a horizontal plate at its end with
    /// <see cref="RunConfig.CoralPlateChance"/>.
    /// </para>
    /// <para>
    /// <b>No trig after placement.</b> Each child's tilt and azimuth are drawn as angles and turned
    /// into cosine and sine pairs here, once, and the piece keeps its ends. A capsule's distance is
    /// a projection, a clamp and a square root, and a plate's is
    /// <c>length((max(|xz| − R, 0), y)) − t/2</c> in its own frame, so no per-step reader needs an
    /// angle.
    /// </para>
    /// <para>
    /// <b>Placed by the reef stream</b> (<see cref="World.ReefPlacementIndex"/>), which D192 seeds
    /// from the landscape's seed, so one seed is one coral. See <see cref="Place(RunConfig, CoralFootprint, BedShape, Func{float, float, float}, ulong, bool)"/> for the rule.
    /// </para>
    /// </remarks>
    public sealed class CoralGeometry
    {
        /// <summary>No piece comes nearer the surface than this, m.</summary>
        public const float SurfaceClearanceMetres = 2f;

        /// <summary>The canopy's columns, m, on which each island's roof is counted.</summary>
        public const float CanopyColumnMetres = 1f;

        /// <summary>Each canopy column is sampled on this many points a side for its covered share.</summary>
        public const int CanopySamplesAcross = 4;

        /// <summary>How many centres one colony is offered before the world is refused.</summary>
        public const int PlacementAttempts = ReefGeometry.PlacementAttempts;

        /// <summary>The least island share D194 allows.</summary>
        public const float LeastIslandShare = 0.8f;

        /// <summary>A piece's primitive.</summary>
        public enum PieceKind
        {
            /// <summary>A segment from A to B swept by a sphere of <see cref="Piece.Radius"/>.</summary>
            Capsule = 0,

            /// <summary>A horizontal disc of <see cref="Piece.Radius"/> about A swept by a sphere of <see cref="Piece.HalfThickness"/>.</summary>
            Plate = 1,
        }

        /// <summary>One piece of a colony.</summary>
        public struct Piece
        {
            /// <summary>Capsule or plate.</summary>
            public PieceKind Kind;

            /// <summary>The colony it belongs to.</summary>
            public int Colony;

            /// <summary>Its parent's index within the colony; −1 for the trunk.</summary>
            public int Parent;

            /// <summary>Its number of ancestors: 0 for the trunk.</summary>
            public int Depth;

            /// <summary>A capsule's base, where it meets its parent; a plate's centre. m, tank frame.</summary>
            public double AX, AY, AZ;

            /// <summary>A capsule's top end; for a plate, its centre again. m.</summary>
            public double BX, BY, BZ;

            /// <summary>A capsule's radius, or a plate's disc radius, m.</summary>
            public double Radius;

            /// <summary>Half a plate's thickness, m; 0 for a capsule.</summary>
            public double HalfThickness;

            /// <summary>The cosine and sine of the tilt from the parent's axis: 1 and 0 for the trunk and a plate.</summary>
            public double TiltCos, TiltSin;

            /// <summary>The cosine and sine of the azimuth about the parent's axis: 1 and 0 for the trunk and a plate.</summary>
            public double AzimuthCos, AzimuthSin;
        }

        /// <summary>One colony: where it stands, which island it grows from, and its pieces.</summary>
        public struct Colony
        {
            /// <summary>The trunk's axis, x and z, m, tank frame.</summary>
            public double X, Z;

            /// <summary>The bed's height under the trunk, m.</summary>
            public double BaseY;

            /// <summary>The island it was drawn on, or −1 for a desert colony.</summary>
            public int Island;

            /// <summary>Its first piece in <see cref="PieceAt"/>, and how many.</summary>
            public int FirstPiece, PieceCount;

            /// <summary>The canopy samples it covered first, at <see cref="CanopySamplesAcross"/>² a column.</summary>
            public int CanopySamples;

            /// <summary>Whether it was drawn on an island.</summary>
            public bool OnIsland => Island >= 0;
        }

        private readonly Colony[] _colonies;
        private readonly Piece[] _pieces;

        // Each colony's box over its pieces' surfaces, m.
        private readonly double[] _minX, _minY, _minZ, _maxX, _maxY, _maxZ;

        private readonly double[] _islandTargetSamples;
        private readonly int[] _islandGotSamples;
        private readonly int[] _islandColumns;

        /// <summary>
        /// Colonies and their pieces. The placer builds this through <see cref="Place(RunConfig, CoralFootprint, BedShape, Func{float, float, float}, ulong, bool)"/>; tests may
        /// build it directly, with no island record.
        /// </summary>
        /// <param name="filletMetres">The fillet that joins a colony's pieces, m.</param>
        /// <param name="colonies">The colonies, each naming a contiguous run of pieces in order.</param>
        /// <param name="pieces">Every piece, colony by colony.</param>
        public CoralGeometry(float filletMetres, Colony[] colonies, Piece[] pieces)
            : this(filletMetres, colonies, pieces, null, null, null, 0d, 0, 0f)
        {
        }

        private CoralGeometry(
            float filletMetres, Colony[] colonies, Piece[] pieces,
            double[] islandTargetSamples, int[] islandGotSamples, int[] islandColumns,
            double desertTargetSamples, int desertGotSamples, float islandRoof)
        {
            if (colonies == null) throw new ArgumentNullException(nameof(colonies));
            if (pieces == null) throw new ArgumentNullException(nameof(pieces));

            if (!(filletMetres >= 0f) || float.IsInfinity(filletMetres))
            {
                throw new ArgumentException(
                    FormattableString.Invariant($"A coral's fillet is finite and not negative; have {filletMetres} m."),
                    nameof(filletMetres));
            }

            FilletMetres = filletMetres;
            _colonies = (Colony[])colonies.Clone();
            _pieces = (Piece[])pieces.Clone();

            int n = _colonies.Length;
            _minX = new double[n];
            _minY = new double[n];
            _minZ = new double[n];
            _maxX = new double[n];
            _maxY = new double[n];
            _maxZ = new double[n];

            int next = 0;
            for (int c = 0; c < n; c++)
            {
                Colony colony = _colonies[c];
                if (colony.FirstPiece != next || colony.PieceCount < 1 || colony.FirstPiece + colony.PieceCount > _pieces.Length)
                {
                    throw new ArgumentException(
                        FormattableString.Invariant(
                            $"Colony {c} names pieces {colony.FirstPiece} to {colony.FirstPiece + colony.PieceCount - 1}, ") +
                        FormattableString.Invariant(
                            $"and the next piece is {next} of {_pieces.Length}: colonies hold contiguous runs, in order, of at least one piece."),
                        nameof(colonies));
                }

                _minX[c] = _minY[c] = _minZ[c] = double.PositiveInfinity;
                _maxX[c] = _maxY[c] = _maxZ[c] = double.NegativeInfinity;

                for (int k = 0; k < colony.PieceCount; k++)
                {
                    int index = colony.FirstPiece + k;
                    Piece p = _pieces[index];
                    RefusePiece(index, c, k, p);

                    double rh = p.Kind == PieceKind.Plate ? p.Radius + p.HalfThickness : p.Radius;
                    double rv = p.Kind == PieceKind.Plate ? p.HalfThickness : p.Radius;
                    _minX[c] = Math.Min(_minX[c], Math.Min(p.AX, p.BX) - rh);
                    _maxX[c] = Math.Max(_maxX[c], Math.Max(p.AX, p.BX) + rh);
                    _minZ[c] = Math.Min(_minZ[c], Math.Min(p.AZ, p.BZ) - rh);
                    _maxZ[c] = Math.Max(_maxZ[c], Math.Max(p.AZ, p.BZ) + rh);
                    _minY[c] = Math.Min(_minY[c], Math.Min(p.AY, p.BY) - rv);
                    _maxY[c] = Math.Max(_maxY[c], Math.Max(p.AY, p.BY) + rv);
                }

                next += colony.PieceCount;
            }

            if (next != _pieces.Length)
            {
                throw new ArgumentException(
                    FormattableString.Invariant($"The colonies hold {next} pieces and {_pieces.Length} were given."),
                    nameof(pieces));
            }

            _islandTargetSamples = islandTargetSamples ?? Array.Empty<double>();
            _islandGotSamples = islandGotSamples ?? Array.Empty<int>();
            _islandColumns = islandColumns ?? Array.Empty<int>();
            DesertTargetSamples = desertTargetSamples;
            DesertGotSamples = desertGotSamples;
            IslandRoof = islandRoof;
        }

        private static void RefusePiece(int index, int colony, int within, Piece p)
        {
            bool finite =
                Finite(p.AX) && Finite(p.AY) && Finite(p.AZ) && Finite(p.BX) && Finite(p.BY) && Finite(p.BZ) &&
                p.Radius > 0d && Finite(p.Radius) && p.HalfThickness >= 0d && Finite(p.HalfThickness);

            bool shaped = p.Kind == PieceKind.Capsule
                ? p.HalfThickness == 0d
                : p.HalfThickness > 0d && p.AX == p.BX && p.AY == p.BY && p.AZ == p.BZ;

            bool family = p.Colony == colony && p.Parent < within && (within == 0) == (p.Parent < 0) && p.Depth >= 0;

            if (!finite || !shaped || !family)
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"Coral piece {index} ({p.Kind}, colony {p.Colony}, parent {p.Parent}, radius {p.Radius} m, half thickness {p.HalfThickness} m) ") +
                    "is refused: a piece is finite with a radius above 0; a capsule has no thickness and a plate has one, " +
                    "centred at one point; and every piece but its colony's first names an earlier piece of the same " +
                    "colony as its parent. logbook/specs/coral-build.md.",
                    nameof(p));
            }
        }

        private static bool Finite(double d) => !double.IsNaN(d) && !double.IsInfinity(d);

        /// <summary>How many colonies.</summary>
        public int Count => _colonies.Length;

        /// <summary>How many pieces, over every colony.</summary>
        public int PieceCount => _pieces.Length;

        /// <summary>The fillet that joins a colony's pieces, m.</summary>
        public float FilletMetres { get; }

        /// <summary>A colony as placed.</summary>
        public Colony ColonyAt(int colony) => _colonies[colony];

        /// <summary>A piece as grown.</summary>
        public Piece PieceAt(int piece) => _pieces[piece];

        /// <summary>A colony's box over its pieces' surfaces, m.</summary>
        public void Bounds(int colony, out double minX, out double minY, out double minZ, out double maxX, out double maxY, out double maxZ)
        {
            minX = _minX[colony];
            minY = _minY[colony];
            minZ = _minZ[colony];
            maxX = _maxX[colony];
            maxY = _maxY[colony];
            maxZ = _maxZ[colony];
        }

        /// <summary>How many islands the placer counted canopy on; 0 for coral built by hand.</summary>
        public int IslandCount => _islandColumns.Length;

        /// <summary>The roof each island was filled to, as a share of its canopy columns.</summary>
        public float IslandRoof { get; }

        /// <summary>An island's canopy columns.</summary>
        public int IslandColumnCount(int island) => _islandColumns[island];

        /// <summary>An island's roof target, in canopy samples.</summary>
        public double IslandTargetSamples(int island) => _islandTargetSamples[island];

        /// <summary>The canopy samples island colonies covered on an island.</summary>
        public int IslandGotSamples(int island) => _islandGotSamples[island];

        /// <summary>The share of an island's canopy columns the coral roofs.</summary>
        public double IslandRoofGot(int island) =>
            _islandGotSamples[island] / ((double)_islandColumns[island] * CanopySamplesAcross * CanopySamplesAcross);

        /// <summary>The desert colonies' canopy target, in canopy samples.</summary>
        public double DesertTargetSamples { get; }

        /// <summary>The canopy samples desert colonies covered.</summary>
        public int DesertGotSamples { get; }

        /// <summary>One canopy sample's area, m².</summary>
        public static double SampleSquareMetres =>
            (CanopyColumnMetres / (double)CanopySamplesAcross) * (CanopyColumnMetres / (double)CanopySamplesAcross);

        /// <summary>
        /// A digest of every colony and piece, to the bit: what one seed's coral is, for tests and
        /// for the record.
        /// </summary>
        public ulong Digest()
        {
            ulong h = 14695981039346656037UL;
            h = Mix(h, (ulong)_colonies.Length);
            h = Mix(h, (ulong)_pieces.Length);
            h = Mix(h, (ulong)BitConverter.SingleToInt32Bits(FilletMetres));

            foreach (Colony c in _colonies)
            {
                h = Mix(h, Bits(c.X));
                h = Mix(h, Bits(c.Z));
                h = Mix(h, Bits(c.BaseY));
                h = Mix(h, (ulong)(long)c.Island);
                h = Mix(h, (ulong)c.FirstPiece);
                h = Mix(h, (ulong)c.PieceCount);
            }

            foreach (Piece p in _pieces)
            {
                h = Mix(h, (ulong)p.Kind);
                h = Mix(h, (ulong)p.Colony);
                h = Mix(h, (ulong)(long)p.Parent);
                h = Mix(h, (ulong)p.Depth);
                h = Mix(h, Bits(p.AX));
                h = Mix(h, Bits(p.AY));
                h = Mix(h, Bits(p.AZ));
                h = Mix(h, Bits(p.BX));
                h = Mix(h, Bits(p.BY));
                h = Mix(h, Bits(p.BZ));
                h = Mix(h, Bits(p.Radius));
                h = Mix(h, Bits(p.HalfThickness));
                h = Mix(h, Bits(p.TiltCos));
                h = Mix(h, Bits(p.TiltSin));
                h = Mix(h, Bits(p.AzimuthCos));
                h = Mix(h, Bits(p.AzimuthSin));
            }

            return h;
        }

        private static ulong Bits(double d) => (ulong)BitConverter.DoubleToInt64Bits(d);

        private static ulong Mix(ulong h, ulong v)
        {
            for (int b = 0; b < 8; b++)
            {
                h ^= (v >> (8 * b)) & 0xFFUL;
                h *= 1099511628211UL;
            }

            return h;
        }

        // ------------------------------------------------------------------ the refusals

        /// <summary>
        /// Every refusal of the coral group that the config and the tank's radius can decide. At
        /// <see cref="ReefShape.Mushroom"/> the only one is that every coral dial stands at its
        /// default, so a mushroom world's hash carries no coral it does not have.
        /// </summary>
        /// <param name="config">The config.</param>
        /// <param name="tankRadiusMetres">The tank's radius, m.</param>
        /// <param name="worldRules">
        /// False skips the two refusals about the rest of the world (a box, a field that is not a
        /// grid), as <see cref="ReefGeometry.Refuse"/> does for the theatre's picture reader.
        /// </param>
        public static void Refuse(RunConfig config, float tankRadiusMetres, bool worldRules = true) =>
            Refuse(config, CoralFootprint.Tank(tankRadiusMetres), worldRules);

        /// <summary>
        /// Every refusal of the coral group that the config and the footprint can decide: a tank's
        /// disc, or D204's square (<see cref="CoralFootprint.Box"/>).
        /// </summary>
        public static void Refuse(RunConfig config, CoralFootprint footprint, bool worldRules = true)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            float tankRadiusMetres = footprint.TankRadiusMetres;

            if (config.ReefShape != ReefShape.Coral)
            {
                // Every coral dial by name. CoralGeometryTests nudges every tunable of the group
                // through the schema, so a dial added to the group and not here fails there.
                var d = new RunConfig();
                var off = new List<string>();

                void Check(string dial, IFormattable value, IFormattable standard)
                {
                    if (!value.Equals(standard)) off.Add(dial + " " + value.ToString(null, System.Globalization.CultureInfo.InvariantCulture));
                }

                Check("CoralIslandShare", config.CoralIslandShare, d.CoralIslandShare);
                Check("CoralIslandRoof", config.CoralIslandRoof, d.CoralIslandRoof);
                Check("CoralMaxColonies", config.CoralMaxColonies, d.CoralMaxColonies);
                Check("CoralMaxPiecesPerColony", config.CoralMaxPiecesPerColony, d.CoralMaxPiecesPerColony);
                Check("CoralMaxPieces", config.CoralMaxPieces, d.CoralMaxPieces);
                Check("CoralTrunkHeightMinMetres", config.CoralTrunkHeightMinMetres, d.CoralTrunkHeightMinMetres);
                Check("CoralTrunkHeightMaxMetres", config.CoralTrunkHeightMaxMetres, d.CoralTrunkHeightMaxMetres);
                Check("CoralTrunkRadiusMinMetres", config.CoralTrunkRadiusMinMetres, d.CoralTrunkRadiusMinMetres);
                Check("CoralTrunkRadiusMaxMetres", config.CoralTrunkRadiusMaxMetres, d.CoralTrunkRadiusMaxMetres);
                Check("CoralChildrenMin", config.CoralChildrenMin, d.CoralChildrenMin);
                Check("CoralChildrenMax", config.CoralChildrenMax, d.CoralChildrenMax);
                Check("CoralTiltMeanDegrees", config.CoralTiltMeanDegrees, d.CoralTiltMeanDegrees);
                Check("CoralTiltSpreadDegrees", config.CoralTiltSpreadDegrees, d.CoralTiltSpreadDegrees);
                Check("CoralLengthFactor", config.CoralLengthFactor, d.CoralLengthFactor);
                Check("CoralRadiusFactor", config.CoralRadiusFactor, d.CoralRadiusFactor);
                Check("CoralMaxDepth", config.CoralMaxDepth, d.CoralMaxDepth);
                Check("CoralMinRadiusMetres", config.CoralMinRadiusMetres, d.CoralMinRadiusMetres);
                Check("CoralPlateChance", config.CoralPlateChance, d.CoralPlateChance);
                Check("CoralPlateRadiusMinMetres", config.CoralPlateRadiusMinMetres, d.CoralPlateRadiusMinMetres);
                Check("CoralPlateRadiusMaxMetres", config.CoralPlateRadiusMaxMetres, d.CoralPlateRadiusMaxMetres);
                Check("CoralPlateThicknessMetres", config.CoralPlateThicknessMetres, d.CoralPlateThicknessMetres);
                Check("CoralFilletMetres", config.CoralFilletMetres, d.CoralFilletMetres);
                Check("CoralFadeMetres", config.CoralFadeMetres, d.CoralFadeMetres);
                Check("CoralFadeFloor", config.CoralFadeFloor, d.CoralFadeFloor);
                Check("CoralTransmission", config.CoralTransmission, d.CoralTransmission);
                Check("CoralGridCellMetres", config.CoralGridCellMetres, d.CoralGridCellMetres);

                if (off.Count == 0) return;

                throw new ArgumentException(
                    "ReefShape is " + config.ReefShape + " and the coral group sets " + string.Join(", ", off) +
                    ". With no coral every coral dial stands at its default, or the config's hash would carry a " +
                    "coral the world does not have. logbook/specs/coral-build.md.",
                    nameof(config));
            }

            if (config.ReefCover > 0f)
            {
                throw new ArgumentException(
                    FormattableString.Invariant($"ReefShape is Coral and ReefCover is {config.ReefCover}. ") +
                    "The coral replaces the mushrooms (D194): set ReefCover 0, and the reef group stands at its " +
                    "defaults. logbook/specs/coral-build.md.",
                    nameof(config));
            }

            if (!(config.MatterIslandWavelengthMetres > 0f))
            {
                throw new ArgumentException(
                    "ReefShape is Coral and the world has no islands (MatterIslandWavelengthMetres 0). The coral " +
                    "grows from the islands' columns (D194), so it needs them. logbook/specs/coral-build.md.",
                    nameof(config));
            }

            if (worldRules && (config.WorldShape != WorldShape.Tank) != footprint.IsBox)
            {
                throw new ArgumentException(
                    footprint.IsBox
                        ? FormattableString.Invariant($"WorldShape is {config.WorldShape} and the coral was placed over a box. ") +
                          "The square's coral (D204) is the box's, and the tank's is placed over its disc. " +
                          "logbook/specs/coral-build.md."
                        : FormattableString.Invariant($"WorldShape is {config.WorldShape} and ReefShape is Coral. ") +
                          "The coral is the tank's, as the reefs are: its fade is on the streams' potential and its " +
                          "rock is a mask on the tank's grid. logbook/specs/coral-build.md.",
                    nameof(config));
            }

            if (worldRules && config.FieldModel != MatterField.Grid)
            {
                throw new ArgumentException(
                    FormattableString.Invariant($"FieldModel is {config.FieldModel} and ReefShape is Coral. ") +
                    "The coral's island test reads the grid's columns. logbook/specs/coral-build.md.",
                    nameof(config));
            }

            var wrong = new List<string>();

            if (config.CoralIslandShare < LeastIslandShare)
                wrong.Add(FormattableString.Invariant($"CoralIslandShare {config.CoralIslandShare} is under D194's {LeastIslandShare}"));
            if (!(config.CoralIslandRoof > 0f))
                wrong.Add("CoralIslandRoof is 0, which grows no coral");
            if (!(config.CoralTrunkHeightMinMetres > 0f) || config.CoralTrunkHeightMaxMetres < config.CoralTrunkHeightMinMetres)
                wrong.Add(FormattableString.Invariant($"the trunk's height runs {config.CoralTrunkHeightMinMetres} to {config.CoralTrunkHeightMaxMetres} m, not from above 0 upward"));
            if (!(config.CoralTrunkRadiusMinMetres > 0f) || config.CoralTrunkRadiusMaxMetres < config.CoralTrunkRadiusMinMetres)
                wrong.Add(FormattableString.Invariant($"the trunk's radius runs {config.CoralTrunkRadiusMinMetres} to {config.CoralTrunkRadiusMaxMetres} m, not from above 0 upward"));
            if (config.CoralChildrenMax < config.CoralChildrenMin)
                wrong.Add(FormattableString.Invariant($"the children run {config.CoralChildrenMin} to {config.CoralChildrenMax}"));
            if (config.CoralTiltMeanDegrees - config.CoralTiltSpreadDegrees < 0f || config.CoralTiltMeanDegrees + config.CoralTiltSpreadDegrees > 180f)
                wrong.Add(FormattableString.Invariant($"a tilt of {config.CoralTiltMeanDegrees} ± {config.CoralTiltSpreadDegrees} degrees leaves 0 to 180"));
            if (!(config.CoralLengthFactor > 0f) || config.CoralLengthFactor > 1f)
                wrong.Add(FormattableString.Invariant($"CoralLengthFactor {config.CoralLengthFactor} is not above 0 and at most 1"));
            if (!(config.CoralRadiusFactor > 0f) || config.CoralRadiusFactor > 1f)
                wrong.Add(FormattableString.Invariant($"CoralRadiusFactor {config.CoralRadiusFactor} is not above 0 and at most 1"));
            if (!(config.CoralMinRadiusMetres > 0f))
                wrong.Add("CoralMinRadiusMetres is 0");
            if (!(config.CoralPlateRadiusMinMetres > 0f) || config.CoralPlateRadiusMaxMetres < config.CoralPlateRadiusMinMetres)
                wrong.Add(FormattableString.Invariant($"the plate's radius runs {config.CoralPlateRadiusMinMetres} to {config.CoralPlateRadiusMaxMetres} m, not from above 0 upward"));
            if (!(config.CoralPlateThicknessMetres > 0f))
                wrong.Add("CoralPlateThicknessMetres is 0");
            if (!(config.CoralFadeMetres > 0f))
                wrong.Add("CoralFadeMetres is 0");
            if (!(config.CoralGridCellMetres > 0f))
                wrong.Add("CoralGridCellMetres is 0");

            double margin = Math.Max(config.MatterIslandGlassMarginMetres, ReefGeometry.GlassClearanceMetres);
            if (footprint.IsBox)
            {
                double side = Math.Min(footprint.LengthMetres, footprint.WidthMetres);
                if (!(side - 2d * margin - 2d * config.CoralTrunkRadiusMinMetres > 0d))
                    wrong.Add(FormattableString.Invariant($"a margin of {margin} m from every face leaves no room for a trunk in a box {side:0.##} m on its shorter side"));
            }
            else if (!(tankRadiusMetres - margin - config.CoralTrunkRadiusMinMetres > 0d))
                wrong.Add(FormattableString.Invariant($"a glass margin of {margin} m leaves no room for a trunk in a tank {tankRadiusMetres:0.##} m in radius"));

            if (wrong.Count == 0) return;

            throw new ArgumentException(
                "The coral group is refused: " + string.Join("; ", wrong) + ". logbook/specs/coral-build.md.",
                nameof(config));
        }

        // ------------------------------------------------------------------ the placement

        /// <summary>
        /// The world's coral, placed from the landscape's seed as <see cref="World"/> will place it:
        /// the island map from <see cref="World.IslandMapIndex"/> and the draws from
        /// <see cref="World.ReefPlacementIndex"/>, both of the landscape seed (D192). Null at
        /// <see cref="ReefShape.Mushroom"/>.
        /// </summary>
        public static CoralGeometry PlaceFromLandscape(
            RunConfig config, float tankRadiusMetres, BedShape bed, ulong landscapeSeed, bool worldRules = true) =>
            PlaceFromLandscape(config, CoralFootprint.Tank(tankRadiusMetres), bed, landscapeSeed, worldRules);

        /// <inheritdoc cref="PlaceFromLandscape(RunConfig, float, BedShape, ulong, bool)"/>
        public static CoralGeometry PlaceFromLandscape(
            RunConfig config, CoralFootprint footprint, BedShape bed, ulong landscapeSeed, bool worldRules = true)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            Refuse(config, footprint, worldRules);
            if (config.ReefShape != ReefShape.Coral) return null;

            var map = new GradientNoise(Rng.SeedFor(landscapeSeed, World.IslandMapIndex), config.MatterIslandWavelengthMetres);
            return Place(config, footprint, bed, map.Fbm, Rng.SeedFor(landscapeSeed, World.ReefPlacementIndex), worldRules);
        }

        /// <summary>
        /// The world's coral, grown from the reef stream on the islands of <paramref name="islandMap"/>.
        /// Null at <see cref="ReefShape.Mushroom"/>, with no draw.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The island test is the coral's own</b> (<see cref="CoralIslandColumns"/>): the matter
        /// grid's columns, live by the glass and the bed alone, less the islands' glass margin, with
        /// the seeding's threshold. Rounds 60 and 61 keep their worlds, and in a world whose mask
        /// has no rock it is the seeding's own test, column for column.
        /// </para>
        /// <para>
        /// <b>Each colony first draws its kind</b>, an island colony with
        /// <see cref="RunConfig.CoralIslandShare"/> and a desert one otherwise. A kind whose canopy
        /// is already full spends the draw and the next colony draws again. It then draws a matter
        /// column of its kind, uniformly over the open islands' columns or the desert's, and a
        /// point uniform in that column, and grows there. A trunk that cannot stand (too near the
        /// surface or the glass) is a failed try; <see cref="PlacementAttempts"/> failed tries for
        /// one colony refuse the world.
        /// </para>
        /// <para>
        /// <b>The canopy is counted on 1 m columns</b>, each by the share of its 4 by 4 samples
        /// under a piece's projection. Island i is filled to <see cref="RunConfig.CoralIslandRoof"/>
        /// of its own canopy columns, counting what island colonies cover there. A colony that
        /// would push its island past the target by more than half its own canopy there is drawn
        /// again elsewhere and closes that island, as the mushroom placer keeps the last reef only
        /// when it lands nearer the target. The desert colonies fill a canopy of
        /// <c>(1 − share)/share</c> of the islands' targets by the same rule. Past
        /// <see cref="RunConfig.CoralMaxColonies"/> colonies or <see cref="RunConfig.CoralMaxPieces"/>
        /// pieces the world is refused, not truncated.
        /// </para>
        /// </remarks>
        public static CoralGeometry Place(
            RunConfig config, float tankRadiusMetres, BedShape bed, Func<float, float, float> islandMap,
            ulong streamSeed, bool worldRules = true) =>
            Place(config, CoralFootprint.Tank(tankRadiusMetres), bed, islandMap, streamSeed, worldRules);

        /// <summary>
        /// The world's coral over a footprint: a tank's disc, or D204's square, where every piece
        /// keeps the islands' margin (or <see cref="ReefGeometry.GlassClearanceMetres"/>, the larger)
        /// from every face. The seams then lie in water with no rock near them, so nothing about the
        /// coral is read the short way round.
        /// </summary>
        public static CoralGeometry Place(
            RunConfig config, CoralFootprint footprint, BedShape bed, Func<float, float, float> islandMap,
            ulong streamSeed, bool worldRules = true)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            Refuse(config, footprint, worldRules);
            if (config.ReefShape != ReefShape.Coral) return null;
            if (islandMap == null) throw new ArgumentNullException(nameof(islandMap));

            BedShape floor = bed != null && bed.HasRelief ? bed : null;
            CoralIslandColumns columns = CoralIslandColumns.Build(
                footprint, config.FieldMatterCellMetres, config.WorldDepthMetres, floor, islandMap,
                config.MatterIslandCover, config.MatterIslandGlassMarginMetres);

            if (columns.IslandCount == 0)
            {
                throw new ArgumentException(
                    "ReefShape is Coral and this seed's island map put no live column above its threshold, so there " +
                    "is no island for the coral to grow from. logbook/specs/coral-build.md.",
                    nameof(config));
            }

            return new Placer(config, footprint, floor, columns, streamSeed).Run();
        }

        /// <summary>The placer's state: the canopy's samples, each island's count, and the stream.</summary>
        private sealed class Placer
        {
            private readonly RunConfig _config;
            private readonly double _tankRadius;
            private readonly BedShape _floor;
            private readonly CoralIslandColumns _columns;
            private readonly Rng _rng;

            private readonly double _reachLimit;
            private readonly bool _box;
            private readonly double _margin, _length, _width;
            private readonly double _sampleMetres;
            private readonly int _samplesAlong;
            private readonly int _samplesAcross;
            private readonly int _canopyAlong;
            private readonly int _canopyAcross;
            private readonly int[] _canopyIsland;
            private readonly bool[] _covered;
            private readonly int[] _stamp;
            private int _stampNow;

            private readonly int[] _islandColumns;
            private readonly double[] _target;
            private readonly int[] _got;
            private readonly bool[] _closed;
            private readonly double _desertTarget;
            private int _desertGot;
            private bool _desertClosed;

            private readonly List<Colony> _colonies = new List<Colony>();
            private readonly List<Piece> _pieces = new List<Piece>();
            private readonly List<Piece> _grown = new List<Piece>();
            private readonly List<int> _fresh = new List<int>();

            internal Placer(RunConfig config, CoralFootprint footprint, BedShape floor, CoralIslandColumns columns, ulong streamSeed)
            {
                float tankRadius = footprint.TankRadiusMetres;
                _config = config;
                _tankRadius = tankRadius;
                _floor = floor;
                _columns = columns;
                _rng = new Rng(streamSeed);
                _margin = Math.Max(config.MatterIslandGlassMarginMetres, ReefGeometry.GlassClearanceMetres);
                _reachLimit = tankRadius - _margin;
                _box = footprint.IsBox;
                _length = footprint.LengthMetres;
                _width = footprint.WidthMetres;

                // The canopy's columns over the tank's square, or the box, and their samples: along x,
                // then across z.
                _canopyAlong = _box
                    ? (int)Math.Ceiling(_length / CanopyColumnMetres - 1e-6)
                    : (int)Math.Ceiling(2d * tankRadius / CanopyColumnMetres - 1e-6);
                _canopyAcross = _box ? (int)Math.Ceiling(_width / CanopyColumnMetres - 1e-6) : _canopyAlong;
                _samplesAlong = _canopyAlong * CanopySamplesAcross;
                _samplesAcross = _canopyAcross * CanopySamplesAcross;
                _sampleMetres = CanopyColumnMetres / (double)CanopySamplesAcross;
                _canopyIsland = new int[_canopyAlong * _canopyAcross];
                _covered = new bool[_samplesAlong * _samplesAcross];
                _stamp = new int[_samplesAlong * _samplesAcross];

                // Each canopy column belongs to the matter column its centre lies in.
                int islands = columns.IslandCount;
                _islandColumns = new int[islands];

                for (int ci = 0; ci < _canopyAlong; ci++)
                {
                    double cx = (ci + 0.5d) * CanopyColumnMetres;
                    for (int cj = 0; cj < _canopyAcross; cj++)
                    {
                        double cz = (cj + 0.5d) * CanopyColumnMetres;
                        int label = columns.IslandAt(cx, cz);
                        _canopyIsland[ci * _canopyAcross + cj] = label;
                        if (label >= 0) _islandColumns[label]++;
                    }
                }

                double perColumn = CanopySamplesAcross * CanopySamplesAcross;
                _target = new double[islands];
                _got = new int[islands];
                _closed = new bool[islands];
                double islandTarget = 0d;

                for (int i = 0; i < islands; i++)
                {
                    _target[i] = config.CoralIslandRoof * perColumn * _islandColumns[i];
                    islandTarget += _target[i];
                    if (!(_target[i] > 0d)) _closed[i] = true;
                }

                double share = config.CoralIslandShare;
                _desertTarget = share < 1d ? (1d - share) / share * islandTarget : 0d;
                _desertClosed = !(_desertTarget > 0d) || columns.DesertColumns.Count == 0;
            }

            private bool AnyIslandOpen()
            {
                for (int i = 0; i < _closed.Length; i++)
                {
                    if (!_closed[i]) return true;
                }

                return false;
            }

            internal CoralGeometry Run()
            {
                float share = _config.CoralIslandShare;

                while (AnyIslandOpen() || !_desertClosed)
                {
                    bool island = _rng.NextFloat() < share;
                    if (island ? !AnyIslandOpen() : _desertClosed) continue;
                    PlaceOne(island);
                }

                return new CoralGeometry(
                    _config.CoralFilletMetres, _colonies.ToArray(), _pieces.ToArray(),
                    _target, _got, _islandColumns, _desertTarget, _desertGot, _config.CoralIslandRoof);
            }

            /// <summary>One colony of a kind: placed, or its kind closed, or the world refused.</summary>
            private void PlaceOne(bool island)
            {
                for (int attempt = 0; attempt < PlacementAttempts; attempt++)
                {
                    if (island ? !AnyIslandOpen() : _desertClosed) return;

                    int column = DrawColumn(island);
                    float cell = _columns.CellMetres;
                    int ix = column / _columns.CellsAcross;
                    int iz = column % _columns.CellsAcross;
                    double x = (ix + (double)_rng.NextFloat()) * cell;
                    double z = (iz + (double)_rng.NextFloat()) * cell;
                    int label = island ? _columns.IslandOf(ix, iz) : -1;

                    double baseY = _floor != null ? _floor.FloorY(x, z) : -(double)_config.WorldDepthMetres;
                    if (!Grow(x, baseY, z)) continue;

                    CountFresh();

                    if (island)
                    {
                        int onIsland = 0;
                        foreach (int s in _fresh)
                        {
                            if (_canopyIsland[CanopyColumnOf(s)] == label) onIsland++;
                        }

                        if (_got[label] + onIsland - _target[label] > 0.5d * onIsland)
                        {
                            _closed[label] = true;
                            continue;
                        }
                    }
                    else if (_desertGot + _fresh.Count - _desertTarget > 0.5d * _fresh.Count)
                    {
                        _desertClosed = true;
                        continue;
                    }

                    Commit(x, baseY, z, label);
                    return;
                }

                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"The seed's reef stream found no spot for coral colony {_colonies.Count} ({(island ? "island" : "desert")}) ") +
                    FormattableString.Invariant(
                        $"in {PlacementAttempts} tries: no trunk stood {SurfaceClearanceMetres} m under the surface and ") +
                    FormattableString.Invariant(
                        $"inside the glass margin on its kind's columns. logbook/specs/coral-build.md."),
                    "config");
            }

            private int DrawColumn(bool island)
            {
                if (!island)
                {
                    IReadOnlyList<int> desert = _columns.DesertColumns;
                    return desert[_rng.Range(desert.Count)];
                }

                int total = 0;
                for (int i = 0; i < _closed.Length; i++)
                {
                    if (!_closed[i]) total += _columns.IslandColumns(i).Count;
                }

                int k = _rng.Range(total);
                for (int i = 0; i < _closed.Length; i++)
                {
                    if (_closed[i]) continue;
                    IReadOnlyList<int> list = _columns.IslandColumns(i);
                    if (k < list.Count) return list[k];
                    k -= list.Count;
                }

                throw new InvalidOperationException("An open island's column was drawn past the open islands.");
            }

            private void Commit(double x, double baseY, double z, int label)
            {
                if (_colonies.Count >= _config.CoralMaxColonies)
                {
                    throw new ArgumentException(
                        FormattableString.Invariant(
                            $"The seed's reef stream reached the ceiling of {_config.CoralMaxColonies} coral colonies with ") +
                        FormattableString.Invariant(
                            $"{Unfilled()} still to fill. Raise CoralMaxColonies or lower CoralIslandRoof. logbook/specs/coral-build.md."),
                        "config");
                }

                if (_pieces.Count + _grown.Count > _config.CoralMaxPieces)
                {
                    throw new ArgumentException(
                        FormattableString.Invariant(
                            $"The coral would hold {_pieces.Count + _grown.Count} pieces with colony {_colonies.Count}, past ") +
                        FormattableString.Invariant(
                            $"the ceiling of {_config.CoralMaxPieces}, with {Unfilled()} still to fill. Raise CoralMaxPieces ") +
                        "or lower CoralIslandRoof or CoralMaxPiecesPerColony. logbook/specs/coral-build.md.",
                        "config");
                }

                int colony = _colonies.Count;
                _colonies.Add(new Colony
                {
                    X = x,
                    Z = z,
                    BaseY = baseY,
                    Island = label,
                    FirstPiece = _pieces.Count,
                    PieceCount = _grown.Count,
                    CanopySamples = _fresh.Count,
                });

                foreach (Piece p in _grown)
                {
                    Piece q = p;
                    q.Colony = colony;
                    _pieces.Add(q);
                }

                foreach (int s in _fresh)
                {
                    _covered[s] = true;
                    if (label < 0) continue;
                    int on = _canopyIsland[CanopyColumnOf(s)];
                    if (on >= 0) _got[on]++;
                }

                if (label < 0)
                {
                    _desertGot += _fresh.Count;
                    if (_desertGot >= _desertTarget) _desertClosed = true;
                }

                for (int i = 0; i < _got.Length; i++)
                {
                    if (_got[i] >= _target[i]) _closed[i] = true;
                }
            }

            private string Unfilled()
            {
                int open = 0;
                for (int i = 0; i < _closed.Length; i++)
                {
                    if (!_closed[i]) open++;
                }

                return FormattableString.Invariant($"{open} of {_closed.Length} islands") + (_desertClosed ? "" : " and the desert");
            }

            private int CanopyColumnOf(int sample)
            {
                int p = sample / _samplesAcross;
                int q = sample % _samplesAcross;
                return (p / CanopySamplesAcross) * _canopyAcross + q / CanopySamplesAcross;
            }

            // ---------------------------------------------------------- growth

            /// <summary>Grows one colony at a point into <see cref="_grown"/>; false when its trunk cannot stand.</summary>
            private bool Grow(double x, double baseY, double z)
            {
                RunConfig c = _config;
                _grown.Clear();

                double height = c.CoralTrunkHeightMinMetres + (c.CoralTrunkHeightMaxMetres - (double)c.CoralTrunkHeightMinMetres) * _rng.NextFloat();
                double radius = c.CoralTrunkRadiusMinMetres + (c.CoralTrunkRadiusMaxMetres - (double)c.CoralTrunkRadiusMinMetres) * _rng.NextFloat();

                var trunk = new Piece
                {
                    Kind = PieceKind.Capsule,
                    Parent = -1,
                    Depth = 0,
                    AX = x, AY = baseY, AZ = z,
                    BX = x, BY = baseY + height, BZ = z,
                    Radius = radius,
                    TiltCos = 1d, AzimuthCos = 1d,
                };

                if (!Fits(trunk)) return false;
                _grown.Add(trunk);

                int cap = c.CoralMaxPiecesPerColony;
                double plateHalf = 0.5d * c.CoralPlateThicknessMetres;

                for (int next = 0; next < _grown.Count; next++)
                {
                    Piece parent = _grown[next];
                    if (parent.Kind != PieceKind.Capsule) continue;
                    if (_grown.Count >= cap) break;

                    int grown = 0;
                    double childRadius = parent.Radius * c.CoralRadiusFactor;

                    if (parent.Depth < c.CoralMaxDepth && childRadius >= c.CoralMinRadiusMetres)
                    {
                        int children = _rng.Range(c.CoralChildrenMin, c.CoralChildrenMax + 1);

                        // The parent's axis and two directions square to it, chosen without trig.
                        double ax = parent.BX - parent.AX, ay = parent.BY - parent.AY, az = parent.BZ - parent.AZ;
                        double length = Math.Sqrt(ax * ax + ay * ay + az * az);
                        ax /= length;
                        ay /= length;
                        az /= length;

                        double rx = Math.Abs(ax) < 0.9d ? 1d : 0d, rz = Math.Abs(ax) < 0.9d ? 0d : 1d;
                        double along = rx * ax + rz * az;
                        double ux = rx - along * ax, uy = -along * ay, uz = rz - along * az;
                        double un = Math.Sqrt(ux * ux + uy * uy + uz * uz);
                        ux /= un;
                        uy /= un;
                        uz /= un;

                        // w = a × u.
                        double wx = ay * uz - az * uy, wy = az * ux - ax * uz, wz = ax * uy - ay * ux;
                        double childLength = length * c.CoralLengthFactor;

                        for (int k = 0; k < children; k++)
                        {
                            double tilt = (c.CoralTiltMeanDegrees + c.CoralTiltSpreadDegrees * (2d * _rng.NextFloat() - 1d)) * (Math.PI / 180d);
                            double azimuth = 2d * Math.PI * _rng.NextFloat();
                            double tc = Math.Cos(tilt), ts = Math.Sin(tilt);
                            double pc = Math.Cos(azimuth), ps = Math.Sin(azimuth);

                            double dx = tc * ax + ts * (pc * ux + ps * wx);
                            double dy = tc * ay + ts * (pc * uy + ps * wy);
                            double dz = tc * az + ts * (pc * uz + ps * wz);
                            double dn = Math.Sqrt(dx * dx + dy * dy + dz * dz);

                            var child = new Piece
                            {
                                Kind = PieceKind.Capsule,
                                Parent = next,
                                Depth = parent.Depth + 1,
                                AX = parent.BX, AY = parent.BY, AZ = parent.BZ,
                                BX = parent.BX + childLength * dx / dn,
                                BY = parent.BY + childLength * dy / dn,
                                BZ = parent.BZ + childLength * dz / dn,
                                Radius = childRadius,
                                TiltCos = tc, TiltSin = ts,
                                AzimuthCos = pc, AzimuthSin = ps,
                            };

                            if (_grown.Count < cap && Fits(child))
                            {
                                _grown.Add(child);
                                grown++;
                            }
                        }
                    }

                    // A tip: a capsule that grew no child carries a plate at its end, by chance.
                    if (grown == 0 && _grown.Count < cap && _rng.NextFloat() < c.CoralPlateChance)
                    {
                        double plateRadius = c.CoralPlateRadiusMinMetres +
                                             (c.CoralPlateRadiusMaxMetres - (double)c.CoralPlateRadiusMinMetres) * _rng.NextFloat();

                        var plate = new Piece
                        {
                            Kind = PieceKind.Plate,
                            Parent = next,
                            Depth = parent.Depth + 1,
                            AX = parent.BX, AY = parent.BY, AZ = parent.BZ,
                            BX = parent.BX, BY = parent.BY, BZ = parent.BZ,
                            Radius = plateRadius,
                            HalfThickness = plateHalf,
                            TiltCos = 1d, AzimuthCos = 1d,
                        };

                        if (Fits(plate)) _grown.Add(plate);
                    }
                }

                return true;
            }

            /// <summary>Whether a piece stays <see cref="SurfaceClearanceMetres"/> under the surface and outside the glass margin.</summary>
            private bool Fits(in Piece p)
            {
                double top, reach;

                if (p.Kind == PieceKind.Capsule)
                {
                    top = Math.Max(p.AY, p.BY) + p.Radius;
                    reach = Math.Max(FromAxis(p.AX, p.AZ), FromAxis(p.BX, p.BZ)) + p.Radius;
                }
                else
                {
                    top = p.AY + p.HalfThickness;
                    reach = FromAxis(p.AX, p.AZ) + p.Radius + p.HalfThickness;
                }

                if (_box) return top <= -(double)SurfaceClearanceMetres && InsideFaces(p);

                return top <= -(double)SurfaceClearanceMetres && reach <= _reachLimit;
            }

            /// <summary>Whether a piece's footprint stays the margin inside every face of the box.</summary>
            private bool InsideFaces(in Piece p)
            {
                double reach = p.Kind == PieceKind.Capsule ? p.Radius : p.Radius + p.HalfThickness;
                double minX = Math.Min(p.AX, p.BX) - reach, maxX = Math.Max(p.AX, p.BX) + reach;
                double minZ = Math.Min(p.AZ, p.BZ) - reach, maxZ = Math.Max(p.AZ, p.BZ) + reach;

                return minX >= _margin && maxX <= _length - _margin &&
                       minZ >= _margin && maxZ <= _width - _margin;
            }

            private double FromAxis(double x, double z)
            {
                double dx = x - _tankRadius, dz = z - _tankRadius;
                return Math.Sqrt(dx * dx + dz * dz);
            }

            // ---------------------------------------------------------- canopy

            /// <summary>The canopy samples the grown colony covers that no colony covered before, into <see cref="_fresh"/>.</summary>
            private void CountFresh()
            {
                _fresh.Clear();
                _stampNow++;
                double h = _sampleMetres;

                foreach (Piece p in _grown)
                {
                    double reach = p.Kind == PieceKind.Plate ? p.Radius + p.HalfThickness : p.Radius;
                    double minX = Math.Min(p.AX, p.BX) - reach, maxX = Math.Max(p.AX, p.BX) + reach;
                    double minZ = Math.Min(p.AZ, p.BZ) - reach, maxZ = Math.Max(p.AZ, p.BZ) + reach;

                    int p0 = Math.Max(0, (int)Math.Floor(minX / h - 0.5d));
                    int p1 = Math.Min(_samplesAlong - 1, (int)Math.Ceiling(maxX / h - 0.5d));
                    int q0 = Math.Max(0, (int)Math.Floor(minZ / h - 0.5d));
                    int q1 = Math.Min(_samplesAcross - 1, (int)Math.Ceiling(maxZ / h - 0.5d));

                    double sx = p.BX - p.AX, sz = p.BZ - p.AZ;
                    double run2 = sx * sx + sz * sz;
                    double reach2 = reach * reach;

                    for (int pi = p0; pi <= p1; pi++)
                    {
                        double x = (pi + 0.5d) * h;
                        for (int qi = q0; qi <= q1; qi++)
                        {
                            int sample = pi * _samplesAcross + qi;
                            if (_covered[sample] || _stamp[sample] == _stampNow) continue;

                            double z = (qi + 0.5d) * h;
                            double ex = x - p.AX, ez = z - p.AZ;

                            // The point's distance from the piece's projection: a disc, or a stadium.
                            if (run2 > 0d)
                            {
                                double t = (ex * sx + ez * sz) / run2;
                                if (t < 0d) t = 0d;
                                else if (t > 1d) t = 1d;
                                ex -= t * sx;
                                ez -= t * sz;
                            }

                            if (ex * ex + ez * ez > reach2) continue;

                            _stamp[sample] = _stampNow;
                            _fresh.Add(sample);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// The coral's island test (<c>logbook/specs/coral-build.md</c>, the survey's option (a)): the
    /// matter grid's columns, live by the glass and the bed alone, less D185's glass margin, split
    /// at the seeding's threshold into island columns and desert ones, and the island columns
    /// joined into islands across their sides and corners.
    /// </summary>
    /// <remarks>
    /// <see cref="GridField.PlanIslands"/>'s arithmetic, step for step and in its own floats, with
    /// the reefs left out of the live test. In a world whose mask has no rock in it the two agree
    /// on every column; with rock, a column whose every cell is rock is live here and dead there.
    /// </remarks>
    public sealed class CoralIslandColumns
    {
        /// <summary>A column outside the glass, filled by the bed, or inside the margin.</summary>
        public const int Ineligible = -2;

        /// <summary>A desert column.</summary>
        public const int Desert = -1;

        private readonly int[] _label;
        private readonly List<int>[] _islands;
        private readonly List<int> _desert;

        private CoralIslandColumns(float cellMetres, int along, int across, float threshold, int[] label, List<int>[] islands, List<int> desert)
        {
            CellMetres = cellMetres;
            CellsAlong = along;
            CellsAcross = across;
            Threshold = threshold;
            _label = label;
            _islands = islands;
            _desert = desert;
        }

        /// <summary>The matter grid's cell, m.</summary>
        public float CellMetres { get; }

        /// <summary>Columns along x: across the tank's square, or along the box.</summary>
        public int CellsAlong { get; }

        /// <summary>Columns across z, the stride of a column's index (<c>ix · across + iz</c>).</summary>
        public int CellsAcross { get; }

        /// <summary>The map's value at and below which a column is desert.</summary>
        public float Threshold { get; }

        /// <summary>How many islands.</summary>
        public int IslandCount => _islands.Length;

        /// <summary>An island's columns, as <c>ix · across + iz</c>, ascending.</summary>
        public IReadOnlyList<int> IslandColumns(int island) => _islands[island];

        /// <summary>The desert's columns, ascending.</summary>
        public IReadOnlyList<int> DesertColumns => _desert;

        /// <summary>A column's island, or <see cref="Desert"/> or <see cref="Ineligible"/>.</summary>
        public int IslandOf(int ix, int iz) =>
            ix < 0 || iz < 0 || ix >= CellsAlong || iz >= CellsAcross ? Ineligible : _label[ix * CellsAcross + iz];

        /// <summary>Whether a column is an island column.</summary>
        public bool IsIslandColumn(int ix, int iz) => IslandOf(ix, iz) >= 0;

        /// <summary>The island of the column a horizontal point lies in, or <see cref="Desert"/> or <see cref="Ineligible"/>.</summary>
        public int IslandAt(double x, double z) =>
            IslandOf((int)Math.Floor(x / CellMetres), (int)Math.Floor(z / CellMetres));

        /// <summary>The island test on a tank's matter columns.</summary>
        /// <param name="tankRadiusMetres">The tank's radius, m.</param>
        /// <param name="cellMetres">The matter grid's cell, m.</param>
        /// <param name="worldDepthMetres">The tank's depth, m: the floor where there is no bed.</param>
        /// <param name="bed">The bed, or null for a flat floor.</param>
        /// <param name="map">The island map.</param>
        /// <param name="cover">The share of the eligible columns that are islands.</param>
        /// <param name="glassMarginMetres">D185's margin, m; 0 leaves none out.</param>
        public static CoralIslandColumns Build(
            float tankRadiusMetres, float cellMetres, float worldDepthMetres, BedShape bed,
            Func<float, float, float> map, float cover, float glassMarginMetres) =>
            Build(CoralFootprint.Tank(tankRadiusMetres), cellMetres, worldDepthMetres, bed, map, cover, glassMarginMetres);

        /// <summary>
        /// The island test on a footprint's matter columns: a tank's, or D204's box, whose columns
        /// are all inside and whose margin is from every face (<see cref="GridField.PlanIslands"/>).
        /// </summary>
        public static CoralIslandColumns Build(
            CoralFootprint footprint, float cellMetres, float worldDepthMetres, BedShape bed,
            Func<float, float, float> map, float cover, float glassMarginMetres)
        {
            float tankRadiusMetres = footprint.TankRadiusMetres;
            bool box = footprint.IsBox;

            if (map == null) throw new ArgumentNullException(nameof(map));
            if (!(cellMetres > 0f) || float.IsInfinity(cellMetres))
                throw new ArgumentOutOfRangeException(nameof(cellMetres), cellMetres, "A cell is positive and finite.");
            if (!(cover > 0f) || cover > 1f)
                throw new ArgumentOutOfRangeException(nameof(cover), cover, "A cover is a share in (0, 1].");
            if (!(glassMarginMetres >= 0f) || float.IsInfinity(glassMarginMetres))
                throw new ArgumentOutOfRangeException(nameof(glassMarginMetres), glassMarginMetres, "A glass margin is finite and not negative.");

            BedShape floor = bed != null && bed.HasRelief ? bed : null;

            // GridField's own column counts and centres, in its floats: the tank's bounding square
            // rounded up, or the box's sides, which a cell divides.
            float length = 2f * tankRadiusMetres;
            int nx = box
                ? (int)Math.Round(footprint.LengthMetres / cellMetres)
                : (int)Math.Ceiling(length / cellMetres - 1e-6);
            int n = box ? (int)Math.Round(footprint.WidthMetres / cellMetres) : nx;
            double inner = tankRadiusMetres - (double)glassMarginMetres;
            bool margined = glassMarginMetres > 0f;

            var value = new float[nx * n];
            var label = new int[nx * n];
            var eligible = new List<int>();

            for (int ix = 0; ix < nx; ix++)
            {
                float cx = (ix + 0.5f) * cellMetres;

                for (int iz = 0; iz < n; iz++)
                {
                    int column = ix * n + iz;
                    label[column] = Ineligible;

                    float cz = (iz + 0.5f) * cellMetres;
                    if (!box && !TankGeometry.Inside(cx, cz, tankRadiusMetres)) continue;

                    // Live when the top cell's centre is above the floor: the grid's test, without the reefs.
                    float floorY = floor != null ? (float)floor.FloorY(cx, cz) : -worldDepthMetres;
                    if (-((0 + 0.5f) * cellMetres) <= floorY) continue;

                    if (margined && !box)
                    {
                        double dx = cx - (double)tankRadiusMetres, dz = cz - (double)tankRadiusMetres;
                        if (Math.Sqrt(dx * dx + dz * dz) > inner) continue;
                    }
                    else if (margined &&
                             (cx < glassMarginMetres || cx > footprint.LengthMetres - glassMarginMetres ||
                              cz < glassMarginMetres || cz > footprint.WidthMetres - glassMarginMetres))
                    {
                        continue;
                    }

                    value[column] = map(cx, cz);
                    eligible.Add(column);
                }
            }

            var desert = new List<int>();
            if (eligible.Count == 0)
            {
                return new CoralIslandColumns(cellMetres, nx, n, 0f, label, Array.Empty<List<int>>(), desert);
            }

            var sorted = new float[eligible.Count];
            for (int i = 0; i < sorted.Length; i++) sorted[i] = value[eligible[i]];
            Array.Sort(sorted);

            int first = (int)Math.Floor((1d - cover) * sorted.Length);
            if (first >= sorted.Length) first = sorted.Length - 1;
            if (first < 0) first = 0;
            float threshold = sorted[first];

            float ramp = 0.25f * (sorted[sorted.Length - 1] - threshold);
            var island = new bool[nx * n];

            foreach (int column in eligible)
            {
                // PlanIslands' weight, and an island column is one whose weight is above 0.
                float excess = value[column] - threshold;
                float weight = !(excess > 0f) ? 0f : ramp > 0f && excess < ramp ? excess / ramp : 1f;

                if (weight > 0f) island[column] = true;
                else label[column] = Desert;
            }

            foreach (int column in eligible)
            {
                if (!island[column]) desert.Add(column);
            }

            // The islands: island columns joined across their sides and corners, labelled in
            // ascending order of their first column.
            var islands = new List<List<int>>();
            var queue = new List<int>();

            foreach (int start in eligible)
            {
                if (!island[start] || label[start] != Ineligible) continue;

                int id = islands.Count;
                var members = new List<int>();
                queue.Clear();
                queue.Add(start);
                label[start] = id;

                for (int head = 0; head < queue.Count; head++)
                {
                    int column = queue[head];
                    members.Add(column);
                    int ix = column / n, iz = column % n;

                    for (int jx = ix - 1; jx <= ix + 1; jx++)
                    {
                        for (int jz = iz - 1; jz <= iz + 1; jz++) Visit(jx, jz);
                    }

                    void Visit(int jx, int jz)
                    {
                        if (jx < 0 || jz < 0 || jx >= nx || jz >= n) return;
                        int neighbour = jx * n + jz;
                        if (!island[neighbour] || label[neighbour] != Ineligible) return;
                        label[neighbour] = id;
                        queue.Add(neighbour);
                    }
                }

                members.Sort();
                islands.Add(members);
            }

            return new CoralIslandColumns(cellMetres, nx, n, threshold, label, islands.ToArray(), desert);
        }
    }

    /// <summary>
    /// Where the coral may stand: a tank's disc about <c>(R, R)</c>, or D204's square of
    /// <c>[0, L) × [0, W)</c>.
    /// </summary>
    public readonly struct CoralFootprint
    {
        private CoralFootprint(float tankRadiusMetres, float lengthMetres, float widthMetres)
        {
            TankRadiusMetres = tankRadiusMetres;
            LengthMetres = lengthMetres;
            WidthMetres = widthMetres;
        }

        /// <summary>The tank's radius, m; 0 in a box.</summary>
        public float TankRadiusMetres { get; }

        /// <summary>The extent along x, m: the box's side, or the tank's diameter.</summary>
        public float LengthMetres { get; }

        /// <summary>The extent along z, m: the box's side, or the tank's diameter.</summary>
        public float WidthMetres { get; }

        /// <summary>Whether this is the box.</summary>
        public bool IsBox => !(TankRadiusMetres > 0f);

        /// <summary>A tank of this radius.</summary>
        public static CoralFootprint Tank(float radiusMetres) =>
            new CoralFootprint(radiusMetres, 2f * radiusMetres, 2f * radiusMetres);

        /// <summary>D204's box, <c>World.BoxLengthMetres</c> by <c>World.BoxWidthMetres</c>.</summary>
        public static CoralFootprint Box(float lengthMetres, float widthMetres)
        {
            if (!(lengthMetres > 0f) || float.IsInfinity(lengthMetres))
                throw new ArgumentOutOfRangeException(nameof(lengthMetres), lengthMetres, "A box's side is positive and finite.");
            if (!(widthMetres > 0f) || float.IsInfinity(widthMetres))
                throw new ArgumentOutOfRangeException(nameof(widthMetres), widthMetres, "A box's side is positive and finite.");

            return new CoralFootprint(0f, lengthMetres, widthMetres);
        }
    }
}
