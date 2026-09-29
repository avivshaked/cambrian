# The proposed ageing clock (D128) against round 50's bodies, and the age at death in rounds 44 to 50.
import json, glob, math, random, sys
T, D = 3000.0, 300.0                      # the base and the doubling time (a tenth of the base)
b = math.log(2) / D
a = b * math.log(2) / (math.exp(b * T) - 1)   # so that the median age death is T
H = lambda c: (a / b) * (math.exp(b * c) - 1)  # cumulative hazard at clock c

def pct(p):  # the age rule alone, no pushes
    return math.log(1 + (b / a) * (-math.log(1 - p))) / b

def lineage(arm):
    run = sorted(glob.glob(f'runs/{arm}/2026*'))[-1]
    born, par, bf, died = {}, {}, {}, {}
    with open(f'{run}/lineage.jsonl', encoding='utf-8') as f:
        for line in f:
            if '"e":"b"' in line[:12]:
                r = json.loads(line); born[r['id']] = r['t']; par[r['id']] = r.get('p', -1); bf[r['id']] = r.get('bf', 0.4)
            elif '"e":"d"' in line[:12]:
                r = json.loads(line); died[r['id']] = r['t']
    return born, par, bf, died

def med(xs):
    xs = sorted(xs); return xs[len(xs) // 2] if xs else float('nan')

def death_age(births, U, m):
    # births: list of (age, f) sorted; the clock runs with time and jumps by m*f*D at each birth
    clock, age, acc = 0.0, 0.0, 0.0
    for (ba, f) in births + [(1e12, 0)]:
        seg = ba - age
        need = U - acc
        end = float('inf') if ba >= 1e12 or b * (clock + seg) > 700 else H(clock + seg) - H(clock)
        if end >= need:  # dies inside this segment
            c_death = math.log(math.exp(b * clock) + need * b / a) / b
            return age + (c_death - clock)
        acc += end; clock += seg; age = ba
        clock += m * f * D
    return float('inf')

mode = sys.argv[1]
if mode == 'curve':
    print('the age rule alone: base 3,000 s, doubling every 300 s')
    for p in (0.01, 0.1, 0.25, 0.5, 0.75, 0.9, 0.99):
        print(f'  {int(p*100):>2}% have died of age by {pct(p):6.0f} s')
elif mode == 'rounds':
    for rnd in ['r44', 'r45', 'r46', 'r47', 'r48', 'r49', 'r50']:
        allm, oldm, over = [], [], []
        for s in (1, 2, 3):
            born, par, bf, died = lineage(f'{rnd}-s{s}')
            ages = [died[i] - born[i] for i in died if i in born and died[i] >= 5000]
            allm += ages
        old = [x for x in allm if x >= 1000]
        print(f'{rnd}: deaths {len(allm):>7}, median age at death {med(allm):6.0f} s, of those past 1,000 s {med(old):6.0f} s, share past 3,000 s {sum(1 for x in allm if x > 3000)/max(len(allm),1):.2f}')
elif mode == 'clock':
    random.seed(51)
    for s in (1, 2, 3):
        born, par, bf, died = lineage(f'r50-s{s}')
        kids = {}
        for i, p in par.items():
            if p is not None and p >= 0 and p in born:
                kids.setdefault(p, []).append((born[i] - born[p], bf.get(i, 0.4)))
        cohort = [i for i in born if 5000 <= born[i] <= 15000]
        nk = [len(kids.get(i, [])) for i in cohort]
        breeders = [i for i in cohort if kids.get(i)]
        fs = [f for i in breeders for (_, f) in kids[i]]
        print(f'r50-s{s}: cohort {len(cohort)}, breeders {len(breeders)} ({100*len(breeders)/len(cohort):.0f}%), children per breeder median {med([len(kids[i]) for i in breeders])}, mean {sum(len(kids[i]) for i in breeders)/len(breeders):.1f}, child size at birth median {med(fs):.2f}')
        actual = [(died.get(i, 30000) - born[i]) for i in cohort]
        print(f'   actual (round 50, starvation under the wear): median age at death {med(actual):.0f} s; breeders {med([(died.get(i,30000)-born[i]) for i in breeders]):.0f} s')
        for m in (0, 0.5, 1, 2):
            new_all, new_br, age_only_br = [], [], []
            for i in cohort:
                bs = sorted(kids.get(i, []))
                U = random.expovariate(1.0)
                A = death_age(bs, U, m)
                s_act = died.get(i, 30000) - born[i]
                new_all.append(min(s_act, A))
                if bs:
                    new_br.append(min(s_act, A)); age_only_br.append(A)
            print(f'   m={m}: every body min(starved, aged) {med(new_all):6.0f} s; breeders {med(new_br):6.0f} s; breeders by age alone {med(age_only_br):6.0f} s')
