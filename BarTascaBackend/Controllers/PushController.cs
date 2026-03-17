using BarTasca.DTOs.Push;
using BarTasca.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BarTascaBackend.Controllers;

[ApiController]
[Route("api/push")]
public class PushController : ControllerBase
{
    private readonly IPushSubscriptionService _push;

    public PushController(IPushSubscriptionService push)
    {
        _push = push;
    }

    [HttpGet("vapid-public-key")]
    [AllowAnonymous]
    public async Task<ActionResult<VapidPublicKeyResponse>> GetVapidPublicKey(CancellationToken ct)
    {
        try
        {
            var dto = await _push.GetVapidPublicKeyAsync(ct);
            return Ok(dto);
        }
        catch (InvalidOperationException)
        {
            return NotFound(new { code = "WEBPUSH_NOT_CONFIGURED" });
        }
    }
}