# narration

Turns the approved script into the film's narration: the script's words spoken in the film's voice, cut
into pieces, placed on the film's timeline with the padding between passages, and every sentence timed.

## Deliverable

In the film's folder, once the owner has approved a draft by ear:

- `narration/<piece>.wav`: the pieces, each a stretch of a take the voice read, cut out unchanged. They
  stay separate files and are composed in the final edit.
- `narration.json`: each piece's place on the film's timeline, and each passage's and each sentence's
  start and end on it, padding included, in the form [narration-schema.md](narration-schema.md) fixes.
  The shot list and the edit work from this one timeline.

The whole narration joined into one file exists only as a draft, for the owner to listen to. It is not
handed over.

When this session cannot reach the service, the owner picks one of three routes, set out under "Without
the service". They can get the service, bring their own files, one a passage, or skip the narration for
now with an estimate of the timeline and no sound.

The narration speaks the script's words exactly, as `script-check.py say` prints them. A name between
double braces is spoken without its braces. Nothing is reworded here. A word the owner wants changed goes
back to the prose step, then the script step, and this step starts again from the new script.

The timeline is this step's. The owner asked for each scene's timing from the narration and its padding.
The shot list may cut a passage into shots within it.

The step is done when the owner has approved, by ear, a draft whose check passed, in words saved as they
wrote them, and `keep` has written the deliverable. A draft whose check failed never becomes
`narration.json`. On the estimate route the step is done when the estimate is written, since there is
nothing to hear.

## Segments and pieces

A **segment** is what the voice reads at once: one prose paragraph of `prose.md`, since the script's
sentences are prose.md's in order. The delivery depends on context, so the voice reads as much as it
reliably can, and the service takes no unspoken context. A paragraph longer than the voice's measured
`max_segment_chars` is cut at the last passage end that fits. A passage longer than that is cut at the
last sentence end that fits. The service reads longer text, but its pace starts to drift, so the limit is
this skill's rule. A single sentence longer than the limit cannot be cut, so the tool refuses the request;
only the prose step can shorten it.

A **job** is what the service is sent at once: one beat's segments. A beat of more than ten segments is
cut into even jobs of ten or fewer, since the service's answer for a larger job is too long to read at
once.

A **piece** is a file cut from a take. The tool cuts every take at every passage boundary where the voice
left a clean pause. A clean pause has four marks:

- both sentences were placed;
- the service found a pause there: no `CUE_BOUNDARY_NO_PAUSE` on that boundary, and, once the service
  has measured its alignment error, no `CUE_ALIGNMENT_DISAGREE` either. Until then the thresholds of its
  aligners are placeholders, so a disagreement is printed as a warning and does not fail the cut;
- the pause is at least `min_pause_s` long;
- the tool's own reading of the samples finds it silent.

A piece is an exact stretch of its take's samples. It keeps at most `edge_s` of silence at each edge: at
a cut, before the take's first word and after its last. It changes nothing else.

Where a cut fails, the two passages stay in one piece, with the voice's own pause between them. The edit
cannot widen that pause. When that boundary needs silence, the next draft splits the segment there, so
that the voice ends its segment on that passage. A boundary needs silence when the passage before it has a
`Hold:`, or when a passage with no `Say:` lies between. Nothing else is split, because every split costs
the voice context.

## The timeline

Four padding values are in the film's `narration-settings.json`. The timeline, in order:

- The first word falls at `lead_s`, after the holds of any silent passages before it.
- Before a piece that starts a new passage, the silence from the last word to its first word is
  `between_s`, plus the earlier passage's hold, plus the holds of any silent passages between. A hold is
  always added on top of the padding.
- Between two pieces inside one passage, the silence is `within_s`.
- The narration ends `end_s` after the last word and its hold, plus the holds of any silent passages
  after it.

The settings must keep the pieces apart. `lead_s` is at most `between_s`. `lead_s` and `end_s` are at
least `edge_s`, and `between_s` and `within_s` at least twice `edge_s`. The tool refuses settings that
break these rules.

