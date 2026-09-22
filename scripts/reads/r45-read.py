#!/usr/bin/env python3
"""The clause reader for round 45 (logbook/0114-the-mouth.md, J1 to J7), on the mouth build
(logbook/specs/mouth-spec.md, D106).

Modelled on round 44's reader (scripts/reads/r44-read.py): same run-directory layout, same
tolerant reading of stats.jsonl/positions.jsonl/lineage.jsonl/config.json/run.json (a missing
field prints 'absent' rather than raising), scripts/reads/contact_aliases.py's `field()` reused
for every stats.jsonl lookup (round 45's runs are on the .NET farm, which names the
creature-creature census `overlap*`; nothing here reads that census, but the alias is kept for
parity with r44-read.py and for any clause added later that would), the censored-manifest rule,
one row per seed per clause, and a `held in N of M` line per clause.

NAMES below is the one dictionary the task asked for: every report column, stats.jsonl field and
lineage.jsonl flag this script reads, confirmed against source on 2026-09-22 —
src/Evosim.Farm/Report.cs (`BaseColumns`), src/Evosim.Farm/Row.cs (the `.Field(...)` chain) and
src/Evosim.Core/Ecosystem/LineageEvent.cs (`ToJson`) — rather than guessed the way round 44's
ASSUMED_NAMES had to be, because this script was written after the build landed (mouth-spec.md's
"As built" section is the record of what came out different from the plan).

Three things about the mouth's recorded fields bite every clause below, and are why several
clauses print 'absent' rather than a verdict on a real run:

  - `attackShare`, `intakeShare`, `protectionShare` (report columns `attack %`, `intake %`,
    `prot %`) are STATE — the share of the living carrying the attribute above zero right now
    (`World.AttackShare` etc., `Organism.HasAttack` cached at birth and refreshed on a plan
    change) — not a running total. `partsKilled`, `bodiesEaten`, `corpsesEaten`,
    `corpsesFromKills`, `unitsEaten` and `healingJoules` (columns `killed`, `eaten`,
    `corpse eat`, `heal J`) are CUMULATIVE totals a reader windows by differencing two samples,
    exactly like `floorSpawns`/`wraps`/`crowded` before them (Row.cs's own remark: "D106 items 1,
    3 and 4, rule 8. The six counters are running totals... The three shares are states.").
  - lineage.jsonl's `atk`/`ink`/`prt` (`LineageEvent.HasAttack`/`HasIntake`/`HasProtection`) are
    booleans about the GENOME at birth — whether any node carries the attribute above zero — not
    about the developed body and not a count. `atk` and `prt` start at 0 for every founder (mouth
    spec rule 1: "Founders draw attack and protection at zero"), so a nonzero row is a real,
    unambiguous signal that the attribute appeared by mutation. `ink` does NOT have that property:
    rule 1 also says "intake at the consumer cell's recorded rate on consumer nodes", so `ink` is
    already 1 for essentially every founder that carries a consumer (mouth) cell, from the very
    first sample of every seed. The lineage row carries no per-node-type breakdown (no way to
    tell "intake on a non-consumer node" from "intake on the founding consumer node" once
    collapsed to one boolean), so this script cannot compute the stricter reading the entry's own
    prose asks for ("intake above the founder's consumer value... on a non-consumer node"). J1
    below says exactly what it reads instead, and reports the population-level `intake %` share
    alongside as the honest picture of how close to universal that flag already is at t=0.
  - No per-creature kill event is written anywhere. `Organism.LostPartPaths` (mouth-spec.md, "As
    built", item 1) records which developer path was bitten off, but it is in-memory/checkpoint
    state, never a row in lineage.jsonl or positions.jsonl; only the aggregate `partsKilled`
    counter reaches stats.jsonl. J3, which needs "the bodies that lose a non-root part, read
    1,000 s later", has no instrument to read from any file this script opens, and prints
    'absent' rather than guess a proxy — the task's own fallback for exactly this case.

Run from anywhere:

    python scripts/reads/r45-read.py
    python scripts/reads/r45-read.py --arms r44-s1 r44-s2 r44-s3   # the fixture: round 44's
                                                                    # own runs, pre-mouth, every
                                                                    # mouth clause reads 'absent'
    python scripts/reads/r45-read.py --out logbook/specs/r45-read/clauses.tsv

Touches nothing but reads; --out creates only the directory of the path given, and only then.
"""
import argparse
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))

