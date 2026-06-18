$ErrorActionPreference = 'Stop'

function Assert-Admin {
  $currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
  $principal = New-Object Security.Principal.WindowsPrincipal($currentIdentity)
  if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Hay chay PowerShell voi quyen Administrator.'
  }
}

Assert-Admin

$programDataDir = 'C:\ProgramData\AdultContentShutdownGuard'
New-Item -ItemType Directory -Force -Path $programDataDir | Out-Null
$transcriptPath = Join-Path $programDataDir 'install.log'
Start-Transcript -Path $transcriptPath -Append | Out-Null

$serviceName = 'AdultContentShutdownGuard'
$installDir = 'C:\Program Files\AdultContentShutdownGuard'
$logDir = 'C:\ProgramData\AdultContentShutdownGuard\Logs'
$configDir = 'C:\ProgramData\AdultContentShutdownGuard\Config'
$managedExtensionDir = 'C:\ProgramData\AdultContentShutdownGuard\browser-extension'
$managedExtensionCrxPath = Join-Path $programDataDir 'AdultContentShutdownGuard.crx'
$managedExtensionKeyPath = Join-Path $programDataDir 'AdultContentShutdownGuard.pem'
$managedExtensionUpdateManifestPath = Join-Path $programDataDir 'updates.xml'
$managedExtensionUpdateUrl = 'http://127.0.0.1:8765/extensions/updates.xml'
$managedExtensionCrxUrl = 'http://127.0.0.1:8765/extensions/AdultContentShutdownGuard.crx'
$localViolationUrl = 'http://127.0.0.1:8765/violation'
$localHealthUrl = 'http://127.0.0.1:8765/health'
$publishDir = Join-Path $PSScriptRoot '..\publish\Guard.Service'
$publishScript = Join-Path $PSScriptRoot 'publish-service.ps1'

if (-not (Test-Path $publishDir)) {
  Write-Host 'Chua co thu muc publish, dang chay publish-service.ps1 truoc...'
  & $publishScript
}

if (-not (Test-Path $publishDir)) {
  throw "Khong tim thay thu muc publish: $publishDir. Hay chay scripts/publish-service.ps1 truoc khi cai dat."
}

