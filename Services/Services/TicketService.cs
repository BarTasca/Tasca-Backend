using AutoMapper;
using BarTasca.Data.Interfaces;
using BarTasca.DTOs.Queue;
using BarTasca.DTOs.Ticket;
using BarTasca.Models;
using BarTasca.Services.Exceptions;
using BarTasca.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace BarTasca.Services.Services;

public class TicketService : ITicketService
{
    private readonly ICustomerRepository _customers;
    private readonly ITicketRepository _tickets;
    private readonly IMapper _mapper;
    private readonly INotificationService _notificationService;
    private readonly ISignalRPublicNotificationService _publicSignalR;
    private readonly IServiceStateService _serviceStateService;
    private readonly ILogger<TicketService> _logger;
    private readonly IPushSubscriptionService _pushSubs;

    public TicketService(ICustomerRepository customers, ITicketRepository tickets, IMapper mapper, INotificationService notificationService, IServiceStateService serviceStateService, ILogger<TicketService> logger, ISignalRPublicNotificationService publicSignalR, IPushSubscriptionService pushSubs)
    {
        _customers = customers;
        _tickets = tickets;
        _mapper = mapper;
        _notificationService = notificationService;
        _serviceStateService = serviceStateService;
        _logger = logger;
        _publicSignalR = publicSignalR;
        _pushSubs = pushSubs;
    }

