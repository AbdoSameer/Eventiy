using Application.Abstractions.Messaging;
using Domain.Common;
using FluentValidation;
using MediatR;

namespace EventManagementSystem.Application.Abstractions.Behaviors;

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
        // If no validators, skip validation
        if (!_validators.Any())
        {
            return await next();
        }

        // Validate the request
        var context = new ValidationContext<TRequest>(request);
        var validationResults = await Task.WhenAll(
            _validators.Select(v =>
                v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(x => x.Errors)
            .Where(x => x is not null)
            .ToList();

        // If validation passes, continue
        if (!failures.Any())
        {
            return await next();
        }

        // Convert validation failures to Error array
        var errors = failures
            .Select(f => Error.Validation(
                f.PropertyName,
                f.ErrorMessage))
            .Distinct()
            .ToArray();

        return CreateValidationFailure(errors);
    }

    private static TResponse CreateValidationFailure(Error[] errors)
    {
        // Check if TResponse is a non-generic Result (ICommand)
        if (typeof(TResponse) == typeof(Result))
        {
            return (TResponse)(object)Result.Failure(errors);
        }

        // Check if TResponse is a generic Result<T> (ICommand<T> or IQuery<T>)
        if (typeof(TResponse).IsGenericType &&
            typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
        {
            try
            {
                // Get the generic type argument (T)
                var valueType = typeof(TResponse).GetGenericArguments()[0];

                // Use the static Failure method from Result<T>
                var resultType = typeof(Result<>).MakeGenericType(valueType);
                var failureMethod = resultType.GetMethod(
                    nameof(Result<object>.Failure),
                    new[] { typeof(Error[]) });

                if (failureMethod != null)
                {
                    var result = failureMethod.Invoke(null, new object[] { errors });
                    return (TResponse)result!;
                }

                // Alternative: Use the constructor
                var constructor = resultType.GetConstructor(
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance,
                    null,
                    new[] { typeof(bool), typeof(object), typeof(IReadOnlyList<Error>) },
                    null);

                if (constructor != null)
                {
                    var result = constructor.Invoke(new object[] { false, null, errors });
                    return (TResponse)result!;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Error creating validation failure result: {ex.Message}",
                    ex);
            }
        }

        throw new InvalidOperationException(
            $"Unsupported response type {typeof(TResponse).Name}. " +
            $"The type must be Result (for ICommand) or Result<T> (for ICommand<T> or IQuery<T>).");
    }
}