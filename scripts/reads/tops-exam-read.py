"""The read of a D172 exam (exam.pool "tops"): every distinct top and the last elites, under each control.

    python scripts/reads/tops-exam-read.py <run folder>

Reads tank-exam-pool.jsonl (where each exam genome came from) and tank-exam.jsonl (its eating under each control). Prints
the five best genomes on held-out seeds with their knockout, then the means of the tops by the generation they first
topped (0-9, 10-19, 20 on) and of the last elites, with the knockout's paired cost against the genome's own brain and its
standard error over the genomes in the block. The knockout is the last control, the genome's own brain the first.
"""
import json
import math
import sys


def main():
    run = sys.argv[1]
    pool = [json.loads(l) for l in open(run + '/tank-exam-pool.jsonl', encoding='utf-8') if l.strip()]
    rows = [json.loads(l) for l in open(run + '/tank-exam.jsonl', encoding='utf-8') if l.strip()]
    brains = []
    for r in rows:
        if r['brain'] not in brains:
            brains.append(r['brain'])
    eaten = {b: {r['genome']: r['eaten'] for r in rows if r['brain'] == b} for b in brains}
    own, knock = brains[0], brains[-1]
    print('%s: %d genomes under %s' % (run, len(pool), ', '.join(brains)))

    best = sorted(range(len(pool)), key=lambda i: -eaten[own][i])[:5]
    for i in best:
        p = pool[i]
        print('  best: %s (tops %s%s) own %.1f J, knockout %.1f J' % (p['id'], p['tops'], ', elite' if p['elite'] else '', eaten[own][i], eaten[knock][i]))

    def block(name, idx):
        if not idx:
            return
        d = [eaten[knock][i] - eaten[own][i] for i in idx]
        m = sum(d) / len(d)
        se = math.sqrt(sum((x - m) ** 2 for x in d) / (len(d) - 1) / len(d)) if len(d) > 1 else math.nan
        means = ', '.join('%s %.1f' % (b, sum(eaten[b][i] for i in idx) / len(idx)) for b in brains)
        print('  %s (%d): %s; knockout cost %.1f J (se %.1f)' % (name, len(idx), means, m, se))

    for lo, hi in ((0, 9), (10, 19), (20, 10 ** 6)):
        block('tops first from generations %d-%s' % (lo, hi if hi < 10 ** 6 else 'on'),
              [i for i, p in enumerate(pool) if p['tops'] and lo <= p['tops'][0] <= hi])
    block('last elites', [i for i, p in enumerate(pool) if p['elite']])


if __name__ == '__main__':
    main()
