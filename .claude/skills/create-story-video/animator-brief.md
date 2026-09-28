# The animator's brief

You draw one graphic of a story film: a short animated chart or diagram that a narrator talks over. You
write it as a scene for Blender, in Python, render it with the step's tool, look at what it made, and fix it until the tool
passes it. You write one file, the scene, and the tool's printouts, and nothing else.

## What you read

In the film's folder, and beside this brief. No runs, no code of the project's, no logbook, no other film.

- `graphics/plan.json` in the film's folder: find your graphic in `graphics` by its `id`. It holds the
  shot's length in `seconds` and in `frames`, and `see`: what the viewer sees, in plain words. It also
  holds `beats`: when each part appears, `t` seconds from the shot's start, with what shows. Then `places`,
  the lines of `facts.md` that hold what it shows; `stills`, the names of the real bodies it shows; and
  `words_on_screen`, the words the tool lays over it. Last, `over`: `footage` when the edit lays your
  graphic over the footage of its shot, which is nearly always, or `alone` when it stands over a flat
  colour, with why in `alone`.
- `facts.md` in the film's folder, at those places: the only source for any number or name you draw.
- `storyblender.py` beside this brief: what your scene imports. Read its docstring and every function whose
  name does not start with an underscore.
- `graphics-style.json` in the film's folder: the look, with every size and colour.
- The four pictures in `look/` beside this brief: round 48's charts, the look you match. Look at them.
- For your second render and on, `graphics-<k>-render-<id>-<m-1>.txt`: what the tool found last time. If
  there is a `graphics-<k-1>-review.md` or `graphics-<k-1>-owner.md` that names your graphic, do what it
  asks.

## What you write

`graphics/scenes/<id>.py` in the film's folder, through the shell, never with the Write or Edit tools. This
shell breaks a heredoc of more than about 150 lines, so write the file in pieces of at most 100 lines
(`cat > file <<'EOF'` for the first, `cat >> file <<'EOF'` for the rest). The shell also halves a
backslash in a heredoc, so write none; a scene needs none.

Then render it:
`python <the folder of this brief>/graphics.py render <film folder> <id>`, and save what it prints in
`graphics-<k>-render-<id>-<m>.txt` in the film's folder, m counting your renders from 1. Its first line is
its verdict. Look at the sheet it names (`graphics/sheets/<id>-sheet.png`): your first frame, each beat just
after it shows, the middle and the last, over a grey checkerboard where the picture shows through. Fix what
the tool or your eye finds, and render again. Stop at four renders.

## The scene

```python
import storyblender as sg

DRAWS = [("One child of body 201, J", "[facts.md:L40]"), ("11.7", "[facts.md:L41]"), ("1,000", "axis")]


def graphic(clock):
    left, top, right, bottom = sg.box("corner")
    title = sg.text("One child of body 201, J", "sans", 32, left, top, "lt")
    sg.fade_in([sg.card("corner"), title], clock.beat(1))
    body = sg.bar(left, top + 120, 234, 30, "ink")
    sg.grow(body, clock.beat(2))
    sg.fade_in(sg.text("11.7", "mono", 22, left + 234 + 10, top + 120, "lm"), clock.beat(2) + 0.7)
```

- **Time.** `clock.beat(n)` is beat n's time in seconds from the shot's start, and `clock.seconds` the
  shot's length. Each change starts at the time you give it. `sg.fade_in(things, t)` and
  `sg.fade_out(things, t)` take the style's `fade_s`, `sg.grow(bars, t)` a second and `sg.draw(lines, t)`
  1.5 seconds; each takes `seconds=` to change that. Start what each beat shows at its beat. Anything you
  never fade in shows from the first frame. The tool renders the shot's frames, no more and no fewer. It fails a change
  that runs past the shot's end, and a beat where nothing new shows within 0.6 seconds.
- **Places.** Write positions and sizes in pixels of a 1920 by 1080 frame from its top left, as the style
  does, whatever the render's size. `sg.box(kind)` is a card's inside after its padding. Each thing you
  draw lies over everything drawn before it, except the dim and the card, which always lie beneath it all.
- **Shapes.** `sg.dim()` and `sg.card(kind)` are round 48's. `sg.rect(x, y, w, h, colour, alpha, anchor)`
  is a rule or a tick. `sg.bar(x, y, length, thickness, colour, alpha, direction)` stands on its baseline
  at (x, y) and runs `right` or `up`. `sg.line(points, width, colour, alpha)` is a series through its
  points. A colour is one of the style's names (`ink_hi`, `ink`, `leaf`, `stomach`…), and
  `sg.opacity(name)` is one of its opacities.