sys.path.insert(0, HERE)
from contact_aliases import field as _aliased_field  # noqa: E402  (path set above)

DEFAULT_ARMS = ["r45-s1", "r45-s2", "r45-s3"]

# A window (J1, J2, J3's "20 consecutive windows"/"20 consecutive samples") is one stats.jsonl
# row, per Row.cs's own description of the counters it windows ("per window", differenced
# between two samples) — the same reading round 44's H4 used for its own windows.
WINDOW_SAMPLES = 20

# The share-above-1%-of-the-living threshold J4 asks of `attack %` / `prot %`.
J4_THRESHOLD_PCT = 1.0

# J7's "exceeds... by a factor of 10 or more".
J7_FACTOR = 10.0

# ---------------------------------------------------------------------- the one name dictionary
#
# Every column, stats.jsonl field and lineage.jsonl flag this script reads, in one place, per
# the task brief. Confirmed against source 2026-09-22 (see the module docstring for the three
# files and what each entry backs).
NAMES = {
    # src/Evosim.Farm/Report.cs's BaseColumns — the table's own words for D106 items 1, 3 and 4,
    # rule 8's seven columns, appended after the module gene's five. Not parsed by this script
    # (which reads stats.jsonl directly, as r44-read.py does); kept here so the printed rows and
    # this dictionary use the same words the entry and the report use.
    "col_attack_pct": "attack %",
    "col_intake_pct": "intake %",
    "col_prot_pct": "prot %",
    "col_killed": "killed",
    "col_eaten": "eaten",
    "col_corpse_eat": "corpse eat",
    "col_heal_j": "heal J",

    # stats.jsonl fields (src/Evosim.Farm/Row.cs's .Field(...) chain) backing those columns.
    # attackShare/intakeShare/protectionShare are STATE (World.AttackShare etc., a walk over the
    # living's cached HasAttack/HasIntake/HasProtection); the rest are CUMULATIVE totals a
    # reader windows by differencing two samples (Row.cs's own remark, quoted in the module
    # docstring).
    "stats_attack_share": "attackShare",
    "stats_intake_share": "intakeShare",
    "stats_protection_share": "protectionShare",
    "stats_parts_killed": "partsKilled",             # cumulative -> the "killed" column
    "stats_bodies_eaten": "bodiesEaten",              # cumulative -> the "eaten" column
    "stats_corpses_from_kills": "corpsesFromKills",   # cumulative; stays 0 for the run's whole
                                                       # life when CorpseDecayPerSecond is 0
                                                       # (World.CorpseJoules's own remark) — J7
                                                       # checks this before reading it as "no
                                                       # kills happened"
    "stats_units_eaten": "unitsEaten",                # cumulative, in CHARGED UNITS, not joules
    "stats_corpses_eaten": "corpsesEaten",            # cumulative -> the "corpse eat" column
    "stats_healing_joules": "healingJoules",          # cumulative -> the "heal J" column
    "stats_corpse_joules": "corpseJoules",            # STANDING total across every corpse that
                                                       # exists right now (World.CorpseJoules) —
                                                       # already present on a pre-mouth report;
                                                       # not itself a mouth-only field
    "stats_alive": "alive",
    "stats_audit_residual": "auditResidual",
    "stats_matter_residual": "matterResidual",
    "stats_diverged": "diverged",

    # config.json's world group (src/Evosim.Core/RunConfigJson.cs), read the same way r44-read.py
    # reads matterBudgetUnits: the density that turns unitsEaten's charged units into joules so
    # J7 can compare them against corpseJoules.
    "config_joules_per_unit": "joulesPerUnit",

    # lineage.jsonl birth-row flags (src/Evosim.Core/Ecosystem/LineageEvent.cs's ToJson). All
    # three are booleans (0/1) about the GENOME (any node with the attribute > 0), recorded at
    # birth only, never per sample and never about the developed body.
    "lineage_atk": "atk",   # HasAttack — 0 for every founder (spec rule 1); a nonzero row is
                             # unambiguously a mutation
    "lineage_ink": "ink",   # HasIntake — 1 for every founder with a consumer node already (rule
                             # 1's founding draw); see the module docstring for what this costs
                             # J1's reading
    "lineage_prt": "prt",   # HasProtection — 0 for every founder (spec rule 1)
    "lineage_id": "id",
    "lineage_parent": "p",
    "lineage_event": "e",
}

