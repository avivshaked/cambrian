# The smell screen (logbook/specs/r65-prereg/predictions.md), on the coordinator's go only, after the bench and the identity
# sitting (rounds/bench-r65.ps1, rounds/identity-r65.ps1), from the worktree at the pre-registration's commit with its own
# Release farm. Two card groups of three founded for 24,000 s, one a seed, 6202 first: C, R and O on rounds/env-r65-c.ps1,
# -r.ps1 and -o.ps1. Group 32, the snow's transport on the card, the heat stop on (D258), checkpoints every 1,500 s, a 600 s
# wait for run.json, logical 20-29 at 10 threads, off a render's 16-19. -Seed runs one group alone (to resume or rerun it).
# Start it detached and hidden, with the worktree as the working directory.
# PowerShell's names ignore case: nothing here may be named $r beside $R.
param([string]$Worktree = 'D:\Projects\experiments\evolution-simulator\scratch\wt-r65',
      [ValidateSet('both', '6202', '6203')][string]$Seed = 'both')
$R = 'D:\Projects\experiments\evolution-simulator'
$b = $Worktree
$launchers = "$b\rounds\env-r65-c.ps1,$b\rounds\env-r65-r.ps1,$b\rounds\env-r65-o.ps1"
$seeds = if ($Seed -eq 'both') { @('6202', '6203') } else { @($Seed) }
foreach ($s in $seeds) {
    & pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name "r65-$s" -Arms "r65-c-$s,r65-r-$s,r65-o-$s" `
        -Launcher $launchers -Seeds "$s,$s,$s" -Seconds 24000 -Group 32 -Transport -CheckpointEvery 1500 -Threads 10 `
        -Mask 1072693248 -HeatStop -WallMinutes 600 -WaitForManifestSeconds 600 `
        -RunsRoot "$R\runs" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe"
    if ($LASTEXITCODE -ne 0) { "group r65-$s ended with exit $LASTEXITCODE; the next group is not started"; exit $LASTEXITCODE }
}
