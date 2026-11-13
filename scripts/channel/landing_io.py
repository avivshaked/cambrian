"""The landing's shared files: the pose file and the crossing-bubble export, and the one conversion between Unity's and
Blender's axes. Plain Python and numpy, no bpy, so the authoring tool, sea_dive.py and the tests all read and write
through the same code. The formats are specified in scratch/theatre-lane/landing-b3/formats.md (theatre lane,
2026-11-12); this module is their reference implementation, and `bubble_positions` is the closed form the theatre's
TheatreCrossingBubbles evaluates.

Unity is y-up, Blender z-up, both with the mean sea at 0: Blender = (Unity.x, Unity.z, Unity.y). The swap is a
reflection, so angles are never converted directly; convert vectors and measure angles afterwards."""
import hashlib, json, math
import numpy as np

POSES_FORMAT = "landing-poses 1"
BUBBLES_FORMAT = "crossing-bubbles 1"
FPS = 30


def to_blender(v):
    """Unity (x, y, z) to Blender (x, y, z); works on one vector or an (n, 3) array."""
    a = np.asarray(v, dtype=float)
    return a[..., [0, 2, 1]]


def to_unity(v):
    """Blender (x, y, z) to Unity (x, y, z); the swap is its own inverse."""
    a = np.asarray(v, dtype=float)
    return a[..., [0, 2, 1]]


def up_for(forward, world_up=(0.0, 1.0, 0.0)):
    """The roll-free up vector for a Unity forward: world up projected off forward, normalised."""
    f = np.asarray(forward, dtype=float)
    f = f / np.linalg.norm(f)
    u = np.asarray(world_up, dtype=float) - f * float(np.dot(world_up, f))
    n = np.linalg.norm(u)
    if n < 1e-9:
        raise ValueError(f"forward {f.tolist()} is vertical; up is undefined")
    return u / n


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def check_poses(doc):
    """Raise ValueError naming the first breach of the pose format; return doc."""
    if doc.get("format") != POSES_FORMAT:
        raise ValueError(f"format is {doc.get('format')!r}, not {POSES_FORMAT!r}")
    if doc.get("fps") != FPS:
        raise ValueError(f"fps is {doc.get('fps')!r}, not {FPS}")
    cut, wms, rows = doc["cut_frame"], float(doc["world_minus_sim"]), doc["rows"]
    if not rows:
        raise ValueError("no rows")
    for i, r in enumerate(rows):
        if r["f"] != i:
            raise ValueError(f"row {i} has f {r['f']}; rows must run 0, 1, 2, ... with none missing")
        if abs(r["film_s"] - i / FPS) > 1e-6:
            raise ValueError(f"row {i}: film_s {r['film_s']} is not f / {FPS}")
        f = np.asarray(r["forward"], float)
        u = np.asarray(r["up"], float)
        if abs(np.linalg.norm(f) - 1) > 1e-5 or abs(np.linalg.norm(u) - 1) > 1e-5 or abs(float(f @ u)) > 1e-5:
            raise ValueError(f"row {i}: forward and up must be orthogonal unit vectors")
        if not 1.0 < r["vfov"] < 179.0:
            raise ValueError(f"row {i}: vfov {r['vfov']} out of range")
        if i < cut:
            if r["world_s"] is not None:
                raise ValueError(f"row {i} is before the cut frame {cut} and has a world_s")
        else:
            if r["world_s"] is None:
                raise ValueError(f"row {i} is at or after the cut frame {cut} and has no world_s")
            if abs(r["world_s"] - (r["sim_s"] + wms)) > 1e-6:
                raise ValueError(f"row {i}: world_s {r['world_s']} is not sim_s + world_minus_sim")
            if i > cut and abs(r["world_s"] - rows[i - 1]["world_s"] - 1 / FPS) > 1e-6:
                raise ValueError(f"row {i}: world_s does not rise by 1/{FPS} from the row before")
    return doc


def write_poses(path, rows, cut_frame, world_minus_sim, source):
    doc = {"format": POSES_FORMAT, "fps": FPS, "cut_frame": int(cut_frame), "world_minus_sim": float(world_minus_sim),
           "source": source, "rows": rows}
    check_poses(doc)
    with open(path, "w", encoding="utf-8") as fh:
        json.dump(doc, fh, indent=None, separators=(",", ":"))
    return doc


def read_poses(path):
    with open(path, encoding="utf-8") as fh:
        return check_poses(json.load(fh))


def write_bubbles(path, x0, v0, ts, r, rise, tau, flat, world_minus_sim, source, surface_y=0.0):
    """Arrays in Unity coordinates and on sim_s, as formats.md says; x0 and v0 are (n, 3)."""
    x0, v0 = np.asarray(x0, float), np.asarray(v0, float)
    n = len(x0)
    cols = {"ts": ts, "r": r, "rise": rise, "tau": tau, "flat": flat}
    for k, v in cols.items():
        if len(np.ravel(v)) != n:
            raise ValueError(f"{k} has {len(np.ravel(v))} entries, x0 has {n}")
    doc = {"format": BUBBLES_FORMAT, "world_minus_sim": float(world_minus_sim), "surface_y": float(surface_y),
           "count": n, "source": source, "x0": x0.tolist(), "v0": v0.tolist()}
    doc.update({k: np.ravel(np.asarray(v, float)).tolist() for k, v in cols.items()})
    with open(path, "w", encoding="utf-8") as fh:
        json.dump(doc, fh, separators=(",", ":"))
    return doc


def read_bubbles(path):
    with open(path, encoding="utf-8") as fh:
        doc = json.load(fh)
    if doc.get("format") != BUBBLES_FORMAT:
        raise ValueError(f"format is {doc.get('format')!r}, not {BUBBLES_FORMAT!r}")
    return doc


def bubble_positions(doc, world_s):
    """The bubbles' Unity centres at a world second and which are alive: sea_dive.py's Bubbles.positions with the
    vertical moved to y, against the flat surface at surface_y."""
    sim = world_s - doc["world_minus_sim"]
    x0, v0 = np.asarray(doc["x0"], float), np.asarray(doc["v0"], float)
    ts, r = np.asarray(doc["ts"], float), np.asarray(doc["r"], float)
    rise, tau = np.asarray(doc["rise"], float), np.asarray(doc["tau"], float)
    a = sim - ts
    live = a >= 0
    a = np.maximum(a, 0.0)[:, None]
    w = np.zeros_like(x0)
    w[:, 1] = rise
    X = x0 + w * a + (v0 - w) * tau[:, None] * (1 - np.exp(-a / tau[:, None]))
    live &= X[:, 1] < doc["surface_y"] - r
    return X, live
