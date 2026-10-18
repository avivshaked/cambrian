"""Which of a kernel's uniform branches can split a warp, read from the PTX that ILGPU writes for it.

`bra.uni` promises the card that every thread of a warp takes the branch the same way; the card's behaviour is
undefined when the promise is false, and a warp that misses a loop's exit can spin for ever. ILGPU 1.5.3 at its
default level writes conditional branches as plain `bra`; at O2 it writes `@%p bra.uni` on predicates that
differ between threads, and the step hung on the card in its first block (card-audit-r57.md, 2026-10-08).

Reads one .ptx file and prints, for its kernel entry, how many predicated `bra.uni` it holds and how many test a
predicate that can differ within a warp; with a count, it lists that many of them by line. A register counts as
divergent if any of its definitions reads %tid or %laneid, loads from any memory but .param, or reads a
divergent register, to a fixpoint over every definition, since PTX reuses registers in loops. Conservative: a
load at an address every thread shares counts as divergent too, so a branch inside a section only one thread
runs is listed though it is harmless there.

The PTX comes from ILGPU's PTX backend without a card: `new PTXBackend(context, new CudaCapabilityContext(SM_89),
SM_89, ISA_80, null).Compile(EntryPointDescription.FromImplicitlyGroupedKernel(method), default)` on a context
with no CUDA device, at the level the engine uses, and the result's PTXAssembly written to a file.

    python -I scripts/reads/ptx-uniform.py <kernel.ptx> [<lines to list>]
"""
import re, sys
text = open(sys.argv[1]).read().splitlines()
start = next(i for i, l in enumerate(text) if '.entry' in l)
body = [(n + 1, l.strip()) for n, l in enumerate(text) if n >= start]
reg = re.compile(r'%[a-z]+[0-9]+')
defs = {}   # reg -> list of (line, op, sources)
branches = []
for n, l in body:
    g = re.match(r'^@(!?)(%p[0-9]+)\s+bra\.uni\s+(\S+);', l)
    if g:
        branches.append((n, g.group(2), g.group(3))); continue
    l2 = re.sub(r'^@!?%p[0-9]+\s+', '', l)
    m = re.match(r'^([a-z][\w.]*)\s+(.*);$', l2)
    if not m: continue
    op, args = m.group(1), m.group(2)
    if op.startswith(('st.', 'bra', 'bar', 'ret', 'call', 'membar', 'atom', 'red')): continue
    parts = [a.strip() for a in args.split(',')]
    dst = reg.findall(parts[0])
    if not dst: continue
    srcs = ','.join(parts[1:])
    tainted_src = ('%tid' in srcs or '%laneid' in srcs or
                   (op.startswith('ld.') and not op.startswith('ld.param')))
    for d in dst[:1]:
        defs.setdefault(d, []).append((n, tainted_src, reg.findall(srcs)))
    # setp with two predicate outputs (p|q)
    if '|' in parts[0]:
        for d in reg.findall(parts[0])[1:]:
            defs.setdefault(d, []).append((n, tainted_src, reg.findall(srcs)))
div = set()
changed = True
while changed:
    changed = False
    for r, ds in defs.items():
        if r in div: continue
        if any(t or any(s in div for s in ss) for _, t, ss in ds):
            div.add(r); changed = True
bad = [(n, p, t) for n, p, t in branches if p in div]
print(f'{sys.argv[1]}: predicated bra.uni {len(branches)}, on a predicate that can differ within a warp {len(bad)}')
if len(sys.argv) > 2:
    for n, p, t in bad[:int(sys.argv[2])]: print(f'  line {n}: {p} -> {t}')
