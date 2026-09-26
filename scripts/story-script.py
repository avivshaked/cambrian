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
ROUND_NUMBER = re.compile(r"\b(rounds?|experiments?)\s+(number\s+)?(\d+|(zero|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|thirteen|fourteen|fifteen|sixteen|seventeen|eighteen|nineteen|twenty|thirty|forty|fifty|sixty|seventy|eighty|ninety|hundred)([ -](one|two|three|four|five|six|seven|eight|nine))?)\b", re.I)   # a viewer has no use for the project's round numbers
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


def cmd_apply(folder, rnd=None, author="editor"):
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
    if rnd is None:
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
    with_author = new_file
    if not new_file:
        with io.open(edits_path, encoding="utf-8") as f:
            with_author = f.readline().rstrip("\n").split("\t")[-1] == "author"
    with io.open(edits_path, "a", encoding="utf-8", newline="\n") as f:
        if new_file:
            f.write("round\tscene\tline\tbefore\tafter\tauthor\n")
        for n, line, before, after in rows:
            f.write("%d\t%s\t%s\t%s\t%s%s\n" % (rnd, n, line, before.replace("\t", " "), after.replace("\t", " "),
                                                ("\t" + author) if with_author else ""))
    with io.open(os.path.join(folder, "script.md"), "w", encoding="utf-8", newline="\n") as f:
        f.write(render(story))
    out("applied round %d (%s): %d scene(s) changed, %d edit row(s) in %s" % (rnd, author, changed_scenes, len(rows), edits_path))
    for n, a, b in lengthened:
        out("  scene %d lengthened from %g s to %g s to hold its captions" % (n, a, b))
    for n, a, b in restored:
        out("  scene %d back from %g s to %g s, the %s length" % (
            n, a, b, "filmed" if base_path == filmed else "writer's"))
    total = sum(float(s.get("seconds") or 0) for s in scenes_of(story))
    out("  the scenes now run %d s (%d min %02d s), chapter cards and title not counted" % (total, total // 60, total % 60))


# The narration rules the tool can count (logbook/specs/story-writer-brief.md, "The narration"). Every one
# is an ERROR for the writer's draft and for the editor's script alike, so a rule holds whoever wrote the line.
CARDINALS = {"zero", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve",
             "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen", "twenty", "thirty",
             "forty", "fifty", "sixty", "seventy", "eighty", "ninety", "hundred", "thousand", "million", "billion",
             "dozen", "dozens", "hundreds", "thousands", "millions"}
ALWAYS_FRACTIONS = {"half", "halves", "twice", "double", "triple", "percent"}
FRACTIONS = {"third", "thirds", "quarter", "quarters", "fifth", "fifths", "sixth", "sixths", "seventh", "sevenths",
             "eighth", "eighths", "ninth", "ninths", "tenth", "tenths"}
NUMBER_JOINS = {"and", "a", "an", "point"}
IDENTIFIED_BY = {"tank", "tanks", "body", "bodies", "seed", "seeds", "line", "lines", "chapter", "chapters", "scene",
                 "scenes", "round", "rounds", "experiment", "experiments", "prediction", "predictions", "number"}
UNIT_SYMBOL = re.compile(r"\d\s*(%|m/s|m\u00b2|m\u00b3|m2|m3|kg|km|cm|mm|s|m|J|W|g)(?![\w/])")
LONG_CLOCK = re.compile(r"\b(\d[\d,]*)\s+seconds?\b")
DECIMAL = re.compile(r"(?<![\w.])\d[\d,]*\.\d+")
FORBIDDEN_INTENSIFIERS = {"simply", "just", "really", "truly", "incredibly", "remarkably", "extremely", "genuinely",
                          "honestly", "crucially", "importantly", "notably", "quietly"}
SOFT_INTENSIFIERS = {"very", "exactly", "precisely"}


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


def number_tokens(text):
    """The text as tokens for number phrases: digits with their commas dropped, and words in lower case,
    a hyphen and "per cent" read as a space and "percent"."""
    t = text.lower().replace("\u2019", "'").replace("-", " ").replace("\u2013", " ")
    t = re.sub(r"\bper\s+cent\b", "percent", t)
    return [x.replace(",", "") for x in re.findall(r"\d[\d,]*(?:\.\d+)?|[a-z]+", t)]


def number_phrases(text):
    """Every number a listener hears in the text, as (phrase tokens, identifier). A phrase is a run of
    digits, cardinal words and fraction words, joined through "and", "a", "an" and "point" ("a hundred
    and sixty", "two and a half", "zero point five", "fourteen percent"). "One" counts only before a
    fraction or a power ("one third", "one hundred"), and "a third" or "two thirds" counts where "the
    third" does not. A phrase straight after "tank", "body", "line" and the like is an identifier, not
    a number to hold."""
    tok = number_tokens(text)

    def is_num(i):
        w = tok[i]
        if w[0].isdigit() or w in CARDINALS or w in ALWAYS_FRACTIONS:
            return True
        if w in FRACTIONS:
            p = tok[i - 1] if i > 0 else ""
            return p in ("a", "an", "one") or p in CARDINALS or p[:1].isdigit()
        if w == "one":
            return i + 1 < len(tok) and (tok[i + 1] in FRACTIONS or tok[i + 1] in ("hundred", "thousand", "million", "half"))
        return False

    found, i = [], 0
    while i < len(tok):
        if not is_num(i):
            i += 1
            continue
        j = i + 1
        while j < len(tok):
            if is_num(j):
                j += 1
                continue
            k = j
            while k < len(tok) and tok[k] in NUMBER_JOINS:
                k += 1
            if k > j and k < len(tok) and is_num(k):
                j = k
                continue
            break
        found.append((tok[i:j], i > 0 and tok[i - 1] in IDENTIFIED_BY))
        i = j
    return found


def contains_run(haystack, needle):
    n = len(needle)
    return any(haystack[i:i + n] == needle for i in range(len(haystack) - n + 1))


def read_checks(folder):
    """checks.tsv as [(row number, {column: cell}, scenes)], scenes a set of scene numbers or None for every scene."""
    path = os.path.join(folder, "checks.tsv")
    if not os.path.isfile(path):
        return []
    with io.open(path, encoding="utf-8-sig") as f:
        lines = [l for l in f.read().splitlines() if l.strip()]
    head = lines[0].split("\t") if lines else []
    rows = []
    for r, line in enumerate(lines[1:], 1):
        cells = line.split("\t")
        row = {h: (cells[i] if i < len(cells) else "") for i, h in enumerate(head)}
        found = [int(x) for x in re.findall(r"\d+", row.get("scene", ""))]
        rows.append((r, row, set(found) if found else None))
    return rows


def rows_for(checks, n):
    return [row for _, row, scenes in checks if scenes is None or n in scenes]


def checked_numbers(folder):
    """{scene number or None for every scene: set of number keys} from checks.tsv's shown and exact columns."""
    pool = {}
    for _, row, scenes in read_checks(folder):
        text = " ".join(row.get(h, "") for h in ("shown", "exact", "item"))
        keys = set(numbers(text)) | set(word_numbers(text))
        for t in (scenes or [None]):
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


def paragraphs_of(scene):
    """A scene's narration paragraphs as [(the line of its first cue, text)]. A Card's first cue is its
    title and belongs to no paragraph."""
    paras, cur, first = [], [], None
    for k, c in enumerate(captions_of(scene)):
        if card_title(scene, k):
            continue
        if c.get("new_paragraph") and cur:
            paras.append((first, " ".join(cur)))
            cur = []
        if not cur:
            first = k + 1
        cur.append(" ".join(str(c.get("text", "")).split()))
    if cur:
        paras.append((first, " ".join(cur)))
    return paras


def chart_values(chart):
    """Every number a chart draws: its points, its bars' values and its marks' places."""
    vals = []
    if not isinstance(chart, dict):
        return vals
    for series in chart.get("series") or []:
        for p in (series.get("points") or []) if isinstance(series, dict) else []:
            vals += [v for v in (p if isinstance(p, (list, tuple)) else [p]) if isinstance(v, (int, float))]
    for bar in chart.get("bars") or []:
        if isinstance(bar, dict) and isinstance(bar.get("value"), (int, float)):
            vals.append(bar["value"])
    for mark in chart.get("marks") or []:
        if isinstance(mark, dict) and isinstance(mark.get("x"), (int, float)):
            vals.append(mark["x"])
    return vals


def findings(folder):
    story = load(os.path.join(folder, "story.json"))
    draft_path = os.path.join(folder, "story.draft.json")
    draft = {s["n"]: s for s in scenes_of(load(draft_path))} if os.path.isfile(draft_path) else {}
    checks = read_checks(folder)
    chapters = chapter_of(story)
    found = []

    def add(level, n, k, message):
        found.append((level, n, k, message))

    # A narrated story's captions follow the speech (logbook/specs/story-narration.md): readable at
    # 17 characters a second, with no least hold and no gap to keep, since the voice sets both.
    narrated = isinstance(story, dict) and bool(story.get("narration"))
    pace, least, gap = (17.0, 0.3, 0.05) if narrated else (PACE, LEAST, GAP)

    for r, row, _ in checks:
        if not row.get("source", "").strip():
            add("ERROR", 0, "checks.tsv row %d" % r, "no source for \"%s\"" % row.get("shown", ""))

    def unchecked(n, where, text, shown_rows):
        """A number on screen is checked when its words stand in the `shown` cell of a checks.tsv row
        for its scene (the writer's brief: the exact value goes in checks.tsv beside the words shown)."""
        cells = [number_tokens(row.get("shown", "")) for row in shown_rows]
        for phrase, ident in number_phrases(text):
            if ident:
                continue
            if not any(contains_run(c, phrase) for c in cells):
                add("ERROR", n, where, "'%s' has no row in checks.tsv whose shown words hold it" % " ".join(phrase))

    numbered = "an experiment's number means nothing to a viewer; name it by what it asked, or an earlier one as the experiment before this one"
    title = str(story.get("title", "")) if isinstance(story, dict) else ""
    for m in ROUND_NUMBER.finditer(title):
        add("ERROR", 0, "title", "'%s' in the title: %s" % (m.group(0), numbered))
    unchecked(0, "title", title, [row for _, row, _ in checks])
    questions, shorts, in_run = {}, {}, []
    for s in scenes_of(story):
        n, caps = s["n"], captions_of(s)
        here_rows = rows_for(checks, n)
        chart_rows = [row for row in here_rows if row.get("item", "").strip().lower().startswith("chart")]
        on_screen = [("chapter", str(s.get("chapter") or ""))]
        on_screen += [(k + 1, c.get("text", "")) for k, c in enumerate(caps)]
        on_screen += [("chart " + where, text) for where, text in chart_words(s.get("chart"))]
        for where, text in on_screen:
            for m in ROUND_NUMBER.finditer(text):
                add("ERROR", n, where, "'%s' on screen: %s" % (m.group(0), numbered))
        if s.get("chapter"):
            unchecked(n, "chapter", str(s["chapter"]), here_rows)
        for where, text in chart_words(s.get("chart")):
            unchecked(n, "chart " + where, text, chart_rows)
            for w in words(text):
                if w in CODE_WORDS:
                    add("ERROR", n, "chart " + where, "a code name on screen: '%s' in \"%s\"" % (w, text))
                elif w in NARRATOR:
                    add("ERROR", n, "chart " + where, "a narrator in the chart: '%s' in \"%s\"" % (w, text))
                elif w in SOFT_CODE_WORDS:
                    add("WARN", n, "chart " + where, "a word the glossary may not define: '%s' in \"%s\"" % (w, text))
        if isinstance(s.get("chart"), dict):
            pool = set()
            for row in chart_rows:
                pool |= set(numbers(row.get("exact", "") + " " + row.get("shown", "")))
            missing = [number_key(repr(float(v))) for v in chart_values(s["chart"])]
            missing = [v for v in missing if v not in pool]
            if missing:
                add("ERROR", n, "chart", "the chart draws %d value(s) that no chart row of checks.tsv holds in its exact "
                    "value (%s%s); a line chart's points go in full" % (len(missing), ", ".join(missing[:4]),
                                                                        ", ..." if len(missing) > 4 else ""))

        # The narration, paragraph by paragraph (rules 2, 3, 5, 7 and 9).
        paras = paragraphs_of(s)
        for p, (line, text) in enumerate(paras):
            unchecked(n, line, text, here_rows)
            held = [ph for ph, ident in number_phrases(text) if not ident]
            if len(held) > 2:
                add("ERROR", n, line, "%d numbers in one paragraph (%s); one or two a paragraph at most" % (
                    len(held), "; ".join(" ".join(ph) for ph in held)))
            ss = sentences(text)
            run = []
            for x in ss + [None]:
                if x is not None and len(x.split()) < 10:
                    run.append(x)
                    continue
                if len(run) >= 3:
                    add("ERROR", n, line, "%d short sentences in a row, a list and not speech: %s" % (
                        len(run), " / ".join("'%s'" % y for y in run)))
                run = []
            for a in range(len(ss) - 2):
                heads = [" ".join(words(x)[:2]) for x in ss[a:a + 3]]
                if heads[0] and heads[0] == heads[1] == heads[2]:
                    add("ERROR", n, line, "three sentences in a row open '%s': a list read aloud" % heads[0])
                    break
            if ss and len(ss) > 1 and len(ss[-1].split()) <= 3:
                add("ERROR", n, line, "the paragraph ends on a short sentence, '%s', which reads as a slogan" % ss[-1])
            if p == 0 and ss:
                first = ss[0]
                if re.match(r"^(in\s+)?(tank|seed)s?\s+\S+?[,.:]", first, re.I) or \
                        re.match(r"^(at\s+)?\d[\d,.]*\s*(s|seconds)\b", first, re.I) or \
                        (len(first.split()) <= 5 and re.search(r"\d", first)):
                    add("ERROR", n, line, "the scene opens on a label, '%s'; the picture says where we are" % first)
            for m in UNIT_SYMBOL.finditer(text):
                add("ERROR", n, line, "'%s': a unit is said in words in the narration" % m.group(0))
            for m in DECIMAL.finditer(text):
                add("ERROR", n, line, "'%s': a listener cannot hold a decimal; round it" % m.group(0))
            for m in LONG_CLOCK.finditer(text):
                if float(m.group(1).replace(",", "")) >= 120:
                    add("ERROR", n, line, "'%s': give the tank's time as a listener's time, in minutes or hours" % m.group(0))

        length = float(s.get("seconds") or 0)
        prev_end = None
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
            for w in words(text):
                if w in CODE_WORDS:
                    add("ERROR", n, line, "a code name on screen: '%s' (the glossary has the plain word)" % w)
                elif w in SOFT_CODE_WORDS:
                    add("WARN", n, line, "a word the glossary may not define: '%s'" % w)
                elif w in NARRATOR:
                    add("ERROR", n, line, "a narrator in the text: '%s' (the voice is detached)" % w)
                elif w in FORBIDDEN_INTENSIFIERS:
                    add("ERROR", n, line, "an intensifier: '%s'" % w)
                elif w in SOFT_INTENSIFIERS:
                    add("WARN", n, line, "an intensifier: '%s'" % w)
            for x in sentences(text):
                if re.search(r",\s*(and\s+)?not\s+[^,]+[.!]$", x) or re.search(r"\brather than\b[^,]*[.!]$", x):
                    add("ERROR", n, line, "a closing contrast ('X, not Y'): '%s'" % x)
            rel = re.search(r"(\d[\d.,]*) ?s (from here|from now|in)\b(?! tank)", text)
            if rel and float(rel.group(1).replace(",", "")) <= max(60.0, 2 * length):
                add("WARN", n, line, "'%s' is heard at %g s into the scene: is that time true when the line is read?" % (
                    rel.group(0), at))
            if "which is why" in text.lower():
                add("WARN", n, line, "'which is why'")
            if ":" in re.sub(r"\d:\d", "", text) and not card_title(s, k):
                add("ERROR", n, line, "a colon: say it inside the sentence, never as a definition after a colon")
            if text.rstrip().endswith("?"):
                questions.setdefault(chapters[n], []).append((n, line))
                if k == len(caps) - 1:
                    add("ERROR", n, line, "a question as the scene's last line")
            for sentence in sentences(text):
                if len(sentence.split()) <= 3 and not card_title(s, k):
                    shorts.setdefault(chapters[n], []).append((n, line, sentence))
            if "in the run" in text.lower():
                in_run.append((n, line))
        if draft:
            was = draft.get(n, s)
            was_text = " ".join(c.get("text", "") for c in captions_of(was)) + " " + str(was.get("chapter", ""))
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
        out("%-5s  %s: %s" % (level, "title" if n == 0 else "scene %s, line %s" % (n, line), message))
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


def cmd_revert(folder, scenes, rnd=None):
    """The writer's words back in the scenes named (0 for the title), from story.draft.json, applied as
    a round of their own so that edits.tsv says who wrote them. The flow reverts a scene the editor
    could not bring through the check or the fact checker, since the writer's draft passed both."""
    draft = load(os.path.join(folder, "story.draft.json"))
    story = load(os.path.join(folder, "story.json"))
    page = render(story).splitlines()
    by_n = {s["n"]: s for s in scenes_of(draft)}
    want = render(draft).splitlines()

    def block(lines, n):
        start = next((i for i, l in enumerate(lines) if re.match(r"### Scene %d\b" % n, l)), None)
        if start is None:
            return None, None
        end = next((i for i in range(start + 1, len(lines)) if lines[i].startswith("#")), len(lines))
        return start, end

    for n in sorted(scenes, reverse=True):
        if n == 0:
            page[0] = "# " + str(draft.get("title", "") if isinstance(draft, dict) else "")
            continue
        if n not in by_n:
            sys.exit("scene %d is not in the writer's draft" % n)
        a, b = block(page, n)
        c, d = block(want, n)
        if a is None or c is None:
            sys.exit("scene %d has no heading on the page" % n)
        page[a + 1:b] = want[c + 1:d]
    with io.open(os.path.join(folder, "script.md"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(page).rstrip() + "\n")
    cmd_apply(folder, rnd, author="revert")


def cmd_edits(folder, rnd, out_path):
    """One round's rows of edits.tsv, for that round's fact checker."""
    with io.open(os.path.join(folder, "edits.tsv"), encoding="utf-8") as f:
        lines = f.read().splitlines()
    keep = [lines[0]] + [l for l in lines[1:] if l.split("\t", 1)[0] == str(rnd)]
    with io.open(out_path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(keep) + "\n")
    out("wrote %s (%d row(s) of round %d)" % (out_path, len(keep) - 1, rnd))


def cmd_cold(folder, out_path=None):
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
    path = out_path or os.path.join(folder, "script.cold.md")
    with io.open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines).strip() + "\n")
    out("wrote " + path)


def main():
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8", errors="replace")
        except (AttributeError, ValueError):
            pass
    import argparse
    ap = argparse.ArgumentParser(description=__doc__.strip().splitlines()[0])
    ap.add_argument("command", choices=("render", "apply", "check", "cold", "revert", "edits"))
    ap.add_argument("folder")
    ap.add_argument("--force", action="store_true")
    ap.add_argument("--stats", action="store_true")
    ap.add_argument("--round", type=int)
    ap.add_argument("--author", default="editor")
    ap.add_argument("--scenes", default="")
    ap.add_argument("--out")
    a = ap.parse_args()
    folder = a.folder
    if a.command == "render":
        cmd_render(folder, a.force)
    elif a.command == "apply":
        cmd_apply(folder, a.round, a.author)
    elif a.command == "check":
        sys.exit(cmd_check(folder, a.stats))
    elif a.command == "cold":
        cmd_cold(folder, a.out)
    elif a.command == "revert":
        cmd_revert(folder, [int(x) for x in re.findall(r"\d+", a.scenes)], a.round)
    elif a.command == "edits":
        if a.round is None or not a.out:
            sys.exit("edits needs --round N and --out <file>")
        cmd_edits(folder, a.round, a.out)


if __name__ == "__main__":
    main()
