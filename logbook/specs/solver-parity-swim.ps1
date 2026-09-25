# The parity swims under PhysX: round 42 seed 4's genomes alone in still water, on main's
# worker 6 refreshed for Assets/Theatre/Editor/ParitySwim.cs. -SelfCollision 0 runs the other half.
# -N sets how many genomes (EVOSIM_PARITY_COUNT); 40 is the set the spike's traj40 holds.
param([int]$SelfCollision = 1, [int]$N = 40)
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe'
$repo  = 'D:\Projects\experiments\evolution-simulator'
if (Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -match 'evolution-simulator\\unity-w6' }) { throw 'worker 6 is busy' }
$lock = "$repo\unity-w6\Temp\UnityLockfile"
if (Test-Path $lock) { [System.IO.File]::Delete($lock) }
& "$repo\scripts\new-worker.ps1" -Workers @(6) | Select-Object -First 1
$run = "$repo\runs\r42-s4\2026-09-21-051718-ff557bce"
$suffix = if ($SelfCollision -eq 1) { '' } else { '-noself' }
$log = "$repo\scratch\logs\parity-swim$suffix.log"
$env:EVOSIM_PARITY_SNAPSHOT = "$run\snapshots\000020000.jsonl"
$env:EVOSIM_PARITY_CONFIG   = "$run\config.json"
$env:EVOSIM_PARITY_OUT      = "$repo\scratch\solver-spike\traj-physx$suffix"
$env:EVOSIM_PARITY_COUNT    = "$N"
$env:EVOSIM_PARITY_SECONDS  = '60'
$env:EVOSIM_PARITY_SELF_COLLISION = "$SelfCollision"
$p = Start-Process -FilePath $unity -PassThru -NoNewWindow -ArgumentList @(
    '-projectPath', "$repo\unity-w6", '-batchmode', '-nographics',
    '-executeMethod', 'Evosim.Theatre.EditorTools.ParitySwim.Run', '-logFile', $log)
$p.WaitForExit(1800000) | Out-Null
"exit $($p.ExitCode)"
Select-String -Path $log -Pattern 'error CS', 'could not be found', '\[ParitySwim\].*(genome|LOST|solverIterations)' | ForEach-Object { $_.Line.Substring(0, [Math]::Min(260, $_.Line.Length)) }

# The probe. These two greps are what settles the drive-axis question; both files land beside
# the bench's own, which scripts/../solver-spike/probe/bench-genomeNN.txt already holds.
$probe = "$repo\scratch\solver-spike\probe"
New-Item -ItemType Directory -Force -Path $probe | Out-Null
Select-String -Path $log -Pattern '\[ParitySwim\] probe genome=' -Raw |
    ForEach-Object { ($_ -split '\[ParitySwim\] ')[-1] } | Set-Content "$probe\physx-probe.txt"
Select-String -Path $log -Pattern '\[ParitySwim\] probe-frames genome=' -Raw |
    ForEach-Object { ($_ -split '\[ParitySwim\] ')[-1] } | Set-Content "$probe\physx-frames.txt"
"probe lines: $((Get-Content "$probe\physx-probe.txt" | Measure-Object -Line).Lines), frames: $((Get-Content "$probe\physx-frames.txt" | Measure-Object -Line).Lines)"
