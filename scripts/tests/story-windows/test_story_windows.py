"""The window planner's rules on a run made up for the test (scripts/story-windows.py, B3).

    python scripts/tests/story-windows/test_story_windows.py

The fixture is written at test time under scratch/tests/story-windows/, which git ignores, because
a run directory is lineage.jsonl and stats.jsonl under runs/, both ignored patterns: a committed
fixture there would be invisible to the next checkout. It is removed when the tests end.
"""
import importlib.util
import json
import math
import os
import shutil
import sys
import unittest
import uuid

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
SCRIPT = os.path.join(ROOT, "scripts", "story-windows.py")

spec = importlib.util.spec_from_file_location("story_windows", SCRIPT)
planner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(planner)

RUN_NAME = "2026-01-01-000000-aaaa0000"

# id, parent, second, (absorptive, jointed, photosynthetic)
BIRTHS = [
    (0, -1, 0.5, (0, 0, 1)), (1, -1, 0.5, (1, 0, 0)),
    (3, 0, 100.0, (0, 0, 1)), (5, 0, 150.0, (0, 0, 1)),
    (7, 3, 480.0, (0, 0, 1)), (8, 3, 510.0, (0, 0, 1)),
    (9, 1, 600.0, (1, 0, 0)),
    (10, 3, 700.0, (1, 0, 1)),            # new flags: founds a clade whose parent clade is 0's
    (11, 10, 720.0, (1, 0, 1)), (12, 10, 760.0, (1, 0, 1)),
]

SCENES = [
    {"n": 1, "run": "t-s1", "station": "Descent", "chapter": "How it works", "second": 100, "subject": "world", "seconds": 16},
    {"n": 2, "run": "t-s1", "station": "Portrait", "chapter": "A body", "flexible": False, "second": 200, "subject": "body 5", "seconds": 20},
    {"n": 3, "run": "t-s1", "station": "NEW:title card", "chapter": "Ignored", "second": 300, "subject": "world", "seconds": 10},
    {"n": 4, "run": "t-s1", "station": "Time", "from": 400, "to": 900, "subject": "world", "seconds": 10},
    {"n": 5, "run": "t-s1", "station": "Birth", "second": 505, "subject": "the leaves (root 0); parent body 3"},
    {"n": 6, "run": "t-s1", "station": "Birth", "second": 600, "subject": "child body 9"},
    {"n": 7, "run": "t-s1", "station": "Birth", "second": 705, "subject": "root 10"},
    {"n": 8, "run": "t-s1", "station": "Arrival", "second": 990, "subject": "world", "seconds": 20},
    {"n": 9, "run": "t-s1", "station": "Portrait", "flexible": False, "second": 995, "subject": "body 5", "seconds": 20},
    {"n": 10, "run": "t-s1", "station": "Card", "subject": "world"},
    {"n": 11, "run": "t-s2", "station": "Arrival", "second": 50, "subject": "world", "seconds": 10},
]


class PlannerTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.base = os.path.join(ROOT, "scratch", "tests", "story-windows", uuid.uuid4().hex)
        run = os.path.join(cls.base, "runs", "t-s1", RUN_NAME)
        os.makedirs(os.path.join(run, "checkpoints"))
        with open(os.path.join(run, "checkpoints", "000000500.ckpt"), "w") as f:
            f.write("")
        with open(os.path.join(run, "run.json"), "w") as f:
            json.dump({"arm": "t-s1", "requestedSeconds": 1000, "simulatedSeconds": 1000}, f)
        with open(os.path.join(run, "stats.jsonl"), "w") as f:
            for t in range(0, 1001, 10):
                f.write(json.dumps({"t": float(t), "alive": 10}) + "\n")
        with open(os.path.join(run, "lineage.jsonl"), "w") as f:
            for i, p, t, (a, j, ph) in BIRTHS:
                f.write(json.dumps({"e": "b", "t": t, "id": i, "p": p, "abs": a, "jnt": j, "pho": ph}) + "\n")
        cls.run_dir = run
        cls.story = os.path.join(cls.base, "story.json")
        with open(cls.story, "w") as f:
            json.dump({"title": "a test", "scenes": SCENES}, f)

    @classmethod
    def tearDownClass(cls):
        shutil.rmtree(cls.base, ignore_errors=True)

    def plan(self, name, *extra):
        out = os.path.join(self.base, name)
        code = planner.main([self.story, "--out", out, "--runs-root", os.path.join(self.base, "runs")] + list(extra))
        self.assertEqual(0, code)
        with open(os.path.join(out, planner.FILE_NAME), encoding="utf-8") as f:
            return json.load(f)

    def window(self, manifest, scene, part="main"):
        found = [w for w in manifest["windows"] if w["scene"] == scene and w["part"] == part]
        self.assertEqual(1, len(found), "scene %d %s" % (scene, part))
        return found[0]

    def test_the_timing_rules(self):
        m = self.plan("all")
        self.assertEqual(planner.FORMAT, m["format"])

        # A flexible scene's chapter card plays from its second, the takes after it.
        w = self.window(m, 1)
        self.assertEqual((100.0, 100.0, 125.0, 8.0, 16.0), (w["start"], w["from"], w["to"], w["card"], w["seconds"]))
        self.assertEqual(os.path.abspath(self.run_dir).replace("\\", "/"), w["run"])
        self.assertEqual("story-01-t-s1-main", w["out"])
        self.assertEqual(30.0, w["fps"])

        # A held scene's card plays before its second, the takes from it.
        w = self.window(m, 2)
        self.assertEqual((192.0, 221.0, 8.0), (w["start"], w["to"], w["card"]))

        # A title card carries no chapter card.
        w = self.window(m, 3)
        self.assertEqual(("Card", 300.0, 311.0, 0.0), (w["station"], w["start"], w["to"], w["card"]))

        # A time scene is two windows, each one take long.
        a, b = self.window(m, 4), self.window(m, 4, "time-b")
        self.assertEqual((400.0, 406.0, 5.0), (a["start"], a["to"], a["seconds"]))
        self.assertEqual((900.0, 906.0), (b["start"], b["to"]))
        self.assertEqual("story-04-t-s1-time-b", b["out"])

    def test_a_birth_is_found_before_it_is_filmed(self):
        m = self.plan("births")

        # The named parent's child nearest the second, and the window opens 8 s before it.
        w = self.window(m, 5)
        self.assertEqual({"parent": 3, "child": 8, "at": 510.0}, w["birth"])
        self.assertEqual((502.0, 521.0), (w["start"], w["to"]))

        # A named child.
        self.assertEqual({"parent": 1, "child": 9, "at": 600.0}, self.window(m, 6)["birth"])

        # Near its founding, a clade's birth is its founder's, a child of the parent clade.
        w = self.window(m, 7)
        self.assertEqual({"parent": 3, "child": 10, "at": 700.0}, w["birth"])
        self.assertEqual(692.0, w["start"])

    def test_the_run_end_and_the_neighbours(self):
        m = self.plan("end")

        # A flexible scene past the last row is moved back to end on it.
        w = self.window(m, 8)
        self.assertEqual((979.0, 1000.0), (w["start"], w["to"]))
        self.assertTrue(any("moved back" in n for n in w["notes"]))

        # A held one is left, and says the farm will call it unverified.
        w = self.window(m, 9)
        self.assertEqual(995.0, w["start"])
        self.assertTrue(any("UNVERIFIED" in n for n in w["notes"]))

        # A card with no second takes its neighbour's, and is moved back like any flexible one.
        w = self.window(m, 10)
        self.assertEqual((991.0, 1000.0), (w["start"], w["to"]))

        # A scene whose run cannot be found is skipped, never guessed.
        self.assertEqual([11], [s["scene"] for s in m["skipped"]])
        self.assertIn("no run for t-s2", m["skipped"][0]["why"])

    def test_the_manifest_is_merged_and_carries_what_the_reader_reads(self):
        self.plan("merged", "--scenes", "1")
        m = self.plan("merged", "--scenes", "3", "--runs", "t-s1")
        self.assertEqual([1, 3], [w["scene"] for w in m["windows"]])

        # StoryWindows.WindowOf's fields (src/Evosim.Farm/StoryWindows.cs).
        for w in m["windows"]:
            for key in ("scene", "part", "arm", "station", "run", "from", "to", "fps", "start", "card", "seconds", "out", "notes"):
                self.assertIn(key, w)
            self.assertGreater(w["to"], w["from"])

    def test_the_story_reader(self):
        shot = planner.read_shot({"n": "#4", "station": "NEW:crowd pull-back - the colony", "subject": "Frondium x (guide clade 38), founder body 39"}, 0)
        self.assertEqual(4, shot.number)
        self.assertEqual(39, shot.root)
        self.assertEqual(38, shot.guide_index)
        self.assertEqual(-1, shot.body)
        self.assertEqual("Colony", planner.station_of(shot, True))

        shot = planner.read_shot({"subject": "Vorafrons vabecrepis (guide clade 958); parent body 44820, child body 48048"}, 0)
        self.assertEqual((44820, 48048, -1), (shot.parent_body, shot.child_body, shot.body))
        self.assertEqual("Vorafrons vabecrepis", shot.name)
        self.assertTrue(planner.same_run("s1", "r48-s1"))
        self.assertFalse(planner.same_run("s2", "r48-s1"))


if __name__ == "__main__":
    unittest.main(verbosity=2)
