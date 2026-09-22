using System;
using System.Collections.Generic;

namespace Evosim.Dynamics
{
    /// <summary>
    /// A uniform hash grid over the creatures' bounding spheres, rebuilt once per step from the
    /// places they held at the end of the previous one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The whole point is that a creature's step never reads another creature's current
    /// state.</b> The grid is built serially before anything is stepped, from state that is by
    /// then frozen, and each creature then reads it. Nothing is written into it during the
    /// parallel phase, so a body's trajectory cannot depend on which thread reached it first.
    /// </para>
    /// <para>
    /// <b>The cell is two of the largest radius</b>, so two spheres that touch at all are in
    /// cells no more than one apart on every axis and the twenty-seven-cell neighbourhood is
    /// exact rather than approximate.
    /// </para>
    /// <para>
    /// <b>Buckets are a compressed row, not a dictionary of lists.</b> Two counting passes and a
    /// prefix sum fill two flat arrays that are reused from step to step, so the grid allocates
    /// only when the population grows.
    /// </para>
    /// </remarks>
    public sealed class ContactGrid
    {
        private int[] _bucketStart = new int[1];
        private int[] _cursor = new int[1];
        private int[] _items = Array.Empty<int>();
        private int[] _cell = Array.Empty<int>();     // 3 per creature
        private int _mask;
        private double _cellSize = 1.0;
        private int _count;

        private IReadOnlyList<Creature> _creatures = Array.Empty<Creature>();

        public double CellSize => _cellSize;

        public void Build(IReadOnlyList<Creature> creatures)
        {
            _creatures = creatures;
            _count = creatures.Count;

            if (_count == 0) return;

            double largest = 0;
            for (int i = 0; i < _count; i++)
            {
                Creature body = creatures[i];
                if (!body.ContactActive) continue;
                if (body.ContactRadius > largest) largest = body.ContactRadius;
            }

            _cellSize = largest > 1e-6 ? 2.0 * largest : 1.0;

            int buckets = 1;
            while (buckets < _count * 2) buckets <<= 1;
            _mask = buckets - 1;

            if (_bucketStart.Length < buckets + 1)
            {
                _bucketStart = new int[buckets + 1];
                _cursor = new int[buckets + 1];
            }
            if (_items.Length < _count) _items = new int[_count];
            if (_cell.Length < 3 * _count) _cell = new int[3 * _count];

            Array.Clear(_bucketStart, 0, buckets + 1);

            for (int i = 0; i < _count; i++)
            {
                Creature body = creatures[i];
                int cx = Floor(body.ContactCentre.X / _cellSize);
                int cy = Floor(body.ContactCentre.Y / _cellSize);
                int cz = Floor(body.ContactCentre.Z / _cellSize);

                _cell[3 * i] = cx;
                _cell[3 * i + 1] = cy;
                _cell[3 * i + 2] = cz;

                _bucketStart[Hash(cx, cy, cz) & _mask]++;
            }

            int running = 0;
            for (int b = 0; b < buckets; b++)
            {
                int n = _bucketStart[b];
                _bucketStart[b] = running;
                _cursor[b] = running;
                running += n;
            }
            _bucketStart[buckets] = running;

            for (int i = 0; i < _count; i++)
            {
                int h = Hash(_cell[3 * i], _cell[3 * i + 1], _cell[3 * i + 2]) & _mask;
                _items[_cursor[h]++] = i;
            }
        }

        /// <summary>
        /// Fills <paramref name="into"/> with the indices of every creature whose cell is within
        /// one of <paramref name="self"/>'s, itself excluded, sorted ascending.
        /// </summary>
        public int Neighbours(int self, ref int[] into)
        {
            if (_count == 0) return 0;

            int cx = _cell[3 * self], cy = _cell[3 * self + 1], cz = _cell[3 * self + 2];
            int found = 0;

            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
            {
                int ax = cx + dx, ay = cy + dy, az = cz + dz;
                int h = Hash(ax, ay, az) & _mask;

                for (int k = _bucketStart[h]; k < _bucketStart[h + 1]; k++)
                {
                    int other = _items[k];
                    if (other == self) continue;
                    if (_cell[3 * other] != ax || _cell[3 * other + 1] != ay ||
                        _cell[3 * other + 2] != az)
                    {
                        continue;   // a hash collision, not a neighbour
                    }

                    if (found == into.Length) Array.Resize(ref into, into.Length * 2);
                    into[found++] = other;
                }
            }

            // Ascending, so the sum below is taken in id order however the buckets were laid
            // out. Insertion sort: the neighbourhood of one body is tens of entries at most.
            for (int i = 1; i < found; i++)
            {
                int value = into[i];
                int j = i - 1;
                while (j >= 0 && into[j] > value) { into[j + 1] = into[j]; j--; }
                into[j + 1] = value;
            }

            return found;
        }

