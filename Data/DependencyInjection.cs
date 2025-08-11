using BarTasca.Data.Interfaces;
using BarTasca.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace BarTasca.Data;

public static class DependencyInjection
{
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ITicketRepository, TicketRepository>();
        return services;
    }
}
