"""D325's height read pooled: gripping plants and drifting plants of each arm, all seeds, after 3,000 s.

    python -I logbook/specs/plants-hold/height-read/pooled.py <census folder>
"""
import glob, json, os, re, sys

def median(xs):
    xs = sorted(xs); n = len(xs)
    return None if n == 0 else (xs[n // 2] if n % 2 else 0.5 * (xs[n // 2 - 1] + xs[n // 2]))

def main(folder):
    arms = {'plp': [], 'pldr': []}
    for path in sorted(glob.glob(os.path.join(folder, '*.jsonl'))):
        m = re.match(r'(plp|pldr)-(\d+)[ab]?\.jsonl$', os.path.basename(path))
        if not m:
            continue
        with open(path, encoding='utf-8') as f:
            arms[m.group(1)] += [json.loads(l) for l in f if l.strip()]
    print('arm   kind      bodies  parts  rise  >=0.5m  >=1m  top depth')
    for arm in ('plp', 'pldr'):
        late = [r for r in arms[arm] if r['t'] > 3000 and r['plant']]
        for kind, rows in (('gripping', [r for r in late if r['grips']]), ('drifting', [r for r in late if not r['grips']])):
            rise = [r['max_y'] - r['root_y'] for r in rows]
            span = [r['max_y'] - r['min_y'] for r in rows]
            print('%-5s %-9s %6d %6.1f %5.2f %6.0f%% %4.0f%% %9.1f   (span max-min %.2f)' % (
                arm, kind, len(rows), median([r['parts'] for r in rows]), median(rise),
                100.0 * sum(x >= 0.5 for x in rise) / len(rows), 100.0 * sum(x >= 1.0 for x in rise) / len(rows),
                median([-r['max_y'] for r in rows]), median(span)))
    return 0

if __name__ == '__main__':
    sys.exit(main(sys.argv[1]))