        public Creature At(int index) => _creatures[index];

        private static int Floor(double v)
        {
            int i = (int)v;
            return v < i ? i - 1 : i;
        }

        private static int Hash(int x, int y, int z) =>
            (x * 73856093) ^ (y * 19349663) ^ (z * 83492791);
    }

    /// <summary>
    /// The soft pushes: creature against creature, creature against the bed, creature against
    /// the glass. Each is a spring-damper on the penetration of two bounding spheres, or of one
    /// sphere and a plane or a cylinder.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Not a contact solve, and it is the largest departure from PhysX in the spike.</b> The
    /// farm gives the engine every collider and gets a joint solve with friction and
    /// depenetration back. This gives one force per creature pair, applied at each body's root
    /// link, computed from state that is already frozen. It cannot jam, it cannot farm
    /// depenetration (<c>MaxDepenetrationVelocity</c> exists in the farm to stop exactly that),
    /// and it has no friction at all.
    /// </para>
    /// <para>
    /// <b>Antisymmetric by construction, so no shared write is needed.</b> The reduced mass and
    /// the normal both flip when the pair is read from the other side, so each creature computes
    /// its own half and the pair is equal and opposite without either touching the other.
    /// </para>
    /// </remarks>
    public static class Contacts
    {
        public static void Apply(
            Creature body, int index, ContactGrid grid, SolverConfig config, ref int[] scratch)
        {
            Vec3 force = Vec3.Zero;

            bool instrument = config.ContactInstrument;
            if (instrument) body.BeginContactStep();

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

                    if (instrument) body.NoteOverlap(other.Id);

                    Vec3 normal = distance > 1e-9
                        ? between * (1.0 / distance)
                        : Vec3.UnitY;

                    double reduced =
                        body.TotalMass * other.TotalMass / (body.TotalMass + other.TotalMass);

                    double stiffness = reduced * config.ContactOmega * config.ContactOmega;
                    double damping = 2.0 * config.ContactDampingRatio * reduced * config.ContactOmega;

                    double approach = Vec3.Dot(body.ContactVelocity - other.ContactVelocity, normal);

                    force += normal * ContactLaw.PairPush(
                        stiffness * penetration - damping * approach,
                        reduced, approach, config);
                }
            }

            double mass = body.TotalMass;
            double bedStiffness = mass * config.ContactOmega * config.ContactOmega;
            double bedDamping = 2.0 * config.ContactDampingRatio * mass * config.ContactOmega;

            // The bed. Flat at -depth, or the height map's own surface when the world has one:
            // two branches rather than one general path with a zero in it, so a flat world's
            // arithmetic is identical and not merely equal — PlacementFloor's own gate, for its
            // reason.
            if (config.Bed == null)
            {
                // The bed, flat at -depth.
                double below = -config.WorldDepthMetres - (body.ContactCentre.Y - body.ContactRadius);
                if (below > 0)
                {
                    double up = ContactLaw.PairPush(
                        bedStiffness * below - bedDamping * body.ContactVelocity.Y,
                        mass, body.ContactVelocity.Y, config);

                    force += new Vec3(0, up, 0);

                    if (instrument) body.TouchedBedOrGlass = true;
                }
            }
            else
            {
                Vec3 rock = ContactBed.Push(body, config, bedStiffness, bedDamping);

                if (rock.X != 0 || rock.Y != 0 || rock.Z != 0)
                {
                    force += rock;
                    if (instrument) body.TouchedBedOrGlass = true;
                }
            }

            // The glass, a cylinder about the vertical axis.
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

                    if (instrument) body.TouchedBedOrGlass = true;
                }
            }

            force = ContactLaw.BodyPush(force, mass, config);

            if (force.X != 0 || force.Y != 0 || force.Z != 0)
            {
                // Spread over the links by mass, so the push accelerates the whole body and
                // strains none of its joints — the same way a uniform field does.
                //
                // Putting it all on the root instead, which is where the bounding sphere is
                // centred, was the first build and it threw bodies: a deep first-step overlap
                // in the bench's scatter is kilonewtons, and a root link with heavy children on
                // compliant joints is whipped by it. Six of a thousand bodies were lost to that
                // and none to anything else (the isolation is in scratch/solver-spike/logs/).
                // The sphere is a whole-body approximation in the first place, so a whole-body
                // force is the honest way to hand it over.
                double scale = 1.0 / body.TotalMass;
                for (int i = 0; i < body.Links; i++)
                {
                    Vec3.Add(body.Fext, 6 * i + 3, force * (body.Mass[i] * scale));
                }
            }
        }
    }
}
