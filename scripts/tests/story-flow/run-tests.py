"""The story flow's tests: every stage walked on a small synthetic story, the readers' answers given
as fake transcripts, and the failure paths (a failing draft, three of them, a false line in the last
round, the owner's changes). Run: python scripts/tests/story-flow/run-tests.py"""
import io
import json
import os
import subprocess
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fixture  # noqa: E402

REPO = fixture.REPO
FLOW = os.path.join(REPO, "scripts", "story-flow.py")
ENTRY = os.path.join(REPO, "logbook", "0122-the-eaters-that-lasted-caught-light.md")
PREREG = os.path.join(REPO, "logbook", "0121-a-founder-earns-its-last-tenth.md")
ROOT = os.path.join(REPO, "scratch", "tests", "story-flow")
failures, passed = [], 0
ids = iter(range(1000, 100000))


def run(*args, ok=True):
    r = subprocess.run([sys.executable, FLOW] + list(args), capture_output=True, text=True, encoding="utf-8",
                       env=dict(os.environ, EVOSIM_TRANSCRIPTS=TRANSCRIPTS))
    if ok and r.returncode != 0:
        raise AssertionError("story-flow.py %s exited %d:\n%s%s" % (" ".join(args[:2]), r.returncode, r.stdout, r.stderr))
    return r.stdout + r.stderr


def check(name, cond, detail=""):
    global passed
    if cond:
        passed += 1
    else:
        failures.append("%s %s" % (name, detail))
        print("FAIL", name, detail[:600])


def flow():
    with io.open(os.path.join(FOLDER, "flow.json"), encoding="utf-8") as f:
        return json.load(f)


def answer(role, text):
    """Answers the pending launch of a role with a fake agent and saves it."""
    p = next(p for p in flow()["pending"] if p["role"] == role)
    aid = "t%d" % next(ids)
    fixture.agent(TRANSCRIPTS, aid, "Some preamble the harness adds.\n" + p["prompt"], text)
    return run("save", FOLDER, aid, ok=False)


def owner(stage, words, verdict):
    path = os.path.join(FOLDER, "owner-reply.txt")
    fixture.write(path, words)
    return run("owner", FOLDER, stage, path, "--verdict", verdict)


def edit_page(old, new):
    p = os.path.join(FOLDER, "script.md")
    with io.open(p, encoding="utf-8") as f:
        text = f.read()
    assert old in text, old
    fixture.write(p, text.replace(old, new))


def start(name):
    global FOLDER, RUNS, TRANSCRIPTS
    FOLDER, RUNS, TRANSCRIPTS = fixture.make(os.path.join(ROOT, name))
    run("init", FOLDER, "--round", "t", "--runs", "t-s1", "--runs-root", RUNS)
    run("set", FOLDER, "entry", ENTRY)
    run("set", FOLDER, "prereg", PREREG)


def to_script_stage():
    """Walks a fresh story to the script stage's first editor."""
    out = run("next", FOLDER)
    check("arc: the writer's first pass is launched", "launch the writer-arc" in out, out)
    fixture.arc_files(FOLDER)
    out = run("next", FOLDER)
    check("arc: the owner rules", "next (owner)" in out and os.path.isfile(
        os.path.join(REPO, "scratch", "owner", "story-t-arc.md")), out)
    out = owner("arc", "1. agree\n", "approve")
    check("arc: approved", "arc approved       done" in out, out)
    check("write: the writer's second pass is launched next", "writer-story" in json.dumps(flow()["pending"]) or
          "the writer's second pass" in out, out)
    run("next", FOLDER)
    fixture.writer_files(FOLDER)
    out = run("next", FOLDER)
    check("draft: the check is clean and the readers are launched",
          "launch the listener" in out and "launch the facts-draft" in out, out)
    out = answer("listener", "VERDICT: PASS\n")
    check("draft: the listener's PASS is saved", "saved the listener" in out and os.path.isfile(
        os.path.join(FOLDER, "drafts", "1", "listen.md")), out)
    answer("facts-draft", "row 1 | TRUE\nVERDICT: 0 false of 1\n")
    out = run("next", FOLDER)
    check("script: round 1's editor is launched", "launch the editor" in out and os.path.isfile(
        os.path.join(FOLDER, "script.md")), out)
    return out


