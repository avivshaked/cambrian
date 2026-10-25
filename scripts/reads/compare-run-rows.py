"""Compare one run's stats and lineage rows with an earlier build's record of the same world, over the seconds both hold.

    python -I scripts/reads/compare-run-rows.py <recorded run dir> <new run dir> [--from S]

For a run on a build that adds columns the recording's build did not write: a resume against the recording, or a
founded run against a recorded seed (the contact-eating build's card identity, logbook/specs/r62ce-prereg/identity.md).
gpu-accept.py exits 1 on any field present in one run only. This leaves out every wall* field (they time the
machine) and the seven intake-origin columns of 44712ce3 (NEW, NEW_LINEAGE), and compares every other field the two
rows share. It prints the overlap, the first row and field that differ (if any), and every field present in one run
and not the other, which must be NEW alone for the claim to stand. For lineage.jsonl, it compares the rows after
--from, apart from NEW_LINEAGE, in order.
"""
import argparse
import json
import os

NEW = {"intakeOwnKillJ", "intakeOtherKillJ", "intakeCarrionJ", "mouthedIntakeOwnKillJ",
       "mouthedIntakeOtherKillJ", "mouthedIntakeCarrionJ", "intakeOriginFrom"}
NEW_LINEAGE = {"iok", "iak", "ic"}


def rows(path):
    with open(path, encoding="utf-8") as f:
        return [json.loads(l) for l in f if l.strip()]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("recorded")
    ap.add_argument("resumed")
    ap.add_argument("--from", dest="t_from", type=float, default=0)
    a = ap.parse_args()

    rec = {r["t"]: r for r in rows(os.path.join(a.recorded, "stats.jsonl"))}
    res = [r for r in rows(os.path.join(a.resumed, "stats.jsonl")) if r["t"] > a.t_from]
    both = [r for r in res if r["t"] in rec]
    print(f"stats: resumed rows after {a.t_from:g} s: {len(res)}, of which in the recording: {len(both)}"
          + (f", {both[0]['t']:g} to {both[-1]['t']:g} s" if both else ""))
    only_res = set()
    only_rec = set()
    first = None
    compared = 0
    for r in both:
        o = rec[r["t"]]
        only_res |= {k for k in r if k not in o}
        only_rec |= {k for k in o if k not in r}
        for k, v in r.items():
            if k.startswith("wall") or k in NEW or k not in o:
                continue
            compared += 1
            if o[k] != v and first is None:
                first = (r["t"], k, o[k], v)
    print(f"  fields compared per row: {len({k for k in both[0] if not k.startswith('wall') and k not in NEW and k in rec[both[0]['t']]}) if both else 0}"
          f", values compared: {compared}")
    print("  first difference: " + ("none" if first is None else f"t {first[0]:g} field {first[1]}: recorded {first[2]!r}, resumed {first[3]!r}"))
    print(f"  only in the resumed rows: {sorted(only_res)}")
    print(f"  only in the recorded rows: {sorted(only_rec)}")

    t_end = both[-1]["t"] if both else a.t_from
    def window(path):
        out = []
        for r in rows(path):
            t = r.get("t", r.get("born", r.get("died")))
            if isinstance(t, (int, float)) and a.t_from < t <= t_end:
                out.append(r)
        return out
    rl = window(os.path.join(a.recorded, "lineage.jsonl"))
    sl = window(os.path.join(a.resumed, "lineage.jsonl"))
    strip = lambda r: {k: v for k, v in r.items() if k not in NEW_LINEAGE}
    same = len(rl) == len(sl) and all(strip(x) == strip(y) for x, y in zip(rl, sl))
    print(f"lineage rows in ({a.t_from:g}, {t_end:g}] s: recorded {len(rl)}, resumed {len(sl)}, equal apart from "
          f"{sorted(NEW_LINEAGE)}: {same}")
    if not same:
        for i, (x, y) in enumerate(zip(rl, sl)):
            if strip(x) != strip(y):
                diff = sorted(k for k in set(strip(x)) | set(strip(y)) if strip(x).get(k) != strip(y).get(k))
                print(f"  first differing lineage row {i}: fields {diff[:10]}")
                break


if __name__ == "__main__":
    main()
