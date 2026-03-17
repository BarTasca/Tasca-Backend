using System.Security.Cryptography;
using System.Text;
using BarTasca.Data.Interfaces;
using BarTasca.Models;
using Microsoft.EntityFrameworkCore;

namespace BarTasca.Data.Repositories;

public class PushSubscriptionRepository : IPushSubscriptionRepository
{
    private readonly ColaDbContext _db;

    public PushSubscriptionRepository(ColaDbContext db) => _db = db;

    public Task<PushSubscription?> GetByEndpointHashAsync(byte[] endpointHash, CancellationToken ct = default)
        => _db.PushSubscriptions
            .FirstOrDefaultAsync(ps => ps.EndpointHash == endpointHash, ct);

    public async Task<IReadOnlyList<PushSubscription>> ListActiveByTicketIdAsync(int ticketId, CancellationToken ct = default)
        => await _db.PushSubscriptions
            .AsNoTracking()
            .Where(ps => ps.TicketId == ticketId && ps.IsActive)
            .OrderByDescending(ps => ps.UpdatedAt)
            .ToListAsync(ct);

    public async Task<UpsertPushSubscriptionResult> UpsertAsync(
        int ticketId,
        string endpoint,
        string p256dh,
        string auth,
        CancellationToken ct = default)
    {
        var endpointHash = ComputeEndpointHash(endpoint);

        var existing = await _db.PushSubscriptions
            .FirstOrDefaultAsync(ps => ps.EndpointHash == endpointHash, ct);

        if (existing is null)
        {
            _db.PushSubscriptions.Add(new PushSubscription
            {
                TicketId = ticketId,
                Endpoint = endpoint,
                EndpointHash = endpointHash,
                P256dh = p256dh,
                Auth = auth,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            return UpsertPushSubscriptionResult.Created;
        }

        existing.TicketId = ticketId;
        existing.Endpoint = endpoint;
        existing.P256dh = p256dh;
        existing.Auth = auth;
        existing.IsActive = true;
        existing.UpdatedAt = DateTime.UtcNow;

        _db.PushSubscriptions.Update(existing);
        return UpsertPushSubscriptionResult.Updated;
    }

    public async Task<bool> DeactivateByEndpointAsync(string endpoint, CancellationToken ct = default)
    {
        var endpointHash = ComputeEndpointHash(endpoint);

        var existing = await _db.PushSubscriptions
            .FirstOrDefaultAsync(ps => ps.EndpointHash == endpointHash, ct);

        if (existing is null) return false;

        if (existing.IsActive)
        {
            existing.IsActive = false;
            existing.UpdatedAt = DateTime.UtcNow;
            _db.PushSubscriptions.Update(existing);
        }

        return true;
    }

    public async Task<int> DeactivateByTicketIdAsync(int ticketId, CancellationToken ct = default)
    {
        var list = await _db.PushSubscriptions
            .Where(ps => ps.TicketId == ticketId && ps.IsActive)
            .ToListAsync(ct);

        if (list.Count == 0) return 0;

        var now = DateTime.UtcNow;
        foreach (var ps in list)
        {
            ps.IsActive = false;
            ps.UpdatedAt = now;
        }

        return list.Count;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);

    private static byte[] ComputeEndpointHash(string endpoint)
    {
        var bytes = Encoding.UTF8.GetBytes(endpoint);
        return SHA256.HashData(bytes);
    }

    public async Task<bool> DeactivateByEndpointHashAsync(byte[] endpointHash, CancellationToken ct = default)
    {
        var existing = await _db.PushSubscriptions
            .FirstOrDefaultAsync(ps => ps.EndpointHash == endpointHash, ct);

        if (existing is null) return false;

        if (existing.IsActive)
        {
            existing.IsActive = false;
            existing.UpdatedAt = DateTime.UtcNow;
            _db.PushSubscriptions.Update(existing);
        }

        return true;
    }
}