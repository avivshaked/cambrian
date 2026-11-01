# D275's build spec: a bite needs the mouth

*An Opus design read for the coordinator on 2026-11-01, read-only. The coordinator reviewed it and added the amendments at
the end, which override the body where they differ. File and line numbers are as read that evening on main at `2d7a0a11`,
and the theatre's on `theatre-look-merged`.*

## 1. Where a bite is decided today, and the change

- **The contacts.** The farm's one producer of contacts is `HandOverContacts` (`src/Evosim.Farm/Metabolise.cs:469-484`,
  `new CreatureContact` at :480). It names one link pair per touching body pair. Under true shape that pair is the first
  touching link pair in A's link order (`CreatureContacts.cs:107-133`, written at `ContactTrueShape.cs:285` after `Pair` at
  :96-129, and read back by `DynamicsWorldContacts.cs:285-292`).
- **The blow.** Core's `ApplyDamage` (`WorldMouth.cs:362-443`) works out each side's blow: on B at :410-411, on A at
  :426-427.
- **The wound.** `Wound` (:486-531) turns a blow into a share of health and records the attacker (:492).
- **The meal.** `OfferBite` (`WorldContact.cs:734-810`) turns the share into the meal and caps it (:796-803).
- **Settlement.** `SettleBites` (:819-896) pays the meal and sets the ate bit through `NoteTookMatterAtPart` (:840, then
  `WorldMouth.cs:254-270`).

One gate covers the wound and the meal together. The meal is `OfferBite(..., tookFromB)`, and `tookFromB` is what `Wound`
returns. So a blow zeroed before `Wound` leaves nothing behind: no wound, no meal, no ate bit, no attacker record (so no
kill credit and no swallow) and no `MouthBlows` count. The contact sense (`NoteContact`, :406-407) still records the touch.

Core has no part poses, and GJK lives in Dynamics. So the farm measures the gap, and Core rules on the measure.

- **S1, Core.** Add `MouthPoint.Radius(Phenotype, int)` in `src/Evosim.Core/Development/MouthPoint.cs`, beside `Local`.
  Its arithmetic is in section 4.
- **S2, Core.** Give `CreatureContact` (`CreatureContact.cs:29-53`) the mouth's measure for each side; amendment 1 says
  which fields. NaN means not measured. The existing constructor sets every new field to NaN or -1.
- **S3, Dynamics.** Add a new static `MouthTouch.Reach(...)` in `src/Evosim.Dynamics/MouthTouch.cs`. It calls
  `ConvexCore.Distance` (`ConvexCore.cs:233-304`) unchanged, so nothing in `gpu-kernel.template` moves.
- **S4, Farm.** In `HandOverContacts`, only with `Config.BiteNeedsMouth` on, measure each side's mouths, serially and in
  list order. With the switch off, line 480 is unchanged.
- **S5, Core gate.** After :411, and the same after :427: if the switch is on, the blow is above 0, the attacking part
  carries intake, the attacker is not remains, and the mouth's ratio is not at most 1, the blow is set to 0 and
  `MouthBlowsRefused` rises. Read the switch once per pass, beside `biteScale` (:380). Write the test as `!(ratio <= 1f)`,
  so that NaN refuses. A claw's blow (intake 0) and every blow from remains never reach the gate.
- **S6, instruments.**
  - A `mouthBlowsRefused` column in the stats row (`Row.cs:1066`), written only with the switch on.
  - The counter in the checkpoint only with the switch on (`WorldState.cs:621-659`, :1022-1034), so that the layout is
    unchanged when it is off.
  - A header token, `· a bite needs the mouth`, in `Report.cs:480-488`.

## 2. The switch and its refusals

- **The tunable.** `RunConfig.BiteNeedsMouth`, a bool with `[Tunable("mouth")]`, after `DeathRowIncome`
  (`RunConfig.cs:1783-1784`), default off.
- **The environment variable.** `EVOSIM_BITE_NEEDS_MOUTH`: a `Flag` in `EnvBinding.cs:553-565`, a settings field near
  :2235, and the copy into the config near :1209.
- **The refusals,** in `World.ValidateFounding` near :3135-3177. It is refused when `ContactEating` is off. It is refused
  unless `ContactShape` is `TrueShape` or `TrueShapeAtPoint`, since below true shape the solver builds no cores
  (`Creature.cs:555-559`). The existing refusal at :3076 already requires `ContactPerPart`.
- **The reflection tests.** `Hash()` (`RunConfig.cs:4937-4961`) and the JSON both walk `ConfigSchema`, so the attribute
  reaches both. The JSON key is `biteNeedsMouth`.

## 3. The host and the card

