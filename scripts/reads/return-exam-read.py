"""The exam of a body search (D178, D179) beside the genomes it started from.

    python scripts/reads/return-exam-read.py <run folder>

Reads tank-exam-pool.jsonl (where each exam genome came from), tank-exam.jsonl (its score, the run's own reward over its
held-out episodes, and its eating, under each control) and inocula/r59-direction-tops/origins.tsv (the seeds' own-brain
eating in leaf-direction-sparse's exam, which drew the same held-out episodes: the same seed and exam salt). Prints, under
each control, the best and the mean score and eating; the five best genomes by score under their own brain, with their
eating and their eating with the smell knocked out; and how many genomes eat at least half the seeds' mean.

The seeds were examined in tanks of the ten tops' kind at the old prices, these in tanks of their own kind at D177's: the
held-out episodes are the same, the tank-mates and the prices are not.
"""
import json
import os
import statistics
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def main():
    run = sys.argv[1]
    pool = [json.loads(l) for l in open(os.path.join(run, 'tank-exam-pool.jsonl'), encoding='utf-8') if l.strip()]
    rows = [json.loads(l) for l in open(os.path.join(run, 'tank-exam.jsonl'), encoding='utf-8') if l.strip()]
    with open(os.path.join(ROOT, 'inocula', 'r59-direction-tops', 'origins.tsv'), encoding='utf-8') as f:
        lines = [l.rstrip('\r\n').split('\t') for l in f if l.strip()]
    seeds = [float(r[lines[0].index('eaten own')]) for r in lines[1:]]
    seed_mean = statistics.mean(seeds)

    brains = []
    for r in rows:
        if r['brain'] not in brains:
            brains.append(r['brain'])
    by = {b: {r['genome']: r for r in rows if r['brain'] == b} for b in brains}
    own, knock = brains[0], brains[-1]

    print('%s: %d genomes under %s' % (run, len(pool), ', '.join(brains)))
    print('seeds (the ten r59-direction-tops, own brain, the same held-out episodes): eaten mean %.1f J, %.1f to %.1f' %
          (seed_mean, min(seeds), max(seeds)))
    for b in brains:
        sc = [r['score'] for r in by[b].values()]
        ea = [r['eaten'] for r in by[b].values()]
        print('  %-40s score best %.3f mean %.3f | eaten best %.1f mean %.1f J' % (b, max(sc), statistics.mean(sc), max(ea), statistics.mean(ea)))
    best = sorted(by[own], key=lambda g: -by[own][g]['score'])[:5]
    for g in best:
        p = pool[g]
        print('  best: %-7s (tops %s%s) score %.3f, eaten %.1f J, knocked out %.1f J, %.0f%% of the seeds\' mean' % (
            p['id'], p['tops'], ', elite' if p['elite'] else '', by[own][g]['score'], by[own][g]['eaten'], by[knock][g]['eaten'],
            100 * by[own][g]['eaten'] / seed_mean))
    half = [g for g in by[own] if by[own][g]['eaten'] >= seed_mean / 2]
    print('  genomes eating at least half the seeds\' mean (%.1f J) under their own brain: %d of %d%s' % (
        seed_mean / 2, len(half), len(pool),
        '' if not half else '; best score among them %.3f' % max(by[own][g]['score'] for g in half)))


if __name__ == '__main__':
    main()
