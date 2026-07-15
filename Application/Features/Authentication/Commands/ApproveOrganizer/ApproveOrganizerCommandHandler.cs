using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.UserAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;

namespace Application.Features.Authentication.Commands.ApproveOrganizer;

internal sealed class ApproveOrganizerCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<ApproveOrganizerCommand>
{
    public async Task<Result> Handle(
        ApproveOrganizerCommand request,
        CancellationToken cancellationToken)
    {
        var userIdResult = UserId.Create(request.UserId);
        if (userIdResult.IsFailure)
            return Result.Failure(userIdResult.Errors.ToArray());

        var user = await userRepository.GetByIdAsync(userIdResult.Value, cancellationToken);
        if (user is null)
            return Result.Failure(UserErrors.NotFound());

        if (user.Role != Role.Organizer)
            return Result.Failure(UserErrors.NotOrganizer());

        if (user.IsApproved)
            return Result.Success();

        user.Approve();

        var rows = await unitOfWork.CommitAsync(cancellationToken);
        if (rows <= 0)
            return Result.Failure(Error.Failure("User.ApprovalFailed", "Failed to approve organizer account."));

        return Result.Success();
    }
}
