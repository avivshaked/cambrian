"""Round 62's grazer screen in short windows, with the season switches marked: is the start of summer the hard time?

Usage: python -I r62g-windows.py RUN [RUN ...] [--end 12000] [--window 500]

A RUN is an arm folder or a run directory, read through r61-grazer-economy's Economy (a grazer is a body born mouthed;
a kill row has its victim in id, its attacker in by, and the corpse's joules in tj + rj). The season switches are the
world's own, as r62g-seasons.py finds them. Reads the files only; writes nothing.

Columns, per window (t0, t1]:
  season      the season at the window's middle; a * marks a window holding a switch.
  g mean      grazer-seconds over the window's length; g end, grazers alive at t1.
  g starved   grazers that died starved in the window.
  kept W/g    stats unitsEaten over the window x joulesPerUnit x (1 - intakeWasteFraction), over grazer-seconds: watts per
              grazer if all mouths' intake went to grazers (an upper bound); share is that over --upkeep-w.
  corpse kJ   stats corpseJoules, the mean over the window's rows: the joules standing in corpses (settled ones
              included); settled kJ, corpseJoulesSettled likewise.
  emptied     stats corpsesEaten over the window: corpses mouths ate to nothing.
  g>leaf      kills by grazers of living leaves (r61-grazer-economy's leaf guild).
  kill J      the corpse joules founded by every grazer kill in the window, x (1 - intakeWasteFraction): an upper bound on
              what kills fed. other J is the window's kept intake less kill J: a lower bound on what came from corpses
              no grazer killed in the window (scavenging, or kills from an earlier window).
"""
import argparse
import importlib.util
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)


def _load(name, file):
    spec = importlib.util.spec_from_file_location(name, os.path.join(HERE, file))
    mod = importlib.util.module_from_spec(spec)
    sys.modules[name] = mod
    spec.loader.exec_module(mod)
    return mod


r61 = _load("r61econ", "r61-grazer-economy.py")
seasons = _load("r62gseasons", "r62g-seasons.py")
r59 = r61.r59


def arm(d, a):
    e = r61.Economy(d, a)
    w = e.w
    cfg = w.config
    keep = 1.0 - e.waste
    halves = seasons.half_years(cfg, e.end)
    switches = [h0 for (h0, _, _) in halves if h0 > 0]

    def season_at(t):
        for (h0, h1, kind) in halves:
            if h0 <= t < h1:
                return kind
        return halves[-1][2]

    grazers = [i for i in w.born if e.mouthed(i) and w.born[i][0] <= e.end + 1e-9]
    gset = set(grazers)
    lives = {i: e.life(i) for i in grazers}
    stats = list(w.seg_rows("stats.jsonl"))
    rows = []
    t0 = 0.0
    while t0 < e.end - 1e-9:
        t1 = min(e.end, t0 + a.window)
        gsec = sum(seasons.overlap(b0, b1, t0, t1) for (b0, b1, _) in lives.values())
        g_end = sum(1 for i, (b0, b1, died) in lives.items() if b0 <= t1 + 1e-9 and (b1 > t1 + 1e-9 or not died))
        starved = sum(1 for i in grazers if i in w.deaths and t0 < w.deaths[i][0] <= t1 + 1e-9 and w.deaths[i][1] == "starved")
        s0, s1 = e.stat_at(t0), e.stat_at(t1)

        def d_(key):
            if s1 is None or s1.get(key) is None:
                return None
            return s1[key] - ((s0 or {}).get(key) or 0)

        units, emptied = d_("unitsEaten"), d_("corpsesEaten")
        inside = [r for r in stats if t0 < r["t"] <= t1 + 1e-9]
        cj = sum(r.get("corpseJoules") or 0 for r in inside) / len(inside) if inside else None
        cjs = sum(r.get("corpseJoulesSettled") or 0 for r in inside) / len(inside) if inside else None
        kg = [k for k in e.kill_j if t0 < k[0] <= t1 + 1e-9 and k[2] in gset]
        kgl = sum(1 for k in kg if r61.guild(e.bits(k[1])) == "leaf")
        kill_j = sum(k[3] for k in kg) * keep
        kept = d_("keptJ")  # kept intake under either rule (r61-grazer-economy.py, D219-D221)
        rows.append({
            "t0": t0, "t1": t1, "season": season_at(0.5 * (t0 + t1)),
            "switch": any(t0 < s <= t1 for s in switches), "gmean": gsec / (t1 - t0), "gend": g_end,
            "starved": starved, "keptw": kept / gsec if (kept is not None and gsec > 0) else None,
            "cj": cj, "cjs": cjs, "emptied": emptied, "kgl": kgl, "killj": kill_j,
            "otherj": kept - kill_j if kept is not None else None})
        t0 = t1
    return w.name, switches, rows


def f(x, spec):
    return "-" if x is None else format(x, spec)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("runs", nargs="+")
    ap.add_argument("--end", type=float, default=12000.0)
    ap.add_argument("--year", type=float, default=6000.0)
    ap.add_argument("--window", type=float, default=500.0)
    ap.add_argument("--upkeep-w", type=float, default=0.908)
    a = ap.parse_args()
    a.every = a.year
    r59.runrec.VERBOSE = False
    print(f"== round 62 grazer screen in {a.window:g} s windows: end {a.end:g} s, upkeep {a.upkeep_w:g} W")
    for d in a.runs:
        name, switches, rows = arm(d, a)
        print(f"\n{name}: season switches at {', '.join(f'{s:g}' for s in switches)} s")
        print(f"  {'window':<12} {'season':<8} {'g mean':>6} {'g end':>5} {'starv':>5} {'kept W/g':>8} {'share':>5} "
              f"{'corpse kJ':>9} {'settled':>7} {'empt':>4} {'g>leaf':>6} {'kill J':>7} {'other J':>8}")
        for r in rows:
            share = r["keptw"] / a.upkeep_w if r["keptw"] is not None else None
            span = f"{r['t0']:.0f}-{r['t1']:.0f}"
            mark = r["season"] + ("*" if r["switch"] else "")
            print(f"  {span:<12} {mark:<8} {r['gmean']:>6.1f} {r['gend']:>5} {r['starved']:>5} {f(r['keptw'], '>8.3f')} "
                  f"{f(share, '>5.2f')} {f(r['cj'] / 1000 if r['cj'] is not None else None, '>9.1f')} "
                  f"{f(r['cjs'] / 1000 if r['cjs'] is not None else None, '>7.1f')} {f(r['emptied'], '>4')} {r['kgl']:>6} "
                  f"{r['killj']:>7.0f} {f(r['otherj'], '>8.0f')}")


if __name__ == "__main__":
    main()
