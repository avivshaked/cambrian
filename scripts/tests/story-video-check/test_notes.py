"""What the prose check fails, and what it only notes (.claude/skills/create-story-video/script-check.py).

    python scripts/tests/story-video-check/test_notes.py

A draft fails only on what the voice cannot speak or the science forbids. The habits of model prose
are noted for the owner and never fail it, since a rule obeyed by rewrite kills good prose faster
than the habit does (prose.md, "The check"). The sentences below come from a passage the owner
liked, which the earlier check failed on its contrast of two numbers.
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


def rules(*sentences):
    entries = [(f"beat 1, sentence {i + 1}", s) for i, s in enumerate(sentences)]
    errors = []
    flagged, noted, _ = check.sentence_rules(entries, errors)
    return flagged, noted


class FailsOrNotes(unittest.TestCase):
    def test_two_numbers_that_collide_pass(self):
        flagged, _ = rules("In ten seconds it took in more than the snow brings in two and a half minutes.")
        self.assertEqual(flagged, {})

    def test_short_lines_pass(self):
        flagged, noted = rules("No children.", "The first of its kind, and the last.",
                               "And that was it.", "Nothing like that meal ever came again.")
        self.assertEqual(flagged, {})

    def test_wanting_still_fails(self):
        flagged, _ = rules("It learned to swim toward the snow.")
        self.assertIn(0, flagged)

    def test_what_the_voice_misreads_still_fails(self):
        flagged, _ = rules("The world dropped it in: no parent.", "It lived 12 minutes.")
        self.assertEqual(sorted(flagged), [0, 1])

    def test_an_appraising_word_is_only_noted(self):
        flagged, noted = rules("It was a remarkable creature.")
        self.assertEqual(flagged, {})
        self.assertIn(0, noted)


class Sample(unittest.TestCase):
    """A sample holds some of the story's beats, keeping their numbers (prose.md, step 0)."""
    STORY = [(1, "Cold open"), (2, "Number 10"), (3, "Number 9"), (4, "Number 8")]
    TEXT = "# A film\n\n## 2. Number 10\n\nA leaf.\n\n## 4. Number 8\n\nA mouth.\n\n## Notes\n\nNone.\n"

    def test_a_sample_may_skip_beats(self):
        prose, errors = check.parse_prose(self.TEXT, sample=True)
        check.same_beats(prose["beats"], self.STORY, errors, sample=True)
        self.assertEqual(errors, [])

    def test_a_draft_may_not(self):
        prose, errors = check.parse_prose(self.TEXT)
        check.same_beats(prose["beats"], self.STORY, errors)
        self.assertTrue(errors)

    def test_a_sample_keeps_the_story_order(self):
        text = "# A film\n\n## 4. Number 8\n\nA mouth.\n\n## 2. Number 10\n\nA leaf.\n"
        prose, errors = check.parse_prose(text, sample=True)
        check.same_beats(prose["beats"], self.STORY, errors, sample=True)
        self.assertTrue(errors)


if __name__ == "__main__":
    unittest.main()
