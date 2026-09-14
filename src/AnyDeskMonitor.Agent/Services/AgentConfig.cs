using System.Text.Json;

namespace AnyDeskMonitor.Agent.Services;

public class AgentConfig
{
    public string ServerUrl { get; set; } = "http://localhost:5000";
    public string WebDashboardUrl { get; set; } = "http://localhost:7001";
    public string AgentId { get; set; } = string.Empty;
    public string InstallationId { get; set; } = Guid.NewGuid().ToString();
    public int HeartbeatIntervalSeconds { get; set; } = 30;
    public bool AutoStart { get; set; } = true;
    public string? ApiToken { get; set; }

    public static string GetConfigDirectory()
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var dir = Path.Combine(programData, "RotinaAddonAnydesk");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return dir;
    }

    public static string GetConfigFilePath()
    {
        return Path.Combine(GetConfigDirectory(), "agentconfig.json");
    }

    public static AgentConfig Load()
    {
        try
        {
            var path = GetConfigFilePath();
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var config = JsonSerializer.Deserialize<AgentConfig>(json);
                if (config != null)
                {
                    var updated = false;
                    if (string.IsNullOrWhiteSpace(config.InstallationId))
                    {
                        config.InstallationId = Guid.NewGuid().ToString();
                        updated = true;
                    }
                    if (string.IsNullOrWhiteSpace(config.WebDashboardUrl) || config.WebDashboardUrl == "http://localhost:5000")
                    {
                        config.WebDashboardUrl = "http://localhost:7001";
                        updated = true;
                    }
                    if (updated)
                    {
                        config.Save();
                    }
                    return config;
                }
            }
        }
        catch
        {
            // Fallback to default
        }

        var newConfig = new AgentConfig();
        newConfig.Save();
        return newConfig;
    }

    public void Save()
    {
        try
        {
            var path = GetConfigFilePath();
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch
        {
            // Handle write errors gracefully
        }
    }
}

