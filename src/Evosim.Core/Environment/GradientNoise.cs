using System;

namespace Evosim.Core
{
    /// <summary>
    /// Two-dimensional gradient noise, seeded, in a few octaves: the landscape the matter is
    /// seeded in as islands and deserts (D109, <c>RunConfig.MatterIslandWavelengthMetres</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Perlin's construction, with a PCG-seeded table.</b> A unit gradient is hung on every
    /// lattice point by a 256-entry permutation drawn from <see cref="Rng"/>, and the value at a
    /// point is the quintic-smoothed blend of the four corner dot products. The permutation is
    /// what makes the map a property of the seed: two worlds with one seed have one map, and
    /// <c>Rng</c>'s recurrence is fixed (CLAUDE.md), so the map replays on any runtime that
    /// rounds a float the same way. Nothing here uses <c>System.Random</c> or <c>Math.Sin</c>.
    /// </para>
    /// <para>
    /// <b>Octaves.</b> <see cref="Fbm"/> sums <see cref="Octaves"/> layers, each at twice the
    /// frequency and half the amplitude of the last, normalised so the sum stays within
    /// <c>±1</c>. Three octaves at a 40 m wavelength give islands of 20 to 40 m with ragged
    /// edges at 10 m; one octave gives smooth blobs. The wavelength is the first octave's
    /// lattice spacing.
    /// </para>
    /// <para>
    /// <b>Floats, deliberately.</b> The field is read once at seeding, so the cost is nothing
    /// and the choice is about identity: a float path rounds the same under Mono and RyuJIT
    /// only where it avoids fused multiply-adds, which the Editor's replay of a farm's seeding
    /// would otherwise part on. This class holds every product in a float before adding it.
    /// </para>
    /// </remarks>
    public sealed class GradientNoise
    {
        private const int TableSize = 256;

        private readonly byte[] _perm = new byte[TableSize * 2];

        /// <summary>The first octave's lattice spacing, in the units of the coordinates.</summary>
        public float Wavelength { get; }

        /// <summary>How many octaves <see cref="Fbm"/> sums; 1 to 8.</summary>
        public int Octaves { get; }

        public GradientNoise(ulong seed, float wavelength, int octaves = 3)
        {
            if (!(wavelength > 0f) || float.IsInfinity(wavelength))
            {
                throw new ArgumentOutOfRangeException(nameof(wavelength), wavelength, "A wavelength is positive and finite.");
            }

            if (octaves < 1 || octaves > 8)
            {
                throw new ArgumentOutOfRangeException(nameof(octaves), octaves, "One to eight octaves.");
            }

            Wavelength = wavelength;
            Octaves = octaves;

            // A Fisher–Yates shuffle of 0..255 from the seed, then doubled so that a lookup at
            // p[p[x] + y] never needs a modulus.
            var rng = new Rng(seed);
            var table = new byte[TableSize];
            for (int i = 0; i < TableSize; i++) table[i] = (byte)i;

            for (int i = TableSize - 1; i > 0; i--)
            {
                int j = rng.Range(i + 1);
                byte t = table[i];
                table[i] = table[j];
                table[j] = t;
            }

            for (int i = 0; i < TableSize; i++)
            {
                _perm[i] = table[i];
                _perm[i + TableSize] = table[i];
            }
        }

        /// <summary>One octave of gradient noise at a point, in about <c>[-1, 1]</c>.</summary>
        /// <remarks>
        /// Classic Perlin: the raw range of two-dimensional gradient noise with unit gradients is
        /// <c>±sqrt(2)/2</c>, and the result is scaled by <c>sqrt(2)</c> so that the extremes
        /// reach about <c>±1</c>.
        /// </remarks>
        public float Sample(float x, float z) => SampleAt(x, z, Wavelength);

        /// <summary>
        /// The octaves summed, each at twice the last's frequency and half its amplitude,
        /// normalised to about <c>[-1, 1]</c>.
        /// </summary>
        public float Fbm(float x, float z)
        {
            float sum = 0f;
            float amplitude = 1f;
            float norm = 0f;
            float fx = x;
            float fz = z;

            for (int o = 0; o < Octaves; o++)
            {
                float layer = SampleAt(fx, fz, Wavelength / (1 << o));
                float term = layer * amplitude;
                sum += term;
                norm += amplitude;
                amplitude *= 0.5f;

                // Each octave is offset by a lattice-unrelated shift so that every octave's
                // lattice points do not coincide at the origin, which would print the lattice
                // on the map.
                fx += 17.3f;
                fz += 29.1f;
            }

            return sum / norm;
        }

        private float SampleAt(float x, float z, float wavelength)
        {
            float fx = x / wavelength;
            float fz = z / wavelength;

            int x0 = FloorToInt(fx);
            int z0 = FloorToInt(fz);

            float dx = fx - x0;
            float dz = fz - z0;

            int xi = x0 & (TableSize - 1);
            int zi = z0 & (TableSize - 1);

            float n00 = Grad(_perm[_perm[xi] + zi], dx, dz);
            float n10 = Grad(_perm[_perm[xi + 1] + zi], dx - 1f, dz);
            float n01 = Grad(_perm[_perm[xi] + zi + 1], dx, dz - 1f);
            float n11 = Grad(_perm[_perm[xi + 1] + zi + 1], dx - 1f, dz - 1f);

            float u = Fade(dx);
            float v = Fade(dz);

            float a = Lerp(n00, n10, u);
            float b = Lerp(n01, n11, u);

            float value = Lerp(a, b, v);
            return value * 1.41421356f;
        }

        /// <summary>Eight unit-ish gradients, the classic set: the axes and the diagonals.</summary>
        private static float Grad(int hash, float x, float z)
        {
            switch (hash & 7)
            {
                case 0: return x;
                case 1: return -x;
                case 2: return z;
                case 3: return -z;
                case 4: return 0.70710678f * (x + z);
                case 5: return 0.70710678f * (x - z);
                case 6: return 0.70710678f * (-x + z);
                default: return 0.70710678f * (-x - z);
            }
        }

        /// <summary>Perlin's quintic: zero first and second derivatives at 0 and 1.</summary>
        private static float Fade(float t)
        {
            float t3 = t * t * t;
            float inner = t * 6f - 15f;
            float poly = t * inner + 10f;
            return t3 * poly;
        }

        private static float Lerp(float a, float b, float t)
        {
            float d = b - a;
            float scaled = d * t;
            return a + scaled;
        }

        private static int FloorToInt(float v)
        {
            int i = (int)v;
            return v < i ? i - 1 : i;
        }
    }
}
