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
        public float Income => LightIncome + FoodIncome;

        /// <summary>Joules spent on standing costs — tissue upkeep and idle joint capacity.</summary>
        public float Upkeep { get; }

        /// <summary>Joules spent on neurons and their connections.</summary>
        public float Neural { get; }

        /// <summary>Joules spent doing mechanical work at the joints.</summary>
        public float Work { get; }

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
        {
            LightIncome = intake.FromLight;
            FoodIncome = intake.FromPool;
            PoolDrawn = intake.PoolDrawn;
            Upkeep = upkeep;
            Neural = neural;
            Work = work;
            Exuded = exuded;
        }

        private EnergyLedger(
            float lightIncome, float foodIncome, float poolDrawn,
            float upkeep, float neural, float work, float exuded)
        {
            LightIncome = lightIncome;
            FoodIncome = foodIncome;
            PoolDrawn = poolDrawn;
            Upkeep = upkeep;
            Neural = neural;
            Work = work;
            Exuded = exuded;
        }

        public float Expenditure => Upkeep + Neural + Work;

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
        public float Net => Income - Expenditure - Exuded;

        /// <summary>Joules taken from the world and kept by nobody — the loss on transfer.</summary>
        public float Wasted => PoolDrawn - FoodIncome;

        public static EnergyLedger operator +(EnergyLedger a, EnergyLedger b) =>
            new EnergyLedger(
                a.LightIncome + b.LightIncome, a.FoodIncome + b.FoodIncome,
                a.PoolDrawn + b.PoolDrawn,
                a.Upkeep + b.Upkeep, a.Neural + b.Neural, a.Work + b.Work,
                a.Exuded + b.Exuded);

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
            float workJoules,
            float seconds)
        {
            if (light == null) throw new ArgumentNullException(nameof(light));

            return StepAt(
                phenotype, config, light.IrradianceAt(creatureHeightY),
                nutrientDensity, workJoules, seconds);
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

            foreach (PhenotypePart part in phenotype.Parts)
            {
                CellType cell = config.CellTypes.Resolve(part.CellTypeId);

                // Named throughout: these are eight floats and ints of similar magnitude, and a
                // transposed pair would produce a plausible number rather than an error.
                var context = new CellContext(
                    seconds: seconds,
                    volume: part.Volume,
                    litArea: part.LitArea,
                    irradiance: irradiance,
                    nutrientDensity: nutrientDensity,
                    contact: null,
                    power: part.Power,
                    dof: part.JointType.DofCount(),
                    lift: part.Lift,
                    satiationWattsPerCubicMetre: config.SatiationWattsPerCubicMetre,
                    clearanceToeDensity: config.ClearanceToeDensity);

                intake += cell.Acquire(context);
                upkeep += cell.Upkeep(context);

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
                intake = new CellIntake(
                    intake.FromLight / wear, intake.FromPool / wear, intake.PoolDrawn);
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

            return new EnergyLedger(
                intake, upkeep * wear, neural * wear,
                Math.Max(0f, workJoules) * config.WorkCostMultiplier,
                exuded);
        }

        /// <summary>
        /// The standing cost of simply existing, in watts — no light, no work, no nutrients.
        /// </summary>
        /// <remarks>
        /// The denominator of §5A.2's ratio, and the most useful single number about a body:
        /// divided into a creature's reserve it gives the seconds it can survive earning nothing,
        /// which is what <see cref="SensorChannel.Energy"/> reports (§4.4).
        /// </remarks>
        public static float StandingWatts(Phenotype phenotype, RunConfig config)
        {
            EnergyLedger ledger = Step(
                phenotype, config, DarkWorld, creatureHeightY: -1000f,
                nutrientDensity: 0f, workJoules: 0f, seconds: 1f);

            return ledger.Expenditure;
        }

        /// <summary>
        /// Energy embodied in a developed body, in joules — DESIGN.md §5A.2c.
        /// </summary>
        /// <remarks>
        /// What a parent pays to build this creature and what the nutrient pool receives when it
        /// dies. Both call this, so the two figures cannot drift apart — and if they did, a
        /// birth-and-death cycle would create or destroy energy.
        /// </remarks>
        public static float TissueJoules(Phenotype phenotype, RunConfig config)
        {
            if (phenotype == null) throw new ArgumentNullException(nameof(phenotype));
            if (config == null) throw new ArgumentNullException(nameof(config));

            float total = 0f;
            foreach (PhenotypePart part in phenotype.Parts)
            {
                total += Math.Max(0f, part.Volume) *
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
