# Build spec: a body is born inside the glass, all of it (Astra review F2, 2026-09-12)

Read `CLAUDE.md` in full, then `logbook/specs/tank-spec.md`, `src/Evosim.Core/Environment/TankGeometry.cs`
and `src/Evosim.Core.Tests/TankTests.cs`, `unity/Assets/Evosim/Sim/SharedVolume.cs`
(`InTheWater`, `Free`, `TryReserveFounder`, the offspring reservation path, and where the
radius handed to `Free` comes from), `unity/Assets/Evosim/Sim/TankWall.cs` (the class remarks
and the `Segments` remarks) and `unity/Assets/Evosim/Sim/Editor/SharedSpaceSmoke.cs` part 4.

Rules: edit only in this worktree (`D:\Projects\experiments\evolution-simulator\scratch\wt-clearance`,
branch `clearance`); write nothing outside it, nothing under the session scratchpad or TEMP;
no commit; no Unity (everything under `unity/` is uncompiled by you; say so); no `.md` files;
do not touch running workers, `runs/` or the main tree; never sleep, poll or wait. Do not edit
`CurrentField.cs` or `GridField.cs` (another builder owns them).
`./scripts/core-test.ps1 -Filter TankTests` as you go and the default suite once at the end.

## The fault

`SharedVolume.Free(p, radius)` documents itself as asking whether a sphere is clear and in the
water. In a tank the water test is `InTheWater(p.x, p.z)`: the centre alone. The radius is
tested against other bodies and never against the glass, so a candidate one centimetre inside
a slab is accepted with a half-metre bounding radius, and a unit-cube founder drawn over the
full disc had a corner outside the circle in 208 draws of 1,000 (the review's arithmetic
probe, no physics). A body born through a static collider is a contact the solver resolves
on its first step, which is a throw the birth gate exists to prevent.

## The repair

1. **`TankGeometry.Inside(x, z, radius, clearance)`** (Core): true when the point is at least
   `clearance` inside the circle, so the existing two-argument-plus-radius form is the
   clearance-0 case. Tests in `TankTests`: a point at R − 0.49 with clearance 0.5 is out; at
   R − 0.51 is in; the axis is in for any clearance below R; clearance at or above R refuses
   everything.
2. **`SharedVolume.InTheWater(x, z, clearance)`** and `Free` calls it with the sphere's
   radius. Both founder and offspring reservation go through `Free`, so both gain it without
   another call site. A draw refused for the glass is a rejection like a draw onto an
   occupied spot (the existing `AttemptBudget` reading). The reservation policy itself does
   not change: a child reserves its adult radius (D088), a founder its birth radius.
3. **Verify the radius bounds the body**: find where the radius handed to `Free` comes from
   (`Body.Radius`, the phenotype's extent, or the placer's own arithmetic) and state whether
   it bounds every part of the developed body about the placed root, at the orientation the
   harness builds it. If it does not, say so in the final message and in a comment; do not
   invent a new radius.
4. **`TankWall` comments**: the prism's radial excess `R(sec(π/48) − 1)` is 12.1 mm in a
   5.64 m tank and 24.2 mm in an 11.3 m one, ten times what both comments say, and above the
   default contact offset, not under a quarter of it. Correct both remarks and their
   consequence: a body at a slab join can stand up to 12 mm outside the mathematical circle;
   the placer never puts one there, the field reads the glass for it (clamped `s`), and the
   smoke's quarter-metre tolerance covers it.
5. **The smoke, part 4**: where it checks founder centres inside the circle, check the
   reserved sphere: centre radius plus the body's bounding radius at or below R for every
   founder and every offspring born in the walk, and after the world is built, every part's
   world position plus its own bounding extent at or below R plus the wall tolerance. Report
   the count of placements checked in the smoke's line. The caller compiles and runs it.

## Final message

Tables: files changed; the new tests and their numbers; what the radius handed to `Free` is
and whether it bounds every part (with the code path); the smoke's new check (uncompiled);
the suite's count and time; anything ambiguous and what you chose.
