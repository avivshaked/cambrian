# The hand-built strokers under PhysX, on worker 6. Self-collision off by default (the bodies
# never fold, so it should not matter; -SelfCollision 1 checks that).
param([int]$SelfCollision = 0)
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe'
$repo  = 'D:\Projects\experiments\evolution-simulator'
if (Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -match 'evolution-simulator\\unity-w6' }) { throw 'worker 6 is busy' }
$lock = "$repo\unity-w6\Temp\UnityLockfile"
if (Test-Path $lock) { [System.IO.File]::Delete($lock) }
& "$repo\scripts\new-worker.ps1" -Workers @(6) | Select-Object -First 1
$run = "$repo\runs\r42-s4\2026-09-21-051718-ff557bce"
$suffix = if ($SelfCollision -eq 1) { '-self' } else { '' }
$log = "$repo\scratch\logs\parity-stroker$suffix.log"
$env:EVOSIM_PARITY_SNAPSHOT = "$repo\logbook\specs\reactive-thrust-stroker\snapshot.jsonl"
$env:EVOSIM_PARITY_CONFIG   = "$run\config.json"
$env:EVOSIM_PARITY_OUT      = "$repo\scratch\solver-spike\stroker\traj-physx$suffix"
$env:EVOSIM_PARITY_COUNT    = '2'
$env:EVOSIM_PARITY_SECONDS  = '60'
$env:EVOSIM_PARITY_TAG      = 'r42-s4-20000'
$env:EVOSIM_PARITY_SELF_COLLISION = "$SelfCollision"
$p = Start-Process -FilePath $unity -PassThru -NoNewWindow -ArgumentList @(
    '-projectPath', "$repo\unity-w6", '-batchmode', '-nographics',
    '-executeMethod', 'Evosim.Theatre.EditorTools.ParitySwim.Run', '-logFile', $log)
$p.WaitForExit(1800000) | Out-Null
"exit $($p.ExitCode)"
Select-String -Path $log -Pattern 'error CS', 'could not be found', '\[ParitySwim\].*(genome\d|LOST|refused)' | ForEach-Object { $_.Line.Substring(0, [Math]::Min(300, $_.Line.Length)) } | Select-Object -First 12
