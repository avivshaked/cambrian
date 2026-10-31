# Mouth animation: design (c8, 2026-10-31)

*On hold since the owner's word of 2026-10-31. Written by the theatre lane the same day and kept as written for when the work resumes.*

The owner wants the mouths to move when they eat, and each kind (TheatreMouth2.MouthKind: disc, toothed ring, sucker, crown, beak,
gape) to move in its own way. The build follows the rule for new features: every motion starts overpowered at strength 1, is
filmed, and is then dialled back. Nothing here changes a pixel until its dial is on.

## The signal the theatre needs

**What the recording must carry: per part, per pose frame, one bit.** The bit says that this part took matter at any simulation step
since the previous pose frame. It is an OR over the steps, so a bite shorter than the pose interval is not lost. It is per part, not
per body, because a body has up to four mouths (the one skin's `_MouthSkin0` to `_MouthSkin3`) and each moves alone. Only eating
parts (`MouthPoint.IsEatingPart`) can set it, so it can be stored as a bitmask over the body's eating parts in part order, beside
the anchoring build's holding bit.

**Nice to have, not needed:** a two-bit amount per eating part per frame, as quartiles of that part's intake in the interval. It
would scale the pulse's strength. Without it every bite is drawn at full strength.

The `k` rows (kills, body ids) stay as they are. A kill is not a bite: grazing a leaf is a bite with no kill.

**Live mode** (the theatre stepping the world itself) reads the same bit from the world's per-part intake at each step. The
builder should expose it from Core in the same shape (per part, per step, took matter or not), so one theatre path serves both modes.

## From bit to timer, in the theatre

For each mouth the theatre keeps a **seconds-since-bite-onset** timer in film seconds. An onset is a frame whose bit is set after a
frame whose bit was clear, or the frame on which the previous motion has finished while the bit is still set. So sustained eating
reads as a rhythm, one motion after another at the kind's own period, and never as a flicker. The timer starts at a large value
(idle) and is reset to 0 at an onset.

The durations are in film seconds, not simulation seconds, so a film played at 10x still shows whole motions. The refractory rule
above caps the rate at one motion per duration.

The timers reach the shader as **`_MouthBite`**, a float4. On a part, x is that part's mouth timer. On the one skin, x to w are
the timers of `_MouthSkin0` to `_MouthSkin3`, in the same slot order, which the one skin's dress already fixes for the axes. 1e4
means idle.

**`_MouthBiteParams`**, also a float4: x is the overall strength (the dial), y the pulse's flash share, z a test period, and w
spare.

## The shared pulse, all kinds (step 1)

On every onset each mouth gets a short pulse. Duration **0.35 s**.
- **Flash:** the lip and the disc colour lift toward white by `0.6 * strength * e(t)`, where `e` is a fast attack (0.05 s, ease-out
  quad) and a slow release (0.30 s, ease-in cubic).
- **Swell:** the mouth's whole radius scales by `1 + 0.25 * strength * e(t)`. The shader divides `mouthU` by the factor before
  everything else reads it, so the throat, lip, disc and every kind's pattern swell together. This is a colour and normal change
  only, as the mouth is today; no vertex moves.

The pulse is what reads at film distance, where a mouth is a few pixels across. The kinds' own motions below read only in close
shots.

## Each kind's own motion (steps 2 and 3)

`p = saturate(t / D)` is the motion's phase. `o(p) = sin(pi * p)^2` opens and closes once, and `s(p)` is a smoothstep. Every
amplitude below is at strength 1, the overshoot.

| kind | motion | D (s) | easing | parameter it moves |
|---|---|---|---|---|
| disc (0) | iris: the opening widens, and the stripes rotate a little and run outward as it opens | 0.50 | `o(p)`, attack faster: `p^0.7` inside | throat radius `MouthThroatIn` and `MouthThroatOut` x `(1 + 0.8 o)`; stripe angle + `0.25 o / count` turns |
| toothed ring (1) | the teeth close: the tips reach in and the ring tightens | 0.40 | `o(p)` | `MouthToothTip` from 0.6 toward 0.15 by `o`; the teeth's base share up by `0.3 o` |
| gape (5) | the slit opens wide and the lip parts | 0.50 | open fast (0.15 s ease-out), close slow (0.35 s ease-in-out) | `MouthGapeWide` 0.4 to 0.9; the lip's width x `(1 + 0.6 o)` and its brightness up |
| sucker (2) | the ring contracts, then releases with a slight overshoot | 0.60 | `s` in over 0.25 s, then a damped release: `exp(-6 q) cos(10 q)` over the rest | the rows' radii x `(1 - 0.25 c)`; the dots' size x `(1 + 0.4 c)` |
| beak (4) | the plates part at the seam and snap shut | 0.30 | open 0.10 s ease-out, shut 0.20 s ease-in (a snap) | the seam's half-width 0.04 to 0.35 radii, showing throat between the plates |
| crown (3) | the lobes fold in over the mouth, then spread back | 0.70 | `o(p)` with a 0.1 s lag growing outward along each lobe, so it curls | the lobe's reach 2.6 toward 1.3 radii, its tip drawn inward; width x `(1 + 0.3 o)` as it bunches |

Each is the existing kind's code with its constants turned into functions of `t`. The kinds are already drawn in the disc's polar
frame, so nothing new is needed but the timer.

## Dials

- **`EVOSIM_THEATRE_MOUTH2_BITE`** (0 to 1, default 0): the strength, `_MouthBiteParams.x`. 0 is today to the pixel, with the
  timer path not even read. Filmed at 1 first, then dialled back.
- **`EVOSIM_THEATRE_MOUTH2_BITE_TEST`** (seconds, default 0): a fake onset every so many seconds on every mouth, needing no
  recording. It lets the motions be built and judged before the builder's bit lands, in stills, portraits and live play.
- **`EVOSIM_THEATRE_MOUTH2_BITE_AT`** (seconds, unset by default): pins every mouth's timer to one value, for phase strips in stills.

## How to check it at film distance

1. **Phase strips, stills only (cheap, first).** For each kind, on a real animal facing its mouth (`EVOSIM_STILLS_FROM=mouth`),
   render `BITE_AT` = 0, 0.05, 0.1, ... up to D, and set the strips side by side. Read each against the motion it should show, and
   against the mouth references in `scratch/owner/realism-refs/mouths-<kind>/`.
2. **Close portrait.** One safari portrait per kind with `PORTRAIT_FROM=mouth` and `BITE_TEST=1.5`. Make a contact sheet of
   consecutive frames around one onset, and check the frame-to-frame jump is smooth: the largest jump no more than 2x its neighbours,
   as the reference check reads it.
3. **Film distance.** The wide film framing of the r63g-b clip's scenes 1 and 2, with `BITE_TEST`. Measure the mouth's radius in
   pixels at that distance, which I expect to be about 3 to 8. Then measure the pulse: the mouth disc's mean luma against the body
   around it. The pulse reads if it lifts the mouth by at least 0.05 luma over at least 3 frames. The kinds' own motions are not
   expected to read there, and the check says so rather than assuming it.
4. **Real bites**, once the bit lands: a window of a recorded run with grazing in it, filmed with the dial on. Compare onsets in the
   film against the bit's rows, and check that sustained grazing shows a rhythm, not a flicker.
5. **Dials off.** The chain's step 0 (dials-off IDENTICAL) on every build.

## Order of work

1. The timer, the shared pulse and the two test dials, in TheatreMouth2, the shader and the one skin's dress. Phase strips for the pulse.
2. The four flat kinds' motions (disc, toothed ring, gape, sucker). Phase strips, then close portraits.
3. Beak and crown. The same checks.
4. The real signal, once the builder's bit lands: the onset and refractory logic reading it, then check 4.
5. Dial back from 1 to the level the film distance check and the portraits support.

Steps 1 to 3 need no recording change and can run before the builder's bit lands, using `BITE_TEST`.
