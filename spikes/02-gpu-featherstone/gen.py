#!/usr/bin/env python3
"""Writes KernelD.cs / KernelF.cs and RunnerD.cs / RunnerF.cs from the two templates.

One source, two precisions. The double and the single kernel must be the same arithmetic
written once, or a deviation between them is a difference in transcription rather than a
difference in precision — which is the whole question the spike asks.

    python gen.py [MAXLINKS]
"""
import pathlib
import sys

HERE = pathlib.Path(__file__).parent
maxl = int(sys.argv[1]) if len(sys.argv) > 1 else 8

VARIANTS = [
    ("D", "Gpu.Dbl", "System.Double", "System.Math", 8),
    ("F", "Gpu.Sgl", "System.Single", "System.MathF", 4),
]


def emit(template, out, ns, real, math, size):
    text = (pathlib.Path(HERE / template).read_text(encoding="utf-8")
            .replace("__NS__", ns)
            .replace("__REALT__", real)
            .replace("__M__", math)
            .replace("__SIZE__", str(size))
            .replace("__L__", str(maxl))
            .replace("__D__", str(3 * maxl))
            .replace("__P__", str(32 * maxl)))
    (HERE / out).write_text(text, encoding="utf-8")
    print(f"  {out}: namespace {ns}, Real = {real}, MaxLinks = {maxl}")


for suffix, ns, real, math, size in VARIANTS:
    emit("kernel.template", f"Kernel{suffix}.cs", ns, real, math, size)
    emit("runner.template", f"Runner{suffix}.cs", ns, real, math, size)
