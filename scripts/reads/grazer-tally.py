"""Bred and founder grazers' first minutes, and a world's kill victims by guild, from one run's lineage.jsonl.

A grazer is a body born mouthed (atk 1 and ink 1 at birth), as r61-grazer-economy and r62g-read define it. Bred is
a grazer with a parent in the record (p >= 0); a founder has none. For each, from its death row: age at death,
cause, the reserve it died with (res) and the joules its bites kept in its life (bk). bk 0 means it never kept a
meal. The kill victims are the kill rows whose attacker is a grazer, classed by the victim's birth flags.

Usage: python -I scripts/reads/grazer-tally.py RUN [--end 24000]
"""
import argparse
import json
import os
import statistics

ap = argparse.ArgumentParser()
ap.add_argument("run")
ap.add_argument("--end", type=float, default=24000.0)
a = ap.parse_args()

births = {}
deaths = {}
kills = []
for line in open(os.path.join(a.run, "lineage.jsonl")):
    if not line.strip():
        continue
    r = json.loads(line)
    t = r.get("t", 0)
    if t > a.end:
        break
    e = r.get("e")
    if e == "b":
        births[r["id"]] = r
    elif e == "d":
        deaths[r["id"]] = r
    elif e == "k":
        kills.append(r)


def grazer(b):
    return b.get("atk") == 1 and b.get("ink") == 1


def guild(b):
    if b is None:
        return "unknown"
    if grazer(b):
        return "grazer"
    if b.get("pho") == 1:
        return "leaf"
    if b.get("abs") == 1:
        return "absorber"
    if b.get("jnt") == 1:
        return "other jointed"
    return "other"


def q(xs, p):
    if not xs:
        return float("nan")
    xs = sorted(xs)
    return xs[min(len(xs) - 1, int(p * len(xs)))]


print(f"run {a.run}, to {a.end:.0f} s")
for name, keep in (("bred grazers", lambda b: b.get("p", -1) >= 0), ("founder grazers", lambda b: b.get("p", -1) < 0)):
    ids = [i for i, b in births.items() if grazer(b) and keep(b)]
    dead = [deaths[i] for i in ids if i in deaths]
    ages = [d["t"] - births[d["id"]]["t"] for d in dead]
    causes = {}
    for d in dead:
        causes[d.get("c")] = causes.get(d.get("c"), 0) + 1
    bk = [d.get("bk") or 0.0 for d in dead]
    never = sum(1 for v in bk if v <= 0)
    res = [d.get("res") or 0.0 for d in dead]
    print(f"\n{name}: born {len(ids)}, died {len(dead)}; causes {causes}")
    if dead:
        print(f"  age at death s: median {statistics.median(ages):.1f}, quartiles {q(ages, .25):.1f} / {q(ages, .75):.1f}")
        print(f"  never kept a meal (bk 0): {never} of {len(dead)}")
        # With the kill gut on (D232), a swallowed kill reaches the reserve through the gut and never touches bk, so a
        # body that only ate what it killed reads bk 0. A kill row naming it as the attacker counts as a meal here.
        killers = {k.get("by") for k in kills}
        never_any = sum(1 for d in dead if (d.get("bk") or 0.0) <= 0 and d["id"] not in killers)
        print(f"  never ate at all (bk 0 and no kill, the kill gut's meals counted): {never_any} of {len(dead)}")
        eaters = [v for v in bk if v > 0]
        if eaters:
            print(f"  bk of those that ate, J: median {statistics.median(eaters):.2f}, max {max(eaters):.1f}")
        print(f"  reserve at death J: median {statistics.median(res):.3f}")
        life = sum(ages)
        print(f"  kept intake over the dead's lives: {sum(bk):.0f} J over {life:.0f} s = {sum(bk) / life:.3f} W"
              f" (the living are left out, so long lives are under-counted)")
        mid = sorted(zip(ages, bk))
        half = mid[len(mid) // 2]
        print(f"  the median-lived one: {half[0]:.0f} s, kept {half[1]:.1f} J")
        young = [d for d in dead if d["t"] - births[d["id"]]["t"] <= 60]
        yb = [d.get("bk") or 0.0 for d in young]
        print(f"  dead within 60 s: {len(young)}, of them never ate {sum(1 for v in yb if v <= 0)}")

victims = {}
for k in kills:
    b = births.get(k.get("by"))
    if b is None or not grazer(b) or k.get("root") != 1:
        continue
    g = guild(births.get(k.get("id")))
    victims[g] = victims.get(g, 0) + 1
print(f"\nwhole-body kills by grazers, by victim guild: {victims}")
