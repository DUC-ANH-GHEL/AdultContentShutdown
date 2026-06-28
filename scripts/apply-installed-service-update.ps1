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
$publishDir = Resolve-Path (Join-Path $PSScriptRoot '..\publish\Guard.Service')
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

$json = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
$json.Guard.Enforcement.ActionOnViolation = 'LogOnly'
$json | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $settingsPath -Encoding UTF8

Start-Service -Name $serviceName
(Get-Service -Name $serviceName).WaitForStatus('Running', [TimeSpan]::FromSeconds(30))

Write-Host 'Da cap nhat service dang cai dat: ActionOnViolation=LogOnly, service dang Running.'
