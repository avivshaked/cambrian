"""The tank graphic (the pilot): tank-1, "But before the sea, there was a tank", and tank-2, "how much did you eat?",
as one continuous scene of 716 frames (cues.json: tank-1 344, tank-2 372). After the designer's frames-v2/tank-1.png
and tank-2.png (design/src/tank2.html), from the round's real bodies (bodies-r67.json).

    blender -b --factory-startup --python g_tank.py -- <out dir> <percent> <samples> [frames] [--slot tank-1|tank-2] [--t S,S] [--dry]

What is built, by pixel (1920x1080 design frame):
  * the front face of a glass box (660,330)-(1260,830), open at the top, its back face offset up and right as drawn
    (the camera is square to the front face; a long lens (85 mm) with a lens shift puts the vanishing point up and
    right of the frame, so the back face sits about (+120, -84) px off, and a few pixels smaller, since a
    perspective box shrinks with distance and the drawn one does not);
  * the measure, a glass tube at x 1327 from y 360 to 830, empty in tank-1, filling to 51% in tank-2;
  * the founder (the sea's body 13929, a pool-2 Guzzler) in the tank at (1000,600) / (1010,610), and the queue
    outside at (520,640), (320,596), (150,560), (20,530): pool 23, pool 8, a Nibbler (pool 50) and pool 0, each
    at the depth that gives the drawn length at the Guzzlers' one scale;
  * five leaves drifting inside, a stream of leaves out of the open top into the tube, the word "eaten" (drawn after
    the render by g_words.py at the anchor `word_eaten`, its opacity in anchors.json's "words").

Scene clock: seconds from the first frame of tank-1; tank-2 starts at frame 344 (11.467 s). The motion follows
notes/graphics.md of the build plan: the previous occupant rises out of the open top (0-1.9), the founder swims in over
the rim from the head of the queue (1.0-2.6) while the queue moves up one place (0.1 s apart), reached at 3.0, then a
1.5% push-in; in tank-2 the founder takes the leaf at its mouth (0.3), a leaf leaves the open top every 0.25 s for the
tube, and the tube fills to the founder's 408.8 by 7.4 s ("eat?"). *eaten* rises from 55% to full over 0.3-0.9.

Measured values: the founder's 408.8 (pool/index.tsv, ownEaten). The tube's scale is drawn: its capacity is set so
408.8 reads as the design's 51% (a capacity of 800); the founder graphic's tubes are on their own scale, and this
tube's is not that one.
"""
import math, os, random, sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import g_layout as L
import g_scene as G

# ---------------------------------------------------------------- the design's numbers
FRONT = (660, 330, 1260, 830)
BACK_OFFSET = (120, -84)
TUBE = {"x": 1327, "top": 360, "bot": 830, "w": 54}
LENS, FSTOP = 85.0, 1.4
M_PER_PX = 0.002                    # metres per design pixel on the front plane
DEPTH_M = 0.30                      # the box's depth, away from the camera
FOUNDER_EATEN, TUBE_CAPACITY = 408.8, 800.0
FOUNDER_SIZE_PX = 340.0             # the design's drawn length of the creature in the tank
QUEUE = [   # (key, name, team, x, y, drawn length, fade)
    ("guz-23", "q1", "bone", 520, 640, 230, 0.30),
    ("guz-8", "q2", "yellow", 320, 596, 200, 0.48),
    ("nib-50", "q3", "nibbler", 150, 560, 160, 0.62),
    ("guz-0", "q4", "brown", 20, 530, 140, 0.75),
]
BEFORE = (-110, 500, 120)           # where the last in the queue stood before it moved up
FOOD = [(810, 470, 34, -12), (1110, 420, 28, 10), (1180, 640, 30, -6), (900, 720, 26, 14), (1020, 330, 24, 4)]
FOOD_2 = {2: (1180, 660, 30, -6), 3: (900, 740, 26, 14)}   # the two that stay, as tank-2 draws them
MOUTH_LEAF = (860, 545, 30, -24)
STREAM = ((885, 500), (1150, 150), (TUBE["x"], TUBE["top"] + 10))
STREAM_EVERY, STREAM_DUR, STREAM_FROM, POOL = 0.25, 2.2, 1.2, 10
T1_FRAMES, T2_FRAMES = 344, 372
PUSH = 0.015                         # the plan says about 3%; at 3% the far queue leaves the 19 px layout tolerance


