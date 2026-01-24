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
    public class CancelByPublicIdTest
    {
        private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();
        private readonly ITicketRepository _tickets = Substitute.For<ITicketRepository>();
        private readonly INotificationService _notifications = Substitute.For<INotificationService>();
        private readonly IServiceStateService _serviceState = Substitute.For<IServiceStateService>();
        private readonly IMapper _mapper = Substitute.For<IMapper>();

        private readonly Faker _faker = new("es");


        public CancelByPublicIdTest()
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

            _mapper.Map<TicketDetailDto>(Arg.Any<object>())
                   .Returns(ci =>
                   {
                       var t = (Ticket)ci.Arg<object>();
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
            var service = new TicketService(_customers, _tickets, _mapper, _notifications, _serviceState);
            var ticketPublicId = "test-public-id";
            _tickets.GetByPublicIdAsync(ticketPublicId, Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult<Ticket?>(null));
            var result = await service.CancelByPublicIdAsync(ticketPublicId);
            result.Should().BeNull();
            await _tickets.DidNotReceive().GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
            _tickets.DidNotReceive().Update(Arg.Any<Ticket>());
            await _tickets.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
            await _notifications.DidNotReceive().NotifyTicketUpdatedAsync(Arg.Any<Ticket>(), Arg.Any<int>(), Arg.Any<NotificationType>(), Arg.Any<CancellationToken>());
        }

        //If ticket found and is waiting, cancels it and returns the updated dto
        [Fact]
        public async Task TicketFoundAndWaiting_CancelsAndReturnsDto()
        {
            var service = new TicketService(_customers, _tickets, _mapper, _notifications, _serviceState);
            var ticketPublicId = "test-public-id";

            var ticket = new Ticket
            {
                Id = 1,
                PublicId = ticketPublicId,
                PeopleCount = 2,
                Position = 5,
                Status = TicketStatus.Waiting,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                Customer = new Customer { FullName = "John Doe" }
            };

            _tickets.GetByPublicIdAsync(ticketPublicId, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<Ticket?>(ticket));

            _tickets.GetByIdAsync(ticket.Id, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<Ticket?>(ticket));

            _tickets.CountAheadAsync(ticket.Id, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(3));

            _tickets.ListActiveBehindAsync(ticket.Position, 500, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new List<Ticket>()));


            var result = await service.CancelByPublicIdAsync(ticketPublicId);

            result.Should().NotBeNull();
            result!.Status.Should().Be(TicketStatus.Cancelled.ToString());

            await _tickets.Received(1).GetByPublicIdAsync(ticketPublicId, Arg.Any<CancellationToken>());
            await _tickets.Received(1).GetByIdAsync(ticket.Id, Arg.Any<CancellationToken>());
            _tickets.Received(1).Update(Arg.Is<Ticket>(t => t.Status == TicketStatus.Cancelled));
            await _tickets.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

            await _notifications.Received(1).BroadcastTicketUpdatedAsync(
                Arg.Any<Ticket>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>());
        }


        //If invalid transition from inactive status, throws InvalidOperationException
        [Fact]
        public async Task InvalidTransitionFromInactiveStatus_ThrowsInvalidOperationException()
        {
            var service = new TicketService(_customers, _tickets, _mapper, _notifications, _serviceState);
            var ticketPublicId = "test-public-id";
            var target = TicketStatus.Cancelled;
            var ticket = new Ticket
            {
                Id = 1,
                PublicId = ticketPublicId,
                PeopleCount = 2,
                Position = 5,
                Status = TicketStatus.Confirmed,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                Customer = new Customer { FullName = "John Doe" }
            };
            _tickets.GetByPublicIdAsync(ticketPublicId, Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult<Ticket?>(ticket));
            _tickets.GetByIdAsync(ticket.Id, Arg.Any<CancellationToken>())
                    .Returns(ticket);
            _tickets.ListActiveBehindAsync(ticket.Position, 500, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new List<Ticket>()));

            Func<Task> result = async () => await service.CancelByPublicIdAsync(ticketPublicId);
            await result.Should().ThrowAsync<InvalidOperationException>()
                     .WithMessage($"Invalid transition from {ticket.Status} to {target}");
            await _tickets.Received(1).GetByPublicIdAsync(ticketPublicId, Arg.Any<CancellationToken>());
            _tickets.DidNotReceive().Update(Arg.Any<Ticket>());
            await _tickets.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
            await _notifications.DidNotReceive().NotifyTicketUpdatedAsync(Arg.Any<Ticket>(), Arg.Any<int>(), Arg.Any<NotificationType>(), Arg.Any<CancellationToken>());
        }
    }
}
