"""Replay-identity check between two arms: first stats.jsonl sample whose fields differ.
Usage: python3 scripts/compare-det.py det0-a det0-b [--allow-partial] [--run NAME]

Exit codes (the Astra review's F4, logbook/specs/script-contracts-spec.md):
  0  every shared sample agrees (coverage need not match unless --allow-partial is absent);
  1  a difference was found on a shared sample (message unchanged from before this contract
     existed);
  2  a usage/data problem: an arm has no stats.jsonl, the two runs share no sample, a field
     is present on one side of a shared sample and not the other, or an arm has more than
     one run directory under runs/<arm>/ and --run does not name exactly one of them;
  3  the two runs' sample coverage differs (a sample time in one and not the other, or a
     different last time) and --allow-partial was not passed. The coverage line prints
     either way; --allow-partial only changes whether it is also the exit reason.
"""
import argparse
import glob
import json
import os
import sys


def fail(code, message):
    print(message, file=sys.stderr)
    sys.exit(code)


def find_run_dir(arm, run_name):
    """The single run directory's stats.jsonl under runs/<arm>/, or exit 2."""
    candidates = sorted(glob.glob(f'runs/{arm}/*/stats.jsonl'))
    if not candidates:
        fail(2, f'{arm}: no stats.jsonl under runs/{arm}/*/')

    if len(candidates) == 1:
        return candidates[0]

    if run_name:
        matches = [c for c in candidates
                   if os.path.basename(os.path.dirname(c)) == run_name]
        if len(matches) == 1:
            return matches[0]

    names = [os.path.basename(os.path.dirname(c)) for c in candidates]
    fail(2, f'{arm}: {len(candidates)} run directories under runs/{arm}/ -- pass '
            f'--run <name> to pick one: {", ".join(names)}')


def rows(path):
    with open(path, encoding='utf-8') as f:
        return [json.loads(line) for line in f if line.strip()]


def main():
    parser = argparse.ArgumentParser(
        description='Replay-identity check between two arms\' stats.jsonl.')
    parser.add_argument('a')
    parser.add_argument('b')
    parser.add_argument('--allow-partial', action='store_true',
                         help='unequal sample coverage does not fail the run; identity is '
                              'judged over the shared samples only')
    parser.add_argument('--run', metavar='NAME',
                         help='run directory name to use for whichever arm has more than '
                              'one under runs/<arm>/')
    args = parser.parse_args()

    path_a = find_run_dir(args.a, args.run)
    path_b = find_run_dir(args.b, args.run)

    A = {r['t']: r for r in rows(path_a)}
    B = {r['t']: r for r in rows(path_b)}

    ta = sorted(A)
    tb = sorted(B)
    shared = sorted(set(ta) & set(tb))

    if not shared:
        fail(2, f'{args.a} vs {args.b}: no shared samples (t) between the two runs')

    # A field present on one side of a shared sample and not the other means the two runs
    # are not comparable at all -- not a value difference, a schema mismatch.
    for t in shared:
        r, q = A[t], B[t]
        only_r = sorted(set(r) - set(q))
        only_q = sorted(set(q) - set(r))
        if only_r or only_q:
            missing = ', '.join(only_r + only_q)
            # only_r is present in r (a) and absent from q (b) -- so it is b that is
            # missing the field, and vice versa.
            side = args.b if only_r else args.a
            fail(2, f'{args.a} vs {args.b}: field(s) {missing} present at t={t} on one '
                    f'side only (missing from {side})')

    only_a = sorted(set(ta) - set(tb))
    only_b = sorted(set(tb) - set(ta))
    last_a, last_b = ta[-1], tb[-1]
    unequal = bool(only_a) or bool(only_b) or last_a != last_b
    if unequal:
        print(f'{args.a} vs {args.b}: unequal coverage -- {len(only_a)} sample(s) only in '
              f'{args.a} (last t={last_a}), {len(only_b)} only in {args.b} (last t={last_b})')
        if not args.allow_partial:
            return 3

    same = 0
    first = None
    for t in shared:
        r, q = A[t], B[t]
        d = {k: (r[k], q[k]) for k in r if r[k] != q[k]}
        if d:
            first = t
            prev_t = shared[same - 1] if same else 0
            print(f'{args.a} vs {args.b}: identical through t={prev_t}; first difference '
                  f'at t={t}, {len(d)} fields, e.g. '
                  + ', '.join(f'{k}={v}' for k, v in list(d.items())[:4]))
            break
        same += 1

    if first is not None:
        return 1

    print(f'{args.a} vs {args.b}: identical on all {same} shared samples '
          f'(to t={shared[-1]}); contacts/step at end {A[shared[-1]].get("contactPairsPerStep")}')
    return 0


if __name__ == '__main__':
    sys.exit(main())
