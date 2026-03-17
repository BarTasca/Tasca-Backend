using System.Security.Claims;
using BarTasca.DTOs.Push;
using BarTasca.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BarTascaBackend.Controllers;

[ApiController]
[Route("api/tickets/{publicId}/push-subscriptions")]
public class TicketPushSubscriptionsController : ControllerBase
{
    private readonly IPushSubscriptionService _push;

    public TicketPushSubscriptionsController(IPushSubscriptionService push)
    {
        _push = push;
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<RegisterPushSubscriptionResponse>> Register(
        [FromRoute] string publicId,
        [FromBody] RegisterPushSubscriptionRequest request,
        CancellationToken ct)
    {
        if (User?.Identity?.IsAuthenticated != true) return Unauthorized();

        var isStaff = User.IsInRole("Admin") || User.IsInRole("Worker");
        if (!isStaff)
        {
            var claimPublicId = User.FindFirstValue("ticket_public_id");
            if (string.IsNullOrWhiteSpace(claimPublicId) || !string.Equals(claimPublicId, publicId, StringComparison.Ordinal))
                return Forbid();
        }

        try
        {
            var result = await _push.RegisterAsync(publicId, request, ct);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete]
    [Authorize]
    public async Task<IActionResult> Unregister(
        [FromRoute] string publicId,
        [FromBody] UnregisterPushSubscriptionRequest request,
        CancellationToken ct)
    {
        if (User?.Identity?.IsAuthenticated != true) return Unauthorized();

        var isStaff = User.IsInRole("Admin") || User.IsInRole("Worker");
        if (!isStaff)
        {
            var claimPublicId = User.FindFirstValue("ticket_public_id");
            if (string.IsNullOrWhiteSpace(claimPublicId) || !string.Equals(claimPublicId, publicId, StringComparison.Ordinal))
                return Forbid();
        }

        try
        {
            await _push.UnregisterAsync(publicId, request, ct);
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}