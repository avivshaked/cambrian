# The mouth screen's identity sitting (logbook/specs/r66-prereg/predictions.md, "Before the screen", checks 2 to 4), on the
# card from this worktree's clean tree with its Release farm built, on the coordinator's card go. Logical 20-29, off a
# render's 16-19. Under scratch/r66/identity/<part>.
#   g32     C (rounds/env-r65-c.ps1, D275's switch off) and M (rounds/env-r66-m.ps1, the switch on) on seeds 6202 and
#           6203, one group of four, for 1,200 s, checkpoints every 300 s.
#   g64     M on both seeds at group 64, for 6,000 s, checkpoints every 1,500 s: past the first bites, near 3,800 s in
#           r65's C, which g32's 1,200 s never reached.
#   resume  M on both seeds resumed from g32's 1,200 s checkpoints to 6,000 s at group 32, as one group.
# Then:
#   python -I scripts/reads/compare-run-rows.py <runs/r65-c-<seed>/...> <C at 32> --from 0   (check 2: every row equal)
#   python scripts/reads/gpu-accept.py <M at 32> <M at 64>                                   (check 3: each exits 0)
#   python -I scripts/reads/compare-run-rows.py <M at 64> <M at 32> --from 0                (check 3: rows to 1,200 s)
#   python -I scripts/reads/compare-run-rows.py --from 1200 <M at 64> <M resumed>            (check 3: rows 1,200-6,000 s)
# and the landed share s from M's stats at 32, mouthBlows over mouthBlows plus mouthBlowsRefused (check 4).
# PowerShell's names ignore case: nothing here may be named $r beside $R.
param([ValidateSet('g32', 'g64', 'resume')][string]$Part = 'g32',
      [string]$Worktree = 'D:\Projects\experiments\evolution-simulator\scratch\wt-mouth')
$R = 'D:\Projects\experiments\evolution-simulator'
$b = $Worktree
$exe = "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe"
$root = "$R\scratch\r66\identity\$Part"
New-Item -ItemType Directory -Force $root | Out-Null
if ($Part -eq 'resume') {
    & pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name 'r66id-resume' -Arms 'r66id-mr-6202,r66id-mr-6203' -At 1200 `
        -ResumeFrom "$R\scratch\r66\identity\g32\r66id-m-6202,$R\scratch\r66\identity\g32\r66id-m-6203" -Seconds 4800 -Group 32 `
        -Transport -Threads 10 -Mask 1072693248 -HeatStop -WallMinutes 90 -WaitForManifestSeconds 600 -RunsRoot $root -Exe $exe
    exit $LASTEXITCODE
}
if ($Part -eq 'g64') {
    & pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name 'r66id-g64' -Arms 'r66id-m-6202,r66id-m-6203' `
        -Launcher "$b\rounds\env-r66-m.ps1,$b\rounds\env-r66-m.ps1" -Seeds '6202,6203' -Seconds 6000 `
        -Group 64 -Transport -CheckpointEvery 1500 -Threads 10 -Mask 1072693248 -HeatStop -WallMinutes 90 `
        -WaitForManifestSeconds 600 -RunsRoot $root -Exe $exe
    exit $LASTEXITCODE
}
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name 'r66id-g32' -Arms 'r66id-c-6202,r66id-c-6203,r66id-m-6202,r66id-m-6203' `
    -Launcher "$b\rounds\env-r65-c.ps1,$b\rounds\env-r65-c.ps1,$b\rounds\env-r66-m.ps1,$b\rounds\env-r66-m.ps1" `
    -Seeds '6202,6203,6202,6203' -Seconds 1200 -Group 32 -Transport -CheckpointEvery 300 -Threads 10 -Mask 1072693248 `
    -HeatStop -WallMinutes 60 -WaitForManifestSeconds 600 -RunsRoot $root -Exe $exe
exit $LASTEXITCODE
