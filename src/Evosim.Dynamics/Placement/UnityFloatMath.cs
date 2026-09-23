using System;

namespace Evosim.Dynamics.Placement
{
    /// <summary>
    /// <c>UnityEngine.Mathf</c>, transcribed — the exact expressions Unity's own
    /// <c>Mathf</c> evaluates, with no engine behind them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why a transcription and not <see cref="MathF"/>.</b> The placer draws every founder
    /// and every newborn from one RNG stream and turns the draw into a position with
    /// <c>Mathf.Cos</c>, <c>Mathf.Sin</c> and <c>Mathf.Sqrt</c>. A single ulp of difference in
    /// any of them is a different position, a different <see cref="SharedVolume.Free"/> answer,
    /// a different number of rejections and therefore a different number of draws — and from
    /// the first birth on, a different world. So the port cannot reach for whatever the
    /// framework's fastest single-precision routine happens to be; it has to evaluate what
    /// Unity evaluates.
    /// </para>
    /// <para>
    /// Unity's <c>Mathf</c> is a thin wrapper over <c>System.Math</c>'s <b>double</b> routines
    /// with a cast back to <c>float</c> — <c>Sqrt(f)</c> is <c>(float)Math.Sqrt(f)</c>, not
    /// <c>MathF.Sqrt(f)</c>. The two are not the same function: <see cref="MathF.Sin"/> is the
    /// C runtime's single-precision <c>sinf</c>, correctly rounded from a single-precision
    /// argument, where Unity widens to double, calls <c>sin</c>, and rounds once at the end.
    /// For <c>Sqrt</c> the two provably agree (double rounding of a square root is exact at
    /// this precision ratio); for <c>Sin</c> and <c>Cos</c> they need not, so those two are the
    /// reason this file exists.
    /// </para>
    /// <para>
    /// <b><c>Max</c> is not <see cref="MathF.Max"/> either.</b> Unity's is the ternary
    /// <c>a &gt; b ? a : b</c>, which returns <c>b</c> when <c>a</c> is NaN, where
    /// <c>MathF.Max</c> propagates the NaN. The placer reads a live body's height through
    /// <c>Mathf.Max</c> (<see cref="SharedVolume.TryReserveOffspring"/>) and a diverged body's
    /// height can be NaN between two of <c>Ecosystem</c>'s finite checks — the class the placer
    /// was ported from takes care to leave such a body alone rather than teleport it, so the
    /// NaN path is reachable and its answer has to be Unity's.
    /// </para>
    /// <para>
    /// Only the members <see cref="SharedVolume"/> and <see cref="PlacementFloor"/> actually
    /// call are here. A member added later is added by transcription from Unity's source, not
    /// by picking the nearest thing in <see cref="MathF"/>.
    /// </para>
    /// </remarks>
    internal static class UnityFloatMath
    {
        /// <summary>
        /// <c>Mathf.PI</c> — the literal Unity declares, <c>3.14159274f</c>.
        /// </summary>
        /// <remarks>
        /// The nearest float to π, so it is the same bits as <c>(float)Math.PI</c> and as
        /// <see cref="MathF.PI"/>. Written as Unity writes it so that a reader comparing the
        /// two files sees the same constant rather than a claim that two constants agree.
        /// </remarks>
        public const float PI = 3.14159274F;

        /// <summary>Unity: <c>a &gt; b ? a : b</c>. See the class remarks on NaN.</summary>
        public static float Max(float a, float b) => a > b ? a : b;

        /// <summary>Unity: <c>a &gt; b ? a : b</c>.</summary>
        public static int Max(int a, int b) => a > b ? a : b;

        /// <summary>Unity: clamp by two comparisons, low bound first.</summary>
        public static int Clamp(int value, int min, int max)
        {
            if (value < min) value = min;
            else if (value > max) value = max;
            return value;
        }

        /// <summary>Unity: <c>(float)Math.Sqrt(f)</c>.</summary>
        public static float Sqrt(float f) => (float)Math.Sqrt(f);

        /// <summary>Unity: <c>(float)Math.Sin(f)</c> — the double routine, not <c>sinf</c>.</summary>
        public static float Sin(float f) => (float)Math.Sin(f);

        /// <summary>Unity: <c>(float)Math.Cos(f)</c> — the double routine, not <c>cosf</c>.</summary>
        public static float Cos(float f) => (float)Math.Cos(f);

        /// <summary>Unity: <c>(float)Math.Floor(f)</c>.</summary>
        public static float Floor(float f) => (float)Math.Floor(f);

        /// <summary>Unity: <c>(float)Math.Round(f)</c> — to even, which is both runtimes' default.</summary>
        public static float Round(float f) => (float)Math.Round(f);

        /// <summary>Unity: <c>(int)Math.Floor(f)</c> — the double floor, then the cast.</summary>
        public static int FloorToInt(float f) => (int)Math.Floor(f);

        /// <summary>Unity: <c>(int)Math.Ceiling(f)</c>.</summary>
        public static int CeilToInt(float f) => (int)Math.Ceiling(f);

        /// <summary>
        /// <c>UnityEngine.Vector3.Distance</c>, transcribed: the three differences and their
        /// squares in <c>float</c>, the sum widened once, and <c>Math.Sqrt</c> over the double.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Not <c>Float3.Magnitude</c> of the difference and not <see cref="Sqrt"/> of
        /// <c>SqrMagnitude</c> — those happen to agree here, but the guarantee this port owes
        /// is that the expression is the same one, so the expression is written out.
        /// </para>
        /// <para>
        /// <b>The same expression is not the same bits across the two runtimes</b> (the
        /// float-maths sweep of 2026-09-23, <c>FloatMathCheck</c> in the Editor against
        /// <c>Evosim.Farm --float-math</c>; CLAUDE.md's farm gotcha). .NET 8 rounds each
        /// product and sum here to a float; Mono holds the intermediates wider and rounds only
        /// at a cast, and Unity's own <c>Vector3.Distance</c> is a third evaluation. Over four
        /// million triples the inline form differs from the Editor's inline form in 8% of
        /// distances and from <c>Vector3.Distance</c> in about 8%, by one ulp, while the form
        /// that casts every product and sum to <c>float</c> gives .NET's bits in both runtimes
        /// (the sweep's <c>Distance/cast</c> line: 0 mismatches against the inline form on .NET,
        /// so the farm's recorded worlds keep their bits). So the line is written with the
        /// casts, which pins this expression to one rounding wherever the solver runs, the
        /// Editor's live mode included. What it does not pin is Unity's own placer, which calls
        /// <c>Vector3.Distance</c>: a placement that turns on that ulp is one more reason a live
        /// Editor world is a cousin of a farm run.
        /// </para>
        /// </remarks>
        public static float Distance(Evosim.Core.Float3 a, Evosim.Core.Float3 b)
        {
            float diffX = a.X - b.X;
            float diffY = a.Y - b.Y;
            float diffZ = a.Z - b.Z;

            return (float)Math.Sqrt(
                (float)((float)((float)(diffX * diffX) + (float)(diffY * diffY)) + (float)(diffZ * diffZ)));
        }
    }
}
