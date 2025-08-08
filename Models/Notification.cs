using System.ComponentModel.DataAnnotations.Schema;

namespace BarTasca.Models;

public class Notification
{
    public int Id { get; set; }

    [ForeignKey(nameof(Ticket))]
    public int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;

    public NotificationChannel Channel { get; set; }
    public NotificationType Type { get; set; }
    public NotificationStatus Status { get; set; }

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
