# prose

Turns the approved story into the words of the film, as prose, and gets the owner's approval of them.

## Deliverable

`prose.md` in the film's folder: the narration's words the owner approved, and nothing else. It exists
only once the owner has approved a draft. The script step reads it and keeps its words unchanged.

It holds a title line; then, for every beat of `story.md` in its order, a heading with the beat's
number and name, `## 1. The hook`, and the prose spoken over that beat; and last, the writer's notes
under `## Notes`, which are not spoken. It holds no pictures, citations or timings.

It is done when the owner has approved a draft in words saved as they wrote them. A draft comes to the
owner once the line editor has approved it or had its two rounds and the check, the fact checker and the
comparer have passed it, or after two failed drafts in a row, with the checks that failed it.

## Working files

In the film's folder, beside `facts.md` and `story.md`. Draft n is numbered from 1, and version k of it
from 1 to 3.

- `prose-<n>-v<k>.md`: version k of draft n, the writer's.
- `prose-<n>-key-lines.md`: three versions each of the opening line, the turn, the last line and the
  name the prose gives, if it gives one, until the owner has picked them. A film gives at most one name
  of its own.
- `prose-<n>-v<k>-questions.md`: the writer's questions for facts `facts.md` does not have, in place of
  version k, at most once a version.
- `prose-<n>-edit-v<k>.md`: the line editor's answer on version k, saved word for word.
- `prose-<n>.md`: draft n as it goes to the checks, a copy of its last version.
- `prose-<n>-check.txt` and `prose-<n>-say.txt`: the check of draft n and its spoken words alone, as the
  tool printed them.
- `prose-<n>-facts-check.md`, `prose-<n>-cold-read.md` and `prose-<n>-compare.md`: the checks' answers
  on draft n, saved word for word.
- `prose-<n>-owner.md`: the owner's words and picks on draft n, saved word for word.

## Who does what

| Who | Brief | Reads | Writes |
|---|---|---|---|
| the prose writer | [prose-brief.md](prose-brief.md) | `story.md`, `facts.md`, the version before and its marks, what was said about the draft before, every owner's file with the key lines its picks name, and every `script-<m>-owner.md` | `prose-<n>-v<k>.md` and `prose-<n>-key-lines.md`, or `prose-<n>-v<k>-questions.md` in place of the version |
| the line editor | [line-editor-brief.md](line-editor-brief.md) | one version, its key lines, and on its second round its first marks | its answer |
| the check | [script-check.py](script-check.py) | one draft, its key lines and `story.md` | what it prints |
| the fact checker | [prose-fact-check-brief.md](prose-fact-check-brief.md) | `facts.md`, one draft and its key lines | its answer |
| the cold listener | [prose-cold-listener-brief.md](prose-cold-listener-brief.md) | one draft's spoken words, nothing else | its answer |
| the comparer | [prose-comparer-brief.md](prose-comparer-brief.md) | `story.md` and one cold read | its answer |
| the facts gatherer and the source checker | [facts-gathering.md](facts-gathering.md) and [facts-source-check-brief.md](facts-source-check-brief.md) | a file of questions or the owner's words on a draft, and the project's files and runs | a section at the end of `facts.md`, and the check of it |
| the main session | this file | the files above | the answers, what the tool prints, the owner's words, `prose.md` |
| the owner | | a draft, its key lines, any marks left open, and its checks | the picks, the approval or the changes |

## Launching a subagent

Every subagent but the facts gatherer, whose prompt is in facts-gathering.md, is launched with this
prompt, word for word. Only the parts in angle brackets are
filled in: `<checkout>` is the checkout that holds this file and `<folder>` the film's folder, both as
absolute paths, and `<file>` is the file the table below names. A line marked in parentheses is kept
only for the roles the table gives it, and the mark is not part of the prompt.

```text
You are the <role> of a story film.
Your brief is <checkout>\.claude\skills\create-story-video\<brief>. Read it whole and follow it.
The film's folder is <folder>.                                         (folder)
You write version <k> of draft <n>.                                    (version)
The file you read is <folder>\<file>.                                  (the file)
Write only inside the film's folder, and only through the shell. Never write in your session scratchpad or in TEMP.   (write)
Your time budget is <minutes> minutes.
```

| Role | Agent | Model | Brief | Minutes | Lines kept | The file |
|---|---|---|---|---|---|---|
| prose writer | general-purpose | opus | prose-brief.md | 15 for version 1, 10 after | folder, version, write | |
| line editor | Explore | opus | line-editor-brief.md | 5 | folder, the file | `prose-<n>-v<k>.md` |
| fact checker | Explore | opus | prose-fact-check-brief.md | 10 | folder, the file | `prose-<n>.md` |
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

The check counts; it cannot hear. The line editor, the cold listener and the owner judge the rest. It
refuses, with the research behind each rule in `research/story-video/`:

