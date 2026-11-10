# Stills round 5 (stills-6): the coordinator's read (2026-11-10, about 03:00)

This note is for the coordinator's ruling and stays out of the cut folder, so the film reviewer never reads it.
- **Source:** `scratch/film-light/c8/pass13/stills-6/`: 120 stills, head 9740db49, cut by c8. The head's dials-off render
  is identical to the reference.
- **Read:** every distinct still at its full 960x540, in vertical pairs (`s6/`), against CLAUDE.md's render-check list.
  Twenty stills are byte-identical to others: B = F on 2.4b, Y = F on 3.1a, X = G on 2.1a, U-1 = F-1 on 4.1a, N-1/2 = F-1/2
  on 2.4b. K = J except one still, which differs by render noise. The duplicates were checked with `cmp` and not read again.
- **Also read:** c8's cloud crops and cloud sequences (`look6/`), and the fog trace (`fog5-report.md`).
- **Measured:** `measures-6.txt`, with `measure.py` (film.py's measures at 960).
- **Motion:** the frame-to-frame jump of every raw take on worker 9, in `jumps-6.txt` (`jumps.py`). The largest jump inside any
  take is 1.6x its neighbours (4.1a M, frame 42). The only large jumps are the cuts between two takes: 2.4b at frame 29
  (2.4 to 3.1x) and 6.4a at frame 39 (3.9x). No take pops or snaps.

The reference (rounds 50-57 v3): luma 55 to 68, tone 47 to 63, edges 18 to 37 (floor 16.1), G/B 0.88 to 0.96.

## By shot

- **4.1a, the founder's portrait.**
  - *F:* the hero starts at about 0.45, sharp, with the background blurred. By still 2 it has shrunk to about 0.2 with the
    background still at full blur, against D341. At the end it is whole but tight against the left edge.
  - *M:* the hero is whole with room at the end, but small (0.2 to 0.35) through stills 2 to 4, with the background at full
    blur.
  - *Q:* like M, a little larger.
  - *U:* framed as F. From still 2 the blur eases off as the hero shrinks: the coral, the pale body and the surface are sharp,
    and edges climb from 10.3 to 21.2. FOCUS_FOLLOW does what D341 asks.
  - *Z:* large from still 2, but still 4 cuts the pale body at the right and the end cuts the hero at the left edge. Fails.
  - *T (all four):* the hero holds 0.4 to 0.55 of the width in every still. It is sharp, its maze texture crisp, and whole,
    with room ahead at the end. The background is soft throughout, as D341 allows for a hero at 0.4 or more.
    - *The one tight frame:* still 1, where the hero's left tip sits about 15 px from the edge.
    - *A minor fault:* still 4 cuts a soft orange pair at the top edge.
    - *Its jumps* stay at 1.3x or under.
  - *Edges:* T's measure 9.3 to 13.4, under the floor. The cause is traced: a sharp hero over a full-blur background. The same
    frames under U, with the blur eased, measure 15 to 21.
  - **Verdict: T passes.**
- **2.4b, the birth.**
  - *Take 1 (F, B and N):* the parent is close and sharp. A pink body overlaps its top, as in round 4.
  - *The cloud:* at 20425 to 20426.25 s the birth cloud sits beside the parent. At c8's threefold crop (`2.4b-cloud-F.png`) it
    is about 25 soft peach discs, all one size and one flat tint, barely moving between frames. That is the flat-disc look the
    owner found dated on 2026-11-02. Real spawning shows a milky, uneven plume or a haze of fine bright specks.
  - **The cloud fails, and must not reach the owner as it is.** 6.4a's take 1 shows the same discs, gold above the parent in
    still 3.
  - *The cloud is in the wrong place.* In c8's sequences (`cloud-Fq-seq.png`), it forms beside the parent. The newborn appears
    at 20426.25 s about 1.5 m to the left of it, in empty water. In N's sequence the newborn's spot shows no cloud at all, only
    a few specks at 20426.00, then the whole newborn in one frame, then clear ringed bubbles. So the cloud does not cover the
    birth. It decorates the parent.
  - *Take 2:*
    - F's newborn is small (about 0.1 of the width) at the centre of a busy frame, with a leaf cut at the top left and a body
      cut at the bottom edge.
    - N's newborn fills the frame at 0.97 m, sharp, its disc mouth and stripes clear. It is the strongest frame of the birth.
      Its last still cuts the next body at the right edge.
  - *H:* opens on open water, with no subject in still 1, and its still 2 cuts the parent at the right edge. Fails.
  - *P:* turns take 1's bearing, so the surface leaves the frame and more bodies crowd below. The pink body still overlaps the
    parent. No better than F.
  - **Verdict:** F's take 1 with N's take 2. The newborn's appearance passes only once the cloud forms at the newborn's own spot
    and looks like a spawning plume. Until then 2.4b is not filmed for the owner.
- **2.6a, the colony pull-back.**
  - *C:* loses the anchor by still 2. Stills 2 and 3 cross open water over a crowd. It ends on a near group, with one body cut at
    the top edge and one at the left.
  - *D:* keeps the striped anchor in frame the whole way, and ends wide on a field of green pads with the anchor small and whole
    near the top. Its end's edges (15.4) and tone (36) are low, because the lower two thirds are open water: a wide end, not a
    soft lens, since the lens is off.
  - **Verdict: D passes.**
- **3.1a, the descent.**
  - *F:* the patterned disc stays whole near the middle in all five stills, under shafts and over a deep field of leaves, and the
    surface enters as the look tips up. The end cuts a large white body at the right edge. It reads as a slow push and a tilt up,
    more than as sinking.
  - *J:* the banded animal stays centred and small (about 0.1 of the width), and members are cut at the top and right edges in
    stills 1, 2 and 5. Edges are 11.8 to 15.6, under the floor, because the subject is small and the frame is mostly fine
    specks.
  - **Verdict: F passes on size, edges and light.** The sinking is judged on pass 17's clip, as planned.
- **1.4a, DESCENT_THROUGH (L).**
  - A pink and yellow member passes huge through stills 2 and 3, and is cut at the top in still 4.
  - Stills 4 and 5 are open water with scattered small leaves. The layer reads as specks, not as a canopy, and nothing glows.
  - **Verdict: fails**, as c8 found. The geometry is below.
- **1.3a, GROUP_CARD_SIDE (S).**
  - The group moves left as built.
  - The dark long-snouted swimmer is large in stills 1 to 3. Its red wound cloud is a soft red plume. It reads as a wound and is
    not discs.
  - The swimmer leaves at the top in still 4.
  - The right third still holds bodies at the end (a pink-stalked disc and a banded one), so the card would sit over them. In a
    crowd this dense no third is clear.
  - **Verdict: S passes as built.** The hero leaving at still 4 is an out-point for the edit.
- **2.3a, W.**
  - A slow push-in with a sideways drift onto the blade stack. It is sharp near and far and rich in colour, with the shafts and
    the lit surface in frame.
  - The rise is 0, for the reason c8 found.
  - Edges are 24 to 26.5, and G/B climbs from 0.89 to 1.01.
  - **Verdict: W passes as it is.** The push is the motion D340 asks for. The rise would add to it but is not needed.
- **1.2a and 2.1a, G (and X).**
  - The pull-back ends at 11 to 12 m, and the group is specks.
  - On 1.2a the end cuts the pair at the top edge and a green body in the bottom-left corner.
  - On 2.1a, edges are 9.5 to 12: deep water with no near body.
  - **Verdict: G and X fail.**
- **6.4a, E.**
  - Take 1 holds the parent through the birth, large and cut by the frame's edge in all three stills, with the disc cloud above
    it in still 3.
  - Take 2 holds the child whole, centred, sharp and lit, with the leaves against the surface. It is a good frame.
  - **Verdict: E passes as built.** Take 1's framing is the hero fault that T answers on 4.1a, and the cloud is the 2.4b fault.

## The fog (O, from 3 to 10 m)

c8's trace computes the fog from its own formula, not from pixels. Members 4 to 6 m from the eye keep 97 to 99% of their luma
under O against 100% under 6 to 20 m. 3.1a's members at 4.05 to 5.23 m keep 0.970 to 0.993.

In the stills, no group member at that range is lost or dimmed visibly: 3.1a's disc and J's banded animal are crisp. The
fog's cost falls beyond about 8 m, where G's ends put their groups. That is G's fault, not the fog's. **O stays.**

## 1.4a's geometry, and the choice

The leaf layer runs from 1.01 to 4.95 m deep. Its top is above the dolly's 1.2 m ceiling. At the end, the group is 46° above the
frame's axis, while the window cap allows 23.9°.

The three options:
- **(a) End about 4 m further out under the layer.** The group then sits inside a 24° up-look. It keeps every ruled look, but
  the group is farther away.
- **(b) Lift the window cap for this shot.** That breaks D342's cap, which was set against a pale sheet of the window, for one
  shot.
- **(c) Keep today's 1.4a.** That keeps a shot the reviewer has already failed, on B3 and B4: it never sinks through or looks
  up at the leaves.

**Choice: (a).** Its end puts the eye just under the layer's bottom, not deeper, so the group stands as near as the 24° look
allows. Its test take in pass 17 is judged for a subject: an animal close enough to read, in the left two thirds.

This is inference: the layer here is loose small leaves, and seen from below against the surface they may read as a ceiling
of silhouettes, as 2.3a's V rise did in round 4. Seen across, as in L's stills, they read as specks.

## Correction: the discs are bite crumbs, not a birth cloud

c8 traced it in `scratch/wt-camera5/scratch/logs/theatre-safari-r67ev-24b-6202.log`, and the coordinator checked the line. This
read named the discs a birth cloud after c8's first report, without reading the log. Every passage above that calls them a cloud
is wrong on what they are.
- **2.4b.** The peach discs are the crumbs of a leaf the parent ate at 20424.5 s: "bite crumbs: t 20424.5 eater 28824 … kill,
  prey leaf (27081 part 0), 38 crumbs". They show for 3 s at its mouth.
- **6.4a.** The gold discs are the crumbs of the parent's leaf kill at 8572 s.
- **The film draws a birth as D290's bubbles** (the look's BIRTH_BUBBLES 1, at the owner's count of 4). The bubbles stand at
  the newborn's own spot, 2.08 m from the parent. The placement fault is not real.

Two observations remain:
- **The newborn appears whole in one frame.** Only a bubble or two show over the lead.
- **The crumbs are flat discs of one size and one tint.** That is the look the owner called dated on 2026-11-02. The crumbs'
  look is ruled (D281, D289, D290), so it is the owner's to change.

The plume is withdrawn. Two dials go forward, each off by default:
- the bubbles denser over the lead at the newborn's spot, at the ruled count;
- varied crumbs.

Each goes to the owner as a still pair before it enters the film.

## The reviewer's verdicts (film-stills-6-review.md), and where this read fell short

The reviewer passes no variant. Its light check, B1, fails a take whose luma is under the reference film's mean, 64.52
(`film/reference.json`).
- **The wrong light bar.** This read used the spread of four reference frames, 55 to 68, as its light bar. That is the
  wrong bar for B1. On the right one, T, W, S, E, 3.1a and 2.1a are all dark, at 60 to 64.4. The reviewer's fix lines ask, again
  and again, to lift the light so luma clears about 66.
- **T's middle stills.** This read passed T on size, sharpness and wholeness. The reviewer fails it on light: at 25 and 50 %
  the camera looks level into navy, with no surface in frame. This read had seen that (top fifth 43.7 in still 3) and did not
  weigh it.
- **2.6a.** The reviewer fails D on its drift to the top edge and its flat blue lower half. It fails C only on B1, for having
  no shafts, and its fix keeps C's pull-back. C is the base, not D.
- **What this read caught that the reviewer did not:** the birth cloud's discs, and its place beside the parent rather than
  the newborn's spot.

From here this read uses 64.52 as the light bar, and reads every still's light with its framing.

## For the ruling, once the reviewer's verdicts are in

- **Joining the look (wide):** HERO_ROOM, HERO_FOLLOW, HERO_ZOOM and FOCUS_FOLLOW (T), if the reviewer passes T. They then
  apply to every hero take, 2.4b's and 6.4a's take 1 included, and pass 17's test takes show whether they hold there.
- **Per shot:** COLONY_END with COLONY_TIGHT 6 on 2.6a (D), GROUP_CARD_SIDE on 1.3a (S), W's card dials on 2.3a, and
  BIRTH_FAR_CUT on 6.4a (E).
- **Out:** GROUP_FIT_END, CROWD_WIDE, BIRTH_HOLD, BIRTH_EDGE, DOLLY_EDGE and COLONY_TIGHT 3. BIRTH_CLOUD_FOCUS stays out: it had
  nothing to do here.
- **Built before pass 17 films 2.4b or 6.4a:**
  - the cloud at the newborn's spot, as a spawning plume;
  - 1.4a under (a).
