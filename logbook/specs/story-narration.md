# The narration stage of a story film, and what it needs from the narration service

*Written 2026-09-25 as the owner added a narration step to the story-film flow, and revised the
same day against the narration server's design
(`D:\Projects\experiments\tts-narration-bakeoff\docs\narration-mcp-design.md`, "the design"
below). Part 1 is the stage in this repository's flow. Part 2 is what the flow needs from the
service, keyed to the design's sections, and is written so that it can be handed to the service's
author as it stands. Part 3 is this repository's side, which is not built yet.*

## Part 1: where narration sits in the flow

The flow is `.claude/skills/story-film/SKILL.md`; its stages are in
[`story-film.md`](story-film.md). Narration comes after the script is edited and before anything is
filmed:

    write → script → NARRATE → owner review (script and audio together) → check → film → assemble

It comes before filming because the narration sets each scene's length. Each scene's paragraphs are
spoken once, their lengths are measured, a fixed padding is added between them, and the sum is the
scene's length on screen. The captions follow the speech: each caption is shown while its words
are spoken. The film is then shot to those lengths, and the audio drops onto the Resolve timeline at
known frames.

Three rulings shape it (the owner, 2026-09-25):

1. **A paragraph is the unit sent to the voice**, never a single line. The delivery should carry
   across a paragraph's sentences the way a reader's does; a line-by-line read sounds like a list.
2. **What the voice says is decided before the voice.** The model has no SSML or phoneme input, so
   numbers, units, symbols and invented names are turned into the words to be spoken by
   deterministic rules and a pronunciation list, and those words are what the owner approves.
3. **The padding is ours.** The lead-in after a cut, the gap between two paragraphs and a scene's
   tail are set by this flow in one place, never by the service.

## Part 2: what the flow needs from the service

MUST means the flow cannot work without it; SHOULD means the flow works without it, worse. Each
item names the design's section it touches.

### R1 (MUST): each caption's time in the audio (§7.5, §11, §12's Phase 6 note)

For a segment submitted with `cues`, `get_results` returns, for the selected take and for every
other take it lists:

```json
"cues": [
  {"index": 0, "written": "Light fades with depth.", "spoken": "Light fades with depth.",
   "start_s": 0.08, "end_s": 1.62,
   "words": [{"text": "Light", "start_s": 0.08, "end_s": 0.31}, "..."]},
  "..."],
"alignment": {"method": "whisper-word-timestamps", "model": "openai/whisper-large-v3",
              "revision": "...", "flags": []}
```

- **Times are in the delivery file**, after trim, resample and any stretch: 0 is the first sample of
  `delivery.wav`.
- **Accuracy:** a cue's start and end within about 0.1 s of where its first and last words are
  heard.
- **A cue that cannot be placed** gets a warn flag, `CUE_UNALIGNED`, and goes on `listen_first`. It is
  never silently interpolated.

This is the design's Phase 6 "cue-level alignment", and the flow needs it in the first usable
version. QA already runs Whisper with word timestamps (§11, step 2), so most of the work is mapping
words back to cues. If Whisper's word times prove too loose, a CTC forced aligner over the known
spoken text is the better tool, and the method field says which one ran.

### R2 (MUST): cues without times (§7.2 `Segment`)

The flow sends cues before any time exists; the narration is what makes the times. So
`cues[].at_s` becomes optional, and a segment may be sent as `cues` alone, with `text` omitted and
built by the server as the join (§9, step 2). When `text` and `cues` are both given, the server
refuses a `text` that is not their join.

### R3 (MUST): normalise each cue, then join (§9)

Phrase rules, unit expansion, number words and lexicon matches never cross a cue boundary. Each
cue's spoken text is then its own, and R1 can find its span. The design joins the cues at step 2,
before the other rules; the order has to be the other way round.

### R4 (MUST): no fitting when no budget is given (§7.3 options, §12)

With `scene_seconds` absent:

- no fit remedy runs: no retake for length, no native speed change, no time-stretch;
- no `OVER_SCENE` or fit flag is raised;
- `stitch: "none"` returns the takes alone.

