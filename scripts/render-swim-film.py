"""Renders a nursery film trace (Evosim.Nursery --film, deleted in the nursery's step 6; old traces only) as a video: propose-joint-drive.md's test film.

Each link is drawn as a box at the pose the solver recorded. The stroke is not simulated under the
bend-and-power drive, so it is drawn here from the logged power: a link whose joint has power s wags
about its hinge through A = 0.4 * sqrt(s) rad, at the rate that makes its peak swing s * Omega, in
the owner's curve: each half-stroke is a fast stroke across most of the way (REACH of the distance in
STROKE of the time, with a minor ease in) and then a long ease out to the edge (1 - (1 - s)^GLIDE), the
speed continuous where the two meet. A
child link is carried by its parent's drawn wag. The camera follows the body over a fixed world grid,
so what moves across the grid is the body's own travel.

    python scripts/render-swim-film.py <film.jsonl> <out.mp4> --title "..." [--omega 2] [--from S | --best 20]

--best N picks the N-second window in which the root travels farthest across; --from S starts at S.
A contact sheet of five frames is written beside the video (<out>-sheet.png) for checking.
"""
import argparse, json, math, os, sys
import numpy as np
import matplotlib
import matplotlib.patches
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib import animation
from mpl_toolkits.mplot3d.art3d import Poly3DCollection

ap = argparse.ArgumentParser()
ap.add_argument("film"); ap.add_argument("out")
ap.add_argument("--title", default="")
ap.add_argument("--omega", type=float, default=2.0)
ap.add_argument("--seconds", type=float, default=20.0)
ap.add_argument("--from", dest="start", type=float, default=None)
ap.add_argument("--best", action="store_true")
ap.add_argument("--ffmpeg", default=None)
a = ap.parse_args()

frames = [json.loads(l) for l in open(a.film)]
fps = 30
n = int(round(a.seconds * fps))
root = np.array([[f["links"][0][0], f["links"][0][2]] for f in frames])
if a.start is not None:
    i0 = min(range(len(frames)), key=lambda i: abs(frames[i]["t"] - a.start))
elif a.best:
    full = np.array([f["links"][0][0:3] for f in frames])
    i0 = max(range(0, max(1, len(frames) - n)), key=lambda i: np.linalg.norm(full[min(i + n, len(frames) - 1)] - full[i]))
else:
    i0 = 0
window = frames[i0:i0 + n]
print(f"window {window[0]['t']:.2f} to {window[-1]['t']:.2f} s, {len(window)} frames")

# The camera looks at the body from the side of its travel over the window, a little above it.
_dx = window[-1]["links"][0][0] - window[0]["links"][0][0]
_dz = window[-1]["links"][0][2] - window[0]["links"][0][2]
AZIM = math.degrees(math.atan2(_dz, _dx)) - 90.0

COLOURS = {"consumer": "#d9745b", "link": "#8fb3d9", "absorptive": "#c9b458", "photosynthetic": "#6fbf73", "buoyancy": "#b9a6d9"}

def rodrigues(axis, angle):
    axis = np.asarray(axis, float); axis = axis / (np.linalg.norm(axis) or 1)
    k = np.array([[0, -axis[2], axis[1]], [axis[2], 0, -axis[0]], [-axis[1], axis[0], 0]])
    return np.eye(3) + math.sin(angle) * k + (1 - math.cos(angle)) * k @ k

# The owner's fish stroke (2026-09-30): fast most of the way, then a long ease at the edge. The first
# cut (sign(sin) * |sin|^0.6) took 69% of each half-stroke to cover 85% of the way; the owner asked for
# the stroke to be faster before the ease.
STROKE, REACH, GLIDE, EASE_IN = 0.35, 0.8, 4.0, 0.25