The decision and the settlement run on the host, under either engine.

On the card, the kernels make only the overlap lists (`NoteOverlapPart`, `Kernels.F.g.cs:9956`). Each block, `Unpack`
(`gpu-runner.template:3357`) copies back the bodies' `Position` and `RotationMatrix` (:3392-3420) and the overlap lists with
their parts (:3539-3563). `BuildEvents` (:3705-3740) then fills `Dynamics.Overlaps`, and the same `Metabolise` runs. The
cores, margins and hulls are the host's own tables (`Creature.cs:804-825`).

So the card needs no new data, no kernel change and no regenerated source; the regeneration check should show no
difference. In single precision the host's `Position` is the card's float, so the card's refusals are the card's own
realisation, as everything on the card is.

## 4. The mouth radius and the gap

**The radius** is `TheatreMouth2.Measure`'s (`TheatreMouth2.cs:139-176`), reproduced in Core:

1. Let `h` be the absolute half-extents, per axis, and `p = MouthPoint.Local(phenotype, i)`.
2. For a sphere, `face = min(hx, hy, hz)`.
3. Otherwise take the axis of the largest `|p|` component: x if `|px| >= |py|` and `|px| >= |pz|`, else y if
   `|py| >= |pz|`, else z. `face` is the smaller of the other two half-extents.
4. The radius is valid only if `face > 1e-6` and `|p| > 1e-6`. Then `r = 0.5f * face`.

**The gap,** in double, from the pose `ObserveMouths` uses (`Metabolise.cs:432-434`):

1. The mouth point is `m = Pos_A[3i] + R_A[9i] · Local(solverA.Phenotype, i)`.
2. Take `rel = m − Pos_B[3q]`, the short way round if `config.Seamed` (`Contacts.ShortWay`, `Contacts.cs:936`).
3. Call `ConvexCore.Distance(Vec3.Zero, null, Mat3.Identity, rel, LinkCoreHalf_B[q], LinkCoreHull_B[q], R_B[9q],
   Vec3.Zero, out d, …)`, as the theatre's `PointApart` (`TheatreMouthBite.cs:1374-1384`).
4. `gap = apart ? d − LinkMargin_B[q] : −LinkMargin_B[q]`.

The mouth reaches when `gap / r ≤ 1`, that is, when a ball of radius `r` at the mouth point touches the part's core plus
margin. The cores come from `ConvexCore.Of` (:90-117): a sphere is a point with margin `Mean(h)`, a capsule a segment, a
box a rounded box, and a family part a 98-point hull (`OfFamily`, :127-148).

## 5. What must be re-pinned

- **Config fixtures.** `src/Evosim.Core.Tests/fixtures/r42-config.json` and `r57-s2-config.json` gain
  `"biteNeedsMouth": false` in their `mouth` group, and each gets a new `configHash`.
- **Hash pins.** `EnvBindingTests.cs` (`Round42ConfigHash`), and `ReportTests.cs`'s `Replace` and `EndsWith` lines.
- **Nursery configs.**
  - Forward the three live files in `rounds/nursery/configs/` with `scripts/nursery-config-forward.py`, keeping the old
    ones, and update that folder's README.
  - Move the `InlineData` in `WorldConfigFileTests.cs:20-22` and add `Assert.False(c.BiteNeedsMouth)`.
  - Whether any nursery recipe also needs repointing is unconfirmed.
- **The crowd fixture.** `RunFixture.cs:45,55,344-345` names `runs/cefix-s4/2026-10-31-205155-87f3b329`. It is
  re-recorded on the new build with the switch off and checked row-equal to that run. That is a farm run, the
  coordinator's.
- **Must not move.** `DialOffDigestTests.PinnedDigest` (:48), `BiteRebuildTests`' folded hashes and `FilmWindowDigestTests`
  are part of the switch-off proof.

## 6. Tests to add

**Core,** `BiteNeedsMouthTests.cs`:
- `RefusedWithoutContactEating` and `RefusedWithoutTrueShape`: each refusal names D275.
- `SwitchOffReadsNoMeasure`: one world stepped with the measure at 5, at NaN, and with the old constructor gives equal
  `WriteState` bytes.
- `ASideBrushBlowIsRefusedAndAMouthTouchLands`: switch on.
  - At a ratio of 3: health, tissue, the biter's energy, `TookMatterMask` and `MouthBlows` are all unchanged, and one
    refusal is counted.
  - At 0.5: the blow lands exactly as with the switch off.
- `AClawAndRemainsAreUnchanged`: a claw's blow and a remains part's blow land at NaN.
- `MouthRadiusClosedForms`: the radius for a box, a capsule, a sphere and a family part.

