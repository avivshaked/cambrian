<#
.SYNOPSIS
  The theatre's agreed film look, read from scripts/theatre-film-look.json into the environment.
  Dot-sourced by theatre-safari.ps1 and theatre-film.ps1; it defines Use-FilmLook and nothing else.

.DESCRIPTION
  Unity's look is set by EVOSIM_THEATRE_* variables ("dials") read when the process starts, and the agreed
  values live in one file, so the two launchers do not keep tables of their own that drift apart.

  Use-FilmLook -Sections wide,events[,close] [-PrintOnly]
    wide    every shot
    close   added for a portrait or a close shot; where it names a dial that wide names too, close wins
    events  births, bites and deaths
  The file's 'trial' and 'previewOnly' sections are never set: no film takes a trial dial until a ruling
  moves it up, and the previews' own dials are not a film's.

  For each dial of the sections:
    absent from the environment    set to the file's value;
    holds the same value           left alone (a number is the same number however it is written);
    holds a different value        left as the caller has it, and one line names the dial, the caller's value,
                                   the file's value and the file's ruling. A caller pinning a dial for a
                                   comparison film means it, so it wins, and the line is there so that no
                                   film departs from the look unseen.
  A line of the summary says how many fell in each, and the file it read.

  -PrintOnly changes nothing and says what it would do, a line to a dial.

  The file is found in the main checkout from any worktree (the parent of git's common directory), since a
  worktree holds only what its branch tracks; failing that, in this script's own repository, and it says so.
  A dial with a 'perWorld' table takes its 'value' (the file's stand-in for a world with no row); the table is
  not consulted here, and a line says so.

  It returns the names of the dials it set, so that a launcher which restores its environment afterwards can
  restore these too; it refuses, rather than carry on without the look, when the file is missing or does not
  parse. The file needs its UTF-8 BOM for Windows PowerShell 5.
#>

# Kept at the dot-source, since $PSScriptRoot inside a function is not reliable across PowerShell's editions.
$script:FilmLookDirectory = $PSScriptRoot

function Use-FilmLook {
    [CmdletBinding()]
    param(
        # A comma list arrives as one string through "powershell -File" (CLAUDE.md's new-worker gotcha), so it is split again here.
        [Parameter(Mandatory)][string[]]$Sections,
        [switch]$PrintOnly
    )

    $invariant = [System.Globalization.CultureInfo]::InvariantCulture
    # git writes to stderr when it is not in a repository, and the launchers stop on any error.
    $ErrorActionPreference = 'Continue'

    # EVOSIM_THEATRE_FILM_LOOK=0 sets nothing: an identity check renders with every dial off (the render slot's dials-off seat,
    # against a reference drawn before the look was read here), and says so, since a film that takes it departs from the look.
    $off = [Environment]::GetEnvironmentVariable('EVOSIM_THEATRE_FILM_LOOK')
    if ($off -and $off.Trim() -eq '0') {
        Write-Host 'look: EVOSIM_THEATRE_FILM_LOOK is 0, so no dial of the agreed film look is set (an identity check with every dial off)'
        return @()
    }

    # ------------------------------------------------------------ the sections asked for
    $asked = @()
    foreach ($value in $Sections) {
        foreach ($part in ($value -split ',')) {
            $trimmed = $part.Trim().ToLowerInvariant()
            if ($trimmed) { $asked += $trimmed }
        }
    }
    foreach ($s in $asked) {
        if (@('trial', 'previewOnly') -contains $s) { throw "Use-FilmLook: '$s' is never set. 'trial' dials are under A/B and 'previewOnly' dials are the previews'; a film takes neither." }
        if (@('wide', 'close', 'events') -notcontains $s) { throw "Use-FilmLook: '$s' is not a section a film takes. They are wide, close, events." }
    }
    if ($asked.Count -eq 0) { throw "Use-FilmLook: -Sections needs at least one of wide, close, events." }
    # In the file's order, so that close replaces wide's ONE_SKIN whichever way the caller wrote the list.
    $sections = @('wide', 'close', 'events') | Where-Object { $asked -contains $_ }

    # ------------------------------------------------------------ the file
    $own = Join-Path (Split-Path -Parent $script:FilmLookDirectory) 'scripts\theatre-film-look.json'
    $path = $null
    $why = ''
    try {
        $found = @(& git -C $script:FilmLookDirectory rev-parse --git-common-dir 2>$null)
        $common = if ($found.Count -gt 0) { "$($found[0])".Trim() } else { '' }
        if ($LASTEXITCODE -eq 0 -and $common) {
            if (-not [System.IO.Path]::IsPathRooted($common)) { $common = Join-Path $script:FilmLookDirectory $common }
            $main = Split-Path -Parent ([System.IO.Path]::GetFullPath($common))
            $candidate = Join-Path $main 'scripts\theatre-film-look.json'
            if (Test-Path -LiteralPath $candidate -PathType Leaf) { $path = $candidate }
            else { $why = "no file at $candidate" }
        } else { $why = 'git gave no common directory' }
    } catch { $why = "git failed: $($_.Exception.Message)" }
    if (-not $path) {
        if (-not (Test-Path -LiteralPath $own -PathType Leaf)) {
            throw "Use-FilmLook: no theatre-film-look.json. The main checkout's was not found ($why) and this repository has none at $own."
        }
        Write-Warning "look: the main checkout's theatre-film-look.json was not found ($why); reading this repository's own copy at $own"
        $path = $own
    }
    $path = [System.IO.Path]::GetFullPath($path)

    $look = $null
    try { $look = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8) | ConvertFrom-Json }
    catch { throw "Use-FilmLook: $path does not parse as JSON: $($_.Exception.Message)" }
    if ($null -eq $look) { throw "Use-FilmLook: $path is empty." }

    # ------------------------------------------------------------ the dials: later sections replace earlier ones
    $dials = [ordered]@{}
    foreach ($section in $sections) {
        $block = $look.PSObject.Properties[$section]
        if ($null -eq $block -or $null -eq $block.Value) { throw "Use-FilmLook: $path has no '$section' section." }
        foreach ($property in $block.Value.PSObject.Properties) {
            $entry = $property.Value
            if ($null -eq $entry -or $null -eq $entry.PSObject.Properties['value']) { throw "Use-FilmLook: $path, $section.$($property.Name) has no 'value'." }
            $ruling = ''
            if ($null -ne $entry.PSObject.Properties['ruling']) { $ruling = [string]$entry.ruling }
            $table = ''
            if ($null -ne $entry.PSObject.Properties['perWorld']) { $table = [string]$entry.perWorld }
            $dials[$property.Name] = [pscustomobject]@{ Value = [string]$entry.value; Ruling = $ruling; PerWorld = $table; Section = $section }
        }
    }

    # ------------------------------------------------------------ the environment
    $set = @(); $same = 0; $kept = 0
    foreach ($name in $dials.Keys) {
        $dial = $dials[$name]
        $have = [Environment]::GetEnvironmentVariable($name)
        $equal = $false
        if (-not [string]::IsNullOrEmpty($have)) {
            $equal = ($have -ceq $dial.Value)
            if (-not $equal) {
                $a = 0.0; $b = 0.0
                $style = [System.Globalization.NumberStyles]::Float
                if ([double]::TryParse($have, $style, $invariant, [ref]$a) -and [double]::TryParse($dial.Value, $style, $invariant, [ref]$b)) { $equal = ($a -eq $b) }
            }
        }

        if ([string]::IsNullOrEmpty($have)) {
            $set += $name
            if ($PrintOnly) { Write-Host "  would set $name = $($dial.Value)   [$($dial.Section)]" }
            else { [Environment]::SetEnvironmentVariable($name, $dial.Value, 'Process') }
        } elseif ($equal) {
            $same++
            if ($PrintOnly) { Write-Host "  already   $name = $have   [$($dial.Section)]" }
        } else {
            $kept++
            Write-Host "look: $name is '$have' in the environment and stays so; the look says '$($dial.Value)' ($($dial.Ruling))"
            continue
        }
        if ($dial.PerWorld) { Write-Host "look: $name takes '$($dial.Value)', the look's value for a world with no row; its per-world table $($dial.PerWorld) was not consulted" }
    }

    $verb = if ($PrintOnly) { 'would set' } else { 'set' }
    Write-Host ("look: {0} {1}, {2} already equal, {3} kept different ({4}) from {5}{6}" -f `
        $verb, $set.Count, $same, $kept, ($sections -join ', '), $path, $(if ($PrintOnly) { '; nothing changed' } else { '' }))

    if ($PrintOnly) { return }
    return $set
}
