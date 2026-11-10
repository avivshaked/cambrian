"""Frame-to-frame jump of every round 5 take: the mean absolute luma change between consecutive frames at 480 wide,
and the largest jump against the median of its neighbours. A cut between takes shows as one large jump; a pop in size
or a camera snap shows as a jump well above its neighbours inside a take.

Usage: python -I jumps.py <out.txt> <mp4> [<mp4> ...]
"""
import statistics
import subprocess
import sys
from pathlib import Path


def diffs(mp4: Path) -> list[float]:
    cmd = ["ffmpeg", "-v", "error", "-i", str(mp4), "-vf",
           "scale=480:-2,format=gray,tblend=all_mode=difference,signalstats,metadata=print:key=lavfi.signalstats.YAVG:file=-",
           "-f", "null", "-"]
    out = subprocess.run(cmd, capture_output=True, text=True, check=True).stdout
    vals = [float(l.split("=")[1]) for l in out.splitlines() if "YAVG=" in l]
    return vals


def main() -> None:
    out = Path(sys.argv[1])
    lines = []
    for p in sys.argv[2:]:
        mp4 = Path(p)
        d = diffs(mp4)
        if not d:
            lines.append(f"{mp4}: no frames")
            continue
        med = statistics.median(d)
        ranked = []
        for i, v in enumerate(d):
            nb = d[max(0, i - 3):i] + d[i + 1:i + 4]
            m = statistics.median(nb) if nb else med
            ranked.append((v / max(m, 0.05), v, i + 1))
        ranked.sort(reverse=True)
        top = ", ".join(f"frame {i} {v:.2f} ({r:.1f}x)" for r, v, i in ranked[:3])
        lines.append(f"{mp4.parent.name}/{mp4.stem[:40]}: frames {len(d) + 1}, median {med:.2f}, max {max(d):.2f}; top: {top}")
    out.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print("\n".join(lines))


if __name__ == "__main__":
    main()
