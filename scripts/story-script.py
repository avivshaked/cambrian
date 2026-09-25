#!/usr/bin/env python3
"""A story's script: the captions of story.json as a page to edit, and the checks an edit must pass.

    python scripts/story-script.py render <story folder> [--force]
    python scripts/story-script.py apply  <story folder>
    python scripts/story-script.py check  <story folder> [--stats]

The script stage of the story-film flow (`.claude/skills/story-script/SKILL.md`,
`logbook/specs/story-script-brief.md`) edits the captions a writer wrote until they read as a
person's script. Editing captions inside JSON is error-prone, so the edit happens on a page:

- `render` writes `script.md` from `story.json`: the title, the arc as a note, a heading per
  chapter, and under each scene's heading one line per caption. It refuses to overwrite a
  `script.md` whose edits are not applied yet, unless `--force`.
- `apply` reads `script.md` back into `story.json`. The first apply keeps the writer's version as
  `story.draft.json`. A caption whose words changed is re-timed by the reading-pace rule (4 s at
  least, 14 characters a second, 1 s between captions), later captions move only as far as they
  must, and a scene too short to hold its captions is lengthened and named. Every change is a row
  of `edits.tsv` (round, scene, line, before, after). The headings are the structure: a scene
  heading may not be added, removed or renumbered.
- `check` runs the mechanical checks on `story.json` against the draft and `checks.tsv`, prints
  each finding as ERROR, WARN or INFO, and exits 1 on any ERROR. `--stats` prints the draft's and
  the script's counts side by side.

The page's form: `## Chapter K: <title>` opens a chapter, `### Scene N · ...` a scene (only N is
read), each non-empty line under it is a caption, a blank line between two captions starts a new
narration paragraph (`new_paragraph: true` on the second), and a line starting with `>` is a note
and is not read. A `# ` line is the story's title.
"""
import io
import json
import math
import os
import re
import shutil
import sys

PACE = 14.0          # characters a second a caption is held for
LEAST = 4.0          # the shortest hold, s
GAP = 1.0            # between two captions, s
PARAGRAPH_GAP = 1.0  # more between two paragraphs of a scene, s
FIRST = 0.5          # the first caption's offset, s
TAIL = 0.5           # from the last caption's end to the scene's end, s
LONG, TOO_LONG = 70, 84

CODE_WORDS = ["bf", "pool", "trickle", "clade", "clades", "absorptive", "photosynthetic"]
SOFT_CODE_WORDS = ["genome", "genomes", "phenotype", "checkpoint", "snapshot", "lineage", "organism"]
NARRATOR = ["i", "i'm", "i've", "i'd", "i'll", "me", "my", "mine", "we", "we're", "we've", "we'd", "our", "ours", "us"]
INTENSIFIERS = ["very", "really", "truly", "incredibly", "remarkably", "extremely", "exactly", "precisely",
                "quietly", "simply", "genuinely", "honestly", "crucially", "importantly", "notably", "just"]
NUMBER_WORDS = {"two": 2, "three": 3, "four": 4, "five": 5, "six": 6, "seven": 7, "eight": 8, "nine": 9,
                "ten": 10, "eleven": 11, "twelve": 12, "twenty": 20, "thirty": 30, "forty": 40, "fifty": 50,
                "hundred": 100, "thousand": 1000}
NUMBER = re.compile(r"(?<![\w.])\d[\d,]*(?:\.\d+)?")

MARK = "<!-- story-script: one line is one caption; a blank line inside a scene starts a new narration " \
       "paragraph; a line starting with > is a note; keep the ### headings as they are. -->"


def out(*a):
    print(*a)


def load(path):
    with io.open(path, encoding="utf-8-sig") as f:
        return json.load(f)


def save(path, data):
    with io.open(path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, ensure_ascii=False, indent=1)
        f.write("\n")


def scenes_of(story):
    return story["scenes"] if isinstance(story, dict) else story


def hold(text):
    """A caption's hold at the reading pace, to the half second."""
    return max(LEAST, math.ceil(len(text) / PACE * 2.0) / 2.0)


