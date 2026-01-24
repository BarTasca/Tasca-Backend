using AutoMapper;
using BarTasca.Data.Interfaces;
using BarTasca.DTOs.Ticket;
using BarTasca.Models;
using BarTasca.Services.Interfaces;
using BarTasca.Services.Services;
using Bogus;
using FluentAssertions;
using NSubstitute;

namespace BarTasca.Tests.Unit.Services.Tickets
{
    public class NotifyAsyncTest
    {
        private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();
        private readonly ITicketRepository _tickets = Substitute.For<ITicketRepository>();
        private readonly INotificationService _notifications = Substitute.For<INotificationService>();
        private readonly IServiceStateService _serviceState = Substitute.For<IServiceStateService>();   
        private readonly IMapper _mapper = Substitute.For<IMapper>();

        private readonly Faker _faker = new("es");


        public NotifyAsyncTest()
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

        // If ticket not found, returns null and does nothing else
        [Fact]
        public async Task NotifyAsync_TicketNotFound_ReturnsNull()
        {
            var service = new TicketService(_customers, _tickets, _mapper, _notifications, _serviceState);
            int ticketId = _faker.Random.Int(1, 1000);
            _tickets.GetByIdAsync(ticketId, Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult<Ticket?>(null));
            
            var result = await service.NotifyAsync(ticketId, force: false);
         
            result.Should().BeNull();
            await _notifications.DidNotReceiveWithAnyArgs().NotifyTicketUpdatedAsync(default!, default, default, default);
        }

        // If ticket is in a terminal status (Confirmed/Skipped/Cancelled), throws InvalidOperationException
        [Fact]
        public async Task NotifyAsync_TicketInTerminalStatus_ThrowsInvalidOperationException()
        {
            var service = new TicketService(_customers, _tickets, _mapper, _notifications, _serviceState);
            int ticketId = _faker.Random.Int(1, 1000);
            var userName = _faker.Name.FullName();
            var terminalStatuses = new[] { TicketStatus.Confirmed, TicketStatus.Skipped, TicketStatus.Cancelled };
            foreach (var status in terminalStatuses)
            {
                var ticket = new Ticket
                {
                    Id = ticketId,
                    PeopleCount = 4,
                    Position = 1,
                    Status = status,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                    Customer = new Customer { FullName = userName }
                };
                _tickets.GetByIdAsync(ticketId, Arg.Any<CancellationToken>())
                        .Returns(Task.FromResult<Ticket?>(ticket));
                Func<Task> act = async () => await service.NotifyAsync(ticketId, force: false);
                await act.Should().ThrowAsync<InvalidOperationException>()
                         .WithMessage($"Invalid transition from {ticket.Status} to Notified");
                await _notifications.DidNotReceiveWithAnyArgs().NotifyTicketUpdatedAsync(default!, default, default, default);
            }
        }

        // If ticket already Notified and force=false, returns dto with correct Ahead and does not update or notify
        [Fact]
        public async Task NotifyAsync_TicketAlreadyNotified_ForceFalse_ReturnsDtoWithoutChanges()
        {
            var service = new TicketService(_customers, _tickets, _mapper, _notifications, _serviceState);
            int ticketId = _faker.Random.Int(1, 1000);
            var userName = _faker.Name.FullName();
            var ticket = new Ticket
            {
                Id = ticketId,
                PeopleCount = 4,
                Position = 1,
                Status = TicketStatus.Notified,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                NotifiedAt = DateTime.UtcNow.AddMinutes(-5),
                Customer = new Customer { FullName = userName }
            };
            _tickets.GetByIdAsync(ticketId, Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult<Ticket?>(ticket));
            _tickets.CountAheadAsync(ticketId, Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult(0));
            var result = await service.NotifyAsync(ticketId, force: false);
            result.Should().NotBeNull();
            result!.Id.Should().Be(ticket.Id);
            result.Status.Should().Be(TicketStatus.Notified.ToString());
            result.Ahead.Should().Be(0);
            result.CustomerFullName.Should().BeEmpty();
            await _notifications.DidNotReceiveWithAnyArgs().NotifyTicketUpdatedAsync(default!, default, default, default);
        }

