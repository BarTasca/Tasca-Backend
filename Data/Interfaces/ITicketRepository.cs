using BarTasca.Models;

namespace BarTasca.Data.Interfaces;

public interface ITicketRepository
{
    Task<Ticket?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Devuelve el ticket activo (Waiting/Notified) por teléfono del cliente, si existe.
    /// </summary>
    Task<Ticket?> GetActiveByPhoneAsync(string phone, CancellationToken ct = default);

    /// <summary>
    /// Máxima Position entre tickets en estado Waiting. Si no hay, devuelve 0.
    /// </summary>
    Task<int> GetMaxWaitingPositionAsync(CancellationToken ct = default);

    /// <summary>
    /// Cuenta las mesas por delante del ticket indicado:
    /// COUNT(*) WHERE Status IN (Waiting, Notified) AND Position < @MyPosition.
    /// </summary>
    Task<int> CountAheadAsync(int ticketId, CancellationToken ct = default);

    Task AddAsync(Ticket ticket, CancellationToken ct = default);
    void Update(Ticket ticket);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
