using Domain.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Abstractions.Behaviors;

public sealed class ConcurrencyRetryPipelineBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IValidationResult<TResponse>
{
    private static readonly AsyncLocal<int> _retryDepth = new();

    private const int MaxRetries = 3;
    private const int BaseDelayMs = 100;

    private static readonly Error ConcurrencyError = Error.Conflict(
        "Concurrency.RetryExhausted",
        "The operation could not be completed after multiple retries due to concurrent access conflicts.");

    private readonly IServiceScopeFactory _scopeFactory;

    public ConcurrencyRetryPipelineBehavior(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        _retryDepth.Value++;
        try
        {
            if (_retryDepth.Value > 1)
                return await next();

            for (var attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    if (attempt > 1)
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                        return await mediator.Send(request, cancellationToken);
                    }

                    return await next();
                }
                catch (ConcurrencyException) when (attempt < MaxRetries)
                {
                    var delayMs = BaseDelayMs * (int)Math.Pow(2, attempt - 1);
                    await Task.Delay(delayMs, cancellationToken);
                }
            }

            return TResponse.CreateFailure([ConcurrencyError]);
        }
        finally
        {
            _retryDepth.Value--;
        }
    }
}
