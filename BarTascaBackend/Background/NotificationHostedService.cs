using System.Collections.Concurrent;
using BarTasca.Data.Interfaces;
using BarTasca.Models;
using BarTasca.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BarTascaBackend.Background;

public class NotificationHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationHostedService> _logger;

    private readonly ConcurrentDictionary<int, int> _lastAheadSent = new();

    private static readonly TimeSpan _interval = TimeSpan.FromSeconds(30);

    public NotificationHostedService(IServiceScopeFactory scopeFactory, ILogger<NotificationHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NotificationHostedService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var ticketsRepo = scope.ServiceProvider.GetRequiredService<ITicketRepository>();
                var notifier = scope.ServiceProvider.GetRequiredService<INotificationService>();
                var notificationsRepo = scope.ServiceProvider.GetRequiredService<INotificationRepository>();

                // 1) Traer tickets relevantes: Waiting + Notified (activos)
                var activeStatuses = new[] { TicketStatus.Waiting, TicketStatus.Notified };
                var tickets = await ticketsRepo.ListByStatusesAsync(activeStatuses, take: 200, stoppingToken);

                foreach (var t in tickets)
                {
                    // Recalcular ahead en cada iteración
                    var ahead = await ticketsRepo.CountAheadAsync(t.Id, stoppingToken);

                    // === Solo respaldo para TURN (ahead == 1) ===
                    // Sin Reminder aquí. Nada para ahead == 3.

                    // Caso A: ticket en Waiting y entra a TURN (ahead == 1)
                    if (t.Status == TicketStatus.Waiting && ahead == 1)
                    {
                        // Deduplicar: si ya se envió TURN, saltar
                        var alreadySent = await notificationsRepo.ExistsSentAsync(t.Id, NotificationType.Turn, stoppingToken);
                        if (!alreadySent)
                        {
                            // Promocionar a Notified y persistir antes de notificar
                            t.Status = TicketStatus.Notified;
                            t.NotifiedAt = DateTime.UtcNow;
                            ticketsRepo.Update(t);
                            await ticketsRepo.SaveChangesAsync(stoppingToken);

                            // Notificar TURN
                            await notifier.NotifyTicketUpdatedAsync(t, ahead, NotificationType.Turn, stoppingToken);

                            // Sincronizar memoria para no reemitir en siguiente ciclo
                            _lastAheadSent[t.Id] = 1;
                        }
                        else
                        {
                            // Ya enviado; sincronizar memoria igualmente
                            _lastAheadSent[t.Id] = 1;
                        }
                    }
                    // Caso B: ticket ya Notified y se mantiene/entra en TURN (ahead == 1)
                    else if (t.Status == TicketStatus.Notified)
                    {
                        var previous = _lastAheadSent.GetOrAdd(t.Id, int.MaxValue);

                        if (ahead == 1 && previous != 1)
                        {
                            // Deduplicar antes de reemitir
                            var alreadySent = await notificationsRepo.ExistsSentAsync(t.Id, NotificationType.Turn, stoppingToken);
                            if (!alreadySent)
                            {
                                await notifier.NotifyTicketUpdatedAsync(t, ahead, NotificationType.Turn, stoppingToken);
                            }
                            _lastAheadSent[t.Id] = 1;
                        }
                        else if (ahead != 1)
                        {
                            // Si cambia el ahead, sincronizamos memoria para futuras transiciones a 1
                            _lastAheadSent[t.Id] = ahead;
                        }
                    }
                    else
                    {
                        // Otros casos (Waiting con ahead != 1): no hacer nada aquí.
                        // Este HostedService es solo respaldo para TURN.
                        if (t.Status == TicketStatus.Waiting && ahead != 1)
                        {
                            _lastAheadSent[t.Id] = ahead;
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Apagando: salir limpio
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in NotificationHostedService loop.");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Apagando: salir limpio
            }
        }

        _logger.LogInformation("NotificationHostedService stopped.");
    }
}
