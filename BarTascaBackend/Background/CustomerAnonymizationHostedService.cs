using BarTasca.Data.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BarTascaBackend.Background;

public class CustomerAnonymizationHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CustomerAnonymizationHostedService> _logger;

    private static readonly TimeSpan Interval = TimeSpan.FromDays(1);
    private const int BatchSize = 500;

    public CustomerAnonymizationHostedService(IServiceScopeFactory scopeFactory, ILogger<CustomerAnonymizationHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CustomerAnonymizationHostedService started.");
        await RunOnceAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Interval, stoppingToken);
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // apagando
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CustomerAnonymizationHostedService loop.");
            }
        }

        _logger.LogInformation("CustomerAnonymizationHostedService stopped.");
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();

        var anonymizeBeforeUtc = DateTime.UtcNow.AddDays(-30);
        int totalProcessed = 0;

        while (!ct.IsCancellationRequested)
        {
            var batch = await repo.ListForAnonymizationAsync(anonymizeBeforeUtc, BatchSize, ct);
            if (batch.Count == 0) break;

            foreach (var c in batch)
            {
                c.FullName = "Anonymized";
                c.Phone = BuildMaskedPhone(c.Id);
                c.IsAnonymized = true;
            }

            totalProcessed += batch.Count;
            await repo.SaveChangesAsync(ct);
        }

        _logger.LogInformation("Customer anonymization run finished. Threshold: {Threshold}, Anonymized: {Count}", anonymizeBeforeUtc, totalProcessed);
    }

    private static string BuildMaskedPhone(int id)
    {
        var raw = "000000000" + id.ToString();
        return raw.Length <= 15 ? raw : raw[^15..];
    }
}
