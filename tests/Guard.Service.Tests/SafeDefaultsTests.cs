using AdultContentShutdownGuard.Guard.Service.Models;
using System.Text.Json;
using Xunit;

namespace Guard.Service.Tests;

public sealed class SafeDefaultsTests
{
    [Fact]
    public void GuardOptions_defaults_do_not_mutate_network_or_browser_settings()
    {
        var options = new GuardOptions();

        Assert.False(options.Dns.Enabled);
        Assert.False(options.Enforcement.ApplyOnStartup);
        Assert.False(options.Enforcement.ConfigureDnsAdapters);
        Assert.False(options.Enforcement.ConfigureFirewallRules);
        Assert.False(options.BrowserPolicies.Enabled);
        Assert.False(options.Tamper.RestoreSettings);
        Assert.True(options.PassiveDnsMonitor.Enabled);
        Assert.True(options.NetworkPosture.Enabled);
        Assert.Equal("LogOnly", options.NetworkPosture.ActionOnUnsafePosture);
        Assert.Contains("protonvpn", options.ProcessRules.AllowedWorkVpnProcesses);
        Assert.Contains("tor", options.ProcessRules.BlockedProcessNames);
        Assert.DoesNotContain("protonvpn", options.ProcessRules.BlockedProcessNames);
        Assert.Equal("LogOnly", options.ProcessRules.ActionOnWorkVpnDetected);
        Assert.False(options.ManagedBrowserEndpoint.Enabled);
        Assert.Equal("", options.ManagedBrowserEndpoint.UpdateManifestPath);
        Assert.Equal("", options.ManagedBrowserEndpoint.CrxPath);
    }

    [Fact]
    public void Appsettings_defaults_do_not_mutate_network_or_browser_settings()
    {
        var appsettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        using var document = JsonDocument.Parse(File.ReadAllText(appsettingsPath));
        var guard = document.RootElement.GetProperty("Guard");

        Assert.False(guard.GetProperty("Dns").GetProperty("Enabled").GetBoolean());
        Assert.False(guard.GetProperty("Enforcement").GetProperty("ApplyOnStartup").GetBoolean());
        Assert.False(guard.GetProperty("Enforcement").GetProperty("ConfigureDnsAdapters").GetBoolean());
        Assert.False(guard.GetProperty("Enforcement").GetProperty("ConfigureFirewallRules").GetBoolean());
        Assert.False(guard.GetProperty("BrowserPolicies").GetProperty("Enabled").GetBoolean());
        Assert.False(guard.GetProperty("Tamper").GetProperty("RestoreSettings").GetBoolean());
        Assert.True(guard.GetProperty("PassiveDnsMonitor").GetProperty("Enabled").GetBoolean());
        Assert.True(guard.GetProperty("NetworkPosture").GetProperty("Enabled").GetBoolean());
        Assert.Equal("LogOnly", guard.GetProperty("NetworkPosture").GetProperty("ActionOnUnsafePosture").GetString());
        Assert.Contains("protonvpn", guard.GetProperty("ProcessRules").GetProperty("AllowedWorkVpnProcesses").EnumerateArray().Select(item => item.GetString()));
        Assert.DoesNotContain("protonvpn", guard.GetProperty("ProcessRules").GetProperty("BlockedProcessNames").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal("LogOnly", guard.GetProperty("ProcessRules").GetProperty("ActionOnWorkVpnDetected").GetString());
        Assert.False(guard.GetProperty("ManagedBrowserEndpoint").GetProperty("Enabled").GetBoolean());
        Assert.Equal("", guard.GetProperty("ManagedBrowserEndpoint").GetProperty("UpdateManifestPath").GetString());
        Assert.Equal("", guard.GetProperty("ManagedBrowserEndpoint").GetProperty("CrxPath").GetString());
    }
}