def clock(s):
    return "{:,}".format(int(round(s))) if s is not None else "?"


def captions_of(scene):
    """A scene's captions as dicts, whatever form the writer used."""
    given = scene.get("captions") or []
    if isinstance(given, str):
        given = [given]
    return [dict(c) if isinstance(c, dict) else {"text": str(c)} for c in given]


def render(story):
    """The page for a story: every caption a line, under its scene and chapter."""
    title = story.get("title", "") if isinstance(story, dict) else ""
    lines = ["# " + title, "", MARK, ""]
    arc = story.get("arc") if isinstance(story, dict) else None
    if arc:
        lines += ["> arc: " + " ".join(str(arc).split()), ""]
    chapter = 0
    for s in scenes_of(story):
        if s.get("chapter"):
            chapter += 1
            lines += ["## Chapter %d: %s" % (chapter, s["chapter"].strip()), ""]
        subject = " ".join(str(s.get("subject", "")).split())
        lines.append("### Scene %d · %s · %s · %s at %s s · %g s" % (
            s["n"], s.get("station", "?"), subject[:70], s.get("run", "?"), clock(s.get("second")), s.get("seconds") or 0))
        if s.get("why"):
            lines.append("> why: " + " ".join(str(s["why"]).split()))
        chart = s.get("chart")
        if isinstance(chart, dict):
            lines.append("> chart: %s (%s, %s to %s s)" % (chart.get("title", ""), chart.get("place", "corner"),
                                                          chart.get("at", "?"), chart.get("until", "end")))
        for k, c in enumerate(captions_of(s)):
            if k > 0 and c.get("new_paragraph"):
                lines.append("")
            lines.append(" ".join(str(c.get("text", "")).split()))
        lines.append("")
    return "\n".join(lines).rstrip() + "\n"


def parse(text):
    """A page back to {title, order, scenes {n: [(text, new_paragraph)]}, chapters {n: title}}."""
    title, order, scenes, chapters = None, [], {}, {}
    current, pending_chapter, pending_break = None, None, False
    for number, raw in enumerate(text.splitlines(), 1):
        line = raw.strip()
        if line.startswith("<!--") or line.startswith(">"):
            continue
        if line.startswith("### "):
            m = re.match(r"###\s+Scene\s+(\d+)\b", line)
            if not m:
                sys.exit("script.md line %d: a scene heading must start '### Scene N': %s" % (number, line))
            current = int(m.group(1))
            if current in scenes:
                sys.exit("script.md line %d: scene %d appears twice" % (number, current))
            order.append(current)
            scenes[current] = []
            if pending_chapter is not None:
                chapters[current] = pending_chapter
                pending_chapter = None
            pending_break = False
            continue
        if line.startswith("## "):
            m = re.match(r"##\s+Chapter\s+\d+\s*:\s*(.+)$", line)
            if not m:
                sys.exit("script.md line %d: a chapter heading must read '## Chapter K: title': %s" % (number, line))
            pending_chapter = m.group(1).strip()
            current = None
            continue
        if line.startswith("# "):
            title = line[2:].strip()
            continue
        if not line:
            if current is not None and scenes[current]:
                pending_break = True
            continue
        if current is None:
            sys.exit("script.md line %d: a caption outside any scene: %s" % (number, line))
        scenes[current].append((" ".join(line.split()), pending_break))
        pending_break = False
    return {"title": title, "order": order, "scenes": scenes, "chapters": chapters}


def page_of(story):
    return parse(render(story))


def in_sync(folder):
    """Whether script.md says what story.json says (captions, breaks, chapters, title)."""
    path = os.path.join(folder, "script.md")
    if not os.path.isfile(path):
        return False
    with io.open(path, encoding="utf-8") as f:
        return parse(f.read()) == page_of(load(os.path.join(folder, "story.json")))


