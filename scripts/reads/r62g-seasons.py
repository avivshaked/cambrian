"""Round 62's grazer screen by season: corpses made and eaten, and the grazers' economy, per summer and winter half-year.

Usage: python -I r62g-seasons.py RUN [RUN ...] [--end 12000]

A RUN is an arm folder or a run directory, read through r61-grazer-economy's Economy (its definitions: a grazer is a body
born mouthed, a kill row has its victim in id and its attacker in by). Reads the files only; writes nothing.

The season is the world's own: under seasonShape TwoSeason, u = (t + dayLength/4) mod seasonLength, summer while
u < seasonLength/2 (LightModel.TwoSeasonSign; the ramp of seasonRampSeconds straddles each switch and is split at it).
A half-year is the span between two switches, clipped to 0 and --end.

Columns, per half-year:
  leaf deaths   leaves (r61-grazer-economy's leaf guild) that died in it, all causes; of them aged, starved, eaten.
                Every death leaves a corpse here (unbittenDeathsLeaveNoCorpse is false; checked against the config).
  kills         kill rows in it; by grazers; by grazers of leaves.
  eaten         stats corpsesEaten over it: corpses that mouths emptied to nothing (all mouths); fromK is stats
                corpsesFromKills over it, corpses a kill founded. e-fK is their difference. It is not an exact count of
                scavenged corpses: a kill's corpse can be emptied in a later half-year, or never, and a corpse fed on but
                not emptied is in neither. When e-fK far exceeds fromK, most emptied corpses were not made by kills.
  kept W/g      stats unitsEaten over it x joulesPerUnit x (1 - intakeWasteFraction), over grazer-seconds in it: watts
                per grazer if all mouths' intake went to grazers (an upper bound); share is that over --upkeep-w.
  g mean, g end grazer-seconds in it over its length; grazers alive at its end.
  born fnd/bred grazers born in it without a parent in the record / of a parent.
"""
import argparse
import importlib.util
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


def half_years(cfg, end):
    """[(start, stop, 'summer'|'winter')] between the world's switches, clipped to [0, end]."""
    shape = r59.find(cfg, "seasonShape")
    if shape != "TwoSeason":
        raise SystemExit(f"seasonShape is {shape!r}; this read is written for TwoSeason")
    year = float(r59.find(cfg, "seasonLengthSeconds"))
    day = float(r59.find(cfg, "dayLengthSeconds"))
    shift = 0.25 * day  # u = (t + shift) mod year; summer while u < year/2
    switches = []
    k = -1
    while True:
        t = k * year / 2 - shift
        if t >= end:
            break
        switches.append((t, "summer" if k % 2 == 0 else "winter"))
        k += 1
    out = []
    for n, (t, kind) in enumerate(switches):
        stop = switches[n + 1][0] if n + 1 < len(switches) else end
        a, b = max(0.0, t), min(end, stop)
        if b > a:
            out.append((a, b, kind))
    return out


def overlap(a0, a1, b0, b1):
    return max(0.0, min(a1, b1) - max(a0, b0))


