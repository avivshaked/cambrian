# The see-through plume in motion: the coordinator's read

D346 took the wound's see-through plume (`WOUND_PLUME_HAZE` 2) only if it still reads in motion. The test was 1.3a's bite
at 1920x1080, 8 frames a second, from 27001.0 to 27004.0 s (the scene's window opens at 27001.0), rendered by the theatre
lane at `b2e718f8` in `scratch/film-light/c8/pass13/look8/x-motion/`. The theatre lane measured the red excess round the
wound: it rises from 27001.5, peaks near 27003.4 and is back near its start by 27006, with no flicker.

**Not taken.** At full size, at the shot's own scale, the plume is a faint red tint behind the pale body, and
a viewer would not see that the animal bleeds, which D281 and D286 ask. The opaque plume stays in the look, and the haze
stays in the look's `trial` list.
