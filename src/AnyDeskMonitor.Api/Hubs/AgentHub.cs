using AnyDeskMonitor.Shared.Constants;
using Microsoft.AspNetCore.SignalR;

namespace AnyDeskMonitor.Api.Hubs;

public class AgentHub : Hub
{
    public async Task JoinDashboardGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "DashboardClients");
    }

    public async Task LeaveDashboardGroup()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "DashboardClients");
    }

    public async Task BroadcastStatusUpdate(string message)
    {
        await Clients.Group("DashboardClients").SendAsync(SignalRConstants.Events.StatusChanged, message);
    }
}
