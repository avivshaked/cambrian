# The nursery's world configs

Each file is a world config a recipe names, kept byte for byte as the run that first used it
read it, and named by the hash Core computes from its contents (`RunConfig.Hash()`). A recipe's
`world.configHash` is checked against that computed hash on load.

The `configHash` field inside a file can be stale. `7319d6b528cc77f1.json` is the config of
`pilot-deep-brain2` (`scratch/nursery/runs-jd/big-cur-deep-brain2/config.json`), which was
edited by hand after it was written, and records `ba990f552d008e51`. Every run on it prints
"the config's recorded hash differs". The file name, and the hash on that line, are the ones
to trust.

`3fd98985cdf68008.json` is the leaf tank's world (`logbook/specs/nursery-leaf-tank-plan.md`, step 5): the config of
`r58-s3j` (`runs/r58-s3j/2026-10-12-080742-dfc0e881/config.json`, recorded hash `dfc0e8819ff9b4e0`) on round 59's
format. The sixteen keys round 59's build added are inserted as text at their defaults, as `3f74be1aae2421fe.json`
was made from `55aab9ae45f61732.json`, and nothing else is changed. The leaf recipes' overrides make it a 40 m tank.

`e6bdb9c74cb48742.json` is the leaf tank's world at D177's prices, for D178's arm (`logbook/specs/nursery-leaf-tank-plan.md`,
step 13): `3fd98985cdf68008.json` with the consumer cell's upkeep at 2 W/m³ in place of 6, `attackWattsPerUnit` and
`intakeWattsPerUnit` at 0.033 in place of 0.1, and its `configHash` line set to its own hash. Nothing else is changed.

`a8cbe76c9532bc71.json`, `2b125f6cbda84a01.json` and `e80c201e54b3963f.json` are, in that order,
`3f74be1aae2421fe.json`, `3fd98985cdf68008.json` and `e6bdb9c74cb48742.json` on round 60's format (D185): `world.matterIslandGlassMarginMetres` is inserted at 0 after `matterIslandDepthMetres`, and the
`configHash` line is set to the file's own hash. Nothing else is changed. Round 60's build refuses the old files.

The new files carry brain2's `-w5` recipes, `leaf-direction-sparse-w5` for the tests, and the repeat
`leaf-direction-patches-s2-w5` with its founders' recipe. The leaf tank's other recipes stay on the old files, since
their runs have ended. A recipe that runs again is moved the same way when it is needed.

`b2885199f9213276.json`, `cd9cc88c0054cf90.json` and `9fa947462638667c.json` are, in that order, `a8cbe76c9532bc71.json`, `2b125f6cbda84a01.json` and `e80c201e54b3963f.json` on integration-1's format. Forty-six keys are inserted as text, each at its default and where the writer puts it, as the earlier moves did. The coral group's twenty-seven come in whole (D194, with `reefShape` at Mushroom and every dial at its default). Two come from development: `development.reflectMirroredAnchors` (D228) and `development.scaleLengthensOnce` (D199). Two come from feeding: `feeding.plantsKeepMatter` (D206) and `feeding.plantsKeepOwnFirst` (D207). Then `field.corpseHalfLifeSeconds` (D212) and `fluid.shapedAddedMass` (D229). Seven come from the mouth: `mouth.biteDamageScale` (D226), `mouth.biteTakesStore` (D224), `mouth.contactEating` (D219, D220), `mouth.intakeReachFromMouth` (D214), `mouth.killSwallowsBody` (D232, after `biteDamageScale` as main's fixtures have it), `mouth.intakeScale` and `mouth.remainsWindowSeconds` (D220). Two come from physics: `physics.dragLimitAtEveryStep` and `physics.dragLimitByWeldedBody` (D196, D199). Four come from the world: `world.contactShape` (D228), `world.matterBurialDesertPerSecond` (D190), `world.readAcrossSeams` (D204) and `world.seedOverrides` (D192). Each default leaves the world as it was. They are switches off, scales of 1, a half-life of 0, a burial rate of -1 (the islands' rate), spheres for contact and no seed overrides. `remainsWindowSeconds` stands at 60, which nothing reads without `contactEating`. The `configHash` line is set to the file's own hash, and nothing else is changed, the order of the keys already there included. The inserted lines take their file's line ends, so the CRLF file `a8cbe76c9532bc71.json` yields a CRLF file. Round 62's build refuses the old files.

