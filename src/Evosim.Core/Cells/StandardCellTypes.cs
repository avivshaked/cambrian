using System;
using System.Globalization;

namespace Evosim.Core
{
    /// <summary>
    /// Ids of the built-in types, so callers need not spell strings.
    /// </summary>
    public static class CellTypeIds
    {
        public const string Structural = "structural";
        public const string Link = "link";
        public const string Neural = "neural";
        public const string Photosynthetic = "photosynthetic";
        public const string Absorptive = "absorptive";
        public const string Consumer = "consumer";
        public const string Buoyancy = "buoyancy";
    }

    /// <summary>
    /// Tissue with no way to feed itself — DESIGN.md §5A.1.
    /// </summary>
    /// <remarks>
    /// Without a type permitted to earn nothing, every part must pay for itself and a tail can
    /// never evolve: a fin pays only indirectly, through better swimming, so it is a net loss on
    /// the step it appears. Structural parts are what make fins, levers and streamlining
    /// reachable — the difference between creatures with bodies and creatures that are clumps of
    /// stomachs.
    ///
    /// Its upkeep is the lowest of the four and still not zero, for the reason given on
    /// <see cref="CellType.UpkeepWattsPerCubicMetre"/>.
    /// </remarks>
    public sealed class StructuralCell : CellType
    {
        public StructuralCell(float upkeepWattsPerCubicMetre = 1f)
            : base(upkeepWattsPerCubicMetre) { }

        public override string Id => CellTypeIds.Structural;
        public override CellIntake Acquire(in CellContext context) => CellIntake.None;

        /// <summary>Bone white — inert tissue, and the reference the others read against.</summary>
        public override Float3 InspectionColour => new Float3(0.86f, 0.84f, 0.78f);
    }

    /// <summary>
    /// Connective tissue: the only type that may carry a joint — DESIGN.md §5A.1.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two parts cannot move relative to each other unless a link sits between them.</b>
    /// Everything else attaches rigidly. Motion therefore costs a part, and a creature with no
    /// links is a rigid body — which is what a plant is, without "plant" having to be defined
    /// anywhere.
    /// </para>
    /// <para>
    /// This also supplies the clearance the geometry needs. A cube hinged directly onto a
    /// parent's face keeps only 0.68 rad of swing within a 10% overlap bound, because its
    /// corner crosses the shared plane at nearly zero degrees; a gap of 0.2 half-extents raises
    /// that to 0.94–1.12 rad (measured, `JointClearanceTests`). A link is that gap arrived at
    /// structurally rather than as a fudge factor — and it is the answer to the question that
    /// opened this line of work: whether two directly-connected boxes can actuate at all.
    /// </para>
    /// <para>
    /// It does <i>not</i> fix jamming between parts that are not directly jointed. Siblings and
    /// grandchildren still collide, and the creatures measured at 2%, 33% and 40% of their free
    /// range of motion (logbook/0007) are only partly helped by the extra distance.
    /// </para>
    /// <para>
    /// Upkeep sits above structural tissue and below the feeding types: muscle is expensive to
    /// keep, and this is before any mechanical work it does, which §5A.2 charges separately.
    /// </para>
    /// </remarks>
    public sealed class LinkCell : CellType
    {
        /// <summary>Watts of standing cost per newton-metre of capacity, per degree of freedom.</summary>
        /// <remarks>
        /// <b>This is the term that stops power running away.</b> Charging only for work done is
        /// not a constraint: a link that actuates intermittently would pay almost nothing for
        /// being enormous, so evolution takes the largest capacity on offer and uses it
        /// occasionally. Biology charges the same way — muscle is expensive to keep whether or
        /// not it contracts, and a larger animal spends more merely staying alive.
        ///
        /// Scaling by DOF as well as by torque means a spherical joint costs three times a
        /// hinge of the same strength, because it is three actuators.
        ///
        /// ⚠ Unmeasured (§5A.10). It trades directly against the work coefficient: too low and
        /// capacity is effectively free again, too high and nothing can afford to move. There is
        /// a helpful second pressure in the same direction — ~85% of what a strong link spends
        /// goes into slamming its own joint limits (logbook/0008), so excess power is punished
        /// twice.
        /// </remarks>
        public float IdleWattsPerNewtonMetre { get; }

