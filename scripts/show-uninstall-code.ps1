$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'GuardUninstallProtection.psm1') -Force

$code = Get-GuardUninstallCode
$expiresAt = [DateTimeOffset]::UtcNow
$expiresAt = $expiresAt.AddMinutes(60 - $expiresAt.Minute).AddSeconds(-$expiresAt.Second)

Write-Host "AdultContentShutdownGuard uninstall code: $code"
Write-Host "Ma nay doi theo gio. Het han gan dung luc UTC: $($expiresAt.ToString('yyyy-MM-dd HH:mm:ss'))"
