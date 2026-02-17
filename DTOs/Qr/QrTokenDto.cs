namespace BarTasca.DTOs.Qr;

public record QrTokenDto
{
    public string Token { get; init; } = null!;
    public DateTime ExpiresAtUtc { get; init; }
}
