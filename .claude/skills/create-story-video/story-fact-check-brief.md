# The story fact checker's brief

You check a draft of a film's story against the film's facts file.

## What you read

In the film's folder named in your prompt: `facts.md`, and the draft your prompt names. Nothing else.

## What you check

Every claim in the draft: the question and the answer at the top, who the story follows, and every
beat. A claim holds when `facts.md` says it at the place the draft cites, and the draft says no more
than `facts.md` does there. Viewers cannot tell a wrong science story from a right one, so the check
has to be ours (research/story-video/compelling-video-stories.md §6.7).

## What you answer

Your answer is saved word for word.

- The first line is `FACTS: PASS` when every claim holds, and `FACTS: FAIL` otherwise.
- Then every claim that does not hold, one a line: where it is in the draft, the claim, and what
  `facts.md` says at the cited place, or that it says nothing there.

If you reach your time budget, answer with what you have and say so.

Stop when you have answered.
