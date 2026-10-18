"""The narration's reads: the script's Join: next and the packing of paragraphs into reads.

    python scripts/tests/story-video-check/test_read_packing.py

The voice reads each segment with no context but its own words, so narration.py packs a beat's prose
paragraphs into reads as long as the voice's max_segment_chars allows (narration.md, "Segments and
pieces"). A passage marked Join: next keeps the next paragraph in its read (script-schema.md), and
script-check.py refuses the mark where the edit needs a break. The tests that write a film's folder write
it under scratch/read-packing/tests/ and remove it after.
"""
import contextlib
import importlib.util
import io
import json
import os
import shutil
import unittest
from pathlib import Path

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = Path(HERE).parents[2]
SKILL = ROOT / ".claude" / "skills" / "create-story-video"
WORK = ROOT / "scratch" / "read-packing" / "tests"


def load(name, file):
    spec = importlib.util.spec_from_file_location(name, SKILL / file)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


nar = load("narration_tool", "narration.py")
check = nar.sc

SILENT = "silent"


def text(n, tag=""):
    """A sentence of exactly n characters."""
    head = f"The {tag}"
    return head + "x" * (n - len(head) - 1) + "."


def plan(beats):
    """Sentences and passages, as narration.py's load_script gives them, from a plan of the film: beats of
    paragraphs, a paragraph a list of passages, a passage a list of sentence lengths or a dict with
    'say', 'hold' and 'join'. SILENT between paragraphs is a passage with no Say: and a hold."""
    sentences, passages = [], []
    for b, beat in enumerate(beats, 1):
        k = i = 0
        for item in beat:
            if item == SILENT:
                k += 1
                passages.append({"id": f"{b}.{k}", "say": [], "hold": 2.0, "join": False})
                continue
            i += 1
            for passage in item:
                p = passage if isinstance(passage, dict) else {"say": passage}
                k += 1
                pid = f"{b}.{k}"
                say = [text(n, f"{pid}.{j} ") for j, n in enumerate(p["say"], 1)]
                passages.append({"id": pid, "say": say, "hold": p.get("hold", 0.0), "join": p.get("join", False)})
                sentences += [{"passage": pid, "place": j, "text": t, "paragraph": (b, i)}
                              for j, t in enumerate(say, 1)]
    return sentences, passages


def reads(beats, limit, splits=()):
    sentences, passages = plan(beats)
    segments, problems, notes = nar.segments_of(sentences, set(splits), limit, passages)
    return [nar.chars(seg) for seg in segments], [[(s["passage"], s["place"]) for s in seg] for seg in segments], \
        problems, notes


def old_segments_of(sentences, splits, limit):
    """The rule before packing, kept here to compare with: one read a paragraph, split after each passage
    in splits, cut to the limit."""
    chars = nar.chars
    groups, current = [], []
    for s in sentences:
        if current and (s["paragraph"] != current[-1]["paragraph"]
                        or (current[-1]["passage"] in splits and s["passage"] != current[-1]["passage"])):
            groups.append(current)
            current = []
        current.append(s)
    if current:
        groups.append(current)
    segments = []
    for group in groups:
        while group:
            if chars(group) <= limit:
                segments.append(group)
                break
            ends = [i for i in range(1, len(group))
                    if group[i]["passage"] != group[i - 1]["passage"] and chars(group[:i]) <= limit]
            if not ends:
                ends = [i for i in range(1, len(group)) if chars(group[:i]) <= limit]
            if not ends:
                segments.append(group[:1])
                group = group[1:]
                continue
            segments.append(group[:max(ends)])
            group = group[max(ends):]
    return segments


