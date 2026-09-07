"""Where two runs of one seed stop being the same run — scratch/digest-spec.md.

Reads runs/<arm>/*/digest.jsonl from two arms and prints the last step whose state hash
agrees and the first that does not. If both arms also dumped bodies (digest-bodies.jsonl)
at a step they share, prints the first creature id, in file order, whose row differs, and
every component of it that differs.

Usage: python3 scripts/digest-diff.py det3-a det3-b

Exits 0 when every shared digest step agrees, 1 when any differs, 2 on a usage error -- so a
launcher or a test can gate on it rather than read its prose (the Astra review, 2026-09-07).
"""
import glob
import json
import sys

# The thirteen numbers a part is recorded as, in the order Ecosystem.ReadPartState writes
# them: position, rotation (a quaternion, so four), linear velocity, angular velocity.
COMPONENTS = ['pos.x', 'pos.y', 'pos.z',
              'rot.x', 'rot.y', 'rot.z', 'rot.w',
              'vel.x', 'vel.y', 'vel.z',
              'ang.x', 'ang.y', 'ang.z']


def one_file(arm, name):
    """The single <name> under runs/<arm>/<run>/, or None."""
    found = glob.glob(f'runs/{arm}/*/{name}')
    if len(found) > 1:
        sys.exit(f'{arm}: {len(found)} {name} files under runs/{arm} — which run?')
    return found[0] if found else None


def rows(path):
    with open(path, encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if line:
                yield json.loads(line)


def digests(arm):
    path = one_file(arm, 'digest.jsonl')
    if not path:
        sys.exit(f'{arm}: no digest.jsonl — was it launched with -DigestEvery?')
    return path, {r['step']: r for r in rows(path)}


def bodies(arm):
    """digest-bodies.jsonl as {step: [row, ...]}, keeping file order within a step."""
    path = one_file(arm, 'digest-bodies.jsonl')
    if not path:
        return None, {}

    by_step = {}
    for r in rows(path):
        by_step.setdefault(r['step'], []).append(r)
    return path, by_step


def part_diff(label, a, b, out):
    """Component-by-component, comparing the printed text so a diff is a diff to the bit."""
    if a == b:
        return
    if len(a) != len(b):
        out.append(f'      {label}: {len(a)} components against {len(b)}')
        return
    for i, (x, y) in enumerate(zip(a, b)):
        if x != y:
            name = COMPONENTS[i] if i < len(COMPONENTS) else f'[{i}]'
            out.append(f'      {label} {name}: {x!r} vs {y!r}')


def compare_creature(ra, rb):
    """Every differing value in one creature's row, as printable lines."""
    out = []
    part_diff('root', ra['root'], rb['root'], out)

    la, lb = ra['links'], rb['links']
    if len(la) != len(lb):
        out.append(f'      links: {len(la)} against {len(lb)}')
    else:
        for i, (x, y) in enumerate(zip(la, lb)):
            part_diff(f'link {i + 1}', x, y, out)

    for field in ('sleeping', 'contacts'):
        if ra.get(field) != rb.get(field):
            out.append(f'      {field}: {ra.get(field)!r} vs {rb.get(field)!r}')

    return out


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 2

    a, b = sys.argv[1], sys.argv[2]
    pa, A = digests(a)
    pb, B = digests(b)

    print(f'{a}: {pa} ({len(A)} rows)')
    print(f'{b}: {pb} ({len(B)} rows)')

    shared = sorted(set(A) & set(B))
    if not shared:
        sys.exit('no step is digested in both runs')

    last_same = None
    first_diff = None
    for step in shared:
        if A[step]['hash'] == B[step]['hash']:
            last_same = step
        else:
            first_diff = step
            break

    exit_code = 0
    if first_diff is None:
        print(f'{a} vs {b}: identical over all {len(shared)} shared digest steps '
              f'(to step {shared[-1]}, t={A[shared[-1]]["t"]} s)')
    else:
        print(f'{a} vs {b}: identical through step {last_same} '
              f'(t={A[last_same]["t"] if last_same is not None else 0} s); '
              f'first differing step {first_diff}, t={A[first_diff]["t"]} s')
        for arm, R in ((a, A), (b, B)):
            r = R[first_diff]
            print(f'    {arm}: hash {r["hash"]}, bodies {r["bodies"]}, first id {r["first"]}')
        exit_code = 1

    # The per-body dump, at whatever steps both runs happen to carry one.
    _, DA = bodies(a)
    _, DB = bodies(b)
    common = sorted(set(DA) & set(DB))

    if not common:
        print('no step has a digest-bodies.jsonl row in both runs '
              '(relaunch both with -DigestDump at a step they share)')
        return

    for step in common:
        ra, rb = DA[step], DB[step]
        print(f'bodies at step {step}: {len(ra)} rows in {a}, {len(rb)} in {b}')

        # In file order, which is World.Living order at that step. An id present in one and
        # not the other is itself the difference, so ids are compared before values.
        ids_a = [r['id'] for r in ra]
        ids_b = [r['id'] for r in rb]
        if ids_a != ids_b:
            for i, (x, y) in enumerate(zip(ids_a, ids_b)):
                if x != y:
                    print(f'    first order/membership difference at row {i}: '
                          f'id {x} in {a}, {y} in {b}')
                    break
            else:
                print(f'    same ids to row {min(len(ids_a), len(ids_b)) - 1}, '
                      f'then one run has more')

        found = False
        for i, (x, y) in enumerate(zip(ra, rb)):
            if x['id'] != y['id']:
                continue
            lines = compare_creature(x, y)
            if lines:
                print(f'    first differing creature: id {x["id"]} (row {i}), '
                      f'{len(lines)} differing values')
                for line in lines:
                    print(line)
                found = True
                break

        if not found and ids_a == ids_b:
            print('    every creature identical at this step')

    return exit_code


if __name__ == '__main__':
    sys.exit(main())
