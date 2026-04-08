namespace BarTasca.DTOs.Ticket;

public sealed class TicketStatusDto
{
    public string PublicId { get; set; } = default!;
    public string Status { get; set; } = string.Empty;
    public int Ahead { get; set; }
    public int Position { get; set; }
    public byte PeopleCount { get; set; }
    public string CustomerFullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? NotifiedAt { get; set; }
}