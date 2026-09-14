namespace AnyDeskMonitor.Shared.Constants;

public static class SignalRConstants
{
    public const string HubUrl = "/hubs/agent";

    public static class Events
    {
        public const string ComputerUpdated = "ComputerUpdated";
        public const string AgentUpdated = "AgentUpdated";
        public const string DashboardUpdated = "DashboardUpdated";
        public const string NewEventCreated = "NewEventCreated";
        public const string StatusChanged = "StatusChanged";
    }
}
