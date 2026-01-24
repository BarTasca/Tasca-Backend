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
    public class GetStatusAsyncTest
    {
        private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();
        private readonly ITicketRepository _tickets = Substitute.For<ITicketRepository>();
        private readonly INotificationService _notifications = Substitute.For<INotificationService>();
        private readonly IServiceStateService _serviceState = Substitute.For<IServiceStateService>();
        private readonly IMapper _mapper = Substitute.For<IMapper>();

        private readonly Faker _faker = new("es");


        public GetStatusAsyncTest()
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

        // If ticket not found by publicId, returns null and does nothing else
        [Fact]
        public async Task GetStatusAsync_TicketNotFound_ReturnsNull()
        {
            var service = new TicketService(_customers, _tickets, _mapper, _notifications, _serviceState);
            var publicId = "test-public-id";
            var ct = new CancellationTokenSource().Token;
            _tickets.GetByPublicIdAsync(publicId, ct)
                    .Returns(Task.FromResult<Ticket?>(null));
            
            var result = await service.GetStatusAsync(publicId, ct);
            
            result.Should().BeNull();
            await _tickets.Received(1).GetByPublicIdAsync(publicId, ct);
        }

        // If ticket found, calls CountAhead and returns dto with correct PublicId, Status, Ahead, Position, PeopleCount, CreatedAt and NotifiedAt
        [Fact]
        public async Task GetStatusAsync_TicketFound_ReturnsDtoWithCorrectProperties()
        {
            var service = new TicketService(_customers, _tickets, _mapper, _notifications, _serviceState);
            var publicId = "test-public-id";
            var ct = new CancellationTokenSource().Token;
            var ticket = new Ticket
            {
                Id = 1,
                PublicId = publicId,
                PeopleCount = 4,
                Position = 2,
                Status = TicketStatus.Notified,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                NotifiedAt = DateTime.UtcNow.AddMinutes(-5),
                Customer = new Customer { FullName = "John Doe" }
            };
            _tickets.GetByPublicIdAsync(publicId, ct)
                    .Returns(Task.FromResult<Ticket?>(ticket));
            _tickets.CountAheadAsync(ticket.Id, ct)
                    .Returns(Task.FromResult(1));
            var result = await service.GetStatusAsync(publicId, ct);
            result.Should().NotBeNull();
            result!.PublicId.Should().Be(publicId);
            result.Status.Should().Be(ticket.Status.ToString());
            result.Ahead.Should().Be(1);
            result.Position.Should().Be(ticket.Position);
            result.PeopleCount.Should().Be(ticket.PeopleCount);
            result.CreatedAt.Should().Be(ticket.CreatedAt);
            result.NotifiedAt.Should().Be(ticket.NotifiedAt);
            await _tickets.Received(1).GetByPublicIdAsync(publicId, ct);
            await _tickets.Received(1).CountAheadAsync(ticket.Id, ct);
        }

        // If ticket has NotifiedAt null, dto.NotifiedAt is null as well
        [Fact]
        public async Task GetStatusAsync_TicketWithNullNotifiedAt_ReturnsDtoWithNullNotifiedAt()
        {
            var service = new TicketService(_customers, _tickets, _mapper, _notifications, _serviceState);
            var publicId = "test-public-id";
            var ct = new CancellationTokenSource().Token;
            var ticket = new Ticket
            {
                Id = 1,
                PublicId = publicId,
                PeopleCount = 4,
                Position = 2,
                Status = TicketStatus.Waiting,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                NotifiedAt = null,
                Customer = new Customer { FullName = "John Doe" }
            };
            _tickets.GetByPublicIdAsync(publicId, ct)
                    .Returns(Task.FromResult<Ticket?>(ticket));
            _tickets.CountAheadAsync(ticket.Id, ct)
                    .Returns(Task.FromResult(1));
            var result = await service.GetStatusAsync(publicId, ct);
            result.Should().NotBeNull();
            result!.NotifiedAt.Should().BeNull();
            await _tickets.Received(1).GetByPublicIdAsync(publicId, ct);
            await _tickets.Received(1).CountAheadAsync(ticket.Id, ct);
        }

        // Ensures repository is called with the same publicId and cancellation token
        [Fact]
        public async Task GetStatusAsync_RepositoryCalledWithCorrectParameters()
        {
            var service = new TicketService(_customers, _tickets, _mapper, _notifications, _serviceState);
            var publicId = "test-public-id";
            var ct = new CancellationTokenSource().Token;
            _tickets.GetByPublicIdAsync(publicId, ct)
                    .Returns(Task.FromResult<Ticket?>(null));

            await service.GetStatusAsync(publicId, ct);

            await _tickets.Received(1).GetByPublicIdAsync(publicId, ct);
        }

    }
}
