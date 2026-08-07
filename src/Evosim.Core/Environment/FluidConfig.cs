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
        public static FluidConfig DragOnly => new FluidConfig();

        public FluidConfig Clone() => new FluidConfig
        {
            Density = Density,
            DragCoefficient = DragCoefficient,
            AddedMassCoefficient = AddedMassCoefficient,
            PanelsPerAxis = PanelsPerAxis,
        };

        public override string ToString() =>
            $"rho={Density} Cd={DragCoefficient} Ca={AddedMassCoefficient} panels={PanelsPerAxis}²";
    }
}
