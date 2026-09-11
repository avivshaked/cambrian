using System;

namespace Evosim.Core
{
    /// <summary>
    /// Water that moves — DESIGN.md §5A.4, D036. Off by default.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Still water is why every run so far converged on blobs.</b> With nothing moving, drifting
    /// costs exactly zero and doing nothing is not merely cheap but <i>optimal</i>, so a body that
    /// sits in the light and pays its bills beats every body that tries anything. Locomotion had no
    /// gradient to climb: swimming four metres toward better light pays nothing for the first four
    /// metres, and a creature was measured moving six millimetres in its entire life
    /// (logbook/0021).
    /// </para>
    /// <para>
    /// <b>What moving water buys is station-keeping</b>, which is a task with continuous returns
    /// from arbitrarily close to zero. A creature that swims slightly holds its depth slightly
    /// better than one that does not swim at all, and slightly better depth is slightly more light,
    /// immediately, with no threshold to cross first. That is the shape of thing evolution can
    /// start on.
    /// </para>
    /// <para>
    /// <b>It is an internal wave in depth, and deliberately not the curl-noise field §5A.4
    /// specifies.</b> The reason is a short proof rather than a preference. §6.3 tiles creatures
    /// across x and z for physics isolation and treats horizontal position as ecologically inert —
    /// a tile index is a recycled bookkeeping slot, so anything that reads it makes an artefact
    /// ecologically meaningful. That forces the field to be a function of depth and time alone. But
    /// for such a field <c>div v = ∂w/∂y</c>, so divergence-free would require <c>w</c> to be
    /// constant in depth — a uniform drift that moves every creature identically and therefore
    /// shears nothing past anything. Depth-varying vertical flow and divergence-free are
    /// incompatible here; the compensating horizontal circulation is real and simply lies outside a
    /// modelled column one tile wide.
    /// </para>
    /// <para>
    /// <b>Two standing waves at incommensurate periods, and the first attempt was a conveyor
    /// belt.</b> A single travelling wave has zero time-average velocity at every fixed depth, and
    /// that fact is worth nothing: a particle riding one is dragged along with the phase. The first
    /// embodied run carried the whole population six metres <i>above the surface</i> in 2500 s and
    /// kept going (logbook/0022). The test guarding it asserted the mean velocity at a fixed point
    /// — the Eulerian mean — when the quantity that matters is the mean displacement of something
    /// carried by the flow, which is not the same number and was not zero.
    /// </para>
    /// <para>
    /// Each term here is a standing wave <c>sin(ky)·sin(ωt)</c>, which is antisymmetric about the
    /// half-period: the second half of a cycle undoes the first exactly, so a particle in one term
    /// alone returns precisely to where it started. That is zero drift by symmetry rather than by
    /// cancellation, and it does not depend on the integrator.
    /// </para>
    /// <para>
    /// <b>One such term would also mix nothing</b>, since every particle returns home every cycle
    /// and a creature born deep stays deep — which is the determinism this exists to break. Two
    /// terms with incommensurate cell heights and periods never repeat, so trajectories separate
    /// and neighbouring depths lose track of each other. That is chaotic advection, it is the
    /// standard way a smooth periodic flow mixes at all, and it gives dispersion without a mean.
    /// </para>
    /// </remarks>
    public sealed class CurrentField
    {
        /// <summary>Peak water speed, m/s. 0 is still water and the world every earlier run measured.</summary>
        /// <remarks>
        /// <para>
        /// <b>Read this against what a creature can do, because that ratio is the whole design.</b>
        /// Too slow and it is a rounding error on a world that still rewards sitting still; too
        /// fast and every creature is swept regardless of what it does, which replaces a world with
        /// no signal by a world that is all noise — the same failure in the opposite direction.
        /// </para>
        /// <para>
        /// For scale: the best founder in a survey of two hundred swam at 0.127 m/s and 11%
        /// exceeded 0.01 m/s (logbook/0018). A current an order of magnitude above the fastest
        /// thing alive cannot be held against by anything, ever.
        /// </para>
        /// <para>⚠ Unmeasured (§5A.10). Default 0: the water is still until a run asks otherwise.</para>
        /// </remarks>
        [Tunable("current", Unit = "m/s")]
        public float Speed
        {
            get => _speed;
            set => _speed = value >= 0f && !float.IsInfinity(value)
                ? value
                : throw new ArgumentOutOfRangeException(
                    nameof(Speed), value, "Must be finite and not negative.");
        }

        private float _speed;

        /// <summary>Vertical distance between one up-limb and the next, metres.</summary>
        /// <remarks>
        /// The size of an overturning cell. It decides how far apart two creatures must be
        /// vertically before the water treats them differently, so it is what turns a current from
        /// something that moves everybody into something that separates them. Larger than a
        /// creature by a lot and smaller than the world by a lot, or it is one of those two things.
        /// ⚠ Unmeasured (§5A.10).
        /// </remarks>
        [Tunable("current", Unit = "m")]
        public float CellMetres
        {
            get => _cellMetres;
            set => _cellMetres = value > 0f && !float.IsInfinity(value)
                ? value
                : throw new ArgumentOutOfRangeException(
                    nameof(CellMetres), value,
                    "A cell of zero or infinite height is not a circulation.");
        }

        private float _cellMetres = 25f;

        /// <summary>Seconds for the pattern to travel one cell — how fast the wave moves.</summary>
        /// <remarks>
        /// Against a creature's lifetime rather than against the clock. Much shorter than a life and
        /// the current averages out to nothing over a career, so holding station buys nothing that
        /// waiting would not; much longer and it is a constant, and a constant current is a
        /// one-directional tow rather than a circulation. ⚠ Unmeasured (§5A.10).
        /// </remarks>
        [Tunable("current", Unit = "s")]
        public float PeriodSeconds
        {
            get => _periodSeconds;
            set => _periodSeconds = value > 0f && !float.IsInfinity(value)
                ? value
                : throw new ArgumentOutOfRangeException(
                    nameof(PeriodSeconds), value, "A period of zero or infinity is not a cycle.");
        }

        private float _periodSeconds = 300f;

        /// <summary>
        /// How fast the horizontal flow runs, as a multiple of the vertical.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Horizontal motion is ecologically inert (§6.3), so this buys nothing directly: a
        /// creature swept sideways is exactly as well fed as one that is not. It is here because a
        /// purely vertical field is a strange thing to swim in — real water shears — and because a
        /// current a creature can feel but not profit from is what makes the <c>Flow</c> sensor
        /// worth having for orientation rather than for gain.
        /// </para>
        /// <para>
        /// It is also the honest bookkeeping of the divergence argument above: the horizontal flow
        /// is what the missing circulation would be doing. Set to 0 for a purely vertical field.
        /// </para>
        /// </remarks>
        [Tunable("current")]
        public float HorizontalRatio { get; set; } = 1f;

        /// <summary>
        /// Whether the field is organised into convection rolls over D061's patches — D066.
        /// Default false, which is every run before D066 exactly.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Off, this class is the depth-only oscillation described above</b>: one column, the
        /// same water everywhere at a given depth, no horizontal structure that anything can be on
        /// one side of. On, each pair of adjacent patches is one overturning cell — patch <i>k</i>
        /// rising while <i>k+1</i> sinks — joined by horizontal flow along the surface and along
        /// the floor. The vertical profile is <c>sin(π·depth/H)</c> with <c>H</c> =
        /// <see cref="CellMetres"/>, so the flow is exactly zero at the waterline by construction
        /// (logbook/0022's flying population cannot recur through this term) and exactly zero at
        /// the bottom of the cell.
        /// </para>
        /// <para>
        /// <b>It needs at least two patches to mean anything.</b> With
        /// <c>HorizontalPatches</c> = 1 there is no <i>k+1</i> to sink while <i>k</i> rises, so
        /// this degenerates back to the old field and the code takes the old path — a roll needs a
        /// neighbour, which is a statement about the world and not about the implementation.
        /// </para>
        /// </remarks>
        [Tunable("current")]
        public bool Rolls { get; set; }

        /// <summary>
        /// Seconds between reversals of the roll pattern, s. 0 (the default) never reverses.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>This is the difference between a circulation and a stirrer.</b> A steady roll is a
        /// set of closed streamlines: a parcel goes round and round its own cell and never leaves
        /// it, so the world is stirred within each roll and sealed between them — D061's sealed
        /// pools again, drawn slightly differently. Flipping the parity every so often means the
        /// parcel that rode up in patch <i>k</i> next rides down in it, streamlines from one
        /// period do not match the next, and material lines stretch and fold. That is chaotic
        /// advection — the standard way a smooth, prescribed, laminar flow mixes at all — and it
        /// costs one <c>floor</c> and a sign.
        /// </para>
        /// <para>
        /// ⚠ Unmeasured (§5A.10). Against the roll's own turnover time: much shorter and the flow
        /// reverses before a parcel has gone anywhere, much longer and it is a steady roll with an
        /// occasional surprise.
        /// </para>
        /// </remarks>
        [Tunable("current", Unit = "s")]
        public float RollBlinkSeconds
        {
            get => _rollBlinkSeconds;
            set => _rollBlinkSeconds = value >= 0f && !float.IsInfinity(value)
                ? value
                : throw new ArgumentOutOfRangeException(
                    nameof(RollBlinkSeconds), value, "Must be finite and not negative.");
        }

        private float _rollBlinkSeconds;

        /// <summary>
        /// Whether the scalar fields — detritus and matter — are advected by this flow as well as
        /// the bodies. Default false, which is every run before D066.
        /// </summary>
        /// <remarks>
        /// <b>A velocity field is exactly the thing that carries a scalar field</b>, and before
        /// D066 this one carried only bodies while detritus was left to a separate diffusion
        /// (<see cref="NutrientField.Mix"/>) on the argument that a corpse is not a physical
        /// object. Half right: the flow does not know the difference. Kept as its own knob because
        /// the two are separable questions — a run may want water that moves creatures and a
        /// larder that only diffuses, which is what every run up to round 11 measured.
        /// </remarks>
        [Tunable("current")]
        public bool AdvectFields { get; set; }

        /// <summary>
        /// Which field this is: D037's standing waves and D066's rolls, or a three-dimensional transport
        /// field over the whole box. <see cref="CurrentMode.Rolls"/> by default, so every recorded
        /// config replays.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The rolls were built for a world in which horizontal position did not exist.</b>
        /// §6.3 tiled creatures across x and z and treated a tile index as a recycled bookkeeping
        /// slot, so the field was made a function of depth, time and patch, and the class remark
        /// above proves that such a field cannot be divergence-free and depth-varying at once.
        /// D077 gave the world one shared box with real coordinates and D086 put the water on a
        /// grid of cells, which removed the premise. On 2026-09-10 the owner opened round 33 seed
        /// 3 in the theatre and saw the whole world as two vertical ribbons a metre wide in a box
        /// twenty metres long (logbook/0083): a newborn was set down touching its parent, the roll
        /// returned every body to the depth it found it at and moved nothing sideways, and no body
        /// ever swam, so each clade drained the same handful of cells for thirty thousand seconds.
        /// <see cref="CurrentMode.Transport"/> is the water that answers that.
        /// </para>
        /// <para>
        /// <b>The patch-level readers are unchanged in either mode.</b>
        /// <see cref="CrossingDirection"/> and <see cref="HorizontalCrossingFraction"/> move stock
        /// between the cell and vertex fields' patches, which is a coarse-graining that a
        /// positional field does not need and a grid does not use; in
        /// <see cref="CurrentMode.Transport"/> they read the four-argument overload exactly as
        /// they always did, which now samples at the patch's centre. The field that carries stock
        /// with the local water is <see cref="GridField.Advect"/>, and it takes the positional
        /// sampler.
        /// </para>
        /// </remarks>
        [Tunable("current")]
        public CurrentMode Mode { get; set; } = CurrentMode.Rolls;

