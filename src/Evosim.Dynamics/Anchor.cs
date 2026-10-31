using System;
using Evosim.Core;

namespace Evosim.Dynamics
{
    /// <summary>
    /// D162's grip: a link carrying the anchoring cell holds on to the bed or a reef it touches
    /// (round 59, step 4; <c>logbook/specs/r59-anchor-build-plan.md</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Forming.</b> An anchoring link grips on the first step its sphere touches the bed or a
    /// reef (<see cref="Creature.RockTouched"/>, written by this step's contact pass, which runs
    /// just before). The glass sets no touch, so it never grips. It keeps where its origin was and
    /// how it was turned, and owes the cell's build price for a full-strength grip on its volume.
    /// A grip that breaks may form again on the next step and pays again (D165).
    /// </para>
    /// <para>
    /// <b>Holding.</b> A spring and a damper pull the origin back to its place, and a second pair
    /// holds the link's turn. Their stiffness comes from the link's own mass and smallest inertia
    /// at the contact's own frequency (<see cref="SolverConfig.ContactOmega"/>), as the bed's
    /// push does, so they stay inside the explicit step's stability; the pull is capped as the
    /// contact push is (<see cref="ContactLaw.BodyPush"/>), and the turn's torque by the same speed
    /// change at the sphere's rim.
    /// </para>
    /// <para>
    /// <b>Breaking.</b> When the pull the spring and damper demand, before the cap, exceeds the
    /// breaking force, the grip breaks and pulls nothing that step. The breaking force is the
    /// cell's (<see cref="AnchoringCell.BreakingForce"/>, tenacity times volume to the 2/3) times
    /// the grip's strength times the reserve factor. Only the pull breaks it; the turn's torque
    /// never does.
    /// </para>
    /// <para>
    /// <b>Weakening.</b> The strength falls by <c>exp(-dt/τ)</c> each step held, τ the cell's
    /// decay time: the grip's upkeep (D162). The reserve factor is 1 until the body's reserve has
    /// stood below the cell's threshold for longer than its lag; after that it is the reserve's
    /// fraction of the threshold, and it is 0, and every grip lets go, when the reserve is.
    /// </para>
    /// <para>
    /// <b>The brain's hold (D268).</b> Where the cell has <c>BrainHold</c>, a held grip lets go the step its part's
    /// hold output is below the cell's release threshold, and owes the release price for its full breaking force.
    /// A grip does not form while the output is below the threshold, so one that was let go forms again only once
    /// the output is back above it and the part touches the rock, with no latch stored. An unwired output reads
    /// <c>tanh(0) = 0</c>, above the default threshold, so a brain that ignores it holds as the grip did before.
    /// Holding owes nothing. A break and a release at an empty reserve owe no release price: the reserve's rule and
    /// the pull's come first, and only the brain's own act is priced. The pull as a share of the breaking force is
    /// kept per link (<see cref="Creature.GripPull"/>) for the second grip sense.
    /// </para>
    /// <para>
    /// <b>A fixed order, and this body's arrays alone</b>, so the pass runs inside the parallel
    /// phase and the thread count stays invisible.
    /// </para>
    /// </remarks>
    public static class Anchor
    {
        public static void Apply(Creature body, SolverConfig config, double dt)
        {
            if (body.GripHeld == null) return;

            AnchoringCell cell = config.Anchoring;
            double reserve = ReserveFactor(body, cell, dt);

            double omega = config.ContactOmega;
            double zeta = config.ContactDampingRatio;
            double decay = Math.Exp(-dt / cell.DecaySeconds);

            // D268: null where the cell does not let the brain hold, and then no line below does anything new.
            float[] hold = cell.BrainHold ? body.HoldSignal : null;
            double[] share = body.GripPull;

            for (int i = 0; i < body.Links; i++)
            {
                if (!body.Anchors[i]) continue;

                bool let = hold != null && HoldOf(body, hold, i) < cell.ReleaseBelow;

                if (!body.GripHeld[i])
                {
                    if (!body.RockTouched[i] || !(reserve > 0) || let)
                    {
                        if (share != null) share[i] = 0.0;
                        continue;
                    }

                    Form(body, i, cell);
                }

                if (!(reserve > 0))
                {
                    Release(body, i);
                    if (share != null) share[i] = 0.0;
                    continue;
                }

                if (let)
                {
                    LetGo(body, i, cell);
                    if (share != null) share[i] = 0.0;
                    continue;
                }

                double mass = body.Mass[i];
                Vec3 position = Vec3.Read(body.Position, 3 * i);
                Vec3 velocity = Vec3.Read(body.Velocity, 3 * i);
                Vec3 away = position - Vec3.Read(body.GripAnchor, 3 * i);

                double stiffness = mass * omega * omega;
                double damping = 2.0 * zeta * mass * omega;
                Vec3 pull = (away * stiffness + velocity * damping) * -1.0;

                double breaking = cell.BreakingForce((float)body.Volume[i]) * body.GripStrength[i] * reserve;
                double demand = pull.Magnitude;
                if (share != null) share[i] = breaking > 0 ? Math.Min(1.0, demand / breaking) : 0.0;
                if (demand > breaking)
                {
                    Release(body, i);
                    if (share != null) share[i] = 0.0;
                    continue;
                }

                // The turn: the rotation from the grip's attitude to the link's, as a small vector.
                Mat3 now = Mat3.Read(body.RotationMatrix, 9 * i);
                Mat3 then = Mat3.Read(body.GripAttitude, 9 * i);
                Vec3 turned = RotationVector(now * then.Transposed);
                Vec3 spin = Vec3.Read(body.Spin, 3 * i);

                double inertia = body.SmallestInertia[i];
                Vec3 twist = (turned * (inertia * omega * omega) + spin * (2.0 * zeta * inertia * omega)) * -1.0;

                Vec3 force = ContactLaw.BodyPush(pull, mass, config);
                Vec3 torque = CapTorque(twist, inertia, body.LinkContactRadius[i], config);

                Vec3.Add(body.Fext, 6 * i, torque);
                Vec3.Add(body.Fext, 6 * i + 3, force);

                body.GripStrength[i] *= decay;
                body.GripHeldSeconds += dt;
            }
        }

