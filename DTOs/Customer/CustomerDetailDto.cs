using BarTasca.DTOs.Ticket;

namespace BarTasca.DTOs.Customer
{
    public record CustomerDetailDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public List<TicketDto> Tickets { get; set; } = new();
    }

}
