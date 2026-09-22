# Cambrian has stronger instruments, but its spatial rules need another correctness pass

This is a fresh review of the project on 12 September 2026, following the review and response of 7 September. I reviewed the current implementation, recent experiments, documentation, research record, and theatre direction. I also ran the standard Core tests and small independent probes.

The project has advanced substantially. Organisms now grow, resources occupy a three-dimensional grid, corpses persist, offspring disperse, and the water carries bodies through space. The theatre has become a useful research instrument. Experiment reports also handle failed predictions and source identity more carefully than before.

My main concern is now spatial correctness. I reproduced a transporter that creates concentrations from uniform water while conserving the total. I also found that tank placement accepts bodies intersecting the wall, and that the occupancy instrument can report more occupied columns than exist. These affect the mechanisms the current experiments are trying to explain.

I recommend a correctness interval before adding the dilute tank, terrain, or another ecological mechanism. Preserve the current runs and their recorded outcomes. Repair the spatial rules, verify them independently, then establish a corrected baseline before drawing further causal conclusions about crowding or movement.

## The review has a defined snapshot and scope

The starting commit was `a73e535fb7167eddeda56cb2a414e6a82d8a4f8f`. During review, `55d14ac39ae60d420281e2b6597b844e56e8ab29` added articulation research notes. I read those notes; that commit changed no tested simulation code. Round 37 was still running during the bounded checks of its reports.

The working tree already contained a modified documentation licence and an untracked theatre design directory. I included that brief in the review and left the existing work alone. No simulator source, launcher, experiment setting, or Unity worker was changed. This review and its [supporting evidence](logbook/specs/astra-2026-09-12-review-evidence.md) are the durable outputs.

I kept implementation review and reproductions in the main context. The three earlier review agents covered design and decisions, experiment history, and research and reader-facing documentation. I checked their material findings against the implementation before including them. The coverage and verification limits appear at the end.

The immediate priorities are these:

| Finding | Priority | Evidence | Consequence |
|---|---|---|---|
| F1: uniform water develops artificial concentrations | High | Reproduced in Core, in both box and tank | Local food and matter gradients cannot yet support the intended ecological interpretation |
| F2: tank placement tests the centre rather than the body | High | Unchanged placement code exercised with arithmetic shims | A birth can begin intersecting the static wall |
| F3: occupied-column numerator and denominator use different geometry | Medium | Implementation and live reports, including `110/100` | Round 37's spatial endpoint needs recomputation |
| F4: public comparison script still exits successfully on disagreement | Medium | Reproduced | Automated replay checks can accept a mismatch |
| F5: the scorer remains a partial goal check | Medium | Current scoring branches and declared unresolved clauses | An unqualified PASS can be mistaken for the complete project goal |
| F6: current-state documents contradict one another | Medium | Current specification, handoff, README, and UI brief | Subsequent work can start from an obsolete model of the project |

## The project has made several valuable changes

The separation between Core and Unity continues to pay for itself. I could test the actual resource transporter without opening an editor or borrowing a worker. The field interface also keeps feeding and lifecycle arithmetic mostly independent of the resource representation. That is useful architecture for this project.

The two accounting identities are valuable. Energy and matter have different sources, transfers, and losses, and the implementation records them separately. Growth treats new tissue as a transfer from reserve, and corpse stocks remain inside the accounting boundary until they decay. Independent ownership totals and short-take counters make failures easier to locate.

The earlier review produced real repairs. Frozen feeding availability and actual-draw credit remain in place. Zero-part offspring no longer lock phantom matter. Global-brain execution was retired through an explicit world decision. The scorer now excludes births after its observation window and labels wall-clock truncation. The digest comparator preserves its failure exit code without an optional body dump.

The theatre also changed the quality of the science. It exposed the lineage ribbons that scalar reports had missed. Position logs, spatial readers, and committed pictures now make similar claims inspectable. The correction to Play-mode replay matters: a check that follows the actual viewing path is more useful than a convenient harness that happens to pass.

Round 36 is well documented. Its predicted standing-joint bar failed, and the entry says so. The unexpected surviving line was investigated by guild, ancestry, expression, and ledger comparisons. The always-on drive limiter was tested and rejected when its criterion failed. Those are practices to preserve.