        /// <summary>
        /// Speed of the upwelling plume, m/s — D067. 0 (the default) is no vent, which is every
        /// run before D067 bit for bit.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The vent is a second prescribed flow superposed on the roll, and its job is the one
        /// the roll cannot do</b>: a roll of <see cref="CellMetres"/> that stops above the floor is
        /// a trapdoor — everything that sinks past it stays sunk (logbook/0048). This lifts the deep
        /// larder into the light in one patch and returns the water uniformly through all the
        /// others, joined by a horizontal leg along the surface and another along the floor, so the
        /// loop closes at both ends.
        /// </para>
        /// <para>
        /// <b>It is divergence-free on the same staggered grid the roll uses</b> — <c>w</c> at layer
        /// interfaces per patch, <c>u</c> at patch faces per layer — and that is what makes it
        /// arithmetic rather than a source: the plume carries <c>Q = s·A_patch</c> up, each of the
        /// <c>K−1</c> return patches sinks exactly <c>Q/(K−1)</c>, and the legs carry precisely
        /// those amounts sideways. A uniform field stirred by it stays uniform.
        /// </para>
        /// <para>
        /// ⚠ Unmeasured (§5A.10). Read it against <see cref="Speed"/> and against what a creature
        /// can swim, the same way: a plume much faster than the fastest thing alive is a world with
        /// no signal in it, and one much slower than the sinking rate loses the race to the floor.
        /// </para>
        /// </remarks>
        [Tunable("current", Unit = "m/s")]
        public float VentSpeed
        {
            get => _ventSpeed;
            set => _ventSpeed = value >= 0f && !float.IsInfinity(value) && !float.IsNaN(value)
                ? value
                : throw new ArgumentOutOfRangeException(
                    nameof(VentSpeed), value, "Must be finite and not negative.");
        }

        private float _ventSpeed;

        /// <summary>Which of D061's patches the plume rises in. Default 0.</summary>
        /// <remarks>
        /// <para>
        /// <b>One patch, not a fraction of every patch.</b> The whole point is that the world is
        /// not the same everywhere any more: one place where the deep comes back up and the light
        /// and the larder meet, and <c>K−1</c> places that pay for it by sinking. A vent spread
        /// evenly over the ring would be a uniform overturning, which moves everything identically
        /// and therefore separates nothing — the same argument that made the depth-only field a
        /// conveyor.
        /// </para>
        /// <para>
        /// <b>Validated against the patch count rather than wrapped.</b> <c>patch % K</c> would
        /// silently relocate the vent whenever someone changed <c>K</c>, and two runs whose configs
        /// name different vents would be the same world with no way to tell from the file. The
        /// setter refuses a negative index and <see cref="World"/> refuses one at or past
        /// <c>HorizontalPatches</c> when the vent is on.
        /// </para>
        /// </remarks>
        [Tunable("current")]
        public int VentPatch
        {
            get => _ventPatch;
            set => _ventPatch = value >= 0
                ? value
                : throw new ArgumentOutOfRangeException(
                    nameof(VentPatch), value, "A patch index is not negative.");
        }

        private int _ventPatch;

        /// <summary>Depth the plume draws from, m — the floor. Default 60.</summary>
        /// <remarks>
        /// <b>Stated here and checked against the world, because the field does not know the
        /// world's depth.</b> A <see cref="CurrentField"/> is handed heights and nothing else, so
        /// it cannot discover where the bottom is; and a plume that stops short of the floor is
        /// exactly the trapdoor D067 exists to close, arriving quietly through a config that looks
        /// fine. So the config states it and <see cref="World"/> refuses to construct if it
        /// disagrees with <see cref="RunConfig.WorldDepthMetres"/>. The default matches that
        /// property's own default, so a run that sets neither is consistent by construction.
        /// </remarks>
        [Tunable("current", Unit = "m")]
        public float VentDepthMetres
        {
            get => _ventDepthMetres;
            set => _ventDepthMetres = value >= 0f && !float.IsInfinity(value) && !float.IsNaN(value)
                ? value
                : throw new ArgumentOutOfRangeException(
                    nameof(VentDepthMetres), value, "Must be finite and not negative.");
        }

        private float _ventDepthMetres = 60f;

        /// <summary>Thickness of the surface and floor legs, m. Default 1.</summary>
        /// <remarks>
        /// <para>
        /// <b>It is a layer thickness, not a length scale.</b> The legs are where the plume hands
        /// its water sideways and where the return water comes back, and on a staggered grid that
        /// hand-off has to happen inside whole cells or the books do not close: the flux across a
        /// face is a fraction of a cell's volume, and a leg that ends halfway through a layer makes
        /// that fraction meaningless. <see cref="World"/> therefore refuses to construct unless
        /// this is a whole number of <see cref="RunConfig.LightLayerMetres"/>.
        /// </para>
        /// <para>
        /// <b>Exact discrete continuity wants one layer</b>, which is the default and what the
        /// tests pin. The prescribed vertical velocity is the full <c>±s</c> everywhere between the
        /// surface and the floor, so a leg two layers thick hands the same total sideways while the
        /// vertical flux through its inner interface is undiminished, and a uniform field acquires
        /// a small standing gradient inside the leg. Nothing is created or destroyed — every move
        /// is still a flux between two named cells — but "uniform stays uniform" is a statement
        /// about a one-layer leg.
        /// </para>
        /// </remarks>
        [Tunable("current", Unit = "m")]
        public float VentLegMetres
        {
            get => _ventLegMetres;
            set => _ventLegMetres = value >= 0f && !float.IsInfinity(value) && !float.IsNaN(value)
                ? value
                : throw new ArgumentOutOfRangeException(
                    nameof(VentLegMetres), value, "Must be finite and not negative.");
        }

        private float _ventLegMetres = 1f;

        /// <summary>
        /// Width of one patch, m — geometry, not a knob. 0 (the default) until a world says.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Not <see cref="TunableAttribute"/>, deliberately.</b> It is
        /// <c>sqrt(WorldAreaSquareMetres / HorizontalPatches)</c> — both of which are already
        /// hashed — so making it a tunable would put a derived quantity in the hash and let a
        /// config name a width its own area and patch count contradict.
        /// <see cref="World"/>'s constructor sets it from <see cref="NutrientField.PatchWidthMetres"/>
        /// so there is one geometry rather than two. It is the one piece of state this otherwise
        /// stateless class holds, so a <see cref="RunConfig"/> shared between two worlds of
        /// different geometry leaves it describing whichever was built last — the same hazard a
        /// shared config already carries everywhere else, and the reason a world builds its own.
        /// </para>
        /// <para>
        /// <b>It buys exactly one thing: the vent's horizontal drag term.</b> The legs are defined
        /// by their volume flux, which is width-free, but the <i>velocity</i> a creature feels in
        /// one is <c>F_j / (L · A_patch / W) = c_j·s·W/L</c> and that needs a width. While this is
        /// 0 the drag term is 0 and nothing else changes — the transport, the plume, the return and
        /// the legs are all unaffected, because none of them is computed from a velocity times a
        /// width. Horizontal motion is ecologically inert (§6.3) in any case, so a field left at 0
        /// is a vent whose water pushes nothing sideways and carries everything sideways exactly as
        /// it should.
        /// </para>
        /// <para>
        /// <b>Set through a method rather than a property setter</b> so the config coverage test
        /// stays true as written: every settable public property on a <c>[TunableGroup]</c> must be
        /// declared a tunable, and this one must not be.
        /// </para>
        /// </remarks>
        public float PatchWidthMetres => _patchWidthMetres;

        private float _patchWidthMetres;

        /// <summary>Tells the field how wide a patch is — see <see cref="PatchWidthMetres"/>.</summary>
        public void SetPatchWidth(float metres)
        {
            if (!(metres >= 0f) || float.IsInfinity(metres))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(metres), metres, "A patch width is finite and not negative.");
            }