def cmd_render(folder, force):
    story = load(os.path.join(folder, "story.json"))
    path = os.path.join(folder, "script.md")
    if os.path.isfile(path) and not force and not in_sync(folder):
        sys.exit("script.md holds edits that story.json does not: apply them first, or pass --force to lose them")
    with io.open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(render(story))
    n = sum(len(captions_of(s)) for s in scenes_of(story))
    out("script: %s (%d scenes, %d captions)" % (path, len(scenes_of(story)), n))


def origins(old, new):
    """Each new caption's old caption, matched by content: the same line, or the line it replaced; None if added."""
    import difflib
    match = [None] * len(new)
    sm = difflib.SequenceMatcher(a=[c.get("text", "") for c in old], b=[t for t, _ in new], autojunk=False)
    for tag, i1, i2, j1, j2 in sm.get_opcodes():
        if tag in ("equal", "replace"):
            for k in range(min(i2 - i1, j2 - j1)):
                match[j1 + k] = old[i1 + k]
    return match


def retime(old, new):
    """
    New captions from (text, new_paragraph) pairs: an unchanged caption keeps its offset and hold, a
    changed one takes the reading pace, and each keeps its old offset unless the one before it now
    ends later. A caption removed leaves its time empty; the picture holds.
    """
    result, prev_end = [], None
    matched = origins(old, new)
    for k, (text, brk) in enumerate(new):
        o = matched[k]
        h = hold(text)
        if o and o.get("text") == text and o.get("seconds"):
            h = max(h, float(o["seconds"]))
        earliest = FIRST if prev_end is None else prev_end + GAP + (PARAGRAPH_GAP if brk else 0.0)
        at = float(o["at"]) if o and o.get("at") is not None else earliest
        at = max(at, earliest)
        c = {k2: v for k2, v in (o or {}).items() if k2 not in ("at", "text", "seconds", "new_paragraph")}
        c.update({"at": round(at, 2), "text": text})
        if abs(h - LEAST) > 1e-9:
            c["seconds"] = h
        if brk and k > 0:
            c["new_paragraph"] = True
        result.append(c)
        prev_end = at + h
    return result, prev_end


def edit_rows(n, old_texts, new_texts):
    import difflib
    rows = []
    sm = difflib.SequenceMatcher(a=old_texts, b=new_texts, autojunk=False)
    for tag, i1, i2, j1, j2 in sm.get_opcodes():
        if tag == "equal":
            continue
        for k in range(max(i2 - i1, j2 - j1)):
            before = old_texts[i1 + k] if i1 + k < i2 else ""
            after = new_texts[j1 + k] if j1 + k < j2 else ""
            rows.append((n, (j1 + k + 1) if j1 + k < j2 else "-", before, after))
    return rows


