using Application;
using EventManagementSystem.Application.Abstractions.Behaviors;
using EventManagementSystem.Application.Features.Bookings.Commands.MakeBooking;
using FluentValidation;
using Infrastructure;
using Infrastructure.Persistence;
using MediatR;
using Scalar.AspNetCore;

namespace Eventy.WebApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllers();

            builder.Services.AddOpenApi();

            builder.Services.AddApplication();

            builder.Services.AddInfrastructure(builder.Configuration);

            builder.Services.AddValidatorsFromAssemblyContaining<
                                         MakeBookingCommandValidator>();

            builder.Services.AddTransient(
                typeof(IPipelineBehavior<,>),
                typeof(ValidationPipelineBehavior<,>));

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.MapScalarApiReference(options =>
                {
                    options.Title = "Eventy API";
                    options.Theme = ScalarTheme.DeepSpace;
                });
            }

            app.UseHttpsRedirection();
            app.UseAuthorization();
            app.MapControllers();
            app.Run();
        }
    }
}