            _patchWidthMetres = metres;
        }

        /// <summary>
        /// Tells the field the box it lives in and the run's seed, which is everything
        /// <see cref="CurrentMode.Transport"/> needs and more than <see cref="CurrentMode.Rolls"/>
        /// reads.
        /// </summary>
        /// <param name="patchWidthMetres">One patch's side, m, on both horizontal axes.</param>
        /// <param name="patchCount">D061's patches, K, laid out K/A along x by A across z.</param>
        /// <param name="depthMetres">The box's depth, m. The floor sits at −this.</param>
        /// <param name="seed">The run's seed, which the transport field's phases are drawn from.</param>
        /// <param name="patchesAcross">
        /// <see cref="RunConfig.PatchesAcross"/>, A. 1 is the row of patches this field has always
        /// been built over, at which every number below is the one it always held.
        /// </param>
        /// <remarks>
        /// <para>
        /// <b>State, not tunables, for <see cref="PatchWidthMetres"/>'s reason.</b> Every number
        /// here is already in the hash somewhere else: the width is
        /// <c>sqrt(WorldAreaSquareMetres / HorizontalPatches)</c>, the count and the depth are
        /// their own tunables, and the seed is recorded in the run manifest beside the config hash.
        /// Declaring them tunable would let a config name a geometry its own area contradicts, and
        /// would put the seed in the hash, which would make one config two configs.
        /// </para>
        /// <para>
        /// <b>The seed is here because the transport field has to differ between seeds.</b> A
        /// fixed set of phases would put the same eddy in the same place in every run of a round,
        /// so five seeds would be five draws of the genome and one draw of the water. That is not
        /// a replicate.
        /// </para>
        /// </remarks>
        /// <param name="shape">
        /// <see cref="RunConfig.WorldShape"/>. <see cref="WorldShape.Box"/> is the container every
        /// run on file was measured in, at which every number below is the one it always held.
        /// </param>
        /// <param name="tankRadiusMetres">
        /// <c>sqrt(area/π)</c> when the shape is a tank, and unread otherwise — the world derives
        /// it (<see cref="TankGeometry.RadiusFor"/>) rather than the field, so one geometry
        /// reaches the fields, the placer and the water.
        /// </param>
        public void SetBox(
            float patchWidthMetres, int patchCount, float depthMetres, ulong seed,
            int patchesAcross = 1, WorldShape shape = WorldShape.Box, float tankRadiusMetres = 0f)
        {
            SetPatchWidth(patchWidthMetres);

            if (shape == WorldShape.Tank && (!(tankRadiusMetres > 0f) || float.IsInfinity(tankRadiusMetres)))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tankRadiusMetres), tankRadiusMetres,
                    "A tank's water is a disc, so its radius is positive and finite. " +
                    "logbook/specs/tank-spec.md.");
            }

            if (patchCount < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(patchCount), patchCount, "A box is at least one patch long.");
            }

            if (patchesAcross < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(patchesAcross), patchesAcross, "A box is at least one patch wide.");
            }

            if (patchCount % patchesAcross != 0)
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"{patchesAcross} patches across do not divide {patchCount} patches, so ") +
                    "the layout leaves a part row. The water is a field over a box, and a part " +
                    "row is not a box.",
                    nameof(patchesAcross));
            }

            if (!(depthMetres > 0f) || float.IsInfinity(depthMetres))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(depthMetres), depthMetres, "A depth is positive and finite.");
            }

            _patchCount = patchCount;
            _patchesAcross = patchesAcross;
            _depthMetres = depthMetres;
            _seed = seed;
            _shape = shape;
            _tankRadiusMetres = shape == WorldShape.Tank ? tankRadiusMetres : 0f;

            // Built lazily on the first sample, so a Rolls world never pays for it and a config
            // handed to two worlds of different geometry rebuilds rather than describing the first.
            _transportAmplitude = null;
            _gyreBuilt = false;
        }

        private int _patchCount;
        private int _patchesAcross = 1;
        private float _depthMetres;
        private ulong _seed;
        private WorldShape _shape = WorldShape.Box;
        private float _tankRadiusMetres;

        /// <summary>
        /// The container this water is in — <see cref="RunConfig.WorldShape"/>.
        /// <see cref="WorldShape.Box"/> until a world says otherwise.
        /// </summary>
        /// <remarks>
        /// <b>The gyre is selected by the shape, not by the mode</b> (logbook/specs/tank-spec.md).
        /// D037's standing waves and D066's rolls are a field over a row of patches and the
        /// transport field is periodic on two rings; neither is a thing a walled cylinder can
        /// hold. So in a tank <see cref="Speed"/> is the RMS of the gyre, whatever
        /// <see cref="Mode"/> says, and <see cref="World"/> refuses a tank under
        /// <see cref="CurrentMode.Rolls"/> at a nonzero speed rather than letting a config name a
        /// field the water is not.
        /// </remarks>
        public WorldShape Shape => _shape;

        /// <summary>The tank's radius, m — 0 in a box. <see cref="TankGeometry"/>.</summary>
        public float TankRadiusMetres => _tankRadiusMetres;

        /// <summary>
        /// How many patches lie across z — <see cref="RunConfig.PatchesAcross"/>, A. 1 until a
        /// world says otherwise, which is the row of patches every run on file was measured in.
        /// </summary>
        public int PatchesAcross => _patchesAcross;

        /// <summary>Patches along x, <c>K / A</c>. 0 until a world says.</summary>
        public int PatchesAlong => _patchesAcross < 1 ? 0 : _patchCount / _patchesAcross;

        /// <summary>
        /// The box's length along x, m: <c>W · K / A</c>, and <c>2R</c> in a tank. 0 until a
        /// world says.
        /// </summary>
        public float LengthMetres =>
            _shape == WorldShape.Tank ? 2f * _tankRadiusMetres : _patchWidthMetres * PatchesAlong;

        /// <summary>
        /// The box's extent along z, m: <c>W · A</c>, and <c>2R</c> in a tank. 0 until a world
        /// says.
        /// </summary>
        public float WidthMetres =>
            _shape == WorldShape.Tank ? 2f * _tankRadiusMetres : _patchWidthMetres * _patchesAcross;

        /// <summary>The box's depth, m. 0 until a world says.</summary>
        public float DepthMetres => _depthMetres;

        /// <summary>
        /// Whether the vent is doing anything at this patch count — D067.
        /// </summary>
        /// <remarks>
        /// <b>Two patches at least, for the same reason a roll needs two.</b> The return flow is
        /// shared over the <c>K−1</c> patches that are not the vent, and with <c>K</c> = 1 there
        /// are none of them: a plume with nowhere to return through is a source. So at one patch
        /// the vent is off and every path takes the pre-D067 branch, which is a statement about the
        /// world and not about the implementation.
        /// </remarks>
        public bool VentActive(int patchCount) => _ventSpeed > 0f && patchCount >= 2;

        /// <summary>Water velocity at a depth and a time, m/s. Zero when <see cref="Speed"/> is 0.</summary>
        /// <remarks>
        /// A pure function of its arguments, with no state, so two creatures at the same depth in
        /// the same step feel the same water however they are ordered — the same property §4.3
        /// buys with synchronous neuron update, for the same reason.
        /// </remarks>
        /// <summary>
        /// Ratio between the two waves' cell heights and periods.
        /// </summary>
        /// <remarks>
        /// Irrational on purpose — the reciprocal of the golden ratio, which is the number worst
        /// approximated by any fraction. A rational ratio makes the two terms share a common period,
        /// the whole field repeats exactly, and every particle returns home on that longer cycle:
        /// the mixing would quietly switch itself off at a timescale nobody chose. This is a
        /// property of the field rather than a knob, so it is not tunable.
        /// </remarks>
        private const double Incommensurate = 0.6180339887498949;

        public Float3 VelocityAt(float heightY, double seconds)
        {
            if (_speed <= 0f) return Float3.Zero;

            double y = 2.0 * Math.PI * heightY / _cellMetres;
            double t = 2.0 * Math.PI * seconds / _periodSeconds;

            // Each term is sin(ky)*sin(wt): antisymmetric about the half-period, so it returns a
            // particle exactly to where it found it. Their sum never repeats, so together they
            // disperse.
            double first = Math.Sin(y) * Math.Sin(t);
            double second = Math.Sin(y / Incommensurate + 1.0) * Math.Sin(t * Incommensurate);

            float vertical = _speed * (float)(0.5 * (first + second));

            // Horizontal flow a quarter turn out of phase with the vertical, which is what makes a
            // streamline a loop rather than a line. Nothing reads horizontal position (§6.3), so
            // this changes what the water feels like and not where anything ends up.
            double firstH = Math.Cos(y) * Math.Sin(t);
            double secondH = Math.Cos(y / Incommensurate + 1.0) * Math.Sin(t * Incommensurate);

            float horizontal = _speed * HorizontalRatio * (float)(0.5 * (firstH + secondH));

            return new Float3(horizontal, vertical, -horizontal);
        }

        /// <summary>
        /// Water velocity at a depth, a time and a patch, m/s — the roll field of D066.
        /// </summary>
        /// <param name="heightY">World height, m. Zero is the waterline, negative is down.</param>
        /// <param name="seconds">The world's clock, s.</param>
        /// <param name="patch">Which of D061's horizontal cells the sample is in.</param>
        /// <param name="patchCount">How many there are.</param>
        /// <remarks>
        /// <para>
        /// <b>Falls back to the pre-D066 field, bit for bit</b>, whenever <see cref="Rolls"/> is
        /// off or there is only one patch — it calls the two-argument overload rather than
        /// reproducing it, so the two cannot drift apart.
        /// </para>
        /// <para>
        /// <b>The roll.</b> Each pair of adjacent patches is one overturning cell of width 2W and
        /// depth <c>H</c> = <see cref="CellMetres"/>. The patch-averaged vertical velocity is
        /// <c>w_k(d,t) = s_k·A(t)·sin(π·d/H)</c> at depth <c>d = -heightY</c>, with the parity
        /// <c>s_k = +1</c> on even patches and <c>-1</c> on odd ones and <c>A(t)</c> the same pair
        /// of incommensurate time terms the steady field uses (<see cref="TimeFactor"/>). The
        /// horizontal is a quarter turn out of phase in depth — <c>cos(π·d/H)</c> — so it is
        /// largest at the surface and at the bottom of the cell, where an overturning cell has to
        /// hand its water sideways, and zero in the middle where it is going straight up.
        /// </para>
        /// <para>
        /// <b>Exactly zero at both ends, by test and not by rounding.</b> <c>Math.Sin(Math.PI)</c>
        /// is 1.2e-16 rather than 0, and a vertical velocity of 1.2e-16 at the waterline is still
        /// a velocity: integrated over a run it is a slow, invisible, one-directional lift of
        /// exactly the kind logbook/0022 already paid for once. The endpoints are therefore
        /// special-cased to zero rather than computed. Below the cell — deeper than <c>H</c> — the
        /// field is zero: the roll is a surface phenomenon and the water beneath it is still.
        /// </para>
        /// <para>
        /// <b>The horizontal component is the value at this patch's <i>right-hand</i>
        /// boundary</b>, the one between patch <c>k</c> and <c>k+1</c> — a staggered grid, w at
        /// cell centres and u at the faces, which is what makes the field conservative when
        /// <see cref="NutrientField.Advect"/> upwinds across those same faces. A creature in patch
        /// <c>k</c> feels it as its drag term, which is a coarse-graining and is honest about
        /// being one: at this resolution a patch has one number for the water beside it.
        /// </para>
        /// <para>
        /// <b>An odd patch count leaves a seam</b> and this does not pretend otherwise. Parity
        /// alternates around the ring, so with <c>K</c> even every patch's two neighbours are its
        /// opposites and every boundary is a roll boundary; with <c>K</c> odd, patches
        /// <c>K-1</c> and <c>0</c> share a parity and the boundary between them is two up-legs (or
        /// two down-legs) facing each other. The field there is still conservative — stock and
        /// bodies cross it in a well-defined direction — but it is not a roll. Prefer an even
        /// <c>K</c>; the seam is reported rather than repaired because repairing it would mean
        /// either a non-alternating parity (which is not a roll pattern either) or an edge in a
        /// ring that D061 deliberately has none of.
        /// </para>
        /// </remarks>
        /// <remarks>
        /// <b>In <see cref="CurrentMode.Transport"/> it samples at the patch's centre</b>, because
        /// a patch index is all this signature carries and the transport field is a function of a
        /// place. Every reader that has a real position calls
        /// <see cref="VelocityAt(float, float, float, double)"/> instead: the harness's drag pass,
        /// the grid's advection and a drifting corpse all do. What is left on this overload is the
        /// cell and vertex fields' patch-level transport, which is a coarse-graining already and
        /// is honest about being one, and any harness that has only a depth.
        /// </remarks>
        /// <remarks>
        /// <b>In a tank it samples the gyre at the ring's mid-radius</b>, on the <c>θ = 0</c> ray,
        /// for the same reason the transport field samples at the patch's centre: a ring index is
        /// all this signature carries and the gyre is a function of a place. A tank runs a grid
        /// (<see cref="World"/> refuses the cell field there), so the field that actually carries
        /// stock reads <see cref="VelocityAt(float, float, float, double)"/> per cell and this
        /// overload is left to whatever holds only a depth and a patch.
        /// </remarks>
        public Float3 VelocityAt(float heightY, double seconds, int patch, int patchCount)
        {
            Float3 flow = _shape == WorldShape.Tank
                ? GyreAt(PatchCentreX(patch), heightY, PatchCentreZ(patch), seconds)
                : Mode == CurrentMode.Transport
                    ? TransportAt(PatchCentreX(patch), heightY, PatchCentreZ(patch), seconds)
                    : RollOrSteady(heightY, seconds, patch, patchCount);

            if (!VentActive(patchCount)) return flow;

            double depth = -(double)heightY;

            float w = (float)VentVertical(depth, patch, patchCount);
            float u = (float)VentHorizontal(depth, patch, patchCount);

            // The vent's own contribution is (u, w, -u), so this is the sum of two velocities and
            // nothing more. Under the rolls both terms satisfy Z = -X and so does the sum; under
            // the transport field the flow half does not, and the addition is unaffected either
            // way because it was never using that property, only restating it.
            return new Float3(flow.X + u, flow.Y + w, flow.Z - u);
        }

        // ------------------------------------------------------------------ the transport field

        /// <summary>
        /// Water velocity at a place and a time, m/s: the primary sampler for anything that knows
        /// where it is.
        /// </summary>
        /// <param name="x">World x, m. The box is a ring <see cref="LengthMetres"/> long.</param>
        /// <param name="y">World height, m. Zero is the waterline, negative is down.</param>
        /// <param name="z">World z, m. The box is a ring <see cref="WidthMetres"/> wide.</param>
        /// <param name="seconds">The world's clock, s.</param>
        /// <remarks>
        /// <b>In <see cref="CurrentMode.Rolls"/> the horizontal coordinates buy only the patch</b>,
        /// because that is all the roll field is a function of. A caller in that mode that already
        /// holds a creature's patch should keep calling the four-argument overload with it: a patch
        /// is assigned by the world and a patch recomputed from x is the same number in a settled
        /// world and not necessarily the same number in the step a body crosses a seam, and every
        /// run in the record was made with the world's answer.
        /// </remarks>
        public Float3 VelocityAt(float x, float y, float z, double seconds)
        {
            // The tank's water is the gyre whatever the mode says — see Shape. The vent is added
            // exactly as it is to the other two fields, and is off in every launcher that has a
            // tank in it.
            if (_shape != WorldShape.Tank && Mode != CurrentMode.Transport)
            {
                return VelocityAt(y, seconds, PatchOfXZ(x, z), Math.Max(1, _patchCount));
            }

            Float3 flow = _shape == WorldShape.Tank
                ? GyreAt(x, y, z, seconds)
                : TransportAt(x, y, z, seconds);
            if (!VentActive(_patchCount)) return flow;

            double depth = -(double)y;
            int patch = PatchOfXZ(x, z);

            float u = (float)VentHorizontal(depth, patch, _patchCount);
            float w = (float)VentVertical(depth, patch, _patchCount);

            return new Float3(flow.X + u, flow.Y + w, flow.Z - u);
        }

        /// <summary>Water velocity at a place and a time, m/s.</summary>
        public Float3 VelocityAt(Float3 at, double seconds) => VelocityAt(at.X, at.Y, at.Z, seconds);

        /// <summary>
        /// The patch a horizontal position falls in — <c>iz · (K / A) + ix</c>, D077's rule as
        /// fable-propose-box.md's clause 3 generalises it, and <c>floor(x / W) mod K</c> term for
        /// term at A = 1.
        /// </summary>
        /// <remarks>
        /// <see cref="GridField.PatchOf"/>'s arithmetic, repeated here rather than shared because
        /// this class knows nothing about fields and a world may have none of them. The two are
        /// held together by <see cref="World"/> handing both the same geometry.
        /// </remarks>
        public int PatchOfXZ(float x, float z)
        {
            if (_patchCount < 1 || !(_patchWidthMetres > 0f)) return 0;

            // A tank's patches are rings of equal area about the axis, so "which patch" is a
            // radius rather than a pair of divisions — logbook/specs/tank-spec.md.
            if (_shape == WorldShape.Tank)
            {
                return TankGeometry.RingOf(x, z, _tankRadiusMetres, _patchCount);
            }

            int along = PatchesAlong;
            if (along < 1) return 0;

            int ix = (int)Math.Floor(WrapAxis(x, LengthMetres) / _patchWidthMetres);
            ix %= along;
            if (ix < 0) ix += along;

            if (_patchesAcross == 1) return ix;

            int iz = (int)Math.Floor(WrapAxis(z, WidthMetres) / _patchWidthMetres);
            iz %= _patchesAcross;
            if (iz < 0) iz += _patchesAcross;

            return iz * along + ix;
        }

        /// <summary>
        /// The x of a patch's centre, m. At A = 1 this is <c>(patch + ½)·W</c>; in a tank it is
        /// the ring's mid-radius on the <c>θ = 0</c> ray from the axis.
        /// </summary>
        private float PatchCentreX(int patch) =>
            _shape == WorldShape.Tank
                ? _tankRadiusMetres + TankGeometry.MidRadiusOf(patch, _tankRadiusMetres, Math.Max(1, _patchCount))
                : (patch % Math.Max(1, PatchesAlong) + 0.5f) * _patchWidthMetres;

        /// <summary>
        /// The z of a patch's centre, m. At A = 1 this is half the box's width; in a tank every
        /// ring's representative point lies on the <c>θ = 0</c> ray, so this is the axis.
        /// </summary>
        private float PatchCentreZ(int patch) =>
            _shape == WorldShape.Tank
                ? _tankRadiusMetres
                : (patch / Math.Max(1, PatchesAlong) + 0.5f) * _patchWidthMetres;

        private static float WrapAxis(float v, float extent)
        {
            if (v >= 0f && v < extent) return v;
            float folded = v - extent * (float)Math.Floor(v / extent);
            if (folded >= extent || folded < 0f) folded = 0f;
            return folded;
        }

        /// <summary>
        /// An upper bound on the speed of the transport field anywhere in the box, m/s. 0 under
        /// <see cref="CurrentMode.Rolls"/>.
        /// </summary>
        /// <remarks>
        /// <b>A bound, not a sample, so a Courant check on it cannot be beaten by looking in the
        /// wrong place.</b> It is the sum of every mode's own amplitude on each axis, combined
        /// across the three: no argument can produce more, because every term is a sine or a
        /// cosine. That makes it an over-estimate of the true supremum, typically by about twice,
        /// and over-estimating is the direction a stability check should err in.
        /// <see cref="GridField.Advect"/> refuses a step this bound would carry more than half a
        /// cell in, which is what the RMS knob cannot answer: the knob is an average over the box
        /// and the fastest water is where the trouble is.
        /// </remarks>
        /// <remarks>
        /// <b>In a tank it is the gyre's own ceiling</b>, which is measured rather than summed:
        /// the gyre's three parts are normalised numerically, so there is no closed form to add
        /// up, and the fastest sample on the construction lattice times 1.1 is the bound
        /// (<see cref="BuildGyre"/>). Same contract either way — a number no argument reaches, so
        /// <see cref="GridField.Advect"/>'s Courant logic is unchanged by the shape.
        /// </remarks>
        public float MaximumTransportSpeed
        {
            get
            {
                if (_speed <= 0f) return 0f;

                if (_shape == WorldShape.Tank)
                {
                    EnsureGyre();
                    return _speed * _gyreBound;
                }

                if (Mode != CurrentMode.Transport) return 0f;
                EnsureTransport();
                return _speed * _transportBound;
            }
        }

        /// <summary>
        /// The number of Fourier modes the vector potential is made of.
        /// </summary>
        /// <remarks>
        /// Five, which is inside the three-to-six the owner's spec asks for. One mode alone is a
        /// single steady eddy and a particle in it circles forever; the incommensurate drift rates
        /// below are what stop the sum repeating, and they need more than one thing to be
        /// incommensurate with.
        /// </remarks>
        private const int TransportModes = 5;

        // Whole numbers of wavelengths across the box in x and z, which is what makes the field
        // exactly periodic on D077's two rings rather than nearly so: a seam in a periodic world
        // is a wall the water piles against. The five pairs are distinct, which
        // MeasureTransportScale's closed form depends on.
        //
        // The vertical mode number is NOT in this table. It is derived per mode from the two
        // horizontal ones so that the eddy is round rather than tall — see VerticalWaves.
        private static readonly int[] TransportWavesX = { 1, 0, 1, 2, 2 };
        private static readonly int[] TransportWavesZ = { 0, 1, 1, 1, 0 };

        /// <summary>
        /// The number of half-sines down the depth for a mode with these horizontal wavenumbers.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Derived, because the first build fixed it at one or two and the field came out
        /// eleven parts vertical to one part horizontal.</b> The per-axis RMS at a knob of 0.3 m/s
        /// read x 0.027, y 0.297, z 0.027, which is a lift and a fall and almost nothing sideways.
        /// The owner asked for water that moves a body "up and down, left and right, in all
        /// directions really", so the ruling of 2026-09-10 was to balance the axes.
        /// </para>
        /// <para>
        /// <b>The balance is one line of algebra.</b> A single mode's mean square velocities are
        /// <c>a²k_y²/4</c> on x, <c>a²k_y²/4</c> on z and <c>a²(k_x² + k_z²)/4</c> on y, because
        /// the curl puts the vertical wavenumber on the horizontal components and the horizontal
        /// wavenumbers on the vertical one. So the three are equal exactly when
        /// <c>k_y = sqrt(k_x² + k_z²)</c>, and since <c>k_y = qπ/D</c> that fixes
        /// <c>q = round(D·sqrt(k_x² + k_z²)/π)</c>. Rounding to a whole number costs at most a few
        /// per cent of the balance and buys the exact zero at the bed that a fractional <c>q</c>
        /// would give up.
        /// </para>
        /// <para>
        /// <b>The eddies are squat because the box is.</b> A round eddy in a box 20 m long, 5 m
        /// wide and 60 m deep is at most 5 m across, so it is at most 5 m tall: for the mode that
        /// carries the z structure this comes out at <c>q</c> = 24, a vertical wavelength of 5 m,
        /// which is of the order of the width rather than of the depth. That is the price of "all
        /// directions" in a box of this shape and the owner ruled it acceptable. Two consequences
        /// to hold in mind: the 1 m detritus grid resolves such a mode at four to ten cells per
        /// wavelength, which is coarse but real, and the 5 m matter grid samples it about once per
        /// wavelength, so what the matter grid feels is one sample of a wave it cannot see. Both
        /// pass every conservation test either way, because a transfer is conservative whatever
        /// velocity it is handed; what is under-resolved is the shape of the transport, not the
        /// books.
        /// </para>
        /// <para>
        /// <b>Never below one.</b> A wide shallow box would round to zero, at which the mode has no
        /// vertical structure at all, no vertical velocity, and no closure to prove at the bed.
        /// </para>
        /// </remarks>
        private static int VerticalWaves(double kx, double kz, double depth)
        {
            int q = (int)Math.Round(depth * Math.Sqrt(kx * kx + kz * kz) / Math.PI);
            return q < 1 ? 1 : q;
        }

        private double[] _transportAmplitude;
        private double[] _transportKx;
        private double[] _transportKz;
        private double[] _transportKy;
        private double[] _transportRateF;
        private double[] _transportRateG;
        private double[] _transportPhaseF;
        private double[] _transportPhaseG;
        private float _transportBound;

        /// <summary>
        /// The three-dimensional field: the curl of a vector potential, sampled at a place and a
        /// time, in units of <see cref="Speed"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The construction, in full.</b> Write the box as <c>x</c> on a ring of length
        /// <c>L</c>, <c>z</c> on a ring of width <c>W</c>, and <c>y</c> from the floor at
        /// <c>−D</c> to the waterline at 0. The potential is
        /// <c>A = (Φ_m(y)·f_m, 0, Φ_m(y)·g_m)</c> summed over modes, with
        /// <c>Φ_m(y) = sin(q_m·π·y/D)</c>, <c>f_m = a_m·cos(χ_m^f)</c>,
        /// <c>g_m = a_m·cos(χ_m^g)</c> and
        /// <c>χ = k_x·x + k_z·z + θ + σ·(2π·t/T)</c>, where <c>k_x = 2π·n/L</c> and
        /// <c>k_z = 2π·p/W</c>. The velocity is its curl:
        /// </para>
        /// <para>
        /// <c>u = Φ'·g</c>, <c>w = Φ·(∂f/∂z − ∂g/∂x)</c>, <c>v = −Φ'·f</c>, writing <c>u, w, v</c>
        /// for the x, y and z components and <c>Φ' = (q·π/D)·cos(q·π·y/D)</c>. Since <c>f</c> and
        /// <c>g</c> do not depend on <c>y</c>, the divergence is
        /// <c>Φ'·∂g/∂x + Φ'·(∂f/∂z − ∂g/∂x) − Φ'·∂f/∂z = 0</c> identically, for every mode and so
        /// for the sum. Divergence-free by derivation, not by correction: nothing here adjusts a
        /// component after the fact, which is the mistake that would make the property hold at the
        /// points a test happens to check and nowhere else.
        /// </para>
        /// <para>
        /// <b>The vertical vanishes at both ends because <c>Φ</c> does</b>, and it is the only
        /// factor on <c>w</c>. <c>sin(q·π·y/D)</c> is 0 at <c>y = 0</c> and at <c>y = −D</c> for
        /// every whole <c>q</c>, so the water slides along the surface and along the bed and
        /// carries nothing through either. Both ends are special-cased to exactly zero rather than
        /// computed, for the reason the roll's are: <c>Math.Sin(-Math.PI)</c> is −1.2e-16, and a
        /// vertical velocity of 1.2e-16 at the waterline is still a velocity, integrated over a
        /// run into the slow invisible one-directional lift that carried a whole population six
        /// metres into the air in 2,500 s (logbook/0022). A body or a parcel that has arrived
        /// outside the box reads the water at the nearest face, so nothing above the surface is
        /// pushed further up and nothing below the bed is pushed further down.
        /// </para>
        /// <para>
        /// <b>Each mode's amplitude is divided by its own wavenumber norm</b>,
        /// <c>κ = sqrt(2(qπ/D)² + k_x² + k_z²)</c>, so that the five modes contribute comparably
        /// instead of the shortest wavelength drowning the rest: <c>w</c> scales as <c>a·k</c> and
        /// the horizontal components as <c>a·qπ/D</c>, so equal <c>a</c> would weight a mode by how
        /// finely it is cut. It also makes every mode contribute exactly a quarter to the mean
        /// square, which is what lets the RMS scale be a closed form rather than a lattice: see
        /// <see cref="MeasureTransportScale"/>. The whole field is multiplied by that scale so its
        /// RMS speed over the box and over time is exactly 1, and <see cref="Speed"/> multiplies
        /// that.
        /// </para>
        /// <para>
        /// <b>The three axes carry the same RMS, and the first build's did not.</b> The curl puts
        /// the vertical wavenumber on the horizontal components and the horizontal ones on the
        /// vertical, so a mode with wavelengths of the order of the box on every axis is a tall
        /// thin eddy that runs almost entirely up and down: the field measured eleven parts
        /// vertical to one part horizontal in a box 20 m by 5 m by 60 m. Choosing the vertical mode
        /// number to match the horizontal wavenumbers instead makes each mode round, and a round
        /// eddy has no preferred axis. <see cref="VerticalWaves"/> carries the algebra and the
        /// consequence, which is that the eddies are as squat as the box is narrow.
        /// <see cref="HorizontalRatio"/> stays a rolls-only knob: scaling one component of a curl
        /// by hand is exactly the correction that would give up divergence-free.
        /// </para>
        /// <para>
        /// <b>The drift rates are incommensurate multiples of the period</b>,
        /// <c>σ_j = 1 + j·φ</c> with <c>φ</c> the golden ratio's reciprocal and <c>j</c> running
        /// over all ten halves of the five modes, with alternating signs so that no direction is
        /// preferred. The ratio of any two of them is irrational, so the sum has no period, no
        /// parcel is returned to where it started, and the mixing cannot switch itself off at a
        /// timescale nobody chose. That is the same argument <see cref="Incommensurate"/> records
        /// for the rolls, applied to ten terms rather than two.
        /// </para>
        /// </remarks>
        private Float3 TransportAt(float x, float y, float z, double seconds)
        {
            if (_speed <= 0f) return Float3.Zero;

            EnsureTransport();

            return Unit(x, y, z, 2.0 * Math.PI * seconds / _periodSeconds) * (_speed * _transportScale);
        }

        private float _transportScale;

        /// <summary>
        /// The field at unit <see cref="Speed"/> and unit scale, at a place and an already-scaled
        /// phase. Shared by the sampler and by the scale measurement so the two cannot describe
        /// different water.
        /// </summary>
        private Float3 Unit(double x, double y, double z, double t)
        {
            double depth = _depthMetres;

            // Outside the box reads the nearest face — see the remarks on TransportAt.
            if (y > 0d) y = 0d;
            else if (y < -depth) y = -depth;

            bool atFace = y >= 0d || y <= -depth;

            double vx = 0d, vy = 0d, vz = 0d;

            for (int m = 0; m < TransportModes; m++)
            {
                double phase = _transportKx[m] * x + _transportKz[m] * z;
                double chiF = phase + _transportPhaseF[m] + _transportRateF[m] * t;
                double chiG = phase + _transportPhaseG[m] + _transportRateG[m] * t;

                double a = _transportAmplitude[m];
                double ky = _transportKy[m];

                double profile = atFace ? 0d : Math.Sin(ky * y);
                double slope = ky * Math.Cos(ky * y);

                double f = a * Math.Cos(chiF);
                double g = a * Math.Cos(chiG);

                vx += slope * g;
                vz -= slope * f;
                vy += profile * a * (_transportKz[m] * -Math.Sin(chiF) + _transportKx[m] * Math.Sin(chiG));
            }

            return new Float3((float)vx, (float)vy, (float)vz);
        }

        private void EnsureTransport()
        {
            if (_transportAmplitude != null) return;

            if (!(_patchWidthMetres > 0f) || _patchCount < 1 || !(_depthMetres > 0f))
            {
                throw new InvalidOperationException(
                    "CurrentMode.Transport is a field over a box, and this field has not been " +
                    "told what box it is in. Call SetBox(patchWidth, patchCount, depth, seed) " +
                    "first; World's constructor does it for every world. " +
                    FormattableString.Invariant(
                        $"Have patch width {_patchWidthMetres} m, {_patchCount} patches, depth {_depthMetres} m."));
            }

            BuildTransport();
        }

        /// <summary>
        /// Draws the modes' phases from the run's seed and fixes the scale that makes the RMS
        /// speed the knob.
        /// </summary>
        /// <remarks>
        /// <b>Independent of <see cref="Speed"/> and <see cref="PeriodSeconds"/> on purpose.</b>
        /// The speed multiplies the whole field and the period only rescales the clock, so neither
        /// changes the shape and neither forces a rebuild: sweeping either is then a cheap sweep
        /// rather than a rebuild per sample. Geometry and the seed do change the shape, and
        /// <see cref="SetBox"/> drops the tables when they move.
        /// </remarks>
        private void BuildTransport()
        {
            double length = LengthMetres;
            double width = WidthMetres;
            double depth = _depthMetres;

            _transportKx = new double[TransportModes];
            _transportKz = new double[TransportModes];
            _transportKy = new double[TransportModes];
            _transportRateF = new double[TransportModes];
            _transportRateG = new double[TransportModes];
            _transportPhaseF = new double[TransportModes];
            _transportPhaseG = new double[TransportModes];
            _transportAmplitude = new double[TransportModes];

            var rng = new Rng(_seed);

            for (int m = 0; m < TransportModes; m++)
            {
                double kx = 2.0 * Math.PI * TransportWavesX[m] / length;
                double kz = 2.0 * Math.PI * TransportWavesZ[m] / width;
                double ky = VerticalWaves(kx, kz, depth) * Math.PI / depth;

                _transportKx[m] = kx;
                _transportKz[m] = kz;
                _transportKy[m] = ky;

                // κ, so that a finely cut mode does not outweigh a coarse one — see TransportAt.
                _transportAmplitude[m] = 1.0 / Math.Sqrt(2.0 * ky * ky + kx * kx + kz * kz);

                // σ_j = 1 + j·φ over the ten halves, signs alternating. (1 + jφ)/(1 + kφ) is
                // rational only when j = k, so every pair of these is incommensurate.
                _transportRateF[m] = Rate(2 * m);
                _transportRateG[m] = Rate(2 * m + 1);

                _transportPhaseF[m] = 2.0 * Math.PI * rng.NextFloat();
                _transportPhaseG[m] = 2.0 * Math.PI * rng.NextFloat();
            }

            _transportScale = (float)(1.0 / MeasureTransportScale());

            // The bound: every cosine and sine is at most 1, so this is a ceiling no argument
            // reaches. See MaximumTransportSpeed.
            double bx = 0d, by = 0d, bz = 0d;
            for (int m = 0; m < TransportModes; m++)
            {
                double a = _transportAmplitude[m] * _transportScale;
                bx += _transportKy[m] * a;
                bz += _transportKy[m] * a;
                by += a * (Math.Abs(_transportKz[m]) + Math.Abs(_transportKx[m]));
            }

            _transportBound = (float)Math.Sqrt(bx * bx + by * by + bz * bz);

            double Rate(int j) => ((j & 1) == 0 ? 1.0 : -1.0) * (1.0 + j * Incommensurate);
        }

        /// <summary>
        /// The RMS speed of the unit field over the box and over time, which the scale divides out
        /// so that <see cref="Speed"/> means what it says.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>A closed form, and the first build measured it on a lattice instead.</b> That was the
        /// right call while two modes could share a wavevector, because then the cross terms do not
        /// vanish and the algebra is a page long. Since the vertical mode number became a function
        /// of the horizontal ones (see <see cref="VerticalWaves"/>) the five <c>(n, p)</c> pairs
        /// are distinct, no two modes share a wavevector, and every cross term integrates to zero
        /// over the box. The lattice then stopped being safe as well as stopping being necessary: a
        /// balanced mode set puts up to twenty-seven half-sines down the depth, and twelve samples
        /// in y alias that into a number with no relation to the field.
        /// </para>
        /// <para>
        /// <b>The derivation.</b> For one mode, <c>&lt;v_x²&gt; = &lt;Φ'²&gt;&lt;g²&gt;</c> with
        /// <c>Φ' = k_y·cos(k_y·y)</c>, and the y-average of <c>cos²</c> over a whole number of
        /// half-periods is exactly a half, as is the average of <c>cos²(χ)</c> over the box and the
        /// clock; so <c>&lt;v_x²&gt; = a²k_y²/4</c>, and the same for z. On y the two halves of the
        /// mode carry different incommensurate drift rates, so their cross term averages to zero
        /// over time and <c>&lt;v_y²&gt; = a²(k_x² + k_z²)/4</c>. Adding the three gives
        /// <c>a²(2k_y² + k_x² + k_z²)/4</c>, which is <c>a²κ²/4</c> — and <c>a</c> is <c>1/κ</c>, so
        /// every mode contributes exactly a quarter and the whole field's mean square is
        /// <c>M/4</c>. The RMS is <c>sqrt(M)/2</c>, whatever the box and whatever the seed.
        /// </para>
        /// <para>
        /// <b>It is checked and not merely asserted.</b> <c>CurrentTransportTests</c> measures the
        /// RMS on its own lattice, at three knobs, and demands 5%.
        /// </para>
        /// </remarks>
        private double MeasureTransportScale()
        {
            // The derivation needs distinct wavevectors, so it is checked rather than remembered:
            // a repeated (n, p) pair would leave a cross term standing and the scale would be
            // wrong by however much of it survived, silently and only in some boxes.
            for (int m = 0; m < TransportModes; m++)
            {
                for (int n = m + 1; n < TransportModes; n++)
                {
                    if (TransportWavesX[m] == TransportWavesX[n] &&
                        TransportWavesZ[m] == TransportWavesZ[n])
                    {
                        throw new InvalidOperationException(
                            FormattableString.Invariant(
                                $"Transport modes {m} and {n} share the wavevector ({TransportWavesX[m]}, {TransportWavesZ[m]}). ") +
                            "The RMS scale is a closed form that assumes distinct wavevectors, so " +
                            "two modes on one would make Speed mean something other than the RMS.");
                    }
                }
            }

            return Math.Sqrt(TransportModes) / 2.0;
        }

        // ----------------------------------------------------------------------------- the gyre

        /// <summary>
        /// The tank's water: a slow swirl about the axis, two overturning cells on top of it and
        /// two horizontal eddies, in units of <see cref="Speed"/>.
        /// <c>logbook/specs/tank-spec.md</c>, <c>fable-propose-aquarium.md</c> ruling 1.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Prescribed, divergence-free, and tangential at the glass by construction.</b> Not by
        /// correction: each of the three parts is either a pure swirl or a curl, so
        /// <c>div v = 0</c> holds identically rather than at the points a test happens to look —
        /// the same discipline <see cref="TransportAt"/> keeps and for the same reason. A wall the
        /// water piled against would be a source of stock at the rim and a sink at the axis, and
        /// the grid's transfers would carry it faithfully.
        /// </para>
        /// <para>
        /// <b>In cylindrical coordinates about the axis</b>, with <c>r</c> from the axis at
        /// <c>(R, R)</c>, <c>θ</c> from the <c>+x</c> ray, and <c>y</c> from the floor at
        /// <c>−D</c> to the waterline at 0. Write <c>s = r/R</c>.
        /// </para>
        /// <para>
        /// <b>1. The swirl</b>, azimuthal only:
        /// <c>v_θ = S·s(1 − s²)·(1 + ½cos(π y/D))</c>. It depends on <c>r</c> and <c>y</c> and
        /// nothing else, so its divergence — <c>(1/r)∂v_θ/∂θ</c> — is zero term for term. It
        /// vanishes on the axis and at the glass, and it turns three times faster at the surface
        /// than at the bed, which is what a wind-driven tank does.
        /// </para>
        /// <para>
        /// <b>2. The overturning</b>, axisymmetric, from a Stokes stream function
        /// <c>Ψ(r, y) = Ψ₀·r²(1 − r/R)·sin(qπy/D)</c> with <c>v_r = −(1/r)∂Ψ/∂y</c> and
        /// <c>v_y = (1/r)∂Ψ/∂r</c>, which is divergence-free for any <c>Ψ</c> whatever because the
        /// two mixed partials cancel. So <c>v_r = −Ψ₀·r(1 − s)(qπ/D)cos(qπy/D)</c>, which vanishes
        /// at the axis and at the glass, and <c>v_y = Ψ₀(2 − 3s)sin(qπy/D)</c>, which vanishes at
        /// the surface and at the floor for every whole <c>q</c>. Two cells, <c>q</c> = 1 and 2,
        /// each with its own slow phase, so the pattern does not stand still.
        /// </para>
        /// <para>
        /// <b>3. The eddies</b>, horizontal, from a vertical vector potential
        /// <c>A_y = Σ a_m·f_m(r)·cos(mθ + φ_m + ω_m t)·sin(q_m πy/D)</c> with
        /// <c>f_m(r) = s^m(1 − s²)</c> for <c>m</c> = 1 and 2. The curl of a purely vertical
        /// potential has no vertical component, so these move water sideways only:
        /// <c>v_r = (1/r)∂A_y/∂θ</c>, which carries <c>f_m(r)/r = s^(m−1)(1 − s²)/R</c> and
        /// therefore vanishes at the glass and is finite on the axis, and
        /// <c>v_θ = −∂A_y/∂r</c>. The <c>1/r</c> is cancelled analytically rather than guarded
        /// numerically, which is what makes the axis an ordinary point of the field instead of a
        /// hole with a tolerance round it.
        /// </para>
        /// <para>
        /// <b>The three are balanced numerically, because there is no closed form to balance
        /// them by.</b> <see cref="BuildGyre"/> measures each part's mean square on a lattice over
        /// the live volume and a few phases, scales the overturning so that the vertical per-axis
        /// RMS equals the horizontal per-axis RMS — the owner's "up and down, left and right, in
        /// all directions really" of 2026-09-10, applied to a second field — and then scales the
        /// whole thing so the RMS speed is <see cref="Speed"/>. The transport field could do that
        /// in algebra (<see cref="MeasureTransportScale"/>); three parts of different families
        /// with cross terms between them cannot, and a measurement that the tests re-take on a
        /// different lattice is the honest substitute.
        /// </para>
        /// <para>
        /// <b>Outside the water reads the nearest surface.</b> <c>y</c> is clamped to the box and
        /// <c>s</c> to 1, so a body that has overshot the waterline is not pushed further up, a
        /// parcel at the bed is not pushed further down, and a float-error metre past the glass
        /// reads the water at the glass rather than an extrapolation. The vertical component is
        /// special-cased to exactly zero at both faces for the reason the rolls' is:
        /// <c>Math.Sin(-Math.PI)</c> is −1.2e-16 and a vertical velocity of 1.2e-16 at the
        /// waterline is still a velocity, integrated over a run into the lift that carried a whole
        /// population six metres into the air (logbook/0022).
        /// </para>
        /// </remarks>
        private Float3 GyreAt(float x, float y, float z, double seconds)
        {
            if (_speed <= 0f) return Float3.Zero;

            EnsureGyre();

            return GyreUnit(x, y, z, 2.0 * Math.PI * seconds / _periodSeconds, _gyreOverturning)
                * (_speed * _gyreScale);
        }

        /// <summary>How many horizontal eddies the vector potential carries — <c>m</c> = 1 and 2.</summary>
        private const int GyreEddies = 2;

        /// <summary>How many overturning cells the stream function carries — <c>q</c> = 1 and 2.</summary>
        private const int GyreCells = 2;

        private bool _gyreBuilt;
        private double[] _gyreEddyPhase;
        private double[] _gyreEddyRate;
        private double[] _gyreCellPhase;
        private double[] _gyreCellRate;
        private double _gyreSwirlWeight;
        private double _gyreEddyWeight;
        private double _gyreOverturningWeight;
        private double _gyreOverturning;
        private float _gyreScale;
        private float _gyreBound;

        /// <summary>The three parts' RMS speeds at the field's own scale, m/s — for the tests.</summary>
        /// <remarks>
        /// Exposed so that the dead-pocket and balance checks report the field rather than
        /// asserting it: a number a test prints is a number the caller can read in a build report,
        /// which is what <c>logbook/specs/tank-spec.md</c> asks of this field before a round runs
        /// on it.
        /// </remarks>
        public (float Swirl, float Overturning, float Eddies) GyreComponentRms
        {
            get
            {
                if (_shape != WorldShape.Tank) return (0f, 0f, 0f);
                EnsureGyre();

                float scale = _speed * _gyreScale;
                return (
                    (float)(_gyreRmsSwirl * scale),
                    (float)(_gyreRmsOverturning * _gyreOverturning * scale),
                    (float)(_gyreRmsEddies * scale));
            }
        }

        private double _gyreRmsSwirl;
        private double _gyreRmsOverturning;
        private double _gyreRmsEddies;

        private void EnsureGyre()
        {
            if (_gyreBuilt) return;

            if (_shape != WorldShape.Tank)
            {
                throw new InvalidOperationException(
                    "The gyre is the tank's water and this field is in a box. Call " +
                    "SetBox(..., WorldShape.Tank, radius) first; World's constructor does it for " +
                    "every tank.");
            }

            if (!(_tankRadiusMetres > 0f) || !(_depthMetres > 0f))
            {
                throw new InvalidOperationException(
                    "The gyre is a field over a tank, and this field has not been told what tank " +
                    "it is in. " +
                    FormattableString.Invariant(
                        $"Have radius {_tankRadiusMetres} m, depth {_depthMetres} m."));
            }

            BuildGyre();
            _gyreBuilt = true;
        }

        /// <summary>
        /// Draws the parts' phases from the run's seed, balances the axes and fixes the scale that
        /// makes the RMS speed the knob.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The balance is one quadratic.</b> Write the overturning's amplitude as <c>β</c> and
        /// hold the other two at their own unit RMS. The vertical mean square is <c>β²V</c>,
        /// since only the overturning has a vertical component; the horizontal mean square is
        /// <c>H₀ + 2βC + β²H₁</c>, with <c>C</c> the cross term between the overturning's radial
        /// flow and the rest, which is small but is measured rather than assumed away. Per-axis
        /// equality is <c>β²V = ½(H₀ + 2βC + β²H₁)</c>, so
        /// <c>β²(V − H₁/2) − βC − H₀/2 = 0</c> and <c>β</c> is its positive root. The
        /// overturning is about ninety-nine parts vertical in a tank this shape — <c>v_r</c>
        /// carries a factor <c>qπ/D</c>, which is 0.05 per metre at 60 m — so the root is real and
        /// well away from the degenerate case; the code checks anyway rather than trusting the
        /// geometry it happens to be built with.
        /// </para>
        /// <para>
        /// <b>The lattice is Cartesian over the bounding square and keeps what is inside the
        /// circle</b>, which is the live volume the mask calls live
        /// (<see cref="GridField"/>), so the RMS the knob names is the RMS of the water that
        /// exists rather than of a square that includes four dry corners.
        /// </para>
        /// <para>
        /// <b>Independent of <see cref="Speed"/> and <see cref="PeriodSeconds"/></b>, for
        /// <see cref="BuildTransport"/>'s reason: the speed multiplies the whole field and the
        /// period only rescales the clock, so sweeping either is a sweep rather than a rebuild per
        /// sample.
        /// </para>
        /// </remarks>
        private void BuildGyre()
        {
            _gyreEddyPhase = new double[GyreEddies];
            _gyreEddyRate = new double[GyreEddies];
            _gyreCellPhase = new double[GyreCells];
            _gyreCellRate = new double[GyreCells];

            var rng = new Rng(_seed);

            // σ_j = 1 + j·φ with φ the golden ratio's reciprocal and alternating signs, over the
            // four moving halves of the field: the ratio of any two is irrational, so the sum has
            // no period, no parcel is returned to where it started, and the stirring cannot switch
            // itself off at a timescale nobody chose. Incommensurate's own argument, applied to
            // four terms rather than two.
            for (int m = 0; m < GyreEddies; m++)
            {
                _gyreEddyPhase[m] = 2.0 * Math.PI * rng.NextFloat();
                _gyreEddyRate[m] = Rate(m);
            }

            for (int q = 0; q < GyreCells; q++)
            {
                _gyreCellPhase[q] = 2.0 * Math.PI * rng.NextFloat();
                _gyreCellRate[q] = Rate(GyreEddies + q);
            }

            // Two passes, because the parts must be comparable before they can be balanced. The
            // first measures the swirl and the eddies at raw amplitude 1 and gives each the weight
            // that makes its RMS speed 1; the second measures the balanced pair against the
            // overturning and solves for the amplitude that squares the axes. Without the first
            // pass the two horizontal parts would be in whatever ratio their formulae happen to
            // produce in a tank of this shape, which is a number nobody chose.
            _gyreSwirlWeight = 1d;
            _gyreEddyWeight = 1d;
            _gyreOverturningWeight = 1d;

            double rawSwirl = 0d, rawEddies = 0d;
            int counted = Walk((x, y, z, t) =>
            {
                Float3 swirl = GyreUnit(x, y, z, t, 0d, eddies: false);
                Float3 both = GyreUnit(x, y, z, t, 0d);

                rawSwirl += (double)swirl.X * swirl.X + (double)swirl.Z * swirl.Z;

                double eddyX = both.X - swirl.X;
                double eddyZ = both.Z - swirl.Z;
                rawEddies += eddyX * eddyX + eddyZ * eddyZ;
            });

            if (counted == 0 || !(rawSwirl > 0d) || !(rawEddies > 0d))
            {
                throw new InvalidOperationException(
                    "The gyre measured nothing over its own tank, which means the lattice found " +
                    FormattableString.Invariant(
                        $"no live water at radius {_tankRadiusMetres} m and depth {_depthMetres} m."));
            }

            _gyreSwirlWeight = Math.Sqrt(counted / rawSwirl);
            _gyreEddyWeight = Math.Sqrt(counted / rawEddies);

            // The second pass. `h0` is the mean square of the horizontal flow without the
            // overturning, `h1` the overturning's own horizontal mean square at unit amplitude,
            // `cross` the term between them — small, since the overturning is nearly all vertical,
            // but measured rather than assumed away — and `vertical` its vertical mean square.
            double h0 = 0d, h1 = 0d, cross = 0d, vertical = 0d;

            Walk((x, y, z, t) =>
            {
                Float3 rest = GyreUnit(x, y, z, t, 0d);
                Float3 whole = GyreUnit(x, y, z, t, 1d);

                double overX = whole.X - rest.X;
                double overZ = whole.Z - rest.Z;

                h0 += (double)rest.X * rest.X + (double)rest.Z * rest.Z;
                h1 += overX * overX + overZ * overZ;
                cross += rest.X * overX + rest.Z * overZ;
                vertical += (double)whole.Y * whole.Y;
            });

            // Per-axis equality, which is what "the vertical RMS equals the horizontal" means in a
            // field with two horizontal axes and one vertical: the horizontal sums above carry x
            // and z together, so the condition is beta^2*V = (h0 + 2*beta*C + beta^2*h1)/2.
            double a = (vertical - 0.5 * h1) / counted;
            double b = -cross / counted;
            double c = -0.5 * h0 / counted;

            if (!(a > 1e-12))
            {
                throw new InvalidOperationException(
                    "The overturning carries no more vertical motion than horizontal, so there is " +
                    "no amplitude at which the axes balance. A tank whose depth is of the order " +
                    "of its radius would do that; this one is not meant to be.");
            }

            _gyreOverturning = (-b + Math.Sqrt(b * b - 4d * a * c)) / (2d * a);

            double meanSquare =
                (h0 + 2d * _gyreOverturning * cross +
                 _gyreOverturning * _gyreOverturning * (h1 + vertical)) / counted;

            _gyreScale = (float)(1d / Math.Sqrt(meanSquare));

            // The three parts' own RMS speeds at unit Speed and unit scale, kept so that
            // GyreComponentRms can report them without measuring again. The first two are 1 by
            // construction after the first pass; the overturning's is at unit amplitude, and the
            // property multiplies it by the amplitude just solved for.
            _gyreRmsSwirl = 1d;
            _gyreRmsEddies = 1d;
            _gyreRmsOverturning = Math.Sqrt((h1 + vertical) / counted);

            // The bound, measured on the same lattice with everything applied and lifted a tenth.
            // The transport field can sum its modes' amplitudes for a ceiling no argument reaches;
            // three numerically balanced parts have no such sum, so this is the fastest water the
            // lattice saw plus a margin for the water between its points. GridField.Advect refuses
            // a step this would carry more than half a cell in, and an under-estimate there would
            // pass a step the fastest water breaks, so the margin errs upward.
            double fastest = 0d;

            Walk((x, y, z, t) =>
            {
                double speed = (GyreUnit(x, y, z, t, _gyreOverturning) * _gyreScale).Magnitude;
                if (speed > fastest) fastest = speed;
            });

            _gyreBound = (float)(1.1d * fastest);

            double Rate(int j) => ((j & 1) == 0 ? 1.0 : -1.0) * (1.0 + j * Incommensurate);
        }

        /// <summary>
        /// Visits every live point of the construction lattice at every phase, and returns how
        /// many there were.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>One walk, three measurements.</b> <see cref="BuildGyre"/> passes over the same
        /// points three times — to weigh the two horizontal parts against each other, to balance
        /// them against the overturning, and to find the fastest water — and three copies of a
        /// triple loop is three places for a lattice to drift.
        /// </para>
        /// <para>
        /// <b>Cartesian over the bounding square, keeping what is inside the circle</b>, so the
        /// measurement is over the live volume the grid's mask calls live rather than over a
        /// square with four dry corners in it. The lattice is fine enough that a mode with two
        /// half-sines down the depth and two wavelengths round the rim is sampled many times over,
        /// and coarse enough that the whole construction is a fraction of a second: about 150,000
        /// samples a pass.
        /// </para>
        /// <para>
        /// <b>The clock is sampled more finely than the space, and the first build had that the
        /// wrong way round.</b> Twenty-eight points across and five phases put the RMS 19% above
        /// the knob and left the vertical axis 1.47 times the horizontal on a second lattice: the
        /// space is smooth at the scale of the tank and five samples of a cosine are not five
        /// samples of its mean square. The phases are a golden-ratio sequence,
        /// <c>t_p = 2π·p/φ</c>, and every drift rate here is <c>±(1 + jφ)</c>, so each mode's own
        /// phase walks the circle in equal steps of <c>φ</c> and thirty-two of them cover it
        /// evenly. The same argument <see cref="Incommensurate"/> records, used to sample rather
        /// than to stir.
        /// </para>
        /// </remarks>
        private int Walk(Action<float, float, float, double> visit)
        {
            const int Horizontal = 22;
            const int Vertical = 16;
            const int Phases = 32;

            int taken = 0;

            for (int p = 0; p < Phases; p++)
            {
                double t = 2.0 * Math.PI * p / Incommensurate;

                for (int iy = 0; iy < Vertical; iy++)
                {
                    float y = -(float)((iy + 0.5) * _depthMetres / Vertical);

                    for (int ix = 0; ix < Horizontal; ix++)
                    {
                        float x = (float)((ix + 0.5) * 2d * _tankRadiusMetres / Horizontal);

                        for (int iz = 0; iz < Horizontal; iz++)
                        {
                            float z = (float)((iz + 0.5) * 2d * _tankRadiusMetres / Horizontal);
                            if (!TankGeometry.Inside(x, z, _tankRadiusMetres)) continue;

                            visit(x, y, z, t);
                            taken++;
                        }
                    }
                }
            }

            return taken;
        }

        /// <summary>
        /// The gyre at unit <see cref="Speed"/> and unit scale, at a place and an already-scaled
        /// phase, with the overturning at <paramref name="overturning"/>.
        /// </summary>
        /// <remarks>
        /// One function for the sampler and for the measurement, so the water a run feels and the
        /// water the scale was measured on cannot be two different fields — the discipline
        /// <see cref="Unit"/> keeps for the transport. The overturning's amplitude is an argument
        /// rather than a field so that <see cref="BuildGyre"/> can separate it from the rest with
        /// two calls instead of a second copy of the arithmetic.
        /// </remarks>
        private Float3 GyreUnit(
            double x, double y, double z, double t, double overturning, bool eddies = true)
        {
            double depth = _depthMetres;
            double radius = _tankRadiusMetres;

            // Outside reads the nearest face — see the remarks on GyreAt.
            if (y > 0d) y = 0d;
            else if (y < -depth) y = -depth;

            bool atFace = y >= 0d || y <= -depth;

            double dx = x - radius;
            double dz = z - radius;
            double r = Math.Sqrt(dx * dx + dz * dz);
            double s = r / radius;
            if (s > 1d) s = 1d;

            // θ = 0 on the axis, which is not a choice but the limit: the m = 1 eddy's Cartesian
            // velocity there is the same whichever ray it is approached along (the r and θ terms
            // combine into a constant), and every other term vanishes as r goes to zero.
            double theta = r > 0d ? Math.Atan2(dz, dx) : 0d;

            double radial = 0d;
            double azimuthal = 0d;
            double vy = 0d;

            // 1. The swirl.
            azimuthal += _gyreSwirlWeight * s * (1d - s * s) *
                (1d + 0.5d * Math.Cos(Math.PI * y / depth));

            // 2. The overturning, two cells.
            if (overturning != 0d)
            {
                for (int q = 0; q < GyreCells; q++)
                {
                    double ky = (q + 1) * Math.PI / depth;
                    double amplitude = overturning * _gyreOverturningWeight *
                        Math.Cos(_gyreCellRate[q] * t + _gyreCellPhase[q]);

                    radial -= amplitude * r * (1d - s) * ky * Math.Cos(ky * y);

                    // Exactly zero at the waterline and at the bed, not nearly — see the remarks.
                    if (!atFace) vy += amplitude * (2d - 3d * s) * Math.Sin(ky * y);
                }
            }

            // 3. The eddies, horizontal only.
            if (eddies)
            {
                for (int e = 0; e < GyreEddies; e++)
                {
                    int m = e + 1;
                    double ky = m * Math.PI / depth;
                    double profile = atFace ? 0d : Math.Sin(ky * y);
                    if (profile == 0d) continue;

                    double chi = m * theta + _gyreEddyPhase[e] + _gyreEddyRate[e] * t;

                    // f_m(r)/r, written as s^(m−1)(1 − s²)/R so the axis needs no guard, and
                    // f_m'(r) = [m·s^(m−1) − (m + 2)·s^(m+1)]/R.
                    double sPow = Math.Pow(s, m - 1);
                    double overR = sPow * (1d - s * s) / radius;
                    double slope = (m * sPow - (m + 2) * sPow * s * s) / radius;

                    radial -= _gyreEddyWeight * m * overR * Math.Sin(chi) * profile;
                    azimuthal -= _gyreEddyWeight * slope * Math.Cos(chi) * profile;
                }
            }

            double cos = r > 0d ? dx / r : 1d;
            double sin = r > 0d ? dz / r : 0d;

            return new Float3(
                (float)(radial * cos - azimuthal * sin),
                (float)vy,
                (float)(radial * sin + azimuthal * cos));
        }

        /// <summary>
        /// Everything the field was before D067 — the roll when <see cref="Rolls"/> is on and there
        /// is a neighbour, the depth-only oscillation otherwise.
        /// </summary>
        /// <remarks>
        /// <b>Extracted verbatim rather than rewritten</b>, so that a vent left off is not merely
        /// close to the pre-D067 field but the same arithmetic in the same order — which is what
        /// <c>RollCellTests</c> asserts with <c>==</c> and what makes every measurement on file
        /// still describe a world that exists.
        /// </remarks>
        private Float3 RollOrSteady(float heightY, double seconds, int patch, int patchCount)
        {
            if (!Rolls || patchCount < 2) return VelocityAt(heightY, seconds);
            if (_speed <= 0f) return Float3.Zero;

            double depth = -(double)heightY;
            if (depth < 0d || depth > _cellMetres) return Float3.Zero;

            double amplitude = Parity(patch, seconds) * TimeFactor(seconds);
            double phase = Math.PI * depth / _cellMetres;

            // Exactly zero at the waterline and at the bottom of the cell — see the remarks.
            double profile = depth <= 0d || depth >= _cellMetres ? 0d : Math.Sin(phase);

            float vertical = (float)(_speed * amplitude * profile);
            float horizontal = (float)(_speed * HorizontalRatio * amplitude * Math.Cos(phase));

            return new Float3(horizontal, vertical, -horizontal);
        }

        /// <summary>
        /// The roll's own horizontal velocity at this patch's right-hand face, m/s — 0 when the
        /// rolls are off.
        /// </summary>
        /// <remarks>
        /// <b>The roll's, not the field's.</b> The transport passes must not be fed by the
        /// two-argument field's horizontal term: it never carried anything (§6.3 makes it drag and
        /// nothing else), and turning the vent on must not quietly start moving stock round the ring
        /// by a term that was inert for the whole history of the project. Nor may they be fed by the
        /// combined <see cref="VelocityAt(float,double,int,int)"/>, which now carries the vent's own
        /// drag term and would count the legs twice.
        /// </remarks>
        private float RollHorizontal(float heightY, double seconds, int patch, int patchCount) =>
            Rolls && patchCount >= 2 ? RollOrSteady(heightY, seconds, patch, patchCount).X : 0f;

        /// <summary>
        /// The vent's vertical velocity at a depth, m/s — up in the plume, down everywhere else.
        /// </summary>
        /// <remarks>
        /// <b>Exactly zero at both ends, special-cased rather than computed</b>, for the reason the
        /// roll's endpoints are: a residual vertical velocity at the waterline is a slow invisible
        /// lift into a region the world has no top to (logbook/0022, logbook/0034). The plume runs
        /// at <see cref="VentSpeed"/> and each of the <c>K−1</c> return patches at
        /// <c>−VentSpeed/(K−1)</c>, so the column integrates to zero net vertical flux across every
        /// level: what rises in one patch sinks in the others, to the last bit.
        /// </remarks>
        private double VentVertical(double depth, int patch, int patchCount)
        {
            if (depth <= 0d || depth >= _ventDepthMetres) return 0d;

            return patch == _ventPatch
                ? _ventSpeed
                : -(double)_ventSpeed / (patchCount - 1);
        }

        /// <summary>
        /// The vent's horizontal drag velocity at this patch's right-hand face, m/s. 0 outside a
        /// leg, and 0 while <see cref="PatchWidthMetres"/> is 0.
        /// </summary>
        /// <remarks>
        /// <c>u = F_j / (L · A_patch / W) = c_j·s·W/L</c> — the leg's volume flux divided by the
        /// area of the face it crosses. This is what a creature feels and nothing else: horizontal
        /// position is ecologically inert (§6.3), so it is drag and orientation, never gain. The
        /// transport uses <see cref="VentTransportFraction"/> instead, which is width-free and
        /// therefore cannot disagree with the flux the legs are defined by.
        /// </remarks>
        private double VentHorizontal(double depth, int patch, int patchCount)
        {
            if (!(_patchWidthMetres > 0f)) return 0d;

            double c = LegCoefficient(depth, patch, patchCount);
            return c == 0d ? 0d : c * _ventSpeed * _patchWidthMetres / _ventLegMetres;
        }

        /// <summary>
        /// The leg coefficient <c>c_j</c> at this patch's right-hand face, signed — 0 between the
        /// legs.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>This is the whole geometry of the vent, and it is one line of algebra.</b> Number the
        /// faces round the ring from the plume: face <c>j</c> is the one between patch <c>V+j</c>
        /// and <c>V+j+1</c>, and the flux through it, positive toward <c>V+j+1</c>, is
        /// <c>F_j = c_j·Q</c> with <c>Q = s·A_patch</c> and <c>c_j = ½ − j/(K−1)</c>. The plume's
        /// water therefore leaves it half to each side (<c>c_0 = +½</c>, <c>c_{K−1} = −½</c>), and
        /// each return patch keeps <c>c_{j−1} − c_j = Q/(K−1)</c> of what passes it — which is
        /// exactly what it sinks. That identity is the continuity proof; there is nothing else to
        /// it.
        /// </para>
        /// <para>
        /// <b>The floor leg is the same fluxes negated</b>, because the water comes back along the
        /// bottom. Its band is <c>D − L ≤ d &lt; D</c> rather than <c>D − L &lt; d ≤ D</c> so that
        /// the last layer of a world whose depth is a whole number of layers is the leg, which is
        /// what the mid-height sampling in <see cref="NutrientField.Advect"/> asks for.
        /// </para>
        /// <para>
        /// <b>If the two legs overlap the surface one wins</b> — a leg thicker than half the world
        /// is not a circulation and the vent is not defined for it; this resolves the case rather
        /// than pretending it cannot arise.
        /// </para>
        /// </remarks>
        private double LegCoefficient(double depth, int patch, int patchCount)
        {
            if (!(_ventLegMetres > 0f)) return 0d;

            double sign;
            if (depth >= 0d && depth < _ventLegMetres) sign = 1d;
            else if (depth >= _ventDepthMetres - (double)_ventLegMetres && depth < _ventDepthMetres) sign = -1d;
            else return 0d;

            int j = (patch - _ventPatch) % patchCount;
            if (j < 0) j += patchCount;

            return sign * (0.5d - (double)j / (patchCount - 1));
        }

        /// <summary>
        /// Fraction of a leg cell's volume that crosses this patch's right-hand face in
        /// <paramref name="dt"/>, signed. 0 outside a leg and 0 when the vent is off.
        /// </summary>
        /// <remarks>
        /// <b>Width-free, and computed from the flux rather than from a velocity.</b>
        /// <c>F_j·dt / (A_patch·L) = |c_j|·s·dt/L</c>: the patch area cancels, so this never needs
        /// to know how wide a patch is and cannot drift away from the flux the legs are defined by.
        /// Deriving it from <see cref="VentHorizontal"/> and a width instead would make the
        /// transport depend on a number the field does not really have — and would silently stop
        /// transporting anything whenever <see cref="PatchWidthMetres"/> had not been set.
        /// </remarks>
        private double VentTransportFraction(float heightY, int patch, int patchCount, double dt)
        {
            if (!VentActive(patchCount)) return 0d;

            double c = LegCoefficient(-(double)heightY, patch, patchCount);
            return c == 0d ? 0d : c * _ventSpeed * dt / _ventLegMetres;
        }

        /// <summary>
        /// The net signed fraction crossing this patch's right-hand face in <paramref name="dt"/>:
        /// the roll's and the vent's, added.
        /// </summary>
        /// <remarks>
        /// <b>Added as signed fractions and only then split into a magnitude and a direction</b>,
        /// because the two flows can oppose each other at the same face and what crosses is the net
        /// of them. Summing magnitudes instead would move more stock than the water carries and
        /// would move it in whichever direction happened to win, which is a transport nobody
        /// specified. <paramref name="dt"/> multiplies both terms, so the <i>sign</i> of this is
        /// independent of it — which is what lets <see cref="CrossingDirection"/> answer without
        /// being told a step length.
        /// </remarks>
        private double SignedTransportFraction(
            float heightY, double seconds, int patch, int patchCount, double dt, double widthMetres)
        {
            double signed = 0d;

            if (widthMetres > 0d)
            {
                float u = RollHorizontal(heightY, seconds, patch, patchCount);
                if (u != 0f) signed = u * dt / widthMetres;
            }

            return signed + VentTransportFraction(heightY, patch, patchCount, dt);
        }

        /// <summary>Magnitude of <see cref="SignedTransportFraction"/>, clamped at ½.</summary>
        private double HorizontalTransportFraction(
            float heightY, double seconds, int patch, int patchCount, double dt, double widthMetres)
        {
            double fraction = Math.Abs(
                SignedTransportFraction(heightY, seconds, patch, patchCount, dt, widthMetres));

            return fraction > 0.5d ? 0.5d : fraction;
        }

        /// <summary>Sign of <see cref="SignedTransportFraction"/>.</summary>
        private int HorizontalTransportSign(
            float heightY, double seconds, int patch, int patchCount, double dt, double widthMetres)
        {
            double signed = SignedTransportFraction(heightY, seconds, patch, patchCount, dt, widthMetres);
            return signed > 0d ? 1 : signed < 0d ? -1 : 0;
        }

        /// <summary>
        /// Fraction of what sits in this patch that crosses its right-hand boundary in one step
        /// — D066's conservative upwind transfer. Zero when <see cref="Rolls"/> is off.
        /// </summary>
        /// <param name="heightY">World height, m. Zero is the waterline, negative is down.</param>
        /// <param name="seconds">The world's clock, s.</param>
        /// <param name="patch">Which of D061's horizontal cells the sample is in.</param>
        /// <param name="patchCount">How many there are.</param>
        /// <param name="dt">Step length, s.</param>
        /// <param name="patchWidthMetres">Width of one patch, m — the distance to cross.</param>
        /// <remarks>
        /// <b>Clamped at ½, the same Courant number <see cref="NutrientField.Mix"/> uses.</b> Each
        /// cell has two boundaries, so a clamp at ½ is what keeps a cell from being asked for more
        /// than it holds however coarse the step — the transfer is conservative at any timestep and
        /// non-negative at any timestep, and neither property depends on anyone choosing dt well.
        /// The magnitude is the same for a body and for the water beside it, which is the point:
        /// the two cross together in expectation rather than the detritus staying behind.
        /// </remarks>
        public double HorizontalCrossingFraction(
            float heightY, double seconds, int patch, int patchCount, double dt, float patchWidthMetres)
        {
            if (!VentActive(patchCount))
            {
                // The pre-D067 path, unchanged — see RollOrSteady's remarks for why it is left
                // alone rather than folded into the general case below.
                if (!Rolls || patchCount < 2 || _speed <= 0f) return 0d;
                if (!(dt > 0d) || !(patchWidthMetres > 0f)) return 0d;

                double u = Math.Abs(VelocityAt(heightY, seconds, patch, patchCount).X);
                double fraction = u * dt / patchWidthMetres;

                return fraction > 0.5d ? 0.5d : fraction;
            }

            // D067. The vent's own term is width-free, so a leg transports whether or not anyone
            // has told the field how wide a patch is; the roll's is not and is skipped without one,
            // exactly as it was before.
            if (!(dt > 0d)) return 0d;

            return HorizontalTransportFraction(
                heightY, seconds, patch, patchCount, dt, patchWidthMetres);
        }

        /// <summary>
        /// Which way the water crosses the boundary between <paramref name="patch"/> and the patch
        /// after it: +1 from <c>k</c> to <c>k+1</c>, -1 the other way. 0 when there is no flow.
        /// </summary>
        /// <remarks>
        /// <b>The convention, stated once.</b> In a roll where <c>k</c> is the leg going up and
        /// <c>k+1</c> the leg coming down, water leaves the up-leg at the surface and returns to it
        /// along the floor. So in the upper half of the cell (<c>cos(π·d/H) &gt; 0</c>) the flow at
        /// the boundary runs from the up-leg toward the down-leg, and in the lower half it runs
        /// back. Both the parity and the amplitude carry a sign — a roll whose amplitude has gone
        /// negative this half-cycle <i>is</i> the mirror roll — so this is simply the sign of the
        /// horizontal velocity at that boundary, which is what keeps bodies and stock moving the
        /// same way.
        /// </remarks>
        /// <remarks>
        /// <para>
        /// <b>D067 adds a second flow at the same face and this is their net.</b> The vent's legs
        /// and the roll's limbs can oppose each other, and what crosses is the difference — so the
        /// direction is the sign of the summed signed fraction rather than of either term.
        /// </para>
        /// <para>
        /// <b>It reads <see cref="PatchWidthMetres"/> rather than being handed a width</b>, which
        /// is the one place the vent forces this class to hold state. The roll's fraction is
        /// <c>u·dt/W</c> and the vent's is <c>|c_j|·s·dt/L</c>: <c>dt</c> cancels out of the
        /// comparison, <c>W</c> does not, so a signature with neither cannot weigh the two. The
        /// step length is therefore passed as 1 here and the width comes from the world, which
        /// <see cref="World"/>'s constructor sets from the same
        /// <see cref="NutrientField.PatchWidthMetres"/> its callers pass to
        /// <see cref="HorizontalCrossingFraction"/> — one geometry, so the sign and the magnitude
        /// cannot describe different water. With the vent off nothing reads it and the pre-D067
        /// path is taken unchanged.
        /// </para>
        /// </remarks>
        public int CrossingDirection(float heightY, double seconds, int patch, int patchCount)
        {
            if (!VentActive(patchCount))
            {
                if (!Rolls || patchCount < 2 || _speed <= 0f) return 0;

                float u = VelocityAt(heightY, seconds, patch, patchCount).X;
                return u > 0f ? 1 : u < 0f ? -1 : 0;
            }

            return HorizontalTransportSign(heightY, seconds, patch, patchCount, 1d, _patchWidthMetres);
        }

        /// <summary>
        /// The roll amplitude at a time, dimensionless and signed — the two incommensurate time
        /// terms of the steady field, without the depth.
        /// </summary>
        /// <remarks>
        /// <b>Not shared with the two-argument overload, and that is deliberate.</b> The steady
        /// field is <c>sin(ky)·sin(ωt) + sin(k'y + 1)·sin(ω't)</c>, which does not factor into a
        /// depth times a time: each term pairs its own wavenumber with its own frequency. The roll
        /// does factor — its depth profile is fixed by the cell geometry and only the amplitude
        /// moves — so it gets the time halves of those same two terms and the same golden-ratio
        /// incommensurability, for the same reason: two commensurate terms share a period, the
        /// field repeats exactly, every parcel returns home, and the mixing switches itself off at
        /// a timescale nobody chose.
        /// </remarks>
        private double TimeFactor(double seconds)
        {
            double t = 2.0 * Math.PI * seconds / _periodSeconds;
            return 0.5 * (Math.Sin(t) + Math.Sin(t * Incommensurate));
        }

        /// <summary>
        /// Which way this patch's leg of the roll runs: +1 or -1, alternating with the patch index
        /// and flipping every <see cref="RollBlinkSeconds"/>.
        /// </summary>
        private int Parity(int patch, double seconds)
        {
            // Floor, not truncation, so the blink is uniform across a clock that could be handed a
            // negative time by a test — truncation toward zero would give the interval either side
            // of t=0 twice the length of every other one.
            int sign = (patch & 1) == 0 ? 1 : -1;

            if (_rollBlinkSeconds > 0f)
            {
                double blink = Math.Floor(seconds / _rollBlinkSeconds);
                if (Math.Abs(blink % 2d) == 1d) sign = -sign;
            }

            return sign;
        }

        /// <summary>
        /// Net displacement of a particle carried by the flow over <paramref name="seconds"/>, m.
        /// </summary>
        /// <remarks>
        /// <b>The number that actually decides whether this is a circulation or a conveyor</b>, and
        /// the one the first version of this class did not check. <see cref="MeanVerticalOver"/>
        /// asks what the water does at a fixed depth; this asks what happens to something the water
        /// is carrying, and a field can have zero of the first and plenty of the second — which is
        /// exactly what a travelling wave has, and how the population ended up six metres into the
        /// air (logbook/0022).
        /// </remarks>
        public double DriftOf(float heightY, double seconds, double step = 0.05)
        {
            if (!(step > 0d)) throw new ArgumentOutOfRangeException(nameof(step));

            double y = heightY;

            for (double t = 0d; t < seconds; t += step)
            {
                // Midpoint, because forward Euler on an oscillating field accumulates a bias of
                // its own and would be measuring the integrator rather than the flow.
                double half = y + 0.5 * step * VelocityAt((float)y, t).Y;
                y += step * VelocityAt((float)half, t + 0.5 * step).Y;
            }

            return y - heightY;
        }

        /// <summary>
        /// Mean vertical velocity over one whole period at a fixed depth. Should be ~0.
        /// </summary>
        /// <remarks>
        /// Exposed so a test can assert it rather than a comment claiming it. A field with a
        /// nonzero time-mean is a conveyor belt: it would carry every creature and every particle
        /// steadily in one direction and pile the world against a boundary, and it would do so
        /// slowly enough to look like an ecological result for a long time.
        /// </remarks>
        public double MeanVerticalOver(float heightY, int periods = 64, int samples = 65536)
        {
            if (samples < 1) throw new ArgumentOutOfRangeException(nameof(samples));
            if (periods < 1) throw new ArgumentOutOfRangeException(nameof(periods));

            // Over many periods rather than one, and never exactly zero for a finite window. The
            // two terms have incommensurate periods by construction, so no interval contains a
            // whole number of both cycles and there is always a partial cycle left over. That
            // residual falls like 1/periods; a genuine bias would not, which is how the two are
            // told apart.
            double window = _periodSeconds * periods;

            double sum = 0d;
            for (int i = 0; i < samples; i++)
            {
                sum += VelocityAt(heightY, i * window / samples).Y;
            }

            return sum / samples;
        }

        // Invariant, not the process culture. Every string this returns reaches a run header, a
        // log line or a test assertion, and on a machine with a comma decimal separator the
        // process culture renders "vent 0.1 m/s" as "vent 0,1 m/s" — which changes what a header
        // says the world was and fails VentTests' assertion for a reason that has nothing to do
        // with the world. Same rule ConfigSchema.Format() and Json.Writer already follow.
        public override string ToString() =>
            (_speed <= 0f
                ? "still water"
                : (_shape == WorldShape.Tank
                      // The gyre is chosen by the shape and not by the mode, so the string says
                      // gyre wherever the water is a tank's — a header that printed "transport"
                      // for a field with no rings in it would name a knob the run did not spend.
                      ? FormattableString.Invariant(
                            $"{_speed:0.###} m/s RMS gyre, {_periodSeconds:0.#} s period")
                      : Mode == CurrentMode.Transport
                      // The knob means the RMS over the box here and the peak under the rolls, so
                      // the string says which rather than printing one number under two meanings.
                      // CellMetres is a roll's own geometry and is not named in a mode that has no
                      // rolls to size.
                      ? FormattableString.Invariant(
                            $"{_speed:0.###} m/s RMS transport, {_periodSeconds:0.#} s period")
                      : FormattableString.Invariant(
                            $"{_speed:0.###} m/s peak, {_cellMetres:0.#} m cells, {_periodSeconds:0.#} s period") +
                        (Rolls
                            ? _rollBlinkSeconds > 0f
                                ? FormattableString.Invariant($", rolls blinking every {_rollBlinkSeconds:0.#} s")
                                : ", steady rolls"
                            : "")) +
                  (AdvectFields ? ", advecting the fields" : "")) +
            (_ventSpeed > 0f
                ? FormattableString.Invariant($", vent {_ventSpeed:0.###} m/s in patch {_ventPatch}")
                : "");
    }

    /// <summary>
    /// Which field <see cref="CurrentField"/> is — see <see cref="CurrentField.Mode"/> for why
    /// there are two.
    /// </summary>
    public enum CurrentMode
    {
        /// <summary>
        /// D037's standing wave in depth and D066's rolls over D061's patches: a function of
        /// depth, time and patch index, and every run in the record. The default, so a recorded
        /// config replays.
        /// </summary>
        Rolls = 0,

        /// <summary>
        /// A three-dimensional, divergence-free, time-varying field over D077's whole box, drawn
        /// from the run's seed and sampled at a position.
        /// </summary>
        /// <remarks>
        /// It moves a body up, down and sideways, and it moves the grid's water the same way, so
        /// a clade that sits still is carried away from the cells it has drained. That is the
        /// answer to the ribbons of logbook/0083; the construction is written out in full, with
        /// its equations, on <c>CurrentField.TransportAt</c>.
        /// </remarks>
        Transport = 1,
    }
}