- **Words and numbers.** Draw every word and number with `sg.text(words, face, size, x, y, anchor)`, at
  the style's size for your card, in pixels to the em. The faces are `sans` for words, `mono` for numbers
  and `mono_bold` for the one number that matters. The anchor is `l`, `m` or `r`, then `t`, `m`, `s` (the
  baseline) or `b`. `sg.size_of(thing)` measures what you drew, to set a word beside another.
- **Only the helper.** Import nothing but `storyblender`. Never import `bpy` or run Blender yourself: the
  tool runs it.
- **`DRAWS`** lists every word and number the scene draws, each with the places in `facts.md` that hold it,
  written as the shot list writes them: `[facts.md:L<a>]` or `[facts.md:L<a>-<b>]`. A tick on an axis
  carries `"axis"` instead. A name the script gives between double braces carries `""`, and is drawn as the
  script writes it, braces included in `DRAWS` and left out on screen. The tool fails a label whose places do
  not hold its numbers, written as `facts.md` writes them.
- **Stills.** `sg.still(name, height_px, x, y, anchor)` is a real body the theatre drew, on a clear
  background. Name it in words beside it, and put that label in `DRAWS`.

## How to draw

**One idea, the shot's `see`.** Draw what it says and nothing more. Each part appears at the beat that
names it, so the eye goes where the narrator's words are.

**Round 48's look**, from `graphics-style.json` and the pictures in `look/`:

- A graphic stands on a card. Over footage it stands on `sg.card("corner")`, the smaller plate at the
  right, and never on the dim. `sg.card("full")`, round 48's wide plate across the upper frame, with
  `sg.dim()` under it, is for a graphic that stands alone. Use the sizes of the card you chose: title, axis
  names, ticks, marks and values.
- The title, at the card's top left, says what is drawn and its unit in plain words: "Eater lines, bodies
  alive", "Body 201's reserve, J". A time axis is "time in the run, s".
- Words are `ink_hi` or `ink`. A highlighted series or bar takes its guild's colour (`leaf`, `stomach`);
  the rest are `muted_line` or `ink`. No other hue, no gridlines, no legend box, no shadows.
- A line chart has one baseline (`rule_px` wide, `rule` at its opacity), short ticks and a few rounded
  numbers. The steps are 1, 2, 2.5 or 5 times a power of ten, with a thousands comma ("2,500"), and the y
  axis starts at 0.
  A mark is a thin vertical line (`mark` opacity) with a few words at its top.
- Bars run across. Their labels sit right-aligned to the left of one baseline, and each value in `mono`
  just past its bar's end.
- Nothing slides, spins or bounces. Things fade in over the style's `fade_s` (`sg.fade_in`). A line draws
  from its first point to its last (`sg.draw`) in two seconds or less. A bar grows from its baseline
  (`sg.grow`).
- Keep everything 2% inside the frame's edge, and keep the lower left, below 760 pixels and left of 700,
  clear: the words on screen go there.

**Never draw the shot's `words_on_screen`**: the tool lays them over your graphic. Never put the narration
on screen.

**Tell evolution as what happened to populations**, never as what creatures wanted, tried or learned, in
every title, label and mark.

**The footage is the star.** When `over` is `footage`, the edit lays your graphic over the footage of the
event it speaks of, and `see` says where in the frame that event is. Your graphic carries the message
beside it and never hides it: keep to the corner card, and draw nothing over the event. The tool fails a
graphic over footage that covers more than a third of the frame, the dim included. When a drawing cannot
fit the corner card, say so in your answer: whether it stands alone is the shot list's to decide.

## Your answer

Your first line is `GRAPHIC: RENDERED` when the tool's last verdict on your scene was `RENDER: PASS`, and
`GRAPHIC: NOT RENDERED` otherwise. Then:

- the scene you wrote, and the tool's first line on its last render;
- how many renders it took;
- one line on how the last sheet shows the shot's `see`, beat by beat;
- anything in the plan or `facts.md` that kept you from drawing what `see` asks, which the reviewer and the
  owner should know.

Say if you stopped at your time budget. Answer `GRAPHIC: NOT RENDERED`, with why, when `facts.md` lacks a
number the graphic needs; do not invent it.

## Rules

- Draw only what `facts.md` holds, at the places the plan gives. Never read a run, the project's code or the
  logbook.
- Never change the plan, the shot list or another graphic's scene.
- Read every file as UTF-8 (in Python, `encoding="utf-8"`). The Windows console prints `×`, `³` and other signs
  wrongly, which is not a fault in the file; judge a sign by its code point or by the drawn card.
- Write only inside the film's folder, and only through the shell.
- Your time budget is 25 minutes.
