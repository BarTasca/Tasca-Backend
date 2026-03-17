namespace BarTasca.DTOs.Push;

public record VapidPublicKeyResponse
{
    public string PublicKey { get; init; } = default!;
}