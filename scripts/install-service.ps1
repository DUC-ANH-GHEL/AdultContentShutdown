$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'GuardUninstallProtection.psm1') -Force

function Assert-Admin {
  $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
  $principal = [Security.Principal.WindowsPrincipal]::new($identity)
  if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Hay chay PowerShell voi quyen Administrator.'
  }
}

function Remove-LegacyExtensionPolicy {
  param([string[]]$ExtensionIds)

  foreach ($extensionId in ($ExtensionIds | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)) {
    foreach ($browserPath in @('HKLM:\SOFTWARE\Policies\Google\Chrome', 'HKLM:\SOFTWARE\Policies\Microsoft\Edge')) {
      $forceListPath = Join-Path $browserPath 'ExtensionInstallForcelist'
      if (Test-Path $forceListPath) {
        (Get-ItemProperty -Path $forceListPath).PSObject.Properties |
          Where-Object { $_.Name -notmatch '^PS' -and ([string]$_.Value).StartsWith("$extensionId;", [StringComparison]::OrdinalIgnoreCase) } |
          ForEach-Object { Remove-ItemProperty -Path $forceListPath -Name $_.Name -ErrorAction SilentlyContinue }
      }

      $browserKey = Get-Item -Path $browserPath -ErrorAction SilentlyContinue
      $json = if ($browserKey) { [string]$browserKey.GetValue('ExtensionSettings', '') } else { '' }
      if (-not [string]::IsNullOrWhiteSpace($json)) {
        try {
          $settings = $json | ConvertFrom-Json
          if ($settings.PSObject.Properties[$extensionId]) {
            $settings.PSObject.Properties.Remove($extensionId)
            Set-ItemProperty -Path $browserPath -Name 'ExtensionSettings' -Value ($settings | ConvertTo-Json -Compress -Depth 20) -Type String
          }
        } catch {
          Write-Warning "Khong doc duoc ExtensionSettings cu tai $browserPath; giu nguyen de tranh xoa nham policy khac."
        }
      }

      Remove-Item -Path (Join-Path $browserPath "3rdparty\extensions\$extensionId") -Recurse -Force -ErrorAction SilentlyContinue
    }

    $inPrivatePath = 'HKLM:\SOFTWARE\Policies\Microsoft\Edge\MandatoryExtensionsForInPrivateNavigation'
    if (Test-Path $inPrivatePath) {
      (Get-ItemProperty -Path $inPrivatePath).PSObject.Properties |
        Where-Object { $_.Name -notmatch '^PS' -and $_.Value -eq $extensionId } |
        ForEach-Object { Remove-ItemProperty -Path $inPrivatePath -Name $_.Name -ErrorAction SilentlyContinue }
    }
  }
}

Assert-Admin

$serviceName = 'AdultContentShutdownGuard'
$programDataDir = 'C:\ProgramData\AdultContentShutdownGuard'
$installDir = 'C:\Program Files\AdultContentShutdownGuard'
$publishDir = Join-Path $PSScriptRoot '..\publish\Guard.Service'
$publishScript = Join-Path $PSScriptRoot 'publish-service.ps1'
$installedSettingsPath = Join-Path $installDir 'appsettings.json'

if (-not (Test-Path $publishDir)) {
  & $publishScript
}

if (-not (Test-Path $publishDir)) {
  throw "Khong tim thay thu muc publish: $publishDir"
}

$legacyExtensionIds = @()
if (Test-Path $installedSettingsPath) {
  try {
    $oldSettings = Get-Content -LiteralPath $installedSettingsPath -Raw | ConvertFrom-Json
    $legacyExtensionIds += @($oldSettings.Guard.ManagedBrowserEndpoint.ChromeExtensionId, $oldSettings.Guard.ManagedBrowserEndpoint.EdgeExtensionId)
  } catch {
    Write-Warning 'Khong doc duoc cau hinh cu; bo qua don policy extension cu de tranh xoa nham policy khac.'
  }
}

$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($service) {
  Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
  & sc.exe delete $serviceName | Out-Null
  Start-Sleep -Seconds 1
}

Remove-LegacyExtensionPolicy -ExtensionIds $legacyExtensionIds
Remove-Item -LiteralPath (Join-Path $programDataDir 'browser-extension') -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath (Join-Path $programDataDir 'AdultContentShutdownGuard.crx') -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath (Join-Path $programDataDir 'AdultContentShutdownGuard.pem') -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath (Join-Path $programDataDir 'updates.xml') -Force -ErrorAction SilentlyContinue

New-Item -ItemType Directory -Force -Path $programDataDir, $installDir | Out-Null
New-GuardUninstallSecret | Out-Null
$dnsBackupPath = Join-Path $programDataDir 'dns-backup.json'
if (-not (Test-Path -LiteralPath $dnsBackupPath)) {
  Get-DnsClientServerAddress -ErrorAction Stop |
    Select-Object InterfaceIndex, ServerAddresses |
    ConvertTo-Json -Depth 5 |
    Set-Content -LiteralPath $dnsBackupPath -Encoding UTF8
}

Copy-Item -Path (Join-Path $publishDir '*') -Destination $installDir -Recurse -Force
Copy-Item -Path (Join-Path $PSScriptRoot '..\src\Guard.Service\appsettings.json') -Destination $installedSettingsPath -Force

# The service runs as LocalSystem; standard users may read logs but cannot alter rules, cache, or the uninstall secret.
& icacls.exe $programDataDir /inheritance:r /grant:r 'SYSTEM:(OI)(CI)(F)' 'Administrators:(OI)(CI)(F)' 'Users:(OI)(CI)(RX)' | Out-Null

$serviceExe = Join-Path $installDir 'Guard.Service.exe'
New-Service -Name $serviceName -BinaryPathName ('"' + $serviceExe + '"') -DisplayName $serviceName -StartupType Automatic | Out-Null
& sc.exe description $serviceName 'Dich vu loc noi dung nguoi lon cap may, khong phu thuoc extension trinh duyet.' | Out-Null
& sc.exe failure $serviceName 'reset=' '60' 'actions=' 'restart/5000/restart/5000/restart/5000' | Out-Null
Start-Service -Name $serviceName

Write-Host 'Da cai dat che do bao ve cap may.'
Write-Host 'DNS cuc bo IPv4/IPv6, chan DoH/QUIC, Private/Guest mode va tamper repair da duoc bat.'
Write-Host 'Kiem tra trang thai tai http://127.0.0.1:8765/health'