# A sentinel distinct from every legitimate stats.jsonl value (including 0 and None-shaped
# JSON), so "the field is not on this row" is never confused with "the field read zero".
_MISSING = object()


def sfield(row, name):
    """row[name] (or its farm/Unity alias, via contact_aliases.field) — _MISSING if neither key
    is on the row. Never raises: a clause that needs a missing field prints 'absent' and the
    script keeps going, per the task's rule against crashing on an old run."""
    return _aliased_field(row, name, default=_MISSING)


def present(row, name):
    return sfield(row, name) is not _MISSING


# ---------------------------------------------------------------------- reading a run directory

def run_dir(runs_root, arm):
    base = os.path.join(runs_root, arm)
    if not os.path.isdir(base):
        raise SystemExit("no run directory for arm %r under %r" % (arm, runs_root))
    dirs = [d for d in sorted(os.listdir(base)) if os.path.isdir(os.path.join(base, d))]
    if len(dirs) != 1:
        raise SystemExit(
            "expected exactly one run directory for %r, saw %r" % (arm, dirs))
    return os.path.join(base, dirs[0])


def load_jsonl(path):
    """A list of parsed rows, or None when the file does not exist — a run directory this old
    or this partial simply lacks it, which is not a crash.

    A live arm's JSONL can be mid-write on the one line the writer is appending when this
    script opens it (the file is append-only and every completed row is valid — CLAUDE.md's
    "A killed run leaves every completed row valid"), so a parse failure on the LAST line is
    read as an in-progress write and dropped rather than crashing the read; a parse failure on
    any earlier line is real corruption and still raises."""
    if not os.path.exists(path):
        return None
    with open(path, encoding="utf-8") as f:
        lines = [ln.strip() for ln in f if ln.strip()]
    rows = []
    for i, line in enumerate(lines):
        try:
            rows.append(json.loads(line))
        except json.JSONDecodeError:
            if i == len(lines) - 1:
                break
            raise
    return rows


def load_json(path):
    if not os.path.exists(path):
        return None
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def sample_at(ts, by_t, target):
    """The stats.jsonl row at exactly `target` seconds, or the last row at or before it when
    the run ended early — "a manifest reading `error` or `stopped` is censored and read at its
    last sample" (round 44's Rules, unchanged for round 45). Returns (row, actual_t, was_short)."""
    if target in by_t:
        return by_t[target], target, False
    earlier = [t for t in ts if t <= target]
    if earlier:
        t = max(earlier)
        return by_t[t], t, True
    t = ts[-1]
    return by_t[t], t, True


def windowed_bools(ts, by_t, field_name):
    """(times, bools) aligned to ts[1:] — bools[i] is True where field_name's CUMULATIVE value
    differenced between ts[i] and ts[i+1] is > 0 (one report/stats row is one window, per the
    module docstring). (None, None) when the field is missing from the first sample (an older
    run, or a field this build never wrote)."""
    if not ts or not present(by_t[ts[0]], field_name):
        return None, None
    times = []
    bools = []
    prev = sfield(by_t[ts[0]], field_name)
    for t in ts[1:]:
        v = sfield(by_t[t], field_name)
        if v is _MISSING:
            return None, None
        times.append(t)
        bools.append((v - prev) > 0)
        prev = v
    return times, bools


