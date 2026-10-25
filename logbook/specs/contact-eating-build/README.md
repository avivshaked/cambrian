# The contact-eating build: the crowd fixture and its dial-off proof

`fixtures.ps1` records the Dynamics crowd fixture (round 44's world, seed 4, 20,000 s) on `mouth-point`. Its
`config.json` must carry every tunable the build has, so the fixture is recorded again whenever a tunable is added.

## The record on the contact-eating build, before D224 (2026-10-24)

`runs/cefix-s4/2026-10-24-080816-5511de3c` was recorded unbroken from commit `bc800b3c`, a clean tree. It used the
Release farm built from `4935a4b8`'s source (core `5a66b5fd`, dynamics `bb71b218`, farm `96ab5286`), on logical 8 to
11 at 4 threads, and took 38 minutes of wall. Its config hash is `5511de3c7754e008`. It differs from the earlier
fixture's `bfbdfbc68771a2fd` only by the fields added since, each at its off value.

Two tries before it stopped short:

- One at 9,170 s, stopped by hand for the machine's heat beside the `r62ce` screen.
- One at 9,040 s, stopped by its own pace guard at 0.25. The guard compared the run's last ten minutes with its
  fast first 470 s on a quiet machine, and every row of that try equalled the earlier fixture's. The script now sets
  the guard at 0.1.

**The proof.** Against `runs/r62sfix-s4/2026-10-20-060945-bfbdfbc6`, the fixture recorded on the build before
contact eating (`92b61136`), `python -I scripts/reads/compare-run-rows.py <old> <new> --from 0` gives:

- stats: 2,000 rows, 10 to 20,000 s, 170 fields a row, 340,000 values, no difference;
- lineage: 10,408 rows in each, equal apart from the intake-origin fields `iok`, `iak` and `ic`;
- the only stats fields in one run alone are the seven intake-origin columns, from `44712ce3`.

The 20,000 s snapshot, `snapshots/000020000.jsonl`, is byte-equal between the two. The comparison's output is
`cefix-s4-vs-r62sfix-s4-rows.txt`.

So with both of its dials off, the contact-eating build steps a 20,000 s world on the CPU solver bit for bit as the
build before it did. This run cannot serve as the fixture of the D224 build, whose new tunable it lacks.

## The record on the D224 build, the fixture (2026-10-24)

`runs/cefix-s4/2026-10-24-090221-59a8f659` was recorded unbroken from commit `d8e4f2e2`, a clean tree, on the Release
farm built from that commit (core `75e257f4`), on logical 8 to 11 at 4 threads, in 37 minutes of wall. Its config hash
is `59a8f65945a17b51`, and it carries `biteTakesStore` at its off value. `src/Evosim.Dynamics.Tests/RunFixture.cs`
reads it.

Against the same earlier fixture, the comparison gives the same result: 2,000 stats rows and 340,000 values with no
difference, 10,408 lineage rows equal apart from `iok`, `iak` and `ic`, and the 20,000 s snapshot byte-equal
(`cefix-s4-d224-vs-r62sfix-s4-rows.txt`). With the fixture in place, the Dynamics tests outside the slow category pass,
239 of 240, with 1 skipped.

## The record on the D226 build (2026-10-24)

`runs/cefix-s4/2026-10-24-170754-85523498` was recorded unbroken from commit `88982067`, a clean tree, on the Release
farm built from that commit (core `69a403e6`), on logical 8 to 11 at 4 threads. Its config hash is `85523498161bcc9e`,
and it carries `biteDamageScale` at its default of 1. `src/Evosim.Dynamics.Tests/RunFixture.cs` reads it.

Against the same earlier fixture, the comparison gives the same result as before. There are 2,000 stats rows and
340,000 values with no difference, 10,408 lineage rows equal apart from `iok`, `iak` and `ic`, and the 20,000 s
snapshot is byte-equal (`cefix-s4-d226-vs-r62sfix-s4-rows.txt`). With the fixture in place, the Dynamics tests
outside the slow category pass, 239 of 240, with 1 skipped.