def camera_model():
    """The camera: a long lens square to the front face. The principal point is where the lines running away from the
    camera meet; a box of depth D at distance d shrinks its back face by s = d / (d + D) about that point, so the
    drawn offset (120, -84) of the back face's centre fixes it: pp = front centre + offset / (1 - s)."""
    f_px = LENS / L.SENSOR_H * L.H
    d = M_PER_PX * f_px
    s = d / (d + DEPTH_M)
    fc = ((FRONT[0] + FRONT[2]) / 2, (FRONT[1] + FRONT[3]) / 2)
    pp = (fc[0] + BACK_OFFSET[0] / (1 - s), fc[1] + BACK_OFFSET[1] / (1 - s))
    # the world origin is the point the principal point sees at the front plane
    return L.Camera(LENS, (0.0, -d, 0.0), pp, FSTOP, focus=d + DEPTH_M / 2), d


def lerp3(a, b, k):
    return tuple(L.lerp(x, y, k) for x, y in zip(a, b))


def main():
    args = G.parse()
    cues = G.load_cues()
    model, d = camera_model()
    run = G.Run(args, cues, "tank", model)
    rng = random.Random(67)
    t_front, t_in = d, d + DEPTH_M / 2
    T2 = T1_FRAMES / G.FPS
    END = run.N / G.FPS

    # ---- the water, the light, the glass
    G.water_gradient()
    G.shafts(run, seed=11)
    tk = G.tank(run, FRONT, DEPTH_M, t_front)
    cf = G.caustic_floor(run, tk["corners"])
    tb = G.tube(run, "tube", TUBE["x"], TUBE["top"], TUBE["bot"], TUBE["w"], t_front - 0.02)
    G.set_tube_level(tb, 0.0)
    mat = G.body_material(run)

    # ---- bodies at one Guzzler scale: the founder's drawn length is the unit
    founder_len = G.body_length("founder")
    SG = FOUNDER_SIZE_PX * (t_in / model.f_px) / founder_len
    founder = G.add_body(run, "founder", "founder", "taleplum", mat, SG, rng)
    prev = G.add_body(run, "guz-10", "prev", "guzzler", mat, SG, rng)
    queue = []
    for key, name, team, x, y, size, fade in QUEUE:
        b = G.add_body(run, key, name, team, mat, SG, rng)
        b.t = b.length * SG * model.f_px / size            # the depth at which it is `size` px long
        b.at, b.fade = (x, y), fade
        queue.append(b)
    # pre-move places: each member stood one place further back (the head's place was the founder's)
    places = [(q.at[0], q.at[1], q.t) for q in queue]
    start = [places[i + 1] if i + 1 < len(places) else (BEFORE[0], BEFORE[1], places[-1][2] * 1.12) for i in range(len(places))]
    start_fade = [queue[i + 1].fade if i + 1 < len(queue) else 0.8 for i in range(len(queue))]
    founder_start = places[0]
    founder_end = (1000, 600, t_in)
    founder_end2 = (1010, 610, t_in)

    # ---- leaves: five drifting in the tank, one at the mouth is the first of them, and the stream
    food = [G.leaf(run, "food%d" % i, x, y, t_in, rx, tilt) for i, (x, y, rx, tilt) in enumerate(FOOD)]
    stream = [G.leaf(run, "stream%d" % i, 0, 0, t_in, 10, 0) for i in range(POOL)]

    # ---- anchors, one per layout point
    c = tk["corners"]
    for name, key in (("front_tl", "FTL"), ("front_tr", "FTR"), ("front_bl", "FBL"), ("front_br", "FBR"),
                      ("back_tl", "BTL"), ("back_tr", "BTR"), ("back_br", "BBR")):
        e = run.anchor(name, 0, 0, 1)
        e.location = c[key]
    run.anchor("tube_top", TUBE["x"], TUBE["top"], t_front - 0.02)
    run.anchor("tube_bottom", TUBE["x"], TUBE["bot"], t_front - 0.02)
    run.anchor("word_eaten", TUBE["x"], 900, t_front - 0.02)
    run.anchor("founder", 0, 0, 1, parent=founder.root)
    for i, b in enumerate(queue):
        run.anchor("queue_%d" % (i + 1), 0, 0, 1, parent=b.root)
    run.anchor("tube_level", 0, 0, 1, parent=tb["meniscus"])
    run.anchor("leaf_mouth", 0, 0, 1, parent=food[0])

    # near the lens, out of focus: a leaf drifting past, and a cyan haze (the design's two foreground ellipses)
    G.blob(run, "near1", 1760, 960, 2.5, 130, 42, "#3f9e60", 0.4, 0.55, rot_deg=14)
    G.blob(run, "near2", 120, 160, 2.5, 100, 34, "#4fc9b8", 0.4, 0.3)
    G.snow(run, rng, count=1100)
    G.S.grade()

    # ---- the motion, baked per frame
    cam_focus = t_in
    run.focus_at(cam_focus)
    level_full = FOUNDER_EATEN / TUBE_CAPACITY
    sp = L.span
    for f in range(1, run.N + 1):
        t = (f - 1) / G.FPS
        u = t - T2                                   # seconds into tank-2 (negative in tank-1)
        # camera: a 1.5% push-in after the reach at 3.0, held
        run.apply_zoom(1.0 + PUSH * sp(t, 3.0, T2 - 0.5), frame=f)
        run.focus_at(cam_focus, frame=f)
        cf["w"] = t * 0.35
        cf.keyframe_insert('["w"]', frame=f)

        # the previous occupant rises out of the open top over 0-1.9 and is gone
        k = sp(t, 0.0, 1.9)
        # it leaves the frame through the top (row -260, well past its own half height) before the cut to a hidden scale;
        # fade mixes toward the DARK tank water, so above the glass it must stay slight or it reads as a black silhouette
        px, py = 1000, L.lerp(600, -260, k)
        prev.place(model.to_world(px, py, t_in), -65, roll_deg=-4, scale=(1e-4 if k >= 0.999 else None), frame=f)
        prev.swim(t, f, 1.0)
        prev.state(f, fade=0.25 * k)

        # the founder: at the head of the queue, over the rim into the tank (1.0-2.6), turning to face the leaf
        k = sp(t, 1.0, 2.6)
        a, b = founder_start, founder_end
        mid = (800, 190, (a[2] + b[2]) / 2)             # the arc passes over the open top
        ppx, ppy = L.quad_bezier((a[0], a[1]), (mid[0], mid[1]), (b[0], b[1]), k)
        tt = L.lerp(a[2], b[2], k)
        px, py, tt = ppx, ppy, tt
        if u > 0:                                       # tank-2: a small step to the drawn place, and the roll
            k2 = sp(u, 0.0, 0.3)
            px, py = L.lerp(px, founder_end2[0], k2), L.lerp(py, founder_end2[1], k2)
        yaw = L.lerp(65.0, -65.0, sp(t, 1.7, 2.8))
        roll = L.lerp(-4.0, 6.0, sp(u, 0.0, 0.3))
        bob = 0.012 * math.sin(2 * math.pi * 0.35 * t)
        pos = model.to_world(px, py, tt)
        founder.place((pos[0], pos[1], pos[2] + bob), yaw, roll_deg=roll, frame=f)
        founder.swim(t, f, 1.0)
        founder.state(f, fade=L.lerp(queue[0].fade, 0.0, k))

        # the queue moves up one place, 0.1 s apart from the head
        for i, qb in enumerate(queue):
            k = sp(t, 1.1 + 0.1 * i, 2.7 + 0.1 * i)
            p = lerp3(start[i], places[i], k)
            pos = model.to_world(p[0], p[1], p[2])
            bob = 0.01 * math.sin(2 * math.pi * 0.3 * t + i)
            qb.place((pos[0], pos[1], pos[2] + bob), 65, roll_deg=3 * (1 if i % 2 else -1), frame=f)
            qb.swim(t + i * 0.7, f, 0.8)
            fd = L.lerp(start_fade[i], qb.fade, k)
            qb.state(f, fade=fd, grey=0.45 * fd)

        # the food: five leaves drift in the tank. In tank-2 the first goes to the founder's mouth and is eaten (0.3-0.7),
        # the second and the fifth are eaten by 0.6, and two stay, at the places tank-2 draws.
        for i, lf in enumerate(food):
            x, y, rx, tilt = FOOD[i]
            sway = 6 * math.sin(0.8 * t + i * 1.7)
            x += sway; y += 4 * math.cos(0.6 * t + i)
            size = rx
            if i == 0:
                k = sp(u, 0.0, 0.3)
                x, y = L.lerp(x, MOUTH_LEAF[0], k), L.lerp(y, MOUTH_LEAF[1], k)
                tilt = L.lerp(tilt, MOUTH_LEAF[3], k)
                size = L.lerp(rx, MOUTH_LEAF[2], k) * (1 - sp(u, 0.3, 0.7))
            elif i in FOOD_2:
                k = sp(u, 0.0, 0.5)
                x, y = L.lerp(x, FOOD_2[i][0] + sway, k), L.lerp(y, FOOD_2[i][1], k)
            else:
                size = rx * (1 - sp(u, 0.0, 0.6))
            G.place_leaf(run, lf, x, y, t_in, max(size, 0.01), tilt, frame=f)

        # the stream out of the open top into the tube's mouth, one leaf every 0.25 s from 1.2 s into tank-2
        for j, lf in enumerate(stream):
            n = math.floor((u - STREAM_FROM) / STREAM_EVERY)
            # the newest launch index congruent to j (mod POOL) that is not in the future
            kk = n - ((n - j) % POOL) if n >= 0 else -1
            age = (u - STREAM_FROM - kk * STREAM_EVERY) if kk >= 0 else -1
            if kk < 0 or age > STREAM_DUR or age < 0:
                G.place_leaf(run, lf, 0, 0, t_in, 0.01, 0, frame=f)
                continue
            p = age / STREAM_DUR
            x, y = L.quad_bezier(STREAM[0], STREAM[1], STREAM[2], p)
            size = (19 - 4 * p) * (1 - sp(p, 0.92, 1.0))
            G.place_leaf(run, lf, x, y, L.lerp(t_in, t_front - 0.02, sp(p, 0.6, 1.0)), max(size, 0.01), -30 + (kk % 9) * 38, frame=f)

        # the measure fills to the founder's 408.8 by 7.4 s ("eat?")
        G.set_tube_level(tb, level_full * sp(u, 2.0, 7.4), frame=f)

    # ---- words: the opacity of "eaten" in scene seconds (the design's 55% in tank-1, full in tank-2)
    run.words["eaten"] = {"text": "eaten", "anchor": "word_eaten", "face": "mono-semibold", "size_px": 44, "tracking_px": 4,
                          "color": "#e6fbf6", "glow": "#9ff5e6", "glow_alpha": 0.45,
                          "opacity": [[0.0, 0.55], [T2 + 0.3, 0.55], [T2 + 0.9, 1.0], [END, 1.0]]}

    frames = G.frame_list(args, cues, "tank")
    # the layout check, at each design frame's check frames (slot-relative seconds from layout/<frame>.json)
    ok = True
    for name, offset in (("tank-1", 0), ("tank-2", T1_FRAMES)):
        lay = L.load_layout(name)
        check = [offset + 1 + int(round(s * G.FPS)) for s in lay["check_s"]]
        ok = run.layout_check(name, check, offset) and ok
    print("LAYOUT %s" % ("ALL WITHIN TOLERANCE" if ok else "SOME ANCHORS OUT OF TOLERANCE"))
    if args.dry:
        run.write_anchors(sorted(set(frames[:3] + [offset + 1 + int(round(s * G.FPS)) for offset in (0, T1_FRAMES) for s in (3.5, 3.0)])))
    else:
        run.write_anchors(range(1, run.N + 1))
    run.render(frames, "tank.blend")


if __name__ == "__main__":
    main()
