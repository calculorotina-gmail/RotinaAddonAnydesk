using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using AnyDeskMonitor.Agent.Services;
using Microsoft.Extensions.Logging;

namespace AnyDeskMonitor.Agent.IPC;

public class IpcMessage
{
    public string Command { get; set; } = string.Empty; // GetStatus, SyncNow, RestartService
    public string? Payload { get; set; }
}

public class IpcStatusResponse
{
    public string Status { get; set; } = "ONLINE"; // ONLINE, OFFLINE, SYNCING, ERROR
    public string MachineName { get; set; } = string.Empty;
    public string? AnyDeskId { get; set; }
    public string Version { get; set; } = "1.0.0";
    public DateTime? LastHeartbeat { get; set; }
    public string ServerUrl { get; set; } = string.Empty;
    public string DashboardUrl { get; set; } = string.Empty;
}

public class NamedPipeIpcServer
{
    private const string PipeName = "RotinaAddonAnydeskPipe";
    private readonly AgentConfig _config;
    private readonly HeartbeatService _heartbeatService;
    private readonly ComputerInfoService _computerInfo;
    private readonly AnyDeskInfoService _anyDeskInfo;
    private readonly ILogger<NamedPipeIpcServer> _logger;
    private DateTime? _lastSuccessfulHeartbeat;

    public NamedPipeIpcServer(
        AgentConfig config,
        HeartbeatService heartbeatService,
        ComputerInfoService computerInfo,
        AnyDeskInfoService anyDeskInfo,
        ILogger<NamedPipeIpcServer> logger)
    {
        _config = config;
        _heartbeatService = heartbeatService;
        _computerInfo = computerInfo;
        _anyDeskInfo = anyDeskInfo;
        _logger = logger;
    }

    public void SetLastHeartbeat(DateTime time)
    {
        _lastSuccessfulHeartbeat = time;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Servidor IPC NamedPipe iniciado no pipe '{PipeName}'", PipeName);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var pipeServer = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await pipeServer.WaitForConnectionAsync(cancellationToken);

                using var reader = new StreamReader(pipeServer, Encoding.UTF8, leaveOpen: true);
                using var writer = new StreamWriter(pipeServer, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

                var json = await reader.ReadLineAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var msg = JsonSerializer.Deserialize<IpcMessage>(json);
                    var responseJson = await HandleCommandAsync(msg);
                    await writer.WriteLineAsync(responseJson);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Erro no loop do servidor Named Pipe IPC.");
                await Task.Delay(1000, cancellationToken);
            }
        }
    }

    private async Task<string> HandleCommandAsync(IpcMessage? msg)
    {
        if (msg == null) return JsonSerializer.Serialize(new { Error = "Mensagem nula" });

        var anydesk = await _anyDeskInfo.GetAnyDeskInfoAsync();

        switch (msg.Command)
        {
            case "GetStatus":
                var response = new IpcStatusResponse
                {
                    Status = "ONLINE",
                    MachineName = _computerInfo.GetMachineName(),
                    AnyDeskId = anydesk?.AnyDeskId,
                    Version = "1.0.0",
                    LastHeartbeat = _lastSuccessfulHeartbeat ?? DateTime.UtcNow,
                    ServerUrl = _config.ServerUrl,
                    DashboardUrl = string.IsNullOrWhiteSpace(_config.WebDashboardUrl) ? _config.ServerUrl : _config.WebDashboardUrl
                };
                return JsonSerializer.Serialize(response);

            case "SyncNow":
                _logger.LogInformation("Comando SyncNow recebido via IPC.");
                await _heartbeatService.SendHeartbeatAsync(_config);
                return JsonSerializer.Serialize(new { Success = true, Message = "Sincronização iniciada." });

            default:
                return JsonSerializer.Serialize(new { Error = "Comando desconhecido" });
        }
    }
}

public class NamedPipeIpcClient
{
    private const string PipeName = "RotinaAddonAnydeskPipe";

    public async Task<IpcStatusResponse?> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipeClient.ConnectAsync(2000, cancellationToken);

            using var writer = new StreamWriter(pipeClient, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(pipeClient, Encoding.UTF8, leaveOpen: true);

            var req = JsonSerializer.Serialize(new IpcMessage { Command = "GetStatus" });
            await writer.WriteLineAsync(req);

            var respJson = await reader.ReadLineAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(respJson))
            {
                return JsonSerializer.Deserialize<IpcStatusResponse>(respJson);
            }
        }
        catch
        {
            // Windows Service may be starting or offline
        }

        return null;
    }

    public async Task<bool> TriggerSyncAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipeClient.ConnectAsync(2000, cancellationToken);

            using var writer = new StreamWriter(pipeClient, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(pipeClient, Encoding.UTF8, leaveOpen: true);

            var req = JsonSerializer.Serialize(new IpcMessage { Command = "SyncNow" });
            await writer.WriteLineAsync(req);

            var respJson = await reader.ReadLineAsync(cancellationToken);
            return !string.IsNullOrWhiteSpace(respJson) && respJson.Contains("true");
        }
        catch
        {
            return false;
        }
    }
}
