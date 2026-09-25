"""Brings format-8 genome files forward to format 9 (the ruling of 2026-09-24).

Adds "mode":"Lump","gestation":0.5 after the reproduction object's margin and changes the
format number; no other byte moves. Every format-8 genome is a lump breeder, and 0.5 is the
share every founder of the defaults carries. Usage:

    python scripts/convert-format8-to-9.py inocula            # dry run: lists files
    python scripts/convert-format8-to-9.py inocula --write    # rewrites in place

Walks the directory recursively and touches only files whose text carries "format":8.
"""
import os
import re
import sys

root = sys.argv[1] if len(sys.argv) > 1 else 'inocula'
write = '--write' in sys.argv
pattern = re.compile(r'("reproduction":\{[^{}]*"margin":[^,}]*)\}')

for d, _, files in os.walk(root):
    for f in files:
        if not f.endswith('.json'):
            continue
        p = os.path.join(d, f)
        with open(p, encoding='utf-8', newline='') as h:
            s = h.read()
        n8 = s.count('"format":8')
        if n8 == 0:
            continue
        t = s.replace('"format":8', '"format":9')
        t, n = pattern.subn(r'\1,"mode":"Lump","gestation":0.5}', t)
        if n != n8:
            print(f'SKIP {p}: {n8} format tags but {n} reproduction objects')
            continue
        print(('wrote ' if write else 'would write ') + p)
        if write:
            with open(p, 'w', encoding='utf-8', newline='') as h:
                h.write(t)
