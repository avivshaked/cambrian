#!/usr/bin/env python3
"""The shot list step's tool, for the create-story-video skill (shot-list.md).

    python shots.py check <folder> <n>    check draft n of the shot list; on PASS copy it to shots.json
    python shots.py sheet <shots json>     print one line a shot, for the owner and the reviewers

The first line each command prints is its verdict, as shot-list.md reads it. The last line says how long the
command took. Exit status: 0 on PASS or a sheet, 1 otherwise, 2 on a wrong command. It uses the standard
library only, and reads the script through script-check.py's own parser.
"""

import hashlib
import importlib.util
import json
import math
import re
import shutil
import sys
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
_spec = importlib.util.spec_from_file_location("script_check", HERE / "script-check.py")
sc = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(sc)

SCHEMA = "create-story-video/shots/2"
CAMERAS = ("Arrival", "Descent", "Portrait", "Floor", "Birth", "Colony", "Card")
MIN_SHOT_S = 2.0
EPS = 0.0015                       # the timeline's own rounding
FRAME_S = 1 / 30                   # how far a stretch may differ from its shot's length
SHOT_ID = re.compile(r"([0-9]+[.][0-9]+)([a-z])")
TOP = {"schema", "script_sha256", "narration_sha256", "shots"}
COMMON = {"id", "passage", "kind", "from_s", "to_s", "see", "text", "places"}
FOOTAGE = {"run", "bodies", "camera", "run_from_s", "run_to_s"}
DRAWING = {"beats", "stills"}
# Every shot carries footage, and a graphic is a layer over its own shot's footage. Only a graphic that stands
# alone, over no footage, holds `alone` in place of the footage's fields (the owner's ruling of 2026-09-28).
KEYS = {"run": [COMMON | FOOTAGE], "graphic": [COMMON | DRAWING | FOOTAGE, COMMON | DRAWING | {"alone"}]}
PLACES_FORM = "places are written as the script writes them, [facts.md:L<a>] or [facts.md:L<a>-<b>]"


class Refused(Exception):
    """A command that cannot go on; its lines say why."""

    def __init__(self, *lines):
        super().__init__(lines[0] if lines else "")
        self.lines = list(lines)