def happy_path():
    start("happy")
    to_script_stage()
    edit_page("goes into a reserve that carries it through the dark hours.",
              "goes into a reserve, which carries it through the dark hours.")
    fixture.write(os.path.join(FOLDER, "script", "1", "editor.md"), "| scene | change |\n|---|---|\n| 2 | joined |\n")
    out = run("next", FOLDER)
    check("script: applied, checked, the fact checker and the cold reader launched",
          "launch the facts-script" in out and "launch the cold" in out and
          os.path.isfile(os.path.join(FOLDER, "script", "1", "cold-page.md")), out)
    with io.open(os.path.join(FOLDER, "script", "1", "edits.tsv"), encoding="utf-8") as f:
        rows = f.read().splitlines()
    check("script: the round's edits are extracted", len(rows) >= 2 and all(r.endswith("\teditor") for r in rows[1:]),
          repr(rows))
    answer("facts-script", "scene 2, line 1 | TRUE\nVERDICT: 0 false of 1\n")
    answer("cold", "1. STUMBLES: 0\n2. none stand out\n3. A leaf line grew a stomach. Near the end it earned from it.\n")
    out = run("next", FOLDER)
    check("script: the arc comparer is launched on the pair", "launch the arc-compare" in out and os.path.isfile(
        os.path.join(FOLDER, "script", "1", "arc-pair.md")), out)
    answer("arc-compare", "TURN: TOLD\n")
    out = run("next", FOLDER)
    check("script: a clean round closes the stage", "script             done" in out, out)
    check("script approved: the owner reads the script", "next (owner)" in out and os.path.isfile(
        os.path.join(REPO, "scratch", "owner", "story-t-script.md")), out)
    out = owner("script", "approved\n", "approve")
    check("script approved: done, and narration comes next", "script approved    done" in out and
          "narrate" in out, out)
    out = run("skip", FOLDER, "narrate", "--note", "no service yet")
    check("narrate: skipped, and the check comes next", "narrate            skipped" in out and "-Check" in out, out)


def failing_drafts():
    start("drafts")
    out = run("next", FOLDER)
    fixture.arc_files(FOLDER)
    run("next", FOLDER)
    owner("arc", "1. agree\n", "approve")
    run("next", FOLDER)
    bad = fixture.story()
    bad["scenes"][1]["captions"][0]["text"] = "A leaf catches light. It makes energy. The energy is saved."
    fixture.writer_files(FOLDER, bad)
    out = run("next", FOLDER)
    check("draft 1: a check ERROR fails it and relaunches the writer",
          "draft 1 failed: " in out and "launch the writer-story" in out, out)
    with io.open(os.path.join(FOLDER, "drafts", "1", "check.txt"), encoding="utf-8") as f:
        check("draft 1: the check's file names the list", "short sentences in a row" in f.read())
    for k in (2, 3):
        s = fixture.story()
        s["title"] = "The leaf that kept a stomach" + (", again" if k == 2 else ", once more")
        fixture.writer_files(FOLDER, s)
        out = run("next", FOLDER)
        check("draft %d: clean check, readers launched" % k, "launch the listener" in out, out)
        answer("listener", 'scene 2, line 1 | written | "carries it through the dark hours"\nVERDICT: FAIL\n')
        answer("facts-draft", "row 1 | TRUE\nVERDICT: 0 false of 1\n")
        out = run("next", FOLDER)
    check("draft 3: three failures in a row go to the owner", "next (owner)" in out and "3 drafts in a row" in out, out)
    out = owner("draft", "Let it through; the editor can fix that line.\n", "pass")
    check("draft 3: the owner's pass moves on to the script stage", "draft              done" in out and
          "launch the editor" in run("next", FOLDER), out)
    check("draft 3: the owner's words sit beside the draft",
          os.path.isfile(os.path.join(FOLDER, "drafts", "3", "owner.md")))
    fixture.agent(TRANSCRIPTS, "stray", "A prompt the flow never printed.", "VERDICT: PASS\n")
    msg = run("save", FOLDER, "stray", ok=False)
    check("save: an answer to no pending prompt is refused", "not launched with a prompt" in msg, msg)


def bad_answers():
    start("answers")
    run("next", FOLDER)
    fixture.arc_files(FOLDER)
    run("next", FOLDER)
    owner("arc", "1. agree\n", "approve")
    run("next", FOLDER)
    fixture.writer_files(FOLDER)
    run("next", FOLDER)
    msg = answer("listener", "The draft reads well to me.\n")
    check("save: an answer with no verdict is refused and kept aside", "no verdict" in msg and any(
        n.startswith("listen.invalid-") for n in os.listdir(os.path.join(FOLDER, "drafts", "1"))), msg)
    out = run("next", FOLDER)
    check("save: the refused reader is launched again", "launch the listener" in out, out)
    msg = answer("listener", 'scene 1, line 1 | written | "x"\nVERDICT: PASS\n')
    check("save: a PASS that lists a fault is refused", "no verdict" in msg, msg)
    run("next", FOLDER)
    msg = answer("facts-draft", "row 1 | FALSE | 2.7 is not nearly three\nVERDICT: 0 false of 1\n")
    check("save: a count that disagrees with its FALSE lines is refused", "no verdict" in msg, msg)


