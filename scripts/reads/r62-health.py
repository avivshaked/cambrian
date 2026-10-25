"""Round 62's grazer health (D215): the two measures the screen's reads lack, per world.

Usage: python -I r62-health.py RUN [RUN ...] [--end 24000] [--at 12000,24000] [--from 12000] [--bin 3000]

A RUN is an arm folder (its latest dated run is read) or a run directory, read as one world with any run it resumed,
through r61-grazer-economy's Economy, whose definitions this keeps: a grazer is a body born mouthed (atk 1 and ink 1
at birth); a kill row ("e":"k") has its victim in id, its attacker in by, and the joules of the corpse it founds in
tj + rj. Reads the files only; writes nothing. Standard library only.

1 Born share (D215's first test). At each second of --at, the living grazers (born at or before it, not dead by
  it), how many were founded (no parent in the record: the trickle's and the pool's founders) and how many were born
  in the world (a parent in the record, mouthed or not), and the born share.
2 Own-kill share (D215's third test), per bin of --bin seconds from --from to --end. kill J is the corpse joules
  founded by grazers' kills in the bin, kept at 1 - intakeWasteFraction; intake J is stats unitsEaten over the bin x
  joulesPerUnit x (1 - intakeWasteFraction), every mouth's kept intake. The share is kill J over intake J. The record
  does not say which corpse a mouth ate from, so this is a proxy and not the grazers' own share: the numerator is
  what their kills could at most have fed them (a kill's corpse is shared, can be scavenged by others, and spoils
  under a half-life), and the denominator is every mouth's intake, which a mouth census put at 0.98 grazers' in the
  spoilage screen's 3 m worlds at half-lives 0 and 500 s (logbook/specs/r62-spoil-screen/). D215 reads its trend,
  not its level, and the proxy's biases hold within a world from bin to bin, an inference. kills/g-s is grazer kills
  over grazer-seconds in the bin. The trend line compares the last full bin with the first.
"""
import argparse
import importlib.util
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
spec = importlib.util.spec_from_file_location("r61econ", os.path.join(HERE, "r61-grazer-economy.py"))
r61 = importlib.util.module_from_spec(spec)
sys.modules["r61econ"] = r61
spec.loader.exec_module(r61)


def seconds_list(text):
    return [float(x) for x in text.split(",") if x.strip()]


def born_share(e, T):
    """(living grazers, founded, born in the world) at second T."""
    w = e.w
    alive = [i for i, (bt, p, b) in w.born.items()
             if b & r61.r59.MOUTHED and bt <= T + 1e-9 and not (i in w.deaths and w.deaths[i][0] <= T + 1e-9)]
    founded = sum(1 for i in alive if w.born[i][1] is None or w.born[i][1] < 0 or w.born[i][1] not in w.born)
    return len(alive), founded, len(alive) - founded


def grazer_seconds(e, t0, t1):
    w = e.w
    s = 0.0
    for i, (bt, p, b) in w.born.items():
        if not b & r61.r59.MOUTHED or bt >= t1:
            continue
        d = w.deaths.get(i)
        dt = d[0] if d else float("inf")
        lo, hi = max(bt, t0), min(dt, t1, e.end)
        if hi > lo:
            s += hi - lo
    return s


def own_kill_bins(e, t_from, t_end, width):
    keep = 1.0 - e.waste
    rows = []
    t0 = t_from
    while t0 + width <= t_end + 1e-9:
        t1 = t0 + width
        kill_j = sum(j for (t, victim, by, j) in e.kill_j
                     if t0 < t <= t1 and by is not None and e.mouthed(by)) * keep
        kills = sum(1 for (t, victim, by, j) in e.kill_j if t0 < t <= t1 and by is not None and e.mouthed(by))
        a, b = e.stat_at(t0), e.stat_at(t1)
        if a is None or b is None or a.get("unitsEaten") is None or b.get("unitsEaten") is None:
            intake_j = None
        else:
            intake_j = (b["unitsEaten"] - a["unitsEaten"]) * e.jpu * keep
        gs = grazer_seconds(e, t0, t1)
        share = kill_j / intake_j if intake_j else None
        rows.append((t0, t1, kills, kill_j, intake_j, share, kills / gs if gs > 0 else None, gs))
        t0 = t1
    return rows


def living_bins(e, t_from, t_end, width):
    """D222's test 3 under contact eating (D219-D221): per bin, the kept intake bitten off the living over all kept
    intake, the rest being bites on remains. Measured, not a proxy: the stats' biteKept columns split the two."""
    rows = []
    t0 = t_from
    while t0 + width <= t_end + 1e-9:
        t1 = t0 + width
        a, b = e.stat_at(t0), e.stat_at(t1)
        if a is None or b is None or b.get("keptJ") is None or b.get("keptLivingJ") is None:
            living = kept = None
        else:
            living = b["keptLivingJ"] - (a.get("keptLivingJ") or 0.0)
            kept = b["keptJ"] - (a.get("keptJ") or 0.0)
        share = living / kept if kept else None
        rows.append((t0, t1, living, kept, share, grazer_seconds(e, t0, t1)))
        t0 = t1
    return rows


