"""The hue-dials sheet's stills lists: the 30 picks of hue-spread/picks.json plus six members of one family tree of
r62-h6202, one list per look.

    python -I make_lists.py

Looks: map1, map2, turn1, turn05, floor, turn1floor. Each list is hue-dials/<look>/list.json with its pictures under
hue-dials/<look>/png/. The genomes are read once per run (port/make_stills_list.py, into hue-dials/parts/) and shared by
every list, since a look differs in the environment the render is run under and not in the genome. The six family members
are added to their run's ids; an id already among the picks (43328) is kept once. Prints the item count of each list.
"""
import collections
import json
import os
import subprocess
import sys

R = 'D:/Projects/experiments/evolution-simulator'
S = R + '/scratch/skin-map'
PICKS = S + '/hue-spread/picks.json'
HERE = S + '/hue-dials'

FAMILY_RUN = 'runs/r62-h6202/2026-10-23-090508-c5c9f416'
FAMILY = [36766, 38847, 40020, 43308, 43309, 42495]

LOOKS = ['map1', 'map2', 'turn1', 'turn05', 'floor', 'turn1floor']


def segment(run):
    full = os.path.join(R, run)
    if os.path.exists(os.path.join(full, 'genomes.jsonl.gz')):
        return full
    subs = [d for d in os.listdir(full) if os.path.isdir(os.path.join(full, d))]
    assert len(subs) == 1, (run, subs)
    return os.path.join(full, subs[0])


def main():
    picks = json.load(open(PICKS, encoding='utf-8'))
    print(len(picks), 'picks')

    by_id = collections.defaultdict(list)
    for p in picks:
        by_id[p['id']].append(p['world'])
    shared = {i: ws for i, ws in by_id.items() if len(ws) > 1}
    print('ids shared by two or more picks:', shared or 'none')

    # (run, id) in the order of the picks, then the family tree's six.
    rows = []
    seen = set()
    for p in picks:
        key = (p['run'], p['id'])
        if key not in seen:
            seen.add(key)
            rows.append(key)
    for gid in FAMILY:
        key = (FAMILY_RUN, gid)
        if key in seen:
            print('family id', gid, 'is already among the picks')
            continue
        seen.add(key)
        rows.append(key)
    print(len(rows), 'rows')

    by_run = collections.OrderedDict()
    for run, gid in rows:
        by_run.setdefault(run, []).append(gid)

    base_items = {}
    for k, (run, ids) in enumerate(by_run.items()):
        part = os.path.join(HERE, 'parts', 'run-%d' % k)
        subprocess.run([sys.executable, '-I', os.path.join(R, 'scratch/skin-map/port/make_stills_list.py'), '--run', segment(run),
                        '--out', part, '--ids', ','.join(str(i) for i in ids), '--worker', '7'], check=True)
        for it in json.load(open(os.path.join(part, 'list.json'), encoding='utf-8'))['items']:
            gid = int(it['name'].split('-')[1])
            base_items[(run, gid)] = it

    for look in LOOKS:
        items = []
        png_dir = os.path.join(HERE, look, 'png').replace('\\', '/')
        os.makedirs(png_dir, exist_ok=True)
        for key in rows:
            it = dict(base_items[key])
            it['png_file'] = png_dir + '/' + os.path.basename(it['png_file'])
            items.append(it)
        out = os.path.join(HERE, look, 'list.json')
        with open(out, 'w', encoding='utf-8', newline='\n') as f:
            json.dump({'schema': 'create-story-video/stills-list/1', 'background': 'transparent', 'items': items}, f, indent=1)
        print(look, len(items), 'stills ->', out)


if __name__ == '__main__':
    main()
