using BarTasca.Models;

namespace BarTasca.Data.Interfaces;

public interface INotificationRepository
{
    Task AddAsync(Notification notification, CancellationToken ct = default);
    Task<bool> ExistsSentAsync(int ticketId, NotificationType type, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
