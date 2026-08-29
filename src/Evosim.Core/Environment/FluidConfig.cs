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

        public static FluidConfig DragOnly => new FluidConfig();

        public FluidConfig Clone() => new FluidConfig
        {
            Density = Density,
            DragCoefficient = DragCoefficient,
            AddedMassCoefficient = AddedMassCoefficient,
            PanelsPerAxis = PanelsPerAxis,
            TissueExcessDensity = TissueExcessDensity,
        };

        public override string ToString() =>
            $"rho={Density} Cd={DragCoefficient} Ca={AddedMassCoefficient} panels={PanelsPerAxis}²";
    }
}
