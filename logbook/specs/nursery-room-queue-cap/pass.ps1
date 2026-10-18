# The room's queue-cap build and tests on room-cap (scratch/wt-room-cap), pinned to 4 logical processors (20-23 by default; 8-11 = 3840 from
# the screen's go), BelowNormal, one job at a time, with its deadline here. -Filter limits the suite; -Golden re-records the room's saved
# answers (ROOM_GOLDEN_UPDATE=1). This shell pins itself first, so dotnet and every child it starts (the headless browser included) inherit it.
param([string]$Filter = '', [switch]$Golden, [int64]$Mask = 15728640, [int]$Minutes = 20)
$ErrorActionPreference = 'Stop'
if (($Mask -band 48) -ne 0) { "the mask names logical 4 or 5, which are never used; nothing run"; exit 4 }
$w = 'D:\Projects\experiments\evolution-simulator\scratch\wt-room-cap'
$log = 'D:\Projects\experiments\evolution-simulator\scratch\room-cap'
$d = 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Data\DotNetSdk\dotnet.exe'
$mask = [IntPtr]$Mask
$me = [System.Diagnostics.Process]::GetCurrentProcess()
$me.ProcessorAffinity = $mask
$me.PriorityClass = 'BelowNormal'
$env:DOTNET_PROCESSOR_COUNT = '4'
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
if ($Golden) { $env:ROOM_GOLDEN_UPDATE = '1' } else { Remove-Item Env:ROOM_GOLDEN_UPDATE -ErrorAction SilentlyContinue }
$deadline = (Get-Date).AddMinutes($Minutes)
"start $(Get-Date -Format s), deadline $($deadline.ToString('s')), mask $Mask, filter '$Filter'"
$a = @('test', 'src/Evosim.NurseryRoom.Tests', '-c', 'Debug', '-m:1', '-nologo', '-nodeReuse:false', '-p:UseSharedCompilation=false', '--logger', 'console;verbosity=minimal')
if ($Filter) { $a += @('--filter', $Filter) }
$stamp = Get-Date -Format 'HHmmss'
$out = Join-Path $log "room-tests-$stamp.log"
$p = Start-Process -FilePath $d -ArgumentList $a -WorkingDirectory $w -NoNewWindow -PassThru -RedirectStandardOutput $out -RedirectStandardError "$out.err"
$p.ProcessorAffinity = $mask
if (-not $p.WaitForExit([int][Math]::Max(0, ($deadline - (Get-Date)).TotalMilliseconds))) { $p.Kill($true); "stopped at the deadline $(Get-Date -Format s)"; exit 3 }
"exit $($p.ExitCode) at $(Get-Date -Format s), log $out"
Select-String -Path $out -Pattern '^(Passed!|Failed!)|Total tests|error CS|\[FAIL\]' | ForEach-Object { $_.Line }
exit $p.ExitCode
