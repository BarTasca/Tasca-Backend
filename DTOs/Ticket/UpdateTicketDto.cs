using System.ComponentModel.DataAnnotations;

namespace BarTasca.DTOs.Ticket
{
    public record UpdateTicketDto
    {
        [Range(1, 15)]
        public byte PeopleCount { get; init; }
    }
}
