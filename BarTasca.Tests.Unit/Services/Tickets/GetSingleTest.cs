using AutoMapper;
using BarTasca.Data.Interfaces;
using BarTasca.DTOs.Ticket;
using BarTasca.Models;
using BarTasca.Services.Interfaces;
using BarTasca.Services.Services;
using Bogus;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace BarTasca.Tests.Unit.Services.Tickets
{
    public class GetSingleTest
    {
        private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();
        private readonly ITicketRepository _tickets = Substitute.For<ITicketRepository>();
        private readonly INotificationService _notifications = Substitute.For<INotificationService>();
        private readonly IServiceStateService _serviceState = Substitute.For<IServiceStateService>();
        private readonly IMapper _mapper = Substitute.For<IMapper>();
        private readonly ILogger<TicketService> _logger = Substitute.For<ILogger<TicketService>>();
        private readonly ISignalRPublicNotificationService _publicSignalR = Substitute.For<ISignalRPublicNotificationService>();

        private readonly Faker _faker = new("es");


        public GetSingleTest()
        {
            _mapper.Map<TicketDetailDto>(Arg.Any<Ticket>())
                   .Returns(ci =>
                   {
                       var t = ci.Arg<Ticket>();
                       return new TicketDetailDto
                       {
                           Id = t.Id,
                           PublicId = t.PublicId ?? string.Empty,
                           PeopleCount = t.PeopleCount,
                           Position = t.Position,
                           Status = t.Status.ToString(),
                           CreatedAt = t.CreatedAt,
                           NotifiedAt = t.NotifiedAt,
                           ConfirmedAt = t.ConfirmedAt,
                           ExpiresAt = null,
                           Ahead = 0,
                           CustomerFullName = t.Customer?.FullName ?? string.Empty
                       };
                   });
        }

        // If ticket not found, returns null and does not call mapper or count
        [Fact]
        public async Task GetAsync_ticket_not_found_returns_null_and_does_not_call_mapper_or_count()
        {
            var svc = new TicketService(_customers, _tickets, _mapper, _notifications, _serviceState, _logger, _publicSignalR);
            var ticketId = 1;
            _tickets.GetByIdAsync(ticketId, Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult<Ticket?>(null));
            var result = await svc.GetAsync(ticketId);
            result.Should().BeNull();
            await _tickets.Received(1).GetByIdAsync(ticketId, Arg.Any<CancellationToken>());
            await _tickets.DidNotReceive().CountAheadAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
            _mapper.DidNotReceive().Map<TicketDetailDto>(Arg.Any<Ticket>());
        }

        // If ticket found, returns dto with correct ahead and empty customer name
        [Fact]
        public async Task GetAsync_existing_ticket_returns_dto_with_correct_ahead_and_empty_customer_name()
        {
            var svc = new TicketService(_customers, _tickets, _mapper, _notifications, _serviceState, _logger, _publicSignalR);
            var ticketId = 1;
            var ticket = new Ticket
            {
                Id = ticketId,
                PublicId = "ULID-EXISTENTE",
                PeopleCount = 2,
                Position = _faker.Random.Int(5, 100),
                Status = TicketStatus.Waiting,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                Customer = new Customer
                {
                    FullName = _faker.Name.FullName(),
                    Phone = _faker.Phone.PhoneNumber()
                }
            };
            _tickets.GetByIdAsync(ticketId, Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult<Ticket?>(ticket));
            var expectedAhead = 5;
            _tickets.CountAheadAsync(ticketId, Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult(expectedAhead));
            var result = await svc.GetAsync(ticketId);
            result.Should().NotBeNull();
            result!.Id.Should().Be(ticket.Id);
            result.Ahead.Should().Be(expectedAhead);
            result.CustomerFullName.Should().BeEmpty();
            await _tickets.Received(1).GetByIdAsync(ticketId, Arg.Any<CancellationToken>());
            await _tickets.Received(1).CountAheadAsync(ticketId, Arg.Any<CancellationToken>());
            _mapper.Received(1).Map<TicketDetailDto>(ticket);
        }

        // Overrides mapped ahead and CustomerFullName with countAhead and Empty value
        [Fact]
        public async Task GetAsync_overrides_mapped_ahead_with_countAhead_value()
        {
            var ticketId = Guid.NewGuid();
            var expectedAhead = 5;
            var ticket = new Ticket
            {
                Id = 1,
                PublicId = ticketId.ToString(),
                PeopleCount = 4,
                Position = _faker.Random.Int(10, 200),
                Status = TicketStatus.Waiting,
                CreatedAt = DateTime.UtcNow.AddMinutes(-20),
                Customer = new Customer
                {
                    FullName = _faker.Name.FullName(),
                    Phone = _faker.Phone.PhoneNumber()
                }
            };
            var svc = new TicketService(_customers, _tickets, _mapper, _notifications, _serviceState, _logger, _publicSignalR);
            _tickets.GetByIdAsync(ticket.Id, Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult<Ticket?>(ticket));
            _tickets.CountAheadAsync(ticket.Id, Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult(expectedAhead));
            var result = await svc.GetAsync(ticket.Id);
            result.Should().NotBeNull();
            result!.Ahead.Should().Be(expectedAhead);
            result!.CustomerFullName.Should().BeEmpty();    
        }

        [Fact]
        public async Task GetAsync_invalid_id_returns_null()
        {
            var svc = new TicketService(_customers, _tickets, _mapper, _notifications, _serviceState, _logger, _publicSignalR);
            var invalidId = 0;
            var result = await svc.GetAsync(invalidId);
            result.Should().BeNull();
            await _tickets.Received(1).GetByIdAsync(invalidId, Arg.Any<CancellationToken>());
            await _tickets.DidNotReceive().CountAheadAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
            _mapper.DidNotReceive().Map<TicketDetailDto>(Arg.Any<Ticket>());
        }

    }
}
