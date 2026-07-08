using Domain.Common;
using FluentValidation;
using MediatR;
using System.Collections.Concurrent;
using System.Reflection;

namespace Application.Abstractions.Behaviors;

public sealed class ValidationPipelineBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    private static readonly ConcurrentDictionary<Type, Func<Error[], object>> FactoryCache = new();

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

        var factory = FactoryCache.GetOrAdd(typeof(TResponse), static t =>
        {
            var valueType = t.GetGenericArguments()[0];
            var method = typeof(Result<>)
                .MakeGenericType(valueType)
                .GetMethod(nameof(Result<object>.Failure), BindingFlags.Static | BindingFlags.Public, [typeof(Error[])])!;
            return e => method.Invoke(null, [e])!;
        });

        return (TResponse)factory(errors);
    }
}