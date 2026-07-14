#!/usr/bin/env python3
"""
Scaffold the complete Eventy test solution structure.

Usage:
    python scaffold-test-solution.py <solution-root-path>

Generates:
    - Test project .csproj files with correct package references
    - Folder structure following the 3-tier architecture
    - xUnit configuration files
    - Empty test class stubs

Example:
    python scaffold-test-solution.py /home/user/Eventy
"""

import os
import sys
import argparse
from pathlib import Path


TEST_PROJECTS = {
    "Eventy.Testing.Foundation": {
        "folder": "tests/Eventy.Testing.Foundation",
        "is_test_project": False,
        "packages": [
            ('Microsoft.AspNetCore.Mvc.Testing', '9.0.0'),
            ('Testcontainers.MsSql', '4.0.0'),
            ('Microsoft.EntityFrameworkCore.Design', '9.0.0'),
            ('Respawn', '6.0.0'),
        ],
        "folders": [
            "Containers",
            "Web",
            "Database",
            "Fixtures",
            "Builders",
            "Extensions",
        ],
        "files": [
            ("Containers/SqlServerContainerFactory.cs", CONTAINER_FACTORY),
            ("Web/EventyWebApplicationFactory.cs", WEB_FACTORY),
            ("Web/TestAuthenticationHandler.cs", AUTH_HANDLER),
            ("Builders/EventBuilder.cs", EVENT_BUILDER),
        ],
    },
    "Eventy.Domain.UnitTests": {
        "folder": "tests/Eventy.Domain.UnitTests",
        "is_test_project": True,
        "packages": [
            ('Microsoft.NET.Test.Sdk', '17.12.0'),
            ('xunit', '2.9.2'),
            ('xunit.runner.visualstudio', '2.8.2'),
            ('FluentAssertions', '7.0.0'),
            ('NSubstitute', '5.3.0'),
        ],
        "folders": [
            "Aggregates",
            "ValueObjects",
            "DomainEvents",
            "Builders",
        ],
        "files": [
            ("Aggregates/BookingTests.cs", BOOKING_TESTS_STUB),
            ("Aggregates/TicketTypeTests.cs", TICKET_TESTS_STUB),
            ("ValueObjects/EmailTests.cs", VALUE_OBJECT_STUB),
        ],
    },
    "Eventy.Application.UnitTests": {
        "folder": "tests/Eventy.Application.UnitTests",
        "is_test_project": True,
        "packages": [
            ('Microsoft.NET.Test.Sdk', '17.12.0'),
            ('xunit', '2.9.2'),
            ('xunit.runner.visualstudio', '2.8.2'),
            ('FluentAssertions', '7.0.0'),
            ('NSubstitute', '5.3.0'),
        ],
        "folders": [
            "Features/Bookings/Commands",
            "Behaviors",
            "Builders",
        ],
        "files": [
            ("Features/Bookings/Commands/CreateBookingHandlerTests.cs", HANDLER_TESTS_STUB),
            ("Behaviors/ValidationBehaviorTests.cs", BEHAVIOR_TESTS_STUB),
        ],
    },
    "Eventy.IntegrationTests": {
        "folder": "tests/Eventy.IntegrationTests",
        "is_test_project": True,
        "packages": [
            ('Microsoft.NET.Test.Sdk', '17.12.0'),
            ('xunit', '2.9.2'),
            ('xunit.runner.visualstudio', '2.8.2'),
            ('FluentAssertions', '7.0.0'),
            ('Microsoft.AspNetCore.Mvc.Testing', '9.0.0'),
        ],
        "folders": [
            "Features/Events",
            "Features/Tickets",
            "Features/Bookings",
            "Fixtures",
            "Scenarios",
            "Assertions",
            "Helpers",
        ],
        "files": [
            ("Fixtures/IntegrationTestFixture.cs", INTEGRATION_FIXTURE),
            ("Features/Bookings/CreateBookingTests.cs", INTEGRATION_TEST_STUB),
            ("Assertions/DatabaseAssertions.cs", DB_ASSERTIONS_STUB),
        ],
    },
    "Eventy.ConcurrencyTests": {
        "folder": "tests/Eventy.ConcurrencyTests",
        "is_test_project": True,
        "packages": [
            ('Microsoft.NET.Test.Sdk', '17.12.0'),
            ('xunit', '2.9.2'),
            ('xunit.runner.visualstudio', '2.8.2'),
            ('FluentAssertions', '7.0.0'),
            ('Microsoft.AspNetCore.Mvc.Testing', '9.0.0'),
        ],
        "folders": [
            "Scenarios",
            "Engine",
            "Assertions",
            "Fixtures",
        ],
        "files": [
            ("Engine/ConcurrentExecutor.cs", CONCURRENT_EXECUTOR),
            ("Scenarios/LastTicketRaceTests.cs", LAST_TICKET_TEST),
            ("Fixtures/ConcurrencyTestFixture.cs", CONCURRENCY_FIXTURE),
        ],
    },
}

