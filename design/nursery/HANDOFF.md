# The nursery room: design handoff

Second pass, 2026-10-02. The five screens are carried in direction A, "deep ocean", which the
owner chose from three, finding direction A the best but possibly slightly over the top. It is
toned down here. After the owner's note on contrast it was also darkened, the owner having suggested
darkening the cloudish background. The pass follows `BRIEF.md` and the notes relayed by session
evolution-simulator-a8.

Every number on every screen is invented. Real files were read only to learn their shapes, and no
real run's numbers are in these files.

## The current files

| File | What it is |
|---|---|
| `1-registry.html` | Every experiment, numbered and append-only, with its state, a sparkline of its score and its recipe hash; the activity feed; the selected entry with the owner's three buttons |
| `2-recipe.html` | A draft recipe an agent wrote, in the real recipe format, read against its resolved base, with two refused fields and the launch panel |
| `3-run.html` | One run in its exam against a control: the stats band, the search hero, the exam table, and the energy, reward-parts, leader, brains and activity panels |
| `4-tank.html` | One exam episode swum twice, own brain beside a control, each view following body 1 with a whole-tank map in its corner; the brain live; a timeline |
| `5-genome.html` | The brain as the hero graph, the body plan, the genome graph, the lineage and what changed from the base parent |
| `nursery.css` | The one stylesheet: tokens, panels, type, status marks, chart classes |
| `render.ps1` | Renders each screen to `shots/` with headless Edge, at any size and scale |
| `contrast.py`, `contrast-probe.html` | The contrast check: every text against what is behind it, and the tank's brightness ranking |
| `directions/run.html` | The three directions as first shown, kept as the record of the choice (`?dir=a`, `b` or `c`) |
| `shots/` | `<screen>.png` at 1920 by 1080, `<screen>-<w>x<h>.png` at the other sizes, `<screen>-3840-at150.png` for 4K at 150 % |

The fonts are the theatre's IBM Plex copies, referenced by relative path from
`unity/Assets/Theatre/UI/Fonts/` (SIL OFL). Nothing loads from the internet.

## How to check a change

Render, then check with numbers rather than by eye:

```
pwsh -File design/nursery/render.ps1                       # 1920 by 1080
pwsh -File design/nursery/render.ps1 -Width 1366 -Height 768
pwsh -File design/nursery/render.ps1 -Width 2560 -Height 1440 -Scale 1.5   # 4K at 150 %
python design/nursery/contrast.py --size 1366x768
python design/nursery/contrast.py --tank
```

`contrast.py` loads each screen in a frame at the render's size and reads every piece of text's
colour, opacity and box. It reads the background behind each box from the render and reports
every text below 4.5:1 and the count below 7:1. The rule, relayed from the owner, is at least
4.5:1 for every text and at least 7:1 for numbers read at a glance. `--tank` freezes the tank's two views and draws masks
of the focus body, each tank-mate and the snow. It reports the focus body's median luminance
against the water around it (rule: at least 5 times) and the brightest mote against the dimmest
visible tank-mate (rule: no brighter).

At the last render, no text on any screen fell below 4.5:1 at 1366, 1920, 2560 or 3440 wide. The
focus body was 20 to 63 times the water around it. No mote was brighter than a tank-mate, though
in the left view no tank-mate was in frame at the measured moment.

Headless Edge's launcher returns before its child does, so `--dump-dom` never reaches standard
output. The probe logs to the console instead, `--enable-logging` writes the console to the
profile's `chrome_debug.log`, and `Start-Process -Wait` waits for the whole tree. The window of a
headless Edge is also shorter than `--window-size`, while its screenshot is not, so the probe sizes
its frame from the query.

## The sizes

The root font size is `clamp(11px, min(1.2vh, .68vw), 22px)` and every length is in rem, so a
screen scales with the window. Each page names its grid areas three times: a default and two others. An ultrawide layout, at an
aspect of 2:1 or wider, adds a column. A stacked layout under 1600 pixels wide scrolls rather than
crushing its panels. Charts are SVG drawn to their element's
size by script and redrawn on resize. Shots exist at 1366 by 768, 1440 by 900, 1920 by 1080, 1920
by 1200, 2560 by 1440, 3440 by 1440, 3840 by 2160, and 2560 by 1440 at 150 %. Under 1600 pixels the
shot shows the first screenful of a page that scrolls.

## The choices a reader should check

- **Panels are near-black navy** (`#071820` to `#050f16`, luminance under 1 %), opaque, with the
  haze and the caustics only in the gutters. Glass reads from the border and an inset top
  highlight. Glow and gradient fill belong to the hero chart and the live point alone.
- **Four inks:** `--ink-hi` for values, `--ink` for body text, `--ink-lo` for labels, column heads
  and axis ticks, and `--ink-dim` for units, keys and gridline labels only. A value label that
  sits over a line or a fill carries a dark edge (`svg text.halo`).
