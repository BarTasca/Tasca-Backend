using BarTasca.Models;

namespace BarTasca.Data.Interfaces;

public interface INotificationRepository
{
    Task AddAsync(Notification notification, CancellationToken ct = default);
    Task<bool> ExistsSentAsync(int ticketId, NotificationType type, NotificationChannel channel, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<bool> WasSentRecentlyAsync(int ticketId, NotificationType type, NotificationChannel channel, TimeSpan window, CancellationToken ct = default);
}