def state_bools(ts, by_t, field_name, threshold=0.0):
    """bools aligned to ts — True where field_name's own (state, not windowed) value at that
    sample is > threshold. None when the field is missing from the first sample."""
    if not ts or not present(by_t[ts[0]], field_name):
        return None
    bools = []
    for t in ts:
        v = sfield(by_t[t], field_name)
        if v is _MISSING:
            return None
        bools.append(v > threshold)
    return bools


def longest_true_run(bools):
    """(length, start_index) of the longest run of consecutive True values, (0, None) if none."""
    best, cur = 0, 0
    start, best_start = None, None
    for i, b in enumerate(bools):
        if b:
            if cur == 0:
                start = i
            cur += 1
            if cur > best:
                best, best_start = cur, start
        else:
            cur = 0
    return best, best_start


def lineage_inherited_flag_births(lineage_rows, flag_key):
    """(available, inherited_count, flagged_count) for a lineage.jsonl birth flag ('atk', 'ink'
    or 'prt'): how many births carry the flag at all, and how many of those are 'inherited' in
    the project's own convention — the child carries it AND its parent's own birth row also
    carried it (CLAUDE.md's floor-contamination gotcha reads jointedInherited/
    absorptiveInherited the same way: "creatures whose parent had the trait"). A diagnostic only
    — J1/J2 below do not gate on it, because the flag alone (no per-node-type breakdown) cannot
    distinguish a lineage passing a mutation down many generations from many independent
    founders each drawing the same flag at t=0; it is printed so a reader can see which case a
    given seed looks like. `available` is False when no birth row carries `flag_key` at all (a
    run recorded before the mouth build)."""
    if not lineage_rows:
        return False, 0, 0
    flag_map = {}
    available = False
    for row in lineage_rows:
        if row.get(NAMES["lineage_event"]) != "b":
            continue
        if flag_key in row:
            available = True
            flag_map[row[NAMES["lineage_id"]]] = row[flag_key]
    if not available:
        return False, 0, 0

    inherited = 0
    flagged = 0
    for row in lineage_rows:
        if row.get(NAMES["lineage_event"]) != "b" or flag_key not in row:
            continue
        if row[flag_key]:
            flagged += 1
            parent_flag = flag_map.get(row[NAMES["lineage_parent"]])
            if parent_flag:
                inherited += 1
    return True, inherited, flagged


# ---------------------------------------------------------------------- J1: a scavenger line founds

def j1(seed, ts, by_t, lineage_rows):
    """J1: intake above zero (read as the population share `intake %` > 0 — see the module
    docstring for why this script cannot isolate "on a non-consumer node") AND `corpse eat`
    above zero, together, for 20 consecutive windows, in 2 of 3 (the entry's own count)."""
    ink_field = NAMES["stats_intake_share"]
    corpse_field = NAMES["stats_corpses_eaten"]

    if not ts or not present(by_t[ts[0]], ink_field):
        return dict(clause="J1", seed=seed, held="absent",
                     note="attackShare/intakeShare/protectionShare are not on this report "
                          "(a pre-mouth run)")

    ink_state = state_bools(ts, by_t, ink_field)
    corp_times, corp_wins = windowed_bools(ts, by_t, corpse_field)
    if corp_times is None:
        return dict(clause="J1", seed=seed, held="absent",
                     note="corpsesEaten is not on this report (a pre-mouth run)")

    ink_windowed = ink_state[1:]  # align to corp_times (ts[1:])
    combined = [a and b for a, b in zip(ink_windowed, corp_wins)]
    run_len, run_start = longest_true_run(combined)
    held = run_len >= WINDOW_SAMPLES

    inherited_ok, inherited_n, flagged_n = lineage_inherited_flag_births(
        lineage_rows, NAMES["lineage_ink"])

    intake_share_end = sfield(by_t[ts[-1]], ink_field)

    return dict(
        clause="J1", seed=seed,
        longest_combined_run_windows=run_len,
        run_started_at=(corp_times[run_start] if run_start is not None else None),
        intake_pct_at_end=(100.0 * intake_share_end if intake_share_end is not _MISSING else None),
        ink_flagged_births=flagged_n, ink_inherited_births=inherited_n,
        held=held,
        note="intake % is near-universal from t=0 in most seeds (every founding consumer "
             "carries it); this reading is dominated by the corpse-eat condition, not by ink "
             "appearing — see the module docstring")


