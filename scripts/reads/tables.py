"""Tables from scratch/r47-stomachs/r47-s*.json (written by stomachs.py)."""
import json, math, os
from collections import Counter, defaultdict

OUT = r"D:\Projects\experiments\evolution-simulator\scratch\r47-stomachs"
BE, BR = 0.44, 1.0
SRCS = ["child:photo", "child:stomach", "child:mixo", "child:other", "floor", "trickle", "pool"]
seeds = {s: json.load(open(os.path.join(OUT, "r47-s%d.json" % s))) for s in (1, 2, 3)}


def q(xs, fmt="%.2f"):
    xs = sorted(x for x in xs if x is not None)
    if not xs:
        return "-"
    n = len(xs)
    return ("n=%d " + fmt + " [" + fmt + "-" + fmt + "]") % (n, xs[n // 2], xs[n // 4], xs[(3 * n) // 4])


def stom(o):
    return [r for r in o["recs"] if r["abs"]]


def groups():
    for s, o in seeds.items():
        yield "s%d" % s, [o]
    yield "pooled", list(seeds.values())


print("## Q1 stomach births by source (abs 1 at birth; mixo = also pho 1)")
print("group | source | stomachs | of them mixo | per 1000 births | per 1000 s | births from that source | stomach share of that source")
for name, os_ in groups():
    tb = sum(o["births_total"] for o in os_)
    T = sum(o["t_end"] for o in os_)
    denom = dict(floor=sum(o["n_floor"] for o in os_), trickle=sum(o["n_trickle"] for o in os_),
                 pool=sum(o["n_pool"] for o in os_))
    denom["child:photo"] = sum(o["n_photo_parent_births"] for o in os_)
    denom["child:stomach"] = None
    denom["child:mixo"] = None
    # births from stomach / mixo parents
    sp = mp = 0
    for o in os_:
        pass
    allst = [r for o in os_ for r in stom(o)]
    for src in SRCS:
        rs = [r for r in allst if r["src"] == src]
        if not rs and src in ("child:other", "child:mixo"):
            continue
        dn = denom.get(src)
        print("%s | %s | %d | %d | %.2f | %.3f | %s | %s" % (
            name, src, len(rs), sum(1 for r in rs if r["pho"]), 1000 * len(rs) / tb, 1000 * len(rs) / T,
            dn if dn is not None else "-", ("%.3f" % (len(rs) / dn)) if dn else "-"))
    print("%s | ALL | %d | %d | %.2f | %.3f | %d births | " % (name, len(allst), sum(1 for r in allst if r["pho"]),
          1000 * len(allst) / tb, 1000 * len(allst) / T, tb))

print()
print("## Q2 where stomachs are born (first positions row <=10 s after birth; column snow from the dump at/before birth, J/m3 of water; densityHere = absorptive.jsonl first row)")
print("group | source | n placed | depth m med[q1-q3] | column snow | densityHere at first ledger row | share col>0.44 | share col>1 | share dh>0.44 | share dh>1 | reef axis dist m | share under a cap")
for name, os_ in groups():
    allst = [r for o in os_ for r in stom(o)]
    for src in SRCS + ["ALL"]:
        rs = [r for r in allst if (src == "ALL" or r["src"] == src)]
        pl = [r for r in rs if "x" in r]
        if not rs:
            continue
        dh = [r["dh0"] for r in rs if "dh0" in r]
        cs = [r["col_snow"] for r in pl]
        print("%s | %s | %d | %s | %s | %s | %s | %s | %s | %s | %s | %s" % (
            name, src, len(pl), q([r["depth"] for r in pl], "%.1f"), q(cs), q(dh),
            ("%.2f" % (sum(c > BE for c in cs) / len(cs))) if cs else "-",
            ("%.2f" % (sum(c > BR for c in cs) / len(cs))) if cs else "-",
            ("%.2f" % (sum(c > BE for c in dh) / len(dh))) if dh else "-",
            ("%.2f" % (sum(c > BR for c in dh) / len(dh))) if dh else "-",
            q([r["reef_dist"] for r in pl], "%.1f"),
            ("%.2f" % (sum(1 for r in pl if r["under_cap"]) / len(pl))) if pl else "-"))

print()
print("## Q2b first-row ledger: food W, upkeep W, net W (absorptive.jsonl first row)")
print("group | source | food W | upkeep W | net W | share net>0 | absVolume m3 | volume m3")
for name, os_ in groups():
    allst = [r for o in os_ for r in stom(o)]
    for src in SRCS + ["ALL"]:
        rs = [r for r in allst if (src == "ALL" or r["src"] == src) and "dh0" in r]
        if not rs:
            continue
        print("%s | %s | %s | %s | %s | %.2f | %s | %s" % (name, src, q([r["food0"] for r in rs], "%.3f"),
              q([r["upk0"] for r in rs], "%.3f"), q([r["net0"] for r in rs], "%.3f"),
              sum(r["net0"] > 0 for r in rs) / len(rs), q([r["absVol"] for r in rs], "%.3f"),
              q([r["vol"] for r in rs], "%.3f")))

print()
print("## Q2c over the whole life (absorptive.jsonl live rows, per-body medians)")
print("group | source | median densityHere | median food W | median upkeep W | median net W | share bodies with median net>0 | share ever under 0.44")
for name, os_ in groups():
    allst = [r for o in os_ for r in stom(o)]
    for src in SRCS + ["ALL"]:
        rs = [r for r in allst if (src == "ALL" or r["src"] == src) and "dh_med_life" in r]
        if not rs:
            continue
        print("%s | %s | %s | %s | %s | %s | %.2f | %.2f" % (name, src, q([r["dh_med_life"] for r in rs]),
              q([r["food_med_life"] for r in rs], "%.3f"), q([r["upk_med_life"] for r in rs], "%.3f"),
              q([r["net_med_life"] for r in rs], "%.3f"), sum(r["net_med_life"] > 0 for r in rs) / len(rs),
              sum(1 for r in rs if r.get("first_dh_below_be") is not None) / len(rs)))


def band(v):
    if v is None:
        return "no row"
    return "<0.44" if v < BE else ("0.44-1" if v <= BR else ">1")


print()
print("## Q3 lifetimes (s; died only; alive at end counted apart) and breeding, by source")
print("group | source | n | died | life med[q1-q3] | alive at end | with >=1 child | children total | stomach children")
for name, os_ in groups():
    allst = [r for o in os_ for r in stom(o)]
    for src in SRCS + ["ALL"]:
        rs = [r for r in allst if (src == "ALL" or r["src"] == src)]
        if not rs:
            continue
        print("%s | %s | %d | %d | %s | %d | %d | %d | %d" % (name, src, len(rs), sum(1 for r in rs if r["life"] is not None),
              q([r["life"] for r in rs], "%.0f"), sum(r["alive_end"] for r in rs), sum(r["nkids"] > 0 for r in rs),
              sum(r["nkids"] for r in rs), sum(r["kid_stomachs"] for r in rs)))

print()
print("## Q3b by snow band at birth (band from densityHere at the first ledger row; column band in brackets)")
print("group | band | n | life med[q1-q3] | with >=1 child | children | n by column band")
for name, os_ in groups():
    allst = [r for o in os_ for r in stom(o)]
    for b in ("<0.44", "0.44-1", ">1", "no row"):
        rs = [r for r in allst if band(r.get("dh0")) == b]
        cb = [r for r in allst if "col_snow" in r and band(r["col_snow"]) == b]
        print("%s | %s | %d | %s | %d | %d | %d" % (name, b, len(rs), q([r["life"] for r in rs], "%.0f"),
              sum(r["nkids"] > 0 for r in rs), sum(r["nkids"] for r in rs), len(cb)))

print()
print("## Q3c the children of stomachs")
print("group | children | stomach children | their densityHere at first row | their column snow | lifetimes | with >=1 child")
for name, os_ in groups():
    kids = []
    for o in os_:
        by = {r["id"]: r for r in o["recs"]}
        for r in stom(o):
            for c in r["kids"]:
                if c in by:
                    kids.append(by[c])
    print("%s | %d | %d | %s | %s | %s | %d" % (name, len(kids), sum(1 for k in kids if k["abs"]),
          q([k.get("dh0") for k in kids]), q([k.get("col_snow") for k in kids]),
          q([k["life"] for k in kids], "%.0f"), sum(k["nkids"] > 0 for k in kids)))

print()
print("## Q4 the plant sheet against the snow")
print("seed | t | photo bodies | photo depth m med[q1-q3] | tank mean snow J/m3 | column snow over water columns med[q1-q3] | share cols >0.44 | >1 | column snow under photo bodies (body-weighted) | densest-photo 10% cols mean | cols with no photo mean | floor-cell share of snow | living absorptive densityHere")
for s, o in seeds.items():
    for t in sorted(o["sheet"], key=int):
        h = o["sheet"][t]

        def f(d, fmt="%.2f"):
            if not d or d.get("n", 0) == 0:
                return "-"
            return ("n=%d " + fmt + " [" + fmt + "-" + fmt + "]") % (d["n"], d["med"], d["q1"], d["q3"])
        print("s%d | %s | %d | %s | %.3f | %s | %.2f | %.2f | %s | %.2f | %.2f | %s | %s" % (
            s, t, h["n_photo"], f(h["photo_depth"], "%.1f"), h["tank_mean"], f(h["col_q"]),
            h["share_cols_above_be"], h["share_cols_above_1"], f(h["under_photo_bodyweighted"]),
            h["top10pct_photo_cols_mean"], h["unoccupied_cols_mean"],
            ("%.3f" % h["floor_share"]) if h["floor_share"] is not None else "-", f(h["abs_densityHere"])))

print()
print("## tank mean snow J/m3 every 1000 s")
for s, o in seeds.items():
    ts = sorted(o["tank_mean_series"], key=int)
    print("s%d: " % s + ", ".join("%s:%.2f" % (t, o["tank_mean_series"][t]) for t in ts))

print()
print("## Q5 seed 1: pool-rooted stomachs alive at 5,000 s")
o = seeds[1]
by = {r["id"]: r for r in o["recs"]}
ids = [i for i in o["alive5000_stomach_ids"] if by.get(i, {}).get("rootsrc") == "pool"]
other = [i for i in o["alive5000_stomach_ids"] if by.get(i, {}).get("rootsrc") != "pool"]
print("stomachs alive at 5000: %d, pool-rooted %d, others %s" % (len(o["alive5000_stomach_ids"]), len(ids),
      [(i, by[i]["rootsrc"], by[i]["src"]) for i in other]))
print("id | born s | parent (src) | gen | life s | kids | dist parent m | birth col snow | parent col snow | dh at birth | dh median life | dh min life | first dh<0.44 at | own column snow min / at death | birth column snow at death | net median W | E max J")
for i in ids:
    r = by[i]
    tc = [v for t, v in r.get("track_col", []) if v is not None]
    bc = r.get("birthcol_series", [])
    print("%d | %.0f | %d (%s) | %s | %s | %d | %s | %s | %s | %s | %s | %s | %s | %s / %s | %s | %s | %s" % (
        i, r["t"], r["p"], by.get(r["p"], {}).get("src", "?"), r["g"],
        ("%.0f" % r["life"]) if r["life"] is not None else "alive", r["nkids"],
        ("%.1f" % r["dist_parent"]) if "dist_parent" in r else "-",
        ("%.2f" % r["col_snow"]) if "col_snow" in r else "-",
        ("%.2f" % r["parent_col_snow"]) if "parent_col_snow" in r else "-",
        ("%.2f" % r["dh0"]) if "dh0" in r else "-",
        ("%.2f" % r["dh_med_life"]) if "dh_med_life" in r else "-",
        ("%.2f" % r["dh_min_life"]) if "dh_min_life" in r else "-",
        ("%.0f" % r["first_dh_below_be"]) if r.get("first_dh_below_be") is not None else "never",
        ("%.2f" % min(tc)) if tc else "-", ("%.2f" % tc[-1]) if tc else "-",
        ("%.2f" % bc[-1][1]) if bc else "-",
        ("%.3f" % r["net_med_life"]) if "net_med_life" in r else "-",
        ("%.0f" % r["e_max"]) if "e_max" in r else "-"))
# the whole pool line of seed 1: every stomach rooted in a pool founder
line = [r for r in stom(o) if r["rootsrc"] == "pool"]
print("seed 1 pool-rooted stomachs ever: %d; with >=1 child %d; dh at birth %s; life %s" % (
    len(line), sum(r["nkids"] > 0 for r in line), q([r.get("dh0") for r in line]), q([r["life"] for r in line], "%.0f")))
print("  their parents' ids -> count of kids: %s" % Counter(r["p"] for r in line if r["p"] != -1).most_common(8))

print()
print("## Q6 mixotrophs (abs 1 and pho 1 at birth)")
print("group | n | by source | life med[q1-q3] | alive end | with >=1 child | children | stomach children")
for name, os_ in groups():
    rs = [r for o in os_ for r in stom(o) if r["pho"]]
    print("%s | %d | %s | %s | %d | %d | %d | %d" % (name, len(rs), dict(Counter(r["src"] for r in rs)),
          q([r["life"] for r in rs], "%.0f"), sum(r["alive_end"] for r in rs), sum(r["nkids"] > 0 for r in rs),
          sum(r["nkids"] for r in rs), sum(r["kid_stomachs"] for r in rs)))
print("seed | t | genomes | any absorptive node | reachable absorptive node | abs bit in positions | reachable but not expressed")
for s, o in seeds.items():
    for t, v in sorted(o["snap_q6"].items(), key=lambda kv: int(kv[0])):
        print("s%d | %s | %d | %d | %d | %d | %d" % (s, t, v["genomes"], v["abs_node_any"], v["abs_node_reachable"],
              v["abs_expressed_positions"], v["reachable_but_unexpressed"]))
