using System.Net;
using System.Net.Sockets;
using AdultContentShutdownGuard.Guard.Service.Models;
using Microsoft.Extensions.Options;

namespace AdultContentShutdownGuard.Guard.Service.Services;

public sealed class SystemEnforcementService
{
    private const string RulePrefix = "AdultContentShutdownGuard";
    private readonly GuardOptions _options;
    private readonly SystemCommandRunner _commandRunner;
    private readonly FileLogger _fileLogger;

    public SystemEnforcementService(IOptions<GuardOptions> options, SystemCommandRunner commandRunner, FileLogger fileLogger)
    {
        _options = options.Value;
        _commandRunner = commandRunner;
        _fileLogger = fileLogger;
    }

    public async Task ApplyAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enforcement.ApplyOnStartup)
        {
            return;
        }

        if (_options.Enforcement.ConfigureDnsAdapters)
        {
            await ConfigureDnsAdaptersAsync(cancellationToken);
        }

        if (_options.Enforcement.ConfigureFirewallRules)
        {
            await ConfigureFirewallRulesAsync(cancellationToken);
        }

        await _fileLogger.LogAsync("INFO", "System enforcement applied.", cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<string>> RepairAsync(CancellationToken cancellationToken)
    {
        var restored = new List<string>();
        if (_options.Enforcement.ConfigureDnsAdapters && !await DnsAdaptersUseLocalResolverAsync(cancellationToken))
        {
            await ConfigureDnsAdaptersAsync(cancellationToken);
            restored.Add("dns-adapters");
        }

        if (_options.Enforcement.ConfigureFirewallRules && !await FirewallRulesExistAsync(cancellationToken))
        {
            await ConfigureFirewallRulesAsync(cancellationToken);
            restored.Add("firewall-rules");
        }

        return restored;
    }

    private async Task ConfigureDnsAdaptersAsync(CancellationToken cancellationToken)
    {
        var resolverAddresses = string.Join(
            ",",
            _options.Dns.ListenAddresses
                .Select(IPAddress.Parse)
                .Where(IPAddress.IsLoopback)
                .Select(address => "'" + EscapePowerShell(address.ToString()) + "'"));
        if (string.IsNullOrWhiteSpace(resolverAddresses))
        {
            throw new InvalidOperationException("DNS must listen on at least one loopback address.");
        }

        var script = "$adapters=Get-NetAdapter -IncludeHidden -ErrorAction SilentlyContinue | " +
                     "Where-Object { $_.Status -ne 'Disabled' -and $_.Name -notmatch 'Loopback' };" +
                     "foreach($adapter in $adapters) { " +
                     "Set-DnsClientServerAddress -InterfaceIndex $adapter.ifIndex -ServerAddresses @(" + resolverAddresses + ") -ErrorAction SilentlyContinue }";
        await _commandRunner.RunPowerShellAsync(script, cancellationToken);
    }

    private async Task ConfigureFirewallRulesAsync(CancellationToken cancellationToken)
    {
        var script = "Get-NetFirewallRule -DisplayName '" + RulePrefix + "*' -ErrorAction SilentlyContinue | Remove-NetFirewallRule;" +
                     "New-NetFirewallRule -DisplayName '" + RulePrefix + " Block DNS over TLS' -Direction Outbound -Action Block -Protocol TCP -RemotePort 853 -RemoteAddress Any | Out-Null;" +
                     "$cocCocTorClientPattern=Join-Path $env:LOCALAPPDATA 'CocCoc\\Browser\\User Data\\CocCocTorClient\\*\\tor-client-win32.exe';" +
                     "Get-ChildItem -Path $cocCocTorClientPattern -File -ErrorAction SilentlyContinue | ForEach-Object { New-NetFirewallRule -DisplayName '" + RulePrefix + " Block CocCoc Tor' -Direction Outbound -Action Block -Program $_.FullName | Out-Null };" +
                     "$paths=@(" +
                     "\"$env:ProgramFiles\\Google\\Chrome\\Application\\chrome.exe\"," +
                     "\"${env:ProgramFiles(x86)}\\Google\\Chrome\\Application\\chrome.exe\"," +
                     "\"$env:LOCALAPPDATA\\Google\\Chrome\\Application\\chrome.exe\"," +
                     "\"$env:ProgramFiles\\Microsoft\\Edge\\Application\\msedge.exe\"," +
                     "\"${env:ProgramFiles(x86)}\\Microsoft\\Edge\\Application\\msedge.exe\"," +
                     "\"$env:LOCALAPPDATA\\Microsoft\\Edge\\Application\\msedge.exe\"," +
                     "\"$env:ProgramFiles\\Mozilla Firefox\\firefox.exe\"," +
                     "\"${env:ProgramFiles(x86)}\\Mozilla Firefox\\firefox.exe\"," +
                     "\"$env:LOCALAPPDATA\\Mozilla Firefox\\firefox.exe\"," +
                     "\"$env:ProgramFiles\\BraveSoftware\\Brave-Browser\\Application\\brave.exe\"," +
                     "\"$env:LOCALAPPDATA\\BraveSoftware\\Brave-Browser\\Application\\brave.exe\"," +
                     "\"$env:ProgramFiles\\Opera\\opera.exe\"," +
                     "\"$env:LOCALAPPDATA\\Programs\\Opera\\opera.exe\"," +
                     "\"$env:LOCALAPPDATA\\Vivaldi\\Application\\vivaldi.exe\"," +
                     "\"$env:ProgramFiles\\CocCoc\\Browser\\Application\\browser.exe\"," +
                     "\"${env:ProgramFiles(x86)}\\CocCoc\\Browser\\Application\\browser.exe\"," +
                     "\"$env:LOCALAPPDATA\\CocCoc\\Browser\\Application\\browser.exe\");" +
                     "$paths | Where-Object { Test-Path $_ } | Select-Object -Unique | ForEach-Object {" +
                     "New-NetFirewallRule -DisplayName ('" + RulePrefix + " Block UDP DNS ' + [IO.Path]::GetFileNameWithoutExtension($_)) -Direction Outbound -Action Block -Program $_ -Protocol UDP -RemotePort 53 | Out-Null;" +
                     "New-NetFirewallRule -DisplayName ('" + RulePrefix + " Block TCP DNS ' + [IO.Path]::GetFileNameWithoutExtension($_)) -Direction Outbound -Action Block -Program $_ -Protocol TCP -RemotePort 53 | Out-Null;" +
                     "New-NetFirewallRule -DisplayName ('" + RulePrefix + " Block QUIC ' + [IO.Path]::GetFileNameWithoutExtension($_)) -Direction Outbound -Action Block -Program $_ -Protocol UDP -RemotePort 443 | Out-Null }";
        await _commandRunner.RunPowerShellAsync(script, cancellationToken);
    }

    private async Task<bool> DnsAdaptersUseLocalResolverAsync(CancellationToken cancellationToken)
    {
        var ipv4 = EscapePowerShell(GetLoopbackAddress(AddressFamily.InterNetwork));
        var ipv6 = EscapePowerShell(GetLoopbackAddress(AddressFamily.InterNetworkV6));
        var script = "$v4=Get-DnsClientServerAddress -AddressFamily IPv4 | Where-Object { $_.InterfaceAlias -notmatch 'Loopback' -and $_.ServerAddresses.Count -gt 0 -and ($_.ServerAddresses -notcontains '" + ipv4 + "') };" +
                     "$v6=Get-DnsClientServerAddress -AddressFamily IPv6 | Where-Object { $_.InterfaceAlias -notmatch 'Loopback' -and $_.ServerAddresses.Count -gt 0 -and ($_.ServerAddresses -notcontains '" + ipv6 + "') };" +
                     "if ($v4 -or $v6) { exit 2 } else { exit 0 }";
        return await _commandRunner.RunPowerShellAsync(script, cancellationToken) == 0;
    }

    private async Task<bool> FirewallRulesExistAsync(CancellationToken cancellationToken)
    {
        var script = "$rule=Get-NetFirewallRule -DisplayName '" + RulePrefix + " Block DNS over TLS' -ErrorAction SilentlyContinue;" +
                     "$torClient=Get-ChildItem -Path (Join-Path $env:LOCALAPPDATA 'CocCoc\\Browser\\User Data\\CocCocTorClient\\*\\tor-client-win32.exe') -File -ErrorAction SilentlyContinue;" +
                     "$torRule=Get-NetFirewallRule -DisplayName '" + RulePrefix + " Block CocCoc Tor' -ErrorAction SilentlyContinue;" +
                     "if ($rule -and (-not $torClient -or $torRule)) { exit 0 } else { exit 2 }";
        return await _commandRunner.RunPowerShellAsync(script, cancellationToken) == 0;
    }

    private string GetLoopbackAddress(AddressFamily addressFamily)
    {
        var address = _options.Dns.ListenAddresses
            .Select(IPAddress.Parse)
            .FirstOrDefault(candidate => candidate.AddressFamily == addressFamily && IPAddress.IsLoopback(candidate));
        return address?.ToString() ?? throw new InvalidOperationException($"DNS must listen on a {addressFamily} loopback address.");
    }

    private static string EscapePowerShell(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}
