#!/usr/bin/env python3
"""The checks of the prose and script steps, for the create-story-video skill.

    python script-check.py prose <prose>     check the narration's prose against the rules that can be
                                             counted; the first line is PROSE CHECK: PASS or FAIL
    python script-check.py script <script>   check a script against script-schema.md and the approved
                                             prose; the first line is SCRIPT CHECK: PASS or FAIL
    python script-check.py say <file>        print the spoken words of a prose or a script file alone,
                                             one paragraph for each paragraph or passage, a given
                                             name without its braces
    python script-check.py json <script>     print a script as data, in the shape script-schema.md gives
    python script-check.py sample <facts> [--last]
                                             print the facts the source checker checks: up to 15 of
                                             facts.md's, spread through it, or through its last section

A prose file is prose.md or one of its drafts, and a script file script.md or one of its drafts, in the
film's folder beside facts.md, story.md and, for a script, prose.md. Exit status: 0 on a pass, 1 on a
fail or a file that does not follow its form, 2 on a missing file or a wrong command.
"""

import json
import re
import sys
from pathlib import Path

# prose.md under "The check" and script-schema.md under "What the check refuses" give the research behind the limits.
WORDS_MIN, WORDS_MAX = 400, 1000  # the check refuses outside these
WORDS_TARGET = 800                # what a draft aims at; never cut good prose only to reach it
PACE = 145                        # words a minute, for the spoken time
SENTENCE_MAX = 25                 # words in one spoken sentence
TEXT_MAX = 4                      # words in one Text: line
SAME_LENGTH = 1                   # three sentences in a row this close in length fail
SHORT_PAIR = 8                    # two sentences this short, one negative, are a split corrective

FIELDS = ["Say", "Facts", "Picture", "See", "Run", "Bodies", "Seconds", "From", "Text", "Hold"]
REPEATABLE = {"Say", "Text"}
FIELD_LINE = re.compile(r"^(" + "|".join(FIELDS) + r"):(.*)$")
PLACE = re.compile(r"\[facts\.md:L(\d+)(?:-(\d+))?\]")
NUMBER = r"\d+(?:\.\d+)?"
GIVEN = re.compile(r"\{\{([^{}]*)\}\}")     # a name we give, which the fact checker leaves alone
NAME = re.compile(r"[A-Z][A-Za-z'\u2019-]*(?: [A-Za-z][A-Za-z'\u2019-]*){0,2}")

TELLS = [
    (r"\b(?:testament|pivotal|crucial|vital|remarkable|fascinating|groundbreaking|profound|vibrant|"
     r"intricate|meticulous|tapestry|nestled|renowned)\b",
     "a word that praises or appraises; state what happened"),
    (r"\b(?:delv(?:e|es|ed|ing)|underscor(?:e|es|ed|ing)|highlight(?:s|ed|ing)?|showcas(?:e|es|ed|ing)|"
     r"emphasi[sz](?:e|es|ed|ing)|foster(?:s|ed|ing)?|enhanc(?:e|es|ed|ing)|bolster(?:s|ed|ing)?|"
     r"garner(?:s|ed|ing)?|boast(?:s|ed|ing)?)\b",
     "a verb that appraises or connects; say what happened"),
    (r"\b(?:serves?|served|stands?|stood) as\b", "a dodge of 'is'; say 'is'"),
    (r"\b(?:aligns?|resonates?) with\b", "a stock phrase; say what happened"),
    (r"\b(?:subtle|shadowy|whispers?|woven|a sense of)\b", "fake subtlety; say the thing itself"),
    (r"\b(?:key turning point|setting the stage|sets the stage)\b", "a claim of significance"),
    (r"\b(?:experts|researchers|scientists|observers|some say)\b", "a vague agent; say who"),
    (r"\b(?:here['\u2019]s the thing|and honestly)\b", "a hype interjection"),
    (r"\b(?:a reminder that|changes everything|the lesson|ultimately)\b",
     "a summary or a moral; stop when the story stops"),
]
PURPOSE = re.compile(
    r"\b(?:in order to|so as to|so that|evolv(?:e|es|ed) to|learn(?:s|ed|t)? to|tr(?:y|ies|ied|ying) to|"
    r"want(?:s|ed)? to|need(?:s|ed)? to|decid(?:e|es|ed) to|figured out|in an effort to|"
    r"so (?:it|they|he|she) (?:can|could|would|might))\b", re.I)
