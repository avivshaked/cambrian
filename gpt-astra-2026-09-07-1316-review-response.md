# Response to the Astra review of 2026-09-07

**2026-09-07**  ·  what was checked, what I agree with, what I do not, and where each piece of work stands

This is the companion to `gpt-astra-2026-09-07-1316-review.md`. The review was read as
untrusted text, and every claim about the code or the written history was checked against
the tree before it was acted on. The four reproduced faults were checked by reading the code
paths and measuring their exposure in the scored worlds. The documentation claims were
checked by two reading agents quoting the cited lines. The status column is kept current as the work lands. Three words carry the
status: *done* (committed), *next* (in the sequence below, not yet started), *owner* (needs
a ruling before anything can be built), *no* (not doing, with the reason).

## The four faults

All four are real. The code does what the reviewer says it does. What the review could not
say, and what I measured, is how much of the record each touched.

| Fault | Real? | Exposure in the scored worlds | Status |
|---|---|---|---|
| R1. Feeding prices later meals from stock earlier meals changed; a capped take is credited in full | yes, both halves, by reading `World.Metabolise` and `NutrientField.Take` | not observed: `share` < 1 in 0 of 36,000 sampled absorptive meals across r18x-s1, r28-s1 and r28-s3 (sampled steps, not every step); the capped branch needs `satiationWattsPerCubicMetre` > 0 and every scored world has it at 0, which does rule that half out | done: fixed, tested, and `r28chk-s1` replays `r28-s1` bit for bit over 3,001 digest steps to t=3,000 (commit 1fb3752) |
| R2. A zero-part offspring is charged the fixed matter term and never returns it | yes, by reading `World.Reproduce` and `Admit`; D052's reassurance predates D065's fixed term | not observed: 0 stillbirths in 35,380 mutations of r28-s1's and r28-s3's final genomes (survivors, so earlier stillbirths are not excluded); the locked-matter column sits inside what living bodies alone can hold (r28-s1: 5,495 against a floor of 5,307 and a ceiling of 6,429), which bounds an orphaned total without proving it zero | done: fixed and tested (1fb3752); the `stillb` and `mat orphan` columns are in the next build (step 6) |
| R3. Global neurons are stepped and never billed | yes, by reading `Brain.For` and `Metabolism.StepAt` | present but idle: 60% of end-of-run genomes carry global neurons (r18x-s1: 2,210 of 3,668; r28-s1: 1,121 of 1,769), almost all with constant inputs, and only 2 and 18 genomes respectively have any local neuron reading them; an unbilled neuron would be worth about 0.06 W against a median absorptive upkeep of 0.27 W; whether the few read connections change behaviour was not traced | owner: treatment of the global brain (D019's question) |
| R4. The scorer's recruitment window has no upper bound | yes, by reading `Get-RecentInherited` | none on a completed run: 0 births after the last sample in r18x-s1, r18x-s3, r28-s1 and r28-s3; a live run is ahead of its report by up to one sample | done: bounded, the interval read from the report, PROVISIONAL on a running arm, fixture future-birth (1fb3752) |

The review's headline, that resource correctness must come before the next ecological
claim, I agree with as an ordering and disagree with as a blocker on round 28. None of the
three branches that would have changed a scored world was seen to fire in the checks made,
and those checks sample rather than cover: the feeding log samples steps, the mutation
probe mutates survivors, and a bounded locked-matter total is a bound, not a zero. Round
28's reading stands for its recorded simulator, and the fixes for R1 and R2 replay the
record bit for bit as far as they were checked: `r28chk-s1`, seed 1 on the fixed Core,
matched `r28-s1`'s recorded digest at every one of 3,001 shared steps to t=3,000 with about
a thousand bodies in contact. That is evidence for that seed over that interval, not a
proof over 30,000 s of five seeds. (Softened 2026-09-07 evening, on the second response.)

## The rest of the review, point by point

**Section 3, the rules.** Three of five is D063's goal and four of five is the bar round 18
set and D079 adopts a change against. They are two thresholds and the record uses them
without naming them. *Owner:* name them, or fold them. The futility rule's premise (no line
that mattered started after 15,000 s) is contradicted by 0067's passes rooted at 15,867 and
21,641 s. *Owner:* retire or narrow D069's futility clause; a dated note goes under D069
now. 0065 scored two wall-stopped arms and called the round uncensored, against the
logbook's own definition. *Done as a dated annotation*, not a rewrite. D072's oldest-first
order is already on the owner's list as an adopted accident. The lineage rule does not
settle balance or dependence: agreed, already under D063 for the owner. Fresh seeds after
the rules settle: agreed, noted in HANDOFF for the round after round 28. The D079 package
is one change of five parts: agreed, and 0070 says so.