        /// <summary>
        /// Fraction of incident light this tissue captures, as a photosynthetic cell would.
        /// Zero — the default — restores §5A.1's rule that muscle earns nothing.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Why muscle is allowed to earn at all.</b> §5A.1 charges a link for its volume and
        /// pays it nothing, so a two-part flagellate forfeits half its income to carry one hinge:
        /// 1.30 W of the 2.22 W a 20 N·m joint costs is not upkeep or idle capacity at all, it is
        /// the photosynthesis the same volume would have done (logbook/0026). That term dominates
        /// the other two together, and no setting of either can reach it — which is why four
        /// separate sweeps of actuator cost found nothing alive with a joint.
        /// </para>
        /// <para>
        /// <b>It is also what biology did.</b> Motility did not begin as a separate inert tissue
        /// that a body had to afford; it began in cells that swam and fed at once — a flagellum
        /// is an organelle on a metabolically productive cell, not a segment bolted to one.
        /// <i>Chlamydomonas</i> photosynthesises and swims with the same cell, and the
        /// choanoflagellates the animals came from feed with the collar that drives their
        /// flagellum. A muscle that earns nothing is a late, large-animal arrangement being
        /// charged to the first thing that ever moved.
        /// </para>
        /// <para>
        /// ⚠ Unmeasured (§5A.10), and deliberately defaulted to zero so that no existing run
        /// changes behaviour. At 1.0 a link is a photosynthetic cell that also moves, the
        /// trade-off disappears and joints drift neutrally rather than being selected — which
        /// would be as uninformative as never affording one.
        /// </para>
        /// </remarks>
        public float PhotosyntheticEfficiency { get; }

