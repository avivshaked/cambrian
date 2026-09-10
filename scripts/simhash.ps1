#Requires -Version 5
<#
    The simHash, computed exactly as BuildIdentity.HashSourceTree and
    EvolutionRun.HashSourceTree compute it: for every .cs under the root, in ordinal
    order of its relative path with forward slashes, "relativePath\nsha256\n"; then
    SHA-256 over that manifest as UTF-8 without BOM, lowercase hex.

    scripts/simhash.py disagreed with the C# once (CLAUDE.md); this is the third
    implementation, written from the C# and checked against a manifest the build wrote.
#>
param([Parameter(Mandatory = $true)][string]$Root)

$full = [System.IO.Path]::GetFullPath($Root).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
if (-not (Test-Path -LiteralPath $full)) { throw "no such directory: $full" }

$files = [System.IO.Directory]::GetFiles($full, '*', [System.IO.SearchOption]::AllDirectories) |
    Where-Object { [System.IO.Path]::GetExtension($_).ToLowerInvariant() -eq '.cs' }

if ($files.Count -eq 0) { throw "no .cs files under $full" }

$byRelative = @{}
$relative = New-Object 'System.Collections.Generic.List[string]'
foreach ($path in $files) {
    $rel = [System.IO.Path]::GetFullPath($path).Substring($full.Length + 1).Replace([System.IO.Path]::DirectorySeparatorChar, '/')
    $rel = $rel -replace '\\', '/'
    $relative.Add($rel) | Out-Null
    $byRelative[$rel] = $path
}
$relative.Sort([System.StringComparer]::Ordinal)

$sha = [System.Security.Cryptography.SHA256]::Create()
$manifest = New-Object System.Text.StringBuilder
foreach ($rel in $relative) {
    $digest = $sha.ComputeHash([System.IO.File]::ReadAllBytes($byRelative[$rel]))
    $hex = ([System.BitConverter]::ToString($digest) -replace '-', '').ToLowerInvariant()
    [void]$manifest.Append($rel).Append("`n").Append($hex).Append("`n")
}

$utf8 = New-Object System.Text.UTF8Encoding($false)
$total = $sha.ComputeHash($utf8.GetBytes($manifest.ToString()))
$out = ([System.BitConverter]::ToString($total) -replace '-', '').ToLowerInvariant()

"{0}  ({1} files)  {2}" -f $out, $relative.Count, $full
