<#
.SYNOPSIS
  The digest EvolutionRun.HashSourceTree computes, over any directory.

.DESCRIPTION
  A copy of run-arm.ps1's Get-SourceTreeHash, callable on a directory that is not a worker's
  Assets/Evosim -- which is what a before/after comparison needs. For every .cs under the root,
  in ordinal order of its path relative to the root with forward slashes:
  "<relative path>`n<sha256 of the file's bytes>`n". SHA-256 over that, lowercase hex.

  CLAUDE.md's warning about scripts/simhash.py applies to this too: it is not the hash, it is a
  second implementation of it. Validate it against a number a build actually wrote (a run.json's
  source.simHash, or run-arm.ps1's own printout) before trusting a comparison made with it.

.EXAMPLE
  ./scripts/tree-hash.ps1 unity/Assets/Evosim
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)][string]$Root
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $Root)) { throw "No directory at $Root" }

$full = (Resolve-Path $Root).Path.TrimEnd([IO.Path]::DirectorySeparatorChar)
$files = @(Get-ChildItem -LiteralPath $full -Recurse -File |
    Where-Object { $_.Extension -ieq '.cs' })

if ($files.Count -eq 0) { throw "No .cs under $full" }

$rels = New-Object 'string[]' $files.Count
$paths = New-Object 'string[]' $files.Count

for ($i = 0; $i -lt $files.Count; $i++) {
    $rels[$i] = $files[$i].FullName.Substring($full.Length + 1).Replace(
        [IO.Path]::DirectorySeparatorChar, '/')
    $paths[$i] = $files[$i].FullName
}

# Ordinal, never culture-aware: a digest that depends on regional settings identifies nothing.
[Array]::Sort($rels, $paths, [System.StringComparer]::Ordinal)

$sha = [System.Security.Cryptography.SHA256]::Create()
try {
    $sb = New-Object System.Text.StringBuilder
    for ($i = 0; $i -lt $rels.Length; $i++) {
        $bytes = [System.IO.File]::ReadAllBytes($paths[$i])
        $digest = -join ($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString('x2') })
        [void]$sb.Append($rels[$i]).Append("`n").Append($digest).Append("`n")
    }

    $utf8 = New-Object System.Text.UTF8Encoding($false)
    $hash = -join ($sha.ComputeHash($utf8.GetBytes($sb.ToString())) |
        ForEach-Object { $_.ToString('x2') })
}
finally { $sha.Dispose() }

Write-Host "$hash  ($($files.Count) .cs files under $full)"
$hash
