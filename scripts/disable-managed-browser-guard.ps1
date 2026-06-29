param(
  [string]$UninstallCode
)

$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'GuardUninstallProtection.psm1') -Force
Assert-GuardUninstallCode -Code $UninstallCode

$serviceName = 'AdultContentShutdownGuard'
$installedSettingsPath = 'C:\Program Files\AdultContentShutdownGuard\appsettings.json'

$extensionIds = @()
if (Test-Path -LiteralPath $installedSettingsPath) {
  $settings = Get-Content -LiteralPath $installedSettingsPath -Raw | ConvertFrom-Json
  foreach ($id in @($settings.Guard.ManagedBrowserEndpoint.ChromeExtensionId, $settings.Guard.ManagedBrowserEndpoint.EdgeExtensionId)) {
    if (-not [string]::IsNullOrWhiteSpace($id)) {
      $extensionIds += [string]$id
    }
  }

  $settings.Guard.ManagedBrowserEndpoint.Enabled = $false
  $settings | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $installedSettingsPath -Encoding UTF8
}

Remove-Item -Path 'HKLM:\SOFTWARE\Policies\Google\Chrome\ExtensionInstallForcelist' -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Edge\ExtensionInstallForcelist' -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Edge\MandatoryExtensionsForInPrivateNavigation' -Recurse -Force -ErrorAction SilentlyContinue
Remove-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Google\Chrome' -Name 'ExtensionSettings' -Force -ErrorAction SilentlyContinue
Remove-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Edge' -Name 'ExtensionSettings' -Force -ErrorAction SilentlyContinue

foreach ($extensionId in ($extensionIds | Select-Object -Unique)) {
  Remove-Item -Path "HKLM:\SOFTWARE\Policies\Google\Chrome\3rdparty\extensions\$extensionId" -Recurse -Force -ErrorAction SilentlyContinue
  Remove-Item -Path "HKLM:\SOFTWARE\Policies\Microsoft\Edge\3rdparty\extensions\$extensionId" -Recurse -Force -ErrorAction SilentlyContinue
}

$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($service) {
  Restart-Service -Name $serviceName -Force
}

Write-Host 'Da tat managed browser guard policy. Hay reload edge://policy/chrome://policy hoac khoi dong lai trinh duyet.'
