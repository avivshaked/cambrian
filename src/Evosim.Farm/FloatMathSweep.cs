using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Evosim.Farm
{
    /// <summary>
    /// The farm's half of the placer's float-maths check: the same sweep the Editor's
    /// <c>Evosim.Theatre.EditorTools.FloatMathCheck</c> runs over <c>UnityEngine.Mathf</c>, run here
    /// over the transcription's expressions on .NET, printing one digest per function for the
    /// comparison.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What the two files decide between them.</b> <c>Evosim.Dynamics.Placement.UnityFloatMath</c>
    /// is a transcription of <c>Mathf</c>: <c>(float)Math.Sin(f)</c> for <c>Mathf.Sin(f)</c>, and so
    /// on, because the placer turns one RNG draw into a position through those routines and one ulp
    /// is a different world. The Editor check asks whether the transcription is right in Unity's
    /// runtime; this sweep asks whether .NET 8 evaluates the transcribed expression to the same bits
    /// as Mono does, which the first check alone cannot know. Both print FNV-1a digests over the
    /// result bits of one integer-generated sweep; equal digests mean a founding placed on the farm
    /// is placed where the Editor would place it, function by function.
    /// </para>
    /// <para>
    /// <b>The multi-operation expressions are evaluated twice</b>, inline and narrowed to float
    /// by an explicit cast at every operation, as the Editor's twin does, because ECMA-335 lets a
    /// runtime hold a float intermediate wider than a float and C# promises the rounding only at
    /// a cast, so the two runtimes need not agree. The line's two digests are the inline and the
    /// cast form.
    /// </para>
    /// <para>
    /// <b>The expressions are written out</b> rather than called on <c>UnityFloatMath</c>, which is
    /// internal to the solver's assembly and is not widened for a check. Each is the one line that
    /// file carries, and a reader comparing the two sees the same line.
    /// </para>
    /// <code>
    /// Evosim.Farm --float-math [scratch/floatmath/dotnet.txt]
    /// </code>
    /// </remarks>
    internal static class FloatMathSweep
    {
        private const int RandomWords = 4_000_000;
        private const int AngleSteps = 1_000_000;
        private const uint Seed = 0x9E3779B9u;
        private const float PI = 3.14159274F;

        private sealed class Tally
        {
            public readonly string Name;
            public ulong Digest = Fnv.Offset;
            public ulong Other = Fnv.Offset;
            public int Count, Mismatches;
            public Tally(string name) { Name = name; }
        }

        public static int Run(string outPath)
        {
            var sin = new Tally("Sin"); var cos = new Tally("Cos"); var sqrt = new Tally("Sqrt");
            var round = new Tally("Round"); var floor = new Tally("Floor");
            var floorInt = new Tally("FloorToInt"); var ceilInt = new Tally("CeilToInt");
            var distance = new Tally("Distance"); var distanceCast = new Tally("Distance/cast");
            var sinA = new Tally("Sin(angle)"); var cosA = new Tally("Cos(angle)");
            var angle = new Tally("angle/cast"); var fold = new Tally("Fold");

            uint word = Seed;
            float[] triple = new float[6];
            int filled = 0;

            for (int i = 0; i < RandomWords; i++)
            {
                word = Xorshift(word);
                float f = BitConverter.ToSingle(BitConverter.GetBytes(word), 0);
                if (float.IsNaN(f) || float.IsInfinity(f)) continue;

                float sinF = (float)Math.Sin(f); Add(sin, sinF, sinF);
                float cosF = (float)Math.Cos(f); Add(cos, cosF, cosF);
                float a = Math.Abs(f);
                float sqrtF = (float)Math.Sqrt(a); Add(sqrt, sqrtF, sqrtF);
                float roundF = (float)Math.Round(f); Add(round, roundF, roundF);
                float floorF = (float)Math.Floor(f); Add(floor, floorF, floorF);

                if (a < 1e9f)
                {
                    AddInt(floorInt, (int)Math.Floor(f));
                    AddInt(ceilInt, (int)Math.Ceiling(f));
                }

                if (a < 1000f)
                {
                    const float extent = 20f;
                    float inlineFold = f - extent * (float)Math.Round(f / extent);
                    float q = f / extent;
                    float r = (float)Math.Round(q);
                    float m = extent * r;
                    float storedFold = f - m;
                    Add(fold, inlineFold, storedFold);

                    triple[filled++] = f;
                    if (filled == 6)
                    {
                        filled = 0;
                        float dx = triple[0] - triple[3], dy = triple[1] - triple[4], dz = triple[2] - triple[5];
                        float inlineDistance = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                        float castDistance = (float)Math.Sqrt(
                            (float)((float)((float)(dx * dx) + (float)(dy * dy)) + (float)(dz * dz)));
                        Add(distance, inlineDistance, inlineDistance);
                        Add(distanceCast, inlineDistance, castDistance);
                    }
                }
            }

            for (int i = 0; i < AngleSteps; i++)
            {
                float t = (float)i / AngleSteps * (2f * PI);
                float tCast = (float)((float)((float)i / AngleSteps) * (2f * PI));
                Add(angle, t, tCast);
                float sinT = (float)Math.Sin(t); Add(sinA, sinT, sinT);
                float cosT = (float)Math.Cos(t); Add(cosA, cosT, cosT);
            }

            float maxA = Max(float.NaN, 1f);
            float maxB = Max(1f, float.NaN);
            bool maxOk = maxA == 1f && float.IsNaN(maxB);

            var lines = new StringBuilder();
            foreach (Tally t in new[] { sin, cos, sqrt, round, floor, floorInt, ceilInt, sinA, cosA }) lines.AppendLine(Line(t));
            foreach (Tally t in new[] { distance, distanceCast, angle, fold }) lines.AppendLine(Line(t));
            lines.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "Max(NaN,1)={0} Max(1,NaN)={1} ok={2}", maxA, maxB, maxOk));
            lines.AppendLine("PI ok=" + (PI == (float)Math.PI));
            lines.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "runtime={0} expression mismatches: Distance/cast {1} angle/cast {2} Fold {3}",
                System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                distanceCast.Mismatches, angle.Mismatches, fold.Mismatches));

            if (!string.IsNullOrEmpty(outPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
                File.WriteAllText(outPath, lines.ToString());
            }
            Console.Write(lines.ToString());
            return maxOk ? 0 : 1;
        }

        /// <summary>Unity's <c>Mathf.Max</c>: <c>a &gt; b ? a : b</c>.</summary>
        private static float Max(float a, float b) => a > b ? a : b;

        private static void Add(Tally t, float first, float second)
        {
            uint bits = Bits(first), other = Bits(second);
            t.Digest = Fnv.Add(t.Digest, bits);
            t.Other = Fnv.Add(t.Other, other);
            t.Count++;
            if (bits != other) t.Mismatches++;
        }

        private static void AddInt(Tally t, int value)
        {
            t.Digest = Fnv.Add(t.Digest, (uint)value);
            t.Other = Fnv.Add(t.Other, (uint)value);
            t.Count++;
        }

        private static uint Bits(float f) => (uint)BitConverter.ToInt32(BitConverter.GetBytes(f), 0);

        private static string Line(Tally t)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} {1:x16} {2:x16} n={3} mismatches={4}",
                t.Name, t.Digest, t.Other, t.Count, t.Mismatches);
        }

        private static uint Xorshift(uint x)
        {
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            return x;
        }

        private static class Fnv
        {
            public const ulong Offset = 14695981039346656037UL;
            private const ulong Prime = 1099511628211UL;

            public static ulong Add(ulong h, uint word)
            {
                for (int i = 0; i < 4; i++)
                {
                    h ^= (word >> (8 * i)) & 0xFFu;
                    h *= Prime;
                }
                return h;
            }
        }
    }
}
