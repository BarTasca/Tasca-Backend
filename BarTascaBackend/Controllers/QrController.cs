using BarTasca.DTOs.Qr;
using BarTasca.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BarTascaBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class QrController : ControllerBase
{
    private readonly IQrTokenService _qr;

    public QrController(IQrTokenService qr)
    {
        _qr = qr;
    }

    [HttpGet("current")]
    public ActionResult<QrTokenDto> GetCurrent()
    {
        var dto = _qr.GetCurrentToken();
        return Ok(dto);
    }

    [HttpGet("validate")]
    public ActionResult<object> Validate([FromQuery] string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(new { code = "QR_TOKEN_REQUIRED" });

        var result = _qr.Validate(token);

        if (!result.IsValid)
            return StatusCode(410, new { code = "QR_EXPIRED" });

        return Ok(new { valid = true });
    }
}