## F1: the discrete transporter creates gradients from a uniform field

A spatially uniform dissolved concentration should remain uniform when carried by incompressible water in a closed container. With no organisms, sources, sinking, or burial, there is nothing to create a food patch. Diffusion also leaves a constant field unchanged.

I seeded the actual grid implementation uniformly at one unit per cubic metre. I used the campaign's current seed stream, a speed of 0.1 m/s, a period of 6,000 seconds, and half-second steps. The footprint was 100 square metres and the depth 60 metres. I tested both container shapes and both cell sizes.

These are the results after 600 simulated seconds with the campaign mixing rates. Every row began at a concentration of 1 throughout.

| Container | Cell size | Diffusivity | Minimum concentration | Maximum concentration | Spatial standard deviation / mean |
|---|---:|---:|---:|---:|---:|
| Box | 1 m | 0.02 m²/s | 0.357 | 2.454 | 30.4% |
| Tank | 1 m | 0.02 m²/s | 0.210 | 3.064 | 29.2% |
| Box | 5 m | 2 m²/s | 0.907 | 1.113 | 5.2% |
| Tank | 5 m | 2 m²/s | 0.889 | 1.146 | 7.1% |

The total remained conserved to roughly one part in a quadrillion. With mixing disabled, the tank's metre-cell maximum reached 13.8. This is a large local distortion hidden by an excellent global balance.

The implementation explains the failure. The transport sweep samples horizontal velocity at a cell's centre, then uses that sample for its east and front faces. Vertical velocity is sampled at an interface. The resulting face fluxes conserve stock between neighbours, but they do not enforce zero discrete divergence. The sequential axis sweeps and the tank's stepped boundary also need validation as part of the complete operator.

Relevant code is [GridField.cs](src/Evosim.Core/Environment/GridField.cs), lines 1340–1499, especially the horizontal sampling at 1366–1371 and horizontal fluxes at 1419–1429 and 1489–1499. The continuous current's divergence tests do not test this discrete operator. The existing transport tests check total conservation and non-negativity; the tank tests also check dry cells. Those properties can all hold while concentrations become wrong.

This affects local feeding, matter available for growth and conception, chemical sensing, and the apparent reward for leaving a depleted cell. It is relevant to the D088 transport world, including rounds 34–36, and to the tank in round 37. I have not established how much it changed any particular lineage or final verdict.

The repair needs a discrete flow representation that preserves constant concentration, including at boundaries. Correctly locating samples on faces is part of that work; sampling the continuous field at face centres alone is not a complete guarantee. Test the actual flux balance, the full split-step operator, and resolution sensitivity. Retain conservation and positivity checks beside the new constant-field check.

After repair, run controlled sitter-versus-mover assays again, then establish a corrected ecological baseline. The existing runs remain reproducible records of their implementation. Their spatial explanations need a dated limitation until the effect of the repair is measured.

## F2: the tank can place a body through its own glass

The placement method receives a candidate position and a bounding radius. Its documentation says it checks whether that sphere is clear and in the water. In the tank, however, the boundary test checks only the candidate's horizontal centre. The radius is used against other creatures, but never against the wall.

See [SharedVolume.cs](unity/Assets/Evosim/Sim/SharedVolume.cs), lines 450–493. Both founder and offspring placement reach this method. Founder centres are drawn over the full disc at lines 654–670, so the missing clearance is reachable through ordinary placement.

I compiled the unchanged placement class outside Unity with small arithmetic shims. A candidate centre one centimetre inside a wall slab was accepted with a half-metre bounding radius. That sphere extends 49 centimetres beyond the slab's inner face. In 1,000 independent placements of a unit-cube founder, 208 cubes had a corner outside the mathematical circle.

This reproduction tests placement geometry. The 208 cases cross the mathematical circle; they were not individually tested against the circumscribed slab colliders. The probe does not establish a resulting PhysX impulse, mortality, or effect on the running round. If a placed articulation intersects a static collider, the solver must resolve a contact that the birth gate was supposed to prevent.