Each passage has a span on the timeline, and the spans tile it with no gap:

- A span starts `lead_s` before the passage's first word when the passage starts a piece.
- A span starts the smaller of `lead_s` and half the voice's pause before the first word when the passage
  starts inside a piece.
- The first span starts at 0.
- A passage with no `Say:` spans its hold.

A span ends where the next one starts. That span is the passage's timing for the shot list and the edit.
On the owner's files and on an estimate, every passage with words starts a piece of its own.

## The service

The voice is the narration service, a local MCP server registered as `narration`. It runs on this
machine's GPU, and per its documents sends nothing off it. It keeps its cache and reports in its own
store, outside the film's folder. It speaks segments of cues in one voice. It returns each take as a WAV,
with every cue's start and end in it, and QA's verdict and flags. Here each sentence is a cue.

The skill's `narration-settings.json` and `pronunciation.json`, beside this file, are the owner's
defaults.

- `narration-settings.json` holds:
  - the film's canon voice: the clip's path, its sha256, its exact transcript, the engine profile hash it
    was measured under, and its `max_segment_chars`;
  - the padding;
  - `min_pause_s` and `edge_s`;
  - `takes`, how many readings of each segment the service makes, of which it suggests one, and
    `max_retakes`;
  - `accept`: the codes of QA flags the owner has ruled do not fail a take in this film, `[]` by default.
    A take the service failed on those codes alone is used as it stands, and the narration records the
    codes against its segment. `collect` reads the list as the film's copy holds it when it runs, since the
    owner may rule after hearing a draft, and a check fails a narration whose codes the list no longer
    holds.
- `pronunciation.json` holds respellings for names the voice says wrongly. A hint gets a `respell` only
  once the owner has heard the name.

The tool sends every name it finds in a job's words as a hint with no respelling: every word with a
capital inside a sentence, and every name given between double braces. Without a hint, the service's
speech recognition mishears a name, fails the take, and has it read again for nothing. A respelling in
`pronunciation.json` replaces the plain hint.

The tool copies both files into the film's folder the first time it runs for the film, on any route,
unless they are there. From then on the step reads and changes only those copies. The skill's own files
change only when the owner asks. The canon voice is chosen, locked in and measured outside this skill.
Until the settings name it, `request` refuses, and the step cannot run on the service.

## Without the service

When this session cannot reach the service, the owner picks one of three routes:

- **Get the service.** It can be got from https://github.com/avivshaked/narration-mcp. Once it is set up,
  the step starts again.
- **Bring narration files.** The owner puts one WAV for each passage that has words in the folder
  `narration-own` in the film's folder, named by the passage's id, such as `2.4.wav`, and reading that
  passage's words as `script-check.py say` prints them. Each file becomes one piece, named `p` and the
  passage's id (`p2.4`), copied unchanged. The tool places each file on the timeline with the padding and
  the holds, as it places pieces. Nothing times the sentences inside a file, so each sentence's time is
  left empty, and the shot list places its shots by passage.
- **Skip the narration for now.** The tool writes the timeline from an estimate: each sentence at 145 words
  a minute, with the padding and the holds, and no sound. The shot list and the edit can go ahead from it,
  but every time moves when a real narration replaces it, and the shot list is then made again.

`narration.json` names its route in `source`, as `service`, `own files` or `estimate`, so that no later
step takes an estimate for a narration.

## Working files

In the film's folder. Draft n is numbered from 1, and the narrator's run k from 1. When the step starts
again in a folder that already holds drafts, on any route, the numbers go on from the highest there, so
that no draft is written over.

- `narration-settings.json` and `pronunciation.json`: the film's copies.
- `narration-<n>-request.json`: the `submit_job` arguments for each of draft n's jobs, as the tool wrote
  them.
- `narration-<n>-plan.json`: the segments, the sentences each holds, and the attempts, as the tool wrote
  them.
- `narration-<n>-next.json`: what `collect` found the next draft needs: the segments that need new
  attempts and the passages to split after, written whenever it could read the draft's plan and takes.
