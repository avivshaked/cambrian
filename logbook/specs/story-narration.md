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
2. **What the voice says is decided here, before the voice.** The model has no SSML or phoneme
   input. So this flow turns numbers, units, symbols and invented names into the words to be
   spoken, by its own deterministic rules and a pronunciation list kept in this repository ("on
   our side", the owner's words). Those words are what the owner approves, and the service speaks them
   as sent.
3. **The padding is ours.** The lead-in after a cut, the gap between two paragraphs and a scene's
   tail are set by this flow in one place, never by the service.

And one principle over all three (the owner, 2026-09-26): **the service knows nothing of this
use.** It narrates text in a locked voice and reports what it did, as it would for any caller.
What the film says, how its numbers and names are read, when a job may run, how long a scene is
and who approves the result all belong to this flow. Part 2 asks the service only for things any
caller could use; Part 3 holds the rest.

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
- **Accuracy:** the service measures how far its cue boundaries fall from where the words are
  heard, and publishes it with the method (p50 and p95). How much error a caller can bear is the
  caller's; this flow's tolerance is in Part 3.
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

### R3 (MUST): each cue is its own unit (§9)

Whatever the service does to the text (a pronunciation hint, a validation) stays inside the cue it
came from. Each cue's spoken text is then its own, and R1 can find its span.

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

### R6 (MUST): the text spoken as sent (§9, `text_mode: "spoken"`)

This flow sends each cue already in the words to be spoken. The service speaks them as given. It
applies only the pronunciation hints the caller has entered for a term, and it reports any digit,
symbol or unit-like token left in a cue as a warning. It needs no number convention, locale rule
or keyword list of this flow's. A normaliser the service keeps for other callers is not used here.
None of the service's defaults should carry this flow's vocabulary either: the identifier keywords
body, tank, round, seed and line, a project named for this flow, or tests built from its captions.

*Revised 2026-09-26.* R6 first asked the service's normaliser for this flow's conventions (British
number words, identifiers after set keywords, the units of these captions). That put this flow's
choices in the service, against ruling 2, and the conventions moved to Part 3.

### R7 (MUST): what was spoken, echoed per cue (§7.5)

The results carry, for each cue, the text as received and the text given to the engine after any
pronunciation hint. The flow checks the first against what it sent. The written-to-spoken table
the owner reviews is the flow's own (Part 3).

*Revised 2026-09-26.* R7 first asked the service to show the spoken text before rendering, from its
own normaliser; with the words made here, the table is made here too.

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

### R13: withdrawn (2026-09-26)

R13 asked that the service's human approval of a cut be visible, so that the film would be timed
from the approved cut. Approving a narration for a film is the owner's review in this flow
(`story-flow.py approve script`), which records the cut and the takes it approved. The service
may keep approvals of its own, such as the lock on a voice; the flow neither reads nor needs them.

### R14 (SHOULD): spans that must be heard exactly (§11)

The caller may mark spans of a cue's spoken text as exact, such as a number in words. Any difference
the transcript shows inside a marked span fails the take, whatever the word error rate says. The
design's number check read its spans from its own normaliser. With the words made on the caller's
side, the caller marks them. A misread number in a paragraph of sixty words is under two per cent
of it, which the rate alone would pass.

### Answers to the design's open questions, from this flow

- For **Q3**, the film has music under the narration, mixed in Resolve. So 48 kHz, 24-bit mono at
  −16 LUFS per take suits it; the balance with the music is set in the mix, and −23 LUFS takes are
  not needed.
- For **Q4**, nothing: this flow sends its numbers already in words (R6 as revised).
- For **Q7**, no automatic speed-up or stretch for the films, as R4. A scene too long for its picture is
  a script edit.
- For **Q9**, no export alias into this repository, as R12.
- For **Q13**, captions: a segment is one paragraph of a scene's caption lines, sent as cues. Prose is
  not narrated.

The other questions are the owner's and do not change what the flow needs.

## Part 2b: review of the design's revision 3 (2026-09-26)

Revision 3 of `narration-mcp-design.md` met every MUST of Part 2 as it then stood, and its section
21 maps each one. R5's `pad_s` is taken here as it asked. The same day the owner asked that the
service know nothing of this use, and Part 2 was revised under that rule. R6 and R7 now ask the
service to speak text sent in words, R13 is withdrawn, and R14 is new. This review is revised with
it. Two changes to the design are still needed, V1 and V2. V3 and V4 were withdrawn, and their
content moved to Part 3.

### V1 (MUST): keep by-ear picks made on a draft cut (§8, cuts and inheritance)

Inheritance runs from the latest approved cut, and `select_take` writes a new draft on the latest
cut. So a pick can be lost. Cut 2 is approved. A take of `s07-p2` is picked by ear, which makes
draft cut 3. One caption is edited elsewhere and the script is sent again. The base is cut 2 once
more, and `s07-p2` goes back to its automatic take.

The fix is to inherit each selection from the latest cut, draft or approved, wherever the engine
text and voice hash match. `changes[]` would compare against a cut the caller names, the previous
cut when it names none, since which cut a caller last used is the caller's to know.

### V2 (MUST): prefer a take whose cues are all placed (§8, selection)

Selection takes the lowest passing attempt, else the lowest warned one. An unplaced cue is only a
warning, so its take can be chosen over a later take whose cues are all placed but which carries
another warning, such as `PACE_FAST`. The flow refuses to time a segment with an unplaced cue
(Part 3). Among warned takes, the rule should prefer those with every cue placed.

### V3: withdrawn (2026-09-26)

V3 asked for an identifier style set per keyword, so that "body 201" and "round 48" would be read
differently. How this flow reads its identifiers is its own choice, and its converter makes it
(Part 3).

### V4: withdrawn (2026-09-26)

V4 asked that the service's tests be built on this flow's current captions. The forms it listed ("Tank
2's", "Tanks 1 and 3", "0.50 J", a bare "J", a unit before a word that is no noun, a title line
with no full stop) are now the tests of this flow's converter (Part 3). A normaliser the service
keeps for other callers is tested on its own material.

### R1, read for Phase 0

The service publishes its measured boundary error, as R1 now asks. This flow bears up to 0.2 s at
p95 (Part 3); the figure is information for the designer and no requirement on the service.

### For information: the paragraphs' spoken length (R8)

Numbers grow when read as words. By a rough expansion, the longest paragraph of the current
script (the first of scene 11) is 391 written and about 620 spoken characters. That is past the
ladder's top rung of 560. Five of the 25 paragraphs are over 350 spoken. The flow therefore splits
at caption boundaries against the voice's `max_segment_chars`, using the spoken text it
makes itself (Part 3). It would help if `SEGMENT_TOO_LONG` carried each offender's
spoken length and the limit, which any caller that splits could use.

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
- **The spoken form is made here** (ruling 2, R6). The stage turns each caption into the words
  to be spoken by its own rules, which are this flow's choices and the owner's to rule on:
  - numbers in British words ("three thousand two hundred and seven", "zero point five four",
    "fourteen per cent"), with no comma inside a spoken number;
  - identifiers after the words body, tank, round, seed and line, read in the style the owner
    rules for each (the recommendation is digits for bodies, "body two oh one", and cardinals for
    the rest, "round forty-eight");
  - the units of these captions (s, m, J, "J a second", m/s, m³) and their symbols;
  - invented names from a pronunciation list kept in this repository: each term, how it should
    sound, and the respelling that gets the voice there, sent to the service as its
    pronunciation hints. The owner approves each by ear from the service's auditions.

  Its tests are the forms the current captions hold: "Tank 2's", "Tanks 1 and 3", "0.50 J", a bare
  "J", a unit before a word that is no noun ("157 s ago", "4,200 s old"), and a title line with no
  full stop. The stage marks each number span as exact (R14), and writes the caption-to-spoken
  table beside the script for the owner's review.
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
- **What the design's revision 3 and the separation rule add to this side**, none of it built yet:
  - the converter above, with its tests;
  - `segments` splits a paragraph at a caption boundary when its spoken text is longer than the
    voice's `max_segment_chars`, which the service publishes;
  - the call step copies each selected take into `narration/`, checks its sha256, and refuses a
    segment whose cue has no time;
  - the flow bears a cue boundary error up to 0.2 s at p95, since a caption changes in the pause
    between two cues; above that, each caption is padded by the service's published error;
  - `story-flow.py approve script` records the cut and the takes the owner approved at the review.
    It reads no approval from the service;
  - `scripts/sweep-orphans.ps1` knows the service's daemon (`-m narration.daemon`) and its workers
    (`-m narration_worker`) by their command lines, which the design documents, and lists them
    apart from strays; the daemon exits on its own after 15 idle minutes;
  - the script stage estimates each paragraph's spoken length and warns before the voice's limit.
- **Without the service** the stage is skipped (`story-flow.py skip narrate`), the captions keep the
  writer's timing and the reading-pace rule, and the film carries music only.