        public LinkCell(
            float idleWattsPerNewtonMetre = 0.02f,
            float upkeepWattsPerCubicMetre = 2.5f,
            float photosyntheticEfficiency = 0f)
            : base(upkeepWattsPerCubicMetre)
        {
            if (idleWattsPerNewtonMetre <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(idleWattsPerNewtonMetre), idleWattsPerNewtonMetre,
                    "Capacity with no standing cost is free capacity, and evolution takes all of it.");
            }
            if (photosyntheticEfficiency < 0f || photosyntheticEfficiency > 1f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(photosyntheticEfficiency), photosyntheticEfficiency, "Must be in [0, 1].");
            }
            IdleWattsPerNewtonMetre = idleWattsPerNewtonMetre;
            PhotosyntheticEfficiency = photosyntheticEfficiency;
        }

        public override string Id => CellTypeIds.Link;
        public override bool AllowsJoint => true;
        public override CellIntake Acquire(in CellContext context) =>
            PhotosyntheticEfficiency > 0f
                ? CellIntake.Light(
                    Math.Max(0f, context.Irradiance) * Math.Max(0f, context.LitArea) *
                    PhotosyntheticEfficiency * context.Seconds)
                : CellIntake.None;

        /// <summary>Amber — muscle. The only type that can move, so it should read at a glance.</summary>
        public override Float3 InspectionColour => new Float3(0.95f, 0.55f, 0.15f);

        public override void WriteParameters(Json.Writer writer)
        {
            writer.Field("idleWattsPerNewtonMetre", IdleWattsPerNewtonMetre);
            writer.Field("photosyntheticEfficiency", PhotosyntheticEfficiency);
        }

        /// <remarks>
        /// Three terms: volume, the photosynthetic machinery if any, and capacity.
        ///
        /// <para>
        /// <b>The middle one exists because otherwise a link beats a plant outright.</b>
        /// <see cref="PhotosyntheticEfficiency"/> buys a link a photosynthetic cell's income, and
        /// a link's own upkeep is 2.5 W/m³ against green tissue's 3. So at full efficiency it
        /// earned exactly what a plant earns for half a watt per cubic metre less, and measured
        /// 103.7% of a two-part plant at 5 N·m — a joint that pays you to carry it. The
        /// surcharge brings the rate to green tissue's at full efficiency, leaving the capacity
        /// term as the only difference, which is what D043 intended to be measuring.
        /// </para>
        /// <para>
        /// Derived from <see cref="PhotosyntheticCell.DefaultUpkeepWattsPerCubicMetre"/> rather
        /// than taken as a parameter, because it is not an independent choice — it is whatever
        /// green tissue costs — and a second copy is how the two would drift apart. ⚠ It is
        /// therefore not separately visible in <see cref="HashContribution"/>: the inputs are
        /// unchanged, so a run from before this existed carries the same config hash and
        /// different economics.
        /// </para>
        /// </remarks>
        public override float Upkeep(in CellContext context)
        {
            float seconds = context.Seconds;
            float volume = Math.Max(0f, context.Volume);

            float photoFraction = PhotosyntheticEfficiency / PhotosyntheticCell.DefaultEfficiency;
            float photoSurcharge = Math.Max(
                0f, PhotosyntheticCell.DefaultUpkeepWattsPerCubicMetre - UpkeepWattsPerCubicMetre);

            return base.Upkeep(context) +
                photoFraction * photoSurcharge * volume * seconds +
                IdleWattsPerNewtonMetre * Math.Max(0f, context.Power) *
                Math.Max(0, context.Dof) * seconds;
        }

        public override string HashContribution() =>
            string.Format(
                CultureInfo.InvariantCulture,
                "{0}:upkeep={1:R},joint={2},idle={3:R},photo={4:R},photoUpkeep={5:R}",
                Id, UpkeepWattsPerCubicMetre, AllowsJoint, IdleWattsPerNewtonMetre,
                PhotosyntheticEfficiency,
                // Derived rather than stored (see Upkeep), so without it the hash cannot tell a
                // run from before the surcharge existed from one after — same inputs, different
                // economics. A config hash that cannot detect that is not doing its one job.
                PhotosyntheticCell.DefaultUpkeepWattsPerCubicMetre);
    }

    /// <summary>
    /// Tissue that makes thinking cheap — DESIGN.md §5A.1, and the reason a head can evolve.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why it is a cell type and not a field on the creature.</b> §5A.1's thesis is that
    /// energy acquisition is a property of a <i>part</i>, and that this is what makes trophic
    /// strategy a morphological trait the §4.1 graph already encodes. The identical argument
    /// applies to cognition: with neurons spread uniformly over parts and a global brain owned by
    /// nobody, a brain has no volume, no place and nothing that can be bitten — so brain size and
    /// brain placement cannot evolve, because there is nowhere for a brain to be. Cephalization,
    /// one of the most universal patterns in animal evolution, was unreachable by construction.
    /// </para>
    /// <para>
    /// <b>It earns nothing, and it is not cheap.</b> Upkeep sits just under a consumer's: real
    /// nervous tissue is among the most metabolically expensive an animal carries, and a brain
    /// that cost little would grow without limit for the same reason a free part would (§5A.1).
    /// </para>
    /// <para>
    /// <b>Growing more than you use is waste.</b> Tissue past what
    /// <see cref="NeuronsSupportedPerCubicMetre"/> needs to cover the neurons actually present
    /// discounts nothing further and still pays upkeep, so there is an optimum size rather than a
    /// ceiling to press against. That is the shape a cost should have: pressure from both
    /// directions, no wall.
    /// </para>
    /// </remarks>
    public sealed class NeuralCell : CellType
    {
        /// <summary>How many neurons a cubic metre of this tissue supports at the discount.</summary>
        /// <remarks>
        /// ⚠ Unmeasured (§5A.10), and it sets how large a brain has to be before it pays. Too
        /// high and neural tissue is free capacity; too low and no body can afford a brain at all.
        /// </remarks>
        public float NeuronsSupportedPerCubicMetre { get; }

        /// <summary>What a supported neuron costs, as a fraction of the standard rate.</summary>
        public float DiscountedCostFraction { get; }

        public NeuralCell(
            float neuronsSupportedPerCubicMetre = 400f,
            float discountedCostFraction = 0.2f,
            float upkeepWattsPerCubicMetre = 5f)
            : base(upkeepWattsPerCubicMetre)
        {
            if (!(neuronsSupportedPerCubicMetre > 0f))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(neuronsSupportedPerCubicMetre), neuronsSupportedPerCubicMetre,
                    "Neural tissue that supports no neurons is upkeep with no function.");
            }

            if (!(discountedCostFraction >= 0f) || discountedCostFraction >= 1f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(discountedCostFraction), discountedCostFraction,
                    "Must be in [0, 1). At 1 the tissue discounts nothing and is dead weight; " +
                    "above 1 a brain would make thinking more expensive than having no brain.");
            }

            NeuronsSupportedPerCubicMetre = neuronsSupportedPerCubicMetre;
            DiscountedCostFraction = discountedCostFraction;
        }

        public override string Id => CellTypeIds.Neural;
        public override CellIntake Acquire(in CellContext context) => CellIntake.None;

        /// <summary>Violet — nervous tissue, and distinct from every other type at a glance.</summary>
        public override Float3 InspectionColour => new Float3(0.60f, 0.35f, 0.85f);

        /// <remarks>
        /// Blended rather than switched: neurons up to what the tissue supports pay the discount
        /// and the rest pay full price, so the return on a marginal cubic metre falls smoothly to
        /// zero instead of stepping. A step would make brain size a threshold to find rather than
        /// a gradient to climb, and §2's whole concern is that this search is bad at thresholds.
        /// </remarks>
        public override float NeuronCostMultiplier(int neuronCount, float volume)
        {
            if (neuronCount <= 0) return 1f;

            float supported = Math.Max(0f, volume) * NeuronsSupportedPerCubicMetre;
            if (supported >= neuronCount) return DiscountedCostFraction;

            float share = supported / neuronCount;
            return share * DiscountedCostFraction + (1f - share);
        }

        public override void WriteParameters(Json.Writer writer) => writer
            .Field("neuronsSupportedPerCubicMetre", NeuronsSupportedPerCubicMetre)
            .Field("discountedCostFraction", DiscountedCostFraction);

        public override string HashContribution() =>
            string.Format(
                CultureInfo.InvariantCulture,
                "{0}:upkeep={1:R},supported={2:R},discount={3:R}",
                Id, UpkeepWattsPerCubicMetre, NeuronsSupportedPerCubicMetre, DiscountedCostFraction);
    }

    /// <summary>
    /// Feeds on light — DESIGN.md §5A.1.
    /// </summary>
    /// <remarks>
    /// Intake scales with <i>lit area</i>, not volume, which is what makes the trade-off real:
    /// a flat spread-out body collects more light and swims worse. Irradiance falls with depth
    /// (§5A.4), so this type prefers the surface, and that preference is half of what keeps the
    /// world from becoming one strategy everywhere.
    ///
    /// ⚠ <see cref="Efficiency"/> against upkeep is the ratio §5A.2 identifies as deciding
    /// everything: if light alone covers a creature's standing cost, nothing anywhere ever has
    /// to move. Neither number is measured yet (§5A.10).
    /// </remarks>
    public sealed class PhotosyntheticCell : CellType
    {
        /// <summary>Fraction of incident light captured.</summary>
        public float Efficiency { get; }

        /// <summary>
        /// The default capture fraction, named so that tissue defined as a fraction of green
        /// tissue has one number to refer to — see <see cref="LinkCell.PhotosyntheticEfficiency"/>.
        /// </summary>
        public const float DefaultEfficiency = 0.05f;

        /// <summary>What green tissue costs to keep alive, W/m³.</summary>
        /// <remarks>
        /// Named because <see cref="LinkCell"/> has to charge the same rate for the same
        /// machinery. A link that photosynthesises at a photosynthetic cell's efficiency while
        /// paying a link's cheaper upkeep is a strictly better plant than a plant, which
        /// abolishes the trade-off instead of pricing it.
        /// </remarks>
        public const float DefaultUpkeepWattsPerCubicMetre = 3f;

        public PhotosyntheticCell(
            float efficiency = DefaultEfficiency,
            float upkeepWattsPerCubicMetre = DefaultUpkeepWattsPerCubicMetre)
            : base(upkeepWattsPerCubicMetre)
        {
            if (efficiency <= 0f || efficiency > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(efficiency), efficiency, "Must be in (0, 1].");
            }
            Efficiency = efficiency;
        }

        public override string Id => CellTypeIds.Photosynthetic;

        /// <summary>Green, for the obvious reason.</summary>
        public override Float3 InspectionColour => new Float3(0.25f, 0.72f, 0.30f);

        public override CellIntake Acquire(in CellContext context) =>
            CellIntake.Light(
                Math.Max(0f, context.Irradiance) * Math.Max(0f, context.LitArea) *
                Efficiency * context.Seconds);

        public override void WriteParameters(Json.Writer writer) =>
            writer.Field("efficiency", Efficiency);

        public override string HashContribution() =>
            string.Format(
                CultureInfo.InvariantCulture,
                "{0}:upkeep={1:R},joint={2},eff={3:R}", Id, UpkeepWattsPerCubicMetre, AllowsJoint, Efficiency);
    }

    /// <summary>
    /// Feeds on nutrients suspended in the water — DESIGN.md §5A.1.
    /// </summary>
    /// <remarks>
    /// Intake scales with the volume of water swept, so it rewards being <i>where the food is</i>
    /// rather than being large — and since nutrients drift on the current and deplete locally
    /// (§5A.4), that means travelling. This is the term that makes swimming necessary rather
    /// than optional.
    ///
    /// Nutrients are recycled dead matter, never a primary input; sunlight is the only source
    /// of new energy in the world (§5A.2). That is what makes the total auditable.
    /// </remarks>
    public sealed class AbsorptiveCell : CellType
    {
        /// <summary>Cubic metres of water cleared per second, per cubic metre of tissue.</summary>
        /// <remarks>
        /// <b>Capture</b>, not assimilation: how much water this tissue can strain, which is what
        /// limits a filter feeder in thin water. What it keeps of what it catches is
        /// <see cref="Yield"/>. Two rates because they fail differently — a bigger filter helps in
        /// an empty ocean and not in a rich one, and better digestion helps in both.
        /// <para>
        /// <b>Raised 0.5 → 1.0 at D041, and the reason is a shape asymmetry rather than a
        /// generosity judgement.</b> Photosynthesis scales with lit area and this scales with
        /// volume, so the two trades want opposite bodies — and every absorptive creature in this
        /// world is a mutant of a photosynthesiser, wearing a body selection spread out to catch
        /// light. At equal volume that body earns 4.59 W from light and 0.50 W from filtering: a
        /// <b>9.2× income collapse</b> on conversion, against 2.1× for a cube. An earlier
        /// measurement priced both trades on a cube, found parity, and withdrew this change
        /// (D039); a cube is a shape neither trade would build.
        /// </para>
        /// <para>
        /// <b>Deliberately short of parity.</b> Matching a spread photosynthesiser at the 10 J/m³
        /// this world reaches needs ≈1.3. This is 1.0, because absorption is depth-independent and
        /// photosynthesis is not: a trade that ties at the surface wins everywhere below it, and
        /// the world would swap one monoculture for another. At 1.0 the deep water becomes
        /// habitable and the lit layer does not change hands, which is §5A.4's depth gradient
        /// finally having two sides. <see cref="Metabolism"/>'s margin tests guard both directions.
        /// </para>
        /// ⚠ Still unmeasured — §5A.10. What is measured is the asymmetry it answers.
        /// </remarks>
        public float ClearanceRate { get; }

        /// <summary>Fraction of captured matter the cell keeps. The rest is lost, not returned.</summary>
        /// <remarks>
        /// <para>
        /// <b>Defaults to 1, and that is a modelling statement rather than a missing number.</b>
        /// Dissolved matter taken across a membrane has no mechanical loss the way a torn-up
        /// carcass does — the waste in real filter feeding is in <i>capture</i>, which
        /// <see cref="ClearanceRate"/> already carries. So filtering is lossless here by default
        /// and biting is not (<see cref="ConsumerCell.CarrionYield"/>), and most of why the two
        /// strategies coexist is that difference.
        /// </para>
        /// <para>
        /// It is settable anyway, because "assimilation is perfect" is a claim and §5A.10's rule
        /// is that a claim nobody has measured must be one a run can vary. Turning it below 1
        /// models digestion that is not free.
        /// </para>
        /// </remarks>
        public float Yield { get; }

        public AbsorptiveCell(
            float clearanceRate = 1.0f, float upkeepWattsPerCubicMetre = 4f, float yield = 1f)
            : base(upkeepWattsPerCubicMetre)
        {
            if (clearanceRate <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(clearanceRate), clearanceRate, "Must be positive.");
            }

            if (!(yield > 0f) || yield > 1f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(yield), yield,
                    "Yield must be in (0, 1]. Above 1 a feeder gains more than it takes, which is " +
                    "a free-energy source (§11.2) and not a tuning choice; at zero the cell " +
                    "pays upkeep to eat nothing.");
            }

            ClearanceRate = clearanceRate;
            Yield = yield;
        }

        public override string Id => CellTypeIds.Absorptive;

        /// <summary>Cyan — it feeds on what the water carries, so it reads as the water's own.</summary>
        public override Float3 InspectionColour => new Float3(0.20f, 0.65f, 0.85f);

        /// <remarks>
        /// Filtering, so nothing is lost in the transfer: what leaves the water is what the cell
        /// keeps. A consumer tears its food up and wastes most of it (§5A.3); a filter feeder does
        /// not, and that difference is most of why the two strategies coexist.
        /// </remarks>
        public override CellIntake Acquire(in CellContext context) =>
            CellIntake.Food(
                Math.Max(0f, context.NutrientDensity) * ClearanceRate *
                Math.Max(0f, context.Volume) * context.Seconds,
                Yield);

        public override void WriteParameters(Json.Writer writer) =>
            writer.Field("clearanceRate", ClearanceRate)
                  .Field("yield", Yield);

        public override string HashContribution() =>
            string.Format(
                CultureInfo.InvariantCulture,
                "{0}:upkeep={1:R},joint={2},clearance={3:R},yield={4:R}",
                Id, UpkeepWattsPerCubicMetre, AllowsJoint, ClearanceRate, Yield);
    }

    /// <summary>
    /// Feeds on tissue — carrion, plants, or other animals — DESIGN.md §5A.3.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One type covers scavenging, grazing and predation, because they differ only in what gets
    /// touched. That is what lets them appear in that order without any morphological innovation
    /// between them: herbivore and carnivore are behavioural outcomes, not body plans.
    /// </para>
    /// <para>
    /// <b>Carrion pays best and resists least</b>, which is the point. A consumer cell costs
    /// upkeep from the mutation that creates it and would pay nothing until perception, directed
    /// movement and prey density all existed together — a valley too wide for a population to
    /// cross, so the trait would be purged before it could ever be useful. Feeding on the dead
    /// works while drifting blind, so the cell survives long enough to become a predator.
    /// </para>
    /// <para>
    /// Yields are the highest upkeep of the four: this is expensive tissue that is worthless
    /// without something to eat.
    /// </para>
    /// </remarks>
    public sealed class ConsumerCell : CellType
    {
        /// <summary>Joules taken per second of contact, per cubic metre of tissue, before yield.</summary>
        public float BiteRate { get; }

        /// <summary>Fraction kept when feeding on dead tissue. Highest — carrion cannot resist.</summary>
        public float CarrionYield { get; }

        /// <summary>Fraction kept when feeding on living non-consumer tissue: grazing.</summary>
        public float GrazingYield { get; }

        /// <summary>Fraction kept when feeding on another consumer: contested, so lowest.</summary>
        public float PredationYield { get; }

        /// <summary>
        /// Cubic metres of water searched for detritus per second, per cubic metre of tissue.
        /// </summary>
        /// <remarks>
        /// The scavenging counterpart of <see cref="AbsorptiveCell.ClearanceRate"/>, and separate
        /// from it because the two describe different animals: a filter feeder sweeps water
        /// continuously, a mouth hunts through it. Together with <see cref="BiteRate"/> it sets
        /// which of the two limits a scavenger — searching in thin water, or swallowing in thick.
        /// ⚠ Unmeasured — §5A.10.
        /// </remarks>
        public float ScavengeRate { get; }

        /// <param name="biteRate">Joules per second of contact, per cubic metre, before yield.</param>
        /// <param name="upkeepWattsPerCubicMetre">Standing cost — the highest of the five (§5A.3).</param>
        /// <param name="grazingYield">Fraction kept when feeding on living non-consumer tissue.</param>
        /// <param name="predationYield">Fraction kept when feeding on another consumer.</param>
        /// <param name="carrionYield">
        /// Must stay the largest of the three, and that ordering is load-bearing rather than
        /// cosmetic. It is what lets a consumer cell pay its way before perception exists, and
        /// so survive the predator valley long enough to become a predator (§5A.3). A run that
        /// inverts it is testing a different hypothesis, which is allowed — but it will get a
        /// world with no carnivores in it, and should expect to.
        /// </param>
        public ConsumerCell(
            float biteRate = 20f,
            float upkeepWattsPerCubicMetre = 6f,
            float carrionYield = 0.8f,
            float grazingYield = 0.5f,
            float predationYield = 0.2f,
            float scavengeRate = 1f)
            : base(upkeepWattsPerCubicMetre)
        {
            if (biteRate <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(biteRate), biteRate, "Must be positive.");
            }

            if (scavengeRate <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(scavengeRate), scavengeRate,
                    "A consumer that searches no water can never find carrion, which closes the " +
                    "only route across the predator valley (§5A.3).");
            }

            Require(carrionYield, nameof(carrionYield));
            Require(grazingYield, nameof(grazingYield));
            Require(predationYield, nameof(predationYield));

            BiteRate = biteRate;
            ScavengeRate = scavengeRate;
            CarrionYield = carrionYield;
            GrazingYield = grazingYield;
            PredationYield = predationYield;
        }

        /// <summary>
        /// A yield above 1 would return more energy than was taken, which is a free-energy
        /// source (§11.2) and not a tuning choice.
        /// </summary>
        private static void Require(float yield, string name)
        {
            if (!(yield >= 0f) || yield > 1f)
            {
                throw new ArgumentOutOfRangeException(
                    name, yield,
                    "Yield must be in [0, 1]. Above 1 a feeder gains more than it takes, and a " +
                    "food chain that gains energy at every level has no reason to end.");
            }
        }

        public override string Id => CellTypeIds.Consumer;

        /// <summary>
        /// Red — a mouth. Paired with green it is the worst case for red-green colour blindness,
        /// which is a real cost accepted for a real reason: plant-green and mouth-red are the two
        /// strongest colour intuitions available, and the alternative is a palette nobody can
        /// read without a legend. The overlay prints a legend regardless, and the two also differ
        /// in brightness, so hue is not the only channel carrying the distinction.
        /// </summary>
        public override Float3 InspectionColour => new Float3(0.85f, 0.24f, 0.22f);

        /// <summary>Fraction of what is taken that the feeder keeps — DESIGN.md §5A.3.</summary>
        /// <remarks>
        /// The rest is lost, not transferred, which is what makes a food chain lose energy at
        /// every level rather than recycling it indefinitely.
        /// </remarks>
        public float YieldAgainst(TissueContact contact)
        {
            if (contact == null) return 0f;
            if (!contact.IsAlive) return CarrionYield;
            if (contact.Type.Id == CellTypeIds.Consumer) return PredationYield;
            return GrazingYield;
        }

        /// <remarks>
        /// <para>
        /// <b>Two routes, and only one of them exists yet.</b> Feeding on a body it is touching
        /// needs contact, which needs physics — that arrives at Milestone 4. Feeding on carrion
        /// does not: dead tissue is sinking through the water as detritus (§5A.2c), and a mouth in
        /// that water can scavenge without perceiving anything.
        /// </para>
        /// <para>
        /// <b>That route is the bridge across the predator valley</b> (§5A.3). A consumer part
        /// costs upkeep from the mutation that creates it and pays nothing until perception,
        /// directed movement and prey density all exist together — a valley too wide for a
        /// population to cross. Carrion pays from the first generation, so the part survives long
        /// enough to become something. Detritivore, then scavenger, then predator: a gradient
        /// rather than a leap.
        /// </para>
        /// <para>
        /// The carrion yield applies, so a consumer wastes most of what it tears up where a filter
        /// feeder wastes none — which is what makes scavenging a worse living than filtering, and
        /// worth abandoning as soon as something better appears.
        /// </para>
        /// </remarks>
        public override CellIntake Acquire(in CellContext context)
        {
            float bite = BiteRate * Math.Max(0f, context.Volume) * context.Seconds;

            CellIntake carrion = context.NutrientDensity > 0f
                ? CellIntake.Food(
                    Math.Min(bite, Math.Max(0f, context.NutrientDensity) * ScavengeVolume(context)),
                    CarrionYield)
                : CellIntake.None;

            TissueContact contact = context.Contact;
            if (contact == null || contact.AvailableJoules <= 0f) return carrion;

            float taken = Math.Min(contact.AvailableJoules, bite);
            return carrion + CellIntake.Food(taken, YieldAgainst(contact));
        }

        /// <summary>Cubic metres of water this part can pick detritus out of, per step.</summary>
        /// <remarks>
        /// Scaled by the same clearance idea a filter feeder uses, so that a consumer in thin
        /// water is limited by how much water it can search rather than only by its bite. Without
        /// it, a mouth in an almost-empty ocean still takes a full bite every step and scavenging
        /// becomes a better living the emptier the world gets.
        /// </remarks>
        private float ScavengeVolume(in CellContext context) =>
            ScavengeRate * Math.Max(0f, context.Volume) * context.Seconds;

        public override void WriteParameters(Json.Writer writer) =>
            writer.Field("biteRate", BiteRate)
                  .Field("scavengeRate", ScavengeRate)
                  .Field("carrionYield", CarrionYield)
                  .Field("grazingYield", GrazingYield)
                  .Field("predationYield", PredationYield);

        public override string HashContribution() =>
            string.Format(
                CultureInfo.InvariantCulture,
                "{0}:upkeep={1:R},joint={2},bite={3:R},scavenge={4:R},yield={5:R}/{6:R}/{7:R}",
                Id, UpkeepWattsPerCubicMetre, AllowsJoint, BiteRate, ScavengeRate,
                CarrionYield, GrazingYield, PredationYield);
    }

    /// <summary>
    /// Tissue that holds gas and so weighs less than water — DESIGN.md §5A.1, D049.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The oldest behaviour there is.</b> Gas vesicles are protein shells that let
    /// cyanobacteria hold a depth in a light gradient, and they predate muscle by roughly three
    /// billion years. This world's only economic axis is vertical (D037), so a creature's depth
    /// is very nearly its whole strategy — and until D049 there was no organ for choosing one.
    /// The joint was being asked to do this job, which is a Cambrian answer to an Archean
    /// question and the reason it never paid (logbook/0027).
    /// </para>
    /// <para>
    /// <b>Lift, not density.</b> The cell cancels some of its own weight rather than setting an
    /// absolute density: being heavier is already free — that is
    /// <see cref="FluidConfig.TissueExcessDensity"/> — so what wants an organ and a price is
    /// going *up*. <see cref="MorphNode.Lift"/> carries the amount, in kg/m³ of displaced water
    /// cancelled, and is evolvable per part.
    /// </para>
    /// <para>
    /// <b>Charged for whether or not it is doing anything</b>, exactly as
    /// <see cref="LinkCell.IdleWattsPerNewtonMetre"/> charges capacity. Free lift runs away to
    /// the maximum on offer and every creature returns to the surface, which is precisely the
    /// world D048 was built to escape. Real vesicles cost protein to build and to keep from
    /// collapsing under pressure.
    /// </para>
    /// <para>
    /// It earns nothing, like <see cref="StructuralCell"/>. Whether that is survivable is the
    /// measurement D049 exists to make: a buoyancy cell has to pay for itself entirely through
    /// where it puts the rest of the body, which is the same shape of bet as a fin and the
    /// reason §5A.1 permits earning nothing at all.
    /// </para>
    /// </remarks>
    public sealed class BuoyancyCell : CellType
    {
        /// <summary>What holding a unit of lift costs, W per (kg/m³) per m³ of tissue.</summary>
        /// <remarks>
        /// ⚠ Unmeasured (§5A.10), and it trades the same way <c>IdleWattsPerNewtonMetre</c> does:
        /// too low and depth is free, too high and nothing can afford to leave the surface. The
        /// calibration question is whether a creature can buy its way to the matter at depth for
        /// less than the matter is worth — which is a comparison D048 made possible and nothing
        /// before it could have asked.
        /// </remarks>
        public float WattsPerLiftUnit { get; }

        /// <summary>Most lift one cell may hold, kg/m³ — a numerical bound, not an economic one.</summary>
        public const float MaxLiftKgPerCubicMetre = 50f;

        public BuoyancyCell(
            float wattsPerLiftUnit = 0.05f,
            float upkeepWattsPerCubicMetre = 2.5f)
            : base(upkeepWattsPerCubicMetre)
        {
            if (wattsPerLiftUnit <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(wattsPerLiftUnit), wattsPerLiftUnit,
                    "Lift must cost something, or every creature holds the maximum.");
            }

            WattsPerLiftUnit = wattsPerLiftUnit;
        }

        public override string Id => CellTypeIds.Buoyancy;

        public override Float3 InspectionColour => new Float3(0.55f, 0.80f, 0.95f);

        public override CellIntake Acquire(in CellContext context) => CellIntake.None;

        public override float Upkeep(in CellContext context) =>
            base.Upkeep(context) +
            WattsPerLiftUnit * Math.Max(0f, context.Lift) *
            Math.Max(0f, context.Volume) * context.Seconds;

        public override void WriteParameters(Json.Writer writer) =>
            writer.Field("wattsPerLiftUnit", WattsPerLiftUnit);

        public override string HashContribution() =>
            string.Format(
                CultureInfo.InvariantCulture,
                "{0}:upkeep={1:R},joint={2},lift={3:R}",
                Id, UpkeepWattsPerCubicMetre, AllowsJoint, WattsPerLiftUnit);
    }
}