- `narration-<n>-request.txt`, `narration-<n>-machine-<k>.txt`, `narration-<n>-check.txt`,
  `narration-<n>-sheet.txt` and `narration-keep.txt`: what the tool printed.
- `narration-<n>-machine-owner-<k>.md`: the owner's words on a busy machine before run k, saved word for
  word, with their words at a later check for the same run added below.
- `narration-<n>-run-<k>.md`: the narrator's answer on run k, saved word for word.
- `narration-<n>-wait-owner-<k>.md`: the owner's words on waiting longer after run k, saved word for word.
- `narration-<n>-takes-<job>.json`: the takes the service returned for each job, as the narrator copied
  them.
- `narration-<n>/`: draft n's pieces. `narration-<n>.json`: their timeline. `narration-<n>.wav`: the
  whole draft, joined, for listening.
- `narration-<n>-owner.md`: the owner's words on draft n, saved word for word, with any later answer
  about the draft added below them.
- `narration-<n>-redo.txt`: the passages the owner asked to hear read again in draft n, one id a line,
  copied from their words on the draft before.
- `narration-route-owner.md`: the owner's words on the route, when the service cannot be reached, saved
  word for word, with their words each later time added below.
- `narration-own`: the owner's own files, on that route.
- `narration-estimate.txt`: what the tool printed, on the estimate route.

A draft's audio is about 120 MB. Old drafts are removed only on the owner's word.

## Who does what

| Who | Brief | Reads | Writes |
|---|---|---|---|
| the tool | [narration.py](narration.py), whose commands [narration-tool.md](narration-tool.md) sets out | `script.md` through `script-check.py`'s parser, `prose.md`, the film's settings and pronunciations, the processes on the machine; for `request`, this draft's redo list, every earlier draft's plan and takes, and the last draft's next file; for `collect`, this draft's plan and every job's takes, and the take files in the service's store; for `own`, `narration-own`; for `check`, `sheet` and `keep`, a draft's timeline and pieces | what it prints; the film's settings and pronunciations, copied the first time; draft n's request, plan, next file, pieces, timeline and joined file; on the estimate route, `narration.json` at once; on approval, the deliverable |
| the narrator | [narrator-brief.md](narrator-brief.md) | `narration-<n>-request.json` | `narration-<n>-takes-<job>.json` for each job, and its answer |
| the main session | this file | what the tool prints, the answers, the owner's words | those, saved; the redo list; the film's settings and pronunciations, changed to the owner's words |
| the owner | | `narration-<n>.wav`, the timing sheet, the check, and what the service asks to be heard first | the route without the service; the approval, or what the next draft should change: passages to read again, a respelling or new padding; a go on a busy machine; whether to wait longer, and whether to go on after the third draft |

No AI judges the narration. The tool checks what can be counted, and the owner listens.

## Launching the narrator

The narrator is launched with this prompt, word for word. Only the parts in angle brackets are filled in:

- `<checkout>` is the checkout that holds this file, as an absolute path;
- `<folder>` is the film's folder, as an absolute path;
- `<n>` is the draft's number;
- `<k>` is the run's number.

```text
You are the narrator of a story film.
Your brief is <checkout>\.claude\skills\create-story-video\narrator-brief.md. Read it whole and follow it.
The film's folder is <folder>.
You narrate draft <n>, run <k>.
Write only inside the film's folder, and only through the shell. Never write in your session scratchpad or in TEMP.
Your time budget is 60 minutes.
```

