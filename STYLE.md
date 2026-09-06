# How we write

*This guide governs every piece of prose in the repository: the logbook, `DECISIONS.md`,
`DESIGN.md`, the primer, `README.md`, `HANDOFF.md`, `CLAUDE.md`, proposal files, and
commit messages. It exists because the owner read logbook/0069 and could not follow it,
and because the record is meant to become a book one day. It is written for two readers
at once: a person reading the research cold, and an agent writing or rewriting a piece
who needs rules it can check. Adopted 2026-09-06. Licensed CC BY 4.0 with the rest of
the prose.*

## 1. Who is talking, and to whom

**The narrator is a person who was there.** When the agent that did the work writes about
it, it says *I*: "I expected the plume to stop at the surface. It didn't." When the text
speaks for the project, its decisions and its rules, it says *we*: "We chose the vent
shape." Never "the agent", never "the record", never the passive voice to avoid saying
who did a thing.

**The reader is intelligent and new.** Assume they can follow an argument and know
nothing about this project. Every term of art is introduced in the sentence that first
uses it, or linked to the glossary in `logbook/README.md`. A run's name, a column's name
or a setting's name is never an explanation.

**Personality is welcome, content is not negotiable.** A joke that is true and short, an
aside that teaches something, an admission of what you expected and how it felt to be
wrong: all of these belong in the logbook and the primer, and a book will need them. They
never belong in a specification, a pre-registration or a decision's rule. An aside is one
paragraph at most and is marked as one ("An aside:" or a parenthesis that opens and
closes in the same paragraph). A joke never replaces the sentence that says what happened.

## 2. The shape of a piece

1. **The point comes first.** Every entry, every section of any length, opens with what
   happened and why it matters, in plain words. A reader who stops after the first
   paragraph should still have the point. Titles state; they do not tease. "The lean dose
   starves the stomachs", not "One box".
2. **Then the story, in the order it happened.** What we expected, what we did, what we
   saw, what we made of it. Say how long things took and what you were doing while
   waiting. Say what you thought before the number came in; that is what makes a result
   a finding rather than a fact.
3. **Then the evidence.** Tables, numbers, the run names, the commands. Every table is
   preceded by a sentence saying what to look for in it.
4. **Then what changes.** The decision, the next round, the open question, and who
   decides (the owner rules on world rules and scope; the agent rules on instruments).
5. **Cause, evidence and consequence get separate sentences.** "The runs parted at step
   147,778. We know because the digest hashes differ from that step. So the shared world
   does not replay." Never all three in one clause.

The pre-registration blocks in the logbook (the hypothesis, the predictions, what would
falsify each, the two-sided readings) are the one exception to everything about shape:
they are written before the run, committed before the run, and never edited after it.
Their density is deliberate.

## 3. Sentences and paragraphs

- **One idea per sentence.** Most sentences under twenty words. A sentence that needs two
  dashes is two sentences. Let a long sentence stand when it earns its length, so the
  rhythm varies; let a short one stand alone now and then.
- **Plain verbs, active voice, concrete subjects.** "The vent lifted the bodies to the
  surface", not "the plume's conveyor packs bodies into its convergence zones". "The
  world ran out of matter", not "conceptions were refused for want of matter".
- **Concrete before abstract.** The creature at 60 m before "the benthic niche". The
  number before the pattern.
- **Say what you don't know in ordinary words.** "I think", "probably", "we don't know
  yet", "this is a guess from reading the code, not a measurement". Not "arguably", not
  "it is worth noting", not "notably".
- **Paragraphs carry one step of the argument** and end when it is made. They do not end
  on a punchline. A one-line paragraph is allowed when it is the point.

## 4. Numbers, terms, code and typography

- **Say what a number means before quoting it.** Give the normal value, then the measured
  one: "A quiet world has ten or twenty contacts per step. This one had 13,600." Numbers
  that need comparing go in a table. Units on every quantity. Two or three significant
  figures unless the extra digits are the point (they were, in 0069).
