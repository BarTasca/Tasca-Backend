using BarTasca.Data.Interfaces;
using BarTasca.Models;
using Microsoft.EntityFrameworkCore;

namespace BarTasca.Data.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly ColaDbContext _db;
    public NotificationRepository(ColaDbContext db) => _db = db;

    public Task AddAsync(Notification notification, CancellationToken ct = default)
        => _db.Notifications.AddAsync(notification, ct).AsTask();

    public Task<bool> ExistsSentAsync(int ticketId, NotificationType type, CancellationToken ct = default)
        => _db.Notifications.AnyAsync(n =>
            n.TicketId == ticketId &&
            n.Type == type &&
            n.Status == NotificationStatus.Sent, ct);

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
