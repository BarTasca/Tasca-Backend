using Microsoft.Extensions.DependencyInjection;

namespace BarTascaBackend;

public static class DependencyInjection
{
    public static IServiceCollection AddApiLayer(this IServiceCollection services)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        services.AddSignalR(); // el Hub se mapeará en Program.cs cuando lo tengas
        return services;
    }
}
