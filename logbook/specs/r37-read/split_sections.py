# -*- coding: utf-8 -*-
"""Cut report-readings.tsv into the files the read brief names."""
import os

HERE = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(HERE, 'report-readings.tsv'), encoding='utf-8').read()
blocks = []
cur = []
for line in src.splitlines():
    if line.startswith('## ') and cur:
        blocks.append(cur)
        cur = []
    cur.append(line)
blocks.append(cur)


def grab(prefix):
    return ['\n'.join(b) for b in blocks if b[0].startswith(prefix)]


open(os.path.join(HERE, 'exposure.tsv'), 'w', encoding='utf-8').write(
    '\n\n'.join(grab('## M6')) + '\n')
open(os.path.join(HERE, 'cycle.tsv'), 'w', encoding='utf-8').write(
    '\n\n'.join(grab('## Cycle')) + '\n')
print('exposure.tsv, cycle.tsv written')
