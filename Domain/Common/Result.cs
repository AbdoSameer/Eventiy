
namespace Domain.Common;

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string Error { get; }

    protected Result(
        bool isSuccess,
        string error)
    {       
        if (isSuccess && !string.IsNullOrEmpty(error))
            throw new ArgumentException("A successful result cannot contain an error.", nameof(error));

        if (!isSuccess && string.IsNullOrEmpty(error))
            throw new ArgumentException("A failed result must contain an error.", nameof(error));

        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success()
        => new(true, string.Empty);

    public static Result Failure(string error)
        => new(false, error);

}

public class Result<T> : Result
{
    private readonly T? _value;

    private Result(bool isSuccess, T? value, string error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    public T Value => IsSuccess ? _value!
        : throw new InvalidOperationException($"Cannot access the value of a failed result. Error: {Error}");

    public static Result<T> Success(T value)
        => new(true, value, string.Empty);

    public static new Result<T> Failure(string error)
        => new(false, default, error);
}