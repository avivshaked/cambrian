"""D126's check, the comparison half (check.ps1 runs the windows).

For each of round 50's seeds and each resume second, the window on the card (g) and on the CPU's
transport (c) against round 50's own record: the report rows through scripts/compare-det.py, the
lineage rows as a slice of the round's own, and every checkpoint payload the two share byte for
byte (the gzip member after the header, which holds the world, the harness and the sampler). Then
each window's pace from its own rows, from 100 s after the resume to its end, so the restore is
left out.

    python compare.py <runs root> --seeds 1,2,3 --at 5000,15000,25000
"""
import argparse, glob, json, os, subprocess, sys, zlib

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))


def one_run(runs, arm):
    dirs = sorted(d for d in glob.glob(os.path.join(runs, arm, "*")) if os.path.isfile(os.path.join(d, "run.json")))
    return dirs[-1] if dirs else None


def payload(path):
    b = open(path, "rb").read()
    i = b.find(b"\x1f\x8b\x08")
    return zlib.decompressobj(16 + zlib.MAX_WBITS).decompress(b[i:]) if i >= 0 else None


def checkpoints(run):
    return {int(os.path.basename(f)[:9]): f for f in glob.glob(os.path.join(run, "checkpoints", "*.ckpt"))}


def lines(path):
    with open(path, encoding="utf-8") as f:
        return [l for l in f if l.strip()]


def lineage_slice(orig, mine):
    if not mine:
        return "empty"
    try:
        i = orig.index(mine[0])
    except ValueError:
        return "first row not in the round's lineage"
    same = orig[i:i + len(mine)] == mine
    return "identical (%d rows)" % len(mine) if same else "DIFFERENT"


def pace(run, skip):
    rows = [json.loads(l) for l in lines(os.path.join(run, "stats.jsonl"))]
    rows = [r for r in rows if r["t"] >= rows[0]["t"] + skip] or rows
    a, b = rows[0], rows[-1]
    wall = (b["wallTotalMs"] - a["wallTotalMs"]) / 1000.0
    sim = b["t"] - a["t"]
    return (sim / wall if wall > 0 else float("nan"), 1000.0 * wall / (sim / 0.5) if sim > 0 else float("nan"), b.get("alive"))


def main():
    p = argparse.ArgumentParser()
    p.add_argument("runs")
    p.add_argument("--source", default="r50")
    p.add_argument("--seeds", default="1,2,3")
    p.add_argument("--at", default="5000,15000,25000")
    a = p.parse_args()
    runs = os.path.abspath(a.runs)
    root = os.path.dirname(runs)
    bad = 0
    table = []
    for s in [int(x) for x in a.seeds.split(",")]:
        orig = one_run(runs, "%s-s%d" % (a.source, s))
        if not orig:
            print("seed %d: no %s run under %s" % (s, a.source, runs)); bad += 1; continue
        orig_lineage = lines(os.path.join(orig, "lineage.jsonl"))
        orig_ck = checkpoints(orig)
        for at in [int(x) for x in a.at.split(",")]:
            for mode in "gc":
                arm = "%sck-s%d-%d%s" % (a.source, s, at, mode)
                run = one_run(runs, arm)
                if not run:
                    print("%s: no run" % arm); bad += 1; continue
                m = json.load(open(os.path.join(run, "run.json"), encoding="utf-8"))
                cd = subprocess.run([sys.executable, os.path.join(REPO, "scripts", "compare-det.py"), "%s-s%d" % (a.source, s), arm,
                                     "--allow-partial", "--skip", "wall", "harnessBodySteps", "fluidLinkSteps"],
                                    cwd=root, capture_output=True, text=True)
                said = cd.stdout.strip().splitlines() + cd.stderr.strip().splitlines()
                rows_line = next((l for l in said if "identical" in l or "differ" in l.lower()), said[0] if said else "no output")
                lin = lineage_slice(orig_lineage, lines(os.path.join(run, "lineage.jsonl")))
                mine = checkpoints(run)
                shared = sorted(set(mine) & set(orig_ck))
                same = sum(1 for t in shared if payload(mine[t]) == payload(orig_ck[t]))
                x, ms, alive = pace(run, 100)
                ok = cd.returncode == 0 and lin.startswith("identical") and shared and same == len(shared)
                bad += not ok
                table.append((s, at, mode, m.get("transport"), cd.returncode, lin, same, len(shared), x, ms, alive))
                print("%s  transport %s  rows: %s" % (arm, m.get("transport"), rows_line))
                print("    lineage %s; checkpoints %d of %d identical; %.2fx real time, %.1f ms a metabolic step, %s alive"
                      % (lin, same, len(shared), x, ms, alive))
    print()
    print("seed      at  mode  transport  rows  lineage  ckpts    pace   ms/step")
    for s, at, mode, tr, rc, lin, same, n, x, ms, alive in table:
        print("%4d %7d  %4s  %9s  %4s  %7s  %2d/%-2d  %5.2fx  %7.1f" % (s, at, mode, tr, "ok" if rc == 0 else "DIFF", "ok" if lin.startswith("identical") else "DIFF", same, n, x, ms))
    for s in sorted({t[0] for t in table}):
        for at in sorted({t[1] for t in table if t[0] == s}):
            g = [t for t in table if t[0] == s and t[1] == at and t[2] == "g"]
            c = [t for t in table if t[0] == s and t[1] == at and t[2] == "c"]
            if g and c:
                print("seed %d at %d: the card's transport %.2fx against the CPU's %.2fx, %+.0f%%, %.1f ms a step saved"
                      % (s, at, g[0][8], c[0][8], 100 * (g[0][8] / c[0][8] - 1), c[0][9] - g[0][9]))
    print("VERDICT: %s" % ("every window identical to round 50's own record" if bad == 0 else "%d window(s) differ or are missing" % bad))
    sys.exit(1 if bad else 0)


main()