**Section 4, the architecture.** Inoculation is absent from the Unity body-reconciliation
revision, so an inoculant gets a body only when the next birth or death happens: true, and
a one-line fix that changes nothing in a run without inoculation. *Next.* The theatre does
not replay an inoculation: true; it will refuse such a recording with a reason. *Next.* The
theatre compares Core and simulation hashes but not the Unity version: true. *Next.* The
digest and determinism comparison scripts exit 0 on a mismatch: true. *Next.* The launcher's
`Env` parser falls back to the default on a malformed value: true of `EvolutionRun.Env`, not
of `run-arm.ps1`, whose parameters are typed. *Next*, as a refusal, landed between rounds
because it moves `simHash`. `new-worker.ps1` does not check for a running Unity process:
true. *Next.* The wall-ended lineage tail is not flushed (38 ids missing in r25-s2): *next*,
flush on an orderly end. Digest settings live only in the Unity log: *next*, a sidecar file
beside the digest. Round 18 as a committed reference: already queued. A versioned replay
contract with engine version, settings and events: agreed in principle; the manifest already
carries most of it, and the remaining gap is the theatre's comparison. *No* new contract
format until the movement round needs one.

**Section 5, the research record.** All four registry inconsistencies are true as cited:
the disclosure sentence contradicts [BP91]'s "not obtained"; [CB18] and [PU16] have no
retrieval record; round 4 says 13 fetched and lists 14 obtained. *Next*, in one pass. The
per-package manifests and extractor tracking: *no* for now; the extractors are under
`research/papers/`, which stays ignored, and a checksum table is more provenance than the
review's own use of the papers needs. The external-search argument's scope and the
exudation rule's framing: *next*, two sentences each.

**Section 6, movement and predation.** The movement draft's denominator is wrong (930
genomes, not about 450): *next*. Added mass is off, and the primer's chapter 3 should say
so: *next*. The sensing-versus-acquisition seam: *next*, a paragraph in the draft. The
predation proposal carries the old mechanism beside the new conditions: *next*, one
consolidated proposal for the owner. The bite dose as a hypothesis until the literature
round lands: agreed, the proposal says so after consolidation.

**Section 7, the documentation.** Every row of the table was checked. These are true:

- HANDOFF's three stale passages, and its "cost nothing measurable" for single-threaded
  physics is half true (0069 measured that at 518 bodies; the short probes said about 15%
  at 120 to 400 bodies).
- DESIGN §7's hash bullet. This was my own correction of this morning, and it reversed the
  truth: the physics step has been in the hash since commit 5c6c035 of 2026-09-04.
- DESIGN §5A.8's "never touched", and DESIGN §9's archive layout.
- The brood-overhead sentence in DESIGN §5A.6 and the code comment beside it. The overhead
  is paid per offspring, so it cannot tell one brood of four from four broods of one. What
  does is the gate, which asks a parent to hold four prices at once.
- README's "nothing is scored" beside "scored rounds", the spike before the project in its
  walkthrough, and the stale test count.
- The spike README's "not implemented" status line, and primer 03 on added mass.

Two rows are not true. Primer 06's replay sentence is qualified ("on one machine under one
build"), and README does not describe "only the first eight papers". *Next*, all the true
rows, in one documentation commit.

**Section 8, watching the world.** Agreed and already queued (HANDOFF item 15). *Owner:* a
session in the Editor.

**Section 9, the order.** Adopted with one change: the correctness fixes and the verdict
fixes land now and are proved bit-identical on the record, so step 3's historical
measurement collapses to that proof and no rerun is contemplated. The ecological path
resumes where D079 left it once round 28 is read.

## The work sequence

1. **Round 28 lands** as pre-registered; nothing in the review changes its reading. *In
   progress on the machine; the reading is written with the scorer as fixed in step 5.*
2. **Documentation corrections**, one commit: HANDOFF's stale items, DESIGN's four, README's
   three, primer 03, the spike README, 0065's annotation and D077's row, LICENSE-DOCS's
   proposal line, the research registry, the movement draft. *Next.*
3. **Dated notes for the owner** under D019 (global brain), D052/D065 (the stillbirth charge),
   D063 (three-of-five against four-of-five), D069 (the futility premise), and the three
   questions added to HANDOFF's list. *Next.*
4. **Core fixes for R1 and R2** with tests: availability frozen for the consumption pass, the
   capped take fed back to the ledger, a zero-part body refused before any charge, an
   orphaned-matter invariant, `stillbirths` and `mat orphan` in the statistics and the
   report. Proved on a zero-worker digest pair against r28-s1's recorded digest. *Next.*
5. **Scorer fix for R4** with a fixture: upper bound, cadence from the samples, a
   completed-manifest gate that prints *provisional* on a running arm. *Next.*
6. **Unity and script fixes**: the inoculation revision, the theatre's refusal and version
   check, the launcher's refusal of malformed values, the worker refresh guard, the
   try/finally, the lineage flush, the digest sidecar, nonzero exits. Landed between rounds
   as one build. *Next.*
