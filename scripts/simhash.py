import hashlib, os, sys
root = os.path.abspath(sys.argv[1]); rels = {}
for d, _, fs in os.walk(root):
    for f in fs:
        if f.lower().endswith('.cs'):
            p = os.path.join(d, f)
            rels[os.path.relpath(p, root).replace(os.sep, '/')] = p
m = ''.join(r + '\n' + hashlib.sha256(open(rels[r], 'rb').read()).hexdigest() + '\n' for r in sorted(rels))
print(hashlib.sha256(m.encode()).hexdigest())