TAIL = re.compile(
    r",\s+(?:revealing|highlighting|suggesting|showing|underscoring|reflecting|indicating|demonstrating|"
    r"signall?ing|emphasi[sz]ing|illustrating|proving|marking|hinting|pointing to)\b", re.I)
CORRECTIVE = re.compile(
    r"\bnot (?:just|only|merely|simply)\b"
    r"|\b(?:isn['\u2019]t|wasn['\u2019]t|aren['\u2019]t|weren['\u2019]t|not)\b[^,]*,\s*"
    r"(?:it['\u2019]s|it is|it was|they['\u2019]re|they are|they were)\b"
    r"|,\s*not\s+(?:the|a|an|its|their)\b", re.I)
NEGATIVE = re.compile(r"\b(?:no|not|nobody|nothing|none|never|neither|nor|\w+n['\u2019]t)\b", re.I)
NUMBER_WORDS = set((
    "zero two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen sixteen "
    "seventeen eighteen nineteen twenty thirty forty fifty sixty seventy eighty ninety hundred hundreds "
    "thousand thousands million millions billion billions dozen dozens half halves third thirds quarter "
    "quarters fifth fifths sixth sixths seventh sevenths eighth eighths ninth ninths tenth tenths "
    "twentieth twentieths hundredth hundredths thousandth thousandths").split())
NUMBER_JOINS = {"and", "or", "to", "in", "a", "point"}


def read(path):
    return path.read_text(encoding="utf-8-sig").replace("\r\n", "\n").replace("\r", "\n")


def squash(text):
    return " ".join(text.split())


# The prose

def split_sentences(paragraph):
    return [s for s in re.split(r"(?<=[.?!])\s+", paragraph.strip()) if s]


def parse_prose(text):
    """The prose as beats of paragraphs of sentences, and every way it departs from its form."""
    errors = []
    prose = {"title": None, "beats": [], "notes": ""}
    beat = notes = None
    lines = []

    def close_paragraph():
        if lines and beat is not None:
            beat["paragraphs"].append(split_sentences(" ".join(lines)))
        lines.clear()

    for number, raw in enumerate(text.split("\n"), 1):
        where = f"line {number}"
        line = raw.strip()
        if notes is not None:
            notes.append(raw.rstrip())
            continue
        if not line:
            close_paragraph()
            continue
        if prose["title"] is None:
            m = re.fullmatch(r"# (\S.*)", line)
            if not m:
                errors.append((where, "the prose opens with '# <the film's title>'"))
            prose["title"] = m.group(1).strip() if m else ""
            continue
        if line == "## Notes":
            close_paragraph()
            notes = []
            continue
        m = re.fullmatch(r"## (\d+)\. (\S.*)", line)
        if m:
            close_paragraph()
            beat = {"number": int(m.group(1)), "name": m.group(2).strip(), "paragraphs": []}
            prose["beats"].append(beat)
            continue
        if line.startswith("#"):
            errors.append((where, "a heading is '## <n>. <beat name>' or '## Notes': " + line[:60]))
            continue
        if re.match(r"(?:[-*+>|]|\d+\.)\s", line):
            errors.append((where, "prose only, with no lists, quotations or tables: " + line[:60]))
            continue
        if beat is None:
            errors.append((where, "words before the first beat's heading: " + line[:60]))
            continue
        lines.append(line)
    close_paragraph()
    if notes is not None:
        prose["notes"] = "\n".join(notes).strip()
    if prose["title"] is None:
        errors.append(("prose", "the file is empty"))
    for expected, beat in enumerate(prose["beats"], 1):
        if beat["number"] != expected:
            errors.append((f"beat {beat['number']}", f"beats are numbered 1, 2, 3 in order; expected {expected}"))
        if not beat["paragraphs"]:
            errors.append((f"beat {beat['number']}", "no prose under it"))
    return prose, errors


def prose_entries(prose):
    """Every spoken sentence of the prose, with where it is."""
    return [(f"beat {b['number']}, paragraph {i}, sentence {j}", s)
            for b in prose["beats"] for i, para in enumerate(b["paragraphs"], 1)
            for j, s in enumerate(para, 1)]


# The script

