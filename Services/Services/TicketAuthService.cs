using BarTasca.Data.Interfaces;
using BarTasca.Services.Options;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using BarTasca.Services.Interfaces;

namespace BarTasca.Services.Services
{
    public class TicketAuthService : ITicketAuthService
    {
        private readonly ITicketRepository _repo;
        private readonly JwtOptions _jwt;

        public TicketAuthService(ITicketRepository repo, JwtOptions jwt) {
            _repo = repo;
            _jwt = jwt ?? throw new InvalidOperationException("Jwt options not set");
        }

        public async Task<string?> GenerateTokenAsync(string publicId, CancellationToken ct = default)
        {
            var ticket = await _repo.GetByPublicIdAsync(publicId.ToString(), ct);
            if (ticket is null) throw new InvalidOperationException("Ticket not found");

            var expires = DateTime.UtcNow.AddHours(_jwt.CustomerExpiresHours);

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim("ticket_public_id", publicId),
                new Claim("scope", "ticket:read")
            };

            var token = new JwtSecurityToken(
                issuer: _jwt.Issuer,
                audience: _jwt.Audience,
                claims: claims,
                expires: expires,
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
