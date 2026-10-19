<#
  One safari render of r62d-u-6102 for the HDRP spike's second test (D202), on 01's terms: cores 8-15
  (affinity 65280), BelowNormal, one Unity at a time. The base look is the side-by-side's; -Env adds or
  overrides dials as "NAME=value;NAME=value".

    ./unity-hdrp/tools/render.ps1 -Target hdrp -Folder native-t1 -Scenes 1 -Fps 1 -Env "EVOSIM_HDRP_NATIVE=1"
#>
param(
    [ValidateSet('hdrp', 'urp')][string]$Target,
    [string]$Folder,
    [string]$Scenes = '1',
    [int]$Fps = 24,
    [string]$Env = '',
    [int]$WallMinutes = 30
)
$ErrorActionPreference = 'Stop'
$r = 'D:\Projects\experiments\evolution-simulator'

if (Get-CimInstance Win32_Process -Filter "Name='Unity.exe'") { throw 'A Unity is already running: one at a time.' }

$p = [System.Diagnostics.Process]::GetCurrentProcess()
$p.ProcessorAffinity = [IntPtr]65280
$p.PriorityClass = 'BelowNormal'

$base = @{
    EVOSIM_THEATRE_SAFARI_CLOCK = '1'; EVOSIM_SAFARI_FLOOR_AT = '64,-1.5,74'; EVOSIM_THEATRE_LOOK = '4';
    EVOSIM_THEATRE_SHELL_GAIN = '1.25'; EVOSIM_THEATRE_SNOW_QUANTITY = '1.5'; EVOSIM_THEATRE_SNOW_LEAST = '0.75';
    EVOSIM_THEATRE_SNOW_AMBIENT = '4'; EVOSIM_THEATRE_GLASS = '0'; EVOSIM_THEATRE_CAUSTIC_SCALE = '0.75'
}
foreach ($k in $base.Keys) { Set-Item "env:$k" $base[$k] }
foreach ($pair in ($Env -split ';')) {
    if ($pair.Trim() -eq '') { continue }
    $kv = $pair.Split('=', 2)
    Set-Item "env:$($kv[0].Trim())" $kv[1].Trim()
    "dial $($kv[0].Trim())=$($kv[1].Trim())"
}

$where = if ($Target -eq 'hdrp') { @{ Project = 'unity-hdrp' } } else { @{ Worker = 6 } }
$sceneList = [int[]]($Scenes -split ',')
"start $(Get-Date -Format HH:mm:ss) $Target $Folder scenes $Scenes at $Fps fps"
& "$r\scripts\theatre-safari.ps1" r62d-u-6102 -Story scratch\hdrp-spike\r62d\story.json -FromWindows scratch\hdrp-spike\r62d\windows `
    -Scenes $sceneList -Fps $Fps -Size 1920x1080 @where -NoBurnText -Folder $Folder -WallMinutes $WallMinutes 2>&1 | Select-Object -Last 3
"end $(Get-Date -Format HH:mm:ss) exit $LASTEXITCODE"
