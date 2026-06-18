using AdultContentShutdownGuard.Guard.Service.Models;
using Microsoft.Extensions.Options;
using Microsoft.Win32;

namespace AdultContentShutdownGuard.Guard.Service.Services;

public sealed class BrowserPolicyService
{
    private readonly GuardOptions _options;
    private readonly FileLogger _fileLogger;

    public BrowserPolicyService(IOptions<GuardOptions> options, FileLogger fileLogger)
    {
        _options = options.Value;
        _fileLogger = fileLogger;
    }

    public async Task ApplyAsync(CancellationToken cancellationToken)
    {
        if (!_options.BrowserPolicies.Enabled || !OperatingSystem.IsWindows())
        {
            return;
        }

        ApplyChromiumPolicy(@"SOFTWARE\Policies\Google\Chrome");
        ApplyChromiumPolicy(@"SOFTWARE\Policies\Microsoft\Edge");
        ApplyFirefoxPolicy();
        await _fileLogger.LogAsync("INFO", "Browser policies applied.", cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<string>> RepairAsync(CancellationToken cancellationToken)
    {
        if (PoliciesAreApplied())
        {
            return [];
        }

        await ApplyAsync(cancellationToken);
        return ["browser-policies"];
    }

    private void ApplyChromiumPolicy(string path)
    {
        using var key = Registry.LocalMachine.CreateSubKey(path);
        if (key is null)
        {
            return;
        }

        if (_options.BrowserPolicies.DisableDnsOverHttps)
        {
            key.SetValue("DnsOverHttpsMode", "off", RegistryValueKind.String);
        }

        if (_options.BrowserPolicies.DisableQuic)
        {
            key.SetValue("QuicAllowed", 0, RegistryValueKind.DWord);
        }

        if (_options.BrowserPolicies.LockProxySettings)
        {
            key.SetValue("ProxyMode", "direct", RegistryValueKind.String);
        }
    }

    private void ApplyFirefoxPolicy()
    {
        using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Mozilla\Firefox\DNSOverHTTPS");
        if (key is null)
        {
            return;
        }

        if (_options.BrowserPolicies.DisableDnsOverHttps)
        {
            key.SetValue("Enabled", 0, RegistryValueKind.DWord);
            key.SetValue("Locked", 1, RegistryValueKind.DWord);
        }
    }

    private bool PoliciesAreApplied()
    {
        if (!_options.BrowserPolicies.Enabled || !OperatingSystem.IsWindows())
        {
            return true;
        }

        return ChromiumPolicyIsApplied(@"SOFTWARE\Policies\Google\Chrome") &&
               ChromiumPolicyIsApplied(@"SOFTWARE\Policies\Microsoft\Edge") &&
               FirefoxPolicyIsApplied();
    }

    private bool ChromiumPolicyIsApplied(string path)
    {
        using var key = Registry.LocalMachine.OpenSubKey(path);
        if (key is null)
        {
            return false;
        }

        if (_options.BrowserPolicies.DisableDnsOverHttps &&
            !string.Equals(key.GetValue("DnsOverHttpsMode") as string, "off", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (_options.BrowserPolicies.DisableQuic && Convert.ToInt32(key.GetValue("QuicAllowed", 1)) != 0)
        {
            return false;
        }

        if (_options.BrowserPolicies.LockProxySettings &&
            !string.Equals(key.GetValue("ProxyMode") as string, "direct", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private bool FirefoxPolicyIsApplied()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Mozilla\Firefox\DNSOverHTTPS");
        if (key is null)
        {
            return false;
        }

        if (!_options.BrowserPolicies.DisableDnsOverHttps)
        {
            return true;
        }

        return Convert.ToInt32(key.GetValue("Enabled", 1)) == 0 &&
               Convert.ToInt32(key.GetValue("Locked", 0)) == 1;
    }
}
