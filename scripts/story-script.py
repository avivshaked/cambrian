#!/usr/bin/env python3
"""A story's script: the captions of story.json as a page to edit, and the checks an edit must pass.

    python scripts/story-script.py render <story folder> [--force]
    python scripts/story-script.py apply  <story folder>
    python scripts/story-script.py check  <story folder> [--stats]
    python scripts/story-script.py cold   <story folder>

The script stage of the story-film flow (`.claude/skills/story-script/SKILL.md`,
`logbook/specs/story-script-brief.md`) edits the captions a writer wrote until they read as a
person's script. Editing captions inside JSON is error-prone, so the edit happens on a page:

- `render` writes `script.md` from `story.json`: the title, the arc as a note, a heading per
  chapter, and under each scene's heading one line per caption. It refuses to overwrite a
  `script.md` whose edits are not applied yet, unless `--force`.
- `apply` reads `script.md` back into `story.json`. The first apply keeps the writer's version as
  `story.draft.json`. Every apply times a changed scene from a fixed base, the story as filmed
  (`story.filmed.json`) once there is one and the writer's draft before that, so a round's result
  depends on the page alone and not on the rounds before it. A caption whose words changed is
  re-timed by the reading-pace rule (4 s at least, 14 characters a second, 1 s between captions),
  later captions move only as far as they must, and a scene too short to hold its captions is
  lengthened and named; one that needs less goes back to the base's length. Every change is a row
  of `edits.tsv` (round, scene, line, before, after). The headings are the structure: a scene
  heading may not be added, removed or renumbered.
- `check` runs the mechanical checks on `story.json` against the draft and `checks.tsv`, prints
  each finding as ERROR, WARN or INFO, and exits 1 on any ERROR. `--stats` prints the draft's and
  the script's counts side by side.
- `cold` writes `script.cold.md`, the page as the cold reader gets it: the title, the chapters and
  the captions, each scene's heading cut to its number, and no note. A viewer never sees a scene's
  run, second or subject, so the reader does not either.

The page's form: `## Chapter K: <title>` opens a chapter, `### Scene N · ...` a scene (only N is
read), each non-empty line under it is spoken text, a blank line starts a new narration paragraph
(`new_paragraph: true` on its first cue), and a line starting with `>` is a note and is not read.
`render` writes each paragraph on one line, and `parse` cuts every line into subtitle cues with
`split_cues` (2026-09-26, so that the owner and the editor read the script as prose). A `# ` line is the story's title.
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

MARK = "<!-- story-script: each line is spoken text, cut into subtitles by the tool; a blank line inside a scene " \
       "starts a new narration paragraph; a line starting with > is a note; keep the ### headings as they are. -->"


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


def chart_words(chart):
    """Every piece of a chart's text a viewer reads, as (where, text): its title, axis labels,
    series names, bar labels and marks."""
    if not isinstance(chart, dict):
        return []
    found = [("title", chart.get("title"))]
    for axis in ("x", "y"):
        if isinstance(chart.get(axis), dict):
            found.append((axis + " label", chart[axis].get("label")))
    for key, what, field in (("series", "series", "name"), ("bars", "bar", "label"), ("marks", "mark", "label")):
        for item in chart.get(key) or []:
            if isinstance(item, dict):
                found.append((what, item.get(field)))
    return [(w, str(t)) for w, t in found if t]


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
            rest = ["%s \"%s\"" % (w, t) for w, t in chart_words(chart) if w != "title"]
            if rest:
                lines.append("> chart words (edit in story.json): " + " · ".join(rest))
        paragraph = []
        for k, c in enumerate(captions_of(s)):
            if k > 0 and c.get("new_paragraph") and paragraph:
                lines += [" ".join(paragraph), ""]
                paragraph = []
            paragraph.append(" ".join(str(c.get("text", "")).split()))
        if paragraph:
            lines.append(" ".join(paragraph))
        lines.append("")
    return "\n".join(lines).rstrip() + "\n"


JOINERS = {"and", "but", "so", "or", "which", "that", "while", "because", "when", "where", "with", "to",
           "from", "at", "in", "on", "of", "as", "until", "after", "before", "than"}
CLINGERS = {"a", "an", "the", "its", "his", "her", "their", "our", "this", "that", "these", "those", "of", "to",
            "at", "in", "on", "by", "for", "from", "with", "and", "or", "but", "is", "are", "was", "were", "be",
            "each", "every", "no", "one", "two", "three"}
SENTENCE = re.compile(r"(?<=[.!?])\s+(?=[A-Z0-9\"'(\u201c\u2018])")


def split_cues(paragraph, limit=LONG):
    """A spoken paragraph cut into subtitle cues of at most `limit` characters: one sentence a cue,
    and a longer sentence cut into as few pieces as fit, each cut placed near an even share of the
    sentence, at a semicolon, comma, dash or colon where one lies near, else at a space, a space before a
    joining word ("and", "when", "with") preferred. The writer's builder and `apply` both cut with
    this, so a paragraph cuts the same way wherever it is cut."""
    cues = []
    for sentence in SENTENCE.split(" ".join(str(paragraph).split())):
        rest = sentence
        while len(rest) > limit:
            pieces = -(-len(rest) // limit)
            target = len(rest) / pieces
            best, best_score = None, None
            for i, ch in enumerate(rest[:limit + 1]):
                if ch != " " or i < limit // 4:
                    continue
                before, after = rest[:i], rest[i + 1:]
                score = abs(i - target)
                if before.endswith((",", ";", ":", "\u2014")):
                    score -= 14
                elif after.split(" ", 1)[0].lower() in JOINERS:
                    score -= 6
                if before.rsplit(" ", 1)[-1].lower() in CLINGERS:
                    score += 40
                if best_score is None or score < best_score:
                    best, best_score = i, score
            cut = best if best is not None else limit
            cues.append(rest[:cut].strip())
            rest = rest[cut:].strip()
        if rest:
            cues.append(rest)
    return cues


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
        for k, cue in enumerate(split_cues(line)):
            scenes[current].append((cue, pending_break and k == 0))
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
    old = [dict(c, text=" ".join(str(c.get("text", "")).split())) for c in old]
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

    filmed = os.path.join(folder, "story.filmed.json")
    base_path = filmed if os.path.isfile(filmed) else draft
    base = {b["n"]: b for b in scenes_of(load(base_path))}

    edits_path = os.path.join(folder, "edits.tsv")
    rnd = 1
    if os.path.isfile(edits_path):
        with io.open(edits_path, encoding="utf-8") as f:
            rounds = [int(l.split("\t", 1)[0]) for l in f.read().splitlines()[1:] if l.split("\t", 1)[0].isdigit()]
        rnd = max(rounds, default=0) + 1

    rows, lengthened, restored, changed_scenes = [], [], [], 0
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
        if [(" ".join(str(c.get("text", "")).split()), bool(c.get("new_paragraph")) and k > 0) for k, c in enumerate(old)] == \
                [(t, b) for t, b in new]:
            continue
        changed_scenes += 1
        rows += edit_rows(n, [c.get("text", "") for c in old], [t for t, _ in new])
        b = base.get(n)
        if b is None or any(b.get(k) != s.get(k) for k in ("run", "second", "station", "subject")):
            b = s   # the base holds another scene under this number: time from the story as it stands
        s["captions"], end = retime(captions_of(b), new)
        need = math.ceil(end + TAIL) if end is not None else 0
        length = float(b.get("seconds") or 0)
        was_len = float(s.get("seconds") or 0)
        if need > length:
            if need != was_len:
                lengthened.append((n, length, need))
            s["seconds"] = need
        elif was_len != length:
            restored.append((n, was_len, length))
            s["seconds"] = b.get("seconds")
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
    for n, a, b in restored:
        out("  scene %d back from %g s to %g s, the %s length" % (
            n, a, b, "filmed" if base_path == filmed else "writer's"))
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

    # A narrated story's captions follow the speech (logbook/specs/story-narration.md): readable at
    # 17 characters a second, with no least hold and no gap to keep, since the voice sets both.
    narrated = isinstance(story, dict) and bool(story.get("narration"))
    pace, least, gap = (17.0, 0.3, 0.05) if narrated else (PACE, LEAST, GAP)

    questions, shorts, in_run = {}, {}, []
    for s in scenes_of(story):
        n, caps = s["n"], captions_of(s)
        for where, text in chart_words(s.get("chart")):
            for w in words(text):
                if w in CODE_WORDS:
                    add("ERROR", n, "chart " + where, "a code name on screen: '%s' in \"%s\"" % (w, text))
                elif w in NARRATOR:
                    add("ERROR", n, "chart " + where, "a narrator in the chart: '%s' in \"%s\"" % (w, text))
                elif w in SOFT_CODE_WORDS:
                    add("WARN", n, "chart " + where, "a word the glossary may not define: '%s' in \"%s\"" % (w, text))
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
            if h < least - 1e-9 or len(text) / pace > h + 0.25:
                add("ERROR", n, line, "held %g s; it needs %.1f s at %g characters a second" % (
                    h, max(least, len(text) / pace), pace))
            if prev_end is not None and at < prev_end - 1e-6:
                add("ERROR", n, line, "starts at %g s, before the caption before it ends (%g s)" % (at, prev_end))
            elif prev_end is not None and at < prev_end + gap - 0.05:
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
            rel = re.search(r"(\d[\d.,]*) ?s (from here|from now|in)\b(?! tank)", text)
            if rel and float(rel.group(1).replace(",", "")) <= max(60.0, 2 * length):
                add("WARN", n, line, "'%s' is heard at %g s into the scene: is that time true when the line is read?" % (
                    rel.group(0), at))
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


def cmd_cold(folder):
    lines = []
    for line in render(load(os.path.join(folder, "story.json"))).splitlines():
        if line.startswith(">") or line.startswith("<!--"):
            continue
        m = re.match(r"### Scene (\d+)", line)
        if m:
            line = "### Scene " + m.group(1)
        if not line.strip() and lines and not lines[-1].strip():
            continue
        lines.append(line)
    path = os.path.join(folder, "script.cold.md")
    with io.open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines).strip() + "\n")
    out("wrote " + path)


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
    if len(args) != 2 or args[0] not in ("render", "apply", "check", "cold"):
        out("\n".join(l.strip() for l in __doc__.strip().splitlines()[2:6]))
        sys.exit(2)
    command, folder = args
    if not os.path.isfile(os.path.join(folder, "story.json")):
        sys.exit("no story.json in " + folder)
    if command == "render":
        cmd_render(folder, force)
    elif command == "apply":
        cmd_apply(folder)
    elif command == "cold":
        cmd_cold(folder)
    else:
        sys.exit(cmd_check(folder, with_stats))


if __name__ == "__main__":
    main()
