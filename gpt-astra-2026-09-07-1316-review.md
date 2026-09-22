# The project has a strong foundation, but resource correctness must come before the next ecological claim

Reviewed on 7 September 2026, starting from commit `c8f9c98034a252aebbe56a34fff1b73617112d0e`. The filename uses Europe/London time, UTC+01:00.

I would keep this architecture and continue the project. Its strongest feature is the willingness to build instruments that disprove an attractive result. The separation between the simulation's engine-independent rules and Unity's physical bodies makes that possible. The research history also preserves failed ideas and the reasons they failed.

The immediate problem is that several basic rules do not hold in the implementation. I reproduced unequal feeding between identical creatures, energy creation during capped feeding, and matter trapped by an offspring that never exists. I also reproduced executable global neurons receiving no neural energy bill. The current tests all pass. These findings warrant a correctness checkpoint before interpreting another change to the ecology.

I have not measured their effect on historical runs. Round 18's reported four-seed success remains a result of its recorded simulator. It is not yet evidence that the same result survives corrected resource allocation. Round 28 was still running at the review snapshot, so this review does not give it a final verdict.

## 1. The review combines direct inspection with delegated reading

I read the operating guide, handoff, style guide, and complete design directly. I inspected the implementation paths for development, neural control, metabolism, reproduction, fields, configuration, Unity integration, replay, and scoring. I ran the existing Core tests and added isolated diagnostic programs under the ignored scratch directory.

For the larger prose corpus, I used three GPT-5.6-sol sub-agents. The first read the complete decision history. The second read every numbered logbook entry, the primer, and the physics spike. The third reviewed the research documents, public instructions, licences, proposal, and working documents. I retained the main design and implementation evidence in my own context and checked consequential findings against their source. This kept the historical reading out of the main context without delegating the final judgement.

The review covers the project's authored documentation. Third-party papers were inventoried and selected source-package details checked; their full texts were not independently reread. I did not revalidate the literature through external research. Generated Unity files, duplicate worker checkouts, and the full historical run corpus were outside the reading scope. The coverage and verification notes at the end make those boundaries explicit.

I changed no simulator source, configuration, decision, or historical result. I did not stop or relaunch an existing experiment. The recommendations in this review remain proposals for subsequent work.

## 2. Four reproduced faults deserve immediate attention

The findings below distinguish a demonstrated defect from its possible ecological consequences. “High” means the issue can change the simulated economy or undermine a central claim. It does not mean that every current run exercises that path.

| Finding | Priority | Evidence | Consequence |
|---|---|---|---|
| R1. Feeding uses changing stock during allocation | High | Reproduced in Core | Identical feeders receive unequal meals; capped feeding can create energy |
| R2. A bodyless offspring locks fixed matter | High | Reproduced through ordinary mutation | Matter disappears from circulation while the total audit appears closed |
| R3. Global neurons execute without a neural bill | High before behavioural claims | Reproduced in Core | Computation can evade the tissue-based cost model |
| R4. Scoring counts births after its report cutoff | High for result validity | Reproduced in the scorer | An unchanged report changes from FAIL to PASS using future recruitment |

### R1. Identical feeders receive unequal meals, and capped feeding can create energy

The allocation pass reads stock that earlier creatures have already changed. The demand pass first estimates every creature's appetite. The consumption pass then walks the population backwards. It calculates each share from the remaining stock and applies that share to the remaining density. Earlier meals therefore change both terms used to price later meals.

With two identical absorptive bodies in the same cell, each demanding 10 J from a 10 J pool, equal sharing would give each 5 J. The actual meals were 5 J and 1.25 J. The pool retained 3.75 J despite both creatures having unmet demand. Admission order was the only difference between the bodies. The later list entry ate first.

The capped case also breaks conservation. With a 6 J pool, high clearance, and a feeding cap of 10 W per cubic metre, both bodies received 5 J. The pool lost only 6 J. The energy audit changed by -4 J, indicating 4 J had been created. The field correctly capped the actual withdrawal. The creature's credited ledger retained the larger planned income.