class Packing(unittest.TestCase):
    def test_fewest_reads_then_the_longest_shortest_read(self):
        # Greedy filling would read 150+40+40 and then 150; the packing reads two of 191.
        lengths, _, _, _ = reads([[[[150]], [[40]], [[40]], [[150]]]], 240)
        self.assertEqual(lengths, [191, 191])
        # No tiny read is left at the end of a beat: 200 and 131, not 301 and 30.
        lengths, _, _, _ = reads([[[[200]], [[100]], [[30]]]], 310)
        self.assertEqual(lengths, [200, 131])
        # Everything that fits goes in one read.
        lengths, _, _, _ = reads([[[[100]], [[100]], [[100]]]], 302)
        self.assertEqual(lengths, [302])

    def test_a_joined_unit_is_kept_whole_when_it_fits(self):
        # Unjoined, paragraphs 2 and 3 fall in different reads (the case above). Joined, they share one.
        lengths, shape, problems, notes = reads([[[[150]], [{"say": [40], "join": True}], [[40]], [[150]]]], 240)
        self.assertEqual(len(lengths), 2)
        self.assertTrue(any(("1.2", 1) in seg and ("1.3", 1) in seg for seg in shape), shape)
        self.assertEqual((problems, notes), ([], []))

    def test_a_chain_of_joins_is_one_unit(self):
        _, shape, _, notes = reads([[[{"say": [60], "join": True}], [{"say": [60], "join": True}], [[60]],
                                     [[200]]]], 200)
        self.assertEqual(shape[0], [("1.1", 1), ("1.2", 1), ("1.3", 1)])
        self.assertEqual(notes, [])

    def test_the_limit_breaks_a_joined_unit_with_a_note(self):
        lengths, shape, problems, notes = reads([[[{"say": [200], "join": True}], [[150]]]], 300)
        self.assertEqual(lengths, [200, 150])
        self.assertEqual(problems, [])
        self.assertEqual(len(notes), 1)
        for part in ("NOTE", "passages 1.1 to 1.2", "351 characters", "51 over the voice's 300", "after passage 1.1"):
            self.assertIn(part, notes[0])

    def test_a_hold_breaks_the_read(self):
        lengths, _, _, _ = reads([[[{"say": [50], "hold": 2.0}], [[50]], [[50]]]], 1000)
        self.assertEqual(lengths, [50, 101])

    def test_a_passage_with_no_say_breaks_the_read(self):
        lengths, _, _, _ = reads([[[[50]], SILENT, [[50]], [[50]]]], 1000)
        self.assertEqual(lengths, [50, 101])

    def test_a_split_breaks_the_read(self):
        lengths, _, _, _ = reads([[[[50]], [[50]], [[50]]]], 1000, splits={"1.2"})
        self.assertEqual(lengths, [101, 50])
        lengths, _, _, _ = reads([[[[50], [50]], [[50]]]], 1000, splits={"1.1"})
        self.assertEqual(lengths, [50, 101])

    def test_a_hold_inside_a_paragraph_does_not_split_it(self):
        # Inside a paragraph the voice keeps reading; the cut makes the silence, and only a failed cut splits.
        lengths, _, _, _ = reads([[[{"say": [50], "hold": 2.0}, [50]]]], 1000)
        self.assertEqual(lengths, [101])

    def test_the_last_piece_of_a_cut_is_packed_with_what_follows(self):
        # As round 57's beat 3 at 301: a paragraph of 318 cut into 255 and 62, then a paragraph of 109.
        lengths, shape, _, _ = reads([[[[150, 104, 62]], [[109]]]], 301)
        self.assertEqual(lengths, [255, 62 + 1 + 109])
        self.assertEqual(shape[1], [("1.1", 3), ("1.2", 1)])
        # The pieces before the last stay as the cut made them, and the last joins the packing of the rest.
        lengths, _, _, _ = reads([[[[100, 100, 100]], [[60]], [[60]], [[200]]]], 250)
        self.assertEqual(lengths, [201, 100 + 1 + 60 + 1 + 60, 200])
        # A hard break still holds: across a hold, the last piece stays alone.
        lengths, _, _, _ = reads([[[{"say": [100, 100, 100], "hold": 1.0}], [[40]]]], 250)
        self.assertEqual(lengths, [201, 100, 40])
        # So does a beat.
        lengths, _, _, _ = reads([[[[100, 100, 100]]], [[[40]]]], 250)
        self.assertEqual(lengths, [201, 100, 40])
        # A sentence over the limit, cut alone, is not packed with anything.
        lengths, _, problems, _ = reads([[[[300]], [[40]]]], 250)
        self.assertEqual((lengths, len(problems)), ([300, 40], 1))

    def test_no_read_crosses_a_beat(self):
        lengths, shape, _, _ = reads([[[[50]]], [[[50]]]], 1000)
        self.assertEqual(lengths, [50, 50])
        self.assertEqual(shape, [[("1.1", 1)], [("2.1", 1)]])

    def test_a_unit_over_the_limit_is_cut_as_before(self):
        # At the last passage end that fits.
        lengths, _, problems, notes = reads([[[[100, 100], [100]]]], 250)
        self.assertEqual(lengths, [201, 100])
        self.assertEqual((problems, notes), ([], []))
        # Inside a passage, at the last sentence end that fits.
        lengths, _, _, _ = reads([[[[100, 100, 100]]]], 250)
        self.assertEqual(lengths, [201, 100])
        # A sentence over the limit refuses the request.
        _, _, problems, _ = reads([[[[300]]]], 250)
        self.assertEqual(len(problems), 1)
        self.assertIn("passage 1.1, sentence 1: 300 characters", problems[0])
        # The cut is the old rule's.
        sentences, passages = plan([[[[100, 100], [100, 120]], [[90]]]])
        new = nar.segments_of(sentences, set(), 250, passages)[0]
        self.assertEqual(new, old_segments_of(sentences, set(), 250))

    def test_an_old_script_reads_as_the_packing_rule_implies(self):
        # No Join: anywhere. At 200 paragraph 2 is cut, and its last piece, 90, is packed with paragraph 3;
        # paragraph 4 follows a hold and stays alone. At 450 the stretch packs whole, and the reads keep
        # whole sentences in order.
        film = [[[[120]], [[130, 60], [90]], [{"say": [80], "hold": 1.5}], [[70]]], [[[100]], SILENT, [[110]]]]
        sentences, passages = plan(film)
        lengths, _, _, _ = reads(film, 200)
        self.assertEqual(lengths, [120, 191, 90 + 1 + 80, 70, 100, 110])
        lengths, shape, problems, notes = reads(film, 450)
        # Beat 1's first stretch, before the hold, packs as 120 and 282 + 80, not 282 + 120 and 80.
        self.assertEqual(lengths, [120, 282 + 1 + 80, 70, 100, 110])
        self.assertEqual([s for seg in shape for s in seg], [(s["passage"], s["place"]) for s in sentences])
        self.assertEqual((problems, notes), ([], []))


