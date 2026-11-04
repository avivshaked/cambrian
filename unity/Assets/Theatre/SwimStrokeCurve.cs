using System;

namespace Evosim.Theatre
{
    /// <summary>
    /// The owner's swimming stroke as a function of phase: ported line for line from
    /// <c>scripts/render-swim-film.py</c> (<c>_half</c>, <c>ease</c>, and the amplitude and rate
    /// <c>wags</c> computes), so the theatre draws the curve the owner accepted in that film.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Under D135 the solver never swings a powered limb: the joint's power pushes the body, and
    /// the stroke is drawn from the logged power (<see cref="TheatreSwimStroke"/>). This file is the
    /// curve and nothing else. It holds no <c>UnityEngine</c> type, in doubles, so a console test
    /// can compare it with the script to the last digit.
    /// </para>
    /// <para>
    /// Each half-stroke is a fast sweep across most of the way (<see cref="Reach"/> of the
    /// distance in <see cref="Stroke"/> of the time, with a minor ease in) and then a long ease out
    /// to the edge (<c>1 - (1 - t)^Glide</c>), the speed continuous where the two meet.
    /// </para>
    /// </remarks>
    public static class SwimStrokeCurve
    {
        /// <summary>The share of a half-stroke the fast sweep takes.</summary>
        public const double Stroke = 0.35;

        /// <summary>The share of the way the fast sweep covers.</summary>
        public const double Reach = 0.8;

        /// <summary>The exponent of the ease out to the edge.</summary>
        public const double Glide = 4.0;

        /// <summary>The slope the sweep starts with, as a multiple of a straight line over the sweep.</summary>
        public const double EaseIn = 0.25;

        /// <summary>Position 0 to 1 over one half-stroke, <paramref name="u"/> in [0, 1).</summary>
        public static double Half(double u)
        {
            if (u < Stroke)
            {
                // Cubic Hermite from 0 to Reach: a minor ease in (slope EaseIn), and at its end the
                // glide's starting speed, so the stroke hands over to the ease without a jolt.
                double t = u / Stroke;
                double b = (1 - Reach) * Glide / (1 - Stroke) * Stroke / Reach;
                double f = (Math.Pow(t, 3) - 2 * Math.Pow(t, 2) + t) * EaseIn +
                           (-2 * Math.Pow(t, 3) + 3 * Math.Pow(t, 2)) +
                           (Math.Pow(t, 3) - Math.Pow(t, 2)) * b;
                return Reach * f;
            }

            double s = (u - Stroke) / (1 - Stroke);
            return Reach + (1 - Reach) * (1 - Math.Pow(1 - s, Glide));
        }

        /// <summary>
        /// The wag at phase <paramref name="phi"/>, -1 to 1: at the edge -1 at phi = -pi/2 and +1 at
        /// pi/2, as sin is.
        /// </summary>
        public static double Ease(double phi)
        {
            const double Tau = 2 * Math.PI;

            // Python's % takes the divisor's sign, so a negative phase wraps into [0, 2 pi) as
            // C#'s does not: the same fmod and the same one add Python's float modulo makes.
            double r = (phi + Math.PI / 2) % Tau;
            if (r < 0) r += Tau;

            double u = r / Math.PI;
            return u < 1 ? -1 + 2 * Half(u) : 1 - 2 * Half(u - 1);
        }

        /// <summary>The drawn swing, radians, at power <paramref name="s"/>: 0.4 * sqrt(s).</summary>
        public static double Amplitude(double s) => 0.4 * Math.Sqrt(s);

        /// <summary>
        /// How fast the phase turns at power <paramref name="s"/>, radians per second: the rate
        /// that makes the swing's peak speed s * <paramref name="omega"/>, 2 pi f with
        /// f = s * omega / (2 pi * amplitude). 0 when <paramref name="s"/> is not above 0.
        /// </summary>
        public static double PhaseRate(double s, double omega)
        {
            if (s <= 0) return 0d;

            double amp = Amplitude(s);
            double f = s * omega / (2 * Math.PI * amp);
            return 2 * Math.PI * f;
        }
    }
}
