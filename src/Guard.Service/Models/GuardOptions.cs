namespace AdultContentShutdownGuard.Guard.Service.Models;

public sealed class GuardOptions
{
    public int Port { get; set; } = 8765;

    public string Token { get; set; } = "CHANGE_THIS_SECRET_TOKEN";

    public bool LegacyExtensionEndpointEnabled { get; set; }

    public bool DryRun { get; set; } = false;

    public string ShutdownCommand { get; set; } = "shutdown.exe";

    public string ShutdownArguments { get; set; } = "/s /t 0 /f /c \"Adult content blocked by AdultContentShutdownGuard\"";

    public string LogDirectory { get; set; } = @"C:\ProgramData\AdultContentShutdownGuard\Logs";

    public int DebounceSeconds { get; set; } = 3;

    public bool EnableDomainRules { get; set; } = true;

    public bool EnableKeywordRules { get; set; } = true;

    public int MinScoreToShutdown { get; set; } = 100;

    public DnsOptions Dns { get; set; } = new();

    public EnforcementOptions Enforcement { get; set; } = new();

    public BrowserPolicyOptions BrowserPolicies { get; set; } = new();

    public TamperOptions Tamper { get; set; } = new();

    public ProcessRulesOptions ProcessRules { get; set; } = new();

    public BlocklistUpdateOptions BlocklistUpdates { get; set; } = new();

    public PassiveDnsMonitorOptions PassiveDnsMonitor { get; set; } = new();

    public NetworkPostureOptions NetworkPosture { get; set; } = new();

    public ManagedBrowserEndpointOptions ManagedBrowserEndpoint { get; set; } = new();
}

public sealed class DnsOptions
{
    public bool Enabled { get; set; }

    public string ListenAddress { get; set; } = "127.0.0.1";

    public int ListenPort { get; set; } = 53;

    public string[] UpstreamServers { get; set; } = ["1.1.1.1", "8.8.8.8"];

    public int UpstreamTimeoutMilliseconds { get; set; } = 2500;

    public bool ReturnNxDomain { get; set; }

    public string SinkholeAddress { get; set; } = "0.0.0.0";
}

public sealed class EnforcementOptions
{
    public string ActionOnViolation { get; set; } = "Shutdown";

    public string ActionOnTamper { get; set; } = "Shutdown";

    public bool ApplyOnStartup { get; set; }

    public bool ConfigureDnsAdapters { get; set; }

    public bool ConfigureFirewallRules { get; set; }
}

public sealed class BrowserPolicyOptions
{
    public bool Enabled { get; set; }

    public bool DisableDnsOverHttps { get; set; } = true;

    public bool DisableQuic { get; set; } = true;

    public bool LockProxySettings { get; set; }
}

public sealed class TamperOptions
{
    public bool Enabled { get; set; } = true;

    public int CheckIntervalSeconds { get; set; } = 30;

    public bool RestoreSettings { get; set; }
}

public sealed class ProcessRulesOptions
{
    public bool Enabled { get; set; } = true;

    public int CheckIntervalSeconds { get; set; } = 10;

    public string ActionOnWorkVpnDetected { get; set; } = "LogOnly";

    public string[] AllowedWorkVpnProcesses { get; set; } =
    [
        "openvpn",
        "protonvpn",
        "nordvpn",
        "expressvpn",
        "windscribe",
        "cloudflare-warp",
        "warp-svc"
    ];

    public string[] BlockedProcessNames { get; set; } =
    [
        "tor",
        "torbrowser",
        "psiphon",
        "ultrasurf"
    ];
}

public sealed class BlocklistUpdateOptions
{
    public bool Enabled { get; set; } = true;

    public string RemoteUrl { get; set; } = "";

    public string Sha256 { get; set; } = "";

    public int RefreshIntervalMinutes { get; set; } = 1440;

    public string CacheFilePath { get; set; } = @"C:\ProgramData\AdultContentShutdownGuard\Config\adult-domains.remote.txt";
}

public sealed class PassiveDnsMonitorOptions
{
    public bool Enabled { get; set; } = true;

    public string EventLogName { get; set; } = "Microsoft-Windows-DNS-Client/Operational";

    public int PollIntervalSeconds { get; set; } = 5;

    public int LookbackMinutesOnStartup { get; set; } = 2;
}

public sealed class NetworkPostureOptions
{
    public bool Enabled { get; set; } = true;

    public int CheckIntervalSeconds { get; set; } = 60;

    public string ActionOnUnsafePosture { get; set; } = "LogOnly";
}

public sealed class ManagedBrowserEndpointOptions
{
    public bool Enabled { get; set; }

    public string ChromeExtensionId { get; set; } = "";

    public string EdgeExtensionId { get; set; } = "";

    public string UpdateUrl { get; set; } = "";

    public string UpdateManifestPath { get; set; } = "";

    public string CrxPath { get; set; } = "";
}
