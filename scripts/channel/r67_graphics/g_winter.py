"""Through the winter: winter-1 "It killed most of the Nibblers." and winter-2 "Of the other Guzzlers, nearly nine in every ten
died. The taleplums lost only one in every six." One scene of 513 frames: winter-1 (beat 9, 4.70-14.70, 300 frames) then winter-2
(beat 9, 20.90-28.00, 213 frames), after the designer's frames-v5/winter-1..3.png (design/src/winter.html).

    blender -b --factory-startup --python g_winter.py -- <out dir> <percent> <samples> [frames] [--slot winter-1|winter-2] [--t S,S] [--dry]

Three groups in cold water: 10 Nibblers (x 330), 10 other Guzzlers (x 960, the label reads "other Guzzlers") and 6 taleplums
(x 1590, the founder's pale maze with the pink tip). Bodies are the real run's: nib-N (pools 28-72), guz-N (the tank pool's
Guzzlers) and the six sea taleplums tp-ID. A body the winter takes (`die`): it turns over, loses its colour, sinks and leaves a
faint trail of four dots; 1.3 s each, 0.1 s apart, started at the words.

DEATH COUNTS, from the record behind the narration (scratch/story-video/round-67-202611061206/facts.md L848-853, `reads/q6-check.txt`
"15000 to 18000"; scratch/r67-salvage/script/fact-check-1.md row "Taleplums lost 1 in 6; the rest of the team lost 8 in 10"):
from 15,000 to 18,000 s the thrifty lines (the Nibblers' team) fell 370 -> 100, -73.0 %; the shaped animals outside 13929's
lineage (the other Guzzlers) 51 -> 7, -86.3 %; 13929's lineage (the taleplums) 69 -> 58, -15.9 %. Ten bodies to a group, so:
  - Nibblers: 7 of 10 die (73.0 %). The notes and the design drew 8 of 10 (80 %). THE SOURCE WINS: 7.
  - other Guzzlers: 9 of 10 die (86.3 % rounds to 9; the narration says "nearly nine in every ten" and the fact check suggests
    that wording). Matches the design.
  - taleplums: 1 of 6 die (15.9 % = 0.95 of 6). Matches the design.
These are net counts of animals alive at two reads, not a census of deaths: the bodies shown are a sample, ten to a group, drawn to
the record's shares. The picture is to those shares, not a census.

Motion (notes/graphics.md, with the cues of cues.json, scene clock): the seven Nibblers die from "killed" (0.42 s), 0.1 s apart,
reached 2.5 s; the nine other Guzzlers from "Of the other Guzzlers" (scene 10.225, winter-2 0.22 s) 0.1 s apart; the one taleplum
at "lost only one" (scene 14.57). The dead Nibblers stay where they sank across the cut (winter-2 opens with them already gone).
The light is colder and the snow doubled. Layout anchors are computed from the design's scatter (g_sealay.py), never measured off
the PNG; the live bodies stand where each will die from, which the design's own frames do not (see g_sealay.py).
"""
import math, os, random, sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import g_layout as L
import g_scene as G
import g_common as C
import g_sealay as SL

PLANE = 7.65
DIE_S, GAP_S = 1.3, 0.1
NIB_KEYS_N = 40
TP_KEYS = ["tp-42605", "tp-74039", "tp-118251", "tp-166382", "tp-179291", "tp-131141"]
GUZ_POOLS = [23, 21, 26, 19, 20, 22, 25, 27, 24, 18, 17, 16, 14, 15]


