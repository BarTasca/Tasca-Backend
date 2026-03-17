namespace BarTasca.DTOs.Push;

public record PushSubscriptionDto
{
    public string Endpoint { get; init; } = default!;
    public PushSubscriptionKeysDto Keys { get; init; } = default!;
}