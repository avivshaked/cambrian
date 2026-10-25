"""Round 62 grazer screen: mouthed children per grazer by birth cohort of 3,000 s, children counted to --end.

Usage: python -I r62g-cohorts.py RUN [RUN ...] [--end 24000]

A RUN is read as one world with the run it resumed, through r61-grazer-economy (a grazer is a body born mouthed). The
last cohorts are censored by the end of the run. Reads the files only.
"""
import importlib.util, os, sys, argparse
from collections import Counter
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
spec = importlib.util.spec_from_file_location("r61econ", os.path.join(HERE, "r61-grazer-economy.py"))
r61 = importlib.util.module_from_spec(spec); sys.modules["r61econ"] = r61; spec.loader.exec_module(r61)
ap = argparse.ArgumentParser(); ap.add_argument("runs", nargs="+"); ap.add_argument("--end", type=float, default=24000)
ap.add_argument("--year", type=float, default=6000); a = ap.parse_args(); a.every = a.year
r61.r59.runrec.VERBOSE = False
for d in a.runs:
    e = r61.Economy(d, a); w = e.w
    kids = Counter(p for i, (t, p, b) in w.born.items() if t <= e.end and p is not None and p >= 0 and b & r61.r59.MOUTHED)
    g = [i for i in w.born if e.mouthed(i) and w.born[i][0] <= e.end]
    print(w.name)
    for c0 in range(0, int(e.end), 3000):
        coh = [i for i in g if c0 < w.born[i][0] <= c0 + 3000]
        bred = [i for i in coh if w.born[i][1] is not None and w.born[i][1] >= 0 and w.born[i][1] in w.born]
        if coh:
            print(f"  born {c0}-{c0+3000}: n {len(coh)} (bred {len(bred)}), mouthed kids/body {sum(kids[i] for i in coh)/len(coh):.2f}, bred ones {sum(kids[i] for i in bred)/len(bred) if bred else float('nan'):.2f}")
