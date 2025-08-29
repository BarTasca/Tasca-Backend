using BarTasca.Services.Interfaces;
using BarTasca.Services.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BarTasca.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ITicketService, TicketService>();
        services.AddScoped<IStaffAuthService, StaffAuthService>();
        return services;
    }
}
