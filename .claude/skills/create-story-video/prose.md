# prose

Turns the approved story into the words of the film, as prose, and gets the owner's approval of them.

The rules of the film live in the steps before this one: `facts.md` holds what is true, and `story.md`
what happens, in what order, and where each idea is explained. This step makes it alive, so its writer
works from a feeling and an example rather than from rules, and its checks charge only what the truth
and the voice require. The owner ruled on 2026-10-07 that the prose has to flow and be exciting and
compelling, that rules belong to the facts and the outline, and that a redraft made for correctness
must not kill good prose.

## Deliverable

`prose.md` in the film's folder: the narration's words the owner approved, and nothing else. It exists
only once the owner has approved a draft. The script step reads it and keeps its words unchanged.

It holds a title line; then, for every beat of `story.md` in its order, a heading with the beat's
number and name, `## 1. The hook`, and the prose spoken over that beat; and last, the writer's notes
under `## Notes`, which are not spoken. It holds no pictures, citations or timings.

It is done when the owner has approved a draft in words saved as they wrote them.

## Working files

In the film's folder, beside `facts.md` and `story.md`. Drafts are numbered from 1.

- `prose-<n>.md`: draft n, the writer's, a copy of its last pass.
- `prose-<n>-pass-<k>.md`: the writer's passes on draft n, each with what it found wrong at its end
  (prose-brief.md, "Write it in passes"), kept so the owner can see how the draft got there.
- `prose-<n>-key-lines.md`: three versions each of the opening line, the turn, the last line and the
  name the prose gives, if it gives one, until the owner has picked them. A countdown has no turn, so
  its `## Turn` section holds the first sentence under the beat named "Number 1" (tested in
  `scripts/tests/story-video-check/test_key_lines.py`).
- `prose-<n>-questions.md`: the writer's questions for facts `facts.md` does not have, in place of the
  draft, at most once a draft.
- `prose-<n>-check.txt` and `prose-<n>-say.txt`: the check of draft n and its spoken words alone, as the
  tool printed them.
- `prose-<n>-facts-check.md`, `prose-<n>-line-check.md`, `prose-<n>-cold-read.md` and
  `prose-<n>-compare.md`: the checks' answers on draft n, saved word for word.
- `prose-<n>-session-read.md`: the main session's read of draft n (the procedure, "The session's
  read").
- `prose-sample-<s>.md`, with its checks named the same way: a sample of a few beats, when the owner
  asks to hear some before a whole draft is written (the procedure, step 0).
- `prose-<n>-owner.md`: the owner's words and picks on draft n, saved word for word.

## Who does what

| Who | Brief | Reads | Writes |
|---|---|---|---|
| the prose writer | [prose-brief.md](prose-brief.md) | `story.md`, `facts.md`, the draft before and its fact check, every earlier draft's and sample's line check and session's read, every owner's file with the key lines its picks name, and up to three earlier films' `prose.md` | its passes, `prose-<n>.md` and `prose-<n>-key-lines.md`, or `prose-<n>-questions.md` in place of the draft |
| the check | [script-check.py](script-check.py) | one draft, its key lines and `story.md` | what it prints |
| the fact checker | [prose-fact-check-brief.md](prose-fact-check-brief.md) | `facts.md`, one draft and its key lines | its answer |
| the line checker | [prose-line-check-brief.md](prose-line-check-brief.md) | one draft's spoken words, nothing else | its answer |
| the cold listener | [prose-cold-listener-brief.md](prose-cold-listener-brief.md) | one draft's spoken words, nothing else | its answer |
| the comparer | [prose-comparer-brief.md](prose-comparer-brief.md) | `story.md` and one cold read | its answer |
| the facts gatherer and the source checker | [facts-gathering.md](facts-gathering.md) and [facts-source-check-brief.md](facts-source-check-brief.md) | a file of questions or the owner's words on a draft, and the project's files and runs | a section at the end of `facts.md`, and the check of it |
| the main session | this file | the files above | the answers, what the tool prints, its own read of each draft, the owner's words, `prose.md` |
| the owner | | a draft, its key lines, and its checks | the picks, the approval or the changes |

## Launching a subagent

