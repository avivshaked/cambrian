"""A pose file with its cut frame moved.

    python -I move_cut.py --poses <in> --cut <frame> --out <out>

Copies the pose file with `cut_frame` set to the new frame. `world_s` is null before it and `sim_s + world_minus_sim` from it on, on
the slow clock, so a step may be far below 1/30 s through the slow motion. (landing_io.check_poses requires a 1/30 step from the
cut on, so a cut inside the slow motion is not written through landing_io.write_poses; this tool checks what it can instead:
world_s is sim_s + world_minus_sim and rises.) Everything else is unchanged. Refuses a cut at or before 0 or past the last row."""
import argparse, json, os, sys
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import landing_io as LIO


def move_cut(doc, cut):
    """A new pose document with the cut moved; returns (doc, (first, last world_s), smallest step, largest step)."""
    rows = doc["rows"]
    if cut <= 0 or cut > len(rows) - 1:
        raise ValueError("cut %d is outside 1..%d (the last row)" % (cut, len(rows) - 1))
    wms = float(doc["world_minus_sim"])
    new_rows = []
    for r in rows:
        r = dict(r)
        r["world_s"] = None if r["f"] < cut else float(r["sim_s"]) + wms
        new_rows.append(r)
    ws = np.array([r["world_s"] for r in new_rows[cut:]])
    steps = np.diff(ws)
    if len(steps) and np.any(steps <= 0):
        raise ValueError("world_s does not rise from the new cut on")
    out = dict(doc)
    out["cut_frame"] = int(cut)
    out["source"] = "%s; cut moved to %d by move_cut.py" % (doc["source"], cut)
    out["rows"] = new_rows
    return out, (float(ws[0]), float(ws[-1])), (float(steps.min()) if len(steps) else None), \
        (float(steps.max()) if len(steps) else None)


def main(argv=None):
    ap = argparse.ArgumentParser(prog="move_cut.py")
    ap.add_argument("--poses", required=True)
    ap.add_argument("--cut", type=int, required=True)
    ap.add_argument("--out", required=True)
    a = ap.parse_args(argv)
    with open(a.poses, encoding="utf-8") as fh:
        doc = json.load(fh)
    try:
        out, span, lo, hi = move_cut(doc, a.cut)
    except ValueError as ex:
        print("REFUSED: %s" % ex)
        return 1
    os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
    with open(a.out, "w", encoding="utf-8") as fh:
        json.dump(out, fh, indent=None, separators=(",", ":"))
    print("CUT moved from %d to %d: %d rows with a world_s, world_s %.6f..%.6f (span %.6f s), step smallest %.6f s (%.4f of 1/30), "
          "largest %.6f s" % (doc["cut_frame"], a.cut, len(out["rows"]) - a.cut, span[0], span[1], span[1] - span[0], lo,
                              lo * LIO.FPS, hi))
    print("WROTE %s" % a.out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
