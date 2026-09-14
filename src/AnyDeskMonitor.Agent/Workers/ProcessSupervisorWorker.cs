using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AnyDeskMonitor.Agent.Workers;

public class ProcessSupervisorWorker : BackgroundService
{
    private readonly ILogger<ProcessSupervisorWorker> _logger;
    private Process? _apiProcess;
    private Process? _webProcess;

    public ProcessSupervisorWorker(ILogger<ProcessSupervisorWorker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var apiPath = Path.Combine(baseDir, "Api", "AnyDeskMonitor.Api.exe");
        var webPath = Path.Combine(baseDir, "Web", "AnyDeskMonitor.Web.exe");

        _logger.LogInformation("Supervisor de processos iniciado. A verificar sub-serviços Api ({ApiPath}) e Web ({WebPath})...", apiPath, webPath);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                EnsureProcessRunning(ref _apiProcess, apiPath, "http://localhost:5000", "Api", baseDir);
                EnsureProcessRunning(ref _webProcess, webPath, "http://localhost:7001", "Web", baseDir);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no supervisor de processos Api/Web.");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        StopChildProcesses();
    }

    private void EnsureProcessRunning(ref Process? process, string exePath, string urls, string serviceName, string baseDir)
    {
        if (!File.Exists(exePath))
        {
            return;
        }

        if (process != null && !process.HasExited)
        {
            return;
        }

        try
        {
            _logger.LogInformation("A iniciar sub-serviço {ServiceName} a partir de {ExePath}...", serviceName, exePath);

            var workingDir = Path.GetDirectoryName(exePath) ?? baseDir;

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            psi.EnvironmentVariables["ASPNETCORE_URLS"] = urls;

            process = Process.Start(psi);
            if (process != null)
            {
                _logger.LogInformation("Sub-serviço {ServiceName} iniciado com sucesso (PID {PID}) em {Urls}.", serviceName, process.Id, urls);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao iniciar sub-serviço {ServiceName} em {ExePath}.", serviceName, exePath);
        }
    }

    private void StopChildProcesses()
    {
        _logger.LogInformation("A finalizar sub-serviços Api e Web...");
        StopProcess(ref _apiProcess, "Api");
        StopProcess(ref _webProcess, "Web");
    }

    private void StopProcess(ref Process? process, string serviceName)
    {
        if (process != null && !process.HasExited)
        {
            try
            {
                process.Kill(true);
                process.WaitForExit(3000);
                _logger.LogInformation("Sub-serviço {ServiceName} parado com sucesso.", serviceName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao parar sub-serviço {ServiceName}.", serviceName);
            }
            finally
            {
                process.Dispose();
                process = null;
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        StopChildProcesses();
        await base.StopAsync(cancellationToken);
    }
}