- **One name per thing.** The glossary in `logbook/README.md` is the reference; use its
  word and no synonym. Stomach, absorptive and chain are three words; pick the one the
  glossary gives for the sense you mean.
- **Code identifiers only when the reader must go and find that thing.** At most one per
  sentence, two per paragraph, never inside a parenthesis in the middle of a sentence.
  Environment variables, column names and file names are code identifiers. Commands,
  snippets and errors go in fenced blocks.
- **Bold is for the two or three words that begin a list item or a decision**, and rarely
  elsewhere. No bold lead-ins that end in a colon or a full stop. No bold sentences.
- **Dashes.** At most one em dash per paragraph. Use commas, full stops or parentheses
  instead. En dashes stay in ranges (1,400–1,500).
- **Headers** only in a piece longer than about five hundred words, and at most three
  levels deep. A header is a statement, not a label.
- **Links** to `DESIGN.md`, `DECISIONS.md` and other entries instead of restating them.
  The logbook is never a source of truth (its README says why).

## 5. The tells

These are the habits that make prose read as machine-written. `scripts/style-check.py`
counts most of them; the rest are for the writer's ear. None is forbidden outright, but
each one found is a reason to rewrite the sentence, and a page with several of them fails.

| tell | example from our own record | what to do instead |
|---|---|---|
| Three facts joined by dashes | "matter arrives at the surface, the leaves lock it within a step, and a dead body's matter at −1 m sinks at 0.002 m/s — eight hours to the floor, where burial waits" | three sentences |
| The verdict-headline sentence | "The vent connects the outflow; the surface does not; and at this dose neither balances." | "Matter that arrives at the vent gets buried. Matter that arrives at the surface does not. Either way, at this dose, more comes in than goes out." |
| "Not X but Y", "X, not Y" as a closer | "a fork with a few branches, not noise" | say what it is: "the runs fell into four states, so this is a fork with a few branches" |
| Bold lead-in with a colon or stop | "**The crowd.** The plume's conveyor…" | a plain sentence that names the subject |
| The fragment as punchline | "Not noise." "A fork, not a fluke." | attach it to the sentence it belongs to |
| Triads by habit | "honest, brief, never performative" | two items, or four, or a sentence |
| Intensifiers and stage directions | exactly, precisely, quietly, simply, genuinely, honest, the whole point, crucially, importantly, notably, in short, put differently | delete them; the sentence is usually fine without |
| Self-narration of virtue | "the failure is mine, and the record will say so" | "I chose these two doses to keep the run cheap. Together they starved the world." |
| Code in the middle of prose | "the plume lifts bodies at 0.05 m/s in its patch (`EVOSIM_CURRENT_ADVECT 1`), its vertical velocity is special-cased to zero at the waterline so it does not push them *through*" | "In the vent's patch the plume lifts bodies at five centimetres a second. It stops pushing at the waterline, but a body that arrives there moving upward keeps going, and above the surface nothing pulls it back." |
| "which is why / which is what / which is where" | "…within a single metabolic step, which is why the first visible difference is forty columns at once" | a new sentence: "That is why the first visible difference is forty columns at once." |
| Rhetorical questions | "What would let a stomach live where the matter is?" | state the question as a question we hold: "The open question is what would let a stomach live where the matter is." |
| The nominal subject | "What the audit did find were amplifiers" | "The audit found amplifiers" |
| Semicolon chains | "…the top holds; the populations live in the water; founding is safe; the stock levels" | a list, or sentences |
| Numbers before meaning | "Identical through step 147,777" | "A step is a hundredth of a second, and a run has three million of them. The two runs agree through step 147,777." |
| The teasing title | "One box", "The senses answer" | "The world becomes one box", "Three new senses, and they work" |
| Words that only appear in machine prose | delve, tapestry, landscape, robust, leverage, underscores, highlights, testament, nuanced, navigate, seamless, journey, elegant, sits at the heart of, a hard truth | the plain word, or nothing |

## 6. What humanises, positively

