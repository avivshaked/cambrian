# Growth pulses, r65-c-6203 window 22500-23010 s (510 s, 30 fps, 15301 frames)

Data: scratch/theatre-realism/windows/r65-c-6203-22500-510s/film.poses.bin (v5 state stream). The fraction is Organism.BodyFraction (src/Evosim.Core/Ecosystem/Organism.cs:93), a VOLUME fraction of adult (tissue J / adult tissue J), written per body per frame (src/Evosim.Farm/PoseRecorder.cs:151,181; PoseStream.cs:1012, float32 at body offset 32). Linear scale = cube root. Not event rows.
Constants: MetabolicStepSeconds = 0.5 (src/Evosim.Farm/EnvBinding.cs:60); GrowthStepSeconds = 10 (src/Evosim.Core/RunConfig.cs:1385, applied src/Evosim.Farm/Metabolise.cs:319-330, solver resize only).
Growth rule: World.cs:5693-5740: target = Energy - reserveFloor*tissue, capped at remaining; fraction=(tissue+target)/adult; BodyFraction set at once, every metabolic step.
Verdict caveat: window is "cousin" (parted from the run at 22510 s), so bodies are those of this window, not the farm run proper.
Files: measure.py, burst.py, out.txt, fractions.csv (body,second,fraction), per_body.csv.
Results: see out.txt; summary in the reply.

## Numbers (volume fraction of adult unless noted)
- 5281 bodies in set (2212 born in window, 5207 with at least one change). 1,987,545 upward steps.
- Step size: median 0.00025, p90 0.0028, max 0.0509 (linear: median 0.00014, p90 0.0013, max 0.0172).
- Spacing: all steps on the 0.5 s grid (every one within 0.03 s); median gap 0.5 s. On the 10 s grid: 5.0% = chance (1/20). No 10 s signature.
- Share of growth arriving in steps above 0.05: 3.3% (1283 steps), all of them the final snap to 1.0 from 0.9491.
- Per body: median of median steps 0.00057; median of largest steps 0.0071, p90 0.0505.
- Bursty within a body (bodies with >=20 steps, n=5048): median CV of step size 0.95; top 10% of steps carry 25% of a body's growth; 61% of 0.5 s slots carry a step.
- Birth to 0.9 of adult (1045 of 2212 born bodies reached it): median 82.5 s.
- Missing: the reserve is not recorded, so a step cannot be tied to a meal; "bites" are in events.jsonl but not joined.

## Relative jumps (c8, 2026-11-02; relative.py, relative.txt)

The absolute step sizes above understate what is seen, because what shows is a step relative to the body's current size. Per step,
the linear jump is (f1/f0)^(1/3) - 1, the snap to adult excluded:
- 85.4% of steps are under 0.1% linear, 8.5% are 0.1-0.3%, 4.0% are 0.3-1%, 1.7% are 1-2%, 0.4% are 2-5%, and 32 steps are 5-6.7%.
- 2,053 bodies have at least one jump of 1% or more, 1,091 of 2% or more, and 20 of 5% or more. The largest are newborns at
  fractions near 0.005, growing about 6% linear every 0.5 s in a row (82211 from 22,610.5 s; 81535 from 22,501 s).
- Reading (inference): at 1x a young body holds its size for 15 frames, then grows 2-6% in one frame, and does it again. That
  staircase is the pulse the owner sees. The 10 s solver resize does not show in the recorded fraction, which the film draws. The
  snap to adult, about 1.7% linear, is one more step of the same kind.
- Fix, display only: EVOSIM_THEATRE_GROWTH_EASE eases the drawn size over each 0.5 s step on the run's clock (FilmWindowWorld.cs,
  as a uniform factor on `grow`).

## Growth ease build (FilmWindowWorld.cs)

Dial EVOSIM_THEATRE_GROWTH_EASE (default 0). On: per-body prev/cur/since, smoothstep over 0.5 s of run, grow *= pow(eased/cur,1/3). SizedFor unchanged. The second fraction site (unborn bounds, ~line 1090) draws nothing and applies no grow, so it is untouched. Rewind detected as Second < last Second; stale entries pruned when count > 2*bodies+64. Not compiled or run.

## The A/B on newborn 81535 (c8, 2026-11-03; phase109 U+056F off, U+0570 on, from 22,501 s for 5 s at 1x, seat 42bfa98a)

The mean absolute change between consecutive frames, in grey levels, over the centre crop 640-1280 x 300-780 where the close shot
holds the newborn:
- With the ease off, the mean is 0.451 and the largest 1.524. Every frame over twice the median falls on a 0.5 s step: f15, 30, 45,
  60, 75, 90, 105, 120, 135. That is the pulse, measured.
- With the ease on, the mean is 0.439 and the largest 0.554. No frame is over twice the median.
- Folded by phase within the 15-frame cycle (cycle.py), the mean change per phase:
  - off: 1.328 at the step frame and 0.38-0.42 at every other phase (max/min 3.48, sd/mean 0.511);
  - smoothstep: 0.379 at the step, rising to 0.509 mid-cycle and back to 0.393 (max/min 1.34, sd/mean 0.078). That is the
    coordinator's reading in numbers: the swelling rests at each step and runs fastest mid-step, a 2 Hz breath.
  - The floor of about 0.38 is the rest of the scene's motion, which is in every phase.
- The straight line (EVOSIM_THEATRE_GROWTH_EASE=2) goes in as phase110 U+0579, to be folded the same way.
- The straight line (phase110 U+0579, EASE=2), the same fold: 0.416 0.459 0.432 0.424 0.460 0.426 0.428 0.471 0.427 0.427 0.458
  0.420 0.420 0.461 0.419 (max/min 1.13, sd/mean 0.042, against the smoothstep's 1.34 and 0.078). The rise to mid-cycle is gone.
  What is left is a small bump every third frame (phases 1, 4, 7, 10, 13). It is in no 0.5 s rhythm, and likely the recorded poses'
  own cadence. Inference: it was there all along under the other two, at 0.41-0.47.
