# Per-round reads

These scripts turn a round's arms into the numbers its entry quotes: one reader per round
for the most part, plus the older per-round scorers and the small shared analyses (parent
age, matter budget, matter profile, bids, late balance). Each takes arm names, resolves the
repository root from its own location and reads `runs/` from there, so
`python scripts/reads/r33-read.py r33-s1 r33-s2` works from any directory. They are kept
because entries cite them by name and a reading has to be reproducible; they are not general
instruments, since a reader written for one round assumes that round's columns.
