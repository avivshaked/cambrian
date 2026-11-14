"""The founder graphic: "Back in the tank, the founder had been one of the smallest eaters among the Guzzlers sent out.
And the tank, which measured only eating, rated it low. But in the tank, the founder usually ended its turn with savings."
One slot of 13.7 s, 411 frames (RULINGS 1: beat 9, 50.50-64.20), after the designer's frames-v5/founder-1..3.png
(design/src/founder.html): the tank's place, a row of seven Guzzlers each over a tube of what it ate, the founder fifth.

    blender -b --factory-startup --python g_founder.py -- <out dir> <percent> <samples> [frames] [--t S,S] [--dry]

The bodies are the sea run's real pool Guzzlers, left to right pools 23, 19, 21, 14, the founder (pool 2, the sea's body
13929, drawn in the taleplum palette as the design draws it), 26 and 10, so the three fullest (23, 21, 26) stand first, third
and sixth, where the design lights them. One scale for the Guzzlers; the founder keeps its real, smaller size.

LEVELS (RULINGS 3 and 4). Every level is MEASURED, none is drawn, from the 1,800 s tank exam of the shaped arm, own brain
(scratch/kit-arm/runs/forager-kit-shaped/tank-exam.jsonl; facts.md L896-925; fact-check-1's founder row). The TUBES are joules
eaten (2,449.9 for pool 23 down to 408.8 for the founder) on g_common.K_J, which puts 2,449.9 J at 86% of a tube. The VIALS are the
exam's net, end energy less start energy, on a scale of their own, separate from the tubes' and chosen to be readable: the
largest |net| of the seven (400.6 J, pool 10) is 0.3 of a vial's height (240 px, so 72 px) and every other vial is to scale with
it (VIAL_PX_PER_J = 0.3 x 240 / 400.6 = 0.1797 px per joule). Within the vials everything is to scale; a vial's height is not
comparable with a tube's. The nets are, in order: +45.3, -97.0, -57.5, +162.2, +179.5 (the founder, 4th of 28), -255.0 and -400.6 J.
The record behind "usually ended its turn with savings" is the founder's mean net of +179.5 J, with 11 of its 32 draws below
zero; the picture shows the mean. Two others, pools 23 and 14, also net above zero and show a bead too.

Motion (notes/graphics.md): a cone finds the founder at "founder" (1.21), reached 2.5; the light moves to the three fullest
(6.0-7.0) while the founder dims, reached 8.0; on "But" (8.67) a vial rises beside every tube over 0.8 s; 9.3-10.5 the others
sink to their hollows; at "the founder" (10.30) the light comes back to it; 11.9-12.4 the founder's bead fills and lands on
"savings" (12.40), and "left" comes up at 12.14; reached 13.0.
"""
import math, os, random, sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import g_layout as L
import g_scene as G
import g_common as C

PLANE = 7.65
ROW = [23, 19, 21, 14, 2, 26, 10]
FOUNDER, TOP3 = 4, [2, 0, 5]
X0, STEP, GAP = 140, 260, 72
TUBE = {"top": 690, "bot": 930, "w": 50}
VIAL = {"w": 40, "ledge": 810}
TANK = (820, 150, 1120, 380)


def px(i):
    return X0 + i * STEP


def body_y(i):
    return 560 + (16 if i % 2 else -8)


VIAL_PX_PER_J = 0.3 * (TUBE["bot"] - TUBE["top"]) / 400.6     # the vials' own scale: 400.6 J = 0.3 of a vial


def vial_row(pool):
    """The design row of a vial's surface for a creature: the ledge less its measured net, on the vials' own scale."""
    return VIAL["ledge"] - C.NET[pool] * VIAL_PX_PER_J