The current smoke checks founder centres and root trajectories. It does not establish whole-body clearance at birth. Its root tolerance is 25 centimetres. Those checks can pass while a newly placed limb or part crosses the glass.

Require wall clearance for the reserved adult envelope, or perform an equivalent whole-body geometry test. Test founders and offspring near slab midpoints and joins, including large and articulated plans. Keep the distinction between reserving future size at birth and guaranteeing separation throughout later growth and movement.

There is also an arithmetic error in the comments of [TankWall.cs](unity/Assets/Evosim/Sim/TankWall.cs), lines 25–32 and 63–68. The stated polygon formula gives about 12.1 mm at 100 m² and 24.2 mm at 400 m². The comments say 1.2 and 2.4 mm. Construction follows the specified 48-sided prism; its comments understate the implemented prism's radial excess by ten times.

## F3: the occupancy fraction can exceed one hundred percent

The tank counts columns whose centres lie inside the disc to obtain its denominator. It then counts every occupied column in the enclosing rectangle for the numerator. A body can be inside the circle while occupying a boundary cell whose centre is outside it.

The mismatch is in [Ecosystem.cs](unity/Assets/Evosim/Sim/Ecosystem.cs), lines 828–850 and 862–881. The offline reader repeats it: the common occupancy method counts all indexed columns, while the tank denominator uses the circular mask. See [positions-read.py](scripts/positions-read.py), lines 181–188 and 282–299.

This is already visible in round 37. At 3,200 seconds, seed 5 reported 628 living bodies and `110/100` occupied columns. The following sample reported `111/100`. These observations do not establish that bodies escaped through the glass. They establish that the fraction combines different sets of columns.

Use the same definition in numerator and denominator. Either count the specified centre-inside columns in both, or define a different consistent footprint measure and document it. Add a boundary-cell fixture and require occupancy to remain within its denominator.

Round 37's M2 endpoint uses this instrument. Recompute it from the saved positions after the correction and append the corrected reading. There is no need to rerun physics merely to repair this measurement.

## F4 and F5: the command-line verdicts still need firmer contracts

The current [compare-det.py](scripts/compare-det.py) prints a mismatch and exits zero. I reproduced it with two one-row statistics files that differed only in the living count. It also compares intersecting times and keys, skips unmatched samples, and takes the first matching run directory. A successful process exit therefore does not establish replay identity.

The earlier response discusses a corrected scratch copy. The tracked public script remains wrong, regardless of what that scratch copy contained. Repair the public entry point and test disagreement, no common samples, unequal coverage, missing fields, and ambiguous run directories.

The [digest comparator](scripts/digest-diff.py) now returns failure on differing hashes without body dumps. That repair is confirmed. Its success contract remains agreement over shared digest steps, which its wording states. A caller needing complete replay coverage must also require the expected interval and sample coverage.

The [clade scorer](scripts/clade-score.ps1) has improved too. Its six fixtures passed all 39 assertions. The wall-clock case now receives a censoring label, and future lineage births cannot recruit for an earlier observation window.

It still does not independently establish the complete project goal. The main PASS branch checks four absorptive-clade clauses. Producer readings are printed separately and do not decide the verdict. The script also does not establish a 30,000-second goal duration or closure of the population floor from the configuration. I gave a passing fixture a completed 10,000-second budget manifest. It received an unqualified clade PASS.

Keep that analytical reading, but give it a precise name. A complete goal verdict should combine the chosen producer clause, required duration, population-floor condition, and run validity with the clade result. Until those choices are settled, separate the clade result from goal eligibility in the output. This is partly an unresolved owner rule and partly an instrument contract; it should not remain implicit.

One smaller testing issue appeared under Windows PowerShell 5.1. The positions suite passed its positive cases, then aborted on expected stderr from a missing arm before checking the exit code. The failure is at [run-tests.ps1](scripts/tests/positions/run-tests.ps1), line 179. Test expected native-command failures without turning their stderr into a terminating shell error.

## The experiments support narrower causal claims than some conclusions suggest

