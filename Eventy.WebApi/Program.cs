using System.Net;
using System.Text.Json;
using System.Threading.RateLimiting;
using Application;
using Eventy.WebApi.Middlewares;
using Infrastructure;
using Infrastructure.Seed;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
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

                    options.JsonSerializerOptions.Converters.Add(
                        new System.Text.Json.Serialization.JsonStringEnumConverter());

                    options.JsonSerializerOptions.Converters.Add(
                        new Infrastructure.Persistence.Outbox.Converters.ValueObjectJsonConverterFactory());
                });

            builder.Services.AddOpenApi();
            builder.Services.AddApplication();
            builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
            builder.Services.AddHostedService<DatabaseSeederService>();

            builder.Services.AddRateLimiter(rateLimiter =>
            {
                rateLimiter.AddFixedWindowLimiter("Booking", options =>
                {
                    options.PermitLimit = 10;
                    options.Window = TimeSpan.FromSeconds(1);
                    options.QueueLimit = 0;
                });
            });

            builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = 60 * 1024 * 1024;
            });

            // Forwarded Headers: behind Azure Front Door / App Gateway / Nginx,
            // the backend sees the proxy's IP & http (not the client's). This
            // restores the original scheme/host/remote IP so HTTPS-related
            // decisions (UseHttpsRedirection, auth callbacks) work correctly.
            //
            // KnownNetworks/Proxies are cleared so any trusted proxy is accepted.
            // Tighten this to your Azure egress ranges if you expose the backend
            // directly to the internet (not recommended — keep it internal).
            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders =
                    ForwardedHeaders.XForwardedFor |
                    ForwardedHeaders.XForwardedProto |
                    ForwardedHeaders.XForwardedHost;
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
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

            // Apply Forwarded Headers early — must run before anything that
            // inspects the request scheme (UseHttpsRedirection, CORS, auth).
            app.UseForwardedHeaders();

            app.UseMiddleware<CorrelationIdMiddleware>();
            app.UseWebSockets(new WebSocketOptions
            {
                KeepAliveInterval = TimeSpan.FromSeconds(30),
            });
            app.UseMiddleware<WebSocketMiddleware>();
            app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

            app.UseStaticFiles();

            // HTTPS redirect: behind a TLS-terminating proxy (Azure App Service,
            // Front Door, our Nginx image) this would cause an infinite redirect
            // loop, because the proxy talks http to the container while the
            // client connection is https. With ForwardedHeaders configured above,
            // ASP.NET Core already sees the correct scheme — so we only redirect
            // in Development (local Kestrel with real certs). In Production, TLS
            // is terminated upstream and there is nothing to redirect to.
            if (!app.Environment.IsDevelopment())
            {
                app.UseHsts();
            }

            if (app.Environment.IsDevelopment())
            {
                app.UseHttpsRedirection();
            }

            app.UseCors("AllowAngular");
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseRateLimiter();
            app.MapControllers();

            app.Run();
        }
    }
}
