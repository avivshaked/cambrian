# The underwater look, second batch: contrast, and the run's own snow

Written 2026-10-07. The first batch (`brief.md`, merged on main) made the water visible, and it made the
picture flatter. On the before-and-after stills the creature close-up lost 30% of its luma spread and its
brightness doubled. The reef view, at the merged in-scatter of 0.02 per metre, kept its overall spread but
lost about a fifth of its local contrast. The bodies stayed sharp: the edge measure rose. What went is the
dark end. The in-scatter adds blue light over every metre of water, so nothing in the water is darker than
that light, and the near rock melts into a teal veil. The owner asked for this to be iterated until the
pictures are really nice, and asked whether the snow could follow the run's real snow.

Every rule of `brief.md` still holds. The work stays under `unity/Assets/Theatre/`, and every item sits
behind its own switch, off by default. The census and solo stills keep their look, and nothing within
about 3 m of the camera is softened. Nothing is vendored, colours set from script take `.linear`, and
anything noisy is a function of position and the clock, so a film does not shimmer. `EVOSIM_THEATRE_LOOK=3` keeps meaning
the first batch alone, so the first batch's pictures can be re-taken; `EVOSIM_THEATRE_LOOK=4` turns on
both batches.

## Contrast

1. **The water fades things into the water behind them, not into a brighter fog.** Far objects should
   approach the colour of the open water seen in the same direction, the backdrop the camera would see if
   nothing stood there, so an object loses contrast with distance and the picture keeps its black point.
   Today the in-scatter's limit is brighter than the backdrop, which is what lifts the darks. Tie the
   in-scatter's colour to the backdrop's at that direction and depth, keep the forward glow towards the sun,
   and let it grow from about 3 m rather than from the lens.
2. **A tone step at the end.** One curve after everything else: a black point and a gentle S, with dials,
   so the darkest tenth of a frame reads dark again and the mid-tones keep their hue. Not a grade: the hues
   of the guilds must survive it, and it must not clip the leaves' highlights.
3. **The shafts in their place.** On the creature close-up the shafts cross the whole frame. Fewer and
   narrower, strongest where they cross dark water, and faint in front of a lit object.

## Snow from the run

The farm records the snow every 100 s (`<run>/fields/`, read through `layout.json`):
`NNNNNNNNN.snow-columns.f32` is each 1 m column's snow in joules, and `NNNNNNNNN.snow-floor.f32` the
stock of the column's lowest cell; `bed.f32` is the floor's height. `stats.jsonl` has the totals
(`detritusJoules`, `detritusOnFloor`) every 10 s.

What the data says, read on `r57-s2` at 27,000, 30,000, 36,000 and 45,000 s: the snow is close to even.
The column's density (its joules over its water depth) runs from 0.7 to 1.3 J per cubic metre between the
5th and 99th percentiles, and the total moves by about a tenth over the year. The floor's stock varies
more, about twofold between the median and the 99th percentile. So snow drawn from the data will look
much the same everywhere in round 57; it will show more in a world whose snow is patchy.

4. **The motes' density follows the column they are in.** The snow shells' mote density at a point is
   scaled by that column's density, interpolated in time between the two dumps either side of the frame's
   second, against a fixed reference density (a dial, default 1 J per cubic metre). The reference is fixed
   and never rescaled per frame, so a film across a year shows the real change and two rounds compare. Where
   the run has no `fields/` folder, the snow is drawn as today and the label says so.
5. **The floor carries its settled snow.** The bed takes a pale dusting in proportion to the floor's stock
   from `snow-floor.f32`, against a fixed reference, interpolated in the same way.
6. **The label says where the snow came from**: "snow from the run" or "snow decorative".

7. **The snow drifts as the run's water does.** It floats rather than falls, as the owner pointed out the
   same day. In round 57 it sinks at 0.002 m/s (`nutrientSinkMetresPerSecond`), while the current carries
   it at up to 0.1 m/s in rolls of 30 m cells (the config's `current` block, `advectFields` true). The
   shell snow, a shader with no particles, sinks at the run's own rate and drifts with the current at the
   shot's camera and second, with a small time-stable sway. The drift follows the simulation's seconds
   when a film's clock runs faster, and a frozen frame keeps a slow drift. Where the current cannot be
   read, it drifts at zero and the label says so. Motes seen towards the sun glow brighter than those seen
   away from it, by the water's forward-peaked phase.

The snow is a field in the simulation, not particles, so the motes' positions stay decorative; only their
density is the run's. The snow is additive and never writes depth, so it cannot hide a body.

## How it is judged

The test set, all at 3840x2160 on `unity-w5`: the creature close-up (`theatre-film.ps1 r56-s2 -At 29000
-Shots close -Freeze`, one frame), the reef and close views from `r56-s2`'s 29,000 s checkpoint, and the sky
and bed views of `r57-s2` at 27,000 s (winter) and 36,000 s (summer) from the snapshot. The old look's
frames stand in `scratch/look-build/stills-1/before`, `stills-2/before` and `stills-4/before`.

Each iteration is read by eye on a side-by-side sheet of the old look, the first batch and the new one.
It is also read by number with `scratch/look-build/contrast.py`'s measures. The bar has four parts:

- a luma spread (5th to 95th percentile) and a local contrast (mean s.d. in 32 px blocks) at least 0.95
  of the old look's on every view;
- a mean luma between the old look's and the first batch's;
- a 100% crop of a body as sharp as the old look's;
- by eye, water that still reads as water: depth fading, the shafts, the snow.

A number that passes on a frame that looks worse is a failed frame.
