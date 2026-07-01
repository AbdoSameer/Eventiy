using Application.Abstractions.Outbox;
using Application.Abstractions.Persistence;
using Application.Abstractions.Security;
using Domain.Common;
using Domain.Persistence.Repositories;
using Infrastructure.Authentication;
using Infrastructure.BackgroundJobs;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Outbox;
using Infrastructure.Persistence.Repositories;
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
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IUserRepository, UserRepository>();  

        services.AddScoped<IOutboxMessageService, OutboxMessageService>();
        services.AddScoped<IEventSerializer, EventSerializer>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddHostedService<OutboxProcessor>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();


        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));

        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<ICurrentUserService, CurrentUserService>(); 
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        services.AddHttpContextAccessor();
        
        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()!;

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
