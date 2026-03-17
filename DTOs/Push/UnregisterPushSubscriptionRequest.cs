namespace BarTasca.DTOs.Push;

public record UnregisterPushSubscriptionRequest
{
    public string TicketToken { get; init; } = default!;
    public string Endpoint { get; init; } = default!;
}