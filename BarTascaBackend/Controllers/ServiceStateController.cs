using BarTasca.DTOs.ServiceState;
using BarTasca.Services.Interfaces;
using BarTasca.Services.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BarTascaBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ServiceStateController : ControllerBase
{
    private readonly IServiceStateService _service;
    public ServiceStateController(IServiceStateService service)
    {
        _service = service;
    }
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ServiceStateDto>> Get(CancellationToken ct)
    {
        var state = await _service.GetAsync(ct);
        return Ok(state);
    }

    [HttpPut]
    [Authorize(Roles = "Admin,Worker")]
    public async Task<IActionResult> SetOpen([FromBody] UpdateServiceStateDto dto, CancellationToken ct)
    {
        var changed = await _service.SetOpenAsync(dto, ct);
        return Ok(new
        {
            changed,
            dto.IsOpen
        });
    }
}
