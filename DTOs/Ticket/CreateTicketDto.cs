using System.ComponentModel.DataAnnotations;

namespace BarTasca.DTOs.Ticket
{
    public record CreateTicketDto
    {
        [Required]
        [StringLength(80, MinimumLength = 2)]
        [RegularExpression(@".*\S.*", ErrorMessage = "FullName cannot be empty or whitespace.")]
        public string FullName { get; init; } = null!;

        [Required]
        [RegularExpression(@"^\+[1-9]\d{0,3}\s\d{6,14}$", ErrorMessage = "Phone must be in international format, +XX XXXXXXXXX.")]

        public string Phone { get; init; } = null!;

        [Range(1, 15)]
        public byte PeopleCount { get; init; }

        [Required]
        public string QrToken { get; init; } = null!;

    }

}
