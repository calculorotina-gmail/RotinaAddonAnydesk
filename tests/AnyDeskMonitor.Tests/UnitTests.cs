using AnyDeskMonitor.Application.Services;
using AnyDeskMonitor.Domain.Entities;
using AnyDeskMonitor.Domain.Enums;
using AnyDeskMonitor.Domain.Services;
using AnyDeskMonitor.Infrastructure.Authentication;
using AnyDeskMonitor.Infrastructure.Data;
using AnyDeskMonitor.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace AnyDeskMonitor.Tests;

public class AnyDeskProviderTests
{
    [Fact]
    public void ParseConfigFile_ValidContent_ReturnsAnyDeskInfo()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempFile, new[]
            {
                "ad.anydesk.id=123456789",
                "ad.roster.alias=PC-Recepcao@ad"
            });

            var result = LocalAgentAnyDeskProvider.ParseConfigFile(tempFile);

            Assert.NotNull(result);
            Assert.Equal("123456789", result.AnyDeskId);
            Assert.Equal("PC-Recepcao@ad", result.Alias);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void ParseConfigFile_AnynetIdAndRoster_ExtractsIdAndRemoteIds()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempFile, new[]
            {
                "ad.anynet.id=1738812731",
                "ad.roster.items=1050400175,1050400175,,;1183488151,1183488151,,;192196259,192196259,,"
            });

            var result = LocalAgentAnyDeskProvider.ParseConfigFile(tempFile);

            Assert.NotNull(result);
            Assert.Equal("1738812731", result.AnyDeskId);
            Assert.NotNull(result.RemoteIds);
            Assert.Contains(result.RemoteIds, r => r.AnyDeskId == "1050400175");
            Assert.Contains(result.RemoteIds, r => r.AnyDeskId == "1183488151");
            Assert.Contains(result.RemoteIds, r => r.AnyDeskId == "192196259");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task OfficialAnyDeskApiProvider_ReturnsNullInfo()
    {
        var provider = new OfficialAnyDeskApiProvider();
        var result = await provider.GetAnyDeskInfoAsync();

        Assert.NotNull(result);
        Assert.Null(result.AnyDeskId);
    }
}

public class AgentServiceTests
{
    private ApplicationDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task RegisterAgentAsync_CreatesComputerAndAgent()
    {
        var db = GetInMemoryDbContext();
        var eventService = new EventService(db);
        var agentService = new AgentService(db, eventService);

        var dto = new AgentRegistrationDto
        {
            InstallationId = "inst-guid-1234",
            MachineName = "TEST-PC",
            OperatingSystem = "Windows 11",
            OperatingSystemVersion = "10.0.22621",
            IPAddress = "192.168.1.100",
            Version = "1.0.0",
            AnyDeskId = "987654321"
        };

        var result = await agentService.RegisterAgentAsync(dto);

        Assert.NotNull(result);
        Assert.Equal("inst-guid-1234", result.InstallationId);
        Assert.Equal("TEST-PC", result.MachineName);

        var computer = await db.Computers.FirstOrDefaultAsync(c => c.MachineName == "TEST-PC");
        Assert.NotNull(computer);
        Assert.Equal("987654321", computer.AnyDeskId);
        Assert.Equal(ComputerStatus.ONLINE, computer.Status);
    }

    [Fact]
    public async Task ProcessHeartbeatAsync_UpdatesHeartbeatTimestamp()
    {
        var db = GetInMemoryDbContext();
        var eventService = new EventService(db);
        var agentService = new AgentService(db, eventService);

        var regDto = new AgentRegistrationDto
        {
            InstallationId = "inst-guid-5678",
            MachineName = "SERVER-01",
            OperatingSystem = "Windows Server 2022",
            IPAddress = "10.0.0.5",
            Version = "1.0.0"
        };

        await agentService.RegisterAgentAsync(regDto);

        var hbDto = new HeartbeatDto
        {
            InstallationId = "inst-guid-5678",
            MachineName = "SERVER-01",
            OperatingSystem = "Windows Server 2022",
            IPAddress = "10.0.0.5",
            Version = "1.0.1",
            AnyDeskId = "111222333"
        };

        var result = await agentService.ProcessHeartbeatAsync(hbDto);

        Assert.NotNull(result);
        Assert.Equal("1.0.1", result.Version);
        Assert.NotNull(result.LastHeartbeat);
    }

