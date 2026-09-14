using AnyDeskMonitor.Agent.IPC;
using AnyDeskMonitor.Agent.Services;
using AnyDeskMonitor.Agent.Tray;
using AnyDeskMonitor.Agent.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;

namespace AnyDeskMonitor.Agent;

public class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var isTray = args.Contains("--tray") || Environment.UserInteractive;
        var isService = args.Contains("--service") || WindowsServiceHelpers.IsWindowsService();

        if (isTray && !isService)
        {
            // Run Windows Tray UI
            ApplicationConfiguration.Initialize();
            Application.Run(new TrayApplicationContext());
            return;
        }

        // Run Host as Windows Service or Console Worker
        var builder = Host.CreateDefaultBuilder(args)
            .UseWindowsService(options =>
            {
                options.ServiceName = "RotinaAddonAnydesk";
            })
            .ConfigureServices((hostContext, services) =>
            {
                var config = AgentConfig.Load();
                services.AddSingleton(config);

                services.AddHttpClient<ApiClientService>();
                services.AddSingleton<ComputerInfoService>();
                services.AddSingleton<AnyDeskInfoService>();
                services.AddSingleton<AgentRegistrationService>();
                services.AddSingleton<HeartbeatService>();
                services.AddSingleton<RemoteIdSyncService>();
                services.AddSingleton<NamedPipeIpcServer>();

                services.AddHostedService<HeartbeatWorker>();
                services.AddHostedService<ProcessSupervisorWorker>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.AddEventLog(settings =>
                {
                    settings.SourceName = "RotinaAddonAnydesk";
                });
            });

        builder.Build().Run();
    }
}
