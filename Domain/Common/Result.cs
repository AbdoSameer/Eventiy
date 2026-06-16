
namespace Domain.Common
{
    public class Result
    {
        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public string Error { get; }

        protected Result(bool isSuccess, string error)
        {
            IsSuccess = isSuccess;
            Error = error;
        }

        public static Result Success() => new(true, string.Empty);
        public static Result Failure(string error) => new(false, error);
    }

    public class Result<T>: Result
    {
        public bool IsSuccess { get; private set; }
        public bool IsFailure => !IsSuccess;
        public T Value { get; private set; }
        public string Error { get; private set; }

        private Result(bool isSuccess, T value, string error) : base(isSuccess, error)
        {
            Value = value;
        }

        public static Result<T> 
            FromCondition(bool condition, T value, string error) =>
            condition ? Success(value) : Failure(error);
        
        public static Result<T> Success(T value)=> 
            new Result<T>(true, value,string.Empty);
        
        public static Result<T> Failure(string error) =>
            new Result<T>(false, default(T)!, error);


    }
}