        // If ticket already Notified and force=true, updates NotifiedAt, saves changes and sends Manual notification
        [Fact]
        public async Task NotifyAsync_TicketAlreadyNotified_ForceTrue_UpdatesNotifiedAtAndSendsNotification()
        {
            var service = new TicketService(_customers, _tickets, _mapper, _notifications, _serviceState);
            int ticketId = _faker.Random.Int(1, 1000);
            var userName = _faker.Name.FullName();
            var ticket = new Ticket
            {
                Id = ticketId,
                PeopleCount = 4,
                Position = 1,
                Status = TicketStatus.Notified,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                NotifiedAt = DateTime.UtcNow.AddMinutes(-5),
                Customer = new Customer { FullName = userName }
            };
            _tickets.GetByIdAsync(ticketId, Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult<Ticket?>(ticket));
            _tickets.CountAheadAsync(ticketId, Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult(0));
            var beforeNotifyAt = ticket.NotifiedAt;
            var result = await service.NotifyAsync(ticketId, force: true);
            result.Should().NotBeNull();
            result!.Id.Should().Be(ticket.Id);
            result.Status.Should().Be(TicketStatus.Notified.ToString());
            result.Ahead.Should().Be(0);
            result.CustomerFullName.Should().BeEmpty();
            ticket.NotifiedAt.Should().BeAfter(beforeNotifyAt.Value);
            await _notifications.Received(1).NotifyTicketUpdatedAsync(ticket, 0, NotificationType.Manual, Arg.Any<CancellationToken>());
        }

        // If ticket is Waiting, updates status to Notified, sets NotifiedAt, saves changes and sends Manual notification
        [Fact]
        public async Task NotifyAsync_TicketIsWaiting_UpdatesStatusToNotifiedAndSendsNotification()
        {
            var service = new TicketService(_customers, _tickets, _mapper, _notifications, _serviceState);
            int ticketId = _faker.Random.Int(1, 1000);
            var userName = _faker.Name.FullName();
            var ticket = new Ticket
            {
                Id = ticketId,
                PeopleCount = 4,
                Position = 1,
                Status = TicketStatus.Waiting,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                Customer = new Customer { FullName = userName }
            };
            _tickets.GetByIdAsync(ticketId, Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult<Ticket?>(ticket));
            _tickets.CountAheadAsync(ticketId, Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult(0));
            var result = await service.NotifyAsync(ticketId, force: false);
            result.Should().NotBeNull();
            result!.Id.Should().Be(ticket.Id);
            result.Status.Should().Be(TicketStatus.Notified.ToString());
            result.Ahead.Should().Be(0);
            result.CustomerFullName.Should().BeEmpty();
            ticket.Status.Should().Be(TicketStatus.Notified);
            ticket.NotifiedAt.Should().NotBeNull();
            await _notifications.Received(1).NotifyTicketUpdatedAsync(ticket, 0, NotificationType.Manual, Arg.Any<CancellationToken>());
        }

        // Ensures returned dto clears CustomerFullName and sets Ahead from repository count
        [Fact]
        public async Task NotifyAsync_ReturnedDto_HasClearedCustomerFullNameAndCorrectAhead()
        {
            var service = new TicketService(_customers, _tickets, _mapper, _notifications, _serviceState);
            int ticketId = _faker.Random.Int(1, 1000);
            var userName = _faker.Name.FullName();
            var ticket = new Ticket
            {
                Id = ticketId,
                PeopleCount = 4,
                Position = 5,
                Status = TicketStatus.Waiting,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                Customer = new Customer { FullName = userName }
            };
            _tickets.GetByIdAsync(ticketId, Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult<Ticket?>(ticket));
            int aheadCount = 3;
            _tickets.CountAheadAsync(ticketId, Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult(aheadCount));
            var result = await service.NotifyAsync(ticketId, force: false);
            result.Should().NotBeNull();
            result!.Id.Should().Be(ticket.Id);
            result.Status.Should().Be(TicketStatus.Notified.ToString());
            result.Ahead.Should().Be(aheadCount);
            result.CustomerFullName.Should().BeEmpty();
        }
    }
}
