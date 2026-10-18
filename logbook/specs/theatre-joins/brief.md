# Brief: the theatre draws every joined body whole

## The requirement

Wherever Core joins two parts, the theatre must not draw a gap between them, in any mode: a solo
still, a snapshot reconstruction or a live frame. Core's geometry is whole: in every body measured,
the two anchors of each joint coincide to 0.0000 m (`logbook/specs/theatre-joins/partcheck-*.txt`). A
gap in a picture is therefore the skin's doing, and it shows a creature that never existed. The
owner wants no body in the film that looks disjointed.

## The three causes found

1. **A sphere part is drawn as the genome's ellipsoid**, scaled so that its longest semi-axis
   equals the physics ball's radius. A joint anchored on the ball, off that long axis, then lies
   outside the drawn surface. Example: r56-s3 #8004 has a sphere with half-extents
   (0.377, 0.163, 0.208) and a ball radius of about 0.249 m. Its joint is at +y 0.250 m, and the
   drawn y semi-axis is about 0.108 m, so 14 cm of the joint shows as gap. r57-s2 #16076 shows
   about 15 cm.
2. **A photosynthetic box is drawn as a leaf** inside its box (`TheatrePalette.DrawnAsLeaf`), whose
   rim thins to nothing. Example: r57-s1 #20184's leaf box has half-extents (0.163, 0.085, 0.268).
   Its stomach bud (radius 0.021) sits on the box's underside at y −0.106, so it floats about 10 cm
   below the drawn leaf.
3. **A leaf's outline thins to nothing at the rim**, so a part joined face to face, or near an
   edge, touches at a point or not at all. Examples: r57-s1 #18207 has two leaf boxes fixed face to
   face. r56-s2 #90944 has links anchored at 0.872 of a plate's half-width.

## The rules for the change

- **Keep the look the owner accepted.** The ellipsoid, the leaf, the carve and the rounding stay.
  Close each gap by bridging it: extend the neck the skin already draws at every joint
  (`TheatrePalette`'s necks and `Knuckle`) so that it runs from the anchor to each part's drawn
  surface. Prefer this over changing a part's drawn shape. If a cause cannot be closed this way, say
  why and propose the smallest change of shape that closes it. Do not make that change; it goes
  to the owner.
- **Stay inside the collider.** Everything drawn lies inside the physics shape, as the carve's rule
  already ensures. A bridge between the anchor and a drawn surface inside the collider satisfies
  this by construction. Check that it does.
- **One code path for every mode.** Solo stills (`TheatreSoloStill`), snapshot reconstructions
  (`SnapshotWorld`) and live frames (`LiveWorldView`) all dress bodies through `TheatrePalette`. The
  fix goes where they share it, and it must hold for a body that moves: in a live frame the
  joint's anchors move with the parts, and a neck is refitted every frame.
- **Edit only `unity/Assets/Theatre/`.** `Assets/Evosim` and `src/` are not touched. The theatre
  sits outside `simHash`, so no recording stops replaying.
- **No Unity.** The builder does not launch Unity. The coordinator compiles the change and draws
  the check, and sends back any compile errors.

## The check

- The 15 solo stills of `logbook/specs/creature-video-picks.md` are drawn again, on a worker
  refreshed from the change's worktree. They are set side by side with the current stills
  (`scratch/story-video/rounds-53-57-202610061945/stills/`). In each joined pair, the drawn shapes
  must meet at the anchor the partcheck lists.
- A snapshot reconstruction of one crowd, before and after the change, compared for anything else
  that moved (size, brightness, the bed and the colours).
- A frame of a live film, before and after the change, for the same comparison.

## Where to write

- Code: the worktree `D:\Projects\experiments\evolution-simulator\scratch\wt-theatre-joins`
  (branch `theatre-joins`), under `unity/Assets/Theatre/` only.
- Helper scripts, notes and lists: `D:\Projects\experiments\evolution-simulator\scratch\theatre-joins\`.
- Nowhere else: not the session's scratchpad, not TEMP and not the main checkout. Do not cd into
  the worktree. Reach it by absolute path and `git -C`.
- The report goes in the final message: what was changed, file by file; how each of the three
  causes is closed; which bodies the stills should show whole; anything left open.
