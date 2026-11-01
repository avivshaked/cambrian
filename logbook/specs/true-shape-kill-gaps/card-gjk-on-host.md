# Kill gaps, second pass: every part pair and the card GJK on the host numbers

From `scratch/killgap/card_read.py` on the `-gaps2` windows (killgap `20fbeefa`). Rows are the card run kills the host census did not name; the controls are those it did.

fTrace disagrees with fCard on 0 rows
controls (host also named): card exit and card-touching: {('held', True): 27, ('tolerance', True): 90}
controls dSameInputs (apart, penetration>0): {(True, True): 117}

| window | t | killer->victim (parts) | kinds | surf | allMin (parts) | cardInputs | fCard apart, dist, pen | fTrace exit, passes | dSame apart, dist, pen |
|---|---|---|---|---|---|---|---|---|---|
| t6202-19500 | 19503 | 41286->41839 (0,0) | point/box | 0.5553 | 0.5553 (0,0) | committed | True, 0.8327, -0.5568 | tolerance, 1 | True, 0.8327, -0.5568 |
| t6202-19500 | 19505 | 39885->37800 (0,0) | box/box | 0.0138 | 0.0138 (0,0) | committed | True, 0.3255, -0.0139 | cap, 32 | True, 0.3255, -0.0139 |
| t6202-19500 | 19505.5 | 39885->39678 (0,0) | box/box | 0.0397 | 0.0397 (0,0) | committed | True, 0.2526, -0.0386 | tolerance, 0 | True, 0.2526, -0.0386 |
| t6202-21000 | 21002 | 51129->52147 (0,1) | box/point | 0.0074 | 0.0074 (0,1) | committed | True, 0.2413, -0.0079 | held, 2 | True, 0.2413, -0.0079 |
| t6202-21000 | 21003.5 | 42253->43528 (0,0) | box/box | 0.0129 | -0.0005 (1,0) | committed | True, 0.2562, -0.0125 | cap, 32 | True, 0.2562, -0.0125 |
| t6202-21000 | 21006.5 | 47195->42570 (0,0) | box/box | 0.5417 | 0.5417 (0,0) | committed | True, 0.7740, -0.5420 | tolerance, 0 | True, 0.7740, -0.5420 |
| t6202-21000 | 21008 | 39490->52415 (0,1) | box/point | 0.3686 | 0.0854 (0,0) | committed | True, 0.5985, -0.3690 | tolerance, 0 | True, 0.5985, -0.3690 |
| t6203-15000 | 15006.5 | 31017->35177 (0,0) | box/box | 0.0976 | 0.0976 (0,0) | committed | True, 0.3118, -0.0998 | tolerance, 0 | True, 0.3118, -0.0998 |
| t6203-21000 | 21007.5 | 50975->64460 (0,0) | box/box | 0.3919 | 0.3919 (0,0) | committed | True, 0.5490, -0.3921 | tolerance, 0 | True, 0.5490, -0.3921 |
| t6203-21000 | 21010 | 58463->59446 (0,0) | box/box | 0.0000 | 0.0000 (0,0) | committed | True, 0.1995, -0.0000 | tolerance, 2 | True, 0.1995, -0.0000 |
| t6203-22500 | 22500.5 | 66874->70081 (0,0) | box/box | 0.1822 | 0.1822 (0,0) | committed | True, 0.4122, -0.1815 | tolerance, 2 | True, 0.4122, -0.1815 |
