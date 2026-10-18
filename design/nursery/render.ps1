# Renders each screen of the nursery room's wireframes to a PNG in shots/, with headless Edge.
# Usage, from anywhere:  pwsh -File design/nursery/render.ps1 [-Width 1920 -Height 1080] [-Only 3-run]
# The visual directions:  pwsh -File design/nursery/render.ps1 -Directions [-Width 2560 -Height 1440]
# The Edge profile lives in scratch/edge-nursery/, inside the repository, as the project's rules ask.
param(
    [int]$Width = 1920,
    [int]$Height = 1080,
    [string]$Only = '',
    [switch]$Directions,
    [double]$Scale = 1
)
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$root = (Resolve-Path "$here\..\..").Path
$edge = 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
$profileDir = "$root\scratch\edge-nursery"
New-Item -ItemType Directory -Force "$here\shots", $profileDir | Out-Null

$suffix = if ($Width -eq 1920 -and $Height -eq 1080 -and $Scale -eq 1) { '' } elseif ($Scale -ne 1) { "-$([int]($Width * $Scale))-at$([int]($Scale * 100))" } else { "-${Width}x$Height" }
$jobs = @()
if ($Directions) {
    $file = 'file:///' + ("$here\directions\run.html" -replace '\\', '/')
    foreach ($d in 'a', 'b', 'c') { $jobs += @{ Out = "$here\shots\direction-$d$suffix.png"; Url = "$file`?dir=$d"; Name = "direction-$d" } }
} else {
    $pages = Get-ChildItem "$here\*.html" | Where-Object { $_.BaseName -match '^\d-' }
    if ($Only) { $pages = $pages | Where-Object { $_.BaseName -eq $Only } }
    foreach ($p in $pages) { $jobs += @{ Out = "$here\shots\$($p.BaseName)$suffix.png"; Url = 'file:///' + ($p.FullName -replace '\\', '/'); Name = $p.BaseName } }
}

foreach ($j in $jobs) {
    $out = $j.Out
    $url = $j.Url
    $a = @('--headless=new', '--disable-gpu', '--hide-scrollbars', '--no-first-run',
           '--allow-file-access-from-files', "--user-data-dir=$profileDir",
           "--window-size=$Width,$Height", "--force-device-scale-factor=$Scale", '--virtual-time-budget=3000',
           "--screenshot=$out", $url)
    Start-Process -FilePath $edge -ArgumentList $a -Wait -WindowStyle Hidden
    "$($j.Name) -> $out"
}
