# Encodes the takes of a safari folder whose scene has no clip yet, exactly as scripts/theatre-safari.ps1's encode
# does (libx264, crf 18, yuv420p), without deleting frames. For look test 1, whose script stopped at the delete step.
param([string]$Out, [int]$Fps = 30)
$ErrorActionPreference = 'Stop'
foreach ($line in (Get-Content (Join-Path $Out 'scenes.tsv') | Select-Object -Skip 1)) {
    $cells = $line -split "`t"; if ($cells.Count -lt 8) { continue }
    $slug = $cells[1]; if ($cells[6] -ne 'played') { Write-Host "  $slug : $($cells[6])"; continue }
    $clip = Join-Path $Out "$slug.mp4"; if (Test-Path $clip) { Write-Host "  $slug : has its clip"; continue }
    $dir = Join-Path $Out $slug
    $takes = @(Get-ChildItem -Path $dir -Directory -Filter 'take-*' | Sort-Object { [int]($_.Name -replace 'take-', '') })
    $parts = @()
    foreach ($t in $takes) {
        $part = Join-Path $dir "$($t.Name).mp4"
        & ffmpeg -hide_banner -loglevel error -y -framerate $Fps -start_number 0 -i (Join-Path $t.FullName 'frame-%06d.png') `
            -c:v libx264 -pix_fmt yuv420p -crf 18 -r $Fps -threads 4 -movflags +faststart $part
        if ($LASTEXITCODE -ne 0) { throw "$slug $($t.Name): ffmpeg failed" }
        $parts += $part
    }
    if ($parts.Count -ne 1) { throw "$slug : $($parts.Count) takes; this script handles one" }
    Copy-Item $parts[0] $clip -Force
    & python (Join-Path $PSScriptRoot '..\..\scripts\film-sheet.py') $clip 2>&1 | Select-Object -Last 1
    Write-Host "  $slug : encoded"
}