# ---------------------------------------------------------------------- J2: a killer line founds

def j2(seed, ts, by_t, lineage_rows):
    """J2: attack above zero (population share `attack %` > 0 — atk is 0 for every founder, so
    this is an unambiguous emergence-by-mutation signal, unlike ink) AND `killed` above zero,
    together, for 20 consecutive windows, in 1 of 3 or more."""
    atk_field = NAMES["stats_attack_share"]
    killed_field = NAMES["stats_parts_killed"]

    if not ts or not present(by_t[ts[0]], atk_field):
        return dict(clause="J2", seed=seed, held="absent",
                     note="attackShare/intakeShare/protectionShare are not on this report "
                          "(a pre-mouth run)")

    atk_state = state_bools(ts, by_t, atk_field)
    kill_times, kill_wins = windowed_bools(ts, by_t, killed_field)
    if kill_times is None:
        return dict(clause="J2", seed=seed, held="absent",
                     note="partsKilled is not on this report (a pre-mouth run)")

    atk_windowed = atk_state[1:]
    combined = [a and b for a, b in zip(atk_windowed, kill_wins)]
    run_len, run_start = longest_true_run(combined)
    held = run_len >= WINDOW_SAMPLES

    inherited_ok, inherited_n, flagged_n = lineage_inherited_flag_births(
        lineage_rows, NAMES["lineage_atk"])

    atk_share_end = sfield(by_t[ts[-1]], atk_field)

    return dict(
        clause="J2", seed=seed,
        longest_combined_run_windows=run_len,
        run_started_at=(kill_times[run_start] if run_start is not None else None),
        attack_pct_at_end=(100.0 * atk_share_end if atk_share_end is not _MISSING else None),
        atk_flagged_births=flagged_n, atk_inherited_births=inherited_n,
        held=held)


# ---------------------------------------------------------------------- J3: grazing survival

def j3(seed):
    """J3: among bodies that lose a non-root part, whether an indeterminate node (round 44's
    gene) predicts surviving 1,000 s later — unreadable from any file this script opens.

    A kill's identity — which body, which part, at what time — lives only as
    `Organism.LostPartPaths` (mouth-spec.md's "As built" item 1), which is never written to
    lineage.jsonl, positions.jsonl or stats.jsonl: only the aggregate `partsKilled` counter
    reaches a report, with no id attached. lineage.jsonl's death rows carry a cause
    ('starved'/'diverged'/'eaten') but 'eaten' is reserved for a ROOT death (BodiesEaten, not
    PartsKilled — WorldMouth.cs's KillPart: "the difference is the point" — a body that loses a
    non-root part and survives never gets a death row at all, which is exactly the case this
    clause needs to find). The instrument J3 needs — a per-creature "lost a non-root part at
    time t; alive at t+1000?" record, joined to whether its genome carries an indeterminate node
    — does not exist in any recorded output, so this prints 'absent' rather than proxy one."""
    return dict(
        clause="J3", seed=seed, held="absent",
        note="no per-body kill event is recorded anywhere (Organism.LostPartPaths is in-memory/"
             "checkpoint state only); lineage.jsonl and stats.jsonl carry only the aggregate "
             "partsKilled counter, with no id, so a body cannot be tied to the loss of a "
             "specific non-root part or watched for 1,000 s afterward. The missing instrument "
             "is a per-creature kill-event log (id, time, part path, was-root) beside "
             "lineage.jsonl's birth/death rows.")


# ---------------------------------------------------------------------- J4: defence follows offence

