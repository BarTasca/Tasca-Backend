using System.Security.Claims;
using System.Text;
using BarTasca.Data.Interfaces;
using BarTasca.DTOs.Auth;
using BarTasca.Models;
using BarTasca.Services.Interfaces;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace BarTasca.Services.Services
{
    public class StaffAuthService : IStaffAuthService
    {
        private readonly IStaffUserRepository _repo;
        private readonly string _jwtSecret;
        private readonly string _jwtIssuer;
        private readonly string _jwtAudience;

        public StaffAuthService(IStaffUserRepository repo, string jwtSecret, string jwtIssuer, string jwtAudience)
        {
            _repo = repo;
            _jwtSecret = jwtSecret ?? throw new InvalidOperationException("JWT_SECRET not set");
            _jwtIssuer = jwtIssuer ?? "BarTasca";
            _jwtAudience = jwtAudience ?? "BarTasca.Client";
        }

        public async Task<LoginResponseDto?> LoginAsync(LoginRequestDto dto, CancellationToken ct = default)
        {
            var user = await _repo.GetByEmailAsync(dto.Email, ct);
            if (user is null) return null;

            if (!VerifyPassword(dto.Password, user.PasswordHash)) return null;

            var expires = DateTime.UtcNow.AddHours(24);
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
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _jwtIssuer,
                audience: _jwtAudience,
                claims: claims,
                expires: expiresUtc,
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
