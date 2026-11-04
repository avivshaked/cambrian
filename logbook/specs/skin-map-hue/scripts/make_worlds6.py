"""Writes worlds6.cmd: the skin vectors of the pooled fit's six other reference worlds, so that all ten (the fitting set's 2,131
animals) have vectors for the hue histogram (the coordinator's ask of 2026-10-30). One after another, on logical 30."""
import json

j = json.load(open('scratch/skin-map/pooled-24000/projection.json'))
win = lambda p: p.replace('/', '\\')
refs = ' '.join('--ref %s@24000' % win(r['run']) for r in j['reference_runs'])
lines = ['@echo off', 'rem Skin vectors of the six other reference worlds on the pooled fit (c8, 2026-10-30), logical 30, BelowNormal.',
         'cd /d D:\\Projects\\experiments\\evolution-simulator']
for w in ('r63b-a-6202', 'r63b-a-6203', 'r63b-b-6202', 'r63b-c-6202', 'r62cf-6203', 'r62cf-6204'):
    seg = win([r['run'] for r in j['reference_runs'] if w in r['run']][0])
    lines.append('python -I scratch\\skin-map\\skin_features.py %s --at 24000 --out scratch\\skin-map\\pooled-%s %s '
                 '--max-cousins 50 --max-siblings 50 --strangers 200 --check-pairs 500 --bootstrap 10 --no-plot '
                 '> scratch\\skin-map\\pooled-%s.log 2>&1' % (seg, w, refs, w))
lines += ['echo == end > scratch\\skin-map\\worlds6.done', 'exit']
with open('scratch/skin-map/worlds6.cmd', 'w', newline='\r\n') as f:
    f.write('\n'.join(lines) + '\n')
print('\n'.join(lines[3:4])[:300])
