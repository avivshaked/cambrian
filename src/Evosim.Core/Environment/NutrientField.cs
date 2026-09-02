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
    /// <para>
    /// <b>D061: storage generalises from per-layer to per-layer-per-patch.</b> Each depth layer
    /// now holds <see cref="PatchCount"/> horizontally-adjacent columns rather than one perfectly
    /// mixed slab. Every cell of the field is addressed by <c>(layer, patch)</c> and stored in a
    /// single flat list, <c>layer * PatchCount + patch</c> — the layer-major layout that keeps a
    /// layer's patches contiguous, which is what the new horizontal <see cref="Mix"/> pass walks.
    /// <see cref="Deposit(float, float, int)"/>, <see cref="Settle"/> and <see cref="Remineralise"/> stay strictly
    /// vertical and within-patch, exactly as before, just repeated <see cref="PatchCount"/> times;
    /// <see cref="Mix"/> keeps that same vertical behaviour per patch and additionally exchanges
    /// between horizontally-adjacent patches when asked to.
    /// </para>
    /// <para>
    /// <b>Every public per-depth API now has a patch-index overload.</b> The old, patch-less
    /// signatures keep working exactly as before when <see cref="PatchCount"/> is 1 — reading and
    /// writing patch 0, bit-identical to the field's whole history before D061 — and throw
    /// <see cref="InvalidOperationException"/> when <see cref="PatchCount"/> is above 1, because
    /// there is then no single honest answer to "which patch". A caller in a K&gt;1 world must
    /// say which patch it means.
    /// </para>
    /// <para>
    /// <b>Geometry: patches split <see cref="WorldArea"/> equally.</b> Each patch's own
    /// horizontal area is <c>WorldArea / PatchCount</c>, so <see cref="LayerVolume"/> — used by
    /// every density read — is now a <i>per-patch</i> volume, and <see cref="PatchWidthMetres"/>
    /// (the characteristic length horizontal diffusion mixes across) is the square root of a
    /// patch's own area, <c>sqrt(WorldArea / PatchCount)</c>: the same "treat the footprint as a
    /// square and take its side" approximation the field otherwise has no shape opinion about,
    /// chosen because <see cref="Mix"/>'s vertical pass already measures its own diffusion length
    /// the same way — a layer's thickness — and a horizontal pass wants the equivalent quantity
    /// for the plane rather than a second, differently-shaped model.
    /// </para>
    /// <para>
    /// <b>The boundary wraps — a ring, not a wall.</b> D061's own reasoning: the world has no
    /// horizontal walls, and a ring means no patch is architecturally an edge (a linear row would
    /// make the two end patches special for no ecological reason). Patch <c>PatchCount - 1</c> is
    /// adjacent to patch 0.
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

        /// <summary>
        /// Horizontal cells per layer — D061. 1 is this field's whole history before D061: a
        /// single, perfectly-mixed column per layer.
        /// </summary>
        public int PatchCount { get; }

        /// <summary>
        /// The characteristic horizontal length <see cref="Mix"/>'s horizontal pass diffuses
        /// across, m — <c>sqrt(WorldArea / PatchCount)</c>. See the class remarks for why this
        /// shape was chosen. Meaningless (and unused) when <see cref="PatchCount"/> is 1.
        /// </summary>
        public float PatchWidthMetres { get; }

        /// <summary>
        /// Layers, counted up from the floor, that no mouth can reach — D055.
        /// </summary>
        /// <remarks>0 when the field was built with no refuge, which is the field's whole history
        /// before D055 and must stay bit-identical to it.</remarks>
        public int RefugeLayerCount { get; }

        /// <summary>
        /// Fraction of a refuge layer's density that feeding can see and take, in [0, 1] —
        /// arm C's knob on D055. Zero is D055's own refuge: total exclusion.
        /// </summary>
        /// <remarks>0 when the field was built with no fraction, which is this field's whole
        /// history before the knob existed and must stay bit-identical to it. Irrelevant when
        /// <see cref="RefugeLayerCount"/> is 0 — with no refuge layers, nothing reads it.</remarks>
        public float RefugeEdibleFraction { get; }

        public NutrientField(
            float worldArea, float layerMetres, float sinkMetresPerSecond, float worldDepth,
            float refugeMetres = 0f, float refugeEdibleFraction = 0f, int patchCount = 1)
        {
            if (!(worldArea > 0f) || float.IsInfinity(worldArea))
                throw new ArgumentOutOfRangeException(nameof(worldArea), worldArea, "Must be positive and finite.");
            if (!(layerMetres > 0f) || float.IsInfinity(layerMetres))
                throw new ArgumentOutOfRangeException(nameof(layerMetres), layerMetres, "Must be positive and finite.");
            if (!(sinkMetresPerSecond >= 0f) || float.IsInfinity(sinkMetresPerSecond))
                throw new ArgumentOutOfRangeException(nameof(sinkMetresPerSecond), sinkMetresPerSecond, "Must be finite and not negative.");
            if (!(worldDepth > 0f) || float.IsInfinity(worldDepth))
                throw new ArgumentOutOfRangeException(nameof(worldDepth), worldDepth, "Must be positive and finite.");
            if (!(refugeMetres >= 0f) || float.IsInfinity(refugeMetres))
                throw new ArgumentOutOfRangeException(nameof(refugeMetres), refugeMetres, "Must be finite and not negative.");
            if (!(refugeEdibleFraction >= 0f) || refugeEdibleFraction > 1f)
                throw new ArgumentOutOfRangeException(
                    nameof(refugeEdibleFraction), refugeEdibleFraction,
                    "Must be in [0, 1]. Above 1 feeding would take more than the refuge holds, " +
                    "and negative is not a fraction at all.");
            if (patchCount < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(patchCount), patchCount, "A field needs at least one patch — D061's K >= 1.");

            WorldArea = worldArea;
            LayerMetres = layerMetres;
            SinkMetresPerSecond = sinkMetresPerSecond;
            LayerCount = Math.Max(1, (int)Math.Ceiling(worldDepth / layerMetres));
            PatchCount = patchCount;
            PatchWidthMetres = (float)Math.Sqrt(WorldArea / PatchCount);
            RefugeLayerCount = Math.Min(LayerCount, (int)Math.Ceiling(refugeMetres / layerMetres));
            RefugeEdibleFraction = refugeEdibleFraction;

            int cells = LayerCount * PatchCount;
            for (int i = 0; i < cells; i++)
            {
                _stock.Add(0.0);
                _demand.Add(0.0);
                _sinking.Add(0.0);
            }
        }

        /// <summary>Flat storage index for a (layer, patch) cell — the layer-major layout the class remarks describe.</summary>
        private int Cell(int layer, int patch) => layer * PatchCount + patch;

        /// <summary>Throws if <paramref name="patch"/> is not a real patch of this field.</summary>
        private void ValidatePatch(int patch)
        {
            if (patch < 0 || patch >= PatchCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(patch), patch,
                    $"This field has {PatchCount} patch(es), indexed 0..{PatchCount - 1}.");
            }
        }

        /// <summary>
        /// What every patch-less overload below resolves to. Patch 0 when there is only one
        /// patch — bit-identical to this field's whole history before D061 — and a refusal
        /// otherwise, because with more than one patch there is no single honest answer to
        /// "which patch" and a caller must say.
        /// </summary>
        private int SinglePatchOrThrow()
        {
            if (PatchCount > 1)
            {
                throw new InvalidOperationException(
                    $"This field has {PatchCount} patches (D061), so a caller must say which one " +
                    "— use the overload that takes a patch index rather than the pre-D061 signature.");
            }

            return 0;
        }

        /// <summary>Whether a layer is buried beyond any mouth's reach — D055.</summary>
        public bool IsRefuge(int layer) => layer >= LayerCount - RefugeLayerCount;

        /// <summary>
        /// What a refuge layer's stock feeding may currently see and take, J, in one patch — the
        /// arm C generalisation of D055's all-or-nothing refuge.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Re-evaluated from the current stock, not tracked as its own ledger.</b> The edible
        /// share of a refuge layer is <c>RefugeEdibleFraction × _stock[cell]</c> at the instant
        /// this is called, exactly as before D061 — only the cell being read has gained a patch
        /// index. That is deliberately not an exact per-step bound: two draws against the same
        /// refuge cell in one step (two feeders at the refuge depth in the same patch, or a
        /// recomputed short-larder ledger — see <see cref="World.Metabolise"/>) each see the
        /// fraction of what is left <i>after</i> the first, so the second can never take more
        /// than the edible share of the remainder. It cannot be pushed past 100% of the true
        /// stock either — every <c>Take</c> is still capped at the cell's own stock — and it is
        /// self-limiting the same way compound interest is. A layer with no refuge
        /// (<see cref="IsRefuge"/> false) has no such limit at all — this method is only ever
        /// consulted from inside a refuge branch.
        /// </para>
        /// </remarks>
        private double EdibleStock(int layer, int patch) => _stock[Cell(layer, patch)] * RefugeEdibleFraction;

        /// <summary>
        /// Volume of one layer <i>in one patch</i>, m³ — <see cref="WorldArea"/> divided equally
        /// among <see cref="PatchCount"/> patches, times <see cref="LayerMetres"/>. Bit-identical
        /// to the whole-world layer volume this property reported before D061 when
        /// <see cref="PatchCount"/> is 1.
        /// </summary>
        public float LayerVolume => (WorldArea / PatchCount) * LayerMetres;

        /// <summary>Everything the pool holds, across every layer and every patch, J. Part of §5A.2's audit.</summary>
        /// <remarks>
        /// A double, because a long run accumulates and spends this millions of times and a float
        /// would stop registering small additions long before the run ended — the failure mode
        /// where an energy audit silently becomes decorative. Summing the flat store rather than
        /// summing per patch: the total does not care how the field is subdivided.
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

        /// <summary>The layer a world height falls in, clamped to the world. Patch-independent.</summary>
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

        /// <summary>Adds dead tissue at a depth, in one patch.</summary>
        public void Deposit(float heightY, float joules, int patch)
        {
            if (!(joules > 0f)) return;
            ValidatePatch(patch);
            _stock[Cell(LayerOf(heightY), patch)] += joules;
        }

        /// <summary>Pre-D061 signature — patch 0 when <see cref="PatchCount"/> is 1, throws otherwise.</summary>
        public void Deposit(float heightY, float joules) => Deposit(heightY, joules, SinglePatchOrThrow());

        /// <summary>
        /// Energy density of the water at a depth, in one patch, J/m³ — what the water physically
        /// holds.
        /// </summary>
        /// <remarks>
        /// Truthful regardless of <see cref="RefugeLayerCount"/>: a refuge changes what feeding
        /// can price, not what the field reports. See <see cref="EdibleDensityAt(float, int)"/>
        /// for the version a mouth actually reads — D055.
        /// </remarks>
        public float DensityAt(float heightY, int patch)
        {
            ValidatePatch(patch);
            return (float)(_stock[Cell(LayerOf(heightY), patch)] / LayerVolume);
        }

        /// <summary>Pre-D061 signature — patch 0 when <see cref="PatchCount"/> is 1, throws otherwise.</summary>
        public float DensityAt(float heightY) => DensityAt(heightY, SinglePatchOrThrow());

        /// <summary>
        /// Energy density a feeding cell may actually draw at a depth, in one patch, J/m³ — D055,
        /// generalised by <see cref="RefugeEdibleFraction"/>.
        /// </summary>
        /// <remarks>
        /// <c>RefugeEdibleFraction × DensityAt</c> inside the refuge — zero at the D055 default,
        /// whatever <see cref="DensityAt(float, int)"/> reports there; identical to
        /// <see cref="DensityAt(float, int)"/> everywhere else. This is what <see cref="Demand(float, float, int)"/>
        /// and <see cref="Take(float, float, int)"/> enforce, so it is what a caller should price rather than
        /// reimplementing the refuge check.
        /// </remarks>
        public float EdibleDensityAt(float heightY, int patch)
        {
            ValidatePatch(patch);
            int layer = LayerOf(heightY);
            double stock = IsRefuge(layer) ? EdibleStock(layer, patch) : _stock[Cell(layer, patch)];
            return (float)(stock / LayerVolume);
        }

        /// <summary>Pre-D061 signature — patch 0 when <see cref="PatchCount"/> is 1, throws otherwise.</summary>
        public float EdibleDensityAt(float heightY) => EdibleDensityAt(heightY, SinglePatchOrThrow());

        /// <summary>Discards last step's demand, in every patch. Call before <see cref="Demand(float, float, int)"/>.</summary>
        public void ClearDemand()
        {
            for (int i = 0; i < _demand.Count; i++) _demand[i] = 0.0;
        }

        /// <summary>Registers what one creature would take at this depth and patch if nothing competed.</summary>
        /// <remarks>
        /// Refuses a refuge layer outright at <see cref="RefugeEdibleFraction"/> zero — D055.
        /// The field enforces its own invariant here so no caller can forget it by pricing
        /// <see cref="DensityAt(float, int)"/> instead of <see cref="EdibleDensityAt(float, int)"/>.
        /// Above zero, demand against a refuge cell is registered exactly like any other cell's —
        /// what bounds it is <see cref="ShareAt(float, int)"/> and <see cref="Take(float, float, int)"/> reading the edible
        /// share of stock rather than the whole of it.
        /// </remarks>
        public void Demand(float heightY, float joules, int patch)
        {
            if (!(joules > 0f)) return;
            ValidatePatch(patch);
            int layer = LayerOf(heightY);
            if (IsRefuge(layer) && RefugeEdibleFraction <= 0f) return;
            _demand[Cell(layer, patch)] += joules;
        }

        /// <summary>Pre-D061 signature — patch 0 when <see cref="PatchCount"/> is 1, throws otherwise.</summary>
        public void Demand(float heightY, float joules) => Demand(heightY, joules, SinglePatchOrThrow());

        /// <summary>
        /// The fraction of its demand a feeder at this depth and patch actually gets, in [0, 1].
        /// </summary>
        /// <remarks>
        /// 1 while the cell holds more than its feeders want, falling as they exhaust it. Valid
        /// after every <see cref="Demand(float, float, int)"/> for the step has been registered. Inside a refuge
        /// layer, "holds" means <see cref="EdibleStock"/> — the fraction of stock feeding can see
        /// — not the full stock, so competitors sharing that patch can never be told there is more
        /// to share than the refuge actually exposes. Patches never compete with each other:
        /// demand and stock are both per-cell.
        /// </remarks>
        public float ShareAt(float heightY, int patch)
        {
            ValidatePatch(patch);
            int layer = LayerOf(heightY);
            int cell = Cell(layer, patch);
            double wanted = _demand[cell];

            if (wanted <= 0.0) return 1f;
            double available = IsRefuge(layer) ? EdibleStock(layer, patch) : _stock[cell];

            return available >= wanted ? 1f : (float)(available / wanted);
        }

        /// <summary>Pre-D061 signature — patch 0 when <see cref="PatchCount"/> is 1, throws otherwise.</summary>
        public float ShareAt(float heightY) => ShareAt(heightY, SinglePatchOrThrow());

        /// <summary>Removes energy from one patch's pool and returns what was actually there to take.</summary>
        /// <remarks>
        /// <para>
        /// At <see cref="RefugeEdibleFraction"/> zero, refuses a refuge layer outright — D055,
        /// and the same enforcement <see cref="Demand(float, float, int)"/> applies. Stock in that cell is not
        /// touched, matching <c>ShareAt</c>'s reading of it for whoever registered no demand
        /// there.
        /// </para>
        /// <para>
        /// Above zero, a refuge cell's cap is <see cref="EdibleStock"/> rather than the full
        /// stock — the fraction of what remains <i>right now</i>, re-evaluated on every call
        /// rather than tracked as a separate per-step ledger. That is deliberately the simplest
        /// correct form and not an exact per-step bound: two draws against the same cell in one
        /// step each see the edible share of what the first left behind, so repeated taking is
        /// self-limiting — it approaches but can never reach zero — rather than being metered
        /// against a fixed per-step allowance. It can never remove more than the full physical
        /// stock either way, because the edible cap is itself a fraction (≤ 1) of that stock.
        /// </para>
        /// </remarks>
        public float Take(float heightY, float joules, int patch)
        {
            if (!(joules > 0f)) return 0f;

            ValidatePatch(patch);
            int layer = LayerOf(heightY);
            int cell = Cell(layer, patch);
            double cap = IsRefuge(layer) ? EdibleStock(layer, patch) : _stock[cell];

            double taken = Math.Min(joules, cap);
            if (taken <= 0.0) return 0f;

            _stock[cell] -= taken;
            return (float)taken;
        }

        /// <summary>Pre-D061 signature — patch 0 when <see cref="PatchCount"/> is 1, throws otherwise.</summary>
        public float Take(float heightY, float joules) => Take(heightY, joules, SinglePatchOrThrow());

        /// <summary>
        /// Moves detritus downward by one step's worth of sinking, independently within every
        /// patch. Vertical and within-patch only — D061 does not change what this does, only how
        /// many times over it does it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A fraction of each cell moves down rather than the whole cell moving a distance: with
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

            // The floor keeps what it has: there is nowhere below it. Every patch sinks on its
            // own — no term here ever reads or writes a different patch's cell.
            for (int layer = 0; layer < LayerCount - 1; layer++)
            {
                for (int patch = 0; patch < PatchCount; patch++)
                {
                    int cell = Cell(layer, patch);
                    _sinking[cell] = _stock[cell] * fraction;
                }
            }

            for (int layer = 0; layer < LayerCount - 1; layer++)
            {
                for (int patch = 0; patch < PatchCount; patch++)
                {
                    int from = Cell(layer, patch);
                    int to = Cell(layer + 1, patch);
                    _stock[from] -= _sinking[from];
                    _stock[to] += _sinking[from];
                }
            }
        }

        /// <summary>
        /// Leaks a fraction of each patch's floor stock into the layer above it — D051. Vertical
        /// and within-patch, like <see cref="Settle"/>.
        /// </summary>
        /// <param name="seconds">Interval to decay over.</param>
        /// <param name="ratePerSecond">
        /// First-order rate constant, s⁻¹. Zero leaves the field exactly as it was.
        /// </param>
        /// <remarks>
        /// <para>
        /// <see cref="Settle"/> pays into the floor and never out of it, so in still water a pool
        /// with an inflow and no outflow ratchets to the bottom over any long-enough run. This is
        /// a one-way return leg: first-order decay of the floor stock, standing in for benthic
        /// remineralisation. <b>Measured redundant wherever mixing is on</b> (logbook/0036):
        /// <see cref="Mix"/> already runs across the floor interface, and at 0.2 m²/s that
        /// exchange is twenty times this leak at its tested rate. The floor is a ratchet at
        /// mixing 0 only, and this is the knob for that world alone.
        /// </para>
        /// <para>
        /// <b>Exact, not a capped forward-Euler step.</b> The moved fraction is
        /// <c>1 - exp(-rate * seconds)</c>, the closed-form solution of dN/dt = -rate*N, so the
        /// result is step-size independent: one call over 10 s and ten calls over 1 s each move
        /// the same fraction of the floor. That formula never exceeds 1 on its own, so unlike
        /// <see cref="Settle"/> and <see cref="Mix"/> there is no cap to apply.
        /// </para>
        /// </remarks>
        public void Remineralise(double seconds, float ratePerSecond)
        {
            if (!(ratePerSecond > 0f) || LayerCount < 2) return;

            double fraction = 1.0 - Math.Exp(-ratePerSecond * seconds);

            for (int patch = 0; patch < PatchCount; patch++)
            {
                int floorCell = Cell(LayerCount - 1, patch);
                int aboveCell = Cell(LayerCount - 2, patch);

                double moved = _stock[floorCell] * fraction;
                _stock[floorCell] -= moved;
                _stock[aboveCell] += moved;
            }
        }

        /// <summary>
        /// Stirs detritus between neighbouring layers within each patch, and — when
        /// <paramref name="horizontalDiffusivity"/> is above zero — between horizontally
        /// adjacent patches within each layer — DESIGN.md §5A.4, D036, D061.
        /// </summary>
        /// <param name="seconds">Interval to mix over.</param>
        /// <param name="diffusivity">
        /// Vertical eddy diffusivity, m²/s. Zero leaves the vertical pass a no-op, exactly as
        /// before D061.
        /// </param>
        /// <param name="horizontalDiffusivity">
        /// Horizontal eddy diffusivity between adjacent patches, m²/s — D061's
        /// <see cref="RunConfig.HorizontalMixingDiffusivity"/>. Zero (the default, so every
        /// pre-D061 call site is unaffected) leaves the horizontal pass a no-op.
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
        /// <b>Conservative by construction, in both passes.</b> Every joule that leaves a cell
        /// arrives in a neighbour, computed as fluxes across the interfaces rather than as a
        /// per-cell average — so it cannot create or destroy detritus however coarse the
        /// timestep, and §5A.2's audit never has to trust it. The vertical boundaries are closed:
        /// the surface has nothing above it and the floor nothing below. The horizontal boundary
        /// wraps instead — a ring, D061's own choice, so no patch is architecturally an edge —
        /// which is still conservative: every flux subtracted from one patch is added to its
        /// neighbour, the neighbour just happens to be found by wrapping round rather than by
        /// stopping.
        /// </para>
        /// <para>
        /// <b>Clamped rather than sub-stepped, in both passes.</b> Explicit diffusion goes
        /// unstable above a Courant number of ½ and would oscillate a cell negative — which
        /// conservation would happily preserve, giving a world with a debt of detritus in one
        /// cell and a surplus in the next. The mixed fraction is capped there instead. A capped
        /// step is a slower stir than asked for; an uncapped one is a different physics. The
        /// horizontal pass uses <see cref="PatchWidthMetres"/> in place of <see cref="LayerMetres"/>
        /// as its diffusion length — see the class remarks for why that shape was chosen.
        /// </para>
        /// <para>
        /// <b>Bit-identical to the field's whole history before D061</b> when
        /// <paramref name="horizontalDiffusivity"/> is 0 (the default for every call that predates
        /// this knob) — the horizontal pass is skipped entirely, and the vertical pass is
        /// unchanged in every patch when <see cref="PatchCount"/> is 1.
        /// </para>
        /// </remarks>
        public void Mix(float seconds, float diffusivity, float horizontalDiffusivity = 0f)
        {
            if (diffusivity > 0f && seconds > 0f && LayerCount >= 2)
            {
                double fraction = diffusivity * seconds / (LayerMetres * LayerMetres);
                if (fraction > 0.5) fraction = 0.5;

                for (int i = 0; i < _sinking.Count; i++) _sinking[i] = 0.0;

                // _sinking is reused as the flux buffer: it is scratch, cleared before each pass
                // uses it, and a second array of the same shape would be one more thing to keep
                // in step with LayerCount * PatchCount.
                for (int layer = 0; layer < LayerCount - 1; layer++)
                {
                    for (int patch = 0; patch < PatchCount; patch++)
                    {
                        int a = Cell(layer, patch);
                        int b = Cell(layer + 1, patch);
                        _sinking[a] = (_stock[a] - _stock[b]) * fraction;
                    }
                }

                for (int layer = 0; layer < LayerCount - 1; layer++)
                {
                    for (int patch = 0; patch < PatchCount; patch++)
                    {
                        int a = Cell(layer, patch);
                        int b = Cell(layer + 1, patch);
                        _stock[a] -= _sinking[a];
                        _stock[b] += _sinking[a];
                    }
                }
            }

            // D061's horizontal pass. Same Fick's-law form as the vertical one above, geometry
            // from PatchWidthMetres rather than LayerMetres, and the neighbour found by wrapping
            // round the ring rather than stopping at an edge — see the class remarks.
            if (horizontalDiffusivity > 0f && seconds > 0f && PatchCount >= 2)
            {
                double hFraction = horizontalDiffusivity * seconds / (PatchWidthMetres * PatchWidthMetres);
                if (hFraction > 0.5) hFraction = 0.5;

                for (int i = 0; i < _sinking.Count; i++) _sinking[i] = 0.0;

                for (int layer = 0; layer < LayerCount; layer++)
                {
                    for (int patch = 0; patch < PatchCount; patch++)
                    {
                        int a = Cell(layer, patch);
                        int b = Cell(layer, (patch + 1) % PatchCount);
                        _sinking[a] = (_stock[a] - _stock[b]) * hFraction;
                    }
                }

                for (int layer = 0; layer < LayerCount; layer++)
                {
                    for (int patch = 0; patch < PatchCount; patch++)
                    {
                        int a = Cell(layer, patch);
                        int b = Cell(layer, (patch + 1) % PatchCount);
                        _stock[a] -= _sinking[a];
                        _stock[b] += _sinking[a];
                    }
                }
            }
        }

        /// <summary>
        /// Carries the stock with the water — D066. Does nothing unless
        /// <see cref="CurrentField.AdvectFields"/> is on.
        /// </summary>
        /// <param name="current">The flow, or null for none.</param>
        /// <param name="seconds">The world's clock, s — where in the flow's own cycle this is.</param>
        /// <param name="dt">Step length, s.</param>
        /// <param name="patchWidthMetres">
        /// Width of one patch, m. <see cref="PatchWidthMetres"/> is the answer for a field built
        /// over the whole world; it is a parameter so that a caller measuring something else does
        /// not have to lie to this field about its own geometry.
        /// </param>
        /// <remarks>
        /// <para>
        /// <b>Advection, not diffusion, and the difference is the point.</b> <see cref="Mix"/>
        /// moves stock down a gradient and cannot carry anything against one; this moves stock
        /// wherever the water goes, uphill included. That is what makes the deep larder reachable:
        /// detritus that sank is carried back into the light because the water it is in is going
        /// there, not because there is less detritus up there.
        /// </para>
        /// <para>
        /// <b>Upwind, and conservative by construction.</b> Every move takes a fraction of one
        /// cell's stock and gives all of it to a neighbour — across a layer interface within a
        /// patch, or across a patch boundary within a layer — so the total is unchanged to the
        /// last bit of the arithmetic, at any timestep, and §5A.2's audit never has to trust this
        /// method. The fraction is <c>min(½, |v|·dt/L)</c>, the same Courant clamp <see cref="Mix"/>
        /// uses and for a stronger reason: a cell has two faces in each pass, so a clamp at ½ is
        /// exactly what guarantees a cell is never asked for more than it holds and no stock can
        /// go negative.
        /// </para>
        /// <para>
        /// <b>Every move is computed from a snapshot and applied afterwards</b>, in both passes,
        /// so nothing that arrives in a cell can be passed straight on within the same step and
        /// the result does not depend on which end of the loop the pass started at — the same
        /// argument, and the same buffer, as <see cref="Settle"/> and <see cref="Mix"/>.
        /// </para>
        /// <para>
        /// <b>Nothing leaves the world.</b> The vertical pass only ever walks interfaces between
        /// two real layers, and the flow is zero at the waterline and at the bottom of the roll
        /// cell in any case; the horizontal pass wraps around D061's ring. There is no flux out of
        /// the top, the bottom or the side, so the only way to lose stock here would be an
        /// arithmetic one.
        /// </para>
        /// <para>
        /// <b>Every adjacent pair of patches is treated alike</b>, not only the pairs that make up
        /// a roll. In the analytic field the horizontal flow is continuous across the boundary
        /// <i>between</i> two rolls — patches 1 and 2 of rolls (0,1) and (2,3) are a down-leg and
        /// an up-leg facing each other, and water crosses there exactly as it does inside a roll.
        /// Skipping those boundaries would seal alternate pairs of patches off from each other,
        /// which is the sealed-pool failure D066 exists to end. The direction comes from the two
        /// patches' parity via <see cref="CurrentField.CrossingDirection"/>, so it is the same
        /// convention everywhere.
        /// </para>
        /// </remarks>
        public void Advect(CurrentField current, double seconds, float dt, float patchWidthMetres)
        {
            if (current == null || !current.AdvectFields) return;
            if (!(dt > 0f)) return;

            // ---- vertical, within each patch, across each layer interface
            if (LayerCount >= 2)
            {
                for (int i = 0; i < _sinking.Count; i++) _sinking[i] = 0.0;

                // _sinking[Cell(layer, patch)] is the signed move across the interface *below*
                // layer — positive downward, so the sign convention matches Settle's, which writes
                // the same slot for the same interface.
                for (int layer = 0; layer < LayerCount - 1; layer++)
                {
                    float interfaceY = -((layer + 1) * LayerMetres);

                    for (int patch = 0; patch < PatchCount; patch++)
                    {
                        double w = current.VelocityAt(interfaceY, seconds, patch, PatchCount).Y;
                        if (w == 0d) continue;

                        double fraction = Math.Abs(w) * dt / LayerMetres;
                        if (fraction > 0.5) fraction = 0.5;

                        int upper = Cell(layer, patch);
                        int lower = Cell(layer + 1, patch);

                        // Upwind: rising water carries what is below it up, sinking water carries
                        // what is above it down.
                        _sinking[upper] = w > 0d
                            ? -_stock[lower] * fraction
                            : _stock[upper] * fraction;
                    }
                }

                for (int layer = 0; layer < LayerCount - 1; layer++)
                {
                    for (int patch = 0; patch < PatchCount; patch++)
                    {
                        int upper = Cell(layer, patch);
                        int lower = Cell(layer + 1, patch);

                        _stock[upper] -= _sinking[upper];
                        _stock[lower] += _sinking[upper];
                    }
                }
            }

            // ---- horizontal, within each layer, across each patch boundary of the ring
            if (PatchCount >= 2 && patchWidthMetres > 0f)
            {
                for (int i = 0; i < _sinking.Count; i++) _sinking[i] = 0.0;

                // _sinking[Cell(layer, patch)] is the signed move across the boundary between
                // patch and patch+1 — positive toward patch+1, matching CrossingDirection.
                for (int layer = 0; layer < LayerCount; layer++)
                {
                    float midY = -((layer + 0.5f) * LayerMetres);

                    for (int patch = 0; patch < PatchCount; patch++)
                    {
                        int direction = current.CrossingDirection(midY, seconds, patch, PatchCount);
                        if (direction == 0) continue;

                        double fraction = current.HorizontalCrossingFraction(
                            midY, seconds, patch, PatchCount, dt, patchWidthMetres);
                        if (fraction <= 0d) continue;

                        int here = Cell(layer, patch);
                        int next = Cell(layer, (patch + 1) % PatchCount);

                        _sinking[here] = direction > 0
                            ? _stock[here] * fraction
                            : -_stock[next] * fraction;
                    }
                }

                for (int layer = 0; layer < LayerCount; layer++)
                {
                    for (int patch = 0; patch < PatchCount; patch++)
                    {
                        int here = Cell(layer, patch);
                        int next = Cell(layer, (patch + 1) % PatchCount);

                        _stock[here] -= _sinking[here];
                        _stock[next] += _sinking[here];
                    }
                }
            }
        }

        /// <summary>What one layer in one patch holds, J. For reporting and for tests.</summary>
        public double StockInLayer(int layer, int patch)
        {
            if (layer < 0 || layer >= LayerCount) return 0.0;
            ValidatePatch(patch);
            return _stock[Cell(layer, patch)];
        }

        /// <summary>Pre-D061 signature — patch 0 when <see cref="PatchCount"/> is 1, throws otherwise.</summary>
        public double StockInLayer(int layer) => StockInLayer(layer, SinglePatchOrThrow());

        public override string ToString() =>
            PatchCount > 1
                ? $"{TotalJoules:0} J over {LayerCount} layers x {PatchCount} patches, sinking {SinkMetresPerSecond:0.###} m/s"
                : $"{TotalJoules:0} J over {LayerCount} layers, sinking {SinkMetresPerSecond:0.###} m/s";
    }
}
