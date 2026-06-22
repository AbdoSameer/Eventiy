using Domain.Common;
using FluentValidation;
using MediatR;

namespace EventManagementSystem.Application
    .Abstractions.Behaviors;

public sealed class ValidationPipelineBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationPipelineBehavior(
        IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context =
            new ValidationContext<TRequest>(request);

        var validationResults =
            await Task.WhenAll(
                _validators.Select(v =>
                    v.ValidateAsync(
                        context,
                        cancellationToken)));

        var failures =
            validationResults
                .SelectMany(x => x.Errors)
                .Where(x => x is not null)
                .ToList();

        if (!failures.Any())
        {
            return await next();
        }

        var errors =
            failures
                .Select(x =>
                    $"{x.PropertyName}: {x.ErrorMessage}")
                .Distinct()
                .ToArray();

        return CreateFailureResult(errors);
    }

    private static TResponse CreateFailureResult(
        string[] errors)
    {
        if (typeof(TResponse) == typeof(Result))
        {
            return (TResponse)(object)
                Result.Failure(errors);
        }

        if (typeof(TResponse).IsGenericType &&
            typeof(TResponse).GetGenericTypeDefinition()
                == typeof(Result<>))
        {
            var valueType =
                typeof(TResponse)
                    .GetGenericArguments()[0];

            var resultType =
                typeof(Result<>)
                    .MakeGenericType(valueType);

            var failureMethod =
                resultType.GetMethod(
                    nameof(Result<object>.Failure),
                    new[] { typeof(string[]) });

            return (TResponse)failureMethod!
                .Invoke(null, new object[] { errors })!;
        }

        throw new InvalidOperationException(
            $"Unsupported response type {typeof(TResponse).Name}");
    }
}