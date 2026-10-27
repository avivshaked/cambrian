# The scaling audit's measurements 1 to 4 on group 1

*2026-10-26, the speed agent. The audit is `logbook/specs/scaling-audit.md`, section 4. The table is
`scaling-audit-1-4.txt`, from `scripts/reads/scaling-audit-read.py` on `r62p-t3`, `r62p-k3`, `r62p-k3n` and the k = 1
base `r62cs-w500-6201`, in 3,000 s bins to 24,000 s. The base was recorded before the blow columns existed, so it has
no blow figures. Each reading is marked measured or inference.*

## 1. Kill conversion

| | Finishing share of mouth blows, early bins | Late bins |
|---|---|---|
| t3 | 0.45 to 0.49 | 0.50, then 0.39 |
| k3 | 0.48 to 0.57 | 0.73 to 0.74 |
| k3n | 0.45 to 0.60 | 0.77 to 0.80 |

These shares are measured. The audit's item 4 is that at k = 3 contact is sampled only on a metabolic step's last physics step, so
finishing a part would get harder. That predicts a lower finishing share at k = 3. The share is higher, so item 4 is
not what holds k3's grazers back (inference).

## 2. Encounters

| | Blows a grazer-second, 6,000 to 24,000 s | Grazer kills a grazer-second, 21,000 to 24,000 s |
|---|---|---|
| t3 | 0.028 to 0.038 | 0.0114 |
| k3 | 0.014 to 0.028 | 0.0194 |
| k3n | 0.015 to 0.024 | 0.0171 |
| w500 (k = 1) | not recorded | 0.0059 |

Both columns are measured. A k3 grazer lands fewer blows a second than a t3 grazer. Because more of them finish, its kills a second
end the run above t3's, and about three times the base's. So the pace factor's grazers do meet and kill more than
at k = 1. They do not meet more than the thrust arm's. `overlapPairsPerStep` is a count for each physics step, so it is
not compared across k.

## 3. Sinking

The y of the rigid bodies (`depthRigid`, mostly leaves) ends at −5.0 to −5.3 m in t3, k3 and k3n, against −4.2 m in
the base. The swimmers' (`depthJointed`) ends at −4.0 to −4.6 m, against −3.2 m (measured). Every arm sits deeper than the base,
the k = 1 thrust arm included. The audit predicted that k3's leaves would sink faster because gravity is not paced.
The table shows no sinking that k3 has and t3 lacks (inference).

## 4. Speed and work

`speedJointed` is 0.026 to 0.032 in k3 and k3n, 0.025 to 0.027 in the base, and 0.025 to 0.047 in t3 (measured).
Stroke work per jointed body ends at 0.039 W in k3, 0.029 W in the base and 0.35 W in t3 (measured).

If `speedJointed` is the solver's velocity, k3 swims at the base's speed in physical seconds, and so about three
times as far a world second. But the work per jointed body is only about 1.3 times the base's, not the 3 the build
predicts. The time base of these two columns was not checked (`src/Evosim.Farm/Row.cs`). `grazer-motion-read.py`,
which reads the poses in metres a world second, settles the speed. It waits for the next HOLD window.
