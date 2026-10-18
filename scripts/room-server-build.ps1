# The room and nursery Debug build in the main checkout (compile only), pinned to logical 28-31, BelowNormal, one at a time, with a deadline.
param([int64]$Mask = 4026531840, [int]$Minutes = 15)
$ErrorActionPreference = 'Stop'
$w = 'D:\Projects\experiments\evolution-simulator'
$log = 'D:\Projects\experiments\evolution-simulator\scratch\room-server'
$d = 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Data\DotNetSdk\dotnet.exe'
$me = [System.Diagnostics.Process]::GetCurrentProcess()
$me.ProcessorAffinity = [IntPtr]$Mask
$me.PriorityClass = 'BelowNormal'
$env:DOTNET_PROCESSOR_COUNT = '4'
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
$deadline = (Get-Date).AddMinutes($Minutes)
"start $(Get-Date -Format s), deadline $($deadline.ToString('s')), main $(git -C $w rev-parse --short HEAD)"
foreach ($proj in 'Evosim.NurseryRoom', 'Evosim.Nursery') {
    $a = @('build', "src/$proj", '-c', 'Debug', '-m:1', '-nologo', '-nodeReuse:false', '-p:UseSharedCompilation=false', '-v:minimal')
    $out = Join-Path $log "build-$proj.log"
    $p = Start-Process -FilePath $d -ArgumentList $a -WorkingDirectory $w -NoNewWindow -PassThru -RedirectStandardOutput $out -RedirectStandardError "$out.err"
    if (-not $p.WaitForExit([int][Math]::Max(0, ($deadline - (Get-Date)).TotalMilliseconds))) { $p.Kill($true); "stopped at the deadline $(Get-Date -Format s)"; exit 3 }
    "$proj exit $($p.ExitCode) at $(Get-Date -Format s)"
    Select-String -Path $out -Pattern 'error CS|Error\(s\)|Build succeeded' | Select-Object -First 6 | ForEach-Object { $_.Line }
    if ($p.ExitCode -ne 0) { exit 1 }
}
