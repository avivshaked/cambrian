using System;

namespace Evosim.Core
{
    /// <summary>
    /// Deterministic PRNG — PCG-XSH-RR 64/32 (O'Neill 2014).
    /// </summary>
    /// <remarks>
    /// DESIGN.md §7 requires every evaluation to be defined by
    /// <c>(genome, seed, configHash)</c> with no ambient randomness. <c>System.Random</c>
    /// is unsuitable: its algorithm is not contractually stable across .NET versions, and
    /// .NET Core changed it. PCG is a fixed integer recurrence, so a seed means the same
    /// stream on every runtime and platform — which is the only part of reproducibility
    /// this project can actually promise, given that PhysX itself is not bitwise
    /// deterministic across machines (§7).
    /// </remarks>
    public sealed class Rng
    {
        private const ulong Multiplier = 6364136223846793005UL;

        private ulong _state;
        private readonly ulong _increment;

        private bool _hasSpareGaussian;
        private float _spareGaussian;

        /// <summary>The seed this generator was constructed with, for logging alongside results.</summary>
        public ulong Seed { get; }

        public Rng(ulong seed, ulong sequence = 1UL)
        {
            Seed = seed;
            _increment = (sequence << 1) | 1UL;
            _state = 0UL;
            NextUInt();
            _state += seed;
            NextUInt();
        }

        public uint NextUInt()
        {
            ulong old = _state;
            _state = old * Multiplier + _increment;

            uint xorshifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
        }

        /// <summary>Uniform in [0, 1).</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

        /// <summary>Uniform in [min, max).</summary>
        public float Range(float min, float max) => min + (max - min) * NextFloat();

        /// <summary>Uniform integer in [0, exclusiveMax). Rejection-sampled, so unbiased.</summary>
        public int Range(int exclusiveMax)
        {
            if (exclusiveMax <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMax));

            uint bound = (uint)exclusiveMax;
            uint threshold = (uint)((0x100000000UL - bound) % bound);
            while (true)
            {
                uint r = NextUInt();
                if (r >= threshold) return (int)(r % bound);
            }
        }

        /// <summary>Uniform integer in [min, exclusiveMax).</summary>
        public int Range(int min, int exclusiveMax) => min + Range(exclusiveMax - min);

        public bool Chance(float probability) => NextFloat() < probability;

        public T Pick<T>(T[] items)
        {
            if (items == null || items.Length == 0) throw new ArgumentException("Cannot pick from an empty set.", nameof(items));
            return items[Range(items.Length)];
        }

        /// <summary>Normal deviate, mean 0 and unit variance. Box–Muller, with the second value cached.</summary>
        public float Gaussian()
        {
            if (_hasSpareGaussian)
            {
                _hasSpareGaussian = false;
                return _spareGaussian;
            }

            float u, v, s;
            do
            {
                u = NextFloat() * 2f - 1f;
                v = NextFloat() * 2f - 1f;
                s = u * u + v * v;
            }
            while (s >= 1f || s == 0f);

            float scale = (float)System.Math.Sqrt(-2f * System.Math.Log(s) / s);
            _spareGaussian = v * scale;
            _hasSpareGaussian = true;
            return u * scale;
        }

        public float Gaussian(float mean, float stdDev) => mean + Gaussian() * stdDev;

        public Float3 NextFloat3(float min, float max) =>
            new Float3(Range(min, max), Range(min, max), Range(min, max));

        /// <summary>A rotation drawn uniformly from SO(3) (Shoemake's method).</summary>
        public Quat NextRotation()
        {
            float u1 = NextFloat(), u2 = NextFloat(), u3 = NextFloat();
            float s1 = (float)System.Math.Sqrt(1f - u1);
            float s2 = (float)System.Math.Sqrt(u1);
            const float TwoPi = 6.28318530718f;

            return new Quat(
                s1 * (float)System.Math.Sin(TwoPi * u2),
                s1 * (float)System.Math.Cos(TwoPi * u2),
                s2 * (float)System.Math.Sin(TwoPi * u3),
                s2 * (float)System.Math.Cos(TwoPi * u3));
        }

        /// <summary>
        /// A generator derived from this one, for a subsystem that must not perturb the
        /// parent stream's sequence.
        /// </summary>
        public Rng Fork(ulong sequence) => new Rng(NextUInt() | ((ulong)NextUInt() << 32), sequence);
    }
}
