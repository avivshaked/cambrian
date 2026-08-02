# 0004 — Two ways to report a success you don't have

**2026-08-02**  ·  Milestone 1

Both of today's bugs printed the word PASS. Neither was doing the thing it claimed.

## One: a check that couldn't see the failure

The Milestone 1 smoke test builds twelve random creatures, actuates them, steps physics, and
asserts two things — no NaN, and the bodies are moving. Twelve for twelve, PASS.

The creatures were the wrong shape.

Parts are parented to each other so PhysX sees an articulation chain. The first version put
each part's size on that same transform, then tried to cancel Unity's compounding of parent
scale into children by dividing componentwise. That is not the inverse operation. When a
child is rotated relative to a non-uniformly scaled parent, the compounding **shears** — and
no per-axis division undoes a shear.

Errors reached **1.09 m** in position and **0.72 m** in size, on creatures roughly a metre
across. Every one of them still moved, and none produced a NaN, so the assertions were
perfectly satisfied by geometry that bore no relation to the genome.

Fixed by not creating the problem: size now lives on the collider and on a separate visual
child, and every transform that positions a body stays at unit scale. The check now compares
built geometry against the phenotype *before* stepping physics, and it is the assertion that
would have caught this on day one.

**A check that only asks "did something happen" cannot tell a creature from a wrong
creature.** [0002](0002-the-spike-that-was-too-fast.md) was the same shape — a benchmark that
could not tell a simulated creature from a sleeping one.

## Two: a log line that was simply not true

Generating the sandbox scene, the script ran, exited 0, and logged:

```
[Evosim] Sandbox scene written to Assets/Scenes/Sandbox.unity
```

No such file existed.

`EditorSceneManager.SaveScene` fails when the target folder is missing. It reports that by
**returning false**, not by throwing. The script ignored the return value and logged success
unconditionally, so the message described what the code intended rather than what happened.

Caught only because the check afterwards was `Test-Path` on the actual file rather than
grepping the log for the success line — that is, because the verification did not go through
the same code that was lying.

## The common root

In both cases the failing component was also the reporting component. A build that reports
its own geometry is fine; a build that reports its own *correctness* is not. A save that
reports its own success is worthless when the failure mode is precisely that it did not save.

Two habits fall out, and they are cheap:

- **Check return values from anything that reports failure by returning it.** C# APIs that
  return `bool` instead of throwing are exactly where this hides, and Unity's editor API is
  full of them.
- **Verify from outside.** Assert on the artifact — the file on disk, the geometry in the
  scene — never on the log line emitted by the code under test. If the verification reads
  the subject's own account of itself, it verifies nothing.

That is the same conclusion as [0002](0002-the-spike-that-was-too-fast.md), reached twice
more in a single afternoon, which suggests it is not a lesson one learns once.
