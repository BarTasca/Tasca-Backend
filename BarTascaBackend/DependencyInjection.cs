using BarTascaBackend.Background;
using Microsoft.Extensions.DependencyInjection;

namespace BarTascaBackend;

public static class DependencyInjection
{
    public static IServiceCollection AddApiLayer(this IServiceCollection services)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        services.AddSignalR();
        return services;
    }

    public static IServiceCollection AddBackgroundWorkers(this IServiceCollection services)
    {
        services.AddHostedService<NotificationHostedService>();
        return services;
    }
}
