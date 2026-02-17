namespace BarTasca.DTOs.Qr;

public record QrValidationResultDto
{
    public bool IsValid { get; init; }
    public bool IsExpired { get; init; }
}