def j4(seed, ts, by_t):
    """J4: the first window in which `prot %` exceeds 1% comes after the first in which
    `attack %` does, in 3 of 3."""
    atk_field = NAMES["stats_attack_share"]
    prt_field = NAMES["stats_protection_share"]

    if not ts or not present(by_t[ts[0]], atk_field) or not present(by_t[ts[0]], prt_field):
        return dict(clause="J4", seed=seed, held="absent",
                     note="attackShare/protectionShare are not on this report (a pre-mouth run)")

    first_atk_t, first_prt_t = None, None
    for t in ts:
        a = sfield(by_t[t], atk_field)
        p = sfield(by_t[t], prt_field)
        if first_atk_t is None and a is not _MISSING and 100.0 * a > J4_THRESHOLD_PCT:
            first_atk_t = t
        if first_prt_t is None and p is not _MISSING and 100.0 * p > J4_THRESHOLD_PCT:
            first_prt_t = t

    if first_prt_t is None:
        held = True
        note = "protection never exceeded 1% of the living this seed; the ordering claim is " \
               "untested (vacuously not violated), not confirmed"
    elif first_atk_t is None:
        held = False
        note = "protection exceeded 1% while attack never did"
    else:
        held = first_prt_t > first_atk_t
        note = None if held else "protection's first window at or before attack's"

    return dict(clause="J4", seed=seed, first_attack_over_1pct_at=first_atk_t,
                first_protection_over_1pct_at=first_prt_t, held=held, note=note)


# ---------------------------------------------------------------------- J5: the crowd survives

def j5(seed, ts, by_t):
    """J5: `alive` above 300 at 30,000 s, in 2 of 3 (the massacre reading if not)."""
    if not ts:
        return dict(clause="J5", seed=seed, held="absent")

    row, t_actual, short = sample_at(ts, by_t, 30000)
    alive = sfield(row, NAMES["stats_alive"])
    alive = None if alive is _MISSING else alive
    held = alive is not None and alive > 300

    return dict(clause="J5", seed=seed, at=t_actual, short=short, alive=alive, held=held)


# ---------------------------------------------------------------------- J6: the books close

def j6(seed, ts, by_t, manifest):
    """J6: `audit` under 0.1 J at every sample, the matter residual under 1e-5 units at
    30,000 s, `diverged` 0, manifests not censored, in 3 of 3."""
    if not ts:
        return dict(clause="J6", seed=seed, held="absent")

    audit_values = [sfield(by_t[t], NAMES["stats_audit_residual"]) for t in ts]
    audit_values = [v for v in audit_values if v is not _MISSING]
    audit_absmax = max(abs(v) for v in audit_values) if audit_values else None

    row30000, t_actual, short = sample_at(ts, by_t, 30000)
    matter_resid = sfield(row30000, NAMES["stats_matter_residual"])
    matter_resid = None if matter_resid is _MISSING else matter_resid

    diverged_total = sfield(by_t[ts[-1]], NAMES["stats_diverged"])
    diverged_total = None if diverged_total is _MISSING else diverged_total

    status = manifest.get("status") if manifest else None
    censored = status in ("error", "stopped")
    provisional = status == "running"

    held = (not censored and
            audit_absmax is not None and audit_absmax < 0.1 and
            matter_resid is not None and abs(matter_resid) < 1e-5 and
            diverged_total is not None and diverged_total == 0)

    return dict(clause="J6", seed=seed, audit_absmax=audit_absmax, matter_resid_at=t_actual,
                matter_resid=matter_resid, matter_resid_short=short,
                diverged_total=diverged_total, manifest_status=status,
                censored=censored, provisional=provisional, held=held)


# ---------------------------------------------------------------------- J7: the yield is the reserve

