using BarTasca.Models;

namespace BarTasca.Data.Interfaces;

public interface ITicketRepository
{
    /// <summary>
    /// Gets a ticket by its ID.
    /// </summary>
    /// <param name="id">Ticket ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ticket if found; otherwise, null.</returns>
    Task<Ticket?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Gets the active ticket (Waiting/Notified) by customer phone.
    /// </summary>
    /// <param name="phone">Customer phone number.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The active ticket if found; otherwise, null.</returns>
    Task<Ticket?> GetActiveByPhoneAsync(string phone, CancellationToken ct = default);

    /// <summary>
    /// Gets the maximum position among tickets with Waiting status.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The maximum position or 0 if none exist.</returns>
    Task<int> GetMaxWaitingPositionAsync(CancellationToken ct = default);

    /// <summary>
    /// Counts tickets ahead of the specified ticket (Waiting/Notified and lower position).
    /// </summary>
    /// <param name="ticketId">Ticket ID to compare.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of tickets ahead.</returns>
    Task<int> CountAheadAsync(int ticketId, CancellationToken ct = default);

    /// <summary>
    /// Adds a new ticket.
    /// </summary>
    /// <param name="ticket">Ticket to add.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AddAsync(Ticket ticket, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing ticket.
    /// </summary>
    /// <param name="ticket">Ticket to update.</param>
    void Update(Ticket ticket);

    /// <summary>
    /// Saves changes to the data store.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of affected records.</returns>
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Lists tickets by their statuses.
    /// </summary>
    /// <param name="statuses">Array of ticket statuses.</param>
    /// <param name="take">Maximum number of tickets to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of tickets matching the statuses.</returns>
    Task<List<Ticket>> ListByStatusesAsync(TicketStatus[] statuses, int take = 100, CancellationToken ct = default);

    /// <summary>
    /// Lists active tickets (Waiting/Notified) behind a certain position.
    /// </summary>
    /// <param name="position"></param>
    /// <param name="take"></param>
    /// <param name="ct"></param>
    /// <returns>List of tickets.</returns>
    Task<List<Ticket>> ListActiveBehindAsync(int position, int take, CancellationToken ct = default);

    /// <summary>
    /// Gets a ticket by its public ID.
    /// </summary>
    /// <param name="publicId"></param>
    /// <param name="ct"></param>
    /// <returns>the ticket if found; otherwise, null.</returns>
    Task<Ticket?> GetByPublicIdAsync(string publicId, CancellationToken ct = default);

    /// <summary>
    /// Counts active tickets (Waiting/Notified).
    /// </summary>
    /// <param name="ct"></param>
    /// <returns>Number of active tickets.</returns>
    Task<int> CountActiveAsync(CancellationToken ct = default);

}
