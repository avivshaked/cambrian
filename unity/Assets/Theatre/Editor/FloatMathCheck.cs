using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Evosim.Theatre.EditorTools
{
    /// <summary>
    /// Is <c>UnityEngine.Mathf</c> the transcription the farm's placer carries? The farm's
    /// <c>Evosim.Dynamics.Placement.UnityFloatMath</c> evaluates <c>(float)Math.Sin(f)</c> where the
    /// Unity placer evaluates <c>Mathf.Sin(f)</c>, on the claim that the second is the first. This
    /// entry evaluates both in the Editor over one fixed sweep and counts the bits that differ.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two questions, one sweep.</b> The first is the transcription: in this runtime, is
    /// <c>Mathf.Sin(f)</c> bit for bit <c>(float)Math.Sin(f)</c>, and the same for <c>Cos</c>,
    /// <c>Sqrt</c>, <c>Round</c>, <c>Floor</c>, <c>FloorToInt</c>, <c>CeilToInt</c>, <c>Max</c> on a NaN
    /// and <c>Vector3.Distance</c>. A mismatch there means the transcription is wrong and the farm's
    /// placer is not Unity's. The second is the runtime: the digest of each function's results over
    /// the sweep, printed so that the same sweep evaluated under the farm's .NET 8
    /// (<c>Evosim.Farm --float-math</c>) can be held against it. Mono and RyuJIT already disagree on
    /// a double sum (CLAUDE.md), and whether they agree on <c>sin</c> decides whether a founding
    /// on the farm places its bodies where the Editor would.
    /// </para>
    /// <para>
    /// <b>The sweep is integer-generated</b>, an xorshift over 32-bit words, so that both runtimes
    /// draw the same floats without either of them evaluating anything but the function under test.
    /// A word is a float's bits; a non-finite draw is skipped, and an angle sweep of a million
    /// evenly spaced floats over [0, 2π] is added because the placer's arguments to <c>Sin</c> and
    /// <c>Cos</c> are angles. The digest is FNV-1a over the result bits in sweep order.
    /// </para>
    /// <para>
    /// <b>A float expression is a third question.</b> The first run (2026-09-23) found every one
    /// of its 2,912 mismatches in <c>Vector3.Distance</c> against the inline expression
    /// <c>(float)Math.Sqrt(dx * dx + dy * dy + dz * dz)</c>, one ulp apart in 0.8% of triples in
    /// the same runtime, and the angle sweep's digests apart across runtimes while the random
    /// sweep's agreed. ECMA-335 lets a runtime hold a float intermediate wider than a float, and
    /// C# promises a rounding to float only at an explicit cast, so the same source can round once
    /// or three times. Each multi-operation expression is therefore evaluated more than once here,
    /// inline, narrowed by a cast at every operation and, for the distance, as Unity's own
    /// <c>(a - b).magnitude</c>, and every digest is printed; which form Unity's assembly matches
    /// says how Unity compiled it, and which the farm matches says how .NET compiled the placer.
    /// </para>
    /// <para>
    /// It lives beside the theatre and not under <c>Assets/Evosim</c>, for <c>simHash</c>'s sake
    /// (<see cref="DynamicsPackageCheck"/>). It writes one line per digest to
    /// <c>scratch/floatmath/editor.txt</c> under the repository root, which is what the comparison
    /// reads, and the same lines to the log.
    /// </para>
    /// <code>
    /// Start-Process -FilePath $unity -Wait -NoNewWindow -ArgumentList @(
    ///   '-projectPath', "$PWD/unity-w6", '-batchmode', '-quit', '-nographics',
    ///   '-executeMethod', 'Evosim.Theatre.EditorTools.FloatMathCheck.Run',
    ///   '-logFile', "$PWD/scratch/logs/floatmath-editor.log")
    /// </code>
    /// </remarks>
    public static class FloatMathCheck
    {
        private const int RandomWords = 4_000_000;
        private const int AngleSteps = 1_000_000;
        private const uint Seed = 0x9E3779B9u;

        private sealed class Tally
        {
            public readonly string Name;
            public ulong Digest = Fnv.Offset;
            public ulong Other = Fnv.Offset;
            public int Count, Mismatches;
            public Tally(string name) { Name = name; }
        }

        [MenuItem("Evosim/Theatre/Float Math Check")]
        public static void Run()
        {
            int code = 1;
            try
            {
                var report = new StringBuilder();
                var sin = new Tally("Sin"); var cos = new Tally("Cos"); var sqrt = new Tally("Sqrt");
                var round = new Tally("Round"); var floor = new Tally("Floor");
                var floorInt = new Tally("FloorToInt"); var ceilInt = new Tally("CeilToInt");
                var distance = new Tally("Distance");                 // Vector3.Distance against the inline expression
                var distanceCast = new Tally("Distance/cast");        // Vector3.Distance against the expression narrowed by a cast at every operation
                var distanceMagnitude = new Tally("Distance/magnitude"); // Vector3.Distance against (a - b).magnitude
                var sinA = new Tally("Sin(angle)"); var cosA = new Tally("Cos(angle)");
                var angle = new Tally("angle/cast");                  // the inline argument against the cast-narrowed one
                var fold = new Tally("Fold");                         // d - extent * Round(d / extent), SharedVolume's wrap

                uint word = Seed;
                float[] triple = new float[6];
                int filled = 0;

                for (int i = 0; i < RandomWords; i++)
                {
                    word = Xorshift(word);
                    float f = BitConverter.ToSingle(BitConverter.GetBytes(word), 0);
                    if (float.IsNaN(f) || float.IsInfinity(f)) continue;

                    Check(sin, Mathf.Sin(f), (float)Math.Sin(f), f, report);
                    Check(cos, Mathf.Cos(f), (float)Math.Cos(f), f, report);
                    float a = Math.Abs(f);
                    Check(sqrt, Mathf.Sqrt(a), (float)Math.Sqrt(a), a, report);
                    Check(round, Mathf.Round(f), (float)Math.Round(f), f, report);
                    Check(floor, Mathf.Floor(f), (float)Math.Floor(f), f, report);

                    if (a < 1e9f)
                    {
                        CheckInt(floorInt, Mathf.FloorToInt(f), (int)Math.Floor(f), f, report);
                        CheckInt(ceilInt, Mathf.CeilToInt(f), (int)Math.Ceiling(f), f, report);
                    }

                    if (a < 1000f)
                    {
                        // SharedVolume's periodic fold, the placer's other multi-operation expression.
                        const float extent = 20f;
                        float inlineFold = f - extent * Mathf.Round(f / extent);
                        float q = f / extent;
                        float r = Mathf.Round(q);
                        float m = extent * r;
                        float storedFold = f - m;
                        Check(fold, inlineFold, storedFold, f, report);

                        triple[filled++] = f;
                        if (filled == 6)
                        {
                            filled = 0;
                            var p = new Vector3(triple[0], triple[1], triple[2]);
                            var q3 = new Vector3(triple[3], triple[4], triple[5]);
                            float dx = triple[0] - triple[3], dy = triple[1] - triple[4], dz = triple[2] - triple[5];
                            float inlineDistance = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                            // An explicit cast to float is the one thing the C# specification says
                            // rounds an intermediate to float; a local store need not.
                            float castDistance = (float)Math.Sqrt(
                                (float)((float)((float)(dx * dx) + (float)(dy * dy)) + (float)(dz * dz)));
                            float unity = Vector3.Distance(p, q3);
                            Check(distance, unity, inlineDistance, dx, report);
                            Check(distanceCast, unity, castDistance, dx, report);
                            Check(distanceMagnitude, unity, (p - q3).magnitude, dx, report);
                        }
                    }
                }

                for (int i = 0; i < AngleSteps; i++)
                {
                    float t = (float)i / AngleSteps * (2f * Mathf.PI);
                    float tCast = (float)((float)((float)i / AngleSteps) * (2f * Mathf.PI));
                    Check(angle, t, tCast, i, report);
                    Check(sinA, Mathf.Sin(t), (float)Math.Sin(t), t, report);
                    Check(cosA, Mathf.Cos(t), (float)Math.Cos(t), t, report);
                }

                float maxA = Mathf.Max(float.NaN, 1f);
                float maxB = Mathf.Max(1f, float.NaN);
                bool maxOk = maxA == 1f && float.IsNaN(maxB);
                bool piOk = Mathf.PI == 3.14159274F && Mathf.PI == (float)Math.PI;

                var lines = new StringBuilder();
                int transcriptionMismatches = 0;
                foreach (Tally t in new[] { sin, cos, sqrt, round, floor, floorInt, ceilInt, sinA, cosA })
                {
                    lines.AppendLine(Line(t));
                    transcriptionMismatches += t.Mismatches;
                }
                foreach (Tally t in new[] { distance, distanceCast, distanceMagnitude, angle, fold }) lines.AppendLine(Line(t));
                lines.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "Max(NaN,1)={0} Max(1,NaN)={1} ok={2}", maxA, maxB, maxOk));
                lines.AppendLine("PI ok=" + piOk);
                lines.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "runtime={0} transcription mismatches={1} expression mismatches: Distance {2} Distance/cast {3} Distance/magnitude {4} angle/cast {5} Fold {6}",
                    RuntimeName(), transcriptionMismatches, distance.Mismatches, distanceCast.Mismatches,
                    distanceMagnitude.Mismatches, angle.Mismatches, fold.Mismatches));

                string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
                string dir = Path.Combine(root, "scratch", "floatmath");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "editor.txt"), lines.ToString());

                Debug.Log("[FloatMathCheck]\n" + lines + (report.Length > 0 ? "first mismatches:\n" + report : ""));
                bool pass = transcriptionMismatches == 0 && maxOk && piOk;
                Debug.Log(pass
                    ? "[FloatMathCheck] PASS: Mathf is the transcription on this runtime (expression digests above)"
                    : "[FloatMathCheck] FAIL: " + transcriptionMismatches + " transcription mismatches");
                code = pass ? 0 : 1;
            }
            catch (Exception e)
            {
                Debug.LogError("[FloatMathCheck] " + e);
            }

            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        private static string RuntimeName()
        {
            return "Unity " + Application.unityVersion + " / " + Environment.Version + " (Mono " +
                   (Type.GetType("Mono.Runtime") != null ? "yes" : "no") + ")";
        }

        /// <summary>
        /// <c>name digest-of-first digest-of-second n=count mismatches=k</c>: the first digest is
        /// Unity's side (or the inline expression), the second the transcription's (or the stored one).
        /// </summary>
        private static string Line(Tally t)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} {1:x16} {2:x16} n={3} mismatches={4}",
                t.Name, t.Digest, t.Other, t.Count, t.Mismatches);
        }

        private static void Check(Tally t, float first, float second, float input, StringBuilder report)
        {
            int bits = BitConverter.ToInt32(BitConverter.GetBytes(first), 0);
            int other = BitConverter.ToInt32(BitConverter.GetBytes(second), 0);
            t.Digest = Fnv.Add(t.Digest, (uint)bits);
            t.Other = Fnv.Add(t.Other, (uint)other);
            t.Count++;
            if (bits == other) return;
            t.Mismatches++;
            if (t.Mismatches <= 3)
            {
                report.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "  {0}({1:R}): first {2:R} [{3:x8}] second {4:R} [{5:x8}]",
                    t.Name, input, first, bits, second, other));
            }
        }

        private static void CheckInt(Tally t, int first, int second, float input, StringBuilder report)
        {
            t.Digest = Fnv.Add(t.Digest, (uint)first);
            t.Other = Fnv.Add(t.Other, (uint)second);
            t.Count++;
            if (first == second) return;
            t.Mismatches++;
            if (t.Mismatches <= 3)
            {
                report.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "  {0}({1:R}): first {2} second {3}", t.Name, input, first, second));
            }
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
