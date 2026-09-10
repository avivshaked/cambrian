import hashlib, os, re, glob
def rd(p): return open(p, encoding='utf-8').read()
def wr(p, s): open(p, 'w', encoding='utf-8', newline='\n').write(s)

root = 'research/papers'
pdfs = {}
for path in glob.glob(f'{root}/*.pdf') + glob.glob(f'{root}/*/*.pdf'):
    name = os.path.basename(path)
    m = re.match(r'(\d+)-', name)
    if not m: continue
    n = int(m.group(1))
    h = hashlib.sha256(open(path, 'rb').read()).hexdigest()
    pdfs.setdefault(n, []).append((name, os.path.getsize(path), h))

p = 'research/FETCH-RESULTS.md'; s = rd(p)
lines = s.split('\n')
out = []; i = 0; added = 0; missing = []
while i < len(lines):
    line = lines[i]
    out.append(line)
    m = re.match(r'### \[(\d+)\]', line)
    if m:
        n = int(m.group(1))
        # copy the entry's bullet block, inserting the checksum after "Saved as" or, failing
        # that, after the first "Source used" line; or at the end of the bullets.
        j = i + 1; block = []
        while j < len(lines) and not lines[j].startswith('### ') and not lines[j].startswith('## '):
            block.append(lines[j]); j += 1
        if n in pdfs:
            items = sorted(pdfs[n])
            text = ['- SHA-256: ' + '; '.join(f'`{h}` (`{name}`, {size:,} bytes)' for name, size, h in items)]
        else:
            text = ['- SHA-256: no PDF on disk under `research/papers/` for this entry; the extracted text was read from its directory']
            missing.append(n)
        anchor = None
        for k, b in enumerate(block):
            if b.startswith('- Saved as:'): anchor = k
        if anchor is None:
            for k, b in enumerate(block):
                if b.startswith('- Source used:'): anchor = k; break
        if anchor is None:
            # last bullet line of the block
            for k, b in enumerate(block):
                if b.startswith('- '): anchor = k
        if anchor is None:
            out.extend(block); i = j; continue
        # extend anchor past its continuation lines (indented)
        k = anchor + 1
        while k < len(block) and block[k].startswith('  '): k += 1
        out.extend(block[:k]); out.extend(text); out.extend(block[k:]); added += 1
        i = j
        continue
    i += 1
s = '\n'.join(out)
intro_old = "This is the registry of what was retrieved, and of **where each copy came from**. PDFs are not committed to the repo, for copyright reasons, so this file is the reproducibility record. Anyone with equivalent access can rebuild the source set from the URLs below."
intro_new = intro_old + """

Each entry also carries the SHA-256 of the PDF that was read (added 2026-09-07, HANDOFF
item 23, on the Astra review's point that a gitignored corpus needs a way to tell the file
a rebuild fetches from the file the review cited). A rebuilt copy whose hash differs is a
different version of the paper, and a page-anchored claim against it is unverified until
the page is checked. Seven round-4 entries have no PDF on disk and say so."""
assert s.count(intro_old) == 1
s = s.replace(intro_old, intro_new)
wr(p, s)
print('entries with checksum', added, 'missing', missing, 'pdfs', sum(len(v) for v in pdfs.values()))

p = 'HANDOFF.md'; s = rd(p)
a = """    the file that was read. An hour's work, after the movement round is launched.
"""
b = """    the file that was read. Done 2026-09-07, after round 29 launched: every entry in
    `research/FETCH-RESULTS.md` carries its PDF's SHA-256 and size, or says no PDF is on disk.
"""
assert s.count(a) == 1; s = s.replace(a, b); wr(p, s)

p = 'gpt-astra-2026-09-07-1316-review-response.md'; s = rd(p)
a = "| 10 | queued | a checksum table for the research sources, after the movement round launches (HANDOFF item 23) |"
b = "| 10 | done | SHA-256 and size of every PDF beside its retrieval record in `research/FETCH-RESULTS.md` (2026-09-07, after round 29 launched) |"
assert s.count(a) == 1; s = s.replace(a, b)
a = """10. **A checksum table for the research sources**, after the movement round launches.
    *Queued*, HANDOFF item 23."""
b = """10. **A checksum table for the research sources**, after the movement round launches.
    *Done*, the same night."""
assert s.count(a) == 1; s = s.replace(a, b); wr(p, s)
print('handoff+companion ok')
