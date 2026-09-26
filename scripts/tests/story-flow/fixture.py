"""A small story folder for the story flow's tests: a fake run with a guide, an arc, a writer's six
files that pass the check, and fake agent transcripts that `story-flow.py save` reads."""
import importlib.util
import io
import json
import math
import os
import shutil

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))


def tool():
    spec = importlib.util.spec_from_file_location("story_script", os.path.join(REPO, "scripts", "story-script.py"))
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m


def write(path, text):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with io.open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


NARRATION = {
    1: ["The tank is a disc of water lit from above, and every body in it grows from a recipe that its "
        "children inherit with small changes."],
    2: ["A leaf turns the light that falls on it into energy, and whatever a body does not spend goes into "
        "a reserve that carries it through the dark hours."],
    3: ["Near the end of the run, the leaf line that had grown a stomach was earning nearly three per cent "
        "of its living from the snow it caught, which nobody had predicted."],
}


def scene(n, paragraphs, chapter=None):
    t = tool()
    cues = []
    for p, text in enumerate(paragraphs):
        for k, cue in enumerate(t.split_cues(text)):
            cues.append((cue, p > 0 and k == 0))
    caps, end = t.retime([], cues)
    s = {"n": n, "run": "t-s1", "station": "Colony", "second": 3000 + 100 * n, "subject": "world",
         "seconds": math.ceil(end + t.TAIL), "captions": caps, "why": "the story's scene %d" % n}
    if chapter:
        s["chapter"] = chapter
    return s


def story():
    return {"title": "The leaf that kept a stomach", "version": 1,
            "arc": "A leaf line grew a stomach and kept it. Near the end it began to earn from it.",
            "arc_parts": {"followed": "the leaf line with a stomach", "turn": "a bud became a stomach that paid",
                          "kind": "bittersweet"},
            "runs": ["t-s1"], "screen_seconds": 60,
            "scenes": [scene(1, NARRATION[1], "How this world works"), scene(2, NARRATION[2]),
                       scene(3, NARRATION[3], "What the experiment found")]}


def make(root):
    """A fresh story folder under root, with its runs; returns (folder, runs_root, transcripts root)."""
    if os.path.isdir(root):
        shutil.rmtree(root)
    folder = os.path.join(root, "story")
    runs = os.path.join(root, "runs")
    run = os.path.join(runs, "t-s1", "2026-01-01-000000-test")
    write(os.path.join(run, "run.json"), json.dumps({"status": "finished"}))
    write(os.path.join(run, "guide", "guide.json"), json.dumps({"status": "finished"}))
    return folder, runs, os.path.join(root, "transcripts")


def arc_files(folder):
    write(os.path.join(folder, "arc.md"), "# The arc\n\nBittersweet. A leaf line grew a stomach.\n")
    write(os.path.join(folder, "arc-reads.md"), "# Reads\n\nnone\n")
    write(os.path.join(folder, "arc-choices.md"), "# Choices\n\n1. The kind.\n   Recommendation: bittersweet.\n")


def writer_files(folder, s=None):
    s = s or story()
    write(os.path.join(folder, "story.json"), json.dumps(s, ensure_ascii=False, indent=1) + "\n")
    write(os.path.join(folder, "checks.tsv"), "scene\titem\tshown\texact\tsource\thow_read\n"
          "3\tcaption 1\tnearly three per cent\t2.7%\truns/t-s1/absorptive.jsonl\tthe line's snow income over all\n")
    write(os.path.join(folder, "story.md"), "# The story\n\nA leaf line grew a stomach.\n")
    write(os.path.join(folder, "make_story.py"), "# the builder\n")
    write(os.path.join(folder, "writer-report.md"), "# Report\n\n3 scenes; %d\n" % len(json.dumps(s)))
    write(os.path.join(folder, "glossary-rows.md"), "none\n")


def agent(transcripts, agent_id, prompt, answer):
    """A fake agent transcript: its prompt as the first user message and its answer in a handback."""
    path = os.path.join(transcripts, "fake-project", "fake-session", "subagents", "agent-%s.jsonl" % agent_id)
    rows = [{"type": "user", "message": {"role": "user", "content": prompt}},
            {"type": "assistant", "message": {"content": [{"type": "text", "text": "working"}]}},
            {"type": "assistant", "message": {"content": [{"type": "tool_use", "name": "SubagentHandback",
                                                            "input": {"message": answer}}]}}]
    write(path, "".join(json.dumps(r) + "\n" for r in rows))
