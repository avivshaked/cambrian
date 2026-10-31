# The anchoring build's switch-on checks (logbook/specs/anchor-brain-build.md, "After the build", items 3 and 4), from
# scratch/wt-anchor's clean tree with its Release farm built, on the coordinator's go only. The world is
# rounds/env-anchor-brain-check.ps1 (r64 arm A with the anchoring cell at bud weight 1 and EVOSIM_ANCHOR_BRAIN=1), on
# seeds 6202 and 6203, two worlds since a group needs two. Logical 8 to 15 at 8 threads, under scratch/anchor-hold/on/.
#   -Step cpu      founded on ILGPU's CPU accelerator for 30 s, migrating every 10 s, checkpoints every 15 s
#   -Step cpuresume the same two worlds resumed from the cpu step's checkpoints at 15 s for 15 s
#   -Step g32 / g64 founded on the card for 1,200 s at group 32 or 64, the snow's transport on the card, the heat stop on
# Each step is compared after it: cpuresume against cpu with compare-run-rows.py --from 15, and g64 against g32 with
# gpu-accept.py. PowerShell's names ignore case: nothing here may be named $r beside $R.
param([ValidateSet('cpu', 'cpuresume', 'g32', 'g64')][string]$Step)
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-anchor"
$world = "$b\rounds\env-anchor-brain-check.ps1"
$exe = "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe"
$group = "$b\scripts\card\card-group.ps1"
$root = "$R\scratch\anchor-hold\on\$Step"
New-Item -ItemType Directory -Force $root | Out-Null
$arms = "abchk-$Step-6202,abchk-$Step-6203"
$found = @('-Launcher', "$world,$world", '-Seeds', '6202,6203')
$common = @('-Threads', '8', '-Mask', '65280', '-WaitForManifestSeconds', '600', '-RunsRoot', $root, '-Exe', $exe)
switch ($Step) {
    'cpu' {
        & pwsh -NoProfile -File $group -Name "abchk-$Step" -Arms $arms -Device cpu -Seconds 30 -MigrateEvery 10 -CheckpointEvery 15 -WallMinutes 90 @found @common
    }
    'cpuresume' {
        $src = @('6202', '6203') | ForEach-Object { (Get-ChildItem "$R\scratch\anchor-hold\on\cpu\abchk-cpu-$_" -Directory | Sort-Object Name | Select-Object -Last 1).FullName }
        & pwsh -NoProfile -File $group -Name "abchk-$Step" -Arms $arms -Device cpu -ResumeFrom ($src -join ',') -At 15 -Seconds 15 -MigrateEvery 10 -CheckpointEvery 15 -WallMinutes 90 @common
    }
    default {
        $g = if ($Step -eq 'g32') { 32 } else { 64 }
        & pwsh -NoProfile -File $group -Name "abchk-$Step" -Arms $arms -Seconds 1200 -Group $g -Transport -MigrateEvery 10 -CheckpointEvery 1500 -HeatStop -WallMinutes 60 @found @common
    }
}
exit $LASTEXITCODE