Every subagent but the facts gatherer, whose prompt is in facts-gathering.md, is launched with this
prompt, word for word. Only the parts in angle brackets are filled in: `<checkout>` is the checkout
that holds this file and `<folder>` the film's folder, both as absolute paths, and `<file>` is the file
the table below names. A line marked in parentheses is kept only for the roles the table gives it, and
the mark is not part of the prompt.

```text
You are the <role> of a story film.
Your brief is <checkout>\.claude\skills\create-story-video\<brief>. Read it whole and follow it.
The film's folder is <folder>.                                         (folder)
You write draft <n>.                                                   (version)
The file you read is <folder>\<file>.                                  (the file)
Write only inside the film's folder. Never write in your session scratchpad or in TEMP.   (write)
Your time budget is <minutes> minutes.
```

| Role | Agent | Model | Brief | Minutes | Lines kept | The file |
|---|---|---|---|---|---|---|
| prose writer | general-purpose | opus | prose-brief.md | 30 | folder, version, write | |
| fact checker | Explore | opus | prose-fact-check-brief.md | 10 | folder, the file | `prose-<n>.md` |
| line checker | Explore | opus | prose-line-check-brief.md | 5 | the file | `prose-<n>-say.txt` |
| cold listener | Explore | sonnet | prose-cold-listener-brief.md | 3 | the file | `prose-<n>-say.txt` |
| comparer | Explore | sonnet | prose-comparer-brief.md | 2 | folder, the file | `prose-<n>-cold-read.md` |

The writer is a general-purpose agent because it writes its files. The readers are `Explore` agents,
which get no CLAUDE.md, memory or git status, so the cold listener knows nothing of the project. A
reader writes nothing: its final answer is saved word for word.

## The check

The main session runs it with the Python that runs the project's scripts:

```text
python <checkout>\.claude\skills\create-story-video\script-check.py prose <folder>\prose-<n>.md
python <checkout>\.claude\skills\create-story-video\script-check.py say <folder>\prose-<n>.md
```

The check counts; it cannot hear. It fails a draft only on what the voice can't speak or the science
forbids, with the research behind each in `research/story-video/`:

- a file not in the form `prose-brief.md` gives, or beats that differ from `story.md`'s (a sample's
  are some of them, in their order);
- fewer than 400 spoken words, outside a sample. Above 1,000 it only notes the length: narration that
  leaves the pictures room carries 100 to 130 words a minute of film (narration-writing.md §3.4), but
  the owner ruled on 2026-11-06 that a film may run long when it is interesting and the pictures can
  carry it;
- two short sentences set against each other, one of them negative, and a stock transition such as
  "let's back up". The owner ruled on 2026-11-06, after round 67's first prose sample, that these
  must stop: "The tank never counted the cost." reached the owner after "That's the costly team.";
- in a spoken sentence, and in every version of a key line, digits, symbols, colons, semicolons,
  brackets, double quotation marks or dashes, which a voice misreads (narration-writing.md §1.3,
  §1.6). Double braces round a name we give are the one exception, and they hold a name of one to three
  words starting with a capital; `say` prints the name without them;
- words of purpose or wanting, such as "in order to", "so that", "learned to" and "tried to", which
  teach the commonest wrong idea about evolution (narration-writing.md §2.6);
- key lines that are not three different versions of each line, or whose version 1 is not the draft's
  line, and more than one name between braces.

It notes, without failing, the other habits of model prose (human-not-llm-prose.md Part 3 and 4):
words of praise and appraisal, a closing clause that comments, a short question answered at once, very
long sentences, many numbers in one, a run of sentences of one length, and "not this but that" used
often. A rule obeyed by rewrite kills good prose faster than the habit does, so a note goes to the owner
and the writer changes a noted sentence only where it hears it is wrong (tested in
`scripts/tests/story-video-check/test_notes.py`). The tool cannot tell a short line that carries a fact,
such as "No children.", from one that only lands, so a sentence that says nothing is the line
checker's to find, and the session's. It also prints the number of spoken words and the
spoken time at 145 words a minute.

## The procedure

