"""Fat step 0: the share of bodies above D247's cap, P + max(R, 0) + s x W, at small s."""
import csv, os, sys
S = [0, 50, 100, 200, 300, 600]
for f in sys.argv[1:]:
    rows = list(csv.DictReader(open(f), delimiter='\t'))
    out = []
    for s in S:
        n = sum(1 for r in rows if float(r['energy_j']) > float(r['litter_price_j'])
                + max(0.0, float(r['adult_tissue_j']) - float(r['tissue_j'])) + s * float(r['standing_w']))
        out.append('s=%d %.1f%%' % (s, 100.0 * n / len(rows)))
    print(os.path.basename(f), len(rows), ', '.join(out))
