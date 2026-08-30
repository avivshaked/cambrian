# Sol/GPT proposal: make the next result trustworthy

**Date:** 2026-08-29  
**Status:** recommendation, not a decision record  
**Scope:** repository, plans, recorded outcomes, logbook, primer, research, and [Fable's proposal](fable-propose.md)

## The problem to solve

Cambrian already contains the core of an artificial ecosystem. Genomes grow articulated bodies, local neural controllers act through those bodies, energy and matter constrain reproduction, and the water column transports finite resources. This is no longer a prototype waiting for its first mechanism.

The present risk is different. The project can add plausible mechanisms faster than it can establish what caused an outcome. Several recorded failures came from a configuration value that never reached the intended code, a report that measured the wrong thing, a unit mismatch, or a run that used stale sources. Those are experimental-control failures. More biology will make them harder to detect.

The correct next step is to turn the simulator into a dependable experimental instrument, finish the nutrient-cycle test already in progress, then let that result choose the next mechanism. This keeps the project moving. It also stops another long run from answering a question its configuration did not actually ask.

## What the repository has established

The strongest results are engineering results. The physics spike showed that articulated creatures can run at the intended scale once sleeping bodies are excluded from the timing error ([spike findings](spikes/01-articulation-body/results/FINDINGS.md)). The Core model has broad automated coverage. The current worktree passes 339 tests, although the suite takes about 36 seconds rather than the sub-second duration described in older notes.

The ecological results are narrower but real. Finite light creates carrying pressure. Tissue has a cost, death returns resources, current and mixing move those resources, and senescence removes the special status of founders. An absorptive lineage has persisted in at least one isolated run, so the trophic mechanism can work ([logbook 0028](logbook/0028-the-canopy-closed-and-the-scavengers-came.md)). D048 then exposed the main structural failure: matter and energy settle into the bottom layer faster than the living system can recover them. Later buoyancy runs changed vertical position without restoring the food chain ([logbook 0033](logbook/0033-the-surface-stripped-itself.md), [logbook 0034](logbook/0034-the-ocean-had-no-top.md)).

Movement remains unproven as an evolutionary strategy. Random articulated bodies can move, and the actuator economy is no longer obviously impossible. Selection still removes joints because depth can be bought more cheaply with passive lift. The design therefore has a locomotion capability but no demonstrated locomotion prize.

The project has also established a useful negative result: mean depth, producer share, and visual activity are weak evidence. The [logbook](logbook/README.md) repeatedly shows that plausible aggregate values can conceal extinction, founder dependence, a tail event, or an unused setting. The method should now reflect that lesson.

## What remains suggestive or unknown

These claims are not yet established:

- A single world can maintain finite light, finite matter, senescence, buoyancy, and an inherited food chain for a sustained window.
- Remineralisation returns enough resource to the occupied water column to remove the benthic ratchet.
- Passive ballast creates useful vertical cycling rather than a new fixed-depth strategy.
- A chemical sensor has an unambiguous target now that detrital energy and reproductive matter are separate fields.
- Controlled lift, joints, or flow sensing earn their upkeep in competition.
- The current full-world results converge across physics timesteps.
- The simulator can reconstruct lineage activity accurately enough for open-ended evolution measures.
- The intended Unity build, worker source, run configuration, and report all refer to the same experiment.

The last item is the immediate concern. It affects every scientific claim above it.

## Review of the existing plans

The milestone plan in [README.md](README.md), [DESIGN.md](DESIGN.md), and [DECISIONS.md](DECISIONS.md) gave the project a useful build order. That order has now been overtaken by discovery. Parts of Milestone 6 exist before the complete Milestone 5 ecological claim holds, and later decisions have changed what earlier sensors and reports mean. Milestone numbers should remain historical navigation. They should not decide the next experiment.

Fable's proposal identifies the three live constraints correctly: the combined ecology has not persisted, movement has no competitive reward, and throughput becomes a limit in long ecological runs ([fable-propose.md](fable-propose.md)). Its call for the third literature round and the documentation repair has already been acted on. Its two-sided spatial trigger, lineage recommendation, and acceptance gates should be retained.

Three parts need amendment.

First, remineralisation should not be treated as an evidence-backed success because its code and decision entry exist. D051 is built but unmeasured. The literature review itself records GOY23 as abstract-level and says it should be read in full before the D-entry is used ([literature review](research/LITERATURE-REVIEW.md)). D051 currently presents stronger ecological support than the reviewed source can carry. Either complete that appraisal and add a primary source on benthic remineralisation, or label the mechanism plainly as a conservation-driven model choice.

Second, the experimental platform must come before ballast. This is not a long infrastructure detour. A typed manifest, source fingerprint, compact lineage stream, and Unity acceptance smoke directly prevent failures already recorded in the logbook.

Third, the spatial fork belongs after a calibrated D051 result. A remineralisation treatment that fails to raise local resource density has tested transport or dose, not spatial ecology. Tiling becomes warranted only when usable resource reaches occupied layers and the inherited consumer lineage still fails.

## Stop-the-line repairs

These repairs should be completed before another multi-hour ecological arm is interpreted.

### 1. Make D051 match its mathematical claim

`NutrientField.Remineralise` calls its parameter a first-order rate constant but transfers `min(1, k * dt)` of the stock. That is a forward-Euler approximation with a cap. Its result changes when the same elapsed time is divided into different steps.

Use the exact fraction `1 - exp(-k * dt)`. Add a composition test showing that one ten-second call and ten one-second calls produce the same result within numerical tolerance. Keep the conservation, zero-rate, and one-layer tests. D051 should be labelled **active, built, unmeasured** until its acceptance run finishes.

The implementation exposes separate nutrient and matter rates, then the Unity harness assigns both from one environment variable. One physical process should have one declared control unless the experiment has a reason to split it. Use one configuration value now, or record explicitly that the two fields may later receive different calibrated rates.

### 2. Replace loose environment-variable experiments with a typed manifest

The batch runner currently parses numeric values through `float`, including the seed. Large integer seeds therefore lose exactness. Invalid values silently fall back, arbitrary PowerShell keys can become environment variables, and inherited `EVOSIM_*` values can survive outside the settings block.

Each run should start from a complete, typed JSON manifest. It should reject unknown keys, malformed values, duplicate run identities, and values outside declared ranges. The stored report should contain:

- the resolved configuration, not just overrides;
- an exact integer seed;
- the configuration hash;
- the Git commit and dirty-worktree flag;
- a Core assembly or source hash;
- Unity and .NET versions;
- genome schema and mutator code versions;
- the worker/build fingerprint;
- the planned stop rule and primary endpoint.

A preflight command should print the full diff against a named control arm. The run should not start when that diff contains an unplanned variable.

### 3. Close the lineage gap with compact events

`lineage.jsonl` exists but is empty. Sample-time `HashSet` counts miss short-lived organisms between reports, so they cannot support reliable lineage persistence or evolutionary-activity measures.

Record one compact birth event and one death event per organism. A birth needs organism ID, parent ID, birth kind, birth seed, simulated time, generation depth, genome hash, mutator version, and a few selected inherited descriptors. A death needs organism ID, simulated time, and cause. Periodic full snapshots can remain the recovery anchor.

This avoids writing a complete genome at every birth. Given a founder seed, parent genome, child birth seed, fixed mutator version, and pinned source, descendants remain reconstructible. The event stream should be a few megabytes per hour rather than the estimated hundreds of megabytes for full-genome logging. Measure the actual cost in the smoke run.

### 4. Add a scientific Unity smoke test

Core unit tests did not catch the buoyancy unit error because the failure lived at the Unity/Core boundary. Before an experiment launches, a short batch-mode smoke should verify that every requested setting reaches the arithmetic and every promised report field appears.

The smoke should exercise a non-default seed, light level, density, mixing rate, current, senescence time, matter rate, lift cost, and remineralisation rate. It should fail if the resolved report differs from the manifest. Reset the static tracking sets and last-sample counters at the start of every `EvolutionRun`, since repeated editor runs otherwise risk contamination.

### 5. Separate fast tests from calibration tests

The current 339-test suite passes in about 36 seconds. Keep it, but stop describing it as sub-second. Mark stochastic surveys and calibration-heavy tests separately so local development has a fast deterministic lane and CI still runs the complete lane.

Add continuous integration for the Core project. A second job can run the Unity smoke where licensing and runner support permit it. The immediate target is simple: a source change cannot merge with a failing Core contract or a manifest/report mismatch.

### 6. Repair configuration traps before they become experiments

`FluidConfig.Clone()` omits `TissueExcessDensity`. Fix it and add a clone-equivalence test over every public configuration member.

Added mass is also inconsistent across entry points. `FluidConfig.AddedMassCoefficient` defaults to zero, and the main evolution harness does not set it, even though other harnesses use one. Choose the intended model before interpreting swimming. Record the choice in the manifest.

Physics timestep is a compile-time constant and is absent from the configuration hash. Make it a hashed run setting before the planned timestep sweep. Preserve metabolic seconds when changing physics steps, or the sweep will change metabolism at the same time.

## Execution plan

### Gate 0: establish a trustworthy runner

Finish the six repairs above. The exit condition is a manifest-driven smoke whose source, resolved settings, report, and lineage events agree. Archive one known-good smoke as the runner reference.

This gate should be small. It does not require a new UI, distributed scheduler, database, or general experiment framework.

### Gate 1: calibrate remineralisation without creatures

Build a field-only transport harness using the same settle, remineralise, and mix calls as `World.Step`. Sweep a small log-spaced set of half-lives around one stated prior. Report floor stock, occupied-layer concentration, conservation error, and time to equilibrium.

Choose one treatment rate before looking at organism survival. The rate should return resource on an ecological timescale without flattening the water column instantly. This isolates transport from arrival, mutation, reproduction, and predation.

### Gate 2: run the paired D051 screen

Freeze the D048+D050 reference world. Run control and treatment with the same three exact seeds. Remineralisation is the only planned difference.

Pre-register the following screen before launch:

- **Primary endpoint:** `absorptiveInherited` remains above zero throughout the final 3,000 simulated seconds after establishment.
- **Required mechanism check:** floor stock stops its previous monotonic capture and usable resource rises in occupied layers.
- **Required system check:** energy and matter audits stay within their declared tolerance.
- **Safety check:** the treatment does not create unbounded population growth or hit a throughput stop that censors the endpoint.
- **Comparison:** report every treated seed beside its matching control. Do not replace pairs with one pooled mean.

The local concentration near 14 J/m³ from the successful food-chain run is a diagnostic band, not a universal threshold. It comes from too little evidence to serve as a hard pass condition.

Treat two successful pairs out of three as a screening result, not a general law. Any censored run stays censored. Do not convert a wall-clock timeout into extinction or persistence.

### Gate 3: let D051 choose the branch

There are three useful outcomes.

1. **Resource does not return to occupied layers.** Recalibrate rate or transport. No claim about spatial structure follows.
2. **Resource returns, but the inherited consumer lineage still fails.** Run the spatial-architecture spike. Test whether logical patches or a small tiled field improve local encounter and establishment rates before changing biology.
3. **The inherited chain persists.** Freeze this as the first combined reference ecology and proceed to ballast.

This is the most important change to the current plan. The next feature is selected by a measured failure mode.

### Gate 4: test ballast as an ecological strategy

Energy-linked ballast should use a normalized reserve measure, such as seconds of reserve at current maintenance, rather than raw joules. Raw energy scales with body size and would couple lift to size accidentally. Start without a neuron: high reserve increases density, low reserve restores lift, and bounds prevent a singular or runaway force.

Run fixed buoyancy against passive ballast in paired seeds. Judge it by reproduction, resource acquisition, inherited persistence, vertical cycling, and floor occupancy. Mean depth alone cannot establish an advantage.

Read the full primary buoyancy source before making a biological analogy. If that reading is unavailable, describe ballast as an engineered control policy inspired by microbial buoyancy, not as a faithful biological model.

### Gate 5: define sensing before controlled lift

The primer and design describe `Chemical` as local nutrient concentration ([primer](primer/README.md), [design](DESIGN.md)). D048 now separates detrital energy from reproductive matter. One chemical channel cannot silently stand for both.

Choose the sensor contract explicitly. The clean option is separate local detritus and matter channels, with the cost and mutation surface recorded for each. A cheaper temporary option is one declared target field. Do not implement a sensor whose meaning changes by experiment.

After that decision, test controlled lift before returning to joints. It is the cheapest active vertical strategy and therefore the control that locomotion must beat.

### Gate 6: reopen locomotion only in a world that pays for it

Do not resume the muscle-cost campaign in the depth-only reference world. That world rewards passive lift. Create a moving or spatially separated opportunity first, then compare passive buoyancy, controlled lift, and jointed movement under the same resource budget.

Mating is a credible locomotion prize because it makes encounter rate valuable. It also changes the reproductive regime and the meaning of lineage. Treat it as a later experiment, not a convenient patch for joints.

Morphology and controller innovations need persistence-aware measures. A new body can be undervalued while its controller adapts, as the third research round notes ([literature review](research/LITERATURE-REVIEW.md)). Track innovation age, descendants, and activity over time rather than judging a mutation from its first lifetime.

### Gate 7: build the minimum aquarium

The project goal includes a system worth watching, yet the theatre remains absent. Build the smallest useful replay after the runner and lineage stream are sound. It needs camera control, pause and speed controls, organism selection, lineage identity, energy and matter overlays, and replay from a stored snapshot plus events.

This is not a polished product milestone. It is a scientific inspection tool that also tests whether the evolved behaviour is legible and compelling. Defer advanced rendering and authoring UI.

## Research discipline

The third literature round improved the conceptual basis. Its strongest contribution is diagnostic: energy accounting, depletable resources, movement prizes, morphology-controller lag, and lineage activity all map to live design choices.

Its evidence base remains uneven. Several central sources were appraised from abstracts or secondary summaries, and the early-life research says that six of eight key sources were not checked in full ([early-life research](research/early-life/README.md)). That package is useful for hypotheses. It is not a safe source of parameter values or claims of biological fidelity.

Use a small claim ledger for future decisions:

- **Observed here:** linked run, seed, configuration hash, source hash, and report field.
- **Supported externally:** full primary source and the exact claim it supports.
- **Project inference:** a design choice derived from conservation, computational limits, or desired selection pressure.
- **Speculation:** an idea awaiting a test.

This vocabulary would have prevented the current D051 entry from blending a sound conservation argument with literature claims that have not received the stated appraisal.

## What to defer

Do not add excretion, oxygen, a second nutrient, more cell types, flow sensing, or richer mutation operators before the reference ecology passes. Each creates another explanation for failure.

Do not build a large tiled world until the D051 branch points to spatial structure. If that branch fires, prefer logical field patches over duplicated physics scenes first. They are cheaper to profile and easier to compare.

Do not optimize rendering before measuring late-run costs. The existing slowdown appears population-driven. Record physics, controller, field, reporting, and rendering time separately at fixed population sizes.

Do not call a mechanism successful from one seed, a population mean, or a visually plausible trajectory. The repository has already paid for that lesson.

## Recommended order of work

1. Correct D051's rate law and evidence status.
2. Add typed manifests, source fingerprints, compact lineage events, and the Unity smoke.
3. Fix configuration cloning, added-mass consistency, and timestep ownership.
4. Calibrate remineralisation in the field-only harness.
5. Run the paired three-seed D051 screen.
6. Branch to spatial structure only if usable resource returns and the chain still fails.
7. If the chain persists, test passive ballast.
8. Resolve chemical sensing semantics, then test controlled lift.
9. Reopen jointed locomotion in a world with a moving or spatial prize.
10. Add the minimum replay aquarium once lineage events are trustworthy.

The first five items form one deliverable: a result about nutrient-cycle closure that can be reproduced from a manifest and audited back to source.

## Owner decisions

Three decisions affect emphasis but do not block Gate 0.

1. **Primary identity:** is Cambrian first a research instrument or first a mesmerizing artificial aquarium? My recommendation is a research-grade core with a thin replay theatre. That preserves both aims without asking the live renderer to become the evidence record.
2. **Lineage retention:** may compact birth and death events consume a few measured megabytes per run hour? My recommendation is yes. Without them, open-ended evolution claims remain sample-dependent.
3. **Near-term evolutionary target:** should the reference world first prove Archean-style cycling or Cambrian-style motility? My recommendation is to close the resource cycle first, freeze that world, then construct a separate motility challenge. Forcing joints into the cycling world would confuse two selection problems.

## Definition of proceeding correctly

A run is trustworthy when its hypothesis and falsifier were written first, its control differs in one declared mechanism, its exact configuration and source are recoverable, and its endpoint is computed from event-complete data. A mechanism advances only after the arithmetic is isolated, the Unity path is checked, and paired worlds show the predicted effect.

That standard is stricter than the project's early exploratory method. It is now justified. Cambrian has enough interacting parts that the quality of the experiment, rather than the number of mechanisms, determines the value of the next result.
