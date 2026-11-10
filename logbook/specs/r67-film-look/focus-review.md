# Stills round 4, the focus set as rendered so far: the coordinator's read (2026-11-09, about 23:50)

This note is for the coordinator's ruling and stays out of the cut folder. It covers the takes on worker 9
(`scratch/wt-camera5/scratch/safari/`) for A, F and N, before c8 cuts them.
- **Read:** each take's 12-tile sheet, for motion. The middle frame of 1.3a, 2.3a, 2.4b and 3.1a at full size (960x540). Four
  frames of 2.4b around its cut. F's 4.1a sheet, and c8's tiles of its end frames.
- **Measured:** with `measure.py` (`focus/`).
- **Still rendering:** N2 and V.

| shot | variant | luma | tone | edges | G/B |
|---|---|---|---|---|---|
| 1.3a | A | 65.0 | 69 | 20.5 | 0.88 |
| 1.3a | F | 64.8 | 69 | 23.5 | 0.88 |
| 1.3a | N | 64.7 | 68 | 22.4 | 0.88 |
| 2.3a | A | 65.8 | 61 | 26.1 | 0.87 |
| 2.3a | F | 65.6 | 63 | 33.0 | 0.87 |
| 2.3a | N | 65.6 | 63 | 31.8 | 0.87 |
| 2.4b | A | 60.4 | 66 | 22.6 | 0.84 |
| 2.4b | F | 69.3 | 75 | 32.1 | 0.83 |
| 3.1a | F | 65.9 | 54 | 19.9 | 0.80 |

Every F frame is above the reference's edge floor (16.1), and inside its range of 18 to 37.

## By shot

- **1.3a (a group frame closing on a pale sphere, 0.25 of the width).** Under A, the near brown body, the green leaf and every
  far animal are soft. Under F the rule turns the lens off, since the subject is under 0.4. Everything is then sharp near and
  far: the brown body's texture, the leaf's edge and the small far animals, with the fog dimming the deepest ones. This passes
  D341. The close-in moves smoothly over the 12 tiles, with no jump. One orange body is cut at the right edge.
- **2.3a (a column of leaf pads rising to the surface).** A is soft throughout. F is crisp, and its edges rise from 26 to 33.
  N, the fog from 4 to 14 m, dims the far field slightly and costs about 1 in edges. At this size it changes little that the
  eye sees. N2, the overshoot, is still rendering.
- **2.4b (the birth).**
  - *Take 1:* the parent is about 0.4 to 0.48 of the width, sharp, with its background blurred (a hero shot, allowed under
    D341).
  - *A fault in take 1:* at 7.0 s a cluster of large, blurred orange discs sits beside the parent, under full blur. It reads
    as an artefact, close to the flat discs the owner found dated on 2026-11-02. Whether they are the birth cloud or crumbs
    is not yet known.
  - *Another fault in take 1:* a pink body overlaps the top of the parent and reads as part of it.
  - *Take 2* holds at 3.24 m on the newborn's spot. It opens at 7.7 s with the parent, seen end-on as a disc with a pink stalk,
    cut by the right edge, its birth cloud beside it.
  - *The newborn* appears at about 9 s near the centre, at about 0.1 of the width, with bubbles. The birth can be read, but
    it is small in a busy frame, and bodies are cut at the bottom edge.
  - *The lens* is off in take 2, and the frame is clear near and far.
  - F's birth is far nearer than A's, where the parent held 0.26 of the width at the middle.
- **3.1a (the descent onto a group, DOLLY_GROUP).**
  - *Movement:* it starts level at depth with the group small at the bottom, descends and closes, and ends looking up at the
    group.
  - *The middle frame* is clear everywhere, with the coral, the light shafts and the fog giving depth. It is the best frame of
    the set.
  - *A fault:* a large striped body crosses the bottom-right corner, cut, in tiles 5 and 6.
  - *The subject's size:* it ends at about 0.2 of the width, as asked.
- **4.1a (a founder portrait).**
  - *Under F* the hero starts at 0.48, sharp, with its background blurred.
  - *Mid-take* it stands up end-on and falls to about 0.27, with the background still at full blur, against D341. The focus
    rule decides once per take.
  - *At the action's out point* (69.58 s) the bulb is whole but 0.03 of the width from the left edge. It is cut in the
    handle, which the edit does not use.
  - *The lead room* is behind the body after it turns.
  - *Round 5* tests HERO_ROOM, HERO_FOLLOW, HERO_ZOOM and FOCUS_FOLLOW against this, each alone and all together.

## The cut set (stills-5, 30 stills, 00:00): O, the overshoot fog, and V

These were read at full size from `scratch/film-light/c8/pass13/stills-5/`, measured in `measures-5.txt`, and stacked in
`focus/`.

- **O (the fog from 3 to 10 m) on 1.3a.**
  - The far animals are clearly dimmer and bluer, and the deep field recedes.
  - The near animals stay as crisp as under F.
  - Edges fall from 22.8 under F to 21.7 under N and 20.7 under O, all above the floor.
  - Of the three, O is the one where the fog makes the background disappear, as D341 asks. The cost is a sparser-looking
    crowd.
- **O on 2.3a.** Edges are 32.4 under F, 31.1 under N and 30.0 under O. The same thinning, less visible, since the column is
  near.
- **V (CARD_MOVE 4, RISE 2, PUSH 0.5) on 2.3a, five stills.**
  - The camera rises up through the leaf layer toward the surface. The pads grow and the window's ripples widen. The frame
    reads as plants under a lit surface.
  - Tone climbs from 63 to 77 and edges from 34.0 to 36.8.
  - The move gives the shot the motion D340 asks for where nothing else acts.
  - A fault: still 3 has a near orange-yellow body cut by the left edge.

## Where the focus rule stands

F does what D341 asks on every shot without a hero (1.3a, 2.3a, 3.1a, and 2.4b's second take), and on the hero takes at
their start. Its one fault is the level held through a take whose hero shrinks, which round 5's FOCUS_FOLLOW answers. The
leaning is that FOCUS_RULE joins the look with W and R. The ruling waits for the reviewer's verdicts on stills 5.

Faults to send on once the reviewer has judged:
- 2.4b take 1: the blurred orange discs, and the pink body overlapping the parent.
- 2.4b take 2: its first frame cuts the parent at the right edge.
- 3.1a: the corner crossing.