# ---- File Templates ----

CONTAINER_FACTORY = '''using Testcontainers.MsSql;

namespace Eventy.Testing.Foundation.Containers;

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
'''

WEB_FACTORY = '''using Eventy.Testing.Foundation.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Eventy.Testing.Foundation.Web;

public class EventyWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly SqlServerContainerFactory _sqlContainer = new();
    private string _connectionString = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            // Replace production DbContext with test container
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(_connectionString));

            // Add test authentication
            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    "Test", _ => { });
        });
    }

    public async Task InitializeAsync()
    {
        _connectionString = await _sqlContainer.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _sqlContainer.DisposeAsync();
        await base.DisposeAsync();
    }
}
'''

AUTH_HANDLER = '''using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Eventy.Testing.Foundation.Web;

public class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

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
'''

EVENT_BUILDER = '''using Eventy.Domain.Entities;

namespace Eventy.Testing.Foundation.Builders;

public class EventBuilder
{
    private string _name = "Test Event";
    private int _capacity = 100;
    private DateTime _date = DateTime.UtcNow.AddDays(30);

    public EventBuilder WithName(string name) { _name = name; return this; }
    public EventBuilder WithCapacity(int capacity) { _capacity = capacity; return this; }
    public EventBuilder WithDate(DateTime date) { _date = date; return this; }

    public Event Build() => Event.Create(_name, _capacity, _date);
}
'''

BOOKING_TESTS_STUB = '''using FluentAssertions;
using Xunit;

namespace Eventy.Domain.UnitTests.Aggregates;

public class BookingTests
{
    [Fact]
    public void Create_WithValidData_ShouldSucceed()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();

        // Act
        var booking = Booking.Create(userId, ticketId, quantity: 2);

        // Assert
        booking.Should().NotBeNull();
        booking.UserId.Should().Be(userId);
        booking.Status.Should().Be(BookingStatus.Pending);
    }

    [Fact]
    public void Confirm_WhenPending_ShouldSucceed()
    {
        // Arrange
        var booking = Booking.Create(Guid.NewGuid(), Guid.NewGuid(), quantity: 1);

        // Act
        var result = booking.Confirm();

        // Assert
        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Confirmed);
    }
}
'''

TICKET_TESTS_STUB = '''using FluentAssertions;
using Xunit;

namespace Eventy.Domain.UnitTests.Aggregates;

public class TicketTypeTests
{
    [Fact]
    public void Reserve_WhenCapacityExceeded_ShouldReturnFailure()
    {
        // Arrange
        var ticket = TicketType.Create("VIP", capacity: 100);
        ticket.Reserve(50);

        // Act
        var result = ticket.Reserve(51);

        // Assert
        result.IsFailure.Should().BeTrue();
    }
}
'''

