using BarTasca.DTOs.Ticket;

namespace BarTasca.Services.Interfaces;

public interface ITicketService
{
    Task<TicketDetailDto> CreateAsync(CreateTicketDto dto, CancellationToken ct = default);
    Task<TicketDetailDto?> GetAsync(int id, CancellationToken ct = default);
}
