# Builds and runs the self-overlap probe (logbook/0107's round 41c read) over the snapshots it was
# written for. The project is src/Evosim.Overlap (the commit hook tracks project files under src/
# only, and src/Directory.Build.props sends the build to artifacts/). One snapshot by hand:
#   dotnet artifacts/Evosim.Overlap/bin/Release/net8.0/Evosim.Overlap.dll <snapshot.jsonl> [--top N] [--dump <id>] [--silhouette] [--ids a,b]
# Unity's bundled SDK, as scripts/core-test.ps1 and scripts/ledger.ps1 use.

$dotnet = 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Data\DotNetSdk\dotnet.exe'
$here   = Split-Path -Parent $MyInvocation.MyCommand.Path
$root   = (Resolve-Path "$here\..\..").Path
$runs   = "$root\runs"

& $dotnet build "$root\src\Evosim.Overlap\Evosim.Overlap.csproj" -c Release -v quiet --nologo
$exe = "$root\artifacts\Evosim.Overlap\bin\Release\net8.0\Evosim.Overlap.dll"

$want = @(
    @('r41c-s3','000001000'), @('r41c-s3','000002000'), @('r41c-s3','000003000'),
    @('r41c-s2','000003000'),
    @('r41c-s1','000002000'), @('r41c-s1','000003000'),
    @('r40-s1','000003000'),  @('r40-s1','000005000'),
    @('r41b-s1','000003000'), @('r41b-s1','000005000'))

$paths = @()
foreach ($w in $want) {
    $dir = (Get-ChildItem "$runs\$($w[0])" -Directory)[0].FullName
    $p = "$dir\snapshots\$($w[1]).jsonl"
    if (Test-Path $p) { $paths += $p } else { Write-Output "MISSING: $p" }
}

& $dotnet $exe @paths --top 5
