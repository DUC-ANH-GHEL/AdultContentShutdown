$ErrorActionPreference = 'Stop'

function Assert-Admin {
  $currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
  $principal = New-Object Security.Principal.WindowsPrincipal($currentIdentity)
  if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Hay chay PowerShell voi quyen Administrator.'
  }
}

Assert-Admin

$serviceName = 'AdultContentShutdownGuard'
$installDir = 'C:\Program Files\AdultContentShutdownGuard'
$logDir = 'C:\ProgramData\AdultContentShutdownGuard\Logs'
$managedExtensionDir = 'C:\ProgramData\AdultContentShutdownGuard\browser-extension'
$installedSettingsPath = Join-Path $installDir 'appsettings.json'

$extensionIds = @()
if (Test-Path $installedSettingsPath) {
  try {
    $settings = Get-Content $installedSettingsPath -Raw | ConvertFrom-Json
    foreach ($id in @($settings.Guard.ManagedBrowserEndpoint.ChromeExtensionId, $settings.Guard.ManagedBrowserEndpoint.EdgeExtensionId)) {
      if (-not [string]::IsNullOrWhiteSpace($id)) {
        $extensionIds += [string]$id
      }
    }
  }
  catch {
  }
}

$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($service) {
  Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
  sc.exe delete $serviceName | Out-Null
  Start-Sleep -Seconds 2
}

Get-NetFirewallRule -DisplayName 'AdultContentShutdownGuard*' -ErrorAction SilentlyContinue | Remove-NetFirewallRule -ErrorAction SilentlyContinue
Remove-Item -Path 'HKLM:\SOFTWARE\Policies\Google\Chrome\ExtensionInstallForcelist' -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Edge\ExtensionInstallForcelist' -Recurse -Force -ErrorAction SilentlyContinue
Remove-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Google\Chrome' -Name 'ExtensionSettings' -Force -ErrorAction SilentlyContinue
Remove-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Edge' -Name 'ExtensionSettings' -Force -ErrorAction SilentlyContinue

foreach ($extensionId in ($extensionIds | Select-Object -Unique)) {
  Remove-Item -Path "HKLM:\SOFTWARE\Policies\Google\Chrome\3rdparty\extensions\$extensionId" -Recurse -Force -ErrorAction SilentlyContinue
  Remove-Item -Path "HKLM:\SOFTWARE\Policies\Microsoft\Edge\3rdparty\extensions\$extensionId" -Recurse -Force -ErrorAction SilentlyContinue
}

$deleteLogs = Read-Host 'Xoa luon log khong? (y/N)'
if ($deleteLogs -match '^(y|yes)$') {
  Remove-Item -Path $logDir -Recurse -Force -ErrorAction SilentlyContinue
}

Remove-Item -Path $installDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path $managedExtensionDir -Recurse -Force -ErrorAction SilentlyContinue
Write-Host 'Da go cai dat service.'