The relevant code is [World.cs](src/Evosim.Core/Ecosystem/World.cs), lines 960–1056, especially the rationing branch and withdrawal. [NutrientField.cs](src/Evosim.Core/Environment/NutrientField.cs), around lines 313–390, supplies the mutable density, share, and capped withdrawal. The proportional-sharing rule appears in [DESIGN.md](DESIGN.md), around line 1194. The drawn-versus-kept accounting contract appears in [D026](DECISIONS.md#d026).

This is separate from the already documented conception queue. It creates another path through which population order can affect selection. Same-step exudation and death deposits also deserve attention because they occur during the consumption loop.

The inspected round-28 seed-1 configuration has the feeding cap disabled. The energy-creation probe therefore does not establish that this particular branch affected that run. The uncapped allocation defect has broader reach, but its historical magnitude remains unmeasured.

I recommend freezing availability for an allocation interval, calculating each body's valid allocation, and applying matching debits and credits. Capped acquisition must be handled as a nonlinear response; scaling density alone does not guarantee a valid allocation. Add tests for reordered identical feeders, heterogeneous feeders, exhausted stock, conversion loss, and the timing of deposits. Decide explicitly whether new detritus becomes available during the same interval or the next one.

### R2. A bodyless offspring removes matter from circulation permanently

Conception charges matter before admission rejects a phenotype with no parts. The fixed matter charge remains positive even when tissue volume is zero. If admission fails, the parent pays, the field loses matter, and the cached body-matter total increases. No living child owns that matter, so no future death can return it.

The probe used a viable photosynthetic root just above the minimum development volume. A normal scalar mutation shrank it below that limit. With seed 1, one conception produced one stillbirth and no living child. The free matter pool fell from 100 to 98. The cached body-matter total became 2, while the sum actually held by living creatures remained zero.

The reported standing total still read 100. That total adds free matter to the same cached counter that the failed birth incremented. It therefore cannot reveal this orphaned balance.

See [World.cs](src/Evosim.Core/Ecosystem/World.cs), lines 1593–1667 for the charge and admission, around line 1909 for the rejection, and lines 241–244 for the standing total.

This revises an existing reassurance. [D052](DECISIONS.md#d052) treated bodyless offspring as harmless because their tissue price was zero. [D065](DECISIONS.md#d065) later introduced a fixed per-creature matter charge. That invalidates the old argument. The handoff's queued transaction test is therefore guarding a real reachable fault, rather than a hypothetical future admission refusal.

I recommend rejecting an empty body before locking matter, or rolling back the material transaction on failed admission. The treatment of energy spent on a stillbirth can remain a separate explicit rule. Add an independent check that the cached body-matter total equals the sum held by actual living bodies. A total conservation equation alone is insufficient.

### R3. The global brain bypasses the neural energy model

Global neurons remain legal, mutable, and executable. Metabolism bills only neurons attached to developed body parts. The global array has no body part, so its neurons and connections add no neural charge.

The probe constructed one local neuron reading a global neuron, plus 100 global neurons with constant inputs. After two brain steps, the local output was 1. The runtime reported 101 executing neurons. With a cost of 1 J per neuron and 1 J per input over one second, the neural bill was 2 J. It covered only the local neuron and its input.

This is an evolvable path. Mutation can add neurons to an initially empty global array. I have not demonstrated that historical populations evolved useful free global computation. The demonstrated problem is that the implementation permits it.

See [Genome.cs](src/Evosim.Core/Genome/Genome.cs), lines 29 and 217; [Mutator.cs](src/Evosim.Core/Mutation/Mutator.cs), line 68 and the neuron-addition branch; and [Brain.cs](src/Evosim.Core/Brain/Brain.cs), the global group construction and step loop. [Metabolism.cs](src/Evosim.Core/Ecosystem/Metabolism.cs), lines 250–266, counts only part-local neurons.

[D019](DECISIONS.md#d019) explicitly objects to computation with no tissue, location, or exposure to damage. Its proposed economy gives neural tissue a discount so brain placement can evolve. A free global array defeats that mechanism.

The owner should choose the intended treatment of global computation: remove it from new genomes, map it to tissue, or give it an explicit cost and physical interpretation. Migration must preserve historical genome readability and build identity. Add a test that every executing neuron belongs to a declared billing rule. A numerical bill alone would close the energy loophole but would not settle D019's anatomical question.

### R4. Future births can turn an unchanged report into a PASS

The scorer counts recent inherited births after the start of a time window but never checks that they occurred before its end. It reads the report and lineage separately. A live lineage can be ahead of the latest report, so this is a plausible input state during monitoring.

I constructed a report ending at 10,000 seconds. It contained a stable connected family of 11 absorptive creatures but no recent recruitment. The scorer correctly returned FAIL. I then appended one inherited birth at 11,000 seconds to the lineage file. Without changing the report, the scorer returned PASS.

The defect is in [clade-score.ps1](scripts/clade-score.ps1), lines 271–282. The recent-birth condition has a lower time bound and no upper bound. The separate file reads occur around lines 335–351.

There is a broader validity gap. The verdict does not check a completed run manifest, required duration, floor closure, or matching experiment identity. Its recruitment window also assumes that 20 samples means 2,000 seconds, although reporting cadence is configurable. The producer readings are diagnostic and do not gate the main PASS. That last choice is partly acknowledged because the producer rule is unsettled.

I recommend one explicit observation cutoff for every input. Reject future events and validate report cadence. Distinguish “this clade meets the measured conditions at this cutoff” from “this completed run meets the experimental rule.” Missing, running, censored, and incompatible evidence need separate states. Keep live scoring useful, but make its provisional meaning visible. Resolve the producer requirement before presenting the verdict as the whole D063 rule.

## 3. The experiment rules need a single current interpretation

The project's preregistration discipline is valuable. Several active rules nevertheless conflict, and those conflicts can change which experiments count as successes.

### Three-of-five and four-of-five serve different decisions

[D063](DECISIONS.md#d063) sets the formal goal at three passing seeds out of five. [D079](DECISIONS.md#d079) says each new round asks that rule, while the current handoff compares changes against round 18's four-of-five result. [D074](DECISIONS.md#d074) already distinguishes a three-seed goal pass from matching the four-seed reference.

Those can coexist as different thresholds: one for demonstrating the goal, another for adopting a change to the reference world. They must be named separately. Otherwise “failed the round” can mean “missed the formal goal” or “did worse than the reference.”

The sequential rule in [D069](DECISIONS.md#d069) makes this consequential. It proceeds to seeds 3–5 only if a lineage appears in seed 1 or 2. Two initial failures rule out four successes, but they do not rule out three. The remaining three seeds could all pass.

The owner should confirm which endpoint each campaign serves. An instrument can then enforce the corresponding stopping rule without making a scientific choice on the owner's behalf.

### The active early-stop rule relies on a premise that later runs contradicted

D069 also allows failure at 15,000 seconds if no inherited absorptive lineage has appeared. Its rationale was that consequential lineages had not started later.

Subsequent evidence contradicts that premise. [Logbook 0067](logbook/0067-the-shared-world-at-the-fine-step.md), lines 152–165, reports valid completed passes rooted at 15,867 and 21,641 seconds. Successful lineages can therefore originate after the cutoff. The decision index still marks D069 active, so its rationale needs reassessment against the later evidence.

The stopping condition itself needs care. Seed 1's report shows no living inherited absorber in any sample through 15,000 seconds. Its raw lineage nevertheless contains an inherited birth at 406.5 seconds, creature 98, which disappeared between samples. Seed 2 had inherited absorbers alive at the cutoff. Thus these late passing roots invalidate the rationale, but do not prove both arms would be stopped under an “ever appeared” interpretation. Define whether the rule means ever born, alive at the cutoff, or established for a stated duration.

Retire or narrow that rule through an appended decision. Keep the original rationale as history. A stopping rule suitable for a cheap exploratory screen should not silently become a substitute for the confirmation duration.

### Wall-limited results need their censoring qualification restored

The [logbook reading guide](logbook/README.md), lines 98–110, says a wall-clock ending is censored and cannot count as a final outcome. [Logbook 0065](logbook/0065-the-first-shared-water.md), lines 132–169, scores two wall-stopped arms and gives one a pass. It later calls the round uncensored, around lines 235–237.

This makes that screen's four-of-five statement different from a completed confirmation. Annotate the result and any downstream summary that relies on it. Preserve what was observed at the cutoff without implying the missing duration was observed.

### The reference world retains an acknowledged allocation artefact

[D072](DECISIONS.md#d072) identifies oldest-first conception as an accidental list-order advantage. It then retains that order after shuffling harms the absorptive lineage. D079 restores the old reference, so the advantage remains part of the current base.

Preserving a reference is useful for comparison. Preserving its success is not, by itself, a reason to accept an accidental selection rule. The owner should either adopt age priority as a world rule with a rationale, or require a neutral alternative and accept the resulting ecological change. R1 adds a separate feeding-order fault to this already sensitive part of the model.

### Passing the lineage rule does not settle every ecological claim

A connected family carrying absorptive tissue can remain substantially supported by light. That is evidence of inherited absorptive tissue, but it does not establish material dependence on detritus. The handoff already acknowledges this distinction.

I would keep separate outcomes for lineage persistence, detrital dependence, resource balance, and adaptive behaviour. Each answers a different question. A late increase in free matter does not automatically refute lineage persistence. A lineage pass does not establish a balanced world.

The repeated use of five seeds is sensible for paired engineering comparisons. It also means the campaign learns repeatedly from the same founding lotteries. Once rules and instruments are stable, reserve a fresh seed batch for checking whether the conclusion extends beyond those founders. Report per-seed effects alongside pass counts, and keep screens distinct from confirmations.

The first D079 comparison also introduces an architectural package: shared positions, contact, boundaries, placement, and a floor. Its authorisation is clear. Its causal resolution is limited to that package; a failed comparison cannot identify contact alone as the cause.

## 4. The architecture is worth keeping, with stronger boundaries around experiments

The engine-independent Core is the right centre of gravity. The defects above could be demonstrated in small programs without starting Unity. Seeded random streams, reflective configuration handling, explicit cell types, and developed phenotypes provide useful seams for further testing.

I would avoid an engine rewrite or a generic framework expansion. The next architectural work should make failure paths and experiment inputs explicit.

### Inoculation is not fully shared between economics, embodiment, and replay

Static inspection found that Unity's body reconciliation cache counts births, deaths, and floor spawns. Inoculations are absent from that revision count. An injected creature can therefore enter the economic population without triggering body creation until another counted event occurs.

See [Ecosystem.cs](unity/Assets/Evosim/Sim/Ecosystem.cs), lines 1254–1255. The injection path in [World.cs](src/Evosim.Core/Ecosystem/World.cs), around lines 1810–1865, increments a separate inoculation count. The batch runner invokes that path for timed invasion assays.

The Theatre also does not reproduce the batch runner's timed injection event. It reads timing and count as configuration, but I found no equivalent injection in its step path. See [EvolutionRun.cs](unity/Assets/Evosim/Sim/Editor/EvolutionRun.cs), around line 842, compared with [TheatreReplay.cs](unity/Assets/Theatre/TheatreReplay.cs).

These findings come from tracing code paths and still need reproduction in Unity. I recommend a shared population revision and an explicit stream of external experiment events. Until injection replay is supported, reject such recordings with a specific explanation. Verify an inoculation into an otherwise unchanged population, and compare batch and Theatre states across the injection time.

### Replay identity is stronger than before, but it is still incomplete

The million-step replay diagnosis in [logbook 0069](logbook/0069-the-shared-world-does-not-replay.md) is strong engineering work. It found the first physical divergence and demonstrated a working same-environment constraint. Preserve the zero-worker-thread rule for shared contact.

The current source guard hashes C# files in Core and the simulation tree. It does not include package locks, project physics settings, assembly definitions, or the full execution environment. The Theatre loads the recorded Unity version but does not include it in the source comparison. See [BuildIdentity.cs](unity/Assets/Theatre/BuildIdentity.cs), [RunRecord.cs](unity/Assets/Theatre/RunRecord.cs), and the faithfulness assignment around line 149 of [TheatreReplay.cs](unity/Assets/Theatre/TheatreReplay.cs).

Matching source files are valuable evidence, but they do not prove that all replay conditions match. Hashing files on disk also does not directly identify already compiled assemblies. The older same-machine guarantees in D058, D069, and the primer need the later contact/thread qualification.

I recommend a versioned replay contract containing the engine version, relevant settings, package identity, physics step, thread count, and experiment events. Retain existing byte hashes unchanged for historical worlds. If a future hash scheme normalises text, give it a new version rather than reinterpreting old recordings.

The physical digest covers body transforms and velocities. That is excellent for locating trajectory divergence. It is not a complete state checkpoint for fields, reserves, neural memory, and random streams. The Theatre's aggregate checks likewise do not prove full state equality. State these scopes in the tools' output.

The comparison scripts currently work as human diagnostics. [digest-diff.py](scripts/digest-diff.py) compares shared sample steps and reports mismatches without failing the process. [compare-det.py](scripts/compare-det.py) also compares intersecting observations. Before using either as an automated acceptance gate, require expected coverage and a nonzero mismatch exit status.

### Launch inputs should fail early and survive independently of a shell session

The launcher relies on inherited environment variables and handwritten parsing. Some malformed numerical values fall back to defaults. Working-shell settings can therefore affect a run before a person notices the effective configuration in its header. The history repeatedly shows how expensive a setting that reached the wrong place can become.

Use one validated input schema for launching and persistence. Record both requested and effective values, reject malformed input, and run a short validity check before a long allocation. Restore temporary environment changes in a guaranteed cleanup block. Worker refresh scripts should also refuse an active worker rather than relying solely on the operator to remember.

The missing portable reference is the most useful near-term reproducibility improvement. Round 18 currently depends on ignored run directories and a launcher. Preserve its exact configuration, build identity, result summaries, lineage evidence, and selected reference outputs in a small committed package. Include one command that verifies the package and one that opens a demonstration. Full raw runs can remain outside Git if the package records where they live and how to verify them.

### Run artifacts need completion checks and explicit counting definitions

The [floor build report](scratch/floor-build-report.md), lines 127–136, records a wall-ended run with 38 snapshot IDs absent from its lineage. It attributes the gap to an unflushed tail and says it was left unfixed. I did not independently reproduce that historical truncation. It is nevertheless a concrete reason to validate event completeness before joining snapshots to ancestry. A wall-ended artifact should carry an incomplete-state marker. Tests using local ignored runs should state their fixture dependence.

The [digest build report](scratch/digest-build-report.md), lines 85–88, says digest cadence and dump settings live only in the ignored Unity log. Preserve those diagnostic settings beside the digest. They determine what a comparison can observe without changing the simulated trajectory.

The [photosynthetic-flag build report](scratch/photo-flag-build-report.md), lines 200–205, also records 420 manifest births versus 535 lineage birth rows. The extra 115 are floor spawns. This is intentional counting, but both quantities need clear names wherever they are exposed. Otherwise a reader can mistake a definition difference for missing events.

## 5. The research record is useful, but its provenance claims need tightening

The literature review openly describes itself as engineering decision support. It records partial reads, source dependence, and substrate differences. The early-life survey also marks abstract-level evidence and limits its use. Those distinctions should remain prominent.

There are several concrete inconsistencies in the evidence registry.

| Issue | Location | Recommended correction |
|---|---|---|
| The disclosure says every cited work was retrieved and read, while BP91 was not obtained | [LITERATURE-REVIEW.md](research/LITERATURE-REVIEW.md), lines 826–835 and 1087–1088 | Distinguish held primary text, secondary citation, and abstract-level evidence |
| The fetch registry promises every retrieval URL but omits CB18 and PU16 | [FETCH-RESULTS.md](research/FETCH-RESULTS.md), round 3; literature review lines 666–680 | Add the missing source records |
| Round 4 is counted as 13 fetched, but its registry and local packages contain 14 | Literature review lines 43 and 470–489; fetch registry lines 173–261 | Reconcile the per-round totals and repeated paper |
| Most source packages lack a manifest and the extractors are ignored | Local research package inventory | Track extraction code, versions, commands, document checksums, and page counts |

The source inventory contains 37 held PDFs and 37 generated Markdown copies. Only the original eight packages have manifests. Page markers existed for all 21 checked source keys used with direct page locators. That supports navigability of the held copies; it does not independently verify every interpretation or quotation.

The current extraction record lets a reader often find a paper with the right title. It does not consistently let them prove they have the cited edition or reproduce its page-aware text. This can be repaired without redistributing third-party papers.

The opening research synthesis also retains the original external-search argument for protecting promising body plans. The project has since moved to ecological selection, with the archive demoted to observation. Transfer of that argument to ecological niche protection remains a research question. Mark the old conclusion's scope rather than treating it as settled support for both approaches.

The exudation rule has evidence for its broad magnitude, with acknowledged limits. A fixed fraction of current photosynthetic income is a project rule. It should not be described as a faithful model of every biological release mechanism or depth profile.

## 6. Movement and predation need causal demonstrations before ecological interpretation

Movement has costs in the current world, but the history has not shown a sustained advantage from controlled swimming. A moving creature on screen is not enough to establish such an advantage.

The proposed saved-genome assay is the right next instrument once the resource faults are addressed. Compare active control with clamped control for the same body under matched conditions. Repeat across starting orientation and location. Measure the resource benefit, mechanical work, and net energetic result. Then ask whether the same advantage supports a reproducing lineage in the ecology.

Correct the draft's empirical anchor before adopting its thresholds. The [perception build report](scratch/perception-build-report.md), lines 146–148, found eight of 930 final-snapshot genomes carrying a new sense. The [movement draft](scratch/movement-prereg-draft.md), lines 34–36, cites eight of roughly 450 and proposes a 20% requirement. That can be a prospective criterion, but the cited denominator is wrong and the measurement does not establish that threshold.

Added mass is implemented but disabled in the recorded evolutionary worlds. The primer's physical explanation can make a reader assume otherwise. Keep every movement interpretation tied to the actual drag-only setting until the owner decides whether to adopt the additional mechanism. Enabling it creates a new physical world that needs its own validation.

There is also a modelling seam between sensing and acquisition. Chemical sensing samples part depth, while feeding uses the creature's economic location and patch. The light calculation similarly abstracts geometry. Before making claims about adaptive foraging, state which spatial differences can change perception and which can change income. A controller cannot exploit a gradient that its acquisition model does not resolve.

The [predation proposal](fable-propose-predation.md) contains useful amendments: stable contact ordering, target caps, fixed geometry, and separate matter treatment. It still contains an older tissue-shrinking mechanism alongside the later injury-pool alternative. Consolidate the operative proposal before asking for a ruling.

That proposal also needs lifecycle accounting. Define injury at birth, healing if any, its relationship to standing energy, what death releases, and how captured matter becomes available for reproduction. Collect contact observations, order them deterministically, cap each target once, and apply transfers after allocation. R1 is a concrete warning against paying each interaction while later interactions are still being priced.

The literature round for live contact feeding is still queued. Until it is completed, describe the bite parameters as engineering hypotheses. Population trophic-transfer efficiency is not automatically a per-bite assimilation yield.

Finally, distinguish carrion consumption, live contact feeding, and active pursuit. A surviving consumer could rely on corpses. An active predator needs evidence that control improves access to living prey. Report the income streams separately and avoid changing bite dose and sensory access together in the first causal comparison.

## 7. The public documentation currently makes the project harder to enter than it needs to be

The long design and decision history are valuable, but current state is repeated in too many places. Several contradictions occur within the same document. The solution is a small authoritative current-state summary linked to preserved history.

These corrections would have the highest practical value.

| Document | Current problem | Why it matters |
|---|---|---|
| [HANDOFF.md](HANDOFF.md) | Round 28 is running at the top but D078 remains “being built” in the queue; the photosynthetic lineage flag is both done and absent; deleted outside reviews remain queued | A new contributor can repeat completed work or misread evidence |
| HANDOFF, around line 159 | Says zero physics workers cost nothing measurable at 500 bodies | The [build report](scratch/physics-jobs-build-report.md), lines 227–232, describes noisy results with an indicated cost around 15% at 120–400 bodies; cost at full round populations remains unmeasured |
| [DESIGN.md](DESIGN.md), around lines 2000–2005 | Says the physics step is absent from the configuration hash | The current implementation includes it; the review probe confirmed that changing the step changes the hash |
| DESIGN, around line 1673 | Says creatures remain tiled and never touch | Shared contact is now implemented |
| DESIGN, persistence section | Describes an older archive/evaluation layout | It does not teach the current config, manifest, lineage, statistics, and snapshot workflow |
| DESIGN, reproduction discussion; World conception comment | Claims per-offspring overhead distinguishes one brood of four from four broods of one | Both pay four such overheads; any difference requires another mechanism |
| [README.md](README.md), lines 12–35 | Says nothing is scored, then calls the history 28 scored rounds while round 28 is incomplete | Distinguish no individual fitness function from researcher scoring of runs |
| README, setup and evidence instructions | Leads to the disposable spike and assumes an ignored recording exists for Theatre | A fresh clone lacks a direct path to the advertised world |
| README, dependency and source notes | Says no lockfile, gives old test counts, and describes only the first eight papers | The package lock exists and the source corpus has grown |
| [Primer 03](primer/03-what-it-means-to-push-against-water.md) | Explains added mass without stating its absence from the evolutionary runs | Readers can infer physical fidelity the recorded worlds did not have |
| [Primer 05](primer/05-a-brain-that-is-copied-with-the-limb.md) | Says senses remain unwired | The implementation and logbook now say otherwise |
| [Primer 06](primer/06-the-producers-feed-the-water.md) | Gives an unqualified same-machine replay guarantee | The shared-contact/thread limitation belongs beside the claim |
| [Spike README](spikes/01-articulation-body/README.md) and [findings](spikes/01-articulation-body/results/FINDINGS.md) | Still “not implemented”; infers solver parallelism from scaling | Completed measurements need links to later qualifications, including the early torque correction |
| [LICENSE-DOCS](LICENSE-DOCS) and [LICENSE](LICENSE) | Enumerated prose coverage omits the proposal | State the intended scope consistently without relying on an obsolete list |

The physics-step correction deserves emphasis because it is a recent explicit claim about current code. The setting is declared as a physics tunable in [RunConfig.cs](src/Evosim.Core/RunConfig.cs), around line 1106. The runner assigns its actual fixed step before persistence. My probe changed it from 0.01 to 0.02 and obtained a different configuration hash.

Preserve numbered sections and decision anchors. Add supersession notes and update status indexes rather than rewriting preregistrations after results. Generate mechanical facts such as test totals and current file layouts where useful. Keep causal explanation in prose written for a person.

Historical organism-level explanations need their original qualifications too. The [round-12 lineage analysis](scratch/r12y-s3-absorptive-line.md), lines 79–95, could not measure individual depth. It relied on observed snapshot order to join genomes to IDs and warned that a photosynthetic genome node need not develop into tissue. New snapshot IDs improve future evidence but cannot retrospectively strengthen those older inferences.

## 8. The experience of watching the world needs a modest acceptance check

The stated purpose starts with a world worth watching. Most current evidence concerns an economy read through tables. The Theatre is a useful bridge, but I did not open it or verify its visual behaviour during this review.

After validating embodiment and replay, watch a representative saved world from a fresh setup. Check whether a newcomer can select a creature, see its ancestry, understand its energy sources, and notice births or ecological changes. Use a recording with known events so absence on screen is detectable.

The world may evolve too slowly to explain itself on a human timescale. Playback speed, event navigation, and explanatory overlays can help without changing the biology. Shorter lives or stronger selection would change the world and need a separate decision. I would establish this small viewing test before expanding presentation features.

## 9. The next work should proceed through explicit checkpoints

I recommend the following order. These are proposals for subsequent work; this review has not adopted them or altered a running experiment.

1. Repair correctness by closing feeding allocation, material rollback, and global neural billing. Add the small invariant tests that the current suite lacks. Preserve builds and outputs needed to interpret earlier worlds.
2. Repair verdicts by bounding events to the observation cutoff and checking input identity and completion. Distinguish live readings from final outcomes. Resolve the producer rule and the three-versus-four-seed endpoints.
3. Measure the historical effect with short deterministic references and selected round-18 configurations. Determine which mechanisms change after repairs. Expand to a confirmation only when those measurements justify the cost. Do not assume the entire campaign needs rerunning.
4. Make the reference portable by committing the minimum evidence and configuration needed to reproduce, inspect, and explain it. Add explicit replay eligibility and inoculation handling.
5. Refresh current documentation, starting with the handoff and public entry points. Annotate superseded historical claims without erasing their context. Reconcile source entries in the literature registry.
6. Resume the owner's staged ecological path once the corrected reference is understood. Require a causal assay for movement or live feeding before attributing persistence to those behaviours.

The owner needs to decide world semantics: global brain treatment, intentional conception priority, producer dependence, physical fidelity, and adoption thresholds. Allocation correctness, consistent cutoff handling, provenance, and explicit error reporting can be improved without inventing those rules.

## 10. Verification supports the findings within a defined scope

The existing automated checks passed. The new probes expose conditions those checks do not currently cover.

| Check | Result | Scope |
|---|---|---|
| Existing Core suite through the repository script | 531 passed, 0 failed, 0 skipped; about 78 seconds | Engine-independent unit and integration tests |
| Existing scorer fixture suite | 28 assertions passed across four fixtures | Run against byte-for-byte script copies under scratch |
| Equal-feeder probe | Meals 1.25 J and 5 J; 3.75 J left | Same bodies, age, location, and starting resources |
| Capped-feeder probe | 10 J credited; 6 J withdrawn | Feeding cap enabled; audit change -4 J |
| Stillbirth probe | 2 matter units cached in absent bodies | Ordinary scalar mutation, seed 1 |
| Global-brain probe | 101 neurons execute; only 2 J billed | One local neuron and input, plus 100 global neurons and inputs |
| Scorer cutoff probe | FAIL becomes PASS after a future birth | Report remains unchanged through 10,000 seconds |
| Configuration-hash probe | Hash changes with physics step | 0.01 versus 0.02 seconds |

The Core probe settings isolate individual mechanisms. The feeding cases use two one-cubic-metre absorptive bodies in one resource layer and a half-second economic step. The uncapped clearance is 2; the capped case uses clearance 100 and a 10 W per cubic metre cap. Sinking and mixing are disabled.

The stillbirth case uses a root half-extent of 0.0234 metres, fixed matter cost 2, and zero tissue matter price. Scalar mutation is enabled and other mutation chances disabled. One viable parent receives enough energy to conceive. The child's root then falls below the ordinary minimum volume. No private state or reflection-based mutation of world internals is used.

The neural probe gives every global neuron one constant input and lets the local neuron read the first global output. The pricing rates are set to one per neuron and one per connection for a one-second ledger. The observed 2 J bill makes the omission independent of default price magnitudes.

Diagnostic sources and outputs remain under [scratch/astra-review-probes](scratch/astra-review-probes). The cutoff fixture is [scratch/astra-review-scoring/probe.ps1](scratch/astra-review-scoring/probe.ps1). Scratch is ignored by Git; the descriptions above preserve the essential reproductions in this review.

The relevant commands are:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/core-test.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scratch/astra-review-scoring/scripts/tests/clade-score/run-tests.ps1

$reviewSdk = 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Data\DotNetSdk\dotnet.exe'
& $reviewSdk build scratch/astra-review-probes/ReviewProbes.csproj --no-restore --verbosity quiet
& $reviewSdk scratch/astra-review-probes/bin/Debug/net8.0/ReviewProbes.dll
powershell -NoProfile -ExecutionPolicy Bypass -File scratch/astra-review-scoring/probe.ps1

python scripts/style-check.py gpt-astra-2026-09-07-1316-review.md
```

Collectively, the full authored reading includes all root project guides, the proposal, all decisions, and the licences. It also includes logbook entries 0001–0071 and their guide, all six primer chapters and their guide, all five research documents, and both spike documents. The working-document sweep covered all 38 top-level scratch Markdown files and all 11 prose-review notes. Generated scorer fixture reports were treated as test data.

The final file passed the repository style checker with zero findings. Every local Markdown link target was checked for existence. Git showed this review as the only new non-ignored file, with no tracked changes.

Implementation inspection was selective and focused on consequential paths. I did not read every test or every Unity class line by line. I did not run Unity physics, a fresh evolutionary campaign, a cross-machine replay, a performance benchmark, or a visual acceptance session. The static Unity findings need those targeted checks before a repair is considered verified. The historical scientific conclusions remain qualified by the recorded build, settings, scorer, and available evidence.
