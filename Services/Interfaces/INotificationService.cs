using BarTasca.Models;

namespace BarTasca.Services.Interfaces
{
    public interface INotificationService
    {
        Task NotifyTicketCreatedAsync(Ticket ticket, int ahead, CancellationToken ct = default);
        Task NotifyTicketUpdatedAsync(Ticket ticket, int ahead, NotificationType type, CancellationToken ct = default);
        Task BroadcastTicketUpdatedAsync(Ticket ticket, int ahead, CancellationToken ct = default);

    }
}
