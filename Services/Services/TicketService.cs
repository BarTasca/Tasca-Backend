using AutoMapper;
using BarTasca.Data.Interfaces;
using BarTasca.DTOs.Ticket;
using BarTasca.Models;
using BarTasca.Services.Interfaces;

namespace BarTasca.Services.Services;

public class TicketService : ITicketService
{
    private readonly ICustomerRepository _customers;
    private readonly ITicketRepository _tickets;
    private readonly IMapper _mapper;
    private readonly INotificationService _notificationService;

    public TicketService(ICustomerRepository customers, ITicketRepository tickets, IMapper mapper, INotificationService notificationService)
    {
        _customers = customers;
        _tickets = tickets;
        _mapper = mapper;
        _notificationService = notificationService;
    }

    public async Task<TicketDetailDto> CreateAsync(CreateTicketDto dto, CancellationToken ct = default)
    {
        var existingActive = await _tickets.GetActiveByPhoneAsync(dto.Phone, ct);
        if (existingActive is not null)
        {
            var aheadExisting = await _tickets.CountAheadAsync(existingActive.Id, ct);
            var dtoExisting = _mapper.Map<TicketDetailDto>(existingActive);
            dtoExisting.Ahead = aheadExisting;
            dtoExisting.CustomerFullName = string.Empty;
            return dtoExisting;
        }

        var customer = await _customers.GetByPhoneAsync(dto.Phone, ct);
        if (customer is null)
        {
            customer = new Customer { FullName = dto.FullName, Phone = dto.Phone };
            await _customers.AddAsync(customer, ct);
            await _customers.SaveChangesAsync(ct);
        }

        var maxWaiting = await _tickets.GetMaxWaitingPositionAsync(ct);
        var newPosition = maxWaiting + 1;

        var ticket = new Ticket
        {
            CustomerId = customer.Id,
            PeopleCount = dto.PeopleCount,
            Position = newPosition,
            Status = TicketStatus.Waiting
        };

        ticket.Customer = customer;

        await _tickets.AddAsync(ticket, ct);
        await _tickets.SaveChangesAsync(ct);

        var ahead = await _tickets.CountAheadAsync(ticket.Id, ct);

        await _notificationService.NotifyTicketCreatedAsync(ticket, ahead, ct);
        if (ahead <= 3 && ahead > 1)
        {
            await _notificationService.NotifyTicketUpdatedAsync(ticket, ahead, NotificationType.Reminder, ct);
        }
        if (ahead <= 1)
        {
            ticket.Status = TicketStatus.Notified;
            ticket.NotifiedAt = DateTime.UtcNow;
            _tickets.Update(ticket);
            await _tickets.SaveChangesAsync(ct);
            await _notificationService.NotifyTicketUpdatedAsync(ticket, ahead, NotificationType.Turn, ct);
        }

        var result = _mapper.Map<TicketDetailDto>(ticket);
        result.Ahead = ahead;
        result.CustomerFullName = string.Empty;
        return result;
    }

    public async Task<TicketDetailDto?> GetAsync(int id, CancellationToken ct = default)
    {
        var ticket = await _tickets.GetByIdAsync(id, ct);
        if (ticket is null) return null;

        var ahead = await _tickets.CountAheadAsync(ticket.Id, ct);
        var dto = _mapper.Map<TicketDetailDto>(ticket);
        dto.Ahead = ahead;
        dto.CustomerFullName = string.Empty;
        return dto;
    }

    public Task<TicketDetailDto?> ServeAsync(int id, CancellationToken ct = default)
        => UpdateStatusAsync(id, TicketStatus.Confirmed, setConfirmedAt: true, ct);

    public Task<TicketDetailDto?> SkipAsync(int id, CancellationToken ct = default)
        => UpdateStatusAsync(id, TicketStatus.Skipped, setConfirmedAt: false, ct);

    public Task<TicketDetailDto?> CancelAsync(int id, CancellationToken ct = default)
        => UpdateStatusAsync(id, TicketStatus.Cancelled, setConfirmedAt: false, ct);

