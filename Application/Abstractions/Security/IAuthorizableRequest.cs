namespace Application.Abstractions.Security;

public interface IAuthorizableRequest
{
    string[] RequiredRoles { get; }
}
