#!/usr/bin/env python3
"""Writes the fourth spike's whole-step kernel, one source and two precisions, as gen3.py does.

    python spike4/gen4.py

step.template -> StepD.cs (namespace Gpu.Spike4.Dbl) and StepF.cs (Gpu.Spike4.Sgl)

The template carries the brain's precision helpers between //@HELPERS and //@END, exactly as the
third spike's brain template does: the double build calls System.Math through double as
Evosim.Core does, the single build calls ILGPU.Algorithms.XMath in float. Everything else is one
text. The ceilings are substituted here: __L__ links a body, __D__ degrees of freedom a body and
__CAND__ contact candidates a body.
"""
import pathlib

HERE = pathlib.Path(__file__).parent

LINKS = 16
DOF = 48
CAND = 256

VARIANTS = [
    ("D", "Gpu.Spike4.Dbl", "System.Double", "System.Math", "double"),
    ("F", "Gpu.Spike4.Sgl", "System.Single", "System.MathF", "single"),
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
    head, rest = text.split("//@HELPERS", 1)
    _, tail = rest.split("//@END", 1)
    text = head + HELPERS[suffix].strip("\n") + "\n" + tail
    text = (text.replace("__NS__", ns)
            .replace("__REALT__", real)
            .replace("__M__", math)
            .replace("__PREC__", prec)
            .replace("__L__", str(LINKS))
            .replace("__D__", str(DOF))
            .replace("__CAND__", str(CAND))
            .replace("__SIZE__", "8" if suffix == "D" else "4"))
    (HERE / out).write_text(text, encoding="utf-8")
    print(f"  {out}: namespace {ns}, Real = {real}")


for suffix, ns, real, math, prec in VARIANTS:
    emit("step.template", f"Step{suffix}.cs", suffix, ns, real, math, prec)
