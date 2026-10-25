using System;

namespace Evosim.Core
{
    /// <summary>
    /// The gamma and beta functions, which .NET Standard 2.1 does not have — D233.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The superellipsoid's volume, centre of mass and inertia are beta functions of its
    /// exponents (<see cref="Superellipsoid"/>), so Core needs one. This is Lanczos's
    /// approximation with g = 7 and nine coefficients, the set Numerical Recipes and most
    /// libraries print, good to about 1e-15 relative for positive arguments. Below a half it
    /// goes through the reflection formula, which is where the shape's small exponents land.
    /// </para>
    /// <para>
    /// Plain arithmetic in double, with no tables beyond the nine numbers, so it gives the same
    /// bits on every .NET this project builds for. Nothing in a step calls it: a family part
    /// computes its factors once at development.
    /// </para>
    /// </remarks>
    public static class SpecialFunctions
    {
        private const double LanczosG = 7.0;

        private static readonly double[] Lanczos =
        {
            0.99999999999980993,
            676.5203681218851,
            -1259.1392167224028,
            771.32342877765313,
            -176.61502916214059,
            12.507343278686905,
            -0.13857109526572012,
            9.9843695780195716e-6,
            1.5056327351493116e-7,
        };

        /// <summary>The gamma function, for any argument that is not zero or a negative integer.</summary>
        public static double Gamma(double x)
        {
            if (x < 0.5)
            {
                // Reflection: Γ(x)·Γ(1 − x) = π / sin(πx).
                return Math.PI / (Math.Sin(Math.PI * x) * Gamma(1.0 - x));
            }

            x -= 1.0;
            double sum = Lanczos[0];
            for (int i = 1; i < Lanczos.Length; i++) sum += Lanczos[i] / (x + i);

            double t = x + LanczosG + 0.5;
            return Math.Sqrt(2.0 * Math.PI) * Math.Pow(t, x + 0.5) * Math.Exp(-t) * sum;
        }

        /// <summary>The natural logarithm of the gamma function, for a positive argument.</summary>
        /// <remarks>Separate from <see cref="Gamma"/> so a beta of large arguments cannot overflow.</remarks>
        public static double LogGamma(double x)
        {
            if (!(x > 0.0)) throw new ArgumentOutOfRangeException(nameof(x), x, "LogGamma takes a positive argument.");

            if (x < 0.5) return Math.Log(Math.PI / Math.Sin(Math.PI * x)) - LogGamma(1.0 - x);

            x -= 1.0;
            double sum = Lanczos[0];
            for (int i = 1; i < Lanczos.Length; i++) sum += Lanczos[i] / (x + i);

            double t = x + LanczosG + 0.5;
            return 0.5 * Math.Log(2.0 * Math.PI) + (x + 0.5) * Math.Log(t) - t + Math.Log(sum);
        }

        /// <summary>
        /// The beta function, B(x, y) = Γ(x)Γ(y)/Γ(x + y), the integral of
        /// u^(x−1)·(1 − u)^(y−1) over 0 to 1, for positive arguments.
        /// </summary>
        public static double Beta(double x, double y)
        {
            if (!(x > 0.0) || !(y > 0.0))
            {
                throw new ArgumentOutOfRangeException(nameof(x), $"Beta takes positive arguments, got ({x}, {y}).");
            }

            return Math.Exp(LogGamma(x) + LogGamma(y) - LogGamma(x + y));
        }
    }
}
