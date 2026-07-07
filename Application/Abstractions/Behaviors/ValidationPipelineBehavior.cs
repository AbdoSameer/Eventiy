using Application.Abstractions.Messaging;
using Domain.Common;
using FluentValidation;
using MediatR;

namespace Application.Abstractions.Behaviors;

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
            return await next();

        var context = new ValidationContext<TRequest>(request);
        var validationResults = await Task.WhenAll(
            _validators.Select(v =>
                v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(x => x.Errors)
            .Where(x => x is not null)
            .ToList();

        if (!failures.Any())
            return await next();

        var errors = failures
            .Select(f => Error.Validation(f.PropertyName, f.ErrorMessage))
            .Distinct()
            .ToArray();

        return CreateValidationFailure(errors);
    }

    private static TResponse CreateValidationFailure(Error[] errors)
    {
        if (typeof(TResponse) == typeof(Result))
            return (TResponse)(object)Result.Failure(errors);

        if (typeof(TResponse).IsGenericType &&
            typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
        {
            var valueType = typeof(TResponse).GetGenericArguments()[0];
            var result = typeof(ResultHelper)
                .GetMethod(nameof(ResultHelper.Failure))!
                .MakeGenericMethod(valueType)
                .Invoke(null, [errors])!;
            return (TResponse)result;
        }

        throw new InvalidOperationException(
            $"Unsupported response type {typeof(TResponse).Name}. " +
            $"The type must be Result (for ICommand) or Result<T> (for ICommand<T> or IQuery<T>).");
    }
}

internal static class ResultHelper
{
    public static Result<T> Failure<T>(Error[] errors) => Result<T>.Failure(errors);
}