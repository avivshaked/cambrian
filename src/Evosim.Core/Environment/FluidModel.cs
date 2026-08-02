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
        /// Drag force and torque on one box, in world space. Torque is about the box centre.
        /// </summary>
        /// <param name="halfExtents">Box half-extents, in metres.</param>
        /// <param name="rotation">Box orientation in world space.</param>
        /// <param name="velocity">Velocity of the box centre.</param>
        /// <param name="angularVelocity">Angular velocity, radians per second.</param>
        public static void BoxDrag(
            Float3 halfExtents,
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
            if (k <= 0f) return;

            int n = config.PanelsPerAxis < 1 ? 1 : config.PanelsPerAxis;

            for (int axis = 0; axis < 3; axis++)
            {
                int u = (axis + 1) % 3;
                int v = (axis + 2) % 3;

                float panelArea = 4f * halfExtents[u] * halfExtents[v] / (n * n);
                if (panelArea <= 0f) continue;

                for (int side = 0; side < 2; side++)
                {
                    float sign = side == 0 ? 1f : -1f;

                    Float3 normalLocal = AxisVector(axis, sign);
                    Float3 normal = rotation.Rotate(normalLocal);

                    for (int i = 0; i < n; i++)
                    {
                        // Panel centres, evenly spaced across the face.
                        float du = (2f * (i + 0.5f) / n - 1f) * halfExtents[u];

                        for (int j = 0; j < n; j++)
                            {
                            float dv = (2f * (j + 0.5f) / n - 1f) * halfExtents[v];

                            Float3 panelLocal =
                                normalLocal * halfExtents[axis]
                                + AxisVector(u, du)
                                + AxisVector(v, dv);

                            Float3 offset = rotation.Rotate(panelLocal);

                            // Velocity where this panel actually is. Sampling the face centre
                            // alone would report zero for a box spinning about one of its own
                            // axes, because a face centre moves perpendicular to its normal —
                            // and a limb flapping about its joint is precisely that case.
                            Float3 panelVelocity = velocity + Float3.Cross(angularVelocity, offset);

                            float normalSpeed = Float3.Dot(panelVelocity, normal);
                            if (normalSpeed <= 0f) continue; // trailing: no pressure drag

                            Float3 panelForce = normal * (-k * panelArea * normalSpeed * normalSpeed);

                            force += panelForce;
                            torque += Float3.Cross(offset, panelForce);
                        }
                    }
                }
            }
        }

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

        private static Float3 AxisVector(int axis, float sign)
        {
            switch (axis)
            {
                case 0: return new Float3(sign, 0f, 0f);
                case 1: return new Float3(0f, sign, 0f);
                default: return new Float3(0f, 0f, sign);
            }
        }
    }
}
