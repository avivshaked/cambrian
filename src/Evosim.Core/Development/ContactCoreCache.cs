using System;
using System.Collections.Generic;
using System.Threading;

namespace Evosim.Core
{
    /// <summary>
    /// A family part's contact core at the size it was built at, with its margin, its two measures and its reach —
    /// D233. Immutable once built; <see cref="Points"/> is shared, so a caller must not change it.
    /// </summary>
    public sealed class ContactCoreBuild
    {
        /// <summary>The core's points, three doubles a point, in the shifted frame and the lattice's order.</summary>
        public readonly double[] Points;

        /// <summary>The margin, the bulge and the shortfall, metres (<see cref="Superellipsoid.ContactCore(Float3, out double, out double, out double)"/>).</summary>
        public readonly double Margin, Bulge, Shortfall;

        /// <summary>A radius about the origin that holds every core point plus the margin and every point of the solid, metres.</summary>
        public readonly double Reach;

        public ContactCoreBuild(Superellipsoid geometry, Float3 halfExtents)
        {
            if (geometry == null) throw new ArgumentNullException(nameof(geometry));

            Double3[] core = geometry.ContactCore(halfExtents, out double margin, out double bulge, out double shortfall);

            Points = new double[3 * core.Length];
            double farthest = 0.0;
            for (int i = 0; i < core.Length; i++)
            {
                Points[3 * i] = core[i].X;
                Points[3 * i + 1] = core[i].Y;
                Points[3 * i + 2] = core[i].Z;

                double length = core[i].Length;
                if (length > farthest) farthest = length;
            }

            Margin = margin;
            Bulge = bulge;
            Shortfall = shortfall;

            // Every core point plus the margin, and every point of the solid.
            Reach = Math.Max(geometry.Reach(halfExtents), farthest + margin);
        }
    }

    /// <summary>
    /// The contact cores already built, keyed exactly on what a build reads: the form's three genes and the three
    /// half-extents, as the bit patterns of their floats — D233, the coordinator's ruling of 2026-10-25.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A hit is the cold build's bits.</b> A build is a pure function of its six floats, so the core held for a
    /// key is the core a fresh build would give, to the bit. The key is the floats' bit patterns, not their values:
    /// -0 and 0 are two keys, and a NaN, which no form in range carries, matches nothing it is not identical to.
    /// Nothing is rounded or quantised, so a part never touches as a neighbouring form would.
    /// </para>
    /// <para>
    /// <b>Bounded, least recently used out first.</b> A newborn whose part did not mutate in form or size reuses its
    /// parent's core. Which entries are held decides only whether a build runs, never what it returns, so eviction
    /// changes no bit, and two worlds in one process can share the cache.
    /// </para>
    /// <para>
    /// <b>Thread-safe.</b> Reads and writes hold one lock; a build runs outside it. Two threads that miss on one key
    /// both build, build equal cores, and the first to file its own keeps the slot.
    /// </para>
    /// </remarks>
    public sealed class ContactCoreCache
    {
        /// <summary>The entries the process's cache holds: about 2.4 KB each, so about 10 MB at most.</summary>
        public const int SharedCapacity = 4096;

        /// <summary>The cache every family part reads through.</summary>
        public static readonly ContactCoreCache Shared = new ContactCoreCache(SharedCapacity);

        private readonly struct Key : IEquatable<Key>
        {
            private readonly int _profile, _section, _taper, _x, _y, _z;

            public Key(PartForm form, Float3 halfExtents)
            {
                _profile = BitConverter.SingleToInt32Bits(form.Profile);
                _section = BitConverter.SingleToInt32Bits(form.Section);
                _taper = BitConverter.SingleToInt32Bits(form.Taper);
                _x = BitConverter.SingleToInt32Bits(halfExtents.X);
                _y = BitConverter.SingleToInt32Bits(halfExtents.Y);
                _z = BitConverter.SingleToInt32Bits(halfExtents.Z);
            }

            public bool Equals(Key other) =>
                _profile == other._profile && _section == other._section && _taper == other._taper &&
                _x == other._x && _y == other._y && _z == other._z;

            public override bool Equals(object obj) => obj is Key other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = _profile;
                    hash = hash * 31 + _section;
                    hash = hash * 31 + _taper;
                    hash = hash * 31 + _x;
                    hash = hash * 31 + _y;
                    hash = hash * 31 + _z;
                    return hash;
                }
            }
        }

        private readonly int _capacity;
        private readonly Dictionary<Key, LinkedListNode<KeyValuePair<Key, ContactCoreBuild>>> _byKey;
        private readonly LinkedList<KeyValuePair<Key, ContactCoreBuild>> _recent;   // most recent first
        private readonly object _lock = new object();
        private long _builds, _hits;

        public ContactCoreCache(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "A cache holds at least one core.");

            _capacity = capacity;
            _byKey = new Dictionary<Key, LinkedListNode<KeyValuePair<Key, ContactCoreBuild>>>(capacity);
            _recent = new LinkedList<KeyValuePair<Key, ContactCoreBuild>>();
        }

        /// <summary>The most entries held.</summary>
        public int Capacity => _capacity;

        /// <summary>How many entries are held now.</summary>
        public int Count
        {
            get { lock (_lock) return _byKey.Count; }
        }

        /// <summary>How many cores this cache has built, a miss each.</summary>
        public long Builds => Interlocked.Read(ref _builds);

        /// <summary>How many asks found their core held.</summary>
        public long Hits => Interlocked.Read(ref _hits);

        /// <summary>The core of <paramref name="geometry"/> at <paramref name="halfExtents"/>: held, or built and held.</summary>
        public ContactCoreBuild For(Superellipsoid geometry, Float3 halfExtents)
        {
            if (geometry == null) throw new ArgumentNullException(nameof(geometry));

            var key = new Key(geometry.Form, halfExtents);
            lock (_lock)
            {
                if (_byKey.TryGetValue(key, out var node))
                {
                    _recent.Remove(node);
                    _recent.AddFirst(node);
                    Interlocked.Increment(ref _hits);
                    return node.Value.Value;
                }
            }

            var built = new ContactCoreBuild(geometry, halfExtents);
            Interlocked.Increment(ref _builds);

            lock (_lock)
            {
                // A thread that built the same key meanwhile filed an equal core; the first one filed is kept.
                if (_byKey.TryGetValue(key, out var raced)) return raced.Value.Value;

                var node = _recent.AddFirst(new KeyValuePair<Key, ContactCoreBuild>(key, built));
                _byKey.Add(key, node);

                if (_byKey.Count > _capacity)
                {
                    LinkedListNode<KeyValuePair<Key, ContactCoreBuild>> oldest = _recent.Last;
                    _recent.RemoveLast();
                    _byKey.Remove(oldest.Value.Key);
                }
            }

            return built;
        }
    }
}
