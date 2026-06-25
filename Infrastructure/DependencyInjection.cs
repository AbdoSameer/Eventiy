using Application.Abstractions.Outbox;
using Application.Abstractions.Persistence;
using Domain.Abstractions.Persistence;
using Infrastructure.BackgroundJobs;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Outbox;
using Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

            // Add DbContext with logging
            services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
            {
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);
                });

                // Enable detailed error logging in development
                if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
                {
                    options.EnableSensitiveDataLogging();
                    options.EnableDetailedErrors();
                }

                // Add logging
                var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
                if (loggerFactory != null)
                {
                    options.UseLoggerFactory(loggerFactory);
                }
            });

            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IEventRepository, EventRepository>();
            services.AddScoped<IBookingRepository, BookingRepository>();

            // Outbox
            services.AddScoped<IEventSerializer, EventSerializer>();
            services.AddScoped<IOutboxMessageService, OutboxMessageService>();
            services.AddScoped<IOutboxRepository, OutboxRepository>();
            services.AddScoped<IEventSerializer, EventSerializer>();


            // Background Processor
            services.AddHostedService<OutboxProcessor>();


            // Register the read context adapter
            services.AddScoped<IApplicationReadDbContext, ReadDbContextAdapter>();

            return services;
        }
    }
}