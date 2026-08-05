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
        public const string Photosynthetic = "photosynthetic";
        public const string Absorptive = "absorptive";
        public const string Consumer = "consumer";
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
        public override float Acquire(in CellContext context) => 0f;
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

        public LinkCell(float idleWattsPerNewtonMetre = 0.02f, float upkeepWattsPerCubicMetre = 2.5f)
            : base(upkeepWattsPerCubicMetre)
        {
            if (idleWattsPerNewtonMetre <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(idleWattsPerNewtonMetre), idleWattsPerNewtonMetre,
                    "Capacity with no standing cost is free capacity, and evolution takes all of it.");
            }
            IdleWattsPerNewtonMetre = idleWattsPerNewtonMetre;
        }

        public override string Id => CellTypeIds.Link;
        public override bool AllowsJoint => true;
        public override float Acquire(in CellContext context) => 0f;

        public override void WriteParameters(Json.Writer writer) =>
            writer.Field("idleWattsPerNewtonMetre", IdleWattsPerNewtonMetre);

        public override float Upkeep(in CellContext context) =>
            base.Upkeep(context) +
            IdleWattsPerNewtonMetre * Math.Max(0f, context.Power) *
            Math.Max(0, context.Dof) * context.Seconds;

        public override string HashContribution() =>
            string.Format(
                CultureInfo.InvariantCulture,
                "{0}:upkeep={1:R},joint={2},idle={3:R}",
                Id, UpkeepWattsPerCubicMetre, AllowsJoint, IdleWattsPerNewtonMetre);
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

        public PhotosyntheticCell(float efficiency = 0.05f, float upkeepWattsPerCubicMetre = 3f)
            : base(upkeepWattsPerCubicMetre)
        {
            if (efficiency <= 0f || efficiency > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(efficiency), efficiency, "Must be in (0, 1].");
            }
            Efficiency = efficiency;
        }

        public override string Id => CellTypeIds.Photosynthetic;

        public override float Acquire(in CellContext context) =>
            Math.Max(0f, context.Irradiance) * Math.Max(0f, context.LitArea) *
            Efficiency * context.Seconds;

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
        public float ClearanceRate { get; }

        public AbsorptiveCell(float clearanceRate = 0.5f, float upkeepWattsPerCubicMetre = 4f)
            : base(upkeepWattsPerCubicMetre)
        {
            if (clearanceRate <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(clearanceRate), clearanceRate, "Must be positive.");
            }
            ClearanceRate = clearanceRate;
        }

        public override string Id => CellTypeIds.Absorptive;

        public override float Acquire(in CellContext context) =>
            Math.Max(0f, context.NutrientDensity) * ClearanceRate *
            Math.Max(0f, context.Volume) * context.Seconds;

        public override void WriteParameters(Json.Writer writer) =>
            writer.Field("clearanceRate", ClearanceRate);

        public override string HashContribution() =>
            string.Format(
                CultureInfo.InvariantCulture,
                "{0}:upkeep={1:R},joint={2},clearance={3:R}", Id, UpkeepWattsPerCubicMetre, AllowsJoint, ClearanceRate);
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
            float predationYield = 0.2f)
            : base(upkeepWattsPerCubicMetre)
        {
            if (biteRate <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(biteRate), biteRate, "Must be positive.");
            }

            Require(carrionYield, nameof(carrionYield));
            Require(grazingYield, nameof(grazingYield));
            Require(predationYield, nameof(predationYield));

            BiteRate = biteRate;
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

        public override float Acquire(in CellContext context)
        {
            TissueContact contact = context.Contact;
            if (contact == null || contact.AvailableJoules <= 0f) return 0f;

            float taken = Math.Min(
                contact.AvailableJoules,
                BiteRate * Math.Max(0f, context.Volume) * context.Seconds);

            return taken * YieldAgainst(contact);
        }

        public override void WriteParameters(Json.Writer writer) =>
            writer.Field("biteRate", BiteRate)
                  .Field("carrionYield", CarrionYield)
                  .Field("grazingYield", GrazingYield)
                  .Field("predationYield", PredationYield);

        public override string HashContribution() =>
            string.Format(
                CultureInfo.InvariantCulture,
                "{0}:upkeep={1:R},joint={2},bite={3:R},yield={4:R}/{5:R}/{6:R}",
                Id, UpkeepWattsPerCubicMetre, AllowsJoint, BiteRate,
                CarrionYield, GrazingYield, PredationYield);
    }
}
