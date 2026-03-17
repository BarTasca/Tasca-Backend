namespace BarTasca.DTOs.Push;

public record RegisterPushSubscriptionResponse
{
    public bool Created { get; init; }
    public bool Updated { get; init; }
}