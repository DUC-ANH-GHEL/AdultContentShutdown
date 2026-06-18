$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$extensionDir = Join-Path $root 'browser-extension'
$distDir = Join-Path $root 'dist\edge-addons'
$tempDir = Join-Path $env:TEMP ('acsg-edge-package-' + [Guid]::NewGuid().ToString('N'))

New-Item -ItemType Directory -Force -Path $distDir | Out-Null
Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $tempDir | Out-Null

try {
  Copy-Item -Path (Join-Path $extensionDir '*') -Destination $tempDir -Recurse -Force
  Get-ChildItem -Path $tempDir -Recurse -File -Include '*.md' | Remove-Item -Force

  $manifestPath = Join-Path $tempDir 'manifest.json'
  $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
  if ($manifest.PSObject.Properties['update_url']) {
    $manifest.PSObject.Properties.Remove('update_url')
    $manifest | ConvertTo-Json -Depth 20 | Set-Content -Path $manifestPath -Encoding UTF8
  }

  foreach ($required in @('manifest.json', 'background.js', 'content.js', 'rules.js', 'schema.json', 'popup.html', 'popup.js', 'icons\icon16.png', 'icons\icon32.png', 'icons\icon48.png', 'icons\icon128.png')) {
    $path = Join-Path $tempDir $required
    if (-not (Test-Path $path)) {
      throw "Thieu file extension bat buoc: $required"
    }
  }

  $forbidden = Get-ChildItem -Path $tempDir -Recurse -File |
    Where-Object { $_.Extension -in @('.pem', '.crx') -or $_.Name -match 'private|secret' }
  if ($forbidden) {
    throw "Package dang co file khong nen upload: $($forbidden.FullName -join ', ')"
  }

  $version = [string]$manifest.version
  $packagePath = Join-Path $distDir "AdultContentShutdownGuard-edge-$version.zip"
  Remove-Item -Path $packagePath -Force -ErrorAction SilentlyContinue
  Compress-Archive -Path (Join-Path $tempDir '*') -DestinationPath $packagePath -Force
  Write-Host "Da tao Edge Add-ons package: $packagePath"
}
finally {
  Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}
