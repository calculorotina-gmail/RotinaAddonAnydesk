using AnyDeskMonitor.Api.Hubs;
using AnyDeskMonitor.Application.Interfaces;
using AnyDeskMonitor.Shared.Constants;
using AnyDeskMonitor.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace AnyDeskMonitor.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var result = await _authService.LoginAsync(dto);
        if (result == null)
        {
            return Unauthorized(new { Message = "Credenciais inválidas." });
        }
        return Ok(result);
    }
}

[ApiController]
[Route("api/[controller]")]
public class AgentsController : ControllerBase
{
    private readonly IAgentService _agentService;
    private readonly IHubContext<AgentHub> _hubContext;

    public AgentsController(IAgentService agentService, IHubContext<AgentHub> hubContext)
    {
        _agentService = agentService;
        _hubContext = hubContext;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] AgentRegistrationDto dto)
    {
        var result = await _agentService.RegisterAgentAsync(dto);
        await _hubContext.Clients.All.SendAsync(SignalRConstants.Events.AgentUpdated, result);
        await _hubContext.Clients.All.SendAsync(SignalRConstants.Events.DashboardUpdated);
        return Ok(result);
    }

    [HttpPost("heartbeat")]
    [AllowAnonymous]
    public async Task<IActionResult> Heartbeat([FromBody] HeartbeatDto dto)
    {
        var result = await _agentService.ProcessHeartbeatAsync(dto);
        if (result == null)
        {
            return NotFound(new { Message = "Agente não encontrado." });
        }
        await _hubContext.Clients.All.SendAsync(SignalRConstants.Events.AgentUpdated, result);
        await _hubContext.Clients.All.SendAsync(SignalRConstants.Events.DashboardUpdated);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var agents = await _agentService.GetAllAgentsAsync();
        return Ok(agents);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var agent = await _agentService.GetAgentByIdAsync(id);
        if (agent == null) return NotFound();
        return Ok(agent);
    }

    [HttpPut("{id:guid}/enable")]
    public async Task<IActionResult> Enable(Guid id)
    {
        var success = await _agentService.EnableAgentAsync(id);
        if (!success) return NotFound();
        await _hubContext.Clients.All.SendAsync(SignalRConstants.Events.DashboardUpdated);
        return Ok(new { Message = "Agente ativado com sucesso." });
    }

    [HttpPut("{id:guid}/disable")]
    public async Task<IActionResult> Disable(Guid id)
    {
        var success = await _agentService.DisableAgentAsync(id);
        if (!success) return NotFound();
        await _hubContext.Clients.All.SendAsync(SignalRConstants.Events.DashboardUpdated);
        return Ok(new { Message = "Agente desativado com sucesso." });
    }

    [HttpPost("{id:guid}/sync")]
    public async Task<IActionResult> Sync(Guid id)
    {
        var success = await _agentService.SyncAgentAsync(id);
        if (!success) return NotFound();
        await _hubContext.Clients.All.SendAsync(SignalRConstants.Events.DashboardUpdated);
        return Ok(new { Message = "Sincronização iniciada." });
    }
}
