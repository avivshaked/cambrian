# The milestone backup of D260 (logbook/specs/data-retention-and-backup.md), run by the owner, who types the
# repository's password when restic asks. It makes the backup set's list (backup-set.py), then one restic snapshot
# of it into the local repository on the second disk, or into the encrypted one on Google Drive:
#
#   pwsh -NoProfile -File scripts/backup/backup.ps1 -To local
#   pwsh -NoProfile -File scripts/backup/backup.ps1 -To gdrive
#
# restic compresses, encrypts and stores each piece once, so a later snapshot sends only what changed. A Drive login
# older than 7 days (the app is in Testing) is renewed first with `rclone config reconnect gdrive:`.
param(
    [Parameter(Mandatory)][ValidateSet('local', 'gdrive')][string]$To,
    [switch]$DryRun
)
$ErrorActionPreference = 'Stop'
$R = 'D:\Projects\experiments\evolution-simulator'
$repo = if ($To -eq 'local') { 'C:\Backups\evolution-simulator\restic' } else { 'rclone:gdrive:evosim-backup' }
# The program path as Windows holds it, since a terminal opened before restic and rclone were installed lacks it.
$env:Path = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('Path', 'User')
$me = [System.Diagnostics.Process]::GetCurrentProcess()
$me.PriorityClass = 'BelowNormal'

& python -I "$R\scripts\backup\backup-set.py"
if ($LASTEXITCODE -ne 0) { throw 'backup-set.py failed' }
$list = "$R\scratch\backup\files.txt"
$extra = @()
if ($DryRun) { $extra += '--dry-run' }
& restic -r $repo backup --files-from-verbatim $list --compression auto --tag milestone --host evosim @extra
if ($LASTEXITCODE -ne 0) { throw "restic backup exited $LASTEXITCODE" }
& restic -r $repo snapshots --latest 1
& restic -r $repo stats --mode raw-data
