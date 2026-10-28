using System;
using Evosim.Core;

namespace Evosim.Dynamics
{
    /// <summary>
    /// D255: a family limb's fin law. The constants the powered stroke's push reads for a jointed link whose part
    /// is a superellipsoid of the family (D233), written at a build and again at every resize.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One exact value for each degree of freedom, for life.</b> Under the powered stroke a degree's stroke
    /// direction and the limb's axis are fixed in the link's frame, so the added mass <c>M = ∫ ρπ/4 c² ds</c> of
    /// the part's true slices and its first moment <c>I1 = ∫ ρπ/4 c² s ds</c> about the joint are computed once,
    /// by <see cref="Superellipsoid.StrokeIntegrals(Float3, Double3, Double3, Double3, int, out double, out double)"/>.
    /// The step reads two doubles a degree (<see cref="Fluid.Apply(Creature, SolverConfig)"/>).
    /// </para>
    /// <para>
    /// <b>From the developed part's own quantities.</b> The integral is taken at the size the developer built the
    /// part at, from <see cref="FamilyGeometry.ReferenceHalfExtents"/> and the joint's place there
    /// (<see cref="FamilyGeometry.ReferenceChildAnchor"/>), and kept on the part's <see cref="FamilyGeometry"/>,
    /// which every scaled copy shares. A copy at length ratio s has M times s³ and I1 times s⁴
    /// (<see cref="PhenotypePart.FamilyScale"/>), in double. So a body built from a scaled phenotype and a body
    /// built at the adult and resized to it carry the same bits, as the panels do.
    /// </para>
    /// </remarks>
    public sealed partial class Creature
    {
        /// <summary>
        /// For each degree of freedom, indexed like <see cref="DofStart"/>: the added mass ρ·M of a family link's
        /// part moving along that degree's stroke direction at the body's present size, kg. 0 for a degree of a
        /// link that is not a family link. Null unless <see cref="SolverConfig.ShapedAddedMass"/> is on and a
        /// family part has a degree of freedom.
        /// </summary>
        public double[] FamilyAddedMass;

        /// <summary>For each degree of freedom: the first moment ρ·I1 of that added mass about the joint, kg·m. Null as <see cref="FamilyAddedMass"/> is.</summary>
        public double[] FamilyFirstMoment;

        /// <summary>
        /// The refusals of a family limb under the fin law, taken before anything is built. A family link with a
        /// parent under the swing push is refused: the swing's direction changes every step and the strip integral
        /// is taken along a fixed stroke.
        /// </summary>
        internal static void RefuseFamilyStroke(SolverConfig config, Phenotype phenotype)
        {
            if (!config.ShapedAddedMass) return;
            if (!(config.LimbPushCoefficient > 0 && !(config.PowerPushRadiansPerSecond > 0))) return;

            for (int i = 0; i < phenotype.PartCount; i++)
            {
                PhenotypePart part = phenotype.Parts[i];
                if (part.Family == null || part.IsRoot) continue;

                throw new ArgumentException(
                    "SolverConfig.ShapedAddedMass is on and part " + i + " is a family limb (D233) under the swing push " +
                    "(LimbPushCoefficient above 0 without the bend-and-power drive). The swing's direction changes every " +
                    "step and D255 computes a family limb's added mass once for each degree of freedom, along a fixed " +
                    "stroke, so only the power push is built (EVOSIM_SHAPED_ADDED_MASS, D255).");
            }
        }

        private static bool HasFamilyJoint(Phenotype phenotype)
        {
            for (int i = 0; i < phenotype.PartCount; i++)
            {
                PhenotypePart part = phenotype.Parts[i];
                if (part.Family != null && !part.IsRoot && part.JointType.DofCount() > 0) return true;
            }

            return false;
        }

        private void AllocateFamilyStroke()
        {
            FamilyAddedMass = new double[Dof];
            FamilyFirstMoment = new double[Dof];
        }

        /// <summary>
        /// Writes every family link's two numbers a degree of freedom, in place, at the size the phenotype now
        /// stands at. Call after <see cref="WriteSizedProperties"/>, whose joint frames it reads.
        /// </summary>
        private void WriteFamilyStroke()
        {
            Array.Clear(FamilyAddedMass, 0, Dof);
            Array.Clear(FamilyFirstMoment, 0, Dof);

            for (int i = 0; i < Links; i++)
            {
                PhenotypePart part = Phenotype.Parts[i];
                if (part.Family == null || Parent[i] < 0 || DofCount[i] == 0) continue;

                double scale = part.FamilyScale;
                double cubed = scale * scale * scale;
                double fourth = cubed * scale;

                QuatD frame = QuatD.Read(JointFrame, 4 * i);
                int start = DofStart[i];

                for (int d = 0; d < DofCount[i]; d++)
                {
                    Vec3 dofAxis = frame.Rotate(AxisOf(d));
                    part.Family.ReferenceStroke(
                        new Double3(dofAxis.X, dofAxis.Y, dofAxis.Z),
                        out double mass, out double moment);

                    FamilyAddedMass[start + d] = _config.Density * (mass * cubed);
                    FamilyFirstMoment[start + d] = _config.Density * (moment * fourth);
                }
            }
        }
    }
}