- **Colour has jobs.** Cyan is this run, violet the control (a ghost band), amber a significant
  cell (a tint, never text alone), and vermilion only as a plate behind a refusal. Status has no
  hue: shape and fill carry it, as in the theatre.
- **The tank follows body 1** at true scale, 2.5 m back and above, with the same camera pose in
  both views so own brain and control compare frame by frame. The whole-tank map in the corner
  carries where it goes; W switches to the wide shot, which the mock does not draw. Body 1 is the
  brightest thing in the view, tank-mates are dim grey-blue, and snow is texture at 13 % or less.
  Islands are tinted pools on the bed, and carrion is amber, ringed once eaten. The owner said the
  new tank looks great.
- **The owner's controls are few:** Open run, Open recipe and Stop on the registry; Edit by hand
  and launch on the recipe. Agents run experiments through the MCP, and the activity feeds name
  who did what and why.
- **Launch as trial** is badged "unregistered, pending a ruling". Trials wait on the owner's
  ruling and the mock does not decide it.

## Nothing about today's experiment is built in

The owner's rule, relayed by a8, is to take care not to make things specific. Scent matters
today, and next week another input may. Today's names stand in the mocks as the
invented example. Every panel carries an HTML comment, `<!-- source: ... -->`, saying where its
rows come from:

- Senses and outputs come from the genome's brain, grouped by part, in its order, with one output
  group per joint.
- Bodies are drawn part by part from the genome. The box and the capsule are today's example.
- Exam columns are the recipe's `exam.controls`, in order, with their labels. The reader pins
  the columns that lead; the defaults are the best constant and the first knockout. A control is
  greyed only when the genome makes it uninformative, and the reason is shown.
- The tank's right-hand view is a picker that defaults to the first control that is not own.
- Charts plot the reward's value and its named parts from `tank-scores.jsonl`, with its class shown.
  The search hero's axis is score or any measure both runs record. Net J is just one measure among
  them.
- The recipe screen is built from the recipe's structure, so a new section or override appears with
  no design change.

## Known faults of this pass

- The tank is drawn on a 2D canvas with its own projection, because no copy of three.js is in the
  repository and nothing may load from the internet. The build would use WebGL. Whether to vendor
  three.js is a8's or the owner's call.
- The wide tank shot (W) appears only as a tag; the mock does not draw it.
- At 3440 the run screen's activity column and the registry's list leave empty space.
- The genome screen's body plan is a fixed drawing that does not grow with its panel at ultrawide.

## What the screens need that the nursery does not write yet

This list is meant for the build.

### Every screen

- The room's server appends one row per operation, from the browser or the MCP alike: the time,
  who acted, the action, its target and the reason given. The activity feeds read it.
- Pictures and films need a store: a folder per run with a manifest of who asked, when, what it
  shows, at what size and why.
- The server opens live files with `FileShare.ReadWrite`, so it can read a file the nursery is
  still writing.
- Re-stepping an episode counts against the processor cap and never starts during a timing read.
- The machine's cap, sixteen of the thirty-two logical processors, is the owner's ruling and
  needs a setting the room can read. Temperature can come from `scripts/temp-check.ps1`.

### Registry

- The registry is append-only and numbered. A rerun is its own entry naming the entry it reran,
  with "identical" or the first file and line that differ.
- Each row's sparkline reads `tank-generations.jsonl`.

### Recipe

- The recipe format exists (format 1: world, bodies, setup, search, reward, exam). The base is
  merged under its child and the resolved recipe is hashed, so "differs from base" is a diff of two
  resolved recipes.
- The checker must return every refused field with its path and message. One that stops at the
  first refusal cannot fill a form.

### Run

- `tank-scores.jsonl` gives each member's reward value and named parts per generation.
- The exam needs `fresh` per genome per control. The build is adding it.
- The planned exam brains and controls should be in `run.json`, with a row per finished brain
  marking progress.

### Tank

- No episode is recorded step by step. The room re-steps one on the server with the nursery's own
  code, from the genome, the episode's seed and tank index, and the tank-mates' genomes. Which
  genomes shared a tank on a draw is not written yet.
- Only the top genome of a search generation can be re-stepped, because only `tank-best.jsonl`
  keeps a genome.
- A re-stepped exam episode is checked exact against `tank-exam.jsonl`'s per-draw `per`. A search
  episode has no recorded per-episode net, so it is marked unverified.
- The stepper needs to report the live brain values, the layout and the events: carrion eaten,
  islands entered, deaths.
- Export for Blender has no format yet. It should be agreed with the channel's Blender scripts in
  `scripts/channel/`.

### Genome

- No row names a genome's parents by id. The lineage panel and the change list need a genome id
  and its parents' ids, at least for each generation's top genome, plus the base parent's genome.
- A brain names its ports by hashed ids. The server can turn an id into a part and a sense with
  Core's development code.
