"""The prose check's silent channel intro (.claude/skills/create-story-video/script-check.py).

    python scripts/tests/story-video-check/test_channel_intro.py

The owner's intro card plays under a beat named "Channel intro", so that beat keeps its heading and
has no words under it (prose-brief.md). Every other beat still needs prose.
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


def prose(intro_words="", second_beat_words="Number ten is a leaf."):
    return ("# A film\n\n## 1. Cold open\n\nThe first creature was a leaf.\n\n"
            f"## 2. Channel intro\n\n{intro_words}\n\n"
            f"## 3. Number 10\n\n{second_beat_words}\n\n## Notes\n\nNone.\n")


class ChannelIntro(unittest.TestCase):
    def errors(self, text):
        return check.parse_prose(text)[1]

    def test_a_silent_intro_passes(self):
        self.assertEqual(self.errors(prose()), [])

    def test_words_under_the_intro_are_refused(self):
        found = self.errors(prose(intro_words="Welcome to the channel."))
        self.assertTrue(any(where == "beat 2" and "channel intro" in what for where, what in found), found)

    def test_another_empty_beat_is_still_refused(self):
        found = self.errors(prose(second_beat_words=""))
        self.assertTrue(any(where == "beat 3" and "no prose" in what for where, what in found), found)

    def test_the_intro_says_nothing(self):
        parsed, _ = check.parse_prose(prose())
        self.assertNotIn("beat 2", " ".join(where for where, _ in check.prose_entries(parsed)))


if __name__ == "__main__":
    unittest.main()
