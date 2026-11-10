"""Measure each still of a stills folder as film.py measures a clip, at 960 wide: mean luma, tone (YHIGH - YLOW),
Sobel edges, the bottom fifth's mean luma, the top fifth's, and green over blue under the top fifth.
Usage: python -I measure.py <stills dir> [reference png ...]"""
import re
import subprocess
import sys
from pathlib import Path


def stats(path, pre=""):
    r = subprocess.run(["ffmpeg", "-v", "error", "-i", str(path), "-vf",
                        f"scale=960:-2,{pre}signalstats,metadata=print:file=-", "-f", "null", "-"],
                       capture_output=True, text=True)
    t = r.stdout
    g = lambda k: float(re.findall(k + r"=([0-9.]+)", t)[0])
    return g("YAVG"), g("YHIGH") - g("YLOW")


def edges(path):
    r = subprocess.run(["ffmpeg", "-v", "error", "-i", str(path), "-vf",
                        "scale=960:-2,format=gray,sobel,signalstats,metadata=print:key=lavfi.signalstats.YAVG:file=-",
                        "-f", "null", "-"], capture_output=True, text=True)
    return float(re.findall(r"YAVG=([0-9.]+)", r.stdout)[0])


def gb(path):
    r = subprocess.run(["ffmpeg", "-v", "error", "-i", str(path), "-vf",
                        "scale=960:-2,crop=iw:ih*4/5:0:ih/5,scale=160:72:flags=area,format=rgb24", "-f", "rawvideo", "-"],
                       capture_output=True)
    px = r.stdout
    n = len(px) // 3
    rr, gg, bb = (sum(px[c::3]) / n for c in range(3))
    return gg / bb if bb else 0.0


def row(p):
    y, tone = stats(p)
    bot, _ = stats(p, "crop=iw:ih/5:0:ih*4/5,")
    top, _ = stats(p, "crop=iw:ih/5:0:0,")
    return f"{p.name:22s} luma {y:6.1f} tone {tone:5.0f} edges {edges(p):6.2f} top5 {top:6.1f} bottom5 {bot:6.1f} G/B {gb(p):.2f}"


if __name__ == "__main__":
    d = Path(sys.argv[1])
    for p in sorted(d.glob("*.png")):
        print(row(p))
    for ref in sys.argv[2:]:
        print("ref " + row(Path(ref)))
