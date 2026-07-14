# Phase 2 — Testing Foundation Architecture

The Testing Foundation is a shared infrastructure library (not a test project) that all test projects consume. It provides application host management, database environment, test data builders, and external dependency control.

## Table of Contents
1. [Project Structure](#project-structure)
2. [SQL Server Container Factory](#sql-server-container-factory)
3. [WebApplicationFactory](#webapplicationfactory)
4. [Authentication Mock](#authentication-mock)
5. [Database Isolation](#database-isolation)
6. [Seed Builders](#seed-builders)
7. [Required NuGet Packages](#required-nuget-packages)

## Project Structure

```
Eventy.Testing.Foundation/
├── Containers/
│   └── SqlServerContainerFactory.cs
├── Web/
│   ├── EventyWebApplicationFactory.cs
│   └── TestAuthenticationHandler.cs
├── Database/
│   ├── DatabaseInitializer.cs
│   ├── MigrationRunner.cs
│   └── DatabaseResetService.cs
├── Fixtures/
│   └── IntegrationTestFixture.cs
├── Builders/
│   ├── EventBuilder.cs
│   ├── TicketBuilder.cs
│   └── BookingBuilder.cs
└── Extensions/
    └── ServiceCollectionExtensions.cs
```

## SQL Server Container Factory

Responsibilities: Create SQL Server container, expose connection string, run EF migrations, return ready database.

```csharp
public class SqlServerContainerFactory : IAsyncDisposable
{
    private readonly MsSqlContainer _container;

    public SqlServerContainerFactory()
    {
        _container = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .Build();
    }

    public async Task<string> StartAsync()
    {
        await _container.StartAsync();
        return _container.GetConnectionString();
    }

    public async Task StopAsync() => await _container.StopAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}
```

Why SQL Server (not PostgreSQL) for Eventy: The production system uses SQL Server. Concurrency behavior must match production exactly — locking, isolation levels, deadlock detection.

## WebApplicationFactory

Replaces production dependencies with test equivalents:

```csharp
public class EventyWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly SqlServerContainerFactory _sqlContainer;
    private string _connectionString = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove production DbContext
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            // Add test DbContext pointing to container
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(_connectionString));

            // Replace Redis with fake
            services.RemoveAll<IConnectionMultiplexer>();
            services.AddSingleton<ICacheService, FakeCacheService>();

            // Replace external services with fakes
            services.RemoveAll<IPaymentService>();
            services.AddSingleton<IPaymentService, FakePaymentService>();

            // Disable background workers for determinism
            services.RemoveAll<IHostedService>();

            // Add test authentication
            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    "Test", _ => { });
        });
    }

    public async Task InitializeAsync()
    {
        _sqlContainer = new SqlServerContainerFactory();
        _connectionString = await _sqlContainer.StartAsync();
    }

    public async Task DisposeAsync() => await _sqlContainer.DisposeAsync();
}
```

## Authentication Mock

Creates deterministic test identities without real JWT tokens:

```csharp
public class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimTypes.Email, "test@eventy.com")
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
```

For concurrency tests, create multiple test users:

```csharp
public static class TestUsers
{
    public static ClaimsPrincipal UserA => CreateUser("11111111-1111-1111-1111-111111111111");
    public static ClaimsPrincipal UserB => CreateUser("22222222-2222-2222-2222-222222222222");
    // ... up to N users for race scenarios
}
```

## Database Isolation

Two-level isolation strategy:

**Level 1 — Container Isolation**: One SQL Server container per test assembly. Used for migration validation and infrastructure tests.

**Level 2 — Reset Isolation**: Clean database between individual tests using Respawn:

```csharp
public class DatabaseResetService
{
    private readonly Respawner _respawner;

    public async Task ResetAsync(DbConnection connection)
    {
        await _respawner.ResetAsync(connection);
    }
}
```

Container lifecycle per test collection:

```
Test Suite Start
    → Create Docker Container
    → Create Database
    → Apply EF Migrations
    → Seed Required Data
    → Execute Tests
    → Cleanup (Respawn between tests)
    → Dispose Container (end of suite)
```

## Seed Builders

Never use `new Event()` directly in tests. Use fluent builders:

```csharp
public class EventBuilder
{
    private string _name = "Test Event";
    private int _capacity = 100;
    private DateTime _date = DateTime.UtcNow.AddDays(30);
    private EventStatus _status = EventStatus.Published;

    public EventBuilder WithName(string name) { _name = name; return this; }
    public EventBuilder WithCapacity(int capacity) { _capacity = capacity; return this; }
    public EventBuilder WithDate(DateTime date) { _date = date; return this; }
    public EventBuilder AsDraft() { _status = EventStatus.Draft; return this; }

    public Event Build() => Event.Create(_name, _capacity, _date, _status);
}
```

Create scenario-level builders for common test situations:

```csharp
public static class TestScenarios
{
    public static (Event, TicketType) AvailableTicketEvent()
    {
        var event = new EventBuilder().WithCapacity(10).Build();
        var ticket = new TicketBuilder().WithEvent(event).WithCapacity(10).Build();
        return (event, ticket);
    }

    public static (Event, TicketType) SoldOutEvent()
    {
        var event = new EventBuilder().WithCapacity(1).Build();
        var ticket = new TicketBuilder().WithEvent(event).WithCapacity(0).Build();
        return (event, ticket);
    }
}
```

## Required NuGet Packages

```xml
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="9.0.0" />
<PackageReference Include="Testcontainers.MsSql" Version="4.*" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.0.0" />
<PackageReference Include="Respawn" Version="6.*" />
```
