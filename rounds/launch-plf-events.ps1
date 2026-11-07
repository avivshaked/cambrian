# The forest test's two fixed shots, as the owner ruled on 2026-11-07 for round 67's film
# (scratch/story-video/round-67-202611061206/card-forest-owner.md): one card resume of the forest run plf-6202 from
# its 7,500 s checkpoint, with checkpoints every 30 s, so that shot 9.9b (the stand's peak of 38 at 8,500 s) and 9.10a
# (its first plant starving, at 8,550 s) start a few seconds after an exact state. 1,120 s, to 8,620 s, past 9.10a's
# stretch, its 3 s handle and its 1 s tail (8,555.5 s). A card group holds two worlds or more, so the same checkpoint is
# resumed twice, as plfev-6202 and plfevb-6202; the film uses plfev-6202, and the second doubles as a check that the
# group steps both alike. Runs as plf-6202 ran (rounds/launch-plants-forest.ps1 at 1519f7cd): scratch/wt-plf's
# Release farm (that commit, its src unchanged), the snow's transport on the card, group 32. Each run's stats and
# lineage are compared with plf-6202's by scripts/reads/resume-match.py before the film uses it. Runs beside its source
# under scratch/plants-work/runs. Runs on the coordinator's go only, with no Unity and no other farm running. Start it
# detached and hidden.
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-plf"
$runs = "$R\scratch\plants-work\runs"
$exe = "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe"
$src = "$runs\plf-6202\2026-11-06-134402-c7e97670"
$g = @('-NoProfile', '-File', "$b\scripts\card\card-group.ps1", '-Name', 'plfev-group',
       '-Arms', 'plfev-6202,plfevb-6202', '-At', '7500,7500', '-Seconds', 1120, '-ResumeFrom', "$src,$src",
       '-CheckpointEvery', 30, '-Group', 32, '-WallMinutes', 60, '-Transport', '-Threads', 12, '-Mask', 4293918720,
       '-HeatStop', '-WaitForManifestSeconds', 600, '-RunsRoot', $runs, '-Exe', $exe)
& pwsh @g
if ($LASTEXITCODE -ne 0) { "group plfev-group ended with exit $LASTEXITCODE"; exit $LASTEXITCODE }
"group plfev-group ended"
