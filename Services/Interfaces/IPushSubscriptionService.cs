using BarTasca.DTOs.Push;

namespace BarTasca.Services.Interfaces;

public interface IPushSubscriptionService
{
    Task<VapidPublicKeyResponse> GetVapidPublicKeyAsync(CancellationToken ct = default);

    Task<RegisterPushSubscriptionResponse> RegisterAsync(
        string publicId,
        RegisterPushSubscriptionRequest request,
        CancellationToken ct = default);

    Task UnregisterAsync(
        string publicId,
        UnregisterPushSubscriptionRequest request,
        CancellationToken ct = default);

    Task<int> DeactivateByTicketIdAsync(int ticketId, CancellationToken ct = default);
}