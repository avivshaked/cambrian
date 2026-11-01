# The smell screen's identity sitting (logbook/specs/r65-prereg/predictions.md, "Checked before launch", step 2): C, R and O
# on seed 6202 (rounds/env-r65-c.ps1, -r.ps1, -o.ps1, R and O at the bench's half-scale) founded in one card group for
# 600 s, at -Group 32 and again at -Group 64, with checkpoints every 300 s. -Part resume then resumes the group-32 R and O
# from their 300 s checkpoints to 600 s, as one group, since a card group is two worlds or more. Then:
#   python scripts/reads/gpu-accept.py <world at 32> <world at 64>                          (each must exit 0)
#   python -I scripts/reads/compare-run-rows.py --from 300 <R at 32> <R resumed>             (no difference; O likewise)
#   python -I scripts/reads/compare-run-rows.py runs/r64t-t-6202 <C at 32>                   (reported, gates nothing)
# Under scratch/r65/identity/<part>. Run only on the coordinator's card go, from the worktree's clean tree with its Release
# farm built. Logical 20-29, off a render's 16-19.
# PowerShell's names ignore case: nothing here may be named $r beside $R.
param([ValidateSet('g32', 'g64', 'resume')][string]$Part = 'g32',
      [string]$Worktree = 'D:\Projects\experiments\evolution-simulator\scratch\wt-r65')
$R = 'D:\Projects\experiments\evolution-simulator'
$b = $Worktree
$exe = "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe"
$root = "$R\scratch\r65\identity\$Part"
New-Item -ItemType Directory -Force $root | Out-Null
if ($Part -eq 'resume') {
    & pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name 'r65id-resume' -Arms 'r65id-rr-6202,r65id-or-6202' -At 300 `
        -ResumeFrom "$R\scratch\r65\identity\g32\r65id-r-6202,$R\scratch\r65\identity\g32\r65id-o-6202" -Seconds 300 -Group 32 -Transport -Threads 10 `
        -Mask 1072693248 -HeatStop -WallMinutes 30 -WaitForManifestSeconds 600 -RunsRoot $root -Exe $exe
    exit $LASTEXITCODE
}
$group = [int]$Part.Substring(1)
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name "r65id-$Part" -Arms 'r65id-c-6202,r65id-r-6202,r65id-o-6202' `
    -Launcher "$b\rounds\env-r65-c.ps1,$b\rounds\env-r65-r.ps1,$b\rounds\env-r65-o.ps1" -Seeds '6202,6202,6202' -Seconds 600 `
    -Group $group -Transport -CheckpointEvery 300 -Threads 10 -Mask 1072693248 -HeatStop -WallMinutes 45 `
    -WaitForManifestSeconds 600 -RunsRoot $root -Exe $exe
exit $LASTEXITCODE
