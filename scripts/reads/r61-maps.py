"""Round 61's map check (D192): each candidate landscape's islands, read the way round 60's reader reads them, so that
one landscape seed can be chosen by its geometry before any life runs.

python -I r61-maps.py RUN [RUN ...] [--footprint-at 100] [--min-columns 10]

A RUN is a run directory or an arm folder (its latest run), founded by rounds/r61.ps1 -Step mapcheck for 100 s. Per
run: its landscape seed (config.json's seedOverrides), and its islands by scripts/reads/r60-read.py's rule: the matter
columns above the water's mean over the top three layers at --footprint-at seconds, none within the config's glass
margin, joined by their sides. An island counts when it has at least --min-columns columns (10 by default, 250 m² on
5 m columns); the line gives how many count, each one's columns and its nearest column centre to the glass, the share
of the island columns the largest holds, and the nearest distance between the centres of two counted islands. The
choice is the reader's: D192 asks for about five or six islands clear of the glass.

Reads the files only; writes nothing. Standard library only.
"""
import argparse
import importlib.util
import math
import os
import sys
import types

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("r60read", os.path.join(HERE, "r60-read.py"))
r60 = importlib.util.module_from_spec(_spec)
sys.modules["r60read"] = r60
_spec.loader.exec_module(r60)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("runs", nargs="+")
    ap.add_argument("--footprint-at", type=float, default=100.0)
    ap.add_argument("--min-columns", type=int, default=10)
    a = ap.parse_args()
    r60.r59.runrec.VERBOSE = False
    args = types.SimpleNamespace(end=a.footprint_at, year=6000.0, every=1500.0, footprint_at=a.footprint_at)
    print(f"== round 61's map check: islands at {a.footprint_at:g} s, counted from {a.min_columns} columns")
    for d in a.runs:
        w = r60.r59.World(d, args, r60.NoSwimmers())
        f = r60.Fed(w, args)
        over = r60.r59.find(w.config, "seedOverrides") or []
        landscape = next((e.split("=", 1)[1] for e in over if e.startswith("landscape=")), "world seed " + str(w.seed))
        # Each island's centre, from its columns.
        sx, sz = [0.0] * len(f.sizes), [0.0] * len(f.sizes)
        for ix in range(f.nx):
            for iz in range(f.nz):
                k = f.comp[ix][iz]
                if k >= 0:
                    sx[k] += (ix + 0.5) * f.cell
                    sz[k] += (iz + 0.5) * f.cell
        counted = sorted((k for k in range(len(f.sizes)) if f.sizes[k] >= a.min_columns), key=lambda k: -f.sizes[k])
        cx = {k: sx[k] / f.sizes[k] for k in counted}
        cz = {k: sz[k] / f.sizes[k] for k in counted}
        gap = min((math.hypot(cx[i] - cx[j], cz[i] - cz[j]) for i in counted for j in counted if i < j), default=float("nan"))
        total = sum(f.sizes)
        print(f"landscape {landscape} ({w.name}): {len(counted)} islands of {a.min_columns}+ columns, of {len(f.sizes)}; "
              f"{total} island columns ({total / f.n_water:.2f} of the water); largest {f.sizes[counted[0]] / total if counted else 0:.2f} "
              f"of them; nearest centres {gap:.0f} m apart; "
              + ", ".join(f"{f.sizes[k]} cols {f.glass[k]:.0f} m off the glass" for k in counted))


if __name__ == "__main__":
    main()
