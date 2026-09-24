# The safari reviewed: what the films say, and what the record could say

*2026-09-24. A read-only review by an Opus subagent of round 47 seed 1's clean safari
(24 clips, `scratch/safari/r47-s1/2026-09-24/`), on the owner's request: "we name them, but
never provide any meaningful information about the creatures, like what are they, what's
special about them, what do they do, how long have they survived". Its two reads are kept as
`scripts/reads/safari-facts.py` and `safari-joints.py`. The implementing pass follows its
ranked list; the summary here is the agent's, kept so the record can cite it.*

## The main finding

Most of what the owner is missing is already in `guide.json`. `SafariGuide.cs` (about lines
395 to 440) reads field names the Python does not write: `generations` is an object read as
an int, the median depth lives at `where.median_depth_m` and the reader looks for `depth`,
`alive_at_end` is an int and the reader accepts only a bool (so the survivor, Lamina
tripleplis, gets no "alive at the end" caption), `body.founder.parts` and
`adult_volume_m3` are not read, `split` and `economics` are dropped. The first fix is
plumbing.

## Defects found on the way

- "Structural only" and "jointed body with no tissue" are false for the viewer: link cells
  earn light at half a leaf's efficiency (`photosyntheticEfficiency` 0.025 against 0.05), so
  Pinnula vibaresa's three-link founder is a green muscle with no leaf, the most striking
  body on the trip, and the caption hides it.
- "A swimmer" means only that the body has a joint: body 355 held both joints fixed over
  194 pose samples; body 4603 moves ±0.9 rad. Joint use is readable from the spread of
  `poses.jsonl`'s `q`.
- Scenes before the first checkpoint are a cousin whose ids do not match the record's; a
  fact about "this body" must come from the live world there.
- No birth scene in the trip (births go only to even-index clades). The trip is ordered by
  score, so time jumps: 2,500 → 1,620 → 1,100 → 10,000 → 20,000 → 12,500 → 2,710 → 25,000
  → 17,500 → 27,500 s. A colony of one member is filmed as a pull-back on one leaf.
- The genus encodes the guild (Pinnifrons, Remiphylla, Lamina by flag triple) and `guide.md`
  says the names mean nothing.

## What is shown now

Seven caption templates repeat across 24 clips: the name with two adjectives and a founding
second; the peak (twice per clade, portrait and colony); members ever; gone by; the tank's
size; the bed's depth; two counts in the time scene. Four faults: unglossed labels
("photosynthetic", "absorptive", "jointed", "random founder", "r47-s1"); numbers without a
scale; the same sentence twice; silence (8 s of captions in a 20 s portrait, 4 s in the 96 s
of descent and floor). What a newcomer never hears: that Pinnifrons febofila was the
longest-lived line of the run (27,274 s, 1,033 members); that crimisis was the first jointed
body to breed and 3.6 times the median volume; that gosurolis was the first leaf to breed;
that filimens lived right under the surface; that plaguplax's two jointless daughters held
1,785 of the 2,619 bodies alive at the end, the run's main story; that balosax was the first
eater to raise a child with a third of its income from snow; that the world went from four
in five jointed bodies at 2,500 s to one in ten at the end.

## What the record could say, and where it lives

Computable now from the files: guild in plain words (lineage flags plus the snapshot's cell
types and the config's link efficiency); parts, joints, neurons and brain inputs (the
snapshot); size as a percentile of founder volume over the picker (median 0.074 m³); the
split's gain or loss and the firsts (the card); member lifespans and the founder's children
(`lineage.jsonl`: febofila's members live a median 3,384 s against 1,895 across all births);
joint use locked or moving (`poses.jsonl`); pose (`tilt.py`); eating (`absorptive.jsonl`:
balosax's founder earned 882 J from food against 1,734 from light); depth, radius, height
above the floor (the card's `where`); the reef relation (`run.json` plus positions: body 6117
sat 1.3 m above a table at 20,000 s); the clade's count over time (positions, for a
sparkline); daughters and their fate (plaguplax has 145 daughter clades, two ending with 904
and 881 alive); the world at the filmed second (`stats.jsonl`: the largest fall was 228
bodies between 1,910 and 4,180 s; 3,433 J on the floor against 191,237 in the water at
20,000 s). Needing a new instrument: speed through the water (relative to the current) and a
high-rate pose trace for a stroke's frequency. Useless: cause of death, every row `starved`.

## How to show it

One fact per caption, about 60 characters, 4 s on and 1 s off; 3 to 4 per portrait (what it
is, what is special, what it does, its fate), 2 to 3 per colony (how many and where, the
world then, what came of it), no sentence twice; the unit and the normal value first; the
run's clock in hours and minutes; no arm code. Call-outs, each against the design spec's "no
interface but the provenance word" and so the owner's ruling: a colony tint (the subject
clade in colour, the rest desaturated: today a colony shot cannot show which bodies are the
clade), a clade sparkline, a tank minimap, a depth gauge, a size silhouette. A trip told in
time in four chapters: the founding (gosurolis, crimisis, febofila), the first eater
(balosax's birth and portrait), the middle (geperax, vibaresa's birth, filimens), the
takeover (plaguplax, its daughters, the time scene, tripleplis). The descent's sand cut or
captioned with depth marks; the floor given a fact or dropped.

## Procedural or a language model

Procedural ranking plus templates gets the facts; a model adds phrasing. The facts that matter
are ranks and comparisons the record holds exactly, and the risk sits in the claim: the
reviewer's own draft wrote "tripleplis, the largest line at the end" and the record says
Thallus pleceplis ended larger (904 against 881); a ranked fact table would have refused the
sentence. Design: `guide.py` emits a ranked `facts[]` per card (`id, kind, value, unit, rank,
of, percentile, normal`) with an interest score; the director takes the mandatory "what it
is" line plus the top two or three; templates per kind. If a model were used it would be sent
the facts as JSON and return lines tagged with the fact ids they use, and a checker would
require every number to equal a cited slot, every name to be a card's, and no superlative
without a rank; on failure the template; cached per card hash. Sending run data out is
outbound data movement under the owner's rule and needs approval each time; the procedural
path does not depend on it.

## The ranked list for the implementing pass

1. `SafariGuide.cs`: read the card's real field names and shapes.
2. `guide.py`: guild in plain words; the genus glossed.
3. `guide.py`: the ranked `facts[]` per card (lifespan, children, eating share, joint use,
   reef relation, daughters, world facts, count series, exemplar).
4. The captions: a template per fact kind, one fact per caption, no repeats, normal value
   first, no arm code.
5. The trip: time order in chapters; a birth for every split clade after the first
   checkpoint; the descent, the chapter card, the floor and the one-member colony fixed.
6. Portraits: the filmed body's age and children from the live world in a cousin; "a
   swimmer" only when the joints move.
7. The call-outs, behind the owner's ruling.
8. New instruments: speed through the water; a high-rate pose trace.
9. Optional: the model-polish pass with the checker, off by default, approval per use.
