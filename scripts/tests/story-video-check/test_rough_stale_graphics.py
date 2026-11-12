"""A rough edit lays out without drawings made for another shot list (.claude/skills/create-story-video/edit.py).

    python scripts/tests/story-video-check/test_rough_stale_graphics.py

On 2026-11-11 round 67's assembly cut needed a rough timeline after the day's shot drafts had changed windows and
colours, and the edit refused it because graphics.json was stamped with an older shot list. In a rough draft such a
graphics.json now counts as absent: each graphic and word card is left off with its own rough line. A finished draft
still refuses it.
"""
import hashlib
import importlib.util
import json
import os
import tempfile
import unittest
from pathlib import Path

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
EDIT = os.path.join(ROOT, ".claude", "skills", "create-story-video", "edit.py")

spec = importlib.util.spec_from_file_location("edit_tool", EDIT)
edit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(edit)


def folder_with(d, graphics_stamp):
    f = Path(d)
    (f / "graphics").mkdir()
    shots = {"shots": [{"id": "1.1a", "kind": "graphic", "from_s": 0.0, "to_s": 5.0, "run": "r1"},
                       {"id": "1.2a", "kind": "run", "from_s": 5.0, "to_s": 9.0, "run": "r1"}]}
    (f / "shots.json").write_text(json.dumps(shots), encoding="utf-8")
    sha = hashlib.sha256((f / "shots.json").read_bytes()).hexdigest()
    for name in ("c1.mp4", "c2.mp4", "g.mov", "w.mov", "n.wav"):
        (f / name).write_bytes(b"x")
    (f / "clips.json").write_text(json.dumps({"shots_sha256": sha, "clips": [
        {"shot": "1.1a", "file": "c1.mp4"}, {"shot": "1.2a", "file": "c2.mp4"}]}), encoding="utf-8")
    (f / "graphics.json").write_text(json.dumps({
        "shots_sha256": sha if graphics_stamp == "current" else "0" * 64,
        "graphics": {"1.1a": {"file": "g.mov"}}, "stills": {}, "words": {"w1": {"file": "w.mov"}}}), encoding="utf-8")
    (f / "graphics" / "plan.json").write_text(json.dumps({"shots_sha256": sha, "words": [{"id": "w1"}]}),
                                              encoding="utf-8")
    (f / "narration.json").write_text(json.dumps({"pieces": [{"id": "p1", "file": {"path": "n.wav"}}]}),
                                      encoding="utf-8")
    return f


class RoughStaleGraphics(unittest.TestCase):
    def test_a_rough_draft_leaves_every_stale_drawing_off(self):
        with tempfile.TemporaryDirectory(dir=os.path.join(ROOT, "scratch")) as d:
            got, clips, left = edit.inputs(folder_with(d, "stale"), 30, rough=True)
            self.assertTrue(left["stale"])
            self.assertEqual(left["graphic"], {"1.1a"})
            self.assertEqual(left["words"], {"w1"})
            self.assertEqual(set(clips), {"1.1a", "1.2a"})

    def test_a_finished_draft_still_refuses_it(self):
        with tempfile.TemporaryDirectory(dir=os.path.join(ROOT, "scratch")) as d:
            with self.assertRaises(edit.Refused) as e:
                edit.inputs(folder_with(d, "stale"), 30, rough=False)
            self.assertIn("graphics.json was made for another shot list", "\n".join(e.exception.lines))

    def test_a_current_graphics_json_is_used_in_a_rough_draft(self):
        with tempfile.TemporaryDirectory(dir=os.path.join(ROOT, "scratch")) as d:
            got, clips, left = edit.inputs(folder_with(d, "current"), 30, rough=True)
            self.assertFalse(left["stale"])
            self.assertEqual((left["graphic"], left["words"]), (set(), set()))


if __name__ == "__main__":
    unittest.main()
