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

An acronym is spelled out the first time it appears, and the spelling-out has to be in
words the newcomer already has. "A compositional pattern-producing network, or CPPN, is a
small mathematical function. Give it a point in space and it returns whether there is body
material there." Expanding an acronym into more jargon teaches nobody anything, so when the
expansion is itself technical, say what the thing does as well. The same goes for a person's
name used as a label: *Sims-style* means Karl Sims's 1994 encoding, and the entry that first
says it has to say so.

The project's own labels are terms of art too, and being ours makes them harder to notice
rather than easier. A milestone number, a round number, a spike, an arm, a worker, a column
heading: a newcomer has no way to guess what any of them mean, and they are the words most
likely to slip through unglossed. Gloss the label where the piece first uses it, and add it
to the key when a later piece will need it.

The test is mechanical, and it is the one thing in this guide a checker cannot do for you.
Read the piece as though you had never seen the project, and stop at the first word you
could not define. That word is a defect in the piece, not a gap in the reader. If the term
will come up again in later entries, add it to the key in `logbook/README.md` as well as
glossing it here.

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
  instead. En dashes stay in ranges (1,400–1,500). The em dash in a logbook entry's title
  line is the format `logbook/README.md` prescribes and never counts against a piece; a
  numbered primer piece's title (`# 01 — …`) is the same format and is equally exempt,
  and the checker exempts both. A
  header below the title may carry one only where it separates a run or round label from
  what happened, as in "Round 2b — results". A header that uses one to weld two ideas
  together is two headers, or one statement.
- **Headers** only in a piece longer than about five hundred words, and at most three
  levels deep. A header is a statement, not a label.
- **No horizontal rules.** A rule between two sections says only that a section ended,
  which the header underneath it already says. Delete them. A piece that seems to need a
  break where no header belongs usually has a paragraph doing two jobs.
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
2. **Pre-registration tables are copied verbatim.** In the logbook, the predictions,
   their falsifiers and the validity checks are not rewritten: a prediction that changes
   wording after the run is a different prediction. The prose around them (the
   hypothesis, the two-sided readings, the launch note) may be restyled sentence by
   sentence, provided every condition, outcome and fact survives and a second reader
   checks the pair for meaning. Add the plain opening paragraph above the block and
   restyle the results and verdict below it. (Ruled 2026-09-07 at the retrofit's review:
   the drafts had recast every two-sided-readings block and launch note, the reviewers
   found no change of meaning, and restoring sixty blocks bought nothing.)
3. **Superseded text stays superseded.** Strike-throughs, "superseded by" notes and
   dated corrections are kept. History is not tidied.
4. **When a sentence's meaning is unclear, keep it and flag it.** Do not guess at what
   the author meant. Put the sentence in a list at the end of the commit message with
   the file and line, for the owner.
5. **A rewrite replaces the original, and never sits beside it.** While the review is
   open, draft it next to the entry as `NNNN-v2-slug.md`. Once the owner, or the reviewer the
   owner has delegated, has approved it, put the new text into the original file and delete the draft in the same commit, so the
   entry number, the index in `logbook/README.md` and every link into the entry stay as
   they were, and the git history holds the old text. Two files never carry one entry
   number. Run both checks against the final file, taking the original from git for the
   preservation check.
6. **One file per commit, or one reviewed batch per commit** with every file named in the
   message, together with the checker's before and after counts and the reviewer's
   findings. `DESIGN.md` and `DECISIONS.md` get a dated note at the top saying
   they were restyled and that the git history holds the originals.
7. **Rulings on the guide are made where the work is and recorded here.** The owner
   authorised the reviewing agent on 2026-09-07 to make style rulings alone once it
   understood what the prose is for, and to persist them in this file. A ruling is a
   dated sentence in the section it changes, with the reason, so a later reader can tell a
   rule from a habit and can reverse it knowingly.
8. **The checker is a flagger, not a judge.** A piece passes when a newcomer can follow
   it, which the checker cannot measure. Read the piece aloud once before committing; the
   sentences that make you run out of breath are the ones to split.

## 9. The checker

```powershell
python scripts/style-check.py logbook/0069-the-shared-world-does-not-replay.md
python scripts/style-check.py logbook/*.md DECISIONS.md          # a report per file
python scripts/style-check.py --preserved old.md new.md          # what a rewrite lost
```

It reads prose only. Fenced blocks, tables and headers are skipped, so its em-dash figure
counts paragraphs and its findings say nothing about your titles; header dashes are counted
and reported on their own line, with the entry title exempt. Terms left unexplained, a
header that labels rather than states, and a piece a newcomer cannot follow are all
invisible to it. A file with zero findings is a file that has passed the countable half.

It reports, per file: words; sentences over thirty words (with line numbers); em dashes
per hundred words and paragraphs with more than one; bold lead-ins ending in a colon or
stop; "not X but Y" and "X, not Y" closers; "which is why/what/where"; rhetorical
questions; paragraphs with more than two code identifiers; the intensifiers and
machine-only words from §5; and headers deeper than three levels. It never edits a file.

A bibliography defeats it. The checker splits sentences at a full stop followed by a
capital, so every author initial in a reference line reads as a sentence of one word and is
reported as a fragment: "**[EA23]** L." and "Eguiarte-Morett and W." are one citation, not
two mistakes. The title, journal, volume and pages that follow then read as one over-long
sentence, for the same reason. A file whose remaining findings are all of that shape has
passed. Say so where the file is handed over, rather than breaking the references to quiet
the tool.
