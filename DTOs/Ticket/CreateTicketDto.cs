using System.ComponentModel.DataAnnotations;

namespace BarTasca.DTOs.Ticket
{
    public record CreateTicketDto
    {
        [Required, StringLength(80)]
        public string FullName { get; init; } = null!;

        [Required, Phone, StringLength(15)]
        public string Phone { get; init; } = null!;

        [Range(1, 15)]
        public byte PeopleCount { get; init; }

        [Required]
        public string QrToken { get; init; } = null!;

    }

}
