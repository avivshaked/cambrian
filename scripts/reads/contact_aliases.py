"""Shared alias table for the contact/overlap instrument's two names.

CLAUDE.md's two-farms gotcha: the .NET farm (src/Evosim.Farm) counts overlapping
bounding spheres between creatures where the Unity build counted PhysX contact
manifolds between colliders -- a different census, so it does not compare, and four
report columns plus the matching stats.jsonl fields (and the floor/bed pair beside
them) were renamed rather than kept (Row.cs's Sampler remarks, Report.cs's BaseColumns
comment). "The contact instrument is renamed on the farm ... and the read scripts still
read the old names" -- this module is what stops that: a script asked for the Unity
name against a farm run, or the farm name against a Unity run, gets the number under
whichever name the file actually carries, and can say once which name it read. It
resolves a name, not a measurement -- the two counts never compare across the change.

    from contact_aliases import field, SAID, note

    v = field(row, 'contactPairsPerStep')   # a stats.jsonl dict; farm or Unity, either
    ...
    if SAID:
        print(note(subject='this run'))
        SAID.clear()

`field` and `note`/`SAID` are for stats.jsonl rows (plain dicts). `COLUMN_ALIASES` is
the same four pairs for a run report's markdown table column names, for a script that
parses `runs/<arm>.md` directly instead of `stats.jsonl` (analyse-arm.ps1's own
`$columnAliases` is the PowerShell twin of this half; it is not imported from here
because PowerShell and Python do not share a module).

Scripts under scripts/reads/ import this by its bare name -- Python puts a script's own
directory on sys.path, and this module lives beside them. A script elsewhere (such as
scripts/compare-det.py) adds scripts/reads to sys.path first:

    import os, sys
    sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'reads'))
    from contact_aliases import field, SAID, note
"""

# The report table's four renamed columns, both directions.
COLUMN_ALIASES = {
    'contacts': 'overlaps', 'overlaps': 'contacts',
    'pairs/body': 'ovl/body', 'ovl/body': 'pairs/body',
    'pairs jnt %': 'ovl jnt %', 'ovl jnt %': 'pairs jnt %',
    'stuck %': 'ovl held %', 'ovl held %': 'stuck %',
}

# stats.jsonl's renamed fields, both directions -- the creature-creature census and the
# bed-or-glass one beside it, renamed on the same build for the same reason.
FIELD_ALIASES = {
    'contactPairs': 'overlapPairs',
    'contactPairsPerStep': 'overlapPairsPerStep',
    'contactPairsJointed': 'overlapPairsJointed',
    'contactPairsPersistent': 'overlapPairsHeld',
    'contactBodies': 'overlapBodies',
    'floorContactPairs': 'bedOrGlassBodies',
    'floorContactPairsPerStep': 'bedOrGlassBodiesPerStep',
}
FIELD_ALIASES.update({v: k for k, v in FIELD_ALIASES.items()})

# name asked -> name actually read, for this process's lifetime. A caller prints `note()`
# once after the numbers it drove and then clears it, as analyse-arm.ps1's own aliasNotes
# does per report.
SAID = {}


_NO_DEFAULT = object()


def field(row, name, default=_NO_DEFAULT):
    """row[name], or row[the renamed field] when the row carries that one instead.

    Raises KeyError(name), like row[name] would, when neither name is present and no
    default was given -- `default=None` is a real default (as `dict.get` treats it), not
    "no default". Records a substitution in SAID so a caller can say once which census it
    actually read.
    """
    if name in row:
        return row[name]
    other = FIELD_ALIASES.get(name)
    if other is not None and other in row:
        SAID[name] = other
        return row[other]
    if default is _NO_DEFAULT:
        raise KeyError(name)
    return default


def note(subject='this report'):
    """The one line to print when SAID recorded a substitution, or None otherwise."""
    if not SAID:
        return None
    return (f'{subject} names them differently: ' +
            '; '.join(f'{k} read as {v}' for k, v in sorted(SAID.items())) +
            ' -- a different census, not the same number')
