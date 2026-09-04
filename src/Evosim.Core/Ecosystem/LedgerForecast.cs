using System;

namespace Evosim.Core
{
    /// <summary>
    /// What a single creature's energy ledger would do across its whole life, integrated alone —
    /// a pocket calculator for <see cref="World"/>'s per-creature rules, without a population,
    /// a light field or a nutrient pool around it. DESIGN.md §5A.2, §5A.6.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this exists.</b> <see cref="World"/> answers "what does a population do", which
    /// needs thousands of creatures and a run directory to read. Sometimes the question is
    /// smaller: "does this one body, at this one depth and this one nutrient density, pay for
    /// itself at all" — and that question has a closed loop small enough to answer in
    /// milliseconds, the same way <see cref="Metabolism"/> itself does. This is that loop, one
    /// creature at a time, from birth to death or to a cap.
    /// </para>
    /// <para>
    /// <b>Not a substitute for <see cref="World"/>.</b> There is no competition for light, no
    /// nutrient depletion, no predation and no mutation — the irradiance and nutrient density a
    /// caller supplies are held constant for the creature's whole life, and every offspring this
    /// method counts is priced at the parent's own tissue rather than a mutated child's, because
    /// there is no mutation to run. It answers "can this body live here", not "what would this
    /// body's descendants become".
    /// </para>
    /// </remarks>
    public static class LedgerForecast
    {
        /// <summary>Step length the lifetime integration uses, seconds.</summary>
        public const float StepSeconds = 0.5f;

        /// <summary>
        /// Simulated seconds after which a still-solvent creature is reported as censored rather
        /// than integrated forever — the same shape of cap <see cref="RunConfig.MaximumPopulation"/>
        /// puts on a world, for the same reason: a body that never dies is not a result, it is an
        /// unbounded loop wearing one.
        /// </summary>
        public const float MaxLifetimeSeconds = 60000f;