def _half(u):
    """Position 0 to 1 over one half-stroke, u in [0, 1)."""
    if u < STROKE:
        # Cubic Hermite from 0 to REACH: a minor ease in (slope EASE_IN), and at its end the glide's
        # starting speed, so the stroke hands over to the ease without a jolt.
        t = u / STROKE
        b = (1 - REACH) * GLIDE / (1 - STROKE) * STROKE / REACH
        f = (t**3 - 2*t**2 + t) * EASE_IN + (-2*t**3 + 3*t**2) + (t**3 - t**2) * b
        return REACH * f
    t = (u - STROKE) / (1 - STROKE)
    return REACH + (1 - REACH) * (1 - (1 - t) ** GLIDE)

def ease(phi):
    """The wag at phase phi, -1 to 1: at the edge -1 at phi = -pi/2 and +1 at pi/2, as sin is."""
    u = ((phi + math.pi / 2) % (2 * math.pi)) / math.pi
    return -1 + 2 * _half(u) if u < 1 else 1 - 2 * _half(u - 1)

# the drawn wag's phase per link, integrated through the window so the rate follows the power
phase = {}
def wags(frame, dt):
    out = {}
    for i, k in enumerate(frame["links"]):
        s, nd = k[18], k[20]
        if nd == 0 or s <= 0:
            out[i] = 0.0; continue
        amp = 0.4 * math.sqrt(s)
        f = s * a.omega / (2 * math.pi * amp)
        phase[i] = phase.get(i, 0.0) + 2 * math.pi * f * dt
        out[i] = amp * ease(phase[i])
    return out

def boxes(frame, angles):
    links = frame["links"]
    transforms = {}   # link -> list of (pivot, R) applied in order, from the root's descendants down
    polys, colours = [], []
    for i, k in enumerate(links):
        parent = k[15]
        chain = list(transforms.get(parent, [])) if parent >= 0 else []
        if angles.get(i, 0.0) != 0.0:
            chain.append((np.array(k[21:24]), rodrigues(k[24:27], angles[i])))
        transforms[i] = chain
        p = np.array(k[0:3]); R = np.array(k[3:12]).reshape(3, 3); h = np.array(k[12:15])
        corners = np.array([[sx * h[0], sy * h[1], sz * h[2]] for sx in (-1, 1) for sy in (-1, 1) for sz in (-1, 1)])
        world = p + corners @ R.T
        for pivot, Rw in reversed(chain):
            world = pivot + (world - pivot) @ Rw.T
        faces = [[0, 1, 3, 2], [4, 5, 7, 6], [0, 1, 5, 4], [2, 3, 7, 6], [0, 2, 6, 4], [1, 3, 7, 5]]
        for fc in faces:
            polys.append([(world[j][0], world[j][2], world[j][1]) for j in fc])   # plot (x, z, y-up)
            colours.append(COLOURS.get(k[17], "#aaaaaa"))
    return polys, colours

fig = plt.figure(figsize=(12.8, 7.2), dpi=100, facecolor="#0b1e33")
ax = fig.add_axes([0, 0, 1, 1], projection="3d", facecolor="#0b1e33")
title = fig.text(0.02, 0.95, a.title, color="white", fontsize=15, family="DejaVu Sans")
note = fig.text(0.02, 0.91, "bend-and-power drive, still water, Omega %.1f rad/s. The stroke is drawn from the logged power; the body's travel is simulated." % a.omega, color="#b8c7d9", fontsize=10.5)
clock = fig.text(0.02, 0.05, "", color="white", fontsize=12, family="DejaVu Sans Mono")
fig.patches.append(matplotlib.patches.Rectangle((0.70, 0.045), 0.25, 0.03, transform=fig.transFigure, facecolor="none", edgecolor="#b8c7d9", lw=1))
gauge = matplotlib.patches.Rectangle((0.70, 0.045), 0.0, 0.03, transform=fig.transFigure, facecolor="#f2c14e")
fig.patches.append(gauge)
gauge_text = fig.text(0.70, 0.085, "", color="white", fontsize=11, family="DejaVu Sans Mono")

