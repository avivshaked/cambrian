"""Builds the stills list for the theatre's solo-still entry (Evosim.Theatre.EditorTools.TheatreSoloStill.Run).

    python -I make_stills_list.py --run <run dir holding genomes.jsonl.gz and config.json> --out <folder under scratch/>
        --ids 5784,6097,... [--size 700]

Writes <out>/genomes/<id>.json (one genome row each, read from genomes.jsonl.gz), <out>/list.json and <out>/command.ps1
(the Start-Process line that draws them on a render worker). Each still is <out>/png/id-<id>.png, 700 px square with a
transparent background, in the same light as every other still. Streaming and read-only on the run.
"""
import argparse
import gzip
import json
import os
import sys

REPO = 'D:/Projects/experiments/evolution-simulator'
UNITY = 'C:/Program Files/Unity/Hub/Editor/6000.5.6f1/Editor/Unity.exe'
ENTRY = 'Evosim.Theatre.EditorTools.TheatreSoloStill.Run'


def head_id(line):
    if not line.startswith('{"id":'):
        return None
    end = line.find(',', 6)
    if end < 0:
        return None
    try:
        return int(line[6:end])
    except ValueError:
        return None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--run', required=True)
    ap.add_argument('--out', required=True)
    ap.add_argument('--ids', required=True)
    ap.add_argument('--size', type=int, default=700)
    ap.add_argument('--worker', type=int, default=6)
    a = ap.parse_args()

    wanted = [int(x) for x in a.ids.split(',') if x.strip()]
    want = set(wanted)
    found = {}
    top = max(want)
    try:
        with gzip.open(os.path.join(a.run, 'genomes.jsonl.gz'), 'rt', encoding='utf-8') as f:
            while True:
                try:
                    line = f.readline()
                except (EOFError, OSError):
                    break
                if not line:
                    break
                gid = head_id(line)
                if gid is None:
                    continue
                if gid in want and gid not in found:
                    found[gid] = line.rstrip('\r\n')
                    if len(found) == len(want):
                        break
                elif gid > top:
                    break
    except FileNotFoundError:
        print('no genomes.jsonl.gz in', a.run, file=sys.stderr)
        return 2
    missing = sorted(want - set(found))
    if missing:
        print('no genome row for ids', missing, file=sys.stderr)
        return 2

    os.makedirs(os.path.join(a.out, 'genomes'), exist_ok=True)
    os.makedirs(os.path.join(a.out, 'png'), exist_ok=True)
    items = []
    for gid in wanted:
        gfile = os.path.abspath(os.path.join(a.out, 'genomes', '%d.json' % gid))
        with open(gfile, 'w', encoding='utf-8', newline='\n') as f:
            f.write(found[gid] + '\n')
        items.append({'name': 'id-%d' % gid, 'genome_file': gfile, 'run_dir': os.path.abspath(a.run),
                      'png_file': os.path.abspath(os.path.join(a.out, 'png', 'id-%d.png' % gid)), 'size': a.size})
    listing = os.path.abspath(os.path.join(a.out, 'list.json'))
    with open(listing, 'w', encoding='utf-8', newline='\n') as f:
        json.dump({'schema': 'create-story-video/stills-list/1', 'background': 'transparent', 'items': items}, f, indent=1)

    log = '%s/scratch/logs/stills-skin-map.log' % REPO
    cmd = [
        '# Run from PowerShell, from the project root, with nothing else on unity-w%d and the worker refreshed' % a.worker,
        '# (./scripts/new-worker.ps1 -Workers %d). Set the dials first: $env:EVOSIM_THEATRE_SKIN_MAP = "1" for the map,' % a.worker,
        '# unset or "0" for today\'s skin, then the same list again; add the film look\'s own dials as the launcher does.',
        '$env:EVOSIM_STILLS_LIST = "%s"' % listing.replace('/', '\\'),
        '$env:EVOSIM_REPO_ROOT = "%s"' % REPO.replace('/', '\\'),
        '$p = Start-Process -FilePath "%s" -ArgumentList @("-projectPath","%s/unity-w%d","-batchmode","-executeMethod",'
        '"%s","-job-worker-count","4","-logFile","%s") -NoNewWindow -PassThru -Wait; exit $p.ExitCode' % (
            UNITY.replace('/', '\\'), REPO.replace('/', '\\'), a.worker, ENTRY, log.replace('/', '\\')),
    ]
    with open(os.path.join(a.out, 'command.ps1'), 'w', encoding='utf-8-sig', newline='\r\n') as f:
        f.write('\r\n'.join(cmd) + '\r\n')
    print('wrote', listing, 'and', os.path.join(a.out, 'command.ps1'), '(%d stills)' % len(items))
    return 0


if __name__ == '__main__':
    sys.exit(main())