        /// <summary>
        /// Integrates one creature's ledger alone, from birth to death or to
        /// <see cref="MaxLifetimeSeconds"/>.
        /// </summary>
        /// <param name="phenotype">The developed body. Fixed for its whole life — there is no growth (§5A.6).</param>
        /// <param name="config">Supplies cell types, shapes, senescence and the economy's world constants.</param>
        /// <param name="irradianceWattsPerSquareMetre">
        /// Light reaching this creature, W/m², before <paramref name="shadeFraction"/> is applied —
        /// typically <see cref="LightModel.IrradianceAt"/> at some depth.
        /// </param>
        /// <param name="nutrientDensityJoulesPerCubicMetre">
        /// Energy density of nutrients at the creature's position, J/m³. Held constant: this
        /// method does not deplete or refill it, unlike <see cref="World"/>'s pool.
        /// </param>
        /// <param name="shadeFraction">
        /// Fraction of <paramref name="irradianceWattsPerSquareMetre"/> blocked before it reaches
        /// this creature, in [0, 1] — a stand-in for a canopy this lone-creature calculation has
        /// no population to cast. 0 is unshaded; effective irradiance is
        /// <c>irradiance × (1 − shadeFraction)</c>.
        /// </param>
        /// <param name="reproduction">
        /// The genome's own <see cref="ReproductionTraits"/> — brood size and offspring endowment.
        /// Not read from <paramref name="phenotype"/>, which carries no genome.
        /// </param>
        /// <remarks>
        /// <para>
        /// <b>Mirrors <see cref="World"/>'s per-step order exactly</b> — metabolise, then check
        /// death, then attempt to breed — so that a body priced here and the same body dropped
        /// into a world at a matching constant depth and density would spend the same way,
        /// step for step, until the world's own dynamics (a moving nutrient pool, competition
        /// for light, mutation) pull them apart.
        /// </para>
        /// <para>
        /// <b>The breeding rule, reproduced from <see cref="World.Reproduce"/> and
        /// <see cref="World.Conceive"/>:</b> a creature attempts to breed once its energy clears
        /// <c>Genome.Reproduction.CostJoules(PerOffspringOverheadJoules + TissueJoules)</c> — the
        /// gate <see cref="Organism.ReproductionThreshold"/> computes, using the creature's own
        /// tissue as the estimate <see cref="World"/> also uses before a child is actually
        /// developed. Once past the gate, it produces up to <c>BroodSize</c> children this step,
        /// each priced at <c>OffspringEndowment + tissue + PerOffspringOverheadJoules</c> — the
        /// parent's own tissue standing in for the child's, since there is no mutation here to
        /// develop a different one — stopping the brood as soon as one child cannot be afforded,
        /// exactly as <see cref="World.Conceive"/> truncates rather than refuses. Matter
        /// availability is never checked: <see cref="World"/> would refuse a conception the local
        /// matter stock cannot cover, and this method has no matter field to consult, so it
        /// reports the price and lets the energy rule alone decide whether a birth happens.
        /// </para>
        /// <para>
        /// <b>D070's exudation arrives for free, and that is deliberate.</b> This integrates
        /// <see cref="EnergyLedger.Net"/>, which already has
        /// <see cref="EnergyLedger.Exuded"/> subtracted, so a producer forecast under a nonzero
        /// <see cref="RunConfig.ExudationFraction"/> keeps less per step, lives no longer and
        /// breeds no more often than the fraction allows — without this method knowing the
        /// mechanism exists. What it cannot see is the other half of the transfer: the joules land
        /// in a nutrient field this calculation does not have, so a forecast of an exuding
        /// *producer* is honest while a forecast of the *consumer* eating what it released holds
        /// the density constant that exudation is supposed to raise. Ask <see cref="World"/> for
        /// that one.
        /// </para>
        /// </remarks>
        public static LedgerForecastResult Forecast(
            Phenotype phenotype,
            RunConfig config,
            float irradianceWattsPerSquareMetre,
            float nutrientDensityJoulesPerCubicMetre,
            float shadeFraction,
            ReproductionTraits reproduction)
        {
            if (phenotype == null) throw new ArgumentNullException(nameof(phenotype));
            if (config == null) throw new ArgumentNullException(nameof(config));

            if (phenotype.PartCount == 0)
            {
                throw new ArgumentException(
                    "Phenotype has no parts — a stillbirth cannot be forecast, since it has no " +
                    "tissue, no upkeep and nothing to price.", nameof(phenotype));
            }

            if (float.IsNaN(irradianceWattsPerSquareMetre) || float.IsInfinity(irradianceWattsPerSquareMetre) ||
                irradianceWattsPerSquareMetre < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(irradianceWattsPerSquareMetre), irradianceWattsPerSquareMetre,
                    "Must be finite and non-negative.");
            }

            if (float.IsNaN(nutrientDensityJoulesPerCubicMetre) ||
                float.IsInfinity(nutrientDensityJoulesPerCubicMetre) ||
                nutrientDensityJoulesPerCubicMetre < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(nutrientDensityJoulesPerCubicMetre), nutrientDensityJoulesPerCubicMetre,
                    "Must be finite and non-negative.");
            }