def places_in(value):
    """The places in a value, and whatever it holds besides places and separators."""
    places = [{"first": int(a), "last": int(b or a)} for a, b in PLACE.findall(value)]
    rest = re.sub(r"[\s,;]+", "", PLACE.sub("", value))
    return places, rest


def new_passage(beat, k):
    return {"id": f"{beat}.{k}", "_beat": beat, "_k": k, "say": [], "facts": [],
            "picture": {"kind": None, "see": None, "run": None, "bodies": [], "seconds": None,
                        "from": []},
            "text": [], "hold": 0.0}


def read_field(passage, name, value, where, errors):
    picture = passage["picture"]
    if name == "Say":
        if value:
            passage["say"].append(value)
        else:
            errors.append((where, "an empty Say: line"))
    elif name in ("Facts", "From"):
        places, rest = places_in(value)
        if rest or not places:
            errors.append((where, f"{name}: holds places only, as [facts.md:L<a>] or [facts.md:L<a>-<b>]"))
        (passage["facts"] if name == "Facts" else picture["from"]).extend(places)
    elif name == "Picture":
        if value not in ("run", "graphic"):
            errors.append((where, "Picture: is 'run' or 'graphic'"))
        picture["kind"] = value
    elif name == "See":
        if not value:
            errors.append((where, "an empty See: line"))
        picture["see"] = value
    elif name == "Run":
        if not re.fullmatch(r"[A-Za-z0-9][\w.-]*", value):
            errors.append((where, "Run: is one run's name, as facts.md writes it"))
        picture["run"] = value
    elif name == "Bodies":
        ids = [b.strip() for b in value.split(",")]
        if any(not re.fullmatch(r"[\w.-]+", b) for b in ids):
            errors.append((where, "Bodies: is body ids, as facts.md writes them, separated by commas"))
        picture["bodies"] = ids
    elif name == "Seconds":
        m = re.fullmatch(rf"({NUMBER})(?:\s+to\s+({NUMBER}))?", value)
        if not m:
            errors.append((where, "Seconds: is '<a>' or '<a> to <b>', in digits without separators"))
        else:
            first = float(m.group(1))
            last = float(m.group(2)) if m.group(2) else first
            if last < first:
                errors.append((where, "Seconds: ends before it starts"))
            picture["seconds"] = [first, last]
    elif name == "Text":
        at = value.find("[")
        words = (value if at < 0 else value[:at]).strip()
        places, rest = places_in("" if at < 0 else value[at:])
        if not words:
            errors.append((where, "Text: starts with the words shown"))
        elif len(unmark(words).split()) > TEXT_MAX:
            errors.append((where, f"Text: shows {len(unmark(words).split())} words; at most {TEXT_MAX}"))
        if rest or not (places or GIVEN.fullmatch(words)):
            errors.append((where, "Text: ends with the places that hold it, unless it is only a name we give"))
        passage["text"].append({"words": words, "facts": places})
    elif name == "Hold":
        if not re.fullmatch(NUMBER, value) or float(value) <= 0:
            errors.append((where, "Hold: is a number of seconds above 0"))
        else:
            passage["hold"] = float(value)


def parse_script(text):
    """The script as data, and every way it departs from the schema, as (where, what) pairs."""
    errors = []
    script = {"title": None, "question": None, "answer": None, "beats": [], "notes": ""}
    beat = passage = notes = None
    seen = []
    for number, raw in enumerate(text.split("\n"), 1):
        where = f"line {number}"
        line = raw.strip()
        if notes is not None:
            notes.append(raw.rstrip())
            continue
        if not line:
            continue
        if script["title"] is None:
            m = re.fullmatch(r"# Script:\s*(\S.*)", line)
            if not m:
                errors.append((where, "the script opens with '# Script: <title>'"))
            script["title"] = m.group(1).strip() if m else ""
            continue
        if line == "## Notes":
            notes = []
            continue
        m = re.fullmatch(r"## (\d+)\. (\S.*)", line)
        if m:
            beat = {"number": int(m.group(1)), "name": m.group(2).strip(), "passages": []}
            script["beats"].append(beat)
            passage = None
            continue
        m = re.fullmatch(r"### (\d+)\.(\d+)", line)
        if m:
            if beat is None:
                errors.append((where, "a passage comes under a beat"))
                continue
            passage = new_passage(int(m.group(1)), int(m.group(2)))
            beat["passages"].append(passage)
            seen = []
            continue
        if line.startswith("#"):
            errors.append((where, "not a heading of the schema: " + line[:60]))
            continue
        m = re.fullmatch(r"(Question|Answer):\s*(.*)", line)
        if m and beat is None:
            key = m.group(1).lower()
            if script[key] is not None:
                errors.append((where, f"a second {m.group(1)}: line"))
            script[key] = m.group(2).strip()
            continue
        m = FIELD_LINE.match(line)
        if m and passage is not None:
            name, value = m.group(1), m.group(2).strip()
            if name in seen and name not in REPEATABLE:
                errors.append((where, f"a second {name}: line in passage {passage['id']}"))
            elif seen and FIELDS.index(name) < FIELDS.index(seen[-1]):
                errors.append((where, f"{name}: after {seen[-1]}: (the order is {', '.join(FIELDS)})"))
            seen.append(name)
            read_field(passage, name, value, where, errors)
            continue
        if m:
            errors.append((where, f"{m.group(1)}: belongs to a passage ('### <beat>.<k>')"))
            continue
        errors.append((where, "not part of the schema: " + line[:60]))
    if notes is not None:
        script["notes"] = "\n".join(notes).strip()
    validate_script(script, errors)
    return script, errors


