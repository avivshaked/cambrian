"""Round 62's grazer screen: one row per arm of the grazer economy.

Usage: python -I r62g-read.py RUN [RUN ...] [--end 12000] [--year 6000] [--upkeep-w 0.908]

A RUN is an arm folder (its latest dated run is read) or a run directory, as in r61-grazer-economy.py, whose
definitions this reads by import (the same World class, bit tables, kill rows and lineage pass): a grazer is a body
born mouthed (atk 1 and ink 1 at birth), a kill row ("e":"k") has its victim in id and its attacker in by. A run
that resumed another is read as one world with it. Reads the files only; writes nothing. Standard library only.

Every config value (joulesPerUnit, intakeWasteFraction) is read from the run's own config.json.
"""
import argparse
import importlib.util
import math
import os
import sys
from collections import Counter

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
_spec = importlib.util.spec_from_file_location("r61econ", os.path.join(HERE, "r61-grazer-economy.py"))
r61 = importlib.util.module_from_spec(_spec)
sys.modules["r61econ"] = r61
_spec.loader.exec_module(r61)
r59 = r61.r59

DEFAULT_UPKEEP_W = 0.908  # the value in r61-grazer-economy's usage line (its argparse default is none)


def fmt(x, f="{:.2f}"):
    return "-" if x is None or (isinstance(x, float) and math.isnan(x)) else f.format(x)


def arm_row(d, a):
    """The numbers of one arm, from r61's Economy."""
    e = r61.Economy(d, a)
    w = e.w
    end = e.end
    keep = 1.0 - e.waste
    grazers = [i for i in w.born if e.mouthed(i) and w.born[i][0] <= end + 1e-9]
    gset = set(grazers)

    def is_founder(i):
        p = w.born[i][1]
        return p is None or p < 0 or p not in w.born

    founders = [i for i in grazers if is_founder(i)]
    bred = [i for i in grazers if not is_founder(i)]
    bred_unm = sum(1 for i in bred if not e.mouthed(w.born[i][1]))

    # depth of a line of mouthed bodies: a founder 0, else the parent's depth + 1 when the parent is mouthed, else 0
    depth = {}
    for i in sorted(grazers, key=lambda k: w.born[k][0]):
        p = w.born[i][1]
        depth[i] = depth[p] + 1 if (not is_founder(i) and p in depth) else 0

    gen_max = max((e.gen.get(i, 0) for i in grazers), default=None)
    chain_max = max(depth.values(), default=None)

    # replacement: mouthed children per mouthed body, for bodies born in the first half of the run
    mkids = Counter()
    for i, (t, p, b) in w.born.items():
        if t > end + 1e-9 or p is None or p < 0:
            continue
        if b & r59.MOUTHED:
            mkids[p] += 1
    half = [i for i in grazers if w.born[i][0] <= end / 2 + 1e-9]
    repl = sum(mkids[i] for i in half) / len(half) if half else None

    gsec = sum(e.life(i)[1] - e.life(i)[0] for i in grazers)
    kills = sum(1 for t, v, by, j in e.kill_j if by is not None and by in gset)
    s = e.stat_at(end)
    units = (s or {}).get("unitsEaten")
    intake = (s or {}).get("keptJ")  # joules kept, all mouths, to --end, under either rule (D219-D221)
    return {
        "arm": w.name, "end": end, "short": w.short, "n_fnd": len(founders), "n_bred": len(bred), "bred_unm": bred_unm,
        "gen": gen_max, "chain": chain_max, "repl": repl, "n_half": len(half), "gsec": gsec, "kills": kills,
        "kps": kills / gsec if gsec > 0 else None,
        "jpk": intake / kills if (intake is not None and kills) else None,
        "ips": intake / gsec if (intake is not None and gsec > 0) else None,
        "jpu": e.jpu, "waste": e.waste,
        "units": units,
    }


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("runs", nargs="+")
    ap.add_argument("--end", type=float, default=12000.0)
    ap.add_argument("--year", type=float, default=6000.0)
    ap.add_argument("--upkeep-w", type=float, default=DEFAULT_UPKEEP_W,
                    help="a grazer's standing cost, watts (default: r61-grazer-economy's usage value)")
    a = ap.parse_args()
    a.every = a.year
    r59.runrec.VERBOSE = False
    rows = [arm_row(d, a) for d in a.runs]

    print(f"== round 62 grazer screen: end {a.end:g} s, upkeep {a.upkeep_w:g} W")
    head = (f"{'arm':<14} {'fnd':>5} {'bred':>5} {'(unm)':>5} {'gen':>4} {'chain':>5} {'repl':>6} {'n 1st half':>10} "
            f"{'graz-s':>10} {'kills':>6} {'kills/g-s':>10} {'J/kill':>8} {'J/g-s':>8} {'intake/upkeep':>13}")
    print(head)
    for r in rows:
        share = r["ips"] / a.upkeep_w if (r["ips"] is not None and a.upkeep_w) else None
        print(f"{r['arm']:<14} {r['n_fnd']:>5} {r['n_bred']:>5} {r['bred_unm']:>5} {fmt(r['gen'], '{}'):>4} "
              f"{fmt(r['chain'], '{}'):>5} {fmt(r['repl'], '{:.3f}'):>6} {r['n_half']:>10} {r['gsec']:>10.0f} {r['kills']:>6} "
              f"{fmt(r['kps'], '{:.2e}'):>10} {fmt(r['jpk'], '{:.1f}'):>8} {fmt(r['ips'], '{:.4f}'):>8} "
              f"{fmt(share, '{:.3f}'):>13}")
    for r in rows:
        if r["short"]:
            print(f"   note: {r['arm']} reaches less than --end; read to {r['end']:g} s")
    r0 = rows[0]
    print(f"""
Columns (a grazer is a body born mouthed, atk 1 and ink 1, as r61-grazer-economy defines it; all counts to the end):
  fnd, bred   grazers born without a parent in the record (trickle and pool founders born mouthed) / grazers born of a parent;
              (unm) is how many of the bred had a parent born without a mouth (they are counted in bred).
  gen         deepest generation of any grazer, the birth row's g (counted from the root founder of its line, whatever its guild).
  chain       deepest run of mouthed parent to mouthed child among grazers (a founder or a grazer of an unmouthed parent is 0).
  repl        mean mouthed children per grazer, over grazers born at or before end/2 (their children counted to the end); n is the count.
  graz-s      grazer-seconds lived: the sum over grazers of (death or end) less birth.
  kills       kill rows whose attacker (by) is a grazer; kills/g-s is kills over grazer-seconds.
  J/kill      kept mouth intake over kills: stats unitsEaten at the end (cumulative from 0) x joulesPerUnit x (1 - intakeWasteFraction),
              over the kills. This charges every mouth's intake in the world (one with no kills as well) to the grazers' kills.
  J/g-s       the same kept intake over grazer-seconds: watts per grazer if all the intake went to grazers.
  intake/upkeep   J/g-s over --upkeep-w ({a.upkeep_w:g} W).
Config values read from each run's config.json: joulesPerUnit {r0['jpu']:g} and intakeWasteFraction {r0['waste']:g} (first arm; the
others are read the same way, each from its own file).""")


if __name__ == "__main__":
    main()
