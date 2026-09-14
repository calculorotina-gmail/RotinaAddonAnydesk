using AnyDeskMonitor.Application.Interfaces;
using AnyDeskMonitor.Application.Services;
using AnyDeskMonitor.Domain.Interfaces;
using AnyDeskMonitor.Domain.Services;
using AnyDeskMonitor.Infrastructure.Authentication;
using AnyDeskMonitor.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AnyDeskMonitor.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureAndApplication(this IServiceCollection services, IConfiguration configuration)
    {
        var connStr = configuration.GetConnectionString("DefaultConnection") 
                      ?? configuration["DATABASE_CONNECTION_STRING"];

        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (string.IsNullOrEmpty(programData))
        {
            programData = @"C:\ProgramData";
        }
        var dbDir = Path.Combine(programData, "AnyDeskMonitor");
        if (!Directory.Exists(dbDir))
        {
            Directory.CreateDirectory(dbDir);
        }
        var sqliteDbPath = Path.Combine(dbDir, "anydeskmonitor.db");
        var sqliteConnStr = $"Data Source={sqliteDbPath}";

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            if (!string.IsNullOrWhiteSpace(connStr) && 
                !connStr.Contains("InMemory", StringComparison.OrdinalIgnoreCase) && 
                !connStr.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase) &&
                !connStr.Contains("Host=", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    options.UseNpgsql(connStr);
                    return;
                }
                catch
                {
                    // Fallback to SQLite
                }
            }

            options.UseSqlite(sqliteConnStr);
        });

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        // Register Authentication & Infrastructure Services
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IPasswordHasher, DefaultPasswordHasher>();
        services.AddScoped<IAnyDeskProvider, LocalAgentAnyDeskProvider>();

        // Register Application Services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IComputerService, ComputerService>();
        services.AddScoped<IAgentService, AgentService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IRemoteIdService, RemoteIdService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IAuditService, AuditService>();

        return services;
    }
}
