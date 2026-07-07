using System.Text.Json;
using Application;
using Eventy.WebApi.Middlewares;
using Infrastructure;
using Infrastructure.Seed;
using Scalar.AspNetCore;

namespace Eventy.WebApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAngular", policy =>
                {
                    var origins = builder.Environment.IsDevelopment()
                        ? ["http://localhost:4200", "http://127.0.0.1:4200",
                           "http://localhost:57354", "http://127.0.0.1:57354"]
                        : builder.Configuration.GetSection("Cors:AllowedOrigins")
                              .Get<string[]>() ?? [];

                    policy.WithOrigins(origins)
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials();
                });
            });

            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                   options.JsonSerializerOptions.PropertyNamingPolicy =
                        JsonNamingPolicy.CamelCase;

                  options.JsonSerializerOptions.DefaultIgnoreCondition =
                        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
                });

            builder.Services.AddOpenApi();
            builder.Services.AddApplication();
            builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
            builder.Services.AddHostedService<DatabaseSeederService>();

            builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = 60 * 1024 * 1024;
            });

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

            app.UseMiddleware<CorrelationIdMiddleware>();
            app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

            app.UseStaticFiles();
            app.UseHttpsRedirection();
            app.UseCors("AllowAngular");  
            app.UseAuthorization();
            app.MapControllers();

            app.Run();
        }
    }
}
