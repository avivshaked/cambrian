<#
.SYNOPSIS
  Make or refresh unity-hdrp/, the theatre's HDRP spike (D202), from unity/.

.DESCRIPTION
  unity-hdrp/ is a Unity project of its own beside unity/, with its own Library/, on the High
  Definition Render Pipeline. It shares the simulation with unity/ and nothing else.

  Assets/Evosim is mirrored from unity/ on every run, as scripts/new-worker.ps1 mirrors it to a
  worker: the copy is never edited, git ignores it, and the one URP-only file in it (the menu
  that builds URP's pipeline asset, which nothing calls) is left out. Evosim.Core, Evosim.Dynamics and
  Evosim.Farm come in as local packages through Packages/manifest.json, as in unity/.

  ProjectSettings/ is copied from unity/ only when unity-hdrp/ has none. The copy then belongs to
  this project: Editor/HdrpSetup.cs gives it an HDRP asset in place of URP's on the first open.

  Assets/Theatre is the spike's own fork of the theatre, the shaders ported to HDRP, and it is
  tracked. This script copies it from unity/ only with -SeedTheatre, which overwrites the fork.

  Never run while a Unity Editor has unity-hdrp/ open.

.PARAMETER SeedTheatre
  Copy unity/Assets/Theatre over unity-hdrp/Assets/Theatre. Only for starting the fork afresh.
#>
param([switch]$SeedTheatre)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$from = Join-Path $root 'unity'
$to = $PSScriptRoot

$open = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" |
    Where-Object { $_.CommandLine -match [regex]::Escape($to) }
if ($open) { throw "A Unity process has $to open (pid $($open.ProcessId -join ', ')). Close it first." }

function Mirror([string]$part) {
    $src = Join-Path $from $part
    $dst = Join-Path $to $part
    $null = robocopy $src $dst /MIR /NFL /NDL /NJH /NJS /NP /R:1 /W:1
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed for $part (exit $LASTEXITCODE)" }
    Write-Host "mirrored $part"
}

Mirror 'Assets\Evosim'
Copy-Item (Join-Path $from 'Assets\Evosim.meta') (Join-Path $to 'Assets\Evosim.meta') -Force

# The one URP file in the simulation: the editor menu that builds URP's pipeline asset. Nothing
# references it, and in a project without URP it cannot compile, so the mirror leaves it out.
$urpSetup = Join-Path $to 'Assets\Evosim\Sim\Editor\RenderPipelineSetup.cs'
Remove-Item $urpSetup, "$urpSetup.meta" -ErrorAction SilentlyContinue
Write-Host 'left out Assets\Evosim\Sim\Editor\RenderPipelineSetup.cs (URP only)'

$settings = Join-Path $to 'ProjectSettings'
if (-not (Test-Path $settings)) {
    Mirror 'ProjectSettings'
    Remove-Item (Join-Path $settings 'URPProjectSettings.asset') -ErrorAction SilentlyContinue
    $p = Join-Path $settings 'ProjectSettings.asset'
    (Get-Content $p -Raw) -replace 'productName: unity', 'productName: unity-hdrp' |
        Set-Content $p -NoNewline -Encoding utf8NoBOM
    Write-Host 'copied ProjectSettings (first time); URP settings left behind'
}

if ($SeedTheatre) {
    Mirror 'Assets\Theatre'
    Copy-Item (Join-Path $from 'Assets\Theatre.meta') (Join-Path $to 'Assets\Theatre.meta') -Force
    Write-Host 'seeded Assets\Theatre from unity/ (the fork was overwritten)'
}

# robocopy's 1 to 7 mean copied or extra files, not failure; do not let them leak out.
exit 0
