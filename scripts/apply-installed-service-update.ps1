$ErrorActionPreference = 'Stop'

function Assert-Admin {
  $currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
  $principal = New-Object Security.Principal.WindowsPrincipal($currentIdentity)
  if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Hay chay PowerShell voi quyen Administrator.'
  }
}

Assert-Admin

function Set-JsonProperty {
  param(
    [object]$Object,
    [string]$Name,
    [object]$Value
  )

  if ($Object.PSObject.Properties[$Name]) {
    $Object.$Name = $Value
  } else {
    $Object | Add-Member -NotePropertyName $Name -NotePropertyValue $Value
  }
}

$serviceName = 'AdultContentShutdownGuard'
$installDir = 'C:\Program Files\AdultContentShutdownGuard'
$publishDir = Resolve-Path (Join-Path $PSScriptRoot '..\publish\Guard.Service')
$overlayPublishDir = Resolve-Path (Join-Path $PSScriptRoot '..\publish\Guard.Overlay')
$settingsPath = Join-Path $installDir 'appsettings.json'

if (-not (Test-Path -LiteralPath $installDir)) {
  throw "Khong tim thay thu muc cai dat: $installDir"
}

if (-not (Test-Path -LiteralPath $settingsPath)) {
  throw "Khong tim thay file cau hinh dang cai dat: $settingsPath"
}

$service = Get-Service -Name $serviceName -ErrorAction Stop
if ($service.Status -ne 'Stopped') {
  Stop-Service -Name $serviceName -Force
  $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
}

Get-ChildItem -LiteralPath $publishDir -Force |
  Where-Object { $_.Name -ne 'appsettings.json' } |
  Copy-Item -Destination $installDir -Recurse -Force

New-Item -ItemType Directory -Path (Join-Path $installDir 'Overlay') -Force | Out-Null
Get-ChildItem -LiteralPath $overlayPublishDir -Force |
  Copy-Item -Destination (Join-Path $installDir 'Overlay') -Recurse -Force

$json = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
$json.Guard.PSObject.Properties.Remove('ManagedBrowserEndpoint')
$json.Guard.PSObject.Properties.Remove('LegacyExtensionEndpointEnabled')
$json.Guard.PSObject.Properties.Remove('Token')
if (-not $json.Guard.Overlay) {
  $json.Guard | Add-Member -NotePropertyName Overlay -NotePropertyValue ([pscustomobject]@{})
}
Set-JsonProperty -Object $json.Guard.Overlay -Name 'Enabled' -Value $false
Set-JsonProperty -Object $json.Guard.Overlay -Name 'DurationSeconds' -Value 300
Set-JsonProperty -Object $json.Guard.Dns -Name 'Enabled' -Value $true
Set-JsonProperty -Object $json.Guard.Dns -Name 'ListenAddresses' -Value @('127.0.0.1', '::1')
Set-JsonProperty -Object $json.Guard.Dns -Name 'ReturnNxDomain' -Value $true
Set-JsonProperty -Object $json.Guard -Name 'AllowMachineShutdown' -Value $false
Set-JsonProperty -Object $json.Guard.Enforcement -Name 'ActionOnViolation' -Value 'LogOnly'
Set-JsonProperty -Object $json.Guard.Enforcement -Name 'ActionOnTamper' -Value 'LogOnly'
Set-JsonProperty -Object $json.Guard.Enforcement -Name 'ApplyOnStartup' -Value $true
Set-JsonProperty -Object $json.Guard.Enforcement -Name 'ConfigureDnsAdapters' -Value $true
Set-JsonProperty -Object $json.Guard.Enforcement -Name 'ConfigureFirewallRules' -Value $true
Set-JsonProperty -Object $json.Guard.BrowserPolicies -Name 'Enabled' -Value $true
Set-JsonProperty -Object $json.Guard.BrowserPolicies -Name 'DisableDnsOverHttps' -Value $true
Set-JsonProperty -Object $json.Guard.BrowserPolicies -Name 'DisableQuic' -Value $true
Set-JsonProperty -Object $json.Guard.BrowserPolicies -Name 'LockProxySettings' -Value $true
Set-JsonProperty -Object $json.Guard.BrowserPolicies -Name 'DisablePrivateBrowsing' -Value $true
Set-JsonProperty -Object $json.Guard.BrowserPolicies -Name 'DisableGuestMode' -Value $true
Set-JsonProperty -Object $json.Guard.Tamper -Name 'RestoreSettings' -Value $true
Set-JsonProperty -Object $json.Guard.NetworkPosture -Name 'ActionOnUnsafePosture' -Value 'LogOnly'
Set-JsonProperty -Object $json.Guard.ProcessRules -Name 'ActionOnWorkVpnDetected' -Value 'LogOnly'
Set-JsonProperty -Object $json.Guard.ProcessRules -Name 'TerminateBlockedProcesses' -Value $true
Set-JsonProperty -Object $json.Guard.ProcessRules -Name 'BlockedProcessNames' -Value @('tor', 'torbrowser', 'tor-client-win32', 'psiphon', 'ultrasurf')
$json | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $settingsPath -Encoding UTF8

Set-Service -Name $serviceName -StartupType Automatic
Start-Service -Name $serviceName
(Get-Service -Name $serviceName).WaitForStatus('Running', [TimeSpan]::FromSeconds(30))

Write-Host 'Da cap nhat service dang cai dat sang che do bao ve cap may, service dang Running.'
