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

        /// <summary>What one layer holds, J. For reporting and for tests.</summary>
        public double StockInLayer(int layer) =>
            layer < 0 || layer >= _stock.Count ? 0.0 : _stock[layer];

        public override string ToString() =>
            $"{TotalJoules:0} J over {LayerCount} layers, sinking {SinkMetresPerSecond:0.###} m/s";
    }
}
