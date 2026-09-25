# Shared readers for round 48's story, version 2. Read-only over runs/; nothing here writes
# anywhere but the caller's own output. Single-threaded, streaming where the file is large.
import json, os, glob, statistics
from collections import defaultdict, Counter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
RUNS = {
    'r48-s1': ('runs/r48-s1/2026-09-24-115115-e5a30c15', 30000.0),
    'r48-s2': ('runs/r48-s2/2026-09-24-115119-e5a30c15', 13690.0),
    'r48-s3': ('runs/r48-s3/2026-09-24-160650-e5a30c15', 30000.0),
}


def rows(path, upto=None):
    """JSONL rows, dropping a torn last line; stops past `upto` on the row's `t`."""
    with open(path, encoding='utf-8') as f:
        for line in f:
            if not line.endswith('\n'):
                break
            try:
                d = json.loads(line)
            except Exception:
                break
            if upto is not None and d.get('t', 0) > upto:
                break
            yield d


class Run:
    def __init__(self, arm, stats_fields=None):
        self.arm = arm
        rel, self.upto = RUNS[arm]
        self.rel = rel
        self.dir = os.path.join(ROOT, rel)
        self.B, self.D, self.cause = {}, {}, {}
        for d in rows(os.path.join(self.dir, 'lineage.jsonl'), self.upto):
            if d['e'] == 'b':
                self.B[d['id']] = d
            else:
                self.D[d['id']] = d['t']
                self.cause[d['id']] = d.get('c')
        self.order = sorted(self.B, key=lambda i: (self.B[i]['t'], i))
        # The guide's clade rule (scripts/guide.py build_clades): a child whose three flags
        # (abs, jnt, pho) equal its parent's joins the parent's clade; otherwise it founds one.
        self.croot = {}
        for i in sorted(self.B):
            b = self.B[i]
            p = b['p']
            if p == -1 or p not in self.B:
                self.croot[i] = i
                continue
            pb = self.B[p]
            same = (pb['abs'], pb['jnt'], pb['pho']) == (b['abs'], b['jnt'], b['pho'])
            self.croot[i] = self.croot[p] if same else i
        self.kids = defaultdict(list)
        for i, b in self.B.items():
            self.kids[b['p']].append(i)
        self.stats = {}
        for d in rows(os.path.join(self.dir, 'stats.jsonl'), self.upto):
            self.stats[d['t']] = d if stats_fields is None else {k: d.get(k) for k in stats_fields}
        gp = os.path.join(self.dir, 'guide', 'guide.json')
        self.guide_cards = []
        if os.path.exists(gp):
            self.guide_cards = json.load(open(gp, encoding='utf-8'))['cards']
        self.card_by_name = {c['name']: c for c in self.guide_cards}
        self.card_by_founder = {c['founder']: c for c in self.guide_cards}
        self.manifest = json.load(open(os.path.join(self.dir, 'run.json'), encoding='utf-8'))
        self.config = json.load(open(os.path.join(self.dir, 'config.json'), encoding='utf-8'))

    # ---------------------------------------------------------------- who is alive
    def alive(self, i, t):
        return i in self.B and self.B[i]['t'] <= t and self.D.get(i, 1e18) > t

    def descendants(self, r):
        out, st = [], [r]
        while st:
            x = st.pop()
            out.append(x)
            st.extend(self.kids.get(x, []))
        return out

    def founder_of(self, i):
        while self.B[i]['p'] != -1 and self.B[i]['p'] in self.B:
            i = self.B[i]['p']
        return i

    def series(self, key_of, keys, times):
        """Counts of living bodies whose key_of(id) is in `keys`, at each of `times` (ascending).
        One event sweep: a body counts at t when born at or before t and not dead at or before t."""
        keys = set(keys)
        ev = []
        for i, b in self.B.items():
            k = key_of(i)
            if k not in keys:
                continue
            ev.append((b['t'], 1, k))
            if i in self.D:
                ev.append((self.D[i], -1, k))
        ev.sort(key=lambda e: (e[0], e[1]))  # deaths (-1) before births at the same instant
        out = {k: [] for k in keys}
        cnt = Counter()
        j = 0
        for t in times:
            while j < len(ev) and ev[j][0] <= t:
                cnt[ev[j][2]] += ev[j][1]
                j += 1
            for k in keys:
                out[k].append(cnt[k])
        return out

    def peak(self, key_of, key):
        ev = []
        for i, b in self.B.items():
            if key_of(i) != key:
                continue
            ev.append((b['t'], 1))
            if i in self.D:
                ev.append((self.D[i], -1))
        ev.sort(key=lambda e: (e[0], e[1]))
        best, at, c = 0, None, 0
        for t, d in ev:
            c += d
            if c > best:
                best, at = c, t
        return best, at

    def clade_alive(self, r, t):
        return sum(1 for i in self.B if self.croot[i] == r and self.alive(i, t))

    def clade_last_death(self, r):
        mem = [i for i in self.B if self.croot[i] == r]
        if any(i not in self.D for i in mem):
            return None
        return max(self.D[i] for i in mem)

    # ---------------------------------------------------------------- other files
    def absrows(self, wanted_pairs=None, wanted_ids=None, upto=None):
        got = defaultdict(list)
        for d in rows(os.path.join(self.dir, 'absorptive.jsonl'), upto or self.upto):
            if wanted_pairs is not None and (d['id'], d['t']) in wanted_pairs:
                got[(d['id'], d['t'])].append(d)
            if wanted_ids is not None and d['id'] in wanted_ids:
                got[d['id']].append(d)
        return got

    def snapshot(self, s):
        p = os.path.join(self.dir, 'snapshots', '%09d.jsonl' % s)
        return list(rows(p))

    def snap_median(self, s, clade):
        inv, brood = [], []
        for d in self.snapshot(s):
            if self.croot.get(d.get('id')) == clade:
                inv.append(d['reproduction']['investment'])
                brood.append(d['reproduction']['brood'])
        return (len(inv), statistics.median(inv) if inv else None, statistics.median(brood) if brood else None)


def med(xs):
    return statistics.median(xs) if xs else None