The flow sets scene lengths from the narration, so the service must never tune a take toward a
length the flow did not ask for. Fitting stays useful for a later case where a picture is fixed
first, and the flow will pass `scene_seconds` then.

### R5 (MUST): exact lengths (§7.5, §13)

Each take in `get_results` carries its delivery file's `samples` and `sample_rate`, from which
`duration_s` follows, and its trim record (`head_s`, `tail_s`, `pad_s`), which is in the sidecar
today but not in the results.

### R6 (MUST): English number words in the first version (§9 steps 4 to 7, Q4)

The owner's ruling above means number words cannot wait for Phase 6. The normaliser needs, per
project:

- **cardinals** in British form: 3,207 as "three thousand two hundred and seven", with no comma in
  the spoken text, since a comma is a pause cue (the design's revision 3, §9);
- **decimals** digit by digit: 0.54 as "zero point five four";
- **per cent**: 14% as "fourteen per cent";
- **identifiers** set apart from quantities: a number after a configured keyword (`body`, `tank`,
  `round`, `seed`, `line`) is read as an identifier. How an identifier is read ("body two oh one" or
  "body two hundred and one") is the owner's choice, so the rule is a project setting, not code;
- **units** as the design's table, plus "J a second" → "joules a second", `m³` and `m3` → "cubic
  metres", `m/s` → "metres a second", `×` → "times", and a thousands comma never read as a pause;
- **symbols** the captions use: an em dash read as a pause, `·` refused rather than read.

The QA number check (§11, step 4) already compares canonical values, so it keeps working when the
spoken text carries words.

### R7 (MUST): the spoken text shown before anything is rendered (§7.6 `normalize_text`)

`normalize_text` returns, per cue: the written text, the spoken text, each substitution with its
rule, and every ambiguity as a warning, never a silent guess. The flow shows the owner a
caption-to-spoken table from it at the review, and renders with `strict_normalisation: true`.

### R8 (SHOULD): a paragraph's safe length (§7.6 `get_server_status` or `list_voices`)

A scene's paragraph is one to eight captions: about 50 to 450 characters, 4 to 35 s of speech. The
service reports, per voice, the longest segment it renders without drift or garbling, in characters
or seconds. The flow's splitter keeps every paragraph under it.

### R9 (SHOULD): several takes of each segment in one job (§7.3 options, §8)

Add an option `takes: 1 to 3` to `submit_job`. It renders attempts 0 to n−1 of every listed segment in
one model load, QA's each, selects the first that passes and lists the others with their cues and
lengths. The owner picks a delivery by ear at the review. `retake_segment` covers one segment at a
time; a batch saves the model loads.

### R10 (SHOULD): the neighbouring text as context (§3.3)

A backend that can condition on the text before and after a segment without speaking it would take
`context_before` and `context_after`, recorded in the request key. A backend that cannot says so in
its `controls`, and the field is refused, as any control it cannot honour is. Least important of
the list.

### R11 (MUST): changes reported after a resubmission (§7.3 `plan`, §10.2)

After a caption edit the flow submits the whole script again. The result names every segment that
was rendered anew, with its old and new take and length. The flow then re-times those scenes alone.
The design's `stale_segments` and cache hits are most of this; the old and new lengths are the
missing piece.

### R12 (MUST): files handed over by path and hash (§7.5, §15)

Absolute paths and sha256 for every delivery file, valid until the owner's `gc`, as designed. The
flow copies the takes it uses into its own story folder and checks the hash; it needs no export alias
into this repository (Q9).

### R13 (MUST): the approval visible to the flow (§11 human gate)

`get_results` carries `human_approved` and the cut number, as designed. The flow records the cut
the owner approved and times the film from that cut only.

### Answers to the design's open questions, from this flow

- For **Q3**, the film has music under the narration, mixed in Resolve. So 48 kHz, 24-bit mono at
  −16 LUFS per take suits it; the balance with the music is set in the mix, and −23 LUFS takes are
  not needed.
- For **Q4**, British number words and "per cent", as R6.
- For **Q7**, no automatic speed-up or stretch for the films, as R4. A scene too long for its picture is
  a script edit.
- For **Q9**, no export alias into this repository, as R12.
- For **Q13**, captions: a segment is one paragraph of a scene's caption lines, sent as cues. Prose is
  not narrated.

The other questions are the owner's and do not change what the flow needs.

## Part 2b: review of the design's revision 3 (2026-09-26)

Revision 3 of `narration-mcp-design.md` meets every MUST above on paper, and its section 21 maps
each one. R5's `pad_s` and R6's comma-free example are taken here as it asked. Four changes are
still needed before the flow can rely on it, and one reading of R1 is settled for Phase 0.

### V1 (MUST): keep by-ear picks made on a draft cut (§8, cuts and inheritance)

Inheritance runs from the latest approved cut, and `select_take` writes a new draft on the latest
cut. So a pick can be lost. Cut 2 is approved. The owner picks take 1 of `s07-p2` by ear, which
makes draft cut 3. One caption is edited elsewhere and the script is sent again. The base is cut 2
once more, and `s07-p2` goes back to its automatic take.

The fix is to inherit each selection from the latest cut, draft or approved, wherever the engine
text and voice hash match. `changes[]` would still be reported against the latest approved cut. Giving both
comparisons, each named, would suit the flow as well.

### V2 (MUST): prefer a take whose cues are all placed (§8, selection)

Selection takes the lowest passing attempt, else the lowest warned one. An unplaced cue is only a
warning, so its take can be chosen over a later take whose cues are all placed but which carries
another warning, such as `PACE_FAST`. The flow refuses to time a segment with an unplaced cue
(Part 3). Among warned takes, the rule should prefer those with every cue placed.

### V3 (MUST): the identifier style set per keyword (§9, Q15)

One style cannot read these captions the way a person would. "Body 201" wants digits ("body two
oh one"), while "round 48" and "tank 3" want cardinals ("round forty-eight"). `identifier_style`
should be a map from keyword to style, and its values are the owner's ruling under Q15. The rule
should also take a plural keyword and a list, so that "Tanks 1 and 3" reads both as identifiers.

### V4 (MUST): golden tests on the current captions (§9)

The golden set is built from `scripts/r48_captions_s01-07.json`, the writer's captions, which the
script stage has since rewritten. The current 94 are in
`D:/Projects/experiments/evolution-simulator/scratch/wt-film/logbook/specs/story-r48-v2/script-trial/story.json`,
on the branch `story-flow` until it is merged. These forms in them need a golden case each.

- "Tank 2's simulation." The "s" after "2'" is a possessive and never the unit for seconds.
- "Tanks 1 and 3." A plural keyword and a list, as V3.
- "0.50 J." A written trailing zero, read "zero point five zero" or "zero point five", to be ruled
  with Q15.
- "Counted in joules, or J." A bare J after no number, which needs a lexicon term ("jay").
- "167 m across", "157 s ago", "4,200 s old", "2,655 s old", "129 J on", "66 J could." A unit
  followed by a word that is no noun. Under strict mode each false `unit.attributive_guess` blocks
  the render.
- "The life of body 201." A Card's title line, written with no full stop, which the join ends with
  one.

The attributive form nearly always has a determiner before the number, as in "a 46 s scene" or
"its 14 J cushion". Firing the guess only on that shape would cut the false warnings.

### R1, read for Phase 0

The flow changes a caption in the pause the snap finds, so it works at a p95 boundary error up to
0.2 s. The aim stays 0.1 s. Above 0.2 s the service publishes the measured error, and the flow
pads each caption by it.

### For information: the paragraphs' spoken length (R8)

Numbers grow when read as words. By a rough expansion, the longest paragraph of the current
script (the first of scene 11) is 391 written and about 620 spoken characters. That is past the
ladder's top rung of 560. Five of the 25 paragraphs are over 350 spoken. The flow therefore splits
at caption boundaries against the voice's `max_segment_chars`, using the spoken text
`normalize_text` returns (Part 3). It would help if `SEGMENT_TOO_LONG` carried each offender's
spoken length and the limit.

## Part 3: this repository's side

Built on 2026-09-25 and tested on a synthetic narration only (tones of the right lengths, with cue
times spread by characters, on round 48's story), because the service does not exist yet. The
stage's own tool is `scripts/story-narration.py`: `segments` writes what is sent, `timing` times the
story from what came back, and `check` reports without writing. What is not built is the step
between them, the calls to the service, since its interface is still being written.

- **A scene's captions** are split into paragraphs where the script has a blank line
  between two captions (`new_paragraph: true` on the caption that opens one, in `story.json`). One
  paragraph is one segment, `sNN-pK` for scene NN's paragraph K. Whether a chapter's title is spoken
  over its 8 s card, as a segment `cNN`, is open for the owner; the recommendation is yes.
- **The spoken form** comes from the service's normaliser. The stage calls `normalize_text` with every paragraph's cues, resolves each
  warning by a lexicon entry or a caption edit, and writes the caption-to-spoken table beside the
  script for the owner's review.
- **The padding** is one set of values in the story's `flow.json`, starting at 0.8 s from a cut to the
  first word, 1.2 s between two paragraphs of a scene, and 1.0 s from the last word to the next cut.
  These are starting values, to be set by ear on the first narrated film.
- **The captions follow the speech.** A caption is shown from its cue's start to its end plus 0.4 s,
  and ends 0.1 s before the next caption; a cue spoken faster than a caption can be read (about
  17 characters a second) is flagged, because the viewer reads what the voice says.
- **The manifest** is `narration/narration.json` in the story's folder: the service, the voice's id
  and hash, the approved cut, the padding, and per segment its scene, paragraph, cues (written,
  spoken, start, end), take id, hash, samples and sample rate, with the take copied beside it as
  `narration/sNN-pK.wav`.
- **The timing** is `story-narration.py timing`. From the manifest it sets each caption's `at` and
  `seconds` and each scene's `seconds` in `story.json`, with the scene's clips and their offsets
  under `narration`, keeping the version before it as `story.pre-timing.json`. It prints each
  scene's length against the one before and the film's total, cuts a chart's `until` to its
  scene's new end, and names a scene the narration shortens by more than a quarter, since its
  picture may need the time; `--keep-length` keeps every scene at least as long as it was. On the
  synthetic narration of round 48's story the film came to 7 min 35 s against the writer's 9 min
  39 s, and six scenes were named. Once a story is timed, the script check reads its captions by
  the speech (17 characters a second, no least hold) and not by the writer's pace rule.
- **On the timeline**, `story-resolve.py` places each clip on the audio track A2, Narration, at its
  scene's record frame plus its offset: after the chapter card as filmed (the assembler's table
  gives each scene's own start, `own_start_s`), or on the card for a spoken chapter title. The plan
  names a clip that runs past its scene's filmed end, which happens when a scene was filmed before
  its narration. The ffmpeg join, the fallback without Resolve, mixes the same clips as the film's
  sound, each delayed to its second; the mix was checked to the hundredth of a second on a test
  tone. The Resolve build of the narration track has not been run: the owner's Resolve was left
  alone while another agent was working in it.
- **The machine's load is this flow's concern, not the service's.** The owner's rulings on load
  (HANDOFF carries the current one) bind whoever starts a job, so the agent running the flow reads
  the machine before it submits a narration job, the way it would before a render: nothing heavy
  beside it, and never a narration job and a film render at once. The service's own status call
  (`get_server_status`) is enough to see whether it is busy.
- **What revision 3 adds to this side** (Part 2b), none of it built yet:
  - `segments` splits a paragraph at a caption boundary when its spoken text, from
    `normalize_text`, is longer than the voice's `max_segment_chars`;
  - the call step copies each selected take into `narration/`, checks its sha256, and refuses a
    segment whose cue has no time;
  - `story-flow.py approve script` reads `human_approved` and the cut from the manifest, since the
    owner approves a cut with `narration-admin cut approve`, outside the flow;
  - `scripts/sweep-orphans.ps1` knows the service's daemon (`-m narration.daemon`) and its workers
    (`-m narration_worker`) by their command lines, which the design documents, and lists them
    apart from strays; the daemon exits on its own after 15 idle minutes;
  - the script stage estimates each paragraph's spoken length and warns before the voice's limit.
- **Without the service** the stage is skipped (`story-flow.py skip narrate`), the captions keep the
  writer's timing and the reading-pace rule, and the film carries music only.
