# Finding: the solver joins a mirrored part where its twin is joined

*2026-10-23, the speed agent, found while writing D214's mouth point. Each claim says whether it was measured or read
in the code, and what is inference. Nothing here changes a world. The fix is an engine change, and it is the owner's
to rule on after round 62 has confirmed the grazers' dials (the coordinator, 2026-10-23).*

## What happens

A genome edge can reflect its child, so that a body grows a mirrored pair, as a left and a right fin.
`Developer` grows the mirrored copy with the transform
`Mirror · Translate(anchorOnParent) · Rotate · Translate(-anchorOnChild)`, and so puts the copy on the far side of
its parent (`Developer.cs`, about lines 325-345). It then stores the part's two joint anchors,
`ParentAnchorLocal` and `ChildAnchorLocal`, unreflected, and `Mat4.Decompose` flips an axis so that the stored
`Rotation` is a proper rotation. The reflection survives only in the part's `Position`. *(Read in the code.)*

Every engine that poses a body rebuilds each part from its parent and the two anchors:
`child = parent + R_parent · ParentAnchor - R_child · ChildAnchor`. With the unreflected anchors, a mirrored part's
joint lands on its twin's joint, and the part lands on its twin. *(Read in the code; measured below.)*

## The evidence

- **One body, measured.** `TestBodies.Chain(2)` with its edge reflected in x, developed and put down as developed
  (`MirroredPoseTests` in `src/Evosim.Dynamics.Tests/`, on `mouth-point`). The twin lands at x 3.4, on its original,
  where development put it at x 2.6. The unmirrored parts land where development put them. The test is skipped
  until the fix, which unskips it.
- **A recorded world, measured.** `--mirror-census` (`src/Evosim.Farm/MirrorCensus.cs` on `r62-mouth`), read-only,
  on the spoilage screen's `r62s-r3-h0`, round 62's world without spoilage at seed 6201. For each mirrored part it
  finds its twin, the other part of the same parent grown from the same node. It then measures the distance
  between the two joints in the solver and in development.

  | Second | Living | With a mirrored part | Pairs development puts apart | Of those, stacked in the solver |
  |---|---|---|---|---|
  | 6,000 | 1,338 | 11 | 11 | 11 |
  | 24,000 | 5,135 | 74 | 66 | 66 |

  The other five pairs at 24,000 s have both joints on the mirror plane in development too, a case the genome
  chooses (logbook/0006). The census's pairs are `logbook/specs/mirrored-anchor-census.tsv`, from the build
  `--mirror-census` was added in.
- **The theatre, measured by the video agent.** The same chain through the theatre's recorded-pose walk
  (`RecordedPoses.Apply`, a transcription of the solver's `Kinematics.Poses`) at zero joint angles stacks the twin at
  x 3.4. Live mode draws the solver's own link places. Either way, the films of farm runs have shown mirrored pairs
  where the physics put them, stacked, so the pictures did not mislead about the physics. Two fallbacks draw at
  development's `Position` instead: a body with no recorded pose, and one whose pose is refused.

## Where it reaches

- **The card engine, read in the code.** `Pack` in `gpu-runner.template` copies the host `Creature`'s `ParentAnchor`
  and `ChildAnchor` to the device, and the kernel's `Poses` transcribes the solver's line for line. The card stacks
  as the CPU does, so no identity claim is affected.
- **No self-collision, read in the code.** A body's own links never pair, in either contact model and on the card
  (`Contacts.cs` about lines 641-663, `Neighbours` at 709, `Candidates` in the kernel). Stacked twins pass through
  each other with no force.
- **Core reads development's geometry, read in the code.** The light silhouette (D099's hull), the support cost
  (`DistanceFromRoot`) and the self-overlap birth rule all use the developed `Position`. A mirrored pair is
  therefore lit, charged and admitted as a spread pair, while it swims as one stacked limb. *(Inference: a body with
  a mirrored pair earns light on a hull wider than its physical one.)*
- **The physics, read in the code.** The solver integrates the stacked body. Its centre of mass, its contact
  spheres, its water sampling, its depth and up senses and its limb push all see the twins at one place.
  *(Inference: a mirrored pair drives straight because both twins push from one spot, not because they balance.)*
- **The Unity farm (round 42 and earlier), read in the code, unresolved.** `PhenotypeBuilder` placed parts at the
  developed pose but gave the joints the same unreflected anchors. Whether PhysX then moved the twin onto its
  original is not settled from the code.
- **Nothing reflects an anchor anywhere,** in `src/` or `unity/Assets/` (read in the code). The anchors are also read
  unreflected by `Phenotype.Scaled`, `Evosim.BodyExport`, the nursery's export, the theatre's palette and the new
  `MouthPoint`. A fix reaches all of them.

## What it may mean

These are inferences. A mirrored pair costs two parts and moves as one, so selection should weigh against it.
Random founders reflect an edge with a chance of 0.35, yet about 1.4% of the living carried a mirrored part at
24,000 s. That fits selection against stacked pairs, and has not been tested.

## The fix, for the owner's ruling

The anchors that reproduce development's pose follow from the transform (derived, unchecked).
`ParentAnchorLocal = F_parent · M · anchorOnParent` and `ChildAnchorLocal = F_child · anchorOnChild`. Here M is the
edge's mirror, and F is the axis flip `Decompose` applied to the part's frame: the identity when the part's
accumulated determinant is positive. The change is in `Developer` (where the anchors are made and stored), so every
consumer above follows. The checks: bit-identical for every body without a reflection; `MirroredPoseTests` passes;
the census reads no stacked pair. A world whose bodies reflect is a new realisation from the first mirrored birth.
`MouthPoint`, which reads the anchors as the solver does, then needs no change.
