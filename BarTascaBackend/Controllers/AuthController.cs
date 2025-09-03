using BarTasca.DTOs.Auth;
using BarTasca.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BarTascaBackend.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IStaffAuthService _auth;
        public AuthController(IStaffAuthService auth) => _auth = auth;

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto dto, CancellationToken ct)
        {
            var res = await _auth.LoginAsync(dto, ct);
            if (res is null) return Unauthorized(new { error = "Invalid credentials" });
            return Ok(res);
        }

        [HttpGet("hash")]
        public IActionResult GetHash(string password)
        {
            var hash = BCrypt.Net.BCrypt.HashPassword(password);
            return Ok(hash);
        }
    }
}
