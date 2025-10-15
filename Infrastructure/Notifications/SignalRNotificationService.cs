namespace BarTasca.Infrastructure.Notifications;

using AutoMapper;
using BarTasca.Data.Interfaces;
using BarTasca.DTOs.Ticket;
using Microsoft.Extensions.Options;
using BarTasca.Infrastructure.Options;
using BarTasca.Models;
using BarTasca.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

public class SignalRNotificationService<THub> : INotificationService where THub : Hub
{
    private readonly IHubContext<THub> _hub;
    private readonly IMapper _mapper;
    private readonly INotificationRepository _notifications;
    private readonly ILogger<SignalRNotificationService<THub>> _logger;
    private const string StaffGroup = "staff";

    private readonly bool _smsEnabled;
    private readonly string? _twilioFrom;

    public SignalRNotificationService(
        IHubContext<THub> hub,
        IMapper mapper,
        INotificationRepository notifications,
        ILogger<SignalRNotificationService<THub>> logger,
        IOptions<TwilioOptions> twilioOptions)
    {
        _hub = hub;
        _mapper = mapper;
        _notifications = notifications;
        _logger = logger;

        var tw = twilioOptions?.Value ?? throw new InvalidOperationException("Twilio options not set");

        _smsEnabled = tw.Enabled;
        _twilioFrom = tw.From;
    }

