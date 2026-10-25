using System;
using System.Globalization;

namespace Evosim.Core
{
    /// <summary>The smells a nose carries as separate channels (D235), and the order its noise is drawn in.</summary>
    public enum NoseSmell
    {
        /// <summary>D167's living leaves, <see cref="SensorChannel.LeafScent"/>: lit square metres, cone-weighted.</summary>
        Leaf = 0,

        /// <summary>The corpses, <see cref="SensorChannel.Scent"/>: joules, cone-weighted.</summary>
        Carrion = 1,

        /// <summary>The marine snow, <see cref="SensorChannel.Chemical"/>: edible joules per cubic metre.</summary>
        Chemical = 2,
    }

    /// <summary>
    /// A nose: the one part that smells in a world whose table lists it — D235, absorbed from
    /// <c>proposals/propose-nose.md</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The table is the switch.</b> Like <see cref="AnchoringCell"/>, the nose is not in
    /// <see cref="CellTypeRegistry.Standard"/>, and a world that wants it appends it
    /// (<see cref="CellTypeRegistry.Appending"/>). In such a world the leaf smell, the carrion
    /// smell and the chemical sense read zero on every part that is not a nose, and the one brain
    /// gives smell ports to nose parts alone (<see cref="PhenotypePart.Smells"/>). A world whose
    /// table lacks it develops, wires and senses as before. Every number the nose needs is on this
    /// type, so it adds no world tunable.
    /// </para>
    /// <para>
    /// <b>A count, not an exact reading.</b> A nose reads the field <c>c</c> at its tip plus
    /// noise, <c>ĉ = max(0, c + σ·z)</c>, with
    /// <c>σ = noise · √((1 + c / background) · referenceArea / A)</c> and <c>A</c> its surface
    /// area. Below the background the noise is fixed and above it the noise grows with the
    /// signal, which is how a Poisson count of captured molecules behaves; so a nose four times
    /// the area has half the noise and detects a smell half as faint. The reading is then
    /// squashed by the world's half-scale as every smell is today. The square-root law is the
    /// standard result for counting, and its biological reading is the proposal's inference.
    /// </para>
    /// <para>
    /// <b>One draw per sniff.</b> <c>z</c> is drawn once per metabolic step (0.5 s) for each
    /// nose part and each smell and held through that step's physics steps, so a brain cannot
    /// average it away by the physics step's length. It is a hash of the world's seed, the body,
    /// the part's path, the sniff and the smell (<see cref="SniffKey"/>, <see cref="Normal"/>):
    /// stateless, so it needs nothing in a checkpoint and does not depend on thread order, and
    /// built from integers and exactly rounded arithmetic so that the card's double kernels can
    /// match it bit for bit. It is the sum of four 16-bit uniforms rather than Box–Muller, which
    /// would need a logarithm and a cosine.
    /// </para>
    /// <para>
    /// <b>The direction form is noisier for a shorter nose.</b> Treating the nose's two halves as
    /// two sensors half its length apart gives gradient noise of <c>4σ / L</c> on each axis of the
    /// part's frame, <c>L</c> the nose's length along its outward axis (the proposal's
    /// derivation, inference). <see cref="GradientSigma"/>.
    /// </para>
    /// <para>
    /// <b>Priced by area.</b> Upkeep is <see cref="CellType.UpkeepWattsPerCubicMetre"/> by volume
    /// plus <see cref="UpkeepWattsPerSquareMetre"/> by surface area, so the size the noise rewards
    /// has a price beside it. ⚠ Every default is unmeasured and is calibrated in the nursery
    /// (D235).
    /// </para>
    /// </remarks>
    public sealed class NoseCell : CellType
    {
        /// <summary>What separates the nose's noise from every other stream drawn from the world's seed.</summary>
        public const ulong NoiseStream = 0x6E6F7365UL; // "nose"

        /// <summary>The draws of a sniff: the three smells, then the gradient's three axes.</summary>
        public const int GradientDrawX = 3;

        /// <summary>
        /// The smallest area and length the noise is computed at, so a degenerate part reads very
        /// noisily rather than dividing by zero.
        /// </summary>
        public const double MinimumExtent = 1e-9;

        /// <summary>Upkeep per square metre of the nose's surface, W/m². D235's 0.5 to start.</summary>
        public float UpkeepWattsPerSquareMetre { get; }

        /// <summary>The area whose noise is <see cref="NoiseOf"/>, m². 0.1 m², about a 13 cm cube's surface.</summary>
        public float ReferenceAreaSquareMetres { get; }

        /// <summary>The leaf smell's noise for a reference nose in clean water, lit m². 0 reads exactly.</summary>
        public float LeafNoise { get; }

        /// <summary>The leaf smell below which its noise is fixed, lit m².</summary>
        public float LeafBackground { get; }

        /// <summary>The carrion smell's noise for a reference nose in clean water, J. 0 reads exactly.</summary>
        public float CarrionNoise { get; }