def validate_script(script, errors):
    if script["title"] is None:
        errors.append(("script", "the file is empty"))
        return
    for key in ("question", "answer"):
        if not script[key]:
            errors.append(("script", f"no {key.capitalize()}: line before the first beat"))
    if not script["beats"]:
        errors.append(("script", "no beats"))
    for expected, beat in enumerate(script["beats"], 1):
        if beat["number"] != expected:
            errors.append((f"beat {beat['number']}", f"beats are numbered 1, 2, 3 in order; expected {expected}"))
        if not beat["passages"]:
            errors.append((f"beat {beat['number']}", "no passages"))
        for k, passage in enumerate(beat["passages"], 1):
            where = f"passage {passage['id']}"
            if passage["_beat"] != beat["number"] or passage["_k"] != k:
                errors.append((where, f"expected passage {beat['number']}.{k}"))
            if passage["say"] and not passage["facts"]:
                errors.append((where, "spoken sentences need a Facts: line"))
            if not passage["say"] and not passage["hold"]:
                errors.append((where, "a passage with no Say: holds its picture, so it needs a Hold:"))
            picture = passage["picture"]
            for field, key in (("Picture", "kind"), ("See", "see"), ("From", "from")):
                if not picture[key]:
                    errors.append((where, f"no {field}: line"))
            if picture["kind"] == "run":
                if not picture["run"]:
                    errors.append((where, "a run's picture names its Run:"))
                if not picture["seconds"]:
                    errors.append((where, "a run's picture names its Seconds:"))
            if picture["kind"] == "graphic" and (picture["run"] or picture["bodies"] or picture["seconds"]):
                errors.append((where, "a graphic has no Run:, Bodies: or Seconds:"))


# story.md

def story_parts(text):
    """The question, the answer and the beats of story.md."""
    lines = text.split("\n")

    def field(label):
        for i, line in enumerate(lines):
            m = re.match(r"^\s*(?:[-*]\s+)?(?:\*\*)?" + label + r":(?:\*\*)?\s*(.*)$", line)
            if m:
                parts = [m.group(1)]
                for more in lines[i + 1:]:
                    if more.strip() and more[:1].isspace() and not re.match(r"^\s*(?:[-*]|\d+\.)\s", more):
                        parts.append(more)
                    else:
                        break
                return squash(" ".join(parts))
        return None

    beats = []
    for line in lines:
        m = re.match(r"^###\s+(\d+)\.\s+(.+?)\s*$", line)
        if m:
            beats.append((int(m.group(1)), squash(m.group(2))))
    return field("Question"), field("Answer"), beats


def same_beats(beats, story_beats, errors):
    if not story_beats:
        errors.append(("story.md", "no beats, as '### <n>. <name>', to compare with"))
    elif [(b["number"], squash(b["name"])) for b in beats] != story_beats:
        errors.append(("beats", "not the beats of story.md in its order, which are "
                       + "; ".join(f"{n}. {name}" for n, name in story_beats)))


# The rules that can be counted

def word_count(sentence):
    return len(re.findall(r"[^\W\d_]+(?:['\u2019-][^\W\d_]+)*", sentence))