    [Fact]
    public async Task DashboardService_GetStatsAsync_CountsActiveInstalledAgents()
    {
        var db = GetInMemoryDbContext();
        db.Agents.Add(new AnyDeskMonitor.Domain.Entities.Agent { Id = Guid.NewGuid(), MachineName = "PC-01", Status = AgentStatus.ACTIVE, IsEnabled = true });
        db.Agents.Add(new AnyDeskMonitor.Domain.Entities.Agent { Id = Guid.NewGuid(), MachineName = "PC-02", Status = AgentStatus.OFFLINE, IsEnabled = true });
        db.Agents.Add(new AnyDeskMonitor.Domain.Entities.Agent { Id = Guid.NewGuid(), MachineName = "PC-03", Status = AgentStatus.INACTIVE, IsEnabled = false });
        await db.SaveChangesAsync();

        var dashboardService = new DashboardService(db);
        var stats = await dashboardService.GetStatsAsync();

        Assert.Equal(2, stats.ActiveAgents);
        Assert.Equal(1, stats.InactiveAgents);
    }
}

public class ComputerServiceTests
{
    private ApplicationDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GetAllComputersAsync_FiltersByNameAndStatus()
    {
        var db = GetInMemoryDbContext();
        db.Computers.Add(new Computer { Id = Guid.NewGuid(), Name = "Alpha", MachineName = "PC-ALPHA", Status = ComputerStatus.ONLINE });
        db.Computers.Add(new Computer { Id = Guid.NewGuid(), Name = "Beta", MachineName = "PC-BETA", Status = ComputerStatus.OFFLINE });
        await db.SaveChangesAsync();

        var eventService = new EventService(db);
        var computerService = new ComputerService(db, eventService);

        var onlineComputers = await computerService.GetAllComputersAsync(status: "ONLINE");
        Assert.Single(onlineComputers);
        Assert.Equal("Alpha", onlineComputers.First().Name);
    }
}

public class RemoteIdServiceTests
{
    private ApplicationDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task CreateRemoteIdAsync_AuditsCreation()
    {
        var db = GetInMemoryDbContext();
        var eventService = new EventService(db);
        var remoteIdService = new RemoteIdService(db, eventService);

        var computerId = Guid.NewGuid();
        var dto = new CreateRemoteAnyDeskIdDto
        {
            ComputerId = computerId,
            AnyDeskId = "555666777",
            Alias = "Remote Server",
            Description = "Test Remote ID"
        };

        var created = await remoteIdService.CreateRemoteIdAsync(dto, "TestUser");

        Assert.NotNull(created);
        Assert.Equal("555666777", created.AnyDeskId);

        var history = await db.RemoteAnyDeskIdHistories.FirstOrDefaultAsync(h => h.RemoteAnyDeskIdId == created.Id);
        Assert.NotNull(history);
        Assert.Equal("Created", history.Action);
        Assert.Equal("TestUser", history.ChangedBy);
    }
}

public class AuthServiceTests
{
    private ApplicationDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsToken()
    {
        var db = GetInMemoryDbContext();
        var configDict = new Dictionary<string, string?>
        {
            { "JWT_SECRET", "SuperSecretTestKeyThatIsAtLeast32BytesLong!" },
            { "JWT_ISSUER", "TestIssuer" },
            { "JWT_AUDIENCE", "TestAudience" }
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var tokenGen = new JwtTokenGenerator(config);
        var passwordHasher = new DefaultPasswordHasher();
        var authService = new AuthService(db, tokenGen, passwordHasher);

        await authService.SeedDefaultUsersAsync();

        var response = await authService.LoginAsync(new LoginDto { Username = "admin", Password = "admin123" });

        Assert.NotNull(response);
        Assert.Equal("admin", response.Username);
        Assert.False(string.IsNullOrWhiteSpace(response.Token));
    }
}
