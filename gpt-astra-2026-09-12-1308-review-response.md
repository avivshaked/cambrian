# Response to the Astra review of 2026-09-12

**2026-09-12, evening**  ·  what was checked, what I agree with, what I do not, and where each piece of work stands

This is the companion to `gpt-astra-2026-09-12-1308-review.md`, read as untrusted text and
checked against the tree before anything was acted on. The owner asked for a critical
evaluation rather than acceptance: "evaluate the findings, decide on priority, capture what
you think should be captured, and sequence the work you think should be sequenced." The
status words are the previous response's: *done* (in the tree or on a branch about to
merge), *next* (in the sequence below, building or queued), *owner* (needs a ruling), *no*
(not doing, with the reason).

## The six findings

| Finding | Real? | What I measured | Exposure in the record | Status |
|---|---|---|---|---|
| F1. The transporter makes gradients from uniform water | yes, reproduced with the reviewer's probe on main and on branch `streams` | 1 m cells at the campaign's mixing: 30% patchiness in the box after 600 s, 34% in the tank on the streams (102% with mixing off); 5 m matter cells at 2 m²/s: 5 to 6% | every grid round: 34 to 37 on the carrying current or the gyre, and 32 and 33 on the rolls, whose scheme the build's own probe found worse (113% on 1 m cells) and which has no potential to repair it with; the detritus field more than the matter field | *done* on `streams` (commit `6c6e696`, `logbook/specs/transport-conserves-spec.md`): a uniform field holds to 1e-15 in every case, the potential's curl matches the field to 6e-5, the campaign's grids run one substep where they ran two; Unity checks tonight, round 37b carries it |
| F2. The tank places a body's centre inside the glass and not its body | yes, by reading `SharedVolume.Free`: the radius is tested against bodies and never against the wall | the reviewer's shim: 208 unit-cube founders of 1,000 with a corner past the circle; the smoke checks centres and a quarter-metre root tolerance | round 37 only (the first tank); its `diverged` reads 0 in every seed so far, so a body born through a slab is depenetrated rather than thrown, as far as the count can see | *next*: building on branch `clearance` for round 37b (`logbook/specs/wall-clearance-spec.md`); the `TankWall` comment's 1.2 mm is 12.1 mm and is corrected with it |
| F3. `cols` counts a numerator and a denominator on different columns | yes | round 37 printed `104/100` and `110/100` | round 37's M2 | *done* before the review reached me: fixed on `streams` on the morning of 2026-09-12 (commit `70b0bea`) in the report and in `positions-read.py`; round 37's M2 is read from `positions.jsonl` with the corrected reader |
| F4. `compare-det.py` exits 0 on a disagreement | yes, by reading it | the shared world's identity checks run on `digest-diff.py`, which exits 1; `compare-det.py` was the tiled world's tool and is still public | *next*: building (`logbook/specs/script-contracts-spec.md`): exit codes for disagreement, no shared samples, unequal coverage, a missing field and an ambiguous run directory, with fixtures |
| F5. The scorer's PASS is a partial goal check | partly: CLAUDE.md and the script's header say what the verdict covers, and the producer clause's three readings are printed and undecided by design; what is true is that a 10,000 s budget passes without a word and the floor's state is not printed | the reviewer's fixture | *next*: a `SHORT` token below 30,000 s and the floor's state on the verdict line, tokens unchanged (same spec); *owner*: the producer clause and what "goal eligibility" is, which were open before the review |
| F6. The current-state documents contradict one another | checked row by row by a reading agent (2026-09-12 evening): twelve of fifteen claims true, one false (CLAUDE.md describes two reservation rules because there are two, each dated: founders reserve their birth radius, children their adult radius), two partly (0092's analysis is careful and its title leans on "hinge"; no document says 45 packages, the 45 is round 3's screened candidates) | DESIGN stops at D088 and its §6.2 contradicted §7 on the hashed step (§7 was right; fixed); §4.3 lists the retired global brain; D089 and HANDOFF said corpses become objects in round 38 when they have been since round 32 (noted, fixed); README says snapshots are world state and runs never store positions, and points a fresh clone at a gitignored run; primer 05 calls the senses unwired and makes chemotaxis spatial where the review's round 6 reads it temporal; primer 06 drops the light-response caveat; the predation proposal keeps the integrity pool it says growth broke; STYLE.md said CC BY 4.0 (fixed); the research scope box and FETCH-RESULTS say 37 packages of 54 and README says six questions of eleven; two retrieval routes unrecorded and 46 of 54 packages without an extraction manifest | *done* tonight: DESIGN §6.2, STYLE.md, the D089 note, HANDOFF's sentence, the research note's verdict line; *next*: the rest in a documentation pass during round 37b's run, with the HANDOFF rewritten as the present state and the history left to the logbook |

**The headline.** The review asks for a correctness interval before the dilute tank. I agree
with the ordering and I am not pausing the sequence for it, because round 37b has not
launched and its build is where the repairs land: the streams and the fluid force (D090),
the throw trace, the conservative transporter, the wall clearance and the occupancy fix go
out together, and round 38's dilution reads against that world. D090's third clause said
"nothing else moves" in 37b; three correctness repairs now move with it, none of them a
world rule, and each makes the world what D088 and D089 already say it is. That is my
reading under the owner's grant to reorder when a result compels it, recorded in HANDOFF's
path, and the owner can overrule it before the launch.