It is a general-purpose agent on sonnet, because it calls the service's tools, writes its file and
judges nothing (SKILL.md's rule on models). It runs in
a subagent so that the service's long answers stay out of the main session. It waits with `get_job`'s own
wait, which the server holds for up to 55 seconds a call, never with a shell loop.
## The procedure

Run the tool with the Python that runs the project's scripts, as
`python <checkout>\.claude\skills\create-story-video\narration.py <command> …`. Every command prints its
verdict on its first line and how long it took on its last; [narration-tool.md](narration-tool.md) lists
each command's verdicts.

1. Call the service's `get_server_status`. If this session has no such tool, or the call gets no answer
   from the server, tell the owner that the narration service cannot be reached from this session, and
   offer the three routes of "Without the service". Save their words in `narration-route-owner.md`, word
   for word, below any earlier answer.
   - **Get the service:** stop. The step starts again once it is set up.
   - **Bring narration files:** go on under "On the owner's files", below.
   - **Skip the narration for now:** run `estimate <folder>` and save what it prints in
     `narration-estimate.txt`. On `NARRATION ESTIMATE: WRITTEN` it has written `narration.json`: tell the
     owner that its times are an estimate, and the step is done. On `NARRATION ESTIMATE: REFUSED`, show
     the owner what it printed and stop.
2. Run `request <folder> <n>` for draft n, numbered as "Working files" says, and save what it prints in
   `narration-<n>-request.txt`.
   - It reads this draft's `narration-<n>-redo.txt`, if there is one, every earlier draft's plan and
     takes, and the last draft's next file.
   - A segment whose words are unchanged keeps its attempt, so the service answers it from its cache. A
     segment the last draft found failed, or that holds a passage the owner asked to hear again, gets a new
     attempt. A segment is split after a passage whose needed pause failed, and the split stays in every
     later draft of the same `script.md`.
   - If its first line is not `NARRATION REQUEST: READY`, show the owner what it printed and stop. A
     settings file that names no canon voice is refused here.
3. Run `machine` and save what it prints in `narration-<n>-machine-<k>.txt`, for run k from 1.
   - On `MACHINE: BUSY`, tell the owner you are blocked on the machine and give what it listed. Ask whether
     to go ahead, and save their words in `narration-<n>-machine-owner-<k>.md`.
   - Go on only on `MACHINE: FREE` or the owner's yes. On a no, stop; the step resumes at this step when
     the owner says so. A check made again for the same run adds its printout and the owner's words below
     the earlier ones, in the same files.
4. Launch the narrator for run k. Save its answer in `narration-<n>-run-<k>.md`, word for word.
   - `NARRATION: DONE`: go on to step 5.
   - `NARRATION: WAITING`: go back to step 3 for run k+1. The same request returns each job while it is
     queued or running. After run 3, show the owner the answers, ask whether to wait longer, and save
     their words in `narration-<n>-wait-owner-<k>.md`. A yes allows three more runs, and then the owner
     is asked again. A no stops the step and leaves the job as it is.
   - `NARRATION: REFUSED` or `NARRATION: NOT SUBMITTED`: show the owner the answer and stop. A voice that
     is not measured, a changed engine, or a service that is not accepting work are the owner's to rule
     on.
   - Any other answer: show the owner the answer and stop.
5. Run `collect <folder> <n>` and save what it prints in `narration-<n>-check.txt`. It writes
   `narration-<n>-next.json` whenever it could read the draft's plan and takes. Its first line is one of:
   - `NARRATION CHECK: PASS`: it cut the pieces, built the timeline and joined the draft for listening,
     and the check passed. Go on to step 7.
   - `NARRATION CHECK: FAIL`: it wrote the same files, but the check failed, a take failed its QA, or a
     needed pause failed. Go on to step 6.
   - `NARRATION CHECK: REFUSED`: the takes could not be used, and it wrote no draft. Go on to step 6.
6. If `collect` names a cue the service can never place (its reason is `no_alignable_words`), show the
   owner what it printed and stop. Only new words can fix it, so the prose step and then the script step
   run again, and this step starts again from the new script. Otherwise go back to step 2 for draft n+1.
   After two drafts in a row that failed or were refused, go on to step 7 with the latest draft instead,
   if `collect` wrote one. If it wrote none, show the owner the check, ask what the next draft should
   change, save their words in `narration-<n>-owner.md`, and act on them as step 7's branches say.
7. Show the owner:
   - the full path of `narration-<n>.wav`, as plain text;
   - the sheet from `sheet <folder>\narration-<n>.json`, saved in `narration-<n>-sheet.txt`;
   - the check;
   - what the service asks to be heard first.

   If the check passed, ask whether they approve draft n as it stands. If it failed, say that this draft
   cannot become the narration, and ask what the next draft should change. From the third draft they have
   heard, ask too whether to go on. Save their words in `narration-<n>-owner.md`, word for word.
   - **Approved, and the check passed:** run `keep <folder> <n>` and save what it prints in
     `narration-keep.txt`. On `NARRATION KEEP: DONE` it has copied draft n's pieces into `narration/`,
     checked their hashes and written `narration.json`, and the step is done. On
     `NARRATION KEEP: REFUSED`, show the owner what it printed and stop.
   - **Passages to read again:** copy their ids into `narration-<n+1>-redo.txt`, one a line, and go back to
     step 2 for draft n+1. If their words do not name a passage plainly, ask them which, and add their
     answer to `narration-<n>-owner.md`.
   - **A respelling or new padding:** change the film's copy to their words, show them the change, and go
     back to step 2 for draft n+1. A change to padding alone reads nothing again, since the service answers
     every unchanged segment from its cache.
   - **A QA flag to accept:** add its code to `accept` in the film's copy, show them the change, and run
     step 5 again on draft n. Nothing is read again.
   - **No change, after a failed check:** go back to step 2 for draft n+1, which takes what the check found.
   - **Words changed:** the prose step, then the script step, run again, and this step starts again from
     the new script.
   - **Not to go on:** stop. A yes to going on allows three more heard drafts, and then the owner is asked
     again.

Tell the owner when a narrator's answer says it stopped at its time budget, and when the tool's work on a
draft, by the times it prints, passes 5 minutes.

### On the owner's files

1. Once the owner says the files are in `narration-own`, run `own <folder> <n>` for draft n, numbered as
   "Working files" says, and save what it prints in `narration-<n>-check.txt`.
2. On `NARRATION CHECK: REFUSED` it wrote no draft. Show the owner what it printed, and run it again once
   they say the files are mended.
3. On `NARRATION CHECK: PASS` or `FAIL`, go on to step 7 above. There, every branch that makes a new
   draft runs `own <folder> <n+1>` in place of step 2. It waits for the owner's word that the files are
   replaced, except after a padding change alone, which runs it at once. There is nothing to respell and
   nothing for a service to read again.

## Before the first film on the service

The service has not yet run under this skill. The first film on it runs these checks in order, and row 5
of [SKILL.md](SKILL.md) is marked implemented only after them.

1. **Wiring.** The owner adds `narration` to the project's MCP configuration and allows its tools. Step
   1's access check then passes, and fails with the link when the server is removed. An engine profile
   shows `installed` and `env_ok`.
2. **The narrator's access.** A general-purpose subagent can call `get_server_status`, and `get_job` with
   `wait_s` 55 on an ended job.
3. **The machine.** `machine` reads `MACHINE: FREE` on an idle machine, and `MACHINE: BUSY` with a farm
   run or a batchmode Unity going. The voice's own worker shows as a compute process on the card and is
   not counted.
4. **The voice.** The owner picks it and has it measured when they choose. The settings take its
   `max_segment_chars` and its engine profile hash.
5. **A dry run.** Round 49's jobs, each sent with `dry_run` true, draws no `LIMIT_EXCEEDED`, and every
   echoed cue is its plan's sentence.
6. **One beat.** In a test folder whose story, prose and script hold beat 1 alone, run the narrator and
   then `collect`. At every passage boundary, compare the tool's silence reading with the service's
   pauses. Listen to each cut edge and to the joined draft at the sheet's times, and set `min_pause_s` and
   `edge_s` from what was heard.
7. **A needed pause that fails.** Force one, with a hold on a boundary the service flags or with a high
   `min_pause_s`. Draft 2 splits the segment there, and every other segment comes from the cache with the
   same take ids.
8. **A redo.** Redo one passage in draft 3. Only its segment gets a new attempt.
9. **The whole film.** The owner listens and approves, `keep` writes the deliverable, and `check` passes.
10. **Cleanup.** `scripts/sweep-orphans.ps1` names the service's processes while they run, and they exit
    when idle.