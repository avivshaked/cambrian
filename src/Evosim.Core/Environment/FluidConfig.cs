namespace Evosim.Core
{
    /// <summary>
    /// Fluid constants for the water environment — DESIGN.md §5.2. Part of the config hash (§7).
    /// </summary>
    public sealed class FluidConfig
    {
        /// <summary>Density in kg/m³. Fresh water is 1000.</summary>
        [Tunable("fluid")]
        public float Density { get; set; } = 1000f;

        /// <summary>
        /// Quadratic drag coefficient. [C18 §2.2, p.5] uses C_d = 1.5 for its mesh-based
        /// drag model, which is the same scheme as this one.
        /// </summary>
        [Tunable("fluid")]
        public float DragCoefficient { get; set; } = 1.5f;

        /// <summary>
        /// How finely each box face is divided when integrating drag: <c>n</c> gives
        /// <c>n × n</c> panels per face, so <c>6n²</c> samples per part per step.
        /// </summary>
        /// <remarks>
        /// One sample per face is not enough, and not by a small margin. A face centre moves
        /// perpendicular to its own normal when the box spins about one of its own axes, so
        /// single-point sampling reports <b>zero</b> drag for that motion — and a limb
        /// flapping about its joint is exactly that motion. Subdividing recovers it, because
        /// points away from the axis do have normal-direction velocity.
        ///
        /// 2 is the cheapest value that works. Raising it refines the pressure distribution
        /// and costs linearly in <c>n²</c>; DESIGN.md §6.4's throughput budget is the
        /// constraint, and this is the term most likely to consume it.
        /// </remarks>
        [Tunable("fluid")]
        public int PanelsPerAxis { get; set; } = 2;

        /// <summary>
        /// Added-mass coefficient: the fluid a part drags along with it, as a multiple of the
        /// mass of water it displaces.
        /// </summary>
        /// <remarks>
        /// <para>
        /// DESIGN.md §5.4 makes this <b>the highest-value single improvement to the fluid
        /// model</b>, and for a reason that is not about physical accuracy. [C18 §4, p.28]
        /// reports that omitting it precludes fish-like creatures and, because pulsed-jetting
        /// cannot be predicted without it, squid-like ones too — leaving "organisms vaguely
        /// resembling medusoids and morphologically similar among themselves." The cheap
        /// model does not merely get the physics wrong; it collapses the morphological
        /// variety that is this project's entire point.
        /// </para>
        /// <para>
        /// Zero here means drag only. It is applied by inflating effective mass rather than as
        /// an explicit force, because a force proportional to measured acceleration is an
        /// implicit term and integrating it explicitly is unstable.
        /// </para>
        /// </remarks>
        [Tunable("fluid")]
        public float AddedMassCoefficient { get; set; }

        /// <summary>
        /// How much of the water's own acceleration a part feels — the second term of the Morison
        /// equation, at 1 the physical value. 0 is drag alone, which is every run on file.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>What it is.</b> A parcel of water on a curved streamline is held on it by the
        /// pressure gradient across the flow, and a body immersed in that water feels the same
        /// gradient as a force <c>(ρV + m_added)·Du/Dt</c> — the displaced mass plus the added
        /// mass's share, times the water's own acceleration along its path
        /// (<see cref="CurrentField.AccelerationAt(float, float, float, double)"/>). With it a
        /// neutrally buoyant body follows the water to first order in its response time: it is a
        /// tracer. §5.2 has the drag and D081 has the added mass, and until
        /// <c>logbook/specs/water-carries-spec.md</c> (2026-09-12) nothing tied a body to the
        /// water but drag.
        /// </para>
        /// <para>
        /// <b>Why it was built.</b> Drag alone makes a tank a centrifuge. A body pulled toward the
        /// water's velocity by drag and nothing else fails to turn as sharply as the water does,
        /// and drifts outward at about <c>τ·u_θ²/r</c> per second — averaged over a disc, outward
        /// for any current with any azimuthal motion in it at all
        /// (<c>StreamsTests.ABodyRidesTheWater</c> carries the identity and the readings). At the
        /// campaign's body size and water speed that is centimetres a second: the whole population
        /// at the glass within an hour of simulated time, which is what round 37 was.
        /// </para>
        /// <para>
        /// <b>It moves no joule of the economy.</b> §5A's books count food, upkeep and the work a
        /// creature's own joints do; the water's mechanical work on a body is not one of their
        /// terms, so the audit closes exactly as it did — the same exemption
        /// <see cref="TissueExcessDensity"/> and the current itself already carry, and for the same
        /// reason. Nothing charges for it and nothing credits it:
        /// <c>FluidEnvironment.DissipatedJoules</c>, which is the only energy this class
        /// integrates, is the drag force alone, and this term is carried in an array of its own so
        /// that it cannot reach it.
        /// </para>
        /// <para>
        /// ⚠ <b>Mechanically it is an external force, like every other term in this class that is
        /// not drag.</b> A body riding a current exchanges kinetic energy with the water, so
        /// DESIGN §11.2's momentum check and <c>Milestone1Smoke</c>'s mechanical energy audit are
        /// invalidated by a nonzero value exactly as they are by
        /// <see cref="TissueExcessDensity"/>, <see cref="BuoyancyCell"/>'s lift and a moving
        /// current. They are run at 0, which is the default and what every harness sets. There is
        /// still no free lunch: the pressure gradient is the water's, not the creature's, and a
        /// creature cannot make the water accelerate — no effector channel reaches it.
        /// </para>
        /// <para>
        /// ⚠ Default 0, so every recorded config replays the world it ran: this is a per-step
        /// force, and any nonzero value is a new chaotic realisation of every seed (CLAUDE.md's
        /// butterfly rule). 1 is the physical value and the campaign's; values between are not
        /// physics but a dial on how much of it a round carries.
        /// <c>EVOSIM_FLUID_ACCEL</c> in the header.
        /// </para>
        /// </remarks>
        [Tunable("fluid")]
        public float FluidAccelerationCoefficient { get; set; }

        /// <summary>
        /// The push a swinging limb gets from the water it throws, as a fraction of
        /// <c>m_a · r · ω⊥²</c> (propose-foraging.md, the owner's rule of 2026-09-29).
        /// </summary>
        /// <remarks>
        /// <para>
        /// Every link with a parent feels a force along its own axis, from the link toward its
        /// joint, of <c>C_T · m_a · r · ω⊥²</c>: <c>m_a</c> the link's added mass
        /// (<see cref="AddedMassCoefficient"/> · ρ · V), <c>r</c> the distance from the joint to the
        /// link's origin, and <c>ω⊥</c> the link's angular velocity relative to its parent,
        /// perpendicular to that axis. The force is never negative and always along the limb, so
        /// over a symmetric wag the sideways parts cancel and the part along the limb's mean
        /// direction adds up, and a limb turned through full circles gains nothing. Drag, unchanged,
        /// takes the speed back. It is applied at the link's origin along the line through the
        /// joint, so it adds no torque about the link.
        /// </para>
        /// <para>
        /// ⚠ Default 0, so every recorded config replays the world it ran: this is a per-step
        /// force, and any nonzero value is a new realisation of every seed. It is not physics; it is
        /// a rule standing in for the reactive force a drag-only fluid lacks, and its value is set
        /// on the bench so that a reference tail-wagger swims at 4 to 8 cm/s.
        /// <c>EVOSIM_LIMB_PUSH</c>; the header prints <c>limbPush</c> when it is above 0.
        /// </para>
        /// </remarks>
        [Tunable("fluid")]
        public float LimbPushCoefficient { get; set; }

        /// <summary>
        /// D229, the fin law: a stroking limb's added mass follows its shape and the direction it
        /// moves, instead of <see cref="AddedMassCoefficient"/> · ρ · V.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Off, both limb pushes (the swinging limb's and the powered stroke's) use the volume
        /// formula and a flat blade pushes no more than a round stick of equal volume. On, each
        /// push takes the part's own textbook added mass for the direction of its motion: a box
        /// takes the plate formula ρ·π/4·w²·L, with <c>L</c> its extent along the limb and <c>w</c>
        /// its extent across both the limb and the motion; a capsule takes the strip formula on its
        /// cylinder plus its two ends as a sphere, whatever the direction; a sphere takes ½·ρ·V,
        /// which is today's number at C_a = 0.5. Nothing else in the push changes, and the work
        /// the powered stroke books follows the push, so a wider fin pays more by itself.
        /// </para>
        /// <para>
        /// ⚠ Default false, so every recorded config replays the world it ran. A world with the
        /// push on and this on is a new realisation of every seed. <c>EVOSIM_SHAPED_ADDED_MASS</c>.
        /// </para>
        /// </remarks>
        [Tunable("fluid")]
        public bool ShapedAddedMass { get; set; }

        /// <summary>
        /// D231: a stroke pushes its whole assembly. The powered stroke's push (D135, D229) falls on
        /// the jointed link's own part alone; with this on, a part welded beyond it moves water too.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A jointed link's assembly is the link plus every link beyond it reached through welds
        /// only, up to the next jointed link. Each part is projected onto the limb's axis and onto
        /// the direction across the stroke, and the added mass is the plate formula integrated in
        /// strips along the axis, <c>M = ∫ ρπ/4 c² ds</c> with <c>c(s)</c> the envelope of the parts
        /// that cover <c>s</c>. The push acts at the moved water, <c>I1 = ∫ ρπ/4 c² s ds</c>, and the
        /// work booked follows it. A link with nothing welded beyond it takes D229's own lines.
        /// </para>
        /// <para>
        /// Requires <see cref="ShapedAddedMass"/> and the bend-and-power drive: the swing push's
        /// direction changes every step, so it is refused. ⚠ Default false, so every recorded config
        /// replays the world it ran; a world with the push on and this on is a new realisation of
        /// every seed with a welded part beyond a joint. <c>EVOSIM_STROKE_ASSEMBLY</c>.
        /// </para>
        /// </remarks>
        [Tunable("fluid")]
        public bool StrokePushesAssembly { get; set; }

        /// <summary>
        /// The bend-and-power drive's stroke rate, Ω in rad/s (propose-joint-drive.md, the owner's
        /// ruling of 2026-09-30). 0 runs the torque drive every recorded world ran.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Above 0, each degree of freedom takes two outputs from the brain: neuron <i>d</i> is its
        /// power and neuron <i>n + d</i> its bend. The power is the neuron's positive half, off at
        /// or below the joint's threshold gene, so a silent neuron gives none; the bend is read from
        /// [-1, 1] onto [0, 1], so a silent neuron centres the joint. The bend is held by a muscle
        /// (<see cref="PoseHertz"/>, capped by the part's Power); the stroke is not simulated.
        /// A link whose joint has power <i>s</i> is pushed as <see cref="LimbPushCoefficient"/>'s
        /// rule pushes a link swinging at <c>ω⊥ = s·Ω</c>: <c>C_T · m_a · r · (s·Ω)²</c> along its
        /// axis toward its joint, each degree of freedom weighted by how far its axis stands from
        /// the limb's (a twist about the limb's own length gives none). The push's work,
        /// <c>F · r · s · Ω</c>, is booked as the drive's work, so the work cost prices it.
        /// </para>
        /// <para>
        /// ⚠ Default 0, so every recorded config replays the world it ran. Set on the bench so that
        /// the reference swimmer at full power swims 4 to 8 cm/s. <c>EVOSIM_POWER_PUSH</c>; the
        /// header prints <c>powerPush</c> when it is above 0.
        /// </para>
        /// </remarks>
        [Tunable("fluid")]
        public float PowerPushRadiansPerSecond { get; set; }

        /// <summary>
        /// How quickly the bend-and-power drive's muscle settles a joint on its bend, as the
        /// natural frequency of a critically damped spring, Hz. Read only when
        /// <see cref="PowerPushRadiansPerSecond"/> is above 0; at 0 the bend is not held.
        /// </summary>
        /// <remarks>
        /// The spring's stiffness and damping are this times the link's smallest principal
        /// inertia, so a heavy limb bends as slowly as a light one in time and no more strongly;
        /// its torque is capped by the part's Power. <c>EVOSIM_POSE_HZ</c>.
        /// </remarks>
        [Tunable("fluid")]
        public float PoseHertz { get; set; }

        /// <summary>Water, with drag only. The state DESIGN.md §5.4 warns is not enough.</summary>
        /// <summary>
        /// How much denser than the water a creature's tissue is, kg/m³. 0 is neutral buoyancy.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Neutral buoyancy is a rare state that organisms spend energy to maintain, and §5.2
        /// hands it out for free.</b> With gravity disabled a creature stays exactly where it was
        /// born, so doing nothing is not merely cheap but optimal, and the population reaches the
        /// best depth by differential survival rather than by swimming (D036, logbook/0027). Most
        /// cells are denser than seawater; flagella in phytoplankton are largely anti-sinking
        /// machinery, and holding station is the oldest thing motility is for.
        /// </para>
        /// <para>
        /// <b>An excess density and not a sink rate</b>, because the rate is what the physics
        /// should decide: a body sinks until §5.2's drag balances its excess weight, so shape
        /// pays exactly as it does for swimming, and a flat body that collects more light also
        /// sinks more slowly. Setting a velocity directly would make sinking a property of the
        /// world rather than of the creature, and would hand every body the same answer.
        /// </para>
        /// <para>
        /// ⚠ Unmeasured (§5A.10), and not transferable from biology: diatoms run 5–75 kg/m³ over
        /// seawater and sink at ~1 m/day, but they are microscopic and in Stokes flow, while these
        /// bodies are tenths of a metre in quadratic drag. Calibrate against the observable — the
        /// terminal sink rate — and not against a real organism's density. <b>The ceiling is what
        /// a joint can push against: 0.017 m/s for a founder-shaped body at 20 N·m</b>
        /// (logbook/0027). Above that nothing holds station and the world drowns.
        /// </para>
        /// <para>
        /// ⚠ <b>Non-zero values invalidate §11.2's momentum check by construction.</b> That check
        /// asserts that with no gravity, drag or contact nothing external acts on a creature, so
        /// internal forces alone cannot move its centre of mass — and this is an external force.
        /// It is a real exemption and not an oversight: the check must be run at 0, which is the
        /// default and is what every harness uses. The metabolic audit is unaffected, because
        /// sinking moves a creature without moving a joule — buoyancy does mechanical work and the
        /// economy of §5A counts food, so there is no free lunch to find here.
        /// </para>
        /// <para>
        /// <b>D049 puts a second force under the same exemption, with the opposite sign.</b>
        /// <see cref="BuoyancyCell"/> lets a part cancel some of its own weight, and
        /// <c>FluidEnvironment</c> nets the two into one term — a part that lifts more than it
        /// weighs rises. The momentum check is invalidated by a creature carrying lift for exactly
        /// the same reason and must likewise be run without one. There is still no free lunch: lift
        /// is billed per unit held, every step, whether or not it is buying anything.
        /// </para>
        /// <para>
        /// ⚠ The older note here said "going back up still costs, and up is where the light is."
        /// Only the first half survives D048. Up is where the light is and <b>down is where the
        /// matter is</b>, and reproduction now needs both — which is what makes depth a trade
        /// rather than a climb, and what gives a buoyancy cell something to be for.
        /// </para>
        /// </remarks>
        [Tunable("fluid")]
        public float TissueExcessDensity { get; set; }

        /// <summary>
        /// D064. Body volume at which tissue is neutrally buoyant, m³. 0 is off — every body feels
        /// the full <see cref="TissueExcessDensity"/>, as it did before D064.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Above 0, the excess density a creature feels is scaled by
        /// <see cref="BuoyancyModel.ExcessDensityFactor"/>: 0 at or below this volume, 0.75 at
        /// eight times it, and converging to 1 for a large body. Sinking becomes something a body
        /// grows into rather than something every body is born with, which is what a water column
        /// does and what §5.2's constant does not.
        /// </para>
        /// <para>
        /// ⚠ Unmeasured, like the density it scales, and the exponent behind it is inference rather
        /// than a cited result — see <see cref="BuoyancyModel"/>. Calibrate against founder volume:
        /// this is denominated in the same m³ that <c>Phenotype.TotalVolume</c> reports, so a value
        /// near a founder's volume makes generation zero neutral and leaves growth to pay for
        /// itself.
        /// </para>
        /// </remarks>
        [Tunable("fluid", Unit = "m3")]
        public float NeutralBodyVolume { get; set; }

        /// <summary>
        /// D077. How hard the world pushes a body back at the top and the bottom, as a fraction
        /// of the body's own weight. 1 is a body out of the water falling at g; 0 is D050's clamp
        /// exactly — every run before D077, bit for bit.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The ocean's top was a ratchet and this is the return leg.</b> D050 stopped a buoyant
        /// body's <i>upward</i> net force at y = 0, which keeps a floater at the waterline but does
        /// nothing to a body that arrived above it with momentum of its own — the vent's plume
        /// carried round 24's populations over the line and nothing brought them back
        /// (logbook/0061). Above y = 0 this gives such a body <c>f ×</c> its full weight
        /// downward — <i>mass × g</i>, with no buoyancy term at all, because above the waterline
        /// there is no water to displace and the upthrust that makes a body near-weightless in
        /// this world is simply gone. Drag still damps the fall (§5.2), so what it reaches is a
        /// terminal speed of order a metre a second rather than free fall. At y = 0 exactly the
        /// clamp is D050's, so a floating body still floats and the rule is continuous with the
        /// one it extends.
        /// </para>
        /// <para>
        /// <b>The first build read the rule as the founder <i>sink</i> rate and it was too weak
        /// to work.</b> That is about 2 mm/s; D067's vent lifts at 50 mm/s, twenty-eight times
        /// harder, and the arm measured 1,634 of 2,245 creatures still above the line at
        /// t = 3,000 with the rule on. Ruled by the owner on 2026-09-05: the strength was the
        /// spec's error, not the measurement's.
        /// </para>
        /// <para>
        /// <b>The fraction governs the surface only where the world has a real sea bed.</b> Until
        /// 2026-09-06 the bottom got the surface's rule with its sign flipped: a body under
        /// <c>-WorldDepthMetres</c> was pushed up by <c>f ×</c> its weight, which kept it in the
        /// world and made the floor a stiff bouncer rather than a bed. That was named a
        /// placeholder in D077's rule-4 amendment and it cost what a spring costs — a founder
        /// drawn on the floor bounced at ~1 m/s in the first seconds (so <c>bestSpeed</c> at
        /// founding was not locomotion), and three newborns at 60 m in <c>r25q-s2</c> diverged
        /// outright (logbook/0065, <c>runs/r25q-s2/*/diverged/</c>). Under
        /// <see cref="RunConfig.SharedSpace"/> the floor is now a static collider
        /// (<c>Evosim.Sim.SeaFloor</c>) and the mirror is gone: a body rests on rock rather than
        /// being thrown off it. In a tiled world there is no collider — nothing there has a
        /// position to collide at — so the mirror is still what keeps a sinking body in the world,
        /// and it is unchanged.
        /// </para>
        /// <para>
        /// <b>Weight, and not the size-scaled excess a body actually feels in water.</b>
        /// <see cref="NeutralBodyVolume"/> makes a small body neutral, and a restoring force
        /// scaled that way would be zero for exactly the founder-sized bodies that most need
        /// bringing back — the surface would still be a ratchet for everything below the neutral
        /// volume. This is the world's rule about its own boundary, not a property of the body
        /// at it.
        /// </para>
        /// <para>
        /// ⚠ Unmeasured (§5A.10).
        /// <c>EVOSIM_SURFACE_RESTORE</c> in the header. It is a separate knob from
        /// <see cref="RunConfig.SharedSpace"/> deliberately: the boundary is a vertical rule and
        /// can be read alone.
        /// </para>
        /// </remarks>
        [Tunable("fluid")]
        public float SurfaceRestoringFraction { get; set; }

        /// <summary>
        /// How long a creature holds one sample of the water before taking another, seconds. 0 is
        /// every run on file: the water is sampled afresh at every link on every physics step.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>It is a cost, not a force.</b> The profile of 2026-09-19
        /// (<c>logbook/specs/harness-profile-spec.md</c> §6) measured the gather loop's water
        /// samples — <see cref="CurrentField.VelocityAt(float, float, float, double)"/> at every
        /// link on every step, and <see cref="CurrentField.AccelerationAt(float, float, float, double)"/>
        /// beside it when <see cref="FluidAccelerationCoefficient"/> is on — at 45% of the fluid
        /// pass and about 16% of the whole wall clock. Above 0 the water is sampled once per
        /// creature, at its root link, and every link of that creature uses the held values until
        /// the hold expires.
        /// </para>
        /// <para>
        /// <b>What it costs in physics is the spread of the body across the field.</b> A body is
        /// tenths of a metre and the current varies on the scale of the box, so the water at a limb
        /// and the water at the root differ by little; the hold trades that difference, and the
        /// staleness within one metabolic step, for the samples. The ruling (owner, 2026-09-19)
        /// is one sample per body per metabolic step.
        /// </para>
        /// <para>
        /// ⚠ Default 0, so every recorded config replays the world it ran: this changes a per-step
        /// force, and any value above 0 is a new chaotic realisation of every seed (CLAUDE.md's
        /// butterfly rule). <c>EVOSIM_WATER_HOLD</c> in the header, which reads
        /// <c>water held 0.5 s</c> or <c>water per link</c>.
        /// </para>
        /// </remarks>
        [Tunable("fluid", Unit = "s")]
        public float WaterHoldSeconds { get; set; }

        /// <summary>
        /// The drag on turning (D161), and which of its two forms the water carries.
        /// <see cref="TurningDragForm.None"/> is every recorded world: the pressure drag alone,
        /// which acts only on the part of a panel's velocity along its normal.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Both forms act only on a part whose origin is in the water (<c>y &lt;= 0</c>), as the limb
        /// pushes and D111's torque do, and both are drag: they are summed with the pressure drag
        /// before the dissipation ledger and the drag limiter, so the energy audit counts them and
        /// the limiter caps them. <see cref="TurningDragForm.Skin"/> reads
        /// <see cref="SkinFrictionCoefficient"/> and <see cref="TurningDragForm.Spin"/> reads
        /// <see cref="SpinDragCoefficient"/>.
        /// </para>
        /// <para>
        /// ⚠ Default <c>None</c>, so every recorded config replays the world it ran. D161 leaves the
        /// form and its size to a nursery check before round 59's pre-registration.
        /// <c>EVOSIM_TURNING_DRAG</c>; the header prints <c>turning</c> when it is not <c>None</c>.
        /// </para>
        /// </remarks>
        [Tunable("fluid")]
        public TurningDragForm TurningDrag { get; set; } = TurningDragForm.None;

        /// <summary>
        /// The skin form's coefficient, C_f: each panel feels a shear of ½ρ·C_f·|t|·t per unit area
        /// against <i>t</i>, its velocity through the water along its own face. Read only under
        /// <see cref="TurningDragForm.Skin"/>.
        /// </summary>
        /// <remarks>
        /// The shear acts on every panel, leading and trailing, so it brakes a part's sliding along
        /// its faces as well as its turning. <c>EVOSIM_SKIN_FRICTION</c>.
        /// </remarks>
        [Tunable("fluid")]
        public float SkinFrictionCoefficient { get; set; }

        /// <summary>
        /// The spin form's coefficient, C_s: a part spinning at ω feels, about each of its own three
        /// axes <i>k</i>, a torque of ½ρ·C_s·M_k·|ω|·ω_k against its spin, where M_k sums each
        /// panel's area times the cube of its distance from that axis through the part's centre.
        /// Read only under <see cref="TurningDragForm.Spin"/>.
        /// </summary>
        /// <remarks>
        /// It is the skin form's answer for a part that only spins, if every panel's motion lay in
        /// its face, so at equal coefficients the two forms brake a pure spin alike to within the
        /// panels' normal motion, which the pressure drag already takes. Unlike the skin form it
        /// leaves a part's sliding alone. <c>EVOSIM_SPIN_DRAG</c>.
        /// </remarks>
        [Tunable("fluid")]
        public float SpinDragCoefficient { get; set; }

        public static FluidConfig DragOnly => new FluidConfig();

        public FluidConfig Clone() => new FluidConfig
        {
            Density = Density,
            DragCoefficient = DragCoefficient,
            AddedMassCoefficient = AddedMassCoefficient,
            FluidAccelerationCoefficient = FluidAccelerationCoefficient,
            LimbPushCoefficient = LimbPushCoefficient,
            ShapedAddedMass = ShapedAddedMass,
            StrokePushesAssembly = StrokePushesAssembly,
            PowerPushRadiansPerSecond = PowerPushRadiansPerSecond,
            PoseHertz = PoseHertz,
            PanelsPerAxis = PanelsPerAxis,
            TissueExcessDensity = TissueExcessDensity,
            NeutralBodyVolume = NeutralBodyVolume,
            SurfaceRestoringFraction = SurfaceRestoringFraction,
            WaterHoldSeconds = WaterHoldSeconds,
            TurningDrag = TurningDrag,
            SkinFrictionCoefficient = SkinFrictionCoefficient,
            SpinDragCoefficient = SpinDragCoefficient,
        };

        public override string ToString() =>
            System.FormattableString.Invariant(
                $"rho={Density} Cd={DragCoefficient} Ca={AddedMassCoefficient} panels={PanelsPerAxis}²");
    }

    /// <summary>
    /// The drag on turning's form (D161, <see cref="FluidConfig.TurningDrag"/>). Serialized by name.
    /// </summary>
    public enum TurningDragForm
    {
        /// <summary>No drag on turning: every recorded world.</summary>
        None,

        /// <summary>A shear on each panel's velocity along its face.</summary>
        Skin,

        /// <summary>A torque on each part against its own spin, and nothing on its sliding.</summary>
        Spin,
    }
}
