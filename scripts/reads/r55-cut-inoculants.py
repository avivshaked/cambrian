"""Round 55's inoculant set: every distinct genome the four brain-v2 nursery runs saved, one file each.

Sources per run: tank-exam-genomes.tsv (body<TAB>genome, the exam's genomes) and tank-best.jsonl (one
genome a line, the top genome of each generation; its body, born, score and episodes are in the same
line of tank-generations.jsonl under "top"). A genome's bytes are written exactly as the run wrote them.
A genome met twice (an elite carried as top for several generations, or an exam genome that was also
a top) is one file, and every place it was met is a row of the origin table.
Usage: python cut.py <out folder>"""
import hashlib, json, os, sys

R = r'D:\Projects\experiments\evolution-simulator'
RUNS = [
    ('pilot', 'pilot-deep-brain2', R + r'\scratch\nursery\pilot-deep-brain2'),
    ('rep2', 'brain2-rep2', R + r'\scratch\wt-nursery-general\runs\nursery\brain2-rep2'),
    ('nodrive', 'brain2-nodrive', R + r'\scratch\wt-nursery-general\runs\nursery\brain2-nodrive'),
    ('nocross', 'brain2-nocross', R + r'\scratch\wt-nursery-general\runs\nursery\brain2-nocross'),
]
OUT = sys.argv[1]


def lines(path):
    with open(path, 'rb') as f:
        data = f.read()
    rows = data.split(b'\n')
    if rows and rows[-1] == b'':
        rows.pop()
    return [r[:-1] if r.endswith(b'\r') else r for r in rows]


files = {}      # sha -> (name, bytes)
origins = []    # one row per place a genome was met
for short, run, folder in RUNS:
    rel = os.path.relpath(folder, R).replace('\\', '/')
    exam_rows = [json.loads(l) for l in lines(os.path.join(folder, 'tank-exam.jsonl'))]
    own = {r['genome']: r for r in exam_rows if r['brain'] == 'own'}
    for i, l in enumerate(lines(os.path.join(folder, 'tank-exam-genomes.tsv'))):
        body, genome = l.split(b'\t', 1)
        json.loads(genome)
        sha = hashlib.sha256(genome).hexdigest()
        name = f'{short}-exam-b{int(body)}.json'
        files.setdefault(sha, (name, genome))
        o = own[i]
        assert o['body'] == int(body), (run, i)
        origins.append(dict(sha=sha, run=run, kind='exam', body=int(body), born='', gen='',
                            source=f'{rel}/tank-exam-genomes.tsv', line=i + 1,
                            score=repr(o['net']), episodes=len(o['per']), scoreIs='exam, own brain, mean net J'))
    gens = [json.loads(l) for l in lines(os.path.join(folder, 'tank-generations.jsonl'))]
    bests = lines(os.path.join(folder, 'tank-best.jsonl'))
    assert len(gens) == len(bests), run
    for i, (g, genome) in enumerate(zip(gens, bests)):
        assert g['gen'] == i, (run, i)
        json.loads(genome)
        top = g['top']
        sha = hashlib.sha256(genome).hexdigest()
        name = f'{short}-best-g{i:03d}-b{top["body"]}.json'
        files.setdefault(sha, (name, genome))
        origins.append(dict(sha=sha, run=run, kind='best', body=top['body'], born=top['born'], gen=i,
                            source=f'{rel}/tank-best.jsonl', line=i + 1,
                            score=repr(top['score']), episodes=top['episodes'], scoreIs="search, the generation's top score"))

os.makedirs(OUT, exist_ok=True)
for sha, (name, genome) in files.items():
    with open(os.path.join(OUT, name), 'wb') as f:
        f.write(genome)

cols = ['file', 'sha256', 'run', 'kind', 'body', 'born', 'gen', 'source', 'line', 'score', 'episodes', 'scoreIs']
with open(os.path.join(OUT, 'origins.tsv'), 'w', encoding='utf-8', newline='\n') as f:
    f.write('\t'.join(cols) + '\n')
    for o in origins:
        o['file'] = files[o['sha']][0]
        o['sha256'] = o['sha']
        f.write('\t'.join(str(o[c]) for c in cols) + '\n')

# counts: distinct genomes per run and per body, by kind
print('run\tbody\texam\tdistinct bests\tgenerations as top')
for short, run, _ in RUNS:
    bodies = sorted({o['body'] for o in origins if o['run'] == run})
    for b in bodies:
        ex = {o['sha'] for o in origins if o['run'] == run and o['body'] == b and o['kind'] == 'exam'}
        be = {o['sha'] for o in origins if o['run'] == run and o['body'] == b and o['kind'] == 'best'}
        tops = sum(1 for o in origins if o['run'] == run and o['body'] == b and o['kind'] == 'best')
        print(f'{run}\t{b}\t{len(ex)}\t{len(be)}\t{tops}')
both = sum(1 for sha in files if {o['kind'] for o in origins if o['sha'] == sha} == {'exam', 'best'})
print(f'\nfiles {len(files)}; origin rows {len(origins)}; exam genomes also a top {both}')
print('bytes', sum(len(g) for _, g in files.values()))
