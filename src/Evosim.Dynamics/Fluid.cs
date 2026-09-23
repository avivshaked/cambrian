using Evosim.Core;

namespace Evosim.Dynamics
{
    /// <summary>
    /// The water, ported term for term from <c>Evosim.Sim.FluidEnvironment</c> and
    /// <c>Evosim.Core.FluidModel</c>: panel drag, the drag impulse limiter, added mass,
    /// the water's own acceleration, buoyancy with D064's size scaling and D049's lift, and
    /// D050/D077's surface rule; and D111's buoyancy offset, which is the farm's alone (the Unity
    /// farm does not bind it).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Added mass is not here</b>, because it is not a force. <c>FluidModel.EffectiveMass</c>
    /// folds it into the link's mass, and <see cref="Creature"/> does the same thing once, at
    /// build.
    /// </para>
    /// <para>
    /// <b>The drag sum is taken in the part's own frame</b>, exactly as <c>FluidModel.Drag</c>
    /// takes it, and for the same reason: a rotation preserves a dot product, so rotating the
    /// two inputs in and the two results out is four rotations per part instead of two per
    /// panel. What differs from the farm is that the velocity arrives in the part's frame
    /// already — the solver carries it there — so two of the four rotations are saved.
    /// </para>
    /// </remarks>
    public static class Fluid
    {
        /// <summary>
        /// Drag, the water's acceleration force and buoyancy on every link of one body, summed
        /// into its <c>Fext</c>. The water is sampled by the caller into <c>Water</c>.
        /// </summary>
        public static void Apply(Creature body, SolverConfig config)
        {
            double k = 0.5 * config.Density * config.DragCoefficient;
            bool limit = config.DragLimiterEngages;
            double dt = config.StepSeconds;

            // D064, once per creature: the excess density a body feels is scaled by its whole
            // volume, so sinking is something a lineage grows into.
            double excess = config.TissueExcessDensity;
            if (config.NeutralBodyVolume > 0)
            {
                excess *= BuoyancyModel.ExcessDensityFactor(
                    (float)body.TotalVolume, (float)config.NeutralBodyVolume);
            }

            double restoringFraction = config.SurfaceRestoringFraction;
            double restoringDensity = restoringFraction * config.TissueDensity;

            bool accelerating = config.FluidAccelerationCoefficient > 0;

            for (int i = 0; i < body.Links; i++)
            {
                Mat3 rotation = Mat3.Read(body.RotationMatrix, 9 * i);

                Vec3 relative = Vec3.Read(body.Velocity, 3 * i) - Vec3.Read(body.Water, 3 * i);
                Vec3.Write(body.RelativeVelocity, 3 * i, relative);

                Vec3 spin = Vec3.Read(body.Spin, 3 * i);

                Vec3 localVelocity = rotation.TransposedTimes(relative);
                Vec3 localSpin = rotation.TransposedTimes(spin);

                Vec3 localForce = Vec3.Zero;
                Vec3 localTorque = Vec3.Zero;

                int from = body.PanelStart[i];
                int to = body.PanelStart[i + 1];

                for (int p = from; p < to; p++)
                {
                    Vec3 centre = Vec3.Read(body.PanelCentre, 3 * p);
                    Vec3 normal = Vec3.Read(body.PanelNormal, 3 * p);

                    Vec3 panelVelocity = localVelocity + Vec3.Cross(localSpin, centre);
                    double normalSpeed = Vec3.Dot(panelVelocity, normal);
                    if (normalSpeed <= 0) continue;   // trailing: no pressure drag

                    Vec3 panelForce = normal * (-k * body.PanelArea[p] * normalSpeed * normalSpeed);

                    localForce += panelForce;
                    localTorque += Vec3.Cross(centre, panelForce);
                }

                Vec3 force = rotation * localForce;
                Vec3 torque = rotation * localTorque;

                // Package B: the dissipation ledger, taken BEFORE the limiter and before the two
                // terms below, exactly as FluidEnvironment keeps _force apart from _stepDrag.
                // What the audit is owed is the drag law's own work; the limiter is a stability
                // device and buoyancy does not dissipate.
                body.NoteDrag(i, force, torque);

                if (limit)
                {
                    double speed = relative.Magnitude;
                    if (speed > 0)
                    {
                        Vec3 direction = relative * (1.0 / speed);
                        double opposing = -Vec3.Dot(force, direction);
                        double allowed = body.Mass[i] * speed / dt;
                        if (opposing > allowed)
                        {
                            force += direction * (opposing - allowed);
                            body.DragImpulsesLimited++;
                        }
                    }

                    double spinRate = spin.Magnitude;
                    if (spinRate > 0)
                    {
                        Vec3 axis = spin * (1.0 / spinRate);
                        double opposing = -Vec3.Dot(torque, axis);
                        double allowed = body.SmallestInertia[i] * spinRate / dt;
                        if (opposing > allowed)
                        {
                            torque += axis * (opposing - allowed);
                            body.DragImpulsesLimited++;
                        }
                    }
                }

                double height = body.Position[3 * i + 1];

                // D090's second Morison term, per link. Package C fills WaterAcceleration in the
                // serial water pass; it is all zeroes in still water, where the term vanishes.
                if (accelerating)
                {
                    Vec3 accelerationForce = Vec3.Read(body.WaterAcceleration, 3 * i) *
                        (config.FluidAccelerationCoefficient * config.Density * body.Volume[i] *
                         (1.0 + config.AddedMassCoefficient));

                    if (accelerationForce.Y > 0 && height >= 0)
                    {
                        accelerationForce = new Vec3(accelerationForce.X, 0, accelerationForce.Z);
                    }

                    force += accelerationForce;
                }

                // D049/D050/D064/D077's one signed term: excess weight less what the part lifts,
                // clamped at the waterline and mirrored at the floor where there is no bed.
                double netDensity = excess * (1.0 - body.Lift[i]);
                double unclamped = netDensity;

                if (restoringFraction > 0)
                {
                    netDensity = Restore(
                        netDensity, height, restoringDensity, config.WorldDepthMetres,
                        floorRestores: !config.FloorIsSolid);
                }
                else if (netDensity < 0 && height >= 0)
                {
                    netDensity = 0;
                }

                if (netDensity != 0)
                {
                    force += new Vec3(
                        0,
                        -netDensity * body.Mass[i] * config.GravityMetresPerSecondSquared /
                            config.TissueDensity,
                        0);
                }

                // D111. The part's whole displaced weight, not the 2% excess above, acts at its
                // buoyancy centre and its weight at the origin, so the force above is untouched
                // and a torque appears: the lever of a frond with gas on one face, which turns
                // that face up. The share follows the clamp: none above the waterline and none
                // where the clamp has zeroed the term, so a body held at the surface is not spun
                // by water it is not in; below the floor's mirror it is still in water. Not
                // entered at all where the price is 0, the recorded solver's bits.
                if (config.BuoyancyOffsetTorque && body.BuoyancyArm[i] != 0.0 && height <= 0 &&
                    !(netDensity == 0 && unclamped != 0))
                {
                    int axis = body.ThinAxis[i];
                    Vec3 arm = rotation * new Vec3(
                        axis == 0 ? body.BuoyancyArm[i] : 0.0,
                        axis == 1 ? body.BuoyancyArm[i] : 0.0,
                        axis == 2 ? body.BuoyancyArm[i] : 0.0);

                    Vec3 displaced = new Vec3(
                        0, config.Density * body.Volume[i] * config.GravityMetresPerSecondSquared, 0);

                    torque += Vec3.Cross(arm, displaced);
                }

                Vec3.Add(body.Fext, 6 * i, torque);
                Vec3.Add(body.Fext, 6 * i + 3, force);
            }
        }

        /// <summary>
        /// The spike's signature, kept so a harness that has no current can still say what the
        /// water is doing: writes one acceleration into every link and then applies as usual.
        /// </summary>
        public static void Apply(Creature body, SolverConfig config, Vec3 waterAcceleration)
        {
            for (int i = 0; i < body.Links; i++)
            {
                Vec3.Write(body.WaterAcceleration, 3 * i, waterAcceleration);
            }

            Apply(body, config);
        }

        /// <summary>
        /// D077's top and bottom — a transcription of <c>FluidEnvironment.Restore</c>, including
        /// the branch at a fraction of zero that keeps D050's clamp exact.
        /// </summary>
        public static double Restore(
            double netDensity, double heightY, double restoringDensity, double worldDepthMetres,
            bool floorRestores = true)
        {
            if (!(restoringDensity > 0)) return netDensity < 0 && heightY >= 0 ? 0 : netDensity;

            if (heightY > 0) return netDensity > restoringDensity ? netDensity : restoringDensity;
            if (netDensity <= 0 && heightY == 0) return 0;

            if (floorRestores && heightY < -worldDepthMetres)
            {
                return netDensity < -restoringDensity ? netDensity : -restoringDensity;
            }

            return netDensity;
        }
    }
}
