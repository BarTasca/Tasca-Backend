using BarTasca.Services.Interfaces;
using BarTasca.Services.Services;
using Microsoft.Extensions.DependencyInjection;
using BarTasca.Services.Options;

namespace BarTasca.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ITicketService, TicketService>();
        services.AddScoped<IStaffAuthService, StaffAuthService>();
        services.AddScoped<ITicketAuthService, TicketAuthService>();
        services.AddScoped<IServiceStateService, ServiceStateService>();
        services.AddScoped<IQrTokenService, QrTokenService>();
        services.AddScoped<IPushSubscriptionService, PushSubscriptionService>();
        services.AddScoped<IInitialAdminService, InitialAdminService>();
        return services;
    }
}
