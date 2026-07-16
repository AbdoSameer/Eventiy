using Domain.Common;
using Domain.Errors;

namespace Application.Abstractions.Persistence;

public static class ConcurrencyRetryHelper
{
    private const int MaxRetries = 3;

    public static async Task<Result> ExecuteWithConcurrencyRetryAsync(
        Func<Task<Result>> operation,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (ConcurrencyException) when (attempt < MaxRetries)
            {
            }
        }

        return Result.Failure(BookingErrors.ConcurrencyConflict());
    }

    public static async Task<Result<T>> ExecuteWithConcurrencyRetryAsync<T>(
        Func<Task<Result<T>>> operation,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (ConcurrencyException) when (attempt < MaxRetries)
            {
            }
        }

        return Result<T>.Failure(BookingErrors.ConcurrencyConflict());
    }
}
