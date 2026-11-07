# Joins a run stopped part way and its resume into one run directory, so the forest reads (rest-read/, grip-read/,
# remains-read/) read D325's screen as one world. Usage: python -I stitch.py <stopped run dir> <resume run dir> <arm>
# The stopped run's rows end where the resume's begin (lineage at the stop's half second, stats and positions at the
# next sample), so each file is the stopped run's followed by the resume's; positions.jsonl.gz is the two gzip members
# one after the other, which gzip reads as one stream. config.json and run.json are the resume's. The farm log the
# island reads take their island line from is copied under the new arm's name. Writes
# scratch/plants-work/runs/<arm>/<the resume's stamp>/ and refuses an arm that exists.
import json, os, shutil, sys

stopped, resumed, arm = sys.argv[1].rstrip('/\\'), sys.argv[2].rstrip('/\\'), sys.argv[3]
root = 'scratch/plants-work/runs'
logs = 'scratch/wt-plants-build/scratch/logs'
out = os.path.join(root, arm, os.path.basename(resumed))
if os.path.exists(os.path.join(root, arm)):
    sys.exit('refused: %s exists' % os.path.join(root, arm))


def last_t(path):
    t = None
    for l in open(path, encoding='utf-8'):
        t = json.loads(l)['t']
    return t


def first_t(path):
    with open(path, encoding='utf-8') as f:
        return json.loads(f.readline())['t']


for name in ('stats.jsonl', 'lineage.jsonl'):
    a, b = last_t(os.path.join(stopped, name)), first_t(os.path.join(resumed, name))
    if not a < b:
        sys.exit('refused: %s of the stopped run ends at %s and the resume begins at %s' % (name, a, b))
os.makedirs(out)
for name in ('stats.jsonl', 'lineage.jsonl', 'positions.jsonl.gz'):
    with open(os.path.join(out, name), 'wb') as w:
        for src in (stopped, resumed):
            with open(os.path.join(src, name), 'rb') as r:
                shutil.copyfileobj(r, w)
for name in ('config.json', 'run.json'):
    shutil.copyfile(os.path.join(resumed, name), os.path.join(out, name))
src_arm = os.path.basename(os.path.dirname(stopped))
shutil.copyfile(os.path.join(logs, src_arm + '.log'), os.path.join(logs, arm + '.log'))
print('%s: %s to %s, then %s' % (arm, os.path.basename(stopped), last_t(os.path.join(stopped, 'stats.jsonl')),
                                  os.path.basename(resumed)))
