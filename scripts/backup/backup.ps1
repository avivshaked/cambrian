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
# The password: with EVOSIM_BACKUP_OP_LOCAL or EVOSIM_BACKUP_OP_GDRIVE in .env (a 1Password reference such as
# op://Private/evosim-backup-gdrive/password, never the password itself), restic asks the 1Password CLI for it, and
# 1Password asks the owner to approve. Without one, restic asks at the prompt.
$key = if ($To -eq 'local') { 'EVOSIM_BACKUP_OP_LOCAL' } else { 'EVOSIM_BACKUP_OP_GDRIVE' }
$line = Get-Content "$R\.env" -ErrorAction SilentlyContinue | Where-Object { $_ -match "^$key=" } | Select-Object -First 1
$ref = if ($line) { ($line -split '=', 2)[1].Trim() } else { '' }
if ($ref) { $env:RESTIC_PASSWORD_COMMAND = "op read `"$ref`"" }
else { Write-Host "An unattended backup needs $key in .env: a 1Password reference to the store's password (see .env.example). Asking at the prompt instead." }
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
