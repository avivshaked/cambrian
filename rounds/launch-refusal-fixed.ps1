# The module rule's shape refusals split by cause on the build the forest worlds ran (logbook/specs/plants-hold/
# refusal-probe.md, "On the fixed build"). The probe build, probe/refusal-fixed 4dee4844 (scratch/wt-refusal-fixed,
# off plants-build 8676833f, never merged), only counts: its columns start at zero on a resume and the world steps as
# before. One card group of two worlds, from each one's 10,500 s checkpoint to 12,000 s, as their runs stepped them
# (group 32, the snow's transport on the card): pldr-6203 (the control with the most tries) as plrf-6203, and
# plp2-6204 (light at each part, the most modules standing) as plrf-6204. The probe's Core differs from the
# checkpoints', so the resume is allowed across the source hashes; each world's stats and lineage are then compared
# with its source's by scripts/reads/resume-match.py, which must find them equal before any count is read. Runs on
# the coordinator's go only, while no other farm and no Unity runs. Start it detached and hidden.
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-refusal-fixed"
$runs = "$R\scratch\plants-work\runs"
$exe = "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe"
$src = @("$runs\pldr-6203\2026-11-07-074804-27d24592", "$runs\plp2-6204\2026-11-07-150253-274b5f36")
$env:EVOSIM_ALLOW_SOURCE_MISMATCH = '1'
$g = @('-NoProfile', '-File', "$b\scripts\card\card-group.ps1", '-Name', 'plrf-group',
       '-Arms', 'plrf-6203,plrf-6204', '-At', '10500,10500', '-Until', 12000, '-ResumeFrom', ($src -join ','),
       '-CheckpointEvery', 1500, '-Group', 32, '-WallMinutes', 60, '-Transport', '-Threads', 12,
       '-Mask', 4293918720, '-HeatStop', '-WaitForManifestSeconds', 600, '-RunsRoot', $runs, '-Exe', $exe)
& pwsh @g
if ($LASTEXITCODE -ne 0) { "group plrf-group ended with exit $LASTEXITCODE"; exit $LASTEXITCODE }
"group plrf-group ended"
