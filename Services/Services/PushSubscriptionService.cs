using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BarTasca.Data.Interfaces;
using BarTasca.DTOs.Push;
using BarTasca.Services.Interfaces;
using BarTasca.Services.Options;
using Microsoft.Extensions.Options;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace BarTasca.Services.Services;

public sealed class PushSubscriptionService : IPushSubscriptionService
{
    private readonly ITicketRepository _tickets;
    private readonly IPushSubscriptionRepository _subs;
    private readonly WebPushOptions _opts;
    private readonly JwtOptions _jwt;

    public PushSubscriptionService(
        ITicketRepository tickets,
        IPushSubscriptionRepository subs,
        IOptions<WebPushOptions> opts,
        IOptions<JwtOptions> jwt)
    {
        _tickets = tickets;
        _subs = subs;
        _opts = opts?.Value ?? throw new InvalidOperationException("WebPush options not set");
        _jwt = jwt?.Value ?? throw new InvalidOperationException("Jwt options not set");
    }

    public Task<VapidPublicKeyResponse> GetVapidPublicKeyAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opts.VapidPublicKey))
            throw new InvalidOperationException("WebPush VAPID public key not set");

        return Task.FromResult(new VapidPublicKeyResponse
        {
            PublicKey = _opts.VapidPublicKey!
        });
    }

    public async Task<RegisterPushSubscriptionResponse> RegisterAsync(
        string publicId,
        RegisterPushSubscriptionRequest request,
        CancellationToken ct = default)
    {
        ValidateTicketTokenMatchesPublicId(request.TicketToken, publicId);

        var ticket = await _tickets.GetByPublicIdAsync(publicId, ct);
        if (ticket is null) throw new KeyNotFoundException("Ticket not found");

        var sub = request.Subscription;
        var keys = sub.Keys;

        var result = await _subs.UpsertAsync(
            ticket.Id,
            sub.Endpoint,
            keys.P256dh,
            keys.Auth,
            ct);

        await _subs.SaveChangesAsync(ct);

        return result switch
        {
            UpsertPushSubscriptionResult.Created => new RegisterPushSubscriptionResponse
            {
                Created = true,
                Updated = false
            },
            _ => new RegisterPushSubscriptionResponse
            {
                Created = false,
                Updated = true
            }
        };
    }

    public async Task UnregisterAsync(string publicId, UnregisterPushSubscriptionRequest request, CancellationToken ct = default)
    {
        ValidateTicketTokenMatchesPublicId(request.TicketToken, publicId);

        var ok = await _subs.DeactivateByEndpointAsync(request.Endpoint, ct);
        if (ok) await _subs.SaveChangesAsync(ct);
    }

    public async Task<int> DeactivateByTicketIdAsync(int ticketId, CancellationToken ct = default)
    {
        var count = await _subs.DeactivateByTicketIdAsync(ticketId, ct);
        if (count > 0) await _subs.SaveChangesAsync(ct);
        return count;
    }

    private void ValidateTicketTokenMatchesPublicId(string token, string publicId)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new UnauthorizedAccessException("Missing ticket token");

        var handler = new JwtSecurityTokenHandler();

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _jwt.Issuer,
            ValidAudience = _jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret)),
            ClockSkew = TimeSpan.Zero
        };

        ClaimsPrincipal principal;
        try
        {
            principal = handler.ValidateToken(token, parameters, out _);
        }
        catch
        {
            throw new UnauthorizedAccessException("Invalid ticket token");
        }

        var claim = principal.FindFirst("ticket_public_id")?.Value;
        if (!string.Equals(claim, publicId, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Ticket token does not match ticket");
    }
}