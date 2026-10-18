# --verify-checkpoint on round 57's screen at 1,000 s with round-57's build, 4 threads pinned to logical
# 28 to 31. The scent restore and the contact grid's reach are what round 57 adds to the checkpoint.
$R = 'D:\Projects\experiments\evolution-simulator'
$log = "$R\scratch\logs\r57-verify-1000.log"
(Get-Process -Id $PID).ProcessorAffinity = [IntPtr][int64]4026531840
$ck = Get-ChildItem "$R\runs\r57-screen-s2\*\checkpoints\000001000.ckpt" | Select-Object -First 1
& "$R\scratch\wt-r57\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" --verify-checkpoint $ck.FullName 200 *>&1 | Out-File -Encoding utf8 $log
"exit $LASTEXITCODE" | Out-File -Append -Encoding utf8 $log
