"""The seas' and the winter's design positions as pure arithmetic (no Blender), ported from the designer's
design/src/seas.html and design/src/winter.html with water.js's LCG, so the scenes and the layout files read the same numbers.

Seas: the four columns, who stands in each, and the design's rejection-sampled scatter. The bodies of sea i are drawn
from the stream rng(7 + i*13 + (50 if after)), as the design draws them.

Winter: the design draws each group from rng(n*17 + x), but consumes a different number of draws for a dead body than for a
live one, so its live bodies jump between its frames. Here every body consumes the same draws in every frame, so a body
that is still alive stands where it will die from. The group places (x, size, count, columns) and who dies are the design's.
"""
import math, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import g_layout as L

W = 340
CX = [270, 723, 1177, 1630]
OURS = 2
SIZE = {"guzzler": 112, "nibbler": 100, "shell": 118}


def cast(i, after):
    """[(kind, count)] of sea i. (a): 4 Guzzlers and 4 Nibblers in every sea. (d): this sea 4 taleplums, 1 other Guzzler
    and 3 Nibblers (the owner's corrected counts, 2026-11-14); the others 2, 1 and 0 Guzzlers, the rest Nibblers."""
    if not after:
        return [("guzzler", 4), ("nibbler", 4)]
    if i == OURS:
        return [("shell", 4), ("guzzler", 1), ("nibbler", 3)]
    guz = {0: 2, 1: 1, 3: 0}[i]
    return [("nibbler", 8 - guz), ("guzzler", guz)]


def scatter(r, cx, n):
    pts = []
    tries = 0
    while len(pts) < n and tries < 4000:
        tries += 1
        x = cx - W * 0.33 + r() * W * 0.66
        y = 270 + r() * 600
        if all(math.hypot(p[0] - x, (p[1] - y) * 1.3) > 128 for p in pts):
            pts.append((x, y))
    return pts


def sea_bodies(i, after):
    """The bodies of sea i: a list of dicts (kind, x, y, size, flip, rot) in the design's pixels, unforwarded."""
    r = L.lcg(7 + i * 13 + (50 if after else 0))
    kinds = [k for k, n in cast(i, after) for _ in range(n)]
    pts = scatter(r, CX[i], len(kinds))
    out = []
    for j, k in enumerate(kinds):
        flip = r() < 0.5
        rot = (r() - 0.5) * 24
        x, y = pts[j]
        out.append({"kind": k, "x": x, "y": y, "size": SIZE[k], "flip": flip, "rot": rot})
    return out


def forward_k(k, cx):
    """The chosen sea's dolly: scale k about (cx, 640)."""
    return lambda x, y: (cx + (x - cx) * k, 640 + (y - 640) * k)


GROUPS = [
    {"name": "Nibblers", "kind": "nibbler", "n": 10, "cols": 3, "x": 330, "size": 100, "die": [0, 1, 2, 4, 5, 6, 8, 9], "when": 1},
    {"name": "other Guzzlers", "kind": "guzzler", "n": 10, "cols": 3, "x": 960, "size": 110, "die": [0, 1, 2, 3, 5, 6, 7, 8, 9], "when": 2},
    {"name": "taleplums", "kind": "shell", "n": 6, "cols": 2, "x": 1590, "size": 130, "die": [3], "when": 3},
]
# The record behind the narration (scratch/story-video/round-67-202611061206/facts.md L848-853): from 15,000 to 18,000 s the
# thrifty lines (the Nibblers' team) fell 370 -> 100 (-73.0 %), the shaped animals outside 13929's lineage 51 -> 7 (-86.3 %),
# and 13929's lineage (the taleplums) 69 -> 58 (-15.9 %). Ten bodies to a group: 7 of 10 Nibblers (the design drew 8, which is
# 80 %), 9 of 10 other Guzzlers (86.3 % rounds to 9 of 10; "nearly nine in every ten"), 1 of 6 taleplums. The source wins.
DIE = {"Nibblers": [0, 1, 2, 4, 5, 6, 8], "other Guzzlers": [0, 1, 2, 3, 5, 6, 7, 8, 9], "taleplums": [3]}


def winter_bodies():
    """[(group, i, kind, x, y, size, flip, rot, dead_x, dead_y, dies)] for every body of the three groups."""
    out = []
    for G in GROUPS:
        r = L.lcg(G["n"] * 17 + G["x"])
        dx = 160 if G["cols"] == 3 else 210
        dy = 100 if G["cols"] == 3 else 140
        for i in range(G["n"]):
            c, row = i % G["cols"], i // G["cols"]
            rown = min(G["cols"], G["n"] - row * G["cols"])
            x = G["x"] + (c - (rown - 1) / 2) * dx + (r() - 0.5) * 56
            y = 250 + row * dy + (r() - 0.5) * 44
            flip = r() < 0.6
            dxx = x + (r() - 0.5) * 20
            dyy = 760 + row * 40 + r() * 36
            rot = (r() - 0.5) * 16
            out.append({"group": G["name"], "i": i, "kind": G["kind"], "x": x, "y": y, "size": G["size"], "flip": flip,
                        "rot": rot, "dx": dxx, "dy": dyy, "dies": i in DIE[G["name"]], "gx": G["x"]})
    return out