        /// <summary>The carrion smell below which its noise is fixed, J.</summary>
        public float CarrionBackground { get; }

        /// <summary>The chemical sense's noise for a reference nose in clean water, J/m³. 0 reads exactly.</summary>
        public float ChemicalNoise { get; }

        /// <summary>The chemical sense's density below which its noise is fixed, J/m³.</summary>
        public float ChemicalBackground { get; }

        /// <summary>
        /// Whether water flowing into the nose's face widens its effective area (D235: built
        /// switched off and tested second). See <see cref="EffectiveArea"/>.
        /// </summary>
        public bool FacingIntake { get; }

        /// <summary>The facing speed that doubles the effective area, m/s. The flow sense's full scale, 0.3.</summary>
        public float IntakeFullScaleMetresPerSecond { get; }

        public NoseCell(
            float upkeepWattsPerCubicMetre = 3f,
            float upkeepWattsPerSquareMetre = 0.5f,
            float referenceAreaSquareMetres = 0.1f,
            float leafNoise = 0.25f,
            float leafBackground = 1f,
            float carrionNoise = 5f,
            float carrionBackground = 20f,
            float chemicalNoise = 0.25f,
            float chemicalBackground = 1f,
            bool facingIntake = false,
            float intakeFullScaleMetresPerSecond = 0.3f)
            : base(upkeepWattsPerCubicMetre)
        {
            NotNegative(upkeepWattsPerSquareMetre, nameof(upkeepWattsPerSquareMetre));
            Positive(referenceAreaSquareMetres, nameof(referenceAreaSquareMetres));
            NotNegative(leafNoise, nameof(leafNoise));
            Positive(leafBackground, nameof(leafBackground));
            NotNegative(carrionNoise, nameof(carrionNoise));
            Positive(carrionBackground, nameof(carrionBackground));
            NotNegative(chemicalNoise, nameof(chemicalNoise));
            Positive(chemicalBackground, nameof(chemicalBackground));
            Positive(intakeFullScaleMetresPerSecond, nameof(intakeFullScaleMetresPerSecond));

            UpkeepWattsPerSquareMetre = upkeepWattsPerSquareMetre;
            ReferenceAreaSquareMetres = referenceAreaSquareMetres;
            LeafNoise = leafNoise;
            LeafBackground = leafBackground;
            CarrionNoise = carrionNoise;
            CarrionBackground = carrionBackground;
            ChemicalNoise = chemicalNoise;
            ChemicalBackground = chemicalBackground;
            FacingIntake = facingIntake;
            IntakeFullScaleMetresPerSecond = intakeFullScaleMetresPerSecond;

            // A soft organ (D235 rule 1): no claw, no mouth, no armour, neutral toughness. The
            // base class's defaults, stated again where the rule is.
            AttackMax = 0f;
            IntakeMax = 0f;
            ProtectionMax = 0f;
            ToughnessMax = 1f;
        }

        public override string Id => CellTypeIds.Nose;

        /// <summary>Rose — a sense organ, apart from every feeding type.</summary>
        public override Float3 InspectionColour => new Float3(0.92f, 0.50f, 0.70f);

        public override CellIntake Acquire(in CellContext context) => CellIntake.None;

        /// <summary>The volume's upkeep and the receptor area's.</summary>
        public override float Upkeep(in CellContext context) =>
            base.Upkeep(context) +
            UpkeepWattsPerSquareMetre * Math.Max(0f, context.SurfaceArea) * context.Seconds;

        /// <summary>The noise of a reference nose in clean water for <paramref name="smell"/>.</summary>
        public float NoiseOf(NoseSmell smell) =>
            smell == NoseSmell.Leaf ? LeafNoise : smell == NoseSmell.Carrion ? CarrionNoise : ChemicalNoise;

        /// <summary>The background of <paramref name="smell"/>, below which its noise is fixed.</summary>
        public float BackgroundOf(NoseSmell smell) =>
            smell == NoseSmell.Leaf ? LeafBackground : smell == NoseSmell.Carrion ? CarrionBackground : ChemicalBackground;

        /// <summary>
        /// <c>σ(c, A)</c>: the reading's standard deviation for a nose of area
        /// <paramref name="area"/> in a field of <paramref name="field"/>. 0 when the smell's noise is 0.
        /// </summary>
        public double Sigma(NoseSmell smell, double field, double area)
        {
            double noise = NoiseOf(smell);
            if (!(noise > 0.0)) return 0.0;

            double c = field > 0.0 ? field : 0.0;
            double a = area > MinimumExtent ? area : MinimumExtent;
            return noise * Math.Sqrt((1.0 + c / BackgroundOf(smell)) * ReferenceAreaSquareMetres / a);
        }

        /// <summary><c>max(0, c + σ·z)</c>: what a nose of area <paramref name="area"/> reads, before the squash.</summary>
        public double Read(NoseSmell smell, double field, double area, double z)
        {
            double v = field + Sigma(smell, field, area) * z;
            return v > 0.0 ? v : 0.0;
        }

