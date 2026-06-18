using AdultContentShutdownGuard.Guard.Service.Models;
using AdultContentShutdownGuard.Guard.Service;
using AdultContentShutdownGuard.Guard.Service.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;

if (args.Length == 2 && string.Equals(args[0], "--extension-id", StringComparison.OrdinalIgnoreCase))
{
    var pem = await File.ReadAllTextAsync(args[1]);
    Console.WriteLine(ChromeExtensionId.FromPemPrivateKey(pem));
    return;
}

var host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options => options.ServiceName = "AdultContentShutdownGuard")
    .ConfigureServices((context, services) =>
    {
        services.Configure<GuardOptions>(context.Configuration.GetSection("Guard"));
        services.AddSingleton<FileLogger>();
        services.AddSingleton<ShutdownService>();
        services.AddSingleton<SystemCommandRunner>();
        services.AddSingleton<GuardEventService>();
        services.AddSingleton<BlocklistUpdateService>();
        services.AddSingleton<LocalDnsResolverService>();
        services.AddSingleton<PassiveDnsMonitorService>();
        services.AddSingleton<NetworkPostureMonitorService>();
        services.AddSingleton<SystemEnforcementService>();
        services.AddSingleton<BrowserPolicyService>();
        services.AddSingleton<TamperMonitorService>();
        services.AddSingleton<ProcessMonitorService>();
        services.AddSingleton<ContentViolationService>();
        services.AddSingleton<LocalHttpServer>();
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();
