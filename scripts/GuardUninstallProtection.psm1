$ErrorActionPreference = 'Stop'

$script:GuardSecurityDirectory = 'C:\ProgramData\AdultContentShutdownGuard\Security'
$script:GuardUninstallSecretPath = Join-Path $script:GuardSecurityDirectory 'uninstall-secret.bin'

function Assert-GuardAdmin {
  $currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
  $principal = New-Object Security.Principal.WindowsPrincipal($currentIdentity)
  if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Hay chay PowerShell voi quyen Administrator.'
  }
}

function Protect-GuardSecurityPath {
  param([Parameter(Mandatory = $true)][string]$Path)

  $acl = Get-Acl -LiteralPath $Path
  $acl.SetAccessRuleProtection($true, $false)

  foreach ($rule in @($acl.Access)) {
    [void]$acl.RemoveAccessRule($rule)
  }

  $item = Get-Item -LiteralPath $Path
  if ($item.PSIsContainer) {
    $inheritanceFlags = [System.Security.AccessControl.InheritanceFlags]'ContainerInherit,ObjectInherit'
    $propagationFlags = [System.Security.AccessControl.PropagationFlags]'None'
  } else {
    $inheritanceFlags = [System.Security.AccessControl.InheritanceFlags]'None'
    $propagationFlags = [System.Security.AccessControl.PropagationFlags]'None'
  }

  $adminRule = New-Object System.Security.AccessControl.FileSystemAccessRule(
    'BUILTIN\Administrators',
    'FullControl',
    $inheritanceFlags,
    $propagationFlags,
    'Allow')
  $systemRule = New-Object System.Security.AccessControl.FileSystemAccessRule(
    'NT AUTHORITY\SYSTEM',
    'FullControl',
    $inheritanceFlags,
    $propagationFlags,
    'Allow')

  $acl.AddAccessRule($adminRule)
  $acl.AddAccessRule($systemRule)
  Set-Acl -LiteralPath $Path -AclObject $acl
}

function New-GuardUninstallSecret {
  Assert-GuardAdmin

  New-Item -ItemType Directory -Force -Path $script:GuardSecurityDirectory | Out-Null
  Protect-GuardSecurityPath -Path $script:GuardSecurityDirectory

  if (-not (Test-Path -LiteralPath $script:GuardUninstallSecretPath)) {
    $bytes = New-Object byte[] 32
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try {
      $rng.GetBytes($bytes)
      [IO.File]::WriteAllBytes($script:GuardUninstallSecretPath, $bytes)
    }
    finally {
      $rng.Dispose()
    }
  }

  Protect-GuardSecurityPath -Path $script:GuardUninstallSecretPath
  return $script:GuardUninstallSecretPath
}

function Get-GuardUninstallSecret {
  if (-not (Test-Path -LiteralPath $script:GuardUninstallSecretPath)) {
    throw "Chua co uninstall secret: $script:GuardUninstallSecretPath"
  }

  return [IO.File]::ReadAllBytes($script:GuardUninstallSecretPath)
}

function ConvertTo-GuardUninstallCode {
  param(
    [Parameter(Mandatory = $true)][byte[]]$Secret,
    [Parameter(Mandatory = $true)][long]$HourBucket
  )

  $payload = [Text.Encoding]::UTF8.GetBytes([string]$HourBucket)
  $hmac = [Security.Cryptography.HMACSHA256]::new($Secret)
  try {
    $hash = $hmac.ComputeHash($payload)
    return (($hash[0..5] | ForEach-Object { $_.ToString('x2') }) -join '').ToUpperInvariant()
  }
  finally {
    $hmac.Dispose()
  }
}

function Get-GuardCurrentHourBucket {
  return [long][Math]::Floor([DateTimeOffset]::UtcNow.ToUnixTimeSeconds() / 3600)
}

function Get-GuardUninstallCode {
  Assert-GuardAdmin

  $secret = Get-GuardUninstallSecret
  return ConvertTo-GuardUninstallCode -Secret $secret -HourBucket (Get-GuardCurrentHourBucket)
}

function Test-GuardUninstallCode {
  param([Parameter(Mandatory = $true)][string]$Code)

  $secret = Get-GuardUninstallSecret
  $normalized = $Code.Trim().ToUpperInvariant()
  $currentHour = Get-GuardCurrentHourBucket

  foreach ($hour in @($currentHour - 1, $currentHour, $currentHour + 1)) {
    $expected = ConvertTo-GuardUninstallCode -Secret $secret -HourBucket $hour
    if ([Security.Cryptography.CryptographicOperations]::FixedTimeEquals(
      [Text.Encoding]::UTF8.GetBytes($expected),
      [Text.Encoding]::UTF8.GetBytes($normalized))) {
      return $true
    }
  }

  return $false
}

function Assert-GuardUninstallCode {
  param([string]$Code)

  Assert-GuardAdmin

  if ([string]::IsNullOrWhiteSpace($Code)) {
    $Code = Read-Host 'Nhap ma go cai dat AdultContentShutdownGuard hien tai'
  }

  if (-not (Test-GuardUninstallCode -Code $Code)) {
    throw 'Ma go cai dat khong hop le hoac da het han.'
  }
}

Export-ModuleMember -Function @(
  'Assert-GuardAdmin',
  'New-GuardUninstallSecret',
  'Get-GuardUninstallCode',
  'Test-GuardUninstallCode',
  'Assert-GuardUninstallCode')
