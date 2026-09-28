# story

Turns `facts.md` into the film's story, and gets the owner's approval of it.

## Deliverable

`story.md` in the film's folder: the story the owner approved, and nothing else. It exists only once
the owner has approved a draft. The script step reads it.

It holds:

- the question the film asks, and its answer, one sentence each;
- who the story follows: one creature, one line, or the population;
- the beats in order, each in plain sentences, with the word joining it to the beat before and the
  places in `facts.md` it rests on;
- the facts in `facts.md` the story leaves out, and why.

## Working files

In the film's folder, beside `facts.md`:

- `angles.md`: the angles the owner chooses from.
- `pick.md`: the owner's choice, in the owner's words.
- `story-<n>.md`: the drafts, numbered from 1.
- `facts-check-<n>.md`, `cold-read-<n>.md` and `compare-<n>.md`: the checks' answers on draft n,
  saved word for word.
- `owner-<n>.md`: the owner's words on draft n, saved word for word.

## Who does what

| Who | Brief | Reads | Writes |
|---|---|---|---|
| the angles writer | [angles-brief.md](angles-brief.md) | `facts.md` | `angles.md` |
| the story writer | [story-brief.md](story-brief.md) | `facts.md`, `angles.md`, `pick.md`, and what was said about the draft before | `story-<n>.md` |
| the fact checker | [story-fact-check-brief.md](story-fact-check-brief.md) | `facts.md` and one draft | its answer |
| the cold reader | [story-cold-reader-brief.md](story-cold-reader-brief.md) | one draft, nothing else | its answer |
| the comparer | [story-comparer-brief.md](story-comparer-brief.md) | `angles.md`, `pick.md` and one cold read | its answer |
| the main session | this file | the files above | `pick.md`, the answers, the owner's words, `story.md` |
| the owner | | the angles, a draft and its checks | the pick, the approval or the changes |

## Launching a subagent

Every subagent is launched with this prompt, word for word. Only the parts in angle brackets are
filled in: `<checkout>` is the checkout that holds this file, `<folder>` the film's folder, both as
absolute paths. A line marked in parentheses is kept only for the roles the table below gives it,
and the mark is not part of the prompt.

```text
You are the <role> of a story film.
Your brief is <checkout>\.claude\skills\create-story-video\<brief>. Read it whole and follow it.
The film's folder is <folder>.                                         (folder)
You write draft <n>.                                                   (draft)
The draft is <folder>\story-<n>.md.                                    (the draft)
The cold read is <folder>\cold-read-<n>.md.                            (the cold read)
Write only inside the film's folder, and only through the shell. Never write in your session scratchpad or in TEMP.   (write)
Your time budget is <minutes> minutes.
```

| Role | Agent | Model | Brief | Minutes | Lines kept |
|---|---|---|---|---|---|
| angles writer | general-purpose | opus | angles-brief.md | 10 | folder, write |
| story writer | general-purpose | opus | story-brief.md | 15 | folder, draft, write |
| fact checker | Explore | opus | story-fact-check-brief.md | 10 | folder, the draft |
| cold reader | Explore | sonnet | story-cold-reader-brief.md | 3 | the draft |
| comparer | Explore | sonnet | story-comparer-brief.md | 2 | folder, the cold read |

The writers are general-purpose agents because they write their files. The readers are `Explore`
agents, which get no CLAUDE.md, memory or git status, so the cold reader knows nothing of the
project. A reader writes nothing: its final answer is saved word for word.

## The procedure

1. Launch the angles writer. It writes `angles.md`.
2. Ask the owner one question, with one choice for each angle: the angle's name as the label, and its
   lines copied word for word from `angles.md`. The owner can also answer "Other" with an angle of
   their own. Save the answer in `pick.md`, word for word.
3. Launch the story writer for draft 1.
4. Check the draft: launch the fact checker and the cold reader, then the comparer on the cold read.
   Save each answer word for word. The draft fails when the fact checker's first line is
   `FACTS: FAIL` or the comparer's is `COLD READ: MISMATCH`.
5. If the draft fails, launch the story writer for the next draft and go back to step 4. After two
   failed drafts in a row, go on to step 6 with the latest draft.
6. Show the owner the draft and its checks, and save the owner's words in `owner-<n>.md`.
   - If the owner approves it, copy the draft to `story.md`. The step is done.
   - If the owner asks for changes, launch the story writer for the next draft and go back to
     step 4. The count of failed drafts starts again.

When a subagent says it reached its time budget, tell the owner.