            if (float.IsNaN(shadeFraction) || shadeFraction < 0f || shadeFraction > 1f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(shadeFraction), shadeFraction, "Must be in [0, 1].");
            }

            if (reproduction.BroodSize < 1)
            {
                throw new ArgumentException(
                    $"Brood size {reproduction.BroodSize} must be at least 1 — the same rule " +
                    "Genome.Validate enforces.", nameof(reproduction));
            }

            if (float.IsNaN(reproduction.OffspringEndowment) ||
                float.IsInfinity(reproduction.OffspringEndowment) ||
                reproduction.OffspringEndowment <= 0f)
            {
                throw new ArgumentException(
                    $"Offspring endowment {reproduction.OffspringEndowment} must be finite and " +
                    "positive — an offspring born with nothing is dead on arrival.",
                    nameof(reproduction));
            }

            float irradiance = irradianceWattsPerSquareMetre * (1f - shadeFraction);
            float tissue = Metabolism.TissueJoules(phenotype, config);

            // Net at birth: age zero, so wear is exactly 1 regardless of
            // SenescenceDoublingSeconds (Metabolism.StepAt's own guard). One second so the
            // ledger's Net reads directly in watts.
            float netWattsAtBirth = Metabolism.StepAt(
                phenotype, config, irradiance, nutrientDensityJoulesPerCubicMetre,
                workJoules: 0f, seconds: 1f, ageSeconds: 0f).Net;

            float? breakEvenDensity = FindBreakEvenDensity(phenotype, config, irradiance);

            float childPrice = reproduction.OffspringEndowment + tissue + config.PerOffspringOverheadJoules;
            float matterPricePerChild = config.MatterPerTissueJoule * tissue + config.MatterPerCreature;
            float reproductionGate = reproduction.CostJoules(config.PerOffspringOverheadJoules + tissue);

            float energy = reproduction.OffspringEndowment;
            float age = 0f;
            float elapsed = 0f;
            int children = 0;
            float? firstChildSeconds = null;
            bool starved = false;

            // Death at zero is checked once per step, same as World.Metabolise; a creature that
            // starts at or below zero (an endowment that cannot outlive its own first instant)
            // never gets a step at all.
            while (energy > 0f && elapsed < MaxLifetimeSeconds)
            {
                EnergyLedger ledger = Metabolism.StepAt(
                    phenotype, config, irradiance, nutrientDensityJoulesPerCubicMetre,
                    workJoules: 0f, seconds: StepSeconds, ageSeconds: age);

                energy += ledger.Net;
                age += StepSeconds;
                elapsed += StepSeconds;

                if (energy <= 0f)
                {
                    energy = 0f;
                    starved = true;
                    break;
                }

                if (reproductionGate <= 0f || energy < reproductionGate) continue;

                for (int n = 0; n < reproduction.BroodSize; n++)
                {
                    if (energy < childPrice) break;

                    energy -= childPrice;
                    children++;
                    if (firstChildSeconds == null) firstChildSeconds = elapsed;
                }
            }

            return new LedgerForecastResult(
                netWattsAtBirth, breakEvenDensity, elapsed, children, firstChildSeconds,
                matterPricePerChild, starved);
        }

        /// <summary>
        /// Solves for the nutrient density at which this body's net, at
        /// <paramref name="irradiance"/> and age zero, is exactly zero — <c>null</c> when the
        /// body carries no <see cref="CellTypeIds.Absorptive"/> tissue, since density then has no
        /// effect on it at all and "break-even density" has no answer.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Bisection over <see cref="Metabolism.StepAt"/> itself rather than an algebraic
        /// inversion of <see cref="AbsorptiveCell.Acquire"/>, so this stays correct through
        /// <see cref="RunConfig.ClearanceToeDensity"/>'s toe and
        /// <see cref="RunConfig.SatiationWattsPerCubicMetre"/>'s plateau — both of which make the
        /// closed form nonlinear — as well as through a body that mixes absorptive tissue with a
        /// photosynthetic or structural part, where the intercept a closed form would need is
        /// whatever <see cref="Metabolism.StepAt"/> already computes for the rest of the body.
        /// Net is non-decreasing in density (capture only ever adds intake, never removes it), so
        /// bisection is exact for this function's shape and does not need to know which term is
        /// doing the moving.
        /// </para>
        /// <para>
        /// A body whose income cannot reach zero net at any density this search tries — light
        /// alone already clears upkeep, or the search range is exhausted before it does — reports
        /// <c>null</c> as well, rather than a number outside any density this tool was asked
        /// about.
        /// </para>
        /// </remarks>
        private static float? FindBreakEvenDensity(Phenotype phenotype, RunConfig config, float irradiance)
        {
            bool hasAbsorptive = false;
            for (int i = 0; i < phenotype.Parts.Count; i++)
            {
                if (phenotype.Parts[i].CellTypeId == CellTypeIds.Absorptive)
                {
                    hasAbsorptive = true;
                    break;
                }
            }

            if (!hasAbsorptive) return null;

            float NetAt(float density) =>
                Metabolism.StepAt(
                    phenotype, config, irradiance, density,
                    workJoules: 0f, seconds: 1f, ageSeconds: 0f).Net;

            // Light alone already covers upkeep: density does not need to contribute anything,
            // so "the density at which net = 0" has no single answer above zero.
            if (NetAt(0f) >= 0f) return 0f;

            float lo = 0f;
            float hi = 1f;
            const int maxDoublings = 64;

            int doublings = 0;
            while (NetAt(hi) < 0f)
            {
                if (++doublings > maxDoublings) return null;
                hi *= 2f;
            }

            for (int i = 0; i < 64; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (NetAt(mid) < 0f) lo = mid; else hi = mid;
            }

            return (lo + hi) * 0.5f;
        }
    }

    /// <summary>One body's whole-life forecast from <see cref="LedgerForecast.Forecast"/>.</summary>
    public readonly struct LedgerForecastResult
    {
        /// <summary>Net joules per second at birth (age zero, senescence wear exactly 1).</summary>
        public float NetWattsAtBirth { get; }

        /// <summary>
        /// Nutrient density, J/m³, at which this body's net is exactly zero at the irradiance it
        /// was forecast under — <c>null</c> when the body has no absorptive tissue for density to
        /// act on, or when no density this side of an exhausted search reaches it.
        /// </summary>
        public float? BreakEvenNutrientDensity { get; }

        /// <summary>
        /// Simulated seconds survived, from birth to death or to
        /// <see cref="LedgerForecast.MaxLifetimeSeconds"/>.
        /// </summary>
        public float LifetimeSeconds { get; }

        /// <summary>Total offspring produced over the forecast lifetime — R0.</summary>
        public int ChildrenProduced { get; }

        /// <summary>Seconds from birth to the first successful reproduction, or <c>null</c> if none.</summary>
        public float? TimeToFirstChildSeconds { get; }

        /// <summary>
        /// Matter each child costs — <see cref="RunConfig.MatterPerTissueJoule"/> × tissue plus
        /// <see cref="RunConfig.MatterPerCreature"/> — reported regardless of whether matter is
        /// actually scarce anywhere, since this method has no matter field to check it against.
        /// </summary>
        public float MatterPricePerChild { get; }

        /// <summary>
        /// True when the forecast ended because energy reached zero; false when it ended because
        /// <see cref="LedgerForecast.MaxLifetimeSeconds"/> was reached while still solvent — a
        /// censored result, the same distinction <see cref="PopulationRunawayException"/> draws
        /// for a whole world.
        /// </summary>
        public bool DiedOfStarvation { get; }

        public LedgerForecastResult(
            float netWattsAtBirth, float? breakEvenNutrientDensity, float lifetimeSeconds,
            int childrenProduced, float? timeToFirstChildSeconds, float matterPricePerChild,
            bool diedOfStarvation)
        {
            NetWattsAtBirth = netWattsAtBirth;
            BreakEvenNutrientDensity = breakEvenNutrientDensity;
            LifetimeSeconds = lifetimeSeconds;
            ChildrenProduced = childrenProduced;
            TimeToFirstChildSeconds = timeToFirstChildSeconds;
            MatterPricePerChild = matterPricePerChild;
            DiedOfStarvation = diedOfStarvation;
        }

        public override string ToString() =>
            $"{NetWattsAtBirth:0.###} W at birth, R0={ChildrenProduced}, " +
            $"lifetime {LifetimeSeconds:0.#} s{(DiedOfStarvation ? " (starved)" : " (censored)")}";
    }
}
