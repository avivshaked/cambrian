# 0004 — Two ways to report a success you don't have

**2026-08-02**  ·  Milestone 1

Both of today's bugs printed the word PASS, and neither was doing the thing it claimed. One
was a test whose assertions could not see the failure sitting in front of them. The other
was a log line announcing a file it had never written. In both, the component doing the work
was also the component reporting on the work.

## A check that could not see the failure

The Milestone 1 smoke test builds twelve random creatures, drives them, steps the physics
and asserts two things. First, that no number has gone to NaN, the value a floating-point
calculation produces when it has failed. Second, that the bodies are moving. Twelve
creatures out of twelve passed both.

The creatures were the wrong shape.

Parts are parented to one another so that the physics engine sees a single articulated
chain. The first version put each part's size on that same parent-child transform. It then
tried to cancel Unity's habit of multiplying a parent's scale into its children, by dividing
component by component. That is not the inverse operation. When a child is rotated relative
to a parent scaled differently along different axes, the compounding shears the child. No
per-axis division undoes a shear.

Errors reached 1.09 m in position and 0.72 m in size, on creatures roughly a metre across.
Every one of them still moved, and none of them produced a NaN. The assertions were
satisfied by geometry that bore no relation to the genome it had been grown from.

The fix was to stop creating the problem. Size now lives on the collider and on a separate
visual child, and every transform that positions a body stays at unit scale. The check now
compares the built geometry against the phenotype, meaning the developed creature the genome
describes, before it steps the physics at all. That is the assertion that would have caught
this on day one.

A check that only asks whether something happened cannot tell a creature from a wrong
creature. That was also the shape of [0002](0002-the-spike-that-was-too-fast.md), a
benchmark that could not tell a simulated creature from a sleeping one.

## A log line that was not true

Generating the sandbox scene, the script ran, exited 0, and logged this:

```
[Evosim] Sandbox scene written to Assets/Scenes/Sandbox.unity
```

There was no such file on disk.

`EditorSceneManager.SaveScene` fails when the target folder is missing, and it reports that
failure by returning false rather than by throwing an exception. The script ignored the
return value and logged its success unconditionally. The message described what the code
intended. What the code did was something else.

I caught it only because the check that followed ran `Test-Path` against the actual file
instead of grepping the log for the success line. The verification did not go through the
same code that was lying.

## The root they share

In both cases the failing component was also the reporting component. A build that reports
its own geometry is fine. A build that reports its own correctness is not, and a save that
reports its own success is worthless when the failure mode is that it did not save.

Two habits fall out of this, and both are cheap.

Check the return value of anything that reports failure by returning it. C# methods that
hand back a `bool` instead of throwing are where this kind of bug hides, and Unity's editor
API is full of them.

Verify it from the outside. Assert on the artifact, the file on disk or the geometry in the
scene, and never on the log line the code under test emitted. A verification that reads its
subject's own account of itself verifies nothing.

That is the same conclusion as [0002](0002-the-spike-that-was-too-fast.md), reached twice
more in a single afternoon, which suggests it is not a lesson anybody learns once.
