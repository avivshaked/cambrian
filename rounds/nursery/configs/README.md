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
