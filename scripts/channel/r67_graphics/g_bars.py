"""The bars graphic: bars-1 "The Nibblers ate little, and spent even less than they ate.", bars-2 "The Guzzlers ate four
times as much as the Nibblers. And they spent more than they ate.", bars-3 "On average, a Guzzler ended its turn hungrier
than when it started." One scene of 538 frames (cues.json: 235 + 180 + 123), after the designer's frames-v2/bars-1..3.png
(design/src/bars2.html).

    blender -b --factory-startup --python g_bars.py -- <out dir> <percent> <samples> [frames] [--slot bars-1|2|3] [--t S,S] [--dry]

Two things stand on one ledge of light, the level where every creature started its turn: a stack of the leaves a team ate
(Nibblers 3, Guzzlers 12: the fact-checked four to one) and a glass vial of what it had left, filled between the ledge and
where the turn ended: a bead above the ledge for the Nibblers, an empty warm-edged hollow below it for the Guzzlers. The
team icons are the real bodies: the Nibbler pool 50 and the Guzzler pool 23, at one scale per team.

SOURCES (round 67's story-video folder, scratch/story-video/round-67-202611061206/): the four numbers are the shaped arm's
1,800 s tank exam against the 40-generation run, own brain, in facts.md: 340.1 J eaten and +248.6 J net for the Nibblers,
1,294.0 J eaten and -42.3 J net for the Guzzlers (facts.md L65-66, and L898-901 for the 28 own-brain rows averaging 1,294.0 J and
-42.3 J, checked in reads/q14-check.txt, "means of the 28"). The Nibbler 340 and Guzzler 1,294 also stand in
scratch/r67-graphics/blender/notes/bodies.md L10-11 (the pool's mean ownEaten).

TO SCALE with each other (the owner's ruling via the coordinator): the Nibblers' bead, the larger level (+248.6 J), sits at 150 px
above the ledge and the Guzzlers' hollow (-42.3 J) at 150 x 42.3 / 248.6 = 25.5 px below it. The vial is 440-820 so the bead
fits (the design's vial stopped at 520). STILL DRAWN: the leaf counts, 3 and 12 (the fact-checked four to one: 340.1 and 1,294.0
are 3.8 to 1), each leaf about a hundred joules, and the leaf-to-vial ratio, which is no common joule scale.

Motion (notes/graphics.md): bars-1, the Guzzler half at 32%, three leaves drop 60 ms apart from 0.64 s, the Nibbler bead
rises over 0.6 s from "saved" (5.96); bars-2, the Nibbler half steps back to 60% (0.22-0.7), twelve leaves drop 60 ms apart
from 1.06; bars-3, the Guzzler level sinks and its hollow appears (1.12-2.12) while its stack eases to 70%.
"""
import math, os, random, sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import g_layout as L
import g_scene as G
import g_common as C

PLANE = 7.65
LEDGE, STEP = 640, 36
BEAD = 150.0                       # +248.6 J
HOLLOW = BEAD * 42.3 / 248.6       # -42.3 J, to scale with it
VIAL = {"w": 84, "top": 440, "bot": 820}
NIB = {"x": 560, "leaves": 3, "left": LEDGE - BEAD, "name": "Nibblers", "key": "nib-50", "team": "nibbler", "icon": 175, "iconX": -200,
       "store": ("#9ff5e6", "#2cc7b4")}
GUZ = {"x": 1360, "leaves": 12, "left": LEDGE + HOLLOW, "name": "Guzzlers", "key": "guz-23", "team": "guzzler", "icon": 190, "iconX": -205,
       "store": ("#ffe2a8", "#b07a22")}
T1, T2, T3 = 235, 180, 123
S1, S2, S3 = 0, 235, 415
FALL, BOUNCE = 0.45, 0.3


def drop_py(since, target, height=380.0):
    """A leaf's design row `since` seconds after it was let go: a fall from `height` px above, then a soft bounce."""
    if since < 0:
        return None
    if since < FALL:
        return target - height * (1 - (since / FALL) ** 2)
    b = since - FALL
    if b < BOUNCE:
        return target - 14.0 * abs(math.sin(math.pi * b / BOUNCE)) * (1 - b / BOUNCE)
    return target


