# Reusing the phenotype's storage in Grow: a review

A read-only review of r58-runner at 02c448e6, 2026-10-10, made by an Opus subagent on c9's brief. c9 then checked
four of its claims in code: the checkpoint writer's `ReferenceEquals(built, creature.Phenotype)`
(src/Evosim.Farm/Simulation.State.cs:220); the solver's `Phenotype = scaled` at a resize
(src/Evosim.Dynamics/Creature.Growth.cs:124); the theatre's `ReferenceEquals(live.Sized, phenotype)`
(unity/Assets/Theatre/LiveWorldView.cs:391); and that the farm's csproj sets no server GC. The rest is the subagent's
reading; where it marked a claim UNVERIFIED, so is it here.

## The answer

The proposal rests on an unmeasured inference: that Grow stopped at 3.2 ms rather than about 1 ms because each of
about 1,000 juveniles allocates a new scaled phenotype a step. Reuse in place, as proposed, is not safe. The
organism's phenotype object is shared by reference with the solver body (which deliberately holds an older, smaller
phenotype between its 10 s resizes), the checkpoint writer (which decides how to restore a growing body by that
reference; aliasing would bring back the resume divergence CheckpointFidelity found on 2026-09-25), and the theatre's
live view (which detects growth by that reference, so bodies would stay at birth size on screen). A safe reuse is
two changes: Core rewrites its own non-adult buffer, and the solver takes a private copy at each resize, with the
checkpoint's "as the organism" test rewritten from a reference test to a plan-revision and ScaledBy test. The gain
is at most about 2% of pace and more likely 0.5 to 1.5 ms a step (inference), about the card's run-to-run spread.

## What Grow allocates a grown juvenile (checked)

`Phenotype.Scaled` (src/Evosim.Core/Development/Phenotype.cs:399-506): one Phenotype, its List of parts (no initial
capacity, so regrowths), a `DevelopmentLimits.Clone()`, and one PhenotypePart (a class) a part. Then
`Metabolism.TissueJoules` and `StandingWatts` (the full Bill) each walk the parts through a boxed enumerator, with
string-keyed registry lookups a part. About 2 KB a body at 8 parts, about 2 MB a world step (part count UNVERIFIED).

Allocation itself scales with threads; GC pauses do not (workstation GC, a gen0 collection stops every thread), and
these phenotypes are medium-lived (one in twenty is held by the solver for 10 s). Equally plausible and unmeasured:
imbalance in Parallelism.ForRanges' equal contiguous slabs on a hybrid P/E machine, the serial walk building the
juvenile list, Parallel.For wake-up, and the world probe's own shared Interlocked counter inflating the 3.2 ms.

## Cheaper steps, in order

1. Measure at no risk: extend the world probe with GC counts and pause time around Grow, each slab's time, and the
   step's allocated bytes; and run the probe build once with server GC (DOTNET_gcServer=1) or a larger gen0 budget.
   If Grow falls to about 1 ms, the cause is GC and a recorded runtime setting may be the whole fix.
2. Trims that touch nothing a body holds, every number the same: pre-size the parts list in Scaled, index loops in
   TissueJoules and Bill (two boxed enumerators a body), and possibly a shared Limits if nothing mutates it.
3. If imbalance is the cause: many more chunks than threads for Grow alone; each body writes only its own fields.
4. Cache the cell type and part shape on the adult's parts instead of three string lookups a part a body.

Ruled out: skipping unchanged scales (1,003 of 1,062 juveniles grow every step), growing only at the 10 s cadence (a
world rule, the owner's), and struct-of-arrays phenotypes (every reader touched for about 2%).

## If reuse were built: risks and proof

Identity needs TotalLitArea re-added from zero in part order, SilhouetteArea and the hull scale from the adult's
values, and ScaledBy written exactly (FreeShare reads it in the bill). Phenotypes are not serialised, so the
checkpoint format is safe; the hazard is the harness's reference test. Thread safety holds if each buffer belongs to
one organism and is never its adult (Scaled(1) returns the adult; a module rule can leave Phenotype equal to it). No
pooling across organisms: a corpse keeps its body's phenotype.

Proof: Core -All; Dynamics GrowthTests; the Farm checkpoint and rebuild tests; new tests that a phenotype reference
taken before a growing step is unchanged after it (no aliasing), that the adult is never written, that a corpse's
body is unchanged, that a resume from a checkpoint between growth steps equals the run straight through, and that the
theatre's live check sees a resize across a growth step; then a CPU farm resume, a stop off the growth cadence with
--verify-checkpoint, the card identical to r57-ref58-card1, and a day-sitting ABBA with the GC counters beside.