- a file not in the form above, or beats that differ from `story.md`'s;
- fewer than 400 or more than 1,000 spoken words. A draft aims at about 800, about five and a half
  minutes of speech, which with its pauses fills a film of five to seven minutes; good prose is never
  cut only to reach 800. Narration that leaves the pictures room carries 100 to 130 words a minute of
  film (narration-writing.md §3.4), so 800 is already at the dense end;
- in a spoken sentence, and in every version of a key line:
  - digits, symbols, colons, semicolons, brackets, double quotation marks or dashes. A listener cannot hear
    them and a voice misreads them (narration-writing.md §1.3, §1.6; human-not-llm-prose.md §1.5).
    Double braces round a name we give are the one exception, and they must hold a name of one to
    three words starting with a capital; `say` prints the name without them;
  - more than 25 words. About 20 is the ceiling for the ear, and the rest is room for the occasional
    longer sentence that runs straight forward (human-not-llm-prose.md Part 4, rule 6);
  - more than one number (narration-writing.md §1.6);
  - words of purpose or wanting, such as "in order to", "so that", "learned to", "tried to" and "needs
    to" (narration-writing.md §2.6);
  - the words and patterns that mark model prose: words of praise and appraisal, dodges of "is", fake
    subtlety, vague agents, hype interjections, a moral at the close, a closing "-ing" clause that
    comments on the sentence, and a question of three words or fewer (human-not-llm-prose.md Part 3);
- two short sentences of one paragraph set against each other, one of them negative: the same figure
  split in two (human-not-llm-prose.md Part 3);
- across the prose, "not this but that" in more than one sentence (human-not-llm-prose.md Part 4,
  rule 5), and three sentences in a row within a word of each other in length (Part 4, rule 6);
- key lines that are not three different versions of each line, or whose version 1 is not the draft's
  line, and names under `## Name` whose version 1 is not a name the prose gives between braces.

It refuses a prose that gives more than one name between braces, too. It also prints the number of
spoken words and the spoken time at 145 words a minute, for the owner.

## The procedure

1. Launch the prose writer for version 1 of draft n, starting at draft 1.

   After every launch of the prose writer, look at what it wrote. If it wrote
   `prose-<n>-v<k>-questions.md` and not `prose-<n>-v<k>.md`, run [facts-gathering.md](facts-gathering.md)'s
   procedure on that file, a file of questions and then its source check, and then launch the prose
   writer again for the same version. A version asks once.
2. The line editor has at most two rounds. Save each of its answers in `prose-<n>-edit-v<k>.md`,
   word for word.
   - Launch it on version 1. If its first line is `EDIT: APPROVED`, version 1 is the draft. If it is
     `EDIT: CHANGES`, launch the prose writer for version 2.
   - Launch it on version 2. If its first line is `EDIT: APPROVED`, version 2 is the draft. If it is
     `EDIT: CHANGES`, launch the prose writer for version 3. Version 3 is the draft, and the editor's
     marks on version 2 go to the owner with it.
3. Copy the draft's version to `prose-<n>.md`. Run the check on it and save what it prints in
   `prose-<n>-check.txt`. Run `say` on it and save what it prints in `prose-<n>-say.txt`.
4. Launch the fact checker on `prose-<n>.md` and the cold listener on `prose-<n>-say.txt`, then the
   comparer on the cold read. Save each answer word for word. Every draft goes through every check,
   whatever the others found. The draft fails when the first line of `prose-<n>-check.txt` is
   `PROSE CHECK: FAIL`, the fact checker's is `FACTS: FAIL`, or the comparer's is `COLD READ: MISMATCH`.
5. If the draft fails, go back to step 1 for the next draft. After two failed drafts in a row, go on to
   step 6 with the latest draft.
6. Show the owner the draft, its checks, and any editor's marks left open. Ask the owner one question
   for each section of `prose-<n>-key-lines.md`, if the draft has one, with one choice for each
   version: `Version 1`, `Version 2` or `Version 3` as the label, and the line or the name, word for
   word, as the description. Save the owner's words and picks in `prose-<n>-owner.md`, word
   for word.
   - If the owner approves the draft as it stands, copy `prose-<n>.md` to `prose.md`. The step is done.
   - If the owner asks for changes, picks a version other than 1 of any key line or name, or sends the
     draft back, go back to step 1 for the next draft. The count of failed drafts starts again. If the
     owner's words ask for facts `facts.md` does not hold, first run facts-gathering.md's procedure on
     `prose-<n>-owner.md`, as step 1 does on a writer's questions.

The script step sends the owner's words back here when, on a script, they change the spoken words
(script.md, step 5). The procedure then runs again from step 1 for the next draft after the last one,
and the draft the owner approves replaces `prose.md`.

When a subagent says it reached its time budget, tell the owner.
