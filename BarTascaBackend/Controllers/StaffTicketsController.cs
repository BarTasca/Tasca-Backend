using BarTasca.DTOs.Ticket;
using BarTasca.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BarTascaBackend.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Worker")]
[Route("api/staff/tickets")]
public class StaffTicketsController : ControllerBase
{
    private readonly ITicketService _service;

    public StaffTicketsController(ITicketService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TicketStaffListDto>>> List([FromQuery] string status = "active", [FromQuery] int take = 20, CancellationToken ct = default)
    {
        var list = await _service.ListForStaffAsync(status, take, ct);
        return Ok(list);
    }

    [HttpPost("{id:int}/serve")]
    public async Task<ActionResult<TicketDetailDto>> Serve(int id, CancellationToken ct)
    {
        try
        {
            var result = await _service.ServeAsync(id, ct);
            if (result is null) return NotFound();
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("{id:int}/skip")]
    public async Task<ActionResult<TicketDetailDto>> Skip(int id, CancellationToken ct)
    {
        try
        {
            var result = await _service.SkipAsync(id, ct);
            if (result is null) return NotFound();
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<TicketDetailDto>> Cancel(int id, CancellationToken ct)
    {
        try
        {
            var result = await _service.CancelAsync(id, ct);
            if (result is null) return NotFound();
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("{id:int}/notify")]
    public async Task<ActionResult<TicketDetailDto>> Notify(int id, [FromQuery] bool force = true, CancellationToken ct = default)
    {
        try
        {
            var result = await _service.NotifyAsync(id, force, ct);
            if (result is null) return NotFound();
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TicketDetailDto>> UpdateTicket(int id, [FromBody] UpdateTicketDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        try
        {
            var result = await _service.UpdateAsync(id, dto, ct);
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
