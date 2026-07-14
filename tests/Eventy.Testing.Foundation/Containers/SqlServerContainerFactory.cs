using Testcontainers.MsSql;

namespace Eventy.Testing.Foundation.Containers;

/// <summary>
/// Manages a SQL Server Testcontainer lifecycle.
/// One instance per test assembly — shared via xUnit collection fixtures.
/// </summary>
public sealed class SqlServerContainerFactory : IAsyncDisposable
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
