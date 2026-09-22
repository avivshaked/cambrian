using System;
using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>What one creature earned and spent over a step, in joules — DESIGN.md §5A.2.</summary>
    /// <remarks>
    /// Income and expenditure are kept apart rather than netted. A type that could refund its own
    /// costs would be invisible in a net figure, and §5A.2's audit — sun in, metabolism out,
    /// everything else conserved — needs both sides to close at all.
    /// </remarks>
    public readonly struct EnergyLedger
    {
        /// <summary>
        /// Joules acquired from sunlight — the only energy that is new to the world.
        /// </summary>
        /// <remarks>
        /// <b>Split from <see cref="FoodIncome"/> because the world has to treat them
        /// differently, and because asking twice is how they came apart.</b> §5A.2 makes sunlight
        /// the sole primary input: light is created, food is taken from somewhere that must lose
        /// it. Reporting only the total left <see cref="World"/> re-running the whole metabolic
        /// step with the food removed just to work out how much of it there had been — four
        /// evaluations per creature per step where one would do, on the only hot loop in the
        /// design.
        /// </remarks>
        public float LightIncome { get; }

        /// <summary>
        /// Joules acquired by eating — nutrients and tissue. Somebody else's loss, always.
        /// </summary>
        public float FoodIncome { get; }

        /// <summary>Joules acquired: light, nutrients, tissue.</summary>
        /// <remarks>
        /// <b>The nine stored terms are floats and the four sums over them are not</b>
        /// (2026-09-22). A term is one step's price, computed from a float body and handed to a
        /// float field exactly as it stands, so widening it would put a rounding at every deposit
        /// that is not there now. The sums — this, <see cref="Expenditure"/>, <see cref="Net"/>
        /// and <see cref="Wasted"/> — are a different thing: they cross into
        /// <c>Organism.Energy</c> and from there into both identities, and adding them in float
        /// left the reserve short of what the books said by a rounding a step. That is the drift
        /// behind <c>World.MatterResidual</c> reading −3.2e-04 units at 30,000 s on the farm.
        /// </remarks>
        public double Income => (double)LightIncome + FoodIncome;

        /// <summary>Joules spent on standing costs — tissue upkeep and idle joint capacity.</summary>
        public float Upkeep { get; }

        /// <summary>Joules spent on neurons and their connections.</summary>
        public float Neural { get; }

        /// <summary>Joules spent doing mechanical work at the joints.</summary>
        public float Work { get; }

        /// <summary>
        /// Joules spent clearing water, <see cref="RunConfig.HandlingCostPerJouleEaten"/> ×
        /// <see cref="PoolDrawn"/> — D098's leg 3.
        /// </summary>
        /// <remarks>
        /// <b>Charged on what the mouth drew, not on what it kept.</b> The cost is the pumping:
        /// a feeder that clears water full of tissue it cannot assimilate has still done the
        /// work. It is an expenditure like any other and burns like any other (leg 2), so a
        /// world at <see cref="RunConfig.HandlingCostPerJouleEaten"/> 0 reads a ledger identical
        /// to the one it always read.
        /// </remarks>
        public float Handling { get; }

        /// <summary>
        /// What the light offered this body, J, whether or not matter let it take it —
        /// <see cref="CellIntake.LightCapacity"/> carried up to the ledger.
        /// </summary>
        public float LightCapacity { get; }

        /// <summary>Whether fixation was bound by uptake rather than by light this step — D098.</summary>
        public bool UptakeLimited => LightCapacity > LightIncome;

        /// <summary>
        /// Joules removed from the nutrient pool to produce <see cref="FoodIncome"/>. Never less.
        /// </summary>
        /// <remarks>
        /// The difference is lost in the transfer — see <see cref="CellIntake.PoolDrawn"/>. The
        /// world has to remove this figure and account the shortfall as an outflow, or a food
        /// chain refunds part of every meal.
        /// </remarks>
        public float PoolDrawn { get; }

        /// <summary>
        /// Joules released into the water while alive — D070's exudation. Never spent, never lost:
        /// they leave the body and arrive in the nutrient field the same step.
        /// </summary>
        /// <remarks>
        /// <b>Not an expenditure, and the distinction is the audit's.</b>
        /// <see cref="Expenditure"/> is metabolism — joules that leave the world through
        /// <c>World.EnergyOut</c> and are held by nobody afterwards. These are still held, by
        /// <c>World.Nutrients</c>, so counting them as expenditure would debit the world twice for
        /// one transfer and §5A.2's books would never close. They come out of <see cref="Net"/>
        /// because the body no longer has them, and <c>World.Metabolise</c> is where they are
        /// deposited. Zero unless <see cref="RunConfig.ExudationFraction"/> is set, so every run
        /// before D070 reads a ledger identical to the one it always read.
        /// </remarks>
        public float Exuded { get; }

        public EnergyLedger(CellIntake intake, float upkeep, float neural, float work)
            : this(intake, upkeep, neural, work, exuded: 0f)
        {
        }

        public EnergyLedger(CellIntake intake, float upkeep, float neural, float work, float exuded)
            : this(intake, upkeep, neural, work, exuded, handling: 0f)
        {
        }

        public EnergyLedger(
            CellIntake intake, float upkeep, float neural, float work, float exuded, float handling)
        {
            LightIncome = intake.FromLight;
            FoodIncome = intake.FromPool;
            PoolDrawn = intake.PoolDrawn;
            LightCapacity = intake.LightCapacity;
            Upkeep = upkeep;
            Neural = neural;
            Work = work;
            Exuded = exuded;
            Handling = handling;
        }

        /// <summary>
        /// Every field of the ledger, by value. What <see cref="WithPoolDrawn"/> and the
        /// checkpoint reader both need: the ledger's terms are a body's own history and there is
        /// no intake to rebuild them from.
        /// </summary>
        internal EnergyLedger(
            float lightIncome, float foodIncome, float poolDrawn, float lightCapacity,
            float upkeep, float neural, float work, float exuded, float handling)
        {
            LightIncome = lightIncome;
            FoodIncome = foodIncome;
            PoolDrawn = poolDrawn;
            LightCapacity = lightCapacity;
            Upkeep = upkeep;
            Neural = neural;
            Work = work;
            Exuded = exuded;
            Handling = handling;
        }

        public double Expenditure => (double)Upkeep + Neural + Work + Handling;

        /// <summary>
        /// This ledger with its pool draw replaced by what the field actually gave, the food
        /// income scaled down in the same ratio. Everything else is unchanged.
        /// </summary>
        /// <remarks>
        /// A creature is credited only what was taken from the water. Before 2026-09-07 the
        /// ledger kept its planned income when <c>NutrientField.Take</c> returned less than it
        /// asked for, and the difference was energy created from nothing (the Astra review's
        /// R1). Conversion is linear in the amount drawn, so scaling is exact.
        /// </remarks>
        public EnergyLedger WithPoolDrawn(float poolDrawn)
        {
            if (!(poolDrawn < PoolDrawn) || PoolDrawn <= 0f) return this;
            float scale = poolDrawn / PoolDrawn;
            return new EnergyLedger(
                LightIncome, FoodIncome * scale, poolDrawn, LightCapacity,
                Upkeep, Neural, Work, Exuded, Handling * scale);
        }

        /// <summary>
        /// This ledger with its fixation replaced by what the spent field actually gave, the
        /// exudation scaled down in the same ratio — D098's leg 1.
        /// </summary>
        /// <remarks>
        /// <see cref="WithPoolDrawn"/>'s twin, on the other field. Exudation is a fraction of
        /// what was fixed (D070), so it has to move with it or a producer that fixed nothing
        /// still releases something and the body funds the difference out of its reserve. The
        /// capacity is untouched: what the light offered did not change because the water was
        /// short, and the step was uptake-bound either way.
        /// </remarks>
        public EnergyLedger WithLightIncome(float lightIncome)
        {
            if (!(lightIncome < LightIncome) || LightIncome <= 0f) return this;
            float scale = lightIncome / LightIncome;
            return new EnergyLedger(
                lightIncome, FoodIncome, PoolDrawn, LightCapacity,
                Upkeep, Neural, Work, Exuded * scale, Handling);
        }

        /// <summary>
        /// What the body actually keeps this step: income, less metabolism, less what it released
        /// to the water (D070).
        /// </summary>
        /// <remarks>
        /// <see cref="Exuded"/> subtracts here and nowhere else, which is what makes the knob
        /// visible to everything that already reads a net — <c>World.Metabolise</c>'s reserve
        /// update and <see cref="LedgerForecast"/>'s whole-life integration both — without either
        /// having to know the mechanism exists.
        /// </remarks>
        public double Net => Income - Expenditure - Exuded;

        /// <summary>Joules taken from the world and kept by nobody — the loss on transfer.</summary>
        public double Wasted => (double)PoolDrawn - FoodIncome;

        public static EnergyLedger operator +(EnergyLedger a, EnergyLedger b) =>
            new EnergyLedger(
                a.LightIncome + b.LightIncome, a.FoodIncome + b.FoodIncome,
                a.PoolDrawn + b.PoolDrawn, a.LightCapacity + b.LightCapacity,
                a.Upkeep + b.Upkeep, a.Neural + b.Neural, a.Work + b.Work,
                a.Exuded + b.Exuded, a.Handling + b.Handling);

        public override string ToString() =>
            FormattableString.Invariant($"+{Income:0.###} −{Expenditure:0.###}") +
            (Exuded > 0f ? FormattableString.Invariant($" ~{Exuded:0.###}") : "") +
            FormattableString.Invariant($" = {Net:0.###} J");
    }

    /// <summary>
    /// Prices a creature's step — DESIGN.md §5A.2.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The whole world turns on one ratio and it lives here:</b> basal metabolism against peak
    /// photosynthesis. §5A.2 states it as a callout because it is the only parameter that can
    /// make the design fail silently in both directions — if light covers upkeep anywhere, that
    /// place becomes a mat where nothing ever has to move; if it covers upkeep nowhere, nothing
    /// lives at all. It has to not quite cover it.
    /// </para>
    /// <para>
    /// Neither side of that ratio is measured (§5A.10), and neither can be reasoned to. What
    /// finds it is the sweep in §5A.6b: generation depth pins at zero below the transition and
    /// runs away above it, so the transition locates itself without anyone having guessed the
    /// value.
    /// </para>
    /// <para>
    /// <b>No <c>UnityEngine</c> and no physics.</b> Everything here is arithmetic over a
    /// developed phenotype and its surroundings, which is what lets a whole population be swept
    /// in milliseconds instead of stepped through a solver. The work term is supplied by the
    /// caller precisely so this stays true: the simulator knows what torque it applied, and this
    /// does not need to.
    /// </para>
    /// </remarks>
    public static class Metabolism
    {
        /// <summary>
        /// Income and expenditure for one creature over <paramref name="seconds"/>.
        /// </summary>
        /// <param name="phenotype">The developed body. Cell types come from its parts.</param>
        /// <param name="config">Supplies cell types, shapes and the neural cost rates.</param>
        /// <param name="light">Irradiance by depth.</param>
        /// <param name="creatureHeightY">World height of the creature, metres. Y is up.</param>
        /// <param name="nutrientDensity">Energy density of nutrients here, J/m³.</param>
        /// <param name="spentDensity">Spent matter dissolved here, units/m³ — D098.</param>
        /// <param name="workJoules">
        /// Mechanical work done at the joints this step, from the simulator. Zero for a creature
        /// that did not actuate, which is every plant and every founder without a link.
        /// </param>
        /// <param name="seconds">Step length.</param>
        public static EnergyLedger Step(
            Phenotype phenotype,
            RunConfig config,
            LightModel light,
            float creatureHeightY,
            float nutrientDensity,
            float spentDensity,
            float workJoules,
            float seconds)
        {
            if (light == null) throw new ArgumentNullException(nameof(light));

            return StepAt(
                phenotype, config, light.IrradianceAt(creatureHeightY),
                nutrientDensity, spentDensity, workJoules, seconds);
        }

        /// <summary>
        /// The same, given an irradiance directly — what <see cref="LightField"/> supplies.
        /// </summary>
        /// <remarks>
        /// <b>This is the primitive and the <see cref="LightModel"/> overload delegates to it.</b>
        /// Competition for light is a world-level question — who is above whom — and answering it
        /// needs every creature's shadow before anyone's income can be computed (§5A.2b). Taking a
        /// scalar here keeps that entirely outside this class: whether the number arrived from a
        /// crowded layer or from an empty ocean, the arithmetic on one body is identical.
        /// </remarks>
        /// <param name="phenotype">The developed body. Cell types come from its parts.</param>
        /// <param name="config">Supplies cell types, shapes and the neural cost rates.</param>
        /// <param name="irradiance">Light reaching this creature, W/m².</param>
        /// <param name="nutrientDensity">Energy density of nutrients here, J/m³.</param>
        /// <param name="spentDensity">
        /// Spent matter dissolved here, units/m³ — D098's leg 1. What fixation takes up. Zero is
        /// water a producer can find nothing in, and with
        /// <see cref="RunConfig.UptakeRatePerSquareMetre"/> at 0 it is read by nothing at all.
        /// </param>
        /// <param name="workJoules">Mechanical work done at the joints this step.</param>
        /// <param name="seconds">Step length.</param>
        /// <param name="ageSeconds">
        /// How long this creature has been alive. Drives senescence — see
        /// <see cref="RunConfig.SenescenceDoublingSeconds"/>. Zero is a world without ageing, and
        /// is what every result before D038 was measured in.
        /// </param>
        public static EnergyLedger StepAt(
            Phenotype phenotype,
            RunConfig config,
            float irradiance,
            float nutrientDensity,
            float spentDensity,
            float workJoules,
            float seconds,
            float ageSeconds = 0f)
        {
            if (phenotype == null) throw new ArgumentNullException(nameof(phenotype));
            if (config == null) throw new ArgumentNullException(nameof(config));

            // Senescence, as a multiplier on the terms of staying alive rather than as a clock
            // that kills (D038). It moves both sides of the ledger from one knob and by the same
            // factor: an old body spends more and converts less, which is what ageing is. Death
            // stays exactly where §5A.6 puts it, at a reserve of zero, so how long a creature
            // lasts depends on how well it earns rather than on a lifespan we picked.
            float wear = config.SenescenceDoublingSeconds > 0f && ageSeconds > 0f
                ? 1f + ageSeconds / config.SenescenceDoublingSeconds
                : 1f;

            CellIntake intake = CellIntake.None;
            float upkeep = 0f, neural = 0f;

            // D099's cap, applied part by part rather than to the body's total, because income is
            // earned per cell and a body's cells are not all of one kind. Every part is shortened
            // by the same factor, so a mixed body keeps the proportions its plan chose; what it
            // loses is the square metres it never had. One with the cap off, which is every world
            // before D099.
            float litFactor = phenotype.LitAreaFactor(config.LightSilhouetteCap);

            // D106 item 3's rule 7, read once before the walk rather than four times inside it.
            // With every price at zero — the default, and every world in the record — no term is
            // computed and no float is added to the upkeep, so a body's bill is the number it has
            // always been down to the last bit.
            bool priced =
                config.AttackWattsPerUnit > 0f || config.IntakeWattsPerUnit > 0f ||
                config.ProtectionWattsPerUnit > 0f || config.ToughnessWattsPerUnit > 0f;

            foreach (PhenotypePart part in phenotype.Parts)
            {
                CellType cell = config.CellTypes.Resolve(part.CellTypeId);

                // Named throughout: these are eight floats and ints of similar magnitude, and a
                // transposed pair would produce a plausible number rather than an error.
                var context = new CellContext(
                    seconds: seconds,
                    volume: part.Volume,
                    litArea: part.LitArea * litFactor,
                    irradiance: irradiance,
                    nutrientDensity: nutrientDensity,
                    contact: null,
                    power: part.Power,
                    dof: part.JointType.DofCount(),
                    lift: part.Lift,
                    satiationWattsPerCubicMetre: config.SatiationWattsPerCubicMetre,
                    clearanceToeDensity: config.ClearanceToeDensity,
                    spentDensity: spentDensity,
                    uptakeRatePerSquareMetre: config.UptakeRatePerSquareMetre,
                    uptakeHalfSaturation: config.UptakeHalfSaturation,
                    joulesPerUnit: config.JoulesPerUnit);

                intake += cell.Acquire(context);
                upkeep += cell.Upkeep(context);

                // D106 item 3's four prices, charged where the cell's own upkeep is and worn with
                // it by the senescence factor below — a claw is tissue a body maintains, and an
                // old body maintains it worse. Attack, intake and protection are rates over the
                // part's area, so they are billed per square metre of it; toughness is health per
                // cubic metre and is billed per cubic metre, and only above the neutral 1, so a
                // part that has not been made tough pays nothing.
                if (priced) upkeep += AttributeWatts(part, config) * seconds;

                // Neurons are billed where they live, and neural tissue discounts them (§5A.1).
                // Counting them creature-wide instead would price a brain identically to the same
                // neurons scattered over the body, and cephalization would have nothing to gain.
                int neurons = part.Neurons.Length;
                if (neurons == 0) continue;

                int connections = 0;
                for (int n = 0; n < neurons; n++) connections += part.Neurons[n].Inputs.Length;

                float rate =
                    neurons * config.NeuralCostPerNeuronWatts +
                    connections * config.NeuralCostPerConnectionWatts;

                neural += rate * cell.NeuronCostMultiplier(neurons, part.Volume) * seconds;
            }

            // Conversion falls by the same factor the costs rise by. Note what is *not* scaled:
            // PoolDrawn. An old creature strips the larder exactly as fast and keeps less of it,
            // and the difference leaves the world through EnergyLedger.Wasted — the same route
            // §5A.3's transfer loss already takes, so §5A.2's audit closes without a new term.
            // Scaling the draw instead would make ageing a discount on the world's groceries.
            if (wear > 1f)
            {
                // The capacity wears with the income it bounds, so the two stay comparable and
                // "uptake bound this step" does not become a statement about the body's age.
                intake = new CellIntake(
                    intake.FromLight / wear, intake.FromPool / wear, intake.PoolDrawn,
                    intake.LightCapacity / wear);
            }

            // D070. A fraction of the light this body actually kept goes back into the water as
            // dissolved organic matter — so it is taken off the intake *after* wear, not before:
            // an old producer fixes less carbon and therefore releases less of it, which is what
            // exuding a fraction of intake means. Not applied to FromPool — a stomach's meal was
            // already somebody else's tissue and re-releasing part of it would be a second,
            // unasked-for transfer loss on top of CellIntake.PoolDrawn's.
            //
            // The world deposits it (World.Metabolise); this only prices it, because a Phenotype
            // has no idea where it is and Metabolism has no field to deposit into. Net carries
            // the deduction, which is what makes the knob reach LedgerForecast for free.
            float exuded = config.ExudationFraction > 0f
                ? config.ExudationFraction * intake.FromLight
                : 0f;

            // D098's leg 3. Priced on the draw, so it is proportional to the water cleared and
            // not to the meal kept, and it scales with a short take through
            // EnergyLedger.WithPoolDrawn exactly as the food income does. Not worn: an old
            // body pumps as hard as a young one for the same water and keeps less of it, which
            // is the same asymmetry PoolDrawn already carries (see the wear block above).
            float handling = config.HandlingCostPerJouleEaten > 0f
                ? config.HandlingCostPerJouleEaten * intake.PoolDrawn
                : 0f;

            return new EnergyLedger(
                intake, upkeep * wear, neural * wear,
                Math.Max(0f, workJoules) * config.WorkCostMultiplier,
                exuded, handling);
        }

        /// <summary>
        /// What one part's four attributes cost to keep, in watts — D106 item 3's rule 7.
        /// </summary>
        /// <remarks>
        /// <b>Public because the ledger prints it beside the body's income</b>
        /// (<c>src/Evosim.Ledger</c>, and the <c>## Mouth</c> section of the screen the spec asks
        /// for before the round). One expression, read by the metabolic step and by the screen, so
        /// the number a launcher is chosen on and the number a body is charged cannot drift apart.
        /// </remarks>
        public static float AttributeWatts(PhenotypePart part, RunConfig config)
        {
            if (part == null) throw new ArgumentNullException(nameof(part));
            if (config == null) throw new ArgumentNullException(nameof(config));

            float area = Math.Max(0f, part.SurfaceArea);
            float above = part.Toughness - 1f;

            return
                config.AttackWattsPerUnit * part.Attack * area +
                config.IntakeWattsPerUnit * part.Intake * area +
                config.ProtectionWattsPerUnit * part.Protection * area +
                (above > 0f
                    ? config.ToughnessWattsPerUnit * above * Math.Max(0f, part.Volume)
                    : 0f);
        }

        /// <summary>
        /// A part's health pool, in health — its volume times its toughness times
        /// <see cref="RunConfig.HealthPerCubicMetre"/>, D106 item 3's rule 3.
        /// </summary>
        /// <remarks>
        /// Derived rather than stored, for <see cref="TissueJoules"/>'s reason: a growing body's
        /// parts change size on every step, and a pool held beside the body would have to be
        /// rewritten every time or would quietly come to describe a body that no longer exists.
        /// <see cref="Organism.PartHealth"/> holds the <i>fraction</i> of this a part has, which is
        /// what makes a resize free.
        /// </remarks>
        public static float HealthPool(PhenotypePart part, RunConfig config)
        {
            if (part == null) throw new ArgumentNullException(nameof(part));
            if (config == null) throw new ArgumentNullException(nameof(config));

            float pool = Math.Max(0f, part.Volume) * part.Toughness * config.HealthPerCubicMetre;
            return pool > 0f ? pool : 0f;
        }

        /// <summary>
        /// The standing cost of simply existing, in watts — no light, no work, no nutrients.
        /// </summary>
        /// <remarks>
        /// The denominator of §5A.2's ratio, and the most useful single number about a body:
        /// divided into a creature's reserve it gives the seconds it can survive earning nothing,
        /// which is what <see cref="SensorChannel.Energy"/> reports (§4.4).
        /// </remarks>
        /// <param name="phenotype">The developed body, at whatever size it is now.</param>
        /// <param name="config">Supplies cell types, shapes and the neural cost rates.</param>
        /// <param name="ageSeconds">
        /// How long this creature has been alive, for senescence (D038). Zero is the answer at
        /// birth and in a world without ageing, and is the default so that every caller written
        /// before growth existed asks the same question it always asked. <c>World.Grow</c> passes
        /// the creature's real age, because a body that changes size mid-life must not have its
        /// wear silently reset by being remeasured.
        /// </param>
        public static float StandingWatts(Phenotype phenotype, RunConfig config, float ageSeconds = 0f)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            EnergyLedger ledger = StepAt(
                phenotype, config, DarkWorld.IrradianceAt(-1000f),
                nutrientDensity: 0f, spentDensity: 0f, workJoules: 0f, seconds: 1f,
                ageSeconds: ageSeconds);

            return (float)ledger.Expenditure;
        }

        /// <summary>
        /// Energy embodied in a developed body, in joules — DESIGN.md §5A.2c.
        /// </summary>
        /// <remarks>
        /// What a parent pays to build this creature and what the nutrient pool receives when it
        /// dies. Both call this, so the two figures cannot drift apart — and if they did, a
        /// birth-and-death cycle would create or destroy energy.
        /// <para>
        /// A double since 2026-09-22, because it is the authority for <c>Organism.TissueJoules</c>
        /// and for what a birth moves out of a parent's reserve, and both of those are standing
        /// accounts the two identities sum. The parts are floats and stay floats; the sum over
        /// them is the number that has to close.
        /// </para>
        /// </remarks>
        public static double TissueJoules(Phenotype phenotype, RunConfig config)
        {
            if (phenotype == null) throw new ArgumentNullException(nameof(phenotype));
            if (config == null) throw new ArgumentNullException(nameof(config));

            double total = 0d;
            foreach (PhenotypePart part in phenotype.Parts)
            {
                total += (double)Math.Max(0f, part.Volume) *
                         config.CellTypes.Resolve(part.CellTypeId).TissueEnergyPerCubicMetre;
            }

            return total;
        }

        /// <remarks>
        /// A metre of attenuation and a kilometre down: irradiance underflows to zero, so
        /// <see cref="StandingWatts"/> measures cost with no income mixed in. Cheaper and less
        /// fragile than a special case inside <see cref="Step"/>, which would be a branch that
        /// only the reporting path ever took.
        /// </remarks>
        private static readonly LightModel DarkWorld = new LightModel(1f, 1f);
    }
}