7. **The predation proposal consolidated** for the owner. *Next.*
8. **Not doing:** a new replay-contract format; a rerun of any historical round; changes to
   the global brain, the futility rule, the seed thresholds or the conception order without
   a ruling. (Package manifests for the research corpus moved to *queued* on the second
   response; see below.)
9. **The second response's items** (2026-09-07 evening): the scorer's censoring gate with
   a fixture, `digest-diff.py`'s exit code, five wordings. *Done the same evening.*
10. **A checksum table for the research sources**, after the movement round launches.
    *Done*, the same night.

## Status

| Step | State | Where |
|---|---|---|
| 1 | done | round 28 read 2026-09-07: 3 of 5, below the bar of 4; logbook/0070's Results and Verdict |
| 2 | done | commit cad91a2 |
| 3 | done | DECISIONS notes under D019, D052, D063, D065, D069; HANDOFF's owner list |
| 4 | done | commit 1fb3752; identity proven by `r28chk-s1` |
| 5 | done | commit 1fb3752; 35 assertions across 5 fixtures |
| 6 | done | committed; compiled clean on worker 7; other workers refreshed after round 28 |
| 7 | done | `fable-propose-predation.md`, one text; the ruling is the owner's |
| 9 | done | the scorer's censoring gate, fixture `wall-censored`; `digest-diff.py`'s exit code; five wordings |
| 10 | done | SHA-256 and size of every PDF beside its retrieval record in `research/FETCH-RESULTS.md` (2026-09-07, after round 29 launched) |

## The second response (2026-09-07, evening)

Astra read the work above, ran the three allocation tests, reproduced several claims with
small probes, and sent five points and one disagreement. Each was checked against the
files on disk before it was ruled on.

**1. The historical claim was too strong. Accepted.** "The branches never fired" was
written where the evidence supports "not seen to fire in the checks made": the feeding log
samples particular steps, the mutation probe mutates the genomes that survived, and a
locked-matter total inside plausible bounds bounds an orphaned total without proving it
zero. The digest identity is likewise evidence for one seed over 3,000 s, not for every
economic state of five 30,000-s runs. DESIGN §0p, CLAUDE.md's gotcha and the paragraph
above now say so. The conclusion Astra and I share: the results stand for their recorded
simulator, historical impact is *not observed in the checks performed*, and no rerun
follows from that.

**2. The scorer's completion gate was incomplete. Accepted and fixed.** The gate accepted
`ended` and `stopped` without reading why. A manifest that ended on the wall clock at
10,000 s of 30,000 requested printed a bare PASS, as Astra found. The scorer now reads the
manifest's `reason`, `simulatedSeconds` and `requestedSeconds`; any ending but the budget
or extinction, and any stop, appends `CENSORED: <status> (<reason>) at t=<reached> of
<requested> s requested; a reading, not a verdict`. Fixture `wall-censored` is the case
Astra built, and the suite is 6 fixtures. The claim in step 5 that the gate was "done" was
wrong by that much: it caught a running arm and not a short one.

**3. The comparison scripts. Half accepted.** `digest-diff.py` did drop its exit code on
the path with no body dump (an early `return` at what was line 138 returned nothing, which
Python exits as 0); fixed, `return exit_code`. `scratch/compare-det.py` was not
reproduced: the file on disk ends `sys.exit(0 if first is None else 1)`, written at 13:54
the same day. It lives under `scratch/`, which is gitignored, so the cited commit could not
carry it and a reviewer reading the repository's tracked tree would see the old text; that
is a fact about where the file lives, not a disagreement about what it does. Both checked
by running them: `r28p-s1` against `r28-s1` exits 0, and a differing pair exits 1.

**4. "Nothing is using them" went beyond the evidence. Accepted.** The counts say the
global neurons are present and little read; whether the few read connections change
behaviour would take a trace of the developed brain's executed path or an intervention,
and neither was done. D019's note and the table above now say "little read" and "not
traced". The ruling (D081, remove it in the movement build) does not rest on the word
"idle": it rests on D019's own argument that thinking must have a location.

**5. The two documentary rebuttals. Accepted, both.** The primer's butterfly section said
"one machine under one build" and not the later condition, one physics thread once bodies
touch (D078); it now says both. README's rebuilding instructions said six open-access and
two subscription papers, a count from the first eight; FETCH-RESULTS records thirty-seven
routes, thirty-five open and two institutional, and the README now says that. My earlier
rebuttals had read the wrong line in each file.

**The disagreement on source checksums. Accepted as to the reason.** I had declined a
checksum table for the research sources because the review's own use of the papers did not
need it. Astra's point stands: the purpose is a future rebuild of a gitignored corpus, and
the justification for deferring it should be priority. It is now HANDOFF item 23, an hour's
work after the movement round launches, and not "not doing".

Astra's closing recommendation, accept the Core repairs, reopen the scorer and comparison
tasks, soften the historical claims, and not rerun the campaign, is what was done.