VALUE_OBJECT_STUB = '''using FluentAssertions;
using Xunit;

namespace Eventy.Domain.UnitTests.ValueObjects;

public class EmailTests
{
    [Theory]
    [InlineData("valid@example.com", true)]
    [InlineData("invalid-email", false)]
    public void Create_ShouldValidateFormat(string input, bool shouldSucceed)
    {
        var result = Email.Create(input);
        result.IsSuccess.Should().Be(shouldSucceed);
    }
}
'''

HANDLER_TESTS_STUB = '''using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Eventy.Application.UnitTests.Features.Bookings.Commands;

public class CreateBookingHandlerTests
{
    private readonly IBookingRepository _bookingRepo = Substitute.For<IBookingRepository>();
    private readonly ITicketTypeRepository _ticketRepo = Substitute.For<ITicketTypeRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CreateBookingHandler _handler;

    public CreateBookingHandlerTests()
    {
        _handler = new CreateBookingHandler(_bookingRepo, _ticketRepo, _unitOfWork);
    }

    [Fact]
    public async Task Handle_WhenTicketAvailable_ShouldCreateBooking()
    {
        // Arrange
        var ticket = TicketType.Create("VIP", capacity: 10);
        var command = new CreateBookingCommand(ticket.Id, Quantity: 2);
        _ticketRepo.GetAsync(ticket.Id).Returns(ticket);
        _unitOfWork.SaveChangesAsync().Returns(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _bookingRepo.Received(1).AddAsync(Arg.Any<Booking>());
    }
}
'''

BEHAVIOR_TESTS_STUB = '''using FluentAssertions;
using Xunit;

namespace Eventy.Application.UnitTests.Behaviors;

public class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_WhenCommandInvalid_ShouldReturnValidationFailure()
    {
        // Arrange
        var command = new CreateBookingCommand(); // Invalid — missing fields
        var validator = new CreateBookingValidator();
        var behavior = new ValidationBehavior<CreateBookingCommand, Result>(
            new[] { validator });

        // Act
        var result = await behavior.Handle(
            command,
            () => throw new Exception("Should not reach handler"),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
    }
}
'''

INTEGRATION_FIXTURE = '''using Eventy.Testing.Foundation.Web;
using Xunit;

namespace Eventy.IntegrationTests.Fixtures;

public class IntegrationTestFixture : IAsyncLifetime
{
    public EventyWebApplicationFactory Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Factory = new EventyWebApplicationFactory();
        Client = Factory.CreateClient();
    }

    public async Task DisposeAsync() => await Factory.DisposeAsync();
}

[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<IntegrationTestFixture> { }
'''

INTEGRATION_TEST_STUB = '''using Eventy.IntegrationTests.Fixtures;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Eventy.IntegrationTests.Features.Bookings;

[Collection("Integration")]
public class CreateBookingTests
{
    private readonly HttpClient _client;

    public CreateBookingTests(IntegrationTestFixture fixture)
    {
        _client = fixture.Client;
    }

    [Fact]
    public async Task CreateBooking_WithValidRequest_ShouldReturn201()
    {
        // Arrange
        var request = new CreateBookingRequest(Guid.NewGuid(), Quantity: 2);

        // Act
        var response = await _client.PostAsJsonAsync("/api/bookings", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
'''

DB_ASSERTIONS_STUB = '''using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Eventy.IntegrationTests.Assertions;

public static class DatabaseAssertions
{
    public static async Task ShouldHaveBookingAsync(
        this ApplicationDbContext db, Guid ticketId, int expectedCount = 1)
    {
        var count = await db.Bookings.CountAsync(b => b.TicketTypeId == ticketId);
        count.Should().Be(expectedCount);
    }

    public static async Task ShouldHaveOutboxMessageAsync(
        this ApplicationDbContext db, string eventType)
    {
        var exists = await db.OutboxMessages.AnyAsync(o => o.Type == eventType);
        exists.Should().BeTrue();
    }
}
'''

