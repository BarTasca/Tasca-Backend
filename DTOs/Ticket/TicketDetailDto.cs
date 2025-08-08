namespace BarTasca.DTOs.Ticket
{
    public record TicketDetailDto
    {
        public int Id { get; init; }
        public byte PeopleCount { get; init; }
        public int Position { get; init; }
        public string Status { get; init; } = string.Empty;

        public DateTime CreatedAt { get; init; }
        public DateTime? NotifiedAt { get; init; }
        public DateTime? ConfirmedAt { get; init; }
        public DateTime? ExpiresAt { get; init; }

        public string CustomerFullName { get; init; } = string.Empty;
    }
}
