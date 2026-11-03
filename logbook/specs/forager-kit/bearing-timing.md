# What the mouth's bearing costs in wall time (D292), 2026-11-03

The bearing (D292) tells a mouth which way the nearest leaf and the nearest animal lie. It is searched for each mouth
when it is held, every physics step at its overpowered start and every 50 steps as the first dial back. The spec's
table left its cost untimed, and this read measures it.

Round 65's control world was founded fresh on seed 6203 and run for 1,000 s, three times in turn. Each run used the
forager kit's build at `30258f19`, at 8 threads on logical 16-23. Nothing else ran on the machine but a two-second
heat look on logical 24-25 during the second run. Each number is the run's last stats row, and the script is
`scratch/forager-kit/bearing-timing.ps1`.

| Run | Bearing | Wall time for 1,000 s | The bearing's own timer | Bodies alive at 1,000 s |
|---|---|---|---|---|
| `bt-off` | off | 711.2 s | none | 1,762 |
| `bt-hold1` | on, held every step | 742.1 s | 22.7 s | 1,806 |
| `bt-hold50` | on, held every 50 steps | 726.9 s | 0.9 s | 1,806 |

Held every step, the search takes 3.1 % of the wall time. Held every 50 steps it takes 0.1 %. Every run kept above real
time, at 1.35 to 1.41 times.

What it means (inference): the overpowered start is cheap enough to screen as it stands. The two runs with the switch on
end with the same count of bodies, so they probably followed one world. That would be because no body wired the bearing
within 1,000 s, and their difference, 15 s, is then the search's cost within the timing noise. The run with the switch
off is a different world, since the sense joins the pool that mutation draws from. So its 31 s gap to `bt-hold1` mixes
the search's cost with a different history, and the timer column is the cleaner measure.
