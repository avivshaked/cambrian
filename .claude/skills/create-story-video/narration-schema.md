# The narration's schema

`narration.json`, and every draft of it (`narration-<n>.json`), is JSON in exactly the form below. The
tool writes it; no agent does. The shot list reads the passages and their sentences. The edit reads the
pieces and places each file at its `at_s`. `narration.py`, beside this file, checks the form against the
script and the audio, and prints a sheet of it for the owner:

```text
python narration.py check <folder>\narration.json
python narration.py sheet <folder>\narration.json
```

## The form

```json
{
  "schema": "create-story-video/narration/1",
  "source": "service",
  "script_sha256": "<sha256 of script.md's bytes>",
  "draft": 2,
  "sample_rate": 48000,
  "length_s": 431.270,
  "padding": {"lead_s": 0.5, "between_s": 1.0, "within_s": 0.6, "end_s": 2.0},
  "cut": {"min_pause_s": 0.12, "edge_s": 0.08},
  "service": {"job_ids": ["job_…"], "voice_hash": "sha256:…", "clip_sha256": "<64 hex>",
              "engine_profile": {"id": "qwen3-base-1.7b.p1", "hash": "sha256:…"}, "measured_error": null},
  "segments": [
    {"id": "s1.1.1", "take_id": "tk_…", "analysis_id": "an_…", "attempt": 0, "take_sha256": "<64 hex>",
     "take_samples": 410880, "verdict": "pass", "accepted": [], "flags": [], "loudness": {"…": "…"}}
  ],
  "pieces": [
    {"id": "p1.1", "file": {"path": "narration/p1.1.wav", "sha256": "<64 hex>", "samples": 410880},
     "segment": "s1.1.1", "take_from_sample": 0, "take_to_sample": 410880,
     "at_sample": 20160, "at_s": 0.420, "passages": ["1.1"]}
  ],
  "passages": [
    {"id": "1.1", "from_s": 0.000, "to_s": 9.400, "speech_from_s": 0.500, "speech_to_s": 8.900, "hold_s": 0.0,
     "sentences": [
       {"place": 1, "text": "This creature is nothing but a stomach, and it's the only one of sixty-three like it that ever had a child.",
        "piece": "p1.1", "from_s": 0.500, "to_s": 8.900}]}
  ]
}
```

The example's numbers belong together:
- The take holds 0.08 s, then 8.40 s of speech (21 words, about 150 a minute), then 0.08 s: 410,880
  samples.
- The first word falls at the lead, 0.5 s, so the piece starts at 0.42 s.
- Passage 1.2 starts a new piece. Its first word falls at 8.9 + 0.0 + 1.0 = 9.9 s, so its span starts
  at 9.4 s.

## Field by field

**Times and ids**
- Every `_s` value is seconds on the film's narration timeline, where 0 is the narration's start. It is
  rounded to the millisecond from whole-sample placements. The length is rounded up to a whole
  millisecond, so that `length_s` holds every sample.
- A piece's id is `p` and its first passage's id, with `.<place>` added when it starts inside a passage.

**`segments`** are what the voice read, one `submit_job` segment each, over all the jobs.
`service.job_ids` names the jobs, one a beat or part of a beat, in order. `verdict` and `flags` are the
service's. `accepted` lists the codes that failed the take when the film's `accept` held every one of them
at `collect`, and is empty otherwise.

**`pieces`** are the files cut from the takes, in order.
- `take_from_sample` and `take_to_sample` are where the piece lies in its take.
- The piece's samples are exactly the take's samples in that range.
- A piece keeps at most `edge_s` of silence before its first word and after its last.
- `at_sample` and `at_s` are where the piece starts on the timeline.
- A draft's paths are in `narration-<n>/`. The deliverable's are in `narration/`, relative to the film's
  folder.

**`passages`** are every passage of the script, in its order, keyed on the script's `id`.
- `from_s` and `to_s` are its span.
- `speech_from_s` and `speech_to_s` are its first word's start and its last word's end, or `null` for a
  passage with no `Say:`.
- `hold_s` is the script's `hold`.

