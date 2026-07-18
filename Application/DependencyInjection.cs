using Application.Abstractions.Behaviors;
using Application.Abstractions.Inventory;
using Application.Features.Bookings.Events.BookingCancelled;
using Application.Features.Bookings.Events.BookingCreated;
using Application.Features.Bookings.Inventory;
using Domain.Aggregates.BookingAggregate.Events;
using Application.Abstractions.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Application;
public static class DependencyInjection
{
    public const string OptimisticStrategyKey = "OptimisticReservation";
    public const string AtomicRedisStrategyKey = "AtomicRedisReservation";

    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddScoped<IEventValidator<BookingCreatedEvent>, BookingCreatedEventValidator>();

        services.AddScoped<IDomainEventHandler<BookingCreatedEvent>, BookingCreatedEventHandler>();
        services.AddScoped<IDomainEventHandler<BookingCancelledEvent>, BookingCancelledEventHandler>();

        services.AddKeyedTransient<IInventoryReservationStrategy, OptimisticReservationStrategy>(OptimisticStrategyKey);

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingPipelineBehavior<,>));

        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(assembly);

            config.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationPipelineBehavior<,>));
        });

        return services;
    }
}