Round 36 provides evidence that adding income to link tissue changes persistence under the recorded world. Its matched ledger shows that a fixed-joint version earns the same link income. The treatment therefore does not isolate the benefit of an active hinge. The write-up admits that distinction in its analysis; its concluding explanation should preserve it.

The surviving jointed absorptive line in seed 1 is an interesting result. It does not yet show that swimming earned a reproductive advantage. Current speed measurements include passive carriage, and the free-joint conditions deliberately remove much of the energetic penalty. A matched active-versus-fixed assay, with food acquired and offspring produced as outcomes, remains more informative than a joint count alone.

Likewise, the free-price package in round 34 changed several costs together. It improved founding, but it cannot identify which cost created the barrier. The association between jointed newborns and early death implicates articulation; it does not by itself identify the lethal mechanism. The failed drive-limiter check is evidence against one proposed remedy, rather than proof that drive behavior is irrelevant.

The new articulation research note usefully labels its evidence as provisional. One proposed explanation needs correction before it guides work. It says growth can drive the mass ratio between links past a threshold. Ordinary growth scales all parts by the same length factor, and both dry and added mass scale with volume. Their ratios should therefore remain constant, outside exceptional clamps or engine behavior. Absolute inertia, contact geometry, and anchor changes can still matter. A trace should measure those quantities rather than assume the ratio changed.

The planned trace immediately before a solver failure is a good next instrument. Preserve per-link positions, velocities, masses, inertias, drive commands, joint limits, recent resizes, and relevant contacts. The 50-dump cap is already disclosed. Aggregate divergence counts should also be paired with exposure: jointed body-time, age, and morphology. A larger population of jointed adults can produce more failures without a higher failure rate.

Round 37 changes a package: topology, wall contacts, current construction, grid mask, patch geometry, and placement boundaries. It can test whether that package supports a standing world. It cannot assign every difference to glass alone. Its two-sided explanations should remain hypotheses for follow-up controls.

Nearest-neighbour distance also changes with abundance. Round 37 permits a broad population range while comparing raw neighbour distances against the preceding round. Compare those distances with a baseline at the same abundance and depth distribution before interpreting them as a change in crowding behavior.

Round 38 will also be a compound treatment. Quadrupling area while holding matter fixed lowers matter density, but it increases the potential sunlight budget and changes spatial scales. It changes dispersal distance relative to the container and the grid's workload. Fixed total matter does not fix body count, turnover, or evolved body size. Read the experiment as the dilute-tank package, and measure its intermediate effects.

The repeated five-seed counts remain useful for discovery. Because those same founding lotteries have guided an adaptive sequence of changes, a fresh seed batch is needed before a durable general claim. The handoff already queues that check. I would keep it as a required confirmation after the spatial repairs.

## The experiment policy needs one current statement

D081 says a change joins the base only when it matches the reference world's passing-seed count. The grid nevertheless became the base after two passes against the vertex world's four. Round 37 explicitly reads the goal rule without requiring it. Later owner rulings may justify these replacements, but the standing adoption rule has not been reconciled with them.

Distinguish necessary corrections to the represented world from optional ecological treatments. State when a baseline is being replaced for correctness or scope, and when a treatment must earn adoption against a reference bar. That allows the project to improve its world without pretending every replacement met the earlier acceptance rule.

There is also a small but concrete preregistration breach. The commit adding round 37's predictions was recorded at 08:31:49 BST. Its five manifests began between 08:28:48 and 08:31:07. The exact thresholds were therefore archived after launch, contrary to the repository's written rule.

This difference of minutes is not evidence that thresholds were chosen after inspecting results. Record it as delayed archival commitment. Have the launcher require a committed prediction document and store that commit in the manifest. Future readers should not need a timestamp investigation to establish the sequence.

## F6: the current documentation needs reconciliation rather than another layer

The repository has a strong memory, but its current-state pages are becoming historical records themselves. HANDOFF contains several generations of status, queued work, and owner questions. Its older statement that no later world matched the original goal sits beside multiple newer five-of-five results.

Rewrite HANDOFF as the present state and the next decisions. Keep history in the logbook and link to it. A new reader should be able to identify the current world, its baseline, running work, unresolved correctness issues, and next permitted action in one short pass.