def sha256_of(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8")) if path.is_file() else None


def number(x):
    return isinstance(x, (int, float)) and not isinstance(x, bool) and math.isfinite(x)


def minutes(seconds):
    t = round(seconds, 1)
    return f"{int(t // 60)}:{t % 60:04.1f}"


def named(name, facts):
    """Whether facts.md names this run or body as a word of its own."""
    return re.search("(?<![A-Za-z0-9_-])" + re.escape(name) + "(?![A-Za-z0-9_-])", facts) is not None


def check_places(value, lines, where, errors):
    if not isinstance(value, str):
        errors.append(f"{where}: {PLACES_FORM}")
        return
    places, rest = sc.places_in(value)
    if rest or not places:
        errors.append(f"{where}: {PLACES_FORM}")
    for p in places:
        if not 1 <= p["first"] <= p["last"] <= lines:
            span = f"{p['first']}" if p["first"] == p["last"] else f"{p['first']}-{p['last']}"
            errors.append(f"{where}: [facts.md:L{span}] is not in facts.md, which has {lines} lines")


def check_text(shot, where, lines, given, errors):
    if not isinstance(shot["text"], list):
        errors.append(f"{where}: text is a list of the words on screen")
        return
    for t in shot["text"]:
        if not isinstance(t, dict) or set(t) != {"words", "facts", "from_s", "to_s"}:
            errors.append(f"{where}: each text holds words, facts, from_s and to_s")
            continue
        words = t["words"]
        if not isinstance(words, str) or not words.strip():
            errors.append(f"{where}: a text with no words")
            continue
        count = len(sc.unmark(words).split())
        if count > sc.TEXT_MAX:
            errors.append(f"{where}: '{words}' is {count} words on screen; at most {sc.TEXT_MAX}")
        m = sc.GIVEN.fullmatch(words)
        if t["facts"] == "" and m:
            if sc.squash(m.group(1)) not in given:
                errors.append(f"{where}: '{words}' is not a name the script gives between braces, so it holds its "
                              "places")
        else:
            check_places(t["facts"], lines, f"{where}, '{words}'", errors)
        inside = (number(t["from_s"]) and number(t["to_s"])
                  and shot["from_s"] - EPS <= t["from_s"] < t["to_s"] <= shot["to_s"] + EPS)
        if not inside:
            errors.append(f"{where}: '{words}' shows inside its shot, from_s before to_s")


def check_footage(shot, where, facts, errors):
    if not isinstance(shot["run"], str) or not named(shot["run"], facts):
        errors.append(f"{where}: run '{shot['run']}' is not named in facts.md")
    if not isinstance(shot["bodies"], list) or any(not isinstance(b, str) for b in shot["bodies"]):
        errors.append(f"{where}: bodies is a list of body ids, each a string")
    else:
        for b in shot["bodies"]:
            if not named(b, facts):
                errors.append(f"{where}: body '{b}' is not named in facts.md")
    if shot["camera"] not in CAMERAS:
        errors.append(f"{where}: camera is one of {', '.join(CAMERAS)}")
    a, b = shot["run_from_s"], shot["run_to_s"]
    if not (number(a) and number(b)) or not 0 <= a < b:
        errors.append(f"{where}: run_from_s and run_to_s are simulated seconds, 0 or more, the first before the last")
        return
    stretch, length = b - a, shot["to_s"] - shot["from_s"]
    if abs(stretch - length) > FRAME_S:
        errors.append(f"{where}: plays {stretch:.2f} s of the run in a shot of {length:.2f} s; footage plays at "
                      "the speed of life")


def good_beat(beat):
    return (isinstance(beat, dict) and set(beat) == {"at_s", "shows"} and number(beat["at_s"])
            and isinstance(beat["shows"], str) and bool(beat["shows"].strip()))


def good_still(still):
    return (isinstance(still, dict) and set(still) == {"run", "body", "second"} and number(still["second"])
            and still["second"] >= 0 and isinstance(still["run"], str) and isinstance(still["body"], str))


def check_graphic(shot, where, facts, errors):
    beats = shot["beats"]
    if not isinstance(beats, list) or not beats:
        errors.append(f"{where}: a graphic has at least one beat")
    else:
        last = None
        for beat in beats:
            if not good_beat(beat):
                errors.append(f"{where}: each beat holds at_s, a time, and shows, what appears then")
                continue
            if not shot["from_s"] - EPS <= beat["at_s"] < shot["to_s"]:
                errors.append(f"{where}: a beat at {beat['at_s']:.3f} s lies outside its shot")
            if last is not None and beat["at_s"] <= last:
                errors.append(f"{where}: its beats are not in order")
            last = beat["at_s"]
    stills = shot["stills"]
    if not isinstance(stills, list):
        errors.append(f"{where}: stills is a list of the bodies to draw")
        return
    for still in stills:
        if not good_still(still):
            errors.append(f"{where}: each still holds run, body and second")
            continue
        for name in (still["run"], still["body"]):
            if not named(name, facts):
                errors.append(f"{where}: '{name}' is not named in facts.md")


def departures(pid, picture, mine):
    """Where a passage's shots depart from the script's directing, as lines. None of them refuses."""
    out = [f"passage {pid}: cut into {len(mine)} shots"] if len(mine) > 1 else []
    for s in mine:
        if s["kind"] != picture["kind"]:
            out.append(f"shot {s['id']}: a {s['kind']} where the script has a {picture['kind']}")
        if picture["kind"] == "run" and "run" in s:
            if s["run"] != picture["run"] or s["bodies"] != picture["bodies"]:
                out.append(f"shot {s['id']}: another run or other bodies than the script's")
            a, b = picture["seconds"] or (None, None)
            known = a is not None and number(s["run_from_s"]) and number(s["run_to_s"])
            if known and (s["run_to_s"] < a or s["run_from_s"] > b):
                out.append(f"shot {s['id']}: plays {s['run_from_s']:g} to {s['run_to_s']:g} s, outside the "
                           f"script's {a:g} to {b:g} s")
    return out


def check_shots(data, folder):
    """Every way a shot list departs from shots-schema.md, as lines, and where it departs from the script."""
    script, script_errors = sc.parse_script(sc.read(folder / "script.md"))
    if script_errors:
        raise Refused("script.md does not follow its form; script-check.py script says how")
    narration = read_json(folder / "narration.json")
    if narration is None:
        raise Refused("the film has no narration.json; the narration step writes it")
    facts = sc.read(folder / "facts.md")
    lines = len(facts.splitlines())
    passages = {p["id"]: p for beat in script["beats"] for p in beat["passages"]}
    given = {sc.squash(g) for p in passages.values() for s in p["say"] for g in sc.GIVEN.findall(s)}
    errors, notes = [], []
    if not isinstance(data, dict) or set(data) != TOP:
        return ["the file does not hold exactly the fields of the form"], notes
    if data["schema"] != SCHEMA:
        errors.append(f"schema is {data['schema']!r}, not {SCHEMA!r}")
    if data["script_sha256"] != sha256_of(folder / "script.md"):
        errors.append("script_sha256 is not the current script.md's: this shot list directs other words")
    if data["narration_sha256"] != sha256_of(folder / "narration.json"):
        errors.append("narration_sha256 is not the current narration.json's: it is timed to another narration")
    if narration.get("source") == "estimate":
        notes.append("timed on an estimate: every time moves when a real narration replaces it")
    shots = data["shots"]
    if not isinstance(shots, list) or not shots:
        return errors + ["shots is a list of at least one shot"], notes

    span = {p["id"]: (p["from_s"], p["to_s"]) for p in narration["passages"]}
    order, by_passage, ids = [p["id"] for p in narration["passages"]], {}, []
    for i, shot in enumerate(shots, 1):
        where = f"shot {shot.get('id')}" if isinstance(shot, dict) and shot.get("id") else f"shot {i}"
        if not isinstance(shot, dict) or shot.get("kind") not in KEYS or set(shot) not in KEYS[shot["kind"]]:
            errors.append(f"{where}: kind is run or graphic, and the shot holds exactly its kind's fields: a run "
                          "shot the footage's, and a graphic its beats and stills and either the footage's or alone")
            continue
        m = SHOT_ID.fullmatch(str(shot["id"]))
        if not m or m.group(1) != shot["passage"]:
            errors.append(f"{where}: its id is its passage's id and a letter")
            continue
        if shot["passage"] not in span or shot["passage"] not in passages:
            errors.append(f"{where}: passage {shot['passage']} is not the script's and the narration's")
            continue
        if not (number(shot["from_s"]) and number(shot["to_s"])):
            errors.append(f"{where}: from_s and to_s are seconds on the timeline")
            continue
        ids.append(shot["id"])
        by_passage.setdefault(shot["passage"], []).append(shot)
        length = shot["to_s"] - shot["from_s"]
        if length < MIN_SHOT_S - EPS:
            errors.append(f"{where}: lasts {length:.2f} s; a shot lasts at least {MIN_SHOT_S:g} s")
        if not isinstance(shot["see"], str) or not shot["see"].strip():
            errors.append(f"{where}: see says what the viewer sees")
        check_places(shot["places"], lines, where, errors)
        check_text(shot, where, lines, given, errors)
        if "run" in shot:
            check_footage(shot, where, facts, errors)
        if shot["kind"] == "graphic":
            check_graphic(shot, where, facts, errors)
        if "alone" in shot and (not isinstance(shot["alone"], str) or not shot["alone"].strip()):
            errors.append(f"{where}: alone says why the graphic stands over no footage")

    if len(set(ids)) != len(ids):
        errors.append("two shots share an id")
    walked = []
    for shot in shots:
        if isinstance(shot, dict) and shot.get("id") in ids and (not walked or walked[-1] != shot["passage"]):
            walked.append(shot["passage"])
    if walked != [pid for pid in order if pid in by_passage]:
        errors.append("the shots are not in the narration's order, each passage's together")
    for pid in order:
        mine = by_passage.get(pid)
        if not mine:
            errors.append(f"passage {pid} has no shot")
            continue
        letters = [SHOT_ID.fullmatch(s["id"]).group(2) for s in mine]
        if letters != [chr(ord("a") + k) for k in range(len(mine))]:
            errors.append(f"passage {pid}: its shots are lettered a, b, c and on, in order")
        start, end = span[pid]
        at = start
        for s in mine:
            if abs(s["from_s"] - at) > EPS:
                errors.append(f"shot {s['id']}: starts at {s['from_s']:.3f} s where {at:.3f} s was due; a passage's "
                              "shots tile its span")
                break
            at = s["to_s"]
        else:
            if abs(at - end) > EPS:
                errors.append(f"passage {pid}: its shots end at {at:.3f} s and the passage at {end:.3f} s")
        notes += departures(pid, passages[pid]["picture"], mine)
    return errors, notes


def cmd_check(folder, n):
    data = read_json(folder / f"shots-{n}.json")
    if data is None:
        raise Refused(f"draft {n} has no shot list: shots-{n}.json")
    errors, notes = check_shots(data, folder)
    shots = [s for s in data.get("shots") or [] if isinstance(s, dict)] if isinstance(data, dict) else []
    timed = [s for s in shots if number(s.get("from_s")) and number(s.get("to_s"))]
    runs = [s for s in shots if s.get("kind") == "run"]
    over = [s for s in shots if s.get("kind") == "graphic" and "run" in s]
    alone = [s for s in timed if s.get("kind") == "graphic" and "alone" in s]
    footage = sum(s["to_s"] - s["from_s"] for s in timed if "run" in s)
    bare = sum(s["to_s"] - s["from_s"] for s in alone)
    lines = [f"shots-{n}.json: {len(shots)} shots: {len(runs)} of footage alone, {len(over)} graphics over footage, "
             f"{len(alone)} graphics alone, {sum(len(s.get('stills') or []) for s in shots)} bodies to draw; "
             f"{footage:.1f} s of footage before handles, {bare:.1f} s with none"]
    lines += [f"alone: shot {s['id']}, {s['to_s'] - s['from_s']:.1f} s over no footage: {s['alone']}" for s in alone]
    lines += [f"departs from the script: {x}" for x in notes if not x.startswith("timed on")]
    lines += [f"warning: {x}" for x in notes if x.startswith("timed on")]
    if errors:
        return "SHOTS CHECK: FAIL", lines + [f"ERROR {e}" for e in errors]
    shutil.copyfile(folder / f"shots-{n}.json", folder / "shots.json")
    return "SHOTS CHECK: PASS", lines + [f"copied shots-{n}.json to shots.json"]


def cmd_sheet(path):
    data = read_json(path)
    if data is None:
        raise Refused(f"cannot read {path}")
    lines = []
    for s in data["shots"]:
        what = f"{s['camera']} on {', '.join(s['bodies']) or 'the world'}" if "run" in s else "no footage"
        what = ("graphic over " + what if "run" in s else "graphic alone") if s["kind"] == "graphic" else what
        words = s["see"].split()
        lines.append(f"{s['id']:>7}  {minutes(s['from_s']):>7}  {s['to_s'] - s['from_s']:5.1f} s  {what:<36}  "
                     + " ".join(words[:9]) + (" ..." if len(words) > 9 else ""))
    return f"SHOTS SHEET: {path.name}, {len(data['shots'])} shots", lines


COMMANDS = {"check": (cmd_check, "SHOTS CHECK: REFUSED", 2), "sheet": (cmd_sheet, "SHOTS SHEET: REFUSED", 1)}


def main(argv):
    started = time.monotonic()
    wrong = (len(argv) < 2 or argv[1] not in COMMANDS or len(argv) != 2 + COMMANDS[argv[1]][2]
             or (COMMANDS[argv[1]][2] == 2 and not (argv[3].isdigit() and int(argv[3]) >= 1)))
    if wrong:
        print(__doc__.strip(), file=sys.stderr)
        return 2
    command, refused, count = COMMANDS[argv[1]]
    args = [Path(argv[2])] + ([int(argv[3])] if count == 2 else [])
    if count == 2 and not args[0].is_dir():
        verdict, lines = refused, [f"cannot read the film's folder {args[0]}"]
    else:
        try:
            verdict, lines = command(*args)
        except Refused as exc:
            verdict, lines = refused, exc.lines
        except (ValueError, TypeError, KeyError, IndexError, AttributeError, OSError) as exc:
            verdict, lines = refused, [f"a file is not in its form or cannot be read: {type(exc).__name__}: {exc}"]
    print(verdict)
    for line in lines:
        print(line)
    print(f"took {time.monotonic() - started:.1f} s")
    return 0 if verdict.endswith("PASS") or (argv[1] == "sheet" and verdict != refused) else 1


if __name__ == "__main__":
    for stream in (sys.stdout, sys.stderr):
        stream.reconfigure(encoding="utf-8")
    sys.exit(main(sys.argv))