    public async Task<TicketDetailDto> CreateAsync(CreateTicketDto dto, CancellationToken ct = default)
    {
        var fullName = dto.FullName?.Trim();
        var phone = dto.Phone?.Trim();

        ValidateCreateTicketInput(fullName, phone, dto.PeopleCount);

        var existingActive = await _tickets.GetActiveByPhoneAsync(phone!, ct);

        if (existingActive is not null)
        {
            var aheadExisting = await _tickets.CountAheadAsync(existingActive.Id, ct);
            var dtoExisting = _mapper.Map<TicketDetailDto>(existingActive);
            dtoExisting.Ahead = aheadExisting;
            dtoExisting.CustomerFullName = string.Empty;
            return dtoExisting;
        }

        var serviceState = await _serviceStateService.GetAsync(ct);
        if (!serviceState.IsOpen)
            throw new ServiceClosedException();

        var customer = await _customers.GetByPhoneAsync(phone!, ct);
        if (customer is null)
        {
            customer = new Customer
            {
                FullName = fullName!,
                Phone = phone!
            };

            await _customers.AddAsync(customer, ct);
            await _customers.SaveChangesAsync(ct);
        }
        else if (customer.FullName != fullName)
        {
            customer.FullName = fullName!;

            await _customers.UpdateNameAsync(customer.Id, fullName!, ct);
            await _customers.SaveChangesAsync(ct);
        }

        var maxWaiting = await _tickets.GetMaxWaitingPositionAsync(ct);
        var newPosition = maxWaiting + 1;

        var ticket = new Ticket
        {
            CustomerId = customer.Id,
            PeopleCount = dto.PeopleCount,
            Position = newPosition,
            Status = TicketStatus.Waiting,
            PublicId = Ulid.NewUlid().ToString()
        };

        await _tickets.AddAsync(ticket, ct);
        await _tickets.SaveChangesAsync(ct);
        await BroadcastPublicAheadAsync(ct);

        var ordered = await _tickets.ListActiveOrderedAsync(ct);
        var aheadMap = CalculateAheadMap(ordered);
        var ahead = aheadMap[ticket.Id];

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

        var ahead = await GetWeightedAheadAsync(ticket.Id, ct);
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

    public async Task<TicketDetailDto?> CancelByPublicIdAsync(string publicId, CancellationToken ct = default)
    {
        var ticket = await _tickets.GetByPublicIdAsync(publicId, ct);
        if (ticket is null) return null;

        return await CancelAsync(ticket.Id, ct);
    }

    public async Task<IReadOnlyList<TicketStaffListDto>> ListForStaffAsync(string status = "active", int? take = null, CancellationToken ct = default)
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

        var beforeList = await _tickets.ListActiveOrderedAsync(ct);
        var beforeMap = CalculateAheadMap(beforeList);

        bool wasActive = ticket.Status == TicketStatus.Waiting || ticket.Status == TicketStatus.Notified;
        bool becomesInactive = target == TicketStatus.Confirmed || target == TicketStatus.Skipped || target == TicketStatus.Cancelled;

        var isActive = ticket.Status == TicketStatus.Waiting || ticket.Status == TicketStatus.Notified;

        if (!isActive && ticket.Status != target)
            throw new InvalidOperationException($"Invalid transition from {ticket.Status} to {target}");

        if (ticket.Status != target)
        {
            ticket.Status = target;
            if (setConfirmedAt) ticket.ConfirmedAt = DateTime.UtcNow;

            _tickets.Update(ticket);
            await _tickets.SaveChangesAsync(ct);

            if (becomesInactive)
            {
                await _pushSubs.DeactivateByTicketIdAsync(ticket.Id, ct);
            }

            if (wasActive && becomesInactive)
            {
                await BroadcastPublicAheadAsync(ct);
            }
        }

        var afterList = await _tickets.ListActiveOrderedAsync(ct);
        var afterMap = CalculateAheadMap(afterList);

        var aheadSelf = afterMap.TryGetValue(ticket.Id, out var selfAheadAfter)
            ? selfAheadAfter
            : 0;

        await _notificationService.BroadcastTicketUpdatedAsync(ticket, aheadSelf, ct);

        var affected = afterList.Where(t => t.Position > ticket.Position).ToList();

        foreach (var a in affected)
        {
            var beforeAhead = beforeMap.TryGetValue(a.Id, out var beforeValue) ? beforeValue : 0;
            var afterAhead = afterMap.TryGetValue(a.Id, out var afterValue) ? afterValue : 0;

            if (beforeAhead > 3 && afterAhead == 3)
            {
                await _notificationService.NotifyTicketUpdatedAsync(a, afterAhead, NotificationType.Reminder, ct);
            }
            else if (beforeAhead > 1 && afterAhead <= 1)
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

                    var finalAhead = await GetWeightedAheadAsync(toUpdate.Id, ct);
                    await _notificationService.NotifyTicketUpdatedAsync(toUpdate, finalAhead, NotificationType.Turn, ct);
                }
            }
            else if (beforeAhead != afterAhead)
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
            var ahead0 = await GetWeightedAheadAsync(ticket.Id, ct);
            var dto0 = _mapper.Map<TicketDetailDto>(ticket);
            dto0.Ahead = ahead0;
            dto0.CustomerFullName = string.Empty;
            return dto0;
        }

        ticket.Status = TicketStatus.Notified;
        ticket.NotifiedAt = DateTime.UtcNow;

        _tickets.Update(ticket);
        await _tickets.SaveChangesAsync(ct);

        var ahead = await GetWeightedAheadAsync(ticket.Id, ct);
        await _notificationService.NotifyTicketUpdatedAsync(ticket, ahead, NotificationType.Manual, ct);

        var dto = _mapper.Map<TicketDetailDto>(ticket);
        dto.Ahead = ahead;
        dto.CustomerFullName = string.Empty;
        return dto;
    }

    public async Task<TicketStatusDto?> GetStatusAsync(string publicId, CancellationToken ct = default)
    {
        var ticket = await _tickets.GetByPublicIdAsync(publicId, ct);
        if (ticket is null) return null;

        var ahead = await GetWeightedAheadAsync(ticket.Id, ct);

        return new TicketStatusDto
        {
            PublicId = ticket.PublicId,
            Status = ticket.Status.ToString(),
            Ahead = ahead,
            Position = ticket.Position,
            PeopleCount = ticket.PeopleCount,
            CreatedAt = ticket.CreatedAt,
            NotifiedAt = ticket.NotifiedAt,
            CustomerFullName = ticket.Customer.FullName
        };
    }

    public async Task<QueueAheadDto> GetAheadAsync(CancellationToken ct = default)
    {
        var state = await _serviceStateService.GetAsync(ct);
        if (!state.IsOpen)
        {
            return new QueueAheadDto
            {
                IsOpen = false,
                Ahead = 0
            };
        }

        var ahead = await GetWeightedActiveCountAsync(ct);

        return new QueueAheadDto
        {
            IsOpen = true,
            Ahead = ahead
        };
    }

    public async Task<TicketDetailDto?> UpdateAsync(int id, UpdateTicketDto dto, CancellationToken ct = default)
    {
        var ticket = await _tickets.GetByIdAsync(id, ct);
        if (ticket is null) return null;

        return await UpdateInternalAsync(ticket, dto, ct);
    }

    public async Task<TicketDetailDto?> UpdateByPublicIdAsync(string publicId, UpdateTicketDto dto, CancellationToken ct = default)
    {
        var ticket = await _tickets.GetByPublicIdAsync(publicId, ct);
        if (ticket is null) return null;

        return await UpdateInternalAsync(ticket, dto, ct);
    }

    private async Task<TicketDetailDto> UpdateInternalAsync(Ticket ticket, UpdateTicketDto dto, CancellationToken ct)
    {
        if (dto.PeopleCount < 1 || dto.PeopleCount > 15)
            throw new ArgumentException("PeopleCount must be between 1 and 15.");

        var isActive = ticket.Status == TicketStatus.Waiting || ticket.Status == TicketStatus.Notified;
        if (!isActive)
            throw new InvalidOperationException($"Cannot update people count for ticket in status {ticket.Status}");

        var beforeList = await _tickets.ListActiveOrderedAsync(ct);
        var beforeMap = CalculateAheadMap(beforeList);
        var oldWeight = GetWeight(ticket);

        if (ticket.PeopleCount == dto.PeopleCount)
        {
            var aheadSame = beforeMap.TryGetValue(ticket.Id, out var sameAhead) ? sameAhead : 0;

            var resultSame = _mapper.Map<TicketDetailDto>(ticket);
            resultSame.Ahead = aheadSame;
            resultSame.CustomerFullName = string.Empty;
            return resultSame;
        }

        ticket.PeopleCount = dto.PeopleCount;
        var newWeight = GetWeight(ticket);

        _tickets.Update(ticket);
        await _tickets.SaveChangesAsync(ct);

        var afterList = await _tickets.ListActiveOrderedAsync(ct);
        var afterMap = CalculateAheadMap(afterList);

        var ahead = afterMap.TryGetValue(ticket.Id, out var selfAhead) ? selfAhead : 0;
        await _notificationService.BroadcastTicketUpdatedAsync(ticket, ahead, ct);

        if (oldWeight != newWeight)
        {
            var affected = afterList.Where(t => t.Position > ticket.Position);

            foreach (var a in affected)
            {
                var beforeAhead = beforeMap.TryGetValue(a.Id, out var beforeValue) ? beforeValue : 0;
                var afterAhead = afterMap.TryGetValue(a.Id, out var afterValue) ? afterValue : 0;

                if (beforeAhead != afterAhead)
                {
                    await _notificationService.BroadcastTicketUpdatedAsync(a, afterAhead, ct);
                }
            }

            await BroadcastPublicAheadAsync(ct);
        }

        var result = _mapper.Map<TicketDetailDto>(ticket);
        result.Ahead = ahead;
        result.CustomerFullName = string.Empty;
        return result;
    }

    private async Task BroadcastPublicAheadAsync(CancellationToken ct)
    {
        var state = await _serviceStateService.GetAsync(ct);

        if (!state.IsOpen)
        {
            await _publicSignalR.BroadcastAheadUpdatedAsync(new QueueAheadDto
            {
                IsOpen = false,
                Ahead = 0
            }, ct);

            return;
        }

        var active = await GetWeightedActiveCountAsync(ct);

        await _publicSignalR.BroadcastAheadUpdatedAsync(new QueueAheadDto
        {
            IsOpen = true,
            Ahead = active
        }, ct);
    }

    private static void ValidateCreateTicketInput(string? fullName, string? phone, byte peopleCount)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("FullName is required.");

        if (string.IsNullOrWhiteSpace(phone))
            throw new ArgumentException("Phone is required.");

        if (!System.Text.RegularExpressions.Regex.IsMatch(phone, @"^\+[1-9]\d{0,3}\s\d{6,14}$"))
            throw new ArgumentException("Phone must be in international format, for example +34 608593022.");

        if (peopleCount < 1 || peopleCount > 15)
            throw new ArgumentException("PeopleCount must be between 1 and 15.");
    }

    /// <summary>
    /// Calculates the weight of a ticket based on the number of people. Tickets with 8 or more people count as 2, otherwise 1.
    /// </summary>
    /// <param name="ticket">Ticket to calculate weight for.</param>
    /// <returns>Weight of the ticket.</returns>
    private static int GetWeight(Ticket ticket)
    {
        return ticket.PeopleCount >= 8 ? 2 : 1;
    }

    /// <summary>
    /// Calculates the total weight of tickets ahead of the target ticket in the ordered list. It iterates through the list until it finds the target ticket, summing the weights of the tickets it encounters. Once it reaches the target ticket, it stops and returns the total weight calculated. This method assumes that the 'ordered' list is sorted by position in ascending order.
    /// </summary>
    /// <param name="ordered">List of tickets ordered by position.</param>
    /// <param name="targetTicketId">Id of the target ticket to calculate ahead for.</param>
    /// <returns>Total weight of tickets ahead of the target ticket.</returns>
    private static int CalculateAhead(IReadOnlyList<Ticket> ordered, int targetTicketId)
    {
        int ahead = 0;

        foreach (var t in ordered)
        {
            if (t.Id == targetTicketId) break;

            ahead += GetWeight(t);
        }
        return ahead;
    }

    /// <summary>
    /// Calculates a map of ticket IDs to their ahead counts based on the ordered list of tickets. It iterates through the ordered list, maintaining a running total of the weight of tickets encountered so far. For each ticket, it stores the current total weight in the result dictionary using the ticket's ID as the key. After processing all tickets, it returns the dictionary containing the ahead counts for each ticket ID. This method assumes that the 'ordered' list is sorted by position in ascending order.
    /// </summary>
    /// <param name="ordered">List of tickets ordered by position.</param>
    /// <returns>Dictionary mapping ticket IDs to their ahead counts.</returns>
    private static Dictionary<int, int> CalculateAheadMap(IReadOnlyList<Ticket> ordered)
    {
        var result = new Dictionary<int, int>();
        int ahead = 0;
        foreach (var t in ordered)
        {
            result[t.Id] = ahead;
            ahead += GetWeight(t);
        }
        return result;
    }

    /// <summary>
    /// Calculates the weighted number of tickets ahead of the specified ticket by first retrieving the ordered list of active tickets, then creating a map of ticket IDs to their ahead counts using the CalculateAheadMap method, and finally returning the ahead count for the specified ticket ID from the map. If the ticket ID is not found in the map, it returns 0. This method provides a more efficient way to get the ahead count for a specific ticket by avoiding repeated calculations for each ticket when multiple ahead counts are needed.
    /// </summary>
    /// <param name="ticketId">Id of the ticket to calculate ahead for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Weighted number of tickets ahead of the specified ticket.</returns>
    private async Task<int> GetWeightedAheadAsync(int ticketId, CancellationToken ct)
    {
        var ordered = await _tickets.ListActiveOrderedAsync(ct);
        var aheadMap = CalculateAheadMap(ordered);

        return aheadMap.TryGetValue(ticketId, out var ahead) ? ahead : 0;
    }

    private async Task<int> GetWeightedActiveCountAsync(CancellationToken ct)
    {
        var ordered = await _tickets.ListActiveOrderedAsync(ct);
        return ordered.Sum(GetWeight);
    }
}