**`sentences`** are the passage's `say`, in order.
- `place` counts from 1.
- `text` is the script's sentence with a given name's braces off.
- `piece` names the file the sentence is in.
- `from_s` and `to_s` are its first word's start and its last word's end, or `null` on the `own files`
  route.

**`measured_error`** is the service's published cue-boundary error, `{p50_s, p95_s, n, benchmark}`. Until
the service has a benchmark it is `null`, and nothing says how close the times are.

## How the timeline is built

- Pieces are placed so that their words fall as the padding sets.
- The first word falls at `lead_s`, plus the holds of any passages with no `Say:` before it.
- **A piece that starts a passage.** The silence from the last word before it to its first word is
  `between_s`, plus the earlier passage's hold, plus the holds of any passages with no `Say:` in between.
- **A piece that continues a passage.** The silence is `within_s`.
- **The end.** The narration ends `end_s` after the last word and its hold, plus the holds of any passages
  with no `Say:` after it.
- **Inside a piece**, times are the take's own.

**Spans**
- A passage's span starts:
  - `lead_s` before its first word, when it starts a piece;
  - the smaller of `lead_s` and half the voice's pause before its first word, when it starts inside a
    piece.
- A passage with no `Say:` spans its hold and ends where the next span starts.
- Every span ends where the next starts. The first starts at 0 and the last ends at `length_s`.
- `lead_s` must not exceed `between_s`, or a span would start before the previous passage's words end.
- `lead_s` and `end_s` are at least `edge_s`, and `between_s` and `within_s` at least twice `edge_s`,
  or a piece's kept silence could overlap the next piece or the narration's ends.

## The other two routes

On the `own files` route:
- `service` is null and `segments` is empty.
- Each piece is one of the owner's files, copied unchanged, named `p` and the passage's id (`p2.4`, in
  `narration/p2.4.wav`). Its `segment`, `take_from_sample` and `take_to_sample` are null.
- The tool places each file as it places a piece that starts a passage, with the file's first sample as
  the first word, since nothing finds the words inside it.
- A passage's `speech_from_s` and `speech_to_s` are its file's first and last sample on the timeline, and
  every sentence's `piece` is the passage's file, with `from_s` and `to_s` null.

On the `estimate` route:
- `service` and `draft` are null, `segments` and `pieces` are empty, and there is no sound.
- `sample_rate` is 48000, so that every time is still whole samples.
- Each sentence lasts its words at 145 a minute, one after another inside its passage with `within_s`
  between them, and the passages are placed with the padding and the holds as above.
- Each passage with words is placed as a piece that starts a passage, so its span starts `lead_s`
  before its first word.
- `piece` is null on every sentence.

## What the check refuses

The check counts; it cannot hear. The owner listens.

**Refused in any narration file:**
- A file not in the form above, or a `script_sha256` that is not the current `script.md`'s.
- Passages that are not the script's, in its order, or sentences whose `text` is not the script's sentence
  with its braces taken off.
- A sentence with no time, except on the `own files` route, or with `from_s` at or after `to_s`, or out
  of order.
- A passage's words or hold outside its span, or spans that do not tile 0 to `length_s`.
- On the `service` route, a passage that shares a piece with the next passage that has a `Say:`, when it
  has a hold or a passage with no `Say:` lies between them. The pause it needs cannot be made.
- A piece whose samples or sha256 differ from its file on disk.
- Pieces that overlap on the timeline, or sample rates that are not all the same.
- A segment whose take's QA verdict is `fail`, unless `accepted` names every code that failed it and no other,
  and the film's settings still accept them.

**Refused by `collect` in the takes files:**
- A job the plan has with no takes file, or jobs whose voice hash, engine profile or measured error
  differ.
- A segment the plan has and the takes lack, or the other way.
- A `take` that is null.
- An unplaced cue.
- A cue whose `received` is not the plan's sentence.
- A take whose file on disk does not match its sha256, samples or rate.
- A `script.md` that changed after the draft's request.

**What `collect` prints:**
- Its first line, `NARRATION CHECK: PASS`, `FAIL` or `REFUSED`.
- The cuts made, and the cuts that failed with why.
- The warnings.
- What the service asks to be heard first.
- The length against five to seven minutes.
- For the next draft: the segments that need new attempts and the segments to split.