def count_numbers(sentence):
    """Numbers said in words, a run like 'one in sixty-three' or 'five or six' counting once. 'One' on
    its own is not counted, since it is as often a pronoun."""
    words = re.findall(r"[a-z]+", sentence.lower())
    count = i = 0
    while i < len(words):
        if words[i] not in NUMBER_WORDS and words[i] != "one":
            i += 1
            continue
        end, counted = i, words[i] in NUMBER_WORDS
        while True:
            k = end + 1
            while k < len(words) and words[k] in NUMBER_JOINS:
                k += 1
            if k < len(words) and (words[k] in NUMBER_WORDS or words[k] == "one"):
                counted = counted or words[k] in NUMBER_WORDS
                end = k
            else:
                break
        count += counted
        i = end + 1
    return count


def unmark(text):
    """The words as the narrator says them: a given name without its braces."""
    return GIVEN.sub(r"\1", text)


def sentence_problems(sentence):
    problems = []
    for inner in GIVEN.findall(sentence):
        if not NAME.fullmatch(inner):
            problems.append(f"'{{{{{inner}}}}}': between the braces goes only a name we give, of one to three words")
    sentence = unmark(sentence)
    body = sentence[:-1] if sentence[-1:] in (".", "?", "!") else sentence
    if body is sentence:
        problems.append("it does not end with '.', '?' or '!'")
    if re.search(r"[.?!]", body):
        problems.append("a point inside the sentence")
    if re.search(r"\d", body):
        problems.append("a number in digits: write it as the narrator says it")
    odd = sorted({c for c in body if not (c.isalpha() or c.isdigit() or c in " ',-.?!\u2019")})
    if odd:
        problems.append("characters a listener cannot hear or a voice misreads: " + " ".join(odd))
    if re.search(r"(?:^|\s)-|-(?:\s|$)|--", body):
        problems.append("a dash: give the aside a sentence of its own")
    words = word_count(sentence)
    if words > SENTENCE_MAX:
        problems.append(f"{words} words: keep a sentence to about 20, and never over {SENTENCE_MAX}")
    if sentence.endswith("?") and words <= 3:
        problems.append("a short question answered at once, a hype device")
    for pattern, what in TELLS:
        m = re.search(pattern, body, re.I)
        if m:
            problems.append(f"'{m.group(0)}': {what}")
    m = PURPOSE.search(body)
    if m:
        problems.append(f"'{m.group(0)}': purpose or wanting; tell what happened to the population")
    m = TAIL.search(body)
    if m:
        problems.append(f"'{m.group(0).strip(', ')}': a closing clause that comments; end on the fact")
    numbers = count_numbers(body)
    if numbers > 1:
        problems.append(f"{numbers} numbers: one to a sentence")
    return problems


STOP = set(("that this with from have they them their there what when were been into only some most more "
            "than then each also just like over such very will would could should about these those").split())


def stems(sentence):
    return {w[:4] for w in re.findall(r"[a-z]+", sentence.lower()) if len(w) >= 4 and w not in STOP}


def split_corrective(where_before, before, length_before, where_after, after, length_after):
    """Two short sentences of one paragraph set against each other, as in 'They scored theirs. Nobody
    scores these.' or 'It didn't breed. It died.'"""
    if where_before.rsplit(", sentence", 1)[0] != where_after.rsplit(", sentence", 1)[0]:
        return False
    if length_before > SHORT_PAIR or length_after > SHORT_PAIR:
        return False
    negative_before, negative_after = bool(NEGATIVE.search(before)), bool(NEGATIVE.search(after))
    if negative_after and not negative_before:
        return length_after <= 4 or bool(stems(before) & stems(after))
    if negative_before and not negative_after:
        return before.split()[0].lower() == after.split()[0].lower()
    return False