0. When the owner asks to hear a few beats before a whole draft is written, launch the prose writer for
   a sample instead, with the prompt's version line reading `You write sample <s>, of beats <list>.`
   It writes `prose-sample-<s>.md` with only those beats. Run the check, the fact checker, the line
   checker and the session's read on it, and skip the cold listener and the comparer, which need the
   whole film. A sample fails as a draft does in step 3; then launch the writer for the next sample, and
   after two failed samples in a row go on. Show the owner the sample and its checks, and save their
   words in `prose-sample-<s>-owner.md`, word for word. The owner's words on a sample hold for every
   draft after it, as their words on a draft do.
1. Launch the prose writer for draft n, starting at draft 1. If it wrote `prose-<n>-questions.md` and
   not `prose-<n>.md`, run [facts-gathering.md](facts-gathering.md)'s procedure on that file, a file of
   questions and then its source check, and launch the writer again for the same draft. A draft asks
   once.
2. Run the check on the draft and save what it prints in `prose-<n>-check.txt`. Run `say` on it and save
   what it prints in `prose-<n>-say.txt`.
3. Launch the fact checker on `prose-<n>.md`, and the line checker and the cold listener on
   `prose-<n>-say.txt`, then the comparer on the cold read. Save each answer word for word. Write the
   session's read before you read the line checker's answer. The draft fails when the check's first
   line is `PROSE CHECK: FAIL`, the fact checker's is `FACTS: FAIL`, the line checker's is
   `LINES: FAIL`, the session's read is `READ: CUT`, or the comparer's is `COLD READ: MISMATCH`.
   A line-check flag on a line the owner's words keep, word for word, in a `*-owner.md` file does not
   fail the draft, since the line checker reads no owner's file and cannot know. Once you have read the
   line check, add `## Kept by the owner` at the end of the session's read, below its verdict, with each
   such line and the owner's file that keeps it. When every line the line checker flags is listed
   there, its `LINES: FAIL` does not fail the draft. The owner ruled this on 2026-11-07, after round
   67's drafts 2 to 4 each failed on lines the owner had kept.
4. If the draft fails, go back to step 1 for the next draft. After two failed drafts in a row, go on to
   step 5 with the latest draft.
5. Show the owner the draft, its checks, the check's notes and the fact checker's pedantic faults.
   The checks test facts and clarity, never voice, so never recommend a draft on its checks alone. Say
   whether it sounds like the owner's example scripts, as the session's own reading, and put first any
   line the writer's own producer would still send back. Never offer a fix that keeps every other line
   word for word unless the voice is already right: that freezes the voice of the draft it keeps.
   Ask the owner one question for each section of `prose-<n>-key-lines.md`, if the draft has one, with
   one choice for each version: `Version 1`, `Version 2` or `Version 3` as the label, and the line or
   the name, word for word, as the description. Save the owner's words and picks in
   `prose-<n>-owner.md`, word for word.
   - If the owner approves the draft as it stands, copy `prose-<n>.md` to `prose.md`. The step is done.
   - If the owner asks for changes, picks a version other than 1 of any key line or name, or sends the
     draft back, go back to step 1 for the next draft. The count of failed drafts starts again. If the
     owner's words ask for facts `facts.md` does not hold, first run facts-gathering.md's procedure on
     `prose-<n>-owner.md`, as step 1 does on a writer's questions.

### The session's read

The main session reads every draft and sample before the owner does, and names every line it would
cut. The owner ruled this on 2026-11-06, after round 67's first prose sample reached them with "The
tank never counted the cost." in it: a line the session could see was empty, and passed on because
the flow left quality to the owner alone. It is the one verdict in the flow that is the session's own.
It judges only whether a line says something. Which of two versions is better stays the owner's, since
models judge that badly (research/story-video/human-not-llm-prose.md §1.4).

Read `prose-<n>-say.txt` straight through, as a listener would, before reading any check's answer.
Write `prose-<n>-session-read.md`: the first line `READ: CUT` when you would cut or rewrite any line,
and `READ: CLEAN` otherwise; then each such line, word for word, with the line checker's kind of flag
(prose-line-check-brief.md) and what it should say plainly. The writer of the next draft reads it.

The script step sends the owner's words back here when, on a script, they change the spoken words
(script.md, step 5). The procedure then runs again from step 1 for the next draft after the last one,
and the draft the owner approves replaces `prose.md`.

When a subagent says it reached its time budget, tell the owner.