        private static void Form(Creature body, int i, AnchoringCell cell)
        {
            body.GripHeld[i] = true;
            body.GripStrength[i] = 1.0;
            Array.Copy(body.Position, 3 * i, body.GripAnchor, 3 * i, 3);
            Array.Copy(body.RotationMatrix, 9 * i, body.GripAttitude, 9 * i, 9);
            Array.Copy(body.RockPoint, 3 * i, body.GripPoint, 3 * i, 3);

            body.GripOwedJoules += cell.BuildPrice(cell.BreakingForce((float)body.Volume[i]));
            body.GripsFormed++;
        }

        /// <summary>A link's hold output; a link with none reads 0, the output of a unit nothing feeds.</summary>
        private static float HoldOf(Creature body, float[] hold, int i)
        {
            int g = body.GripOutputOf[i];
            return g >= 0 && g < hold.Length ? hold[g] : 0f;
        }

        /// <summary>
        /// The brain lets the grip go (D268): cleared as <see cref="Release"/> clears it, counted apart from a break,
        /// and priced by the cell's release price on the part's full breaking force.
        /// </summary>
        private static void LetGo(Creature body, int i, AnchoringCell cell)
        {
            body.GripHeld[i] = false;
            body.GripStrength[i] = 0;
            body.GripOwedJoules += cell.ReleasePrice(cell.BreakingForce((float)body.Volume[i]));
            body.GripsLetGo++;
        }

        private static void Release(Creature body, int i)
        {
            if (!body.GripHeld[i]) return;
            body.GripHeld[i] = false;
            body.GripStrength[i] = 0;
            body.GripsBroken++;
        }

        /// <summary>
        /// D162's reserve rule, advancing the body's lag clock by one step: 1 while the reserve has
        /// not stood below the threshold for longer than the lag, then the reserve's fraction of the
        /// threshold, and 0 at an empty reserve. A body with no reserve source holds as if fed.
        /// </summary>
        internal static double ReserveFactor(Creature body, AnchoringCell cell, double dt)
        {
            IReserveSource source = body.Senses.Reserve;
            if (source == null) return 1.0;

            double seconds = source.SecondsOfReserve;
            if (!(seconds > 0)) return 0.0;

            double threshold = cell.ReserveThresholdSeconds;
            if (seconds >= threshold)
            {
                body.ReserveLowSeconds = 0;
                return 1.0;
            }

            body.ReserveLowSeconds += dt;
            if (body.ReserveLowSeconds <= cell.ReserveLagSeconds) return 1.0;

            return threshold > 0 ? Math.Min(1.0, seconds / threshold) : 1.0;
        }

        /// <summary>The rotation vector of a rotation matrix, axis times angle.</summary>
        internal static Vec3 RotationVector(Mat3 r)
        {
            var axis = new Vec3(r.M21 - r.M12, r.M02 - r.M20, r.M10 - r.M01);
            double sine = 0.5 * axis.Magnitude;
            double cosine = 0.5 * (r.M00 + r.M11 + r.M22 - 1.0);
            if (!(sine > 1e-12)) return axis * 0.5;

            double angle = Math.Atan2(sine, cosine);
            return axis * (0.5 * angle / sine);
        }

        /// <summary>
        /// A torque bounded so that one step of it cannot turn the link's rim faster than the world's contact cap
        /// (<see cref="SolverConfig.MaxContactSpeedChange"/>): at most <c>inertia * (cap / radius) / dt</c>, the
        /// torque that changes the link's angular speed by <c>cap / radius</c> in one step. The direction is kept.
        /// No bound when the cap or the radius is not positive.
        /// </summary>
        internal static Vec3 CapTorque(Vec3 torque, double inertia, double radius, SolverConfig config)
        {
            double cap = config.MaxContactSpeedChange;
            if (!(cap > 0) || !(radius > 0)) return torque;

            double most = inertia * (cap / radius) / config.StepSeconds;
            double magnitude = torque.Magnitude;
            return magnitude > most ? torque * (most / magnitude) : torque;
        }
    }
}
