using AutoMapper;
using BarTasca.DTOs.Ticket;
using BarTasca.Models;
using BarTasca.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace BarTasca.Infrastructure.Notifications;

public class SignalRNotificationService<THub> : INotificationService where THub : Hub
{
    private readonly IHubContext<THub> _hub;
    private readonly IMapper _mapper;

    public SignalRNotificationService(IHubContext<THub> hub, IMapper mapper)
    {
        _hub = hub;
        _mapper = mapper;
    }

    public async Task NotifyTicketCreatedAsync(Ticket ticket, int ahead, CancellationToken ct = default)
    {
        var dto = _mapper.Map<TicketDetailDto>(ticket);
        dto.Ahead = ahead;
        dto.CustomerFullName = string.Empty;

        await _hub.Clients.Group($"ticket:{ticket.Id}")
            .SendAsync("ticketCreated", dto, ct);

        await _hub.Clients.Group("staff")
            .SendAsync("ticketCreated", new
            {
                ticket.Id,
                ticket.PeopleCount,
                ticket.Position,
                Status = ticket.Status.ToString(),
                ticket.CreatedAt,
                CustomerFullName = ticket.Customer?.FullName ?? string.Empty
            }, ct);
    }

    public async Task NotifyTicketUpdatedAsync(Ticket ticket, int ahead, CancellationToken ct = default)
    {
        var dto = _mapper.Map<TicketDetailDto>(ticket);
        dto.Ahead = ahead;
        dto.CustomerFullName = string.Empty;

        await _hub.Clients.Group($"ticket:{ticket.Id}")
            .SendAsync("ticketUpdated", dto, ct);

        await _hub.Clients.Group("staff")
            .SendAsync("ticketUpdated", new
            {
                ticket.Id,
                ticket.PeopleCount,
                ticket.Position,
                Status = ticket.Status.ToString(),
                ticket.CreatedAt,
                CustomerFullName = ticket.Customer?.FullName ?? string.Empty
            }, ct);
    }
}
