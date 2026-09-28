# shots.json

The form of the shot list: every shot of the film, placed on the narration's timeline. The shot-lister
writes each draft as `shots-<n>.json` in the film's folder, `shots.py check` checks it, and a draft
that passes is copied to `shots.json`, the step's deliverable. Filming reads the footage of every shot,
and the graphics step the graphics. Nothing reads it by eye; `shots.py sheet` prints it as a table.

**Every shot carries footage.** A graphic is a layer the edit lays over its own shot's footage, never a
picture of its own. The footage is the star: it shows the event, and the graphic carries the message
beside it without hiding it. Only a graphic that stands alone, over no footage, is the exception: the
film's opening or its ending, or a drawing that works only on a plain ground (the owner's ruling of
2026-09-28).

## The file

```json
{
  "schema": "create-story-video/shots/2",
  "script_sha256": "<64 hex>",
  "narration_sha256": "<64 hex>",
  "shots": [
    {"id": "1.1a", "passage": "1.1", "kind": "run", "from_s": 0.000, "to_s": 6.420,
     "see": "Body 11248 alone in the water, the camera close and slowly circling it.",
     "run": "r49-s2", "bodies": ["11248"], "camera": "Portrait",
     "run_from_s": 4894.5, "run_to_s": 4900.92,
     "text": [], "places": "[facts.md:L41] [facts.md:L118]"},
    {"id": "1.3a", "passage": "1.3", "kind": "graphic", "from_s": 12.620, "to_s": 20.160,
     "see": "Body 11248 drifts in the left half of the frame; on the corner card at the right, two bars of a lone stomach's life at death, the second about twice the first.",
     "run": "r49-s2", "bodies": ["11248"], "camera": "Portrait",
     "run_from_s": 4930.0, "run_to_s": 4937.54,
     "beats": [{"at_s": 12.620, "shows": "the first bar grows to 168 to 186 s"},
               {"at_s": 16.300, "shows": "the second bar grows beside it to 338 to 371 s"}],
     "stills": [],
     "text": [{"words": "about twice as long", "facts": "[facts.md:L77]", "from_s": 17.0, "to_s": 20.16}],
     "places": "[facts.md:L41] [facts.md:L77-78]"},
    {"id": "9.4a", "passage": "9.4", "kind": "graphic", "from_s": 325.000, "to_s": 333.900,
     "see": "The film's closing card: the question it opened with, and its answer.",
     "alone": "the ending: the film closes on its question and answer",
     "beats": [{"at_s": 325.000, "shows": "the question"}, {"at_s": 329.000, "shows": "the answer"}],
     "stills": [], "text": [], "places": "[facts.md:L40]"}
  ]
}
```

- `schema` is `create-story-video/shots/2`.
- `script_sha256` and `narration_sha256` are the sha256 of the film's `script.md` and `narration.json`
  the draft was made from. A shot list times one narration of one script; a new one of either needs a new
  shot list.

## A shot

Every shot has these fields:

| Field | What it holds |
|---|---|
| `id` | The passage's id and a letter, `a` for its first shot, then `b`, `c` and on, in order. |
| `passage` | The passage's id, as the script and the narration give it. |
| `kind` | `run` for footage alone, `graphic` for a drawing laid over footage, or standing alone. |
| `from_s`, `to_s` | Where the shot starts and ends on the narration's timeline, in seconds. |
| `see` | What the viewer sees, in plain words, including how the eye is led to the body the words mean. For a graphic over footage, it also says where in the frame the event is and where the drawing sits, so that the drawing does not hide it. |
| `text` | The words on screen, a list, each `{"words", "facts", "from_s", "to_s"}`: at most four words, digits allowed; the places that hold them, as a `Text:` line of the script writes them, or `""` for a name we give between double braces; and when they show, on the timeline, inside the shot. |
| `places` | The places in `facts.md` that hold what the shot shows: its run, bodies and seconds, a graphic's every number, and what `see` says of them. |

**The footage.** Every run shot, and every graphic that does not stand alone, has five more:

| Field | What it holds |
|---|---|
| `run` | The run to film, as `facts.md` names it. |
| `bodies` | The bodies the camera is on, by their ids as `facts.md` gives them; empty for the whole world. |
| `camera` | The director's camera move: `Arrival`, `Descent`, `Portrait`, `Floor`, `Birth`, `Colony` or `Card`. [shot-lister-brief.md](shot-lister-brief.md) says what each does. |
| `run_from_s`, `run_to_s` | The stretch of the run the shot plays, in simulated seconds. It is the action; the handles are added by filming. |

**The drawing.** A graphic has two more:

| Field | What it holds |
|---|---|
| `beats` | When each part of the drawing appears, a list, each `{"at_s", "shows"}`: a time on the timeline inside the shot, and what appears then, in plain words. |
| `stills` | The real bodies the drawing shows, a list, each `{"run", "body", "second"}`: the theatre draws each from its genome at that second. Empty when it shows none. |

A graphic that stands alone has `alone` in place of the footage's five fields: why it stands over no
footage, in a few words. The edit lays it over the plate, a flat colour.

## The rules the check holds

- The shots cover every passage of the narration, in order.
- A passage's shots tile its span on the timeline with no gap and no overlap. The first starts where the
  passage starts, each starts where the one before it ends, and the last ends where the passage ends.
- So every second of the film has footage under it, except where a graphic stands alone.
- A shot lasts at least 2 seconds.
- Footage plays its stretch at the speed of life, one simulated second a second, in a run shot and under a
  graphic alike. A run's before and after is two shots, one at each second, and the edit may dissolve
  between them.
- Every run and body a shot names is named in `facts.md`, and every place is a line `facts.md` has.
- A graphic has at least one beat, and its beats lie inside the shot, in order.
- A graphic that stands alone says why.
- Words on screen are at most four words, hold their places unless they are only a name we give, and
  show inside their shot.

The check prints each graphic that stands alone, with its length and why, so the owner sees every second
the film goes without footage. It also prints, without refusing, where the shot list departs from the
script: a passage cut into several shots, a picture changed from footage to a drawing or back, or another
run, body or stretch.

## What comes later

Filming records the footage of every shot, run shots and graphics alike, with handles of at least
`handles_s` seconds on each side, where the run allows it ([filming.md](filming.md)). The edit can then
move a cut or make a dissolve. The graphics step draws each graphic to its shot's length, holding its first
and last frames for the same purpose. The edit places every shot's footage at `from_s` on the timeline and
lays each graphic over its own shot's footage, or over the plate when it stands alone.
