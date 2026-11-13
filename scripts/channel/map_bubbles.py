"""Place B3's own crossing-bubble burst in the theatre's world. Plain Python and numpy, no bpy.

    python -I map_bubbles.py --bubbles <B3-frame export> --camera <B3 camera.json> --poses <pose file> --out <mapped export>

`sea_dive.py --path D --bubble-export <file> --world-minus-sim <x>` (without --pose-file) writes the burst in B3's own sea frame,
axes swapped by landing_io.to_unity and nothing more. The pose file's rows before its cut are B3's camera path placed in Unity's
world by the authoring tool: a turn about the vertical, a translation, and that axis swap. This tool recovers the turn and the
translation by fitting B3's camera positions (`loc` in the sea_dive camera file, through to_unity) to the pose rows' `eye`, over
the frames before the cut that both carry, and applies them: the turn and translation to x0, the turn alone to v0. The fit is
rigid: no scale, no reflection. It refuses when the largest residual exceeds 2 mm, because the rows should be B3's path exactly.
`--fit-only` fits and prints, and writes nothing. Formats: scratch/theatre-lane/landing-b3/formats.md; landing_io.py."""
import argparse, json, math, os, sys
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import landing_io as LIO

MAX_RESIDUAL_M = 0.002
WMS_TOL = 1e-6


def rot_y(theta):
    """A turn by theta about Unity's vertical (y), proper, acting on column vectors."""
    c, s = math.cos(theta), math.sin(theta)
    return np.array([[c, 0.0, -s], [0.0, 1.0, 0.0], [s, 0.0, c]])


def fit_turn_translation(P, Q):
    """The turn about y (radians) and translation t (3 vector) with Q ~ P @ rot_y(theta).T + t, least squares: a 2D fit on
    (x, z) for the turn, and the translation from the centroids (so the vertical offset is the mean y difference). Returns
    theta, t, the per-point residual distances."""
    P, Q = np.asarray(P, float), np.asarray(Q, float)
    if P.shape != Q.shape or P.ndim != 2 or P.shape[1] != 3 or len(P) < 2:
        raise ValueError("need matching (n, 3) point sets with n >= 2")
    pc, qc = P.mean(axis=0), Q.mean(axis=0)
    A, B = (P - pc)[:, [0, 2]], (Q - qc)[:, [0, 2]]
    # rot_y(theta) on (x, z) is [[c, -s], [s, c]]: a plain 2D turn by theta
    num = float(np.sum(A[:, 0] * B[:, 1] - A[:, 1] * B[:, 0]))
    den = float(np.sum(A[:, 0] * B[:, 0] + A[:, 1] * B[:, 1]))
    theta = math.atan2(num, den)
    R = rot_y(theta)
    t = qc - R @ pc
    res = np.linalg.norm(P @ R.T + t - Q, axis=1)
    return theta, t, res


def paired_points(camera, poses):
    """B3's loc (through to_unity) and the pose rows' eye, for the frames before the cut that both carry."""
    cut = poses["cut_frame"]
    rows = {r["f"]: r for r in poses["rows"]}
    P, Q, fs = [], [], []
    for fr in camera["frames"]:
        f = fr["f"]
        if f < cut and f in rows:
            P.append(LIO.to_unity(fr["loc"]))
            Q.append(rows[f]["eye"])
            fs.append(f)
    if len(P) < 2:
        raise ValueError("fewer than two frames before the cut are in both the camera file and the pose file")
    return np.array(P), np.array(Q), fs


def place(doc, theta, t):
    """The export's arrays placed: x0 turned and moved, v0 turned only."""
    R = rot_y(theta)
    x0 = np.asarray(doc["x0"], float) @ R.T + t
    v0 = np.asarray(doc["v0"], float) @ R.T
    return x0, v0


def main(argv=None):
    ap = argparse.ArgumentParser(prog="map_bubbles.py")
    ap.add_argument("--bubbles", required=True)
    ap.add_argument("--camera", required=True)
    ap.add_argument("--poses", required=True)
    ap.add_argument("--out", default=None)
    ap.add_argument("--fit-only", action="store_true", help="fit and print; write nothing (--bubbles is then not read)")
    a = ap.parse_args(argv)
    if not a.fit_only and not a.out:
        ap.error("--out is required unless --fit-only")
    with open(a.camera, encoding="utf-8") as fh:
        camera = json.load(fh)
    poses = LIO.read_poses(a.poses)
    P, Q, fs = paired_points(camera, poses)
    theta, t, res = fit_turn_translation(P, Q)
    rms, worst = float(np.sqrt(np.mean(res ** 2))), float(res.max())
    print("FIT %d frames (f %d..%d, before the cut %d): turn %.6f deg about the vertical, translation (%.6f, %.6f, %.6f) m; "
          "residual rms %.6f m, largest %.6f m (at f %d)" % (len(fs), fs[0], fs[-1], poses["cut_frame"], math.degrees(theta),
                                                           *t, rms, worst, fs[int(res.argmax())]), flush=True)
    if worst > MAX_RESIDUAL_M:
        print("REFUSED: the largest residual %.6f m exceeds %.3f m; the rows are not B3's path" % (worst, MAX_RESIDUAL_M))
        return 1
    if a.fit_only:
        return 0
    doc = LIO.read_bubbles(a.bubbles)
    if abs(float(doc["world_minus_sim"]) - float(poses["world_minus_sim"])) > WMS_TOL:
        print("REFUSED: world_minus_sim is %r in the bubbles and %r in the pose file" % (doc["world_minus_sim"],
                                                                                         poses["world_minus_sim"]))
        return 1
    x0, v0 = place(doc, theta, t)
    src = ("map_bubbles.py: bubbles %s sha256 %s (%s); camera %s sha256 %s; poses %s sha256 %s; fit over %d frames, turn %.6f deg "
           "about the vertical, translation (%.6f, %.6f, %.6f) m, residual rms %.6f m, largest %.6f m" % (
               os.path.basename(a.bubbles), LIO.sha256(a.bubbles), doc.get("source", ""), os.path.basename(a.camera),
               LIO.sha256(a.camera), os.path.basename(a.poses), LIO.sha256(a.poses), len(fs), math.degrees(theta), *t, rms, worst))
    os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
    LIO.write_bubbles(a.out, x0, v0, doc["ts"], doc["r"], doc["rise"], doc["tau"], doc["flat"], poses["world_minus_sim"], src,
                      surface_y=doc.get("surface_y", 0.0))
    print("MAPPED %d bubbles to %s" % (doc["count"], a.out), flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
