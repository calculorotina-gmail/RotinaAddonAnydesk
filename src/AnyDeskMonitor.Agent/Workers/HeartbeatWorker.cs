using AnyDeskMonitor.Agent.IPC;
using AnyDeskMonitor.Agent.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AnyDeskMonitor.Agent.Workers;

public class HeartbeatWorker : BackgroundService
{
    private readonly AgentConfig _config;
    private readonly HeartbeatService _heartbeatService;
    private readonly AgentRegistrationService _registrationService;
    private readonly NamedPipeIpcServer _ipcServer;
    private readonly ILogger<HeartbeatWorker> _logger;

    public HeartbeatWorker(
        AgentConfig config,
        HeartbeatService heartbeatService,
        AgentRegistrationService registrationService,
        NamedPipeIpcServer ipcServer,
        ILogger<HeartbeatWorker> logger)
    {
        _config = config;
        _heartbeatService = heartbeatService;
        _registrationService = registrationService;
        _ipcServer = ipcServer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HeartbeatWorker iniciado. Agente InstallationId: {InstallationId}", _config.InstallationId);

        // Start NamedPipe IPC Server in background task
        _ = Task.Run(() => _ipcServer.StartAsync(stoppingToken), stoppingToken);

        // Ensure registration on startup
        await _registrationService.EnsureRegisteredAsync(_config, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var success = await _heartbeatService.SendHeartbeatAsync(_config, stoppingToken);
                if (success)
                {
                    _ipcServer.SetLastHeartbeat(DateTime.UtcNow);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao executar o ciclo de heartbeat.");
            }

            var delaySeconds = _config.HeartbeatIntervalSeconds > 0 ? _config.HeartbeatIntervalSeconds : 30;
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
        }
    }
}
