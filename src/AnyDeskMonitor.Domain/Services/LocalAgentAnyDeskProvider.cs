using System.Text.RegularExpressions;
using AnyDeskMonitor.Domain.Interfaces;

namespace AnyDeskMonitor.Domain.Services;

public class LocalAgentAnyDeskProvider : IAnyDeskProvider
{
    private static readonly Regex AnyDeskIdRegex = new(@"\b\d{9,10}\b|\b\d{3}\s\d{3}\s\d{3}\b", RegexOptions.Compiled);

    public async Task<AnyDeskInfo?> GetAnyDeskInfoAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            if (!IsAnyDeskInstalledOrRunning())
            {
                return new AnyDeskInfo(null, null, null, new List<DiscoveredRemoteIdInfo>(), new List<DiscoveredSessionInfo>());
            }

            string? localId = null;
            string? alias = null;
            string? primaryPath = null;
            var discoveredRemoteIds = new List<DiscoveredRemoteIdInfo>();
            var discoveredSessions = new List<DiscoveredSessionInfo>();
            var uniqueRemoteIds = new HashSet<string>();

            try
            {
                var candidatePaths = GetCandidateConfigFilePaths();
                foreach (var path in candidatePaths)
                {
                    if (File.Exists(path))
                    {
                        var info = ParseConfigFile(path);
                        if (info != null)
                        {
                            if (string.IsNullOrWhiteSpace(localId) && !string.IsNullOrWhiteSpace(info.AnyDeskId))
                            {
                                localId = info.AnyDeskId;
                                alias = info.Alias;
                                primaryPath = path;
                            }

                            if (info.RemoteIds != null)
                            {
                                foreach (var r in info.RemoteIds)
                                {
                                    if (uniqueRemoteIds.Add(r.AnyDeskId))
                                    {
                                        discoveredRemoteIds.Add(r);
                                    }
                                }
                            }

                            if (info.Sessions != null)
                            {
                                foreach (var s in info.Sessions)
                                {
                                    if (!discoveredSessions.Any(x => x.RemoteAnyDeskId == s.RemoteAnyDeskId))
                                    {
                                        discoveredSessions.Add(s);
                                    }
                                }
                            }
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(localId))
                {
                    localId = GetAnyDeskIdFromCli();
                }

                ParseTraceLogs(localId, discoveredRemoteIds, discoveredSessions);

                if (!string.IsNullOrWhiteSpace(localId))
                {
                    // Remove localId from discoveredRemoteIds if present
                    discoveredRemoteIds.RemoveAll(r => r.AnyDeskId == localId);
                    discoveredSessions.RemoveAll(s => s.RemoteAnyDeskId == localId);
                }
            }
            catch
            {
                // Silently handle read errors or permission issues safely
            }

            return new AnyDeskInfo(localId, alias, primaryPath, discoveredRemoteIds, discoveredSessions);
        }, cancellationToken);
    }

    private static string? GetAnyDeskIdFromCli()
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var candidateExes = new[]
            {
                Path.Combine(programFiles, "AnyDesk", "AnyDesk.exe"),
                Path.Combine(programFilesX86, "AnyDesk", "AnyDesk.exe")
            };

            foreach (var exe in candidateExes)
            {
                if (File.Exists(exe))
                {
                    using var p = new System.Diagnostics.Process();
                    p.StartInfo.FileName = exe;
                    p.StartInfo.Arguments = "--get-id";
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.StartInfo.CreateNoWindow = true;
                    p.Start();
                    string output = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit(2000);
                    var clean = output.Replace(" ", "").Replace("\r", "").Replace("\n", "");
                    if (!string.IsNullOrWhiteSpace(clean) && clean.Length >= 9 && clean.All(char.IsDigit))
                    {
                        return clean;
                    }
                }
            }
        }
        catch { }
        return null;
    }

    private static IEnumerable<string> GetCandidateConfigFilePaths()
    {
        var paths = new List<string>();

        if (OperatingSystem.IsWindows())
        {
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (!string.IsNullOrEmpty(programData))
            {
                paths.Add(Path.Combine(programData, "AnyDesk", "system.conf"));
                paths.Add(Path.Combine(programData, "AnyDesk", "user.conf"));
                paths.Add(Path.Combine(programData, "AnyDesk", "service.conf"));
            }

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrEmpty(appData))
            {
                paths.Add(Path.Combine(appData, "AnyDesk", "user.conf"));
                paths.Add(Path.Combine(appData, "AnyDesk", "system.conf"));
                paths.Add(Path.Combine(appData, "AnyDesk", "service.conf"));
            }

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData))
            {
                paths.Add(Path.Combine(localAppData, "AnyDesk", "user.conf"));
                paths.Add(Path.Combine(localAppData, "AnyDesk", "system.conf"));
                paths.Add(Path.Combine(localAppData, "AnyDesk", "service.conf"));
            }

            // Scan all user profiles in C:\Users to support Windows Service (SYSTEM account)
            try
            {
                var usersDir = @"C:\Users";
                if (Directory.Exists(usersDir))
                {
                    foreach (var userFolder in Directory.GetDirectories(usersDir))
                    {
                        var roaming = Path.Combine(userFolder, @"AppData\Roaming\AnyDesk");
                        if (Directory.Exists(roaming))
                        {
                            paths.Add(Path.Combine(roaming, "system.conf"));
                            paths.Add(Path.Combine(roaming, "user.conf"));
                            paths.Add(Path.Combine(roaming, "service.conf"));
                        }
                        var local = Path.Combine(userFolder, @"AppData\Local\AnyDesk");
                        if (Directory.Exists(local))
                        {
                            paths.Add(Path.Combine(local, "system.conf"));
                            paths.Add(Path.Combine(local, "user.conf"));
                            paths.Add(Path.Combine(local, "service.conf"));
                        }
                    }
                }
            }
            catch
            {
                // Silently skip if C:\Users access is restricted
            }
        }
        else
        {
            paths.Add("/etc/anydesk/system.conf");
            paths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".anydesk", "user.conf"));
        }

        return paths.Distinct();
    }

    public static AnyDeskInfo? ParseConfigFile(string filePath)
    {
        try
        {
            string? id = null;
            string? alias = null;
            var discoveredRemoteIds = new List<DiscoveredRemoteIdInfo>();
            var discoveredSessions = new List<DiscoveredSessionInfo>();
            var uniqueRemoteIds = new HashSet<string>();

            var lines = File.ReadAllLines(filePath);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("ad.anynet.id=", StringComparison.OrdinalIgnoreCase))
                {
                    id = trimmed.Substring("ad.anynet.id=".Length).Trim();
                }
                else if (trimmed.StartsWith("ad.anydesk.id=", StringComparison.OrdinalIgnoreCase))
                {
                    id = trimmed.Substring("ad.anydesk.id=".Length).Trim();
                }
                else if (string.IsNullOrEmpty(id) && trimmed.StartsWith("ad.telemetry.last_cid=", StringComparison.OrdinalIgnoreCase))
                {
                    id = trimmed.Substring("ad.telemetry.last_cid=".Length).Trim();
                }
                else if (trimmed.StartsWith("ad.anynet.alias=", StringComparison.OrdinalIgnoreCase))
                {
                    alias = trimmed.Substring("ad.anynet.alias=".Length).Trim();
                }
                else if (trimmed.StartsWith("ad.roster.alias=", StringComparison.OrdinalIgnoreCase))
                {
                    alias = trimmed.Substring("ad.roster.alias=".Length).Trim();
                }
                else if (trimmed.StartsWith("ad.anydesk.alias=", StringComparison.OrdinalIgnoreCase))
                {
                    alias = trimmed.Substring("ad.anydesk.alias=".Length).Trim();
                }

                // Extract remote AnyDesk IDs from config lines (e.g. ad.roster.items, ad.session.*)
                var matches = AnyDeskIdRegex.Matches(line);
                foreach (Match m in matches)
                {
                    var foundId = m.Value.Replace(" ", "").Replace("-", "").Replace(".", "");
                    if (!string.IsNullOrWhiteSpace(foundId) && foundId.Length >= 9 && foundId.All(char.IsDigit))
                    {
                        if (uniqueRemoteIds.Add(foundId))
                        {
                            discoveredRemoteIds.Add(new DiscoveredRemoteIdInfo(foundId, null, DateTime.UtcNow));
                        }
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(id) || discoveredRemoteIds.Any())
            {
                return new AnyDeskInfo(id, alias, filePath, discoveredRemoteIds, discoveredSessions);
            }
        }
        catch
        {
            // Ignore parse failures
        }

        return null;
    }

    private static void ParseTraceLogs(string? localId, List<DiscoveredRemoteIdInfo> remoteIds, List<DiscoveredSessionInfo> sessions)
    {
        var tracePaths = new List<string>();
        if (OperatingSystem.IsWindows())
        {
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (!string.IsNullOrEmpty(programData))
            {
                tracePaths.Add(Path.Combine(programData, "AnyDesk", "ad.trace"));
                tracePaths.Add(Path.Combine(programData, "AnyDesk", "ad_svc.trace"));
                tracePaths.Add(Path.Combine(programData, "AnyDesk", "connection_trace.txt"));
            }

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrEmpty(appData))
            {
                tracePaths.Add(Path.Combine(appData, "AnyDesk", "ad.trace"));
                tracePaths.Add(Path.Combine(appData, "AnyDesk", "ad_svc.trace"));
                tracePaths.Add(Path.Combine(appData, "AnyDesk", "connection_trace.txt"));
            }

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData))
            {
                tracePaths.Add(Path.Combine(localAppData, "AnyDesk", "ad.trace"));
                tracePaths.Add(Path.Combine(localAppData, "AnyDesk", "ad_svc.trace"));
                tracePaths.Add(Path.Combine(localAppData, "AnyDesk", "connection_trace.txt"));
            }

            try
            {
                var usersDir = @"C:\Users";
                if (Directory.Exists(usersDir))
                {
                    foreach (var userFolder in Directory.GetDirectories(usersDir))
                    {
                        var roaming = Path.Combine(userFolder, @"AppData\Roaming\AnyDesk");
                        if (Directory.Exists(roaming))
                        {
                            tracePaths.Add(Path.Combine(roaming, "ad.trace"));
                            tracePaths.Add(Path.Combine(roaming, "ad_svc.trace"));
                            tracePaths.Add(Path.Combine(roaming, "connection_trace.txt"));
                        }
                        var local = Path.Combine(userFolder, @"AppData\Local\AnyDesk");
                        if (Directory.Exists(local))
                        {
                            tracePaths.Add(Path.Combine(local, "ad.trace"));
                            tracePaths.Add(Path.Combine(local, "ad_svc.trace"));
                            tracePaths.Add(Path.Combine(local, "connection_trace.txt"));
                        }
                    }
                }
            }
            catch { }
        }

        var existingRemoteIds = new HashSet<string>(remoteIds.Select(r => r.AnyDeskId));
        var sessionKeys = new HashSet<string>(sessions.Select(s => $"{s.RemoteAnyDeskId}_{s.Direction}"));

        foreach (var path in tracePaths.Distinct())
        {
            if (!File.Exists(path)) continue;

            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs);
                string? line;
                while ((line = sr.ReadLine()) != null)
                {
                    var matches = AnyDeskIdRegex.Matches(line);
                    foreach (Match m in matches)
                    {
                        var foundId = m.Value.Replace(" ", "").Replace("-", "").Replace(".", "");
                        if (!string.IsNullOrWhiteSpace(foundId) && foundId != localId && foundId.Length >= 9 && foundId.All(char.IsDigit))
                        {
                            if (existingRemoteIds.Add(foundId))
                            {
                                remoteIds.Add(new DiscoveredRemoteIdInfo(foundId, null, DateTime.UtcNow));
                            }

                            var isIncoming = line.Contains("Incoming", StringComparison.OrdinalIgnoreCase) ||
                                             line.Contains("incoming", StringComparison.OrdinalIgnoreCase) ||
                                             line.Contains("accept", StringComparison.OrdinalIgnoreCase) ||
                                             line.Contains("Accept", StringComparison.OrdinalIgnoreCase) ||
                                             line.Contains("inbound", StringComparison.OrdinalIgnoreCase) ||
                                             line.Contains("Inbound", StringComparison.OrdinalIgnoreCase) ||
                                             line.Contains("request from", StringComparison.OrdinalIgnoreCase) ||
                                             line.Contains("Connecting from", StringComparison.OrdinalIgnoreCase) ||
                                             line.Contains("Receiving", StringComparison.OrdinalIgnoreCase) ||
                                             line.Contains("Entrada", StringComparison.OrdinalIgnoreCase);

                            var direction = isIncoming ? "ENTRADA" : "SAÍDA";
                            var sessionKey = $"{foundId}_{direction}";

                            if (sessionKeys.Add(sessionKey))
                            {
                                sessions.Add(new DiscoveredSessionInfo(localId ?? string.Empty, foundId, DateTime.UtcNow.Date, DateTime.UtcNow, "ACTIVE", direction));
                            }
                        }
                    }
                }
            }
            catch
            {
                // Silently skip trace file if locked or unreadable
            }
        }
    }

    public static bool IsAnyDeskInstalledOrRunning()
    {
        if (!OperatingSystem.IsWindows())
        {
            return File.Exists("/usr/bin/anydesk") || File.Exists("/usr/local/bin/anydesk");
        }

        try
        {
            var processes = System.Diagnostics.Process.GetProcessesByName("AnyDesk");
            if (processes != null && processes.Length > 0)
            {
                return true;
            }
        }
        catch { }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        var candidateExes = new[]
        {
            Path.Combine(programFiles, "AnyDesk", "AnyDesk.exe"),
            Path.Combine(programFilesX86, "AnyDesk", "AnyDesk.exe"),
            Path.Combine(localAppData, "AnyDesk", "AnyDesk.exe"),
            Path.Combine(appData, "AnyDesk", "AnyDesk.exe")
        };

        foreach (var exe in candidateExes)
        {
            if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
            {
                return true;
            }
        }

        try
        {
            using var key32 = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\AnyDesk");
            if (key32 != null) return true;

            using var key64 = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\AnyDesk");
            if (key64 != null) return true;
        }
        catch { }

        return false;
    }
}

public class OfficialAnyDeskApiProvider : IAnyDeskProvider
{
    private readonly string? _apiKey;

    public OfficialAnyDeskApiProvider(string? apiKey = null)
    {
        _apiKey = apiKey;
    }

    public Task<AnyDeskInfo?> GetAnyDeskInfoAsync(CancellationToken cancellationToken = default)
    {
        // Extensible stub implementation for future official API integration.
        return Task.FromResult<AnyDeskInfo?>(new AnyDeskInfo(null, null, "OfficialAnyDeskApiProvider"));
    }
}
