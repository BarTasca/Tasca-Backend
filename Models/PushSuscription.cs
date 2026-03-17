using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BarTasca.Models;

public class PushSubscription
{
    public int Id { get; set; }

    [ForeignKey(nameof(Ticket))]
    public int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;

    [Required, MaxLength(2048)]
    public string Endpoint { get; set; } = null!;

    [Required, MaxLength(32)]
    public byte[] EndpointHash { get; set; } = null!;

    [Required, MaxLength(256)]
    public string P256dh { get; set; } = null!;

    [Required, MaxLength(256)]
    public string Auth { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}