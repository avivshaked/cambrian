"""A flat sheet of bodies: each body's parts drawn from the side (x, y) and from above (x, z) in Core's frame,
one scale for every cell so real sizes compare. No Blender; numpy and matplotlib only.

    python -I bodies_sheet.py [--bodies FILE] [--keys a,b,c | --team guzzler,nibbler,taleplum] --out PNG

A box is the hull of its eight corners; a sphere is its ellipsoid; a capsule is the hull of two end ellipsoids
(radius = mean of the x and z half extents, axis y), the way theatre_scene.visuals_of reads it.
"""
import argparse, json, math, os, sys
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
FILL = {"guzzler": "#e0a24a", "nibbler": "#8ae9da", "taleplum": "#d78fae"}
EDGE = {"guzzler": "#ffeccb", "nibbler": "#e2fff9", "taleplum": "#ffe0ec"}


def qmat(q):
    x, y, z, w = q
    return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                     [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                     [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])


def ellipsoid_points(centre, half, n=24):
    u = np.linspace(0, 2 * math.pi, n, endpoint=False)
    v = np.linspace(0, math.pi, n // 2 + 1)
    uu, vv = np.meshgrid(u, v)
    p = np.stack([np.cos(uu) * np.sin(vv), np.cos(vv), np.sin(uu) * np.sin(vv)], axis=-1).reshape(-1, 3)
    return centre + p * half


def part_points(part):
    """World-frame (Core) sample points of a part's surface; the hull of these is its silhouette."""
    h = np.abs(np.array(part["half"], float))
    pos = np.array(part["pos"], float)
    R = qmat(part["rot"])
    if part["shape"] == "box":
        local = np.array([[sx * h[0], sy * h[1], sz * h[2]] for sx in (-1, 1) for sy in (-1, 1) for sz in (-1, 1)])
    elif part["shape"] == "sphere":
        r = h.mean()
        local = ellipsoid_points(np.zeros(3), 2 * 0 + r * h / h.max())
    else:
        r = 0.5 * (h[0] + h[2])
        if h[1] < h[0] + h[2]:
            local = ellipsoid_points(np.zeros(3), np.array([h[0], max(h[1], r), h[2]]))
        else:
            span = max(0.0, h[1] - r)
            e = np.array([h[0], r, h[2]]) if r > 0 else h
            local = np.concatenate([ellipsoid_points(np.array([0, s * span, 0]), e) for s in (1, -1)])
    return pos + local @ R.T


def hull(points):
    pts = sorted(set(map(tuple, np.round(points, 6))))
    if len(pts) < 3:
        return np.array(pts)
    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lower, upper = [], []
    for p in pts:
        while len(lower) >= 2 and cross(lower[-2], lower[-1], p) <= 0:
            lower.pop()
        lower.append(p)
    for p in reversed(pts):
        while len(upper) >= 2 and cross(upper[-2], upper[-1], p) <= 0:
            upper.pop()
        upper.append(p)
    return np.array(lower[:-1] + upper[:-1])


def silhouettes(body, axes):
    """One hull per part, projected on the two Core axes in `axes`."""
    return [hull(part_points(p)[:, list(axes)]) for p in body["parts"]]


def extent(body):
    pts = np.concatenate([part_points(p) for p in body["parts"]])
    return pts.min(axis=0), pts.max(axis=0)


def draw(bodies, keys, out, cols=9, metres=1.6):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    from matplotlib.patches import Polygon
    rows = math.ceil(len(keys) / cols)
    fig, axs = plt.subplots(rows, cols, figsize=(cols * 2.2, rows * 2.5), facecolor="#031821", squeeze=False)
    for ax in axs.ravel():
        ax.axis("off")
    for ax, key in zip(axs.ravel(), keys):
        b = bodies[key]
        lo, hi = extent(b)
        mid = (lo + hi) / 2
        ax.set_xlim(-metres / 2, metres / 2)
        ax.set_ylim(-metres / 2 - 0.45, metres / 2)
        ax.set_aspect("equal")
        team = b.get("team", "guzzler")
        for (axes, shift) in (((0, 1), 0.0), ((0, 2), -0.75)):
            for poly in silhouettes(b, axes):
                if len(poly) < 3:
                    continue
                q = poly - np.array([mid[axes[0]], mid[axes[1]]]) + np.array([0, shift])
                ax.add_patch(Polygon(q, closed=True, facecolor=FILL[team], edgecolor=EDGE[team], alpha=0.85 if shift == 0 else 0.5, lw=0.8))
        ax.text(0, metres / 2 - 0.02, key, color="#e6fbf6", ha="center", va="top", fontsize=9)
        info = "%d parts" % len(b["parts"])
        if "ownEaten" in b:
            info += ", ate %.0f" % b["ownEaten"]
        ax.text(0, -metres / 2 - 0.43, info, color="#9ff5e6", ha="center", va="bottom", fontsize=7)
    fig.tight_layout(pad=0.4)
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    fig.savefig(out, dpi=110, facecolor=fig.get_facecolor())
    plt.close(fig)


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--bodies", default=os.path.join(HERE, "bodies-r67.json"))
    ap.add_argument("--keys")
    ap.add_argument("--team", default="guzzler,nibbler,taleplum")
    ap.add_argument("--nibblers", default="50,42,48,30,35,32,52", help="which Nibbler pool indexes to show")
    ap.add_argument("--out", required=True)
    a = ap.parse_args(argv)
    bodies = json.load(open(a.bodies))
    if a.keys:
        keys = a.keys.split(",")
    else:
        teams = a.team.split(",")
        nib = {"nib-" + x for x in a.nibblers.split(",")}
        def order(k):
            b = bodies[k]
            return (teams.index(b["team"]), b.get("ownEaten", 0.0), k)
        keys = sorted((k for k, b in bodies.items() if b["team"] in teams and (b["team"] != "nibbler" or k in nib)), key=order)
    draw(bodies, keys, a.out)
    print("sheet of %d bodies: %s" % (len(keys), a.out))
    return 0


if __name__ == "__main__":
    sys.exit(main())
