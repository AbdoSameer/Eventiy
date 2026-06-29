using Domain.Aggregates.BookingAggregate;
using Domain.Aggregates.EventAggregate;
using Infrastructure.Persistence.Outbox;
using Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence
{
    public class ApplicationDbContext : DbContext
    {
        private readonly ILogger<ApplicationDbContext> _logger;

        public DbSet<Booking> Bookings { get; set; }
        public DbSet<Event> Events { get; set; }
        public DbSet<TicketType> TicketTypes { get; set; }
        public DbSet<OutboxMessage> OutboxMessages { get; set; } 

        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options,
            ILogger<ApplicationDbContext> logger = null)
            : base(options)
        {
            _logger = logger;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await base.SaveChangesAsync(cancellationToken);
                return result;
            }
            catch (DbUpdateException ex)
            {
                _logger?.LogError(ex, "Database update error");

                // Log inner exception details
                if (ex.InnerException != null)
                {
                    _logger?.LogError($"Inner exception: {ex.InnerException.Message}");
                }

                // Log entity entries that caused the error
                foreach (var entry in ex.Entries)
                {
                    _logger?.LogError($"Entity: {entry.Entity.GetType().Name}, State: {entry.State}");
                }

                throw;
            }
        }

        // Method to seed data
        public async Task SeedDataAsync()
        {
            try
            {
                await DatabaseSeeder.SeedAsync(this, _logger);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error seeding database");
                throw;
            }
        }
    }
}