# D182's patch measures: run the --restep --patches jobs patch-batch.py listed, -Parallel at a time, each one thread, pinned to -Mask
# (decimal; never logical 4-5) at BelowNormal, from -Dll (a frozen build of the watcher's branch) in -Worktree. A job whose folder already
# holds result.json is skipped, so a stopped batch resumes. Log: <jobs folder>/run.log. Run detached; a STOP file beside jobs.txt ends it
# after the running jobs finish.
param([string]$Jobs, [string]$Dll, [string]$Worktree, [int64]$Mask = 4278190080, [int]$Parallel = 8)
$ErrorActionPreference = 'Stop'
if (($Mask -band 48) -ne 0) { "the mask names logical 4 or 5; nothing run"; exit 4 }
$d = 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Data\DotNetSdk\dotnet.exe'
$dir = Split-Path $Jobs
$log = Join-Path $dir 'run.log'
function Say($m) { Add-Content $log "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $m" }

$todo = @(Get-Content $Jobs | Where-Object { $_ } | ForEach-Object { $r, $o = $_ -split "`t"; @{ Req = $r; Out = $o } } |
    Where-Object { -not (Test-Path (Join-Path $_.Out 'result.json')) })
Say "start: $($todo.Count) jobs, $Parallel at a time, mask $Mask, $Dll"
$running = @()
foreach ($j in $todo) {
    while ($running.Count -ge $Parallel) {
        $running = @($running | Where-Object { -not $_.P.HasExited })
        if ($running.Count -ge $Parallel) { Start-Sleep -Seconds 2 }
    }
    if (Test-Path (Join-Path $dir 'STOP')) { Say "STOP found; no new job starts"; break }
    New-Item -ItemType Directory -Force (Split-Path $j.Out) | Out-Null
    $p = Start-Process -FilePath $d -ArgumentList @($Dll, '--restep', $j.Req, '--out', $j.Out, '--patches') -WorkingDirectory $Worktree `
        -WindowStyle Hidden -PassThru -RedirectStandardOutput "$($j.Out).log" -RedirectStandardError "$($j.Out).err"
    $null = $p.Handle
    $p.ProcessorAffinity = [IntPtr]$Mask
    $p.PriorityClass = 'BelowNormal'
    $running += @{ P = $p; J = $j }
}
foreach ($x in $running) { $x.P.WaitForExit() }
$done = @(Get-Content $Jobs | Where-Object { $_ } | Where-Object { Test-Path (Join-Path (($_ -split "`t")[1]) 'result.json') }).Count
$all = @(Get-Content $Jobs | Where-Object { $_ }).Count
Say "end: $done of $all jobs hold a result"
