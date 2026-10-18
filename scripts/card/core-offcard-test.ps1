# One filter of Evosim.Core.Tests (or, with -Project Farm, Evosim.Farm.Tests) in this checkout, off the card
# (nvcuda-guard.ps1): CUDA_VISIBLE_DEVICES=-1, the test host watched for a loaded nvcuda and held to -Minutes,
# the nvcuda line at the end. Pinned to -Mask (decimal; 983040 is logical 16-19) at BelowNormal before dotnet
# starts, the test host held to 4 processors. -Filter is a dotnet test filter with no spaces (e.g.
# FullyQualifiedName~CorpseDepositTests). The Gpu tests and their regeneration are accelerator-batch.ps1's.
# Log: scratch/logs/<core|farm>-offcard-<Name>.log, last line "core test ends".
param(
    [Parameter(Mandatory)][string]$Name,
    [Parameter(Mandatory)][string]$Filter,
    [ValidateSet('Core', 'Farm')][string]$Project = 'Core',
    [int64]$Mask = 983040,
    [double]$Minutes = 10)
$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$logs = Join-Path $root 'scratch\logs'
$kind = $Project.ToLowerInvariant()
$log = Join-Path $logs "$kind-offcard-$Name.log"
$out = Join-Path $logs "$kind-offcard-$Name-test.log"
New-Item -ItemType Directory -Force $logs | Out-Null
if (Test-Path $log) { Write-Host "$log exists; choose another -Name"; exit 1 }
function Say([string]$t) { "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $t" | Add-Content $log }
$me = [System.Diagnostics.Process]::GetCurrentProcess()
$me.ProcessorAffinity = [IntPtr]$Mask
$me.PriorityClass = 'BelowNormal'
. (Join-Path $PSScriptRoot 'nvcuda-guard.ps1')
Enter-OffCard

$dotnet = 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Data\DotNetSdk\dotnet.exe'
$env:DOTNET_PROCESSOR_COUNT = '4'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
Say "core test begins: Evosim.$Project.Tests, $Name, $Filter, head $(git -C $root rev-parse --short HEAD), CUDA_VISIBLE_DEVICES=$env:CUDA_VISIBLE_DEVICES"
$proj = Join-Path $root "src\Evosim.$Project.Tests\Evosim.$Project.Tests.csproj"
$p = Start-Process $dotnet -ArgumentList @('test', $proj, '-c', 'Release', '-m:1',
    '-nr:false', '-p:UseSharedCompilation=false', '--filter', $Filter, '--logger', 'console;verbosity=normal') `
    -NoNewWindow -PassThru -RedirectStandardOutput $out -RedirectStandardError "$out.err"
if (-not (Wait-OffCard $p (Get-Date).AddMinutes($Minutes))) {
    foreach ($id in @(Get-ProcessTree $p.Id)) { Stop-Process -Id $id -Force -ErrorAction SilentlyContinue }
    Say "past its $Minutes-minute deadline, its tree stopped"
    Say (Resolve-OffCard); Say 'core test ends (stopped at a deadline)'; exit 5
}
Say "exit $($p.ExitCode)"
Select-String -Path $out -Pattern '^\s*(Passed|Failed) |Passed!|Failed!|Total tests| error CS|Error Message' -Context 0, 2 |
    ForEach-Object { Say "  $($_.Line.Trim())"; if ($_.Line -match 'Error Message') { $_.Context.PostContext | ForEach-Object { Say "    $($_.Trim())" } } }
Say (Resolve-OffCard)
Say 'core test ends'
exit $p.ExitCode
