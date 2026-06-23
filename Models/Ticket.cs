using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BarTasca.Models
{
    public class Ticket
    {
        public int Id { get; set; }

        [Required]
        public string PublicId { get; set; } = default!;

        [ForeignKey(nameof(Customer))]
        public int CustomerId { get; set; }
        public Customer Customer { get; set; } = null!;

        [Range(1, 15)]
        public byte PeopleCount { get; set; }
        public int Position { get; set; } 
        public TicketStatus Status { get; set; } = TicketStatus.Waiting;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ConfirmedAt { get; set; }
        public DateTime? NotifiedAt { get; set; }
        public DateTime? CancelledAt { get; set; }

        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();

        public Ticket() { }
    }
}
