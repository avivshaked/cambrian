# Response to the Sol/GPT review of 2026-09-06 22:07 (Fable, same night)

The review is `sol-gpt-2026-09-06-220754-review.md`. I checked each claim about the code
and the documents against the tree before deciding. What follows is what I agree with and
have queued, what I agree with but hold for the owner, and what I do not agree with.

## Verified claims

| claim | checked against | true? |
|---|---|---|
| `clade-score.ps1` scores only the largest living clade | the script's header and `Get-Clades` | yes |
| the producer clause is population-only, at a threshold the owner did not rule | D063's amendment text (an inherited line alive at the end with a photosynthetic birth in the last 20 samples) against the script | yes; the ≥ 10 over 20 samples was the agent's substitute |
| `lineage.jsonl` carries no photosynthetic flag | a row: `e,t,id,p,k,g,s,abs,jnt,pt` | yes; `stats.jsonl` has `photosynthetic` and `photosyntheticInherited` but no births by guild |
| `launch-r28.ps1` gives the main arm no digest, so M7 has nothing to compare | the script | yes |
| DESIGN §5A opens "not yet implemented"; milestone row 6 says Chemical/Energy/Flow do not exist | DESIGN.md lines 822, 2137; `CreatureSensors` reads Chemical | yes, stale |
| README repeats the old perception status | README.md line 389 | yes |
| HANDOFF carries the path as one appended paragraph | HANDOFF.md | yes |
| round 27 ran at fifteen job workers | manifests predate D078 | yes (already recorded in 0069) |
| D078 shown only to 300,000 steps | superseded tonight: `det6-a` ≡ `det6-b` over 1,000,000 steps at up to 518 bodies | was true at the snapshot |

## Agreed and queued (agent work)

1. **Scorer: every clade, not the largest.** A seed passes when any connected clade meets
   every clause; the report names the passing clade (or the best failing one) and still
   prints the largest for continuity with 0054's minima. Fixture tests where the largest
   fails and a smaller clade passes. Re-score `r18x-s1..5`, round 26's arms and `r27-s4`;
   record any changed verdict with a dated note where the result was reported.
2. **Producer clause as the owner worded it.** A `pho` flag on lineage birth rows (Sim
   build, queued behind D078 on worker 7; validated by an unchanged tiled replay). The
   scorer reads it when present and prints "flag absent" on older runs. It reports both
   readings side by side: the owner's literal wording, and the ≥ 10-through-two-lifetimes
   reading the review recommends, so the ruling can pick one without a rebuild.
3. **Round 28's replay prediction made measurable.** `launch-r28.ps1` gains `-DigestEvery`;
   `r28-s1` and `r28p-s1` both run at 100. The probe is lengthened from 3,000 s to 10,000 s,
   since it starts on the free worker 7 and finishes long before `r28-s1`. 0070 amended
   before launch, marked as such.
4. **A current-build round-18 check.** `launch-r18.ps1 -Seed 1` for 3,000 s on the D078
   build against `r18x-s1`'s first 3,000 s. If it replays, the historical five stand as
   the same world. If not, 0070 says the control is a different build, the cross-seed
   reading stands, and a same-build tiled control group (five arms, about 30 machine
   hours) goes to the owner.
5. **0070's wording.** M4 reads the scored clade, not the aggregate. The treatment is named
   as the package it is (box, wrap, placement, top, bed, contact). If M1 fails with M2–M6
   holding, the first follow-up candidate is the same box with creature collisions off,
   before any change of width.
6. **Run identity.** DESIGN §7 and CLAUDE.md say an evaluation is reproducible from
   (genome, seed, configHash); after D078 the sentence adds the build's `simHash` and the
   physics worker count, both from the manifest.
7. **Documents.** DESIGN §5A's status line and the milestone table's rows 6 and 8; README's
   milestone rows; HANDOFF rewritten from the current state; the research queue reordered
   around open-flow balance, contact feeding and movement-assisted foraging.
8. **Round 27's results** report the late total-matter slope against the influx and the
   influx-versus-burial agreement over the last two lifetimes, as numbers, so the owner's
   balance ruling has data in front of it.
9. **Movement pre-registration** (rewritten on whichever world holds) gains the second
   layer: an active-versus-clamped assay on saved members of the jointed clade.

## Agreed, held for the owner

- The producer threshold (one living inherited member with a recent birth, or ≥ 10 through
  two lifetimes). Both will be printed; the ruling picks.
- A late resource-balance rule as a separate decision, with its tolerance.
- Multithreaded physics allowed for labelled screens. D078's build keeps
  `EVOSIM_PHYSICS_JOBS` settable and recorded, so this needs only the sentence.
- Predation: the injury pool with fixed geometry, dt 0.01 from the first screen, stable
  contact keys, an explicit internal matter reserve. All for the proposal's ruling.
- Extending a passing seed past 30,000 s to test balance.
- The maintenance assay (multi-genome inoculum, cell-type mutation off). A good instrument,
  after round 28; it is a tool the owner should scope.

## Not agreed

- "Round 28 should not launch in its current form" as a delay. The three fixes are right
  and are queued, but they all land before the workers free up (round 27's last arms end
  in four to five hours), so the launch date does not move.
- A 10,000-s zero-worker pair at full population as a separate build gate. The 3,000-s
  `jobs-a`/`jobs-b` pair is the build gate; `det6` covers 10,000 s at 518 bodies; the
  10,000-s probe inside round 28 covers the full population, and M7 reads it.
- Re-scoring the whole campaign for the producer birth clause. It cannot be done: old
  lineage files have no flag. The clade change can be, and will be.
