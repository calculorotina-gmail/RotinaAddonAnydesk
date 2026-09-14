using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using AnyDeskMonitor.Domain.Interfaces;
using AnyDeskMonitor.Domain.Services;

namespace AnyDeskMonitor.Agent.Services;

public class ComputerInfoService
{
    public string GetMachineName() => Environment.MachineName;

    public string GetOperatingSystem()
    {
        if (OperatingSystem.IsWindows()) return "Windows";
        if (OperatingSystem.IsLinux()) return "Linux";
        if (OperatingSystem.IsMacOS()) return "macOS";
        return RuntimeInformation.OSDescription;
    }

    public string GetOperatingSystemVersion() => Environment.OSVersion.VersionString;

    public string GetLocalIpAddress()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            var endPoint = socket.LocalEndPoint as IPEndPoint;
            return endPoint?.Address.ToString() ?? "127.0.0.1";
        }
        catch
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        return ip.ToString();
                    }
                }
            }
            catch
            {
                // Fallback
            }
        }
        return "127.0.0.1";
    }

    public string? GetMacAddress()
    {
        try
        {
            var nic = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up &&
                                     n.NetworkInterfaceType != NetworkInterfaceType.Loopback);
            return nic?.GetPhysicalAddress().ToString();
        }
        catch
        {
            return null;
        }
    }
}

public class AnyDeskInfoService
{
    private readonly IAnyDeskProvider _provider;

    public AnyDeskInfoService()
    {
        _provider = new LocalAgentAnyDeskProvider();
    }

    public async Task<AnyDeskInfo?> GetAnyDeskInfoAsync(CancellationToken cancellationToken = default)
    {
        return await _provider.GetAnyDeskInfoAsync(cancellationToken);
    }
}
