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
        public Float3 VelocityAt(float heightY, double seconds, int patch, int patchCount)
        {
            Float3 flow = RollOrSteady(heightY, seconds, patch, patchCount);
            if (!VentActive(patchCount)) return flow;

            double depth = -(double)heightY;

            float w = (float)VentVertical(depth, patch, patchCount);
            float u = (float)VentHorizontal(depth, patch, patchCount);

            // Both terms already satisfy Z = -X, so their sum does; nothing here needs to restate
            // the convention.
            return new Float3(flow.X + u, flow.Y + w, flow.Z - u);
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

        public override string ToString() =>
            (_speed <= 0f
                ? "still water"
                : $"{_speed:0.###} m/s peak, {_cellMetres:0.#} m cells, {_periodSeconds:0.#} s period" +
                  (Rolls
                      ? _rollBlinkSeconds > 0f
                          ? $", rolls blinking every {_rollBlinkSeconds:0.#} s"
                          : ", steady rolls"
                      : "") +
                  (AdvectFields ? ", advecting the fields" : "")) +
            (_ventSpeed > 0f ? $", vent {_ventSpeed:0.###} m/s in patch {_ventPatch}" : "");
    }
}
