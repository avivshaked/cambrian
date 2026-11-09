# The module rule's shape refusals split by cause on the build the forest worlds ran (logbook/specs/plants-hold/
# refusal-probe.md, "On the fixed build"). The probe build, probe/refusal-fixed 4dee4844 (scratch/wt-refusal-fixed,
# off plants-build 8676833f, never merged), only counts: its columns start at zero on a resume and the world steps as
# before. Two worlds, each from its 10,500 s checkpoint to 12,000 s, as their runs stepped them (group 32, the snow's
# transport on the card): pldr-6203 (the control with the most tries) and plp2-6204 (light at each part, the most
# modules standing). A resume on plants-build does not repeat its source (forest-run.md), so they run twice, as two
# card groups one after the other: plrc-* on plants-build's own farm (scratch/wt-plants-build, a270a02a, with the grip
# fix), then plrf-* on the probe's, which since 2026-11-09 is probe/refusal-fixed-grip 1b220d02, the probe commit on
# a270a02a (refusal-probe.md). scripts/reads/resume-match.py must find each plrf run equal to its plrc twin before any count is
# read. The probe's Core differs from the checkpoints', so its resume is allowed across the source hashes. Runs on the
# coordinator's go only, while no other farm and no Unity runs. Start it detached and hidden.
$R = 'D:\Projects\experiments\evolution-simulator'
$runs = "$R\scratch\plants-work\runs"
$src = @("$runs\pldr-6203\2026-11-07-074804-27d24592", "$runs\plp2-6204\2026-11-07-150253-274b5f36")
foreach ($pair in @(@('plrc', "$R\scratch\wt-plants-build", '0'), @('plrf', "$R\scratch\wt-refusal-fixed", '1'))) {
    $name, $b, $allow = $pair
    if ($allow -eq '1') { $env:EVOSIM_ALLOW_SOURCE_MISMATCH = '1' }
    $g = @('-NoProfile', '-File', "$b\scripts\card\card-group.ps1", '-Name', "$name-group",
           '-Arms', "$name-6203,$name-6204", '-At', '10500,10500', '-Until', 12000, '-ResumeFrom', ($src -join ','),
           '-CheckpointEvery', 1500, '-Group', 32, '-WallMinutes', 60, '-Transport', '-Threads', 12,
           '-Mask', 4293918720, '-HeatStop', '-WaitForManifestSeconds', 600, '-RunsRoot', $runs,
           '-Exe', "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe")
    & pwsh @g
    if ($LASTEXITCODE -ne 0) { "group $name-group ended with exit $LASTEXITCODE"; exit $LASTEXITCODE }
    "group $name-group ended"
}
