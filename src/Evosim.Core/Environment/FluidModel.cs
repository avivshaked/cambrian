namespace Evosim.Core
{
    /// <summary>
    /// Quadratic drag on a box moving through water — DESIGN.md §5.2.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Drag is accumulated <b>per face</b> rather than from a single whole-body area. §5.2
    /// specifies force against a cross-section "projected onto the velocity direction", and
    /// summing over faces is what that means once a part can rotate as well as translate.
    /// [C18 §2.2, p.5] arrived at the same scheme independently — a "simplified mesh-based
    /// quadratic drag model" using each facet's normal speed — so its reported limitations
    /// apply here directly.
    /// </para>
    /// <para>
    /// Doing it per face buys three things a single area term cannot:
    /// </para>
    /// <list type="bullet">
    /// <item><description>A flat part moving broadside generates far more force than the same
    /// part edge-on. DESIGN.md §5.2 is blunt about the stakes: get this wrong and oscillating
    /// limbs produce no net thrust and nothing ever swims.</description></item>
    /// <item><description>Angular drag comes out for free, because a rotating part's faces
    /// are moving even when its centre is not.</description></item>
    /// <item><description>Torque about the centre of mass falls out of the same sum, so a
    /// part turns as the water pushes on it unevenly.</description></item>
    /// </list>
    /// <para>
    /// <b>Only leading faces contribute.</b> Pressure drag acts on surfaces advancing into
    /// the fluid; counting trailing faces as well would double the force and, worse, would
    /// cancel the asymmetry that makes a paddle work.
    /// </para>
    /// <para>
    /// <b>This model is exploitable and has been exploited in print.</b> [U07 §3, p.5] found
    /// that a per-part reaction-force model and real hydrodynamics disagreed on an evolved
    /// creature's <i>direction of travel</i> — a sign flip, not a magnitude error — and that
    /// the search had found its gait <i>because</i> the model was wrong. See DESIGN.md §5.3
    /// and the champion validation harness in §5.4.
    /// </para>
    /// </remarks>
    public static class FluidModel
    {
        /// <summary>
        /// Drag force and torque on one part, in world space. Torque is about the part centre.
        /// </summary>
        /// <param name="panels">
        /// The part's panels, built once by <see cref="DragPanelSet.For"/> at development.
        /// </param>
        /// <param name="rotation">Orientation in world space.</param>
        /// <param name="velocity">Velocity of the part's centre.</param>
        /// <param name="angularVelocity">Angular velocity, radians per second.</param>
        /// <param name="config">Water density and drag coefficient — §5.2.</param>
        /// <param name="force">Out: world-space drag force on the part.</param>
        /// <param name="torque">Out: world-space torque about the part centre.</param>
        /// <remarks>
        /// <para>
        /// <b>Summed in the part's own frame, and that is an identity rather than an
        /// approximation.</b> The obvious way to write this rotates each panel's normal and centre
        /// into world space — two quaternion rotations per panel, so 48 for a 24-panel box, which
        /// measured as over half the arithmetic in the loop that was 88% of the step (§5A.9).
        /// </para>
        /// <para>
        /// None of it is necessary. A rotation preserves the dot product, so
        /// <c>dot(Rn, Rv) = dot(n, v)</c>; and <c>Rᵀ(ω × Rc) = (Rᵀω) × c</c>, so the panel's own
        /// velocity transforms just as cleanly. Rotating the two inputs in and the two results out
        /// is therefore <b>four rotations per part</b> instead of two per panel, and every
        /// intermediate quantity is the same number it was before, differing only in which basis
        /// it is written in. <c>DragEquivalenceTests</c> holds that against a transcription of the
        /// world-space version.
        /// </para>
        /// </remarks>
        public static void Drag(
            DragPanelSet panels,
            Quat rotation,
            Float3 velocity,
            Float3 angularVelocity,
            FluidConfig config,
            out Float3 force,
            out Float3 torque)
        {
            force = Float3.Zero;
            torque = Float3.Zero;

            float k = 0.5f * config.Density * config.DragCoefficient;
            if (k <= 0f || panels == null) return;

            Quat inverse = rotation.Conjugate;
            Float3 v = inverse.Rotate(velocity);
            Float3 w = inverse.Rotate(angularVelocity);

            Float3[] centres = panels.Centres;
            Float3[] normals = panels.Normals;
            float[] areas = panels.Areas;

            Float3 localForce = Float3.Zero;
            Float3 localTorque = Float3.Zero;

            for (int i = 0; i < areas.Length; i++)
            {
                Float3 centre = centres[i];

                // Velocity where this panel actually is. Sampling the part's centre alone would
                // report zero for a part spinning about one of its own axes, because a surface
                // point moves perpendicular to its normal — and a limb flapping about its joint
                // is precisely that case.
                Float3 panelVelocity = v + Float3.Cross(w, centre);

                Float3 normal = normals[i];
                float normalSpeed = Float3.Dot(panelVelocity, normal);
                if (normalSpeed <= 0f) continue; // trailing: no pressure drag

                Float3 panelForce = normal * (-k * areas[i] * normalSpeed * normalSpeed);

                localForce += panelForce;
                localTorque += Float3.Cross(centre, panelForce);
            }

            force = rotation.Rotate(localForce);
            torque = rotation.Rotate(localTorque);
        }

        private static readonly BoxShape Box = new BoxShape();

        /// <summary>
        /// Drag on a box. Convenience for tests and one-off calls — it builds panels every call.
        /// </summary>
        /// <remarks>
        /// The per-step path over a population must hold a <see cref="DragPanelSet"/> built once
        /// at development and pass it in. Rebuilding panels per part per step was the single
        /// largest term in the step (§5A.9), and it recomputed geometry that had been fixed since
        /// the phenotype was grown.
        /// </remarks>
        public static void BoxDrag(
            Float3 halfExtents,
            Quat rotation,
            Float3 velocity,
            Float3 angularVelocity,
            FluidConfig config,
            out Float3 force,
            out Float3 torque) =>
            Drag(DragPanelSet.For(Box, halfExtents, config.PanelsPerAxis),
                 rotation, velocity, angularVelocity, config, out force, out torque);

        /// <summary>
        /// Effective mass of a part once the water it drags along is included —
        /// <c>m * (1 + Ca * rho * V / m)</c>, i.e. <c>m + Ca * rho * V</c>.
        /// </summary>
        /// <remarks>
        /// Added mass is applied by inflating mass rather than as an explicit force. A force
        /// proportional to a body's own acceleration is an implicit term: integrated
        /// explicitly it feeds back on itself and the simulation blows up once the added mass
        /// approaches the real mass — which, for a neutrally buoyant creature, is exactly
        /// where it sits.
        ///
        /// The cost of doing it this way is that added mass becomes isotropic, while the real
        /// thing is strongly direction-dependent — a flat plate drags far more water
        /// broadside than edge-on, and that anisotropy is part of what makes flapping
        /// propulsion work. Recorded as a known limitation rather than fixed here.
        /// </remarks>
        public static float EffectiveMass(float mass, float volume, FluidConfig config) =>
            mass + config.AddedMassCoefficient * config.Density * volume;
    }
}
