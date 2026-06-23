using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Seed
{
    public class DatabaseSeederService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DatabaseSeederService> _logger;

        public DatabaseSeederService(
            IServiceProvider serviceProvider,
            ILogger<DatabaseSeederService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            try
            {
                _logger.LogInformation("Starting database seeding...");
                await context.Database.MigrateAsync(cancellationToken);
                await context.SeedDataAsync();
                _logger.LogInformation(" Database seeded successfully with FIFA matches!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding database");
                throw;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}