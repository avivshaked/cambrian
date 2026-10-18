"""Brings format-10 genome files forward to format 11 (propose-joint-drive.md, 2026-09-30).

Adds the joint's drive genes after each node's power, at their defaults
("drive":{"threshold":0,"reaction":0.1,"hold":0.1,"release":0.1}), and changes the format
number; no other byte moves. The genes did not exist before format 11, and a format-10 body ran
the torque drive, which reads none of them, so the default is the only value they can be said to
have had. Usage:

    python scripts/convert-format10-to-11.py inocula            # dry run: lists files
    python scripts/convert-format10-to-11.py inocula --write    # rewrites in place

Walks the directory recursively and touches only files whose text carries "format":10.
"""
import os
import re
import sys

root = sys.argv[1] if len(sys.argv) > 1 else 'inocula'
write = '--write' in sys.argv
fmt = re.compile(r'("format"\s*:\s*)10\b')
power = re.compile(r'("power"\s*:\s*[-0-9.eE+]+)')
drive = ',"drive":{"threshold":0,"reaction":0.1,"hold":0.1,"release":0.1}'

for d, _, files in os.walk(root):
    for f in files:
        if not f.endswith('.json') and not f.endswith('.jsonl'):
            continue
        p = os.path.join(d, f)
        with open(p, encoding='utf-8', newline='') as h:
            s = h.read()
        n10 = len(fmt.findall(s))
        if n10 == 0:
            continue
        if '"drive"' in s:
            print(f'SKIP {p}: already carries drive genes')
            continue
        t = fmt.sub(r'\g<1>11', s)
        t, n = power.subn(lambda m: m.group(1) + drive, t)
        if n == 0:
            print(f'SKIP {p}: {n10} format tags and no node power')
            continue
        print(('wrote ' if write else 'would write ') + f'{p} ({n10} genomes, {n} nodes)')
        if write:
            with open(p, 'w', encoding='utf-8', newline='') as h:
                h.write(t)
