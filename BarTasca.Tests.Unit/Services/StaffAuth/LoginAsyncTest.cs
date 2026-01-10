using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BarTasca.Data.Interfaces;
using BarTasca.DTOs.Auth;
using BarTasca.Models;
using BarTasca.Services.Options;
using BarTasca.Services.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using Xunit;

namespace BarTasca.Tests.Unit.Services.StaffAuth
{
    public class LoginAsyncTest
    {
        private static JwtOptions BuildJwtOptions() => new JwtOptions
        {
            Secret = "super-secret-key-super-secret-key-super-secret-key-12345",
            Issuer = "BarTasca",
            Audience = "BarTascaStaff",
            StaffExpiresHours = 24,
            CustomerExpiresHours = 2
        };

        // If user not found by email, returns null
        [Fact]
        public async Task LoginAsync_UserNotFound_ReturnsNull()
        {
            var repo = Substitute.For<IStaffUserRepository>();
            var jwt = Options.Create(BuildJwtOptions());
            var service = new StaffAuthService(repo, jwt);

            var dto = new LoginRequestDto { Email = "nope@bartasca.com", Password = "whatever" };
            var ct = new CancellationTokenSource().Token;

            repo.GetByEmailAsync(dto.Email, ct).Returns((StaffUser?)null);

            var result = await service.LoginAsync(dto, ct);

            result.Should().BeNull();
            await repo.Received(1).GetByEmailAsync(dto.Email, ct);
        }

        // If password is invalid, returns null
        [Fact]
        public async Task LoginAsync_PasswordInvalid_ReturnsNull()
        {
            var repo = Substitute.For<IStaffUserRepository>();
            var jwt = Options.Create(BuildJwtOptions());
            var service = new StaffAuthService(repo, jwt);

            var dto = new LoginRequestDto { Email = "staff@bartasca.com", Password = "wrong-password" };
            var ct = new CancellationTokenSource().Token;

            var storedHash = service.HashPassword("correct-password");

            repo.GetByEmailAsync(dto.Email, ct).Returns(new StaffUser
            {
                Id = 10,
                Email = dto.Email,
                PasswordHash = storedHash,
                Role = StaffRole.Admin
            });

            var result = await service.LoginAsync(dto, ct);

            result.Should().BeNull();
            await repo.Received(1).GetByEmailAsync(dto.Email, ct);
        }

        // If user exists and password ok, returns response with token, role, expires
        [Fact]
        public async Task LoginAsync_ValidCredentials_ReturnsTokenRoleAndExpires()
        {
            var repo = Substitute.For<IStaffUserRepository>();
            var opts = BuildJwtOptions();
            var jwt = Options.Create(opts);
            var service = new StaffAuthService(repo, jwt);

            var dto = new LoginRequestDto { Email = "staff@bartasca.com", Password = "correct-password" };
            var ct = new CancellationTokenSource().Token;

            var user = new StaffUser
            {
                Id = 42,
                Email = dto.Email,
                PasswordHash = service.HashPassword(dto.Password),
                Role = StaffRole.Admin
            };

            repo.GetByEmailAsync(dto.Email, ct).Returns(user);

            var before = DateTime.UtcNow;
            var result = await service.LoginAsync(dto, ct);
            var after = DateTime.UtcNow;

            result.Should().NotBeNull();
            result!.Role.Should().Be(user.Role.ToString());

            result.Token.Should().NotBeNullOrWhiteSpace();
            result.Token.Split('.').Should().HaveCount(3);

            var expectedMin = before.AddHours(opts.StaffExpiresHours).AddSeconds(-10);
            var expectedMax = after.AddHours(opts.StaffExpiresHours).AddSeconds(+10);
            result.ExpiresAtUtc.Should().BeOnOrAfter(expectedMin);
            result.ExpiresAtUtc.Should().BeOnOrBefore(expectedMax);

            await repo.Received(1).GetByEmailAsync(dto.Email, ct);
        }

        // Token validates and contains required claims (sub, email, role)
        [Fact]
        public async Task LoginAsync_ValidCredentials_TokenValidatesAndHasClaims()
        {
            var repo = Substitute.For<IStaffUserRepository>();
            var opts = BuildJwtOptions();
            var jwt = Options.Create(opts);
            var service = new StaffAuthService(repo, jwt);

            var dto = new LoginRequestDto { Email = "staff@bartasca.com", Password = "correct-password" };
            var ct = new CancellationTokenSource().Token;

            var user = new StaffUser
            {
                Id = 7,
                Email = dto.Email,
                PasswordHash = service.HashPassword(dto.Password),
                Role = StaffRole.Admin
            };

            repo.GetByEmailAsync(dto.Email, ct).Returns(user);

            var result = await service.LoginAsync(dto, ct);

            result.Should().NotBeNull();
            var tokenString = result!.Token;

            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = opts.Issuer,

                ValidateAudience = true,
                ValidAudience = opts.Audience,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opts.Secret)),

                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(tokenString, parameters, out _);

            principal.Claims.Should().Contain(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier && c.Value == user.Id.ToString());
            principal.Claims.Should().Contain(c => c.Type == System.Security.Claims.ClaimTypes.Email && c.Value == user.Email);
            principal.Claims.Should().Contain(c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == user.Role.ToString());
            principal.Claims.Should().Contain(c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == user.Role.ToString());
        }
    }
}
