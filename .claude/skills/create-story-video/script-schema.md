# The script's schema

`script.md`, and every draft of it, `script-<n>.md`, are Markdown in exactly the form below, so that a
tool turns them into data with no judgement. The narration step reads the spoken words and the holds
from that data, and the shot list the rest; neither reads the Markdown by eye. `script-check.py`, beside this
file, checks the form (`script`), prints the spoken words alone (`say`) and prints the data (`json`):

```text
python script-check.py script <folder>\script-<n>.md
python script-check.py say <folder>\script-<n>.md
python script-check.py json <folder>\script.md
```

## The form

```markdown
# Script: <the film's title>

Question: <the question, word for word from story.md>
Answer: <the answer, word for word from story.md>

## 1. <the beat's name, as in story.md>

### 1.1

Say: <one sentence of prose.md, exactly as it is there>
Say: <the next sentence>
Facts: [facts.md:L<a>-<b>] [facts.md:L<c>]
Picture: run
See: <what the viewer sees while the sentences are heard, in plain words>
Run: <the run, as facts.md names it>
Bodies: <an id>, <an id>
Seconds: <from> to <to>
From: [facts.md:L<a>-<b>]
Text: <at most four words shown on screen> [facts.md:L<c>]
Hold: <seconds>

### 1.2

...

## 2. <the next beat's name, as in story.md>

...

## Notes

<anything the owner should know, in prose; neither spoken nor shown>
```

Every field is one line and nothing wraps. Blank lines are free.

## Line by line

- `# Script:` opens the file, with the film's title.
- `Question:` and `Answer:` are the question and the answer of `story.md`, word for word, before the
  first beat.
- `## <n>. <name>` is a beat. The script has every beat of `story.md`, in its order, with its number
  and its name.
- `### <beat>.<k>` is a passage: the sentences heard over one picture. Passages are numbered from 1
  within each beat.
- A place is a line or a run of lines of the film's `facts.md`: `[facts.md:L12]` or
  `[facts.md:L12-14]`. Places are separated by spaces.
- `## Notes`, if there is one, is the last section.

The fields of a passage come in this order:

| Field | How many | What it holds |
|---|---|---|
| `Say:` | any number | One sentence of `prose.md`, exactly as it is there, a name between double braces included. The script's sentences, read in order, are `prose.md`'s, beat by beat. |
| `Facts:` | one, when the passage has a `Say:` | The places that hold every claim in the passage's sentences. |
| `Picture:` | one | `run` for footage from a run, or `graphic` for a drawing. |
| `See:` | one | What the viewer sees while the sentences are heard, in plain words, including how the eye is led to the body the words mean. |
| `Run:` | one, for a run | The run to film, as `facts.md` names it. |
| `Bodies:` | at most one, for a run | The bodies to film, by their ids as `facts.md` gives them, separated by commas. |
| `Seconds:` | one, for a run | Where in the run the moment lies, in simulated seconds: `<a>`, or `<a> to <b>`, in digits with no separators. It is not the length of the shot. |
| `From:` | one | The places where the run, the bodies, the seconds, and what `See:` says of them are found. For a graphic, the places that hold what it shows. |
| `Text:` | any number | At most four words shown on screen, digits allowed, then the places that hold them. A name we give keeps its double braces, and a `Text:` that is only such a name needs no place. |
| `Hold:` | at most one | The seconds of silence the narration adds after the passage's last sentence, on top of the padding between passages, while the picture is watched. A passage with no `Say:` has one, and is that silence alone. |

A graphic has no `Run:`, `Bodies:` or `Seconds:`.

## The data

`script-check.py json` prints the script as this data, and prints nothing for a file that does not
follow the form. The values below are examples.

```json
{
  "title": "The film's title",
  "question": "The question, from story.md.",
  "answer": "The answer, from story.md.",
  "beats": [
    {
      "number": 1,
      "name": "The hook",
      "passages": [
        {
          "id": "1.1",
          "say": ["The first sentence.", "The second sentence."],
          "facts": [{"first": 40, "last": 42}],
          "picture": {
            "kind": "run",
            "see": "What the viewer sees.",
            "run": "r12-s3",
            "bodies": ["305", "311"],
            "seconds": [1200.0, 1260.0],
            "from": [{"first": 40, "last": 40}]
          },
          "text": [{"words": "1 in 20", "facts": [{"first": 41, "last": 41}]}],
          "hold": 2.0
        }
      ]
    }
  ],
  "notes": "Anything the owner should know."
}
```

A graphic's `run` and `seconds` are `null` and its `bodies` is empty. A single second is given as the
same number twice. A passage with no `Hold:` has `hold` 0.

## What comes later

The script's directing is a first pass. The narration step adds each passage's span and, except on the
owner's own files, each sentence's start and end, in the form [narration-schema.md](narration-schema.md) fixes, and leaves each hold as
silence. The shot list then directs the film again with those timings: it may change
a passage's picture and its words on screen, and cut a passage into several shots. Both keep the spoken
words, the holds and the passage numbers, and key on a passage's `id` and on a sentence's place in its passage's `say`, counted from 1.

A name between double braces keeps its braces here, so that the later steps know it is a name we
gave. The braces come off only where the words reach the viewer: the narration speaks the name without
them, as `say` prints it, and the subtitles and the words on screen show it without them.

## What the check refuses

The check counts; it cannot see. The composer's review covers the rest.

- A file not in the form above, or a question, answer or beat that differs from `story.md`'s. The film
  keeps the promise its opening makes (research/story-video/compelling-video-stories.md §1.10 to 1.13).
- Spoken words that are not `prose.md`'s, word for word and in order, beat by beat. The owner approved
  those words, and the prose step checked them.
- A place outside `facts.md`, or a run or body that `facts.md` does not name. Every step writes only
  from `facts.md`.
- More than four words in one `Text:`. Key words on screen help, and whole sentences do not
  (research/story-video/narration-writing.md §4.3).

The check also prints the spoken words, the spoken time at 145 words a minute, and the seconds held,
for the owner.

## Queued: the channel's cards

The intro and, later, the outro are to be placed by the script, not by the edit tool (the owner, 2026-09-28).
Each is a passage of its own at its place in the story, the intro straight after the hook, naming its card's
file and carrying no narration. The shot list then carries it as a shot, and the edit lays it like any other.
The form of that passage is not written yet; round 49 placed its intro through the edit settings.
