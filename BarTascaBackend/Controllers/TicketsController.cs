using BarTasca.DTOs.Ticket;
using BarTasca.DTOs.Queue;
using BarTasca.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using BarTasca.Services.Exceptions;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace BarTascaBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _service;
    private readonly ITicketAuthService _ticketAuth;
    private readonly IQrTokenService _qr;


    public TicketsController(ITicketService service, ITicketAuthService authService, IQrTokenService qr)
    {
        _service = service;
        _ticketAuth = authService;
        _qr = qr;
    }

    [HttpPost]
    public async Task<ActionResult<TicketDetailDto>> Create([FromBody] CreateTicketDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var qr = _qr.Validate(dto.QrToken);
        if (!qr.IsValid)
        {
            return StatusCode(410, new { code = "QR_EXPIRED" });
        }


        try
        {
            var created = await _service.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch(ServiceClosedException)
        {
            return Conflict(new { code = "SERVICE_CLOSED" });
        }
        catch (DuplicateActiveTicketNameException ex)
        {
            return Conflict(new { code = "DUPLICATE_NAME", error = ex.Message });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TicketDetailDto>> GetById(int id, CancellationToken ct)
    {
        var result = await _service.GetAsync(id, ct);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpGet("{publicId}/status")]
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

    [HttpPost("{publicId}/cancel")]
    [Authorize]
    public async Task<ActionResult<TicketDetailDto>> CancelByClient(string publicId, CancellationToken ct)
    {
        var claimPublicId = User.FindFirstValue("ticket_public_id");
        if (string.IsNullOrWhiteSpace(claimPublicId))
            return Forbid();

        if (!string.Equals(claimPublicId, publicId, StringComparison.Ordinal))
            return Forbid();

        try
        {
            var result = await _service.CancelByPublicIdAsync(publicId, ct);
            if (result is null) return NotFound();
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpGet("ahead")]
    [AllowAnonymous]
    public async Task<ActionResult<QueueAheadDto>> GetAhead(CancellationToken ct)
    {
        try
        {
            QueueAheadDto dto = await _service.GetAheadAsync(ct);
            return Ok(dto);
        }
        catch (ServiceClosedException)
        {
            return Conflict(new { code = "SERVICE_CLOSED" });
        }
    }

    [HttpPut("{publicId}/people-count")]
    [Authorize]
    public async Task<ActionResult<TicketDetailDto>> UpdateTicket(
    string publicId,
    [FromBody] UpdateTicketDto dto,
    CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var isStaff = User.IsInRole("Admin") || User.IsInRole("Worker");

        if (!isStaff)
        {
            var claim = User.FindFirstValue("ticket_public_id");

            if (string.IsNullOrWhiteSpace(claim))
                return Forbid();

            if (!string.Equals(claim, publicId, StringComparison.Ordinal))
                return Forbid();
        }

        try
        {
            var result = await _service.UpdateByPublicIdAsync(publicId, dto, ct);
            if (result is null) return NotFound();
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }


}