def main():
    args = G.parse()
    cues = G.load_cues()
    model, d = C.camera()
    run = G.Run(args, cues, "founder", model)
    rng = random.Random(67)
    K = G.Keyer()
    END = run.N / G.FPS

    G.water_gradient()
    G.shafts(run, seed=11)
    root, tk = G.tank_back(run, TANK, t_back=18.0)
    mats = G.body_material(run)
    guz_len = sorted(G.body_length(C.key_of(p)) for p in ROW if p != 2)
    SG = 190.0 * (PLANE / model.f_px) / guz_len[len(guz_len) // 2]

    bodies = [G.add_body(run, C.key_of(p), "f%d" % i, "taleplum" if p == 2 else "guzzler", mats, SG, rng) for i, p in enumerate(ROW)]
    t_tube = PLANE - 0.02
    tubes = [G.tube(run, "ft%d" % i, px(i), TUBE["top"], TUBE["bot"], TUBE["w"], t_tube, C.frac(C.EATEN[p]), leaf_rx=0.4 * TUBE["w"], spacing=0.28 * TUBE["w"])
             for i, p in enumerate(ROW)]
    vials = [G.vial(run, "fv%d" % i, px(i) + GAP, TUBE["top"], TUBE["bot"], VIAL["w"], VIAL["ledge"], t_tube, notch=True) for i in range(7)]
    beams = {i: G.beam(run, px(i) + GAP / 2, 700, 170, depth=10.0, name="fcone%d" % i) for i in set(TOP3 + [FOUNDER])}

    G.snow(run, rng, count=1100)
    G.S.grade()

    # ---- anchors
    for i, p in enumerate(ROW):
        run.anchor("body_%d" % i, 0, 0, 1, parent=bodies[i].root)
        run.anchor("tube_top_%d" % i, px(i), TUBE["top"], t_tube)
        run.anchor("tube_bot_%d" % i, px(i), TUBE["bot"], t_tube)
        run.anchor("vial_top_%d" % i, px(i) + GAP, TUBE["top"], t_tube)
        run.anchor("vial_cap_%d" % i, 0, 0, 1, parent=vials[i]["cap"])
    run.anchor("word_eaten", px(FOUNDER) + 22, 990, t_tube)
    run.anchor("word_left", px(FOUNDER) + GAP - 18, 990, t_tube)
    run.anchor("tank_tl", TANK[0], TANK[1], 18.0)

    sp = L.span
    T = {"c1": 1.21, "to3": (6.0, 7.0), "vials": (8.67, 9.47), "sink": (9.3, 10.5), "back": (10.30, 11.0), "bead": (11.9, 12.4), "left": 12.14}
    lit_hold = {}
    for f in range(1, run.N + 1):
        t = (f - 1) / G.FPS
        # who is lit (the design's opacity): founder-1, then the tank's three, then the founder again
        a = sp(t, *T["to3"])                       # 0 -> 1: the light moves to the three
        b = sp(t, *T["back"])                      # 0 -> 1: and back to the founder
        found_in = sp(t, 1.0, 1.8)                 # the first cone finds the founder
        for i, p in enumerate(ROW):
            founder = i == FOUNDER
            top3 = i in TOP3
            if founder:
                lit = L.lerp(0.7, 1.0, found_in)
                lit = L.lerp(lit, 0.42, a)
                lit = L.lerp(lit, 1.0, b)
            elif top3:
                lit = L.lerp(0.7, 1.0, a)
                lit = L.lerp(lit, 0.5, b)
            else:
                lit = L.lerp(0.7, 0.5, a)
                lit = L.lerp(lit, 0.5, b)
            y = body_y(i) - (10 if founder else 0)
            C.kplace(K, bodies[i], model.to_world(px(i) + GAP / 2, y, PLANE), -65 if founder else 65, -4 if founder else (i % 3 - 1) * 5, SG, f)
            bodies[i].swim(t + i * 0.6, f)
            G.tube_level_k(tubes[i], C.frac(C.EATEN[p]), f, K)
            K.set(bodies[i].root, "fade", (1 - lit) * 0.9, f)
            for o in G.tube_group(tubes[i]):
                K.set(o, "dimk", 1 - lit, f)
            # the vial: rises at "But", then each level moves to its measured row
            vk = 1 - sp(t, *T["vials"])
            for o in G.vial_group(vials[i]):
                K.set(o, "dimk", max(vk, 1 - lit), f)
            row_ = vial_row(p)
            if founder:
                lvl = VIAL["ledge"] + (row_ - VIAL["ledge"]) * sp(t, *T["bead"])
            else:
                lvl = VIAL["ledge"] + (row_ - VIAL["ledge"]) * sp(t, *T["sink"])
            G.vial_level_k(vials[i], lvl, f, K)
        # the cones: on the founder first, then the three, then the founder again
        for i, bl in beams.items():
            if i == FOUNDER:
                on = found_in * (1 - a) + b * a * 0 + b
                on = max(0.0, min(1.0, found_in * (1 - a) + b))
            else:
                on = a * (1 - b)
            for o in bl:
                K.set(o, "dimk", 1 - on, f)
        G.tank_back_pose(run, root, TANK, TANK, 18.0, f, K)

    END_T = END
    run.words["eaten"] = {"text": "eaten", "anchor": "word_eaten", "face": "mono-semibold", "size_px": 40, "tracking_px": 4, "align": "end",
                          "color": "#e6fbf6", "glow": "#9ff5e6", "glow_alpha": 0.45,
                          "opacity": [[0.0, 1.0], [T["to3"][0], 1.0], [T["to3"][1], 0.6], [T["back"][0], 0.6], [T["back"][1], 1.0], [END_T, 1.0]]}
    run.words["left"] = {"text": "left", "anchor": "word_left", "face": "mono-semibold", "size_px": 40, "tracking_px": 4, "align": "start",
                         "color": "#e6fbf6", "glow": "#9ff5e6", "glow_alpha": 0.45,
                         "opacity": [[0.0, 0.0], [T["left"], 0.0], [T["left"] + 0.3, 1.0], [END_T, 1.0]]}

    frames = G.frame_list(args, cues, "founder")
    lay_sets = ("founder-1", "founder-2", "founder-3")
    ok = True
    for name in lay_sets:
        lay = L.load_layout(name)
        check = [1 + int(round(s * G.FPS)) for s in lay["check_s"]]
        ok = run.layout_check(name, check, 0) and ok
    print("LAYOUT %s" % ("ALL WITHIN TOLERANCE" if ok else "SOME ANCHORS OUT OF TOLERANCE"))
    if args.dry:
        run.write_anchors(sorted(set(frames[:3])))
    else:
        run.write_anchors(range(1, run.N + 1))
    run.render(frames, "founder.blend")


if __name__ == "__main__":
    main()
