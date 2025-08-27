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

    // Evita re-enviar continuamente el aviso de "es tu turno" (ahead == 1) en NOTIFIED
    private readonly ConcurrentDictionary<int, int> _lastAheadSent = new();

    // Periodicidad del ciclo
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

                // 1) Traer tickets relevantes: Waiting + Notified (activos)
                var activeStatuses = new[] { TicketStatus.Waiting, TicketStatus.Notified };
                var tickets = await ticketsRepo.ListByStatusesAsync(activeStatuses, take: 200, stoppingToken);

                foreach (var t in tickets)
                {
                    // Recalcular ahead en cada iteración
                    var ahead = await ticketsRepo.CountAheadAsync(t.Id, stoppingToken);

                    if (t.Status == TicketStatus.Waiting)
                    {
                        // Regla 1: Waiting y ahead == 3 => Notified + NotifiedAt (quedan 3 por delante)
                        if (ahead == 3)
                        {
                            t.Status = TicketStatus.Notified;
                            t.NotifiedAt = DateTime.UtcNow;
                            ticketsRepo.Update(t);
                            await ticketsRepo.SaveChangesAsync(stoppingToken);

                            // Emitimos 'ticketUpdated' (el cliente verá cambio a Notified)
                            await notifier.NotifyTicketUpdatedAsync(t, ahead, stoppingToken);

                            // Reset de memoria para no disparar "turno" inmediatamente si el cálculo cambia
                            _lastAheadSent.TryRemove(t.Id, out _);
                        }
                    }
                    else if (t.Status == TicketStatus.Notified)
                    {
                        // Regla 2: Notified y ahead == 1 => enviar aviso ("es tu turno"), sin cambiar estado
                        var previous = _lastAheadSent.GetOrAdd(t.Id, int.MaxValue);
                        if (ahead == 1 && previous != 1)
                        {
                            // No cambiamos estado; re-emitimos 'ticketUpdated' para notificar al cliente/staff
                            await notifier.NotifyTicketUpdatedAsync(t, ahead, stoppingToken);
                            _lastAheadSent[t.Id] = 1;
                        }

                        // Si vuelve a crecer la cola o cambia, actualizamos memoria para futuros envíos
                        if (ahead != 1)
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
