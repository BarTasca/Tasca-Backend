namespace BarTasca.DTOs.Push;

public record PushSubscriptionKeysDto
{
    public string P256dh { get; init; } = default!;
    public string Auth { get; init; } = default!;
}