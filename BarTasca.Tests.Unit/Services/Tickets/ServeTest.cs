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
    public class ServeTest
    {
        private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();
        private readonly ITicketRepository _tickets = Substitute.For<ITicketRepository>();
        private readonly INotificationService _notifications = Substitute.For<INotificationService>();
        private readonly IMapper _mapper = Substitute.For<IMapper>();

        private readonly Faker _faker = new("es");


        public ServeTest()
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
        public async Task ServeAsync_ticket_not_found_returns_null_and_does_nothing_else()
        {
            var svc = new TicketService(_customers, _tickets, _mapper, _notifications);
            var ticketId = 1;
            _tickets.GetByIdAsync(ticketId, Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult<Ticket?>(null));
            var result = await svc.ServeAsync(ticketId);
            result.Should().BeNull();
            await _tickets.Received(1).GetByIdAsync(ticketId, Arg.Any<CancellationToken>());
            await _tickets.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
            await _tickets.DidNotReceive().CountAheadAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
            await _tickets.DidNotReceive().ListActiveBehindAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
            await _notifications.DidNotReceive().BroadcastTicketUpdatedAsync(Arg.Any<Ticket>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
            await _notifications.DidNotReceive().NotifyTicketUpdatedAsync(Arg.Any<Ticket>(), Arg.Any<int>(), Arg.Any<NotificationType>(), Arg.Any<CancellationToken>());
        }

        // If valid transition, updates status, sets ConfirmedAt and saves changes
        [Fact]
        public async Task ServeAsync_valid_transition_updates_status_sets_confirmedat_and_saves_changes()
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
                       Status = TicketStatus.Confirmed.ToString()
                   });

            _notifications.BroadcastTicketUpdatedAsync(Arg.Any<Ticket>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                          .Returns(Task.CompletedTask);

            var result = await svc.ServeAsync(ticketId);

            result.Should().NotBeNull();
            result!.Status.Should().Be(TicketStatus.Confirmed.ToString());
            ticket.ConfirmedAt.Should().NotBeNull();

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
        public async Task ServeAsync_invalid_transition_from_inactive_status_throws_invalidoperationexception()
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

            Func<Task> act = async () => { await svc.ServeAsync(ticketId); };

            await act.Should().ThrowAsync<InvalidOperationException>()
                     .WithMessage($"Invalid transition from {ticket.Status} to {TicketStatus.Confirmed}");

            await _tickets.Received(1).GetByIdAsync(ticketId, Arg.Any<CancellationToken>());
            _tickets.DidNotReceive().Update(Arg.Any<Ticket>());
            await _tickets.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());

            await _tickets.DidNotReceive().CountAheadAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
            await _tickets.DidNotReceive().ListActiveBehindAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());

            await _notifications.DidNotReceive()
                .BroadcastTicketUpdatedAsync(Arg.Any<Ticket>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
            await _notifications.DidNotReceive()
                .NotifyTicketUpdatedAsync(Arg.Any<Ticket>(), Arg.Any<int>(), Arg.Any<NotificationType>(), Arg.Any<CancellationToken>());
        }


        // If ticket already Confirmed, does not save changes but still broadcasts update
        [Fact]
        public async Task ServeAsync_ticket_already_confirmed_does_not_save_changes_but_still_broadcasts_update()
        {
            var svc = new TicketService(_customers, _tickets, _mapper, _notifications);
            var ticketId = 1;
            var ticket = new Ticket
            {
                Id = ticketId,
                Status = TicketStatus.Confirmed,
                Position = 1,
                Customer = new Customer { FullName = "John Doe" }
            };
            _tickets.GetByIdAsync(ticketId, Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult<Ticket?>(ticket));
            _tickets.CountAheadAsync(ticketId, Arg.Any<CancellationToken>())
                    .Returns(0);
            _tickets.ListActiveBehindAsync(ticket.Position, 500, Arg.Any<CancellationToken>())
                    .Returns(new List<Ticket>());
            var result = await svc.ServeAsync(ticketId);
            result.Should().NotBeNull();
            result!.Status.Should().Be(TicketStatus.Confirmed.ToString());
            await _tickets.Received(1).GetByIdAsync(ticketId, Arg.Any<CancellationToken>());
            await _tickets.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
            await _notifications.Received(1)
                .BroadcastTicketUpdatedAsync(ticket, 0, Arg.Any<CancellationToken>());
            await _notifications.DidNotReceive()
                .NotifyTicketUpdatedAsync(Arg.Any<Ticket>(), Arg.Any<int>(), Arg.Any<NotificationType>(), Arg.Any<CancellationToken>());
        }

        //------------------------------Revisar----------------------------

        // If affected ticket reaches ahead=3, sends Reminder notification
        [Fact]
        public async Task ServeAsync_affected_ticket_reaches_ahead_3_sends_reminder_notification()
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

            var affectedTicket = new Ticket
            {
                Id = 2,
                Status = TicketStatus.Waiting,
                Position = 5,
                Customer = new Customer { FullName = "Jane Smith" }
            };

            _tickets.CountAheadAsync(affectedTicket.Id, Arg.Any<CancellationToken>())
                   .Returns(3);

            _tickets.ListActiveBehindAsync(ticket.Position, 500, Arg.Any<CancellationToken>())
                    .Returns(new List<Ticket> { affectedTicket });

            _mapper.Map<TicketDetailDto>(ticket)
                   .Returns(new TicketDetailDto
                   {
                       Id = ticket.Id,
                       Status = TicketStatus.Confirmed.ToString()
                   });

            _notifications.BroadcastTicketUpdatedAsync(Arg.Any<Ticket>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                          .Returns(Task.CompletedTask);

            _notifications.NotifyTicketUpdatedAsync(Arg.Any<Ticket>(), Arg.Any<int>(), Arg.Any<NotificationType>(), Arg.Any<CancellationToken>())
                          .Returns(Task.CompletedTask);

            var result = await svc.ServeAsync(ticketId);

            result.Should().NotBeNull();
            result!.Status.Should().Be(TicketStatus.Confirmed.ToString());
            ticket.ConfirmedAt.Should().NotBeNull();

            await _notifications.Received(1)
                .BroadcastTicketUpdatedAsync(ticket, 0, Arg.Any<CancellationToken>());

            await _notifications.Received(1)
                .NotifyTicketUpdatedAsync(affectedTicket, 3, NotificationType.Reminder, Arg.Any<CancellationToken>());

            await _notifications.DidNotReceive()
                .NotifyTicketUpdatedAsync(Arg.Any<Ticket>(), Arg.Any<int>(), NotificationType.Turn, Arg.Any<CancellationToken>());

            await _notifications.DidNotReceive()
                .BroadcastTicketUpdatedAsync(affectedTicket, Arg.Any<int>(), Arg.Any<CancellationToken>());
        }

        // If affected ticket reaches ahead<=1, updates to Notified if needed and sends Turn notification
        [Fact]
        public async Task ServeAsync_affected_ticket_reaches_ahead_leq_1_updates_to_notified_if_needed_and_sends_turn_notification()
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

            var affectedTicket = new Ticket
            {
                Id = 2,
                Status = TicketStatus.Waiting,
                Position = 2,
                Customer = new Customer { FullName = "Jane Smith" }
            };

            _tickets.CountAheadAsync(affectedTicket.Id, Arg.Any<CancellationToken>())
                   .Returns(1);

            _tickets.ListActiveBehindAsync(ticket.Position, 500, Arg.Any<CancellationToken>())
                    .Returns(new List<Ticket> { affectedTicket });

            _tickets.GetByIdAsync(affectedTicket.Id, Arg.Any<CancellationToken>())
                    .Returns(affectedTicket);

            _mapper.Map<TicketDetailDto>(ticket)
                   .Returns(new TicketDetailDto
                   {
                       Id = ticket.Id,
                       Status = TicketStatus.Confirmed.ToString()
                   });

            _notifications.BroadcastTicketUpdatedAsync(Arg.Any<Ticket>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                          .Returns(Task.CompletedTask);

            _notifications.NotifyTicketUpdatedAsync(Arg.Any<Ticket>(), Arg.Any<int>(), Arg.Any<NotificationType>(), Arg.Any<CancellationToken>())
                          .Returns(Task.CompletedTask);

            var result = await svc.ServeAsync(ticketId);

            result.Should().NotBeNull();
            result!.Status.Should().Be(TicketStatus.Confirmed.ToString());
            ticket.ConfirmedAt.Should().NotBeNull();

            affectedTicket.Status.Should().Be(TicketStatus.Notified);
            affectedTicket.NotifiedAt.Should().NotBeNull();

            await _notifications.Received(1).NotifyTicketUpdatedAsync(
                Arg.Is<Ticket>(t => t.Id == affectedTicket.Id),
                1,
                NotificationType.Turn,
                Arg.Any<CancellationToken>());

            await _notifications.DidNotReceive()
                .NotifyTicketUpdatedAsync(Arg.Any<Ticket>(), Arg.Any<int>(), NotificationType.Reminder, Arg.Any<CancellationToken>());
        }

        // If affected ticket doesn't hit thresholds, broadcasts update only
        [Fact]
        public async Task ServeAsync_affected_ticket_doesnt_hit_thresholds_broadcasts_update_only()
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

            var affectedTicket = new Ticket
            {
                Id = 2,
                Status = TicketStatus.Waiting,
                Position = 2,
                Customer = new Customer { FullName = "Jane Smith" }
            };

            _tickets.CountAheadAsync(affectedTicket.Id, Arg.Any<CancellationToken>())
                   .Returns(5);

            _tickets.ListActiveBehindAsync(ticket.Position, 500, Arg.Any<CancellationToken>())
                    .Returns(new List<Ticket> { affectedTicket });

            _mapper.Map<TicketDetailDto>(ticket)
                   .Returns(new TicketDetailDto
                   {
                       Id = ticket.Id,
                       Status = TicketStatus.Confirmed.ToString()
                   });

            _notifications.BroadcastTicketUpdatedAsync(Arg.Any<Ticket>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                          .Returns(Task.CompletedTask);

            _notifications.NotifyTicketUpdatedAsync(Arg.Any<Ticket>(), Arg.Any<int>(), Arg.Any<NotificationType>(), Arg.Any<CancellationToken>())
                          .Returns(Task.CompletedTask);

            var result = await svc.ServeAsync(ticketId);

            result.Should().NotBeNull();
            result!.Status.Should().Be(TicketStatus.Confirmed.ToString());
            ticket.ConfirmedAt.Should().NotBeNull();

            await _notifications.Received(1)
                .BroadcastTicketUpdatedAsync(affectedTicket, 5, Arg.Any<CancellationToken>());

            await _notifications.DidNotReceive()
                .NotifyTicketUpdatedAsync(Arg.Any<Ticket>(), Arg.Any<int>(), Arg.Any<NotificationType>(), Arg.Any<CancellationToken>());
        }
    }
}
