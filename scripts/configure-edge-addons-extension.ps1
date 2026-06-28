param(
  [Parameter(Mandatory = $true)]
  [ValidatePattern('^[a-p]{32}$')]
  [string]$EdgeExtensionId
)

$ErrorActionPreference = 'Stop'

function Assert-Admin {
  $currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
  $principal = New-Object Security.Principal.WindowsPrincipal($currentIdentity)
  if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Hay chay PowerShell voi quyen Administrator.'
  }
}

function Set-RegistryString {
  param(
    [string]$SubKeyPath,
    [string]$Name,
    [string]$Value
  )

  $key = [Microsoft.Win32.Registry]::LocalMachine.CreateSubKey($SubKeyPath)
  if (-not $key) {
    throw "Khong tao duoc registry key HKLM:\$SubKeyPath"
  }

  try {
    $key.SetValue($Name, $Value, [Microsoft.Win32.RegistryValueKind]::String)
  }
  finally {
    $key.Dispose()
  }
}

function Set-ExtensionSettingsPolicy {
  param(
    [string]$ExtensionId,
    [string]$UpdateUrl
  )

  $subKeyPath = 'SOFTWARE\Policies\Microsoft\Edge'
  $key = [Microsoft.Win32.Registry]::LocalMachine.CreateSubKey($subKeyPath)
  if (-not $key) {
    throw "Khong tao duoc registry key HKLM:\$subKeyPath"
  }

  try {
    $settings = @{
      $ExtensionId = @{
        installation_mode = 'force_installed'
        update_url = $UpdateUrl
        override_update_url = $true
      }
    }
    $key.SetValue('ExtensionSettings', ($settings | ConvertTo-Json -Compress -Depth 10), [Microsoft.Win32.RegistryValueKind]::String)
  }
  finally {
    $key.Dispose()
  }
}

Assert-Admin

$serviceName = 'AdultContentShutdownGuard'
$installedSettingsPath = 'C:\Program Files\AdultContentShutdownGuard\appsettings.json'
$edgeStoreUpdateUrl = 'https://edge.microsoft.com/extensionwebstorebase/v1/crx'
$violationUrl = 'http://127.0.0.1:8765/violation'
$healthUrl = 'http://127.0.0.1:8765/health'

if (-not (Test-Path $installedSettingsPath)) {
  throw "Khong tim thay appsettings da cai: $installedSettingsPath. Hay cai service truoc."
}

$settings = Get-Content $installedSettingsPath -Raw | ConvertFrom-Json
$token = [string]$settings.Guard.Token
if ([string]::IsNullOrWhiteSpace($token) -or $token -eq 'CHANGE_THIS_SECRET_TOKEN') {
  throw 'Installed token khong hop le. Hay chay install-service.ps1 de sinh token truoc.'
}

$settings.Guard.ManagedBrowserEndpoint.Enabled = $true
$settings.Guard.ManagedBrowserEndpoint.EdgeExtensionId = $EdgeExtensionId
$settings.Guard.ManagedBrowserEndpoint.UpdateUrl = $edgeStoreUpdateUrl
$settings.Guard.ManagedBrowserEndpoint.UpdateManifestPath = ''
$settings.Guard.ManagedBrowserEndpoint.CrxPath = ''
$settings | ConvertTo-Json -Depth 20 | Set-Content -Path $installedSettingsPath -Encoding UTF8

Set-RegistryString -SubKeyPath 'SOFTWARE\Policies\Microsoft\Edge\ExtensionInstallForcelist' -Name '1' -Value "$EdgeExtensionId;$edgeStoreUpdateUrl"
Set-ExtensionSettingsPolicy -ExtensionId $EdgeExtensionId -UpdateUrl $edgeStoreUpdateUrl
Set-RegistryString -SubKeyPath "SOFTWARE\Policies\Microsoft\Edge\3rdparty\extensions\$EdgeExtensionId\policy" -Name 'serviceUrl' -Value $violationUrl
Set-RegistryString -SubKeyPath "SOFTWARE\Policies\Microsoft\Edge\3rdparty\extensions\$EdgeExtensionId\policy" -Name 'healthUrl' -Value $healthUrl
Set-RegistryString -SubKeyPath "SOFTWARE\Policies\Microsoft\Edge\3rdparty\extensions\$EdgeExtensionId\policy" -Name 'token' -Value $token
Remove-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Edge' -Name 'InPrivateModeAvailability' -Force -ErrorAction SilentlyContinue
Set-RegistryString -SubKeyPath 'SOFTWARE\Policies\Microsoft\Edge\MandatoryExtensionsForInPrivateNavigation' -Name '1' -Value $EdgeExtensionId

$edgeManagedExtensionsRoot = 'HKLM:\SOFTWARE\Policies\Microsoft\Edge\3rdparty\extensions'
if (Test-Path $edgeManagedExtensionsRoot) {
  Get-ChildItem -Path $edgeManagedExtensionsRoot -ErrorAction SilentlyContinue |
    Where-Object { $_.PSChildName -ne $EdgeExtensionId } |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}

$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($service) {
  Restart-Service -Name $serviceName -Force
}

Write-Host "Da cau hinh Edge Add-ons extension: $EdgeExtensionId"
Write-Host 'Da yeu cau Edge InPrivate chi duoc duyet khi extension duoc allow trong InPrivate.'
Write-Host 'Mo edge://policy va bam Tai lai chinh sach, hoac khoi dong lai Edge.'
