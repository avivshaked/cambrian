"""The recorded snow at the test seconds: column density (J over the column's water depth, J/m3)
and the floor cell's stock (J), as percentiles over the live columns, to set the look's fixed
references (brief-2.md items 4 and 5)."""
import glob
import os
import numpy as np

R = r'D:\Projects\experiments\evolution-simulator\runs'
CASES = [('r57-s2', [27000, 30000, 36000, 45000]), ('r56-s2', [29000, 30000])]

for arm, seconds in CASES:
    run = sorted(glob.glob(os.path.join(R, arm, '*')))[-1]
    f = os.path.join(run, 'fields')
    bed = np.fromfile(os.path.join(f, 'bed.f32'), dtype='<f4')
    depth = -bed
    live = depth > 0.05
    print(arm, 'columns', bed.size, 'live', live.sum(), 'depth p5/50/95', np.percentile(depth[live], [5, 50, 95]).round(2))
    for s in seconds:
        cpath = os.path.join(f, '%09d.snow-columns.f32' % s)
        fpath = os.path.join(f, '%09d.snow-floor.f32' % s)
        if not os.path.exists(cpath):
            print(' ', s, 'absent')
            continue
        col = np.fromfile(cpath, dtype='<f4')
        flo = np.fromfile(fpath, dtype='<f4')
        dens = col[live] / depth[live]
        print('  %6d density p1/5/50/95/99 %s  mean %.3f | floor p5/50/95/99 %s mean %.3f | total %.0f' % (
            s, np.percentile(dens, [1, 5, 50, 95, 99]).round(3), dens.mean(),
            np.percentile(flo[live], [5, 50, 95, 99]).round(3), flo[live].mean(), col.sum()))
