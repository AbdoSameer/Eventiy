using Domain.Common;
using Domain.Errors;

namespace Application.Abstractions.Persistence;

public static class ConcurrencyRetryHelper
{
    private const int MaxRetries = 3;
    private const int BaseDelayMs = 100;

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
            catch (ConcurrencyException)
            {
                if (attempt < MaxRetries)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return Result.Failure(BookingErrors.ConcurrencyConflict());

                    var delayMs = BaseDelayMs * (int)Math.Pow(2, attempt - 1);
                    await Task.Delay(delayMs, cancellationToken);
                }
                else
                {
                    return Result.Failure(BookingErrors.ConcurrencyConflict());
                }
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
            catch (ConcurrencyException)
            {
                if (attempt < MaxRetries)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return Result<T>.Failure(BookingErrors.ConcurrencyConflict());

                    var delayMs = BaseDelayMs * (int)Math.Pow(2, attempt - 1);
                    await Task.Delay(delayMs, cancellationToken);
                }
                else
                {
                    return Result<T>.Failure(BookingErrors.ConcurrencyConflict());
                }
            }
        }

        return Result<T>.Failure(BookingErrors.ConcurrencyConflict());
    }
}