def j7(seed, ts, by_t, config):
    """J7: mean `unitsEaten` per `corpsesEaten` at the end exceeds ten times the mean tissue of
    a killed part.

    `unitsEaten` is in charged units (D098), so it is converted to joules via config.json's
    `world.joulesPerUnit` before comparing against `corpseJoules`, which is joules directly
    (World.CorpseJoules). No stats field gives a killed part's tissue on its own (mouth-spec.md
    says as much: "not recorded per part"), so this reads the task's named fallback —
    `corpseJoules` (a STANDING total over every corpse that currently exists, of any origin)
    divided by `corpsesFromKills` (a CUMULATIVE count of every corpse a kill has ever founded) —
    at the run's last sample. That mixes a snapshot with a running total and mixes kill-corpses
    with ordinary-death corpses (both go through the same `Bury` once `CorpseDecayPerSecond` is
    above 0), so it is an order-of-magnitude estimate, not a per-part reading, and is reported
    as one. `corpsesFromKills` (and therefore this whole estimate) stays 0 for a run's entire
    life when `CorpseDecayPerSecond` is 0 — "every run on file" per `World.CorpseJoules`'s own
    remark — which is read here as the missing instrument, not as "nothing was killed"."""
    units_f = NAMES["stats_units_eaten"]
    corp_eat_f = NAMES["stats_corpses_eaten"]
    corp_kill_f = NAMES["stats_corpses_from_kills"]
    corp_j_f = NAMES["stats_corpse_joules"]

    if not ts:
        return dict(clause="J7", seed=seed, held="absent")

    last = by_t[ts[-1]]
    units_eaten = sfield(last, units_f)
    corpses_eaten = sfield(last, corp_eat_f)

    if units_eaten is _MISSING or corpses_eaten is _MISSING:
        return dict(clause="J7", seed=seed, held="absent",
                     note="unitsEaten/corpsesEaten are not on this report (a pre-mouth run)")

    if not corpses_eaten or corpses_eaten <= 0:
        return dict(clause="J7", seed=seed, held="absent", units_eaten=units_eaten,
                     corpses_eaten=corpses_eaten,
                     note="corpsesEaten reads 0 at the run's last sample -- no corpse has been "
                          "emptied by a mouth yet, so unitsEaten/corpsesEaten has no mean")

    corpses_from_kills = sfield(last, corp_kill_f)
    if corpses_from_kills is _MISSING:
        return dict(clause="J7", seed=seed, held="absent",
                     units_eaten=units_eaten, corpses_eaten=corpses_eaten,
                     note="corpsesFromKills is not on this report (a pre-mouth run)")

    if not corpses_from_kills or corpses_from_kills <= 0:
        return dict(clause="J7", seed=seed, held="absent",
                     units_eaten=units_eaten, corpses_eaten=corpses_eaten,
                     corpses_from_kills=corpses_from_kills,
                     note="corpsesFromKills reads 0 at the run's last sample -- either no kill "
                          "has yet founded a standing corpse, or CorpseDecayPerSecond is 0 in "
                          "this config, which suppresses corpse creation (and this counter) "
                          "entirely per World.CorpseJoules's own remark; check config.json's "
                          "feeding group before reading this as 'nothing has been killed'")

    corpse_joules = sfield(last, corp_j_f)
    if corpse_joules is _MISSING:
        return dict(clause="J7", seed=seed, held="absent",
                     units_eaten=units_eaten, corpses_eaten=corpses_eaten,
                     corpses_from_kills=corpses_from_kills,
                     note="corpseJoules is not on this report")

    joules_per_unit = None
    if config is not None:
        try:
            joules_per_unit = config["world"][NAMES["config_joules_per_unit"]]
        except (KeyError, TypeError):
            joules_per_unit = None

    if joules_per_unit is None:
        return dict(clause="J7", seed=seed, held="absent",
                     units_eaten=units_eaten, corpses_eaten=corpses_eaten,
                     note="config.json has no world.joulesPerUnit to convert unitsEaten "
                          "(charged units) into joules for comparison against corpseJoules")

    mean_take_units = units_eaten / corpses_eaten
    mean_take_joules = mean_take_units * joules_per_unit
    mean_tissue_joules_est = corpse_joules / corpses_from_kills

    if mean_tissue_joules_est <= 0:
        return dict(clause="J7", seed=seed, held="absent",
                     units_eaten=units_eaten, corpses_eaten=corpses_eaten,
                     mean_take_units=mean_take_units, mean_take_joules=mean_take_joules,
                     corpses_from_kills=corpses_from_kills,
                     corpse_joules_standing=corpse_joules,
                     note="the estimated mean tissue of a killed part reads 0 or less (every "
                          "kill-corpse standing at the last sample has already decayed or been "
                          "eaten away) -- the ratio is degenerate")

    ratio = mean_take_joules / mean_tissue_joules_est
    held = ratio > J7_FACTOR

    return dict(clause="J7", seed=seed, units_eaten=units_eaten, corpses_eaten=corpses_eaten,
                mean_take_units=mean_take_units, mean_take_joules=mean_take_joules,
                corpses_from_kills=corpses_from_kills, corpse_joules_standing=corpse_joules,
                mean_tissue_joules_est=mean_tissue_joules_est, ratio=ratio, held=held)


