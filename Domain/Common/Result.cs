namespace Domain.Common;
public class Result
{
    protected Result(bool isSuccess, IReadOnlyList<Error> errors)
    {
        if (isSuccess && errors.Any(e => e != Error.None))
            throw new ArgumentException("A successful result cannot contain errors.", nameof(errors));

        if (!isSuccess && !errors.Any())
            throw new ArgumentException("A failed result must contain at least one error.", nameof(errors));

        IsSuccess = isSuccess;
        Errors = errors;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public IReadOnlyList<Error> Errors { get; }

    public static Result Success() => new(true, [Error.None]);

    public static Result Failure(params Error[] errors) => new(false, errors);
}

public class Result<TValue> : Result
{
    private readonly TValue? _value;

    protected internal Result(bool isSuccess, TValue? value, IReadOnlyList<Error> errors)
        : base(isSuccess, errors)
    {
        _value = value;
    }

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access the value of a failed result.");

    public static implicit operator Result<TValue>(TValue? value) =>
        value is not null ? Success(value) : Failure(Error.NullValue);

    public static Result<TValue> Success(TValue value) => new(true, value, [Error.None]);

    public new static Result<TValue> Failure(params Error[] errors) => new(false, default, errors);
}