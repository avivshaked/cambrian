# Per-part contact: the build spec

*Fable, 2026-09-23, from D107 (per-part contact beside the support cost) and
`fable-propose-round-46.md`'s rule 4, written before the ruling so the build is a day. The
farm's contact today is one bounding sphere a body (`Creature.RefreshContactSphere`: the
root's position, a radius reaching the farthest link's own half-diagonal), so a fan of seven
leaves is a sphere twenty metres across that shoves every neighbour, and the mouth's
`NearestParts` finds the touching parts by a search after the fact. This spec puts the
sphere on the link.*

## 1. The model

Each link `i` of a body carries a sphere at its own position with radius `LinkReach[i]`
(half its diagonal, the padding the body's sphere already uses) and its own velocity. The
grid enters every active body's links, sized at two mean link radii (the same rule, on
links). A body's query, per link, gathers the other bodies' links in the covered cells,
sorted and deduplicated by `(body, link)` as `ContactGrid.Neighbours` sorts today; a
body's own links never pair (self-contact is not modelled, and D101's stillbirth rule covers
the folded body). For each overlapping link pair the push is `ContactLaw.PairPush` with the
same spring and damper, the reduced mass from the **two links' masses** rather than the two
bodies', and the force is applied at the link into its own `Fext` row rather than spread from
the body's total, because the solver carries it through the joints from there. The bed and
the glass read each link's own sphere. The dedup key makes the sum a fixed order, so the
result is thread-count independent as now.

On a crowd of one-link bodies the model is the recorded one bit for bit: a one-link body's
sphere is its link's, its velocity its link's, and the reduced mass its own. So the change
reads only where a multi-part body touches something, which is the acceptance's lever.

## 2. Where it lives

- **`RunConfig.ContactPerPart`**, bool, default false; `EVOSIM_CONTACT_PER_PART`; the header
  token `contact per part` or `contact per body` beside `ovl` in the census line. A tunable
  and not an engine version, so that every recorded world replays and the round's manifest
  says which model ran; it refuses every earlier config, as every tunable does.
- **`Creature`**: `LinkContactActive`, and the pending and committed sphere per link
  (centre, radius, velocity), refreshed and committed where the body's is; `MarkLost` zeroes
  every link's radius. The body's sphere stays for `Volume.Note` and the placer.
- **`ContactGrid`**: entries are links when the flag is on, `(body, link)` in the bucket; the
  tree mean and the largest radius read link radii; `Neighbours` takes a link and returns
  `(body, link)` candidates.
- **`Contacts.Apply`**: the per-link loop above under the flag; the recorded path untouched
  under it.
- **`DynamicsWorldContacts`**: `OverlapPair` stays a body pair, deduplicated across link pairs
  so `overlaps`, `ovl/body`, `ovl jnt %` and `ovl held %` keep their meaning, and carries the
  first overlapping link pair `(partA, partB)` seen in the fixed order, so `NearestParts` returns
  it directly under the flag and the mouth's contact is the part that touches.
- **The census note** in `analyse-arm.ps1` and `contact_aliases.py` says which model a run's
  columns came from, from the header token.

## 3. The card

Spike3's contact kernel enters a sphere in every cell it covers and sorts the candidates;
per-link entries are the same kernel over `Σ links` entries with a two-integer key. Round 45's
crowd has about 1.4 links a body (280 jointed of 6,145, the largest 16), so the entry count
grows by about that and the occupancy capacities (256 candidates, 64 overlaps) are re-measured
on the flag's first run before the port carries them.

## 4. Cost

By arithmetic the contact phase scales with entries times the cells a sphere covers, and link
spheres are smaller than body spheres, so a body's per-link queries read fewer cells each. On
the record the contact phase is a few percent of the step. The first screen's `wallStep*`
phases are the measurement.

## 5. Tests

1. Two one-link bodies touching: the force under the flag equals the recorded model's to the
   bit, both signs.
2. A two-link body whose far link touches a one-link body: the push lands on the far link's
   `Fext` row alone, with the reduced mass of the two links; the body's root row reads zero
   from the contact.
3. A fan of seven copies (the giant's genome) beside a leaf a metre from its root and twelve
   metres from its seventh copy: the leaf is pushed under the recorded model and not under
   the flag.
4. The digest at 1 and 16 threads is equal under the flag over 1,000 steps of the crowd
   fixture.
5. With the flag off the crowd fixture's 3,000 s regress is identical in every field and the
   lineage byte-equal.
6. With it on, a 3,000 s dt 0.02 screen of round 45's launcher closes both books, `ovl/body`
   is reported, and the mouth's kills name the touching part (the kill rows' part index
   against the overlap's link pair).

## 6. As built (2026-09-23, an Opus subagent in a worktree, merged at `6ae0482`)

Sections 1, 2 and 5 as written, with `PerPartContactTests` (four, and a Slow wall reading).
Two one-part bodies read the same forces to the bit under both models, in open water and
against the bed and the glass together; a push on a far link lands in that link's own row
with the two links' reduced mass (5.33 kg where the two bodies' gave 7.27) and the root's row
reads zero; the seven-copy fan no longer shoves a leaf eleven metres from its seventh copy
(640 N under one sphere a body, 0 under a sphere a part); and the digest is equal at 1 and
16 threads over 1,000 steps of 1,000 bodies (`014abf9e2aa40fc1`). Five departures. The header
token is at the end of the line before the hash, where a new knob goes, not beside `ovl`.
The per-body speed cap is applied per link against the link's mass. A one-part body's link
sphere is a copy of its body sphere, which is what makes bit-for-bit true (the recorded
velocity is `(v·m)·(1/m)`, an ulp off `v`). Whether a body is in contact at all stays the
body's flag. And a checkpoint rebuilds the link spheres on restore rather than storing them,
so `StateVersion` is unchanged by this rule; `--verify-checkpoint` with the switch on has not
been run and is owed before a resume under it is trusted. **The cost is not what section 4
expected**: a link's sphere covers fewer cells, but there are 2.8 times as many spheres, so
the contact phase is 1.9 times the recorded one (1.48 s against 0.79 s for 1,000 steps of
1,000 bodies on one thread) and the whole body phase 19% more at 8 threads; read on the
screen's `wallStep*` phases at the round's crowd.
