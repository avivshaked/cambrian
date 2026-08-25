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

        /// <summary>
        /// A seed for item <paramref name="index"/> of run <paramref name="stream"/>, decorrelated
        /// from every other pair — SplitMix64's finaliser (Steele, Lea &amp; Flood 2014).
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Consecutive seeds do not give independent runs, and this project ran on the
        /// assumption that they did.</b> <see cref="World"/> issued each creature a seed from a
        /// counter started at the run's own seed, so a run seeded 1 drew its founders from seeds
        /// 1…40 and a run seeded 2 drew from 2…41 — thirty-nine of the same forty genomes. Two
        /// "independent" runs were one experiment offset by a single creature, which is how they
        /// came to report a fastest-ever speed identical to four significant figures
        /// (logbook/0019). Every earlier claim of the form "consistent across three seeds" rests
        /// on much less than it appears to; the calibration in logbook/0017 is the one that
        /// matters.
        /// </para>
        /// <para>
        /// A mixing function rather than a wider stride. Striding by a million makes the overlap
        /// unlikely rather than impossible and leaves the streams correlated in a way nobody would
        /// think to check; the failure it replaces was itself the plausible-looking kind. This is
        /// a bijection on 64 bits with avalanche, so adjacent inputs give unrelated outputs and no
        /// two pairs collide.
        /// </para>
        /// <para>
        /// Fixed integer arithmetic, for the reason the class exists at all: §7 promises a seed
        /// means a sequence, and that only holds if the recurrence never changes.
        /// </para>
        /// </remarks>
        public static ulong SeedFor(ulong stream, ulong index)
        {
            // Golden-ratio odd constant: distinct (stream, index) pairs land on distinct inputs
            // for any run of indices shorter than 2^64.
            ulong z = stream * 0x9E3779B97F4A7C15UL + index;

            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
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
