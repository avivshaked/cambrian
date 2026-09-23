#!/usr/bin/env python3
"""Writes the third spike's kernels, one source and two precisions, as gen.py does for the second.

    python spike3/gen3.py

contact.template -> ContactD.cs (namespace Gpu.Spike3.Dbl) and ContactF.cs (Gpu.Spike3.Sgl)
brain.template   -> BrainD.cs   and BrainF.cs

The brain template carries a block of precision helpers between //@HELPERS and //@END: the double
build calls System.Math through double exactly as Evosim.Core does, the single build calls
ILGPU.Algorithms.XMath in float. Everything else is one text.
"""
import pathlib

HERE = pathlib.Path(__file__).parent

VARIANTS = [
    ("D", "Gpu.Spike3.Dbl", "System.Double", "System.Math", "double"),
    ("F", "Gpu.Spike3.Sgl", "System.Single", "System.MathF", "single"),
]

HELPERS = {
    "D": """
        // The CPU's own arithmetic: float operands promoted to double, System.Math, narrowed.
        private static float OscSin(double arg) => (float)Math.Sin(arg);
        private static float SinF(float a) => (float)Math.Sin(a);
        private static float CosF(float a) => (float)Math.Cos(a);
        private static float TanhF(float a) => (float)Math.Tanh(a);
        private static float FloorF(float a) => (float)Math.Floor(a);
        private static Real SqrtR(Real a) => Math.Sqrt(a);
        private static Real AbsR(Real a) => Math.Abs(a);
""",
    "F": """
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
""",
}


def emit(template, out, suffix, ns, real, math, prec):
    text = (HERE / template).read_text(encoding="utf-8")
    if "//@HELPERS" in text:
        head, rest = text.split("//@HELPERS", 1)
        _, tail = rest.split("//@END", 1)
        text = head + HELPERS[suffix].strip("\n") + "\n" + tail
    text = (text.replace("__NS__", ns)
            .replace("__REALT__", real)
            .replace("__M__", math)
            .replace("__PREC__", prec)
            .replace("__CAND__", "256")
            .replace("__SIZE__", "8" if suffix == "D" else "4"))
    (HERE / out).write_text(text, encoding="utf-8")
    print(f"  {out}: namespace {ns}, Real = {real}")


for suffix, ns, real, math, prec in VARIANTS:
    for template, stem in (("contact.template", "Contact"), ("brain.template", "Brain")):
        if (HERE / template).exists():
            emit(template, f"{stem}{suffix}.cs", suffix, ns, real, math, prec)
