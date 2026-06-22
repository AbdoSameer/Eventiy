namespace Domain.Common;

public class Result
{
    protected Result(
        bool isSuccess,
        IReadOnlyList<string> errors)
    {
        if (isSuccess && errors.Any())
            throw new ArgumentException(
                "Successful result cannot contain errors.");

        if (!isSuccess && !errors.Any())
            throw new ArgumentException(
                "Failed result must contain errors.");

        IsSuccess = isSuccess;
        Errors = errors;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public IReadOnlyList<string> Errors { get; }

    public static Result Success()
        => new(true, []);

    public static Result Failure(
        params string[] errors)
        => new(false, errors);
}

public class Result<T> : Result
{
    private readonly T? _value;

    private Result(
        bool isSuccess,
        T? value,
        IReadOnlyList<string> errors)
        : base(isSuccess, errors)
    {
        _value = value;
    }

    public T Value =>
        IsSuccess
            ? _value!
            : throw new InvalidOperationException(
                "Cannot access value of failed result.");

    public static Result<T> Success(T value)
        => new(true, value, []);

    public new static Result<T> Failure(
        params string[] errors)
        => new(false, default, errors);
}