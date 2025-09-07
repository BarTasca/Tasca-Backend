using BarTasca.DTOs.Ticket;
using BarTasca.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BarTascaBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _service;
    private readonly ITicketAuthService _ticketAuth;

    public TicketsController(ITicketService service, ITicketAuthService authService)
    {
        _service = service;
        _ticketAuth = authService;
    }

    [HttpPost]
    public async Task<ActionResult<TicketDetailDto>> Create([FromBody] CreateTicketDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var created = await _service.CreateAsync(dto, ct);
        // 201 con Location al recurso
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TicketDetailDto>> GetById(int id, CancellationToken ct)
    {
        var result = await _service.GetAsync(id, ct);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpGet("{id:int}/status")]
    public async Task<ActionResult<TicketStatusDto>> GetStatus(string publicId, CancellationToken ct)
    {
        var user = HttpContext.User;
        if (user?.Identity?.IsAuthenticated != true) return Unauthorized();
        var dto = await _service.GetStatusAsync(publicId, ct);

        var isStaff = User.IsInRole("Admin") || User.IsInRole("Worker");
        if (isStaff)
        {
            if (dto is null) return NotFound();
            return Ok(dto);
        }

        var claim = User.FindFirst("ticket_public_id");
        if (claim is null || !string.Equals(claim.Value, publicId, StringComparison.Ordinal)) return Forbid();

        if (dto is null) return NotFound();
        return Ok(dto);
    }

    [HttpPost("{publicId}/token")]
    public async Task<ActionResult<object>> CreateTicketToken(string publicId, CancellationToken ct)
    {
        var token = await _ticketAuth.GenerateTokenAsync(publicId, ct);
        if (token is null) return NotFound();

        return Ok(new { token });
    }
}