    public async Task NotifyTicketCreatedAsync(Ticket ticket, int ahead, CancellationToken ct = default)
    {
        var dto = _mapper.Map<TicketDetailDto>(ticket);
        dto.Ahead = ahead;
        dto.CustomerFullName = string.Empty;

        try
        {
            await _hub.Clients.Group($"ticket:{ticket.Id}").SendAsync("ticketCreated", dto, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR failed: group={Group} event={Event} ticketId={TicketId}", $"ticket:{ticket.Id}", "ticketCreated", ticket.Id);
        }

        try
        {
            await _hub.Clients.Group(StaffGroup).SendAsync("ticketCreated", new
            {
                ticket.Id,
                ticket.PeopleCount,
                ticket.Position,
                Status = ticket.Status.ToString(),
                ticket.CreatedAt,
                CustomerFullName = ticket.Customer?.FullName ?? string.Empty
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR failed: group={Group} event={Event} ticketId={TicketId}", "staff", "ticketCreated", ticket.Id);
        }

    }

    public async Task NotifyTicketUpdatedAsync(Ticket ticket, int ahead, NotificationType type, CancellationToken ct = default)
    {
        var dto = _mapper.Map<TicketDetailDto>(ticket);
        dto.Ahead = ahead;
        dto.CustomerFullName = string.Empty;

        bool clientOk = true;
        bool staffOk = true;

        try
        {
            await _hub.Clients.Group($"ticket:{ticket.Id}").SendAsync("ticketUpdated", dto, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR failed: group={Group} event={Event} ticketId={TicketId}", $"ticket:{ticket.Id}", "ticketUpdated", ticket.Id);
            clientOk = false;
        }
        try
        {
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR failed: group={Group} event={Event} ticketId={TicketId}", "staff", "ticketUpdated", ticket.Id);
            staffOk = false;
        }

        bool shouldPersistSignalR = type == NotificationType.Manual || !(await _notifications.ExistsSentAsync(ticket.Id, type, NotificationChannel.SignalR, ct));

        if (shouldPersistSignalR)
        {
            var status = (clientOk && staffOk) ? NotificationStatus.Sent : NotificationStatus.Failed;

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

        if (!_smsEnabled) return;

        switch (type)
        {
            case NotificationType.Reminder:
                {
                    // Solo si no existe ya Reminder
                    var already = await _notifications.ExistsSentAsync(ticket.Id, NotificationType.Reminder, NotificationChannel.Sms, ct);
                    if (!already)
                    {
                        await SendSmsAndPersistAsync(ticket, NotificationType.Reminder, BuildReminderMessage(ticket, ahead), ct);
                    }
                    break;
                }
            case NotificationType.Turn:
                {
                    // Nunca SMS en Turn
                    break;
                }
            case NotificationType.Manual:
                {
                    // Siempre SMS para Manual
                    var window = TimeSpan.FromMinutes(5);
                    if (!await _notifications.WasSentRecentlyAsync(ticket.Id, NotificationType.Manual, NotificationChannel.Sms, window, ct))
                    {
                        await SendSmsAndPersistAsync(ticket, NotificationType.Manual, BuildManualMessage(ticket, ahead), ct);
                    }
                    else
                    {
                        _logger.LogInformation("Manual SMS rate limit: ticketId={TicketId}", ticket.Id);
                    }
                        break;
                }
        }
    }

    public async Task BroadcastTicketUpdatedAsync(Ticket ticket, int ahead, CancellationToken ct = default)
    {
        var dto = _mapper.Map<TicketDetailDto>(ticket);
        dto.Ahead = ahead;
        dto.CustomerFullName = string.Empty;

        try
        {
            await _hub.Clients.Group($"ticket:{ticket.Id}").SendAsync("ticketUpdated", dto, ct);

        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR failed: group={Group} event={Event} ticketId={TicketId}", $"ticket:{ticket.Id}", "ticketUpdated", ticket.Id);
        }

        try
        {
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR failed: group={Group} event={Event} ticketId={TicketId}", "staff", "ticketUpdated", ticket.Id);
        }
    }

    private static string BuildReminderMessage(Ticket ticket, int ahead)
        => $"Bar La Tasca: quedan {ahead} por delante. Ticket #{ticket.Position} (personas: {ticket.PeopleCount}). Ves vieniendo y que aproveche.";

    private static string BuildManualMessage(Ticket ticket, int ahead)
        => $"Bar La Tasca: actualización de tu ticket #{ticket.Position}. Quedan {ahead} por delante. Ves vieniendo y que aproveche.";

    private async Task SendSmsAndPersistAsync(Ticket ticket, NotificationType type, string body, CancellationToken ct)
    {
        var toRaw = ticket.Customer?.Phone;
        var toNorm = NormalizePhone(toRaw);

        if (!IsE164(toNorm))
        {
            _logger.LogWarning("Invalid phone number for SMS: ticketId={TicketId} phone={Phone}", ticket.Id, toNorm ?? "<null>");

            await _notifications.AddAsync(new Notification
            {
                TicketId = ticket.Id,
                Channel = NotificationChannel.Sms,
                Type = type,
                Status = NotificationStatus.Failed,
                SentAt = DateTime.UtcNow
            }, ct);
            await _notifications.SaveChangesAsync(ct);

            return;
        }

        NotificationStatus status = NotificationStatus.Sent;

        try
        {
            var msg = await MessageResource.CreateAsync(
                to: new Twilio.Types.PhoneNumber(toNorm),
                from: new Twilio.Types.PhoneNumber(_twilioFrom),
                body: body
            );
        }
        catch
        {
            status = NotificationStatus.Failed;
        }

        await _notifications.AddAsync(new Notification
        {
            TicketId = ticket.Id,
            Channel = NotificationChannel.Sms,
            Type = type,
            Status = status,
            SentAt = DateTime.UtcNow
        }, ct);
        await _notifications.SaveChangesAsync(ct);
    }

    private static string NormalizePhone(string? input) => string.IsNullOrWhiteSpace(input) ? "" :
    input.Trim().Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");

    private static bool IsE164(string? phone)
    {
        phone = NormalizePhone(phone);
        if (string.IsNullOrWhiteSpace(phone)) return false;
        if (!phone.StartsWith("+")) return false;
        for (int i = 1; i < phone.Length; i++)
            if (!char.IsDigit(phone[i])) return false;
        return phone.Length >= 8 && phone.Length <= 16;
    }

}
