using System.ComponentModel.DataAnnotations;

namespace BarTasca.Models
{
    public class Customer
    {
        public int Id { get; set; }

        [Required, StringLength(80)]
        public string FullName { get; set; } = null!;

        [Required, Phone, StringLength(15)]
        public string Phone { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsAnonymized { get; set; } = false;

        public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();

        public Customer() { }
    }
}
