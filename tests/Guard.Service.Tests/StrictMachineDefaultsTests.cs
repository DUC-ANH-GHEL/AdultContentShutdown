using System.Text.Json;
using AdultContentShutdownGuard.Guard.Service.Models;
using Xunit;

namespace Guard.Service.Tests;

public sealed class StrictMachineDefaultsTests
{
    [Fact]
    public void GuardOptions_defaults_enable_machine_wide_protection()
    {
        var options = new GuardOptions();

        Assert.True(options.Dns.Enabled);
        Assert.Equal(new[] { "127.0.0.1", "::1" }, options.Dns.ListenAddresses);
        Assert.True(options.Dns.ReturnNxDomain);
        Assert.True(options.Enforcement.ApplyOnStartup);
        Assert.True(options.Enforcement.ConfigureDnsAdapters);
        Assert.True(options.Enforcement.ConfigureFirewallRules);
        Assert.False(options.AllowMachineShutdown);
        Assert.Equal("LogOnly", options.Enforcement.ActionOnViolation);
        Assert.True(options.BrowserPolicies.Enabled);
        Assert.True(options.BrowserPolicies.DisableDnsOverHttps);
        Assert.True(options.BrowserPolicies.DisableQuic);
        Assert.True(options.BrowserPolicies.LockProxySettings);
        Assert.True(options.BrowserPolicies.DisablePrivateBrowsing);
        Assert.True(options.BrowserPolicies.DisableGuestMode);
        Assert.True(options.Tamper.RestoreSettings);
        Assert.Equal("LogOnly", options.NetworkPosture.ActionOnUnsafePosture);
    }

    [Fact]
    public void Appsettings_enables_machine_wide_protection_without_extension_configuration()
    {
        var appsettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        using var document = JsonDocument.Parse(File.ReadAllText(appsettingsPath));
        var guard = document.RootElement.GetProperty("Guard");

        Assert.True(guard.GetProperty("Dns").GetProperty("Enabled").GetBoolean());
        Assert.Equal(2, guard.GetProperty("Dns").GetProperty("ListenAddresses").GetArrayLength());
        Assert.True(guard.GetProperty("Dns").GetProperty("ReturnNxDomain").GetBoolean());
        Assert.True(guard.GetProperty("Enforcement").GetProperty("ApplyOnStartup").GetBoolean());
        Assert.True(guard.GetProperty("Enforcement").GetProperty("ConfigureDnsAdapters").GetBoolean());
        Assert.True(guard.GetProperty("Enforcement").GetProperty("ConfigureFirewallRules").GetBoolean());
        Assert.False(guard.GetProperty("AllowMachineShutdown").GetBoolean());
        Assert.Equal("LogOnly", guard.GetProperty("Enforcement").GetProperty("ActionOnViolation").GetString());
        Assert.True(guard.GetProperty("BrowserPolicies").GetProperty("Enabled").GetBoolean());
        Assert.True(guard.GetProperty("BrowserPolicies").GetProperty("DisablePrivateBrowsing").GetBoolean());
        Assert.True(guard.GetProperty("BrowserPolicies").GetProperty("DisableGuestMode").GetBoolean());
        Assert.False(guard.TryGetProperty("ManagedBrowserEndpoint", out _));
        Assert.False(guard.TryGetProperty("LegacyExtensionEndpointEnabled", out _));
        Assert.False(guard.TryGetProperty("Token", out _));
    }
}
