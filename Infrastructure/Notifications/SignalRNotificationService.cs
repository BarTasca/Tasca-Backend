namespace BarTasca.Infrastructure.Notifications;

using AutoMapper;
using BarTasca.Data.Interfaces;
using BarTasca.DTOs.Ticket;
using BarTasca.Models;
using BarTasca.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;

public class SignalRNotificationService<THub> : INotificationService where THub : Hub
{
    private readonly IHubContext<THub> _hub;
    private readonly IMapper _mapper;
    private readonly INotificationRepository _notifications;

    public SignalRNotificationService(IHubContext<THub> hub, IMapper mapper, INotificationRepository notifications)
    {
        _hub = hub;
        _mapper = mapper;
        _notifications = notifications;
    }

    public async Task NotifyTicketCreatedAsync(Ticket ticket, int ahead, CancellationToken ct = default)
    {
        var dto = _mapper.Map<TicketDetailDto>(ticket);
        dto.Ahead = ahead;
        dto.CustomerFullName = string.Empty;

        await _hub.Clients.Group($"ticket:{ticket.Id}").SendAsync("ticketCreated", dto, ct);
        await _hub.Clients.Group("staff").SendAsync("ticketCreated", new
        {
            ticket.Id,
            ticket.PeopleCount,
            ticket.Position,
            Status = ticket.Status.ToString(),
            ticket.CreatedAt,
            CustomerFullName = ticket.Customer?.FullName ?? string.Empty
        }, ct);
    }

    public async Task NotifyTicketUpdatedAsync(Ticket ticket, int ahead, NotificationType type, CancellationToken ct = default)
    {
        if (type is NotificationType.Reminder or NotificationType.Turn)
        {
            var already = await _notifications.ExistsSentAsync(ticket.Id, type, ct);
            if (already) return;
        }

        var dto = _mapper.Map<TicketDetailDto>(ticket);
        dto.Ahead = ahead;
        dto.CustomerFullName = string.Empty;

        var status = NotificationStatus.Sent;
        try
        {
            await _hub.Clients.Group($"ticket:{ticket.Id}").SendAsync("ticketUpdated", dto, ct);
            await _hub.Clients.Group("staff").SendAsync("ticketUpdated", new
            {
                ticket.Id,
                ticket.PeopleCount,
                ticket.Position,
                Status = ticket.Status.ToString(),
                ticket.CreatedAt,
                CustomerFullName = ticket.Customer?.FullName ?? string.Empty
            }, ct);
        }
        catch
        {
            status = NotificationStatus.Failed;
        }

        await _notifications.AddAsync(new Notification
        {
            TicketId = ticket.Id,
            Channel = NotificationChannel.SignalR,
            Type = type,
            Status = status,
            SentAt = DateTime.UtcNow
        }, ct);
        await _notifications.SaveChangesAsync(ct);
    }

    public async Task BroadcastTicketUpdatedAsync(Ticket ticket, int ahead, CancellationToken ct = default)
    {
        var dto = _mapper.Map<TicketDetailDto>(ticket);
        dto.Ahead = ahead;
        dto.CustomerFullName = string.Empty;

        await _hub.Clients.Group($"ticket:{ticket.Id}").SendAsync("ticketUpdated", dto, ct);
        await _hub.Clients.Group("staff").SendAsync("ticketUpdated", new
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
