"""Fat step 0: the litter price P against s x W for leaves, and how far the bodies with tissue above adult tissue go."""
import csv, os, sys, statistics as st
for f in sys.argv[1:]:
    rows = list(csv.DictReader(open(f), delimiter='\t'))
    L = [r for r in rows if r['photosynthetic'] == '1' and r['absorptive'] == '0']
    P = [float(r['litter_price_j']) for r in L]
    W = [float(r['standing_w']) for r in L]
    E = [float(r['energy_j']) for r in L]
    T = [float(r['tissue_j']) for r in L]
    neg = [(float(r['tissue_j']) - float(r['adult_tissue_j'])) / float(r['adult_tissue_j'])
           for r in rows if float(r['adult_tissue_j']) < float(r['tissue_j'])]
    print(os.path.basename(f), 'leaves', len(L))
    print('  median P %.1f J, tissue %.1f J, W %.4f W, 1000 s x W %.1f J, energy %.1f J, median P/(1000 s x W) %.2f'
          % (st.median(P), st.median(T), st.median(W), 1000 * st.median(W), st.median(E),
             st.median([p / (1000 * w) for p, w in zip(P, W)])))
    print('  tissue above adult: n %d, largest overshoot %.2e of adult' % (len(neg), max(neg) if neg else 0))
