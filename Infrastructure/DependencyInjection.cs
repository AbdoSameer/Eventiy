using Application.Abstractions.Outbox;
using Application.Abstractions.Persistence;
using Domain.Abstractions.Persistence;
using Domain.Common;
using Infrastructure.BackgroundJobs;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Outbox;
using Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;


namespace Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        // Database with Resilience and Logging
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

            // Use configured application logger factory
            var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            if (loggerFactory != null)
            {
                options.UseLoggerFactory(loggerFactory);
            }
        });

        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();

        // Application Abstractions (Implementation in Infrastructure)
        services.AddScoped<IOutboxMessageService, OutboxMessageService>();
        services.AddScoped<IEventSerializer, EventSerializer>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Background Services
        services.AddHostedService<OutboxProcessor>();

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        return services;
    }
}