These discrepancies deserve a targeted documentation pass:

| Document | Current discrepancy | Needed correction |
|---|---|---|
| DESIGN, opening and environment sections | Stops at D088 despite the launched D089 tank | Integrate the current tank, gyre, mask, and matter-budget rules |
| DESIGN, physics section | Says the timestep is hashed and outside the hash | State the implemented contract once |
| DESIGN, brain section | Still presents global-brain input as operative | Mark the retired execution path consistently |
| CLAUDE, growth and placement notes | Describes both birth-radius and adult-radius reservation | Retain the current rule and date the old incident |
| HANDOFF and D089 index | Suggest corpse objects begin in round 38 | State that the decay mechanism has been enabled since round 32 |
| README, status and run layout | Lags recent rounds; calls genome snapshots world state and says runs never store positions | Explain current records, including positions and the absence of resumable checkpoints |
| Primer 05 | Calls implemented senses unwired and equates simultaneous spatial sensing with bacterial chemotaxis | Update implementation status and reconcile the biological analogy with round 6's temporal-sensing account |
| Primer 06 | Gives stronger biological support to a flat light-income release fraction than the research review does | Preserve the review's limitation about the light response |
| Predation proposal | Admits growth invalidated its premises but retains the obsolete integrity-pool argument | Recut it before a future ruling; keep its current deferred status visible |
| STYLE and licence summaries | Retain conflicting attribution/noncommercial labels and unclear treatment of prose inside code directories | Align the descriptions with the declared licence scopes |

The licensing observations concern internal clarity. I have not reviewed the legal effect of the licences or contributor grant.

The public onboarding path also remains incomplete. A fresh clone cannot open the ignored historical run used by the theatre instructions. Supply a short, reproducible demonstration command using the current format, or a deliberately maintained demonstration artifact with suitable provenance. A spike and a smoke test demonstrate components; the README advertises an ecosystem that a newcomer should be able to reach.

## Research provenance improved, but its summaries have drifted again

The addition of PDF checksums is worthwhile. The literature review also records partial reading, source dependence, search deviations, and AI involvement more candidly than many research projects do. The separate theatre-look survey correctly treats rendering references as design precedent.

The cumulative counts are nevertheless inconsistent. There are 54 local source packages after the seventeen additions in round 6. Several summaries still say 37; one says 45. The public question count also trails the eleven-question review. These are simple errors with a disproportionate effect on trust because the protocol explicitly requires cumulative reconciliation.

The retrieval registry has two entries whose original retrieval route was not recorded. Most source packages still lack extraction manifests. The hashes identify the PDFs, but they do not reproduce the extracted Markdown or its page mapping. Keep that distinction explicit and preserve extraction tool versions and commands as packages are next touched.

The newest synthesis is useful, but its answer labels should match its stated evidence limits. A universal claim about how nature solved movement and sensing reaches further than selected passages across several literatures. Describe the bacterial and animal mechanisms supported by those readings, then label the project-wide conclusion as synthesis or inference.

I did not reread the 54 third-party papers or verify their claims externally in this review. These findings concern consistency between the project's own evidence appraisal, summaries, and design conclusions.

## The theatre should make uncertainty and geometry easy to inspect

I inspected the committed round-36 close view and round-35 top view. The visual direction is coherent: the bodies have recognizable material and shape, and the top view makes spatial occupation immediately legible. The close view is very dark in places, with fine detail and some guild differences difficult to read.

Keep the atmospheric presentation, but provide a dependable inspection view with readable illumination, a scale, a guild legend, and visible collider boundaries when needed. The tank-placement finding makes geometry inspection particularly useful. Appearance can stay inside a collider while still giving a misleading impression of where physical contact occurs.

The new UI brief starts from the right user questions. Its most valuable priorities are identifying the run, distinguishing replay checks from unchecked playback, finding a selected creature, and understanding change over time. Its handling of absent historical data and expensive seeking is thoughtful.

Fix its data semantics before implementing the screens. The brief groups mean depth and local matter under invariants while omitting the actual matter residual. Those are different kinds of quantity. A world can change depth or exhaust one cell while its accounting remains correct. Conversely, a rounded zero should not conceal a residual outside an agreed tolerance.