- Tell it in the order it happened, including what you expected and how long it took.
- Use the ordinary word. Died, not was extinguished. Ran out, not was depleted. Touching,
  not in contact.
- Admit wrong turns the way a colleague would, in one or two sentences, then move on.
- Name the thing you were staring at: the column, the log, the graph, the Play button.
- Let a small true joke through when it costs nothing. "PhysX documents that the thread
  count does not matter. PhysX has not met our creatures."
- Vary the rhythm. A page of twenty-word sentences is as tiring as a page of sixty-word
  ones.
- Prefer a concrete example to a general claim, and a general claim to a slogan.
- Say "I don't know" when you don't. It is the most human sentence in the language and
  the one a machine finds hardest.

## 7. Per-document notes

- **Logbook.** The story, first person, the day it happened. Every entry opens with the
  point in one paragraph. Pre-registration blocks frozen. Humour and asides welcome.
- **`DECISIONS.md`.** Each entry opens with the decision in one sentence, then why, then
  what was rejected and why. "We" throughout. No humour in the rule itself; the reasoning
  may have a voice. Append-only for content; restyling an old entry is allowed and must
  not change what it decides.
- **`DESIGN.md`.** The specification. Precise, complete, third person or "we". Citations
  keep their exact form (`[KEY §section, p.N]`) and section numbers never change, because
  other documents point at them. No jokes.
- **Primer.** The book's draft. Narrative, first person where the agent was there, with
  the room to digress. Every claim traceable to a source or marked as inference.
- **`README.md` and `CLAUDE.md`.** Instructions: imperative mood, one step per line,
  the command in a fenced block. A gotcha states the incident in one sentence and the
  rule in the next.
- **Proposals to the owner.** The proposal in one paragraph, the rules as a numbered list,
  what the owner is asked to rule on at the end.
- **Commit messages.** A title under seventy characters that says what changed and why;
  a body in plain sentences. No bullet lists of files.

## 8. Rewriting what already exists

The record is being restyled in full. The rules for that work, which an agent other than
the author will do:

1. **Nothing true may become false, and nothing may be lost.** Every number, unit, date,
   time, run name, seed, commit hash, `simHash`, decision number, citation locator,
   section number, file path, quotation and link in the original must appear in the
   rewrite with the same meaning. `scripts/style-check.py --preserved old.md new.md`
   lists the tokens that vanished; the list must be empty or every item explained in the
   commit message.
2. **Pre-registration blocks are copied verbatim.** In the logbook, everything an entry
   says it wrote before the run (hypothesis, predictions, falsifiers, two-sided readings,
   validity checks, the launch note) is not rewritten. Add the plain opening paragraph
   above it and restyle the results and verdict below it.
3. **Superseded text stays superseded.** Strike-throughs, "superseded by" notes and
   dated corrections are kept. History is not tidied.
4. **When a sentence's meaning is unclear, keep it and flag it.** Do not guess at what
   the author meant. Put the sentence in a list at the end of the commit message with
   the file and line, for the owner.
5. **One file per commit**, the commit message naming the file and the checker's before
   and after counts. `DESIGN.md` and `DECISIONS.md` get a dated note at the top saying
   they were restyled and that the git history holds the originals.
6. **The checker is a flagger, not a judge.** A piece passes when a newcomer can follow
   it, which the checker cannot measure. Read the piece aloud once before committing; the
   sentences that make you run out of breath are the ones to split.

## 9. The checker

```powershell
python scripts/style-check.py logbook/0069-the-shared-world-does-not-replay.md
python scripts/style-check.py logbook/*.md DECISIONS.md          # a report per file
python scripts/style-check.py --preserved old.md new.md          # what a rewrite lost
```

It reports, per file: words; sentences over thirty words (with line numbers); em dashes
per hundred words and paragraphs with more than one; bold lead-ins ending in a colon or
stop; "not X but Y" and "X, not Y" closers; "which is why/what/where"; rhetorical
questions; paragraphs with more than two code identifiers; the intensifiers and
machine-only words from §5; and headers deeper than three levels. It never edits a file.
