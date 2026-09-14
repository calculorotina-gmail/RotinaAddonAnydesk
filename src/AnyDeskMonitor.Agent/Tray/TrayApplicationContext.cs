using System.Diagnostics;
using System.Drawing;
using System.ServiceProcess;
using System.Windows.Forms;
using AnyDeskMonitor.Agent.IPC;
using AnyDeskMonitor.Agent.Services;

namespace AnyDeskMonitor.Agent.Tray;

public class TrayApplicationContext : ApplicationContext
{
    private const string ServiceName = "RotinaAddonAnydesk";
    private readonly NotifyIcon _notifyIcon;
    private readonly NamedPipeIpcClient _ipcClient;
    private readonly System.Windows.Forms.Timer _updateTimer;

    private ToolStripMenuItem _headerItem = null!;
    private ToolStripMenuItem _serviceStatusItem = null!;
    private ToolStripMenuItem _computerItem = null!;
    private ToolStripMenuItem _anydeskItem = null!;

    private ToolStripMenuItem _btnStartService = null!;
    private ToolStripMenuItem _btnStopService = null!;
    private ToolStripMenuItem _btnRestartService = null!;

    private string _dashboardUrl = "http://localhost:7001";

    public TrayApplicationContext()
    {
        _ipcClient = new NamedPipeIpcClient();

        _notifyIcon = new NotifyIcon
        {
            Icon = GetApplicationIcon(),
            Text = "Rotina AnyDesk Monitor",
            Visible = true
        };

        _notifyIcon.ContextMenuStrip = BuildContextMenu();
        _notifyIcon.DoubleClick += (s, e) => OpenDashboard();

        _updateTimer = new System.Windows.Forms.Timer
        {
            Interval = 4000
        };
        _updateTimer.Tick += async (s, e) => await RefreshStatusAsync();
        _updateTimer.Start();

        Task.Run(async () => await RefreshStatusAsync());
    }

    private static Icon GetApplicationIcon()
    {
        try
        {
            var exePath = Application.ExecutablePath;
            var extracted = Icon.ExtractAssociatedIcon(exePath);
            if (extracted != null) return extracted;
        }
        catch { }

        try
        {
            var iconFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            if (File.Exists(iconFile))
            {
                return new Icon(iconFile);
            }
        }
        catch { }

        return SystemIcons.Application;
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();

        _headerItem = new ToolStripMenuItem("Rotina AnyDesk Monitor") { Enabled = false, Font = new Font(Control.DefaultFont, FontStyle.Bold) };
        _serviceStatusItem = new ToolStripMenuItem("Serviço Windows: 🟠 A verificar...") { Enabled = false };
        _computerItem = new ToolStripMenuItem("Computador: ...") { Enabled = false };
        _anydeskItem = new ToolStripMenuItem("AnyDesk ID: ...") { Enabled = false };

        menu.Items.Add(_headerItem);
        menu.Items.Add(_serviceStatusItem);
        menu.Items.Add(_computerItem);
        menu.Items.Add(_anydeskItem);
        menu.Items.Add(new ToolStripSeparator());

        var btnOpenPanel = new ToolStripMenuItem("🌐 Abrir Painel Web", null, (s, e) => OpenDashboard());
        
        _btnStartService = new ToolStripMenuItem("▶ Iniciar Serviço Windows", null, (s, e) => StartService());
        _btnStopService = new ToolStripMenuItem("⏹ Parar Serviço Windows", null, (s, e) => StopService());
        _btnRestartService = new ToolStripMenuItem("🔄 Reiniciar Serviço Windows", null, (s, e) => RestartService());

        menu.Items.Add(btnOpenPanel);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_btnStartService);
        menu.Items.Add(_btnStopService);
        menu.Items.Add(_btnRestartService);
        menu.Items.Add(new ToolStripSeparator());

        var btnAgentStatus = new ToolStripMenuItem("ℹ️ Estado detalhado", null, async (s, e) => await ShowStatusDialogAsync());
        var btnSyncNow = new ToolStripMenuItem("⚡ Sincronizar agora", null, async (s, e) => await TriggerSyncAsync());
        var btnSettings = new ToolStripMenuItem("⚙️ Configurações", null, (s, e) => OpenSettings());
        var btnViewLogs = new ToolStripMenuItem("📁 Ver pasta de logs", null, (s, e) => OpenLogs());

        menu.Items.Add(btnAgentStatus);
        menu.Items.Add(btnSyncNow);
        menu.Items.Add(btnSettings);
        menu.Items.Add(btnViewLogs);
        menu.Items.Add(new ToolStripSeparator());

        var btnExit = new ToolStripMenuItem("❌ Fechar Agente Tray", null, (s, e) => Exit());
        menu.Items.Add(btnExit);

