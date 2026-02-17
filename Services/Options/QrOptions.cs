namespace BarTasca.Services.Options;

public class QrOptions
{
    public required string Secret { get; init; }
    public int RotationMinutes { get; init; } = 10;
}
