# The nose graft films, read

Nursery entries 23, 24 and 25, of 2026-10-26 and 2026-10-27. D235's nose was grafted onto D178's ten founders
(`rounds/nursery/pools/410a70db44a4b20a.tsv`) to film a nose at work before any lineage has evolved one. Every run
is a 300 s bench of two episodes in the round-60 world's patchy leaf tank (`leaf-direction-patches-s2-w5`). All three
share a salt, so they share seeds and tanks. Each swims under the body's own brain and again with the leaf smell
knocked out.

## The three entries

| Entry | Recipe | Nose | Area | Pool |
|---|---|---|---|---|
| 23 | `nose-graft-film` | a stalk 8.5 by 8.5 by 25 cm, upright on the root's top face | 0.1 m² | `2a101bfbce346e65` |
| 24 | `nose-graft-film-baseline` | none, the world without the nose | | `410a70db44a4b20a` |
| 25 | `nose-short-film` | a snout 6 by 6 by 9 cm, on the root's front face, pointing the way the bodies swim | 0.0288 m² | `06c9b91ce9a3d835` |

The stalk's size was a default: the table's reference area at three times as long as wide. The owner found it wrong
to look at, and the snout replaced it in the film. The snout points straight forward and not at an angle because the
direction sense reads in the nose's own frame. An unturned nose keeps the root's frame, so the direction links moved
off the root stay exact under the brain's fixed wiring.

Entry 24's row names commit `dd558c55`, which does not hold its recipe: the baseline was swum before its recipe was
committed in `9dd15b6b`. The recipe there hashes to the row's `recipeHash` (`1feb449e…`) and is unchanged since, so
the row stands as written.

## The bench

| | Own brain, net | Knockout, net | Path, own | Eaten, own / knockout |
|---|---|---|---|---|
| 23, stalk | −213.3 J | −202.2 J | 55.1 m | 0 / 0 J |
| 24, no nose | −182.5 J | −183.5 J | 59.1 m | 0 / 0 J |
| 25, snout | −204.7 J | −178.9 J | 56.5 m | 0 / 9.4 J |

No body ate under its own brain, with a nose or without one. So the bench cannot say whether a nose helps a body
feed. The 9.4 J under entry 25's knockout is one meal in twenty swims. The knockout's leaf smell is off whichever nose
a body carries, so I read it as chance. The snout's lower upkeep shows in the own brain's net, about 9 J above
the stalk's.

## The sense

Episode 0's tank 1 (pools 3, 8, 5 and 9) was restepped at 25 frames a second for each nose, and the nose part's
direction reading read from its frames. A frame counts as inside a patch when the root is within 6 m of a patch's
centre.

| | Inside, mean (median) | Open water, mean (median) | Contrast |
|---|---|---|---|
| Stalk | 0.364 (0.410) | 0.106 (0.044) | 3.4 times |
| Snout | 0.233 (0.206) | 0.129 (0.028) | 1.8 times |

Pool 8 is the body the film follows. It stayed in the patch at (12.5, 38.0) from 105 s to 232 s with the stalk, and
from 116 s to 180 s with the snout. With the snout its reading inside was 0.149 against 0.126 outside, close to no contrast
at all. Pool 3 still read its patches clearly with the snout, at 0.394 inside against 0.131 outside.

## What it means

At D235's default noise a smaller nose senses less clearly. That is the cost the rule was built to make real. A noisy
count makes size pay, and the snout's 5.2 times the stalk's direction noise blurs a patch for a body passing through
it. That the film's body left the patch sooner fits a weaker pull, but one tank cannot show the cause.

Inference: at these constants a nose of about 0.03 m² is near the edge of use for a 60 cm swimmer. The nursery's
noise calibration should start from that. These bodies were bred to whole-body smell and not to a nose, so the
numbers measure the sense and not what evolution would make of it.

## Where the files are

The resteps are under `scratch/wt-nose/scratch/restep/`, as `graft-own-d0-t1-25fps-leaves` and
`short-own-d0-t1-25fps-leaves`. Each has an `episode-config.json` beside it, and each replays its bench's nets bit for
bit. They are scratch, and only this read cites them; the registry rows and the recipes hold the runs.
