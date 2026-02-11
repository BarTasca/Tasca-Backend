using BarTasca.DTOs.Queue;

namespace BarTasca.Services.Interfaces;

public interface ISignalRPublicNotificationService
{
    Task BroadcastAheadUpdatedAsync(QueueAheadDto dto, CancellationToken ct = default);
}
