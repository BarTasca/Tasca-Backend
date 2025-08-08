using System.ComponentModel.DataAnnotations;

namespace BarTasca.Models;

public class StaffUser
{
    public int Id { get; set; }

    [Required, EmailAddress, StringLength(120)]
    public string Email { get; set; } = null!;

    [Required]
    public string PasswordHash { get; set; } = null!;
    public StaffRole Role { get; set; } = StaffRole.Worker;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
