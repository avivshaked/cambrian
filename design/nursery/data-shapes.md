# What a nursery run folder holds

Working notes for the dashboard's designer brief and its build (2026-10-02). Read from the nursery's
code on `joint-drive` at `dc5dd6f` (`Tank.cs`, `Program.cs`) and from real runs under
`scratch/nursery/`. Ranges are measured on `pilot-deep-own16` (old brain, finished, 101 generations)
and `pilot-deep-brain2` (central brain with crossover, live, 76 generations at 10:30).

## The folder

A run is a folder with a `nursery.log`. Tank search runs hold:

| File | Written | Rows |
|---|---|---|
| `nursery.log` | one line per event, appended, never truncated (a reused folder holds several runs) | human-readable |
| `tank-generations.jsonl` | one row per generation, flushed per row, truncated at start | 0.4-0.8 KB |
| `tank-best.jsonl` | one genome per generation (the top), row index = generation | 1.7-10 KB |
| `tank-crossovers.jsonl` | with crossover only; one row per child that tried a crossover | 0.23 KB |
| `tank-exam-genomes.tsv` | once, as the exam starts: `body<TAB>genome JSON` | 1.5-9.4 KB |
| `tank-exam.jsonl` | during the exam, one brain's rows at a time | 0.4-0.7 KB |
| `pilot.log`, `pilot.err` | the launcher's redirected stdout and stderr; `pilot.log` = the log plus a hash note | |

A bench run holds `tank-bench.jsonl` and `tank-bench-genomes.tsv` instead, in the exam's shape. Older
arena runs hold `generations.jsonl`, `evaluations.jsonl`, `final.tsv` and others, and end with
`done in N min`.

Readers open with shared read and write (Python `open` is fine) and skip a last line that does not
parse.

## A generation row

Every field may be absent in older builds; `made`, `brain` and `envelope` are conditional.

| Field | Meaning | Range seen |
|---|---|---|
| `gen` | generation, from 0; a run of `--generations N` writes 0..N | |
| `seconds` | wall seconds since the search began (excludes the exam) | 343 s/gen (own16), 464 s/gen (brain2) |
| `landed` | members that landed in at least one episode | 96 |
| `netMean` | mean over members of this generation's mean net energy, J | 107-695 |
| `netBest` | best member's net this generation, J | 361-1040 |
| `alive` | share of episodes survived | 0.90-0.99 |
| `workWatts` | joints' mean mechanical work, W | 0.025-0.23 |
| `path` | mean path per 600 s episode, m | 55-84 |
| `fresh` | mean snow density at the body, J/m3 | 0.93-1.73 |
| `top.score` | the leader's running mean net over all its episodes, J | |
| `top.episodes` | episodes behind that mean | 16-80 |
| `top.body` | the leader's body (0-12; six kept in current runs) | |
| `top.born` | generation the leader was born | |
| `top.nodes`, `threshold`, `reaction`, `hold`, `release` | the leader's node count and first joint's drive genes | |
| `made.*` | crossover only: children, crossovers, transferred, added, refused{reason:n}, ends{sense,oscillator,loop,unresolved}, routeLength, unread, baseBetter, dMean, mutated, neuronsMean, neuronsMax | |
| `brain.*` | central brain only: hidden, hiddenMax, links, linksMax, topHidden, topLinks | hidden 0-10, links 82-135 |
| `envelope.*` | `--evolve-envelope` only: population mean {joints, threshold, reaction, hold, release} | |

Single generations are noisy: each generation's water comes from its own seed, shared across arms
with the same seed. The nursery session reads ten-generation means and compares arms generation by
generation.

## An exam row

One row per (genome, brain). For six bodies: 90 rows with 15 brains, 108 with 18.

| Field | Meaning |
|---|---|
| `brain` | `own`, `frozen`, `full`, `knockout`, `brainless`, then `knockout <channel>` per sense group, `constant <v>` per fixed stroke, `kinesis <mode> <bias> <gain>` |
| `genome` | index in the exam's pool (`tank-exam-genomes.tsv`) |
| `body` | body index |
| `net` | mean net energy over the draws, J |
| `per` | per-draw net energy, one per held-out draw (32), `null` where it did not land |
| `work`, `path` | means, W and m |

The verdict per body is paired: own minus another brain draw by draw, mean and standard error
(sd / sqrt n). D145's bar is a body's best constant beaten by two standard errors. Checked: computed
this way, `pilot-deep-own16` gives the plan's table exactly (body 0: own 437 J, best constant 0.6 at
416 J, +21 J se 18; body 3: own minus brainless +236 J se 46).

## The log

- First header line: `arena search: config hash <h>, tank ..., 600 s, 5 corpses of 40 J`.
- Second: `nursery: config <path> (hash <h> ...), tank ..., push, scent, drive sense, work cost, brain <Model>`.
  The path is relative to the launcher's working folder, usually `scratch/wt-joint`.
- Body lines, `kept bodies ...`, the `tank search:` settings line, and the `crossover ...` line.
- Per generation: `gen N: ...`, `brains: ...` (central), `made: ...` (crossover).
- `slow tank, seed S: ...` once a minute while an episode has run over 60 s of wall time (v10 on).
- `exam: N genomes under M brains, ..., K draws on held-out seeds (salt S), ...`, then one line per brain.
  A run is finished when all M lines are there; tank mode prints no `done`.

Not recorded in the folder: `--generations`, `--seed`, `--threads` and the command line. They are in
the launcher scripts (`scratch/nursery/launch-*.ps1`, `chain-*.ps1`), their logs (start time, pid),
and the live process's command line (`dotnet ... Evosim.Nursery.dll ... --out <folder>`).
