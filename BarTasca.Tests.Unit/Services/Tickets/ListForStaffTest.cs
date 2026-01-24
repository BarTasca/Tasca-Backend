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
    public class ListForStaffTest
    {
        private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();
        private readonly ITicketRepository _tickets = Substitute.For<ITicketRepository>();
        private readonly INotificationService _notifications = Substitute.For<INotificationService>();
        private readonly IServiceStateService _serviceState = Substitute.For<IServiceStateService>();   
        private readonly IMapper _mapper = Substitute.For<IMapper>();

        private readonly Faker _faker = new("es");


        public ListForStaffTest()
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

        // If status="waiting", queries repository with [Waiting] and returns mapped list
        [Fact]
        public async Task StatusWaiting_QueriesWaitingStatus()
        {
            var service = new TicketService(_customers, _tickets, _mapper, _notifications, _serviceState);
            var tickets = new List<Ticket>
            {
                new Ticket
                {
                    Id = 1,
                    PeopleCount = 4,
                    Position = 1,
                    Status = TicketStatus.Waiting,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                    Customer = new Customer { FullName = "John Doe" }
                },
                new Ticket
                {
                    Id = 2,
                    PeopleCount = 2,
                    Position = 2,
                    Status = TicketStatus.Waiting,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                    Customer = new Customer { FullName = "Jane Smith" }
                },
                new Ticket
                {
                    Id = 3,
                    PeopleCount = 3,
                    Position = 3,
                    Status = TicketStatus.Notified,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-2),
                    Customer = new Customer { FullName = "Bob Johnson" }
                }
            };

            var waitingTickets = tickets.Where(t => t.Status == TicketStatus.Waiting).ToList();

            _tickets.ListByStatusesAsync(Arg.Is<TicketStatus[]>(s => s.SequenceEqual(new[] { TicketStatus.Waiting })),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<List<Ticket>>(waitingTickets));
            var result = await service.ListForStaffAsync("waiting");
            result.Should().BeEquivalentTo(new[]
            {
                new TicketStaffListDto { Id = 1, PeopleCount = 4, Position = 1, Status = "Waiting", CustomerFullName = "John Doe" },
                new TicketStaffListDto { Id = 2, PeopleCount = 2, Position = 2, Status = "Waiting", CustomerFullName = "Jane Smith" },
            }, opts => opts.Excluding(x => x.CreatedAt));
        }

        // If status="notified", queries repository with [Notified] and returns mapped list
        [Fact]
        public async Task StatusNotified_QueriesNotifiedStatus()
        {
            var service = new TicketService(_customers, _tickets, _mapper, _notifications, _serviceState);
            var tickets = new List<Ticket>
            {
                new Ticket
                {
                    Id = 1,
                    PeopleCount = 4,
                    Position = 1,
                    Status = TicketStatus.Waiting,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                    Customer = new Customer { FullName = "John Doe" }
                },
                new Ticket
                {
                    Id = 2,
                    PeopleCount = 2,
                    Position = 2,
                    Status = TicketStatus.Notified,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                    Customer = new Customer { FullName = "Jane Smith" }
                },
                new Ticket
                {
                    Id = 3,
                    PeopleCount = 3,
                    Position = 3,
                    Status = TicketStatus.Notified,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-2),
                    Customer = new Customer { FullName = "Bob Johnson" }
                }
            };
            var notifiedTickets = tickets.Where(t => t.Status == TicketStatus.Notified).ToList();
            _tickets.ListByStatusesAsync(Arg.Is<TicketStatus[]>(s => s.SequenceEqual(new[] { TicketStatus.Notified })),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<List<Ticket>>(notifiedTickets));
            var result = await service.ListForStaffAsync("notified");
            result.Should().BeEquivalentTo(new[]
            {
                new TicketStaffListDto { Id = 2, PeopleCount = 2, Position = 2, Status = "Notified", CustomerFullName = "Jane Smith" },
                new TicketStaffListDto { Id = 3, PeopleCount = 3, Position = 3, Status = "Notified", CustomerFullName = "Bob Johnson" },
            }, opts => opts.Excluding(x => x.CreatedAt));
        }

        // If status="active" (default), queries repository with [Waiting, Notified] and returns mapped list
        [Fact]
        public async Task StatusActive_QueriesWaitingAndNotifiedStatuses()
        {
            var service = new TicketService(_customers, _tickets, _mapper, _notifications, _serviceState);
            var tickets = new List<Ticket>
            {
                new Ticket
                {
                    Id = 1,
                    PeopleCount = 4,
                    Position = 1,
                    Status = TicketStatus.Waiting,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                    Customer = new Customer { FullName = "John Doe" }
                },
                new Ticket
                {
                    Id = 2,
                    PeopleCount = 2,
                    Position = 2,
                    Status = TicketStatus.Notified,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                    Customer = new Customer { FullName = "Jane Smith" }
                },
                new Ticket
                {
                    Id = 3,
                    PeopleCount = 3,
                    Position = 3,
                    Status = TicketStatus.Confirmed,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-2),
                    Customer = new Customer { FullName = "Bob Johnson" }
                }
            };
            var activeTickets = tickets.Where(t => t.Status == TicketStatus.Waiting || t.Status == TicketStatus.Notified).ToList();
            _tickets.ListByStatusesAsync(Arg.Is<TicketStatus[]>(s => s.SequenceEqual(new[] { TicketStatus.Waiting, TicketStatus.Notified })),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<List<Ticket>>(activeTickets));
            var result = await service.ListForStaffAsync();
            result.Should().BeEquivalentTo(new[]
            {
                new TicketStaffListDto { Id = 1, PeopleCount = 4, Position = 1, Status = "Waiting", CustomerFullName = "John Doe" },
                new TicketStaffListDto { Id = 2, PeopleCount = 2, Position = 2, Status = "Notified", CustomerFullName = "Jane Smith" },
            }, opts => opts.Excluding(x => x.CreatedAt));
        }

        // If status is unknown, falls back to "active" behavior ([Waiting, Notified])
        [Fact]
        public async Task StatusUnknown_FallsBackToActive()
        {
            var service = new TicketService(_customers, _tickets, _mapper, _notifications, _serviceState);
            var tickets = new List<Ticket>
            {
                new Ticket
                {
                    Id = 1,
                    PeopleCount = 4,
                    Position = 1,
                    Status = TicketStatus.Waiting,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                    Customer = new Customer { FullName = "John Doe" }
                },
                new Ticket
                {
                    Id = 2,
                    PeopleCount = 2,
                    Position = 2,
                    Status = TicketStatus.Notified,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                    Customer = new Customer { FullName = "Jane Smith" }
                },
                new Ticket
                {
                    Id = 3,
                    PeopleCount = 3,
                    Position = 3,
                    Status = TicketStatus.Confirmed,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-2),
                    Customer = new Customer { FullName = "Bob Johnson" }
                }
            };
            var activeTickets = tickets.Where(t => t.Status == TicketStatus.Waiting || t.Status == TicketStatus.Notified).ToList();
            _tickets.ListByStatusesAsync(Arg.Is<TicketStatus[]>(s => s.SequenceEqual(new[] { TicketStatus.Waiting, TicketStatus.Notified })),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<List<Ticket>>(activeTickets));
            var result = await service.ListForStaffAsync();
            result.Should().BeEquivalentTo(new[]
            {
                new TicketStaffListDto { Id = 1, PeopleCount = 4, Position = 1, Status = "Waiting", CustomerFullName = "John Doe" },
                new TicketStaffListDto { Id = 2, PeopleCount = 2, Position = 2, Status = "Notified", CustomerFullName = "Jane Smith" },
            }, opts => opts.Excluding(x => x.CreatedAt));
        }

        // If status="all", queries repository with all TicketStatus values and returns mapped list
        [Fact]
        public async Task StatusAll_QueriesAllStatuses()
        {
            var service = new TicketService(_customers, _tickets, _mapper, _notifications, _serviceState);
            var tickets = new List<Ticket>
            {
                new Ticket
                {
                    Id = 1,
                    PeopleCount = 4,
                    Position = 1,
                    Status = TicketStatus.Waiting,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                    Customer = new Customer { FullName = "John Doe" }
                },
                new Ticket
                {
                    Id = 2,
                    PeopleCount = 2,
                    Position = 2,
                    Status = TicketStatus.Notified,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                    Customer = new Customer { FullName = "Jane Smith" }
                },
                new Ticket
                {
                    Id = 3,
                    PeopleCount = 3,
                    Position = 3,
                    Status = TicketStatus.Confirmed,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-2),
                    Customer = new Customer { FullName = "Bob Johnson" }
                }
            };
            _tickets.ListByStatusesAsync(Arg.Is<TicketStatus[]>(s => s.Length == Enum.GetValues<TicketStatus>().Length),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<List<Ticket>>(tickets));
            var result = await service.ListForStaffAsync("all");
            result.Should().BeEquivalentTo(new[]
            {
                new TicketStaffListDto { Id = 1, PeopleCount = 4, Position = 1, Status = "Waiting", CustomerFullName = "John Doe" },
                new TicketStaffListDto { Id = 2, PeopleCount = 2, Position = 2, Status = "Notified", CustomerFullName = "Jane Smith" },
                new TicketStaffListDto { Id = 3, PeopleCount = 3, Position = 3, Status = "Confirmed", CustomerFullName = "Bob Johnson" },
            }, opts => opts.Excluding(x => x.CreatedAt));
        }

        // If status is case-insensitive (e.g. "WaItInG"), still maps to correct statuses
        [Fact]
        public async Task StatusCaseInsensitive_MapsCorrectly()
        {
            var service = new TicketService(_customers, _tickets, _mapper, _notifications, _serviceState);
            var tickets = new List<Ticket>
            {
                new Ticket
                {
                    Id = 1,
                    PeopleCount = 4,
                    Position = 1,
                    Status = TicketStatus.Waiting,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                    Customer = new Customer { FullName = "John Doe" }
                },
                new Ticket
                {
                    Id = 2,
                    PeopleCount = 2,
                    Position = 2,
                    Status = TicketStatus.Waiting,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                    Customer = new Customer { FullName = "Jane Smith" }
                }
            };
            var waitingTickets = tickets.Where(t => t.Status == TicketStatus.Waiting).ToList();
            _tickets.ListByStatusesAsync(Arg.Is<TicketStatus[]>(s => s.SequenceEqual(new[] { TicketStatus.Waiting })),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<List<Ticket>>(waitingTickets));
            var result = await service.ListForStaffAsync("WaItInG");
            result.Should().BeEquivalentTo(new[]
            {
                new TicketStaffListDto { Id = 1, PeopleCount = 4, Position = 1, Status = "Waiting", CustomerFullName = "John Doe" },
                new TicketStaffListDto { Id = 2, PeopleCount = 2, Position = 2, Status = "Waiting", CustomerFullName = "Jane Smith" },
            }, opts => opts.Excluding(x => x.CreatedAt));
        }

        // If repository returns empty list, returns empty list
        [Fact]
        public async Task RepositoryReturnsEmptyList_ReturnsEmptyList()
        {
            var service = new TicketService(_customers, _tickets, _mapper, _notifications, _serviceState);
            _tickets.ListByStatusesAsync(Arg.Any<TicketStatus[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new List<Ticket>()));
            var result = await service.ListForStaffAsync("waiting");
            result.Should().BeEmpty();
        }

        // If mapping is correct, each dto contains Id, PeopleCount, Position, Status string, CreatedAt, and CustomerFullName from Customer
        [Fact]
        public async Task Mapping_IsCorrect()
        {
            var service = new TicketService(_customers, _tickets, _mapper, _notifications, _serviceState);
            var tickets = new List<Ticket>
            {
                new Ticket
                {
                    Id = 1,
                    PeopleCount = 4,
                    Position = 1,
                    Status = TicketStatus.Waiting,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                    Customer = new Customer { FullName = "John Doe" }
                }
            };
            _tickets.ListByStatusesAsync(Arg.Any<TicketStatus[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<List<Ticket>>(tickets));
            var result = await service.ListForStaffAsync("waiting");
            var dto = result.First();
            dto.Id.Should().Be(1);
            dto.PeopleCount.Should().Be(4);
            dto.Position.Should().Be(1);
            dto.Status.Should().Be("Waiting");
            dto.CreatedAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(-10), TimeSpan.FromSeconds(5));
            dto.CustomerFullName.Should().Be("John Doe");

        }
    }
}
