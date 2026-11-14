"""The parents graphic: parents-1 "The best eaters became parents.", parents-2 "Their children were copies of them,
apart from the occasional small mistake.", parents-3 "And round after round, the creatures got better at eating."
One scene of 360 frames (cues.json: 75 + 117 + 168), after the designer's frames-v3/parents-1..3.png
(design/src/parents.html), the tank's water with the tank far back and soft.

    blender -b --factory-startup --python g_parents.py -- <out dir> <percent> <samples> [frames] [--slot parents-1|2|3] [--t S,S] [--dry]

RULINGS 2: the bodies are Guzzler-team genomes of the sea run's pool, drawn in the design's pale cyan (the teams are
not named at this point of the film). Seven of them stand over tubes of what each ate, the three fullest lit
(pools 21, 23, 26: the three best of the pool, 2,076 / 2,450 / 2,058 J); the other four are pools 7, 9, 11 and 5. Tube
levels are measured joules on the one tank scale (g_common.K_J), so they differ from the design's drawn levels
(.32 .82 .26 .9 .44 .74 .2) where the real eaters do. parents-2's children are copies of their parent's body, and the one
child with one extra segment is a real genome edit grown by Core: pool 23's root node recursiveLimit 2 -> 3 (body
`guz-23x`, nine parts to the parent's six), see scratch/r67-graphics/blender/tools/make_edit_pool.py. parents-3's five
generations are three Guzzlers each from the brackets g0-4, g8-12, g16-20, g32-36 and g37-40 (a bracket with fewer than
three genomes repeats its nearest), their tubes at the bracket's measured mean ownEaten (362.5, 699.9, 1196.5, 1980.5 and
2013.6 J): they rise at every step, from 15% to 86% of a tube, against the design's drawn .30 to .86.

Motion (notes/graphics.md): p-1, cones come down on the three best (0-1.0) and the other four drift back, down and dim
(0.3-1.8); p-2, the others fade out (0-0.4), the parents glide to rows y 300, 590, 880 at x 420 (0-1.0), two children each
swim out (0.25-2.25) trailing a thread of light, and a bloom marks the mutant child's extra segment ("small" 2.43, peak
2.7, gone by 3.6); p-3, a camera track right (0-4.0) along five generations, their tubes filling left to right 0.4 s apart
from 0.6, the newest group's cone coming up on "eating" (3.19). Cut between slots is hard.
"""
import math, os, random, sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import g_layout as L
import g_scene as G
import g_common as C

PLANE = 7.65
ROW = [7, 21, 9, 23, 11, 26, 5]                  # pool indexes, left to right
BEST = [1, 3, 5]                                  # positions in ROW of the three fullest
ROWS_Y = [300, 590, 880]
TUBE1 = {"top": 690, "bot": 930, "w": 50}
TUBE3 = {"top": 640, "bot": 930, "w": 54}
GEN_POOLS = [[0, 1, 3], [6, 7, 6], [10, 11, 10], [19, 20, 19], [21, 22, 23]]
GEN_MEAN = ["g0-4", "g8-12", "g16-20", "g32-36", "g37-40"]
GEN_OP = [0.38, 0.52, 0.68, 0.84, 1.0]
GEN_BODIES = [(-50, -70, 120, 0.8), (55, -40, 115, 0.8), (0, 30, 145, 1.0)]
TANK = [(820, 150, 1120, 380), (820, 120, 1080, 320), (820, 110, 1060, 290)]
T1, T2, T3 = 75, 117, 168
S1, S2, S3 = 0, 75, 192                           # slot frame offsets


def X(i):
    return 210 + i * 250


def row_y(i):
    return 560 + (18 if i % 2 else -10)


