"""How far bodies travel: each body's path and its distance from where it was first seen, by guild, from positions.

    python -I scripts/reads/travel.py RUN [RUN ...] [--from 12000] [--end 24000] [--every 100] [--min-seen 600]

The contact-eating screen's fourth reading (logbook/specs/r62ce-prereg/predictions.md, test 4), reported beside the
controls with no pass mark. A run is a run directory or the arm above one. Positions come from runrec (either record
format). The water's shape comes from scripts/positions-read.py's world_of, so a separation wraps where the box wraps.

Guilds, from each body's birth row in lineage.jsonl: a grazer carries attack and intake (atk and ink), a leaf carries
photosynthetic cells and is not a grazer, and every other body is "other". A body with no birth row is "other".

For each body seen at --min-seen seconds or more between --from and --end, sampled every --every seconds:
  path m/ks   the sum of the wrapped horizontal steps between its samples, per 1,000 s seen
  net m       the wrapped horizontal distance from its first sample to its last
  far         the share of bodies whose net distance passes a third of the box's shorter side
Each column prints the median over the guild's bodies, and the count. The sampling understates a path that turns
within --every seconds, so compare runs only at the same --every.

Exits 0 on runs it could read, 2 on a usage error or a run it could not find.
"""
import argparse
import importlib.util
import json
import math
import os
import statistics
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import runrec  # noqa: E402

_spec = importlib.util.spec_from_file_location("positions_read", os.path.join(os.path.dirname(HERE), "positions-read.py"))
positions_read = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(positions_read)


def guilds(run):
    out = {}
    path = os.path.join(run, "lineage.jsonl")
    with open(path, encoding="utf-8") as f:
        for line in f:
            if '"e":"b"' not in line:
                continue
            r = json.loads(line)
            if r.get("atk") and r.get("ink"):
                out[r["id"]] = "grazer"
            elif r.get("pho"):
                out[r["id"]] = "leaf"
            else:
                out[r["id"]] = "other"
    return out


def wrapped(d, extent):
    if extent and extent > 0:
        d = abs(d) % extent
        return min(d, extent - d)
    return abs(d)


def read(run, t_from, t_end, every, min_seen):
    with open(os.path.join(run, "config.json"), encoding="utf-8") as f:
        config = json.load(f)
    water = positions_read.world_of(config)
    wrap = isinstance(water, positions_read.Box)
    lx, lz = (water.length, water.width) if wrap else (None, None)
    kind = guilds(run)

    first, last, path, seen_from, seen_to = {}, {}, {}, {}, {}
    next_t = t_from
    for row in runrec.positions(run):
        t = row["t"]
        if t < next_t:
            continue
        if t > t_end:
            break
        next_t = t + every
        for b in row["b"]:
            i, x, z = b[0], b[1], b[3]
            if i not in first:
                first[i] = (x, z)
                seen_from[i] = t
                path[i] = 0.0
            else:
                px, pz = last[i]
                path[i] += math.hypot(wrapped(x - px, lx), wrapped(z - pz, lz))
            last[i] = (x, z)
            seen_to[i] = t

    shorter = min(water.length, water.width)
    table = {}
    for i in first:
        seen = seen_to[i] - seen_from[i]
        if seen < min_seen:
            continue
        g = kind.get(i, "other")
        fx, fz = first[i]
        lx_, lz_ = last[i]
        net = math.hypot(wrapped(lx_ - fx, lx), wrapped(lz_ - fz, lz))
        table.setdefault(g, []).append((path[i] / seen * 1000.0, net))
    return table, shorter


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("runs", nargs="+")
    ap.add_argument("--from", dest="t_from", type=float, default=12000)
    ap.add_argument("--end", type=float, default=24000)
    ap.add_argument("--every", type=float, default=100)
    ap.add_argument("--min-seen", type=float, default=600)
    a = ap.parse_args()

    print(f"== travel: bodies seen {a.min_seen:g} s or more in {a.t_from:g}-{a.end:g} s, sampled every {a.every:g} s")
    for given in a.runs:
        try:
            run = runrec.run_directory(given)
        except Exception as ex:  # noqa: BLE001
            print(f"{given}: {ex}", file=sys.stderr)
            return 2
        table, shorter = read(run, a.t_from, a.end, a.every, a.min_seen)
        print(f"\n{os.path.basename(os.path.dirname(run.rstrip(os.sep)))}  ({run})")
        print(f"   {'guild':<8} {'bodies':>7} {'path m/ks':>10} {'net m':>8} {'far':>6}")
        for g in ("grazer", "leaf", "other"):
            rows = table.get(g, [])
            if not rows:
                print(f"   {g:<8} {0:>7}")
                continue
            far = sum(1 for _, n in rows if n > shorter / 3) / len(rows)
            print(f"   {g:<8} {len(rows):>7} {statistics.median(p for p, _ in rows):>10.1f} "
                  f"{statistics.median(n for _, n in rows):>8.1f} {far:>6.2f}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
