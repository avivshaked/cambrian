# The smell screen's founding bench (logbook/specs/r65-prereg/predictions.md, "The founding bench sets the half-scale"):
# arm R (rounds/env-r65-r.ps1, at the default half-scale of 1) founded on seeds 6202 and 6203 in one card group for 6,000 s.
# Then, for each world:
#   python -I scripts/reads/smell/plume-bench.py <run> --seconds all --json
# and k is the median of 6202's figure over the dumps from 4,500 to 6,000 s. Under scratch/r65/bench. Run only on the
# coordinator's card go, from the worktree's clean tree with its Release farm built. Logical 20-29, off a render's 16-19.
# PowerShell's names ignore case: nothing here may be named $r beside $R.
param([string]$Worktree = 'D:\Projects\experiments\evolution-simulator\scratch\wt-r65')
$R = 'D:\Projects\experiments\evolution-simulator'
$b = $Worktree
$root = "$R\scratch\r65\bench"
New-Item -ItemType Directory -Force $root | Out-Null
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name r65bench -Arms 'r65bench-r-6202,r65bench-r-6203' `
    -Launcher "$b\rounds\env-r65-r.ps1,$b\rounds\env-r65-r.ps1" -Seeds '6202,6203' -Seconds 6000 -Group 32 -Transport `
    -CheckpointEvery 1500 -Threads 10 -Mask 1072693248 -HeatStop -WallMinutes 120 -WaitForManifestSeconds 600 `
    -RunsRoot $root -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe"
exit $LASTEXITCODE