The new files carry the `-w7` copies of `brain2-w5`, `leaf-direction-sparse-w5`, `leaf-direction-patches-s2-w5` and `leaf-direction-patches-s2-founders-w5`, and `leaf-direction-patches-w7`, which is `leaf-direction-patches` moved from `e6bdb9c74cb48742.json` to `9fa947462638667c.json`, the successor of `e80c201e54b3963f.json`. They also carry the curriculum's `curriculum-rung1` and `curriculum-bench-w7`. The `-w5` recipes and every other recipe stay on the old files, and `Recipe.Read` reads them without opening the world.

`27b08fb4062517dc.json` is `9fa947462638667c.json` without `mouth.killSwallowsBody`, on round 62's format. It stays because the curriculum's calibration bench, `curriculum-bench`, ran on it on the curriculum build `fbe5b233` (the local tag `nursery-bench-fbe5b233`). Integration-1's build refuses it.

`613ca725c30288c0.json`, `ba387b3293d820e0.json` and `9d9aca71c61bd36c.json` are, in that order, `b2885199f9213276.json`, `cd9cc88c0054cf90.json` and `9fa947462638667c.json` on the pace build's format (D239). One key is inserted as text, `physics.paceFactor` at 1, between `driveLimitAtEveryStep` and `physicsStepSeconds` where the writer puts it, and 1 leaves the world as it was. The `configHash` line is set to the file's own hash, and nothing else is changed, the line ends included, so the file that was CRLF stays CRLF. The pace build refuses the old files, which stay for the recipes that name them. The new files carry the `-w8` copies of `brain2-w7`, `leaf-direction-sparse-w7`, `leaf-direction-patches-w7`, `leaf-direction-patches-s2-w7`, `leaf-direction-patches-s2-founders-w7` and `curriculum-bench-w7`, and `curriculum-rung1`, which is repointed to `9d9aca71c61bd36c.json`. `27b08fb4062517dc.json` and `curriculum-bench` are not moved, as the paragraph above says.

`220192064e01e1c1.json`, `d17282b4a0caf9db.json` and `11819c680236db49.json` are, in that order, `613ca725c30288c0.json`, `ba387b3293d820e0.json` and `9d9aca71c61bd36c.json` on main's format after integration branch 2 (D243, D248). Nine keys are inserted as text, each in the template's place within its group. Each default leaves the world as it was: `development.shapeIds` (box, sphere, capsule), `fluid.strokePushesAssembly` (false), `growth.growthAdultSeconds` (0), `growth.growthReserveMaxSeconds` (300), `growth.growthReserveMinSeconds` (1), `growth.growthReserveStartSeconds` (30), `growth.juvenileStrengthScales` (false), `mutation.formChance` (0) and `mutation.growthReserveChance` (0). `scripts/nursery-config-forward.py` does the insertion, from `src/Evosim.Core.Tests/fixtures/r42-config.json` as the template, and the next build that adds tunables is met with the same script. The `configHash` line is set to the file's own hash, as the build prints it. Nothing else is changed, the line ends included: the file that was CRLF with one LF line stays so, and its new lines take CRLF. Main refuses the old files, which stay for the recipes that name them. The new files carry the `-w9` copies of `brain2-w8`, `leaf-direction-sparse-w8`, `leaf-direction-patches-w8`, `leaf-direction-patches-s2-w8`, `leaf-direction-patches-s2-founders-w8` and `curriculum-bench-w8`, and `curriculum-rung1`, which is repointed to `11819c680236db49.json`. `27b08fb4062517dc.json`, `curriculum-bench` and the `-w8` recipes are not moved.

`3a95d053dd97082d.json` is D235's graft film's world (`rounds/nursery/nose-graft-film.json`), on branch `nose`'s format. Its
source is `9fa947462638667c.json` on main, which is `e80c201e54b3963f.json` on integration-1's format. One line was taken out:
`mouth.killSwallowsBody`, false there, which branch `nose`'s reader does not have. The nursery's `--graft-nose` mode wrote it from that file and the nose's defaults. Its cell-type table appends the nose at its defaults. Nothing else differs from that file but the `configHash` line.
