"""Does unity-wN/Assets/Evosim carry the same .cs bytes as unity/Assets/Evosim?

The hash check CLAUDE.md's new-worker gotcha demands, done on the bytes rather than on a
digest of them, so a mismatch names the file.
"""
import hashlib
import os
import sys


def digest(root):
    out = {}
    for dirpath, _dirnames, filenames in os.walk(root):
        for name in filenames:
            if not name.endswith('.cs'):
                continue
            path = os.path.join(dirpath, name)
            rel = os.path.relpath(path, root).replace(os.sep, '/')
            out[rel] = hashlib.sha256(open(path, 'rb').read()).hexdigest()
    return out


def main():
    worker = sys.argv[1] if len(sys.argv) > 1 else 'unity-w6'
    a = digest('unity/Assets/Evosim')
    b = digest(worker + '/Assets/Evosim')

    only_a = sorted(set(a) - set(b))
    only_b = sorted(set(b) - set(a))
    differ = sorted(k for k in a if k in b and a[k] != b[k])

    print('unity/: %d .cs   %s: %d .cs' % (len(a), worker, len(b)))
    print('only in unity/: %s' % (only_a or 'none'))
    print('only in %s: %s' % (worker, only_b or 'none'))
    print('differing bytes: %s' % (differ or 'none'))

    # The same digest EvolutionRun.HashSourceTree is a digest of, in spirit: every .cs under
    # Assets/Evosim, sorted by path. Not the authority (CLAUDE.md: scripts/simhash.py is not
    # the hash) — the authority is what run-arm.ps1 prints — but it does say whether the two
    # trees are the same tree.
    for label, tree in (('unity/', a), (worker, b)):
        roll = hashlib.sha256()
        for key in sorted(tree):
            roll.update(key.encode('utf-8'))
            roll.update(tree[key].encode('ascii'))
        print('%s rolling digest %s' % (label, roll.hexdigest()[:16]))

    return 0 if not (only_a or only_b or differ) else 1


if __name__ == '__main__':
    sys.exit(main())
