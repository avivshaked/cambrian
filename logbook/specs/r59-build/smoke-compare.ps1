# Round 59's step 8 CPU smoke, the comparison: r59smoke-t1 against r59smoke-t16 by their stats rows
# (wall-clock fields skipped), their digests and their last checkpoints' bytes, then CheckpointFidelity
# on the 16-thread run's last checkpoint for 100 s. Prints each tool's exit code.
$R = 'D:\Projects\experiments\evolution-simulator'
$w = "$R\scratch\wt-r59-anchor"
$exe = "$w\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe"
Push-Location $R
try {
    python "$w\scripts\compare-det.py" r59smoke-t1 r59smoke-t16 --skip wall
    Write-Host "compare-det exit $LASTEXITCODE"
    python "$w\scripts\digest-diff.py" r59smoke-t1 r59smoke-t16
    Write-Host "digest-diff exit $LASTEXITCODE"
    $a = Get-ChildItem "$R\runs\r59smoke-t1\*\checkpoints\*.ckpt" | Sort-Object Name | Select-Object -Last 1
    $b = Get-ChildItem "$R\runs\r59smoke-t16\*\checkpoints\*.ckpt" | Sort-Object Name | Select-Object -Last 1
    Write-Host "checkpoints: $($a.Name) and $($b.Name)"
    python "$w\logbook\specs\r59-build\ckpt-grip.py" $a.FullName $b.FullName
    Write-Host "ckpt-grip exit $LASTEXITCODE"
    & $exe --verify-checkpoint $b.FullName 100
    Write-Host "verify-checkpoint exit $LASTEXITCODE"
}
finally { Pop-Location }
