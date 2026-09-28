# script

Turns the approved prose into the film's script: the prose cut into passages, each with its picture,
the facts behind it, any words on screen and any pause.

## Deliverable

`script.md` in the film's folder: the script the owner approved, and nothing else. It exists only once
the owner has approved a draft, since the narration speaks from it. The narration step and the shot list
read it.

Its form is fixed by [script-schema.md](script-schema.md), so that a tool turns it into data with no
judgement: `script-check.py json` prints that data. It holds:

- the question and the answer, word for word from `story.md`;
- the story's beats in order, each cut into passages, a passage being the sentences heard over one
  picture;
- for each passage: its sentences, exactly as `prose.md` has them; the lines of `facts.md` that hold
  them; the picture, with where to find it (the run, the bodies, the seconds, and the lines of
  `facts.md` they come from); the few words shown on screen, if any; and how long the picture holds
  without words after them;
- notes for the owner, which are neither spoken nor shown.

The script's directing is a first pass. The narration adds each sentence's timing, and the shot list then
directs the film again with those timings in hand: it may change a passage's picture and its words on
screen, and cut a passage into several shots. The spoken words, the holds and the passage numbers stay as
the script has them, since the narration speaks the words, leaves the holds as silence and is keyed on the
passage numbers.

It is done when the owner has approved a draft whose check passed, in words saved as they wrote them. A
draft comes to the owner once the check's first line is `SCRIPT CHECK: PASS` and the composer's review's
is `REVIEW: PASS`, or after two failed drafts in a row, with the check and the review that failed it. A
draft whose check failed never becomes `script.md`: the later steps read the script through
`script-check.py json`, which prints nothing for a draft out of form, and take whatever it prints as
checked.

## Working files

In the film's folder, beside `facts.md`, `story.md` and `prose.md`:

- `script-<n>.md`: draft n, numbered from 1.
- `script-<n>-review.md`: the composer's answer on draft n, its review, saved word for word.
- `script-<n>-check.txt`: the check of draft n, as the tool printed it.
- `script-<n>-owner.md`: the owner's words on draft n, saved word for word.
- the files facts-gathering.md's procedure writes, when the owner's words ask for facts.

## Who does what

| Who | Brief | Reads | Writes |
|---|---|---|---|
| the composer | [composer-brief.md](composer-brief.md) | `prose.md`, `story.md`, `facts.md`, the schema, the draft before with its check and its review, and every `script-<m>-owner.md` | `script-<n>.md`, and its review as its answer |
| the check | [script-check.py](script-check.py) | one draft, `prose.md`, `story.md` and `facts.md` | what it prints |
| the prose step | [prose.md](prose.md) | the owner's words on a draft that change the spoken words | a new `prose.md` the owner approved |
| the facts gatherer and the source checker | [facts-gathering.md](facts-gathering.md) and [facts-source-check-brief.md](facts-source-check-brief.md) | the owner's words on a draft, and the project's files and runs | a section at the end of `facts.md`, and the check of it |
| the main session | this file | the files above | the review, what the tool prints, the owner's words, `script.md` |
| the owner | | a draft, what its check printed and its review | the approval or the changes |

## Launching the composer

The composer is launched with this prompt, word for word. Only the parts in angle brackets are filled
in: `<checkout>` is the checkout that holds this file and `<folder>` the film's folder, both as absolute
paths, and `<n>` the draft's number.

```text
You are the script composer of a story film.
Your brief is <checkout>\.claude\skills\create-story-video\composer-brief.md. Read it whole and follow it.
The film's folder is <folder>.
You write draft <n>.
Write only inside the film's folder, and only through the shell. Never write in your session scratchpad or in TEMP.
Your time budget is 15 minutes.
```

It is a general-purpose agent on opus, because it writes its file.

## The procedure

1. Launch the composer for draft n, starting at 1. Save its answer in `script-<n>-review.md`, word for
   word.
2. Run the check on `script-<n>.md` and save what it prints in `script-<n>-check.txt`, with the Python
   that runs the project's scripts:

   ```text
   python <checkout>\.claude\skills\create-story-video\script-check.py script <folder>\script-<n>.md
   ```

3. If the check's first line is `SCRIPT CHECK: PASS` and the review's is `REVIEW: PASS`, go on to
   step 5.
4. Otherwise, go back to step 1 for the next draft. After two failed drafts in a row, go on to step 5
   with the latest draft.
5. Show the owner the draft, what its check printed and its review, and ask whether they approve the
   draft as it stands. If its check failed, say that it cannot become `script.md` as it stands. If it
   failed on a file this step does not write, `prose.md`, `story.md` or `facts.md`, say which, since only
   that file's step can fix it. Save the owner's words in `script-<n>-owner.md`, word for word.
   - If the owner approves the draft as it stands and its check passed, copy `script-<n>.md` to
     `script.md`. The step is done.
   - If the owner's words change the spoken words, the change belongs to the prose step, since the
     composer copies the prose word for word and the check refuses anything else. Run prose.md's
     procedure again for its next draft, whose writer reads `script-<n>-owner.md` with the owner's
     other words. Once the owner has approved the new `prose.md`, go back to step 1 here for the next
     draft. The count of failed drafts starts again.
   - Otherwise, go back to step 1 for the next draft. The count of failed drafts starts again. If the
     owner's words ask for facts `facts.md` does not hold, first run facts-gathering.md's procedure on
     `script-<n>-owner.md`, a file of questions and then its source check.

When the composer says it reached its time budget, tell the owner.
