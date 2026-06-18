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
        var dns = _options.Dns.ListenAddress;
        var script = "$servers=@('" + EscapePowerShell(dns) + "');" +
                     "Get-DnsClientServerAddress -AddressFamily IPv4 | " +
                     "Where-Object { $_.InterfaceAlias -notmatch 'Loopback|vEthernet' } | " +
                     "ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.InterfaceIndex -ServerAddresses $servers -ErrorAction SilentlyContinue }";
        await _commandRunner.RunPowerShellAsync(script, cancellationToken);
    }

    private async Task ConfigureFirewallRulesAsync(CancellationToken cancellationToken)
    {
        var script = "Get-NetFirewallRule -DisplayName '" + RulePrefix + "*' -ErrorAction SilentlyContinue | Remove-NetFirewallRule;" +
                     "New-NetFirewallRule -DisplayName '" + RulePrefix + " Block DNS over TLS' -Direction Outbound -Action Block -Protocol TCP -RemotePort 853 -RemoteAddress Any | Out-Null;" +
                     "$paths=@(" +
                     "\"$env:ProgramFiles\\Google\\Chrome\\Application\\chrome.exe\"," +
                     "\"${env:ProgramFiles(x86)}\\Google\\Chrome\\Application\\chrome.exe\"," +
                     "\"$env:ProgramFiles\\Microsoft\\Edge\\Application\\msedge.exe\"," +
                     "\"${env:ProgramFiles(x86)}\\Microsoft\\Edge\\Application\\msedge.exe\"," +
                     "\"$env:ProgramFiles\\Mozilla Firefox\\firefox.exe\"," +
                     "\"${env:ProgramFiles(x86)}\\Mozilla Firefox\\firefox.exe\");" +
                     "$paths | Where-Object { Test-Path $_ } | ForEach-Object {" +
                     "New-NetFirewallRule -DisplayName ('" + RulePrefix + " Block UDP DNS ' + [IO.Path]::GetFileNameWithoutExtension($_)) -Direction Outbound -Action Block -Program $_ -Protocol UDP -RemotePort 53 | Out-Null;" +
                     "New-NetFirewallRule -DisplayName ('" + RulePrefix + " Block TCP DNS ' + [IO.Path]::GetFileNameWithoutExtension($_)) -Direction Outbound -Action Block -Program $_ -Protocol TCP -RemotePort 53 | Out-Null }";
        await _commandRunner.RunPowerShellAsync(script, cancellationToken);
    }

    private async Task<bool> DnsAdaptersUseLocalResolverAsync(CancellationToken cancellationToken)
    {
        var script = "$bad=Get-DnsClientServerAddress -AddressFamily IPv4 | " +
                     "Where-Object { $_.InterfaceAlias -notmatch 'Loopback|vEthernet' -and ($_.ServerAddresses -notcontains '" + EscapePowerShell(_options.Dns.ListenAddress) + "') };" +
                     "if ($bad) { exit 2 } else { exit 0 }";
        return await _commandRunner.RunPowerShellAsync(script, cancellationToken) == 0;
    }

    private async Task<bool> FirewallRulesExistAsync(CancellationToken cancellationToken)
    {
        var script = "$rule=Get-NetFirewallRule -DisplayName '" + RulePrefix + " Block DNS over TLS' -ErrorAction SilentlyContinue;" +
                     "if ($rule) { exit 0 } else { exit 2 }";
        return await _commandRunner.RunPowerShellAsync(script, cancellationToken) == 0;
    }

    private static string EscapePowerShell(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }
}
