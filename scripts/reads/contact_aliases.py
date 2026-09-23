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

`contact_model` says which contact a run's overlap numbers came from (D114, 2026-09-23):
the body sphere, one bounding sphere a body, or per-part contact, a sphere on every part, under
which the same columns count pairs of bodies touching part to part. The two models' counts do
not compare either, and a read that prints an overlap number prints `model_note` beside it.

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


# D114's two contact models. The report's settings line carries `contact per part` or
# `contact per body`; config.json carries world.contactPerPart. A farm run from before the
# token has neither and ran the body sphere, the only contact there was; a Unity run is PhysX's
# colliders and is neither model.
PER_PART = 'contact per part'
PER_BODY = 'contact per body'


def contact_model(header_line=None, config=None):
    """PER_PART or PER_BODY for a farm run, from its settings line or its config.json dict.

    Either argument will do; the header wins when both are given. Returns None when neither
    says (a Unity report, or nothing handed in) -- a farm header written before the token
    (it starts `engine=dynamics`) is PER_BODY.
    """
    if header_line:
        if ' contact per part ' in header_line:
            return PER_PART
        if ' contact per body ' in header_line:
            return PER_BODY
        if header_line.startswith('engine=dynamics'):
            return PER_BODY
    if config is not None:
        world = config.get('world', {}) if isinstance(config, dict) else {}
        if 'contactPerPart' in world:
            return PER_PART if world['contactPerPart'] else PER_BODY
    return None


def model_note(header_line=None, config=None, subject='this run'):
    """The one line to print beside an overlap number, or None when the model is unknown."""
    model = contact_model(header_line, config)
    if model is None:
        return None
    what = ('a sphere on every part (D114)' if model == PER_PART else 'one sphere a body')
    return (f"{subject}'s overlap columns: {model}, {what} -- the two models' counts "
            "do not compare")