trail = []
def draw(j):
    frame = window[j]
    dt = 1.0 / fps
    angles = wags(frame, dt)
    ax.cla()
    ax.set_facecolor("#0b1e33"); ax.set_axis_off()
    r0 = frame["links"][0]
    cx, cy, cz = r0[0], r0[2], r0[1]
    half = 0.7
    ax.set_xlim(cx - half, cx + half); ax.set_ylim(cy - half, cy + half); ax.set_zlim(cz - half * 0.56, cz + half * 0.56)
    ax.set_box_aspect((1, 1, 0.56))
    ax.view_init(elev=22, azim=AZIM)
    # a fixed world grid a little below the body, every 10 cm
    gz = cz - 0.45
    g0x, g0y = math.floor((cx - half) * 10) / 10, math.floor((cy - half) * 10) / 10
    for v in np.arange(g0x, cx + half + 0.1, 0.1):
        ax.plot([v, v], [cy - half, cy + half], [gz, gz], color="#27496d", lw=0.7)
    for v in np.arange(g0y, cy + half + 0.1, 0.1):
        ax.plot([cx - half, cx + half], [v, v], [gz, gz], color="#27496d", lw=0.7)
    trail.append((cx, cy))
    if len(trail) > 1:
        tr = np.array(trail)
        ax.plot(tr[:, 0], tr[:, 1], [gz] * len(tr), color="#f2c14e", lw=1.5, alpha=0.9)
    polys, colours = boxes(frame, angles)
    ax.add_collection3d(Poly3DCollection(polys, facecolors=colours, edgecolors="#10263d", linewidths=0.6, alpha=0.95))
    s = max((k[18] for k in frame["links"]), default=0)
    k0 = max(0, j - fps // 2)
    a0, a1 = window[k0]["links"][0], window[j]["links"][0]
    d = math.sqrt(sum((a1[c] - a0[c]) ** 2 for c in range(3)))
    speed = 100 * d / max(1e-9, window[j]["t"] - window[k0]["t"]) if j > 0 else 0.0
    clock.set_text(f"t {frame['t']:6.2f} s   depth {-r0[1]:4.2f} m   speed over 0.5 s {speed:4.1f} cm/s   grid 10 cm")
    gauge.set_width(0.25 * s)
    gauge_text.set_text(f"power {s:4.2f}")
    return []

if a.ffmpeg: plt.rcParams["animation.ffmpeg_path"] = a.ffmpeg
writer = animation.FFMpegWriter(fps=fps, bitrate=6000, codec="libx264", extra_args=["-pix_fmt", "yuv420p"])
with writer.saving(fig, a.out, dpi=100):
    for j in range(len(window)):
        draw(j)
        writer.grab_frame()

# the contact sheet: five frames spread through the window, drawn again from the same state
trail.clear(); phase.clear()
sheet = [int(x) for x in np.linspace(0, len(window) - 1, 5)]
imgs = []
for j in range(len(window)):
    draw(j)
    if j in sheet:
        fig.canvas.draw()
        imgs.append(np.asarray(fig.canvas.buffer_rgba())[:, :, :3].copy())
row = np.concatenate([im[::2, ::2] for im in imgs], axis=1)
plt.imsave(os.path.splitext(a.out)[0] + "-sheet.png", row)

# and six consecutive frames a tenth of a second apart from the middle, for the drawn stroke
trail.clear(); phase.clear()
mid = len(window) // 2
strip_at = [mid + 3 * q for q in range(6)]
strip = []
for j in range(min(len(window), strip_at[-1] + 1)):
    draw(j)
    if j in strip_at:
        fig.canvas.draw()
        im = np.asarray(fig.canvas.buffer_rgba())[:, :, :3].copy()
        h, w = im.shape[:2]
        strip.append(im[int(h * 0.2):int(h * 0.8), int(w * 0.3):int(w * 0.7)])
plt.imsave(os.path.splitext(a.out)[0] + "-strip.png", np.concatenate(strip, axis=1))
print("wrote", a.out)