# ---------------------------------------------------------------------- driving it over the arms

def read_arm(runs_root, arm):
    d = run_dir(runs_root, arm)

    stats = load_jsonl(os.path.join(d, "stats.jsonl"))
    if not stats:
        raise SystemExit("no stats.jsonl (or an empty one) for arm %r at %r" % (arm, d))
    by_t = {r["t"]: r for r in stats}
    ts = sorted(by_t)

    lineage = load_jsonl(os.path.join(d, "lineage.jsonl"))
    manifest = load_json(os.path.join(d, "run.json"))
    config = load_json(os.path.join(d, "config.json"))

    return [
        j1(arm, ts, by_t, lineage),
        j2(arm, ts, by_t, lineage),
        j3(arm),
        j4(arm, ts, by_t),
        j5(arm, ts, by_t),
        j6(arm, ts, by_t, manifest),
        j7(arm, ts, by_t, config),
    ]


def fmt_cell(v):
    if v is None:
        return ""
    if isinstance(v, float):
        return "%.6g" % v
    return str(v)


def write_tsv(path, rows):
    out_dir = os.path.dirname(path)
    if out_dir:
        os.makedirs(out_dir, exist_ok=True)

    cols = []
    for r in rows:
        for k in r:
            if k not in cols:
                cols.append(k)

    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\t".join(cols) + "\n")
        for r in rows:
            f.write("\t".join(fmt_cell(r.get(k)) for k in cols) + "\n")


# The entry's own outer count per clause, printed beside each summary line so "held in N of 3"
# reads against the threshold that decides the round, not just a raw tally.
CLAUSE_THRESHOLD_TEXT = {
    "J1": "2 of 3",
    "J2": "1 of 3 or more",
    "J3": "2 of 3 (unreadable -- see note)",
    "J4": "3 of 3",
    "J5": "2 of 3",
    "J6": "3 of 3",
    "J7": "reads at the end, no outer count in the entry",
}


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                  formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--runs-root", default=os.path.join(REPO, "runs"),
                     help="defaults to <repo>/runs")
    ap.add_argument("--arms", nargs="+", default=DEFAULT_ARMS)
    ap.add_argument("--out", default=None,
                     help="write the TSV here (creates its directory; nothing is written "
                          "without this flag)")
    args = ap.parse_args()

    clauses = ("J1", "J2", "J3", "J4", "J5", "J6", "J7")
    all_rows = []
    by_clause = {c: [] for c in clauses}

    for arm in args.arms:
        try:
            arm_rows = read_arm(args.runs_root, arm)
        except SystemExit as e:
            print("skipping %r: %s" % (arm, e), file=sys.stderr)
            continue

        for r in arm_rows:
            all_rows.append(r)
            by_clause[r["clause"]].append(r.get("held"))
            print(r)

    print()
    for c in clauses:
        results = by_clause[c]
        n_true = sum(1 for x in results if x is True)
        n_absent = sum(1 for x in results if x == "absent")
        note = " (%d absent)" % n_absent if n_absent else ""
        print("%s: held in %d of %d%s -- needs %s" %
              (c, n_true, len(results), note, CLAUSE_THRESHOLD_TEXT[c]))

    if args.out:
        write_tsv(args.out, all_rows)
        print()
        print("wrote", args.out)


if __name__ == "__main__":
    main()
