using System;
using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>
    /// Three per-axis flags. Carries <c>MorphEdge.reflect</c> — DESIGN.md §4.1.
    /// </summary>
    /// <remarks>
    /// Draft 2 of the design made reflection a single boolean. [K12 §2.1, p.3] is explicit
    /// that there are three: <i>"if one, two or three reflection flags are enabled, two,
    /// four or eight mirrored copies of a child node are created in the phenotype graph."</i>
    /// Reflection is the only source of bilateral symmetry in this encoding, and symmetric
    /// creatures read as organisms rather than debris.
    /// </remarks>
    public readonly struct Bool3 : IEquatable<Bool3>
    {
        public readonly bool X;
        public readonly bool Y;
        public readonly bool Z;

        public Bool3(bool x, bool y, bool z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static readonly Bool3 None = new Bool3(false, false, false);

        public bool this[int axis]
        {
            get
            {
                switch (axis)
                {
                    case 0: return X;
                    case 1: return Y;
                    case 2: return Z;
                    default: throw new IndexOutOfRangeException("Axis must be 0, 1 or 2.");
                }
            }
        }

        public int EnabledCount => (X ? 1 : 0) + (Y ? 1 : 0) + (Z ? 1 : 0);

        /// <summary>Number of phenotype copies this edge produces: 1, 2, 4 or 8.</summary>
        public int CopyCount => 1 << EnabledCount;

        public Bool3 WithAxis(int axis, bool value)
        {
            switch (axis)
            {
                case 0: return new Bool3(value, Y, Z);
                case 1: return new Bool3(X, value, Z);
                case 2: return new Bool3(X, Y, value);
                default: throw new IndexOutOfRangeException("Axis must be 0, 1 or 2.");
            }
        }

        public Bool3 Toggled(int axis) => WithAxis(axis, !this[axis]);

        /// <summary>
        /// Every combination of the enabled axes, unmirrored copy first. One enabled flag
        /// yields <c>{none, X}</c>; two yield <c>{none, X, Y, XY}</c>; three yield all eight.
        /// </summary>
        public IEnumerable<Bool3> MirrorCombinations()
        {
            int[] active = new int[3];
            int n = 0;
            for (int axis = 0; axis < 3; axis++)
            {
                if (this[axis]) active[n++] = axis;
            }

            int combos = 1 << n;
            for (int mask = 0; mask < combos; mask++)
            {
                Bool3 combo = None;
                for (int bit = 0; bit < n; bit++)
                {
                    if ((mask & (1 << bit)) != 0) combo = combo.WithAxis(active[bit], true);
                }
                yield return combo;
            }
        }

        public bool Equals(Bool3 other) => X == other.X && Y == other.Y && Z == other.Z;

        public override bool Equals(object obj) => obj is Bool3 other && Equals(other);

        public override int GetHashCode() => (X ? 1 : 0) | (Y ? 2 : 0) | (Z ? 4 : 0);

        public override string ToString() => $"({X}, {Y}, {Z})";
    }
}
