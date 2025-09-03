using System.Security.Claims;
using System.Text;
using BarTasca.Data.Interfaces;
using BarTasca.DTOs.Auth;
using BarTasca.Models;
using BarTasca.Services.Interfaces;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;
using BarTasca.Services.Options;

namespace BarTasca.Services.Services
{
    public class StaffAuthService : IStaffAuthService
    {
        private readonly IStaffUserRepository _repo;
        private readonly JwtOptions _jwt;

        public StaffAuthService(IStaffUserRepository repo, IOptions<JwtOptions> jwt)
        {
            _repo = repo;
            _jwt = jwt?.Value ?? throw new InvalidOperationException("Jwt options not set");
        }

        public async Task<LoginResponseDto?> LoginAsync(LoginRequestDto dto, CancellationToken ct = default)
        {
            var user = await _repo.GetByEmailAsync(dto.Email, ct);
            if (user is null) return null;

            if (!VerifyPassword(dto.Password, user.PasswordHash)) return null;

            var expires = DateTime.UtcNow.AddHours(_jwt.ExpiresHours);
            var token = CreateJwt(user, expires);

            return new LoginResponseDto
            {
                Token = token,
                Role = user.Role.ToString(),
                ExpiresAtUtc = expires
            };
        }

        public string HashPassword(string password)
            => BCrypt.Net.BCrypt.HashPassword(password);

        public bool VerifyPassword(string password, string storedHashBase64)
            => BCrypt.Net.BCrypt.Verify(password, storedHashBase64);

        private string CreateJwt(StaffUser user, DateTime expiresUtc)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _jwt.Issuer,
                audience: _jwt.Audience,
                claims: claims,
                expires: expiresUtc,
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
