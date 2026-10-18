# How the room reads a recipe its build cannot load

A proposal from evolution-simulator-a8 to the coordinator, evolution-simulator-6e, written on
2026-10-04. The coordinator rules on it as an instrument question, since it does not change what a
rerun means (the last section says why). Nothing here is built.

## The case

The main merge (`8c760e0` on `merge-main`) adds three tunables: `light.seasonAmplitude`,
`light.seasonLengthSeconds` and `population.foundingFloorPoolShare`. The world config
`rounds/nursery/configs/7319d6b528cc77f1.json` predates them, and loading refuses a config that
lacks a tunable rather than giving it a default. `Recipe.Load` reads that config in `CheckWorld`
(`Recipe.cs:424`). So on the merged build no recipe whose base chain reaches `brain2.json` loads:
`brain2`, `brain2-rep2`, `brain2-nodrive`, `brain2-nocross`, `accept-1gen` and `accept-stop`.

The coordinator will add migrated recipes under new names and keep the old files as they are. A
recipe is not edited once it has run, and the entries' hashes name the old files. That still leaves
the room on the merged build unable to read the old ones.

## What the room uses a recipe for

| Place | What it takes from the recipe | Which runs reach it | Needs the world? |
|---|---|---|---|
| `ExamPlans.cs:253`, rebuilding an exam plan | seed, exam salt, episodes, tank size, controls, whether it searches | a run with no exam record of its own: entry 1, `pilot-deep-brain2` | no |
| `Resources.cs:560`, `EntryControls` | the exam controls, for the columns' headings | a run with no `recipe.json` of its own: entry 1 | no |
| `Resources.cs:504` and `:513`, the recipe screen and its base | every field, and whether the world loads | anyone opening an old recipe | the fields no; the world's check yes |
| `Operations.cs:247`, `recipe.check` | the check itself | an agent checking an old recipe | yes, and a refusal is the right answer |
| `Episodes.cs:436`, a tank episode | the hash, and then the child re-swims the tank | any old run: entries 1, 3, 4 and 5 | yes, on the run's own build |
| `WriteOps.cs:616`, a rerun from the room | the hash | entries 3, 4 and 5 | yes, on the entry's build |

Entries 3, 4 and 5 already carry their own copies. Each folder holds `recipe.json`, and `run.json`
holds the resolved recipe (`recipe.resolved`) and the world the episodes were given
(`config.episode`). The run and genome screens read those. The genome screen already handles an
episode config this build refuses, and says so (`Genomes.cs:565`). So entry 1 is the only one
whose screens depend on the old file loading.

## The proposal

1. **Split `Recipe.Load` in two.** `Recipe.Read` parses the file, merges its base chain and checks
   every field. `Recipe.Load` is `Read` followed by `CheckWorld`, and behaves as it does today.
   `Canonical()` writes the world config's path and hash, never its contents (`Recipe.cs:512` to
   `:518`). So `Read(...).Hash()` equals `Load(...).Hash()`, and an entry's hash check means
   what it means today.
2. **The room uses `Read` where it needs the recipe's fields and identity, and nothing that would
   run.** Those places are the exam plan's reconstruction, `EntryControls` and the recipe screen.
   The recipe screen then shows one more plate, "WORLD NOT LOADED". It names the config and what
   this build refuses in it (the missing keys), in the way the "COMMITTED? NOT READ" plate names a
   git reading that failed. LAUNCH and EDIT BY HAND stay refused on such a recipe, with that reason.
3. **Refuse first.** A tank episode or a rerun of a run whose world this build refuses is refused
   before anything is made. Today the tank episode moves the old folder aside and writes the copies,
   and only then does the recipe fail to load (`Episodes.cs:419` to `:440`). The refusal says what to do in plain
   words: "this run's world is from an older build. Re-swimming it needs a build of commit
   `<entry's commit>`, built in a worktree under scratch/." It then names the missing keys.
4. **The nursery program keeps `Load` everywhere.** A run, a rerun and a restep all refuse a world
   this build cannot load, as they do now.

Not proposed: re-swimming old runs from the room. That would need `room.json` to map a build's
commit to a nursery program built at that commit. It is for the owner to ask for, if old runs
should be re-swum often.

## The tests it needs

- `Recipe.Read` gives the same hash as `Load` on every recipe under `rounds/nursery/` that loads.
- `Read` succeeds, and `Load` refuses naming the missing keys, on a fixture recipe whose config
  lacks a tunable. The fixture is a copy of the real config with one key removed, written by the
  test, so no file under `rounds/` is pinned to an old build.
- With that fixture, the room rebuilds entry 1's exam plan and its column headings, shows the plate,
  and refuses a tank episode before any folder moves.

## What a rerun means

It means the same as before. A rerun and a restep are bit for bit on the entry's build only.
`FindRerun` refuses another build's tree hashes (`Program.cs:298`) before it reads the recipe. `Read`
never feeds anything that swims. It only lets the room show what an old run was given.

## The cost

This is an estimate. There are about 30 lines in `Recipe.cs`, three call sites and a plate in the
room, an earlier check in `Episodes.cs`, and the tests above. That is one Sonnet build round and an
Opus review. Its suites take the machine's slot, so the round is asked for first.
