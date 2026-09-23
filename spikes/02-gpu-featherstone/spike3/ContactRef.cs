using System.Collections.Generic;
using Evosim.Dynamics;

namespace Gpu.Spike3
{
    /// <summary>
    /// <c>Contacts.Apply</c>, copied line for line from <c>src/Evosim.Dynamics/Contacts.cs</c>
    /// (lines 280-398 at commit d153224), with three recorders added and nothing else changed:
    /// each overlapping pair's index and its push vector, the pair sum before the bed, and the
    /// capped whole-body force. The library does not expose any of the three, and the kernel
    /// writes all of them.
    /// </summary>
    /// <remarks>
    /// The copy is checked against the library on every body at every step it is used: the
    /// harness calls <c>Contacts.Apply</c> itself on the same body and compares the per-link
    /// force it adds to <c>Fext</c> with this copy's, to the bit. A copy that drifted from the
    /// library would fail there before it could vouch for the kernel.
    /// </remarks>
    internal static class ContactRef
    {
        public sealed class Result
        {
            public readonly List<int> Pairs = new List<int>();
            public readonly List<Vec3> Pushes = new List<Vec3>();
            public Vec3 PairSum;
            public Vec3 Force;
            public bool BedOrGlass;
            public double[] Fext = new double[3 * 16];

            public void Clear()
            {
                Pairs.Clear();
                Pushes.Clear();
                PairSum = Vec3.Zero;
                Force = Vec3.Zero;
                BedOrGlass = false;
                System.Array.Clear(Fext, 0, Fext.Length);
            }
        }

        public static void Apply(
            Creature body, int index, ContactGrid grid, SolverConfig config, ref int[] scratch,
            Result into)
        {
            into.Clear();
            if (into.Fext.Length < 3 * body.Links) into.Fext = new double[3 * body.Links];

            Vec3 force = Vec3.Zero;

            if (config.CreatureContact)
            {
                int n = grid.Neighbours(index, ref scratch);
                for (int k = 0; k < n; k++)
                {
                    Creature other = grid.At(scratch[k]);
                    if (!other.ContactActive) continue;

                    Vec3 between = body.ContactCentre - other.ContactCentre;
                    double distance = between.Magnitude;
                    double penetration = body.ContactRadius + other.ContactRadius - distance;
                    if (penetration <= 0) continue;

                    into.Pairs.Add(scratch[k]);

                    Vec3 normal = distance > 1e-9
                        ? between * (1.0 / distance)
                        : Vec3.UnitY;

                    double reduced =
                        body.TotalMass * other.TotalMass / (body.TotalMass + other.TotalMass);

                    double stiffness = reduced * config.ContactOmega * config.ContactOmega;
                    double damping = 2.0 * config.ContactDampingRatio * reduced * config.ContactOmega;

                    double approach = Vec3.Dot(body.ContactVelocity - other.ContactVelocity, normal);

                    Vec3 push = normal * ContactLaw.PairPush(
                        stiffness * penetration - damping * approach,
                        reduced, approach, config);
                    into.Pushes.Add(push);
                    force += push;
                }
            }

            into.PairSum = force;

            double mass = body.TotalMass;
            double bedStiffness = mass * config.ContactOmega * config.ContactOmega;
            double bedDamping = 2.0 * config.ContactDampingRatio * mass * config.ContactOmega;

            if (config.Bed == null)
            {
                double below = -config.WorldDepthMetres - (body.ContactCentre.Y - body.ContactRadius);
                if (below > 0)
                {
                    double up = ContactLaw.PairPush(
                        bedStiffness * below - bedDamping * body.ContactVelocity.Y,
                        mass, body.ContactVelocity.Y, config);

                    force += new Vec3(0, up, 0);
                    into.BedOrGlass = true;
                }
            }
            else
            {
                Vec3 rock = ContactBed.Push(body, config, bedStiffness, bedDamping);

                if (rock.X != 0 || rock.Y != 0 || rock.Z != 0)
                {
                    force += rock;
                    into.BedOrGlass = true;
                }
            }

            if (config.TankRadiusMetres > 0)
            {
                double x = body.ContactCentre.X - config.TankAxisX;
                double z = body.ContactCentre.Z - config.TankAxisZ;
                double radial = System.Math.Sqrt(x * x + z * z);
                double outside = radial + body.ContactRadius - config.TankRadiusMetres;

                if (outside > 0)
                {
                    Vec3 inward = radial > 1e-9
                        ? new Vec3(-x / radial, 0, -z / radial)
                        : Vec3.UnitX;

                    double approach = Vec3.Dot(body.ContactVelocity, inward);

                    force += inward * ContactLaw.PairPush(
                        bedStiffness * outside - bedDamping * approach, mass, approach, config);

                    into.BedOrGlass = true;
                }
            }

            force = ContactLaw.BodyPush(force, mass, config);
            into.Force = force;

            if (force.X != 0 || force.Y != 0 || force.Z != 0)
            {
                double scale = 1.0 / body.TotalMass;
                for (int i = 0; i < body.Links; i++)
                {
                    // Vec3.Add(body.Fext, 6 * i + 3, ...) onto a cleared Fext: 0 + v.
                    Vec3 v = force * (body.Mass[i] * scale);
                    into.Fext[3 * i] += v.X;
                    into.Fext[3 * i + 1] += v.Y;
                    into.Fext[3 * i + 2] += v.Z;
                }
            }
        }
    }
}