def arm(d, a):
    e = r61.Economy(d, a)
    w = e.w
    cfg = w.config
    if r59.find(cfg, "unbittenDeathsLeaveNoCorpse") not in (False, None, "false"):
        raise SystemExit(f"{w.name}: unbittenDeathsLeaveNoCorpse is set; leaf deaths are not all corpses")
    keep = 1.0 - e.waste
    grazers = [i for i in w.born if e.mouthed(i) and w.born[i][0] <= e.end + 1e-9]
    gset = set(grazers)
    lives = {i: e.life(i) for i in grazers}
    rows = []
    for (h0, h1, kind) in half_years(cfg, e.end):
        dc = Counter()
        for i, dv in w.deaths.items():
            t = dv[0]
            if not (h0 < t <= h1 + 1e-9):
                continue
            if r61.guild(e.bits(i)) == "leaf":
                dc["leaf"] += 1
                dc[dv[1]] += 1
        kills = [k for k in e.kill_j if h0 < k[0] <= h1 + 1e-9]
        kg = [k for k in kills if k[2] in gset]
        kgl = [k for k in kg if r61.guild(e.bits(k[1])) == "leaf"]
        s0, s1 = e.stat_at(h0), e.stat_at(h1)

        def d_(key):
            if s1 is None or s1.get(key) is None:
                return None
            return s1[key] - ((s0 or {}).get(key) or 0)

        units, eaten, fromk = d_("unitsEaten"), d_("corpsesEaten"), d_("corpsesFromKills")
        gsec = sum(overlap(b0, b1, h0, h1) for (b0, b1, _) in lives.values())
        g_end = sum(1 for (b0, b1, died) in lives.values() if b0 <= h1 + 1e-9 and (b1 > h1 + 1e-9 or (not died and b1 >= h1 - 1e-9)))
        born = [i for i in grazers if h0 < w.born[i][0] <= h1 + 1e-9]
        fnd = sum(1 for i in born if (w.born[i][1] is None or w.born[i][1] < 0 or w.born[i][1] not in w.born))
        kept = d_("keptJ")  # kept intake under either rule (r61-grazer-economy.py, D219-D221)
        kept_w = kept / gsec if (kept is not None and gsec > 0) else None
        rows.append({
            "kind": kind, "h0": h0, "h1": h1, "leaf": dc["leaf"], "aged": dc["aged"], "starved": dc["starved"],
            "eaten_d": dc["eaten"], "kills": len(kills), "kg": len(kg), "kgl": len(kgl), "eaten": eaten,
            "fromk": fromk, "keptw": kept_w, "gmean": gsec / (h1 - h0), "gend": g_end, "fnd": fnd,
            "bred": len(born) - fnd})
    return w.name, rows


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("runs", nargs="+")
    ap.add_argument("--end", type=float, default=12000.0)
    ap.add_argument("--year", type=float, default=6000.0)
    ap.add_argument("--upkeep-w", type=float, default=0.908)
    a = ap.parse_args()
    a.every = a.year
    r59.runrec.VERBOSE = False
    print(f"== round 62 grazer screen by season: end {a.end:g} s, upkeep {a.upkeep_w:g} W")
    for d in a.runs:
        name, rows = arm(d, a)
        print(f"\n{name}")
        print(f"  {'half-year':<22} {'leafD':>6} {'aged':>5} {'starv':>5} {'eatn':>4} | {'kills':>5} {'by g':>4} {'g>leaf':>6} | "
              f"{'eaten':>5} {'fromK':>5} {'e-fK':>5} | {'kept W/g':>8} {'share':>5} | {'g mean':>6} {'g end':>5} {'fnd':>4} {'bred':>4}")
        tot = {}
        for r in rows:
            share = r["keptw"] / a.upkeep_w if r["keptw"] is not None else None
            efk = r["eaten"] - r["fromk"] if (r["eaten"] is not None and r["fromk"] is not None) else None
            span = f"{r['kind']} {r['h0']:.0f}-{r['h1']:.0f}"
            def f(x, spec):
                return "-" if x is None else format(x, spec)
            print(f"  {span:<22} {r['leaf']:>6} {r['aged']:>5} {r['starved']:>5} {r['eaten_d']:>4} | {r['kills']:>5} {r['kg']:>4} "
                  f"{r['kgl']:>6} | {f(r['eaten'], '>5')} {f(r['fromk'], '>5')} {f(efk, '>5')} | {f(r['keptw'], '>8.3f')} "
                  f"{f(share, '>5.2f')} | {r['gmean']:>6.1f} {r['gend']:>5} {r['fnd']:>4} {r['bred']:>4}")
            t = tot.setdefault(r["kind"], Counter())
            for k in ("leaf", "aged", "kills", "kg", "kgl", "eaten", "fromk", "fnd", "bred"):
                t[k] += r[k] or 0
            t["secs"] += r["h1"] - r["h0"]
        for kind, t in tot.items():
            print(f"  all {kind:<8} {t['secs']:>6.0f} s: leaf deaths {t['leaf']} (aged {t['aged']}), kills by grazers {t['kg']} "
                  f"(of leaves {t['kgl']}), corpses eaten {t['eaten']} (from kills {t['fromk']}), grazers born {t['fnd']} founded, "
                  f"{t['bred']} bred")


if __name__ == "__main__":
    main()
