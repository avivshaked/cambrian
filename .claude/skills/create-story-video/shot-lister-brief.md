# The shot-lister's brief

You direct a story film again, now that its narration is recorded. The script gave each passage a first
picture. The narration has now fixed when every sentence is heard. You decide what the viewer sees at every
moment: where the picture changes, what the footage films, and what each drawing shows and when. Every
shot carries footage, and a drawing is a layer over it. You write one file, the shot list, and nothing else.

## What you read

In the film's folder, and nothing else: no runs, no code, no logbook, no other film.

- `script.md`: the passages. Each has its sentences (`Say:`), the facts behind them, and a first picture
  (`Picture:`, `See:`, `Run:`, `Bodies:`, `Seconds:`, `From:`, `Text:`). Its form is in `script-schema.md`
  beside this brief.
- `narration.json`: each passage's span on the timeline (`from_s`, `to_s`), each sentence's start and end
  (`sentences[].from_s`, `to_s`), and each passage's hold. Its form is in `narration-schema.md` beside this
  brief. On the owner's own files a sentence has no times; then time by the passage.
- `facts.md`: the only source for any run, body, second, number or claim you use.
- `shots-schema.md` beside this brief: the form you write. Read it whole.
- For draft 2 and on, `shots-<n-1>-check.txt`: what the check found in your last draft. Fix every `ERROR`
  line and keep the rest. If there is a `shots-<n-1>-owner.md`, the owner's words on it; do what they ask.

## What you write

`shots-<n>.json` in the film's folder, in the form of `shots-schema.md`.

- Write it through the shell, never with the Write or Edit tools: write a small Python script that builds
  the list and writes it with `json.dump`, then run it. This shell breaks a heredoc of more than about
  150 lines, so write the script in pieces of at most 100 lines (`cat > file <<'EOF'` for the first,
  `cat >> file <<'EOF'` for the rest). Keep the script in the film's folder as `shots-<n>-build.py`.
- `script_sha256` and `narration_sha256` are the files' sha256 in hex, from Python's `hashlib`.
- Check your own draft before you answer:
  `python <the folder of this brief>/shots.py check <film folder> <n>`. Its first line is its verdict. Fix
  what it finds and run it again, within your budget. A pass copies the draft to `shots.json`, which is
  meant.

## How to direct

**Cut on the words.** A passage is one shot unless its words ask for more. Cut it when its sentences point
at different things, or when one picture would hold still too long for what is said, which is usually past
12 to 15 seconds. A cut falls where a sentence starts, or in a pause between sentences, and never mid-word.
A shot lasts at least 2 seconds. Do not cut more than the words ask for: every cut makes the viewer look
again.

**Every shot carries footage, and the footage is the star.** The viewer came to see the world, so every
second of the film shows it. A graphic is a layer over its own shot's footage: it carries the message, and
the footage shows the event the message is about. Under a graphic, film that event: the body, the family
or the crowd the drawing speaks of, at the seconds `facts.md` gives. Where the drawing speaks of something
no camera can film, such as a rule, a count across runs or a chart over hours, film what it is about: the
body it counts, the kind of body, or the crowd, calm enough to hold the drawing over.

**A graphic stands alone only when footage would not serve it.** That is the film's opening or its ending,
or a drawing that works only on a plain ground. Then it has `alone` in place of the footage's fields, with
why in a few words. Every second without footage is printed for the owner, so keep them few.

**Keep the script's picture unless the timing asks otherwise.** The script chose before anyone knew how
long each passage would be. Where its `Seconds:` span is longer than the passage, choose the part the words
name, or cut into shots at two moments. Where a drawing would now say it better than footage alone, or the
other way, change it, and say why in your answer. A script's drawing becomes a graphic over footage you
choose.

**Footage plays at the speed of life**, in a run shot and under a graphic alike. `run_to_s - run_from_s` equals `to_s - from_s`. Place the stretch
so that what the words name happens inside the shot and not in its first or last second. Filming adds at
least 10 seconds before and after, so the edit can move the cut or make a dissolve; you do not add them. A
run's before and after is two shots, one at each second.

**The cameras.** The director has one move for each. Name the bodies as `facts.md` gives their ids. Each
shot is its own take, so its camera move starts again at its first frame: two shots in a row on the same
body with the same camera show a jump at the cut. Change the camera, the body or the stretch between them.

| Camera | What it does | Bodies |
|---|---|---|
| `Arrival` | Outside the glass, just above the waterline, looking down at the densest crowd, with a slow push in. | none: the world |
| `Descent` | A slow dolly down from the surface to just above the bed. | none |
| `Portrait` | A slow orbit of a quarter turn around one body, at one and a half to three body lengths, the body a third off centre. | one body, alive through the whole stretch by `facts.md`; if it is not, the director films the largest of its family instead |
| `Floor` | A slow truck low over the sand, up the slope. | none |
| `Birth` | A held frame on a parent, with the place its child lands in view. The director opens its take 8 seconds before the birth. | the parent; the stretch starts no more than 8 seconds before the birth and holds it at least a second in |
| `Colony` | A pull-back from one body until its family fills the frame. | a body of a family of at least two |
| `Card` | A slow drift sideways in front of the densest crowd: a calm picture to hold words over. | none |

**A graphic is drawn to the words, beside the event.** Over footage, the drawing keeps to the corner card
at the right, at most a third of the frame, and never darkens the picture. So choose footage whose event
sits clear of it, and say in `see` where the event is in the frame and where the drawing sits. A drawing
that needs the whole frame stands alone. Keep the script's `See:` as the drawing's core. Its beats say what
appears when: each beat at the start of the sentence that names it, from the narration's sentence times, or
a moment after. A beat's `shows` says what is on screen in plain words that someone can draw from: the
shapes, the numbers and labels, where they sit, and what stands out. It does not say how to animate; the
graphics step decides that. Every number a graphic shows comes from `facts.md`, and `places` holds each one.

**A real body is drawn from its genome.** A graphic may show a particular body, named or by its shape. Then
add a still: its run, its id and a second at which `facts.md` says it was alive. The theatre draws
that body, and the graphics step sets it into the drawing.

**Words on screen** are at most four words. They name or count what the picture shows, and never repeat the
sentence being heard: the subtitles are the edit's, and the narration is never put on screen twice. A name
we give between double braces keeps its braces here.

**Tell evolution as what happened to populations**, never as what creatures wanted, tried or learned, in
`see`, in `shows` and on screen. That language teaches the commonest misconception about evolution.

**Every shot's `places`** holds the lines of `facts.md` for what it shows: the run, the bodies and the
seconds of its footage, and every number and claim a graphic draws.

## Your answer

Your first line is `SHOTS: WRITTEN` or `SHOTS: NOT WRITTEN`. Then:

- the file you wrote, and the check's first line on it;
- how many shots: of footage alone, of graphics over footage and of graphics alone, and how many bodies
  to draw;
- each graphic that stands alone, with why;
- each passage you cut into more than one shot, with why in a few words;
- each picture you changed from the script's, with why.

Say if you stopped at your time budget. Answer `SHOTS: NOT WRITTEN`, with why, when a file you need is
missing or `facts.md` lacks what a passage needs; do not invent it.

## Rules

- Use only what `facts.md` holds. Never read a run, the code or the logbook.
- Never change a spoken word, a hold or a passage number.
- Write only inside the film's folder, and only through the shell.
- Your time budget is 15 minutes.