def main():
    args = G.parse()
    cues = G.load_cues()
    model, d = C.camera()
    run = G.Run(args, cues, "parents", model)
    rng = random.Random(67)
    K = G.Keyer()
    END = run.N / G.FPS
    p2_t, p3_t = S2 / G.FPS, S3 / G.FPS

    G.water_gradient()
    G.shafts(run, seed=11)
    root, tk = G.tank_back(run, TANK[0], t_back=18.0)
    mats = G.body_material(run)

    # one Guzzler scale for the graphic: the row's median body is 210 px long at the plane
    lengths = sorted(G.body_length(C.key_of(p)) for p in ROW)
    SG = 210.0 * (PLANE / model.f_px) / lengths[len(lengths) // 2]

    def depth_for(size_px):
        return PLANE * 210.0 / size_px

    def make(key, name):
        return G.add_body(run, key, name, "pale", mats, SG, rng)

    row = [make(C.key_of(p), "row%d" % i) for i, p in enumerate(ROW)]
    kids = {}
    for r, bi in enumerate(BEST):
        for k in range(2):
            key = "guz-23x" if (r == 1 and k == 1) else C.key_of(ROW[bi])
            kids[(r, k)] = make(key, "kid%d%d" % (r, k))
    gen = [[make(C.key_of(p), "g%d_%d" % (i, j)) for j, p in enumerate(pools)] for i, pools in enumerate(GEN_POOLS)]

    # tubes: 7 for p1, 5 for p3
    t_tube = PLANE - 0.02
    tubes1 = [G.tube(run, "t1_%d" % i, X(i), TUBE1["top"], TUBE1["bot"], TUBE1["w"], t_tube, 0.0, leaf_rx=0.4 * TUBE1["w"], spacing=0.28 * TUBE1["w"])
              for i in range(7)]
    tubes3 = [G.tube(run, "t3_%d" % i, 250 + 355 * i, TUBE3["top"], TUBE3["bot"], TUBE3["w"], t_tube, 0.0)
              for i in range(5)]

    # cones and the pools of light
    beams1 = [G.beam(run, X(bi), 700, 170, depth=10.0, name="cone1_%d" % bi) for bi in BEST]
    beam3 = G.beam(run, 250 + 355 * 4, 660, 190, depth=10.0, name="cone3")
    pools2 = [G.blob(run, "pool2_%d" % r, 420, ROWS_Y[r], 10.0, 240, 130, "#d8fffb", 1.0, 0.28) for r in range(3)]
    threads2 = []
    for r in range(3):
        for k, (cx, dy) in enumerate(((1180, -62), (1300, 62))):
            mistake = r == 1 and k == 1
            threads2.append(((r, k), G.thread(run, "thread2_%d%d" % (r, k), (420 + 120, ROWS_Y[r]), (cx - (150 if mistake else 95), ROWS_Y[r] + dy), PLANE + 1.0, 0.8)))
    threads3 = [G.thread(run, "thread3_%d" % i, (250 + 355 * (i - 1) + 110, 470), (250 + 355 * i - 110, 470), PLANE + 2.5, 0.35 + 0.12 * i)
                for i in range(1, 5)]

    # the bloom on the mutant child's extra segment: a soft sphere riding the third body unit
    mut = kids[(1, 1)]
    parts = G.S.BODIES["guz-23x"]["parts"]
    import numpy as np
    centre = np.mean([p["pos"] for p in parts], axis=0)
    extra = next(p for p in parts if p["i"] == 6)           # the third consumer unit
    local = G.S.to_blender((np.asarray(extra["pos"], float) - centre)[None, :])[0]
    bloom = G.blob(run, "bloom", 0, 0, PLANE, 60, 60, "#e8fffb", 1.0, 0.55)
    bloom.parent = mut.root
    bloom.location = tuple(local)
    bloom.scale = tuple(60 * model.metres_per_px(depth_for(165)) / SG for _ in range(3))
    bloom.rotation_euler = (0, 0, 0)

    G.snow(run, rng, count=1100)
    G.S.grade()

    # ---- anchors for the layout check
    for i in range(7):
        run.anchor("body_%d" % i, 0, 0, 1, parent=row[i].root)
        run.anchor("tube_top_%d" % i, X(i), TUBE1["top"], t_tube)
        run.anchor("tube_bot_%d" % i, X(i), TUBE1["bot"], t_tube)
    for r in range(3):
        run.anchor("parent_%d" % r, 0, 0, 1, parent=row[BEST[r]].root)
        for k in range(2):
            run.anchor("child_%d%d" % (r, k), 0, 0, 1, parent=kids[(r, k)].root)
    for i in range(5):
        run.anchor("gen_front_%d" % i, 0, 0, 1, parent=gen[i][2].root)
        run.anchor("gen_tube_top_%d" % i, 250 + 355 * i, TUBE3["top"], t_tube)
        run.anchor("gen_tube_bot_%d" % i, 250 + 355 * i, TUBE3["bot"], t_tube)
    run.anchor("tank_p1_tl", TANK[0][0], TANK[0][1], 18.0)
    run.anchor("word_eaten", 960, 1010, t_tube)

    # ---- the motion, baked per frame. Each frame says what it wants shown (placements, fades, dims); what it does not
    # name is hidden or dimmed out, and the Keyer writes only the changes.
    sp = L.span
    all_bodies = row + list(kids.values()) + [x for gg in gen for x in gg]
    groups = {}
    for tb in tubes1 + tubes3:
        groups[tb["glass"].name] = G.tube_group(tb)
    for i_, bl in enumerate(beams1):
        groups[bl[0].name] = bl
    groups[beam3[0].name] = beam3
    groups[pools2[0].name] = pools2[:1]
    groups[pools2[1].name] = pools2[1:2]
    groups[pools2[2].name] = pools2[2:3]
    for _, th in threads2:
        groups[th[0].name] = th
    for th in threads3:
        groups[th[0].name] = th
    groups[bloom.name] = [bloom]
    for f in range(1, run.N + 1):
        t = (f - 1) / G.FPS
        slot = 1 if f <= S2 else 2 if f <= S3 else 3
        u = t - (0 if slot == 1 else p2_t if slot == 2 else p3_t)
        place, fades, dims, gone = {}, {}, {}, {}

        def put(b, pos, yaw, roll, scale):
            place[b.name] = (b, pos, yaw, roll, scale)

        def dim(objs, k):
            dims[objs[0].name] = k

        G.tank_back_pose(run, root, TANK[0], TANK[slot - 1], 18.0, f, K)
        cx = -0.4 * (1 - sp(u, 0.0, 4.0)) if slot == 3 else 0.0     # parents-3: the camera tracks right
        K.set(run.cam, "location", (cx, model.pos[1], model.pos[2]), f)

        if slot == 1:
            k = sp(u, 0.3, 1.8)                    # the four others drift back, down and dim
            for i, b in enumerate(row):
                best = i in BEST
                kk = 0.0 if best else k
                size = 210 - 40 * kk
                put(b, model.to_world(X(i) + 10, row_y(i) + 26 * kk, depth_for(size)), 65, 8 * kk, SG)
                b.swim(t + i * 0.6, f)
                fades[b.name] = 0.6 * kk
                dim(G.tube_group(tubes1[i]), 0.6 * kk)
                G.tube_level_k(tubes1[i], C.frac(C.EATEN[ROW[i]]), f, K)
            for bl in beams1:
                dim(bl, 1 - sp(u, 0.0, 1.0))
        elif slot == 2:
            fo = sp(u, 0.0, 0.4)
            for i, b in enumerate(row):
                if i not in BEST:
                    if fo < 0.999:
                        put(b, model.to_world(X(i) + 10, row_y(i) + 26, depth_for(170)), 65, 8, SG)
                        b.swim(t + i * 0.6, f)
                        fades[b.name] = 0.6; gone[b.name] = fo           # to transparent, not toward the dark water
                    dim(G.tube_group(tubes1[i]), 0.6 + 0.4 * fo)
                else:
                    r = BEST.index(i)
                    k = sp(u, 0.0, 1.0)
                    px, py = L.lerp(X(i) + 10, 420, k), L.lerp(row_y(i), ROWS_Y[r], k)
                    put(b, model.to_world(px, py, depth_for(L.lerp(210, 230, k))), 65, 0, SG)
                    b.swim(t + i * 0.6, f)
                    fades[b.name] = 0.0
                    dim(G.tube_group(tubes1[i]), fo)
                    dim(pools2[r:r + 1], 1 - k)
                    for (rr, kk_), th in threads2:
                        if rr != r:
                            continue
                        cx_, dy = ((1180, -62), (1300, 62))[kk_]
                        kid = kids[(r, kk_)]
                        a = sp(u, 0.25, 2.25)
                        ex = L.lerp(420 + 40, cx_, a)
                        ey = L.lerp(ROWS_Y[r], ROWS_Y[r] + dy, a) - 30 * math.sin(math.pi * a)
                        if u >= 0.25:
                            put(kid, model.to_world(ex, ey, depth_for(165)), 65, 0, SG)
                            kid.swim(t + kk_ * 0.5, f)
                            fades[kid.name] = 0.0; gone[kid.name] = 1 - sp(u, 0.25, 0.5)
                        th[0]["grow"] = 1.0
                        K.set(th[0], "grow", a, f)
                        dim(th, 1 - sp(u, 0.25, 0.5))
                G.tube_level_k(tubes1[i], C.frac(C.EATEN[ROW[i]]), f, K)
            bk = sp(u, 2.43, 2.7) * (1 - sp(u, 2.7, 3.6))        # the bloom: in at "small", peak 2.7, gone by 3.6
            dim([bloom], 1 - bk)
        else:
            for i in range(5):
                x0 = 250 + 355 * i
                depth_i = PLANE * [1.5, 1.25, 1.0, 1.0, 1.0][i]
                comp = depth_i / PLANE
                for j, b in enumerate(gen[i]):
                    dx, dy, size, kk = GEN_BODIES[j]
                    put(b, model.to_world(x0 + dx + 40, 470 + dy, depth_i), 65, 0, SG * comp * size / 210.0)
                    b.swim(t + i * 0.9 + j * 0.4, f)
                    fades[b.name] = 1 - GEN_OP[i] * kk
                tb = tubes3[i]
                dim(G.tube_group(tb), 1 - max(0.5, GEN_OP[i]))
                fill = sp(u, 0.6 + 0.4 * i, 1.4 + 0.4 * i)
                G.tube_level_k(tb, C.frac(C.BRACKET_MEAN[GEN_MEAN[i]]) * fill, f, K)
                if i:
                    th = threads3[i - 1]
                    th[0]["grow"] = 1.0
                    K.set(th[0], "grow", fill, f)
                    dim(th, 1 - sp(u, 0.6 + 0.4 * i, 0.9 + 0.4 * i))
            dim(beam3, 1 - sp(u, 3.19, 3.8))

        for b in all_bodies:
            if b.name in place:
                _, pos, yaw, roll, scale = place[b.name]
                C.kplace(K, b, pos, yaw, roll, scale, f)
                if b.name in fades:
                    K.set(b.root, "fade", fades[b.name], f)
                K.set(b.root, "dimk", gone.get(b.name, 0.0), f)
            else:
                C.kplace(K, b, (0, 0, 0), 0, 0, C.HIDDEN, f)
        for name, objs in groups.items():
            G.set_dim(objs, dims.get(name, 1.0), f, K)

    run.words["eaten"] = {"text": "eaten", "anchor": "word_eaten", "face": "mono-semibold", "size_px": 44, "tracking_px": 4,
                          "color": "#e6fbf6", "glow": "#9ff5e6", "glow_alpha": 0.45,
                          "opacity": [[0.0, 0.75], [p2_t - 0.01, 0.75], [p2_t, 0.0], [p3_t - 0.01, 0.0], [p3_t, 0.75], [END, 0.75]]}

    frames = G.frame_list(args, cues, "parents")
    ok = True
    for name, offset in (("parents-1", S1), ("parents-2", S2), ("parents-3", S3)):
        lay = L.load_layout(name)
        check = [offset + 1 + int(round(s * G.FPS)) for s in lay["check_s"]]
        ok = run.layout_check(name, check, offset) and ok
    print("LAYOUT %s" % ("ALL WITHIN TOLERANCE" if ok else "SOME ANCHORS OUT OF TOLERANCE"))
    if args.dry:
        run.write_anchors(sorted(set(frames[:3])))
    else:
        run.write_anchors(range(1, run.N + 1))
    run.render(frames, "parents.blend")


if __name__ == "__main__":
    main()
