"""The read of a tops bench (D172): each generation's saved top, swum eight to a tank on held-out seeds, under its own
brain, no brain and the leaf smell knocked out.

    python scripts/reads/tops-bench-read.py <bench run folder> <pool record tops-<hash>.json>

Prints a line a generation: the top's search score, its eaten under each brain, and the knockout's cost against its own
brain; then the best tops on held-out seeds, and the mean of each brain over generations 0-9, 10-19 and 20-30, with the
smell's paired cost and its standard error over the tops in the block.
"""
import json
import math
import sys


def main():
    run, record = sys.argv[1], sys.argv[2]
    rows = [json.loads(l) for l in open(run + '/tank-bench.jsonl', encoding='utf-8') if l.strip()]
    tops = json.load(open(record, encoding='utf-8'))['tops']
    brains = []
    for r in rows:
        if r['brain'] not in brains:
            brains.append(r['brain'])
    eaten = {b: {r['genome']: r['eaten'] for r in rows if r['brain'] == b} for b in brains}
    own, knock = brains[0], brains[-1]
    print('gen  search   ' + '  '.join('%10s' % b[:10] for b in brains) + '   smell cost   same as')
    for t in tops:
        i = t['line']
        cells = '  '.join('%10.1f' % eaten[b].get(i, math.nan) for b in brains)
        same = '' if t['sameAsLine'] == i else 'gen %d' % tops[t['sameAsLine']]['gen']
        print('%3d  %6.1f   %s   %10.1f   %s' % (t['gen'], t['score'], cells, eaten[knock][i] - eaten[own][i], same))

    best = sorted(range(len(tops)), key=lambda i: -eaten[own][i])[:5]
    print('best on held-out seeds (own): ' + ', '.join('gen %d %.1f J' % (tops[i]['gen'], eaten[own][i]) for i in best))
    for lo, hi in ((0, 9), (10, 19), (20, 30)):
        idx = [t['line'] for t in tops if lo <= t['gen'] <= hi]
        means = ', '.join('%s %.1f' % (b, sum(eaten[b][i] for i in idx) / len(idx)) for b in brains)
        d = [eaten[knock][i] - eaten[own][i] for i in idx]
        m = sum(d) / len(d)
        se = math.sqrt(sum((x - m) ** 2 for x in d) / (len(d) - 1) / len(d)) if len(d) > 1 else math.nan
        print('generations %d-%d (%d tops): %s; smell cost %.1f J (se %.1f)' % (lo, hi, len(idx), means, m, se))


if __name__ == '__main__':
    main()
