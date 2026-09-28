# facts-gathering

Collect every interesting fact for the film's scope, each with its provenance, into one file that
the later steps work from, and have a sample of it checked against its sources.

## Input

One or more logbook entries (paths or links), or one or more round numbers. They set the scope.

## What the gatherer does

1. Create the folder `scratch/story-video/<name>-<yyyymmddhhmm>/`: `<name>` is a short descriptive
   name for the scope, and `<yyyymmddhhmm>` the local date and time the folder is made.
2. Read. Reading is free: the logbook entries given (for a round number, the logbook entries that
   report it), any primer entries it judges relevant, and anything else it needs, such as logs and
   runs. The logbook entries are canon.
3. Write `facts.md` in that folder: every interesting fact for the scope, in connection with the
   logbook or primer entries, each with its provenance (the file and the place in it, or for a run,
   what was read and the value it gave). Write each fact as a list item, `- `, ending with its
   provenance in square brackets, since the source check finds the facts that way.
4. Add to `facts.md` a section on the project and its world, written for a viewer who knows nothing
   of it: what the project is, what it is trying to reach, how its world works and what is in it,
   and how this experiment moves the project toward its goal. Read it from the project's own
   documents, such as `README.md`, `DESIGN.md` and the primer, each fact with its provenance as in
   step 3. Gather it fresh for every film, since the world changes between experiments.

## A folder that already exists

When the prompt names a film's folder that already exists and no file of questions and no source check,
and its `facts.md` has no section on the project and its world, do only step 4: add that section at the
end of the file, and change nothing else.

## Questions to answer

When the prompt names a film's folder that already exists and a file of questions in it, answer only
the questions of fact in that file. Read what is needed to answer each, as in step 2, and add one
section to the end of `facts.md`, headed `## Added for <the questions file's name>`, holding the answers
as facts with their provenance, as in step 3. Change nothing else in the file. A question the project's
files and runs cannot answer gets a line saying so and what was read.

A creature's name, its family's size, its generations and its fate come from `scripts/guide.py`: it
names every clade of a run (a founder, or a child whose expressed traits differ from its parent's,
starts one) with a Latin binomial worked out from the founder's genome, and writes a card of facts
for each. Run it with `--out` inside the film's folder, since by default it writes into the run's
directory, and with `--no-economics`, which spares a process start for every founder.

## A source check to answer

When the prompt names a source check, fix only the facts it found not sourced. For each, give it the
place that holds it, correct it to what its place says, or remove it. Change nothing else in the file,
and list in your answer what you changed.

## For the main session

### Who does what

| Who | Brief | Reads | Writes |
|---|---|---|---|
| the facts gatherer | this file | the scope, a questions file or a source check, and the project's files and runs | `facts.md`, or a section added to it, or its fixes |
| the source checker | [facts-source-check-brief.md](facts-source-check-brief.md) | the facts the tool samples from `facts.md`, and the places they cite | its answer |
| the main session | this file | the answers | `sources-check-<k>.md`, the checker's answers saved word for word, numbered from 1 in each film |
| the owner | | a source check that failed twice | the ruling |

### Launching a subagent

Every subagent here is launched with this prompt, word for word. Only the parts in angle brackets are
filled in: `<checkout>` is the checkout that holds this file and `<folder>` the film's folder, both as
absolute paths; `<scope>` the logbook entries' paths or the round numbers; `<file>` the questions file;
and `<k>` the number of the source check. A line marked in parentheses is kept only for the runs the
table below gives it, and the mark is not part of the prompt.

```text
You are the <role> of a story film.
Your brief is <checkout>\.claude\skills\create-story-video\<brief>. Read it whole and follow it.
The film's scope is <scope>.                                            (scope)
The film's folder is <folder>.                                          (folder)
The questions are in <folder>\<file>.                                   (questions)
The source check is in <folder>\sources-check-<k>.md.                   (fix)
Check only the last section of facts.md.                                (last section)
Write only under <checkout>\scratch\story-video\, and only through the shell. Never write in your session scratchpad or in TEMP.   (write)
Your time budget is <minutes> minutes.
```

| Run | Role | Agent | Model | Brief | Minutes | Lines kept |
|---|---|---|---|---|---|---|
| a new film | facts gatherer | general-purpose | opus | facts-gathering.md | 30 | scope, write |
| the section on the project, in a folder that has none | facts gatherer | general-purpose | opus | facts-gathering.md | 10 | folder, write |
| a file of questions | facts gatherer | general-purpose | opus | facts-gathering.md | 10 | folder, questions, write |
| a fix after a failed source check | facts gatherer | general-purpose | opus | facts-gathering.md | 10 | folder, fix, write |
| the check of a whole `facts.md` | source checker | Explore | opus | facts-source-check-brief.md | 10 | folder |
| the check of an added section | source checker | Explore | opus | facts-source-check-brief.md | 10 | folder, last section |

The gatherer is a general-purpose agent because it reads runs and writes `facts.md`. The checker is an
`Explore` agent, which writes nothing: its answer is saved word for word.

### The procedure

1. Launch the facts gatherer for the run that is wanted: a new film, the section on the project, or a
   file of questions.
2. Launch the source checker: on the whole `facts.md` after a new film, and on its last section after
   the section on the project or a file of questions. Save its answer in `sources-check-<k>.md`, word
   for word.
3. If its first line is `FACTS SOURCED: PASS`, the step is done. If it is `FACTS SOURCED: FAIL`, launch
   the facts gatherer on that check to fix the facts it found, and then the source checker again, as
   in step 2. A second `FACTS SOURCED: FAIL` goes to the owner with both checks, and the owner's words
   are saved in `sources-owner-<k>.md`, word for word.

4. When a gatherer's answer names a fault in an earlier section of `facts.md` (a citation that no
   longer lands, a field read as something it is not), write a new file of questions asking for the
   fact from a source that holds it, and run steps 1 to 3 on it. The fix route cannot reach such a
   fault, because a source check samples and may never name it, and the gatherer changes nothing
   outside its own section. Tell the owner which step already used the fact, since the answer can
   change what the script says. (Round 49: the one-part stomach was cited from the lineage field
   `pt`, which is the patch, and was found by the gatherer answering the questions on its dots.)

When a subagent says it reached its time budget, tell the owner.
