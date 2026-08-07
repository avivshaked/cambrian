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
        /// <summary>Joules acquired: light, nutrients, tissue.</summary>
        public float Income { get; }

        /// <summary>Joules spent on standing costs — tissue upkeep and idle joint capacity.</summary>
        public float Upkeep { get; }

        /// <summary>Joules spent on neurons and their connections.</summary>
        public float Neural { get; }

        /// <summary>Joules spent doing mechanical work at the joints.</summary>
        public float Work { get; }

        public EnergyLedger(float income, float upkeep, float neural, float work)
        {
            Income = income;
            Upkeep = upkeep;
            Neural = neural;
            Work = work;
        }

        public float Expenditure => Upkeep + Neural + Work;
        public float Net => Income - Expenditure;

        public static EnergyLedger operator +(EnergyLedger a, EnergyLedger b) =>
            new EnergyLedger(
                a.Income + b.Income, a.Upkeep + b.Upkeep, a.Neural + b.Neural, a.Work + b.Work);

        public override string ToString() =>
            $"+{Income:0.###} −{Expenditure:0.###} = {Net:0.###} J";
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
        public static EnergyLedger StepAt(
            Phenotype phenotype,
            RunConfig config,
            float irradiance,
            float nutrientDensity,
            float workJoules,
            float seconds)
        {
            if (phenotype == null) throw new ArgumentNullException(nameof(phenotype));
            if (config == null) throw new ArgumentNullException(nameof(config));

            float income = 0f, upkeep = 0f, neural = 0f;

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
                    dof: part.JointType.DofCount());

                income += cell.Acquire(context);
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

            return new EnergyLedger(
                income, upkeep, neural, Math.Max(0f, workJoules) * config.WorkCostMultiplier);
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

        /// <remarks>
        /// A metre of attenuation and a kilometre down: irradiance underflows to zero, so
        /// <see cref="StandingWatts"/> measures cost with no income mixed in. Cheaper and less
        /// fragile than a special case inside <see cref="Step"/>, which would be a branch that
        /// only the reporting path ever took.
        /// </remarks>
        private static readonly LightModel DarkWorld = new LightModel(1f, 1f);
    }
}
