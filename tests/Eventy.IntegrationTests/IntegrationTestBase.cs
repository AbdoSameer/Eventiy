using Eventy.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Eventy.IntegrationTests;

/// <summary>
/// Base class for all integration tests — enforces the three
/// Testing Integrity Standard rules:
///
/// 1. Rule of One: Provides a <see cref="TestDataBuilder"/> that
///    creates all test data through the HTTP API (handler-driven),
///    never via direct DB insertion.
///
/// 2. Clean Up Hook: Resets the database before each test via
///    <see cref="IntegrationTestFixture.ResetDatabaseAsync"/>. Each
///    test starts with a clean slate — no data leakage.
///
/// 3. Logging the State: Provides a <see cref="TestStateLogger"/>
///    that logs Before/After states to the xUnit test output for
///    debugging flaky tests.
/// </summary>
[Collection("Integration")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly IntegrationTestFixture Fixture;
    protected readonly HttpClient Client;
    protected readonly TestDataBuilder Data;
    protected readonly TestStateLogger State;

    private readonly ITestOutputHelper _output;

    protected IntegrationTestBase(IntegrationTestFixture fixture, ITestOutputHelper output)
    {
        Fixture = fixture;
        Client = fixture.Client;
        _output = output;
        Data = new TestDataBuilder(fixture);
        State = new TestStateLogger(output, fixture);
    }

    /// <summary>
    /// Rule 2: Clean Up Hook — resets the database before each test.
    /// Override to add custom setup, but always call base.InitializeAsync().
    /// </summary>
    public virtual async Task InitializeAsync()
    {
        await Fixture.ResetDatabaseAsync();
        State.LogTestStart(GetType().Name);
    }

    public virtual Task DisposeAsync()
    {
        State.LogTestEnd(GetType().Name);
        return Task.CompletedTask;
    }
}