CONCURRENT_EXECUTOR = '''using System.Collections.Concurrent;

namespace Eventy.ConcurrencyTests.Engine;

public class ConcurrentExecutor
{
    public async Task<ConcurrentResult<TResult>> ExecuteAsync<TResult>(
        int workerCount,
        Func<Task<TResult>> action)
    {
        var countdown = new CountdownEvent(1);
        var barrier = new CountdownEvent(workerCount);
        var results = new ConcurrentBag<TResult>();

        var tasks = Enumerable.Range(0, workerCount).Select(async _ =>
        {
            barrier.Signal();
            countdown.Wait();
            var result = await action();
            results.Add(result);
        });

        barrier.Wait();
        countdown.Signal();
        await Task.WhenAll(tasks);

        return new ConcurrentResult<TResult>(results);
    }
}

public record ConcurrentResult<TResult>(IEnumerable<TResult> Results)
{
    public int SuccessCount => Results.Count(r =>
        r is HttpResponseMessage msg && msg.IsSuccessStatusCode);
}
'''

LAST_TICKET_TEST = '''using Eventy.ConcurrencyTests.Engine;
using Eventy.ConcurrencyTests.Fixtures;
using FluentAssertions;
using System.Net.Http.Json;
using Xunit;

namespace Eventy.ConcurrencyTests.Scenarios;

[Collection("Concurrency")]
public class LastTicketRaceTests
{
    private readonly ConcurrencyTestFixture _fixture;
    private readonly HttpClient _client;

    public LastTicketRaceTests(ConcurrencyTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task LastTicket_With100Users_ShouldAllowExactlyOneBooking()
    {
        // Arrange
        var ticket = await _fixture.CreateSingleTicketAsync();
        var executor = new ConcurrentExecutor();

        // Act
        var result = await executor.ExecuteAsync(100, async () =>
        {
            return await _client.PostAsJsonAsync("/api/bookings",
                new CreateBookingRequest(ticket.Id, Quantity: 1));
        });

        // Assert HTTP
        result.SuccessCount.Should().Be(1);

        // Assert Database
        await using var db = _fixture.CreateDbContext();
        var bookingCount = await db.Bookings
            .CountAsync(b => b.TicketTypeId == ticket.Id);
        bookingCount.Should().Be(1);
    }
}
'''

CONCURRENCY_FIXTURE = '''using Eventy.Testing.Foundation.Web;
using Xunit;

namespace Eventy.ConcurrencyTests.Fixtures;

public class ConcurrencyTestFixture : IAsyncLifetime
{
    public EventyWebApplicationFactory Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Factory = new EventyWebApplicationFactory();
        Client = Factory.CreateClient();
    }

    public async Task DisposeAsync() => await Factory.DisposeAsync();

    public async Task<TicketType> CreateSingleTicketAsync()
    {
        // TODO: Implement seeding logic
        throw new NotImplementedException();
    }

    public ApplicationDbContext CreateDbContext()
    {
        var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }
}

[CollectionDefinition("Concurrency")]
public class ConcurrencyCollection : ICollectionFixture<ConcurrencyTestFixture> { }
'''


