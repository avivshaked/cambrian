# Offline test of scripts/card/driver-watch.ps1's end rule. A log holding only the watch's own first line,
# which names the end pattern, must not end it; a real line with the pattern must. It needs no card and reads
# no event it could act on: its Controller matches no process. Writes only in scratch/card/watch-end-test.
# Takes about 75 s. Prints "watch alive True" after the begin line, then "watch exited True, code 0".
$root = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
$here = Join-Path $root 'scratch\card\watch-end-test'
New-Item -ItemType Directory -Force $here | Out-Null
$log = Join-Path $here 'job.log'; $out = Join-Path $here 'watch.out'; $settings = Join-Path $here 'settings.json'
Remove-Item $log, $out -ErrorAction SilentlyContinue
[ordered]@{ Since = (Get-Date).ToString('s'); Deadline = (Get-Date).AddMinutes(2).ToString('s'); JobLog = $log
    EndPattern = 'books over'; StopLine = 'test stop'; Controller = 'no-such-script\.ps1' } |
    ConvertTo-Json | Set-Content $settings -Encoding utf8
"x watch pid 1: 2026-10-08T11:31:14 watch begins: since x, deadline y, log z, end pattern 'books over', controller 'c'" | Set-Content $log
$w = Start-Process pwsh -ArgumentList @('-NoProfile', '-File', (Join-Path $root 'scripts\card\driver-watch.ps1'), '-Settings', $settings) `
     -WindowStyle Hidden -PassThru -RedirectStandardOutput $out
Start-Sleep -Seconds 45
"after 45 s with only the begin line: watch alive $(-not $w.HasExited)"
"  books over 30 stats rows: max |auditResidual| 0" | Add-Content $log
$d = (Get-Date).AddSeconds(30); while (-not $w.HasExited -and (Get-Date) -lt $d) { Start-Sleep -Seconds 1 }
"after the real line: watch exited $($w.HasExited), code $(if ($w.HasExited) { $w.ExitCode })"
if (-not $w.HasExited) { Stop-Process -Id $w.Id -Force -ErrorAction SilentlyContinue }
Get-Content $out
