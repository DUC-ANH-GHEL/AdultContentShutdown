using AdultContentShutdownGuard.Guard.Service.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AdultContentShutdownGuard.Guard.Service;

public sealed class Worker : BackgroundService
{
    private readonly LocalHttpServer _localHttpServer;
    private readonly LocalDnsResolverService _localDnsResolverService;
    private readonly PassiveDnsMonitorService _passiveDnsMonitorService;
    private readonly NetworkPostureMonitorService _networkPostureMonitorService;
    private readonly BlocklistUpdateService _blocklistUpdateService;
    private readonly SystemEnforcementService _systemEnforcementService;
    private readonly BrowserPolicyService _browserPolicyService;
    private readonly TamperMonitorService _tamperMonitorService;
    private readonly ProcessMonitorService _processMonitorService;
    private readonly FileLogger _fileLogger;
    private readonly ILogger<Worker> _logger;

    public Worker(
        LocalHttpServer localHttpServer,
        LocalDnsResolverService localDnsResolverService,
        PassiveDnsMonitorService passiveDnsMonitorService,
        NetworkPostureMonitorService networkPostureMonitorService,
        BlocklistUpdateService blocklistUpdateService,
        SystemEnforcementService systemEnforcementService,
        BrowserPolicyService browserPolicyService,
        TamperMonitorService tamperMonitorService,
        ProcessMonitorService processMonitorService,
        FileLogger fileLogger,
        ILogger<Worker> logger)
    {
        _localHttpServer = localHttpServer;
        _localDnsResolverService = localDnsResolverService;
        _passiveDnsMonitorService = passiveDnsMonitorService;
        _networkPostureMonitorService = networkPostureMonitorService;
        _blocklistUpdateService = blocklistUpdateService;
        _systemEnforcementService = systemEnforcementService;
        _browserPolicyService = browserPolicyService;
        _tamperMonitorService = tamperMonitorService;
        _processMonitorService = processMonitorService;
        _fileLogger = fileLogger;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AdultContentShutdownGuard service is starting.");
        await _fileLogger.LogAsync("INFO", "Service startup initiated.", cancellationToken: stoppingToken);

        await _blocklistUpdateService.InitializeAsync(stoppingToken);
        await _systemEnforcementService.ApplyAsync(stoppingToken);
        await _browserPolicyService.ApplyAsync(stoppingToken);
        await _localDnsResolverService.StartAsync(stoppingToken);
        await _passiveDnsMonitorService.StartAsync(stoppingToken);
        await _networkPostureMonitorService.StartAsync(stoppingToken);
        await _localHttpServer.StartAsync(stoppingToken);
        await _tamperMonitorService.StartAsync(stoppingToken);
        await _processMonitorService.StartAsync(stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }

        _logger.LogInformation("AdultContentShutdownGuard service is stopping.");
        await _fileLogger.LogAsync("INFO", "Service shutdown initiated.", cancellationToken: CancellationToken.None);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _processMonitorService.StopAsync(cancellationToken);
        await _tamperMonitorService.StopAsync(cancellationToken);
        await _networkPostureMonitorService.StopAsync(cancellationToken);
        await _passiveDnsMonitorService.StopAsync(cancellationToken);
        await _localHttpServer.StopAsync(cancellationToken);
        await _localDnsResolverService.StopAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
