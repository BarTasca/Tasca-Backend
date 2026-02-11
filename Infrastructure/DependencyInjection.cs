using BarTasca.Infrastructure.Notifications;
using BarTasca.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace BarTasca.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registra adaptadores de infraestructura (SignalR, Twilio)
    /// </summary>
    /// <typeparam name="THub">Hub de SignalR que usará el notifier (p.ej., QueueHub)</typeparam>
    public static IServiceCollection AddInfrastructure<THub>(this IServiceCollection services)
        where THub : Hub
    {
        // SignalRNotificationService como implementación de INotificationService
        services.AddScoped<INotificationService, SignalRNotificationService<THub>>();

        return services;
    }

    public static IServiceCollection AddPublicInfrastructure<THub>(this IServiceCollection services)
        where THub : Hub
    {
        services.AddScoped<ISignalRPublicNotificationService, SignalRPublicNotificationService<THub>>();
        return services;
    }
}
