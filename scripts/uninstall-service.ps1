param(
  [string]$UninstallCode
)

$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'GuardUninstallProtection.psm1') -Force

function Assert-Admin {
  $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
  $principal = [Security.Principal.WindowsPrincipal]::new($identity)
  if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Hay chay PowerShell voi quyen Administrator.'
  }
}

function Restore-DnsBackup {
  param([string]$Path)

  if (-not (Test-Path -LiteralPath $Path)) {
    Write-Warning 'Khong co ban sao DNS truoc khi cai dat. DNS dang giu 127.0.0.1 va ::1; hay cau hinh lai DNS truoc khi xoa file service.'
    return
  }

  $backup = @(Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json)
  foreach ($group in ($backup | Group-Object InterfaceIndex)) {
    $addresses = @($group.Group | ForEach-Object { $_.ServerAddresses } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
    if ($addresses.Count -gt 0) {
      Set-DnsClientServerAddress -InterfaceIndex ([int]$group.Name) -ServerAddresses $addresses -ErrorAction SilentlyContinue
    } else {
      Set-DnsClientServerAddress -InterfaceIndex ([int]$group.Name) -ResetServerAddresses -ErrorAction SilentlyContinue
    }
  }
}

Assert-Admin
Assert-GuardUninstallCode -Code $UninstallCode

$serviceName = 'AdultContentShutdownGuard'
$installDir = 'C:\Program Files\AdultContentShutdownGuard'
$programDataDir = 'C:\ProgramData\AdultContentShutdownGuard'
$dnsBackupPath = Join-Path $programDataDir 'dns-backup.json'

$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($service) {
  Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
  & sc.exe delete $serviceName | Out-Null
  Start-Sleep -Seconds 2
}

Get-NetFirewallRule -DisplayName 'AdultContentShutdownGuard*' -ErrorAction SilentlyContinue | Remove-NetFirewallRule -ErrorAction SilentlyContinue
Restore-DnsBackup -Path $dnsBackupPath

Remove-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Google\Chrome' -Name 'DnsOverHttpsMode', 'QuicAllowed', 'ProxyMode', 'IncognitoModeAvailability', 'BrowserGuestModeEnabled' -Force -ErrorAction SilentlyContinue
Remove-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Edge' -Name 'DnsOverHttpsMode', 'QuicAllowed', 'ProxyMode', 'InPrivateModeAvailability', 'BrowserGuestModeEnabled' -Force -ErrorAction SilentlyContinue
Remove-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Mozilla\Firefox\DNSOverHTTPS' -Name 'Enabled', 'Locked' -Force -ErrorAction SilentlyContinue
Remove-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Mozilla\Firefox' -Name 'DisablePrivateBrowsing' -Force -ErrorAction SilentlyContinue

$deleteLogs = Read-Host 'Xoa luon log va cau hinh khong? (y/N)'
if ($deleteLogs -match '^(y|yes)$') {
  Remove-Item -Path $programDataDir -Recurse -Force -ErrorAction SilentlyContinue
}

Remove-Item -Path $installDir -Recurse -Force -ErrorAction SilentlyContinue
Write-Host 'Da go cai dat service va khoi phuc DNS da luu truoc khi cai dat.'
