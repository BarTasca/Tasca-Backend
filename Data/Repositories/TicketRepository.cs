using BarTasca.Data.Interfaces;
using BarTasca.Models;
using Microsoft.EntityFrameworkCore;

namespace BarTasca.Data.Repositories;

public class TicketRepository : ITicketRepository
{
    private readonly ColaDbContext _db;

    public TicketRepository(ColaDbContext db) => _db = db;

    public Task<Ticket?> GetByIdAsync(int id, CancellationToken ct = default)
        => _db.Tickets
              .Include(t => t.Customer)
              .FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<Ticket?> GetActiveByPhoneAsync(string phone, CancellationToken ct = default)
        => _db.Tickets
              .Include(t => t.Customer)
              .Where(t => t.Customer.Phone == phone &&
                          (t.Status == TicketStatus.Waiting || t.Status == TicketStatus.Notified))
              .OrderBy(t => t.Position)
              .FirstOrDefaultAsync(ct);

    public async Task<int> GetMaxWaitingPositionAsync(CancellationToken ct = default)
    {
        var max = await _db.Tickets
                           .Where(t => t.Status == TicketStatus.Waiting || t.Status == TicketStatus.Notified)
                           .Select(t => (int?)t.Position)
                           .MaxAsync(ct);
        return max ?? 0;
    }

    public async Task<int> CountAheadAsync(int ticketId, CancellationToken ct = default)
    {
        var ticket = await _db.Tickets.AsNoTracking()
                          .FirstOrDefaultAsync(t => t.Id == ticketId, ct);

        if (ticket is null) return 0;

        return await _db.Tickets.AsNoTracking()
            .Where(t =>
                (t.Status == TicketStatus.Waiting || t.Status == TicketStatus.Notified) &&
                t.Position < ticket.Position)
            .CountAsync(ct);
    }

    public Task<List<Ticket>> ListActiveOrderedAsync(CancellationToken ct = default)
    => _db.Tickets
        .AsNoTracking()
        .Where(t => t.Status == TicketStatus.Waiting || t.Status == TicketStatus.Notified)
        .OrderBy(t => t.Position)
        .ToListAsync(ct);

    public Task AddAsync(Ticket ticket, CancellationToken ct = default)
        => _db.Tickets.AddAsync(ticket, ct).AsTask();

    public void Update(Ticket ticket) => _db.Tickets.Update(ticket);

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);

    public Task<List<Ticket>> ListByStatusesAsync(TicketStatus[] statuses, int take, CancellationToken ct = default)
        => _db.Tickets
              .AsNoTracking()
              .Include(t => t.Customer)
              .Where(t => statuses.Contains(t.Status))
              .OrderBy(t => t.Position)
              .Take(take)
              .ToListAsync(ct);

    public Task<List<Ticket>> ListActiveBehindAsync(int position, int take, CancellationToken ct = default)
        => _db.Tickets
            .AsNoTracking()
            .Include(t => t.Customer)
            .Where(t =>
                (t.Status == TicketStatus.Waiting || t.Status == TicketStatus.Notified) &&
                t.Position > position)
            .OrderBy(t => t.Position)
            .Take(take)
            .ToListAsync(ct);

    public Task<Ticket?> GetByPublicIdAsync(string publicId, CancellationToken ct = default)
        => _db.Tickets
              .AsNoTracking()
              .Include(t => t.Customer)
              .FirstOrDefaultAsync(t => t.PublicId == publicId, ct);

    public Task<int> CountActiveAsync(CancellationToken ct = default)
        => _db.Tickets
            .AsNoTracking()
            .Where(t => t.Status == TicketStatus.Waiting || t.Status == TicketStatus.Notified)
            .CountAsync(ct);

}
