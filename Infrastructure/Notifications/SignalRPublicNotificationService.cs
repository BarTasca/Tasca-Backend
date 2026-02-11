using BarTasca.DTOs.Queue;
using Microsoft.AspNetCore.SignalR;
using BarTasca.Services.Interfaces;

namespace BarTasca.Infrastructure.Notifications;

public class SignalRPublicNotificationService<THub> : ISignalRPublicNotificationService
    where THub : Hub
{
    public const string PublicQueueGroup = "public-queue";
    private readonly IHubContext<THub> _hub;

    public SignalRPublicNotificationService(IHubContext<THub> hub)
    {
        _hub = hub;
    }

    public Task BroadcastAheadUpdatedAsync(QueueAheadDto dto, CancellationToken ct = default)
    {
        return _hub.Clients.Group(PublicQueueGroup).SendAsync("aheadUpdated", dto, ct);
    }
}
