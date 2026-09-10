# Round launchers

One PowerShell script per round. A launcher holds that round's world in its defaults: every
knob the round was pre-registered with, written out where a reader can see it. Its header
comment names the tokens to check in the run report afterwards. It assembles the settings
block and calls [`../scripts/run-arm.ps1`](../scripts/run-arm.ps1), which starts the worker.

```powershell
./rounds/launch-r35.ps1 -Seed 3 -Worker 7
```

Seed and worker are what change between the arms of one round. Everything else has a
default, and the default is the round. That is why an entry cites a launcher by round number
instead of quoting a launch command: a command shows only what was overridden. The launcher
shows the whole world.

A launcher is not edited once its round has landed. The single exception is a header comment
that names a token to verify, which may be corrected. Anything else becomes a new launcher
with a new number, because editing an old one rewrites what an entry says was run, without
leaving a mark.

Settings are still read back from the run report's own header rather than from here
(CLAUDE.md, Commands). A launcher says what was asked for; the header says what the world
was given.

`launch-det.ps1` and `launch-fp.ps1` are the determinism and footprint probes. They launch
the same way without being rounds. The `queue-*.ps1` scripts are how rounds 13 to 16 started
the next arm on a worker as it came free; they are kept as they were, and are unused.
