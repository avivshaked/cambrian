<#
.SYNOPSIS
  Write the synthetic run directory scripts/tests/positions/run-tests.ps1 reads.

.DESCRIPTION
  One hand-built world of three samples, small enough that every number the reader prints can
  be worked out on paper. It is generated rather than committed as data for two reasons: .jsonl
  is gitignored (simulation output), and the intent of a fixture ("two bodies a metre apart, so
  the median nearest neighbour is 0.75 m") is legible in the code that builds it and invisible
  in a row of coordinates.

  positions-read.py reads three files under <RunsRoot>/<arm>/<newest run>/: config.json for the
  box, positions.jsonl for the samples, and lineage.jsonl, when it is there, for the clades.

  The world is the campaign's box: 100 m^2 over four patches is 20 x 5 m, 60 m deep, so the
  footprint is 100 columns of a metre.

  Sample 1 (t = 100), four bodies, two clades of two:

      id 1  (1.0, -10, 1)  plain     clade 1
      id 2  (2.0, -10, 1)  stomach   clade 1, child of 1
      id 3  (5.0, -10, 1)  leaf      clade 3
      id 4  (5.5, -20, 1)  mixotroph clade 3, child of 3

    Flat nearest neighbours are 1.0, 1.0, 0.5, 0.5, so the median is 0.75 m. In three
    dimensions they are 1.0, 1.0, 3.0 and 10.01, so the median is 2.0 m: the pair that looks
    adjacent from above is ten metres apart in the water, which is the whole reason the reader
    prints both. Three columns of the hundred are occupied, and the two bodies of clade 1 are
    exactly a metre apart, so the clade count is 2.

  Sample 2 (t = 200), a ribbon: three bodies of one clade at x = 10.0, 10.2 and 10.4, all at
  z = 2.5 and y = -5. One occupied column, both medians 0.2 m, and all three within a metre of
  a clade mate.

  Sample 3 (t = 300), nothing alive: n = 0, and every statistic that needs a body prints a dash
  rather than a zero.

  A second arm, fx-no-positions, is a run directory with a config.json and no positions.jsonl:
  a tiled world writes no such file, and neither does any run recorded before 2026-09-10, and
  the reader has to refuse both rather than read them as a world with nothing in it.

  A third arm, fx-square, is the same three samples in the same 100 m^2 of water laid out two
  patches by two (fable-propose-box.md): a 10 x 10 m box rather than a 20 x 5 m one. The bodies
  do not move, so what the arm holds fixed is the reader -- it has to take the layout out of
  config.json and describe the box the run was in, and the footprint is a hundred columns
  either way.
#>
param(
    [string]$Root = $PSScriptRoot
)

$ErrorActionPreference = 'Stop'

$fixtures = Join-Path $Root 'fixtures'
if (Test-Path -LiteralPath $fixtures) { Remove-Item -LiteralPath $fixtures -Recurse -Force }

$runDir = Join-Path $fixtures 'fx-positions/2026-01-01-000000-fixture'
New-Item -ItemType Directory -Path $runDir -Force | Out-Null

# Only the three fields the reader looks for, in two groups, so that the test also proves it
# finds them by name anywhere in the tree rather than at a fixed path.
$config = @'
{
  "format": 2,
  "configHash": "fixture",
  "world": {
    "worldAreaSquareMetres": 100,
    "worldDepthMetres": 60
  },
  "patches": {
    "horizontalPatches": 4
  }
}
'@

# One row per sample, one line each, exactly as PositionsRow writes them.
$positions = @(
    '{"t":100,"n":4,"b":[[1,1,-10,1,0],[2,2,-10,1,1],[3,5,-10,1,4],[4,5.5,-20,1,5]]}'
    '{"t":200,"n":3,"b":[[5,10,-5,2.5,2],[6,10.2,-5,2.5,3],[7,10.4,-5,2.5,7]]}'
    '{"t":300,"n":0,"b":[]}'
)

# Birth rows in the shape lineage.jsonl carries them. Two founders (p = -1) and one child each
# in the first sample; one founder and two children in the second.
$lineage = @(
    '{"e":"b","t":10,"id":1,"p":-1,"k":"f","g":0,"s":0,"abs":0,"jnt":0,"pho":0,"pt":0,"bf":1,"as":1}'
    '{"e":"b","t":20,"id":2,"p":1,"k":"r","g":1,"s":0,"abs":1,"jnt":0,"pho":0,"pt":0,"bf":0.3,"as":1}'
    '{"e":"b","t":30,"id":3,"p":-1,"k":"f","g":0,"s":0,"abs":0,"jnt":0,"pho":1,"pt":1,"bf":1,"as":1}'
    '{"e":"b","t":40,"id":4,"p":3,"k":"r","g":1,"s":0,"abs":1,"jnt":0,"pho":1,"pt":1,"bf":0.3,"as":1}'
    '{"e":"b","t":50,"id":5,"p":-1,"k":"f","g":0,"s":0,"abs":0,"jnt":1,"pho":0,"pt":2,"bf":1,"as":1}'
    '{"e":"b","t":60,"id":6,"p":5,"k":"r","g":1,"s":0,"abs":1,"jnt":1,"pho":0,"pt":2,"bf":0.3,"as":1}'
    '{"e":"b","t":70,"id":7,"p":5,"k":"r","g":1,"s":0,"abs":1,"jnt":1,"pho":1,"pt":2,"bf":0.3,"as":1}'
)

$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText((Join-Path $runDir 'config.json'), $config, $utf8)
[System.IO.File]::WriteAllText((Join-Path $runDir 'positions.jsonl'), ($positions -join "`n") + "`n", $utf8)
[System.IO.File]::WriteAllText((Join-Path $runDir 'lineage.jsonl'), ($lineage -join "`n") + "`n", $utf8)

# The run that carries no positions.jsonl. Its config is the same box, so the only thing the
# reader can complain about is the file that is not there.
$absentDir = Join-Path $fixtures 'fx-no-positions/2026-01-01-000000-fixture'
New-Item -ItemType Directory -Path $absentDir -Force | Out-Null
[System.IO.File]::WriteAllText((Join-Path $absentDir 'config.json'), $config, $utf8)

# The same world, two patches by two. Only the one added key differs, so anything the reader
# prints differently for this arm is the layout and nothing else.
$square = @'
{
  "format": 2,
  "configHash": "fixture",
  "world": {
    "worldAreaSquareMetres": 100,
    "worldDepthMetres": 60
  },
  "patches": {
    "horizontalPatches": 4,
    "patchesAcross": 2
  }
}
'@

$squareDir = Join-Path $fixtures 'fx-square/2026-01-01-000000-fixture'
New-Item -ItemType Directory -Path $squareDir -Force | Out-Null
[System.IO.File]::WriteAllText((Join-Path $squareDir 'config.json'), $square, $utf8)
[System.IO.File]::WriteAllText((Join-Path $squareDir 'positions.jsonl'), ($positions -join "`n") + "`n", $utf8)
[System.IO.File]::WriteAllText((Join-Path $squareDir 'lineage.jsonl'), ($lineage -join "`n") + "`n", $utf8)

Write-Host "wrote $runDir"
Write-Host "wrote $absentDir"
Write-Host "wrote $squareDir"
