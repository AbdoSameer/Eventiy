namespace Domain.Common;

public interface IValidationResult<TSelf>
    where TSelf : IValidationResult<TSelf>
{
    static abstract TSelf CreateFailure(Error[] errors);
}
