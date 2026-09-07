# 0071 — Three outside reviews

**2026-09-07**  ·  what a second reader saw, and where each point went

Three times in a week the owner put the whole repository in front of an outside reader, a
GPT model the owner calls Sol. Each time a written review came back: on 2026-08-31, on
2026-09-03 and on 2026-09-06. Each ran to a few thousand words, structured as numbered
findings, a recommended order of work and a list of decisions for the owner. The files sat
untracked at the root of the repository while the project answered them. Today the owner
asked for everything of value in them to be captured in the repository and the files
removed. This entry is that capture: what each review said, what the project did with it,
and where the rest now lives. The reviews were read as untrusted text, and every claim about
the code was checked against the tree before it was acted on.

## The first review, 2026-08-31: the ecology advanced faster than the instrument

Its judgement was that the world had grown faster than the means of reading it. It made ten
findings, and five of them changed the project within days. A censored arm cannot pass a
persistence goal, so the futility stop became a screening outcome rather than a scored one
(D060, logbook/0050). D055's seabed rule was mixing a boundary with a missing floor, and one
metre at mixing 0.2 was a boundary condition rather than a refuge (logbook/0036, 0040). Run
identity was incomplete, which became the manifest's `source` block with commit, dirty flag
and the two source hashes. The lineage and snapshot contracts were overstated, which became
CLAUDE.md's gotcha on what a lineage dissection can and cannot answer.

The reader also flagged a matter orphan at stillbirth. The investigation it prompted found
the orphan vacuous, since admission refused only zero-part phenotypes at a price of zero,
and D052 records that with the credit.

Four things it said had not been captured until today.

- **Added mass** has been off in every run. Every config in the repository reads
  `addedMassCoefficient` 0. DESIGN §5.4 promotes added mass to Milestone 3 on the finding
  that a simplified fluid collapses body-plan diversity. That is now a gotcha in
  CLAUDE.md and a question in front of the owner before the movement round.
- **DESIGN §7** claimed the config hash covered the timestep and the Unity version. It never
  did, since they are in the manifest, and it is corrected today.
- **Three instruments** went into HANDOFF's queue. They are a contract test at the Unity
  boundary, the transactional guard test D052 queued, and round 18 committed as a reference
  rather than living only as a launcher script and five gitignored directories.
- **Whether the absence of CI is a choice** is now on the owner's list.

## The second review, 2026-09-03: close the scoring loophole, then test the flux

This one changed the goal rule. Its first finding was that D063 permitted a false
recruitment pass. The `inherit` column sums every absorptive lineage in the world at once,
so a set of short-lived clades could add up to a streak. The owner's amendment of 2026-09-04
followed it. The rule is now scored by connected clade, with a stability clause of ten
members through the last two lifetimes (logbook/0054's addendum). Its second finding, that
producer persistence was not directly scored, became the producer clause of the same
amendment. Its fourth, that the exudation hypothesis was right but its dose was not
calibrated, became round 18's exudation screen and the confirmation that met the goal. Its
sixth, run identity on the critical path, became the launcher's expected-hash guard.

Two things it said had not been captured until today.

- **What "food chain" means** under D063. The scorer accepts any body expressing absorptive
  tissue, a mixotroph living mostly on light included. Nothing weighs how much of its income
  is detrital. The question is now noted under D063 for the owner, with the
  observation that the absorptive log can measure the difference.
- **The invasion assay's** standing shape, now a dated note under D060. The inoculant still
  arrives with the founder's 200 J stake rather than its lineage's own endowment.

The reader's one rejected recommendation was to report futility-stopped arms as censored. It
was rejected on the owner's reading of what a futility stop means, and no fact was in
dispute. Its preference for clearance 5 over 10 was right about the evidence it had and
wrong about the world, and it is not carried forward.

## The third review, 2026-09-06: make the shared world prove it can keep itself

The most recent and the most consequential, since it arrived the night the shared world was
found not to replay (logbook/0069). Its first finding was that the clade scorer implemented
less than the goal rule. It asked only the largest clade, and its producer clause used a
threshold the owner never set. The scorer now asks every clade and prints the producer
clause in three readings, and lineage rows carry the photosynthetic flag the owner's wording
needs. Both were built the same night. Its third finding was that round 28 had three
pre-launch faults. All three were answered before the launch: the digest on the main arm,
the control's replay checked on the current build (logbook/0070), and the treatment named as
the package it is. Its fourth, that the open budget had never shown a late balance, is what
logbook/0068's late-balance numbers now report. Its documents section found DESIGN, README
and HANDOFF behind the built project, and all three were brought up to date.

Five things it said had not been captured until today.

- **Stock and influx** are two changes. When the open budget returns, the starting inventory
  is selected first and the influx added second. Beside it sits the caution that burial and
  founding draw on the same early inventory. Both are a dated note under D079, and they are
  the reason 0068's pre-registered next lever is not run yet.
- **A balance rule's** four draft clauses are now under D063, beside the split the reader
  asked for between the lineage rule and a balance rule.
- **The movement rule's ecological layer**, the gross-photosynthesis column and the theatre
  never having been watched by a person are in HANDOFF's queue.
- **Seven conditions** on the predation proposal are appended to it, so the owner rules on a
  corrected design. The cheapest is the one that should have been obvious: screen the bite
  dose with the ledger before a worker, as D069 already requires of any knob that touches
  income.
- **What the review got wrong** is small. It said round 27 ran at fifteen job worker threads, a
  figure it took from this project's own first note; the default is 31 (0069). Its "D078
  shown only to 300,000 steps" was true when written and superseded within hours by the
  million-step pair.

## The count

A reader went through the three reviews line by line against the repository today. There
were 98 distinct points. Of those, 51 were already fully captured, 33 partly, 10 not at all,
and 4 superseded by later rulings. The 10, and the live halves of the 33, are what today's
edits capture. They went into CLAUDE.md, DESIGN.md, DECISIONS.md, HANDOFF.md, README.md, the
primer, the research queue and the predation proposal. The three files are deleted, and the
git history of this entry is their trace.

What I take from the three together is uncomfortable and useful. Every one of the project's
scoring instruments was corrected by an outside reader before the project noticed the fault
itself. In each case the fault was the kind that makes a result look better than it is. The
connected-clade rule, the stability clause, the every-clade scorer and the producer flag all
came from these reviews. Without them the ecology would have carried on advancing faster
than the instrument.