def sentence_rules(entries, errors):
    """Checks every spoken sentence and the runs between them. Returns the problems by sentence and
    the number of spoken words."""
    flagged = {}
    for j, (_, sentence) in enumerate(entries):
        for problem in sentence_problems(sentence):
            flagged.setdefault(j, []).append(problem)
    corrective = [j for j, (_, s) in enumerate(entries) if CORRECTIVE.search(s)]
    if len(corrective) > 1:
        for j in corrective:
            flagged.setdefault(j, []).append(
                f"'not this but that' in {len(corrective)} sentences; once in a film at most, "
                "and only where 'this' was really expected")
    lengths = [word_count(s) for _, s in entries]
    for j in range(1, len(entries)):
        (where_before, before), (where_after, after) = entries[j - 1], entries[j]
        if split_corrective(where_before, before, lengths[j - 1], where_after, after, lengths[j]):
            flagged.setdefault(j, []).append(
                f"two short sentences set against each other, one of them negative (\"{before}\" then this): "
                "the corrective split in two; say what happens, in one plain sentence")
    for j in range(2, len(entries)):
        run = lengths[j - 2:j + 1]
        if max(run) - min(run) <= SAME_LENGTH:
            flagged.setdefault(j, []).append(
                f"the third sentence in a row of about the same length ({', '.join(map(str, run))} words); vary them")
    words = sum(lengths)
    if not WORDS_MIN <= words <= WORDS_MAX:
        errors.append(("length", f"{words} spoken words; the budget is {WORDS_MIN} to {WORDS_MAX}"))
    return flagged, words


def check_key_lines(text, beats, errors, name):
    """beats: (name, sentences) for each beat, in order."""
    sections, order = {}, []
    for number, raw in enumerate(text.split("\n"), 1):
        line = raw.strip()
        if not line or re.fullmatch(r"# \S.*", line):
            continue
        m = re.fullmatch(r"## (Opening|Turn|Last line|Name)", line)
        if m:
            order.append(m.group(1))
            sections[m.group(1)] = []
            continue
        m = re.fullmatch(r"(\d+)\. (\S.*)", line)
        if m and order:
            sections[order[-1]].append((int(m.group(1)), m.group(2).strip()))
            continue
        errors.append((f"{name} line {number}", "not part of the key lines' form: " + line[:60]))
    allowed = ["Opening", "Turn", "Last line", "Name"]
    if not order or len(set(order)) != len(order) or order != [s for s in allowed if s in order]:
        errors.append((name, "the sections are those of '## Opening', '## Turn', '## Last line' and '## Name' "
                             "still to be picked, each once, in that order"))
    every = [s for _, sentences in beats for s in sentences]
    turn = next((sentences for beat, sentences in beats if beat.strip().lower() == "the turn"), None)
    if turn is None:
        errors.append((name, "there is no beat named 'The turn'"))
    in_draft = {"Opening": every[0] if every else None,
                "Turn": turn[0] if turn else None,
                "Last line": every[-1] if every else None}
    given = {squash(g) for s in every for g in GIVEN.findall(s)}
    for section, versions in sections.items():
        where = f"{name}, {section}"
        if [n for n, _ in versions] != [1, 2, 3]:
            errors.append((where, "three versions, numbered 1, 2 and 3"))
        if len({squash(v).lower() for _, v in versions}) != len(versions):
            errors.append((where, "two of the versions are the same line"))
        if section == "Name":
            for n, version in versions:
                if not NAME.fullmatch(version):
                    errors.append((f"{where}, version {n}", f"a name of one to three words, without braces: \"{version}\""))
            if versions and squash(versions[0][1]) not in given:
                errors.append((where, f"version 1 is not a name the prose gives between braces, as {{{{{versions[0][1]}}}}}"))
            continue
        line = in_draft.get(section)
        if versions and line is not None and squash(versions[0][1]) != squash(line):
            errors.append((where, f"version 1 is not the draft's line, which is \"{line}\""))
        for n, version in versions:
            for problem in sentence_problems(version):
                errors.append((f"{where}, version {n}", f"{problem}: \"{version}\""))


# The sample of facts

SAMPLE = 15   # facts a source check opens (facts-gathering.md)


def fact_items(text, last_only=False):
    """The facts of facts.md, as (line, text): list items that carry their provenance in square
    brackets, outside the Sources section; with last_only, those of the last '## ' section alone."""
    lines = text.split("\n")
    heads = [i for i, line in enumerate(lines) if re.match(r"## \S", line)]
    start = heads[-1] if last_only and heads else 0
    items, current, section = [], None, None
    for i in range(start, len(lines)):
        line = lines[i]
        m = re.match(r"^(#+) (.*)$", line)
        if m:
            if len(m.group(1)) == 2:
                section = m.group(2).strip()
            current = None
        elif re.match(r"^\s*- ", line):
            current = [i + 1, line.strip()[2:], section]
            items.append(current)
        elif current and line.strip() and line[:1].isspace():
            current[1] += " " + line.strip()
        else:
            current = None
    return [(n, t) for n, t, sec in items if "[" in t and (sec or "").lower() != "sources"]


