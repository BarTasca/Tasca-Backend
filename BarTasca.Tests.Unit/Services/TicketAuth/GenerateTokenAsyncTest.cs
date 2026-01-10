using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BarTasca.Data.Interfaces;
using BarTasca.Models;
using BarTasca.Services.Options;
using BarTasca.Services.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using Xunit;

namespace BarTasca.Tests.Unit.Services.TicketAuth
{
    public class GenerateTokenAsyncTest
    {
        private static JwtOptions BuildJwtOptions() => new JwtOptions
        {
            Secret = "super-secret-key-super-secret-key-super-secret-key-12345",
            Issuer = "BarTasca",
            Audience = "BarTascaClients",
            StaffExpiresHours = 24,
            CustomerExpiresHours = 2
        };


        // If ticket not found by publicId, returns null and does not generate token
        [Fact]
        public async Task GenerateTokenAsync_TicketNotFound_ReturnsNull()
        {
            var repo = Substitute.For<ITicketRepository>();
            var jwt = Options.Create(BuildJwtOptions());
            var service = new TicketAuthService(repo, jwt);

            var publicId = "test-public-id";
            var ct = new CancellationTokenSource().Token;

            repo.GetByPublicIdAsync(publicId, ct).Returns((Ticket?)null);

            var result = await service.GenerateTokenAsync(publicId, ct);

            result.Should().BeNull();
            await repo.Received(1).GetByPublicIdAsync(publicId, ct);
        }

        // If ticket exists, returns a non-empty JWT string with 3 segments
        [Fact]
        public async Task GenerateTokenAsync_TicketExists_ReturnsJwtWith3Segments()
        {
            var repo = Substitute.For<ITicketRepository>();
            var jwt = Options.Create(BuildJwtOptions());
            var service = new TicketAuthService(repo, jwt);

            var publicId = "ticket_abc";
            repo.GetByPublicIdAsync(publicId, Arg.Any<CancellationToken>())
                .Returns(new Ticket { PublicId = publicId });

            var token = await service.GenerateTokenAsync(publicId);

            token.Should().NotBeNullOrWhiteSpace();
            token!.Split('.').Should().HaveCount(3);
        }

        // Token contains claim "ticket_public_id" equal to requested publicId
        [Fact]
        public async Task GenerateTokenAsync_IncludesTicketPublicIdClaim()
        {
            var repo = Substitute.For<ITicketRepository>();
            var jwt = Options.Create(BuildJwtOptions());
            var service = new TicketAuthService(repo, jwt);

            var publicId = "ticket_123";
            repo.GetByPublicIdAsync(publicId, Arg.Any<CancellationToken>())
                .Returns(new Ticket { PublicId = publicId });

            var tokenString = await service.GenerateTokenAsync(publicId);

            var token = new JwtSecurityTokenHandler().ReadJwtToken(tokenString);

            token.Claims.Should().Contain(c => c.Type == "ticket_public_id" && c.Value == publicId);
        }

        // Token contains claim "scope" equal to "ticket:read"
        [Fact]
        public async Task GenerateTokenAsync_IncludesScopeClaim()
        {
            var repo = Substitute.For<ITicketRepository>();
            var jwt = Options.Create(BuildJwtOptions());
            var service = new TicketAuthService(repo, jwt);

            var publicId = "ticket_123";
            repo.GetByPublicIdAsync(publicId, Arg.Any<CancellationToken>())
                .Returns(new Ticket { PublicId = publicId });

            var tokenString = await service.GenerateTokenAsync(publicId);

            var token = new JwtSecurityTokenHandler().ReadJwtToken(tokenString);

            token.Claims.Should().Contain(c => c.Type == "scope" && c.Value == "ticket:read");
        }

        // Token has issuer and audience matching JwtOptions
        [Fact]
        public async Task GenerateTokenAsync_SetsIssuerAndAudienceFromOptions()
        {
            var repo = Substitute.For<ITicketRepository>();
            var opts = BuildJwtOptions();
            var jwt = Options.Create(opts);
            var service = new TicketAuthService(repo, jwt);

            var publicId = "ticket_123";
            repo.GetByPublicIdAsync(publicId, Arg.Any<CancellationToken>())
                .Returns(new Ticket { PublicId = publicId });

            var tokenString = await service.GenerateTokenAsync(publicId);

            var token = new JwtSecurityTokenHandler().ReadJwtToken(tokenString);

            token.Issuer.Should().Be(opts.Issuer);
            token.Audiences.Should().ContainSingle(a => a == opts.Audience);
        }

        // Token expiration is in the future and roughly equals now + CustomerExpiresHours
        [Fact]
        public async Task GenerateTokenAsync_ExpirationIsRoughlyNowPlusCustomerExpiresHours()
        {
            var repo = Substitute.For<ITicketRepository>();
            var opts = BuildJwtOptions();
            var jwt = Options.Create(opts);
            var service = new TicketAuthService(repo, jwt);

            var publicId = "ticket_123";
            repo.GetByPublicIdAsync(publicId, Arg.Any<CancellationToken>())
                .Returns(new Ticket { PublicId = publicId });

            var before = DateTime.UtcNow;
            var tokenString = await service.GenerateTokenAsync(publicId);
            var after = DateTime.UtcNow;

            var token = new JwtSecurityTokenHandler().ReadJwtToken(tokenString);

            token.ValidTo.Should().BeAfter(after);

            var expectedMin = before.AddHours(opts.CustomerExpiresHours).AddSeconds(-10);
            var expectedMax = after.AddHours(opts.CustomerExpiresHours).AddSeconds(+10);

            token.ValidTo.Should().BeOnOrAfter(expectedMin);
            token.ValidTo.Should().BeOnOrBefore(expectedMax);
        }

        // Token validates successfully with TokenValidationParameters using the configured secret (signature, issuer, audience)
        [Fact]
        public async Task GenerateTokenAsync_TokenValidatesWithConfiguredParameters()
        {
            var repo = Substitute.For<ITicketRepository>();
            var opts = BuildJwtOptions();
            var jwt = Options.Create(opts);
            var service = new TicketAuthService(repo, jwt);

            var publicId = "ticket_123";
            repo.GetByPublicIdAsync(publicId, Arg.Any<CancellationToken>())
                .Returns(new Ticket { PublicId = publicId });

            var tokenString = await service.GenerateTokenAsync(publicId);

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

            var principal = handler.ValidateToken(tokenString, parameters, out var validatedToken);

            principal.Claims.Should().Contain(c => c.Type == "ticket_public_id" && c.Value == publicId);
            validatedToken.Should().BeOfType<JwtSecurityToken>();
        }

        // If JwtOptions are not set (IOptions.Value null), constructor throws InvalidOperationException
        [Fact]
        public void Constructor_WhenOptionsMissing_Throws()
        {
            var repo = Substitute.For<ITicketRepository>();

            Action act = () => new TicketAuthService(repo, null!);

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("Jwt options not set");
        }

        [Fact]
        public void Constructor_WhenOptionsValueIsNull_Throws()
        {
            var repo = Substitute.For<ITicketRepository>();
            var badOptions = Substitute.For<IOptions<JwtOptions>>();
            badOptions.Value.Returns((JwtOptions)null!);

            Action act = () => new TicketAuthService(repo, badOptions);

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("Jwt options not set");
        }

    }
}
