using Application.Abstractions.Behaviors;
using Application.Features.Bookings.Events.BookingCancelled;
using Application.Features.Bookings.Events.BookingCreated;
using Domain.Aggregates.BookingAggregate.Events;
using Application.Abstractions.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Application;
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddScoped<IEventValidator<BookingCreatedEvent>, BookingCreatedValidator>();

        services.AddScoped<IDomainEventHandler<BookingCreatedEvent>, BookingCreatedEventHandler>();
        services.AddScoped<IDomainEventHandler<BookingCancelledEvent>, BookingCancelledEventHandler>();

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingPipelineBehavior<,>));

        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(assembly);

            config.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationPipelineBehavior<,>));
        });

        return services;
    }
}