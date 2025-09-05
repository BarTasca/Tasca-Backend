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

    public Task<bool> ExistsSentAsync(int ticketId, NotificationType type, NotificationChannel channel, CancellationToken ct = default)
    => _db.Notifications.AnyAsync(n =>
        n.TicketId == ticketId &&
        n.Type == type &&
        n.Channel == channel &&
        n.Status == NotificationStatus.Sent, ct);

    public Task<bool> WasSentRecentlyAsync(int ticketId, NotificationType type, NotificationChannel channel, TimeSpan window, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - window;
        return _db.Notifications.AnyAsync(n =>
            n.TicketId == ticketId &&
            n.Type == type &&
            n.Channel == channel &&
            n.Status == NotificationStatus.Sent &&
            n.SentAt >= cutoff, ct);
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
