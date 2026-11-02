"""Three logs from one MOUTH_TRUE = 2 film log (c8, 2026-11-02, D275's open point 2): each ate-contact and kill-contact line's
"(gap/radius X)" set to the drawn ratio (as written), the farm's ratio, or the farm's measure on the line's own pair, and the level 2
note removed, so ate_table2.py and table_summary.py read each measure unchanged. A ratio that could not be measured, or no part within
reach, is written 999 (far). Prints how many lines carried the note and how many of each variant were not measured.

    python -I -B split_variants.py <film log> <out-dir>
"""
import os, re, sys

log, out = sys.argv[1], sys.argv[2]
NOTE = re.compile(r" \(the farm's measure, D275: (gap/radius (\S+), (?:mouth part \d+ to part \d+|no part within the mouth's reach)|not measured)"
                  r"; the row's pair on the farm's measure: (gap/radius (\S+)|not measured)\)")
RATIO = re.compile(r'\(gap/radius (\S+)\)')


def num(text):
    if text is None:
        return '999'
    if text in ('Infinity', '∞', 'NaN'):
        return '999'
    return text


names = ('drawn', 'farm', 'pair')
files = {n: open(os.path.join(out, 'split-%s.log' % n), 'w', encoding='utf-8') for n in names}
noted = 0
unmeasured = {'farm': 0, 'pair': 0}
for line in open(log, encoding='utf-8', errors='replace'):
    m = NOTE.search(line)
    if not m or ('ate contact: ' not in line and 'kill contact: ' not in line):
        for n in names:
            files[n].write(line)
        continue
    noted += 1
    farm = m.group(2) if m.group(2) is not None else None
    pair = m.group(4) if m.group(4) is not None else None
    if farm is None or num(farm) == '999': unmeasured['farm'] += 1
    if pair is None or num(pair) == '999': unmeasured['pair'] += 1
    bare = line[:m.start()] + line[m.end():]
    files['drawn'].write(bare)
    files['farm'].write(RATIO.sub('(gap/radius %s)' % num(farm), bare, count=1))
    files['pair'].write(RATIO.sub('(gap/radius %s)' % num(pair), bare, count=1))
for f in files.values():
    f.close()
print('lines with the level 2 note', noted, '; far or not measured: farm', unmeasured['farm'], ', row pair', unmeasured['pair'])
