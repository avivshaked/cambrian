"""The time constants and grafted weights of a set of genomes, against the founders they came from (D293, D292).

For each genome of a pool or an exam's tank-exam-genomes.tsv: its hidden units, how many units (hidden and outputs) sit
above 0.1 s, the time constants of the two grafted memory units, and the mean size of the weights on the links the two
grafts made. The grafted units are the hidden units whose time constant is not 0.01 s in every founder; the bearing's
ports are the link sources the bearing graft added, found as the sources in the bearing pool that no source genome has.

Usage: python -I scripts/reads/kit-reads/time-constants.py <genomes.tsv> <founders.tsv> <bearing pool.tsv> <source.tsv>
"""
import json
import statistics
import sys


def genomes(path):
    out = []
    with open(path, encoding='utf-8') as f:
        for line in f:
            parts = line.rstrip('\r\n').split('\t')
            text = next((p for p in parts if p.startswith('{')), None)
            if text:
                out.append(json.loads(text)['brain'])
    return out


def sources(brains):
    return {link['from'] for b in brains for link in b['links']}


arm, founders, bearing, source = (genomes(p) for p in sys.argv[1:5])
memory = set.intersection(*({u['id'] for u in b['hidden'] if u.get('tau', 0.01) != 0.01} for b in founders))
ports = sources(bearing) - sources(source)
print(f'{len(arm)} genomes; grafted memory units {sorted(memory)}; bearing ports {len(ports)}')


def weights(b, keep):
    return [abs(link['w']) for link in b['links'] if keep(link)]


for name, brains in (('founders', founders), ('genomes', arm)):
    mem = [abs(w) for b in brains for w in weights(b, lambda l: l['from'] in memory or l['to'] in memory)]
    bear = [w for b in brains for w in weights(b, lambda l: l['from'] in ports)]
    print(f'{name}: mean |w| on memory links {statistics.mean(mem) if mem else 0:.3f} ({len(mem)}),'
          f' on bearing links {statistics.mean(bear) if bear else 0:.3f} ({len(bear)})')
for i, b in enumerate(arm):
    units = b['hidden'] + b['outputs']
    slow = sum(1 for u in units if u.get('tau', 0.01) > 0.1)
    taus = {u['id']: u.get('tau', 0.01) for u in b['hidden']}
    kept = ' '.join(f'{taus[m]:.3g}' if m in taus else 'gone' for m in sorted(memory))
    print(f'{i:3d} hidden {len(b["hidden"]):2d}  above 0.1 s {slow:2d}  grafted units {kept}')
