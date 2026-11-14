"""Merges Evosim.BodyExport's parts.jsonl rows into bodies-r67.json, the form theatre_scene.build reads
(S.BODIES.update(json.load(...))), with a team and the tank score added to each body.

    python -I merge_bodies.py --pool PARTS.jsonl --index POOL_INDEX.tsv [--taleplums PARTS.jsonl] [--out FILE]

Keys: founder (pool 2, the sea's body 13929), guz-N (a Guzzler, N the pool index), nib-N (a Nibbler),
tp-ID (a taleplum, ID the sea id). The pool index decides the team: pool 0-27 is forager-kit-shaped (the
Guzzlers, whose body could change), 28-72 is forager-kit-range4-g40 (the Nibblers, brain only).
Made with Evosim.BodyExport built at e79fa0fd (the sea run's own build; today's Core refuses its format-19
genomes), carrying the two options of src/Evosim.BodyExport/Program.cs:
  --label r67pool --pool <run dir> --pool-ids 0,1,2:13929,3,...72 --min-parts 1
  --label r67r1-6202 --run <run dir> --ids 166382,118251,131141,74039,42605,179291 --min-parts 1
"""
import argparse, json, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
FOUNDER_POOL = 2
ORIGIN_TEAM = {"forager-kit-shaped": "guzzler", "forager-kit-range4-g40": "nibbler"}


def read_index(path):
    rows = {}
    with open(path, encoding="utf-8") as f:
        head = f.readline().rstrip("\n").split("\t")
        for line in f:
            cells = line.rstrip("\n").split("\t")
            if len(cells) < len(head):
                continue
            r = dict(zip(head, cells))
            rows[int(r["pool"])] = r
    return rows


def key_for(team, pool):
    if pool == FOUNDER_POOL:
        return "founder"
    return ("guz-%d" if team == "guzzler" else "nib-%d") % pool


def merge(pool_rows, index, taleplum_rows=()):
    out = {}
    for row in pool_rows:
        pool = row["pool"]
        meta = index[pool]
        team = ORIGIN_TEAM[meta["origin"]]
        body = dict(row)
        body.update({"team": team, "ownEaten": float(meta["ownEaten"]), "poolId": meta["id"], "origin": meta["origin"]})
        out[key_for(team, pool)] = body
    for row in taleplum_rows:
        body = dict(row)
        body.update({"team": "taleplum"})
        out["tp-%d" % row["id"]] = body
    return out


def read_rows(path):
    with open(path, encoding="utf-8") as f:
        return [json.loads(line) for line in f if line.strip()]


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--pool", required=True)
    ap.add_argument("--index", required=True)
    ap.add_argument("--taleplums")
    ap.add_argument("--out", default=os.path.join(HERE, "bodies-r67.json"))
    a = ap.parse_args(argv)
    bodies = merge(read_rows(a.pool), read_index(a.index), read_rows(a.taleplums) if a.taleplums else ())
    with open(a.out, "w", encoding="utf-8") as f:
        json.dump(bodies, f, separators=(",", ":"), sort_keys=True)
    print("wrote %d bodies to %s" % (len(bodies), a.out))
    return 0


if __name__ == "__main__":
    sys.exit(main())