def sample(items, k=SAMPLE):
    if len(items) <= k:
        return items
    return [items[round(i * (len(items) - 1) / (k - 1))] for i in range(k)]


# The commands

def report(verdict, lines, errors, entries, flagged):
    failed = bool(errors or flagged)
    print(f"{verdict}: {'FAIL' if failed else 'PASS'}")
    for line in lines:
        print(line)
    for where, what in errors:
        print(f"ERROR {where}: {what}")
    for j in sorted(flagged):
        where, sentence = entries[j]
        print(f"ERROR {where}: \"{sentence}\"")
        for problem in flagged[j]:
            print(f"  - {problem}")
    return 1 if failed else 0


def missing(folder, names, verdict):
    gone = [folder / n for n in names if not (folder / n).is_file()]
    if gone:
        print(f"{verdict}: FAIL")
        for f in gone:
            print(f"cannot read {f}")
    return bool(gone)


def check_prose(path):
    folder = path.parent
    if missing(folder, ["story.md"], "PROSE CHECK"):
        return 2
    prose, errors = parse_prose(read(path))
    same_beats(prose["beats"], story_parts(read(folder / "story.md"))[2], errors)
    entries = prose_entries(prose)
    names = sorted({squash(g) for _, s in entries for g in GIVEN.findall(s)})
    if len(names) > 1:
        errors.append(("names", f"the prose gives {len(names)} names between braces ({', '.join(names)}); "
                                "a film gives at most one"))
    flagged, words = sentence_rules(entries, errors)
    m = re.fullmatch(r"prose-(\d+)(?:-v\d+)?\.md", path.name)
    key_note = "not checked on this file"
    if m:
        key_path = folder / f"prose-{m.group(1)}-key-lines.md"
        key_note = f"no {key_path.name}"
        if key_path.is_file():
            key_note = f"{key_path.name} checked"
            beats = [(b["name"], [s for para in b["paragraphs"] for s in para]) for b in prose["beats"]]
            check_key_lines(read(key_path), beats, errors, key_path.name)
    spoken = words / PACE
    return report("PROSE CHECK", [
        f"{path.name}: {words} spoken words in {len(entries)} sentences; the budget is {WORDS_MIN} to {WORDS_MAX}, "
        f"aiming at about {WORDS_TARGET}",
        f"spoken time about {spoken:.1f} min at {PACE} words a minute, before any pauses",
        f"key lines: {key_note}"], errors, entries, flagged)


