"""The style checker STYLE.md §9 describes. It flags; it never edits.

    python scripts/style-check.py FILE [FILE ...]        one report per file
    python scripts/style-check.py --preserved OLD NEW    tokens in OLD that NEW lost

Counts the tells STYLE.md §5 lists: long sentences, em-dash density, bold lead-ins,
"not X but Y" closers, "which is why", rhetorical questions, code-heavy paragraphs,
intensifiers and machine-only words, deep headers. Fenced code blocks and tables are
skipped for the prose checks; pre-registration blocks are not special-cased, because the
checker cannot know where they start — read its report with that in mind.
"""
import re
import sys

INTENSIFIERS = [
    'exactly', 'precisely', 'quietly', 'simply', 'genuinely', 'honest', 'honestly',
    'the whole point', 'crucially', 'importantly', 'notably', 'in short', 'put differently',
    'arguably', 'it is worth', 'note that', 'the record',
]
MACHINE_WORDS = [
    'delve', 'tapestry', 'landscape', 'robust', 'leverage', 'underscores', 'highlights',
    'testament', 'nuanced', 'navigate', 'seamless', 'journey', 'elegant',
    'sits at the heart', 'a hard truth',
]
LONG_SENTENCE = 30


def prose_lines(text):
    """Yield (line_number, line) for lines that are prose: not fenced code, not tables."""
    fenced = False
    for i, line in enumerate(text.splitlines(), 1):
        s = line.strip()
        if s.startswith('```'):
            fenced = not fenced
            continue
        if fenced or s.startswith('|') or s.startswith('<'):
            continue
        yield i, line


def paragraphs(lines):
    """Group (n, line) prose lines into paragraphs: lists of (n, line)."""
    para = []
    for n, line in lines:
        if line.strip() == '':
            if para:
                yield para
                para = []
        else:
            para.append((n, line))
    if para:
        yield para


def sentences(text):
    text = re.sub(r'`[^`]*`', 'CODE', text)
    text = re.sub(r'\[([^\]]*)\]\([^)]*\)', r'\1', text)
    parts = re.split(r'(?<=[.!?])\s+(?=[A-Z"*(])', text)
    return [p.strip() for p in parts if p.strip()]


def report(path):
    text = open(path, encoding='utf-8-sig').read()
    lines = list(prose_lines(text))
    findings = []
    words_total = 0
    dashes_total = 0
    for para in paragraphs(lines):
        first_n = para[0][0]
        joined = ' '.join(l.strip() for _, l in para)
        if joined.startswith('#'):
            depth = len(joined) - len(joined.lstrip('#'))
            if depth > 3:
                findings.append((first_n, 'header deeper than three levels'))
            continue
        words = len(re.findall(r"[A-Za-z0-9'’]+", joined))
        words_total += words
        dashes = joined.count('—')
        dashes_total += dashes
        if dashes > 1:
            findings.append((first_n, f'{dashes} em dashes in one paragraph'))
        codes = len(re.findall(r'`[^`]+`', joined))
        if codes > 2:
            findings.append((first_n, f'{codes} code identifiers in one paragraph'))
        if re.match(r'^\s*(?:[-*]\s+|\d+\.\s+)?\*\*[^*]{1,60}[.:]\*\*', joined):
            findings.append((first_n, 'bold lead-in ending in a colon or stop'))
        for s in sentences(joined):
            n_words = len(re.findall(r"[A-Za-z0-9'’]+", s))
            if n_words > LONG_SENTENCE:
                findings.append((first_n, f'sentence of {n_words} words: "{s[:60]}..."'))
            if re.search(r'\bnot\b[^.;]{1,80}\bbut\b', s, re.I) or re.search(r',\s*not\s+[^,]{1,40}[.!]$', s):
                findings.append((first_n, f'"not X but Y" / "X, not Y": "{s[:60]}..."'))
            if re.search(r'\bwhich is (why|what|where|how)\b', s):
                findings.append((first_n, f'"which is why/what/where": "{s[:60]}..."'))
            if s.endswith('?') and not s.startswith('"'):
                findings.append((first_n, f'rhetorical question: "{s[:60]}..."'))
            is_date = re.match(r'^\*?\d{4}-\d{2}-\d{2}', s) is not None
            if n_words <= 4 and s.endswith('.') and not is_date and \
                    not re.search(r'\b(is|are|was|were|has|have|had|do|does|did)\b', s, re.I):
                findings.append((first_n, f'fragment as punchline: "{s}"'))
            low = s.lower()
            for w in INTENSIFIERS:
                if re.search(r'\b' + re.escape(w) + r'\b', low):
                    findings.append((first_n, f'intensifier "{w}": "{s[:60]}..."'))
            for w in MACHINE_WORDS:
                if re.search(r'\b' + re.escape(w) + r'\b', low):
                    findings.append((first_n, f'machine word "{w}": "{s[:60]}..."'))
    per_hundred = 100.0 * dashes_total / max(words_total, 1)
    print(f'{path}: {words_total} words, {dashes_total} em dashes ({per_hundred:.1f} per 100 words), {len(findings)} findings')
    for n, f in sorted(findings):
        print(f'  L{n}: {f}')


TOKEN_RES = [
    r'\b\d[\d,]*(?:\.\d+)?\b',                 # numbers
    r'\b[a-z]{1,4}\d{1,3}[a-z]*-[a-z]\d+\b',   # arm names like r26d-s4, det3-a
    r'\bD0\d\d\b',                             # decision numbers
    r'\b[0-9a-f]{7,64}\b',                     # hashes
    r'\b\d{4}-\d{2}-\d{2}\b',                  # dates
    r'`[^`]+`',                                # identifiers
    r'\[[^\]]+\]\([^)]+\)',                    # links
]


def tokens(text):
    out = set()
    for r in TOKEN_RES:
        out.update(m.group(0) for m in re.finditer(r, text))
    return out


def preserved(old, new):
    a = tokens(open(old, encoding='utf-8-sig').read())
    b = tokens(open(new, encoding='utf-8-sig').read())
    lost = sorted(a - b)
    print(f'{old} -> {new}: {len(a)} tokens in the original, {len(lost)} not found in the rewrite')
    for t in lost:
        print(f'  lost: {t}')
    return lost


if __name__ == '__main__':
    args = sys.argv[1:]
    if not args:
        sys.exit(__doc__)
    if args[0] == '--preserved':
        sys.exit(1 if preserved(args[1], args[2]) else 0)
    for p in args:
        report(p)
