using AutoMapper;
using BarTasca.Data.Interfaces;
using BarTasca.DTOs.Ticket;
using BarTasca.Models;
using BarTasca.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BarTasca.Services.Services;

public class TicketService : ITicketService
{
    private readonly ICustomerRepository _customers;
    private readonly ITicketRepository _tickets;
    private readonly IMapper _mapper;

    public TicketService(ICustomerRepository customers, ITicketRepository tickets, IMapper mapper)
    {
        _customers = customers;
        _tickets = tickets;
        _mapper = mapper;
    }

    public async Task<TicketDetailDto> CreateAsync(CreateTicketDto dto, CancellationToken ct = default)
    {
        // 1) Regla: un ticket activo por teléfono (Waiting/Notified)
        var existingActive = await _tickets.GetActiveByPhoneAsync(dto.Phone, ct);
        if (existingActive is not null)
        {
            var aheadExisting = await _tickets.CountAheadAsync(existingActive.Id, ct);
            var dtoExisting = _mapper.Map<TicketDetailDto>(existingActive);
            //dtoExisting.Ahead = aheadExisting;
            // No exponer nombre del cliente en DTO público
            //dtoExisting.CustomerFullName = string.Empty;
            return dtoExisting;
        }

        // 2) Buscar o crear Customer por Phone
        var customer = await _customers.GetByPhoneAsync(dto.Phone, ct);
        if (customer is null)
        {
            customer = new Customer
            {
                FullName = dto.FullName,
                Phone = dto.Phone
            };
            await _customers.AddAsync(customer, ct);
            await _customers.SaveChangesAsync(ct);
        }

        // 3) Position = MAX(Position WHERE Status=Waiting) + 1
        var maxWaiting = await _tickets.GetMaxWaitingPositionAsync(ct);
        var newPosition = maxWaiting + 1;

        var ticket = new Ticket
        {
            CustomerId = customer.Id,
            PeopleCount = dto.PeopleCount,
            Position = newPosition,
            Status = TicketStatus.Waiting
        };

        await _tickets.AddAsync(ticket, ct);
        await _tickets.SaveChangesAsync(ct);

        var ahead = await _tickets.CountAheadAsync(ticket.Id, ct);
        var result = _mapper.Map<TicketDetailDto>(ticket);
        //result.Ahead = ahead;
        // No exponer nombre del cliente en DTO público
        //result.CustomerFullName = string.Empty;
        return result;
    }

    public async Task<TicketDetailDto?> GetAsync(int id, CancellationToken ct = default)
    {
        var ticket = await _tickets.GetByIdAsync(id, ct);
        if (ticket is null) return null;

        var ahead = await _tickets.CountAheadAsync(ticket.Id, ct);
        var dto = _mapper.Map<TicketDetailDto>(ticket);
        //dto.Ahead = ahead;
        // No exponer nombre del cliente en DTO público
        //dto.CustomerFullName = string.Empty;
        return dto;
    }
}
