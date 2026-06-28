using Application;
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

            builder.Services.AddControllers();
            builder.Services.AddOpenApi();

            builder.Services.AddApplication(); 
            builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

            builder.Services.AddHostedService<DatabaseSeederService>();


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