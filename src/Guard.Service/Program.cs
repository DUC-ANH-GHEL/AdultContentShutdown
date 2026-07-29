using AdultContentShutdownGuard.Guard.Service.Models;
using AdultContentShutdownGuard.Guard.Service;
using AdultContentShutdownGuard.Guard.Service.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
        services.AddSingleton<LocalHttpServer>();
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();
