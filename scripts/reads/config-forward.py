"""Bring an old run's config.json forward to the current build's format, for a read that only develops genomes or
steps brains: every field the old config has keeps its value, and every field it lacks is taken from a template config
already in the current format whose world had those tunables off (src/Evosim.Core.Tests/fixtures/r42-config.json, a
round-42 world re-pinned for each new tunable at its default). Prints each field taken from the template, so the read
can say what it assumed. The output's configHash is the old one, so the build reports a mismatch rather than a match:
the result is not the old run's config, only its values in a newer shape. Never use it to replay or continue a run.

Usage: python -I scripts/reads/config-forward.py OLD_CONFIG OUT [--template src/Evosim.Core.Tests/fixtures/r42-config.json]
"""
import argparse
import json

ap = argparse.ArgumentParser()
ap.add_argument("old")
ap.add_argument("out")
ap.add_argument("--template", default="src/Evosim.Core.Tests/fixtures/r42-config.json")
a = ap.parse_args()

old = json.load(open(a.old, encoding="utf-8"))
template = json.load(open(a.template, encoding="utf-8"))
taken = []


def merge(t, o, path):
    """The template's shape with the old values laid over it; lists stay the old config's whole."""
    if isinstance(t, dict) and isinstance(o, dict):
        out = {}
        for k, v in t.items():
            if k in o:
                out[k] = merge(v, o[k], path + "." + k if path else k)
            else:
                out[k] = v
                taken.append((path + "." + k if path else k, v))
        for k, v in o.items():
            if k not in t:
                out[k] = v
        return out
    return o


merged = merge(template, old, "")
with open(a.out, "w", encoding="utf-8", newline="\n") as f:
    json.dump(merged, f, indent=2)
    f.write("\n")
print("old %s, template %s, out %s" % (a.old, a.template, a.out))
print("fields taken from the template: %d" % len(taken))
for k, v in taken:
    s = json.dumps(v)
    print("  %s = %s" % (k, s if len(s) <= 80 else s[:77] + "..."))
