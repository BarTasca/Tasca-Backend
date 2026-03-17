using BarTasca.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BarTasca.Data;

public class ColaDbContext : DbContext
{
    public ColaDbContext(DbContextOptions<ColaDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<StaffUser> StaffUsers => Set<StaffUser>();
    public DbSet<ServiceState> ServiceStates => Set<ServiceState>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Relación Ticket-Customer
        modelBuilder.Entity<Ticket>()
            .HasOne(t => t.Customer)
            .WithMany(c => c.Tickets)
            .HasForeignKey(t => t.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relación Notification-Ticket
        modelBuilder.Entity<Notification>()
            .HasOne(n => n.Ticket)
            .WithMany(t => t.Notifications)
            .HasForeignKey(n => n.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Notification>()
            .HasIndex(n => new { n.TicketId, n.Type, n.Channel, n.Status, n.SentAt });

        // Índice único teléfono
        modelBuilder.Entity<Customer>()
            .HasIndex(c => c.Phone)
            .IsUnique();

        // Índice para cálculo de cola
        modelBuilder.Entity<Ticket>()
            .HasIndex(t => new { t.Status, t.Position });

        // Configuración de RowVersion para concurrencia
        modelBuilder.Entity<ServiceState>()
            .Property(e => e.RowVersion)
            .IsRowVersion();

        // Configuración de PushSubscription
        modelBuilder.Entity<PushSubscription>()
            .HasOne(ps => ps.Ticket)
            .WithMany()
            .HasForeignKey(ps => ps.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PushSubscription>()
            .Property(ps => ps.Endpoint)
            .HasMaxLength(2048);

        modelBuilder.Entity<PushSubscription>()
            .Property(ps => ps.EndpointHash)
            .HasColumnType("binary(32)")
            .IsRequired();

        modelBuilder.Entity<PushSubscription>()
            .HasIndex(ps => ps.EndpointHash)
            .IsUnique();

        modelBuilder.Entity<PushSubscription>()
            .HasIndex(ps => new { ps.TicketId, ps.IsActive });

        modelBuilder.Entity<PushSubscription>()
            .Property(ps => ps.P256dh)
            .HasMaxLength(256);

        modelBuilder.Entity<PushSubscription>()
            .Property(ps => ps.Auth)
            .HasMaxLength(256);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

        if (environment == "Development")
        {
            optionsBuilder
                .LogTo(Console.WriteLine, LogLevel.Information)
                .EnableSensitiveDataLogging();
        }
    }
}
