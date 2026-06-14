using Application.Abstractions.Persistence;
using Domain.Common;
using Domain.Aggregates.EventAggregate;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence
{
    internal class ApplicationDbContext : DbContext, IUnitOfWork
    {
        public DbSet<Event> Events { get; set; } = null!;
        public DbSet<TicketType> TicketTypes { get; set; } = null!;



        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {

        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        }

        public async Task<Result> CommitAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await base.SaveChangesAsync(cancellationToken);

                return Result.Success();
            }
            catch (Exception exception)
            {
                return Result.Failure($"Failed to save changes: {exception.Message}");
            }
        }
    }
}
