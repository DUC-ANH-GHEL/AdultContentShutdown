[CmdletBinding()]
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\src\Guard.Service\Config\adult-domains.snapshot.txt.gz')
)

$ErrorActionPreference = 'Stop'

$sourceUri = [Uri]'https://blocklistproject.github.io/Lists/porn.txt'
$maximumDownloadBytes = 35MB
$minimumDomains = 100000
$maximumDomains = 1200000
$downloadPath = Join-Path ([IO.Path]::GetTempPath()) ("adult-blocklist-" + [Guid]::NewGuid().ToString('N') + '.txt')
$temporaryOutputPath = $OutputPath + '.tmp'
$idn = [Globalization.IdnMapping]::new()
$domainLabel = [regex]::new('^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$', [Text.RegularExpressions.RegexOptions]::CultureInvariant -bor [Text.RegularExpressions.RegexOptions]::IgnoreCase)
$domains = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)

function ConvertTo-BlocklistDomain {
    param([string]$Candidate)

    $value = $Candidate.Trim().TrimEnd('.').ToLowerInvariant()
    if ([string]::IsNullOrWhiteSpace($value) -or $value.Length -gt 253) {
        return $null
    }

    $ipAddress = $null
    if ([Net.IPAddress]::TryParse($value, [ref]$ipAddress)) {
        return $null
    }

    try {
        $ascii = $idn.GetAscii($value).ToLowerInvariant()
    }
    catch {
        return $null
    }

    $labels = $ascii.Split('.', [StringSplitOptions]::RemoveEmptyEntries)
    if ($labels.Count -lt 2 -or @($labels | Where-Object { -not $domainLabel.IsMatch($_) }).Count -gt 0) {
        return $null
    }

    return $ascii
}

try {
    Invoke-WebRequest -Uri $sourceUri -OutFile $downloadPath -MaximumRedirection 2
    if ((Get-Item -LiteralPath $downloadPath).Length -gt $maximumDownloadBytes) {
        throw "Blocklist source exceeds the $maximumDownloadBytes byte limit."
    }

    foreach ($line in [IO.File]::ReadLines($downloadPath)) {
        $content = $line.Split('#', 2)[0]
        foreach ($token in $content.Split([char[]]$null, [StringSplitOptions]::RemoveEmptyEntries)) {
            $domain = ConvertTo-BlocklistDomain $token
            if ($null -ne $domain) {
                [void]$domains.Add($domain)
            }
        }
    }

    if ($domains.Count -lt $minimumDomains -or $domains.Count -gt $maximumDomains) {
        throw "Validated domain count ($($domains.Count)) is outside the safe limits."
    }

    $targetDirectory = Split-Path -Parent $OutputPath
    New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null
    $utf8NoBom = [Text.UTF8Encoding]::new($false)
    $output = [IO.File]::Create($temporaryOutputPath)
    $gzip = [IO.Compression.GZipStream]::new($output, [IO.Compression.CompressionLevel]::Optimal)
    $writer = [IO.StreamWriter]::new($gzip, $utf8NoBom)
    try {
        foreach ($domain in $domains) {
            $writer.WriteLine($domain)
        }
    }
    finally {
        $writer.Dispose()
        $gzip.Dispose()
        $output.Dispose()
    }

    Move-Item -LiteralPath $temporaryOutputPath -Destination $OutputPath -Force
    $hash = (Get-FileHash -LiteralPath $OutputPath -Algorithm SHA256).Hash
    $manifest = [ordered]@{
        source = $sourceUri.AbsoluteUri
        retrievedUtc = [DateTime]::UtcNow.ToString('O')
        domainCount = $domains.Count
        sha256 = $hash
    } | ConvertTo-Json
    [IO.File]::WriteAllText((Join-Path $targetDirectory 'adult-domains.snapshot.manifest.json'), $manifest + [Environment]::NewLine, $utf8NoBom)

    Write-Host "Created local snapshot with $($domains.Count) domains: $OutputPath"
}
finally {
    Remove-Item -LiteralPath $downloadPath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $temporaryOutputPath -Force -ErrorAction SilentlyContinue
}
