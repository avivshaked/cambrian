using Evosim.Core;

namespace Evosim.Dynamics
{
    /// <summary>
    /// The water, ported term for term from <c>Evosim.Sim.FluidEnvironment</c> and
    /// <c>Evosim.Core.FluidModel</c>: panel drag, the drag impulse limiter, added mass,
    /// the water's own acceleration, buoyancy with D064's size scaling and D049's lift, and
    /// D050/D077's surface rule; and D111's buoyancy offset and D161's drag on turning, which are
    /// the farm's alone (the Unity farm binds neither).
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

            // D199: where the limiter engages, the force is capped link by link under either law,
            // and the torque either link by link or, under DragLimitByWeldedBody, by each welded
            // body as a whole once every link's drag is noted.
            bool welded = limit && config.DragLimitByWeldedBody;
            bool perLink = limit && !welded;

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
            double omega = config.PowerPushRadiansPerSecond;
            bool powered = config.LimbPushCoefficient > 0 && omega > 0 && body.Drive.BendAndPower;

            // Under the bend-and-power drive the push comes from the power alone: it replaces the
            // push from a real swing (propose-joint-drive.md, ruling 1), so a limb moved through its
            // bend feels its drag and nothing more. Left on beside it, a brain that wagged its bend
            // swam a median 1.2 cm/s with no power at all (the swim test, 2026-09-30).
            bool pushing = config.LimbPushCoefficient > 0 && !powered;

            // D229's fin law. Not entered at false, the recorded solver's bits.
            bool shaped = config.ShapedAddedMass;

            // D231: the powered stroke pushes the parts welded beyond a jointed link too. Not entered at false,
            // and the tables are read only for a link whose assembly has more than one part.
            bool strokeAssembly = powered && config.StrokePushesAssembly;
            if (strokeAssembly && body.StrokeAssemblyParts == null)
            {
                throw new System.InvalidOperationException(
                    "SolverConfig.StrokePushesAssembly is on and this body was built with it off, so it has no " +
                    "assembly table. A body is built, and resized, under the config it is stepped with.");
            }

            // D161's drag on turning. Not entered at None, the recorded solver's bits.
            bool skin = config.TurningDrag == TurningDragForm.Skin;
            bool spinning = config.TurningDrag == TurningDragForm.Spin;
            double negativeSkin = -(0.5 * config.Density * config.SkinFrictionCoefficient);
            double negativeSpin = -(0.5 * config.Density * config.SpinDragCoefficient);

            // -k * area parses as (-k) * area, so the negation is hoisted with the same bits.
            double negativeK = -k;
            double[] centres = body.PanelCentre;
            double[] normals = body.PanelNormal;
            double[] areas = body.PanelArea;

