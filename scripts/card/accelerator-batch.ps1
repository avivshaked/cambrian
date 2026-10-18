# The card engine's proof on ILGPU's CPU accelerator, off the card (src/Evosim.Farm.Gpu/CLAUDE.md), in three
# steps, each with its own deadline, stopping at the first failure:
#   1. the kernels regenerated from their templates by their own test (GpuKernelSourceTests with
#      EVOSIM_GPU_REGENERATE=1), which compiles Evosim.Farm.Tests first;
#   2. the same test again without the switch, after a rebuild: the files are the templates' output;
#   3. every Gpu test of Evosim.Farm.Tests but the card's (Category!=Card): the engine against the solver, the
#      transport against the grid, the refusals, the strays.
# Pinned to -Mask (decimal; 983040 is logical 16-19) at BelowNormal before dotnet starts, the test host held to
# 4 processors, the accelerator's thread count. Off the card by construction (nvcuda-guard.ps1):
# CUDA_VISIBLE_DEVICES=-1, every step watched for a loaded nvcuda and held to -StepMinutes, the nvcuda line said
# before the end, and which kernel files the regeneration changed.
# Log: scratch/logs/accelerator-batch-<Name>.log, last line "accelerator batch ends" (with "(stopped ...)" on a
# failure or a deadline).
param([Parameter(Mandatory)][string]$Name, [int64]$Mask = 983040, [double]$StepMinutes = 15)
$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$proj = Join-Path $root 'src\Evosim.Farm.Tests\Evosim.Farm.Tests.csproj'
$logs = Join-Path $root 'scratch\logs'
$log  = Join-Path $logs "accelerator-batch-$Name.log"
New-Item -ItemType Directory -Force $logs | Out-Null
if (Test-Path $log) { Write-Host "$log exists; choose another -Name"; exit 1 }
$me = [System.Diagnostics.Process]::GetCurrentProcess()
$me.ProcessorAffinity = [IntPtr]$Mask
$me.PriorityClass = 'BelowNormal'
function Say([string]$t) { "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $t" | Add-Content $log }
. (Join-Path $PSScriptRoot 'nvcuda-guard.ps1')
Enter-OffCard

$dotnet = 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Data\DotNetSdk\dotnet.exe'
$env:DOTNET_PROCESSOR_COUNT = '4'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$common = @('-c', 'Release', '-m:1', '-nr:false', '-p:UseSharedCompilation=false')

Say "accelerator batch begins: $Name, affinity $($me.ProcessorAffinity), priority $($me.PriorityClass), head $(git -C $root rev-parse --short HEAD)"
$steps = @(
    @('regenerate', 'FullyQualifiedName~GpuKernelSourceTests', '1'),
    @('source', 'FullyQualifiedName~GpuKernelSourceTests', ''),
    @('gpu', 'FullyQualifiedName~Evosim.Farm.Tests.Gpu&Category!=Card', ''))
foreach ($s in $steps) {
    $step, $filter, $regen = $s
    $env:EVOSIM_GPU_REGENERATE = $regen
    $out = Join-Path $logs "accelerator-batch-$Name-$step.log"
    $p = Start-Process $dotnet -ArgumentList (@('test', $proj) + $common + @('--filter', $filter, '--logger', 'console;verbosity=normal')) `
         -NoNewWindow -PassThru -RedirectStandardOutput $out -RedirectStandardError "$out.err"
    if (-not (Wait-OffCard $p (Get-Date).AddMinutes($StepMinutes))) {
        foreach ($id in @(Get-ProcessTree $p.Id)) { Stop-Process -Id $id -Force -ErrorAction SilentlyContinue }
        Say "${step}: past its $StepMinutes-minute deadline, its tree stopped"
        Say (Resolve-OffCard)
        Say 'accelerator batch ends (stopped at a deadline)'; exit 5
    }
    $code = $p.ExitCode
    Say "$step exit $code"
    Select-String -Path $out -Pattern '^\s*Failed |Passed!|Failed!|Total tests| error CS|Error Message' -Context 0, 1 |
        ForEach-Object { Say "  $($_.Line.Trim())"; if ($_.Line -match 'Error Message') { $_.Context.PostContext | ForEach-Object { Say "    $($_.Trim())" } } }
    if ($code -ne 0) { Say (Resolve-OffCard); Say 'accelerator batch ends (stopped at a failure)'; exit $code }
}
$env:EVOSIM_GPU_REGENERATE = ''
Say "kernels changed by the regeneration: $((git -C $root status --porcelain -- src/Evosim.Farm.Gpu/Kernels.D.g.cs src/Evosim.Farm.Gpu/Kernels.F.g.cs) -join '; ')"
Say (Resolve-OffCard)
Say 'accelerator batch ends'
