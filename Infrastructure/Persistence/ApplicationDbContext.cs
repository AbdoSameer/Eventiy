using Application.Abstractions.Persistence;
using Domain.Aggregates.EventAggregate;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Persistence
{
    internal class ApplicationDbContext : DbContext
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

        public Task SaveChangesAsync()
        {
            return base.SaveChangesAsync();
        }
    }
}
