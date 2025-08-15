namespace BarTasca.DTOs.Ticket
{
    public record TicketStaffListDto
    {
        public int Id { get; init; }
        public byte PeopleCount { get; init; }
        public int Position { get; init; }
        public string Status { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }

        public string CustomerFullName { get; init; } = string.Empty;
    }
}
