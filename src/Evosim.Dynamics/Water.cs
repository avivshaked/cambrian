using Evosim.Core;

namespace Evosim.Dynamics
{
    /// <summary>
    /// Package C: the current, sampled into every body's <c>Water</c> and
    /// <c>WaterAcceleration</c> before the step. A transcription of the water half of
    /// <c>Evosim.Sim.FluidEnvironment.Apply</c>'s gather phase, with the Unity reads gone and
    /// nothing else moved.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Called under a pin, from any thread.</b> <see cref="CurrentField"/> memoises the
    /// instants a call touches, and a miss fills a slot, so two threads sampling one field at
    /// new clocks would race on the slot table and a trajectory would depend on who got there
    /// first — the one thing the whole solver is built not to do. <c>DynamicsWorld.SampleWater</c>
    /// therefore pins the field at the step's clock first (<see cref="CurrentField.PinInstant"/>),
    /// which fills every slot the step will ask for and makes each later lookup a read, and then
    /// calls this across the world's threads. It runs between the contact grid's build and the
    /// parallel phase, over the poses the step begins with, which is where the gather phase sat.
    /// Until 2026-09-23 it ran on one thread for the field's sake (the Unity farm's is on the
    /// main thread for Unity's), and at 1,200 bodies that thread was a third of the step.
    /// </para>
    /// <para>
    /// <b>The two branches are the farm's two branches.</b> At
    /// <see cref="SolverConfig.WaterHoldSeconds"/> 0 every link samples afresh, which is what
    /// every run before D100 did and what a config at 0 still replays. Above 0 the root samples
    /// once and every link of that body reads the same parcel until the hold expires — D100, and
    /// the reason the root's sample is taken inside the link loop at <c>i == 0</c> rather than
    /// before it is the farm's: link 0 <i>is</i> the root and its place has just been read.
    /// </para>
    /// <para>
    /// <b>A tank's acceleration is the streams' closed form and a box's is a nine-sample
    /// stencil</b>, and neither is chosen here: <see cref="CurrentField.AccelerationAt"/> picks
    /// by the shape it was given in <c>SetBox</c>, so this call site cannot disagree with the
    /// farm's about which route answered.
    /// </para>
    /// </remarks>
    public static class Water
    {
        /// <summary>
        /// Fills one body's per-link water velocity and, when D090's term is on, the water's
        /// acceleration beside it.
        /// </summary>
        /// <param name="body">The creature, at the poses the step is about to start from.</param>
        /// <param name="config">The world's water.</param>
        /// <param name="seconds">The world clock at the start of this step.</param>
        public static void Sample(Creature body, SolverConfig config, double seconds)
        {
            CurrentField current = config.Current;

            // Read once per body for the reason the farm reads them once per Apply: whether the
            // water moves at all, and whether its acceleration is felt, are properties of the
            // config and not of a link.
            bool accelerating = config.FluidAccelerationCoefficient > 0 && current != null;
            bool holding = config.WaterHoldSeconds > 0;

            if (current == null)
            {
                // Still water, the spike's case: both arrays are zero from construction and
                // nothing has written them, so there is nothing to clear.
                return;
            }

            bool transport = current.Mode == CurrentMode.Transport;
            int patchCount = config.PatchCount;

            for (int i = 0; i < body.Links; i++)
            {
                float x = (float)body.Position[3 * i];
                float y = (float)body.Position[3 * i + 1];
                float z = (float)body.Position[3 * i + 2];

                Float3 water;

                if (holding)
                {
                    if (i == 0 && seconds - body.WaterSampledAt >= config.WaterHoldSeconds)
                    {
                        body.HeldWater = transport
                            ? current.VelocityAt(x, y, z, seconds)
                            : current.VelocityAt(y, seconds, body.Patch, patchCount);

                        // Taken in the same breath as the velocity, so that a held body feels one
                        // parcel of water rather than a speed from one instant and the gradient
                        // that carries it from another.
                        if (accelerating)
                        {
                            body.HeldWaterAcceleration = current.AccelerationAt(x, y, z, seconds);
                        }

                        body.WaterSampledAt = seconds;
                    }

                    water = body.HeldWater;
                }
                else
                {
                    water = transport
                        ? current.VelocityAt(x, y, z, seconds)
                        : current.VelocityAt(y, seconds, body.Patch, patchCount);
                }

                body.Water[3 * i] = water.X;
                body.Water[3 * i + 1] = water.Y;
                body.Water[3 * i + 2] = water.Z;

                if (!accelerating) continue;

                Float3 acceleration = holding
                    ? body.HeldWaterAcceleration
                    : current.AccelerationAt(x, y, z, seconds);

                body.WaterAcceleration[3 * i] = acceleration.X;
                body.WaterAcceleration[3 * i + 1] = acceleration.Y;
                body.WaterAcceleration[3 * i + 2] = acceleration.Z;
            }
        }
    }
}
