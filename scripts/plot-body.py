"""Draw one developed body from the solver bench's --dump text, as oriented boxes.

    python scripts/plot-body.py scratch/logs/giant-7597.txt --out scratch/snaps/r44-s1/giant-7597.png
    python scripts/plot-body.py dump.txt --title "body 7597 at its module ceiling" --tank-radius 26.46

The dump is what `Evosim.Dynamics.Bench --mode record --dump <id>` prints: one `part` line per
part with its position, half-extents and rotation in the developer's frame. Three panels: from
the side (x against y), from the end (z against y) and from above (x against z), every box drawn
as its twelve edges after the part's own rotation, with an optional bar for scale. It is a
schematic of the geometry the developer built, not the theatre's picture; the theatre cannot draw
a body whose module counts no record holds (logbook/0113's read).
"""

import argparse
import re
import sys

import numpy as np

PART = re.compile(
    r"part\s+(\d+): node (\d+) parent (-?\d+) depth (\d+) (\w+) at \(([^)]*)\) "
    r"half-extents \(([^)]*)\)(?: rot \(([^)]*)\))?"
)
HEAD = re.compile(r"body (\d+): adultScale ([\d.]+), (\d+) nodes, (\d+) parts")


def triple(text):
    return np.array([float(v) for v in text.split(",")], dtype=float)


def rotate(q, v):
    """Rotate v by the unit quaternion q = (x, y, z, w), Hamilton convention."""
    x, y, z, w = q
    u = np.array([x, y, z])
    return v + 2.0 * np.cross(u, np.cross(u, v) + w * v)


def corners(centre, half, q):
    out = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            for sz in (-1, 1):
                out.append(centre + rotate(q, np.array([sx * half[0], sy * half[1], sz * half[2]])))
    return np.array(out)


EDGES = [(0, 1), (0, 2), (0, 4), (1, 3), (1, 5), (2, 3), (2, 6), (3, 7), (4, 5), (4, 6), (5, 7), (6, 7)]


def read(path):
    parts = []
    head = None
    with open(path, encoding="utf-8") as f:
        for line in f:
            m = HEAD.search(line)
            if m:
                head = dict(id=int(m.group(1)), scale=float(m.group(2)),
                            nodes=int(m.group(3)), parts=int(m.group(4)))
            m = PART.search(line)
            if not m:
                continue
            q = triple(m.group(8)) if m.group(8) else np.array([0.0, 0.0, 0.0, 1.0])
            parts.append(dict(index=int(m.group(1)), node=int(m.group(2)), parent=int(m.group(3)),
                              depth=int(m.group(4)), joint=m.group(5), at=triple(m.group(6)),
                              half=triple(m.group(7)), rot=q))
    return head, parts


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("dump")
    ap.add_argument("--out", default=None)
    ap.add_argument("--title", default=None)
    ap.add_argument("--tank-radius", type=float, default=0.0,
                    help="draw the tank's radius as a bar in the top view, metres")
    args = ap.parse_args()

    head, parts = read(args.dump)
    if not parts:
        print("no part lines in " + args.dump, file=sys.stderr)
        return 2

    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    fig, axes = plt.subplots(1, 3, figsize=(18, 6.5))
    fig.patch.set_facecolor("#0b1c26")
    views = [("side: x across, y up", 0, 1), ("end: z across, y up", 2, 1), ("above: x across, z up", 0, 2)]
    cmap = plt.get_cmap("YlGn")
    depth_max = max(p["depth"] for p in parts) or 1

    for ax, (name, a, b) in zip(axes, views):
        ax.set_facecolor("#0b1c26")
        ax.set_aspect("equal")
        ax.set_title(name, color="#cfe6d8", fontsize=10)
        ax.tick_params(colors="#8fb3a3", labelsize=8)
        for spine in ax.spines.values():
            spine.set_color("#2a4a55")
        for p in parts:
            c = corners(p["at"], p["half"], p["rot"])
            colour = cmap(0.35 + 0.6 * p["depth"] / depth_max)
            for i, j in EDGES:
                ax.plot([c[i][a], c[j][a]], [c[i][b], c[j][b]], color=colour, linewidth=1.2)
            ax.plot([p["at"][a]], [p["at"][b]], marker="o", markersize=2.5, color="#ffd27f")
        if args.tank_radius > 0 and a == 0 and b == 2:
            ax.plot([0, args.tank_radius], [0, 0], color="#7fb8ff", linewidth=1)
            ax.text(args.tank_radius / 2, 0.4, f"tank radius {args.tank_radius:0.1f} m",
                    color="#7fb8ff", fontsize=8, ha="center")

    extent = np.max([np.abs(corners(p["at"], p["half"], p["rot"])).max() for p in parts]) * 1.1
    for ax in axes:
        ax.set_xlim(-extent, extent)
        ax.set_ylim(-extent, extent)
        ax.grid(True, color="#1f3a44", linewidth=0.5)

    title = args.title
    if title is None and head:
        title = (f"body {head['id']}: {head['parts']} parts from {head['nodes']} nodes, "
                 f"adult scale {head['scale']:0.2f}; axes in metres")
    if title:
        fig.suptitle(title, color="#e8f2ec", fontsize=12)

    fig.tight_layout()
    out = args.out or args.dump.rsplit(".", 1)[0] + ".png"
    fig.savefig(out, dpi=110, facecolor=fig.get_facecolor())
    print(out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