# ---------------------------------------------------------------- script-check.py's Join: next

SCRIPT_HEAD = "# Script: A film\n\nQuestion: Why?\nAnswer: Because.\n\n"


def passage(pid, say=(), hold=None, join=False):
    lines = [f"### {pid}", ""] + [f"Say: {s}" for s in say]
    if say:
        lines.append("Facts: [facts.md:L1]")
    lines += ["Picture: graphic", "See: A drawing.", "From: [facts.md:L1]"]
    if hold:
        lines.append(f"Hold: {hold}")
    if join:
        lines.append("Join: next")
    return "\n".join(lines) + "\n\n"


def write_film(folder, beats, limit=None):
    """A film's folder from beats of (name, paragraphs, passages): paragraphs is a list of lists of
    sentences, passages a list of (count of sentences, hold, join)."""
    if folder.exists():
        shutil.rmtree(folder)
    folder.mkdir(parents=True)
    story = "- **Question:** Why?\n- **Answer:** Because.\n\n" + "".join(
        f"### {n}. {name}\n\n" for n, (name, _, _) in enumerate(beats, 1))
    prose = "# A film\n\n" + "".join(
        f"## {n}. {name}\n\n" + "".join(" ".join(para) + "\n\n" for para in paragraphs)
        for n, (name, paragraphs, _) in enumerate(beats, 1))
    script = SCRIPT_HEAD
    for n, (name, paragraphs, passages) in enumerate(beats, 1):
        script += f"## {n}. {name}\n\n"
        said = [s for para in paragraphs for s in para]
        for k, (count, hold, join) in enumerate(passages, 1):
            script += passage(f"{n}.{k}", said[:count], hold, join)
            said = said[count:]
    for name, body in (("story.md", story), ("prose.md", prose), ("script.md", script),
                       ("facts.md", "# Facts\n\n- A fact [L1].\n")):
        (folder / name).write_text(body, encoding="utf-8")
    if limit:
        settings = json.loads((SKILL / "narration-settings.json").read_text(encoding="utf-8"))
        settings["voice"]["max_segment_chars"] = limit
        (folder / "narration-settings.json").write_text(json.dumps(settings), encoding="utf-8")
        (folder / "pronunciation.json").write_text("[]", encoding="utf-8")


def check_folder(folder):
    printed = io.StringIO()
    with contextlib.redirect_stdout(printed):
        status = check.check_script(folder / "script.md")
    return status, printed.getvalue()


A, B, C, D = (text(60, f"{x} ") for x in "abcd")