def set_paragraph(scene_n, text):
    """Replaces a scene's one narration line on the page, as an editor would."""
    p = os.path.join(FOLDER, "script.md")
    with io.open(p, encoding="utf-8") as f:
        lines = f.read().splitlines()
    i = next(i for i, l in enumerate(lines) if l.startswith("### Scene %d " % scene_n))
    j = next(j for j in range(i + 1, len(lines)) if lines[j].strip() and not lines[j].startswith(">"))
    lines[j] = text
    fixture.write(p, "\n".join(lines) + "\n")


def last_round_revert():
    start("revert")
    to_script_stage()
    endings = ("over and over", "again and again", "time after time")
    for n in (1, 2, 3):
        set_paragraph(1, fixture.NARRATION[1][0][:-1] + ", " + endings[n - 1] + ".")
        fixture.write(os.path.join(FOLDER, "script", str(n), "editor.md"), "| scene | change |\n")
        out = run("next", FOLDER)
        check("round %d: readers launched" % n, "launch the cold" in out, out)
        answer("facts-script", "scene 1, line 1 | FALSE | a claim | no source\nVERDICT: 1 false of 1\n")
        answer("cold", "STUMBLES: 1\nscene 1, line 1, a word\n2. x\n3. A leaf line kept a stomach.\n")
        run("next", FOLDER)
        answer("arc-compare", "TURN: TOLD\n")
        out = run("next", FOLDER)
        if n < 3:
            check("round %d: an unclean round begins the next" % n, "round %d" % (n + 1) in out and
                  "launch the editor" in run("next", FOLDER), out)
    check("round 3: the round limit closes the stage", "script             done" in out, out)
    s = json.load(io.open(os.path.join(FOLDER, "story.json"), encoding="utf-8"))
    text = " ".join(c["text"] for c in s["scenes"][0]["captions"])
    check("round 3: the false scene has the writer's words back", text == fixture.NARRATION[1][0], text)
    with io.open(os.path.join(FOLDER, "edits.tsv"), encoding="utf-8") as f:
        check("round 3: the revert is logged as the revert's", "\trevert" in f.read())


def owner_changes():
    start("changes")
    happy_path_to_approval()
    out = owner("script", "Please add a scene about the placer fault, and say 'reserve' less.\n", "changes")
    check("changes: an owner's round opens and the approval is withdrawn",
          "script approved" not in out or "open" in out, out)
    out = run("next", FOLDER)
    check("changes: the editor is launched on the owner's round", "launch the editor" in out and os.path.isfile(
        os.path.join(FOLDER, "script", "2", "owner.md")), out)
    fixture.write(os.path.join(FOLDER, "script", "2", "editor.md"), "| scene | change |\n")
    fixture.write(os.path.join(FOLDER, "script", "2", "for-writer.md"),
                  "Please add a scene about the placer fault.\n")
    run("next", FOLDER)
    answer("facts-script", "VERDICT: 0 false of 0\n")
    answer("cold", "STUMBLES: 2\na\nb\n2. x\n3. A leaf line kept a stomach.\n")
    run("next", FOLDER)
    answer("arc-compare", "TURN: TOLD\n")
    out = run("next", FOLDER)
    out = run("next", FOLDER) if "sends the changes" in out else out
    check("changes: the writer gets changes.md and the script stage is set aside",
          os.path.isfile(os.path.join(FOLDER, "changes.md")) and
          os.path.isdir(os.path.join(FOLDER, "superseded", "script-1")) and "launch the writer-story" in out, out)
    s = fixture.story()
    s["scenes"].append(fixture.scene(4, ["A newcomer's leaves were set far too deep, where the light is thin, and "
                                         "they died within minutes of arriving."]))
    fixture.writer_files(FOLDER, s)
    out = run("next", FOLDER)
    check("changes: the writer's new story is reviewed as a new draft", "draft 2" in out and
          "launch the listener" in out, out)


def happy_path_to_approval():
    to_script_stage()
    fixture.write(os.path.join(FOLDER, "script", "1", "editor.md"), "| scene | change |\n")
    run("next", FOLDER)
    answer("facts-script", "VERDICT: 0 false of 0\n")
    answer("cold", "STUMBLES: 0\n2. x\n3. A leaf line kept a stomach and earned from it.\n")
    run("next", FOLDER)
    answer("arc-compare", "TURN: TOLD\n")
    run("next", FOLDER)
    owner("script", "approved\n", "approve")


for test in (happy_path, failing_drafts, bad_answers, last_round_revert, owner_changes):
    try:
        test()
    except Exception as e:  # a test that throws is a failure, and the others still run
        import traceback
        failures.append("%s threw: %s" % (test.__name__, e))
        traceback.print_exc()
print("story flow tests: %d passed, %d failed" % (passed, len(failures)))
sys.exit(1 if failures else 0)
