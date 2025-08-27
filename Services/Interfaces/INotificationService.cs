using BarTasca.Models;

namespace BarTasca.Services.Interfaces;

public interface INotificationService
{
    Task NotifyTicketCreatedAsync(Ticket ticket, int ahead, CancellationToken ct = default);
    Task NotifyTicketUpdatedAsync(Ticket ticket, int ahead, CancellationToken ct = default);
}
