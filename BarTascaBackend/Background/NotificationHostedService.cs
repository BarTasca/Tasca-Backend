using System.Collections.Concurrent;
using BarTasca.Data.Interfaces;
using BarTasca.Models;
using BarTasca.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BarTascaBackend.Background;

/// <summary>
/// Servicio en segundo plano que vigila los tickets activos y garantiza
/// la notificación de "Turn" (canal SignalR) cuando un ticket entra en turno.
/// No envía Reminder. Intervalo de sondeo: 30s.
/// </summary>
public class NotificationHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationHostedService> _logger;

    // Memoria local para evitar reemisiones innecesarias dentro del ciclo
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

                // Tickets activos: Waiting + Notified
                var activeStatuses = new[] { TicketStatus.Waiting, TicketStatus.Notified };
                var tickets = await ticketsRepo.ListByStatusesAsync(activeStatuses, take: 200, stoppingToken);

                foreach (var t in tickets)
                {
                    var ahead = await ticketsRepo.CountAheadAsync(t.Id, stoppingToken);


                    if (t.Status == TicketStatus.Waiting && ahead <= 1)
                    {
                        var alreadySent = await notificationsRepo.ExistsSentAsync(t.Id, NotificationType.Turn, NotificationChannel.SignalR, stoppingToken);
                        if (!alreadySent)
                        {
                            t.Status = TicketStatus.Notified;
                            t.NotifiedAt = DateTime.UtcNow;
                            ticketsRepo.Update(t);
                            await ticketsRepo.SaveChangesAsync(stoppingToken);

                            await notifier.NotifyTicketUpdatedAsync(t, ahead, NotificationType.Turn, stoppingToken);

                            _lastAheadSent[t.Id] = 1;
                        }
                        else
                        {
                            _lastAheadSent[t.Id] = 1;
                        }
                    }
                    else if (t.Status == TicketStatus.Notified)
                    {
                        var previous = _lastAheadSent.GetOrAdd(t.Id, int.MaxValue);

                        if (ahead <= 1 && previous != 1)
                        {
                            var alreadySent = await notificationsRepo.ExistsSentAsync(t.Id, NotificationType.Turn, NotificationChannel.SignalR, stoppingToken);
                            if (!alreadySent)
                            {
                                await notifier.NotifyTicketUpdatedAsync(t, ahead, NotificationType.Turn, stoppingToken);
                            }
                            _lastAheadSent[t.Id] = 1;
                        }
                        else if (ahead != 1)
                        {
                            _lastAheadSent[t.Id] = ahead;
                        }
                    }
                    else
                    {
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
