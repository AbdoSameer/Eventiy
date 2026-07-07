using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Outbox;
using Application.Abstractions.Persistence;
using Application.Abstractions.Security;
using Domain.Abstractions.Persistence;
using Domain.Abstractions.Storage;
using Domain.Common;
using Domain.Persistence.Repositories;
using Infrastructure.Authentication;
using Infrastructure.BackgroundJobs;
using Infrastructure.Caching;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Outbox;
using Infrastructure.Persistence.Repositories;
using Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)  
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.CommandTimeout(300);
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
            });

            if (environment.IsDevelopment())
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }

            var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            if (loggerFactory is not null)
            {
                options.UseLoggerFactory(loggerFactory);
            }
        });

        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<OutboxRepository>();
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IApplicationReadDbContext, ReadDbContextAdapter>();
        services.AddScoped<IEventPhotoRepository, EventPhotoRepository>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();

        services.AddScoped<IOutboxMessageService, OutboxMessageService>();
        services.AddScoped<IEventSerializer, EventSerializer>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddHostedService<OutboxProcessor>();
        services.AddSingleton(TimeProvider.System);


        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));

        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<ICurrentUserService, CurrentUserService>(); 
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        services.AddScoped<IEventMetadataFactory, Messaging.EventMetadataFactory>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();
        services.AddHttpContextAccessor();

        var redisConnectionString = configuration.GetConnectionString("Redis")
            ?? "localhost:6379";
        services.AddSingleton<ICacheService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<RedisCacheService>>();
            return new RedisCacheService(redisConnectionString, logger);
        });
        
        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? throw new InvalidOperationException(
                "JWT settings are not configured. Set Jwt:Secret via User Secrets (dev) or environment variables (prod).");

        if (string.IsNullOrWhiteSpace(jwtSettings.Secret) || jwtSettings.Secret.Length < 32)
            throw new InvalidOperationException(
                "Jwt:Secret must be at least 32 characters. Use User Secrets for development.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                                                  Encoding.UTF8.GetBytes(jwtSettings.Secret))
                };
            });

        services.AddAuthorization();

        return services;
    }
}