def main():
    args = G.parse()
    cues = G.load_cues()
    model, d = C.camera()
    run = G.Run(args, cues, "bars", model)
    rng = random.Random(23)
    K = G.Keyer()
    END = run.N / G.FPS
    t2, t3 = S2 / G.FPS, S3 / G.FPS

    G.water_gradient()
    G.shafts(run, seed=23)
    led = G.ledge(run, t=PLANE)
    mats = G.body_material(run)
    m = model.metres_per_px(PLANE)

    teams = {}
    for T in (NIB, GUZ):
        sx, vx = T["x"] - 100, T["x"] + 70
        r = L.lcg(T["leaves"] * 7)
        leaves = []
        for i in range(T["leaves"]):
            x, tilt = sx + (r() - 0.5) * 12, (r() - 0.5) * 10
            lf = G.leaf(run, "%s-leaf%d" % (T["name"], i), x, LEDGE - 18 - i * STEP, PLANE, 70, tilt, strength=0.95)
            lf.scale = (1e-4,) * 3
            leaves.append((lf, x, LEDGE - 18 - i * STEP, tilt))
        v = G.vial(run, T["name"], vx + VIAL["w"] / 2, VIAL["top"], VIAL["bot"], VIAL["w"], LEDGE, PLANE, store=T["store"])
        ring = G.ring(run, T["name"] + "-ring", sx, LEDGE, 70, 20, PLANE, "#bffcf4", 0.25, 0.05)
        key_len = G.body_length(T["key"])
        scale = T["icon"] * m / key_len
        body = G.add_body(run, T["key"], T["name"] + "-icon", T["team"], mats, scale, rng)
        teams[T["name"]] = {"T": T, "leaves": leaves, "vial": v, "ring": ring, "body": body, "scale": scale, "sx": sx,
                            "vcx": vx + VIAL["w"] / 2}

    G.snow(run, rng, count=1100)
    G.S.grade()

    # ---- anchors
    for name, tm in teams.items():
        tag = "nib" if name == "Nibblers" else "guz"
        T = tm["T"]
        run.anchor(tag + "_vial_top", tm["vcx"], VIAL["top"], PLANE)
        run.anchor(tag + "_vial_bot", tm["vcx"], VIAL["bot"], PLANE)
        run.anchor(tag + "_cap", 0, 0, 1, parent=tm["vial"]["cap"])
        run.anchor(tag + "_top_leaf", 0, 0, 1, parent=tm["leaves"][-1][0])
        run.anchor(tag + "_icon", 0, 0, 1, parent=tm["body"].root)
        run.anchor("ate_" + tag, tm["sx"], 892, PLANE)
        run.anchor("left_" + tag, tm["vcx"], 892, PLANE)
        run.anchor("name_" + tag, T["x"] - 100, 995, PLANE)
    run.anchor("start", 1700, LEDGE - 18, PLANE)

    # ---- the words: (key, text, anchor, face, size, tracking, align, opacity rule by team dim)
    def team_dim(name, t):
        if name == "Nibblers":
            return 1.0 if t < t2 else (1.0 - 0.4 * L.span(t - t2, 0.22, 0.7)) if t < t3 else 1.0
        return 0.32 if t < t2 else 1.0

    def keys(name, extra=1.0):
        pts = [0.0, t2 - 0.01, t2, t2 + 0.22, t2 + 0.7, t3 - 0.01, t3, END]
        return [[p, round(team_dim(name, p) * extra, 3)] for p in pts]

    for name in ("Nibblers", "Guzzlers"):
        tag = "nib" if name == "Nibblers" else "guz"
        for w, text in (("ate_", "ate"), ("left_", "left")):
            run.words[w + tag] = {"text": text, "anchor": w + tag, "face": "mono-semibold", "size_px": 44, "tracking_px": 3,
                                  "color": "#e6fbf6", "glow": "#9ff5e6", "glow_alpha": 0.45, "opacity": keys(name)}
        run.words["name_" + tag] = {"text": name, "anchor": "name_" + tag, "face": "sans", "size_px": 60, "tracking_px": 0, "align": "start",
                                    "color": "#f2fffc", "glow": "#9ff5e6", "glow_alpha": 0.45, "opacity": keys(name)}
    run.words["start"] = {"text": "start", "anchor": "start", "face": "mono", "size_px": 38, "tracking_px": 3, "align": "end",
                          "color": "#bffcf4", "glow": "#9ff5e6", "glow_alpha": 0.0, "opacity": [[0.0, 0.9], [END, 0.9]]}

    sp = L.span
    for f in range(1, run.N + 1):
        t = (f - 1) / G.FPS
        slot = 1 if f <= S2 else 2 if f <= S3 else 3
        u = t - (0 if slot == 1 else t2 if slot == 2 else t3)
        for name, tm in teams.items():
            T = tm["T"]
            nib = name == "Nibblers"
            dim_team = team_dim(name, t)                       # the design's opacity of the team's half
            kdim = 1 - dim_team
            # the stack: leaves let fall 60 ms apart from the slot's cue
            start = None
            if nib and slot == 1:
                start = 0.64
            if not nib and slot == 2:
                start = 1.06
            for i, (lf, x, py, tilt) in enumerate(tm["leaves"]):
                if nib:
                    since = (u - (start + 0.06 * i)) if slot == 1 else 99.0
                    shown = slot >= 1
                else:
                    shown = slot >= 2
                    since = (u - (start + 0.06 * i)) if slot == 2 else 99.0
                row = drop_py(since, py) if shown else None
                if row is None:
                    sc = (1e-4,) * 3
                    row = py
                else:
                    sc = (70 * m, 70 * m * 0.18, 70 * m * 0.3)
                K.set(lf, "location", model.to_world(x, row, PLANE), f)
                K.set(lf, "scale", sc, f)
                stack_dim = kdim
                if not nib and slot == 3:
                    stack_dim = 0.3 * sp(u, 1.12, 2.12)            # eases from full to 70%
                if since < 99.0 and since >= 0:
                    stack_dim = max(stack_dim, 1 - sp(since, 0.0, 0.15))      # the leaf fades in as it is let go
                K.set(lf, "dimk", stack_dim, f)
            # the empty place on the ledge where the stack will stand (the Guzzlers', in bars-1)
            K.set(tm["ring"], "dimk", (1 - dim_team) if (not nib and slot == 1) else 1.0, f)
            # the vial
            v = tm["vial"]
            for o in G.vial_group(v):
                K.set(o, "dimk", kdim, f)
            if nib:
                lvl = LEDGE - BEAD * sp(u, 5.96, 6.56) if slot == 1 else LEDGE - BEAD
            else:
                lvl = LEDGE + HOLLOW * sp(u, 1.12, 2.12) if slot == 3 else LEDGE
            G.vial_level_k(v, lvl, f, K)
            # the team icon
            b = tm["body"]
            iconx = T["x"] + T["iconX"]
            C.kplace(K, b, model.to_world(iconx, 972, PLANE), 65, 0, tm["scale"], f)
            b.swim(t + (0.5 if nib else 0.0), f)
            K.set(b.root, "fade", 1 - dim_team, f)
            K.set(b.root, "grey", 0.0, f)

    frames = G.frame_list(args, cues, "bars")
    ok = True
    for name, offset in (("bars-1", S1), ("bars-2", S2), ("bars-3", S3)):
        lay = L.load_layout(name)
        check = [offset + 1 + int(round(s * G.FPS)) for s in lay["check_s"]]
        ok = run.layout_check(name, check, offset) and ok
    print("LAYOUT %s" % ("ALL WITHIN TOLERANCE" if ok else "SOME ANCHORS OUT OF TOLERANCE"))
    if args.dry:
        run.write_anchors(sorted(set(frames[:3])))
    else:
        run.write_anchors(range(1, run.N + 1))
    run.render(frames, "bars.blend")


if __name__ == "__main__":
    main()