        return menu;
    }

    private async Task RefreshStatusAsync()
    {
        var status = await _ipcClient.GetStatusAsync();
        if (status != null)
        {
            _serviceStatusItem.Text = $"Serviço Windows: 🟢 EM EXECUÇÃO ({status.Status})";
            _computerItem.Text = $"Computador: {status.MachineName}";
            _anydeskItem.Text = string.IsNullOrWhiteSpace(status.AnyDeskId) ? "AnyDesk ID: Indisponível" : $"AnyDesk ID: {status.AnyDeskId}";
            _notifyIcon.Text = $"Rotina AnyDesk Monitor ({status.MachineName})\nServiço: Online";

            _btnStartService.Enabled = false;
            _btnStopService.Enabled = true;
            _btnRestartService.Enabled = true;

            if (!string.IsNullOrWhiteSpace(status.DashboardUrl))
            {
                _dashboardUrl = status.DashboardUrl;
            }
        }
        else
        {
            var isServiceRunning = IsWindowsServiceRunning();
            if (isServiceRunning)
            {
                _serviceStatusItem.Text = "Serviço Windows: 🟡 A INICIALIZAR...";
                _notifyIcon.Text = "Rotina AnyDesk Monitor (Serviço a iniciar)";
                _btnStartService.Enabled = false;
                _btnStopService.Enabled = true;
                _btnRestartService.Enabled = true;
            }
            else
            {
                _serviceStatusItem.Text = "Serviço Windows: 🔴 PARADO";
                _notifyIcon.Text = "Rotina AnyDesk Monitor (Serviço parado)";
                _btnStartService.Enabled = true;
                _btnStopService.Enabled = false;
                _btnRestartService.Enabled = true;
            }
            _computerItem.Text = $"Computador: {Environment.MachineName}";
            _anydeskItem.Text = "AnyDesk ID: Indisponível";
        }
    }

    private static bool IsWindowsServiceRunning()
    {
        try
        {
            using var sc = new ServiceController(ServiceName);
            return sc.Status == ServiceControllerStatus.Running || sc.Status == ServiceControllerStatus.StartPending;
        }
        catch
        {
            return false;
        }
    }

    private void StartService()
    {
        RunServiceScCommand("start");
    }

    private void StopService()
    {
        RunServiceScCommand("stop");
    }

    private void RestartService()
    {
        Task.Run(async () =>
        {
            RunServiceScCommand("stop");
            await Task.Delay(2000);
            RunServiceScCommand("start");
        });
    }

    private static void RunServiceScCommand(string action)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"{action} {ServiceName}",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            };
            var proc = Process.Start(psi);
            proc?.WaitForExit(4000);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível executar '{action}' no serviço '{ServiceName}': {ex.Message}", "Permissão de Administrador", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OpenDashboard()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _dashboardUrl,
                UseShellExecute = true
            });
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{_dashboardUrl}\"",
                    UseShellExecute = true
                });
            }
            catch
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c start \"\" \"{_dashboardUrl}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Não foi possível abrir o painel web em {_dashboardUrl}: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }

    private async Task ShowStatusDialogAsync()
    {
        var status = await _ipcClient.GetStatusAsync();
        if (status != null)
        {
            MessageBox.Show(
                $"Rotina AnyDesk Monitor Agent v{status.Version}\n\n" +
                $"Computador: {status.MachineName}\n" +
                $"AnyDesk ID: {status.AnyDeskId ?? "Indisponível"}\n" +
                $"Último Heartbeat: {status.LastHeartbeat?.ToLocalTime().ToString() ?? "Nunca"}\n" +
                $"Servidor API: {status.ServerUrl}\n" +
                $"Painel Web: {_dashboardUrl}\n" +
                $"Estado: {status.Status}",
                "Estado do Agente",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        else
        {
            MessageBox.Show(
                $"Rotina AnyDesk Monitor Agent\n\n" +
                $"Computador: {Environment.MachineName}\n" +
                $"Serviço Windows: RotinaAddonAnydesk\n" +
                $"Estado do Serviço: {(IsWindowsServiceRunning() ? "Em execução (IPC indisponível)" : "Parado")}\n" +
                $"Painel Web Configurado: {_dashboardUrl}",
                "Estado do Agente",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    private async Task TriggerSyncAsync()
    {
        _serviceStatusItem.Text = "Serviço Windows: 🟠 SINCRONIZANDO...";
        var success = await _ipcClient.TriggerSyncAsync();
        if (success)
        {
            MessageBox.Show("Comando de sincronização enviado ao serviço Windows.", "Sincronização", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            MessageBox.Show("Não foi possível comunicar com o serviço. Verifique se o serviço RotinaAddonAnydesk está ativo.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        await RefreshStatusAsync();
    }

    private void OpenSettings()
    {
        var configPath = AgentConfig.GetConfigFilePath();
        if (File.Exists(configPath))
        {
            Process.Start(new ProcessStartInfo { FileName = configPath, UseShellExecute = true });
        }
        else
        {
            MessageBox.Show($"Ficheiro de configuração em: {configPath}", "Configurações", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void OpenLogs()
    {
        var dir = AgentConfig.GetConfigDirectory();
        if (Directory.Exists(dir))
        {
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        }
    }

    private void Exit()
    {
        _updateTimer.Stop();
        _notifyIcon.Visible = false;
        Application.Exit();
    }
}

