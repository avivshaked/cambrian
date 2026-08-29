using System;
using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>
    /// Dead matter in the water, and what is left of it after everyone has fed — DESIGN.md §5A.2c.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A stock, not a density, and that distinction is the whole design.</b> Before this,
    /// <see cref="AbsorptiveCell"/> read a nutrient density from its surroundings and converted it
    /// to joules with nothing anywhere being reduced — the same infinite-subsidy shape that made
    /// the population unbounded when light worked that way (§5A.2b, logbook/0011). It had simply
    /// not bitten yet, because the density was always zero. Building the pool as a finite stock
    /// that feeding depletes means the fault cannot appear at all rather than appearing later
    /// under a different name.
    /// </para>
    /// <para>
    /// <b>Where the energy comes from is a creature that died</b>, and it was paid for when that
    /// creature was built (<see cref="CellType.TissueEnergyPerCubicMetre"/>). So this pool moves
    /// energy sideways and never creates it — which is what lets §5A.2's audit stay a hard
    /// equality across the whole food web rather than a check on photosynthesis alone.
    /// </para>
    /// <para>
    /// <b>Detritus sinks</b>, which is what makes the deep an economic niche rather than only a
    /// dark one (§5A.4). Light falls off downward and food falls <i>toward</i> the dark, so the
    /// two gradients oppose each other and neither strategy wins everywhere. Nobody had to
    /// arrange that: it follows from photosynthesis needing the surface and corpses having mass.
    /// </para>
    /// <para>
    /// Sharing is proportional and solved in two passes, exactly as <see cref="LightField"/> does
    /// it, and for the same reason: feeding one creature at a time in list order would let
    /// whoever the loop reached first eat their fill, making income depend on iteration order.
    /// </para>
    /// </remarks>
    public sealed class NutrientField
    {
        private readonly List<double> _stock = new List<double>();
        private readonly List<double> _demand = new List<double>();
        private readonly List<double> _sinking = new List<double>();

        /// <summary>Horizontal area of the world, m².</summary>
        public float WorldArea { get; }

        /// <summary>Thickness of one layer, m. The same discretisation <see cref="LightField"/> uses.</summary>
        public float LayerMetres { get; }

        /// <summary>How fast detritus falls, m/s.</summary>
        public float SinkMetresPerSecond { get; }

        /// <summary>Layers in the world. The last one is the floor and detritus piles up there.</summary>
        public int LayerCount { get; }

        public NutrientField(float worldArea, float layerMetres, float sinkMetresPerSecond, float worldDepth)
        {
            if (!(worldArea > 0f) || float.IsInfinity(worldArea))
                throw new ArgumentOutOfRangeException(nameof(worldArea), worldArea, "Must be positive and finite.");
            if (!(layerMetres > 0f) || float.IsInfinity(layerMetres))
                throw new ArgumentOutOfRangeException(nameof(layerMetres), layerMetres, "Must be positive and finite.");
            if (!(sinkMetresPerSecond >= 0f) || float.IsInfinity(sinkMetresPerSecond))
                throw new ArgumentOutOfRangeException(nameof(sinkMetresPerSecond), sinkMetresPerSecond, "Must be finite and not negative.");
            if (!(worldDepth > 0f) || float.IsInfinity(worldDepth))
                throw new ArgumentOutOfRangeException(nameof(worldDepth), worldDepth, "Must be positive and finite.");

            WorldArea = worldArea;
            LayerMetres = layerMetres;
            SinkMetresPerSecond = sinkMetresPerSecond;
            LayerCount = Math.Max(1, (int)Math.Ceiling(worldDepth / layerMetres));

            for (int i = 0; i < LayerCount; i++)
            {
                _stock.Add(0.0);
                _demand.Add(0.0);
                _sinking.Add(0.0);
            }
        }

        /// <summary>Volume of one layer, m³.</summary>
        public float LayerVolume => WorldArea * LayerMetres;

        /// <summary>Everything the pool holds, J. Part of §5A.2's audit.</summary>
        /// <remarks>
        /// A double, because a long run accumulates and spends this millions of times and a float
        /// would stop registering small additions long before the run ended — the failure mode
        /// where an energy audit silently becomes decorative.
        /// </remarks>
        public double TotalJoules
        {
            get
            {
                double sum = 0.0;
                for (int i = 0; i < _stock.Count; i++) sum += _stock[i];
                return sum;
            }
        }

        /// <summary>The layer a world height falls in, clamped to the world.</summary>
        /// <remarks>
        /// Clamped rather than extended, because unlike light the pool has to be conserved: a
        /// deposit at a depth with no layer would vanish, and vanished energy is exactly what the
        /// audit exists to notice. The deepest layer is the sea floor and detritus piles up on it.
        /// </remarks>
        public int LayerOf(float heightY)
        {
            int layer = heightY >= 0f ? 0 : (int)(-heightY / LayerMetres);
            if (layer < 0) return 0;
            return layer >= LayerCount ? LayerCount - 1 : layer;
        }

        /// <summary>Adds dead tissue at a depth.</summary>
        public void Deposit(float heightY, float joules)
        {
            if (!(joules > 0f)) return;
            _stock[LayerOf(heightY)] += joules;
        }

        /// <summary>Energy density of the water at a depth, J/m³ — what a feeding cell reads.</summary>
        public float DensityAt(float heightY) => (float)(_stock[LayerOf(heightY)] / LayerVolume);

        /// <summary>Discards last step's demand. Call before <see cref="Demand"/>.</summary>
        public void ClearDemand()
        {
            for (int i = 0; i < _demand.Count; i++) _demand[i] = 0.0;
        }

        /// <summary>Registers what one creature would take at this depth if nothing competed.</summary>
        public void Demand(float heightY, float joules)
        {
            if (!(joules > 0f)) return;
            _demand[LayerOf(heightY)] += joules;
        }

        /// <summary>
        /// The fraction of its demand a feeder at this depth actually gets, in [0, 1].
        /// </summary>
        /// <remarks>
        /// 1 while the layer holds more than its feeders want, falling as they exhaust it. Valid
        /// after every <see cref="Demand"/> for the step has been registered.
        /// </remarks>
        public float ShareAt(float heightY)
        {
            int layer = LayerOf(heightY);
            double wanted = _demand[layer];

            if (wanted <= 0.0) return 1f;
            double available = _stock[layer];

            return available >= wanted ? 1f : (float)(available / wanted);
        }

        /// <summary>Removes energy from the pool and returns what was actually there to take.</summary>
        public float Take(float heightY, float joules)
        {
            if (!(joules > 0f)) return 0f;

            int layer = LayerOf(heightY);
            double taken = Math.Min(joules, _stock[layer]);
            if (taken <= 0.0) return 0f;

            _stock[layer] -= taken;
            return (float)taken;
        }

        /// <summary>Moves detritus downward by one step's worth of sinking.</summary>
        /// <remarks>
        /// <para>
        /// A fraction of each layer moves down rather than the whole layer moving a distance: with
        /// layers of fixed thickness, a sink speed slower than one layer per step has nowhere else
        /// to go. The fraction is capped at 1, so a step long enough to cross several layers moves
        /// everything down exactly one and no further — a limitation of the discretisation, and
        /// the conservative direction to fail in, since detritus arrives at the deep more slowly
        /// than it should rather than skipping past anything that might have eaten it.
        /// </para>
        /// <para>
        /// Computed into a buffer and applied afterwards, so that a layer cannot receive what fell
        /// from above and immediately pass it on within the same step — which is what a single
        /// top-down in-place loop would do, giving a sink rate that depends on which end you start.
        /// </para>
        /// </remarks>
        public void Settle(float seconds)
        {
            if (SinkMetresPerSecond <= 0f || LayerCount < 2) return;

            double fraction = SinkMetresPerSecond * seconds / LayerMetres;
            if (fraction <= 0.0) return;
            if (fraction > 1.0) fraction = 1.0;

            for (int i = 0; i < _sinking.Count; i++) _sinking[i] = 0.0;

            // The floor keeps what it has: there is nowhere below it.
            for (int layer = 0; layer < LayerCount - 1; layer++)
            {
                _sinking[layer] = _stock[layer] * fraction;
            }

            for (int layer = 0; layer < LayerCount - 1; layer++)
            {
                _stock[layer] -= _sinking[layer];
                _stock[layer + 1] += _sinking[layer];
            }
        }

        /// <summary>Leaks a fraction of the floor's stock into the layer above it — D051.</summary>
        /// <param name="seconds">Interval to decay over.</param>
        /// <param name="ratePerSecond">
        /// First-order rate constant, s⁻¹. Zero leaves the field exactly as it was.
        /// </param>
        /// <remarks>
        /// <see cref="Settle"/> pays into the floor and nothing pays out of it, so without this a
        /// pool with an inflow and no outflow ratchets to the bottom over any long-enough run.
        /// This is the return leg: first-order decay of the floor stock, standing in for benthic
        /// remineralisation, with <see cref="Mix"/> doing the further, upward transport from
        /// there exactly as it does for every other gradient in the column.
        /// </remarks>
        public void Remineralise(double seconds, float ratePerSecond)
        {
            if (!(ratePerSecond > 0f) || LayerCount < 2) return;

            double fraction = Math.Min(1.0, ratePerSecond * seconds);
            double moved = _stock[LayerCount - 1] * fraction;

            _stock[LayerCount - 1] -= moved;
            _stock[LayerCount - 2] += moved;
        }

        /// <summary>
        /// Stirs detritus between neighbouring layers — DESIGN.md §5A.4, D036.
        /// </summary>
        /// <param name="seconds">Interval to mix over.</param>
        /// <param name="diffusivity">
        /// Eddy diffusivity, m²/s. Zero leaves the field exactly as it was.
        /// </param>
        /// <remarks>
        /// <para>
        /// <b>This is the world's only return path for energy, and without it there is none.</b>
        /// Light enters at the surface, plants grow and die at the surface, and
        /// <see cref="Settle"/> carries their bodies down past everything that could eat them onto
        /// a floor sixty metres below anything alive. Measured: 77.5% of every joule of dead matter
        /// the world had ever produced was lying on the sediment, and the nutrient density where
        /// the living population actually sat was exactly zero (logbook/0021). The audit still
        /// closed at 0.0000%, which is the point — the energy was never lost, it was immobilised.
        /// Real oceans have this problem and solve it by mixing.
        /// </para>
        /// <para>
        /// <b>What it buys is a gradient where there was a step.</b> With all the detritus on the
        /// floor, a creature that dives one metre gains nothing, thirty metres gains nothing and
        /// fifty-nine metres gains everything — and evolution cannot climb a step function,
        /// particularly one approached downhill through failing light. Spread the same detritus
        /// through the column and diving one metre is worth one metre of food, immediately. That is
        /// the difference between a deceptive task [K12] and an ordinary one.
        /// </para>
        /// <para>
        /// <b>Conservative by construction.</b> Every joule that leaves a layer arrives in a
        /// neighbour, computed as fluxes across the interfaces rather than as a per-layer average —
        /// so it cannot create or destroy detritus however coarse the timestep, and §5A.2's audit
        /// never has to trust it. The boundaries are closed: the surface has nothing above it and
        /// the floor nothing below, and a flux that is not written is a flux that does not exist.
        /// </para>
        /// <para>
        /// <b>Clamped rather than sub-stepped.</b> Explicit diffusion goes unstable above a
        /// Courant number of ½ and would oscillate a layer negative — which conservation would
        /// happily preserve, giving a world with a debt of detritus in one layer and a surplus in
        /// the next. The mixed fraction is capped there instead. A capped step is a slower stir
        /// than asked for; an uncapped one is a different physics.
        /// </para>
        /// </remarks>
        public void Mix(float seconds, float diffusivity)
        {
            if (!(diffusivity > 0f) || !(seconds > 0f) || LayerCount < 2) return;

            // Fick's law across each interface, discretised: the flux between neighbours is the
            // diffusivity times the concentration difference over the layer thickness. Working in
            // stock rather than concentration is the same equation because every layer has the
            // same volume, and it keeps the arithmetic in the units the audit is written in.
            double fraction = diffusivity * seconds / (LayerMetres * LayerMetres);
            if (fraction > 0.5) fraction = 0.5;

            for (int i = 0; i < _sinking.Count; i++) _sinking[i] = 0.0;

            // _sinking is reused as the flux buffer: it is scratch, cleared at the top of both
            // methods, and a second array of the same shape would be one more thing to keep in
            // step with LayerCount.
            for (int layer = 0; layer < LayerCount - 1; layer++)
            {
                _sinking[layer] = (_stock[layer] - _stock[layer + 1]) * fraction;
            }

            for (int layer = 0; layer < LayerCount - 1; layer++)
            {
                _stock[layer] -= _sinking[layer];
                _stock[layer + 1] += _sinking[layer];
            }
        }

        /// <summary>What one layer holds, J. For reporting and for tests.</summary>
        public double StockInLayer(int layer) =>
            layer < 0 || layer >= _stock.Count ? 0.0 : _stock[layer];

        public override string ToString() =>
            $"{TotalJoules:0} J over {LayerCount} layers, sinking {SinkMetresPerSecond:0.###} m/s";
    }
}