class JoinCheck(unittest.TestCase):
    def setUp(self):
        self.folder = WORK / self.id().rsplit(".", 1)[-1]

    def tearDown(self):
        if self.folder.exists():
            shutil.rmtree(self.folder)

    def film(self, passages, paragraphs=((A, B), (C,), (D,))):
        write_film(self.folder, [("One", [list(p) for p in paragraphs], passages)])
        return check_folder(self.folder)

    def test_a_join_at_a_paragraph_end_passes(self):
        status, printed = self.film([(1, None, False), (1, None, True), (1, None, False), (1, None, False)])
        self.assertEqual(status, 0, printed)
        script, _ = check.parse_script(check.read(self.folder / "script.md"))
        self.assertEqual([p["join"] for p in script["beats"][0]["passages"]], [None, "next", None, None])

    def test_an_old_script_with_no_join_passes(self):
        status, printed = self.film([(2, None, False), (1, 2, False), (1, None, False)])
        self.assertEqual(status, 0, printed)

    def test_a_join_inside_a_paragraph_is_refused(self):
        status, printed = self.film([(1, None, True), (1, None, False), (1, None, False), (1, None, False)])
        self.assertEqual(status, 1)
        self.assertIn("ERROR passage 1.1: Join: next on a passage whose last sentence does not end a paragraph",
                      printed)

    def test_a_join_on_the_last_passage_of_a_beat_is_refused(self):
        status, printed = self.film([(2, None, False), (1, None, False), (1, None, True)])
        self.assertEqual(status, 1)
        self.assertIn("ERROR passage 1.3: Join: next on the last passage of a beat", printed)

    def test_a_join_with_a_hold_is_refused(self):
        status, printed = self.film([(2, 1.5, True), (1, None, False), (1, None, False)])
        self.assertEqual(status, 1)
        self.assertIn("ERROR passage 1.1: Join: next on a passage with a Hold:", printed)

    def test_a_join_before_a_passage_with_no_say_is_refused(self):
        status, printed = self.film([(2, None, True), (0, 2, False), (1, None, False), (1, None, False)])
        self.assertEqual(status, 1)
        self.assertIn("ERROR passage 1.1: Join: next before a passage with no Say:", printed)

    def test_join_takes_only_next(self):
        _, errors = check.parse_script(SCRIPT_HEAD + "## 1. One\n\n" + passage("1.1", [A]).replace(
            "From: [facts.md:L1]", "From: [facts.md:L1]\nJoin: previous"))
        self.assertIn("Join: is 'next'", [what for _, what in errors])


class Request(unittest.TestCase):
    """The request's report, on a film's folder."""

    def setUp(self):
        self.folder = WORK / self.id().rsplit(".", 1)[-1]

    def tearDown(self):
        if self.folder.exists():
            shutil.rmtree(self.folder)

    def segments(self):
        data = json.loads((self.folder / "narration-1-plan.json").read_text(encoding="utf-8"))
        return [[(s["passage"], s["place"]) for s in seg["sentences"]] for seg in data["segments"]]

    def test_an_old_script_packs_its_paragraphs(self):
        write_film(self.folder, [("One", [[A, B], [C], [D]], [(2, None, False), (1, None, False), (1, None, False)]),
                                 ("Two", [[A], [B]], [(1, None, False), (1, None, False)])], limit=130)
        verdict, lines = nar.cmd_request(self.folder, 1)
        self.assertEqual(verdict, "NARRATION REQUEST: READY", lines)
        self.assertEqual(self.segments(), [[("1.1", 1), ("1.1", 2)], [("1.2", 1), ("1.3", 1)],
                                           [("2.1", 1), ("2.2", 1)]])
        self.assertFalse(any(line.startswith("NOTE") for line in lines), lines)

    def test_a_broken_join_is_a_note_and_not_a_refusal(self):
        write_film(self.folder, [("One", [[A, B], [C], [D]], [(2, None, True), (1, None, False), (1, None, False)])],
                   limit=130)
        verdict, lines = nar.cmd_request(self.folder, 1)
        self.assertEqual(verdict, "NARRATION REQUEST: READY", lines)
        notes = [line for line in lines if line.startswith("NOTE")]
        self.assertEqual(len(notes), 1, lines)
        self.assertIn("passages 1.1 to 1.2", notes[0])
        self.assertIn("182 characters, 52 over the voice's 130", notes[0])
        # The cut's last piece, paragraph 2, is packed with paragraph 3.
        self.assertEqual(self.segments(), [[("1.1", 1), ("1.1", 2)], [("1.2", 1), ("1.3", 1)]])


if __name__ == "__main__":
    unittest.main()
