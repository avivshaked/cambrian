import re, os, glob
KEY = re.compile(r'Joule|Matter|Energy|Upkeep|Excret|Detritus|Nutrient|Tissue|Audit|LightIncome|FoodIncome|Reserve|StandingWatts|Deposit|Take\(')
ASSERT = re.compile(r'Assert\.')
d = 'src/Evosim.Core.Tests'
total = 0
for path in sorted(glob.glob(os.path.join(d, '*.cs'))):
    src = open(path, encoding='utf-8-sig').read()
    lines = src.split('\n')
    cls = None
    hits = []
    i = 0
    # find classes
    classpos = [(m.start(), m.group(1)) for m in re.finditer(r'class\s+([A-Za-z0-9_]+)', src)]
    def classof(pos):
        name = None
        for p, n in classpos:
            if p <= pos: name = n
            else: break
        return name
    for m in re.finditer(r'\[(Fact|Theory)[^\]]*\]((?:\s*\[[^\]]*\])*)\s*(?:public|private|internal)[^\n]*?\s([A-Za-z0-9_]+)\s*\(', src):
        start = m.end()
        # body: brace match
        b = src.find('{', start)
        if b < 0: continue
        depth = 0; j = b
        while j < len(src):
            if src[j] == '{': depth += 1
            elif src[j] == '}':
                depth -= 1
                if depth == 0: break
            j += 1
        body = src[b:j]
        if KEY.search(body) and ASSERT.search(body):
            hits.append((classof(m.start()), m.group(3)))
    if hits:
        total += len(hits)
        print('### ' + os.path.basename(path) + '  (' + str(len(hits)) + ')')
        bycls = {}
        for c, n in hits: bycls.setdefault(c, []).append(n)
        for c in bycls:
            print('  ' + str(c) + ': ' + ', '.join(bycls[c]))
print('TOTAL ' + str(total))
