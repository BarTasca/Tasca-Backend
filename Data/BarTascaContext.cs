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