**Dynamics,** `MouthTouchTests.cs`:
- `GapMatchesClosedForms`: a box face, edge and corner, a capsule, a sphere, a hull, a point inside the core (gives
  `−margin`), and a gap across a seam.

**Farm,** `BiteNeedsMouthFarmTests.cs`:
- `SideBrushRefusedTipTouchLands`: a hand-placed eater against a leaf, held there before every census, as
  `DialOffDigestTests` does.
  - Placed side-on: the leaf keeps its health, the bite columns stay flat, and `mouthBlowsRefused` rises.
  - Placed tip-on: a wound, a meal and an ate bit in the pose stream.
- `SwitchOffIdentity`: a pinned digest of a true-shape contact-eating world on the tree before the build, obtained with
  `git archive` as `DialOffDigestTests`' pin was.
- The binding and header tests, written as `ReportTests.cs:921-956` writes them for the kill gut.
- A switch-on world resumed from its checkpoint equals the unbroken run.

**Card:**
- `BiteNeedsMouthOnTheAcceleratorIsTheCpuSolversWorld`, in double, following `GpuRemainsTests.cs:504`, run without
  `GpuMeanTests`.
- Card identity sittings at groups 32 and 64, with the switch on and off. These are the coordinator's.

## 7. The dial for the first screen's overshoot

| Dial | Value in the `env-r65-c` chain | Where it is set |
|---|---|---|
| `IntakeScale` | 16 | `env-r62kg.ps1:7` |
| `BiteTakesStore` | on | `env-r62cs-w500.ps1:5` |
| `BiteDamageScale` | 1 | not set |

- **`IntakeScale`.** Raising it buys nothing per bite. The cap cut 318 J of about 1.03 MJ offered on 6202, and 260 J of
  about 2.75 MJ on 6203.
- **`BiteDamageScale`.** About two-thirds of mouth blows leave their part alive (13,014 finishing of 34,649 on 6202,
  38,965 of 114,335 on 6203), so this dial deepens a landed blow.
- **The open question.** It is unconfirmed which blows go unfinished. If they are mostly on animals, this dial feeds
  predators more than grazers (D225's risk). A grazer-specific arm would be `EVOSIM_LEAF_TISSUE_ENERGY` (D211, at 1),
  which makes every leaf bite richer.

## 8. Open points from the read

1. One link pair per body pair (see amendment 1).
2. The theatre's ratio uses the drawn surface (`SignedDistance`, `TheatreMouthBite.cs:856-878`): exact for a box, but a
   ball of the largest half-extent for spheres and family parts. Until the theatre switches to `MouthTouch` and
   `MouthPoint.Radius`, a filmed bite and a counted bite can disagree on spheres, family parts and box edges. That is
   theatre work.
3. D275's 25% and 41% were measured on the drawn surface against the nearest prey part. The farm rule measures the true
   surface, so the share that lands in a world may differ.
4. One physics step of lag between the pair test and the gap's pose, as with D214's mouth points.
5. `mouthBlows` is not checkpointed at `BiteDamageScale` 1, so a resumed world's counts restart from 0 (unconfirmed
   against `compare-run-rows`).
6. Host cost is unmeasured: up to a few GJK calls per contact per metabolic step, run serially.

## Amendments by the coordinator (2026-11-01)

1. **The mouth's own pair, with the switch on.** The census names one link pair per body pair, the first that touches in
   A's link order, so a mouth that touches behind an earlier link would never bite. That contradicts D275's intent, which
   is that a mouth on food bites. With the switch on, for each side of a contact:
   - Measure every eating part's mouth point (`MouthPoint.IsEatingPart`) against every part of the other body.
   - Keep the smallest `gap / r`, with its mouth link and the other body's part.
   - If that ratio is at most 1, the side's mouth blow is that mouth link's blow on that part: the attacking part and the
     wounded part are that pair, not the census pair.
   - Otherwise the side's mouth blow is refused.

   A claw's blow keeps the census pair, as today. `CreatureContact` therefore carries, for each side, the ratio, the mouth
   link and the other body's part (NaN and -1 when not measured). With the switch off nothing of this is computed or
   read. The coordinator tells the owner this is how D275 is realised.
2. **Cost.** A body's other parts can be screened by distance before GJK: skip a part whose bounding ball, grown by `r`,
   does not reach the mouth point. Measure the host's added time per metabolic step in the farm test, and report it.
3. **Scope of the build.** Sections 1 to 6 with amendment 1. The overshoot dial (section 7) is chosen in the screen's
   pre-registration, not here. The theatre's switch to the true-surface ratio (open point 2) is the theatre lane's.