def generate_csproj(project_name, config):
    """Generate a .csproj file for a test project."""
    is_test = config["is_test_project"]
    packages = config["packages"]

    package_refs = "\n".join(
        f'    <PackageReference Include="{name}" Version="{version}" />'
        for name, version in packages
    )

    project_refs = ""
    if project_name == "Eventy.Testing.Foundation":
        project_refs = '''
    <ItemGroup>
      <ProjectReference Include="..\..\src\Eventy.WebApi\Eventy.WebApi.csproj" />
    </ItemGroup>'''
    elif project_name == "Eventy.Domain.UnitTests":
        project_refs = '''
    <ItemGroup>
      <ProjectReference Include="..\..\src\Eventy.Domain\Eventy.Domain.csproj" />
    </ItemGroup>'''
    elif project_name == "Eventy.Application.UnitTests":
        project_refs = '''
    <ItemGroup>
      <ProjectReference Include="..\..\src\Eventy.Domain\Eventy.Domain.csproj" />
      <ProjectReference Include="..\..\src\Eventy.Application\Eventy.Application.csproj" />
    </ItemGroup>'''
    elif project_name in ("Eventy.IntegrationTests", "Eventy.ConcurrencyTests"):
        project_refs = '''
    <ItemGroup>
      <ProjectReference Include="..\Eventy.Testing.Foundation\Eventy.Testing.Foundation.csproj" />
    </ItemGroup>'''

    sdk = "Microsoft.NET.Sdk.Web" if project_name in ("Eventy.IntegrationTests", "Eventy.ConcurrencyTests") else "Microsoft.NET.Sdk"

    content = f'''<Project Sdk="{sdk}">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>{'<IsTestProject>true</IsTestProject>' if is_test else ''}
  </PropertyGroup>

  <ItemGroup>
{package_refs}
  </ItemGroup>
{project_refs}

</Project>
'''
    return content


def create_project(solution_root, project_name, config):
    """Create a test project with folders and files."""
    project_path = solution_root / config["folder"]
    project_path.mkdir(parents=True, exist_ok=True)

    # Write .csproj
    csproj_content = generate_csproj(project_name, config)
    csproj_path = project_path / f"{project_name}.csproj"
    with open(csproj_path, "w") as f:
        f.write(csproj_content)

    # Create folders
    for folder in config["folders"]:
        (project_path / folder).mkdir(parents=True, exist_ok=True)

    # Create files with content
    for relative_path, content in config["files"]:
        file_path = project_path / relative_path
        file_path.parent.mkdir(parents=True, exist_ok=True)
        with open(file_path, "w") as f:
            f.write(content)

    print(f"  Created: {project_name}")


def scaffold(solution_root: Path):
    """Scaffold the complete test solution."""
    tests_root = solution_root / "tests"
    tests_root.mkdir(parents=True, exist_ok=True)

    print(f"Scaffolding Eventy test solution at: {solution_root}")
    print(f"Test projects root: {tests_root}")
    print()

    for project_name, config in TEST_PROJECTS.items():
        create_project(solution_root, project_name, config)

    # Create solution-level files
    # xUnit runner configuration
    xunit_config = tests_root / "xunit.runner.json"
    with open(xunit_config, "w") as f:
        f.write('''{\n  "parallelizeTestCollections": true,\n  "maxParallelThreads": 4\n}\n''')

    # .runsettings for coverage
    runsettings = tests_root / "Eventy.Tests.runsettings"
    with open(runsettings, "w") as f:
        f.write('''<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat code coverage">
        <Configuration>
          <Format>cobertura</Format>
          <Exclude>[*.Tests]*</Exclude>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
''')

    print()
    print("=" * 60)
    print("Scaffolding complete!")
    print()
    print("Next steps:")
    print("  1. Update project references in .csproj files to match your solution")
    print("  2. Implement the TODO stubs (CreateSingleTicketAsync, etc.)")
    print("  3. Add your domain entities to the builder classes")
    print("  4. Run 'dotnet build' to verify the structure")
    print("  5. Start with Domain unit tests, then Integration, then Concurrency")
    print()
    print("Generated structure:")
    for project_name, config in TEST_PROJECTS.items():
        print(f"  tests/{config['folder']}/")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(
        description="Scaffold Eventy test solution structure"
    )
    parser.add_argument(
        "solution_root",
        help="Path to the Eventy solution root directory"
    )
    args = parser.parse_args()

    root = Path(args.solution_root).resolve()
    if not root.exists():
        print(f"Error: Directory does not exist: {root}")
        sys.exit(1)

    scaffold(root)
