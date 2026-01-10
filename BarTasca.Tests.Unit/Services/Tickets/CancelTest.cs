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
    public class CancelTest
    {
        private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();
        private readonly ITicketRepository _tickets = Substitute.For<ITicketRepository>();
        private readonly INotificationService _notifications = Substitute.For<INotificationService>();
        private readonly IMapper _mapper = Substitute.For<IMapper>();

        private readonly Faker _faker = new("es");


        public CancelTest()
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
        public async Task TicketNotFound_ReturnsNull()
        {
            var service = new TicketService(_customers, _tickets, _mapper, _notifications);
            var ticketId = _faker.Random.Int(1, 1000);
            _tickets.GetByIdAsync(ticketId, Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult<Ticket?>(null));
            var result = await service.CancelAsync(ticketId);
            result.Should().BeNull();
            await _tickets.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
            await _notifications.DidNotReceive().NotifyTicketUpdatedAsync(Arg.Any<Ticket>(), Arg.Any<int>(), Arg.Any<NotificationType>(), Arg.Any<CancellationToken>());
        }

        // If valid transition from active ticket, updates status to Cancelled and saves changes
        [Fact]
        public async Task ValidTransitionFromActive_UpdatesStatusAndSaves()
        {
            var svc = new TicketService(_customers, _tickets, _mapper, _notifications);

            var ticketId = 1;
            var ticket = new Ticket
            {
                Id = ticketId,
                Status = TicketStatus.Notified,
                Position = 1,
                Customer = new Customer { FullName = "John Doe" }
            };

            _tickets.GetByIdAsync(ticketId, Arg.Any<CancellationToken>())
                    .Returns(ticket);

            _tickets.SaveChangesAsync(Arg.Any<CancellationToken>())
                    .Returns(1);

            _tickets.CountAheadAsync(ticketId, Arg.Any<CancellationToken>())
                    .Returns(0);

            _tickets.ListActiveBehindAsync(ticket.Position, 500, Arg.Any<CancellationToken>())
                    .Returns(new List<Ticket>());

            _mapper.Map<TicketDetailDto>(ticket)
                   .Returns(new TicketDetailDto
                   {
                       Id = ticket.Id,
                       Status = TicketStatus.Cancelled.ToString()
                   });

            _notifications.BroadcastTicketUpdatedAsync(Arg.Any<Ticket>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                          .Returns(Task.CompletedTask);

            var result = await svc.CancelAsync(ticketId);

            result.Should().NotBeNull();
            result!.Status.Should().Be(TicketStatus.Cancelled.ToString());
            ticket.ConfirmedAt.Should().BeNull();

            await _tickets.Received(1).GetByIdAsync(ticketId, Arg.Any<CancellationToken>());
            _tickets.Received(1).Update(ticket);
            await _tickets.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

            await _notifications.Received(1)
                .BroadcastTicketUpdatedAsync(ticket, 0, Arg.Any<CancellationToken>());
            await _notifications.DidNotReceive()
                .NotifyTicketUpdatedAsync(Arg.Any<Ticket>(), Arg.Any<int>(), Arg.Any<NotificationType>(), Arg.Any<CancellationToken>());
        }

        // If invalid transition from inactive status, throws InvalidOperationException
        [Fact]
        public async Task InvalidTransitionFromInactive_ThrowsException()
        {
            var svc = new TicketService(_customers, _tickets, _mapper, _notifications);
            var ticketId = 1;
            var target = TicketStatus.Cancelled;
            var ticket = new Ticket
            {
                Id = ticketId,
                Status = TicketStatus.Confirmed,
                Position = 1,
                Customer = new Customer { FullName = "John Doe" }
            };
            _tickets.GetByIdAsync(ticketId, Arg.Any<CancellationToken>())
                    .Returns(ticket);
            Func<Task> act = async () => await svc.CancelAsync(ticketId);
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage($"Invalid transition from {ticket.Status} to {target}");
            await _tickets.Received(1).GetByIdAsync(ticketId, Arg.Any<CancellationToken>());
            _tickets.DidNotReceive().Update(Arg.Any<Ticket>());
            await _tickets.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
            await _notifications.DidNotReceive()
                .BroadcastTicketUpdatedAsync(Arg.Any<Ticket>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
            await _notifications.DidNotReceive()
                .NotifyTicketUpdatedAsync(Arg.Any<Ticket>(), Arg.Any<int>(), Arg.Any<NotificationType>(), Arg.Any<CancellationToken>());
        }

        // If ticket already Cancelled, does not save changes but still broadcasts update
        [Fact]
        public async Task AlreadyCancelled_DoesNotSaveButBroadcasts()
        {
            var svc = new TicketService(_customers, _tickets, _mapper, _notifications);
            var ticketId = 1;
            var ticket = new Ticket
            {
                Id = ticketId,
                Status = TicketStatus.Cancelled,
                Position = 1,
                Customer = new Customer { FullName = "John Doe" }
            };
            _tickets.GetByIdAsync(ticketId, Arg.Any<CancellationToken>())
                    .Returns(ticket);
            _tickets.CountAheadAsync(ticketId, Arg.Any<CancellationToken>())
                    .Returns(0);
            _tickets.ListActiveBehindAsync(ticket.Position, 500, Arg.Any<CancellationToken>())
                    .Returns(new List<Ticket>());
            _mapper.Map<TicketDetailDto>(ticket)
                   .Returns(new TicketDetailDto
                   {
                       Id = ticket.Id,
                       Status = TicketStatus.Cancelled.ToString()
                   });
            _notifications.BroadcastTicketUpdatedAsync(Arg.Any<Ticket>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                          .Returns(Task.CompletedTask);
            var result = await svc.CancelAsync(ticketId);
            result.Should().NotBeNull();
            result!.Status.Should().Be(TicketStatus.Cancelled.ToString());
            await _tickets.Received(1).GetByIdAsync(ticketId, Arg.Any<CancellationToken>());
            _tickets.DidNotReceive().Update(Arg.Any<Ticket>());
            await _tickets.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
            await _notifications.Received(1)
                .BroadcastTicketUpdatedAsync(ticket, 0, Arg.Any<CancellationToken>());
            await _notifications.DidNotReceive()
                .NotifyTicketUpdatedAsync(Arg.Any<Ticket>(), Arg.Any<int>(), Arg.Any<NotificationType>(), Arg.Any<CancellationToken>());
        }

    }
}
