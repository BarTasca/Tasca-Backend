namespace BarTasca.DTOs.Push;

public record RegisterPushSubscriptionRequest
{
    public string TicketToken { get; init; } = default!;
    public PushSubscriptionDto Subscription { get; init; } = default!;
}