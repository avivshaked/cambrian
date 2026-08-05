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