function Wait-ForProcessExit {
  param(
    [string]$ProcessName,
    [int]$TimeoutSeconds = 20
  )

  $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
  while ((Get-Process -Name $ProcessName -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 500
  }
}

function Remove-ServiceIfPresent {
  param([string]$Name)

  $service = Get-Service -Name $Name -ErrorAction SilentlyContinue
  if ($service) {
    if ($service.Status -ne 'Stopped') {
      Stop-Service -Name $Name -Force -ErrorAction SilentlyContinue
    }

    Start-Process -FilePath sc.exe -ArgumentList @('delete', $Name) -Wait -NoNewWindow | Out-Null
    Wait-ForProcessExit -ProcessName 'Guard.Service'

    $deadline = (Get-Date).AddSeconds(20)
    while ((Get-Service -Name $Name -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) {
      Start-Sleep -Milliseconds 500
    }

    if (Get-Service -Name $Name -ErrorAction SilentlyContinue) {
      throw "Service $Name van chua duoc xoa hoan tat."
    }
  }
}

function New-GuardToken {
  $bytes = New-Object byte[] 32
  $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
  try {
    $rng.GetBytes($bytes)
  }
  finally {
    $rng.Dispose()
  }

  return [Convert]::ToBase64String($bytes)
}

function Update-AppSettings {
  param(
    [string]$Path,
    [string]$Token,
    [string]$UpdateUrl,
    [string]$UpdateManifestPath,
    [string]$CrxPath,
    [string]$ExtensionId = ''
  )

  $json = Get-Content $Path -Raw | ConvertFrom-Json
  $json.Guard.Token = $Token

  if (-not $json.Guard.PSObject.Properties['ManagedBrowserEndpoint']) {
    $json.Guard | Add-Member -MemberType NoteProperty -Name ManagedBrowserEndpoint -Value ([pscustomobject]@{
      Enabled = $false
      ChromeExtensionId = ''
      EdgeExtensionId = ''
      UpdateUrl = ''
      UpdateManifestPath = ''
      CrxPath = ''
    })
  }

  foreach ($property in @('ChromeExtensionId', 'EdgeExtensionId', 'UpdateUrl', 'UpdateManifestPath', 'CrxPath')) {
    if (-not $json.Guard.ManagedBrowserEndpoint.PSObject.Properties[$property]) {
      $json.Guard.ManagedBrowserEndpoint | Add-Member -MemberType NoteProperty -Name $property -Value ''
    }
  }

  $json.Guard.ManagedBrowserEndpoint.Enabled = $true
  $json.Guard.ManagedBrowserEndpoint.UpdateUrl = $UpdateUrl
  $json.Guard.ManagedBrowserEndpoint.UpdateManifestPath = $UpdateManifestPath
  $json.Guard.ManagedBrowserEndpoint.CrxPath = $CrxPath

  if (-not [string]::IsNullOrWhiteSpace($ExtensionId)) {
    $json.Guard.ManagedBrowserEndpoint.ChromeExtensionId = $ExtensionId
    $json.Guard.ManagedBrowserEndpoint.EdgeExtensionId = $ExtensionId
  }

  $json | ConvertTo-Json -Depth 20 | Set-Content -Path $Path -Encoding UTF8
}

function Update-ExtensionToken {
  param(
    [string]$RulesPath,
    [string]$Token
  )

  if (-not (Test-Path $RulesPath)) {
    return
  }

  $escapedToken = $Token.Replace('\', '\\').Replace('"', '\"')
  $content = Get-Content $RulesPath -Raw
  $content = $content -replace 'token:\s*"[^"]*"', "token: `"$escapedToken`""
  Set-Content -Path $RulesPath -Value $content -Encoding UTF8
}

function Update-ExtensionManifest {
  param(
    [string]$ManifestPath,
    [string]$UpdateUrl
  )

  $manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json
  if (-not $manifest.PSObject.Properties['update_url']) {
    $manifest | Add-Member -MemberType NoteProperty -Name 'update_url' -Value $UpdateUrl
  } else {
    $manifest.update_url = $UpdateUrl
  }

  $manifest | ConvertTo-Json -Depth 20 | Set-Content -Path $ManifestPath -Encoding UTF8
}

function Find-BrowserPackager {
  $candidates = @(
    (Join-Path $env:ProgramFiles 'Google\Chrome\Application\chrome.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Google\Chrome\Application\chrome.exe'),
    (Join-Path $env:LocalAppData 'Google\Chrome\Application\chrome.exe'),
    (Join-Path $env:ProgramFiles 'Microsoft\Edge\Application\msedge.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Microsoft\Edge\Application\msedge.exe')
  )

  foreach ($candidate in $candidates) {
    if ($candidate -and (Test-Path $candidate)) {
      return $candidate
    }
  }

  throw 'Khong tim thay chrome.exe hoac msedge.exe de pack extension thanh CRX.'
}

function Invoke-ExtensionPack {
  param(
    [string]$ExtensionDir,
    [string]$CrxPath,
    [string]$KeyPath
  )

  $packager = Find-BrowserPackager
  $generatedCrxPath = "$ExtensionDir.crx"
  $generatedKeyPath = "$ExtensionDir.pem"
  $temporaryProfile = Join-Path $env:TEMP ('acsg-pack-profile-' + [Guid]::NewGuid().ToString('N'))

  Remove-Item -Path $generatedCrxPath, $CrxPath -Force -ErrorAction SilentlyContinue
  New-Item -ItemType Directory -Force -Path $temporaryProfile | Out-Null

  $arguments = @(
    "--user-data-dir=$temporaryProfile",
    '--no-first-run',
    '--disable-background-networking',
    "--pack-extension=$ExtensionDir"
  )

  if (Test-Path $KeyPath) {
    $arguments += "--pack-extension-key=$KeyPath"
  } else {
    Remove-Item -Path $generatedKeyPath -Force -ErrorAction SilentlyContinue
  }

  try {
    $process = Start-Process -FilePath $packager -ArgumentList $arguments -Wait -PassThru -NoNewWindow
    if ($process.ExitCode -ne 0) {
      throw "Browser packager exit code $($process.ExitCode)."
    }

    if (-not (Test-Path $generatedCrxPath)) {
      throw "Browser packager khong tao CRX tai $generatedCrxPath."
    }

    Move-Item -Path $generatedCrxPath -Destination $CrxPath -Force

    if (-not (Test-Path $KeyPath)) {
      if (-not (Test-Path $generatedKeyPath)) {
        throw "Browser packager khong tao private key tai $generatedKeyPath."
      }

      Move-Item -Path $generatedKeyPath -Destination $KeyPath -Force
    }
  }
  finally {
    Remove-Item -Path $temporaryProfile -Recurse -Force -ErrorAction SilentlyContinue
  }
}

function Get-ExtensionVersion {
  param([string]$ManifestPath)

  $manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json
  return [string]$manifest.version
}

function Get-ExtensionId {
  param(
    [string]$ServiceExe,
    [string]$KeyPath
  )

  $extensionId = (& $ServiceExe '--extension-id' $KeyPath | Select-Object -First 1).Trim()
  if ($extensionId -notmatch '^[a-p]{32}$') {
    throw "Extension id khong hop le: $extensionId"
  }

  return $extensionId
}

function New-UpdateManifest {
  param(
    [string]$ExtensionId,
    [string]$CrxUrl,
    [string]$Version
  )

  $escapedExtensionId = [Security.SecurityElement]::Escape($ExtensionId)
  $escapedCrxUrl = [Security.SecurityElement]::Escape($CrxUrl)
  $escapedVersion = [Security.SecurityElement]::Escape($Version)
  return "<?xml version=`"1.0`" encoding=`"UTF-8`"?><gupdate xmlns=`"http://www.google.com/update2/response`" protocol=`"2.0`"><app appid=`"$escapedExtensionId`"><updatecheck codebase=`"$escapedCrxUrl`" version=`"$escapedVersion`" /></app></gupdate>"
}

function Set-ForceListPolicy {
  param(
    [string]$Path,
    [string]$ExtensionId,
    [string]$UpdateUrl
  )

  $policyValue = "$ExtensionId;$UpdateUrl"
  $subKeyPath = $Path.Replace('HKLM:\', '').Replace('/', '\')
  $key = [Microsoft.Win32.Registry]::LocalMachine.CreateSubKey($subKeyPath)
  if (-not $key) {
    throw "Khong tao duoc registry key HKLM:\$subKeyPath"
  }

  try {
    $key.SetValue('1', $policyValue, [Microsoft.Win32.RegistryValueKind]::String)
  }
  finally {
    $key.Dispose()
  }
}

function Set-ExtensionSettingsPolicy {
  param(
    [string]$Path,
    [string]$ExtensionId,
    [string]$UpdateUrl
  )

  $settings = @{}
  $subKeyPath = $Path.Replace('HKLM:\', '').Replace('/', '\')
  $key = [Microsoft.Win32.Registry]::LocalMachine.CreateSubKey($subKeyPath)
  if (-not $key) {
    throw "Khong tao duoc registry key HKLM:\$subKeyPath"
  }

  try {
    $existingJson = [string]$key.GetValue('ExtensionSettings', '')
    if (-not [string]::IsNullOrWhiteSpace($existingJson)) {
      $existing = $existingJson | ConvertFrom-Json
      foreach ($property in $existing.PSObject.Properties) {
        $settings[$property.Name] = $property.Value
      }
    }

    $settings[$ExtensionId] = @{
      installation_mode = 'force_installed'
      update_url = $UpdateUrl
      override_update_url = $true
    }

    $settingsJson = $settings | ConvertTo-Json -Compress -Depth 10
    $key.SetValue('ExtensionSettings', $settingsJson, [Microsoft.Win32.RegistryValueKind]::String)
  }
  finally {
    $key.Dispose()
  }
}

function Set-ExtensionManagedStoragePolicy {
  param(
    [string]$Browser,
    [string]$ExtensionId,
    [string]$Token
  )

  if ([string]::IsNullOrWhiteSpace($ExtensionId)) {
    return
  }

  if ($Browser -eq 'Chrome') {
    $subKeyPath = "SOFTWARE\Policies\Google\Chrome\3rdparty\extensions\$ExtensionId\policy"
  } elseif ($Browser -eq 'Edge') {
    $subKeyPath = "SOFTWARE\Policies\Microsoft\Edge\3rdparty\extensions\$ExtensionId\policy"
  } else {
    throw "Browser khong hop le: $Browser"
  }

  $key = [Microsoft.Win32.Registry]::LocalMachine.CreateSubKey($subKeyPath)
  if (-not $key) {
    throw "Khong tao duoc registry key HKLM:\$subKeyPath"
  }

  try {
    $key.SetValue('serviceUrl', $localViolationUrl, [Microsoft.Win32.RegistryValueKind]::String)
    $key.SetValue('healthUrl', $localHealthUrl, [Microsoft.Win32.RegistryValueKind]::String)
    $key.SetValue('token', $Token, [Microsoft.Win32.RegistryValueKind]::String)
  }
  finally {
    $key.Dispose()
  }
}

function Apply-ManagedBrowserPolicy {
  param(
    [object]$ManagedBrowser,
    [string]$Token
  )

  if (-not $ManagedBrowser -or [string]::IsNullOrWhiteSpace($ManagedBrowser.UpdateUrl) -or [string]::IsNullOrWhiteSpace($ManagedBrowser.ChromeExtensionId)) {
    throw 'Managed browser policy khong the ghi vi thieu UpdateUrl hoac ExtensionId.'
  }

  if (-not [string]::IsNullOrWhiteSpace($ManagedBrowser.ChromeExtensionId)) {
    $chromePath = 'HKLM:\SOFTWARE\Policies\Google\Chrome\ExtensionInstallForcelist'
    $chromeSettingsPath = 'HKLM:\SOFTWARE\Policies\Google\Chrome'
    Set-ForceListPolicy -Path $chromePath -ExtensionId $ManagedBrowser.ChromeExtensionId -UpdateUrl $ManagedBrowser.UpdateUrl
    Set-ExtensionSettingsPolicy -Path $chromeSettingsPath -ExtensionId $ManagedBrowser.ChromeExtensionId -UpdateUrl $ManagedBrowser.UpdateUrl
    Set-ExtensionManagedStoragePolicy -Browser 'Chrome' -ExtensionId $ManagedBrowser.ChromeExtensionId -Token $Token
    Write-Host 'Da ghi Chrome ExtensionInstallForcelist.'
  }

  if (-not [string]::IsNullOrWhiteSpace($ManagedBrowser.EdgeExtensionId)) {
    $edgePath = 'HKLM:\SOFTWARE\Policies\Microsoft\Edge\ExtensionInstallForcelist'
    $edgeSettingsPath = 'HKLM:\SOFTWARE\Policies\Microsoft\Edge'
    Set-ForceListPolicy -Path $edgePath -ExtensionId $ManagedBrowser.EdgeExtensionId -UpdateUrl $ManagedBrowser.UpdateUrl
    Set-ExtensionSettingsPolicy -Path $edgeSettingsPath -ExtensionId $ManagedBrowser.EdgeExtensionId -UpdateUrl $ManagedBrowser.UpdateUrl
    Set-ExtensionManagedStoragePolicy -Browser 'Edge' -ExtensionId $ManagedBrowser.EdgeExtensionId -Token $Token
    Write-Host 'Da ghi Edge ExtensionInstallForcelist.'
  }
}

Remove-ServiceIfPresent -Name $serviceName

New-Item -ItemType Directory -Force -Path $installDir | Out-Null
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
New-Item -ItemType Directory -Force -Path $configDir | Out-Null
Remove-Item -Path $managedExtensionDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $managedExtensionDir | Out-Null

Copy-Item -Path (Join-Path $publishDir '*') -Destination $installDir -Recurse -Force
Copy-Item -Path (Join-Path $PSScriptRoot '..\src\Guard.Service\appsettings.json') -Destination (Join-Path $installDir 'appsettings.json') -Force
Copy-Item -Path (Join-Path $PSScriptRoot '..\browser-extension\*') -Destination $managedExtensionDir -Recurse -Force

$installedSettingsPath = Join-Path $installDir 'appsettings.json'
$serviceExe = Join-Path $installDir 'Guard.Service.exe'
$guardToken = New-GuardToken
Update-AppSettings -Path $installedSettingsPath -Token $guardToken -UpdateUrl $managedExtensionUpdateUrl -UpdateManifestPath $managedExtensionUpdateManifestPath -CrxPath $managedExtensionCrxPath
Update-ExtensionToken -RulesPath (Join-Path $managedExtensionDir 'rules.js') -Token $guardToken
Update-ExtensionManifest -ManifestPath (Join-Path $managedExtensionDir 'manifest.json') -UpdateUrl $managedExtensionUpdateUrl
Invoke-ExtensionPack -ExtensionDir $managedExtensionDir -CrxPath $managedExtensionCrxPath -KeyPath $managedExtensionKeyPath
$managedExtensionId = Get-ExtensionId -ServiceExe $serviceExe -KeyPath $managedExtensionKeyPath
$managedExtensionVersion = Get-ExtensionVersion -ManifestPath (Join-Path $managedExtensionDir 'manifest.json')
New-UpdateManifest -ExtensionId $managedExtensionId -CrxUrl $managedExtensionCrxUrl -Version $managedExtensionVersion | Set-Content -Path $managedExtensionUpdateManifestPath -Encoding UTF8
Update-AppSettings -Path $installedSettingsPath -Token $guardToken -UpdateUrl $managedExtensionUpdateUrl -UpdateManifestPath $managedExtensionUpdateManifestPath -CrxPath $managedExtensionCrxPath -ExtensionId $managedExtensionId
$installedSettings = Get-Content $installedSettingsPath -Raw | ConvertFrom-Json
Apply-ManagedBrowserPolicy -ManagedBrowser $installedSettings.Guard.ManagedBrowserEndpoint -Token $guardToken

$serviceBinaryPath = '"' + $serviceExe + '"'
New-Service -Name $serviceName -BinaryPathName $serviceBinaryPath -DisplayName $serviceName -StartupType Automatic | Out-Null
Start-Process -FilePath sc.exe -ArgumentList @('description', $serviceName, 'Dich vu kiem soat noi dung nguoi lon cuc bo de tat may khi phat hien vi pham') -Wait -NoNewWindow | Out-Null
Start-Process -FilePath sc.exe -ArgumentList @('failure', $serviceName, 'reset=', '60', 'actions=', 'restart/5000/restart/5000/restart/5000') -Wait -NoNewWindow | Out-Null
Start-Service -Name $serviceName

Write-Host 'Da cai dat service.'
Write-Host 'Safe mode mac dinh khong tu doi DNS, firewall hoac browser policy.'
Write-Host 'Managed browser endpoint da bat voi token ngau nhien.'
Write-Host "Managed browser extension id: $managedExtensionId"
Write-Host "Extension local co token nam tai $managedExtensionDir"
Write-Host 'Kiem tra trang thai tai http://127.0.0.1:8765/health'
Stop-Transcript | Out-Null
