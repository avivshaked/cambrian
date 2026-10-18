"""Brings format-9 genome files forward to format 10 (D128, 2026-09-27).

Adds "lifespan":1 after the adult scale and changes the format number; no other byte moves.
Every format-9 genome was built to the world's own ageing base, and 1 is the lifespan every
founder of the defaults carries. Usage:

    python scripts/convert-format9-to-10.py inocula            # dry run: lists files
    python scripts/convert-format9-to-10.py inocula --write    # rewrites in place

Walks the directory recursively and touches only files whose text carries "format":9.
"""
import os
import re
import sys

root = sys.argv[1] if len(sys.argv) > 1 else 'inocula'
write = '--write' in sys.argv
pattern = re.compile(r'("adultScale":[^,}]*)(,)')

for d, _, files in os.walk(root):
    for f in files:
        if not f.endswith('.json'):
            continue
        p = os.path.join(d, f)
        with open(p, encoding='utf-8', newline='') as h:
            s = h.read()
        n9 = s.count('"format":9')
        if n9 == 0:
            continue
        t = s.replace('"format":9', '"format":10')
        t, n = pattern.subn(r'\1,"lifespan":1\2', t)
        if n != n9:
            print(f'SKIP {p}: {n9} format tags but {n} adult scales')
            continue
        print(('wrote ' if write else 'would write ') + p)
        if write:
            with open(p, 'w', encoding='utf-8', newline='') as h:
                h.write(t)
