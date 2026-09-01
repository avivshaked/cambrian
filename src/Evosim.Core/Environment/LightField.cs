using System;
using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>
    /// Divides the world's light between everything competing for it — DESIGN.md §5A.2b.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this exists: without it the population is unbounded, and no calibration can fix
    /// that.</b> §5A.2 poses the ratio of basal metabolism to peak photosynthesis as the knob that
    /// decides everything, and it was swept over 400× looking for the setting where a population
    /// holds steady (logbook/0011). There is no such setting. With <see cref="LightModel"/> alone,
    /// a creature's income depends only on its own depth, so every creature above break-even
    /// accumulates surplus at a fixed rate and breeds on a fixed period no matter how many others
    /// exist. That is a linear birth process: it grows without bound for <i>any</i> irradiance
    /// above break-even and goes extinct below it. A step function, not a transition. The knob
    /// only ever decided how fast the world exploded.
    /// </para>
    /// <para>
    /// <b>What was missing is density dependence, and the physically honest source of it is that
    /// the sun is finite.</b> Sunlight arrives as watts per square metre of surface, so a world of
    /// finite width receives finite power, and light one creature absorbs is light that never
    /// reaches whatever is below it. That is a conservation law rather than a tuning parameter,
    /// which is the kind of constraint this design prefers (§5A.2, §11.2): total photosynthetic
    /// income across the whole world cannot exceed
    /// <see cref="LightModel.SurfaceIrradiance"/> × <see cref="WorldArea"/>, whatever evolution
    /// discovers. Carrying capacity stops being a number we chose and becomes a consequence of
    /// the world having a size.
    /// </para>
    /// <para>
    /// <b>Shading uses the same exponential as the water.</b> A layer holding total projected area
    /// <i>L</i> over horizontal area <i>A</i> intercepts a fraction 1 − e<sup>−L/A</sup> of the
    /// light passing through it. This is not an analogy to Beer–Lambert — it is the same
    /// derivation, for randomly-placed absorbers, and it is why real ocean optics treats
    /// chlorophyll and water as two terms of one exponent. Choosing the naive min(1, L/A) instead
    /// would say that two creatures of half the layer's area shade it completely, which is only
    /// true if their shadows never overlap, and it would put a hard edge in the model at exactly
    /// the density where competition starts to matter.
    /// </para>
    /// <para>
    /// <b>It reduces to the unshaded model exactly.</b> As L → 0 the effective irradiance tends to
    /// the plain <see cref="LightModel.IrradianceAt"/> value, continuously — there is no empty-world
    /// special case, and an early world behaves precisely as it did before. That property is the
    /// reason to prefer this form over a fudge factor: it can only take light away from a crowd,
    /// never add any to anyone.
    /// </para>
    /// <para>
    /// <b>Non-photosynthetic tissue shades too.</b> Every part casts a shadow whether or not it can
    /// use the light, and light a structural part intercepts is simply lost. That is not a
    /// modelling convenience — it is what makes bulk cost something in a crowd, and it prices a
    /// canopy correctly: a creature that puts a large opaque body above a photosynthetic one is
    /// taking food from it.
    /// </para>
    /// </remarks>
    public sealed class LightField
    {
        private readonly List<float> _demand = new List<float>();
        private readonly List<float> _factor = new List<float>();

        // D061. Per-patch shading data, maintained only when PerPatchShading is on — see the
        // class remarks on why the pooled lists above are always kept regardless, and why that
        // makes the pre-D061 signatures safe rather than throwing the way NutrientField's do.
        private readonly List<float[]> _demandByPatch = new List<float[]>();
        private readonly List<float[]> _factorByPatch = new List<float[]>();
        private float[] _deepFactorByPatch;

        public LightModel Model { get; }

        /// <summary>Horizontal area of the world, m². The sun's aperture.</summary>
        public float WorldArea { get; }

        /// <summary>Thickness of one shading layer, m.</summary>
        /// <remarks>
        /// Creatures within one layer shade each other symmetrically; a creature only shades those
        /// strictly below its layer. Thinner layers resolve vertical structure more finely and
        /// cost one list slot each. It is a discretisation, and like every discretisation it is
        /// visible at the wrong scale: layers much thicker than a creature would let a creature
        /// shade one beside it, and layers far thinner than the population's vertical spread cost
        /// slots that hold nothing.
        /// </remarks>
        public float LayerMetres { get; }

        /// <summary>
        /// Horizontal cells per layer — D061. 1 is this field's whole history before D061.
        /// </summary>
        public int PatchCount { get; }

        /// <summary>
        /// Whether each patch shades only its own column — D061's endogenous inequality
        /// (<see cref="RunConfig.PerPatchShading"/>). Off is this field's whole history before
        /// D061: one shared canopy, every patch's demand pooled into the same per-layer figure.
        /// </summary>
        public bool PerPatchShading { get; }

        /// <summary>One patch's own horizontal area, m² — <c>WorldArea / PatchCount</c>.</summary>
        public float PatchArea => WorldArea / PatchCount;

        /// <summary>Total power the world received this step, W. The cap on all photosynthesis.</summary>
        public float IncidentWatts => Model.SurfaceIrradiance * WorldArea;

        public LightField(
            LightModel model, float worldArea, float layerMetres,
            int patchCount = 1, bool perPatchShading = false)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));

            if (!(worldArea > 0f) || float.IsInfinity(worldArea))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(worldArea), worldArea,
                    "A world with no area receives no light. An infinite one receives infinite " +
                    "light, which is the unbounded-population failure this class exists to fix.");
            }

            if (!(layerMetres > 0f) || float.IsInfinity(layerMetres))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(layerMetres), layerMetres, "Layers must have a thickness.");
            }

            if (patchCount < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(patchCount), patchCount, "A field needs at least one patch — D061's K >= 1.");
            }

            WorldArea = worldArea;
            LayerMetres = layerMetres;
            PatchCount = patchCount;
            PerPatchShading = perPatchShading;

            if (PerPatchShading)
            {
                _deepFactorByPatch = new float[PatchCount];
                for (int i = 0; i < PatchCount; i++) _deepFactorByPatch[i] = 1f;
            }
        }

        /// <summary>Which layer a world height falls in. The surface and above are layer 0.</summary>
        private int LayerOf(float heightY) =>
            heightY >= 0f ? 0 : (int)(-heightY / LayerMetres);

        private void ValidatePatch(int patch)
        {
            if (patch < 0 || patch >= PatchCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(patch), patch,
                    $"This field has {PatchCount} patch(es), indexed 0..{PatchCount - 1}.");
            }
        }

        /// <summary>Discards last step's demand. Call before <see cref="Contribute(float, float, int)"/>.</summary>
        public void Clear()
        {
            _demand.Clear();
            _factor.Clear();
            _demandByPatch.Clear();
            _factorByPatch.Clear();
        }

        /// <summary>Registers a creature's shadow at its depth and patch.</summary>
        /// <param name="heightY">World height, metres. Y is up, so depths are negative.</param>
        /// <param name="litArea">The creature's <see cref="Phenotype.TotalLitArea"/>, m².</param>
        /// <param name="patch">
        /// Which patch the creature occupies — D061. Always contributes to the pooled, per-layer
        /// demand (used whenever <see cref="PerPatchShading"/> is off, and by every pre-D061
        /// caller); additionally contributes to this patch's own demand when
        /// <see cref="PerPatchShading"/> is on.
        /// </param>
        public void Contribute(float heightY, float litArea, int patch)
        {
            if (!(litArea > 0f)) return;

            int layer = LayerOf(heightY);
            while (_demand.Count <= layer) _demand.Add(0f);
            _demand[layer] += litArea;

            if (!PerPatchShading) return;

            ValidatePatch(patch);
            while (_demandByPatch.Count <= layer) _demandByPatch.Add(new float[PatchCount]);
            _demandByPatch[layer][patch] += litArea;
        }

        /// <summary>
        /// Pre-D061 signature — attributes the shadow to patch 0. Always valid, unlike
        /// <see cref="NutrientField"/>'s patch-less overloads: with <see cref="PerPatchShading"/>
        /// off, patch is not consulted at all regardless of <see cref="PatchCount"/>, and with it
        /// on, "patch 0" is a real and legitimate answer rather than an arbitrary one — a caller
        /// that does not care which patch its shadow lands in is exactly the pre-D061 caller this
        /// overload exists for.
        /// </summary>
        public void Contribute(float heightY, float litArea) => Contribute(heightY, litArea, 0);

        /// <summary>
        /// Works out what fraction of the light each layer's occupants actually get, top down.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>What is stored per layer is a multiplier on <see cref="LightModel"/>, not an
        /// irradiance, and that is the difference between a shading model and a free-energy
        /// source.</b> Storing an irradiance means everyone in a layer receives the light entering
        /// it, so a creature drifting from empty water into an occupied layer above it gets
        /// <i>more</i> light than Beer–Lambert allows — an increase, caused by nothing but the
        /// discretisation, in exactly the direction evolution looks for (§11.2). A multiplier
        /// cannot do that: it is in (0, 1], the depth term stays exact and continuous, and
        /// occupancy can only ever take light away.
        /// </para>
        /// <para>
        /// Two passes over the population per step rather than one — every creature's shadow has
        /// to be known before anyone's income can be. The pass is O(creatures) and touches no
        /// geometry, which is what keeps a whole population steppable in microseconds (§5A.9).
        /// </para>
        /// </remarks>
        public void Solve()
        {
            SolvePooled();
            if (PerPatchShading) SolvePerPatch();
        }

        /// <summary>
        /// The field's whole algorithm before D061, untouched: one shared canopy, everybody's
        /// demand pooled per layer regardless of patch. Always runs, so
        /// <see cref="IrradianceAt(float)"/> and <see cref="ShadingAt"/> — the pre-D061 readings —
        /// stay correct and available whatever <see cref="PerPatchShading"/> is.
        /// </summary>
        private void SolvePooled()
        {
            _factor.Clear();

            float throughWater = (float)Math.Exp(-LayerMetres / Model.AttenuationDepth);

            float watts = IncidentWatts;          // actually entering this layer
            float unshaded = IncidentWatts;       // what would, with no biomass anywhere

            for (int layer = 0; layer < _demand.Count; layer++)
            {
                // Everything absorbed above, as a fraction. Water cancels out of the ratio, which
                // is the point: this term carries shading alone.
                float area = _demand[layer];

                // A canopy dense enough to take every last watt leaves nothing below it, and the
                // ratios that describe sharing become 0/0. Total darkness is a real state of this
                // model, not an edge case to be nudged away from: a mat can close over the world.
                if (!(watts > 0f))
                {
                    _factor.Add(0f);
                    continue;
                }

                float fromAbove = watts / unshaded;

                if (area <= 0f)
                {
                    _factor.Add(fromAbove);
                }
                else
                {
                    double interceptedFraction = InterceptedFraction(area / WorldArea);

                    // Competition within the layer. Sharing the intercepted power in proportion to
                    // lit area makes this one number for everyone here, so income stays a per-part
                    // quantity and Metabolism never has to know that competition happened. It is
                    // at most 1, approaching it as the layer empties.
                    float share = (float)(interceptedFraction * WorldArea / area);

                    _factor.Add(fromAbove * share);
                    watts -= (float)(watts * interceptedFraction);
                }

                watts *= throughWater;
                unshaded *= throughWater;
            }

            _deepFactor = unshaded > 0f ? watts / unshaded : 0f;
        }

        /// <summary>
        /// D061's per-patch pass: the same algorithm as <see cref="SolvePooled"/>, run once per
        /// patch against that patch's own demand and its own share of the aperture
        /// (<see cref="PatchArea"/> in place of <see cref="WorldArea"/>) — a crowded patch darkens
        /// only itself. Only called when <see cref="PerPatchShading"/> is on.
        /// </summary>
        private void SolvePerPatch()
        {
            _factorByPatch.Clear();
            _deepFactorByPatch = new float[PatchCount];

            float patchArea = PatchArea;
            float throughWater = (float)Math.Exp(-LayerMetres / Model.AttenuationDepth);
            float incidentPerPatch = Model.SurfaceIrradiance * patchArea;

            for (int patch = 0; patch < PatchCount; patch++)
            {
                float watts = incidentPerPatch;
                float unshaded = incidentPerPatch;

                for (int layer = 0; layer < _demandByPatch.Count; layer++)
                {
                    while (_factorByPatch.Count <= layer) _factorByPatch.Add(new float[PatchCount]);

                    float area = _demandByPatch[layer][patch];

                    if (!(watts > 0f))
                    {
                        _factorByPatch[layer][patch] = 0f;
                    }
                    else
                    {
                        float fromAbove = watts / unshaded;

                        if (area <= 0f)
                        {
                            _factorByPatch[layer][patch] = fromAbove;
                        }
                        else
                        {
                            double interceptedFraction = InterceptedFraction(area / patchArea);
                            float share = (float)(interceptedFraction * patchArea / area);

                            _factorByPatch[layer][patch] = fromAbove * share;
                            watts -= (float)(watts * interceptedFraction);
                        }
                    }

                    watts *= throughWater;
                    unshaded *= throughWater;
                }

                _deepFactorByPatch[patch] = unshaded > 0f ? watts / unshaded : 0f;
            }
        }

        /// <summary>
        /// 1 − e<sup>−x</sup>, computed so that a small <paramref name="x"/> gives a small answer
        /// rather than a wrong one.
        /// </summary>
        /// <remarks>
        /// <b>The naive form loses the answer entirely for a sparse world.</b> In <c>float</c>,
        /// <c>1 − exp(−2.5e−7)</c> evaluates to 2.384e−7 against a true 2.5e−7 — a 4.6% error,
        /// because the subtraction cancels every significant bit and leaves only the spacing of
        /// floats near 1. That error lands as a 4.6% light tax on a world containing one creature
        /// with a shadow of a square centimetre, and it does not go away as the world empties;
        /// it gets worse. Doubles push the cancellation far below anything that matters, and the
        /// series takes over where even they would lose it — <c>1 − e^−x → x(1 − x/2)</c> to
        /// better than machine precision for x this small.
        /// </remarks>
        private static double InterceptedFraction(double x)
        {
            if (x < 1e-5) return x * (1.0 - 0.5 * x);
            return 1.0 - Math.Exp(-x);
        }

        private float _deepFactor = 1f;

        /// <summary>
        /// The world's current point in its day, as a multiplier on surface irradiance — §5A.4.
        /// </summary>
        /// <remarks>
        /// State, and it lives here rather than on <see cref="LightModel"/> because that object is
        /// configuration: everything on it is a <c>[Tunable]</c> and §7 hashes it, and where the
        /// world has got to is not part of how it was set up. This class already holds the other
        /// thing about light that changes every step, which is who is shading whom.
        /// </remarks>
        public float DayFactor { get; private set; } = 1f;

        /// <summary>Moves the sun to a point in time. Idempotent for a given second.</summary>
        public void Advance(double elapsedSeconds) =>
            DayFactor = Model.DayFactorAt(elapsedSeconds);

        /// <summary>Effective irradiance at a world height after shading, W/m².</summary>
        /// <remarks>
        /// Valid only after <see cref="Solve"/>. Before any creature has contributed this returns
        /// the unshaded <see cref="LightModel"/> value exactly — the model is continuous through
        /// an empty world rather than special-cased at it.
        /// </remarks>
        public float IrradianceAt(float heightY)
        {
            int layer = LayerOf(heightY);
            float factor = layer < _factor.Count ? _factor[layer] : _deepFactor;

            return Model.IrradianceAt(heightY) * factor * DayFactor;
        }

        /// <summary>
        /// Effective irradiance at a world height and patch after shading, W/m² — D061.
        /// </summary>
        /// <remarks>
        /// Falls back to the pooled, patch-less <see cref="IrradianceAt(float)"/> when
        /// <see cref="PerPatchShading"/> is off — every organism in a K&gt;1 world reads the same
        /// shared-canopy irradiance at a given depth unless per-patch shading was explicitly
        /// asked for, exactly as D061 specifies ("light irradiance itself stays global").
        /// </remarks>
        public float IrradianceAt(float heightY, int patch)
        {
            if (!PerPatchShading) return IrradianceAt(heightY);

            ValidatePatch(patch);
            int layer = LayerOf(heightY);
            float factor = layer < _factorByPatch.Count ? _factorByPatch[layer][patch] : _deepFactorByPatch[patch];

            return Model.IrradianceAt(heightY) * factor * DayFactor;
        }

        /// <summary>What fraction of the unshaded light reaches a depth, in (0, 1].</summary>
        /// <remarks>
        /// Reported separately because it is the honest measure of how full a world is, and it is
        /// the one number that says whether the population is actually competing: 1 means the
        /// world is empty of anything casting a shadow there, however many creatures it holds.
        /// </remarks>
        public float ShadingAt(float heightY)
        {
            int layer = LayerOf(heightY);
            return layer < _factor.Count ? _factor[layer] : _deepFactor;
        }

        /// <summary>Layers currently holding biomass. For reporting.</summary>
        public int OccupiedLayers => _demand.Count;

        public override string ToString() =>
            $"{Model}, {WorldArea:0} m² aperture ({IncidentWatts:0} W), {OccupiedLayers} layers occupied";
    }
}
