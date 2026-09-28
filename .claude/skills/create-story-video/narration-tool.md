# narration.py

`narration.py`, beside this file, is the narration step's tool. [narration.md](narration.md) says when
each command runs, and [narration-schema.md](narration-schema.md) fixes what it writes. It uses the
standard library only, and reads the script through `script-check.py`'s own parser. It has been tested on
made-up takes and files, and not yet on the service.

Every command prints its verdict on its first line and how long it took on its last. It exits 0 on a
verdict that passes, 1 on any other, and 2 on a command it does not know. A file it cannot parse, or one
not in its form, gives the command's refusal and the error, never a traceback.

| Command | Its first line |
|---|---|
| `request <folder> <n>` | `NARRATION REQUEST: READY` or `REFUSED` |
| `machine` | `MACHINE: FREE` or `BUSY` |
| `collect <folder> <n>` | `NARRATION CHECK: PASS`, `FAIL` or `REFUSED` |
| `own <folder> <n>` | `NARRATION CHECK: PASS`, `FAIL` or `REFUSED` |
| `estimate <folder>` | `NARRATION ESTIMATE: WRITTEN` or `REFUSED` |
| `check <narration json>` | `NARRATION CHECK: PASS` or `FAIL` |
| `sheet <narration json>` | `NARRATION SHEET:` and the file's name, route and length, or `REFUSED` |
| `keep <folder> <n>` | `NARRATION KEEP: DONE` or `REFUSED` |

A `REFUSED` writes no draft. A `FAIL` from `collect` or `own` writes the draft, so that the owner can hear
it, and says why it cannot become the narration.

`request`, `collect`, `own` and `estimate` refuse unless `script-check.py script` passes on the film's
`script.md`. The first of them to run for a film copies the skill's `narration-settings.json` and
`pronunciation.json` into the film's folder, and each refuses settings that break the rules in
narration.md's "The timeline".

## request

- Refuses while the settings name no canon voice, or name one without its path, sha256, transcript,
  engine profile hash and `max_segment_chars`.
- Makes one segment of each prose paragraph. It splits a paragraph after each passage an earlier draft's
  next file named, then cuts it to the voice's `max_segment_chars`: at the last passage end that fits, or
  at the last sentence end. It refuses a sentence longer than that limit.
- Names each segment `s<passage>.<place>` after its first sentence. Its cues are its sentences, with a
  given name's braces taken off.
- Sets each segment's first attempt. A segment named in the last draft's next file, or holding a passage
  in this draft's redo list, gets one more than the highest attempt any earlier draft used for it. A
  segment with the same sentences as in the last draft keeps its first attempt. Any other gets 0. The
  segment names `takes` attempts in a row from there, and the service reads each.
- Takes the splits and the last draft's next file only from drafts of the same `script.md`.
- Refuses what the service would refuse: over 200 segments, 40 cues in a segment, 600 characters in a cue,
  60,000 characters in a job, 500 hints in a job, or an attempt over 99.
- Groups the segments into jobs, one a beat, and cuts a beat of more than ten segments into even jobs
  of ten or fewer. A job is named `b<beat>`, or `b<beat>-<part>` when its beat is cut.
- Sends each job a hint with no respelling for every name in its words: every word with a capital inside
  a sentence, and every name given between double braces. A respelling in `pronunciation.json` replaces
  the plain hint. It prints the hints sent.
- Applies the service's limits to each job.
- Writes `narration-<n>-request.json`, the `submit_job` arguments of each job with the idempotency key
  `<folder name>-n<n>-<job>`, and `narration-<n>-plan.json`.

## machine

- Prints `MACHINE: BUSY` if any of these runs:
  - `Evosim.Farm.exe`;
  - a `Unity.exe` whose command line holds `-batchmode`;
  - `ffmpeg.exe`;
  - a process that `nvidia-smi` types as pure compute (`C`), unless its command line is the service's own
    `-m narration.daemon` or `-m narration_worker`. Under Windows every program that draws is typed
    `C+G`, and is not counted.
- Prints `MACHINE: BUSY` too when it cannot read the processes, or reads none. Otherwise it prints
  `MACHINE: FREE`.

## collect

- Reads each job's `narration-<n>-takes-<job>.json`, and refuses them for the reasons
  narration-schema.md lists, and when `script.md` changed after the draft's request.
- Fails a cut on `CUE_ALIGNMENT_DISAGREE` only once the takes carry a measured alignment error. Until then
  it prints the flag as a warning.
- Cuts each take at every passage boundary with a clean pause, as narration.md defines it, keeps at most `edge_s` of silence at each
  edge, and places the pieces in whole samples.
- Writes `narration-<n>/`, `narration-<n>.json` and `narration-<n>.wav`, then runs the check.
- Writes `narration-<n>-next.json` whenever it could read the plan and the takes: the segments that need a new attempt, for a missing take, an
  unplaced cue the service may place on another reading, or a QA verdict of `fail`; and the passages to
  split after, where a needed pause failed.
- Takes a take the service failed as if it passed when every code that failed it is in the film's
  `accept`, read from the film's settings as they stand, and prints `accepted:` with the codes.
- Prints `FAIL` when the check fails, a take failed its QA on a code not accepted, or a needed pause
  failed.
- Prints the cuts made and the cuts that failed with why, the warnings, what the service asks to be heard
  first, and the length against five to seven minutes.

## own

- Refuses unless `narration-own` holds one WAV for every passage that has words, named by the passage's
  id, and nothing else. It refuses a file it cannot read or that holds no sound, and files whose channels,
  sample width or rate differ.
- Copies each file unchanged into `narration-<n>/` as the piece `p<id>`, places it, writes the timeline
  and the joined draft, and runs the check.

## estimate

- Refuses when `narration.json` holds a narration that is not an estimate.
- Times each sentence at 145 words a minute, places it with the padding and the holds, runs the check,
  and writes `narration.json`.

## check, sheet and keep

- `check` runs narration-schema.md's checks on a narration file, against the `script.md` and the pieces
  beside it.
- `sheet` prints one line a passage: its id, its start as m:ss.s, its span's length and its first words.
- `keep` refuses a draft whose check fails, a `narration.json` that is not an estimate, or a `narration/`
  folder that holds files. It copies the pieces into `narration/`, checks their hashes, and writes
  `narration.json`.