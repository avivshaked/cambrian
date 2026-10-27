# The structural mutation build's card identity sitting (logbook/specs/r62sm-prereg/predictions.md, "The build"), from
# scratch/wt-struct (branch struct: growth with the seven structural chances bound in Evosim.Farm, each defaulting to
# today's rate). One card group of two worlds founded on seed 6201 for 600 s: r62sid-t60 on growth group 1's world
# (rounds/env-r62gr-t60.ps1) with the new bindings unset, which must be r62gr-t60-6201 bit for bit on the prefix
# (scripts/reads/gpu-accept.py against runs/r62gr-t60-6201), and r62sid-s10 on the screen's tenth-rate arm
# (rounds/env-r62sm-s10.ps1), whose config.json must carry the scaled chances. On logical 22 to 31 at 10 threads, as
# the screen runs. Runs under scratch/struct-build/identity. Run only on the coordinator's card go, from
# scratch/wt-struct's clean tree with its Release farm built, with that worktree as the working directory.
# PowerShell's names ignore case: nothing here may be named $r beside $R.
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-struct"
New-Item -ItemType Directory -Force "$R\scratch\struct-build\identity" | Out-Null
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name r62sid -Arms 'r62sid-t60-6201,r62sid-s10-6201' `
    -Launcher "$b\rounds\env-r62gr-t60.ps1,$b\rounds\env-r62sm-s10.ps1" -Seeds '6201,6201' -Seconds 600 -Group 32 -Transport `
    -Threads 10 -Mask 4290772992 -HeatStop -WallMinutes 45 -WaitForManifestSeconds 600 `
    -RunsRoot "$R\scratch\struct-build\identity" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" `
    -AllowBeside 'Assay\.Run\.dll'