    public async Task<IReadOnlyList<TicketStaffListDto>> ListForStaffAsync(string status = "active", int take = 20, CancellationToken ct = default)
    {
        var statuses = status.ToLowerInvariant() switch
        {
            "waiting" => new[] { TicketStatus.Waiting },
            "notified" => new[] { TicketStatus.Notified },
            "all" => Enum.GetValues<TicketStatus>(),
            _ => new[] { TicketStatus.Waiting, TicketStatus.Notified }
        };

        var list = await _tickets.ListByStatusesAsync(statuses, take, ct);

        return list.Select(t => new TicketStaffListDto
        {
            Id = t.Id,
            PeopleCount = t.PeopleCount,
            Position = t.Position,
            Status = t.Status.ToString(),
            CreatedAt = t.CreatedAt,
            CustomerFullName = t.Customer.FullName
        }).ToList();
    }

    private async Task<TicketDetailDto?> UpdateStatusAsync(int id, TicketStatus target, bool setConfirmedAt, CancellationToken ct)
    {
        var ticket = await _tickets.GetByIdAsync(id, ct);
        if (ticket is null) return null;

        var isActive = ticket.Status == TicketStatus.Waiting || ticket.Status == TicketStatus.Notified;

        if (!isActive && ticket.Status != target)
            throw new InvalidOperationException($"Invalid transition from {ticket.Status} to {target}");

        if (ticket.Status != target)
        {
            ticket.Status = target;
            if (setConfirmedAt) ticket.ConfirmedAt = DateTime.UtcNow;
            _tickets.Update(ticket);
            await _tickets.SaveChangesAsync(ct);
        }

        var aheadSelf = await _tickets.CountAheadAsync(ticket.Id, ct);
        await _notificationService.BroadcastTicketUpdatedAsync(ticket, aheadSelf, ct);

        var affected = await _tickets.ListActiveBehindAsync(ticket.Position, 500, ct);
        foreach (var a in affected)
        {
            var afterAhead = await _tickets.CountAheadAsync(a.Id, ct);
            var previousAhead = afterAhead + 1;

            if (previousAhead > 3 && afterAhead == 3)
            {
                await _notificationService.NotifyTicketUpdatedAsync(a, afterAhead, NotificationType.Reminder, ct);
            }
            else if (previousAhead > 1 && afterAhead <= 1)
            {
                var toUpdate = await _tickets.GetByIdAsync(a.Id, ct);
                if (toUpdate is not null)
                {
                    if (toUpdate.Status != TicketStatus.Notified)
                    {
                        toUpdate.Status = TicketStatus.Notified;
                        toUpdate.NotifiedAt = DateTime.UtcNow;
                        _tickets.Update(toUpdate);
                        await _tickets.SaveChangesAsync(ct);
                    }

                    var finalAhead = await _tickets.CountAheadAsync(toUpdate.Id, ct);
                    await _notificationService.NotifyTicketUpdatedAsync(toUpdate, finalAhead, NotificationType.Turn, ct);
                }
            }
            else
            {
                await _notificationService.BroadcastTicketUpdatedAsync(a, afterAhead, ct);
            }
        }

        var dto = _mapper.Map<TicketDetailDto>(ticket);
        dto.Ahead = aheadSelf;
        dto.CustomerFullName = string.Empty;
        return dto;
    }

    public async Task<TicketDetailDto?> NotifyAsync(int id, bool force = false, CancellationToken ct = default)
    {
        var ticket = await _tickets.GetByIdAsync(id, ct);
        if (ticket is null) return null;

        var isTerminal = ticket.Status is TicketStatus.Confirmed or TicketStatus.Skipped or TicketStatus.Cancelled;
        if (isTerminal) throw new InvalidOperationException($"Invalid transition from {ticket.Status} to Notified");

        if (ticket.Status == TicketStatus.Notified && !force)
        {
            var ahead0 = await _tickets.CountAheadAsync(ticket.Id, ct);
            var dto0 = _mapper.Map<TicketDetailDto>(ticket);
            dto0.Ahead = ahead0;
            dto0.CustomerFullName = string.Empty;
            return dto0;
        }

        ticket.Status = TicketStatus.Notified;
        ticket.NotifiedAt = DateTime.UtcNow;

        _tickets.Update(ticket);
        await _tickets.SaveChangesAsync(ct);

        var ahead = await _tickets.CountAheadAsync(ticket.Id, ct);
        await _notificationService.NotifyTicketUpdatedAsync(ticket, ahead, NotificationType.Manual, ct);

        var dto = _mapper.Map<TicketDetailDto>(ticket);
        dto.Ahead = ahead;
        dto.CustomerFullName = string.Empty;
        return dto;
    }
}
