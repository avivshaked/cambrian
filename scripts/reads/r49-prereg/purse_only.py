"""Round 49 prereg helper (not for the record): of round 48's founders that bred within 1 s of
landing, how many would have cleared the adult gate from the floor's purse alone (200 J x bf),
with the endowment kept out of the reserve, as option (b) proposes.

Estimated, not measured. A founder's adult tissue and standing watts are not on its row. The row
carries bf (tissue at birth over adult tissue), rm (the margin, s) and endow (600 s of the newborn's
standing watts). Tissue is 500 J/m3 for every cell type, and upkeep is u W/m3, so the adult's
tissue is about 500 * (endow / 600) / (u * bf) and its standing watts about (endow / 600) / bf.
u is swept, since a body's mix of cell types is not on the row. The brood is the number of children
born at the first breeding instant. Growth goes first (World.Step: Grow before Reproduce), so the
gate is the adult's.

Usage: python purse_only.py"""
import collections
import glob
import json
import os

ROOT = os.path.join(os.path.dirname(__file__), '..', '..', 'runs')
PURSE = 200.0
FLOOR = 50.0
PER_TISSUE = 2.0
NEWBORN_RESERVE = 0.2
TISSUE = 500.0


def newest(arm):
    return sorted(glob.glob(os.path.join(ROOT, arm, '*', '')))[-1]


def overhead(child_tissue):
    return max(FLOOR, PER_TISSUE * child_tissue)


def check(r, brood, u, with_endow):
    bf, rm, endow = r['bf'], r['rm'], r.get('endow', 0.0)
    sw_n = endow / 600.0
    sw_a = sw_n / bf
    t_a = TISSUE * sw_a / u
    t_n = bf * t_a
    reserve = PURSE * bf + (endow if with_endow else 0.0)
    grow = t_a - t_n
    if reserve - 0.1 * t_n < grow:
        return False, None
    reserve -= grow
    bi_over_brood = bf / (1.0 - NEWBORN_RESERVE)
    share = bi_over_brood * t_a
    gate = brood * (share + overhead((1.0 - NEWBORN_RESERVE) * share)) + rm * sw_a
    return reserve >= gate, reserve - gate


def main():
    for arm in ['r48-s1', 'r48-s2', 'r48-s3']:
        d = newest(arm)
        births, kids = {}, collections.defaultdict(list)
        with open(os.path.join(d, 'lineage.jsonl'), encoding='utf-8') as f:
            for line in f:
                if not line.startswith('{"e":"b"'):
                    continue
                r = json.loads(line)
                births[r['id']] = r
                if r.get('p', -1) >= 0:
                    kids[r['p']].append(r['t'])
        quick = collections.defaultdict(list)
        for i, r in births.items():
            if r.get('k') != 'f' or 'endow' not in r:
                continue
            ks = sorted(kids.get(i, []))
            if not ks or ks[0] - r['t'] > 1.0:
                continue
            brood = sum(1 for t in ks if t == ks[0])
            quick[r.get('src')].append((r, brood))
        for src in sorted(quick):
            rows = quick[src]
            line = '%-7s %-8s bred within 1 s: %3d' % (arm, src, len(rows))
            for u in (2.5, 3.0, 4.0):
                model_a = sum(1 for r, b in rows if check(r, b, u, True)[0])
                purse = sum(1 for r, b in rows if check(r, b, u, False)[0])
                line += ' | u=%.1f: model says (a) %3d, purse alone %3d' % (u, model_a, purse)
            print(line)


if __name__ == '__main__':
    main()
