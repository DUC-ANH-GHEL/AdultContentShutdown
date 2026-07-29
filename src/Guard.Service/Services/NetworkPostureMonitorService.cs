using AdultContentShutdownGuard.Guard.Service.Models;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Sockets;

namespace AdultContentShutdownGuard.Guard.Service.Services;

public sealed class NetworkPostureMonitorService
{
    private readonly GuardOptions _options;
    private readonly SystemCommandRunner _commandRunner;
    private readonly GuardEventService _guardEventService;
    private Task? _monitorTask;
    private CancellationTokenSource? _cancellationTokenSource;
    private string _lastFingerprint = string.Empty;

    public NetworkPostureMonitorService(
        IOptions<GuardOptions> options,
        SystemCommandRunner commandRunner,
        GuardEventService guardEventService)
    {
        _options = options.Value;
        _commandRunner = commandRunner;
        _guardEventService = guardEventService;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.NetworkPosture.Enabled || _monitorTask is not null)
        {
            return Task.CompletedTask;
        }

        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _monitorTask = Task.Run(() => LoopAsync(_cancellationTokenSource.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource?.Cancel();
        if (_monitorTask is not null)
        {
            try
            {
                await _monitorTask.WaitAsync(cancellationToken);
            }
            catch
            {
            }
        }
    }

    private async Task LoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await CheckAsync(cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(10, _options.NetworkPosture.CheckIntervalSeconds)), cancellationToken);
        }
    }

    private async Task CheckAsync(CancellationToken cancellationToken)
    {
        var risks = new List<string>();

        if (!await BrowserPoliciesAreAppliedAsync(cancellationToken))
        {
            risks.Add("browser-doh-policy-not-enforced");
        }

        if (!await DnsAdaptersUseLocalResolverAsync(cancellationToken))
        {
            risks.Add("dns-adapter-not-managed-by-guard");
        }

        if (!await FirewallRulesExistAsync(cancellationToken))
        {
            risks.Add("guard-firewall-rules-not-installed");
        }

        var fingerprint = string.Join('|', risks.OrderBy(risk => risk, StringComparer.OrdinalIgnoreCase));
        if (risks.Count == 0 || string.Equals(fingerprint, _lastFingerprint, StringComparison.Ordinal))
        {
            return;
        }

        _lastFingerprint = fingerprint;
        await _guardEventService.HandleAsync(new GuardEvent
        {
            EventKind = GuardEventKind.UnsafeNetworkPosture,
            Reason = "Safe mode detected non-enforced network posture.",
            MatchedRules = risks.ToArray()
        }, cancellationToken);
    }

    private async Task<bool> BrowserPoliciesAreAppliedAsync(CancellationToken cancellationToken)
    {
        var script = "$chrome=Get-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Google\\Chrome' -ErrorAction SilentlyContinue;" +
                     "$edge=Get-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Edge' -ErrorAction SilentlyContinue;" +
                     "$ffDoh=Get-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Mozilla\\Firefox\\DNSOverHTTPS' -ErrorAction SilentlyContinue;" +
                     "$ff=Get-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Mozilla\\Firefox' -ErrorAction SilentlyContinue;" +
                     "if ($chrome.DnsOverHttpsMode -eq 'off' -and $chrome.QuicAllowed -eq 0 -and $chrome.ProxyMode -eq 'direct' -and $chrome.IncognitoModeAvailability -eq 1 -and $chrome.BrowserGuestModeEnabled -eq 0 -and $edge.DnsOverHttpsMode -eq 'off' -and $edge.QuicAllowed -eq 0 -and $edge.ProxyMode -eq 'direct' -and $edge.InPrivateModeAvailability -eq 1 -and $edge.BrowserGuestModeEnabled -eq 0 -and $ffDoh.Enabled -eq 0 -and $ffDoh.Locked -eq 1 -and $ff.DisablePrivateBrowsing -eq 1) { exit 0 } else { exit 2 }";
        return await _commandRunner.RunPowerShellAsync(script, logNonZeroExit: false, cancellationToken) == 0;
    }

    private async Task<bool> DnsAdaptersUseLocalResolverAsync(CancellationToken cancellationToken)
    {
        var ipv4 = EscapePowerShell(GetLoopbackAddress(AddressFamily.InterNetwork));
        var ipv6 = EscapePowerShell(GetLoopbackAddress(AddressFamily.InterNetworkV6));
        var script = "$v4=Get-DnsClientServerAddress -AddressFamily IPv4 | Where-Object { $_.InterfaceAlias -notmatch 'Loopback' -and $_.ServerAddresses.Count -gt 0 -and ($_.ServerAddresses -notcontains '" + ipv4 + "') };" +
                     "$v6=Get-DnsClientServerAddress -AddressFamily IPv6 | Where-Object { $_.InterfaceAlias -notmatch 'Loopback' -and $_.ServerAddresses.Count -gt 0 -and ($_.ServerAddresses -notcontains '" + ipv6 + "') };" +
                     "if ($v4 -or $v6) { exit 2 } else { exit 0 }";
        return await _commandRunner.RunPowerShellAsync(script, logNonZeroExit: false, cancellationToken) == 0;
    }

    private async Task<bool> FirewallRulesExistAsync(CancellationToken cancellationToken)
    {
        var script = "$rule=Get-NetFirewallRule -DisplayName 'AdultContentShutdownGuard Block DNS over TLS' -ErrorAction SilentlyContinue;" +
                     "if ($rule) { exit 0 } else { exit 2 }";
        return await _commandRunner.RunPowerShellAsync(script, logNonZeroExit: false, cancellationToken) == 0;
    }

    private static string EscapePowerShell(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }

    private string GetLoopbackAddress(AddressFamily addressFamily)
    {
        var address = _options.Dns.ListenAddresses
            .Select(IPAddress.Parse)
            .FirstOrDefault(candidate => candidate.AddressFamily == addressFamily && IPAddress.IsLoopback(candidate));
        return address?.ToString() ?? throw new InvalidOperationException($"DNS must listen on a {addressFamily} loopback address.");
    }
}
