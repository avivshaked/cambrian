# Round 67's three event shots, as the owner ruled on 2026-11-07 for the film (scratch/story-video/round-67-202611061206,
# shots-2-owner.md): the card resumes a world from the checkpoint before each shot and stops a few seconds before the
# event, writing a checkpoint there, so the film's host window starts from the run's own state. Three card groups, one
# after another, because -Until is one second for a whole group:
#   r67ev-24b: r67r1-6202 from 19,500 s to 20,410 s, before the birth at 20,425.5 s (shot 2.4b);
#   r67ev-64a: r67-6202 from 7,500 s to 8,555 s, the state r67r1-6202 began from, before the birth at 8,572 s (shot 6.4a);
#   r67ev-68a: r67r1-6202 from 10,500 s to 11,440 s, before the portrait of 13929 at 11,448 s (shot 6.8a).
# Each runs as round 67's worlds did: wt-card-kit's Release farm (its Core, Dynamics, Farm and Farm.Gpu unchanged since
# e79fa0fd, the build r67r1 ran on), the snow's transport on the card, the scent on the host, group 32, no exchange.
# A resume repeats a card run only on the card; each one's stats are compared with its source's over the span before
# the film uses it. Runs under runs/, beside their sources, since the film finds every shot's run under one root.
# Start it detached and hidden, on the coordinator's go.
param([string]$Only = '')
$R = 'D:\Projects\experiments\evolution-simulator'
$kit = "$R\scratch\wt-card-kit"
$exe = "$kit\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe"
$r1 = "$R\runs\r67r1-6202\2026-11-05-220032-029c8c74"
$r0 = "$R\runs\r67-6202\2026-11-05-210155-029c8c74"
$jobs = @(
    @{ Name = 'r67ev-24b'; Arm = 'r67ev-24b-6202'; From = $r1; At = 19500; Until = 20410 },
    @{ Name = 'r67ev-64a'; Arm = 'r67ev-64a-6202'; From = $r0; At = 7500; Until = 8555 },
    @{ Name = 'r67ev-68a'; Arm = 'r67ev-68a-6202'; From = $r1; At = 10500; Until = 11440 }
)
foreach ($j in $jobs) {
    if ($Only -and $j.Name -ne $Only) { continue }
    $g = @('-NoProfile', '-File', "$kit\scripts\card\card-group.ps1", '-Name', $j.Name, '-Arms', $j.Arm,
           '-At', $j.At, '-Until', $j.Until, '-ResumeFrom', $j.From, '-CheckpointEvery', 1500, '-Group', 32,
           '-WallMinutes', 60, '-Transport', '-Threads', 12, '-Mask', 4293918720, '-HeatStop',
           '-WaitForManifestSeconds', 600, '-RunsRoot', "$R\runs", '-Exe', $exe)
    & pwsh @g
    if ($LASTEXITCODE -ne 0) { "group $($j.Name) ended with exit $LASTEXITCODE; the rest are not started"; exit $LASTEXITCODE }
    "group $($j.Name) ended"
}