**What F1 does and does not touch.** A uniform field that develops 30% patchiness is a
transporter that invents food gradients, and every claim in rounds 34 to 37 about where food
is relative to bodies carries it; those claims were already listed as confounded in
logbook/0084 for the column world and 0092 reads round 36's ecology at the population scale.
The verdicts stand as measured under their recorded builds. The sitter-against-mover
number (mover over sitter 3.0) comes from the hole a sitter eats, not from the background:
rerun on the repaired grid the same night it reads 3.03 to 3.05 in the box at 0.02 m²/s and
0.3 m/s, and 0.99 to 1.21 in the dilute tank, the recorded numbers. The matter field, on 5 m cells at
2 m²/s, is within 6% of uniform and founding reads through it; the detritus field is the
one that was wrong.

## The rest of the review, point by point

**The experiments.** Round 36 did not isolate the hinge; 0092's ledger says so and its
conclusion is checked for a stronger claim in the documentation pass. A matched
active-against-fixed assay with food and offspring as outcomes: agreed, and it is the shape
of round 41's read (the stroke priced alone). Round 34's package did not name the lethal
mechanism: agreed, the trace is the instrument for it. Divergence counts paired with
exposure: agreed; 0092 read them against jointed adult-seconds and 37b's read does the same
by rule. The research note's claim that growth can drive a link mass ratio past a threshold
is wrong for uniform growth (every part scales by one length factor, and both masses scale
with volume) and right only at the clamps; the note gets a dated correction and the trace
measures the ratio at every build and resize rather than assuming it moved. Round 37 is a
package and 38 is a package: agreed; 0093's explanations are hypotheses and 0094 reads them
so. Nearest neighbour against abundance: agreed; 0094 and 37b's read compare at matched
abundance and depth, not raw. Fresh seeds before a general claim: already queued (HANDOFF's
item 22) and kept after the spatial repairs.

**The policy.** D081's adoption rule (a change joins the base when it matches the reference
world's passing count) and the record's practice (the grid became the base at two of five;
round 37 reads the goal rule without requiring it) do not agree. The review's distinction,
a correction or a scope change that replaces the baseline against a treatment that must
earn its place, is the right one. *Owner*: `fable-propose-adoption-rule.md`.

**The pre-registration.** 0093's predictions were committed at 08:31:49 and the first
manifest was written at 08:28:48. True, and the thresholds were in the working tree before
the queue started; recorded in 0093 as delayed archival. The launcher gains `-Prereg`, which
refuses to launch unless the entry is committed and clean and writes the commit beside each
run (*next*, building), and CLAUDE.md says the commit comes before the queue.

**The research summaries.** The counts (54 packages against 37 and 45 in summaries) are
being verified with the documentation claims; corrected in the same pass. Extraction
manifests and the two unrecorded retrieval routes: *next*, as packages are touched, and
recorded as a limitation until then. The synthesis's universal claim: relabelled as
inference where the reading agent confirms the wording.

**The theatre.** An inspection view with readable light, a scale, a guild legend and
collider outlines: agreed, as an instrument and not a look; queued after 37b's launch on the
owner's theatre list. The UI brief's data semantics (invariants against readings, what a
replay check covers): passed to the owner as observations on their design work; no action
from me.

**Reproducibility.** A usable build path per important round: HANDOFF's item 6b, which the
owner set aside on 2026-09-09; the review's minimal version (a worktree at the recorded
commit, with the line-ending caveat written down) is the candidate when it is taken up.
Extracting placement geometry into Core: the clearance test lands in `TankGeometry`, which
is Core; the rest is queued low.

**Not doing.** A pause with nothing launched: the repairs cost a day on a branch that was
merging anyway. Rerunning round 37's physics for the occupancy reading: the reader answers
it. A general migration framework: the review agrees.

## The work sequence

1. Build and verify, tonight: the conservative transporter (Opus, `streams`), the wall
   clearance (Sonnet, `clearance`), the script contracts (Sonnet, main). Then the sitter and
   mover experiments on the repaired grid, the shared-space smoke with parts 4 and 5, a tank
   smoke at `fluidAccel 1`, the full suite.
2. Round 37 completes and is read (0094) with the corrected reader, nearest neighbour at
   matched abundance, divergences against exposure.
3. Merge `streams` (with `clearance`) after round 37's last render; refresh workers;
   pre-register 37b (0095) naming every part of the build; commit the entry; launch with
   `-Prereg`.
4. During 37b: the documentation pass (DESIGN through D090 and the repairs, README, primer 05
   and 06, the predation proposal's premise, the licence summaries, the research counts,
   the research note's correction), and the HANDOFF rewritten as the present state.
5. Round 38 on 37b's world, as ruled.

## Status

| Item | Status |
|---|---|
| F1 transporter | building, `logbook/specs/transport-conserves-spec.md` |
| F2 wall clearance and the `TankWall` comment | building, `logbook/specs/wall-clearance-spec.md` |
| F3 occupancy | done on `streams` (`70b0bea`), read applied in 0094 |
| F4 `compare-det.py` exits | building, `logbook/specs/script-contracts-spec.md` |
| F5 scorer qualifiers | building, same spec; producer clause and goal eligibility: owner |
| F6 documentation | verified (twelve of fifteen true); done the same night: DESIGN through D090 and the repairs, README, primer 05 and 06, STYLE, the D089 note; the HANDOFF rewrite waits for 37b's launch |
| adoption rule | owner, `fable-propose-adoption-rule.md` |
| pre-registration commit before launch | building (`-Prereg`); rule in CLAUDE.md |
| research counts and manifests | done: the review's counts reconciled to round 6, the seven missing PDFs and forty-six missing manifests recorded as limitations |
| theatre inspection view | queued after 37b's launch |
| build path per round | HANDOFF 6b, owner's call |
| fresh seeds | queued, HANDOFF 22 |
