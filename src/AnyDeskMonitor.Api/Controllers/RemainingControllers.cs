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
public class ComputersController : ControllerBase
{
    private readonly IComputerService _computerService;
    private readonly IHubContext<AgentHub> _hubContext;

    public ComputersController(IComputerService computerService, IHubContext<AgentHub> hubContext)
    {
        _computerService = computerService;
        _hubContext = hubContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] string? status, [FromQuery] string? os)
    {
        var computers = await _computerService.GetAllComputersAsync(search, status, os);
        return Ok(computers);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var computer = await _computerService.GetComputerByIdAsync(id);
        if (computer == null) return NotFound();
        return Ok(computer);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ComputerDto dto)
    {
        var updated = await _computerService.UpdateComputerAsync(id, dto);
        if (updated == null) return NotFound();
        await _hubContext.Clients.All.SendAsync(SignalRConstants.Events.ComputerUpdated, updated);
        await _hubContext.Clients.All.SendAsync(SignalRConstants.Events.DashboardUpdated);
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _computerService.DeleteComputerAsync(id);
        if (!deleted) return NotFound();
        await _hubContext.Clients.All.SendAsync(SignalRConstants.Events.DashboardUpdated);
        return Ok(new { Message = "Computador removido com sucesso." });
    }

    [HttpPost("{id:guid}/sync")]
    public async Task<IActionResult> Sync(Guid id)
    {
        var success = await _computerService.SyncComputerAsync(id);
        if (!success) return NotFound();
        await _hubContext.Clients.All.SendAsync(SignalRConstants.Events.ComputerUpdated, await _computerService.GetComputerByIdAsync(id));
        await _hubContext.Clients.All.SendAsync(SignalRConstants.Events.DashboardUpdated);
        return Ok(new { Message = "Sincronização concluída com sucesso." });
    }

    [HttpGet("{id:guid}/events")]
    public async Task<IActionResult> GetEvents(Guid id)
    {
        var events = await _computerService.GetEventsByComputerIdAsync(id);
        return Ok(events);
    }

    [HttpGet("{id:guid}/remoteids")]
    public async Task<IActionResult> GetRemoteIds(Guid id)
    {
        var remoteIds = await _computerService.GetRemoteIdsByComputerIdAsync(id);
        return Ok(remoteIds);
    }
}

[ApiController]
[Route("api/[controller]")]
public class RemoteIdsController : ControllerBase
{
    private readonly IRemoteIdService _remoteIdService;

    public RemoteIdsController(IRemoteIdService remoteIdService)
    {
        _remoteIdService = remoteIdService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var list = await _remoteIdService.GetAllRemoteIdsAsync();
        return Ok(list);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRemoteAnyDeskIdDto dto)
    {
        var user = User.Identity?.Name ?? "Admin";
        var created = await _remoteIdService.CreateRemoteIdAsync(dto, user);
        return CreatedAtAction(nameof(GetAll), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] RemoteAnyDeskIdDto dto)
    {
        var user = User.Identity?.Name ?? "Admin";
        var updated = await _remoteIdService.UpdateRemoteIdAsync(id, dto, user);
        if (updated == null) return NotFound();
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var user = User.Identity?.Name ?? "Admin";
        var success = await _remoteIdService.DeleteRemoteIdAsync(id, user);
        if (!success) return NotFound();
        return Ok(new { Message = "ID Remoto AnyDesk removido." });
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] Guid? remoteId)
    {
        var history = await _remoteIdService.GetHistoryAsync(remoteId);
        return Ok(history);
    }
}

[ApiController]
[Route("api/[controller]")]
public class SessionsController : ControllerBase
{
    private readonly ISessionService _sessionService;

    public SessionsController(ISessionService sessionService)
    {
        _sessionService = sessionService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var list = await _sessionService.GetAllSessionsAsync();
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var session = await _sessionService.GetSessionByIdAsync(id);
        if (session == null) return NotFound();
        return Ok(session);
    }

    [HttpPost]
    public async Task<IActionResult> RecordSession([FromBody] RecordSessionRequest request)
    {
        await _sessionService.RecordSessionAsync(request.ComputerId, request.AgentId, request.RemoteAnyDeskId, request.LocalAnyDeskId, User.Identity?.Name);
        return Ok(new { Message = "Sessão registada com sucesso." });
    }

    [HttpPost("{id:guid}/taskkill")]
    [HttpPost("{id:guid}/kill")]
    public async Task<IActionResult> KillSession(Guid id)
    {
        var user = User.Identity?.Name ?? "Admin";
        var result = await _sessionService.KillSessionAsync(id, user);
        return Ok(new { Message = "Taskkill executado com sucesso na sessão.", Success = result });
    }
}

public class RecordSessionRequest
{
    public Guid ComputerId { get; set; }
    public Guid? AgentId { get; set; }
    public string RemoteAnyDeskId { get; set; } = string.Empty;
    public string? LocalAnyDeskId { get; set; }
    public string? Location { get; set; }
}

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet]
    [HttpGet("statistics")]
    public async Task<IActionResult> GetStats()
    {
        var stats = await _dashboardService.GetStatsAsync();
        return Ok(stats);
    }
}

[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private readonly IEventService _eventService;

    public EventsController(IEventService eventService)
    {
        _eventService = eventService;
    }

    [HttpGet]
    public async Task<IActionResult> GetEvents([FromQuery] string? type, [FromQuery] Guid? computerId, [FromQuery] int limit = 100)
    {
        var events = await _eventService.GetEventsAsync(type, computerId, limit);
        return Ok(events);
    }
}

[ApiController]
[Route("api/[controller]")]
public class AuditController : ControllerBase
{
    private readonly IAuditService _auditService;

    public AuditController(IAuditService auditService)
    {
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAuditLogs([FromQuery] int limit = 100)
    {
        var logs = await _auditService.GetAuditLogsAsync(limit);
        return Ok(logs);
    }
}
