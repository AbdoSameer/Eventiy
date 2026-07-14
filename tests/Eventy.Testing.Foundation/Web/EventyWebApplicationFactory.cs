using Eventy.Testing.Foundation.Containers;
using Eventy.Testing.Foundation.Fakes;
using Eventy.WebApi;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Eventy.Testing.Foundation.Web;

/// <summary>
/// WebApplicationFactory that spins up a real SQL Server container,
/// replaces production DbContext, and substitutes external services
/// (Redis, payments, hosted workers) for deterministic integration tests.
/// </summary>
public class EventyWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly SqlServerContainerFactory _sqlContainer = new();
    private string _connectionString = null!;

    private string? _originalSecret;
    private string? _originalIssuer;
    private string? _originalAudience;
    private string? _originalStripeKey;
    private string? _originalStripeWebhook;
    private string? _originalStripeSuccess;
    private string? _originalStripeCancel;

    internal const string TestJwtSecret =
        "Test_Secret_For_Integration_Tests_Only_Do_Not_Use_In_Production_64_Chars!";

    /// <summary>
    /// The test SQL Server connection string. Exposed so fixtures can pass it
    /// directly to DatabaseResetService instead of reading from IConfiguration
    /// (which points to the local developer DB in appsettings.json).
    /// </summary>
    public string ConnectionString => _connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // JWT settings are set via env vars in InitializeAsync (before Services is accessed),
        // so they're available when Program.Main calls AddInfrastructure(builder.Configuration).
        // The env var set/restore is scoped to the factory lifetime.

        builder.ConfigureTestServices(services =>
        {
            // Replace production DbContext with test container
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(_connectionString));

            // Replace Redis cache with in-memory fake
            services.RemoveAll<StackExchange.Redis.IConnectionMultiplexer>();
            services.RemoveAll<Application.Abstractions.Caching.ICacheService>();
            services.AddSingleton<Application.Abstractions.Caching.ICacheService, FakeCacheService>();

            // Replace payment gateway with fake
            services.RemoveAll<Application.Abstractions.Payments.IPaymentService>();
            services.AddSingleton<Application.Abstractions.Payments.IPaymentService, FakePaymentService>();

            // Disable background workers (outbox processor, etc.) for determinism
            services.RemoveAll<Microsoft.Extensions.Hosting.IHostedService>();

            // Add test authentication scheme
            services.AddAuthentication("Test")
                .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    "Test", _ => { });
        });
    }

    public async Task InitializeAsync()
    {
        // Save and set JWT env vars before host building triggers
        _originalSecret = Environment.GetEnvironmentVariable("Jwt__Secret");
        _originalIssuer = Environment.GetEnvironmentVariable("Jwt__Issuer");
        _originalAudience = Environment.GetEnvironmentVariable("Jwt__Audience");
        _originalStripeKey = Environment.GetEnvironmentVariable("Stripe__SecretKey");
        _originalStripeWebhook = Environment.GetEnvironmentVariable("Stripe__WebhookSecret");
        _originalStripeSuccess = Environment.GetEnvironmentVariable("Stripe__SuccessUrl");
        _originalStripeCancel = Environment.GetEnvironmentVariable("Stripe__CancelUrl");

        Environment.SetEnvironmentVariable("Jwt__Secret", TestJwtSecret);
        Environment.SetEnvironmentVariable("Jwt__Issuer", "Eventy.Tests");
        Environment.SetEnvironmentVariable("Jwt__Audience", "Eventy.Tests");
        Environment.SetEnvironmentVariable("Stripe__SecretKey", "sk_test_placeholder_for_tests");
        Environment.SetEnvironmentVariable("Stripe__WebhookSecret", "whsec_test_placeholder_for_tests");
        Environment.SetEnvironmentVariable("Stripe__SuccessUrl", "https://localhost:57354/payment/success");
        Environment.SetEnvironmentVariable("Stripe__CancelUrl", "https://localhost:57354/payment/cancel");

        _connectionString = await _sqlContainer.StartAsync();

        // Accessing Services triggers host building (which calls ConfigureWebHost)
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        // Restore original env vars to avoid process-level pollution
        Environment.SetEnvironmentVariable("Jwt__Secret", _originalSecret);
        Environment.SetEnvironmentVariable("Jwt__Issuer", _originalIssuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", _originalAudience);
        Environment.SetEnvironmentVariable("Stripe__SecretKey", _originalStripeKey);
        Environment.SetEnvironmentVariable("Stripe__WebhookSecret", _originalStripeWebhook);
        Environment.SetEnvironmentVariable("Stripe__SuccessUrl", _originalStripeSuccess);
        Environment.SetEnvironmentVariable("Stripe__CancelUrl", _originalStripeCancel);

        await _sqlContainer.DisposeAsync();
        await base.DisposeAsync();
    }
}