def print_living(e, a):
    rows = living_bins(e, a.t_from, e.end, a.bin)
    print(f"  {'bin':>13} {'living J':>11} {'kept J':>11} {'share':>7} {'graz-s':>9}")
    for (t0, t1, lj, kj, sh, gs) in rows:
        nan = float('nan')
        print(f"  {t0:>6.0f}-{t1:<6.0f} {(lj if lj is not None else nan):>11.0f} {(kj if kj is not None else nan):>11.0f} "
              f"{(sh if sh is not None else nan):>7.3f} {gs:>9.0f}")
    full = [r for r in rows if r[4] is not None]
    if full:
        last = full[-1]
        verdict = "holds" if last[4] >= 0.5 else "fails"
        print(f"  test 3 (D222): the living share in {last[0]:.0f}-{last[1]:.0f} s is {last[4]:.3f}, {verdict} at 0.5")
    if len(full) >= 2:
        first, last = full[0][4], full[-1][4]
        word = "rises" if last > first else ("falls" if last < first else "is flat")
        print(f"  trend, reported and not required: the share {word}, {first:.3f} in {full[0][0]:.0f}-{full[0][1]:.0f} s "
              f"to {last:.3f} in {full[-1][0]:.0f}-{full[-1][1]:.0f} s")
    elif not full:
        print("  test 3: no bin with kept intake")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("runs", nargs="+")
    ap.add_argument("--end", type=float, default=24000)
    ap.add_argument("--year", type=float, default=6000)
    ap.add_argument("--at", default="12000,24000")
    ap.add_argument("--from", dest="t_from", type=float, default=12000)
    ap.add_argument("--bin", type=float, default=3000)
    a = ap.parse_args()
    a.every = a.year
    r61.r59.runrec.VERBOSE = False

    print(f"== round 62 grazer health (D215): born share at {a.at}; own-kill share per {a.bin:g} s from {a.t_from:g} to {a.end:g} s")
    print("   The own-kill share is a PROXY, an upper bound on the grazers' intake from their own kills: the corpse joules")
    print("   grazers' kills founded (kept at 1 - waste) over every mouth's kept intake. The record does not say which corpse")
    print("   a mouth ate. Accepted for test 3's trend in round 62 (the coordinator, 2026-10-23); its level is not a measurement.")
    for d in a.runs:
        e = r61.Economy(d, a)
        print(f"\n{e.w.name}  (read to {e.end:g} s; joulesPerUnit {e.jpu:g}, intakeWasteFraction {e.waste:g})")
        print(f"  {'second':>7} {'living':>7} {'founded':>8} {'born':>6} {'born share':>10}")
        for T in seconds_list(a.at):
            if T > e.end + 1e-9:
                print(f"  {T:>7.0f}  past the record's end")
                continue
            n, f, b = born_share(e, T)
            print(f"  {T:>7.0f} {n:>7} {f:>8} {b:>6} {(b / n if n else float('nan')):>10.3f}")
        if e.contact:
            print("  contact eating (D219-D221): part 2 is the kept intake bitten off the living over all kept intake")
            print_living(e, a)
            continue
        rows = own_kill_bins(e, a.t_from, e.end, a.bin)
        print(f"  {'bin':>13} {'kills':>6} {'kill J':>10} {'intake J':>11} {'share':>7} {'kills/g-s':>10} {'graz-s':>9}")
        for (t0, t1, k, kj, ij, sh, kg, gs) in rows:
            print(f"  {t0:>6.0f}-{t1:<6.0f} {k:>6} {kj:>10.0f} {(ij if ij is not None else float('nan')):>11.0f} "
                  f"{(sh if sh is not None else float('nan')):>7.3f} {(kg if kg is not None else float('nan')):>10.2e} {gs:>9.0f}")
        full = [r for r in rows if r[5] is not None]
        if len(full) >= 2:
            first, last = full[0][5], full[-1][5]
            word = "rises" if last > first else ("falls" if last < first else "is flat")
            print(f"  trend: the share {word}, {first:.3f} in {full[0][0]:.0f}-{full[0][1]:.0f} s to {last:.3f} in {full[-1][0]:.0f}-{full[-1][1]:.0f} s")
        else:
            print("  trend: fewer than two bins with intake")


if __name__ == "__main__":
    main()