def check_script(path):
    folder = path.parent
    if missing(folder, ["facts.md", "story.md", "prose.md"], "SCRIPT CHECK"):
        return 2
    script, errors = parse_script(read(path))
    facts = read(folder / "facts.md")
    facts_lines = len(facts.splitlines())
    question, answer, story_beats = story_parts(read(folder / "story.md"))
    for label, mine, theirs in (("Question", script["question"], question),
                                ("Answer", script["answer"], answer)):
        if theirs is None:
            errors.append(("story.md", f"no {label}: line to compare the script's with"))
        elif mine and squash(mine) != theirs:
            errors.append((label, f"not the {label.lower()} of story.md word for word"))
    same_beats(script["beats"], story_beats, errors)

    prose, prose_errors = parse_prose(read(folder / "prose.md"))
    given_names = {squash(g) for b in prose["beats"] for para in b["paragraphs"] for s in para for g in GIVEN.findall(s)}
    for beat in script["beats"]:
        for passage in beat["passages"]:
            for t in passage["text"]:
                m = GIVEN.fullmatch(t["words"])
                if m and not t["facts"] and squash(m.group(1)) not in given_names:
                    errors.append((f"passage {passage['id']}", f"Text: {t['words']} is not a name prose.md gives between "
                                   "braces, so it ends with the places that hold it"))
    if prose_errors:
        errors.append(("prose.md", "does not follow its form; the prose command says how"))
    else:
        for n, (beat, prose_beat) in enumerate(zip(script["beats"], prose["beats"]), 1):
            said = [squash(s) for p in beat["passages"] for s in p["say"]]
            approved = [squash(s) for para in prose_beat["paragraphs"] for s in para]
            if said != approved:
                k = next((i for i, (a, b) in enumerate(zip(said, approved)) if a != b), min(len(said), len(approved)))
                mine = f"\"{said[k]}\"" if k < len(said) else "nothing"
                theirs = f"\"{approved[k]}\"" if k < len(approved) else "nothing"
                errors.append((f"beat {n}", f"the words part from prose.md at sentence {k + 1}: the script "
                                            f"has {mine} where prose.md has {theirs}"))
        if len(script["beats"]) != len(prose["beats"]):
            errors.append(("beats", f"the script has {len(script['beats'])} beats and prose.md {len(prose['beats'])}"))

    for beat in script["beats"]:
        for passage in beat["passages"]:
            where = f"passage {passage['id']}"
            picture = passage["picture"]
            places = passage["facts"] + picture["from"] + [p for t in passage["text"] for p in t["facts"]]
            for p in places:
                if not 1 <= p["first"] <= p["last"] <= facts_lines:
                    span = f"{p['first']}" if p["first"] == p["last"] else f"{p['first']}-{p['last']}"
                    errors.append((where, f"[facts.md:L{span}] is not in facts.md, which has {facts_lines} lines"))
            for name in ([picture["run"]] if picture["run"] else []) + picture["bodies"]:
                if name and not re.search(r"(?<![\w-])" + re.escape(name) + r"(?![\w-])", facts):
                    errors.append((where, f"'{name}' is not named in facts.md"))

    sentences = [s for b in script["beats"] for p in b["passages"] for s in p["say"]]
    words = sum(word_count(s) for s in sentences)
    passages = sum(len(b["passages"]) for b in script["beats"])
    held = sum(p["hold"] for b in script["beats"] for p in b["passages"])
    spoken = words / PACE
    return report("SCRIPT CHECK", [
        f"{path.name}: {words} spoken words in {len(sentences)} sentences and {passages} passages",
        f"spoken time about {spoken:.1f} min at {PACE} words a minute, and {held:g} s held: "
        f"about {spoken + held / 60:.1f} min in all"], errors, [], {})


def main(argv):
    if len(argv) == 4 and argv[1] == "sample" and argv[3] == "--last":
        argv = argv[:3] + ["--last"]
    elif len(argv) != 3 or argv[1] not in ("prose", "script", "say", "json", "sample"):
        print(__doc__.strip(), file=sys.stderr)
        return 2
    command, path = argv[1], Path(argv[2])
    if not path.is_file():
        if command in ("prose", "script"):
            print(f"{command.upper()} CHECK: FAIL")
        print(f"cannot read {path}")
        return 2
    if command == "prose":
        return check_prose(path)
    if command == "script":
        return check_script(path)
    if command == "sample":
        items = fact_items(read(path), last_only=len(argv) == 4)
        if not items:
            print(f"{path.name}: no facts found: list items ending with their provenance in square brackets")
            return 1
        chosen = sample(items)
        print(f"{len(chosen)} of {len(items)} facts in {path.name}" + (", last section" if len(argv) == 4 else ""))
        for n, t in chosen:
            print(f"L{n}: {t}")
        return 0
    text = read(path)
    is_script = text.lstrip().startswith("# Script:")
    if command == "json" and not is_script:
        print(f"{path.name} is not a script; json prints a script", file=sys.stderr)
        return 1
    data, errors = parse_script(text) if is_script else parse_prose(text)
    if errors:
        print(f"{path.name} does not follow its form:", file=sys.stderr)
        for where, what in errors:
            print(f"ERROR {where}: {what}", file=sys.stderr)
        return 1
    if is_script:
        for beat in data["beats"]:
            for passage in beat["passages"]:
                passage.pop("_beat")
                passage.pop("_k")
    if command == "json":
        print(json.dumps(data, indent=2, ensure_ascii=False))
    elif is_script:
        print(unmark("\n\n".join(" ".join(p["say"]) for b in data["beats"] for p in b["passages"] if p["say"])))
    else:
        print(unmark("\n\n".join(" ".join(para) for b in data["beats"] for para in b["paragraphs"])))
    return 0


if __name__ == "__main__":
    for stream in (sys.stdout, sys.stderr):
        stream.reconfigure(encoding="utf-8")
    sys.exit(main(sys.argv))
