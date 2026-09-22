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
    /// <b>Every sphere is entered in every cell it covers, and a query walks the cells its own
    /// sphere covers.</b> Two spheres that touch share a point, and the cell that point is in is
    /// in both sphere's ranges, so the query is exact at any cell size. Until 2026-09-22 the grid
    /// entered each sphere in one cell and made the cell two of the <i>largest</i> radius so
    /// that the twenty-seven-cell neighbourhood was exact; round 44 seed 1 then grew module
    /// chains with bounding radii of up to twenty metres in a tank fifty-three metres across,
    /// the cell became the tank, every body was a neighbour of every other, and the solver's
    /// cost per body-step rose seven- to thirtyfold with the neighbour lists and their sort
    /// (logbook/0113's read; the bench's record mode is the measurement). The cell is now two of
    /// the <i>mean</i> radius: a body of ordinary size covers one to eight cells, and a giant
    /// covers as many as its size warrants and pays for its own reach rather than charging it to
    /// the crowd.
    /// </para>
    /// <para>
    /// <b>The pairs are the same pairs, in the same order.</b> A query's candidates are sorted
    /// ascending and deduplicated, the caller's overlap test is unchanged, and the sum it takes
    /// is over the same overlapping bodies in the same index order, so the force is the same
    /// bits as the one-cell grid produced: every recording replays and the digest is unmoved at
    /// every thread count.
    /// </para>
    /// <para>
    /// <b>Buckets are a compressed row, not a dictionary of lists.</b> Two counting passes and a
    /// prefix sum fill flat arrays that are reused from step to step, so the grid allocates only
    /// when the population, or the number of cells its spheres cover, grows.
    /// </para>
    /// </remarks>
    public sealed class ContactGrid
    {
        private int[] _bucketStart = new int[1];
        private int[] _cursor = new int[1];
        private int[] _items = Array.Empty<int>();
        private int[] _lo = Array.Empty<int>();      // 3 per creature: the lowest cell its sphere covers
        private int[] _hi = Array.Empty<int>();      // 3 per creature: the highest; hi < lo is not entered
        private int _mask;
        private double _cellSize = 1.0;
        private int _count;

        private IReadOnlyList<Creature> _creatures = Array.Empty<Creature>();

        public double CellSize => _cellSize;

        /// <summary>The largest active bounding radius the last build saw, metres.</summary>
        public double LargestRadius { get; private set; }

        /// <summary>The mean active bounding radius the last build saw, metres; the cell is twice it.</summary>
        public double MeanRadius { get; private set; }

        /// <summary>Cell entries the last build made, summed over every sphere.</summary>
        public int Entries { get; private set; }

        /// <param name="cellOverride">
        /// A cell size to use in place of the rule, metres; 0 is the rule. The bench's probe of
        /// what the cell costs (logbook/0113's read of round 44 seed 1); no farm sets it.
        /// </param>
        public void Build(IReadOnlyList<Creature> creatures, double cellOverride = 0.0)
        {
            _creatures = creatures;
            _count = creatures.Count;

            if (_count == 0) return;

            double largest = 0, sum = 0;
            int active = 0;
            for (int i = 0; i < _count; i++)
            {
                Creature body = creatures[i];
                if (!body.ContactActive) continue;
                if (body.ContactRadius > largest) largest = body.ContactRadius;
                sum += body.ContactRadius;
                active++;
            }

            LargestRadius = largest;
            MeanRadius = active > 0 ? sum / active : 0;

            // Two of the mean radius, never under a quarter of a metre: the campaign's bodies
            // are half a metre to two metres across the radius, so an ordinary body covers one
            // to eight cells and a query reads a few dozen candidates.
            double rule = MeanRadius > 0.125 ? 2.0 * MeanRadius : 0.25;
            _cellSize = cellOverride > 0 ? cellOverride : rule;

            if (_lo.Length < 3 * _count)
            {
                _lo = new int[3 * _count];
                _hi = new int[3 * _count];
            }

            long entries = 0;
            for (int i = 0; i < _count; i++)
            {
                Creature body = creatures[i];

                if (!body.ContactActive)
                {
                    // Not entered and never a candidate: lo above hi on every axis.
                    _lo[3 * i] = 1; _hi[3 * i] = 0;
                    _lo[3 * i + 1] = 1; _hi[3 * i + 1] = 0;
                    _lo[3 * i + 2] = 1; _hi[3 * i + 2] = 0;
                    continue;
                }

                double r = body.ContactRadius;
                Vec3 c = body.ContactCentre;

                int lx = Floor((c.X - r) / _cellSize), hx = Floor((c.X + r) / _cellSize);
                int ly = Floor((c.Y - r) / _cellSize), hy = Floor((c.Y + r) / _cellSize);
                int lz = Floor((c.Z - r) / _cellSize), hz = Floor((c.Z + r) / _cellSize);

                _lo[3 * i] = lx; _hi[3 * i] = hx;
                _lo[3 * i + 1] = ly; _hi[3 * i + 1] = hy;
                _lo[3 * i + 2] = lz; _hi[3 * i + 2] = hz;

                entries += (long)(hx - lx + 1) * (hy - ly + 1) * (hz - lz + 1);
            }

            if (entries > int.MaxValue / 2)
            {
                throw new InvalidOperationException(
                    FormattableString.Invariant(
                        $"The contact grid would hold {entries} cell entries for {_count} bodies ") +
                    FormattableString.Invariant($"at a cell of {_cellSize:0.###} m: a body's sphere is ") +
                    "so far out of scale with the crowd's that the grid cannot hold it. A radius " +
                    "that size is a diverged body, which the divergence check should have taken " +
                    "out before this step.");
            }

            Entries = (int)entries;

            int buckets = 1;
            while (buckets < _count * 2) buckets <<= 1;
            _mask = buckets - 1;

            if (_bucketStart.Length < buckets + 1)
            {
                _bucketStart = new int[buckets + 1];
                _cursor = new int[buckets + 1];
            }
            if (_items.Length < Entries) _items = new int[System.Math.Max(Entries, 2 * _items.Length)];

            Array.Clear(_bucketStart, 0, buckets + 1);

            for (int i = 0; i < _count; i++)
            {
                for (int x = _lo[3 * i]; x <= _hi[3 * i]; x++)
                for (int y = _lo[3 * i + 1]; y <= _hi[3 * i + 1]; y++)
                for (int z = _lo[3 * i + 2]; z <= _hi[3 * i + 2]; z++)
                {
                    _bucketStart[Hash(x, y, z) & _mask]++;
                }
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
                for (int x = _lo[3 * i]; x <= _hi[3 * i]; x++)
                for (int y = _lo[3 * i + 1]; y <= _hi[3 * i + 1]; y++)
                for (int z = _lo[3 * i + 2]; z <= _hi[3 * i + 2]; z++)
                {
                    _items[_cursor[Hash(x, y, z) & _mask]++] = i;
                }
            }
        }

        /// <summary>
        /// Fills <paramref name="into"/> with the indices of every creature whose sphere covers
        /// a cell that <paramref name="self"/>'s sphere covers, itself excluded, sorted ascending
        /// and each once. Every body whose sphere touches <paramref name="self"/>'s is among them.
        /// </summary>
        public int Neighbours(int self, ref int[] into)
        {
            if (_count == 0) return 0;

            int lx = _lo[3 * self], hx = _hi[3 * self];
            int ly = _lo[3 * self + 1], hy = _hi[3 * self + 1];
            int lz = _lo[3 * self + 2], hz = _hi[3 * self + 2];
            int found = 0;

            for (int x = lx; x <= hx; x++)
            for (int y = ly; y <= hy; y++)
            for (int z = lz; z <= hz; z++)
            {
                int h = Hash(x, y, z) & _mask;

                for (int k = _bucketStart[h]; k < _bucketStart[h + 1]; k++)
                {
                    int other = _items[k];
                    if (other == self) continue;

                    // A hash collision puts another cell's entries in this bucket; an entry is
                    // this cell's only if the cell is inside that body's range.
                    if (x < _lo[3 * other] || x > _hi[3 * other] ||
                        y < _lo[3 * other + 1] || y > _hi[3 * other + 1] ||
                        z < _lo[3 * other + 2] || z > _hi[3 * other + 2])
                    {
                        continue;
                    }

                    if (found == into.Length) Array.Resize(ref into, into.Length * 2);
                    into[found++] = other;
                }
            }

            if (found < 2) return found;

            // Ascending, so the caller's sum is taken in index order however the buckets were
            // laid out; and once each, since a body covering several of self's cells was found
            // in each of them.
            Array.Sort(into, 0, found);

            int unique = 1;
            for (int i = 1; i < found; i++)
            {
                if (into[i] == into[unique - 1]) continue;
                into[unique++] = into[i];
            }

            return unique;
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