            for (int i = 0; i < body.Links; i++)
            {
                Mat3 rotation = Mat3.Read(body.RotationMatrix, 9 * i);

                Vec3 relative = Vec3.Read(body.Velocity, 3 * i) - Vec3.Read(body.Water, 3 * i);
                Vec3.Write(body.RelativeVelocity, 3 * i, relative);

                Vec3 spin = Vec3.Read(body.Spin, 3 * i);

                Vec3 localVelocity = rotation.TransposedTimes(relative);
                Vec3 localSpin = rotation.TransposedTimes(spin);

                int from = body.PanelStart[i];
                int to = body.PanelStart[i + 1];

                // The panel sum written out in scalars (F1, logbook/specs/speed-plan.md): every
                // product and sum is Vec3's own, in Vec3's order (Cross, Dot left to right, the
                // force's -k * area * v * v left-associative, each component accumulated by
                // itself), so the bits are the struct form's; what goes is the struct traffic.
                double vx = localVelocity.X, vy = localVelocity.Y, vz = localVelocity.Z;
                double wx = localSpin.X, wy = localSpin.Y, wz = localSpin.Z;
                double fx = 0, fy = 0, fz = 0;
                double tx = 0, ty = 0, tz = 0;

                for (int p = from; p < to; p++)
                {
                    int at = 3 * p;
                    double cx = centres[at], cy = centres[at + 1], cz = centres[at + 2];
                    double nx = normals[at], ny = normals[at + 1], nz = normals[at + 2];

                    double px = vx + (wy * cz - wz * cy);
                    double py = vy + (wz * cx - wx * cz);
                    double pz = vz + (wx * cy - wy * cx);
                    double normalSpeed = px * nx + py * ny + pz * nz;
                    if (normalSpeed <= 0) continue;   // trailing: no pressure drag

                    double pressure = negativeK * areas[p] * normalSpeed * normalSpeed;
                    double gx = nx * pressure, gy = ny * pressure, gz = nz * pressure;

                    fx += gx; fy += gy; fz += gz;
                    tx += cy * gz - cz * gy;
                    ty += cz * gx - cx * gz;
                    tz += cx * gy - cy * gx;
                }

                double height = body.Position[3 * i + 1];

                // D161, only in the water, as the limb pushes and D111's torque are. Summed after
                // the pressure drag so that it is drag in every sense the code knows: the ledger
                // below counts it and the limiter caps it.
                if (skin && height <= 0)
                {
                    // The shear on each panel's velocity along its face, on leading and trailing
                    // panels alike.
                    for (int p = from; p < to; p++)
                    {
                        int at = 3 * p;
                        double cx = centres[at], cy = centres[at + 1], cz = centres[at + 2];
                        double nx = normals[at], ny = normals[at + 1], nz = normals[at + 2];

                        double px = vx + (wy * cz - wz * cy);
                        double py = vy + (wz * cx - wx * cz);
                        double pz = vz + (wx * cy - wy * cx);
                        double normalSpeed = px * nx + py * ny + pz * nz;

                        double sx = px - normalSpeed * nx;
                        double sy = py - normalSpeed * ny;
                        double sz = pz - normalSpeed * nz;
                        double slide = System.Math.Sqrt(sx * sx + sy * sy + sz * sz);
                        if (slide == 0) continue;

                        double shear = negativeSkin * areas[p] * slide;
                        double gx = sx * shear, gy = sy * shear, gz = sz * shear;

                        fx += gx; fy += gy; fz += gz;
                        tx += cy * gz - cz * gy;
                        ty += cz * gx - cx * gz;
                        tz += cx * gy - cy * gx;
                    }
                }
                else if (spinning && height <= 0)
                {
                    // A torque about each of the link's own axes against its spin, and no force.
                    double rate = System.Math.Sqrt(wx * wx + wy * wy + wz * wz);
                    double brake = negativeSpin * rate;
                    tx += brake * body.SpinMoment[3 * i] * wx;
                    ty += brake * body.SpinMoment[3 * i + 1] * wy;
                    tz += brake * body.SpinMoment[3 * i + 2] * wz;
                }

                Vec3 force = rotation * new Vec3(fx, fy, fz);
                Vec3 torque = rotation * new Vec3(tx, ty, tz);

                // Package B: the dissipation ledger, taken BEFORE the limiter and before the two
                // terms below, exactly as FluidEnvironment keeps _force apart from _stepDrag.
                // What the audit is owed is the drag law's own work; the limiter is a stability
                // device and buoyancy does not dissipate.
                body.NoteDrag(i, force, torque);

                if (limit)
                {
                    Vec3 uncapped = force;
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

                    if (welded) body.NoteWeldedForceCut(i, force - uncapped);
                }

                if (perLink)
                {
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

                // propose-foraging.md's push: a link swinging about its joint is pushed along its
                // own axis, toward the joint, by C_T · m_a · r · ω⊥². Only in the water, only on a
                // link with a parent, and not entered at all at 0, the recorded solver's bits.
                if (pushing && body.Parent[i] >= 0 && height <= 0)
                {
                    Vec3 toOrigin = rotation * (-Vec3.Read(body.ChildAnchor, 3 * i));
                    double reach = toOrigin.Magnitude;
                    if (reach > 0)
                    {
                        Vec3 axis = toOrigin * (1.0 / reach);
                        Vec3 swing = spin - Vec3.Read(body.Spin, 3 * body.Parent[i]);
                        Vec3 across = swing - axis * Vec3.Dot(swing, axis);
                        double addedMass = config.AddedMassCoefficient * config.Density * body.Volume[i];
                        bool shapedSwing = false;
                        double shapedLever = reach;
                        if (shaped)
                        {
                            // D229: the part's added mass for the direction of its swing, and the
                            // lever it acts at for that direction. A sphere keeps the line below,
                            // whose bits are the shaped value's.
                            Vec3 swingMotion = Vec3.Cross(across, axis);
                            double acrossLength = swingMotion.Magnitude;
                            PhenotypePart shapedPart = body.Phenotype.Parts[i];
                            if (acrossLength > 0 && shapedPart.ShapeId != ShapeIds.Sphere)
                            {
                                // across is the swing's angular velocity off the limb's line, the
                                // turning axis; the limb moves along across x axis, as each degree of
                                // freedom's does along dofAxis x axis below.
                                Vec3 swingDirection = swingMotion * (1.0 / acrossLength);
                                addedMass = AddedMassOf(
                                    shapedPart.ShapeId, shapedPart.HalfExtents, rotation, axis,
                                    swingDirection,
                                    config.Density, config.AddedMassCoefficient, body.Volume[i]);
                                shapedLever = LeverOf(reach, axis, swingDirection);
                                shapedSwing = true;
                            }
                        }

                        double push = config.LimbPushCoefficient * addedMass * reach * Vec3.Dot(across, across);
                        if (shapedSwing) push = config.LimbPushCoefficient * addedMass * shapedLever * Vec3.Dot(across, across);
                        force += axis * (-push);
                    }
                }

                // propose-joint-drive.md's push: the stroke is not simulated, and a link whose
                // joint has power s is pushed as the rule above pushes one swinging at s·Ω, each
                // degree of freedom weighted by how far its axis stands from the limb's own.
                if (powered && body.Parent[i] >= 0 && height <= 0 && body.DofCount[i] > 0)
                {
                    Vec3 toOrigin = rotation * (-Vec3.Read(body.ChildAnchor, 3 * i));
                    double reach = toOrigin.Magnitude;
                    if (reach > 0)
                    {
                        Vec3 axis = toOrigin * (1.0 / reach);
                        QuatD frame = QuatD.Read(body.JointFrame, 4 * i);
                        int start = body.DofStart[i];
                        double swing2 = 0;

                        // D229: each degree of freedom moves the limb in its own direction, the
                        // unit vector of dofAxis x axis, and the added mass is the part's for it.
                        // A sphere takes the unshaped line below, whose bits are the shaped value's.
                        PhenotypePart shapedPart = shaped ? body.Phenotype.Parts[i] : null;

                        // D255: a family limb takes its strip integral, computed at build, in place of D229's
                        // lines. Not entered for any other part, whose bits are unchanged.
                        bool family = shaped && shapedPart.Family != null;
                        if (family && body.FamilyAddedMass == null)
                        {
                            throw new System.InvalidOperationException(
                                "A family limb is stepped under the fin law and this body was built without its " +
                                "added mass table. A body is built, and resized, under the config it is stepped with (D255).");
                        }

                        bool shapedPush = shaped && !family && shapedPart.ShapeId != ShapeIds.Sphere;
                        double shapedSum = 0;
                        double familySum = 0;
                        double familyMass = 0;

                        // D231: a link with parts welded beyond it. Its constants were written at build and
                        // at every resize; nothing is computed or allocated here but the sums.
                        bool assembly = strokeAssembly && body.StrokeAssemblyParts[i] > 1;
                        double assemblySum = 0;
                        double assemblyMass = 0;

                        for (int d = 0; d < body.DofCount[i]; d++)
                        {
                            double s = body.Drive.Stroke[start + d];
                            if (s <= 0) continue;

                            if (assembly)
                            {
                                // q_d = (s_d Omega)^2 (1 - along_d^2), with the along of the link's own frame.
                                double alongStored = body.StrokeAlong[start + d];
                                double q = s * omega * s * omega * (1.0 - alongStored * alongStored);
                                swing2 += q;
                                assemblySum += q * body.StrokeFirstMoment[start + d];
                                assemblyMass += q * body.StrokeAddedMass[start + d];
                                continue;
                            }

                            Vec3 dofAxis = rotation * frame.Rotate(Creature.AxisOf(d));
                            double along = Vec3.Dot(dofAxis, axis);
                            swing2 += s * omega * s * omega * (1.0 - along * along);

                            if (family)
                            {
                                // q_d as above, times the degree's I1 and M, which were written at build and at
                                // every resize.
                                double familyQ = s * omega * s * omega * (1.0 - along * along);
                                familySum += familyQ * body.FamilyFirstMoment[start + d];
                                familyMass += familyQ * body.FamilyAddedMass[start + d];
                            }

                            if (shapedPush)
                            {
                                Vec3 motion = Vec3.Cross(dofAxis, axis);
                                double motionLength = motion.Magnitude;
                                if (motionLength > 0)
                                {
                                    Vec3 strokeDirection = motion * (1.0 / motionLength);
                                    shapedSum += LeverOf(reach, axis, strokeDirection) *
                                        AddedMassOf(
                                            shapedPart.ShapeId, shapedPart.HalfExtents, rotation, axis,
                                            strokeDirection,
                                            config.Density, config.AddedMassCoefficient, body.Volume[i]) *
                                        (s * omega * s * omega * (1.0 - along * along));
                                }
                            }
                        }

                        if (swing2 > 0)
                        {
                            double addedMass = config.AddedMassCoefficient * config.Density * body.Volume[i];
                            double push = config.LimbPushCoefficient * addedMass * reach * swing2;
                            if (shapedPush) push = config.LimbPushCoefficient * shapedSum;

                            // D231: C_T times the sum of q_d I1_d, and the lever is the push-weighted one,
                            // (sum q_d I1_d) / (sum q_d M_d). Another link's lever is the reach (LeverOf).
                            double lever = reach;
                            if (assembly)
                            {
                                push = config.LimbPushCoefficient * assemblySum;
                                lever = AssemblyLeverOf(assemblySum, assemblyMass, reach);
                            }
                            else if (family)
                            {
                                // D255: the same law for a family limb, from the strip integral of its true slices.
                                push = config.LimbPushCoefficient * familySum;
                                lever = AssemblyLeverOf(familySum, familyMass, reach);
                            }

                            force += axis * (-push);

                            // The work is the push times the lever it acts at and the swing's speed. A
                            // lever other than reach (LeverOf) must enter this line too.
                            body.MechanicalWorkJoules += push * lever * System.Math.Sqrt(swing2) * config.StepSeconds;
                        }
                    }
                }

                // D049/D050/D064/D077's one signed term: excess weight less what the part lifts,
                // clamped at the waterline and mirrored at the floor where there is no bed.
                // D220: remains take no float lift. The test is false for every body of a world without contact
                // eating, and the product is then the one it always was.
                double netDensity = excess * (1.0 - (body.Remains ? 0.0 : body.Lift[i]));
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

            if (welded) body.LimitWeldedDrag(dt);
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
        /// D229: the lever a limb's push acts at, in metres, for one direction of motion. Today it is
        /// <paramref name="reach"/>, the distance from the joint to the link's origin, exactly, so the
        /// shaped push's bits are the unshaped lever's. It is the one place a later rule would put the
        /// lever where the added mass sits along the limb; the work line (<see cref="Apply"/>) must
        /// follow it.
        /// </summary>
        public static double LeverOf(double reach, Vec3 axis, Vec3 direction) => reach;

        /// <summary>
        /// D231: the lever a stroke's push acts at when it moves a whole assembly, in metres: where the moved
        /// water sits, <c>I1 / M</c>, the first moment of the added mass about the joint over the added mass.
        /// For a stroke of several degrees of freedom the caller passes the sums weighted by each degree's
        /// push, <c>Σ q I1</c> and <c>Σ q M</c>; for one degree the weights cancel. The sibling of
        /// <see cref="LeverOf"/>, which stays the one place a lone part's lever is chosen. Where nothing
        /// is moved (<paramref name="addedMass"/> not above 0) there is no push to place, and the reach is
        /// returned so that the work line stays finite.
        /// </summary>
        public static double AssemblyLeverOf(double firstMoment, double addedMass, double reach) =>
            addedMass > 0 ? firstMoment / addedMass : reach;

        /// <summary>
        /// D229, the fin law: the added mass of one part moving in one direction, in kg.
        /// </summary>
        /// <param name="shapeId">The part's <c>PhenotypePart.ShapeId</c>.</param>
        /// <param name="halfExtents">The part's half-extents, in its own frame.</param>
        /// <param name="rotation">The part's rotation, from its own frame to the frame the other two vectors are in.</param>
        /// <param name="axis">The limb's unit length axis, joint to link, in that frame.</param>
        /// <param name="direction">The unit direction of motion, in that frame.</param>
        /// <param name="density">The water's density, kg/m³.</param>
        /// <param name="coefficient">D135's <c>C_a</c>, used for a sphere alone.</param>
        /// <param name="volume">The part's volume, m³, used for a sphere alone.</param>
        /// <remarks>
        /// <para>
        /// A box takes the plate formula ρ·π/4·w²·L: <c>L</c> its caliper extent along the limb axis,
        /// <c>w</c> its caliper extent along the direction perpendicular to both the motion and the
        /// limb axis. Its extent along the motion does not enter. The limb's line need not be the
        /// box's long axis: both calipers are taken along the box's own axes, <c>L = 2·Σ|axis·e_k|·h_k</c>
        /// and <c>w = 2·Σ|n·e_k|·h_k</c>, with <c>n</c> the unit vector of axis × motion. The motion is
        /// first projected off the axis; if nothing is left of it there is no transverse motion, and
        /// the function returns 0, since that direction's term carries (1 − along²) = 0 anyway.
        /// </para>
        /// <para>
        /// A capsule takes the strip formula on its cylinder, ρ·π·r²·(2·halfSpan), plus its two ends
        /// as a sphere, ½·ρ·4/3·π·r³, in every direction. A sphere takes exactly
        /// <c>C_a · ρ · V</c>, the unshaped value. Any other shape is refused.
        /// </para>
        /// </remarks>
        public static double AddedMassOf(
            string shapeId, Float3 halfExtents, Mat3 rotation, Vec3 axis, Vec3 direction,
            double density, double coefficient, double volume)
        {
            if (shapeId == ShapeIds.Sphere) return coefficient * density * volume;

            if (shapeId == ShapeIds.Capsule)
            {
                double r = CapsuleShape.Radius(halfExtents);
                double halfSpan = CapsuleShape.HalfSpan(halfExtents);
                return density * System.Math.PI * r * r * (2.0 * halfSpan) +
                       0.5 * density * (4.0 / 3.0) * System.Math.PI * r * r * r;
            }

            if (shapeId == ShapeIds.Box)
            {
                Vec3 localAxis = rotation.TransposedTimes(axis);
                Vec3 localDirection = rotation.TransposedTimes(direction);
                Vec3 transverse = localDirection - localAxis * Vec3.Dot(localDirection, localAxis);
                if (!(transverse.SqrMagnitude > 0)) return 0.0;
                Vec3 across = Vec3.Cross(localAxis, transverse);
                double acrossLength = across.Magnitude;
                if (!(acrossLength > 0)) return 0.0;
                across = across * (1.0 / acrossLength);

                double hx = System.Math.Abs((double)halfExtents.X);
                double hy = System.Math.Abs((double)halfExtents.Y);
                double hz = System.Math.Abs((double)halfExtents.Z);

                double length = 2.0 * (System.Math.Abs(localAxis.X) * hx +
                                       System.Math.Abs(localAxis.Y) * hy +
                                       System.Math.Abs(localAxis.Z) * hz);
                double width = 2.0 * (System.Math.Abs(across.X) * hx +
                                      System.Math.Abs(across.Y) * hy +
                                      System.Math.Abs(across.Z) * hz);

                return density * (System.Math.PI / 4.0) * width * width * length;
            }

            throw new System.ArgumentException(
                "The fin law has no added mass for the shape '" + shapeId +
                "'; it knows box, capsule and sphere.", nameof(shapeId));
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
