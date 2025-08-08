namespace BarTasca.DTOs.Ticket
{
    public record TicketDto
    {
        public int Id { get; init; }
        public byte PeopleCount { get; init; }
        public int Position { get; init; }
        public string Status { get; init; } = string.Empty;
    }


}
