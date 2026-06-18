using AdultContentShutdownGuard.Guard.Service.Models;
using Microsoft.Extensions.Options;

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
        var script = "$chrome=(Get-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Google\\Chrome' -ErrorAction SilentlyContinue).DnsOverHttpsMode;" +
                     "$edge=(Get-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Edge' -ErrorAction SilentlyContinue).DnsOverHttpsMode;" +
                     "$ff=(Get-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Mozilla\\Firefox\\DNSOverHTTPS' -ErrorAction SilentlyContinue).Enabled;" +
                     "if ($chrome -eq 'off' -and $edge -eq 'off' -and $ff -eq 0) { exit 0 } else { exit 2 }";
        return await _commandRunner.RunPowerShellAsync(script, logNonZeroExit: false, cancellationToken) == 0;
    }

    private async Task<bool> DnsAdaptersUseLocalResolverAsync(CancellationToken cancellationToken)
    {
        var script = "$bad=Get-DnsClientServerAddress -AddressFamily IPv4 | " +
                     "Where-Object { $_.InterfaceAlias -notmatch 'Loopback|vEthernet' -and ($_.ServerAddresses -notcontains '" + EscapePowerShell(_options.Dns.ListenAddress) + "') };" +
                     "if ($bad) { exit 2 } else { exit 0 }";
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
}
