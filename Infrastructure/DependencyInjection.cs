using Domain.Abstractions.Persistence;
using Application.Abstractions.Persistence;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Domain.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString = configuration
                                  .GetConnectionString("DefaultConnection");

            services.AddDbContext<ApplicationDbContext>
                (options => options.UseSqlServer(connectionString));

            services.AddScoped<IUnitOfWork, UnitOfWork>();

            services.AddScoped<IEventRepository, EventRepository>();

            services.AddScoped<IBookingRepository, BookingRepository>();

            services.AddScoped<IApplicationReadDbContext, ReadDbContextAdapter>();


            return services;

        }
    }

}



