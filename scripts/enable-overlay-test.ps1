[CmdletBinding()]
param(
  [ValidateRange(2, 60)]
  [int]$DurationSeconds = 10,
  [string]$UserName = (Get-CimInstance Win32_ComputerSystem).UserName
)

$ErrorActionPreference = 'Stop'

function Assert-Admin {
  $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
  $principal = [Security.Principal.WindowsPrincipal]::new($identity)
  if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Hay chay PowerShell voi quyen Administrator.'
  }
}

function Set-JsonProperty {
  param([object]$Object, [string]$Name, [object]$Value)

  if ($Object.PSObject.Properties[$Name]) {
    $Object.$Name = $Value
  } else {
    $Object | Add-Member -NotePropertyName $Name -NotePropertyValue $Value
  }
}

Assert-Admin

if ([string]::IsNullOrWhiteSpace($UserName)) {
  throw 'Khong tim thay tai khoan dang dang nhap de chay bo dem.'
}

$installDir = 'C:\Program Files\AdultContentShutdownGuard'
$settingsPath = Join-Path $installDir 'appsettings.json'
$overlayExe = Join-Path $installDir 'Overlay\Guard.Overlay.exe'
if (-not (Test-Path -LiteralPath $settingsPath) -or -not (Test-Path -LiteralPath $overlayExe)) {
  throw 'Ban cai dat hien tai chua co bo dem. Hay cap nhat dich vu truoc.'
}

$json = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
if (-not $json.Guard.Overlay) {
  $json.Guard | Add-Member -NotePropertyName Overlay -NotePropertyValue ([pscustomobject]@{})
}
Set-JsonProperty -Object $json.Guard.Overlay -Name 'Enabled' -Value $true
Set-JsonProperty -Object $json.Guard.Overlay -Name 'DurationSeconds' -Value $DurationSeconds
Set-JsonProperty -Object $json.Guard -Name 'DryRun' -Value $false
Set-JsonProperty -Object $json.Guard -Name 'AllowMachineShutdown' -Value $false
Set-JsonProperty -Object $json.Guard.Enforcement -Name 'ActionOnViolation' -Value 'LogOnly'
Set-JsonProperty -Object $json.Guard.Enforcement -Name 'ActionOnTamper' -Value 'LogOnly'
$json | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $settingsPath -Encoding UTF8

$taskName = 'AdultContentShutdownGuard Overlay'
$taskAction = New-ScheduledTaskAction -Execute $overlayExe -Argument "--duration-seconds $DurationSeconds"
$taskPrincipal = New-ScheduledTaskPrincipal -UserId $UserName -LogonType Interactive -RunLevel Limited
$taskSettings = New-ScheduledTaskSettingsSet -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Minutes 2) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
Register-ScheduledTask -TaskName $taskName -Action $taskAction -Principal $taskPrincipal -Settings $taskSettings -Description 'Hien bo dem tam dung khi AdultContentShutdownGuard phat hien vi pham.' -Force | Out-Null

$service = Get-Service -Name 'AdultContentShutdownGuard' -ErrorAction Stop
if ($service.Status -eq 'Running') {
  Restart-Service -Name $service.Name -Force
} else {
  Start-Service -Name $service.Name
}
(Get-Service -Name $service.Name).WaitForStatus('Running', [TimeSpan]::FromSeconds(30))

Write-Host "Da bat bo dem thu $DurationSeconds giay cho $UserName."