def main():
    args = G.parse()
    cues = G.load_cues()
    model, d = C.camera()
    run = G.Run(args, cues, "winter", model)
    rng = random.Random(47)
    K = G.Keyer()
    END = run.N / G.FPS
    cu = cues["cues"]

    G.water_gradient(2.0 ** -G.EXPOSURE * 0.75)          # colder and darker below than the summer's water
    G.shafts(run, seed=47)
    mats = G.body_material(run)
    nib_keys = sorted(k for k in G.S.BODIES if k.startswith("nib-"))
    m = model.metres_per_px(PLANE)
    sp = SL.winter_bodies()
    counts = {"nibbler": 0, "guzzler": 0, "shell": 0}
    items = []
    for n_, s in enumerate(sp):
        c = counts[s["kind"]]; counts[s["kind"]] += 1
        if s["kind"] == "shell":
            key, team = TP_KEYS[c], "taleplum"
        elif s["kind"] == "guzzler":
            key, team = "guz-%d" % GUZ_POOLS[c % len(GUZ_POOLS)], "guzzler"
        else:
            key, team = nib_keys[(c * 5) % len(nib_keys)], "nibbler"
        name = "w%d" % n_
        b = G.add_body(run, key, name, team, mats, 1.0, rng)
        trail = []
        if s["dies"]:
            for q in range(1, 5):
                t_ = q / 5
                trail.append(G.blob(run, "%s-trail%d" % (name, q), s["x"] + (4 if q % 2 else -4), s["y"] + (s["dy"] - s["y"]) * 0 + 0,
                                    PLANE, 3 - t_ * 1.6, 3 - t_ * 1.6, "#cfe6ea", 1.0, 0.32 * (1 - t_)))
        items.append(dict(s, body=b, key=key, scale=s["size"] * m / b.length, name=name, trail=trail))

    G.snow(run, rng, count=2200)
    G.S.grade()

    # who dies when, by group: the cue and the order within the group
    START = {"Nibblers": cu["killed"]["scene_t"], "other Guzzlers": cu["other_guzzlers"]["scene_t"],
             "taleplums": cu["lost_only_one"]["scene_t"]}
    order = {}
    for it in items:
        if it["dies"]:
            order.setdefault(it["group"], []).append(it)
    t_die = {}
    for g_, lst in order.items():
        for q, it in enumerate(lst):
            t_die[it["name"]] = START[g_] + GAP_S * q

    def place(b, px, py, size, roll, sc0, flip):
        C.kplace(K, b, model.to_world(px, py, PLANE), -65 if flip else 65, roll, sc0 * size / it_size[b.name], f_cur[0])

    it_size = {it["name"]: it["size"] for it in items}
    f_cur = [1]

    for it in items:
        run.anchor(it["name"], 0, 0, 1, parent=it["body"].root)
    for G_ in SL.GROUPS:
        run.anchor("label_" + G_["name"], G_["x"], 1025, PLANE)
        run.words["label_" + G_["name"]] = {"text": G_["name"], "anchor": "label_" + G_["name"], "face": "sans", "size_px": 56,
                                           "tracking_px": 0, "color": "#e6fbf6", "glow": "#9ff5e6", "glow_alpha": 0.45,
                                           "opacity": [[0.0, 1.0], [END, 1.0]]}

    for f in range(1, run.N + 1):
        t = (f - 1) / G.FPS
        f_cur[0] = f
        for it in items:
            b = it["body"]
            if it["name"] in t_die:
                L_ = lambda bb, px, py, size, roll, it=it: place(bb, px, py, size, roll, it["scale"], it["flip"])
                G.die_keys(K, b, it["trail"], t_die[it["name"]], DIE_S, f, t, it["x"], it["y"], it["dx"], it["dy"], it["size"],
                           it["size"] * 0.9, it["flip"], it["rot"], L_)
                if t < t_die[it["name"]] + 0.2:
                    for o in it["trail"]:
                        K.set(o, "dimk", 1.0, f)
                else:
                    pass
                for q, o in enumerate(it["trail"], 1):
                    tt = q / 5
                    # the trail dots lie on the line the body sank along, above where it ended
                    K.set(o, "location", model.to_world(it["dx"] + (4 if q % 2 else -4), it["dy"] - (it["dy"] - it["y"]) * tt, PLANE), f)
            else:
                C.kplace(K, b, model.to_world(it["x"], it["y"], PLANE), -65 if it["flip"] else 65, it["rot"], it["scale"], f)
                K.set(b.root, "grey", 0.0, f)
                K.set(b.root, "fade", 0.0, f)
            b.swim(t + 0.41 * it["i"], f, 0.5 if it["name"] in t_die and t >= t_die[it["name"]] else 1.0)

    frames = G.frame_list(args, cues, "winter")
    ok = True
    for name, off in (("winter-1", 0), ("winter-2", 300), ("winter-3", 300)):
        lay = L.load_layout(name)
        check = [off + 1 + int(round(s * G.FPS)) for s in lay["check_s"]]
        ok = run.layout_check(name, check, off) and ok
    print("LAYOUT %s" % ("ALL WITHIN TOLERANCE" if ok else "SOME ANCHORS OUT OF TOLERANCE"))
    if args.dry:
        run.write_anchors(sorted(set(frames[:3])))
    else:
        run.write_anchors(range(1, run.N + 1))
    run.render(frames, "winter.blend")


if __name__ == "__main__":
    main()