        /// <summary>
        /// The gradient's noise on each axis, <c>4σ / L</c>, for a nose of length
        /// <paramref name="length"/> along its outward axis.
        /// </summary>
        public double GradientSigma(NoseSmell smell, double field, double area, double length) =>
            4.0 * Sigma(smell, field, area) / (length > MinimumExtent ? length : MinimumExtent);

        /// <summary>
        /// The area a nose counts with: its own, widened by water flowing into its face when
        /// <see cref="FacingIntake"/> is on — <c>A · (1 + max(0, s) / v_s)</c>.
        /// </summary>
        /// <param name="area">The nose's surface area, m².</param>
        /// <param name="facingSpeed">
        /// The body's velocity relative to the water along the nose's outward axis, m/s: positive
        /// when the nose moves into the water ahead of it.
        /// </param>
        public double EffectiveArea(double area, double facingSpeed)
        {
            if (!FacingIntake || !(facingSpeed > 0.0)) return area;
            return area * (1.0 + facingSpeed / IntakeFullScaleMetresPerSecond);
        }

        /// <summary>The key of one nose part's sniff: the world's seed, the body, the part's path and the sniff.</summary>
        /// <param name="seed">The world's seed.</param>
        /// <param name="bodyId">The organism's id.</param>
        /// <param name="partPath">The part's <see cref="PhenotypePart.PathId"/>, the same at any size.</param>
        /// <param name="sniff">
        /// The sniff: the index of the metabolic step it is taken at, which a checkpoint restores.
        /// </param>
        public static ulong SniffKey(ulong seed, long bodyId, ulong partPath, long sniff)
        {
            ulong h = Rng.SeedFor(seed, NoiseStream);
            h = Rng.SeedFor(h, unchecked((ulong)bodyId));
            h = Rng.SeedFor(h, partPath);
            return Rng.SeedFor(h, unchecked((ulong)sniff));
        }

        /// <summary>
        /// The sniff's draw <paramref name="draw"/> (a <see cref="NoseSmell"/>, or
        /// <see cref="GradientDrawX"/> plus an axis): mean 0 and variance 1, close to a bell curve.
        /// </summary>
        /// <remarks>
        /// One 64-bit output of the mixer gives four 16-bit integers <c>k</c>, each read as the
        /// midpoint <c>(k + ½) / 65536</c>, so each has mean exactly ½. Then
        /// <c>z = (u₁ + u₂ + u₃ + u₄ − 2) · √3</c>. Every sum is exact in a double and the one
        /// multiply is rounded once, so the card computes the same bits. Its range is ±2√3.
        /// </remarks>
        public static double Normal(ulong sniffKey, int draw)
        {
            ulong h = Rng.SeedFor(sniffKey, unchecked((ulong)draw));

            double sum =
                (double)(h & 0xFFFFUL) + (double)((h >> 16) & 0xFFFFUL) +
                (double)((h >> 32) & 0xFFFFUL) + (double)(h >> 48);

            // (sum + 4 · ½) / 65536 − 2, folded: (sum − 131070) / 65536 is exact.
            return (sum - 131070.0) * (1.0 / 65536.0) * Sqrt3;
        }

        private const double Sqrt3 = 1.7320508075688772;

        public override void WriteParameters(Json.Writer writer) =>
            writer.Field("upkeepWattsPerSquareMetre", UpkeepWattsPerSquareMetre)
                  .Field("referenceAreaSquareMetres", ReferenceAreaSquareMetres)
                  .Field("leafNoise", LeafNoise)
                  .Field("leafBackground", LeafBackground)
                  .Field("carrionNoise", CarrionNoise)
                  .Field("carrionBackground", CarrionBackground)
                  .Field("chemicalNoise", ChemicalNoise)
                  .Field("chemicalBackground", ChemicalBackground)
                  .Field("facingIntake", FacingIntake)
                  .Field("intakeFullScaleMetresPerSecond", IntakeFullScaleMetresPerSecond);

        public override string HashContribution() =>
            string.Format(
                CultureInfo.InvariantCulture,
                "{0}:upkeep={1:R},joint={2},area={3:R},ref={4:R},leaf={5:R}/{6:R},carrion={7:R}/{8:R}," +
                "chemical={9:R}/{10:R},facing={11},intake={12:R}",
                Id, UpkeepWattsPerCubicMetre, AllowsJoint, UpkeepWattsPerSquareMetre, ReferenceAreaSquareMetres,
                LeafNoise, LeafBackground, CarrionNoise, CarrionBackground, ChemicalNoise, ChemicalBackground,
                FacingIntake, IntakeFullScaleMetresPerSecond);

        private static void Positive(float value, string name)
        {
            if (!(value > 0f) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(name, value, "Must be finite and above zero.");
        }

        private static void NotNegative(float value, string name)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(name, value, "Must be finite and not negative.");
        }
    }
}
