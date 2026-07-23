using Application.Abstractions.Inventory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.BackgroundJobs;

public sealed class InventoryReconciliationJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InventoryReconciliationJob> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(30);

    public InventoryReconciliationJob(
        IServiceProvider serviceProvider,
        ILogger<InventoryReconciliationJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Inventory Reconciliation Job started");

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Inventory Reconciliation Job startup delay cancelled");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IInventoryReconciliationService>();
                await service.ReconcileAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in Inventory Reconciliation Job");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("Inventory Reconciliation Job stopped");
    }
}