def cmd_apply(folder):
    story_path = os.path.join(folder, "story.json")
    story = load(story_path)
    with io.open(os.path.join(folder, "script.md"), encoding="utf-8") as f:
        page = parse(f.read())
    was = page_of(story)
    if page["order"] != was["order"]:
        sys.exit("the scene headings changed (script %s, story %s): scenes are added, removed or moved at the "
                 "writer's stage, not in the script" % (page["order"], was["order"]))
    if set(page["chapters"]) != set(was["chapters"]):
        sys.exit("the chapters changed place (script opens chapters at scenes %s, the story at %s): a chapter is "
                 "an 8 s card in the film, and is added or removed at the writer's stage" % (
                     sorted(page["chapters"]), sorted(was["chapters"])))
    draft = os.path.join(folder, "story.draft.json")
    if not os.path.isfile(draft):
        shutil.copyfile(story_path, draft)
        out("kept the writer's version as " + draft)

    edits_path = os.path.join(folder, "edits.tsv")
    rnd = 1
    if os.path.isfile(edits_path):
        with io.open(edits_path, encoding="utf-8") as f:
            rounds = [int(l.split("\t", 1)[0]) for l in f.read().splitlines()[1:] if l.split("\t", 1)[0].isdigit()]
        rnd = max(rounds, default=0) + 1

    rows, lengthened, changed_scenes = [], [], 0
    if page["title"] and isinstance(story, dict) and page["title"] != story.get("title"):
        rows.append((0, "title", story.get("title", ""), page["title"]))
        story["title"] = page["title"]
    renamed = {}
    for s in scenes_of(story):
        n = s["n"]
        if n in page["chapters"] and page["chapters"][n] != s.get("chapter", "").strip():
            rows.append((n, "chapter", s["chapter"], page["chapters"][n]))
            renamed[s["chapter"]] = page["chapters"][n]
            s["chapter"] = page["chapters"][n]
        if s.get("act") in renamed:
            s["act"] = renamed[s["act"]]
        old = captions_of(s)
        new = page["scenes"][n]
        if [(c.get("text"), bool(c.get("new_paragraph")) and k > 0) for k, c in enumerate(old)] == \
                [(t, b) for t, b in new]:
            continue
        changed_scenes += 1
        rows += edit_rows(n, [c.get("text", "") for c in old], [t for t, _ in new])
        s["captions"], end = retime(old, new)
        need = math.ceil(end + TAIL) if end is not None else 0
        length = float(s.get("seconds") or 0)
        if need > length:
            lengthened.append((n, length, need))
            s["seconds"] = need
    save(story_path, story)
    new_file = not os.path.isfile(edits_path)
    with io.open(edits_path, "a", encoding="utf-8", newline="\n") as f:
        if new_file:
            f.write("round\tscene\tline\tbefore\tafter\n")
        for n, line, before, after in rows:
            f.write("%d\t%s\t%s\t%s\t%s\n" % (rnd, n, line, before.replace("\t", " "), after.replace("\t", " ")))
    with io.open(os.path.join(folder, "script.md"), "w", encoding="utf-8", newline="\n") as f:
        f.write(render(story))
    out("applied round %d: %d scene(s) changed, %d edit row(s) in %s" % (rnd, changed_scenes, len(rows), edits_path))
    for n, a, b in lengthened:
        out("  scene %d lengthened from %g s to %g s to hold its captions" % (n, a, b))
    total = sum(float(s.get("seconds") or 0) for s in scenes_of(story))
    out("  the scenes now run %d s (%d min %02d s), chapter cards and title not counted" % (total, total // 60, total % 60))


def number_key(token):
    v = float(token.replace(",", ""))
    return ("%.6f" % v).rstrip("0").rstrip(".")


def numbers(text):
    return [number_key(t) for t in NUMBER.findall(text)]


def word_numbers(text):
    return [str(NUMBER_WORDS[w]) for w in re.findall(r"[a-z]+", text.lower()) if w in NUMBER_WORDS]


def words(text):
    return re.findall(r"[a-z']+", text.lower().replace("\u2019", "'"))


def sentences(text):
    return [p.strip() for p in re.split(r"(?<=[.!?])\s+", text.strip()) if p.strip()]


def checked_numbers(folder):
    """{scene number or None for every scene: set of number keys} from checks.tsv's shown and exact columns."""
    pool = {}
    path = os.path.join(folder, "checks.tsv")
    if not os.path.isfile(path):
        return pool
    with io.open(path, encoding="utf-8-sig") as f:
        lines = f.read().splitlines()
    head = lines[0].split("\t") if lines else []
    col = {h: i for i, h in enumerate(head)}
    for line in lines[1:]:
        cells = line.split("\t")
        scene = cells[col["scene"]] if "scene" in col and col["scene"] < len(cells) else ""
        text = " ".join(cells[col[h]] for h in ("shown", "exact", "item") if h in col and col[h] < len(cells))
        keys = set(numbers(text)) | set(word_numbers(text))
        targets = [int(x) for x in re.findall(r"\d+", scene)] or [None]
        for t in targets:
            pool.setdefault(t, set()).update(keys)
    return pool


def chapter_of(story):
    """{scene n: chapter index} over the story's order."""
    out_, k = {}, 0
    for s in scenes_of(story):
        if s.get("chapter"):
            k += 1
        out_[s["n"]] = k
    return out_


def card_title(s, k):
    return k == 0 and str(s.get("station", "")).lower() == "card"


def findings(folder):
    story = load(os.path.join(folder, "story.json"))
    draft_path = os.path.join(folder, "story.draft.json")
    draft = {s["n"]: s for s in scenes_of(load(draft_path))} if os.path.isfile(draft_path) else {}
    pool = checked_numbers(folder)
    chapters = chapter_of(story)
    found = []

    def add(level, n, k, message):
        found.append((level, n, k, message))

    questions, shorts, in_run = {}, {}, []
    for s in scenes_of(story):
        n, caps = s["n"], captions_of(s)
        length = float(s.get("seconds") or 0)
        prev_end = None
        here = pool.get(n, set()) | pool.get(None, set())
        was = draft.get(n, s)
        was_text = " ".join(c.get("text", "") for c in captions_of(was)) + " " + str(was.get("chapter", ""))
        here |= set(numbers(was_text)) | set(word_numbers(was_text))
        for k, c in enumerate(caps):
            text, line = c.get("text", ""), k + 1
            at = float(c.get("at", FIRST if k == 0 else (prev_end or 0) + GAP))
            h = float(c.get("seconds") or LEAST)
            if len(text) > TOO_LONG:
                add("ERROR", n, line, "%d characters; keep a caption under about %d" % (len(text), LONG))
            elif len(text) > LONG:
                add("WARN", n, line, "%d characters; keep a caption under about %d" % (len(text), LONG))
            if h < LEAST - 1e-9 or len(text) / PACE > h + 0.25:
                add("ERROR", n, line, "held %g s; it needs %g s at %g characters a second" % (h, hold(text), PACE))
            if prev_end is not None and at < prev_end - 1e-6:
                add("ERROR", n, line, "starts at %g s, before the caption before it ends (%g s)" % (at, prev_end))
            elif prev_end is not None and at < prev_end + GAP - 0.05:
                add("WARN", n, line, "only %.1f s after the caption before it" % (at - prev_end))
            if length and at + h > length + 1e-6:
                add("ERROR", n, line, "runs to %g s, past the scene's %g s" % (at + h, length))
            prev_end = at + h
            ws = words(text)
            for w in ws:
                if w in CODE_WORDS:
                    add("ERROR", n, line, "a code name on screen: '%s' (the glossary has the plain word)" % w)
                elif w in SOFT_CODE_WORDS:
                    add("WARN", n, line, "a word the glossary may not define: '%s'" % w)
                elif w in NARRATOR:
                    add("ERROR", n, line, "a narrator in the text: '%s' (the voice is detached)" % w)
                elif w in INTENSIFIERS:
                    add("WARN", n, line, "an intensifier: '%s'" % w)
            if re.search(r",\s*(and\s+)?not\s+[^,]+[.!]?$", text) or re.search(r"\brather than\b[^,]*[.!]?$", text):
                add("WARN", n, line, "a closing contrast ('X, not Y')")
            if "which is why" in text.lower():
                add("WARN", n, line, "'which is why'")
            if ":" in re.sub(r"\d:\d", "", text) and not card_title(s, k):
                add("WARN", n, line, "a colon: can the picture explain it instead of a definition?")
            if text.rstrip().endswith("?"):
                questions.setdefault(chapters[n], []).append((n, line))
                if k == len(caps) - 1:
                    add("ERROR", n, line, "a question as the scene's last line")
            for sentence in sentences(text):
                if len(sentence.split()) <= 3 and not card_title(s, k):
                    shorts.setdefault(chapters[n], []).append((n, line, sentence))
                    if k == len(caps) - 1 and sentence == sentences(text)[-1]:
                        add("WARN", n, line, "the scene ends on a short sentence, '%s', which reads as a slogan" % sentence)
            if "in the run" in text.lower():
                in_run.append((n, line))
            for key in numbers(text):
                if key not in here:
                    add("ERROR", n, line, "the number %s has no row in checks.tsv and was not in the writer's scene" % key)
            for key in word_numbers(text):
                if key not in here:
                    add("WARN", n, line, "the number word for %s has no row in checks.tsv; check it" % key)
        if draft:
            now_text = " ".join(c.get("text", "") for c in caps)
            gone = set(numbers(was_text)) - set(numbers(now_text + " " + str(s.get("chapter", ""))))
            for key in sorted(gone):
                add("INFO", n, "-", "the number %s left the screen" % key)
    for ch, qs in questions.items():
        if len(qs) > 1:
            add("ERROR", qs[1][0], qs[1][1], "%d questions in chapter %d; one at most" % (len(qs), ch))
    for ch, ss in shorts.items():
        if len(ss) > 1:
            add("WARN", ss[1][0], ss[1][1], "%d sentences of three words or fewer in chapter %d (%s); one fragment a "
                "chapter at most" % (len(ss), ch, "; ".join("'%s'" % x[2] for x in ss)))
    if len(in_run) > 1:
        add("INFO", in_run[1][0], in_run[1][1], "'in the run' said %d times (scenes %s); once at the start, then only "
            "where it matters" % (len(in_run), ", ".join(str(x[0]) for x in in_run)))
    return found


def stats(story):
    caps = [c.get("text", "") for s in scenes_of(story) for c in captions_of(s)]
    n = max(1, len(caps))
    colon = sum(1 for s in scenes_of(story) for k, c in enumerate(captions_of(s))
                if ":" in re.sub(r"\d:\d", "", c.get("text", "")) and not card_title(s, k))
    return [
        ("captions", len(caps)),
        ("mean length, characters", round(sum(len(c) for c in caps) / n, 1)),
        ("captions with a number, %", round(100.0 * sum(1 for c in caps if NUMBER.search(c)) / n)),
        ("numbers on screen", sum(len(numbers(c)) for c in caps)),
        ("colons", colon),
        ("narrator words (I, we, our)", sum(1 for c in caps for w in words(c) if w in NARRATOR)),
        ("questions", sum(1 for c in caps if c.rstrip().endswith("?"))),
        ("sentences of three words or fewer", sum(1 for c in caps for x in sentences(c) if len(x.split()) <= 3)),
        ("'in the run'", sum(1 for c in caps if "in the run" in c.lower())),
        ("scenes' seconds", round(sum(float(s.get("seconds") or 0) for s in scenes_of(story)))),
    ]


def cmd_check(folder, with_stats):
    found = findings(folder)
    order = {"ERROR": 0, "WARN": 1, "INFO": 2}
    for level, n, line, message in sorted(found, key=lambda f: (order[f[0]], f[1], str(f[2]))):
        out("%-5s  scene %s, line %s: %s" % (level, n, line, message))
    counts = {lv: sum(1 for f in found if f[0] == lv) for lv in order}
    if with_stats:
        story = load(os.path.join(folder, "story.json"))
        draft_path = os.path.join(folder, "story.draft.json")
        before = stats(load(draft_path)) if os.path.isfile(draft_path) else None
        now = stats(story)
        out("")
        out("%-36s %10s %10s" % ("", "writer's" if before else "", "script"))
        for i, (name, value) in enumerate(now):
            out("%-36s %10s %10s" % (name, before[i][1] if before else "", value))
    out("check: %d error(s), %d warning(s), %d note(s)" % (counts["ERROR"], counts["WARN"], counts["INFO"]))
    return 1 if counts["ERROR"] else 0


def main():
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8", errors="replace")
        except (AttributeError, ValueError):
            pass
    args = sys.argv[1:]
    force = "--force" in args
    with_stats = "--stats" in args
    args = [a for a in args if a not in ("--force", "--stats")]
    if len(args) != 2 or args[0] not in ("render", "apply", "check"):
        out(__doc__.strip().splitlines()[2:5] and "\n".join(l.strip() for l in __doc__.strip().splitlines()[2:5]))
        sys.exit(2)
    command, folder = args
    if not os.path.isfile(os.path.join(folder, "story.json")):
        sys.exit("no story.json in " + folder)
    if command == "render":
        cmd_render(folder, force)
    elif command == "apply":
        cmd_apply(folder)
    else:
        sys.exit(cmd_check(folder, with_stats))


if __name__ == "__main__":
    main()