The same distinction applies to faithful replay. Matching source identity and selected aggregate samples is evidence with a defined coverage. It is stronger than an unchecked rendering, but weaker than comparing complete world state at every step. Display what was checked, how far the check reached, and any mismatch. Avoid turning a limited check into a universal identity badge.

The brief's nine surfaces and numerous state artboards are a large next project. I would implement the world view, selection, and a small timeline first. Use them on an actual research question before building the entire gallery and chart system. Watching a failure in time is currently more valuable than another cosmetic parameter.

## Reproducibility needs an accessible historical build, not only a mismatch warning

Strict loading and source fingerprints prevent silent substitution. They also make older runs inaccessible whenever configuration or genome formats move. The project now delays merges until earlier renders finish, which shows that this is an operational constraint.

Preserve a usable build path per important round: a recorded source revision, exact configuration and schema, Unity and package settings, and a documented way to render it. A controlled worktree or retained build is sufficient initially. A general migration framework is not required before solving the immediate viewing problem.

Source hashes also depend on checkout bytes, including line endings, and omit some environment inputs. The current warnings acknowledge that. Record the remaining inputs needed to recreate a build and standardize future source checkouts deliberately. A fingerprint that detects a mismatch is valuable; it should be accompanied by a practical route back to the matching environment.

The codebase would also benefit from extracting pure placement geometry and instrument calculations into Core. The arithmetic shim used in this review demonstrates that these particular checks do not need a physics engine. Make that logic directly testable rather than relying on comments that describe it as structurally untestable.

I would avoid a broad rewrite. Separate geometry, transfer rules, and diagnostic aggregation at the places where the new failures exposed a boundary. Reduce duplicated definitions between runtime reports and offline readers, while retaining independent arithmetic fixtures that can catch a shared mistake.

## The next work should establish a trustworthy spatial baseline

1. Repair the transporter and require constant-field preservation, conservation, non-negativity, and boundary checks in both shapes.
2. Repair whole-body wall clearance and test birth placement at the actual glass geometry.
3. Correct both occupancy implementations and recompute the affected spatial readings from saved positions.
4. Finish the public script exit contracts and distinguish a clade reading from complete goal eligibility.
5. Run the controlled movement assay and a corrected baseline before interpreting the planned dilution. Add fresh seeds for confirmation.
6. Keep the per-link failure trace ahead of further conclusions about why joints die. Normalize failure counts by relevant exposure.
7. Reconcile current documents and ship a small usable theatre inspection workflow with a reproducible demonstration.

Preserve existing results with their builds and original predictions. Append what the new checks limit. The strongest next improvement is confidence that a food patch, a crowded region, and a creature's death mean what the instruments say they mean.

## Verification and coverage limit what this review claims

The standard Core suite passed 653 tests in 78 seconds. The 13 slow experiments were excluded; I did not rerun the full 666-test suite. The clade scorer passed 39 assertions across six fixtures, and the feeding-log reader passed eight checks. The positions suite aborted during its expected-error checks under Windows PowerShell 5.1.

The new probes exercised the current Core transporter and unchanged placement logic. Placement used arithmetic shims and did not run PhysX. I also checked the current script failure exits, a censored scorer fixture, and bounded samples from live round-37 reports. Exact probe sources, output, and source fingerprints are preserved in the [evidence file](logbook/specs/astra-2026-09-12-review-evidence.md).

Documentation coverage included all current root guides, the current design and changed decisions through D089, and entries 0071–0093. It covered relevant build specifications and read materials, all primer chapters, and authored research summaries. It also included the new theatre brief, predation proposal, licence descriptions, and launcher and reader guides. Earlier decisions and logbook entries informed this pass through the previous review and targeted rereading; I did not reread every historical line.

The third-party research corpus was inventoried rather than fully reread. I inspected two committed images and selected implementation paths, rather than every shader or every recorded creature. No full historical campaign was rerun. These limits leave room for additional defects; they do not reduce the reproducibility of the three spatial findings above.
