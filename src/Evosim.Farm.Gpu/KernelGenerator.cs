using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Evosim.Farm.Gpu
{
    /// <summary>
    /// Writes the GPU engine's two sources, double and single, from one template
    /// (logbook/specs/gpu-port-spec.md section 6): <c>gpu-kernel.template</c> and
    /// <c>gpu-runner.template</c> to <c>Kernels.D.g.cs</c> and <c>Kernels.F.g.cs</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One text, two precisions.</b> The spike's generator (spikes/02-gpu-featherstone/spike4/
    /// gen4.py) moved here so that the build owns it: the two files are committed, and
    /// <c>GpuKernelSourceTests</c> fails when either differs from what this writes, which is
    /// the spec's "run at build or checked by a test". Setting <c>EVOSIM_GPU_REGENERATE=1</c>
    /// for that test rewrites them.
    /// </para>
    /// <para>
    /// <b>Four size classes, one method each.</b> A block between <c>//@CLASS</c> and
    /// <c>//@ENDCLASS</c> is written once per class with <c>__CI__</c> its index, <c>__CL__</c>
    /// its link ceiling and <c>__CD__</c> three degrees of freedom a link. A thread's local
    /// arrays are sized by the ceiling, so a two-link body does not pay for sixteen.
    /// </para>
    /// <para>
    /// <b>The text is LF, and the files are written in the checkout's endings.</b> The template is
    /// read with its line breaks normalised, so a Windows checkout's CRLF generates the same text,
    /// and the test compares with the endings normalised. The files are written with the
    /// template's own endings on disk, so a regenerated file is byte for byte what a checkout of
    /// it holds and the manifest's <c>kernelHash</c>, which is over the bytes, does not move.
    /// </para>
    /// </remarks>
    public static class KernelGenerator
    {
        public const int ClassCount = 4;

        /// <summary>Each class's link ceiling; the last is DevelopmentLimits.MaxParts' default.</summary>
        public static readonly int[] ClassLinks = { 2, 4, 8, 16 };

        public const string KernelTemplateName = "gpu-kernel.template";
        public const string RunnerTemplateName = "gpu-runner.template";
        public const string DoubleName = "Kernels.D.g.cs";
        public const string SingleName = "Kernels.F.g.cs";

        private const string DoubleHelpers = @"
        // The CPU's own arithmetic: float operands promoted to double, System.Math, narrowed.
        private static float OscSin(double arg) => (float)Math.Sin(arg);
        private static float SinF(float a) => (float)Math.Sin(a);
        private static float CosF(float a) => (float)Math.Cos(a);
        private static float TanhF(float a) => (float)Math.Tanh(a);
        private static float FloorF(float a) => (float)Math.Floor(a);
        private static Real SqrtR(Real a) => Math.Sqrt(a);
        private static Real AbsR(Real a) => Math.Abs(a);
";

        private const string SingleHelpers = @"
        // Single on the card: the oscillator's argument is built in double as the CPU builds it,
        // reduced by whole turns in double, then narrowed; everything else is XMath in float.
        private static float OscSin(double arg)
        {
            const double Turn = 6.283185307179586;
            double reduced = arg - Turn * Math.Floor(arg / Turn);
            return XMath.Sin((float)reduced);
        }
        private static float SinF(float a) => XMath.Sin(a);
        private static float CosF(float a) => XMath.Cos(a);
        private static float TanhF(float a) => XMath.Tanh(a);
        private static float FloorF(float a) => XMath.Floor(a);
        private static Real SqrtR(Real a) => XMath.Sqrt(a);
        private static Real AbsR(Real a) => XMath.Abs(a);
";

        /// <summary>One precision's source from the two templates' text.</summary>
        public static string Generate(string kernelTemplate, string runnerTemplate, bool single)
        {
            if (kernelTemplate == null) throw new ArgumentNullException(nameof(kernelTemplate));
            if (runnerTemplate == null) throw new ArgumentNullException(nameof(runnerTemplate));

            string text = Lf(kernelTemplate) + "\n" + Lf(runnerTemplate);

            // The helpers are verbatim strings, which carry this file's own line endings.
            text = Helpers(text, Lf(single ? SingleHelpers : DoubleHelpers));
            text = Classes(text);

            text = text
                .Replace("__NS__", single ? "Evosim.Farm.Gpu.Sgl" : "Evosim.Farm.Gpu.Dbl")
                .Replace("__REALT__", single ? "System.Single" : "System.Double")
                .Replace("__M__", single ? "System.MathF" : "System.Math")
                .Replace("__PREC__", single ? "single" : "double")
                .Replace("__SIZE__", single ? "4" : "8");

            if (text.Contains("__", StringComparison.Ordinal))
            {
                int at = text.IndexOf("__", StringComparison.Ordinal);
                int end = text.IndexOf('\n', at);
                string line = text.Substring(at, (end < 0 ? text.Length : end) - at);
                if (IsPlaceholder(line))
                {
                    throw new InvalidOperationException("A placeholder was left unfilled: " + line.Trim());
                }
            }

            return text;
        }

        /// <summary>Both sources from the templates in <paramref name="directory"/>, as (name, text).</summary>
        public static (string Name, string Text)[] GenerateAll(string directory)
        {
            string kernel = File.ReadAllText(Path.Combine(directory, KernelTemplateName), Encoding.UTF8);
            string runner = File.ReadAllText(Path.Combine(directory, RunnerTemplateName), Encoding.UTF8);

            return new[]
            {
                (DoubleName, Generate(kernel, runner, false)),
                (SingleName, Generate(kernel, runner, true)),
            };
        }

        /// <summary>Writes both sources beside the templates.</summary>
        public static void WriteAll(string directory)
        {
            string template = File.ReadAllText(Path.Combine(directory, KernelTemplateName), Encoding.UTF8);
            bool crlf = template.Contains("\r\n");

            foreach ((string name, string text) in GenerateAll(directory))
            {
                string written = crlf ? text.Replace("\n", "\r\n") : text;
                File.WriteAllText(Path.Combine(directory, name), written, new UTF8Encoding(false));
            }
        }

        /// <summary>This project's source directory, found by walking up from <paramref name="start"/>.</summary>
        public static string FindSourceDirectory(string start)
        {
            for (DirectoryInfo d = new DirectoryInfo(start); d != null; d = d.Parent)
            {
                string candidate = Path.Combine(d.FullName, "src", "Evosim.Farm.Gpu");
                if (File.Exists(Path.Combine(candidate, KernelTemplateName))) return candidate;
            }

            throw new DirectoryNotFoundException(
                "No src/Evosim.Farm.Gpu/" + KernelTemplateName + " above " + start + ".");
        }

        public static string Lf(string text) => text.Replace("\r\n", "\n");

        private static string Helpers(string text, string helpers)
        {
            const string open = "//@HELPERS";
            const string close = "//@END\n";

            int at = text.IndexOf(open, StringComparison.Ordinal);
            if (at < 0) throw new InvalidOperationException("The kernel template has no " + open + " block.");

            int end = text.IndexOf(close, at, StringComparison.Ordinal);
            if (end < 0) throw new InvalidOperationException("The " + open + " block is not closed.");

            if (text.IndexOf(open, at + open.Length, StringComparison.Ordinal) >= 0)
            {
                throw new InvalidOperationException("The templates carry two " + open + " blocks.");
            }

            return text.Substring(0, at) + helpers.Trim('\n') + "\n" + text.Substring(end + close.Length);
        }

        private static string Classes(string text)
        {
            const string open = "//@CLASS\n";
            const string close = "//@ENDCLASS\n";

            var result = new StringBuilder(text.Length * 2);
            int from = 0;

            while (true)
            {
                int at = text.IndexOf(open, from, StringComparison.Ordinal);
                if (at < 0)
                {
                    result.Append(text, from, text.Length - from);
                    break;
                }

                int end = text.IndexOf(close, at, StringComparison.Ordinal);
                if (end < 0) throw new InvalidOperationException("A //@CLASS block is not closed.");

                result.Append(text, from, at - from);
                string body = text.Substring(at + open.Length, end - at - open.Length);

                for (int c = 0; c < ClassCount; c++)
                {
                    result.Append(body
                        .Replace("__CI__", c.ToString(CultureInfo.InvariantCulture))
                        .Replace("__CL__", ClassLinks[c].ToString(CultureInfo.InvariantCulture))
                        .Replace("__CD__", (3 * ClassLinks[c]).ToString(CultureInfo.InvariantCulture)));
                }

                from = end + close.Length;
            }

            return result.ToString();
        }

        private static bool IsPlaceholder(string s)
        {
            // __NAME__ in capitals: the template's own convention. Anything else with a double
            // underscore (none today) is left alone.
            if (s.Length < 5 || !s.StartsWith("__", StringComparison.Ordinal)) return false;
            int close = s.IndexOf("__", 2, StringComparison.Ordinal);
            if (close < 3) return false;
            for (int k = 2; k < close; k++)
            {
                char ch = s[k];
                if (!(ch >= 'A' && ch <= 'Z')) return false;
            }

            return true;
        }
    }
}
