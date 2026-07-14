using Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Respawn;

namespace Eventy.Testing.Foundation.Database;

/// <summary>
/// Resets the database to a clean state between tests using Respawn.
/// Preserves table structure (migrations) but wipes all data rows.
/// </summary>
public sealed class DatabaseResetService : IAsyncDisposable
{
    private readonly string _connectionString;
    private Respawner? _respawner;

    public DatabaseResetService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task InitializeAsync()
    {
        _respawner = await Respawner.CreateAsync(_connectionString, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            SchemasToInclude = new[] { "dbo" },
        });
    }

    public async Task ResetAsync()
    {
        if (_respawner is null)
            throw new InvalidOperationException("Call InitializeAsync first.");

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
    }

    public async ValueTask DisposeAsync()
    {
        // Respawner has no disposal — connection is managed by caller
        await ValueTask.CompletedTask;
    }
}
