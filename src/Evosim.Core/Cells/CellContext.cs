namespace Evosim.Core
{
    /// <summary>
    /// Everything a cell may use to acquire energy in one step — DESIGN.md §5A.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This type exists so that <see cref="CellType.Acquire"/> can be written and tested in
    /// <c>Evosim.Core</c>, which has no <c>UnityEngine</c> (§6.1). The simulator fills it in
    /// from the real world each step; a unit test fills it in by hand. Without it, every
    /// feeding rule would need a physics engine to exercise, and the energy economy is
    /// precisely the part that must not be judged by eye.
    /// </para>
    /// <para>
    /// Fields describe the environment <i>at one part</i>, not the creature. A photosynthetic
    /// cell on a creature's underside sees a different irradiance from one on its back, and
    /// that difference is the whole reason morphology should matter to feeding.
    /// </para>
    /// </remarks>
    public readonly struct CellContext
    {
        /// <summary>Length of the step being accounted for, in seconds.</summary>
        public float Seconds { get; }

        /// <summary>The part's volume, m³. Drives upkeep and, for absorption, how much water it sweeps.</summary>
        public float Volume { get; }

        /// <summary>
        /// Actuation capacity of this part, in newton-metres — meaningful only for links.
        /// </summary>
        /// <remarks>
        /// The peak torque the link may exert, on <i>both</i> sides: a joint torque is a pair,
        /// equal and opposite, or it manufactures angular momentum (§11.2).
        ///
        /// It is charged for whether or not it is used. Cost-on-use alone is not a constraint —
        /// a link that idles most of the time would pay almost nothing for being enormous, so
        /// evolution would take the largest capacity available and use it occasionally. Muscle
        /// costs to maintain whether or not it contracts.
        /// </remarks>
        public float Power { get; }

        /// <summary>Degrees of freedom this part's joint has. Zero for anything rigid.</summary>
        /// <remarks>
        /// Charged for, because a spherical joint is three actuators and a hinge is one. Without
        /// this, degrees of freedom are free and every link evolves to the most permissive joint
        /// type on offer.
        /// </remarks>
        public int Dof { get; }

        /// <summary>
        /// Surface area able to receive light, m². Not the full box area: a face pointing away
        /// from the light, or shadowed by the creature's own body, contributes nothing.
        /// </summary>
        public float LitArea { get; }

        /// <summary>Irradiance at this part, W/m². Falls with depth and with the light cycle (§5A.4).</summary>
        public float Irradiance { get; }

        /// <summary>Energy density of nutrients in the water here, J/m³.</summary>
        public float NutrientDensity { get; }

        /// <summary>
        /// Tissue this part is touching and could feed on, or <c>null</c>. Set by the
        /// simulator from contact; the yield depends on what kind of tissue it is, which is
        /// how herbivory and carnivory come apart without needing separate body types (§5A.3).
        /// </summary>
        public TissueContact Contact { get; }

        public CellContext(
            float seconds,
            float volume,
            float litArea = 0f,
            float irradiance = 0f,
            float nutrientDensity = 0f,
            TissueContact contact = null,
            float power = 0f,
            int dof = 0)
        {
            Seconds = seconds;
            Volume = volume;
            Power = power;
            Dof = dof;
            LitArea = litArea;
            Irradiance = irradiance;
            NutrientDensity = nutrientDensity;
            Contact = contact;
        }
    }

    /// <summary>
    /// What one cell took in over a step, and where from — DESIGN.md §5A.2, §5A.2c.
    /// </summary>
    /// <remarks>
    /// <b>The distinction is a conservation law, not bookkeeping.</b> Sunlight is the world's only
    /// primary input, so light is energy that did not exist a moment ago; everything else was
    /// taken from a pool or a body that must lose exactly as much. A cell reporting one total
    /// would leave the world unable to tell which, and §5A.2's audit closes only because it can.
    /// </remarks>
    public readonly struct CellIntake
    {
        /// <summary>Joules converted from light. New energy.</summary>
        public float FromLight { get; }

        /// <summary>Joules the cell <i>kept</i> from eating. Somebody's loss.</summary>
        public float FromPool { get; }

        /// <summary>
        /// Joules removed from the pool to yield <see cref="FromPool"/>. Never less than it.
        /// </summary>
        /// <remarks>
        /// <b>The gap between the two is what makes a food chain lose energy at every level.</b>
        /// A consumer keeps only a fraction of what it takes (§5A.3), and the remainder is
        /// destroyed rather than left behind — so the world must remove the larger figure and
        /// account the difference as an outflow. Reporting only what was kept would leave that
        /// difference sitting in the pool, quietly turning every meal into a partial refund and
        /// a food chain into a perpetual motion machine.
        /// </remarks>
        public float PoolDrawn { get; }

        public CellIntake(float fromLight, float fromPool, float poolDrawn)
        {
            FromLight = fromLight;
            FromPool = fromPool;
            PoolDrawn = poolDrawn < fromPool ? fromPool : poolDrawn;
        }

        public float Total => FromLight + FromPool;

        /// <summary>Joules lost in the transfer — taken from the world, kept by nobody.</summary>
        public float Wasted => PoolDrawn - FromPool;

        /// <summary>A cell that took nothing in.</summary>
        public static CellIntake None => default;

        /// <summary>Photosynthesis and nothing else.</summary>
        public static CellIntake Light(float joules) => new CellIntake(joules, 0f, 0f);

        /// <summary>Feeding with no loss on transfer — filtering, where nothing is torn up.</summary>
        public static CellIntake Food(float joules) => new CellIntake(0f, joules, joules);

        /// <summary>Feeding that keeps <paramref name="yield"/> of what it takes.</summary>
        public static CellIntake Food(float drawn, float yield) =>
            new CellIntake(0f, drawn * yield, drawn);

        public static CellIntake operator +(CellIntake a, CellIntake b) =>
            new CellIntake(
                a.FromLight + b.FromLight, a.FromPool + b.FromPool, a.PoolDrawn + b.PoolDrawn);

        public override string ToString() =>
            $"light {FromLight:0.###} J, food {FromPool:0.###} J of {PoolDrawn:0.###} drawn";
    }

    /// <summary>Tissue in contact with a feeding cell — DESIGN.md §5A.3.</summary>
    public sealed class TissueContact
    {
        /// <summary>What kind of cell the tissue is. Never null.</summary>
        public CellType Type { get; }

        /// <summary>Energy still in the tissue, J. A feeder cannot take more than this.</summary>
        public float AvailableJoules { get; }

        /// <summary>
        /// False for carrion. Dead tissue does not resist, which is what lets a consumer cell
        /// pay its way before perception exists and so survive long enough to become a
        /// predator (§5A.3).
        /// </summary>
        public bool IsAlive { get; }

        public TissueContact(CellType type, float availableJoules, bool isAlive)
        {
            Type = type;
            AvailableJoules = availableJoules;
            IsAlive = isAlive;
        }
    }
}
