# Round 59's D2 benches on the coordinator's go (2026-10-15): r59-d2-s2 on logical 16-23 and r59-d2-s3 on 24-31, 8 threads each,
# BelowNormal, from the frozen Release build of nursery-patchwatch's HEAD (launch-arms.ps1 -BuildOnly made it), out in that worktree's
# runs/nursery/. -Only s2 or s3 starts one. -Threads and -Mask/-Cores override the defaults for the one started (a narrow start
# that is widened later by setting ProcessorAffinity on the pid). Detached; prints each pid and logs to scratch/leaf-tank/d2-launch.log.
param([string]$Only = '', [int]$Threads = 8, [int64]$Mask = 0, [string]$Cores = '')
$ErrorActionPreference = 'Stop'
$w = 'D:\Projects\experiments\evolution-simulator\scratch\wt-nursery-patchwatch'
$log = 'D:\Projects\experiments\evolution-simulator\scratch\leaf-tank\d2-launch.log'
$d = 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Data\DotNetSdk\dotnet.exe'
function Say($m) { $line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $m"; $line; Add-Content $log $line }

$dirty = git -C $w status --porcelain
if ($dirty) { Say "the tree is not clean; nothing launched:`n$($dirty -join "`n")"; exit 2 }
$sha = (git -C $w rev-parse --short=8 HEAD).Trim()
$dll = Join-Path $w "artifacts\nursery-patchwatch-$sha\Evosim.Nursery.dll"
if (-not (Test-Path $dll)) { Say "no frozen build at $dll; nothing launched"; exit 3 }

$benches = @(@{ Name = 'r59-d2-s2'; Mask = [int64]16711680; Cores = '16-23' }, @{ Name = 'r59-d2-s3'; Mask = [int64]4278190080; Cores = '24-31' })
foreach ($b in $benches) {
    if ($Only -and $b.Name -ne "r59-d2-$Only") { continue }
    if ($Mask -ne 0) { if (-not $Only) { Say "-Mask needs -Only; nothing launched"; exit 5 }; $b.Mask = $Mask; $b.Cores = $Cores }
    if (($b.Mask -band 48) -ne 0) { Say "a mask names logical 4 or 5; nothing launched"; exit 4 }
    $out = Join-Path $w "runs\nursery\$($b.Name)"
    if (Test-Path $out) { Say "$out exists; $($b.Name) not launched"; continue }
    New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null
    $p = Start-Process -FilePath $d -ArgumentList @($dll, '--recipe', "rounds/nursery/$($b.Name).json", '--out', $out, '--threads', "$Threads") `
        -WorkingDirectory $w -WindowStyle Hidden -PassThru -RedirectStandardOutput "$out.log" -RedirectStandardError "$out.err"
    $p.ProcessorAffinity = [IntPtr]$b.Mask
    $p.PriorityClass = 'BelowNormal'
    Say "started $($b.Name) pid $($p.Id) on $($b.Cores), $Threads threads (affinity $([int64]$p.ProcessorAffinity)), commit $sha, out $out"
}
