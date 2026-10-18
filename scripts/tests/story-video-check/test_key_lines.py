"""The prose check's key lines on a countdown (.claude/skills/create-story-video/script-check.py).

    python scripts/tests/story-video-check/test_key_lines.py

A countdown has no beat named "The turn"; its second key line opens the beat named "Number 1"
(prose-brief.md). A story keeps "The turn".
"""
import importlib.util
import os
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
SCRIPT = os.path.join(ROOT, ".claude", "skills", "create-story-video", "script-check.py")

spec = importlib.util.spec_from_file_location("script_check", SCRIPT)
check = importlib.util.module_from_spec(spec)
spec.loader.exec_module(check)

KEY_LINES = """# Key lines

## Opening

1. The first body in the tank was a leaf.
2. A leaf was the first body in the tank.
3. Every tank began with a leaf.

## Turn

1. Number one grew a trumpet on a stomach.
2. The top body grew a trumpet on a stomach.
3. A trumpet stood on the last stomach.

## Last line

1. The leaves kept the tank.
2. The tank stayed with the leaves.
3. Leaves held the water at the end.
"""


def beats(second_name):
    return [("Cold open", ["The first body in the tank was a leaf."]),
            (second_name, ["Number one grew a trumpet on a stomach."]),
            ("Close", ["The leaves kept the tank."])]


class KeyLines(unittest.TestCase):
    def errors(self, second_name):
        errors = []
        check.check_key_lines(KEY_LINES, beats(second_name), errors, "prose-1-key-lines.md")
        return errors

    def test_a_countdown_takes_its_turn_from_number_one(self):
        self.assertEqual(self.errors("Number 1"), [])

    def test_a_story_keeps_the_turn(self):
        self.assertEqual(self.errors("The turn"), [])

    def test_number_one_may_carry_its_name(self):
        self.assertEqual(self.errors('Number 1: Natogastrum trinubum, "swimming stomach"'), [])

    def test_number_ten_is_not_number_one(self):
        found = self.errors("Number 10: Remimixa gamaplaca")
        self.assertTrue(any("no beat named" in what for _, what in found), found)

    def test_neither_beat_is_refused(self):
        found = self.errors("The middle")
        self.assertTrue(any("no beat named" in what for _, what in found), found)

    def test_number_one_must_match_its_draft_line(self):
        errors = []
        changed = [("Cold open", ["The first body in the tank was a leaf."]),
                   ("Number 1", ["Something else opened number one."]),
                   ("Close", ["The leaves kept the tank."])]
        check.check_key_lines(KEY_LINES, changed, errors, "prose-1-key-lines.md")
        self.assertTrue(any(where.endswith("Turn") and "draft's line" in what for where, what in errors), errors)


if __name__ == "__main__":
    unittest.main()
