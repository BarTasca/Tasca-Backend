using BarTasca.DTOs.Queue;
using BarTasca.DTOs.Ticket;

namespace BarTasca.Services.Interfaces;

public interface ITicketService
{
    /// <summary>
    /// Creates a new ticket.
    /// </summary>
    /// <param name="dto">Ticket creation data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Details of the created ticket.</returns>
    Task<TicketDetailDto> CreateAsync(CreateTicketDto dto, CancellationToken ct = default);

    /// <summary>
    /// Gets details of a ticket by ID.
    /// </summary>
    /// <param name="id">Ticket ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Ticket details or null if not found.</returns>
    Task<TicketDetailDto?> GetAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Marks a ticket as served.
    /// </summary>
    /// <param name="id">Ticket ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Updated ticket details or null if not found.</returns>
    Task<TicketDetailDto?> ServeAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Skips a ticket.
    /// </summary>
    /// <param name="id">Ticket ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Updated ticket details or null if not found.</returns>
    Task<TicketDetailDto?> SkipAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Cancels a ticket.
    /// </summary>
    /// <param name="id">Ticket ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Updated ticket details or null if not found.</returns>
    Task<TicketDetailDto?> CancelAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Lists tickets for staff by status.
    /// </summary>
    /// <param name="status">
    /// Ticket status filter. "active" includes Waiting and Notified.
    /// </param>
    /// <param name="take">Maximum number of tickets to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of tickets for staff.</returns>
    Task<IReadOnlyList<TicketStaffListDto>> ListForStaffAsync(string status = "active", int take = 100, CancellationToken ct = default);

    /// <summary>
    /// Notifies the customer of a ticket, optionally forcing the notification.
    /// </summary>
    /// <param name="id">Ticket ID.</param>
    /// <param name="force">If true, forces the notification even if it has already been notified.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Updated ticket details or null if not found.</returns>
    Task<TicketDetailDto?> NotifyAsync(int id, bool force = false, CancellationToken ct = default);

    /// <summary>
    /// Gets the status of a ticket by PublicId.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<TicketStatusDto?> GetStatusAsync(string publicId, CancellationToken ct = default);

    /// <summary>
    /// Cancels a ticket by PublicId.
    /// </summary>
    /// <param name="publicId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<TicketDetailDto?> CancelByPublicIdAsync(string publicId, CancellationToken ct = default);

    /// <summary>
    /// Gets the queue ahead information.
    /// </summary>
    /// <param name="ct"></param>
    /// <returns>The queue ahead data.</returns>
    Task<QueueAheadDto> GetAheadAsync(CancellationToken ct = default);

}
