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
    public class CreateSingleActiveTests
    {
        private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();
        private readonly ITicketRepository _tickets = Substitute.For<ITicketRepository>();
        private readonly INotificationService _notifications = Substitute.For<INotificationService>();
        private readonly IMapper _mapper = Substitute.For<IMapper>();

        private readonly Faker _faker = new("es");

        public CreateSingleActiveTests()
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

        [Fact]
        //Early return if an active ticket exists
        public async Task Existing_active_ticket_returns_existing_without_creating_or_notifying()
        {
            var svc = new TicketService(_customers, _tickets, _mapper, _notifications);

            var phone = "600999888";
            var existing = new Ticket
            {
                Id = 100,
                PublicId = "ULID-EXISTENTE",
                CustomerId = 7,
                Customer = new Customer { Id = 7, FullName = "Cliente Existente", Phone = phone },
                PeopleCount = 2,
                Position = 5,
                Status = TicketStatus.Waiting
            };

            _tickets.GetActiveByPhoneAsync(phone, Arg.Any<CancellationToken>()).Returns(existing);
            _tickets.CountAheadAsync(existing.Id, Arg.Any<CancellationToken>()).Returns(3);

            var dto = new CreateTicketDto
            {
                FullName = "Ignorado",
                Phone = phone,
                PeopleCount = 4
            };

            var result = await svc.CreateAsync(dto, CancellationToken.None);

            await _customers.DidNotReceive().AddAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>());
            await _tickets.DidNotReceive().AddAsync(Arg.Any<Ticket>(), Arg.Any<CancellationToken>());

            await _notifications.DidNotReceive()
                .NotifyTicketCreatedAsync(Arg.Any<Ticket>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
            await _notifications.DidNotReceive()
                .NotifyTicketUpdatedAsync(Arg.Any<Ticket>(), Arg.Any<int>(), Arg.Any<NotificationType>(), Arg.Any<CancellationToken>());

            result.Should().NotBeNull();
            result!.PublicId.Should().Be(existing.PublicId);
            result.Status.Should().Be("Waiting");
            result.Position.Should().Be(existing.Position);
            result.Ahead.Should().Be(3);
            result.CustomerFullName.Should().BeEmpty();
        }


        [Fact]
        //No Active ticket
        //A: New customer
        public async Task New_customer_without_active_ticket_creates_customer_and_ticket_and_notifies_created()
        {
            var svc = new TicketService(_customers, _tickets, _mapper, _notifications);

            var dto = new CreateTicketDto
            {
                FullName = _faker.Person.FullName,
                Phone = "600123123",
                PeopleCount = 3
            };

            _tickets.GetActiveByPhoneAsync(dto.Phone, Arg.Any<CancellationToken>()).Returns((Ticket?)null);
            _customers.GetByPhoneAsync(dto.Phone, Arg.Any<CancellationToken>()).Returns((Customer?)null);

            Customer? capturedCustomer = null;
            _customers.When(x => x.AddAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>()))
                      .Do(ci =>
                      {
                          capturedCustomer = ci.Arg<Customer>();
                          capturedCustomer!.Id = 42;
                      });
            _customers.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

            _tickets.GetMaxWaitingPositionAsync(Arg.Any<CancellationToken>()).Returns(10);

            Ticket? capturedTicket = null;
            _tickets.When(x => x.AddAsync(Arg.Any<Ticket>(), Arg.Any<CancellationToken>()))
                    .Do(ti => capturedTicket = ti.Arg<Ticket>());
            _tickets.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

            _tickets.CountAheadAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(5);

            var result = await svc.CreateAsync(dto, CancellationToken.None);

            await _tickets.Received(1).GetActiveByPhoneAsync(dto.Phone, Arg.Any<CancellationToken>());
            await _customers.Received(1).GetByPhoneAsync(dto.Phone, Arg.Any<CancellationToken>());
            await _customers.Received(1).AddAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>());
            await _customers.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
            await _tickets.Received(1).AddAsync(Arg.Any<Ticket>(), Arg.Any<CancellationToken>());
            await _tickets.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

            capturedTicket.Should().NotBeNull();
            capturedTicket!.CustomerId.Should().Be(42);
            capturedTicket.PeopleCount.Should().Be(dto.PeopleCount);
            capturedTicket.Position.Should().Be(11);
            capturedTicket.Status.Should().Be(TicketStatus.Waiting);

            await _notifications.Received(1)
                .NotifyTicketCreatedAsync(capturedTicket, 5, Arg.Any<CancellationToken>());
            await _notifications.DidNotReceive()
                .NotifyTicketUpdatedAsync(Arg.Any<Ticket>(), Arg.Any<int>(), NotificationType.Reminder, Arg.Any<CancellationToken>());
            await _notifications.DidNotReceive()
                .NotifyTicketUpdatedAsync(Arg.Any<Ticket>(), Arg.Any<int>(), NotificationType.Turn, Arg.Any<CancellationToken>());

            result.Should().NotBeNull();
            result.Ahead.Should().Be(5);
            result.Status.Should().Be("Waiting");
            result.PeopleCount.Should().Be(dto.PeopleCount);
            result.Position.Should().Be(11);
            result.CustomerFullName.Should().BeEmpty();
        }



        [Fact]
        //No Active ticket
        //B: Existing customer
        public async Task Existing_customer_without_active_ticket_creates_new_ticket()
        {
            var svc = new TicketService(_customers, _tickets, _mapper, _notifications);

            var phone = "600555444";
            var existingCustomer = new Customer { Id = 77, FullName = "Cliente Conocido", Phone = phone };

            _tickets.GetActiveByPhoneAsync(phone, Arg.Any<CancellationToken>()).Returns((Ticket?)null);
            _customers.GetByPhoneAsync(phone, Arg.Any<CancellationToken>()).Returns(existingCustomer);

            _tickets.GetMaxWaitingPositionAsync(Arg.Any<CancellationToken>()).Returns(20);

            Ticket? capturedTicket = null;
            _tickets.When(x => x.AddAsync(Arg.Any<Ticket>(), Arg.Any<CancellationToken>()))
                    .Do(ti => capturedTicket = ti.Arg<Ticket>());
            _tickets.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

            _tickets.CountAheadAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(2);

            var dto = new CreateTicketDto
            {
                FullName = "Ignorado al existir cliente",
                Phone = phone,
                PeopleCount = 4
            };

            var result = await svc.CreateAsync(dto, CancellationToken.None);

            await _customers.DidNotReceive().AddAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>());
            await _tickets.Received(1).AddAsync(Arg.Any<Ticket>(), Arg.Any<CancellationToken>());
            await _tickets.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

            capturedTicket.Should().NotBeNull();
            capturedTicket!.CustomerId.Should().Be(77);
            capturedTicket.PeopleCount.Should().Be(dto.PeopleCount);
            capturedTicket.Position.Should().Be(21);
            capturedTicket.Status.Should().Be(TicketStatus.Waiting);

            await _notifications.Received(1)
                .NotifyTicketCreatedAsync(capturedTicket, 2, Arg.Any<CancellationToken>());
            await _notifications.Received(1)
                .NotifyTicketUpdatedAsync(capturedTicket, 2, NotificationType.Reminder, Arg.Any<CancellationToken>());
            await _notifications.DidNotReceive()
                .NotifyTicketUpdatedAsync(Arg.Any<Ticket>(), Arg.Any<int>(), NotificationType.Turn, Arg.Any<CancellationToken>());

            result.Should().NotBeNull();
            result!.Ahead.Should().Be(2);
            result.Status.Should().Be("Waiting");
            result.Position.Should().Be(21);
            result.PeopleCount.Should().Be(4);
            result.CustomerFullName.Should().BeEmpty();
        }

        //Position
        //A: normal position
        [Fact]
        public async Task Created_ticket_with_normal_position()
        {
            var svc = new TicketService(_customers, _tickets, _mapper, _notifications);
            var dto = new CreateTicketDto
            {
                FullName = _faker.Person.FullName,
                Phone = "600777666",
                PeopleCount = 2
            };
            _tickets.GetActiveByPhoneAsync(dto.Phone, Arg.Any<CancellationToken>()).Returns((Ticket?)null);
            _customers.GetByPhoneAsync(dto.Phone, Arg.Any<CancellationToken>()).Returns((Customer?)null);
            Customer? capturedCustomer = null;
            _customers.When(x => x.AddAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>()))
                      .Do(ci =>
                      {
                          capturedCustomer = ci.Arg<Customer>();
                          capturedCustomer!.Id = 88;
                      });
            _customers.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
            _tickets.GetMaxWaitingPositionAsync(Arg.Any<CancellationToken>()).Returns(5);
            Ticket? capturedTicket = null;
            _tickets.When(x => x.AddAsync(Arg.Any<Ticket>(), Arg.Any<CancellationToken>()))
                    .Do(ti => capturedTicket = ti.Arg<Ticket>());
            _tickets.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
            _tickets.CountAheadAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(4);
            var result = await svc.CreateAsync(dto, CancellationToken.None);
            capturedTicket.Should().NotBeNull();
            capturedTicket!.Position.Should().Be(6);
            capturedTicket.Status.Should().Be(TicketStatus.Waiting);
            result.Should().NotBeNull();
            result.Status.Should().Be("Waiting");
        }

        //Position
        //B: position 0
        [Fact]
        public async Task Created_ticket_with_position_zero()
        {
            var svc = new TicketService(_customers, _tickets, _mapper, _notifications);
            var dto = new CreateTicketDto
            {
                FullName = _faker.Person.FullName,
                Phone = "600111222",
                PeopleCount = 5
            };
            _tickets.GetActiveByPhoneAsync(dto.Phone, Arg.Any<CancellationToken>()).Returns((Ticket?)null);
            _customers.GetByPhoneAsync(dto.Phone, Arg.Any<CancellationToken>()).Returns((Customer?)null);
            Customer? capturedCustomer = null;
            _customers.When(x => x.AddAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>()))
                      .Do(ci =>
                      {
                          capturedCustomer = ci.Arg<Customer>();
                          capturedCustomer!.Id = 99;
                      });
            _customers.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
            _tickets.GetMaxWaitingPositionAsync(Arg.Any<CancellationToken>()).Returns(0);
            Ticket? capturedTicket = null;
            _tickets.When(x => x.AddAsync(Arg.Any<Ticket>(), Arg.Any<CancellationToken>()))
                    .Do(ti => capturedTicket = ti.Arg<Ticket>());
            _tickets.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
            _tickets.CountAheadAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(0);
            var result = await svc.CreateAsync(dto, CancellationToken.None);
            capturedTicket.Should().NotBeNull();
            capturedTicket!.Position.Should().Be(1);
            capturedTicket.Status.Should().Be(TicketStatus.Notified);
            result.Should().NotBeNull();
            result.Status.Should().Be("Notified");
        }

        //Ahead
        //A: Ahead 3
        [Fact]
        public async Task Created_ticket_with_ahead_three()
        {
            var svc = new TicketService(_customers, _tickets, _mapper, _notifications);
            var dto = new CreateTicketDto
            {
                FullName = _faker.Person.FullName,
                Phone = "600333444",
                PeopleCount = 2
            };
            _tickets.GetActiveByPhoneAsync(dto.Phone, Arg.Any<CancellationToken>()).Returns((Ticket?)null);
            _customers.GetByPhoneAsync(dto.Phone, Arg.Any<CancellationToken>()).Returns((Customer?)null);
            Customer? capturedCustomer = null;
            _customers.When(x => x.AddAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>()))
                      .Do(ci =>
                      {
                          capturedCustomer = ci.Arg<Customer>();
                          capturedCustomer!.Id = 55;
                      });
            _customers.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
            _tickets.GetMaxWaitingPositionAsync(Arg.Any<CancellationToken>()).Returns(3);
            Ticket? capturedTicket = null;
            _tickets.When(x => x.AddAsync(Arg.Any<Ticket>(), Arg.Any<CancellationToken>()))
                    .Do(ti => capturedTicket = ti.Arg<Ticket>());
            _tickets.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
            _tickets.CountAheadAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(3);
            var result = await svc.CreateAsync(dto, CancellationToken.None);
            capturedTicket.Should().NotBeNull();
            capturedTicket!.Position.Should().Be(4);
            capturedTicket.Status.Should().Be(TicketStatus.Waiting);
            result.Should().NotBeNull();
            result.Should().NotBeNull();
            result.Ahead.Should().Be(3);
            result.Status.Should().Be("Waiting");
        }

        //Ahead
        //B: Ahead 1    
        [Fact]
        public async Task Created_ticket_with_ahead_one()
        {
            var svc = new TicketService(_customers, _tickets, _mapper, _notifications);
            var dto = new CreateTicketDto
            {
                FullName = _faker.Person.FullName,
                Phone = "600222333",
                PeopleCount = 4
            };
            _tickets.GetActiveByPhoneAsync(dto.Phone, Arg.Any<CancellationToken>()).Returns((Ticket?)null);
            _customers.GetByPhoneAsync(dto.Phone, Arg.Any<CancellationToken>()).Returns((Customer?)null);
            Customer? capturedCustomer = null;
            _customers.When(x => x.AddAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>()))
                      .Do(ci =>
                      {
                          capturedCustomer = ci.Arg<Customer>();
                          capturedCustomer!.Id = 66;
                      });
            _customers.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
            _tickets.GetMaxWaitingPositionAsync(Arg.Any<CancellationToken>()).Returns(1);
            Ticket? capturedTicket = null;
            _tickets.When(x => x.AddAsync(Arg.Any<Ticket>(), Arg.Any<CancellationToken>()))
                    .Do(ti => capturedTicket = ti.Arg<Ticket>());
            _tickets.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
            _tickets.CountAheadAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(1);
            var result = await svc.CreateAsync(dto, CancellationToken.None);
            capturedTicket.Should().NotBeNull();
            capturedTicket!.Position.Should().Be(2);
            capturedTicket.Status.Should().Be(TicketStatus.Notified);
            result.Should().NotBeNull();
            result.Ahead.Should().Be(1);
            result.Status.Should().Be("Notified");
        }

        //Ahead
        //C: Ahead 0
        [Fact]
        public async Task Created_ticket_with_ahead_zero()
        {
            var svc = new TicketService(_customers, _tickets, _mapper, _notifications);
            var dto = new CreateTicketDto
            {
                FullName = _faker.Person.FullName,
                Phone = "600444555",
                PeopleCount = 1
            };
            _tickets.GetActiveByPhoneAsync(dto.Phone, Arg.Any<CancellationToken>()).Returns((Ticket?)null);
            _customers.GetByPhoneAsync(dto.Phone, Arg.Any<CancellationToken>()).Returns((Customer?)null);
            Customer? capturedCustomer = null;
            _customers.When(x => x.AddAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>()))
                      .Do(ci =>
                      {
                          capturedCustomer = ci.Arg<Customer>();
                          capturedCustomer!.Id = 33;
                      });
            _customers.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
            _tickets.GetMaxWaitingPositionAsync(Arg.Any<CancellationToken>()).Returns(0);
            Ticket? capturedTicket = null;
            _tickets.When(x => x.AddAsync(Arg.Any<Ticket>(), Arg.Any<CancellationToken>()))
                    .Do(ti => capturedTicket = ti.Arg<Ticket>());
            _tickets.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
            _tickets.CountAheadAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(0);
            var result = await svc.CreateAsync(dto, CancellationToken.None);
            capturedTicket.Should().NotBeNull();
            capturedTicket!.Position.Should().Be(1);
            capturedTicket.Status.Should().Be(TicketStatus.Notified);
            result.Should().NotBeNull();
            result.Ahead.Should().Be(0);
            result.Status.Should().Be("Notified");
        }
    }
}

