namespace BarTasca.DTOs.Ticket
{
    public record TicketCompactDto
    {
        public int Id { get; init; }
        public string Status { get; init; } = string.Empty;
        public byte Position { get; init; }
        public int Ahead { get; set; }
    }
}
